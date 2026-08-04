#if UNITY_EDITOR
using System.Collections;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPG.Demo.Tests.PlayMode
{
    public sealed class FpgBattleTestPlayModeTests
    {
        private const string BattleTestScenePath =
            "Assets/InitTestScene/BattleTest.unity";
        private const string FormalRoomScenePath =
            "Assets/FPGDemo/Scenes/FormalRoom.unity";
        private const string ArtScenePath =
            "Assets/FPGDemo/Presentation/Level/Rooms/Forest/ART_Forest.unity";

        [UnityTest]
        public IEnumerator BattleTestBootsEmptySandboxAndRunsGmCommands()
        {
            Scene initialScene = SceneManager.GetActiveScene();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                BattleTestScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            Scene battleTestScene = SceneManager.GetSceneByPath(
                BattleTestScenePath);
            Assert.That(
                battleTestScene.IsValid() && battleTestScene.isLoaded,
                Is.True);

            FpgBattleTestBootstrap bootstrap =
                FindSingle<FpgBattleTestBootstrap>(battleTestScene);
            for (int frame = 0;
                frame < 900 && !bootstrap.IsReady
                    && string.IsNullOrEmpty(bootstrap.LastError);
                frame++)
            {
                yield return null;
            }

            Assert.That(bootstrap.IsReady, Is.True, bootstrap.LastError);
            Assert.That(bootstrap.FormalHost, Is.Not.Null);
            Assert.That(bootstrap.GmRuntime, Is.Not.Null);
            Assert.That(
                SceneManager.GetSceneByPath(FormalRoomScenePath).isLoaded,
                Is.True);
            Assert.That(
                SceneManager.GetSceneByPath(ArtScenePath).isLoaded,
                Is.True);

            FpgRoomEncounterDirector director =
                bootstrap.FormalHost.EncounterDirector;
            Assert.That(
                director.Session.State,
                Is.EqualTo(FpgEncounterSessionState.Running));
            Assert.That(
                director.Session.Runtime.Mode,
                Is.EqualTo(FpgEncounterRuntimeMode.BattleTestSandbox));
            Assert.That(director.ActiveEnemyCount, Is.Zero);
            Assert.That(director.PendingEntryCount, Is.Zero);

            FpgFormalPlayerTickDriver playerDriver =
                bootstrap.FormalHost.PlayerComposer.PlayerTickDriver;
            CombatAimReticle reticle =
                bootstrap.FormalHost.AimViewportSource as CombatAimReticle;
            Assert.That(reticle, Is.Not.Null);
            reticle.SetInputFrozen(true);
            float[] viewportSides = { 0.25f, 0.75f };
            for (int sideIndex = 0;
                sideIndex < viewportSides.Length;
                sideIndex++)
            {
                reticle.SetViewport(new Vector2(
                    viewportSides[sideIndex],
                    0.5f));
                for (int frame = 0; frame < 30; frame++)
                {
                    yield return null;
                    if (playerDriver.ResolvedAimContext.IsValid
                        && Mathf.Abs(
                            playerDriver.ResolvedAimContext
                                .ReticleViewport.x
                            - viewportSides[sideIndex]) < 0.001f
                        && playerDriver.PrimaryAttackAvailability.Ready)
                    {
                        break;
                    }
                }

                Assert.That(
                    playerDriver.ResolvedAimContext.IsCurrentCoverBlocked,
                    Is.False,
                    "Withdrawn aim must preview the selected side's peek origin.");
                Assert.That(
                    playerDriver.PrimaryAttackAvailability.Ready,
                    Is.True,
                    playerDriver.PrimaryAttackAvailability.Reason.ToString());
                Assert.That(
                    reticle.BaseState,
                    Is.Not.EqualTo(
                        FpgAimIndicatorBaseState.CurrentCoverBlocked));
            }
            reticle.SetInputFrozen(false);

            Assert.That(
                bootstrap.GmRuntime.TryExecute(
                    "gm.spawn not-in-catalog 1",
                    out string invalidResult),
                Is.False);
            StringAssert.Contains("未找到敌人配置 ID", invalidResult);

            Assert.That(
                bootstrap.GmRuntime.TryExecute(
                    "gm.spawn burstbug 1 enemy-any-01",
                    out string spawnResult),
                Is.True,
                spawnResult);
            FpgEnemySlot spawned = FindNewestReservedSlot(
                director.Session.Roster);
            Assert.That(spawned, Is.Not.Null);
            Assert.That(spawned.EnemyDefinitionId, Is.EqualTo("burstbug"));
            Assert.That(spawned.SpawnPointId, Is.EqualTo("enemy-any-01"));

            for (int frame = 0; frame < 600 && !spawned.IsActive; frame++)
            {
                yield return null;
            }

            Assert.That(spawned.IsActive, Is.True);
            Assert.That(director.CombatPort.TryGetEnemyRuntime(
                spawned.RuntimeId,
                out _), Is.True);

            Assert.That(
                bootstrap.GmRuntime.TryExecute(
                    "gm.ai off",
                    out string aiResult),
                Is.True,
                aiResult);
            Assert.That(director.CombatPort.IsEnemyAiEnabled, Is.False);
            Assert.That(
                bootstrap.GmRuntime.TryExecute(
                    "gm.god on",
                    out string godResult),
                Is.True,
                godResult);
            Assert.That(director.CombatPort.IsPlayerInvincible, Is.True);

            string[] roundRobin =
            {
                "enemy-any-01",
                "enemy-any-02",
                "enemy-any-03",
                "enemy-any-04"
            };
            for (int index = 0; index < roundRobin.Length; index++)
            {
                Assert.That(
                    bootstrap.GmRuntime.TryExecute(
                        "gm.spawn burstbug",
                        out string roundRobinResult),
                    Is.True,
                    roundRobinResult);
                FpgEnemySlot latest = FindNewestReservedSlot(
                    director.Session.Roster);
                Assert.That(
                    latest.SpawnPointId,
                    Is.EqualTo(roundRobin[index]));
            }

            Assert.That(
                bootstrap.GmRuntime.TryExecute(
                    "gm.spawn burstbug 2147483647",
                    out string capacityResult),
                Is.False);
            StringAssert.Contains("正式运行时容量已耗尽", capacityResult);

            yield return bootstrap.ShutdownAsync();
            if (initialScene.IsValid() && initialScene.isLoaded)
            {
                SceneManager.SetActiveScene(initialScene);
            }

            if (battleTestScene.IsValid() && battleTestScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(battleTestScene);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDownLoadedScenes()
        {
            string[] paths =
            {
                ArtScenePath,
                FormalRoomScenePath,
                BattleTestScenePath
            };
            for (int index = 0; index < paths.Length; index++)
            {
                Scene scene = SceneManager.GetSceneByPath(paths[index]);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                Scene fallback = FindFallback(scene);
                if (fallback.IsValid() && fallback.isLoaded)
                {
                    SceneManager.SetActiveScene(fallback);
                }

                yield return SceneManager.UnloadSceneAsync(scene);
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

        private static FpgEnemySlot FindNewestReservedSlot(
            FpgEnemyRoster roster)
        {
            FpgEnemySlot newest = null;
            for (int index = 0; index < roster.Capacity; index++)
            {
                FpgEnemySlot slot = roster.GetSlot(index);
                if (slot.IsReserved
                    && (newest == null
                        || slot.SpawnSequence > newest.SpawnSequence))
                {
                    newest = slot;
                }
            }

            return newest;
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
#endif
