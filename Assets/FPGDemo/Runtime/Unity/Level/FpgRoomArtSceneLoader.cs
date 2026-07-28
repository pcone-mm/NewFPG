using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgRoomArtSceneLoader : MonoBehaviour
    {
        private Scene activeArtScene;
        private FpgRoomArtRoot activeArtRoot;
        private bool operationInProgress;

        public Scene ActiveArtScene => activeArtScene;
        public FpgRoomArtRoot ActiveArtRoot => activeArtRoot;
        public bool HasActiveArtScene => activeArtScene.IsValid()
            && activeArtScene.isLoaded;
        public bool IsOperationInProgress => operationInProgress;

        public bool TryValidateAuthoring(out string error)
        {
            Scene ownerScene = gameObject.scene;
            if (!ownerScene.IsValid())
            {
                error = "Room Art Scene Loader must belong to a valid scene.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public IEnumerator LoadAsync(
            FpgRoomDefinition roomDefinition,
            Camera formalCamera,
            ICombatAimViewportSource aimViewportSource,
            Action<bool, string> completed)
        {
            if (operationInProgress)
            {
                Complete(completed, false,
                    "A room Art Scene operation is already in progress.");
                yield break;
            }

            if (HasActiveArtScene)
            {
                Complete(completed, false,
                    $"Art Scene '{activeArtScene.path}' is still active and must be unloaded before loading another room.");
                yield break;
            }

            if (roomDefinition == null)
            {
                Complete(completed, false,
                    "A RoomDefinition is required to load an Art Scene.");
                yield break;
            }

            string scenePath = roomDefinition.ArtScenePath;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                Complete(completed, false,
                    $"Room '{roomDefinition.RoomId}' has no Art Scene path.");
                yield break;
            }

            if (formalCamera == null)
            {
                Complete(completed, false,
                    "Room Art Scene loading requires the explicit formal Camera.");
                yield break;
            }

            Scene ownerScene = gameObject.scene;
            if (!ownerScene.IsValid() || !ownerScene.isLoaded)
            {
                Complete(completed, false,
                    "Room Art Scene Loader must belong to a loaded FormalRoom scene.");
                yield break;
            }

            Scene existingScene = SceneManager.GetSceneByPath(scenePath);
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                Complete(completed, false,
                    $"Art Scene '{scenePath}' is already loaded outside this loader's ownership.");
                yield break;
            }

            operationInProgress = true;
            AsyncOperation loadOperation = null;
            string failure = string.Empty;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(
                    scenePath,
                    LoadSceneMode.Additive);
            }
            catch (Exception exception)
            {
                failure =
                    $"Unable to start loading Art Scene '{scenePath}': {exception.Message}";
            }

            if (loadOperation == null)
            {
                operationInProgress = false;
                Complete(completed, false,
                    string.IsNullOrEmpty(failure)
                        ? $"Unable to start loading Art Scene '{scenePath}'."
                        : failure);
                yield break;
            }

            yield return loadOperation;

            Scene loadedScene = SceneManager.GetSceneByPath(scenePath);
            FpgRoomArtRoot resolvedRoot = null;
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                failure = $"Art Scene '{scenePath}' did not finish loading.";
            }
            else if (!string.Equals(
                    loadedScene.path,
                    scenePath,
                    StringComparison.Ordinal))
            {
                failure =
                    $"Loaded Art Scene path '{loadedScene.path}' does not match requested path '{scenePath}'.";
            }
            else if (!FpgRoomArtRoot.TryResolve(
                    loadedScene,
                    roomDefinition,
                    out resolvedRoot,
                    out failure))
            {
            }
            else if (!SceneManager.SetActiveScene(loadedScene))
            {
                failure = $"Unable to make Art Scene '{scenePath}' active.";
            }
            else if (RenderSettings.sun != resolvedRoot.MainDirectionalLight)
            {
                failure =
                    $"Art Scene '{scenePath}' RenderSettings.sun must reference its declared main directional light.";
            }
            else
            {
                FpgRoomArtPresentationContext context =
                    new FpgRoomArtPresentationContext(
                        formalCamera,
                        resolvedRoot.MainDirectionalLight,
                        aimViewportSource);
                if (!resolvedRoot.TryBindPresentation(context, out failure))
                {
                    failure = "Art Scene presentation binding failed: " + failure;
                }
                else
                {
                    try
                    {
                        LightProbes.Tetrahedralize();
                    }
                    catch (Exception exception)
                    {
                        failure =
                            "Light Probe tetrahedralization failed after loading Art Scene: "
                            + exception.Message;
                    }
                }
            }

            if (!string.IsNullOrEmpty(failure))
            {
                yield return RollbackFailedLoad(
                    loadedScene,
                    ownerScene,
                    resolvedRoot);
                operationInProgress = false;
                Complete(completed, false, failure);
                yield break;
            }

            activeArtScene = loadedScene;
            activeArtRoot = resolvedRoot;
            operationInProgress = false;
            Complete(completed, true, string.Empty);
        }

        public IEnumerator UnloadActiveAsync(
            Scene fallbackActiveScene,
            Action<bool, string> completed)
        {
            if (operationInProgress)
            {
                Complete(completed, false,
                    "A room Art Scene operation is already in progress.");
                yield break;
            }

            if (!HasActiveArtScene)
            {
                activeArtScene = default(Scene);
                activeArtRoot = null;
                Complete(completed, true, string.Empty);
                yield break;
            }

            operationInProgress = true;
            string failure = string.Empty;
            Scene sceneToUnload = activeArtScene;
            FpgRoomArtRoot rootToUnbind = activeArtRoot;

            if (!TrySetFallbackActiveScene(fallbackActiveScene, out failure))
            {
                operationInProgress = false;
                Complete(completed, false, failure);
                yield break;
            }

            if (rootToUnbind == null)
            {
                failure =
                    $"Art Scene '{sceneToUnload.path}' lost its FpgRoomArtRoot before unload.";
            }
            else if (!rootToUnbind.TryUnbindPresentation(
                         out string unbindError))
            {
                failure = unbindError;
            }

            AsyncOperation unloadOperation = null;
            try
            {
                unloadOperation = SceneManager.UnloadSceneAsync(sceneToUnload);
            }
            catch (Exception exception)
            {
                failure = AppendFailure(
                    failure,
                    $"Unable to start unloading Art Scene '{sceneToUnload.path}': {exception.Message}");
            }

            if (unloadOperation == null)
            {
                operationInProgress = false;
                Complete(completed, false,
                    AppendFailure(
                        failure,
                        $"Unable to start unloading Art Scene '{sceneToUnload.path}'."));
                yield break;
            }

            yield return unloadOperation;
            activeArtScene = default(Scene);
            activeArtRoot = null;
            try
            {
                LightProbes.Tetrahedralize();
            }
            catch (Exception exception)
            {
                failure = AppendFailure(
                    failure,
                    "Light Probe tetrahedralization failed after unloading Art Scene: "
                    + exception.Message);
            }

            operationInProgress = false;
            Complete(completed, string.IsNullOrEmpty(failure), failure);
        }

        private IEnumerator RollbackFailedLoad(
            Scene loadedScene,
            Scene fallbackActiveScene,
            FpgRoomArtRoot resolvedRoot)
        {
            resolvedRoot?.UnbindPresentation();
            TrySetFallbackActiveScene(fallbackActiveScene, out _);

            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                AsyncOperation unloadOperation = null;
                try
                {
                    unloadOperation = SceneManager.UnloadSceneAsync(loadedScene);
                }
                catch (Exception)
                {
                }

                if (unloadOperation != null)
                {
                    yield return unloadOperation;
                }
            }

            try
            {
                LightProbes.Tetrahedralize();
            }
            catch (Exception)
            {
            }
        }

        private static bool TrySetFallbackActiveScene(
            Scene fallbackActiveScene,
            out string error)
        {
            if (!fallbackActiveScene.IsValid() || !fallbackActiveScene.isLoaded)
            {
                error =
                    "A valid loaded fallback scene is required before unloading an Art Scene.";
                return false;
            }

            if (SceneManager.GetActiveScene() != fallbackActiveScene
                && !SceneManager.SetActiveScene(fallbackActiveScene))
            {
                error =
                    $"Unable to make fallback scene '{fallbackActiveScene.path}' active.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static string AppendFailure(string current, string next)
        {
            return string.IsNullOrEmpty(current)
                ? next
                : current + " " + next;
        }

        private static void Complete(
            Action<bool, string> completed,
            bool succeeded,
            string error)
        {
            completed?.Invoke(succeeded, error ?? string.Empty);
        }

        private void OnDestroy()
        {
            Scene sceneToUnload = activeArtScene;
            activeArtRoot?.UnbindPresentation();
            activeArtRoot = null;
            activeArtScene = default(Scene);
            operationInProgress = false;

            if (!Application.isPlaying
                || !sceneToUnload.IsValid()
                || !sceneToUnload.isLoaded)
            {
                return;
            }

            Scene fallback = FindLoadedFallbackScene(sceneToUnload);
            if (fallback.IsValid() && fallback.isLoaded
                && SceneManager.GetActiveScene() == sceneToUnload)
            {
                SceneManager.SetActiveScene(fallback);
            }

            try
            {
                if (SceneManager.UnloadSceneAsync(sceneToUnload) == null)
                {
                    Debug.LogError(
                        $"[{nameof(FpgRoomArtSceneLoader)}] Could not start emergency unload for Art Scene '{sceneToUnload.path}'.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[{nameof(FpgRoomArtSceneLoader)}] Emergency Art Scene unload failed: {exception.Message}");
            }
        }

        private Scene FindLoadedFallbackScene(Scene excludedScene)
        {
            Scene ownerScene = gameObject.scene;
            if (ownerScene.IsValid() && ownerScene.isLoaded
                && ownerScene != excludedScene)
            {
                return ownerScene;
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene candidate = SceneManager.GetSceneAt(index);
                if (candidate.IsValid() && candidate.isLoaded
                    && candidate != excludedScene)
                {
                    return candidate;
                }
            }

            return default(Scene);
        }
    }
}
