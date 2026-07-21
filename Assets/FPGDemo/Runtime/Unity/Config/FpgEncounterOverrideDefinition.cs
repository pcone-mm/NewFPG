using System;
using System.Collections.Generic;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [Serializable]
    public sealed class FpgForcedEnemyDefinition
    {
        [SerializeField] private FpgEnemyDefinition enemy;
        [SerializeField, Min(0)] private int count = 1;
        public FpgEnemyDefinition Enemy => enemy;
        public int Count => count;
        public bool TryValidate(int index, out string error)
        {
            error = string.Empty;
            if (enemy == null || string.IsNullOrWhiteSpace(enemy.EnemyDefinitionId) || count < 0)
            {
                error = $"Forced enemy {index} is invalid.";
                return false;
            }
            return true;
        }
    }

    [Serializable]
    public sealed class FpgFixedSpawnDefinition
    {
        [SerializeField, Min(0)] private int waveIndex;
        [SerializeField] private FpgEnemyDefinition enemy;
        [SerializeField, Min(1)] private int count = 1;
        public int WaveIndex => waveIndex;
        public FpgEnemyDefinition Enemy => enemy;
        public int Count => count;
        public bool TryValidate(int index, out string error)
        {
            error = string.Empty;
            if (waveIndex < 0 || count <= 0 || enemy == null
                || string.IsNullOrWhiteSpace(enemy.EnemyDefinitionId))
            {
                error = $"Fixed spawn {index} is invalid.";
                return false;
            }
            return true;
        }
    }

    [CreateAssetMenu(
        fileName = "FpgEncounterOverrideDefinition",
        menuName = "FPG Demo/Formal Encounter/Encounter Override")]
    public sealed class FpgEncounterOverrideDefinition : ScriptableObject, IFpgEncounterOverrideSource
    {
        [SerializeField] private string overrideId = "preview";
        [SerializeField] private FpgEncounterOverrideMode mode = FpgEncounterOverrideMode.Generated;
        [SerializeField] private FpgForcedEnemyDefinition[] forcedEnemies = Array.Empty<FpgForcedEnemyDefinition>();
        [SerializeField] private string[] excludedEnemyDefinitionIds = Array.Empty<string>();
        [SerializeField] private FpgEnemyDefinition[] excludedEnemies = Array.Empty<FpgEnemyDefinition>();
        [SerializeField] private bool lockBudget;
        [SerializeField, Min(0)] private int lockedBudget;
        [SerializeField] private FpgFixedSpawnDefinition[] fixedSpawns = Array.Empty<FpgFixedSpawnDefinition>();

        public string OverrideId => overrideId;
        public FpgEncounterOverrideMode Mode => mode;
        public IReadOnlyList<FpgForcedEnemyDefinition> ForcedEnemies => forcedEnemies ?? Array.Empty<FpgForcedEnemyDefinition>();
        public IReadOnlyList<FpgFixedSpawnDefinition> FixedSpawns => fixedSpawns ?? Array.Empty<FpgFixedSpawnDefinition>();
        public IReadOnlyList<FpgEnemyDefinition> ExcludedEnemies => excludedEnemies ?? Array.Empty<FpgEnemyDefinition>();
        public IReadOnlyList<string> ExcludedEnemyDefinitionIds => excludedEnemyDefinitionIds ?? Array.Empty<string>();
        public bool LockBudget => lockBudget;
        public int LockedBudget => lockedBudget;

        public FpgEncounterOverrideData Data
        {
            get { return TryBuildData(out FpgEncounterOverrideData data, out _) ? data : null; }
        }

        public bool TryBuildData(out FpgEncounterOverrideData data, out string error)
        {
            data = null;
            if (!TryValidate(out error))
            {
                return false;
            }

            FpgForcedEnemyDefinition[] forcedDefinitions = forcedEnemies ?? Array.Empty<FpgForcedEnemyDefinition>();
            FpgFixedSpawnDefinition[] fixedDefinitions = fixedSpawns ?? Array.Empty<FpgFixedSpawnDefinition>();
            List<FpgForcedEnemyCount> forced = new List<FpgForcedEnemyCount>(forcedDefinitions.Length);
            for (int index = 0; index < forcedDefinitions.Length; index++)
            {
                forced.Add(new FpgForcedEnemyCount(forcedDefinitions[index].Enemy.EnemyDefinitionId, forcedDefinitions[index].Count));
            }

            List<string> excluded = new List<string>();
            string[] excludedIds = excludedEnemyDefinitionIds ?? Array.Empty<string>();
            for (int index = 0; index < excludedIds.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(excludedIds[index])) excluded.Add(excludedIds[index].Trim());
            }

            FpgEnemyDefinition[] excludedAssets = excludedEnemies ?? Array.Empty<FpgEnemyDefinition>();
            for (int index = 0; index < excludedAssets.Length; index++)
            {
                if (excludedAssets[index] != null) excluded.Add(excludedAssets[index].EnemyDefinitionId);
            }

            List<FpgFixedSpawnSpec> fixedData = new List<FpgFixedSpawnSpec>(fixedDefinitions.Length);
            for (int index = 0; index < fixedDefinitions.Length; index++)
            {
                fixedData.Add(new FpgFixedSpawnSpec(
                    fixedDefinitions[index].Enemy.EnemyDefinitionId,
                    fixedDefinitions[index].WaveIndex,
                    fixedDefinitions[index].Count));
            }

            data = new FpgEncounterOverrideData(
                mode,
                forced,
                excluded,
                fixedData,
                lockBudget ? lockedBudget : (int?)null);
            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(overrideId)
                || !Enum.IsDefined(typeof(FpgEncounterOverrideMode), mode)
                || lockedBudget < 0)
            {
                error = "Formal encounter override has invalid identity, mode, or budget.";
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            FpgForcedEnemyDefinition[] forcedDefinitions = forcedEnemies ?? Array.Empty<FpgForcedEnemyDefinition>();
            for (int index = 0; index < forcedDefinitions.Length; index++)
            {
                FpgForcedEnemyDefinition forced = forcedDefinitions[index];
                string forcedError = string.Empty;
                if (forced == null || !forced.TryValidate(index, out forcedError))
                {
                    error = string.IsNullOrEmpty(forcedError) ? "Formal override forced entry is missing." : forcedError;
                    return false;
                }

                if (!ids.Add(forced.Enemy.EnemyDefinitionId))
                {
                    error = "Formal override contains duplicate forced enemies.";
                    return false;
                }
            }

            string[] excludedIds = excludedEnemyDefinitionIds ?? Array.Empty<string>();
            for (int index = 0; index < excludedIds.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(excludedIds[index]) || !ids.Add(excludedIds[index].Trim()))
                {
                    error = "Formal override contains duplicate or empty excluded IDs.";
                    return false;
                }
            }

            FpgEnemyDefinition[] excludedAssets = excludedEnemies ?? Array.Empty<FpgEnemyDefinition>();
            for (int index = 0; index < excludedAssets.Length; index++)
            {
                if (excludedAssets[index] == null || !ids.Add(excludedAssets[index].EnemyDefinitionId))
                {
                    error = "Formal override contains duplicate or missing excluded assets.";
                    return false;
                }
            }

            FpgFixedSpawnDefinition[] fixedDefinitions = fixedSpawns ?? Array.Empty<FpgFixedSpawnDefinition>();
            for (int index = 0; index < fixedDefinitions.Length; index++)
            {
                string fixedError = string.Empty;
                if (fixedDefinitions[index] == null || !fixedDefinitions[index].TryValidate(index, out fixedError))
                {
                    error = string.IsNullOrEmpty(fixedError) ? "Formal override fixed entry is missing." : fixedError;
                    return false;
                }
            }

            if (mode == FpgEncounterOverrideMode.FixedWaves && fixedDefinitions.Length == 0)
            {
                error = "FixedWaves mode requires at least one fixed spawn.";
                return false;
            }

            return true;
        }
    }
}
