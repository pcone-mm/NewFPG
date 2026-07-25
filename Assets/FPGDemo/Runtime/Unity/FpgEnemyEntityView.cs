using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Skills;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Formal entity view contract. It exposes explicit anchors and keeps all
    /// hit parts disabled until the encounter activation boundary.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public sealed class FpgEnemyEntityView : MonoBehaviour,
        IFpgFormalEnemyEntityBinder,
        IFpgFormalEnemyPresentationView
    {
        private const string AnimationCuePrefix = "animation.";
        private const int CueFlashFrameBudget = 2;

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
        private D0ActorSocketRegistry socketRegistry;
        [SerializeField]
        private Collider[] hitParts = Array.Empty<Collider>();

        [SerializeField]
        private HitPart[] hitPartKinds = Array.Empty<HitPart>();

        [SerializeField]
        private Color skillWarningTint =
            new Color(1f, 0.3f, 0.12f, 1f);

        [SerializeField]
        private Color skillCueTint =
            new Color(1f, 0.9f, 0.25f, 1f);

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

        [NonSerialized]
        private FpgSpineSkillAnimationEvaluator skillAnimationEvaluator;

        [NonSerialized]
        private SkillExecutionId activeSkillExecutionId =
            SkillExecutionId.Invalid;

        [NonSerialized]
        private FpgFormalEnemySkillSequenceFrame pendingSkillFrame;

        [NonSerialized]
        private bool hasPendingSkillFrame;

        [NonSerialized]
        private int activeSkillWarningCount;

        [NonSerialized]
        private int cueFlashFramesRemaining;

        [NonSerialized]
        private bool hasSkeletonBaseColor;

        [NonSerialized]
        private Color skeletonBaseColor = Color.white;

        [NonSerialized]
        private D0ActorSocketRegistry resolvedSocketRegistry;
        public Transform GameplayAnchor => gameplayAnchor == null ? transform : gameplayAnchor;
        public Transform ProjectileAnchor => projectileAnchor == null ? GameplayAnchor : projectileAnchor;
        public Transform WeakpointAnchor => weakpointAnchor == null ? GameplayAnchor : weakpointAnchor;
        public D0ActorSocketRegistry SocketRegistry => socketRegistry != null
            ? socketRegistry
            : resolvedSocketRegistry != null
                ? resolvedSocketRegistry
                : (resolvedSocketRegistry =
                    GetComponentInChildren<D0ActorSocketRegistry>(true));
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

        public bool TrySetSkillSequenceFrame(
            in FpgFormalEnemySkillSequenceFrame frame)
        {
            if (!presentationInitialized
                || !gameplayEnabled
                || frame.SpawnSequence != spawnSequence
                || frame.Definition == null
                || !OwnsAttack(frame.Definition)
                || !frame.CompiledSequence.IsValid
                || !frame.ExecutionId.IsValid)
            {
                return false;
            }

            pendingSkillFrame = frame;
            hasPendingSkillFrame = true;
            return true;
        }

        public bool TryPresentSkillCue(
            in FpgFormalEnemySkillCuePresentationEvent cueEvent)
        {
            if (!CanPresentSkillEvent(cueEvent.TimelineEvent)
                || !TryValidatePresentationSocket(
                    cueEvent.Resolved.SocketName))
            {
                return false;
            }

            string cueName = cueEvent.Resolved.CueName;
            if (cueName.StartsWith(
                    AnimationCuePrefix,
                    StringComparison.Ordinal))
            {
                string animationName = cueName.Substring(
                    AnimationCuePrefix.Length);
                if (!HasAnimation(animationName))
                {
                    return false;
                }

                try
                {
                    skeletonAnimation.AnimationState.SetAnimation(
                        MainAnimationTrack,
                        animationName,
                        false);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            cueFlashFramesRemaining = CueFlashFrameBudget;
            ApplySkillFeedbackColor();
            return true;
        }

        public bool TrySetSkillWarning(
            in FpgFormalEnemySkillWarningPresentationEvent warningEvent)
        {
            if (!CanPresentSkillEvent(warningEvent.TimelineEvent)
                || !TryValidatePresentationSocket(
                    warningEvent.Resolved.SocketName))
            {
                return false;
            }

            if (warningEvent.IsActive)
            {
                if (activeSkillWarningCount < int.MaxValue)
                {
                    activeSkillWarningCount++;
                }
            }
            else
            {
                activeSkillWarningCount = Math.Max(
                    0,
                    activeSkillWarningCount - 1);
            }

            ApplySkillFeedbackColor();
            return true;
        }

        public void ClearSkillWarnings()
        {
            activeSkillWarningCount = 0;
            cueFlashFramesRemaining = 0;
            ApplySkillFeedbackColor();
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
                    || !TryValidateAttackAnimations(data, attack))
                {
                    string attackId = attack == null
                        ? index.ToString()
                        : attack.SkillId;
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
            if (SocketRegistry == null)
            {
                error = "Formal enemy entity requires a D0ActorSocketRegistry.";
                return false;
            }
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

        private void LateUpdate()
        {
            EvaluatePendingSkillFrame();
            if (cueFlashFramesRemaining <= 0)
            {
                return;
            }

            cueFlashFramesRemaining--;
            if (cueFlashFramesRemaining == 0)
            {
                ApplySkillFeedbackColor();
            }
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

                CaptureSkeletonBaseColor();
                activeSkillWarningCount = 0;
                cueFlashFramesRemaining = 0;
                boundDefinition = definition;
                boundBehavior = definition.Behavior;
                skillAnimationEvaluator =
                    new FpgSpineSkillAnimationEvaluator(
                        skeletonAnimation);
                activeSkillExecutionId = SkillExecutionId.Invalid;
                pendingSkillFrame =
                    default(FpgFormalEnemySkillSequenceFrame);
                hasPendingSkillFrame = false;
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

        private void EvaluatePendingSkillFrame()
        {
            if (!hasPendingSkillFrame
                || !presentationInitialized
                || skillAnimationEvaluator == null)
            {
                return;
            }

            FpgFormalEnemySkillSequenceFrame frame =
                pendingSkillFrame;
            if (frame.State == FpgSkillExecutionState.Canceled)
            {
                ClearPendingSkillFrameAndReturnToIdle();
                return;
            }

            if (!FpgEnemySkillPresentationResolver
                    .TryResolveAnimationName(
                        frame.Definition,
                        frame.CompiledSequence.Kind,
                        frame.ResolvedAnimationId,
                        out string animationName))
            {
                ClearPendingSkillFrameAndReturnToIdle();
                return;
            }

            if (activeSkillExecutionId != frame.ExecutionId)
            {
                skillAnimationEvaluator.Reset();
                activeSkillExecutionId = frame.ExecutionId;
            }

            double interpolation = frame.IsTerminal
                ? 0d
                : FpgFormalPlayerSkillAnimationClock
                    .ResolveInterpolation(
                        Time.timeAsDouble,
                        Time.fixedTimeAsDouble,
                        Time.fixedDeltaTime);
            if (!skillAnimationEvaluator.TryEvaluate(
                    animationName,
                    frame.CompiledSequence,
                    frame.RelativeTick,
                    interpolation,
                    out _))
            {
                ClearPendingSkillFrameAndReturnToIdle();
                return;
            }

            if (frame.State == FpgSkillExecutionState.Completed)
            {
                ClearPendingSkillFrameAndReturnToIdle();
            }
        }

        private void ClearPendingSkillFrameAndReturnToIdle()
        {
            pendingSkillFrame =
                default(FpgFormalEnemySkillSequenceFrame);
            hasPendingSkillFrame = false;
            activeSkillExecutionId = SkillExecutionId.Invalid;
            skillAnimationEvaluator?.Reset();
            if (presentationInitialized
                && boundBehavior != null
                && HasAnimation(boundBehavior.IdleAnimation))
            {
                TryPlayLoop(boundBehavior.IdleAnimation);
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
            ClearSkillWarnings();
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

            skillAnimationEvaluator?.Reset();
            skillAnimationEvaluator = null;
            activeSkillExecutionId = SkillExecutionId.Invalid;
            pendingSkillFrame =
                default(FpgFormalEnemySkillSequenceFrame);
            hasPendingSkillFrame = false;
            presentationInitialized = false;
            boundDefinition = null;
            boundBehavior = null;
        }

        private bool CanPresentSkillEvent(
            in FpgFormalEnemySkillTimelineEvent timelineEvent)
        {
            return presentationInitialized
                && gameplayEnabled
                && timelineEvent.OwnerRuntimeId.IsValid
                && string.Equals(
                    timelineEvent.OwnerRuntimeId.ToString(),
                    RuntimeId,
                    StringComparison.Ordinal)
                && timelineEvent.SpawnSequence == spawnSequence
                && timelineEvent.Definition != null
                && OwnsAttack(timelineEvent.Definition);
        }

        private bool TryValidatePresentationSocket(
            string socketName)
        {
            if (string.IsNullOrEmpty(socketName))
            {
                return true;
            }

            D0ActorSocketRegistry registry = SocketRegistry;
            return registry != null
                && registry.TryResolve(socketName, out _);
        }

        private void CaptureSkeletonBaseColor()
        {
            Spine.Skeleton skeleton = skeletonAnimation == null
                ? null
                : skeletonAnimation.Skeleton;
            if (skeleton == null)
            {
                hasSkeletonBaseColor = false;
                skeletonBaseColor = Color.white;
                return;
            }

            skeletonBaseColor = new Color(
                skeleton.R,
                skeleton.G,
                skeleton.B,
                skeleton.A);
            hasSkeletonBaseColor = true;
        }

        private void ApplySkillFeedbackColor()
        {
            Spine.Skeleton skeleton = skeletonAnimation == null
                ? null
                : skeletonAnimation.Skeleton;
            if (skeleton == null || !hasSkeletonBaseColor)
            {
                return;
            }

            Color color = skeletonBaseColor;
            if (activeSkillWarningCount > 0)
            {
                color.r *= skillWarningTint.r;
                color.g *= skillWarningTint.g;
                color.b *= skillWarningTint.b;
            }

            if (cueFlashFramesRemaining > 0)
            {
                color = Color.Lerp(color, skillCueTint, 0.7f);
                color.a = skeletonBaseColor.a;
            }

            skeleton.R = color.r;
            skeleton.G = color.g;
            skeleton.B = color.b;
            skeleton.A = color.a;
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

        private static bool TryValidateAttackAnimations(
            SkeletonData data,
            FpgEnemyAttackDefinition attack)
        {
            if (data == null || attack == null)
            {
                return false;
            }

            IReadOnlyList<FpgSkillSequenceDefinition> sequences =
                attack.Sequences;
            for (int index = 0; index < sequences.Count; index++)
            {
                FpgSkillSequenceDefinition sequence = sequences[index];
                if (sequence == null
                    || sequence.Kind != FpgSkillSequenceKind.Execute)
                {
                    continue;
                }

                if (!HasAnimation(data, sequence.MainAnimation))
                {
                    return false;
                }

                for (int variantIndex = 0;
                    variantIndex < sequence.AlternateAnimations.Count;
                    variantIndex++)
                {
                    if (!HasAnimation(
                            data,
                            sequence.AlternateAnimations[variantIndex]))
                    {
                        return false;
                    }
                }

                return true;
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
