using FPG.Demo.Editor.LevelAuthoring;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class BuildSettingsTests
    {
        [Test]
        public void ProductionSceneListExcludesBattleTest()
        {
            Assert.That(
                FpgProductionSceneList.TryBuild(
                    out string[] productionScenes,
                    out string error),
                Is.True,
                error);
            Assert.That(
                productionScenes,
                Does.Not.Contain(
                    FpgBattleTestDevelopmentSceneList.BattleTestScenePath));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    FpgBattleTestDevelopmentSceneList.BattleTestScenePath),
                Is.Not.Null);
        }

        [Test]
        public void SceneListValidationRejectsDisabledReorderedAndMissingEntries()
        {
            string[] expected =
            {
                FpgProductionSceneList.BootScenePath,
                FpgProductionSceneList.FormalRoomScenePath,
                "Assets/FPGDemo/Presentation/Level/Rooms/Forest/ART_Forest.unity"
            };
            EditorBuildSettingsScene[] disabled =
            {
                new EditorBuildSettingsScene(expected[0], true),
                new EditorBuildSettingsScene(expected[1], false),
                new EditorBuildSettingsScene(expected[2], true)
            };
            EditorBuildSettingsScene[] reordered =
            {
                new EditorBuildSettingsScene(expected[1], true),
                new EditorBuildSettingsScene(expected[0], true),
                new EditorBuildSettingsScene(expected[2], true)
            };
            EditorBuildSettingsScene[] missing =
            {
                new EditorBuildSettingsScene(expected[0], true),
                new EditorBuildSettingsScene(expected[1], true)
            };

            Assert.That(
                FpgProductionSceneList.TryValidateConfiguredScenes(
                    disabled,
                    expected,
                    out string disabledError),
                Is.False);
            StringAssert.Contains("disabled", disabledError);
            Assert.That(
                FpgProductionSceneList.TryValidateConfiguredScenes(
                    reordered,
                    expected,
                    out string reorderedError),
                Is.False);
            StringAssert.Contains("scene 0", reorderedError);
            Assert.That(
                FpgProductionSceneList.TryValidateConfiguredScenes(
                    missing,
                    expected,
                    out string missingError),
                Is.False);
            StringAssert.Contains("contain 2 scenes", missingError);
        }
    }
}
