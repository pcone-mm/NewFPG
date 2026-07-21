using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class BuildSettingsTests
    {
        private const string BootScenePath = "Assets/FPGDemo/Scenes/Boot.unity";
        
        private const string FormalRoomScenePath = "Assets/FPGDemo/Scenes/FormalRoom.unity";
private const string CombatLabScenePath = "Assets/FPGDemo/Scenes/CombatLab.unity";

        [Test]
        public void FormalFlowScenesHaveStableEnabledBuildIndices()
        {
            EditorBuildSettingsScene[] configuredScenes = EditorBuildSettings.scenes;
            List<string> enabledScenePaths = new List<string>(configuredScenes.Length);
            for (int index = 0; index < configuredScenes.Length; index++)
            {
                EditorBuildSettingsScene scene = configuredScenes[index];
                if (scene != null && scene.enabled)
                {
                    enabledScenePaths.Add(scene.path);
                }
            }

            Assert.That(
                enabledScenePaths.Count,
                Is.GreaterThanOrEqualTo(3),
                "The editor build list requires Boot, legacy CombatLab and FormalRoom.");
            Assert.That(
                enabledScenePaths[0],
                Is.EqualTo(BootScenePath),
                "Boot must be enabled build index 0 so a player build enters the FPG demo.");
            Assert.That(
                enabledScenePaths[1],
                Is.EqualTo(CombatLabScenePath),
                "CombatLab remains build index 1 for direct legacy regression only.");
            Assert.That(
                enabledScenePaths[2],
                Is.EqualTo(FormalRoomScenePath),
                "FormalRoom must be enabled build index 2 so Boot can load the production encounter scene.");
        }
    }
}
