using System;
using System.Collections;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPG.Demo.Tests.PlayMode
{
    public sealed class LuanEnemyLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator HatchRebindsUnitySpatialAndBehaviorStateToNewEnemyRuntime()
        {
            CombatLabPlayModeRuntime runtime = null;
            yield return CombatLabPlayModeHarness.Load(
                value => runtime = value);

            BattleSceneContext context = runtime.Context;
            BattleSessionHost host = runtime.Host;
            Assert.That(context, Is.Not.Null);
            Assert.That(host, Is.Not.Null);
            Assert.That(
                context.ScenarioConfig.AuthoredScenario.EncounterContract,
                Is.EqualTo(D0EncounterContract.LuanHudieSingleProjectile));
            Assert.That(host.TryRestart().IsSuccess, Is.True, host.LastError);

            BattleSession session = host.Session;
            D0EnemyEntityWorld entityWorld = context.EnemyEntityWorld;
            Assert.That(entityWorld, Is.Not.Null);
            Assert.That(entityWorld.IsPrepared, Is.True);
            Assert.That(entityWorld.EntityCount, Is.EqualTo(2));
            D0EnemyEntityView luanEntity = entityWorld.ActiveEntity;
            Assert.That(entityWorld.ActiveEnemyDefinition.EnemyId, Is.EqualTo("luan"));
            Assert.That(context.ActiveD0EnemyActorPresenter, Is.SameAs(luanEntity.ActorPresenter));
            AssertEnemyVisualCalibration(luanEntity, entityWorld.ActiveEnemyDefinition);

            luanEntity.VisualRoot.localPosition += new Vector3(2f, 3f, 4f);
            luanEntity.GameplayAnchor.localPosition = new Vector3(5f, 0f, 0f);
            Assert.That(host.TryRestart().IsSuccess, Is.True, host.LastError);
            session = host.Session;
            luanEntity = entityWorld.ActiveEntity;
            AssertEnemyVisualCalibration(luanEntity, entityWorld.ActiveEnemyDefinition);
            Assert.That(luanEntity.GameplayAnchor.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(luanEntity.GameplayAnchor.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(luanEntity.GameplayAnchor.localScale, Is.EqualTo(Vector3.one));

            D0EnemyBehaviorController behavior = context.D0EnemyBehaviorController;
            Assert.That(behavior, Is.Not.Null);
            UnityBattleInputSource input = new UnityBattleInputSource();
            input.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            input.CaptureAimPose(context.AimAnchor);

            RuntimeId eggRuntimeId = session.EnemyRuntimeId;
            Vector3 inheritedGameplayPosition =
                luanEntity.GameplayAnchor.position;
            Quaternion inheritedGameplayRotation =
                luanEntity.GameplayAnchor.rotation;
            D0ActorSocketRegistry luanSockets = luanEntity.SocketRegistry;
            Collider luanBodyHitbox = luanEntity.BodyHitbox;
            Collider luanWeakpointHitbox = luanEntity.WeakpointHitbox;
            while (session.CurrentTick.Value < 284L)
            {
                PumpOneTick(session, input, behavior);
            }

            Assert.That(session.State, Is.EqualTo(BattleSessionState.Running));
            Assert.That(session.EnemyRuntimeId, Is.Not.EqualTo(eggRuntimeId));
            Assert.That(session.EnemyRuntimeCount, Is.EqualTo(2));
            Assert.That(session.ActiveEnemyDefinitionId, Is.EqualTo(2));
            Assert.That(session.GetFinalSnapshot().EnemyDefinitionId, Is.EqualTo(2));
            Assert.That(entityWorld.ActiveEntity, Is.Not.SameAs(luanEntity));
            Assert.That(entityWorld.ActiveEnemyDefinition.EnemyId, Is.EqualTo("hudie"));
            Assert.That(luanEntity.gameObject.activeSelf, Is.False);
            Assert.That(entityWorld.ActiveEntity.gameObject.activeSelf, Is.True);
            Assert.That(
                context.ActiveD0EnemyActorPresenter,
                Is.SameAs(entityWorld.ActiveEntity.ActorPresenter));
            AssertEnemyVisualCalibration(
                entityWorld.ActiveEntity,
                entityWorld.ActiveEnemyDefinition);
            Assert.That(
                (entityWorld.ActiveEntity.GameplayAnchor.position
                    - inheritedGameplayPosition).sqrMagnitude,
                Is.LessThan(0.000001f));
            Assert.That(
                Quaternion.Angle(
                    entityWorld.ActiveEntity.GameplayAnchor.rotation,
                    inheritedGameplayRotation),
                Is.LessThan(0.01f));
            Assert.That(entityWorld.ActiveEntity.SocketRegistry, Is.Not.SameAs(luanSockets));
            Assert.That(entityWorld.ActiveEntity.BodyHitbox, Is.Not.SameAs(luanBodyHitbox));
            Assert.That(
                entityWorld.ActiveEntity.WeakpointHitbox,
                Is.Not.SameAs(luanWeakpointHitbox));
            Assert.That(
                entityWorld.ActiveEntity.SocketRegistry.TryResolve(
                    D0ActorSocketRegistry.DefaultAttackOriginId,
                    out Transform hudieAttackOrigin),
                Is.True);
            Assert.That(
                hudieAttackOrigin.IsChildOf(entityWorld.ActiveEntity.transform),
                Is.True);
            Assert.That(
                entityWorld.ActiveEntity.WeakpointAnchor.IsChildOf(
                    entityWorld.ActiveEntity.GameplayAnchor),
                Is.True);
            Assert.That(
                behavior.ActiveBehaviorProfile,
                Is.SameAs(
                    context.ScenarioConfig.AuthoredScenario
                        .LuanSummonHudie.HudieEnemy.BehaviorProfile));

            Assert.That(
                context.HitboxRegistry.TryResolve(new GeometryId(2001), out RegisteredHitbox body),
                Is.True);
            Assert.That(body.RuntimeId, Is.EqualTo(session.EnemyRuntimeId));
            Assert.That(
                context.HitboxRegistry.TryResolve(new GeometryId(2002), out RegisteredHitbox weakpoint),
                Is.True);
            Assert.That(weakpoint.RuntimeId, Is.EqualTo(session.EnemyRuntimeId));
            Assert.That(host.IsProjectileWorldReady, Is.True, host.LastError);

            yield return null;
            Assert.That(entityWorld.ActiveEntity.gameObject.activeSelf, Is.True);
            Assert.That(luanEntity.gameObject.activeSelf, Is.False);
            Assert.That(
                context.PresentationCoordinator.DirectHudieAppearancePresentationCount,
                Is.EqualTo(1),
                "Hudie appearance must be presented once by the direct summon timeline.");

            while (session.CurrentTick.Value < 390L)
            {
                PumpOneTick(session, input, behavior);
            }

            Assert.That(session.ThreatCount, Is.GreaterThan(0));
            bool sawButterflyThreat = false;
            for (int index = 0; index < session.Trace.Count; index++)
            {
                CombatEvent combatEvent = session.Trace.GetOldest(index);
                if (combatEvent.EventType == CombatEventType.ThreatStateChanged
                    && combatEvent.SourceId == session.EnemyRuntimeId)
                {
                    sawButterflyThreat = true;
                    break;
                }
            }

            Assert.That(sawButterflyThreat, Is.True);
        }

        private static void AssertEnemyVisualCalibration(
            D0EnemyEntityView entity,
            D0EnemyDefinition enemy)
        {
            Assert.That(entity, Is.Not.Null);
            Assert.That(enemy, Is.Not.Null);
            D0EnemyEntityView prefab = enemy.EntityPrefab;
            Assert.That(prefab, Is.Not.Null);
            Assert.That(
                (entity.VisualRoot.localPosition
                    - prefab.VisualRoot.localPosition).sqrMagnitude,
                Is.LessThan(0.000001f));
            Assert.That(
                Quaternion.Angle(
                    entity.VisualRoot.localRotation,
                    prefab.VisualRoot.localRotation),
                Is.LessThan(0.01f));
            Assert.That(
                (entity.VisualRoot.localScale
                    - prefab.VisualRoot.localScale).sqrMagnitude,
                Is.LessThan(0.000001f));
        }

        private static void PumpOneTick(
            BattleSession session,
            UnityBattleInputSource input,
            D0EnemyBehaviorController behavior)
        {
            long oneTick = (TimeSpan.TicksPerSecond + GameplayClock.DefaultTickRate - 1L)
                / GameplayClock.DefaultTickRate;
            DomainResult pumped = session.PumpWithBattleInput(
                oneTick,
                input,
                behavior,
                out int executedSteps);
            Assert.That(pumped.IsSuccess, Is.True, pumped.ToString());
            Assert.That(executedSteps, Is.EqualTo(1));
        }




    }
}
