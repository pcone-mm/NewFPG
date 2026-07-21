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

            List<FpgSummonActionDefinition> actionAssets = new List<FpgSummonActionDefinition>();
            for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
            {
                FpgEnemyDefinition owner = definitions[definitionIndex];
                for (int attackIndex = 0; attackIndex < owner.AttackPatternCount; attackIndex++)
                {
                    FpgSummonActionDefinition summon = owner.GetAttackPattern(attackIndex).Summon;
                    if (summon != null && !actionAssets.Contains(summon))
                    {
                        actionAssets.Add(summon);
                    }
                }
            }

            List<FpgSummonActionData> projected = new List<FpgSummonActionData>(actionAssets.Count);
            HashSet<string> actionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int actionIndex = 0; actionIndex < actionAssets.Count; actionIndex++)
            {
                FpgSummonActionDefinition action = actionAssets[actionIndex];
                if (!actionIds.Add(action.ActionId))
                {
                    error = $"Formal enemy catalog repeats summon action ID '{action.ActionId}'.";
                    return false;
                }

                List<string> ownerIds = new List<string>();
                for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
                {
                    FpgEnemyDefinition owner = definitions[definitionIndex];
                    bool ownsAction = false;
                    for (int attackIndex = 0; attackIndex < owner.AttackPatternCount; attackIndex++)
                    {
                        if (owner.GetAttackPattern(attackIndex).Summon == action)
                        {
                            ownsAction = true;
                            break;
                        }
                    }

                    if (ownsAction)
                    {
                        ownerIds.Add(owner.EnemyDefinitionId);
                    }
                }

                FpgEnemyDefinition[] candidates = action.CandidateEnemies;
                List<string> candidateIds = new List<string>(candidates.Length);
                for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                {
                    candidateIds.Add(candidates[candidateIndex].EnemyDefinitionId);
                }

                try
                {
                    projected.Add(new FpgSummonActionData(
                        action.ActionId,
                        ownerIds,
                        candidateIds,
                        action.MaxSummonsPerOwner,
                        action.MaxTotalSummonsPerEncounter,
                        action.MaxRecursionDepth));
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
                    FpgSummonActionDefinition summon = owner.GetAttackPattern(attackIndex).Summon;
                    if (summon == null)
                    {
                        continue;
                    }

                    FpgEnemyDefinition[] candidates = summon.CandidateEnemies;
                    for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                    {
                        FpgEnemyDefinition candidate = candidates[candidateIndex];
                        if (!members.Contains(candidate))
                        {
                            error = $"Summon action '{summon.ActionId}' on '{owner.EnemyDefinitionId}' references "
                                + $"'{candidate.EnemyDefinitionId}', which is not in this enemy catalog.";
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}


