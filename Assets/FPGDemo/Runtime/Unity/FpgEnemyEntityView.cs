using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Formal entity view contract. It exposes explicit anchors and keeps all
    /// hit parts disabled until the encounter activation boundary.
    /// </summary>
    public sealed class FpgEnemyEntityView : MonoBehaviour,
        IFpgFormalEnemyEntityBinder,
        IFpgFormalEnemyPresentationView
    {
        private const int MainAnimationTrack = 0;

        [SerializeField]
        private Transform gameplayAnchor;

        [SerializeField]
        private Transform projectileAnchor;

        [SerializeField]
        private Transform weakpointAnchor;

        [SerializeField]
        private Transform overheadHealthBarAnchor;

        [SerializeField]
        private Collider[] hitParts = Array.Empty<Collider>();

        [SerializeField]
        private HitPart[] hitPartKinds = Array.Empty<HitPart>();

        [SerializeField]
        private SkeletonAnimation skeletonAnimation;

        [NonSerialized]
        private string runtimeId;

        [NonSerialized]
        private int spawnSequence = -1;

        [NonSerialized]
        private bool gameplayEnabled;

        [NonSerialized]
        private FpgEnemyDefinition boundDefinition;

        [NonSerialized]
        private FpgEnemyBehaviorDefinition boundBehavior;

        [NonSerialized]
        private bool presentationInitialized;

        public Transform GameplayAnchor => gameplayAnchor == null ? transform : gameplayAnchor;
        public Transform ProjectileAnchor => projectileAnchor == null ? GameplayAnchor : projectileAnchor;
        public Transform WeakpointAnchor => weakpointAnchor == null ? GameplayAnchor : weakpointAnchor;
        public Transform OverheadHealthBarAnchor => overheadHealthBarAnchor == null
            ? GameplayAnchor
            : overheadHealthBarAnchor;
        public IReadOnlyList<Collider> HitParts => hitParts ?? Array.Empty<Collider>();
        public int HitPartCount => hitParts == null ? 0 : hitParts.Length;
        public string RuntimeId => runtimeId ?? string.Empty;
        public int SpawnSequence => spawnSequence;
        public bool GameplayEnabled => gameplayEnabled;
        public SkeletonAnimation SkeletonAnimation => skeletonAnimation;

        public bool TryGetHitPart(
            int hitPartOrdinal,
            out Collider collider,
            out HitPart hitPart)
        {
            Collider[] colliders = hitParts ?? Array.Empty<Collider>();
            if (hitPartOrdinal < 0 || hitPartOrdinal >= colliders.Length)
            {
                collider = null;
                hitPart = HitPart.Body;
                return false;
            }

            collider = colliders[hitPartOrdinal];
            HitPart[] kinds = hitPartKinds ?? Array.Empty<HitPart>();
            hitPart = kinds.Length == 0 ? HitPart.Body : kinds[hitPartOrdinal];
            return collider != null
                && Enum.IsDefined(typeof(HitPart), hitPart)
                && hitPart != HitPart.Projectile;
        }

        public void BindRuntime(string nextRuntimeId, int nextSpawnSequence)
        {
            if (string.IsNullOrWhiteSpace(nextRuntimeId))
            {
                throw new ArgumentException("Runtime id is required.", nameof(nextRuntimeId));
            }

            if (nextSpawnSequence < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nextSpawnSequence));
            }

            runtimeId = nextRuntimeId;
            spawnSequence = nextSpawnSequence;
        }

        public void ClearRuntimeBinding()
        {
            runtimeId = string.Empty;
            spawnSequence = -1;
            gameplayEnabled = false;
        }

        public bool TryBindFormalRuntime(
            RuntimeId nextRuntimeId,
            int nextSpawnSequence,
            FpgEnemyDefinition definition,
            out string error)
        {
            if (!nextRuntimeId.IsValid || definition == null || nextSpawnSequence < 0)
            {
                error = "Formal entity binding requires a valid runtime, sequence and definition.";
                return false;
            }

            if (!TryValidate(out error))
            {
                return false;
            }

            if (!TryValidatePresentation(definition, out error)
                || !TryInitializePresentation(definition, out error))
            {
                return false;
            }

            BindRuntime(nextRuntimeId.ToString(), nextSpawnSequence);
            boundDefinition = definition;
            boundBehavior = definition.Behavior;
            SetFormalGameplayEnabled(false);
            PlayEntry();
            error = string.Empty;
            return true;
        }

        public void SetFormalGameplayEnabled(bool enabled)
        {
            gameplayEnabled = enabled;
            Collider[] colliders = hitParts ?? Array.Empty<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = enabled;
                }
            }
        }

        public void UnbindFormalRuntime()
        {
            SetFormalGameplayEnabled(false);
            ResetPresentation();
            ClearRuntimeBinding();
        }

        public bool TryPlayAttack(FpgEnemyAttackDefinition attack)
        {
            if (!presentationInitialized
                || attack == null
                || !OwnsAttack(attack)
                || !HasAnimation(attack.AnimationSlot))
            {
                return false;
            }

            return TryPlayOneShotThenIdle(attack.AnimationSlot);
        }

        public bool TryInterruptAttack()
        {
            if (!presentationInitialized
                || boundBehavior == null
                || !HasAnimation(boundBehavior.IdleAnimation))
            {
                return false;
            }

            return TryPlayLoop(boundBehavior.IdleAnimation);
        }

        public bool TryValidatePresentation(
            FpgEnemyDefinition definition,
            out string error)
        {
            if (definition == null || definition.Behavior == null)
            {
                error =
                    "Formal enemy presentation requires a definition and behavior.";
                return false;
            }

            SkeletonData data = ResolveSkeletonData();
            if (skeletonAnimation == null || data == null)
            {
                error = $"Formal enemy '{definition.EnemyDefinitionId}' "
                    + "requires a loaded SkeletonAnimation.";
                return false;
            }

            FpgEnemyBehaviorDefinition behavior = definition.Behavior;
            if (!HasAnimation(data, behavior.EntryAnimation)
                || !HasAnimation(data, behavior.IdleAnimation)
                || !HasAnimation(data, behavior.DeathAnimation))
            {
                error = $"Formal enemy '{definition.EnemyDefinitionId}' "
                    + "behavior references a missing Spine animation.";
                return false;
            }

            for (int index = 0; index < definition.AttackPatternCount; index++)
            {
                FpgEnemyAttackDefinition attack =
                    definition.GetAttackPattern(index);
                if (attack == null
                    || !HasAnimation(data, attack.AnimationSlot))
                {
                    string attackId = attack == null
                        ? index.ToString()
                        : attack.AttackId;
                    error = $"Formal enemy '{definition.EnemyDefinitionId}' "
                        + $"attack '{attackId}' references a missing Spine animation.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            Collider[] colliders = hitParts ?? Array.Empty<Collider>();
            HitPart[] kinds = hitPartKinds ?? Array.Empty<HitPart>();
            if (colliders.Length == 0)
            {
                error = "Formal enemy entity requires at least one hit part.";
                return false;
            }

            if (kinds.Length != 0 && kinds.Length != colliders.Length)
            {
                error = "Formal enemy entity hit-part kinds must be empty or parallel the Collider array.";
                return false;
            }

            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] == null)
                {
                    error = $"Formal enemy entity hit part {index} is missing.";
                    return false;
                }

                HitPart kind = kinds.Length == 0 ? HitPart.Body : kinds[index];
                if (!Enum.IsDefined(typeof(HitPart), kind) || kind == HitPart.Projectile)
                {
                    error = $"Formal enemy entity hit part {index} has an invalid combatant kind.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public static long DeriveGeometryId(int spawnSequence, int hitPartOrdinal)
        {
            return FpgFormalGeometryId.Derive(spawnSequence, hitPartOrdinal);
        }

        public static GeometryId DeriveCombatGeometryId(int spawnSequence, int hitPartOrdinal)
        {
            return FpgFormalGeometryId.DeriveCombatGeometryId(spawnSequence, hitPartOrdinal);
        }

        private void Awake()
        {
            SetFormalGameplayEnabled(false);
        }

        private void OnDisable()
        {
            SetFormalGameplayEnabled(false);
            ResetPresentation();
        }

        private bool TryInitializePresentation(
            FpgEnemyDefinition definition,
            out string error)
        {
            try
            {
                skeletonAnimation.Initialize(false);
                if (skeletonAnimation.AnimationState == null)
                {
                    error =
                        "Formal enemy SkeletonAnimation has no animation state.";
                    return false;
                }

                boundDefinition = definition;
                boundBehavior = definition.Behavior;
                presentationInitialized = true;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "Formal enemy presentation initialization failed: "
                    + exception.Message;
                return false;
            }
        }

        private void PlayEntry()
        {
            if (!presentationInitialized || boundBehavior == null)
            {
                return;
            }

            if (string.Equals(
                    boundBehavior.EntryAnimation,
                    boundBehavior.IdleAnimation,
                    StringComparison.Ordinal))
            {
                TryPlayLoop(boundBehavior.IdleAnimation);
                return;
            }

            TryPlayOneShotThenIdle(boundBehavior.EntryAnimation);
        }

        private bool TryPlayOneShotThenIdle(string animationName)
        {
            if (!presentationInitialized
                || boundBehavior == null
                || skeletonAnimation == null
                || skeletonAnimation.AnimationState == null)
            {
                return false;
            }

            try
            {
                skeletonAnimation.AnimationState.SetAnimation(
                    MainAnimationTrack,
                    animationName,
                    false);
                skeletonAnimation.AnimationState.AddAnimation(
                    MainAnimationTrack,
                    boundBehavior.IdleAnimation,
                    true,
                    0f);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool TryPlayLoop(string animationName)
        {
            if (!presentationInitialized
                || skeletonAnimation == null
                || skeletonAnimation.AnimationState == null)
            {
                return false;
            }

            try
            {
                skeletonAnimation.AnimationState.SetAnimation(
                    MainAnimationTrack,
                    animationName,
                    true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ResetPresentation()
        {
            if (skeletonAnimation != null && presentationInitialized)
            {
                try
                {
                    skeletonAnimation.ClearState();
                    skeletonAnimation.Initialize(false);
                    if (skeletonAnimation.Skeleton != null)
                    {
                        skeletonAnimation.Skeleton.SetToSetupPose();
                        skeletonAnimation.Skeleton.UpdateWorldTransform();
                    }
                }
                catch (Exception)
                {
                    // Presentation teardown must not block pool release.
                }
            }

            presentationInitialized = false;
            boundDefinition = null;
            boundBehavior = null;
        }

        private bool OwnsAttack(FpgEnemyAttackDefinition attack)
        {
            if (boundDefinition == null)
            {
                return false;
            }

            for (int index = 0;
                 index < boundDefinition.AttackPatternCount;
                 index++)
            {
                if (boundDefinition.GetAttackPattern(index) == attack)
                {
                    return true;
                }
            }

            return false;
        }

        private SkeletonData ResolveSkeletonData()
        {
            return skeletonAnimation == null
                || skeletonAnimation.SkeletonDataAsset == null
                    ? null
                    : skeletonAnimation.SkeletonDataAsset
                        .GetSkeletonData(true);
        }

        private bool HasAnimation(string animationName)
        {
            return HasAnimation(ResolveSkeletonData(), animationName);
        }

        private static bool HasAnimation(
            SkeletonData data,
            string animationName)
        {
            return data != null
                && !string.IsNullOrWhiteSpace(animationName)
                && data.FindAnimation(animationName) != null;
        }
    }
}
