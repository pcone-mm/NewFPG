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
                    for (int payloadIndex = 0;
                        payloadIndex < attack.PayloadSlots.Count;
                        payloadIndex++)
                    {
                        FpgEnemySkillPayloadSlot payload =
                            attack.PayloadSlots[payloadIndex];
                        if (payload == null
                            || payload.Kind
                                != FpgEnemySkillPayloadKind.Summon)
                        {
                            continue;
                        }

                        int projectionIndex = FindSummonProjection(
                            summonPayloads,
                            payload.SlotId);
                        if (projectionIndex < 0)
                        {
                            summonPayloads.Add(
                                new SummonProjection(
                                    payload,
                                    owner.EnemyDefinitionId));
                        }
                        else if (!summonPayloads[projectionIndex]
                            .TryAddOwner(
                                payload,
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
                FpgEnemyDefinition[] candidates =
                    action.Payload.SummonCandidates;
                List<string> candidateIds = new List<string>(candidates.Length);
                for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                {
                    candidateIds.Add(candidates[candidateIndex].EnemyDefinitionId);
                }

                try
                {
                    projected.Add(new FpgSummonActionData(
                        action.Payload.SlotId,
                        action.OwnerIds,
                        candidateIds,
                        action.Payload.MaxSummonsPerOwner,
                        action.Payload.MaxTotalSummonsPerEncounter,
                        action.Payload.MaxSummonRecursionDepth,
                        action.Payload.SummonOccupancyMode,
                        action.Payload.SummonPlacementMode));
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
                    for (int payloadIndex = 0;
                        payloadIndex < attack.PayloadSlots.Count;
                        payloadIndex++)
                    {
                        FpgEnemySkillPayloadSlot payload =
                            attack.PayloadSlots[payloadIndex];
                        if (payload == null
                            || payload.Kind
                                != FpgEnemySkillPayloadKind.Summon)
                        {
                            continue;
                        }

                        FpgEnemyDefinition[] candidates =
                            payload.SummonCandidates;
                        for (int candidateIndex = 0;
                            candidateIndex < candidates.Length;
                            candidateIndex++)
                        {
                            FpgEnemyDefinition candidate =
                                candidates[candidateIndex];
                            if (!members.Contains(candidate))
                            {
                                error = $"Summon payload '{payload.SlotId}' on '{owner.EnemyDefinitionId}' references "
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
                    values[index].Payload.SlotId,
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
                FpgEnemySkillPayloadSlot payload,
                string ownerId)
            {
                Payload = payload;
                ownerIds.Add(ownerId);
            }

            public FpgEnemySkillPayloadSlot Payload { get; }
            public IReadOnlyList<string> OwnerIds => ownerIds;

            public bool TryAddOwner(
                FpgEnemySkillPayloadSlot payload,
                string ownerId,
                out string error)
            {
                if (!HasEquivalentSummonContract(Payload, payload))
                {
                    error = $"Formal enemy catalog repeats summon payload ID '{payload.SlotId}' with different candidates or policies.";
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
                FpgEnemySkillPayloadSlot left,
                FpgEnemySkillPayloadSlot right)
            {
                if (left.SummonOccupancyMode
                        != right.SummonOccupancyMode
                    || left.SummonPlacementMode
                        != right.SummonPlacementMode
                    || left.SummonOwnerOutcome
                        != right.SummonOwnerOutcome
                    || left.MaxSummonsPerOwner
                        != right.MaxSummonsPerOwner
                    || left.MaxTotalSummonsPerEncounter
                        != right.MaxTotalSummonsPerEncounter
                    || left.MaxSummonRecursionDepth
                        != right.MaxSummonRecursionDepth
                    || left.SummonCandidates.Length
                        != right.SummonCandidates.Length)
                {
                    return false;
                }

                for (int index = 0;
                    index < left.SummonCandidates.Length;
                    index++)
                {
                    if (left.SummonCandidates[index]
                            != right.SummonCandidates[index]
                        || left.GetSummonCandidateWeight(index)
                            != right.GetSummonCandidateWeight(index))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
