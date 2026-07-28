using System.Collections;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPG.Demo.Tests.PlayMode
{
    public sealed class FpgRoomArtSceneLoaderPlayModeTests
    {
        private const string FormalScenePath =
            "Assets/FPGDemo/Scenes/FormalRoom.unity";
        private const string ArtScenePath =
            "Assets/FPGDemo/Presentation/Level/Rooms/Forest/ART_Forest.unity";

        [UnityTest]
        public IEnumerator LoaderOwnsLoadUnloadAndFailedLoadRollback()
        {
            Scene initialScene = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync(
                FormalScenePath,
                LoadSceneMode.Additive);
            Scene formalScene = SceneManager.GetSceneByPath(FormalScenePath);
            Assert.That(formalScene.IsValid() && formalScene.isLoaded, Is.True);
            Assert.That(SceneManager.SetActiveScene(formalScene), Is.True);

            FpgFormalEncounterHost host =
                FindSingle<FpgFormalEncounterHost>(formalScene);
            Assert.That(host.TryValidateAuthoring(out string hostError), Is.True, hostError);
            FpgRoomArtSceneLoader loader = host.RoomArtSceneLoader;
            FpgRoomDefinition room = host.RoomDefinition;

            for (int cycle = 0; cycle < 2; cycle++)
            {
                bool loaded = false;
                string loadError = string.Empty;
                yield return loader.LoadAsync(
                    room,
                    host.WorldCamera,
                    host.AimViewportSource,
                    (succeeded, error) =>
                    {
                        loaded = succeeded;
                        loadError = error;
                    });
                Assert.That(loaded, Is.True, loadError);
                Assert.That(loader.HasActiveArtScene, Is.True);
                Assert.That(loader.ActiveArtScene.path, Is.EqualTo(ArtScenePath));
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(loader.ActiveArtScene));
                Assert.That(loader.ActiveArtRoot.RoomDefinition, Is.SameAs(room));
                Assert.That(
                    RenderSettings.sun,
                    Is.SameAs(loader.ActiveArtRoot.MainDirectionalLight));
                Assert.That(host.gameObject.scene, Is.EqualTo(formalScene));
                Assert.That(host.WorldCamera.gameObject.scene, Is.EqualTo(formalScene));
                Assert.That(loader.gameObject.scene, Is.EqualTo(formalScene));
                Assert.That(CountLoadedDirectionalLights(), Is.EqualTo(1));

                bool unloaded = false;
                string unloadError = string.Empty;
                yield return loader.UnloadActiveAsync(
                    formalScene,
                    (succeeded, error) =>
                    {
                        unloaded = succeeded;
                        unloadError = error;
                    });
                Assert.That(unloaded, Is.True, unloadError);
                Assert.That(loader.HasActiveArtScene, Is.False);
                Assert.That(SceneManager.GetSceneByPath(ArtScenePath).isLoaded, Is.False);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(formalScene));
                Assert.That(CountLoadedDirectionalLights(), Is.Zero);
            }

            FpgRoomDefinition mismatchedRoom =
                Object.Instantiate(room);
            try
            {
                bool loaded = true;
                string loadError = string.Empty;
                yield return loader.LoadAsync(
                    mismatchedRoom,
                    host.WorldCamera,
                    host.AimViewportSource,
                    (succeeded, error) =>
                    {
                        loaded = succeeded;
                        loadError = error;
                    });
                Assert.That(loaded, Is.False);
                Assert.That(loadError, Is.Not.Empty);
                Assert.That(loader.HasActiveArtScene, Is.False);
                Assert.That(SceneManager.GetSceneByPath(ArtScenePath).isLoaded, Is.False);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(formalScene));
            }
            finally
            {
                Object.Destroy(mismatchedRoom);
            }

            if (initialScene.IsValid() && initialScene.isLoaded)
            {
                SceneManager.SetActiveScene(initialScene);
            }
            yield return SceneManager.UnloadSceneAsync(formalScene);
        }

        [UnityTearDown]
        public IEnumerator TearDownLoadedScenes()
        {
            Scene artScene = SceneManager.GetSceneByPath(ArtScenePath);
            if (artScene.IsValid() && artScene.isLoaded)
            {
                Scene fallback = FindFallback(artScene);
                if (fallback.IsValid())
                {
                    SceneManager.SetActiveScene(fallback);
                }
                yield return SceneManager.UnloadSceneAsync(artScene);
            }

            Scene formalScene = SceneManager.GetSceneByPath(FormalScenePath);
            if (formalScene.IsValid() && formalScene.isLoaded)
            {
                Scene fallback = FindFallback(formalScene);
                if (fallback.IsValid())
                {
                    SceneManager.SetActiveScene(fallback);
                }
                yield return SceneManager.UnloadSceneAsync(formalScene);
            }
        }

        private static T FindSingle<T>(Scene scene)
            where T : Component
        {
            T found = null;
            int count = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                T[] values = roots[rootIndex].GetComponentsInChildren<T>(true);
                for (int index = 0; index < values.Length; index++)
                {
                    found = values[index];
                    count++;
                }
            }

            Assert.That(count, Is.EqualTo(1), typeof(T).Name);
            return found;
        }

        private static int CountLoadedDirectionalLights()
        {
            int count = 0;
            for (int sceneIndex = 0;
                sceneIndex < SceneManager.sceneCount;
                sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    Light[] lights =
                        roots[rootIndex].GetComponentsInChildren<Light>(true);
                    for (int lightIndex = 0;
                        lightIndex < lights.Length;
                        lightIndex++)
                    {
                        if (lights[lightIndex].type == LightType.Directional)
                        {
                            count++;
                        }
                    }
                }
            }

            return count;
        }

        private static Scene FindFallback(Scene excluded)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene candidate = SceneManager.GetSceneAt(index);
                if (candidate.IsValid() && candidate.isLoaded
                    && candidate != excluded)
                {
                    return candidate;
                }
            }

            return default(Scene);
        }
    }
}
