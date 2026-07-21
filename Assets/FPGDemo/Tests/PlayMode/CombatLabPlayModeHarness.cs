using System;
using System.Collections;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Tests.PlayMode
{
    internal sealed class CombatLabPlayModeRuntime
    {
        public CombatLabPlayModeRuntime(
            Scene scene,
            BattleSceneContext context,
            BattleSessionHost host)
        {
            Scene = scene;
            Context = context;
            Host = host;
        }

        public Scene Scene { get; }
        public BattleSceneContext Context { get; }

        public BattleSceneContext ActiveContext => Context;
        public BattleSessionHost ActiveHost => Host;
        public BattleSessionHost Host { get; }
    }

    internal static class CombatLabPlayModeHarness
    {
        public static IEnumerator Load(
            Action<CombatLabPlayModeRuntime> completed)
        {
            Assert.That(completed, Is.Not.Null);
            yield return SceneManager.LoadSceneAsync(
                "CombatLab",
                LoadSceneMode.Single);

            Scene scene = SceneManager.GetSceneByName("CombatLab");
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);

            BattleSceneContext context =
                FindComponentInScene<BattleSceneContext>(scene);
            Assert.That(context, Is.Not.Null);
            BattleSessionHost host = context.SessionHost;
            Assert.That(host, Is.Not.Null);
            Assert.That(
                host.TryInitialize(
                    context,
                    context.ScenarioConfig,
                    out string initializationError),
                Is.True,
                initializationError);
            Assert.That(host.Session, Is.Not.Null);
            Assert.That(
                host.Session.State,
                Is.EqualTo(BattleSessionState.Running));

            completed(new CombatLabPlayModeRuntime(scene, context, host));
            yield return null;
        }

        private static T FindComponentInScene<T>(Scene scene)
            where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T component =
                    roots[index].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
