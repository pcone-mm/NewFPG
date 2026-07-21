using System;
using System.Collections.Generic;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [Serializable]
    public sealed class FpgEnemyPoolEntryDefinition
    {
        [SerializeField]
        private FpgEnemyDefinition enemy;

        [SerializeField, Min(1)]
        private int selectionWeight = 1;

        [SerializeField, Min(0)]
        private int minDepth;

        [SerializeField, Min(0)]
        private int maxDepth = int.MaxValue;

        [SerializeField, Min(1)]
        private int maxPerWave = 99;

        [SerializeField, Min(1)]
        private int maxPerRoom = 99;

        [SerializeField]
        private bool themeEligible = true;

        public FpgEnemyDefinition Enemy => enemy;
        public int SelectionWeight => selectionWeight;
        public int MinDepth => minDepth;
        public int MaxDepth => maxDepth;
        public int MaxPerWave => maxPerWave;
        public int MaxPerRoom => maxPerRoom;
        public bool ThemeEligible => themeEligible;

        public bool IsAvailableAtDepth(int depth)
        {
            return depth >= minDepth && depth <= maxDepth;
        }

        public bool TryValidate(int index, out string error)
        {
            error = string.Empty;
            if (enemy == null)
            {
                error = $"Pool entry {index} is missing an enemy definition.";
                return false;
            }

            if (selectionWeight <= 0 || minDepth < 0 || maxDepth < minDepth
                || maxPerWave <= 0 || maxPerRoom <= 0)
            {
                error = $"Pool entry '{enemy.EnemyDefinitionId}' has invalid limits.";
                return false;
            }

            return enemy.TryValidate(out error);
        }
    }

    [CreateAssetMenu(
        fileName = "FpgEnemyPoolDefinition",
        menuName = "FPG Demo/Formal Encounter/Enemy Pool")]
    public sealed class FpgEnemyPoolDefinition : ScriptableObject
    {
        [SerializeField]
        private string poolId = "pool";

        [SerializeField]
        private string displayName = "Enemy Pool";

        [SerializeField, TextArea]
        private string designerNotes;

        [SerializeField]
        private FpgEnemyPoolEntryDefinition[] entries = Array.Empty<FpgEnemyPoolEntryDefinition>();

        public string PoolId => poolId;
        public string DisplayName => displayName;
        public string DesignerNotes => designerNotes;
        public IReadOnlyList<FpgEnemyPoolEntryDefinition> Entries => entries ?? Array.Empty<FpgEnemyPoolEntryDefinition>();
        public int EntryCount => entries == null ? 0 : entries.Length;

        public FpgEnemyPoolEntryDefinition GetEntry(int index)
        {
            if (entries == null || index < 0 || index >= entries.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return entries[index];
        }

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(poolId) || string.IsNullOrWhiteSpace(displayName))
            {
                error = "Formal enemy pool requires a stable ID and display name.";
                return false;
            }

            FpgEnemyPoolEntryDefinition[] values = entries ?? Array.Empty<FpgEnemyPoolEntryDefinition>();
            if (values.Length == 0)
            {
                error = $"Formal enemy pool '{poolId}' requires at least one entry.";
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == null || !values[index].TryValidate(index, out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = $"Formal enemy pool '{poolId}' entry {index} is missing.";
                    }

                    return false;
                }

                if (!ids.Add(values[index].Enemy.EnemyDefinitionId))
                {
                    error = $"Formal enemy pool '{poolId}' repeats enemy ID '{values[index].Enemy.EnemyDefinitionId}'.";
                    return false;
                }
            }

            return true;
        }

        public bool TryGetEligibleEntries(
            int depth,
            List<FpgEnemyPoolEntryDefinition> destination,
            out string error)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            if (!TryValidate(out error))
            {
                return false;
            }

            FpgEnemyPoolEntryDefinition[] values = entries ?? Array.Empty<FpgEnemyPoolEntryDefinition>();
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index].IsAvailableAtDepth(depth))
                {
                    destination.Add(values[index]);
                }
            }

            if (destination.Count == 0)
            {
                error = $"Formal enemy pool '{poolId}' has no entries at depth {depth}.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
