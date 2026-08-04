using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum FpgResolvedAimTargetType
    {
        None = 0,
        Enemy,
        Projectile,
        Environment
    }

    /// <summary>
    /// Immutable result shared by the reticle, attack gate and authoritative
    /// shot query. TargetPoint is the camera intent; SurfacePoint is the first
    /// registered surface reached from ShotOrigin along that intent.
    /// </summary>
    public readonly struct FpgResolvedAimContext
    {
        /// <summary>
        /// Creates a resolved aim context using one surface for both the
        /// reticle and the authoritative shot. This overload remains for
        /// existing callers that do not yet provide the two-stage metadata.
        /// </summary>
        public FpgResolvedAimContext(
            Vector2 reticleViewport,
            Vector3 cameraOrigin,
            Vector3 cameraDirection,
            Vector3 targetPoint,
            Vector3 shotOrigin,
            Vector3 centerDirection,
            Vector3 surfacePoint,
            FpgResolvedAimTargetType targetType,
            RuntimeId targetId,
            QueryTargetKind targetKind,
            HitPart hitPart,
            GeometryId geometryId,
            string targetCoverId,
            string currentCoverId,
            long version,
            long frozenVersion,
            float distance)
            : this(
                reticleViewport,
                cameraOrigin,
                cameraDirection,
                targetPoint,
                shotOrigin,
                centerDirection,
                surfacePoint,
                targetType,
                targetId,
                targetKind,
                hitPart,
                geometryId,
                targetType,
                targetId,
                targetKind,
                hitPart,
                geometryId,
                targetCoverId,
                currentCoverId,
                version,
                frozenVersion,
                distance)
        {
        }

        /// <summary>
        /// Creates a two-stage aim context. Reticle* describes the registered
        /// collider reached by the camera ray, while the unprefixed target
        /// fields describe the first registered surface reached from
        /// ShotOrigin and are authoritative for attack validation/submission.
        /// </summary>
        public FpgResolvedAimContext(
            Vector2 reticleViewport,
            Vector3 cameraOrigin,
            Vector3 cameraDirection,
            Vector3 targetPoint,
            Vector3 shotOrigin,
            Vector3 centerDirection,
            Vector3 surfacePoint,
            FpgResolvedAimTargetType reticleTargetType,
            RuntimeId reticleTargetId,
            QueryTargetKind reticleTargetKind,
            HitPart reticleHitPart,
            GeometryId reticleGeometryId,
            FpgResolvedAimTargetType targetType,
            RuntimeId targetId,
            QueryTargetKind targetKind,
            HitPart hitPart,
            GeometryId geometryId,
            string targetCoverId,
            string currentCoverId,
            long version,
            long frozenVersion,
            float distance)
        {
            ReticleViewport = reticleViewport;
            CameraOrigin = cameraOrigin;
            CameraDirection = cameraDirection;
            TargetPoint = targetPoint;
            ShotOrigin = shotOrigin;
            CenterDirection = centerDirection;
            SurfacePoint = surfacePoint;
            ReticleTargetType = reticleTargetType;
            ReticleTargetId = reticleTargetId;
            ReticleTargetKind = reticleTargetKind;
            ReticleHitPart = reticleHitPart;
            ReticleGeometryId = reticleGeometryId;
            TargetType = targetType;
            TargetId = targetId;
            TargetKind = targetKind;
            HitPart = hitPart;
            GeometryId = geometryId;
            TargetCoverId = targetCoverId ?? string.Empty;
            CurrentCoverId = currentCoverId ?? string.Empty;
            Version = version;
            FrozenVersion = frozenVersion;
            Distance = distance;
        }

        public static FpgResolvedAimContext Invalid =>
            default(FpgResolvedAimContext);

        public Vector2 ReticleViewport { get; }
        public Vector3 CameraOrigin { get; }
        public Vector3 CameraDirection { get; }
        public Vector3 TargetPoint { get; }
        public Vector3 ShotOrigin { get; }
        public Vector3 CenterDirection { get; }
        public Vector3 SurfacePoint { get; }
        public FpgResolvedAimTargetType ReticleTargetType { get; }
        public RuntimeId ReticleTargetId { get; }
        public QueryTargetKind ReticleTargetKind { get; }
        public HitPart ReticleHitPart { get; }
        public GeometryId ReticleGeometryId { get; }
        public FpgResolvedAimTargetType TargetType { get; }
        public RuntimeId TargetId { get; }
        public QueryTargetKind TargetKind { get; }
        public HitPart HitPart { get; }
        public GeometryId GeometryId { get; }
        public string TargetCoverId { get; }
        public string CurrentCoverId { get; }
        public long Version { get; }
        public long FrozenVersion { get; }
        public float Distance { get; }

        public bool IsValid => Version > 0
            && IsFinite(ReticleViewport)
            && IsFinite(CameraOrigin)
            && IsUsableDirection(CameraDirection)
            && IsFinite(TargetPoint)
            && IsFinite(ShotOrigin)
            && IsUsableDirection(CenterDirection)
            && IsFinite(SurfacePoint)
            && IsFinite(Distance)
            && Distance >= 0f;

        public bool IsFrozen => IsValid && FrozenVersion == Version;
        public bool HasSurface => GeometryId.IsValid;
        public bool IsEnemy => TargetType == FpgResolvedAimTargetType.Enemy;
        public bool HasReticleSurface => ReticleGeometryId.IsValid;
        public bool IsReticleEnemy =>
            ReticleTargetType == FpgResolvedAimTargetType.Enemy
            && ReticleTargetKind == QueryTargetKind.Combatant;
        public bool IsCurrentCoverBlocked =>
            TargetType == FpgResolvedAimTargetType.Environment
            && !string.IsNullOrWhiteSpace(CurrentCoverId)
            && string.Equals(
                TargetCoverId,
                CurrentCoverId,
                StringComparison.Ordinal);

        public FpgResolvedAimContext Freeze()
        {
            return !IsValid || IsFrozen
                ? this
                : new FpgResolvedAimContext(
                    ReticleViewport,
                    CameraOrigin,
                    CameraDirection,
                    TargetPoint,
                    ShotOrigin,
                    CenterDirection,
                    SurfacePoint,
                    ReticleTargetType,
                    ReticleTargetId,
                    ReticleTargetKind,
                    ReticleHitPart,
                    ReticleGeometryId,
                    TargetType,
                    TargetId,
                    TargetKind,
                    HitPart,
                    GeometryId,
                    TargetCoverId,
                    CurrentCoverId,
                    Version,
                    Version,
                    Distance);
        }

        public FpgResolvedAimContext WithCurrentCover(string currentCoverId)
        {
            return !IsValid
                ? this
                : new FpgResolvedAimContext(
                    ReticleViewport,
                    CameraOrigin,
                    CameraDirection,
                    TargetPoint,
                    ShotOrigin,
                    CenterDirection,
                    SurfacePoint,
                    ReticleTargetType,
                    ReticleTargetId,
                    ReticleTargetKind,
                    ReticleHitPart,
                    ReticleGeometryId,
                    TargetType,
                    TargetId,
                    TargetKind,
                    HitPart,
                    GeometryId,
                    TargetCoverId,
                    currentCoverId,
                    Version,
                    FrozenVersion,
                    Distance);
        }

        private static bool IsUsableDirection(Vector3 value)
        {
            return IsFinite(value) && value.sqrMagnitude > 0.0000001f;
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public enum FpgAttackUnavailableReason
    {
        None = 0,
        InvalidPlayer,
        PlayerDead,
        EncounterInactive,
        CoverMoving,
        InvalidAim,
        WeaponDisabled,
        Reloading,
        NotEnoughAmmo,
        CurrentCoverBlocked,
        WeaponBusy,
        Cooldown
    }

    /// <summary>
    /// Pure attack-start decision. It deliberately checks the complete authored
    /// ammo cost before a peek begins, so rejected input has no gameplay or
    /// presentation side effects.
    /// </summary>
    public readonly struct FpgAttackAvailability
    {
        private FpgAttackAvailability(
            FpgPlayerSkillSlot slot,
            FpgAttackUnavailableReason reason,
            int ammo,
            int requiredAmmo)
        {
            Slot = slot;
            Reason = reason;
            Ammo = ammo;
            RequiredAmmo = requiredAmmo;
        }

        public FpgPlayerSkillSlot Slot { get; }
        public FpgAttackUnavailableReason Reason { get; }
        public int Ammo { get; }
        public int RequiredAmmo { get; }
        public bool Ready => Reason == FpgAttackUnavailableReason.None;
        public bool ShouldAutoReload =>
            Reason == FpgAttackUnavailableReason.NotEnoughAmmo;

        public static FpgAttackAvailability Resolve(
            FpgPlayerSkillSlot slot,
            bool playerValid,
            bool playerDead,
            bool encounterActive,
            bool coverMoving,
            WeaponState weaponState,
            TickIndex recastLockedUntilTick,
            TickIndex tick,
            int ammo,
            int requiredAmmo,
            in FpgResolvedAimContext aim,
            bool allowActiveReleaseState = false)
        {
            FpgAttackUnavailableReason reason;
            if (!playerValid || slot != FpgPlayerSkillSlot.Primary
                && slot != FpgPlayerSkillSlot.Secondary
                || requiredAmmo <= 0 || ammo < 0)
            {
                reason = FpgAttackUnavailableReason.InvalidPlayer;
            }
            else if (playerDead)
            {
                reason = FpgAttackUnavailableReason.PlayerDead;
            }
            else if (!encounterActive)
            {
                reason = FpgAttackUnavailableReason.EncounterInactive;
            }
            else if (coverMoving)
            {
                reason = FpgAttackUnavailableReason.CoverMoving;
            }
            else if (weaponState == WeaponState.Disabled)
            {
                reason = FpgAttackUnavailableReason.WeaponDisabled;
            }
            else if (weaponState == WeaponState.Reloading)
            {
                reason = FpgAttackUnavailableReason.Reloading;
            }
            else if (ammo < requiredAmmo)
            {
                reason = FpgAttackUnavailableReason.NotEnoughAmmo;
            }
            else if (!aim.IsValid)
            {
                reason = FpgAttackUnavailableReason.InvalidAim;
            }
            else if (aim.IsCurrentCoverBlocked)
            {
                reason = FpgAttackUnavailableReason.CurrentCoverBlocked;
            }
            else if (weaponState != WeaponState.Ready
                && !(allowActiveReleaseState
                    && (slot == FpgPlayerSkillSlot.Primary
                        && weaponState == WeaponState.PrimaryRecovery
                        || slot == FpgPlayerSkillSlot.Secondary
                        && (weaponState == WeaponState.AltRecovery
                            || weaponState == WeaponState.AltCharging))))
            {
                reason = FpgAttackUnavailableReason.WeaponBusy;
            }
            else if (recastLockedUntilTick.IsValid
                && tick.IsValid
                && tick < recastLockedUntilTick)
            {
                reason = FpgAttackUnavailableReason.Cooldown;
            }
            else
            {
                reason = FpgAttackUnavailableReason.None;
            }

            return new FpgAttackAvailability(
                slot,
                reason,
                ammo,
                requiredAmmo);
        }
    }

}
