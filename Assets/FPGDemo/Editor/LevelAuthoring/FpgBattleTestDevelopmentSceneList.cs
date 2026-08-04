using System;
using System.Collections.Generic;
using UnityEditor;

namespace FPG.Demo.Editor.LevelAuthoring
{
    public static class FpgBattleTestDevelopmentSceneList
    {
        public const string BattleTestScenePath =
            "Assets/InitTestScene/BattleTest.unity";

        public static bool TryBuild(out string[] scenes, out string error)
        {
            scenes = Array.Empty<string>();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    BattleTestScenePath) == null)
            {
                error = "BattleTest scene is missing at "
                    + BattleTestScenePath + ".";
                return false;
            }

            if (!FpgProductionSceneList.TryBuild(
                    out string[] productionScenes,
                    out error))
            {
                return false;
            }

            scenes = new string[productionScenes.Length + 1];
            scenes[0] = BattleTestScenePath;
            Array.Copy(
                productionScenes,
                0,
                scenes,
                1,
                productionScenes.Length);
            error = string.Empty;
            return true;
        }

        public static bool TryValidateEditorBuildSettings(out string error)
        {
            if (!TryBuild(out string[] expectedScenes, out error))
            {
                return false;
            }

            if (!FpgProductionSceneList.TryValidateConfiguredScenes(
                    EditorBuildSettings.globalScenes,
                    expectedScenes,
                    out error))
            {
                error = "Global Development Build Settings are invalid: "
                    + error;
                return false;
            }

            if (!FpgProductionSceneList.TryValidateConfiguredScenes(
                    EditorBuildSettings.scenes,
                    expectedScenes,
                    out error))
            {
                error = "Active Development Build Settings are invalid: "
                    + error;
                return false;
            }

            return true;
        }

        public static bool ContainsBattleTest(
            IReadOnlyList<EditorBuildSettingsScene> scenes)
        {
            if (scenes == null)
            {
                return false;
            }

            for (int index = 0; index < scenes.Count; index++)
            {
                EditorBuildSettingsScene scene = scenes[index];
                if (scene != null && scene.enabled
                    && string.Equals(
                        scene.path,
                        BattleTestScenePath,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
