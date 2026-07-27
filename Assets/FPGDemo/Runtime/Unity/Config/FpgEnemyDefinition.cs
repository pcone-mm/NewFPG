using System;
using System.Collections.Generic;
using FPG.Demo.Run;
using FPG.Demo.Skills;
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

            HashSet<string> skillIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < attacks.Length; index++)
            {
                if (attacks[index] == null || !attacks[index].TryValidate(out error))
                {
                    error = $"Formal enemy '{enemyDefinitionId}' attack {index} is invalid: {error}";
                    return false;
                }

                if (!skillIds.Add(attacks[index].SkillId))
                {
                    error = $"Formal enemy '{enemyDefinitionId}' repeats skill ID '{attacks[index].SkillId}'.";
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

            D0ActorSocketRegistry socketRegistry =
                entityViewPrefab.GetComponentInChildren<
                    D0ActorSocketRegistry>(true);
            if (socketRegistry == null
                || !socketRegistry.TryValidate(out error)
                || !TryValidateAttackSockets(
                    attacks,
                    socketRegistry,
                    out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = $"Formal enemy '{enemyDefinitionId}' prefab requires a valid socket registry for its skill events.";
                }

                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateAttackSockets(
            FpgEnemyAttackDefinition[] attacks,
            D0ActorSocketRegistry socketRegistry,
            out string error)
        {
            for (int attackIndex = 0;
                attackIndex < attacks.Length;
                attackIndex++)
            {
                FpgEnemyAttackDefinition attack = attacks[attackIndex];
                for (int sequenceIndex = 0;
                    sequenceIndex < attack.Sequences.Count;
                    sequenceIndex++)
                {
                    FpgSkillSequenceDefinition sequence =
                        attack.Sequences[sequenceIndex];
                    if (!TryValidateActionSockets(
                            attack.SkillId,
                            sequence.AttackEvents,
                            socketRegistry,
                            out error)
                        || !TryValidateActionSockets(
                            attack.SkillId,
                            sequence.ProjectileEvents,
                            socketRegistry,
                            out error)
                        || !TryValidateActionSockets(
                            attack.SkillId,
                            sequence.ReloadEvents,
                            socketRegistry,
                            out error)
                        || !TryValidateActionSockets(
                            attack.SkillId,
                            sequence.SummonEvents,
                            socketRegistry,
                            out error))
                    {
                        return false;
                    }

                    for (int warningIndex = 0;
                        warningIndex < sequence.Warnings.Count;
                        warningIndex++)
                    {
                        FpgSkillWarningDefinition warning =
                            sequence.Warnings[warningIndex];
                        if (!TryValidateSocket(
                                attack.SkillId,
                                warning.EventId,
                                warning.SocketId,
                                socketRegistry,
                                out error))
                        {
                            return false;
                        }
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateActionSockets<TAction>(
            string skillId,
            IReadOnlyList<TAction> actions,
            D0ActorSocketRegistry socketRegistry,
            out string error)
            where TAction : FpgSkillGameplayActionDefinition
        {
            for (int index = 0; index < actions.Count; index++)
            {
                TAction action = actions[index];
                if (!TryValidateSocket(
                        skillId,
                        action.EventId,
                        action.SocketId,
                        socketRegistry,
                        out error))
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateSocket(
            string skillId,
            string eventId,
            string socketId,
            D0ActorSocketRegistry socketRegistry,
            out string error)
        {
            if (string.IsNullOrEmpty(socketId)
                || socketRegistry.TryResolve(socketId, out _))
            {
                error = string.Empty;
                return true;
            }

            error = $"Enemy skill '{skillId}' event '{eventId}' cannot resolve prefab socket '{socketId}'.";
            return false;
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
                FpgEnemyAttackDefinition attack =
                    enemy.GetAttackPattern(index);
                if (!attack.TryCompile(
                        out FpgCompiledEnemySkillDefinition compiledAttack,
                        out error))
                {
                    error = $"Enemy skill '{attack.SkillId}' cannot compile while validating its summon graph: {error}";
                    visiting.Remove(enemy);
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
                        if (!TryValidateEnemyGraph(
                            candidate.Definition,
                            visiting,
                            depth + 1,
                            out error))
                        {
                            error = $"Summon action '{summon.ActionId}' on '{enemy.EnemyDefinitionId}' is invalid: {error}";
                            visiting.Remove(enemy);
                            return false;
                        }

                        visiting.Remove(candidate.Definition);
                    }
                }
            }

            visiting.Remove(enemy);
            error = string.Empty;
            return true;
        }
    }
}
