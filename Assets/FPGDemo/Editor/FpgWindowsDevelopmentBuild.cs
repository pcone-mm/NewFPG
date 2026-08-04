using System;
using System.IO;
using FPG.Demo.Editor.LevelAuthoring;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FPG.Demo.Editor
{
    public static class FpgWindowsDevelopmentBuild
    {
        public const string DefaultOutputRelativePath =
            "Builds/FPGDemo/WindowsDevelopment/FPGDemo-BattleTest.exe";

        [MenuItem("FPG Demo/Build Windows x64 Battle Test (Development)")]
        public static void BuildWindows64Development()
        {
            BuildWindows64DevelopmentAt(GetDefaultOutputPath());
        }

        public static void BuildWindows64DevelopmentFromBatch()
        {
            BuildWindows64DevelopmentAt(GetDefaultOutputPath());
        }

        internal static BuildReport BuildWindows64DevelopmentAt(
            string locationPathName)
        {
            if (string.IsNullOrWhiteSpace(locationPathName)
                || !string.Equals(
                    Path.GetExtension(locationPathName),
                    ".exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "FPG BattleTest development build requires a Windows .exe output path.");
            }

            if (!FpgBattleTestDevelopmentSceneList.TryBuild(
                    out string[] developmentScenes,
                    out string error))
            {
                throw new InvalidOperationException(error);
            }

            string outputDirectory = Path.GetDirectoryName(locationPathName);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException(
                    "FPG BattleTest development build output must include a directory.");
            }

            Directory.CreateDirectory(outputDirectory);
            EditorBuildSettingsScene[] originalGlobalScenes =
                EditorBuildSettings.globalScenes;
            EditorBuildSettingsScene[] originalActiveScenes =
                EditorBuildSettings.scenes;
            EditorBuildSettingsScene[] temporaryScenes =
                CreateBuildSettingsScenes(developmentScenes);
            try
            {
                EditorBuildSettings.globalScenes = temporaryScenes;
                EditorBuildSettings.scenes = temporaryScenes;

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = developmentScenes,
                    locationPathName = locationPathName,
                    targetGroup = BuildTargetGroup.Standalone,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                        | BuildOptions.AllowDebugging
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        "FPG BattleTest Development build failed. "
                        + "Review the Unity build log before retrying. "
                        + "errors=" + report.summary.totalErrors
                        + ", warnings=" + report.summary.totalWarnings
                        + ", output=" + locationPathName);
                }

                Debug.Log(
                    "[FPG_BUILD] BattleTest development success output="
                    + locationPathName + " size=" + report.summary.totalSize
                    + " scenes=" + string.Join(",", developmentScenes));
                return report;
            }
            finally
            {
                EditorBuildSettings.globalScenes = originalGlobalScenes;
                EditorBuildSettings.scenes = originalActiveScenes;
            }
        }

        private static EditorBuildSettingsScene[] CreateBuildSettingsScenes(
            string[] scenePaths)
        {
            EditorBuildSettingsScene[] scenes =
                new EditorBuildSettingsScene[scenePaths.Length];
            for (int index = 0; index < scenePaths.Length; index++)
            {
                scenes[index] = new EditorBuildSettingsScene(
                    scenePaths[index],
                    true);
            }

            return scenes;
        }

        private static string GetDefaultOutputPath()
        {
            return Path.Combine(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                DefaultOutputRelativePath);
        }
    }
}
