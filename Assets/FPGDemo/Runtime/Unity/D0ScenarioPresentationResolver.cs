namespace FPG.Demo.Unity
{
    /// <summary>
    /// Resolves the scene's actor presentation data from the same authored D0
    /// scenario that creates its battle definition. This keeps character and
    /// enemy definitions as the source of truth while preserving the legacy
    /// CombatPresentationProfile path for scenes without an authored scenario.
    /// </summary>
    public static class D0ScenarioPresentationResolver
    {
        public static bool TryResolve(
            BattleScenarioConfig scenarioConfig,
            out D0ActorPresentationDefinition playerPresentation,
            out D0ActorPresentationDefinition enemyPresentation,
            out string error)
        {
            playerPresentation = null;
            enemyPresentation = null;
            error = string.Empty;
            if (scenarioConfig == null)
            {
                error = "BattleScenarioConfig is required to resolve D0 actor presentation.";
                return false;
            }

            if (!scenarioConfig.UsesAuthoredScenario)
            {
                error = string.Empty;
                return true;
            }

            D0CombatScenarioDefinition scenario = scenarioConfig.AuthoredScenario;
            if (scenario == null || scenario.Player == null)
            {
                error = "Authored combat scenario requires a player definition for actor presentation.";
                return false;
            }

            playerPresentation = scenario.Player.ActorPresentation;
            if (playerPresentation == null
                || !playerPresentation.TryGetPlayer(out _)
                || !playerPresentation.TryValidate(out error))
            {
                playerPresentation = null;
                if (string.IsNullOrEmpty(error))
                {
                    error = "Authored combat scenario requires a valid player actor presentation.";
                }

                return false;
            }

            if (scenario.Encounter == null || scenario.Encounter.Enemy == null)
            {
                playerPresentation = null;
                error = "Authored combat scenario requires an encounter enemy for actor presentation.";
                return false;
            }

            enemyPresentation = scenario.Encounter.Enemy.ActorPresentation;
            if (enemyPresentation == null
                || !enemyPresentation.TryGetEnemy(out _)
                || !enemyPresentation.TryValidate(out error))
            {
                playerPresentation = null;
                enemyPresentation = null;
                if (string.IsNullOrEmpty(error))
                {
                    error = "Authored combat scenario requires a valid enemy actor presentation.";
                }

                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
