using System.Collections.Generic;
using FPG.Demo.Editor.LevelAuthoring;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class BuildSettingsTests
    {
        [Test]
        public void FormalFlowScenesHaveStableEnabledBuildIndices()
        {
            Assert.That(
                FpgProductionSceneList.TryValidateEditorBuildSettings(
                    out string validationError),
                Is.True,
                validationError);
            Assert.That(
                FpgProductionSceneList.TryBuild(
                    out string[] expectedPaths,
                    out string buildError),
                Is.True,
                buildError);
            EditorBuildSettingsScene[] configuredScenes = EditorBuildSettings.scenes;
            Assert.That(configuredScenes, Has.Length.EqualTo(expectedPaths.Length));
            for (int index = 0; index < configuredScenes.Length; index++)
            {
                EditorBuildSettingsScene scene = configuredScenes[index];
                Assert.That(scene, Is.Not.Null, $"Build scene {index}");
                Assert.That(scene.enabled, Is.True, scene.path);
                Assert.That(scene.path, Is.EqualTo(expectedPaths[index]));
            }

            Assert.That(expectedPaths[0], Is.EqualTo(FpgProductionSceneList.BootScenePath));
            Assert.That(expectedPaths[1], Is.EqualTo(FpgProductionSceneList.FormalRoomScenePath));

            FpgRoomCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FpgRoomCatalog>(
                    FpgRoomArtSceneEditorUtility.RoomCatalogPath);
            Assert.That(catalog, Is.Not.Null);
            List<FpgRoomDefinition> rooms =
                new List<FpgRoomDefinition>(catalog.Rooms);
            rooms.Sort((left, right) => string.CompareOrdinal(
                left.RoomId,
                right.RoomId));
            Assert.That(expectedPaths, Has.Length.EqualTo(rooms.Count + 2));
            for (int index = 0; index < rooms.Count; index++)
            {
                Assert.That(expectedPaths[index + 2], Is.EqualTo(rooms[index].ArtScenePath));
            }
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
