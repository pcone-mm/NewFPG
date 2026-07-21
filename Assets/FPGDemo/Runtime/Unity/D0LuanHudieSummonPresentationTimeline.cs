namespace FPG.Demo.Unity
{
    /// <summary>
    /// Allocation-free, presentation-only phase gate for the authored
    /// Luan-to-Hudie lifecycle. The combat domain remains the authority for
    /// entity replacement; this gate only guarantees one visual/audio command
    /// per phase and can be reset when a new BattleSession binds.
    /// </summary>
    public sealed class D0LuanHudieSummonPresentationTimeline
    {
        private bool summonConsumed;
        private bool appearanceConsumed;

        public bool SummonConsumed => summonConsumed;
        public bool AppearanceConsumed => appearanceConsumed;
        public int SummonConsumeCount { get; private set; }
        public int AppearanceConsumeCount { get; private set; }

        public void Reset()
        {
            summonConsumed = false;
            appearanceConsumed = false;
            SummonConsumeCount = 0;
            AppearanceConsumeCount = 0;
        }

        public bool TryConsumeSummon(
            D0CombatScenarioDefinition scenario,
            long currentTick,
            int activeEnemyDefinitionId)
        {
            if (summonConsumed
                || !TryResolveContract(
                    scenario,
                    out D0LuanSummonHudieDefinition summon,
                    out D0EncounterDefinition encounter)
                || currentTick < summon.SummonTick
                || encounter.InitialSpawnSlot == null
                || activeEnemyDefinitionId
                    != encounter.InitialSpawnSlot.DefinitionId)
            {
                return false;
            }

            summonConsumed = true;
            SummonConsumeCount++;
            return true;
        }

        public bool TryConsumeAppearance(
            D0CombatScenarioDefinition scenario,
            long currentTick,
            int activeEnemyDefinitionId)
        {
            if (appearanceConsumed
                || !TryResolveContract(
                    scenario,
                    out D0LuanSummonHudieDefinition summon,
                    out D0EncounterDefinition encounter)
                || currentTick < summon.AppearanceTick
                || encounter.SpawnSlotCount != 2)
            {
                return false;
            }

            D0EncounterSpawnSlot hudieSlot = encounter.GetSpawnSlot(1);
            if (hudieSlot == null
                || activeEnemyDefinitionId != hudieSlot.DefinitionId
                || hudieSlot.Enemy != summon.HudieEnemy)
            {
                return false;
            }

            appearanceConsumed = true;
            AppearanceConsumeCount++;
            return true;
        }

        private static bool TryResolveContract(
            D0CombatScenarioDefinition scenario,
            out D0LuanSummonHudieDefinition summon,
            out D0EncounterDefinition encounter)
        {
            summon = scenario == null ? null : scenario.LuanSummonHudie;
            encounter = scenario == null ? null : scenario.Encounter;
            return scenario != null
                && scenario.EncounterContract
                    == D0EncounterContract.LuanHudieSingleProjectile
                && summon != null
                && encounter != null;
        }
    }
}
