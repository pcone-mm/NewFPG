using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using FPG.Demo.Editor.LevelAuthoring;
using FPG.Demo.Unity;
using UnityEngine;

namespace FPG.Demo.Editor
{
    /// <summary>
    /// Builds the formal FPG demo Player with the shared production scene list.
    /// </summary>
    public static class FpgWindowsReleaseBuild
    {
        public const string BootScenePath =
            FpgProductionSceneList.BootScenePath;
        private const string BootstrapConfigPath = "Assets/FPGDemo/Config/GameBootstrapConfig.asset";
        public const string FormalRoomScenePath =
            FpgProductionSceneList.FormalRoomScenePath;
        public const string DefaultOutputRelativePath =
            "Builds/FPGDemo/WindowsRelease/FPGDemo.exe";

        [MenuItem("FPG Demo/Build Windows x64 Release")]
        public static void BuildWindows64Release()
        {
            BuildWindows64ReleaseAt(GetDefaultOutputPath());
        }

        /// <summary>
        /// Batch-mode entry point:
        /// FPG.Demo.Editor.FpgWindowsReleaseBuild.BuildWindows64ReleaseFromBatch
        /// </summary>
        public static void BuildWindows64ReleaseFromBatch()
        {
            BuildWindows64ReleaseAt(GetDefaultOutputPath());
        }

        internal static BuildReport BuildWindows64ReleaseAt(string locationPathName)
        {
            if (!TryValidateBuildInputs(locationPathName, out string error))
            {
                throw new InvalidOperationException(error);
            }

            string outputDirectory = Path.GetDirectoryName(locationPathName);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException(
                    "FPG Windows build output must include a directory.");
            }

            Directory.CreateDirectory(outputDirectory);

            if (!FpgProductionSceneList.TryBuild(
                    out string[] productionScenes,
                    out string sceneListError))
            {
                throw new InvalidOperationException(sceneListError);
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = productionScenes,
                locationPathName = locationPathName,
                targetGroup = BuildTargetGroup.Standalone,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    "FPG Windows Release build failed. "
                    + "Review the Unity build log before retrying. "
                    + $"errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}, output={locationPathName}");
            }

            Debug.Log(
                "[FPG_BUILD] success "
                + $"output={locationPathName} size={report.summary.totalSize} "
                + $"scenes={string.Join(",", productionScenes)}");
            return report;
        }

        private static string GetDefaultOutputPath()
        {
            return Path.Combine(GetProjectRootPath(), DefaultOutputRelativePath);
        }

        private static bool TryValidateBuildInputs(string locationPathName, out string error)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath) == null)
            {
                error = "Formal FPG build requires Boot scene at " + BootScenePath + ".";
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FormalRoomScenePath) == null)
            {
                error = "Formal FPG build requires FormalRoom scene at "
                    + FormalRoomScenePath + ".";
                return false;
            }

            GameBootstrapConfig bootstrapConfig =
                AssetDatabase.LoadAssetAtPath<GameBootstrapConfig>(
                    BootstrapConfigPath);
            if (bootstrapConfig == null)
            {
                error = "Formal FPG build requires GameBootstrapConfig at "
                    + BootstrapConfigPath + ".";
                return false;
            }

            if (!bootstrapConfig.TryValidate(out string configError))
            {
                error = "Formal FPG build has invalid GameBootstrapConfig: "
                    + configError;
                return false;
            }

            string configuredRoomScenePath =
                "Assets/FPGDemo/Scenes/" + bootstrapConfig.RoomSceneName + ".unity";
            if (!FpgProductionSceneList.TryBuild(
                    out string[] productionScenes,
                    out string sceneListError))
            {
                error = sceneListError;
                return false;
            }
            if (!string.Equals(
                    configuredRoomScenePath,
                    FormalRoomScenePath,
                    StringComparison.OrdinalIgnoreCase)
                || Array.FindIndex(
                    productionScenes,
                    scenePath => string.Equals(
                        scenePath,
                        configuredRoomScenePath,
                        StringComparison.OrdinalIgnoreCase)) < 0)
            {
                error =
                    "GameBootstrapConfig room scene must resolve to a scene in the "
                    + "formal production build list. Configured path: "
                    + configuredRoomScenePath + ".";
                return false;
            }

            if (string.IsNullOrWhiteSpace(locationPathName))
            {
                error = "FPG build requires a Windows executable output path.";
                return false;
            }

            if (!string.Equals(
                    Path.GetExtension(locationPathName),
                    ".exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "FPG Windows build output must end with .exe.";
                return false;
            }

            string outputDirectory = Path.GetDirectoryName(locationPathName);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                error = "FPG Windows build output must include a directory.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static string GetProjectRootPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

    }
}
