using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
/// <summary>
    /// Generic summon action. It contains no enemy-ID special cases: every
    /// candidate is submitted to the same encounter spawn queue and capacity
    /// checks as a planned entry. The graph validator rejects cycles before a
    /// room can enter Preparing.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FpgSummonActionDefinition",
        menuName = "FPG Demo/Formal Encounter/Summon Action")]
    public sealed class FpgSummonActionDefinition : ScriptableObject
    {
        [D0PlannerSection("Identity")]
        [D0PlannerField("Action ID", "Stable identity used by the attack director and diagnostics.")]
        [SerializeField]
        private string actionId = "summon";

        [D0PlannerField("Display Name", "Authoring-only display name.")]
        [SerializeField]
        private string displayName = "Summon";

        [D0PlannerSection("Candidates")]
        [D0PlannerField("Candidate Enemies", "Enemy definitions submitted to the shared Spawn Queue.")]
        [SerializeField]
        private FpgEnemyDefinition[] candidateEnemies = Array.Empty<FpgEnemyDefinition>();

        [D0PlannerField("Candidate Weights", "Optional integer weights aligned with Candidate Enemies; empty means weight one.")]
        [SerializeField]
        private int[] candidateWeights = Array.Empty<int>();

        [D0PlannerSection("Hard Limits")]
        [D0PlannerField("Max Per Owner", "Static maximum summons emitted by one owner instance.")]
        [SerializeField, Min(0)]
        private int maxSummonsPerOwner = 2;

        [D0PlannerField("Max Per Encounter", "Static maximum summons emitted by all owners in one encounter.")]
        [SerializeField, Min(0)]
        private int maxTotalSummonsPerEncounter = 8;

        [D0PlannerField("Max Recursion Depth", "Maximum nested summon depth for this action.")]
        [SerializeField, Min(0)]
        private int maxRecursionDepth = 2;

        [D0PlannerField("Cooldown (Ticks)", "Minimum activation interval for this summon action.")]
        [SerializeField, Min(0)]
        private int cooldownTicks = 60;

        public string ActionId => actionId;
        public string DisplayName => displayName;
        public FpgEnemyDefinition[] CandidateEnemies => candidateEnemies ?? Array.Empty<FpgEnemyDefinition>();
        public int[] CandidateWeights => candidateWeights ?? Array.Empty<int>();
        public int MaxSummonsPerOwner => maxSummonsPerOwner;
        public int MaxTotalSummonsPerEncounter => maxTotalSummonsPerEncounter;
        public int MaxRecursionDepth => maxRecursionDepth;
        public int CooldownTicks => cooldownTicks;

        // Explicit marker used by adapters to route this action to SpawnQueue.
        public bool UsesEncounterSpawnQueue => true;

        public int GetCandidateWeight(int index)
        {
            if (index < 0 || index >= CandidateEnemies.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return candidateWeights == null || candidateWeights.Length == 0
                ? 1
                : candidateWeights[index];
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(actionId)
                || string.IsNullOrWhiteSpace(displayName))
            {
                error = "Summon action requires a stable ID and display name.";
                return false;
            }

            if (candidateEnemies == null || candidateEnemies.Length == 0)
            {
                error = $"Summon action '{actionId}' requires at least one candidate enemy.";
                return false;
            }

            if (candidateWeights != null
                && candidateWeights.Length != 0
                && candidateWeights.Length != candidateEnemies.Length)
            {
                error = $"Summon action '{actionId}' candidate weights must be empty or match candidate count.";
                return false;
            }

            if (maxSummonsPerOwner <= 0
                || maxTotalSummonsPerEncounter <= 0
                || maxRecursionDepth < 0
                || maxRecursionDepth > FpgFormalConfigValidation.DefaultMaxSummonGraphDepth
                || cooldownTicks < 0)
            {
                error = $"Summon action '{actionId}' has invalid hard limits.";
                return false;
            }

            HashSet<string> candidateIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < candidateEnemies.Length; index++)
            {
                FpgEnemyDefinition candidate = candidateEnemies[index];
                if (candidate == null)
                {
                    error = $"Summon action '{actionId}' candidate {index} is missing.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(candidate.EnemyDefinitionId)
                    || !candidateIds.Add(candidate.EnemyDefinitionId))
                {
                    error = $"Summon action '{actionId}' contains a duplicate or empty candidate ID.";
                    return false;
                }

                if (candidateWeights != null && candidateWeights.Length > 0
                    && candidateWeights[index] <= 0)
                {
                    error = $"Summon action '{actionId}' candidate weight {index} must be positive.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
