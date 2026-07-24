using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class BuildSettingsTests
    {
        private const string BootScenePath = "Assets/FPGDemo/Scenes/Boot.unity";
        private const string FormalRoomScenePath = "Assets/FPGDemo/Scenes/FormalRoom.unity";


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
                enabledScenePaths,
                Is.EqualTo(new[] { BootScenePath, FormalRoomScenePath }),
                "The production build list must contain only Boot and FormalRoom at stable indices 0 and 1.");
        }
    }
}
