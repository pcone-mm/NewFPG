using System;
using FPG.Demo.Run;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Projects Unity authoring assets into immutable FPG.Run data. Keeping the
    /// conversion here prevents the deterministic planner from referencing
    /// UnityEngine or ScriptableObject instances.
    /// </summary>
    public static class FpgFormalConfigAdapters
    {
        public static bool TryBuildEnemyData(
            FpgEnemyDefinition enemy,
            out FpgEnemyDefinitionData data,
            out string error)
        {
            data = null;
            if (enemy == null)
            {
                error = "Formal enemy asset is missing.";
                return false;
            }

            if (!enemy.TryValidate(out error))
            {
                return false;
            }

            int maxSummons = 0;
            int maxSummonDepth = 0;
            bool hasSummonAction = false;
            for (int index = 0; index < enemy.AttackPatternCount; index++)
            {
                FpgEnemyAttackDefinition attack = enemy.GetAttackPattern(index);
                if (attack == null)
                {
                    continue;
                }

                for (int payloadIndex = 0;
                    payloadIndex < attack.PayloadSlots.Count;
                    payloadIndex++)
                {
                    FpgEnemySkillPayloadSlot payload =
                        attack.PayloadSlots[payloadIndex];
                    if (payload == null
                        || payload.Kind != FpgEnemySkillPayloadKind.Summon)
                    {
                        continue;
                    }

                    hasSummonAction = true;
                    if (payload.SummonOccupancyMode
                        == FpgSummonOccupancyMode.AdditionalEntity)
                    {
                        maxSummons = Math.Max(
                            maxSummons,
                            payload.MaxTotalSummonsPerEncounter);
                    }

                    maxSummonDepth = Math.Max(
                        maxSummonDepth,
                        payload.MaxSummonRecursionDepth);
                }
            }

            try
            {
                data = new FpgEnemyDefinitionData(
                    enemy.EnemyDefinitionId,
                    enemy.Role,
                    enemy.Life,
                    enemy.BreakValue,
                    enemy.SpawnCost,
                    enemy.CapWeight,
                    maxSummons,
                    maxSummonDepth,
                    enemy.EnemyDefinitionId + ":entity",
                    enemy.Behavior == null ? string.Empty : enemy.Behavior.BehaviorId,
                    enemy.AttackPatternCount == 0
                        ? string.Empty
                        : enemy.GetAttackPattern(0).SkillId,
                    hasSummonAction);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                data = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool TryBuildPoolEntryData(
            FpgEnemyPoolEntryDefinition entry,
            out FpgEnemyPoolEntryData data,
            out string error)
        {
            data = default(FpgEnemyPoolEntryData);
            if (entry == null)
            {
                error = "Formal pool entry is missing.";
                return false;
            }

            if (!TryBuildEnemyData(entry.Enemy, out FpgEnemyDefinitionData enemyData, out error))
            {
                return false;
            }

            try
            {
                data = new FpgEnemyPoolEntryData(
                    enemyData,
                    entry.SelectionWeight,
                    entry.MinDepth,
                    entry.MaxDepth,
                    entry.MaxPerWave,
                    entry.MaxPerRoom,
                    entry.ThemeEligible);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                data = default(FpgEnemyPoolEntryData);
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
