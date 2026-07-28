using System;
using System.Collections.Generic;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Explicit Unity-side lookup for formal enemy definitions. The catalog is
    /// a serialized reference list; formal runtime code never searches by
    /// Resources path or scene name.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FpgEnemyDefinitionCatalog",
        menuName = "FPG Demo/Formal Encounter/Enemy Catalog")]
    public sealed class FpgEnemyDefinitionCatalog : ScriptableObject, IFpgEnemyDefinitionCatalog, IFpgSummonGraphCatalog
    {
        [SerializeField]
        private FpgEnemyDefinition[] definitions = Array.Empty<FpgEnemyDefinition>();

        public IReadOnlyList<FpgEnemyDefinition> Definitions =>
            definitions ?? Array.Empty<FpgEnemyDefinition>();

        public int Count => definitions == null ? 0 : definitions.Length;

        public bool TryGet(string enemyDefinitionId, out FpgEnemyDefinitionData definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(enemyDefinitionId) || definitions == null)
            {
                return false;
            }

            for (int index = 0; index < definitions.Length; index++)
            {
                FpgEnemyDefinition candidate = definitions[index];
                if (candidate == null
                    || !string.Equals(candidate.EnemyDefinitionId, enemyDefinitionId, StringComparison.Ordinal))
                {
                    continue;
                }

                return FpgFormalConfigAdapters.TryBuildEnemyData(
                    candidate,
                    out definition,
                    out _);
            }

            return false;
        }

        public bool TryBuildSummonGraph(
            out IReadOnlyList<FpgSummonActionData> actions,
            out string error)
        {
            actions = Array.Empty<FpgSummonActionData>();
            if (!TryValidate(out error))
            {
                return false;
            }

            List<SummonProjection> summonPayloads =
                new List<SummonProjection>();
            for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
            {
                FpgEnemyDefinition owner = definitions[definitionIndex];
                for (int attackIndex = 0; attackIndex < owner.AttackPatternCount; attackIndex++)
                {
                    FpgEnemyAttackDefinition attack =
                        owner.GetAttackPattern(attackIndex);
                    if (!attack.TryCompile(
                            out FpgCompiledEnemySkillDefinition compiledAttack,
                            out error))
                    {
                        error = $"Enemy skill '{attack.SkillId}' cannot compile while building the summon graph: {error}";
                        return false;
                    }

                    for (int actionIndex = 0;
                        actionIndex < compiledAttack.SummonActions.Count;
                        actionIndex++)
                    {
                        FpgCompiledEnemySummonPayload summon =
                            compiledAttack.SummonActions[actionIndex]
                                .SummonPayload;
                        int projectionIndex = FindSummonProjection(
                            summonPayloads,
                            summon.ActionId);
                        if (projectionIndex < 0)
                        {
                            summonPayloads.Add(
                                new SummonProjection(
                                    summon,
                                    owner.EnemyDefinitionId));
                        }
                        else if (!summonPayloads[projectionIndex]
                            .TryAddOwner(
                                summon,
                                owner.EnemyDefinitionId,
                                out error))
                        {
                            return false;
                        }
                    }
                }
            }

            List<FpgSummonActionData> projected =
                new List<FpgSummonActionData>(summonPayloads.Count);
            for (int actionIndex = 0;
                actionIndex < summonPayloads.Count;
                actionIndex++)
            {
                SummonProjection action = summonPayloads[actionIndex];
                FpgCompiledEnemySummonPayload summon = action.Payload;
                List<string> candidateIds =
                    new List<string>(summon.CandidateCount);
                for (int candidateIndex = 0;
                    candidateIndex < summon.CandidateCount;
                    candidateIndex++)
                {
                    candidateIds.Add(
                        summon.GetCandidate(candidateIndex)
                            .EnemyDefinitionId);
                }

                try
                {
                    projected.Add(new FpgSummonActionData(
                        summon.ActionId,
                        action.OwnerIds,
                        candidateIds,
                        summon.MaxSummonsPerOwner,
                        summon.MaxTotalSummonsPerEncounter,
                        summon.MaxRecursionDepth,
                        summon.OccupancyMode,
                        summon.PlacementMode));
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                    return false;
                }
            }

            actions = projected.ToArray();
            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (definitions == null || definitions.Length == 0)
            {
                error = "Formal enemy catalog requires at least one definition.";
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<FpgEnemyDefinition> members = new HashSet<FpgEnemyDefinition>();
            for (int index = 0; index < definitions.Length; index++)
            {
                FpgEnemyDefinition definition = definitions[index];
                string definitionError = string.Empty;
                bool valid = definition != null && definition.TryValidate(out definitionError);
                if (!valid)
                {
                    error = definition == null
                        ? $"Formal enemy catalog definition {index} is missing."
                        : string.IsNullOrWhiteSpace(definitionError)
                            ? $"Formal enemy catalog definition {index} is invalid."
                            : definitionError;
                    return false;
                }

                if (!ids.Add(definition.EnemyDefinitionId))
                {
                    error = $"Formal enemy catalog repeats ID '{definition.EnemyDefinitionId}'.";
                    return false;
                }

                members.Add(definition);
            }

            for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
            {
                FpgEnemyDefinition owner = definitions[definitionIndex];
                for (int attackIndex = 0; attackIndex < owner.AttackPatternCount; attackIndex++)
                {
                    FpgEnemyAttackDefinition attack =
                        owner.GetAttackPattern(attackIndex);
                    if (!attack.TryCompile(
                            out FpgCompiledEnemySkillDefinition compiledAttack,
                            out error))
                    {
                        error = $"Enemy skill '{attack.SkillId}' cannot compile while validating the enemy catalog: {error}";
                        return false;
                    }

                    for (int actionIndex = 0;
                        actionIndex < compiledAttack.SummonActions.Count;
                        actionIndex++)
                    {
                        FpgCompiledEnemySummonPayload summon =
                            compiledAttack.SummonActions[actionIndex]
                                .SummonPayload;
                        for (int candidateIndex = 0;
                            candidateIndex < summon.CandidateCount;
                            candidateIndex++)
                        {
                            FpgCompiledEnemySummonCandidate candidate =
                                summon.GetCandidate(candidateIndex);
                            if (!members.Contains(candidate.Definition))
                            {
                                error = $"Summon action '{summon.ActionId}' on '{owner.EnemyDefinitionId}' references "
                                    + $"'{candidate.EnemyDefinitionId}', which is not in this enemy catalog.";
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        private static int FindSummonProjection(
            List<SummonProjection> values,
            string actionId)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(
                    values[index].Payload.ActionId,
                    actionId,
                    StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private sealed class SummonProjection
        {
            private readonly List<string> ownerIds = new List<string>();

            public SummonProjection(
                FpgCompiledEnemySummonPayload payload,
                string ownerId)
            {
                Payload = payload;
                ownerIds.Add(ownerId);
            }

            public FpgCompiledEnemySummonPayload Payload { get; }
            public IReadOnlyList<string> OwnerIds => ownerIds;

            public bool TryAddOwner(
                FpgCompiledEnemySummonPayload payload,
                string ownerId,
                out string error)
            {
                if (!HasEquivalentSummonContract(Payload, payload))
                {
                    error = $"Formal enemy catalog repeats summon action ID '{payload.ActionId}' with different candidates or policies.";
                    return false;
                }

                if (!ownerIds.Contains(ownerId))
                {
                    ownerIds.Add(ownerId);
                }

                error = string.Empty;
                return true;
            }

            private static bool HasEquivalentSummonContract(
                FpgCompiledEnemySummonPayload left,
                FpgCompiledEnemySummonPayload right)
            {
                if (left.OccupancyMode != right.OccupancyMode
                    || left.PlacementMode != right.PlacementMode
                    || left.MaxSummonsPerOwner
                        != right.MaxSummonsPerOwner
                    || left.MaxTotalSummonsPerEncounter
                        != right.MaxTotalSummonsPerEncounter
                    || left.MaxRecursionDepth != right.MaxRecursionDepth
                    || left.CandidateCount != right.CandidateCount)
                {
                    return false;
                }

                for (int index = 0;
                    index < left.CandidateCount;
                    index++)
                {
                    FpgCompiledEnemySummonCandidate leftCandidate =
                        left.GetCandidate(index);
                    FpgCompiledEnemySummonCandidate rightCandidate =
                        right.GetCandidate(index);
                    if (leftCandidate.Definition != rightCandidate.Definition
                        || leftCandidate.Weight != rightCandidate.Weight)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
