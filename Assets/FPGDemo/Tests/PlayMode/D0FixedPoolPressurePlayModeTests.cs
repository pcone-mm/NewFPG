using System;
using System.Collections;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPG.Demo.Tests.PlayMode
{
    /// <summary>
    /// G5 capacity gate: fill the authored 32-projectile world and the 32-slot
    /// hit-tip pool through the frozen CombatLab regression harness. The test
    /// deliberately asserts fixed identity/counts rather than treating a
    /// rejected overflow as permission to grow a pool.
    /// </summary>
    public sealed class D0FixedPoolPressurePlayModeTests
    {
        [UnityTest]
        public IEnumerator InstalledD0PoolsSustainConfiguredProjectileAndHitTipPressureWithoutExpansion()
        {
            CombatLabPlayModeRuntime runtime = null;
            yield return CombatLabPlayModeHarness.Load(
                value => runtime = value);

            BattleSceneContext context = runtime.Context;
            BattleSessionHost host = runtime.Host;
            Assert.That(host.TryRestart().IsSuccess, Is.True, host.LastError);
            BattleSession session = host.Session;
            D0EnemyBehaviorController behavior = RequireD0EnemyBehavior(context);
            ProjectileViewPool projectilePool = context.PresentationCoordinator.ProjectileViewPool;
            D0HitTipPresenter hitTips = context.D0HitTipPresenter;
            CombatAudioPresenter audio = context.D0CombatAudioPresenter;
            Assert.That(context, Is.Not.Null);
            Assert.That(behavior, Is.Not.Null);
            Assert.That(projectilePool, Is.Not.Null);
            Assert.That(hitTips, Is.Not.Null);
            Assert.That(audio, Is.Not.Null);
            Assert.That(session.Definition.ProjectileCapacity, Is.EqualTo(32));
            Assert.That(projectilePool.Capacity,
                Is.GreaterThanOrEqualTo(session.Definition.ProjectileCapacity),
                "The authored visual catalog may reserve separate slots per presentation key, but it must cover the 32-projectile gameplay contract without growing at runtime.");
            Assert.That(hitTips.Capacity, Is.EqualTo(32));

            ProjectileView[] projectileViewsBefore =
                context.ProjectileViewRoot.GetComponentsInChildren<ProjectileView>(true);
            AudioSource[] audioSourcesBefore =
                audio.AudioSourceRoot.GetComponentsInChildren<AudioSource>(true);
            int hitTipChildrenBefore = hitTips.PoolRoot.childCount;
            Assert.That(projectileViewsBefore, Has.Length.EqualTo(projectilePool.Capacity));
            Assert.That(audioSourcesBefore,
                Has.Length.EqualTo(CombatAudioBank.DefaultConcurrentVoiceLimit));
            Assert.That(hitTipChildrenBefore, Is.EqualTo(hitTips.Capacity));

            UnityBattleInputSource idleInput = CreateIdleInput(context.AimAnchor);
            // Pressure must begin from the authored patrol lane, not while the
            // off-screen entry is still crossing environment cover. This is the
            // same spatial position from which standard attacks are released.
            while (session.CurrentTick.Value < 112L)
            {
                PumpTicks(session, idleInput, 1, behavior);
            }

            Assert.That(behavior.IsPatrolling, Is.True);

            ThreatDefinition pressureThreat = CreateFourProjectileThreat();
            for (int index = 0; index < 8; index++)
            {
                Assert.That(session.TryAddThreat(pressureThreat, out int threatIndex).IsSuccess,
                    Is.True,
                    "The authored threat capacity must accept eight four-projectile pressure threats.");
                Assert.That(session.TryStartThreat(threatIndex).IsSuccess, Is.True);
            }

            // Three ticks prove the initial spawn, but not the recorded sweep
            // path that backs the real 32-projectile presentation. Hold the
            // fully occupied world for four seconds to catch transcript or
            // pool-capacity regressions before an acceptance build is made.
            PumpTicks(session, idleInput, GameplayClock.DefaultTickRate * 4, behavior);
            yield return null;

            Assert.That(session.ActiveProjectileCount, Is.EqualTo(session.Definition.ProjectileCapacity));
            Assert.That(projectilePool.ActiveViewCount,
                Is.EqualTo(session.Definition.ProjectileCapacity));
            Assert.That(projectilePool.ViewPoolRejectCount, Is.Zero,
                "The prewarmed projectile pool must cover the full configured pressure, not drop views or grow.");
            Assert.That(session.State, Is.EqualTo(BattleSessionState.Running));
            Assert.That(host.IsSessionRunning, Is.True, host.LastError);
            Assert.That(host.SpatialTranscript.Count,
                Is.LessThan(host.SpatialTranscript.Capacity),
                "The authored CombatLab transcript must retain headroom while 32 real projectiles are swept for four seconds.");
            AssertProjectileIdentityIsStable(projectileViewsBefore, context.ProjectileViewRoot);

            for (int index = 0; index < hitTips.Capacity; index++)
            {
                Assert.That(
                    hitTips.TryShow(
                        D0HitTipKind.Body,
                        10 + index,
                        new Vector2(0.08f + (index % 8) * 0.11f, 0.18f + (index / 8) * 0.15f)),
                    Is.True);
            }

            Assert.That(hitTips.ActiveCount, Is.EqualTo(hitTips.Capacity));
            int hitTipRejectsBefore = hitTips.SpawnRejectCount;
            Assert.That(hitTips.TryShow(D0HitTipKind.Weakpoint, 999, CombatAimViewportMath.Center), Is.False);
            Assert.That(hitTips.SpawnRejectCount, Is.EqualTo(hitTipRejectsBefore + 1));
            Assert.That(hitTips.PoolRoot.childCount, Is.EqualTo(hitTipChildrenBefore));

            Assert.That(host.TryRestart().IsSuccess, Is.True, host.LastError);
            yield return null;

            Assert.That(projectilePool.ActiveViewCount, Is.Zero);
            Assert.That(hitTips.ActiveCount, Is.Zero);
            Assert.That(audio.ActiveVoiceCount, Is.Zero);
            AssertProjectileIdentityIsStable(projectileViewsBefore, context.ProjectileViewRoot);
            Assert.That(hitTips.PoolRoot.childCount, Is.EqualTo(hitTipChildrenBefore));
            AudioSource[] audioSourcesAfter =
                audio.AudioSourceRoot.GetComponentsInChildren<AudioSource>(true);
            Assert.That(audioSourcesAfter, Has.Length.EqualTo(audioSourcesBefore.Length));
            for (int index = 0; index < audioSourcesBefore.Length; index++)
            {
                Assert.That(audioSourcesAfter[index], Is.SameAs(audioSourcesBefore[index]));
            }
        }

        private static D0EnemyBehaviorController RequireD0EnemyBehavior(
            BattleSceneContext context)
        {
            Assert.That(context, Is.Not.Null);
            Assert.That(context.ScenarioConfig, Is.Not.Null);
            Assert.That(
                context.ScenarioConfig.UsesAuthoredScenario,
                Is.True,
                "This D0 test must use an authored scenario so runtime-binding preflight cannot no-op.");
            Assert.That(
                context.TryValidateD0RuntimeBindings(out string runtimeBindingError),
                Is.True,
                runtimeBindingError);

            D0EnemyBehaviorController behavior = context.D0EnemyBehaviorController;
            Assert.That(
                behavior,
                Is.Not.Null,
                "CombatLab must serialize its D0 enemy behavior controller on BattleSceneContext.");
            Assert.That(behavior.GameplayAnchor, Is.SameAs(context.ActiveEnemyGameplayAnchor));
            return behavior;
        }

        private static ThreatDefinition CreateFourProjectileThreat()
        {
            ProjectileDefinition projectile = new ProjectileDefinition(
                definitionId: 901,
                flightDuration: new TickDuration(300),
                expireDuration: new TickDuration(360),
                damageSpec: new DamageSpec(0, 0),
                maxHitPoints: 1,
                interceptable: true,
                budgetUnits: 1,
                presentationKey: 2,
                sweepRadiusKey: 250);
            return new ThreatDefinition(
                definitionId: 900,
                telegraphDuration: new TickDuration(1),
                windupDuration: new TickDuration(1),
                recoveryDuration: new TickDuration(300),
                projectileDefinition: projectile,
                payloadCount: 4);
        }

        private static UnityBattleInputSource CreateIdleInput(Transform aimAnchor)
        {
            UnityBattleInputSource input = new UnityBattleInputSource();
            input.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            input.CaptureAimPose(aimAnchor);
            return input;
        }

        private static void PumpTicks(
            BattleSession session,
            UnityBattleInputSource input,
            int tickCount,
            IBattleTickObserver tickObserver = null)
        {
            long wallTicks = (TimeSpan.TicksPerSecond + GameplayClock.DefaultTickRate - 1L)
                / GameplayClock.DefaultTickRate;
            for (int index = 0; index < tickCount; index++)
            {
                Assert.That(session.PumpWithBattleInput(
                        wallTicks,
                        input,
                        tickObserver,
                        out int executedSteps).IsSuccess,
                    Is.True);
                Assert.That(executedSteps, Is.EqualTo(1));
            }
        }

        private static void AssertProjectileIdentityIsStable(
            ProjectileView[] expected,
            Transform projectileRoot)
        {
            ProjectileView[] actual = projectileRoot.GetComponentsInChildren<ProjectileView>(true);
            Assert.That(actual, Has.Length.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index], Is.SameAs(expected[index]));
            }
        }




    }
}
