using System;
using System.Collections.Generic;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [CreateAssetMenu(
        fileName = "FpgEnemyDefinition",
        menuName = "FPG Demo/Formal Encounter/Enemy Definition")]
    public sealed class FpgEnemyDefinition : ScriptableObject
    {
        [SerializeField]
        private string enemyDefinitionId = "enemy";

        [SerializeField]
        private string displayName = "Enemy";

        [SerializeField, TextArea]
        private string designerNotes;

        [SerializeField]
        private FpgEnemyRole role = FpgEnemyRole.Melee;

        [SerializeField, Min(1)]
        private int life = 100;

        [SerializeField, Min(1)]
        private int breakValue = 25;

        [SerializeField, Min(1)]
        private int spawnCost = 1;

        [SerializeField, Min(1)]
        private int capWeight = 1;

        [SerializeField, Min(0)]
        private int maxPerEncounter;

        [SerializeField]
        private bool themeEligible = true;

        [SerializeField]
        private GameObject entityViewPrefab;

        [SerializeField]
        private FpgEnemyBehaviorDefinition behavior;

        [SerializeField]
        private FpgEnemyAttackDefinition[] attackPatterns = Array.Empty<FpgEnemyAttackDefinition>();

        public string EnemyDefinitionId => enemyDefinitionId;
        public string EnemyId => enemyDefinitionId;
        public string DisplayName => displayName;
        public string DesignerNotes => designerNotes;
        public FpgEnemyRole Role => role;
        public int Life => life;
        public int BreakValue => breakValue;
        public int SpawnCost => spawnCost;
        public int CapWeight => capWeight;
        public int MaxPerEncounter => maxPerEncounter;
        public bool ThemeEligible => themeEligible;
        public GameObject EntityPrefab => entityViewPrefab;
        public FpgEnemyBehaviorDefinition Behavior => behavior;
        public IReadOnlyList<FpgEnemyAttackDefinition> AttackPatterns =>
            attackPatterns ?? Array.Empty<FpgEnemyAttackDefinition>();
        public int AttackPatternCount => attackPatterns == null ? 0 : attackPatterns.Length;

        public FpgEnemyAttackDefinition GetAttackPattern(int index)
        {
            if (attackPatterns == null || index < 0 || index >= attackPatterns.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return attackPatterns[index];
        }

        public bool TryValidate(out string error)
        {
            return FpgFormalConfigValidation.TryValidateEnemyGraph(
                this,
                new HashSet<FpgEnemyDefinition>(),
                0,
                out error);
        }

        internal bool TryValidateCore(out string error)
        {
            if (string.IsNullOrWhiteSpace(enemyDefinitionId)
                || string.IsNullOrWhiteSpace(displayName)
                || !Enum.IsDefined(typeof(FpgEnemyRole), role)
                || life <= 0
                || breakValue <= 0
                || spawnCost <= 0
                || capWeight <= 0
                || maxPerEncounter < 0)
            {
                error = $"Formal enemy '{enemyDefinitionId}' has invalid identity, role, combat, or budget values.";
                return false;
            }

            if (entityViewPrefab == null || behavior == null)
            {
                error = $"Formal enemy '{enemyDefinitionId}' requires an entity prefab and behavior definition.";
                return false;
            }

            if (!behavior.TryValidate(out error))
            {
                error = $"Formal enemy '{enemyDefinitionId}' behavior is invalid: {error}";
                return false;
            }

            FpgEnemyAttackDefinition[] attacks = attackPatterns ?? Array.Empty<FpgEnemyAttackDefinition>();
            if (attacks.Length == 0)
            {
                error = $"Formal enemy '{enemyDefinitionId}' requires at least one attack pattern.";
                return false;
            }

            HashSet<string> attackIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < attacks.Length; index++)
            {
                if (attacks[index] == null || !attacks[index].TryValidate(out error))
                {
                    error = $"Formal enemy '{enemyDefinitionId}' attack {index} is invalid: {error}";
                    return false;
                }

                if (!attackIds.Add(attacks[index].AttackId))
                {
                    error = $"Formal enemy '{enemyDefinitionId}' repeats attack ID '{attacks[index].AttackId}'.";
                    return false;
                }
            }

            FpgEnemyEntityView entityView =
                entityViewPrefab.GetComponent<FpgEnemyEntityView>();
            if (entityView == null)
            {
                error = $"Formal enemy '{enemyDefinitionId}' prefab requires "
                    + "an FpgEnemyEntityView.";
                return false;
            }

            if (!entityView.TryValidate(out error)
                || !entityView.TryValidatePresentation(this, out error))
            {
                error = $"Formal enemy '{enemyDefinitionId}' entity view is "
                    + "invalid: " + error;
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    internal static class FpgFormalConfigValidation
    {
        public const int DefaultMaxSummonGraphDepth = 8;

        public static bool TryValidateEnemyGraph(
            FpgEnemyDefinition enemy,
            HashSet<FpgEnemyDefinition> visiting,
            int depth,
            out string error)
        {
            if (enemy == null)
            {
                error = "Formal enemy reference is missing.";
                return false;
            }

            if (depth > DefaultMaxSummonGraphDepth)
            {
                error = $"Formal summon graph exceeds depth {DefaultMaxSummonGraphDepth}.";
                return false;
            }

            if (!visiting.Add(enemy))
            {
                error = $"Formal summon graph contains a cycle at '{enemy.EnemyDefinitionId}'.";
                return false;
            }

            if (!enemy.TryValidateCore(out error))
            {
                visiting.Remove(enemy);
                return false;
            }

            for (int index = 0; index < enemy.AttackPatternCount; index++)
            {
                FpgSummonActionDefinition summon = enemy.GetAttackPattern(index).Summon;
                if (summon == null)
                {
                    continue;
                }

                FpgEnemyDefinition[] candidates = summon.CandidateEnemies;
                for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                {
                    if (!TryValidateEnemyGraph(candidates[candidateIndex], visiting, depth + 1, out error))
                    {
                        error = $"Summon action '{summon.ActionId}' on '{enemy.EnemyDefinitionId}' is invalid: {error}";
                        visiting.Remove(enemy);
                        return false;
                    }

                    visiting.Remove(candidates[candidateIndex]);
                }
            }

            visiting.Remove(enemy);
            error = string.Empty;
            return true;
        }
    }
}
