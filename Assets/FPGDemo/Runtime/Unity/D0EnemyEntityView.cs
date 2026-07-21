using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum D0EnemyHitboxFollowMode
    {
        AuthoredTransform = 0,
        SpineBone = 1
    }

    [Serializable]
    public struct D0EnemyHitboxFollowSettings
    {
        [SerializeField]
        private D0EnemyHitboxFollowMode followMode;

        [SerializeField]
        private string boneName;

        // Inverted so older prefab data defaults to following bone rotation
        // when an author first switches the mode from its zero-value default.
        [SerializeField]
        private bool keepAuthoredRotation;

        public D0EnemyHitboxFollowMode FollowMode => followMode;
        public string BoneName => boneName;
        public bool FollowBoneRotation => !keepAuthoredRotation;
    }

    [Serializable]
    public struct D0EnemyBodyHitboxBinding
    {
        [SerializeField]
        private Collider collider;

        [SerializeField]
        private int geometryId;

        [SerializeField]
        private D0EnemyHitboxFollowSettings followSettings;

        public Collider Collider => collider;
        public int GeometryId => geometryId;
        public D0EnemyHitboxFollowSettings FollowSettings => followSettings;
    }

    /// <summary>
    /// Complete authored enemy entity root. Enemy identity, life and behaviour
    /// remain owned by D0EnemyDefinition and the battle session.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class D0EnemyEntityView : D0ActorEntityView
    {
        public const int HitboxLayer = 29;
        public const int MaxAdditionalBodyHitboxCount = 4;
        public const int MaxBodyHitboxCount = 1 + MaxAdditionalBodyHitboxCount;
        public const int MaxHitPartCount = MaxBodyHitboxCount + 1;

        [SerializeField]
        private Transform projectileSpawnAnchor;

        [SerializeField]
        private Transform weakpointAnchor;

        [SerializeField]
        private Collider bodyHitbox;

        [SerializeField]
        private Collider weakpointHitbox;

        [SerializeField]
        private D0EnemyHitboxFollowSettings bodyHitboxFollow;

        [SerializeField]
        private D0EnemyHitboxFollowSettings weakpointHitboxFollow;

        [SerializeField]
        private int bodyGeometryId = 2001;

        [SerializeField]
        private int weakpointGeometryId = 2002;

        [SerializeField]
        private bool hasWeakpoint = true;

        [SerializeField]
        private D0EnemyBodyHitboxBinding[] additionalBodyHitboxes = Array.Empty<D0EnemyBodyHitboxBinding>();

        private HitboxRegistry boundRegistry;
        private D0EnemyHitboxBoneFollowRuntime hitboxBoneFollowRuntime;

        private RuntimeId boundRuntimeId = RuntimeId.Invalid;
        private bool gameplayBound;

        public Transform ProjectileSpawnAnchor => projectileSpawnAnchor;
        public Transform WeakpointAnchor => weakpointAnchor;
        public Collider BodyHitbox => bodyHitbox;
        public Collider WeakpointHitbox => weakpointHitbox;
        public RuntimeId RuntimeId => boundRuntimeId;
        public bool IsGameplayBound => gameplayBound;
        public int BodyGeometryId => bodyGeometryId;
        public int WeakpointGeometryId => weakpointGeometryId;
        public bool HasWeakpoint => hasWeakpoint;
        public int BodyHitboxCount => 1 + AdditionalBodyHitboxCount;
        public int HitPartCount => BodyHitboxCount + (hasWeakpoint ? 1 : 0);
        public int BoneFollowHitPartCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < HitPartCount; index++)
                {
                    if (TryGetHitPartFollowSettings(
                            index,
                            out D0EnemyHitboxFollowSettings settings)
                        && settings.FollowMode == D0EnemyHitboxFollowMode.SpineBone)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private int AdditionalBodyHitboxCount => additionalBodyHitboxes == null
            ? 0 : additionalBodyHitboxes.Length;

        public bool TryGetHitPart(
            int hitPartOrdinal,
            out Collider collider,
            out HitPart hitPart,
            out GeometryId geometryId)
        {
            collider = null;
            hitPart = HitPart.Body;
            geometryId = GeometryId.Invalid;
            if (hitPartOrdinal < 0)
            {
                return false;
            }

            if (hitPartOrdinal == 0)
            {
                collider = bodyHitbox;
                geometryId = new GeometryId(bodyGeometryId);
                return collider != null && geometryId.IsValid;
            }

            int additionalIndex = hitPartOrdinal - 1;
            if (additionalIndex >= 0 && additionalIndex < AdditionalBodyHitboxCount)
            {
                D0EnemyBodyHitboxBinding binding = additionalBodyHitboxes[additionalIndex];
                collider = binding.Collider;
                geometryId = new GeometryId(binding.GeometryId);
                return collider != null && geometryId.IsValid;
            }

            if (hasWeakpoint && hitPartOrdinal == BodyHitboxCount)
            {
                collider = weakpointHitbox;
                hitPart = HitPart.Weakpoint;
                geometryId = new GeometryId(weakpointGeometryId);
                return collider != null && geometryId.IsValid;
            }

            return false;
        }

        public bool TryGetHitPartFollowSettings(
            int hitPartOrdinal,
            out D0EnemyHitboxFollowSettings settings)
        {
            settings = default;
            if (hitPartOrdinal < 0)
            {
                return false;
            }

            if (hitPartOrdinal == 0)
            {
                settings = bodyHitboxFollow;
                return true;
            }

            int additionalIndex = hitPartOrdinal - 1;
            if (additionalIndex >= 0 && additionalIndex < AdditionalBodyHitboxCount)
            {
                settings = additionalBodyHitboxes[additionalIndex].FollowSettings;
                return true;
            }

            if (hasWeakpoint && hitPartOrdinal == BodyHitboxCount)
            {
                settings = weakpointHitboxFollow;
                return true;
            }

            return false;
        }

        private bool TryValidateBoneFollowTargetIsolation(out string error)
        {
            for (int hitPartOrdinal = 0; hitPartOrdinal < HitPartCount; hitPartOrdinal++)
            {
                if (!TryGetHitPartFollowSettings(
                        hitPartOrdinal,
                        out D0EnemyHitboxFollowSettings settings)
                    || settings.FollowMode != D0EnemyHitboxFollowMode.SpineBone)
                {
                    continue;
                }

                if (!TryGetHitPart(
                        hitPartOrdinal,
                        out Collider collider,
                        out HitPart hitPart,
                        out _))
                {
                    error = $"Enemy hit part {hitPartOrdinal} could not validate its bone-follow target.";
                    return false;
                }

                Transform target = hitPart == HitPart.Weakpoint
                    ? weakpointAnchor
                    : collider.transform;
                if (TransformsShareHierarchy(target, projectileSpawnAnchor))
                {
                    error = $"Enemy hit part {hitPartOrdinal} bone-follow target must be independent from ProjectileSpawnAnchor.";
                    return false;
                }

                if (hitPart != HitPart.Weakpoint
                    && TransformsShareHierarchy(target, weakpointAnchor))
                {
                    error = $"Enemy hit part {hitPartOrdinal} bone-follow target must be independent from WeakpointAnchor.";
                    return false;
                }

                for (int otherOrdinal = 0; otherOrdinal < HitPartCount; otherOrdinal++)
                {
                    if (otherOrdinal == hitPartOrdinal)
                    {
                        continue;
                    }

                    if (!TryGetHitPart(
                            otherOrdinal,
                            out Collider otherCollider,
                            out HitPart otherHitPart,
                            out _))
                    {
                        error = $"Enemy hit part {otherOrdinal} could not validate target isolation.";
                        return false;
                    }

                    Transform otherTarget = otherHitPart == HitPart.Weakpoint
                        ? weakpointAnchor
                        : otherCollider.transform;
                    if (TransformsShareHierarchy(target, otherTarget))
                    {
                        error = $"Enemy hit part {hitPartOrdinal} bone-follow target must be independent from hit part {otherOrdinal}.";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TransformsShareHierarchy(Transform first, Transform second)
        {
            return first != null && second != null
                && (first == second
                    || first.IsChildOf(second)
                    || second.IsChildOf(first));
        }

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            Spine.SkeletonData followSkeletonData = null;
            bool followSkeletonDataLoaded = false;

            if (GameplayAnchor.localPosition.sqrMagnitude > 0.000001f
                || Quaternion.Angle(GameplayAnchor.localRotation, Quaternion.identity) > 0.01f
                || (GameplayAnchor.localScale - Vector3.one).sqrMagnitude > 0.000001f)
            {
                error = "Enemy entity GameplayAnchor must use an identity local pose so spawn-pose transfer remains exact.";
                return false;
            }

            if (projectileSpawnAnchor == null
                || projectileSpawnAnchor == GameplayAnchor
                || !projectileSpawnAnchor.IsChildOf(GameplayAnchor))
            {
                error = "Enemy entity projectile spawn anchor must be a distinct child of GameplayAnchor.";
                return false;
            }

            if (weakpointAnchor == null
                || weakpointAnchor == GameplayAnchor
                || !weakpointAnchor.IsChildOf(GameplayAnchor))
            {
                error = "Enemy entity weakpoint anchor must be a distinct child of GameplayAnchor.";
                return false;
            }

            if (!(bodyHitbox is BoxCollider))
            {
                error = "Enemy entity primary body hitbox must be a BoxCollider.";
                return false;
            }

            if (!TryValidateBodyHitbox(bodyHitbox, "primary body", out error))
            {
                return false;
            }

            if (bodyGeometryId <= 0)
            {
                error = "Enemy entity primary body geometry id must be positive.";
                return false;
            }

            if (!TryValidateHitboxFollowSettings(
                    bodyHitboxFollow,
                    "primary body",
                    ref followSkeletonData,
                    ref followSkeletonDataLoaded,
                    out error))
            {
                return false;
            }

            if (AdditionalBodyHitboxCount > MaxAdditionalBodyHitboxCount)
            {
                error = $"Enemy entity supports at most {MaxAdditionalBodyHitboxCount} additional body hitboxes.";
                return false;
            }

            for (int index = 0; index < AdditionalBodyHitboxCount; index++)
            {
                D0EnemyBodyHitboxBinding binding = additionalBodyHitboxes[index];
                if (!TryValidateBodyHitbox(
                        binding.Collider,
                        $"additional body {index}",
                        out error))
                {
                    return false;
                }

                if (binding.GeometryId <= 0)
                {
                    error = $"Enemy entity additional body {index} geometry id must be positive.";
                    return false;
                }

                if (!TryValidateHitboxFollowSettings(
                        binding.FollowSettings,
                        $"additional body {index}",
                        ref followSkeletonData,
                        ref followSkeletonDataLoaded,
                        out error))
                {
                    return false;
                }

                if (binding.Collider == bodyHitbox || binding.GeometryId == bodyGeometryId)
                {
                    error = $"Enemy entity additional body {index} duplicates the primary body collider or geometry id.";
                    return false;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    D0EnemyBodyHitboxBinding previousBinding = additionalBodyHitboxes[previous];
                    if (binding.Collider == previousBinding.Collider
                        || binding.GeometryId == previousBinding.GeometryId)
                    {
                        error = $"Enemy entity additional body {index} duplicates another body collider or geometry id.";
                        return false;
                    }
                }
            }

            if (hasWeakpoint)
            {
                if (!(weakpointHitbox is SphereCollider))
                {
                    error = "Enemy entity weakpoint hitbox must be a SphereCollider.";
                    return false;
                }

                if (!TryValidateWeakpointHitbox(out error))
                {
                    return false;
                }

                if (weakpointGeometryId <= 0)
                {
                    error = "Enemy entity weakpoint geometry id must be positive.";
                    return false;
                }

                if (!TryValidateHitboxFollowSettings(
                        weakpointHitboxFollow,
                        "weakpoint",
                        ref followSkeletonData,
                        ref followSkeletonDataLoaded,
                        out error))
                {
                    return false;
                }

                if (weakpointHitbox == bodyHitbox || weakpointGeometryId == bodyGeometryId)
                {
                    error = "Enemy entity weakpoint duplicates the primary body collider or geometry id.";
                    return false;
                }

                for (int index = 0; index < AdditionalBodyHitboxCount; index++)
                {
                    D0EnemyBodyHitboxBinding binding = additionalBodyHitboxes[index];
                    if (weakpointHitbox == binding.Collider
                        || weakpointGeometryId == binding.GeometryId)
                    {
                        error = $"Enemy entity weakpoint duplicates additional body {index}.";
                        return false;
                    }
                }
            }

            if (!TryValidateBoneFollowTargetIsolation(out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryConfigureActorPresenter(
            CombatPresentationProfile presentationProfile,
            D0ActorPresentationDefinition presentationDefinition,
            out string error)
        {
            Spine.Unity.SkeletonAnimation skeleton = SkeletonAnimation;
            if (skeleton == null || presentationProfile == null
                || presentationDefinition == null)
            {
                error = "Enemy entity actor presentation requires skeleton, profile and authored presentation data.";
                return false;
            }

            Actor2DPresenter presenter = ActorPresenter;
            if (presenter == null)
            {
                presenter = gameObject.AddComponent<Actor2DPresenter>();
            }

            return presenter.TryConfigureRuntime(
                skeleton,
                presentationProfile,
                false,
                VisualRoot,
                presentationDefinition,
                out error);
        }

        private bool TryCreateHitboxBoneFollowRuntime(
            out D0EnemyHitboxBoneFollowRuntime runtime,
            out string error)
        {
            int followCount = BoneFollowHitPartCount;
            if (followCount == 0)
            {
                runtime = null;
                error = string.Empty;
                return true;
            }

            var targets = new D0EnemyHitboxBoneFollowTarget[followCount];
            int targetIndex = 0;
            for (int hitPartOrdinal = 0; hitPartOrdinal < HitPartCount; hitPartOrdinal++)
            {
                if (!TryGetHitPartFollowSettings(
                        hitPartOrdinal,
                        out D0EnemyHitboxFollowSettings settings)
                    || settings.FollowMode != D0EnemyHitboxFollowMode.SpineBone)
                {
                    continue;
                }

                if (!TryGetHitPart(
                        hitPartOrdinal,
                        out Collider collider,
                        out HitPart hitPart,
                        out _))
                {
                    runtime = null;
                    error = $"Enemy hit part {hitPartOrdinal} could not prepare bone following.";
                    return false;
                }

                Transform target = hitPart == HitPart.Weakpoint
                    ? weakpointAnchor
                    : collider.transform;
                targets[targetIndex++] = new D0EnemyHitboxBoneFollowTarget(
                    target,
                    settings.BoneName,
                    settings.FollowBoneRotation);
            }

            return D0EnemyHitboxBoneFollowRuntime.TryCreate(
                SkeletonAnimation,
                targets,
                out runtime,
                out error);
        }

        public bool TryBindGameplay(
            HitboxRegistry registry,
            RuntimeId playerRuntimeId,
            RuntimeId enemyRuntimeId,
            out string error)
        {
            error = string.Empty;
            if (registry == null)
            {
                error = "Enemy entity requires a HitboxRegistry before binding gameplay.";
                return false;
            }

            if (!playerRuntimeId.IsValid || !enemyRuntimeId.IsValid
                || playerRuntimeId == enemyRuntimeId)
            {
                error = "Enemy entity requires valid, distinct player and enemy RuntimeIds.";
                return false;
            }

            if (!TryValidate(out error)
                || !TryCreateHitboxBoneFollowRuntime(
                    out D0EnemyHitboxBoneFollowRuntime nextBoneFollowRuntime,
                    out error))
            {
                return false;
            }

            UnbindGameplay();
            if (!registry.TryBindEnemyEntity(
                    playerRuntimeId,
                    enemyRuntimeId,
                    this,
                    out error))
            {
                nextBoneFollowRuntime?.Dispose();
                return false;
            }

            boundRegistry = registry;
            boundRuntimeId = enemyRuntimeId;
            gameplayBound = true;
            hitboxBoneFollowRuntime = nextBoneFollowRuntime;
            hitboxBoneFollowRuntime?.Activate();
            SetGameplayCollidersEnabled(true);
            return true;
        }

        public void UnbindGameplay()
        {
            hitboxBoneFollowRuntime?.Dispose();
            hitboxBoneFollowRuntime = null;

            if (boundRegistry != null)
            {
                boundRegistry.TryUnbindEnemyEntity(bodyHitbox);
                for (int index = 0; index < AdditionalBodyHitboxCount; index++)
                {
                    boundRegistry.TryUnbindEnemyEntity(additionalBodyHitboxes[index].Collider);
                }

                if (weakpointHitbox != null)
                {
                    boundRegistry.TryUnbindEnemyEntity(weakpointHitbox);
                }
            }

            boundRegistry = null;
            boundRuntimeId = RuntimeId.Invalid;
            gameplayBound = false;
            SetGameplayCollidersEnabled(false);
        }

        public void SetGameplayCollidersEnabled(bool enabled)
        {
            if (bodyHitbox != null)
            {
                bodyHitbox.enabled = enabled;
            }

            for (int index = 0; index < AdditionalBodyHitboxCount; index++)
            {
                Collider collider = additionalBodyHitboxes[index].Collider;
                if (collider != null)
                {
                    collider.enabled = enabled;
                }
            }

            if (weakpointHitbox != null)
            {
                weakpointHitbox.enabled = enabled && hasWeakpoint;
            }
        }

        public void SetWorldPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        private void OnDisable()
        {
            UnbindGameplay();
        }

        private bool TryValidateHitboxFollowSettings(
            D0EnemyHitboxFollowSettings settings,
            string label,
            ref Spine.SkeletonData skeletonData,
            ref bool skeletonDataLoaded,
            out string error)
        {
            if (settings.FollowMode == D0EnemyHitboxFollowMode.AuthoredTransform)
            {
                error = string.Empty;
                return true;
            }

            if (settings.FollowMode != D0EnemyHitboxFollowMode.SpineBone)
            {
                error = $"Enemy entity {label} has an unsupported hitbox follow mode.";
                return false;
            }

            string boneName = settings.BoneName;
            if (string.IsNullOrWhiteSpace(boneName))
            {
                error = $"Enemy entity {label} bone-follow mode requires a Spine bone name.";
                return false;
            }

            if (!string.Equals(boneName, boneName.Trim(), StringComparison.Ordinal))
            {
                error = $"Enemy entity {label} Spine bone name must not have surrounding whitespace.";
                return false;
            }

            Spine.Unity.SkeletonAnimation skeletonAnimation = SkeletonAnimation;
            if (skeletonAnimation == null || skeletonAnimation.SkeletonDataAsset == null)
            {
                error = $"Enemy entity {label} bone-follow mode requires valid Spine skeleton data.";
                return false;
            }

            Spine.Unity.UpdateMode invisibleMode = skeletonAnimation.updateWhenInvisible;
            if (invisibleMode != Spine.Unity.UpdateMode.EverythingExceptMesh
                && invisibleMode != Spine.Unity.UpdateMode.FullUpdate)
            {
                error = $"Enemy entity {label} bone following requires Spine to update bones while invisible.";
                return false;
            }

            if (!skeletonDataLoaded)
            {
                skeletonDataLoaded = true;
                try
                {
                    skeletonData = skeletonAnimation.SkeletonDataAsset.GetSkeletonData(true);
                }
                catch (Exception exception)
                {
                    error = $"Enemy entity Spine skeleton data could not load: {exception.Message}";
                    return false;
                }
            }

            if (skeletonData == null || skeletonData.FindBone(boneName) == null)
            {
                error = $"Enemy entity {label} Spine bone '{boneName}' was not found.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateWeakpointHitbox(out string error)
        {
            if (weakpointHitbox == null
                || (weakpointHitbox.transform != weakpointAnchor
                    && !weakpointHitbox.transform.IsChildOf(weakpointAnchor)))
            {
                error = "Enemy entity weakpoint SphereCollider must be on or below WeakpointAnchor.";
                return false;
            }

            if (weakpointHitbox.gameObject.layer != HitboxLayer)
            {
                error = $"Enemy entity weakpoint must use layer {HitboxLayer} (FPG_Hitbox).";
                return false;
            }

            if (weakpointHitbox.isTrigger)
            {
                error = "Enemy entity weakpoint must not be a trigger.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateBodyHitbox(
            Collider collider,
            string label,
            out string error)
        {
            if (collider == null)
            {
                error = $"Enemy entity {label} collider is missing.";
                return false;
            }

            if (!(collider is BoxCollider)
                && !(collider is CapsuleCollider)
                && !(collider is SphereCollider))
            {
                error = $"Enemy entity {label} must use BoxCollider, CapsuleCollider or SphereCollider.";
                return false;
            }

            Transform colliderTransform = collider.transform;
            if (colliderTransform == GameplayAnchor
                || !colliderTransform.IsChildOf(GameplayAnchor)
                || colliderTransform == weakpointAnchor
                || colliderTransform.IsChildOf(weakpointAnchor))
            {
                error = $"Enemy entity {label} must be below GameplayAnchor and outside WeakpointAnchor.";
                return false;
            }

            if (collider.gameObject.layer != HitboxLayer)
            {
                error = $"Enemy entity {label} must use layer {HitboxLayer} (FPG_Hitbox).";
                return false;
            }

            if (collider.isTrigger)
            {
                error = $"Enemy entity {label} must not be a trigger.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
