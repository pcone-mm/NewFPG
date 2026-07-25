using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum FpgEnemySkillPayloadKind
    {
        None = 0,
        Projectile,
        TimedImpact,
        Summon
    }

    [Serializable]
    public sealed class FpgEnemySkillPayloadSlot
    {
        [SerializeField]
        private string slotId = "payload.enemy";

        [SerializeField]
        private string displayName = "Enemy Payload";

        [SerializeField]
        private FpgEnemySkillPayloadKind kind =
            FpgEnemySkillPayloadKind.Projectile;

        [Header("Threat")]
        [SerializeField, Min(1)]
        private int threatDefinitionId = 1;

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
        private int projectilePresentationKey = 1;

        [SerializeField, Min(1)]
        private int projectileSweepRadiusKey = 1;

        [Header("Timed Impact")]
        [SerializeField]
        private ThreatTargetPolicy timedImpactTargetPolicy =
            ThreatTargetPolicy.PlayerCombatant;

        [SerializeField, Min(0)]
        private int timedImpactDelayTicks;

        [SerializeField, Min(1)]
        private int timedImpactPresentationKey = 1;

        [Header("Summon")]
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

        public string SlotId => slotId;
        public string DisplayName => displayName;
        public FpgEnemySkillPayloadKind Kind => kind;
        public int ThreatDefinitionId => threatDefinitionId;
        public int BaseDamage => baseDamage;
        public int BreakDamage => breakDamage;
        public int WeakpointDamageMultiplierBasisPoints =>
            weakpointDamageMultiplierBasisPoints;
        public int WeakpointBreakMultiplierBasisPoints =>
            weakpointBreakMultiplierBasisPoints;
        public int ProjectileDefinitionId => projectileDefinitionId;
        public int ProjectileCount => projectileCount;
        public int ProjectileFlightTicks => projectileFlightTicks;
        public int ProjectileLifetimeTicks => projectileLifetimeTicks;
        public int ProjectileMaxHitPoints => projectileMaxHitPoints;
        public bool ProjectileInterceptable => projectileInterceptable;
        public int ProjectileBudgetUnits => projectileBudgetUnits;
        public int ProjectilePresentationKey => projectilePresentationKey;
        public int ProjectileSweepRadiusKey => projectileSweepRadiusKey;
        public ThreatTargetPolicy TimedImpactTargetPolicy =>
            timedImpactTargetPolicy;
        public int TimedImpactDelayTicks => timedImpactDelayTicks;
        public int TimedImpactPresentationKey => timedImpactPresentationKey;
        public FpgEnemyDefinition[] SummonCandidates =>
            summonCandidates ?? Array.Empty<FpgEnemyDefinition>();
        public int[] SummonCandidateWeights =>
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
            if (index < 0 || index >= SummonCandidates.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return summonCandidateWeights == null
                || summonCandidateWeights.Length == 0
                    ? 1
                    : summonCandidateWeights[index];
        }

        public bool TryValidate(out string error)
        {
            if (!FpgSkillStableId.IsValid(slotId)
                || string.IsNullOrWhiteSpace(displayName)
                || !Enum.IsDefined(typeof(FpgEnemySkillPayloadKind), kind)
                || kind == FpgEnemySkillPayloadKind.None)
            {
                error = "Enemy skill payload requires a stable slot ID, display name and valid kind.";
                return false;
            }

            switch (kind)
            {
                case FpgEnemySkillPayloadKind.Projectile:
                    return TryValidateProjectile(out error);

                case FpgEnemySkillPayloadKind.TimedImpact:
                    return TryValidateTimedImpact(out error);

                case FpgEnemySkillPayloadKind.Summon:
                    return TryValidateSummon(out error);

                default:
                    error = $"Enemy skill payload '{slotId}' has an unsupported kind.";
                    return false;
            }
        }

        internal FpgCompiledEnemySkillPayloadSlot Compile()
        {
            int compiledSlotId = FpgSkillStableId.CompilePayloadSlot(slotId);
            switch (kind)
            {
                case FpgEnemySkillPayloadKind.Projectile:
                {
                    DamageSpec damage = CompileDamage();
                    ProjectileDefinition projectile = new ProjectileDefinition(
                        projectileDefinitionId,
                        new TickDuration(projectileFlightTicks),
                        new TickDuration(projectileLifetimeTicks),
                        damage,
                        projectileMaxHitPoints,
                        projectileInterceptable,
                        projectileBudgetUnits,
                        projectilePresentationKey,
                        projectileSweepRadiusKey);
                    return FpgCompiledEnemySkillPayloadSlot.ForThreat(
                        compiledSlotId,
                        kind,
                        threatDefinitionId,
                        ThreatPayloadDefinition.SweptProjectile(
                            projectile,
                            projectileCount));
                }

                case FpgEnemySkillPayloadKind.TimedImpact:
                    return FpgCompiledEnemySkillPayloadSlot.ForThreat(
                        compiledSlotId,
                        kind,
                        threatDefinitionId,
                        ThreatPayloadDefinition.TimedImpact(
                            CompileDamage(),
                            timedImpactTargetPolicy,
                            new TickDuration(timedImpactDelayTicks),
                            timedImpactPresentationKey));

                case FpgEnemySkillPayloadKind.Summon:
                {
                    FpgEnemyDefinition[] candidates = SummonCandidates;
                    FpgCompiledEnemySummonCandidate[] compiled =
                        new FpgCompiledEnemySummonCandidate[candidates.Length];
                    for (int index = 0; index < candidates.Length; index++)
                    {
                        compiled[index] = new FpgCompiledEnemySummonCandidate(
                            candidates[index],
                            GetSummonCandidateWeight(index));
                    }

                    return FpgCompiledEnemySkillPayloadSlot.ForSummon(
                        compiledSlotId,
                        new FpgCompiledEnemySummonPayload(
                            compiledSlotId,
                            slotId,
                            compiled,
                            summonOccupancyMode,
                            summonPlacementMode,
                            summonOwnerOutcome,
                            maxSummonsPerOwner,
                            maxTotalSummonsPerEncounter,
                            maxSummonRecursionDepth));
                }

                default:
                    throw new InvalidOperationException(
                        $"Unsupported enemy skill payload kind '{kind}'.");
            }
        }

        private bool TryValidateProjectile(out string error)
        {
            if (!TryValidateDamage()
                || threatDefinitionId <= 0
                || projectileDefinitionId <= 0
                || projectileCount <= 0
                || projectileFlightTicks <= 0
                || projectileLifetimeTicks < projectileFlightTicks
                || projectileMaxHitPoints < 0
                || (projectileInterceptable && projectileMaxHitPoints <= 0)
                || projectileBudgetUnits <= 0
                || projectilePresentationKey <= 0
                || projectileSweepRadiusKey <= 0
                || projectileCount > int.MaxValue / projectileBudgetUnits)
            {
                error = $"Projectile payload '{slotId}' has invalid damage, timing or capacity values.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateTimedImpact(out string error)
        {
            if (!TryValidateDamage()
                || threatDefinitionId <= 0
                || !Enum.IsDefined(
                    typeof(ThreatTargetPolicy),
                    timedImpactTargetPolicy)
                || timedImpactDelayTicks < 0
                || timedImpactPresentationKey <= 0)
            {
                error = $"Timed-impact payload '{slotId}' has invalid damage, timing or presentation values.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateSummon(out string error)
        {
            FpgEnemyDefinition[] candidates = SummonCandidates;
            int[] weights = SummonCandidateWeights;
            if (candidates.Length == 0)
            {
                error = $"Summon payload '{slotId}' requires at least one candidate enemy.";
                return false;
            }

            if (weights.Length != 0 && weights.Length != candidates.Length)
            {
                error = $"Summon payload '{slotId}' candidate weights must be empty or match candidate count.";
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
                error = $"Summon payload '{slotId}' has invalid policies or hard limits.";
                return false;
            }

            if (summonOccupancyMode == FpgSummonOccupancyMode.AdditionalEntity
                && (maxSummonsPerOwner <= 0
                    || maxTotalSummonsPerEncounter <= 0))
            {
                error = $"Summon payload '{slotId}' requires positive gameplay quotas when it adds an entity.";
                return false;
            }

            if (summonOccupancyMode == FpgSummonOccupancyMode.ReplaceOwner
                && (maxSummonsPerOwner != 0
                    || maxTotalSummonsPerEncounter != 0))
            {
                error = $"Summon payload '{slotId}' must leave gameplay quotas at zero when it replaces its owner.";
                return false;
            }

            bool replacesOwner = summonOccupancyMode
                == FpgSummonOccupancyMode.ReplaceOwner;
            bool killsOwner = summonOwnerOutcome
                == FpgSummonOwnerOutcome.DieAfterSuccessfulSummon;
            if (replacesOwner != killsOwner)
            {
                error = $"Summon payload '{slotId}' must pair ReplaceOwner with DieAfterSuccessfulSummon.";
                return false;
            }

            HashSet<string> candidateIds =
                new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < candidates.Length; index++)
            {
                FpgEnemyDefinition candidate = candidates[index];
                if (candidate == null
                    || string.IsNullOrWhiteSpace(candidate.EnemyDefinitionId)
                    || !candidateIds.Add(candidate.EnemyDefinitionId))
                {
                    error = $"Summon payload '{slotId}' contains a missing, duplicate or invalid candidate ID.";
                    return false;
                }

                if (weights.Length > 0 && weights[index] <= 0)
                {
                    error = $"Summon payload '{slotId}' candidate weight {index} must be positive.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateDamage()
        {
            return baseDamage >= 0
                && breakDamage >= 0
                && weakpointDamageMultiplierBasisPoints >= 0
                && weakpointBreakMultiplierBasisPoints >= 0;
        }

        private DamageSpec CompileDamage()
        {
            return new DamageSpec(
                baseDamage,
                breakDamage,
                weakpointDamageMultiplierBasisPoints,
                weakpointBreakMultiplierBasisPoints);
        }
    }

    public readonly struct FpgCompiledEnemySummonCandidate
    {
        internal FpgCompiledEnemySummonCandidate(
            FpgEnemyDefinition definition,
            int weight)
        {
            Definition = definition != null
                ? definition
                : throw new ArgumentNullException(nameof(definition));
            if (weight <= 0
                || string.IsNullOrWhiteSpace(definition.EnemyDefinitionId))
            {
                throw new ArgumentOutOfRangeException(nameof(weight));
            }

            EnemyDefinitionId = definition.EnemyDefinitionId;
            Weight = weight;
        }

        public FpgEnemyDefinition Definition { get; }
        public string EnemyDefinitionId { get; }
        public int Weight { get; }
    }

    public sealed class FpgCompiledEnemySummonPayload
    {
        private readonly FpgCompiledEnemySummonCandidate[] candidates;

        internal FpgCompiledEnemySummonPayload(
            int actionStableId,
            string actionId,
            FpgCompiledEnemySummonCandidate[] candidates,
            FpgSummonOccupancyMode occupancyMode,
            FpgSummonPlacementMode placementMode,
            FpgSummonOwnerOutcome ownerOutcome,
            int maxSummonsPerOwner,
            int maxTotalSummonsPerEncounter,
            int maxRecursionDepth)
        {
            if (actionStableId <= 0
                || !FpgSkillStableId.IsValid(actionId)
                || candidates == null
                || candidates.Length == 0
                || !Enum.IsDefined(typeof(FpgSummonOccupancyMode), occupancyMode)
                || !Enum.IsDefined(typeof(FpgSummonPlacementMode), placementMode)
                || !Enum.IsDefined(typeof(FpgSummonOwnerOutcome), ownerOutcome)
                || maxSummonsPerOwner < 0
                || maxTotalSummonsPerEncounter < 0
                || maxRecursionDepth < 0
                || maxRecursionDepth
                    > FpgFormalConfigValidation.DefaultMaxSummonGraphDepth
                || (occupancyMode == FpgSummonOccupancyMode.AdditionalEntity
                    && (maxSummonsPerOwner <= 0
                        || maxTotalSummonsPerEncounter <= 0))
                || (occupancyMode == FpgSummonOccupancyMode.ReplaceOwner
                    && (maxSummonsPerOwner != 0
                        || maxTotalSummonsPerEncounter != 0))
                || (occupancyMode == FpgSummonOccupancyMode.ReplaceOwner)
                    != (ownerOutcome
                        == FpgSummonOwnerOutcome.DieAfterSuccessfulSummon))
            {
                throw new ArgumentException("Compiled enemy summon payload is invalid.");
            }

            this.candidates =
                new FpgCompiledEnemySummonCandidate[candidates.Length];
            Array.Copy(candidates, this.candidates, candidates.Length);
            ActionStableId = actionStableId;
            ActionId = actionId;
            OccupancyMode = occupancyMode;
            PlacementMode = placementMode;
            OwnerOutcome = ownerOutcome;
            MaxSummonsPerOwner = maxSummonsPerOwner;
            MaxTotalSummonsPerEncounter = maxTotalSummonsPerEncounter;
            MaxRecursionDepth = maxRecursionDepth;

            ulong totalWeight = 0UL;
            for (int index = 0; index < this.candidates.Length; index++)
            {
                totalWeight = checked(
                    totalWeight + unchecked((ulong)this.candidates[index].Weight));
            }

            TotalCandidateWeight = totalWeight;
        }

        public int ActionStableId { get; }
        public string ActionId { get; }
        public IReadOnlyList<FpgCompiledEnemySummonCandidate> Candidates =>
            candidates;
        public int CandidateCount => candidates.Length;
        public ulong TotalCandidateWeight { get; }
        public FpgSummonOccupancyMode OccupancyMode { get; }
        public FpgSummonPlacementMode PlacementMode { get; }
        public FpgSummonOwnerOutcome OwnerOutcome { get; }
        public int MaxSummonsPerOwner { get; }
        public int MaxTotalSummonsPerEncounter { get; }
        public int MaxRecursionDepth { get; }
        public bool UsesEncounterSpawnQueue => true;

        public FpgCompiledEnemySummonCandidate GetCandidate(int index)
        {
            if (index < 0 || index >= candidates.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return candidates[index];
        }
    }

    public readonly struct FpgCompiledEnemySkillPayloadSlot
    {
        private FpgCompiledEnemySkillPayloadSlot(
            int slotId,
            FpgEnemySkillPayloadKind kind,
            int threatDefinitionId,
            ThreatPayloadDefinition threatPayload,
            FpgCompiledEnemySummonPayload summonPayload)
        {
            SlotId = slotId;
            Kind = kind;
            ThreatDefinitionId = threatDefinitionId;
            ThreatPayload = threatPayload;
            SummonPayload = summonPayload;
        }

        public int SlotId { get; }
        public FpgEnemySkillPayloadKind Kind { get; }
        public int ThreatDefinitionId { get; }
        public ThreatPayloadDefinition ThreatPayload { get; }
        public FpgCompiledEnemySummonPayload SummonPayload { get; }
        public int ProjectileCapacity =>
            Kind == FpgEnemySkillPayloadKind.Projectile
                ? ThreatPayload.PayloadCount
                : 0;
        public int ImpactCapacity =>
            Kind == FpgEnemySkillPayloadKind.Projectile
                ? ThreatPayload.PayloadCount
                : Kind == FpgEnemySkillPayloadKind.TimedImpact ? 1 : 0;
        public int SummonCapacity =>
            Kind == FpgEnemySkillPayloadKind.Summon ? 1 : 0;
        public int MaxHitCount => ImpactCapacity;

        internal static FpgCompiledEnemySkillPayloadSlot ForThreat(
            int slotId,
            FpgEnemySkillPayloadKind kind,
            int threatDefinitionId,
            ThreatPayloadDefinition threatPayload)
        {
            if (slotId <= 0
                || (kind != FpgEnemySkillPayloadKind.Projectile
                    && kind != FpgEnemySkillPayloadKind.TimedImpact)
                || threatDefinitionId <= 0
                || !threatPayload.IsValid
                || (kind == FpgEnemySkillPayloadKind.Projectile
                    && !threatPayload.IsSweptProjectile)
                || (kind == FpgEnemySkillPayloadKind.TimedImpact
                    && !threatPayload.IsTimedImpact))
            {
                throw new ArgumentException("Compiled enemy threat payload is invalid.");
            }

            return new FpgCompiledEnemySkillPayloadSlot(
                slotId,
                kind,
                threatDefinitionId,
                threatPayload,
                null);
        }

        internal static FpgCompiledEnemySkillPayloadSlot ForSummon(
            int slotId,
            FpgCompiledEnemySummonPayload summonPayload)
        {
            if (slotId <= 0 || summonPayload == null)
            {
                throw new ArgumentException("Compiled enemy summon payload is invalid.");
            }

            return new FpgCompiledEnemySkillPayloadSlot(
                slotId,
                FpgEnemySkillPayloadKind.Summon,
                0,
                default(ThreatPayloadDefinition),
                summonPayload);
        }
    }
}
