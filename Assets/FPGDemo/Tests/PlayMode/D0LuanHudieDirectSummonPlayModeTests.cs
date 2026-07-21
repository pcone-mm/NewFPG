using System.Collections;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPG.Demo.Tests.PlayMode
{
    public sealed class D0LuanHudieDirectSummonPlayModeTests
    {
        [UnityTest]
        public IEnumerator InstalledBattlePlaysBothDirectSummonPhasesOnce()
        {
            CombatLabPlayModeRuntime runtime = null;
            yield return CombatLabPlayModeHarness.Load(
                value => runtime = value);

            BattleSessionHost host = runtime.Host;
            BattleSceneContext context = runtime.Context;
            BattlePresentationCoordinator coordinator =
                context.PresentationCoordinator;
            D0CombatVfxWorld vfxWorld = context.CombatVfxWorld;
            CombatAudioPresenter audio = context.D0CombatAudioPresenter;
            D0LuanSummonHudieDefinition summon =
                host.ScenarioConfig.AuthoredScenario.LuanSummonHudie;
            Assert.That(summon, Is.Not.Null);
            Assert.That(
                context.D0EnemyBehaviorController.SummonAnimationMotionSkill,
                Is.SameAs(summon),
                "BattleSessionHost must inject the direct summon SO into the runtime-only behavior binding.");
            Assert.That(coordinator, Is.Not.Null);
            Assert.That(vfxWorld, Is.Not.Null);
            Assert.That(audio, Is.Not.Null);

            float deadline = Time.realtimeSinceStartup + 8f;
            while (host.Session.CurrentTick.Value < 285)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Assert.Fail("Battle did not reach the Hudie appearance boundary.");
                }

                yield return null;
            }

            yield return null;
            Assert.That(
                context.EnemyEntityWorld.ActiveEnemyDefinition.EnemyId,
                Is.EqualTo("hudie"));
            Assert.That(
                coordinator.DirectLuanSummonPresentationCount,
                Is.EqualTo(1));
            Assert.That(
                coordinator.DirectHudieAppearancePresentationCount,
                Is.EqualTo(1));
            Assert.That(audio.PlayedCueCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(vfxWorld.HotPathInstantiateCount, Is.Zero);
            Assert.That(vfxWorld.HotPathDestroyCount, Is.Zero);

            yield return null;
            Assert.That(
                coordinator.DirectLuanSummonPresentationCount,
                Is.EqualTo(1));
            Assert.That(
                coordinator.DirectHudieAppearancePresentationCount,
                Is.EqualTo(1));
        }




    }
}
