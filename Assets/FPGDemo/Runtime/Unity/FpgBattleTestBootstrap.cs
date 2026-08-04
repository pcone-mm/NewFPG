#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Unity
{
    [DefaultExecutionOrder(-11000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("FPG Demo/Bootstrap/Battle Test Bootstrap")]
    public sealed class FpgBattleTestBootstrap : MonoBehaviour
    {
        public const string FormalRoomScenePath =
            "Assets/FPGDemo/Scenes/FormalRoom.unity";

        [Header("Fallback Scene Presentation")]
        [SerializeField]
        private Camera bootstrapCamera;

        [SerializeField]
        private Light bootstrapDirectionalLight;

        private Scene formalRoomScene;
        private bool ownsFormalRoomScene;
        private bool shuttingDown;
        private FpgFormalEncounterHost formalHost;
        private FpgBattleGmRuntime gmRuntime;

#if UNITY_EDITOR
        private bool editorReadyPublished;

        public static event Action<FpgBattleTestBootstrap> EditorReady;
        public static event Action<FpgBattleTestBootstrap> EditorUnavailable;
#endif

        public FpgFormalEncounterHost FormalHost => formalHost;
        public FpgBattleGmRuntime GmRuntime => gmRuntime;
        public bool IsReady { get; private set; }
        public string LastError { get; private set; } = string.Empty;

        private IEnumerator Start()
        {
            SetFallbackPresentationEnabled(false);
            yield return BootstrapAsync();
        }

        public IEnumerator ShutdownAsync()
        {
            if (shuttingDown)
            {
                yield break;
            }

            shuttingDown = true;
            PublishEditorUnavailable();
            IsReady = false;
            gmRuntime?.Dispose();
            gmRuntime = null;

            if (formalHost != null)
            {
                formalHost.StopAndClear();
                FpgRoomArtSceneLoader loader = formalHost.RoomArtSceneLoader;
                if (loader != null && loader.HasActiveArtScene)
                {
                    bool unloaded = false;
                    string unloadError = string.Empty;
                    yield return loader.UnloadActiveAsync(
                        formalRoomScene,
                        (succeeded, error) =>
                        {
                            unloaded = succeeded;
                            unloadError = error;
                        });
                    if (!unloaded && string.IsNullOrEmpty(LastError))
                    {
                        LastError = "BattleTest 美术场景卸载失败："
                            + unloadError;
                    }
                }

                formalHost.Dispose();
                formalHost = null;
            }

            Scene bootstrapScene = gameObject.scene;
            if (bootstrapScene.IsValid() && bootstrapScene.isLoaded
                && SceneManager.GetActiveScene() != bootstrapScene)
            {
                SceneManager.SetActiveScene(bootstrapScene);
            }

            if (ownsFormalRoomScene && formalRoomScene.IsValid()
                && formalRoomScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(
                    formalRoomScene);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            formalRoomScene = default(Scene);
            ownsFormalRoomScene = false;
            SetFallbackPresentationEnabled(true);
            shuttingDown = false;
        }

        private IEnumerator BootstrapAsync()
        {
            Scene existing = SceneManager.GetSceneByPath(FormalRoomScenePath);
            if (existing.IsValid() && existing.isLoaded)
            {
                formalRoomScene = existing;
            }
            else
            {
                AsyncOperation loadOperation = null;
                try
                {
                    loadOperation = SceneManager.LoadSceneAsync(
                        FormalRoomScenePath,
                        LoadSceneMode.Additive);
                }
                catch (Exception exception)
                {
                    Fail("无法加载 FormalRoom：" + exception.Message);
                    yield break;
                }

                if (loadOperation == null)
                {
                    Fail("无法开始加载 FormalRoom。");
                    yield break;
                }

                ownsFormalRoomScene = true;
                yield return loadOperation;
                formalRoomScene = SceneManager.GetSceneByPath(
                    FormalRoomScenePath);
            }

            string error = string.Empty;
            if (!formalRoomScene.IsValid() || !formalRoomScene.isLoaded
                || !TryResolveFormalHost(
                    formalRoomScene,
                    out formalHost,
                    out error))
            {
                Fail(string.IsNullOrWhiteSpace(error)
                    ? "FormalRoom 加载失败。"
                    : error);
                yield return ShutdownAsync();
                yield break;
            }

            if (!SceneManager.SetActiveScene(formalRoomScene))
            {
                Fail("无法将 FormalRoom 设为活动场景。");
                yield return ShutdownAsync();
                yield break;
            }

            formalHost.SetPresentationEnabled(false);
            if (!formalHost.TryComposeDefaultPlayer(out error)
                || !formalHost.TryValidate(out error))
            {
                Fail("BattleTest 玩家组合失败：" + error);
                yield return ShutdownAsync();
                yield break;
            }

            bool artLoaded = false;
            string artError = string.Empty;
            yield return formalHost.RoomArtSceneLoader.LoadAsync(
                formalHost.RoomDefinition,
                formalHost.WorldCamera,
                formalHost.AimViewportSource,
                (succeeded, loadError) =>
                {
                    artLoaded = succeeded;
                    artError = loadError;
                });
            if (!artLoaded)
            {
                Fail("BattleTest 美术场景加载失败：" + artError);
                yield return ShutdownAsync();
                yield break;
            }

            if (!formalHost.TryPrepareAndStartSandbox(out error)
                || !formalHost.TryActivatePlayerPresentation(out error))
            {
                Fail("BattleTest 战斗沙盒启动失败：" + error);
                yield return ShutdownAsync();
                yield break;
            }

            formalHost.SetPresentationEnabled(true);
            const int maxStartupFixedTicks = 600;
            int startupFixedTicks = 0;
            while (formalHost.Session != null
                && formalHost.Session.State
                    == FPG.Demo.Run.FpgEncounterSessionState.NotStarted
                && formalHost.EncounterHost.IsPrepared
                && startupFixedTicks++ < maxStartupFixedTicks)
            {
                yield return new WaitForFixedUpdate();
            }

            if (formalHost.Session == null
                || formalHost.Session.State
                    != FPG.Demo.Run.FpgEncounterSessionState.Running)
            {
                Fail("BattleTest 战斗沙盒未进入运行状态："
                    + (string.IsNullOrWhiteSpace(
                            formalHost.EncounterHost.LastError)
                        ? formalHost.Session == null
                            ? "战斗会话不可用。"
                            : formalHost.Session.State.ToString()
                        : formalHost.EncounterHost.LastError));
                yield return ShutdownAsync();
                yield break;
            }

            FpgShootingDevelopmentPanel shootingPanel =
                formalHost.GetComponent<FpgShootingDevelopmentPanel>();
            if (shootingPanel != null)
            {
                shootingPanel.enabled = false;
            }

            gmRuntime = new FpgBattleGmRuntime(formalHost);
            IsReady = true;
            LastError = string.Empty;
            PublishEditorReady();
            Debug.Log("[FpgBattleTestBootstrap] BattleTest sandbox is ready.", this);
        }

        private static bool TryResolveFormalHost(
            Scene scene,
            out FpgFormalEncounterHost host,
            out string error)
        {
            host = null;
            int count = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                FpgFormalEncounterHost[] candidates = roots[rootIndex]
                    .GetComponentsInChildren<FpgFormalEncounterHost>(true);
                for (int index = 0; index < candidates.Length; index++)
                {
                    if (candidates[index] == null)
                    {
                        continue;
                    }

                    host = candidates[index];
                    count++;
                }
            }

            if (count != 1)
            {
                host = null;
                error = "FormalRoom requires exactly one FpgFormalEncounterHost; found "
                    + count + ".";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void SetFallbackPresentationEnabled(bool enabled)
        {
            if (bootstrapCamera != null)
            {
                bootstrapCamera.enabled = enabled;
            }

            if (bootstrapDirectionalLight != null)
            {
                bootstrapDirectionalLight.enabled = enabled;
            }
        }

        private void Fail(string error)
        {
            LastError = string.IsNullOrWhiteSpace(error)
                ? "BattleTest 启动失败。"
                : error;
            Debug.LogError("[FpgBattleTestBootstrap] " + LastError, this);
        }

        private void OnDestroy()
        {
            PublishEditorUnavailable();
            gmRuntime?.Dispose();
            gmRuntime = null;
            if (formalHost != null)
            {
                formalHost.StopAndClear();
                formalHost.Dispose();
            }

            if (!Application.isPlaying || !ownsFormalRoomScene
                || !formalRoomScene.IsValid() || !formalRoomScene.isLoaded)
            {
                return;
            }

            Scene fallback = gameObject.scene;
            if (fallback.IsValid() && fallback.isLoaded)
            {
                SceneManager.SetActiveScene(fallback);
            }

            SceneManager.UnloadSceneAsync(formalRoomScene);
        }

        private void PublishEditorReady()
        {
#if UNITY_EDITOR
            if (editorReadyPublished)
            {
                return;
            }

            editorReadyPublished = true;
            EditorReady?.Invoke(this);
#endif
        }

        private void PublishEditorUnavailable()
        {
#if UNITY_EDITOR
            if (!editorReadyPublished)
            {
                return;
            }

            editorReadyPublished = false;
            EditorUnavailable?.Invoke(this);
#endif
        }
    }
}
#endif
