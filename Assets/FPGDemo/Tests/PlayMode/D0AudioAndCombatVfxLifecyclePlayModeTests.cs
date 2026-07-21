using System.Collections;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPG.Demo.Tests.PlayMode
{
    /// <summary>
    /// Exercises the installed G3 pools through the frozen CombatLab
    /// regression harness. This deliberately verifies runtime
    /// cleanup rather than duplicating the isolated component pool tests.
    /// </summary>
    public sealed class D0AudioAndCombatVfxLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator InstalledAudioAndCombatVfxPoolsReusePrewarmedInstancesAcrossRestart()
        {
            CombatLabPlayModeRuntime runtime = null;
            yield return CombatLabPlayModeHarness.Load(
                value => runtime = value);

            BattleSceneContext context = runtime.Context;
            BattleSessionHost host = runtime.Host;
            CombatAudioPresenter audio = context.D0CombatAudioPresenter;
            D0CombatVfxWorld vfxWorld = context.CombatVfxWorld;
            Assert.That(audio, Is.Not.Null);
            Assert.That(vfxWorld, Is.Not.Null);
            Assert.That(audio.IsInitialized, Is.True);
            Assert.That(vfxWorld.IsPrepared, Is.True);
            Assert.That(vfxWorld.IsCombatActive, Is.True);
            Assert.That(
                audio.CreatedSourceCount,
                Is.EqualTo(CombatAudioBank.DefaultConcurrentVoiceLimit));

            AudioSource[] sourcesBefore =
                audio.AudioSourceRoot.GetComponentsInChildren<AudioSource>(true);
            Assert.That(
                sourcesBefore,
                Has.Length.EqualTo(CombatAudioBank.DefaultConcurrentVoiceLimit),
                "The installed audio presenter must prewarm, not grow, its sources.");
            int prewarmedVfxCount = vfxWorld.PrewarmedInstanceCount;
            int vfxTransformCount =
                vfxWorld.PoolRoot.GetComponentsInChildren<Transform>(true).Length;

            D0WeaponDefinition weapon =
                context.ScenarioConfig.AuthoredScenario.Player.Weapon;
            Assert.That(
                context.PlayerEntity.SocketRegistry.TryResolve(
                    weapon.PrimaryPresentation.SocketId,
                    out Transform muzzle),
                Is.True);
            Assert.That(
                audio.TryPlayPresentationCue(CombatAudioCue.EnemyHeavyThreatTelegraph),
                Is.True,
                "A running session must accept a presentation-only D0 audio cue.");
            Assert.That(
                vfxWorld.TryAcquire(
                    weapon.PrimaryPresentation.MuzzleVfxKey,
                    muzzle,
                    out GameObject acquiredVfx),
                Is.True);
            Assert.That(acquiredVfx, Is.Not.Null);
            Assert.That(vfxWorld.ActiveInstanceCount, Is.GreaterThan(0));
            Assert.That(vfxWorld.HotPathInstantiateCount, Is.Zero);
            Assert.That(vfxWorld.HotPathDestroyCount, Is.Zero);

            yield return null;
            Assert.That(host.TryPause().IsSuccess, Is.True, host.LastError);
            yield return null;
            Assert.That(audio.IsSourcesPaused, Is.True);

            Assert.That(host.TryResume().IsSuccess, Is.True, host.LastError);
            yield return null;
            Assert.That(audio.IsSourcesPaused, Is.False);

            Assert.That(host.TryRestart().IsSuccess, Is.True, host.LastError);
            Assert.That(audio.ActiveVoiceCount, Is.Zero,
                "F5/restart must stop every pooled voice synchronously.");
            Assert.That(audio.IsSourcesPaused, Is.False);
            Assert.That(vfxWorld.ActiveInstanceCount, Is.Zero,
                "F5/restart must release every active combat VFX synchronously.");
            Assert.That(vfxWorld.IsCombatActive, Is.True);
            Assert.That(vfxWorld.PrewarmedInstanceCount, Is.EqualTo(prewarmedVfxCount));
            Assert.That(vfxWorld.HotPathInstantiateCount, Is.Zero);
            Assert.That(vfxWorld.HotPathDestroyCount, Is.Zero);
            Assert.That(
                vfxWorld.PoolRoot.GetComponentsInChildren<Transform>(true).Length,
                Is.EqualTo(vfxTransformCount));
            Assert.That(audio.CreatedSourceCount, Is.EqualTo(sourcesBefore.Length));

            AudioSource[] sourcesAfter =
                audio.AudioSourceRoot.GetComponentsInChildren<AudioSource>(true);
            Assert.That(sourcesAfter, Has.Length.EqualTo(sourcesBefore.Length));
            for (int index = 0; index < sourcesBefore.Length; index++)
            {
                Assert.That(
                    sourcesAfter[index],
                    Is.SameAs(sourcesBefore[index]),
                    "Restart must reuse the installed AudioSource pool rather than allocate a replacement voice.");
            }
        }




    }
}
