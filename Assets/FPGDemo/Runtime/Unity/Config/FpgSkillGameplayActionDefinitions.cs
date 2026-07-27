using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using UnityEngine;
using UnityEngine.Serialization;

namespace FPG.Demo.Unity
{
    public enum FpgSkillAttackMode
    {
        None = 0,
        PelletRays = 1,
        AreaAtFirstSurface = 2,
        BoundTarget = 3
    }

    public enum FpgSkillProjectileImpactMode
    {
        None = 0,
        AreaAtFirstSurface = 1,
        BoundTarget = 2
    }

    public enum FpgSkillAuthoringSchemaState
    {
        V3Only = 3
    }

    [Serializable]
    public abstract class FpgSkillGameplayActionDefinition
    {
        [SerializeField]
        private string eventId = "action";

        [SerializeField, Min(0)]
        private int tick;

        [FormerlySerializedAs("sortOrder")]
        [SerializeField, Min(0)]
        private int authoredOrdinal;

        [SerializeField]
        private string socketId = string.Empty;

        [SerializeField]
        private FpgSkillTargetSource targetSource =
            FpgSkillTargetSource.CurrentAim;

        [SerializeField]
        private Vector3 targetOffset;

        public string EventId => eventId;
        public int Tick => tick;
        public int AuthoredOrdinal => authoredOrdinal;
        public string SocketId => socketId;
        public FpgSkillTargetSource TargetSource => targetSource;
        public Vector3 TargetOffset => targetOffset;

        internal abstract bool TryValidate(
            int durationTicks,
            out string error);

        internal virtual bool HasPresentation => false;

        internal virtual FpgCompiledSkillActionPresentation
            CompilePresentation(
                FpgSkillActionKind actionKind,
                int actionIndex,
                string scopePrefix)
        {
            throw new InvalidOperationException(
                $"Gameplay action '{eventId}' has no presentation to compile.");
        }

        internal int OffsetXMillimeters =>
            Mathf.RoundToInt(targetOffset.x * 1000f);
        internal int OffsetYMillimeters =>
            Mathf.RoundToInt(targetOffset.y * 1000f);
        internal int OffsetZMillimeters =>
            Mathf.RoundToInt(targetOffset.z * 1000f);

        // Unity's EnumFlagsField represents its Everything choice as -1.
        // Gameplay supports only the explicitly declared target flags.
        protected static AttackTargetKinds NormalizeAllowedTargetKinds(
            AttackTargetKinds value)
        {
            return value == (AttackTargetKinds)(-1)
                ? AttackTargetKinds.All
                : value;
        }

        internal bool TryValidateHeader(int durationTicks, out string error)
        {
            if (!FpgSkillStableId.IsValid(eventId))
            {
                error = "Skill gameplay action requires a stable event ID.";
                return false;
            }

            if (tick < 0 || tick > durationTicks || authoredOrdinal < 0)
            {
                error =
                    $"Skill gameplay action '{eventId}' has an invalid tick or authored ordinal.";
                return false;
            }

            if (!string.IsNullOrEmpty(socketId)
                && !FpgSkillStableId.IsValid(socketId))
            {
                error =
                    $"Skill gameplay action '{eventId}' has an invalid socket ID.";
                return false;
            }

            if (!Enum.IsDefined(typeof(FpgSkillTargetSource), targetSource)
                || targetSource == FpgSkillTargetSource.None
                || !IsFinite(targetOffset)
                || Mathf.Abs(targetOffset.x) > 2147483f
                || Mathf.Abs(targetOffset.y) > 2147483f
                || Mathf.Abs(targetOffset.z) > 2147483f)
            {
                error =
                    $"Skill gameplay action '{eventId}' has an invalid target source or offset.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }

    [Serializable]
    public sealed class FpgSkillAttackEventDefinition :
        FpgSkillGameplayActionDefinition
    {
        [SerializeField]
        private FpgSkillAttackMode mode = FpgSkillAttackMode.PelletRays;

        [SerializeField, Min(0)]
        private int ammoCost = 1;

        [SerializeField, Min(0)]
        private int baseDamage = 4;

        [SerializeField, Min(0)]
        private int breakDamage = 4;

        [SerializeField, Min(0)]
        private int weakpointDamageMultiplierBasisPoints = 12000;

        [SerializeField, Min(0)]
        private int weakpointBreakMultiplierBasisPoints = 25000;

        [Header("Pellet Rays")]
        [SerializeField, Min(1)]
        private int pelletCount = WeaponDefinition.PrimaryPelletCount;

        [SerializeField, Min(0)]
        private int additionalPenetrationCount;

        [Header("Area At First Surface")]
        [SerializeField, Min(1)]
        private int areaCombatantLimit = 4;

        [SerializeField, Min(0)]
        private int areaProjectileLimit =
            WeaponDefinition.DefaultSecondaryAreaProjectileLimit;

        [SerializeField]
        private AttackTargetKinds allowedTargetKinds =
            WeaponDefinition.PlayerAttackTargetKinds;

        [Header("Bound Target")]
        [SerializeField, Min(1)]
        private int threatDefinitionId = 1;

        [SerializeField]
        private ThreatTargetPolicy boundTargetPolicy =
            ThreatTargetPolicy.PlayerCombatant;

        [SerializeField, Min(0)]
        private int delayTicks;

        [SerializeField]
        private FpgThreatPresentationKind threatPresentationKind =
            FpgThreatPresentationKind.HeavyWeakpoint;

        [Header("Presentation")]
        [SerializeReference]
        private FpgVfxPresentationDefinition trajectoryPresentation;

        [SerializeReference]
        private FpgImpactPresentationBundleDefinition impactPresentation;

        public FpgSkillAttackMode Mode => mode;
        public int AmmoCost => ammoCost;
        public int BaseDamage => baseDamage;
        public int BreakDamage => breakDamage;
        public int WeakpointDamageMultiplierBasisPoints =>
            weakpointDamageMultiplierBasisPoints;
        public int WeakpointBreakMultiplierBasisPoints =>
            weakpointBreakMultiplierBasisPoints;
        public int PelletCount => pelletCount;
        public int AdditionalPenetrationCount => additionalPenetrationCount;
        public int AreaCombatantLimit => areaCombatantLimit;
        public int AreaProjectileLimit => areaProjectileLimit;
        public AttackTargetKinds AllowedTargetKinds =>
            NormalizeAllowedTargetKinds(allowedTargetKinds);
        public int ThreatDefinitionId => threatDefinitionId;
        public ThreatTargetPolicy BoundTargetPolicy => boundTargetPolicy;
        public int DelayTicks => delayTicks;
        public FpgThreatPresentationKind ThreatPresentationKind =>
            threatPresentationKind;
        public FpgVfxPresentationDefinition TrajectoryPresentation =>
            trajectoryPresentation;
        public FpgImpactPresentationBundleDefinition ImpactPresentation =>
            impactPresentation;

        internal override bool HasPresentation =>
            trajectoryPresentation != null
            || (impactPresentation != null && impactPresentation.HasAny);

        public QueryPolicy QueryPolicy => mode == FpgSkillAttackMode.PelletRays
            ? QueryPolicy.PelletRays
            : mode == FpgSkillAttackMode.AreaAtFirstSurface
                ? QueryPolicy.DirectThenArea
                : mode == FpgSkillAttackMode.BoundTarget
                    ? QueryPolicy.TimedImpact
                    : QueryPolicy.None;

        public AttackQueryMode QueryMode =>
            mode == FpgSkillAttackMode.PelletRays
                ? AttackQueryMode.FirstSurfacePenetration
                : mode == FpgSkillAttackMode.AreaAtFirstSurface
                    ? AttackQueryMode.AreaAtFirstSurface
                    : AttackQueryMode.Legacy;

        public int PayloadCount =>
            mode == FpgSkillAttackMode.PelletRays ? pelletCount : 1;

        public int MaxImpactCount
        {
            get
            {
                switch (mode)
                {
                    case FpgSkillAttackMode.PelletRays:
                        return checked(
                            pelletCount * (additionalPenetrationCount + 1));

                    case FpgSkillAttackMode.AreaAtFirstSurface:
                        return checked(
                            areaCombatantLimit + areaProjectileLimit);

                    case FpgSkillAttackMode.BoundTarget:
                        return 1;

                    default:
                        return 0;
                }
            }
        }

        internal override bool TryValidate(int durationTicks, out string error)
        {
            if (!TryValidateHeader(durationTicks, out error))
            {
                return false;
            }

            if ((trajectoryPresentation != null
                    && !trajectoryPresentation.TryValidate(out error))
                || (impactPresentation != null
                    && !impactPresentation.TryValidate(out error)))
            {
                error =
                    $"Attack action '{EventId}' has invalid presentation: {error}";
                return false;
            }

            if (trajectoryPresentation != null)
            {
                FpgTrajectoryVfxView trajectoryView =
                    trajectoryPresentation.Prefab
                        .GetComponent<FpgTrajectoryVfxView>();
                if (trajectoryView == null
                    || !trajectoryView.TryValidate(out error))
                {
                    error = trajectoryView == null
                        ? $"Attack action '{EventId}' trajectory Prefab root requires FpgTrajectoryVfxView."
                        : $"Attack action '{EventId}' has invalid trajectory Prefab: {error}";
                    return false;
                }
            }

            if (!Enum.IsDefined(typeof(FpgSkillAttackMode), mode)
                || mode == FpgSkillAttackMode.None
                || !TryValidateCostAndDamage())
            {
                error =
                    $"Attack action '{EventId}' has an invalid mode, cost or damage.";
                return false;
            }

            switch (mode)
            {
                case FpgSkillAttackMode.PelletRays:
                    if (pelletCount <= 0
                        || additionalPenetrationCount < 0
                        || additionalPenetrationCount
                            > (int.MaxValue / pelletCount) - 1
                        || !IsValidTargetKinds(AllowedTargetKinds))
                    {
                        error =
                            $"Pellet attack action '{EventId}' has invalid query or capacity values.";
                        return false;
                    }

                    break;

                case FpgSkillAttackMode.AreaAtFirstSurface:
                    if (areaCombatantLimit <= 0
                        || areaProjectileLimit < 0
                        || areaCombatantLimit
                            > int.MaxValue - areaProjectileLimit
                        || !IsValidTargetKinds(AllowedTargetKinds))
                    {
                        error =
                            $"Area attack action '{EventId}' has invalid query or capacity values.";
                        return false;
                    }

                    break;

                case FpgSkillAttackMode.BoundTarget:
                    if (threatDefinitionId <= 0
                        || !Enum.IsDefined(
                            typeof(FpgThreatPresentationKind),
                            threatPresentationKind)
                        || !Enum.IsDefined(
                            typeof(ThreatTargetPolicy),
                            boundTargetPolicy)
                        || delayTicks < 0)
                    {
                        error =
                            $"Bound-target attack action '{EventId}' has invalid target, delay or presentation values.";
                        return false;
                    }

                    if (!FpgThreatPresentationRules.IsValidForTimedImpact(
                            threatPresentationKind))
                    {
                        error =
                            $"Bound-target attack action '{EventId}' requires the heavy-weakpoint presentation kind.";
                        return false;
                    }

                    break;
            }

            error = string.Empty;
            return true;
        }

        internal DamageSpec CompileDamage()
        {
            return new DamageSpec(
                baseDamage,
                breakDamage,
                weakpointDamageMultiplierBasisPoints,
                weakpointBreakMultiplierBasisPoints);
        }

        internal override FpgCompiledSkillActionPresentation
            CompilePresentation(
                FpgSkillActionKind actionKind,
                int actionIndex,
                string scopePrefix)
        {
            if (actionKind != FpgSkillActionKind.Attack
                || !HasPresentation)
            {
                throw new ArgumentOutOfRangeException(nameof(actionKind));
            }

            FpgPresentationHandle trajectory =
                trajectoryPresentation == null
                    ? default(FpgPresentationHandle)
                    : FpgSkillStableId.CompilePresentationHandle(
                        scopePrefix + ":" + EventId + ":trajectory.vfx");
            FpgCompiledImpactPresentation impact =
                impactPresentation == null
                    ? default(FpgCompiledImpactPresentation)
                    : impactPresentation.Compile(
                        scopePrefix + ":" + EventId + ":impact");
            ulong hash = FpgPresentationAuthoringHash.Begin(21UL);
            hash = StableHash.Append(
                hash,
                trajectoryPresentation == null
                    ? 0UL
                    : trajectoryPresentation.ComputeContentHash());
            hash = StableHash.Append(
                hash,
                impactPresentation == null
                    ? 0UL
                    : impactPresentation.ComputeContentHash());
            return new FpgCompiledSkillActionPresentation(
                actionKind,
                actionIndex,
                trajectory,
                impact,
                default(FpgPresentationHandle),
                default(FpgCompiledImpactPresentation),
                0,
                hash);
        }

        private bool TryValidateCostAndDamage()
        {
            return ammoCost >= 0
                && baseDamage >= 0
                && breakDamage >= 0
                && weakpointDamageMultiplierBasisPoints >= 0
                && weakpointBreakMultiplierBasisPoints >= 0;
        }

        private static bool IsValidTargetKinds(AttackTargetKinds value)
        {
            const AttackTargetKinds known =
                AttackTargetKinds.All;
            return value != AttackTargetKinds.None && (value & ~known) == 0;
        }
    }

    [Serializable]
    public sealed class FpgSkillProjectileEventDefinition :
        FpgSkillGameplayActionDefinition
    {
        [SerializeField]
        private FpgSkillProjectileImpactMode impactMode =
            FpgSkillProjectileImpactMode.BoundTarget;

        [SerializeField, Min(0)]
        private int ammoCost;

        [SerializeField, Min(0)]
        private int baseDamage = 10;

        [SerializeField, Min(0)]
        private int breakDamage;

        [SerializeField, Min(0)]
        private int weakpointDamageMultiplierBasisPoints = DamageSpec.BasisPoints;

        [SerializeField, Min(0)]
        private int weakpointBreakMultiplierBasisPoints = DamageSpec.BasisPoints;

        [Header("Projectile")]
        [SerializeField, Min(1)]
        private int threatDefinitionId = 1;

        [SerializeField, Min(1)]
        private int projectileDefinitionId = 1;

        [SerializeField, Min(1)]
        private int projectileCount = 1;

        [SerializeField, Min(1)]
        private int projectileFlightTicks = 30;

        [SerializeField, Min(1)]
        private int projectileLifetimeTicks = 45;

        [SerializeField, Min(0)]
        private int projectileMaxHitPoints;

        [SerializeField]
        private bool projectileInterceptable;

        [SerializeField, Min(1)]
        private int projectileBudgetUnits = 1;

        [SerializeField, Min(1)]
        private int projectileSweepRadiusKey = 1;

        [SerializeField]
        private FpgThreatPresentationKind threatPresentationKind =
            FpgThreatPresentationKind.FastUninterceptable;

        [Header("Area At First Surface")]
        [SerializeField, Min(1)]
        private int areaCombatantLimit = 4;

        [SerializeField, Min(0)]
        private int areaProjectileLimit =
            WeaponDefinition.DefaultSecondaryAreaProjectileLimit;

        [SerializeField]
        private AttackTargetKinds allowedTargetKinds =
            WeaponDefinition.PlayerAttackTargetKinds;

        [Header("Presentation")]
        [SerializeReference]
        private FpgVfxPresentationDefinition flightVfx;

        [SerializeReference]
        private FpgImpactPresentationBundleDefinition collisionPresentation;

        public FpgSkillProjectileImpactMode ImpactMode => impactMode;
        public int AmmoCost => ammoCost;
        public int BaseDamage => baseDamage;
        public int BreakDamage => breakDamage;
        public int WeakpointDamageMultiplierBasisPoints =>
            weakpointDamageMultiplierBasisPoints;
        public int WeakpointBreakMultiplierBasisPoints =>
            weakpointBreakMultiplierBasisPoints;
        public int ThreatDefinitionId => threatDefinitionId;
        public int ProjectileDefinitionId => projectileDefinitionId;
        public int ProjectileCount => projectileCount;
        public int ProjectileFlightTicks => projectileFlightTicks;
        public int ProjectileLifetimeTicks => projectileLifetimeTicks;
        public int ProjectileMaxHitPoints => projectileMaxHitPoints;
        public bool ProjectileInterceptable => projectileInterceptable;
        public int ProjectileBudgetUnits => projectileBudgetUnits;
        public int ProjectileSweepRadiusKey => projectileSweepRadiusKey;
        public FpgThreatPresentationKind ThreatPresentationKind =>
            threatPresentationKind;
        public int AreaCombatantLimit => areaCombatantLimit;
        public int AreaProjectileLimit => areaProjectileLimit;
        public AttackTargetKinds AllowedTargetKinds =>
            NormalizeAllowedTargetKinds(allowedTargetKinds);
        public FpgVfxPresentationDefinition FlightVfx => flightVfx;
        public FpgImpactPresentationBundleDefinition CollisionPresentation =>
            collisionPresentation;

        internal override bool HasPresentation => flightVfx != null
            || (collisionPresentation != null
                && collisionPresentation.HasAny);

        public int MaxImpactCount =>
            impactMode == FpgSkillProjectileImpactMode.AreaAtFirstSurface
                ? checked(areaCombatantLimit + areaProjectileLimit)
                : projectileCount;

        internal override bool TryValidate(int durationTicks, out string error)
        {
            if (!TryValidateHeader(durationTicks, out error))
            {
                return false;
            }

            if ((flightVfx != null && !flightVfx.TryValidate(out error))
                || (collisionPresentation != null
                    && !collisionPresentation.TryValidate(out error)))
            {
                error =
                    $"Projectile action '{EventId}' has invalid presentation: {error}";
                return false;
            }

            if (!Enum.IsDefined(
                    typeof(FpgSkillProjectileImpactMode),
                    impactMode)
                || impactMode == FpgSkillProjectileImpactMode.None
                || ammoCost < 0
                || baseDamage < 0
                || breakDamage < 0
                || weakpointDamageMultiplierBasisPoints < 0
                || weakpointBreakMultiplierBasisPoints < 0
                || !Enum.IsDefined(
                    typeof(FpgThreatPresentationKind),
                    threatPresentationKind)
                || threatDefinitionId <= 0
                || projectileDefinitionId <= 0
                || projectileCount <= 0
                || projectileFlightTicks <= 0
                || projectileLifetimeTicks < projectileFlightTicks
                || projectileMaxHitPoints < 0
                || (projectileInterceptable && projectileMaxHitPoints <= 0)
                || projectileBudgetUnits <= 0
                || projectileSweepRadiusKey <= 0
                || projectileCount > int.MaxValue / projectileBudgetUnits)
            {
                error =
                    $"Projectile action '{EventId}' has invalid mode, damage, timing or capacity values.";
                return false;
            }

            if (!FpgThreatPresentationRules.IsValidForSweptProjectile(
                    threatPresentationKind,
                    projectileInterceptable))
            {
                error =
                    $"Projectile action '{EventId}' presentation kind does not match projectile interceptability.";
                return false;
            }

            if (impactMode
                    == FpgSkillProjectileImpactMode.AreaAtFirstSurface
                && (areaCombatantLimit <= 0
                    || areaProjectileLimit < 0
                    || areaCombatantLimit
                        > int.MaxValue - areaProjectileLimit
                    || !IsValidTargetKinds(AllowedTargetKinds)))
            {
                error =
                    $"Area projectile action '{EventId}' has invalid query or capacity values.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal DamageSpec CompileDamage()
        {
            return new DamageSpec(
                baseDamage,
                breakDamage,
                weakpointDamageMultiplierBasisPoints,
                weakpointBreakMultiplierBasisPoints);
        }

        internal override FpgCompiledSkillActionPresentation
            CompilePresentation(
                FpgSkillActionKind actionKind,
                int actionIndex,
                string scopePrefix)
        {
            if (actionKind != FpgSkillActionKind.LaunchProjectile
                || !HasPresentation)
            {
                throw new ArgumentOutOfRangeException(nameof(actionKind));
            }

            FpgPresentationHandle flight = flightVfx == null
                ? default(FpgPresentationHandle)
                : FpgSkillStableId.CompilePresentationHandle(
                    scopePrefix + ":" + EventId + ":flight.vfx");
            FpgCompiledImpactPresentation collision =
                collisionPresentation == null
                    ? default(FpgCompiledImpactPresentation)
                    : collisionPresentation.Compile(
                        scopePrefix + ":" + EventId + ":collision");
            ulong hash = FpgPresentationAuthoringHash.Begin(22UL);
            hash = StableHash.Append(
                hash,
                flightVfx == null
                    ? 0UL
                    : flightVfx.ComputeContentHash());
            hash = StableHash.Append(
                hash,
                collisionPresentation == null
                    ? 0UL
                    : collisionPresentation.ComputeContentHash());
            return new FpgCompiledSkillActionPresentation(
                actionKind,
                actionIndex,
                default(FpgPresentationHandle),
                default(FpgCompiledImpactPresentation),
                flight,
                collision,
                0,
                hash);
        }

        private static bool IsValidTargetKinds(AttackTargetKinds value)
        {
            const AttackTargetKinds known =
                AttackTargetKinds.All;
            return value != AttackTargetKinds.None && (value & ~known) == 0;
        }
    }

    [Serializable]
    public sealed class FpgSkillReloadEventDefinition :
        FpgSkillGameplayActionDefinition
    {
        [SerializeField]
        private string successAnimationName = string.Empty;

        public string SuccessAnimationName => successAnimationName;

        internal override bool HasPresentation =>
            !string.IsNullOrEmpty(successAnimationName);

        internal override bool TryValidate(int durationTicks, out string error)
        {
            if (!TryValidateHeader(durationTicks, out error))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(successAnimationName)
                && string.IsNullOrWhiteSpace(successAnimationName))
            {
                error =
                    $"Reload action '{EventId}' has an invalid success animation name.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal override FpgCompiledSkillActionPresentation
            CompilePresentation(
                FpgSkillActionKind actionKind,
                int actionIndex,
                string scopePrefix)
        {
            if (actionKind != FpgSkillActionKind.CommitReload
                || !HasPresentation)
            {
                throw new ArgumentOutOfRangeException(nameof(actionKind));
            }

            ulong hash = FpgPresentationAuthoringHash.AppendString(
                FpgPresentationAuthoringHash.Begin(23UL),
                successAnimationName);
            return new FpgCompiledSkillActionPresentation(
                actionKind,
                actionIndex,
                default(FpgPresentationHandle),
                default(FpgCompiledImpactPresentation),
                default(FpgPresentationHandle),
                default(FpgCompiledImpactPresentation),
                FpgSkillStableId.CompileAnimation(successAnimationName),
                hash);
        }
    }

    [Serializable]
    public sealed class FpgSkillSummonEventDefinition :
        FpgSkillGameplayActionDefinition
    {
        [SerializeField]
        private FpgEnemyDefinition[] summonCandidates =
            Array.Empty<FpgEnemyDefinition>();

        [SerializeField]
        private int[] summonCandidateWeights = Array.Empty<int>();

        [SerializeField]
        private FpgSummonOccupancyMode summonOccupancyMode =
            FpgSummonOccupancyMode.AdditionalEntity;

        [SerializeField]
        private FpgSummonPlacementMode summonPlacementMode =
            FpgSummonPlacementMode.EncounterSpawnPoint;

        [SerializeField]
        private FpgSummonOwnerOutcome summonOwnerOutcome =
            FpgSummonOwnerOutcome.RemainAlive;

        [SerializeField, Min(0)]
        private int maxSummonsPerOwner = 2;

        [SerializeField, Min(0)]
        private int maxTotalSummonsPerEncounter = 8;

        [SerializeField, Min(0)]
        private int maxSummonRecursionDepth = 2;

        public IReadOnlyList<FpgEnemyDefinition> SummonCandidates =>
            summonCandidates ?? Array.Empty<FpgEnemyDefinition>();
        public IReadOnlyList<int> SummonCandidateWeights =>
            summonCandidateWeights ?? Array.Empty<int>();
        public FpgSummonOccupancyMode SummonOccupancyMode =>
            summonOccupancyMode;
        public FpgSummonPlacementMode SummonPlacementMode =>
            summonPlacementMode;
        public FpgSummonOwnerOutcome SummonOwnerOutcome => summonOwnerOutcome;
        public int MaxSummonsPerOwner => maxSummonsPerOwner;
        public int MaxTotalSummonsPerEncounter => maxTotalSummonsPerEncounter;
        public int MaxSummonRecursionDepth => maxSummonRecursionDepth;

        public int GetSummonCandidateWeight(int index)
        {
            IReadOnlyList<FpgEnemyDefinition> candidates = SummonCandidates;
            if (index < 0 || index >= candidates.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            IReadOnlyList<int> weights = SummonCandidateWeights;
            return weights.Count == 0 ? 1 : weights[index];
        }

        internal override bool TryValidate(int durationTicks, out string error)
        {
            if (!TryValidateHeader(durationTicks, out error))
            {
                return false;
            }

            IReadOnlyList<FpgEnemyDefinition> candidates = SummonCandidates;
            IReadOnlyList<int> weights = SummonCandidateWeights;
            if (candidates.Count == 0)
            {
                error =
                    $"Summon action '{EventId}' requires at least one candidate enemy.";
                return false;
            }

            if (weights.Count != 0 && weights.Count != candidates.Count)
            {
                error =
                    $"Summon action '{EventId}' candidate weights must be empty or match candidate count.";
                return false;
            }

            if (!Enum.IsDefined(
                    typeof(FpgSummonOccupancyMode),
                    summonOccupancyMode)
                || !Enum.IsDefined(
                    typeof(FpgSummonPlacementMode),
                    summonPlacementMode)
                || !Enum.IsDefined(
                    typeof(FpgSummonOwnerOutcome),
                    summonOwnerOutcome)
                || maxSummonsPerOwner < 0
                || maxTotalSummonsPerEncounter < 0
                || maxSummonRecursionDepth < 0
                || maxSummonRecursionDepth
                    > FpgFormalConfigValidation.DefaultMaxSummonGraphDepth)
            {
                error =
                    $"Summon action '{EventId}' has invalid policies or hard limits.";
                return false;
            }

            if (summonOccupancyMode
                    == FpgSummonOccupancyMode.AdditionalEntity
                && (maxSummonsPerOwner <= 0
                    || maxTotalSummonsPerEncounter <= 0))
            {
                error =
                    $"Summon action '{EventId}' requires positive gameplay quotas when it adds an entity.";
                return false;
            }

            if (summonOccupancyMode == FpgSummonOccupancyMode.ReplaceOwner
                && (maxSummonsPerOwner != 0
                    || maxTotalSummonsPerEncounter != 0))
            {
                error =
                    $"Summon action '{EventId}' must leave gameplay quotas at zero when it replaces its owner.";
                return false;
            }

            bool replacesOwner = summonOccupancyMode
                == FpgSummonOccupancyMode.ReplaceOwner;
            bool killsOwner = summonOwnerOutcome
                == FpgSummonOwnerOutcome.DieAfterSuccessfulSummon;
            if (replacesOwner != killsOwner)
            {
                error =
                    $"Summon action '{EventId}' must pair ReplaceOwner with DieAfterSuccessfulSummon.";
                return false;
            }

            HashSet<string> candidateIds =
                new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < candidates.Count; index++)
            {
                FpgEnemyDefinition candidate = candidates[index];
                if (candidate == null
                    || string.IsNullOrWhiteSpace(candidate.EnemyDefinitionId)
                    || !candidateIds.Add(candidate.EnemyDefinitionId))
                {
                    error =
                        $"Summon action '{EventId}' contains a missing, duplicate or invalid candidate ID.";
                    return false;
                }

                if (weights.Count > 0 && weights[index] <= 0)
                {
                    error =
                        $"Summon action '{EventId}' candidate weight {index} must be positive.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
