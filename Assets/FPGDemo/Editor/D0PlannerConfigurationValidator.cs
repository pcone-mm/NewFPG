using System;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor
{
    /// <summary>
    /// Read-only preflight for the planner-owned D0 configuration graph. It
    /// deliberately validates assets only: installation, scene mutation and
    /// battle simulation stay in their existing commands and runtime paths.
    /// </summary>
    public static class D0PlannerConfigurationValidator
    {
        public const string CombatLabScenarioConfigPath =
            "Assets/FPGDemo/Config/BattleScenarioConfig.asset";

        [MenuItem("FPG Demo/D0 2.5D/Validate Planner Configuration")]
        public static void ValidateCombatLabPlannerConfiguration()
        {
            if (TryValidateCombatLab(out string report))
            {
                Debug.Log(report);
                return;
            }

            Debug.LogError(report);
        }

        /// <summary>
        /// Selects the single authored asset that owns the D0 camera rig and
        /// lens values. This gives designers a direct route to the tunable
        /// camera settings without searching through the stage asset.
        /// </summary>
        [MenuItem("FPG Demo/D0 2.5D/Open Camera & 3C Configuration")]
        public static void OpenCombatLabCameraConfiguration()
        {
            BattleScenarioConfig config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(
                CombatLabScenarioConfigPath);
            D0ThreeCProfile profile = config == null || config.AuthoredScenario == null
                ? null
                : config.AuthoredScenario.ThreeCProfile;
            if (profile == null)
            {
                Debug.LogError(
                    "D0 camera configuration is unavailable because the CombatLab authored 3C profile is missing.");
                return;
            }

            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        public static bool TryValidateCombatLab(out string report)
        {
            BattleScenarioConfig config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(
                CombatLabScenarioConfigPath);
            return TryValidate(config, out report);
        }

        public static bool TryValidate(BattleScenarioConfig config, out string report)
        {
            if (config == null)
            {
                report = "D0 planner validation requires the CombatLab BattleScenarioConfig asset.";
                return false;
            }

            if (!config.UsesAuthoredScenario)
            {
                report = "D0 planner validation requires BattleScenarioConfig.authoredScenario to be enabled.";
                return false;
            }

            D0CombatScenarioDefinition scenario = config.AuthoredScenario;
            if (scenario == null)
            {
                report = "D0 planner validation requires an authored combat scenario asset.";
                return false;
            }

            if (!scenario.TryValidate(out string scenarioError))
            {
                report = $"D0 authored combat scenario is invalid: {scenarioError}";
                return false;
            }

            if (!config.TryValidateSpatialConfiguration(out string spatialError))
            {
                report = $"D0 spatial configuration is invalid: {spatialError}";
                return false;
            }

            if (!config.TryCreateDefinition(out _, out string compositionError))
            {
                report = $"D0 authored scenario cannot compose a BattleSession definition: {compositionError}";
                return false;
            }

            D0StageDefinition stage = scenario.StageDefinition;
            if (stage == null)
            {
                report = "D0 authored combat scenario requires a stage definition.";
                return false;
            }

            if (!stage.TryValidate(out string stageError))
            {
                report = $"D0 stage definition is invalid: {stageError}";
                return false;
            }

            if (!TryValidateStageSprites(stage, out report))
            {
                return false;
            }

            D0EnemyDefinition enemy = scenario.Encounter == null
                ? null
                : scenario.Encounter.Enemy;
            D0EncounterContract encounterContract = scenario.EncounterContract;
            D0ThreeCProfile threeCProfile = scenario.ThreeCProfile;
            if (threeCProfile == null)
            {
                report = "D0 authored combat scenario requires a 3C profile.";
                return false;
            }

            if (!threeCProfile.TryValidate(out string threeCError))
            {
                report = $"D0 3C profile is invalid: {threeCError}";
                return false;
            }

            if (enemy == null || enemy.BehaviorProfile == null)
            {
                report = "D0 enemy behavior profile is missing from the authored enemy asset.";
                return false;
            }

            if (!enemy.BehaviorProfile.TryValidate(out string behaviorError))
            {
                report = $"D0 enemy behavior profile is invalid: {behaviorError}";
                return false;
            }

            string attackContractError = string.Empty;
            if (scenario.Encounter == null
                || !scenario.Encounter.TryValidateCombatContract(out attackContractError))
            {
                report = scenario.Encounter == null
                    ? "D0 reusable attack schedule is missing from the authored encounter."
                    : $"D0 encounter contract is invalid: {attackContractError}";
                return false;
            }

            if (encounterContract == D0EncounterContract.BurstbugStandard)
            {
                D0ActorPresentationDefinition enemyPresentation = enemy == null
                    ? null
                    : enemy.ActorPresentation;
                D0EnemyEffectPresentationDefinition effects = null;
                if (enemyPresentation == null
                    || !enemyPresentation.TryGetEnemyEffects(
                        out effects)
                    || effects == null)
                {
                    report = "D0 Burstbug effect presentation is missing from the authored enemy asset.";
                    return false;
                }

                if (!effects.TryValidate(out string effectsError))
                {
                    report = $"D0 Burstbug effect presentation is invalid: {effectsError}";
                    return false;
                }

                if (!TryValidateBurstbugEffectPrefabs(effects, out report))
                {
                    return false;
                }
            }

            string enemySummary = encounterContract == D0EncounterContract.BurstbugStandard
                ? $"{enemy.DisplayName}; entry/patrol behavior, three reusable attack languages and four death-state FX pools are valid."
                : $"{enemy.DisplayName}; fixed-position behavior, a repeated single-projectile attack and Luan/Hudie presentation are valid.";

            report =
                $"[D0 Planner Validation] Passed\n"
                + $"Scenario: {scenario.DisplayName} ({scenario.ScenarioId})\n"
                + $"Player: {scenario.Player.DisplayName}; 2.5D 3C, weapon and feel profile are valid.\n"
                + $"Enemy: {enemySummary}\n"
                + $"Stage: {stage.DisplayName}; {stage.ForestLayers.Count} direct Sprite layers are valid.\n"
                + "Spatial query composition succeeds. No assets or scenes were modified.";
            return true;
        }

        private static bool TryValidateStageSprites(D0StageDefinition stage, out string error)
        {
            for (int index = 0; index < stage.ForestLayers.Count; index++)
            {
                D0StageForestLayerDefinition layer = stage.ForestLayers[index];
                string spritePath = layer == null || layer.Sprite == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(layer.Sprite);
                if (string.IsNullOrWhiteSpace(spritePath))
                {
                    error = $"Stage layer '{(layer == null ? index.ToString() : layer.LayerId)}' must directly reference a Sprite asset.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateBurstbugEffectPrefabs(
            D0EnemyEffectPresentationDefinition effects,
            out string error)
        {
            foreach (D0EnemyEffectSlot slot in Enum.GetValues(typeof(D0EnemyEffectSlot)))
            {
                if (!effects.TryGet(slot, out D0EnemyEffectPoolDefinition pool)
                    || pool == null)
                {
                    error = $"D0 Burstbug effect slot '{slot}' is missing.";
                    return false;
                }

                string prefabPath = pool.VisualPrefab == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(pool.VisualPrefab);
                if (string.IsNullOrWhiteSpace(prefabPath)
                    || !prefabPath.StartsWith(
                        FpgDemoD0SliceInstaller.D0SpinePresentationFolder + "/",
                        StringComparison.Ordinal))
                {
                    error =
                        $"D0 Burstbug effect slot '{slot}' must reference a D0-derived prefab under "
                        + $"'{FpgDemoD0SliceInstaller.D0SpinePresentationFolder}/', not '{prefabPath}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
