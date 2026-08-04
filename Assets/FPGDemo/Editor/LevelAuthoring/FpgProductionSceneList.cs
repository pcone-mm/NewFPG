using System;
using System.Collections.Generic;
using System.Linq;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Editor.LevelAuthoring
{
    public static class FpgProductionSceneList
    {
        public const string BootScenePath =
            "Assets/FPGDemo/Scenes/Boot.unity";
        public const string FormalRoomScenePath =
            "Assets/FPGDemo/Scenes/FormalRoom.unity";

        public static bool TryBuild(out string[] scenes, out string error)
        {
            scenes = Array.Empty<string>();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath) == null
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    FormalRoomScenePath) == null)
            {
                error =
                    "Production build requires Boot and FormalRoom scenes.";
                return false;
            }

            FpgRoomCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FpgRoomCatalog>(
                    FpgRoomArtSceneEditorUtility.RoomCatalogPath);
            if (catalog == null)
            {
                error = "Production RoomCatalog is missing.";
                return false;
            }

            if (!catalog.TryValidate(out error))
            {
                error = "Production RoomCatalog is invalid: " + error;
                return false;
            }

            return TryBuild(catalog.Rooms, out scenes, out error);
        }

        public static bool TryBuild(
            IReadOnlyList<FpgRoomDefinition> sourceRooms,
            out string[] scenes,
            out string error)
        {
            scenes = Array.Empty<string>();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath) == null
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    FormalRoomScenePath) == null)
            {
                error =
                    "Production build requires Boot and FormalRoom scenes.";
                return false;
            }

            if (sourceRooms == null || sourceRooms.Count == 0)
            {
                error = "Production scene list requires at least one room.";
                return false;
            }

            List<FpgRoomDefinition> rooms =
                sourceRooms.Where(room => room != null).ToList();
            if (rooms.Count != sourceRooms.Count)
            {
                error = "Production scene list contains a missing room.";
                return false;
            }

            rooms.Sort(
                (left, right) => StringComparer.Ordinal.Compare(
                    left.RoomId,
                    right.RoomId));
            List<string> result = new List<string>(rooms.Count + 2)
            {
                BootScenePath,
                FormalRoomScenePath
            };
            HashSet<string> paths =
                new HashSet<string>(result, StringComparer.Ordinal);
            HashSet<string> names =
                new HashSet<string>(StringComparer.Ordinal)
                {
                    "Boot",
                    "FormalRoom"
                };
            for (int index = 0; index < rooms.Count; index++)
            {
                FpgRoomDefinition room = rooms[index];
                if (!FpgRoomArtSceneEditorUtility.TryValidateStoredReference(
                        room,
                        out error))
                {
                    return false;
                }

                if (!paths.Add(room.ArtScenePath)
                    || !names.Add(room.ArtScene.SceneName))
                {
                    error =
                        $"Production scene paths and names must be unique. Duplicate Art Scene for room '{room.RoomId}'.";
                    return false;
                }

                result.Add(room.ArtScenePath);
            }

            scenes = result.ToArray();
            error = string.Empty;
            return true;
        }

        public static bool TryValidateEditorBuildSettings(out string error)
        {
            if (!TryBuild(out string[] expectedScenes, out error))
            {
                return false;
            }

            if (!TryValidateConfiguredScenes(
                    EditorBuildSettings.globalScenes,
                    expectedScenes,
                    out error))
            {
                error = "Global Build Settings are invalid: " + error;
                return false;
            }

            if (!TryValidateConfiguredScenes(
                    EditorBuildSettings.scenes,
                    expectedScenes,
                    out error))
            {
                error = "Active Build Settings are invalid: " + error;
                return false;
            }

            return true;
        }

        public static bool TryValidateConfiguredScenes(
            IReadOnlyList<EditorBuildSettingsScene> configuredScenes,
            IReadOnlyList<string> expectedScenes,
            out string error)
        {
            if (configuredScenes == null || expectedScenes == null)
            {
                error = "Build Settings validation requires both scene lists.";
                return false;
            }

            if (configuredScenes.Count != expectedScenes.Count)
            {
                error =
                    $"Build Settings contain {configuredScenes.Count} scenes; "
                    + $"the production list requires {expectedScenes.Count}.";
                return false;
            }

            for (int index = 0; index < expectedScenes.Count; index++)
            {
                EditorBuildSettingsScene configured = configuredScenes[index];
                if (configured == null
                    || !configured.enabled
                    || !string.Equals(
                        configured.path,
                        expectedScenes[index],
                        StringComparison.Ordinal))
                {
                    string actual = configured == null
                        ? "<missing>"
                        : configured.enabled
                            ? configured.path
                            : configured.path + " (disabled)";
                    error =
                        $"Build Settings scene {index} is '{actual}'; "
                        + $"expected '{expectedScenes[index]}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }

    internal sealed class FpgRoomArtBuildPreprocessor
        : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            bool isBattleTestDevelopmentBuild = report != null
                && (report.summary.options & BuildOptions.Development) != 0
                && FpgBattleTestDevelopmentSceneList.ContainsBattleTest(
                    EditorBuildSettings.scenes);
            string settingsError;
            bool buildSettingsValid = isBattleTestDevelopmentBuild
                ? FpgBattleTestDevelopmentSceneList
                    .TryValidateEditorBuildSettings(out settingsError)
                : FpgProductionSceneList.TryValidateEditorBuildSettings(
                    out settingsError);
            if (!FpgRoomArtSceneContractValidator.TryValidateCatalog(
                    out string error)
                || !buildSettingsValid)
            {
                throw new BuildFailedException(
                    string.IsNullOrWhiteSpace(error)
                        ? settingsError
                        : error);
            }
        }
    }

    internal sealed class FpgBattleTestReleaseSceneGuard
        : IProcessSceneWithReport
    {
        public int callbackOrder => -2000;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null)
            {
                return;
            }

            bool isDevelopmentBuild = report != null
                && (report.summary.options & BuildOptions.Development) != 0;
            if (!isDevelopmentBuild
                && scene.IsValid()
                && string.Equals(
                    scene.path,
                    FpgBattleTestDevelopmentSceneList.BattleTestScenePath,
                    StringComparison.Ordinal))
            {
                throw new BuildFailedException(
                    "Release builds must not contain the BattleTest scene.");
            }
        }
    }
}
