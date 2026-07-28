using System;
using System.Collections.Generic;
using FPG.Demo.Enemy;
using FPG.Demo.Run;

namespace FPG.Demo.Unity
{
    public enum FpgEnemySkillActionKind
    {
        None = 0,
        Projectile,
        TimedImpact,
        Summon,
        SelfDestructOwner
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
                        || maxTotalSummonsPerEncounter != 0)))
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

    public readonly struct FpgCompiledEnemySkillAction
    {
        private FpgCompiledEnemySkillAction(
            int actionId,
            FpgEnemySkillActionKind kind,
            int threatDefinitionId,
            ThreatPayloadDefinition threatPayload,
            FpgCompiledEnemySummonPayload summonPayload,
            int boundGameplayEventId)
        {
            ActionId = actionId;
            Kind = kind;
            ThreatDefinitionId = threatDefinitionId;
            ThreatPayload = threatPayload;
            SummonPayload = summonPayload;
            BoundGameplayEventId = boundGameplayEventId;
        }

        public int ActionId { get; }
        public FpgEnemySkillActionKind Kind { get; }
        public int ThreatDefinitionId { get; }
        public ThreatPayloadDefinition ThreatPayload { get; }
        public FpgCompiledEnemySummonPayload SummonPayload { get; }
        public int BoundGameplayEventId { get; }
        public int ProjectileCapacity =>
            Kind == FpgEnemySkillActionKind.Projectile
                ? ThreatPayload.PayloadCount
                : 0;
        public int ImpactCapacity =>
            Kind == FpgEnemySkillActionKind.Projectile
                ? ThreatPayload.PayloadCount
                : Kind == FpgEnemySkillActionKind.TimedImpact ? 1 : 0;
        public int SummonCapacity =>
            Kind == FpgEnemySkillActionKind.Summon ? 1 : 0;
        public int MaxHitCount => ImpactCapacity;

        internal static FpgCompiledEnemySkillAction ForThreat(
            int actionId,
            FpgEnemySkillActionKind kind,
            int threatDefinitionId,
            ThreatPayloadDefinition threatPayload)
        {
            if (actionId <= 0
                || (kind != FpgEnemySkillActionKind.Projectile
                    && kind != FpgEnemySkillActionKind.TimedImpact)
                || threatDefinitionId <= 0
                || !threatPayload.IsValid
                || (kind == FpgEnemySkillActionKind.Projectile
                    && !threatPayload.IsSweptProjectile)
                || (kind == FpgEnemySkillActionKind.TimedImpact
                    && !threatPayload.IsTimedImpact))
            {
                throw new ArgumentException("Compiled enemy threat payload is invalid.");
            }

            return new FpgCompiledEnemySkillAction(
                actionId,
                kind,
                threatDefinitionId,
                threatPayload,
                null,
                0);
        }

        internal static FpgCompiledEnemySkillAction ForSummon(
            int actionId,
            FpgCompiledEnemySummonPayload summonPayload)
        {
            if (actionId <= 0 || summonPayload == null)
            {
                throw new ArgumentException("Compiled enemy summon payload is invalid.");
            }

            return new FpgCompiledEnemySkillAction(
                actionId,
                FpgEnemySkillActionKind.Summon,
                0,
                default(ThreatPayloadDefinition),
                summonPayload,
                0);
        }

        internal static FpgCompiledEnemySkillAction
            ForSelfDestructOwner(
                int actionId,
                int boundGameplayEventId)
        {
            if (actionId <= 0 || boundGameplayEventId < 0)
            {
                throw new ArgumentException(
                    "Compiled enemy self-destruct action is invalid.");
            }

            return new FpgCompiledEnemySkillAction(
                actionId,
                FpgEnemySkillActionKind.SelfDestructOwner,
                0,
                default(ThreatPayloadDefinition),
                null,
                boundGameplayEventId);
        }
    }
}
