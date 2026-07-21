using System;
using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0ActorPresentationRoutingTests
    {
        private static readonly RuntimeId Player = new RuntimeId(101);
        private static readonly RuntimeId Enemy = new RuntimeId(202);

        private const string PresentationProfilePath =
            "Assets/FPGDemo/Config/D0Slice/CombatPresentationProfile.asset";

        private const string PlayerPrefabPath =
            "Assets/FPGDemo/Presentation/Actors/Fei/PF_D0_FeiEntity.prefab";

        private const string EnemyPrefabPath =
            "Assets/FPGDemo/Presentation/D0Slice/Spine/PF_D0_BurstbugEntity.prefab";

        private const string PlayerPresentationPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_Presentation.asset";

        private const string PlayerWeaponPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_Weapon.asset";

        private const string EnemyPresentationPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Presentation.asset";

        [Test]
        public void DamageMapsLifeHitsButIgnoresPlayerBarrierHits()
        {
            Assert.That(
                Resolve(CombatEventType.DamageApplied, Player, Enemy, 10, 6, false),
                Is.EqualTo("EnemyHit"));
            Assert.That(
                Resolve(CombatEventType.DamageApplied, Player, Enemy, 10, 6, true),
                Is.EqualTo("None"));
            Assert.That(
                Resolve(
                    CombatEventType.DamageApplied,
                    Enemy,
                    Player,
                    8,
                    4,
                    false,
                    damageChannel: DamageChannel.Life),
                Is.EqualTo("PlayerHit"));
            Assert.That(
                Resolve(
                    CombatEventType.DamageApplied,
                    Enemy,
                    Player,
                    8,
                    4,
                    false,
                    damageChannel: DamageChannel.Barrier),
                Is.EqualTo("None"));
            Assert.That(
                Resolve(CombatEventType.DamageApplied, Player, Enemy, 8, 8, false),
                Is.EqualTo("None"));
        }

        [Test]
        public void BreakAndDeathCommandsRespectTheFrozenDomainSemantics()
        {
            Assert.That(
                Resolve(CombatEventType.BarrierBroken, Enemy, Player, 0, 0, false),
                Is.EqualTo("PlayerGroggy"));
            Assert.That(
                Resolve(CombatEventType.GroggyStarted, Enemy, Enemy, 0, 1, false),
                Is.EqualTo("EnemyGroggyStarted"));
            Assert.That(
                Resolve(CombatEventType.GroggyEnded, Enemy, Enemy, 1, 0, true),
                Is.EqualTo("EnemyGroggyEnded"));
            Assert.That(
                Resolve(CombatEventType.Death, Player, Enemy, 4, 0, false),
                Is.EqualTo("EnemyDeath"));
            Assert.That(
                Resolve(CombatEventType.Death, Enemy, Player, 4, 0, false),
                Is.EqualTo("PlayerDefeat"));
        }

        [Test]
        public void CompletionUsesOnlyExplicitVictoryAndDefeatReasons()
        {
            Assert.That(
                Resolve(
                    CombatEventType.BattleCompleted,
                    RuntimeId.Invalid,
                    RuntimeId.Invalid,
                    0,
                    (int)BattleCompletionReason.Victory,
                    false),
                Is.EqualTo("PlayerVictory"));
            Assert.That(
                Resolve(
                    CombatEventType.BattleCompleted,
                    RuntimeId.Invalid,
                    RuntimeId.Invalid,
                    0,
                    (int)BattleCompletionReason.Defeat,
                    false),
                Is.EqualTo("PlayerDefeat"));
            Assert.That(
                Resolve(
                    CombatEventType.BattleCompleted,
                    RuntimeId.Invalid,
                    RuntimeId.Invalid,
                    0,
                    (int)BattleCompletionReason.External,
                    false),
                Is.EqualTo("None"));
        }

        [Test]
        public void SecondaryActorTransitionsUseTheCommittedTraceOrder()
        {
            Assert.That(
                Resolve(
                    CombatEventType.InputAccepted,
                    Player,
                    RuntimeId.Invalid,
                    (int)WeaponState.Ready,
                    (int)WeaponState.AltCharging,
                    false),
                Is.EqualTo("PlayerSecondaryChargeStarted"));
            Assert.That(
                Resolve(
                    CombatEventType.InputAccepted,
                    Player,
                    RuntimeId.Invalid,
                    (int)WeaponState.AltCharging,
                    (int)WeaponState.Ready,
                    false),
                Is.EqualTo("PlayerSecondaryChargeCanceled"));
            Assert.That(
                Resolve(
                    CombatEventType.AttackCanceled,
                    Player,
                    RuntimeId.Invalid,
                    (int)WeaponState.AltCharging,
                    (int)WeaponState.Ready,
                    false),
                Is.EqualTo("PlayerSecondaryChargeCanceled"));
            Assert.That(
                Resolve(
                    CombatEventType.InputAccepted,
                    Player,
                    RuntimeId.Invalid,
                    (int)WeaponState.AltCharging,
                    (int)WeaponState.AltRecovery,
                    false,
                    42L),
                Is.EqualTo("PlayerSecondaryReleaseCommitted"));
            Assert.That(
                Resolve(
                    CombatEventType.ReleaseCommitted,
                    Player,
                    RuntimeId.Invalid,
                    8,
                    7,
                    false),
                Is.EqualTo("None"));
        }

        [Test]
        public void ReloadActorTransitionsUseCommittedReloadEvents()
        {
            Assert.That(
                Resolve(
                    CombatEventType.ReloadStarted,
                    Player,
                    RuntimeId.Invalid,
                    (int)WeaponState.Ready,
                    (int)WeaponState.Reloading,
                    false),
                Is.EqualTo("PlayerReloadStarted"));
            Assert.That(
                Resolve(
                    CombatEventType.ReloadCompleted,
                    Player,
                    RuntimeId.Invalid,
                    (int)WeaponState.Reloading,
                    (int)WeaponState.Ready,
                    false),
                Is.EqualTo("PlayerReloadCompleted"));
            Assert.That(
                Resolve(
                    CombatEventType.ReloadCompleted,
                    Player,
                    RuntimeId.Invalid,
                    (int)WeaponState.Reloading,
                    (int)WeaponState.PrimaryRecovery,
                    false),
                Is.EqualTo("PlayerReloadCompleted"),
                "Holding primary through the completion tick may advance directly into recovery.");
        }

        [Test]
        public void RouterPlaysConfiguredReloadAndReturnsToIdleOnCompletion()
        {
            CombatPresentationProfile profile =
                LoadRequired<CombatPresentationProfile>(PresentationProfilePath);
            GameObject playerObject = UnityEngine.Object.Instantiate(
                LoadRequired<GameObject>(PlayerPrefabPath));
            GameObject enemyObject = UnityEngine.Object.Instantiate(
                LoadRequired<GameObject>(EnemyPrefabPath));

            try
            {
                Actor2DPresenter playerPresenter =
                    ConfigurePresenter(playerObject, profile, playerActor: true);
                Actor2DPresenter enemyPresenter =
                    ConfigurePresenter(enemyObject, profile, playerActor: false);
                object router = CreateBoundRouter(playerPresenter, enemyPresenter);

                InvokeRouterConsume(
                    router,
                    CreateCombatEvent(
                        CombatEventType.ReloadStarted,
                        Player,
                        RuntimeId.Invalid,
                        (int)WeaponState.Ready,
                        (int)WeaponState.Reloading));

                Assert.That(playerPresenter.IsReloading, Is.True);
                Assert.That(playerPresenter.AnimationState, Is.EqualTo(D0ActorAnimationState.Reloading));
                Assert.That(
                    playerPresenter.CurrentAnimationName,
                    Is.EqualTo(playerPresenter.RuntimeWeaponDefinition.ReloadPresentation.PlayAnimation));

                InvokeRouterConsume(
                    router,
                    CreateCombatEvent(
                        CombatEventType.ReloadCompleted,
                        Player,
                        RuntimeId.Invalid,
                        (int)WeaponState.Reloading,
                        (int)WeaponState.Ready));

                Assert.That(playerPresenter.IsReloading, Is.False);
                Assert.That(playerPresenter.AnimationState, Is.EqualTo(D0ActorAnimationState.Idle));
                Assert.That(
                    playerPresenter.CurrentAnimationName,
                    Is.EqualTo(playerPresenter.ActivePlayerPresentation.IdleAnimation));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void BarrierHitsPreserveReloadButLifeHitInterruptsItsPresentation()
        {
            CombatPresentationProfile profile =
                LoadRequired<CombatPresentationProfile>(PresentationProfilePath);
            GameObject playerObject = UnityEngine.Object.Instantiate(
                LoadRequired<GameObject>(PlayerPrefabPath));
            GameObject enemyObject = UnityEngine.Object.Instantiate(
                LoadRequired<GameObject>(EnemyPrefabPath));

            try
            {
                Actor2DPresenter playerPresenter =
                    ConfigurePresenter(playerObject, profile, playerActor: true);
                Actor2DPresenter enemyPresenter =
                    ConfigurePresenter(enemyObject, profile, playerActor: false);
                object router = CreateBoundRouter(playerPresenter, enemyPresenter);

                InvokeRouterConsume(
                    router,
                    CreateCombatEvent(
                        CombatEventType.ReloadStarted,
                        Player,
                        RuntimeId.Invalid,
                        (int)WeaponState.Ready,
                        (int)WeaponState.Reloading));
                string reloadAnimation = playerPresenter.CurrentAnimationName;

                InvokeRouterConsume(
                    router,
                    CreateCombatEvent(
                        CombatEventType.DamageApplied,
                        Enemy,
                        Player,
                        100,
                        80,
                        damageChannel: DamageChannel.Barrier));
                InvokeRouterConsume(
                    router,
                    CreateCombatEvent(
                        CombatEventType.BarrierBroken,
                        Enemy,
                        Player,
                        20,
                        0));

                Assert.That(playerPresenter.IsReloading, Is.True);
                Assert.That(playerPresenter.CurrentAnimationName, Is.EqualTo(reloadAnimation));

                InvokeRouterConsume(
                    router,
                    CreateCombatEvent(
                        CombatEventType.DamageApplied,
                        Enemy,
                        Player,
                        100,
                        90,
                        damageChannel: DamageChannel.Life));

                Assert.That(playerPresenter.IsReloading, Is.False);
                Assert.That(playerPresenter.AnimationState, Is.EqualTo(D0ActorAnimationState.Idle));
                Assert.That(
                    playerPresenter.CurrentAnimationName,
                    Is.EqualTo(playerPresenter.ActivePlayerPresentation.HitAnimation));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void RouterPresentsCommittedSecondaryReleaseWhenChargeTraceWasNotRetained()
        {
            CombatPresentationProfile profile =
                LoadRequired<CombatPresentationProfile>(PresentationProfilePath);
            GameObject playerObject = UnityEngine.Object.Instantiate(
                LoadRequired<GameObject>(PlayerPrefabPath));
            GameObject enemyObject = UnityEngine.Object.Instantiate(
                LoadRequired<GameObject>(EnemyPrefabPath));

            try
            {
                Actor2DPresenter playerPresenter =
                    ConfigurePresenter(playerObject, profile, playerActor: true);
                Actor2DPresenter enemyPresenter =
                    ConfigurePresenter(enemyObject, profile, playerActor: false);
                object router = CreateBoundRouter(playerPresenter, enemyPresenter);

                Assert.That(playerPresenter.AnimationState, Is.EqualTo(D0ActorAnimationState.Idle));
                Assert.That(playerPresenter.IsChargingSecondary, Is.False,
                    "The retained actor trace must not contain a local charge transition for this regression case.");

                CombatEvent release = CreateCombatEvent(
                    CombatEventType.InputAccepted,
                    Player,
                    RuntimeId.Invalid,
                    (int)WeaponState.Ready,
                    (int)WeaponState.AltRecovery,
                    attackIdValue: 42L);
                InvokeRouterConsume(router, release);

                Assert.That(playerPresenter.IsChargingSecondary, Is.False);
                Assert.That(playerPresenter.AnimationState, Is.EqualTo(D0ActorAnimationState.Idle));
                Assert.That(
                    playerPresenter.CurrentAnimationName,
                    Is.EqualTo(playerPresenter.RuntimeWeaponDefinition.SecondaryPresentation.ReleaseAnimation),
                    "A committed release must reach the presenter even when its local charge-start trace was not retained.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(enemyObject);
            }
        }

        private static string Resolve(
            CombatEventType type,
            RuntimeId source,
            RuntimeId target,
            int valueBefore,
            int valueAfter,
            bool enemyGroggy,
            long attackIdValue = 0L,
            DamageChannel damageChannel = DamageChannel.None)
        {
            CombatEvent combatEvent = CreateCombatEvent(
                type,
                source,
                target,
                valueBefore,
                valueAfter,
                attackIdValue,
                damageChannel);
            Type routingType = typeof(BattlePresentationCoordinator).Assembly.GetType(
                "FPG.Demo.Unity.D0ActorPresentationRouting");
            Assert.That(routingType, Is.Not.Null);
            MethodInfo resolve = routingType.GetMethod(
                "ResolveCommand",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(resolve, Is.Not.Null);

            object command = resolve.Invoke(
                null,
                new object[] { combatEvent, Player, Enemy, enemyGroggy });
            Assert.That(command, Is.Not.Null);
            return command.ToString();
        }

        private static CombatEvent CreateCombatEvent(
            CombatEventType type,
            RuntimeId source,
            RuntimeId target,
            int valueBefore,
            int valueAfter,
            long attackIdValue = 0L,
            DamageChannel damageChannel = DamageChannel.None)
        {
            return new CombatEvent(
                1,
                new TickIndex(1),
                type,
                source,
                target,
                new AttackId(attackIdValue),
                ImpactId.Invalid,
                valueBefore,
                valueAfter,
                RejectReason.None,
                0UL,
                damageChannel,
                0,
                false);
        }

        private static Actor2DPresenter ConfigurePresenter(
            GameObject actorObject,
            CombatPresentationProfile profile,
            bool playerActor)
        {
            D0ActorEntityView entity = actorObject.GetComponent<D0ActorEntityView>();
            Assert.That(entity, Is.Not.Null);
            Actor2DPresenter presenter = entity.ActorPresenter;
            Assert.That(presenter, Is.Not.Null);

            D0ActorPresentationDefinition presentation = LoadRequired<D0ActorPresentationDefinition>(
                playerActor ? PlayerPresentationPath : EnemyPresentationPath);
            Assert.That(
                presenter.TryConfigureRuntime(
                    entity.SkeletonAnimation,
                    profile,
                    playerActor,
                    entity.VisualRoot,
                    presentation,
                    out string configureError),
                Is.True,
                configureError);

            if (playerActor)
            {
                Assert.That(
                    presenter.TrySetRuntimeWeaponDefinition(
                        LoadRequired<D0WeaponDefinition>(PlayerWeaponPath),
                        out string weaponError),
                    Is.True,
                    weaponError);
            }

            return presenter;
        }

        private static object CreateBoundRouter(
            Actor2DPresenter playerPresenter,
            Actor2DPresenter enemyPresenter)
        {
            Type routerType = typeof(BattlePresentationCoordinator).Assembly.GetType(
                "FPG.Demo.Unity.D0ActorPresentationRouter");
            Assert.That(routerType, Is.Not.Null);
            object router = Activator.CreateInstance(routerType, nonPublic: true);
            Assert.That(router, Is.Not.Null);

            InvokeRouterBoolean(
                routerType,
                router,
                "TryConfigure",
                new object[] { playerPresenter, enemyPresenter, null });
            InvokeRouterBoolean(
                routerType,
                router,
                "TryBind",
                new object[] { Player, Enemy, null });
            return router;
        }

        private static void InvokeRouterBoolean(
            Type routerType,
            object router,
            string methodName,
            object[] arguments)
        {
            MethodInfo method = routerType.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            bool succeeded = (bool)method.Invoke(router, arguments);
            string error = arguments[arguments.Length - 1] as string;
            Assert.That(succeeded, Is.True, error);
        }

        private static void InvokeRouterConsume(object router, CombatEvent combatEvent)
        {
            MethodInfo consume = router.GetType().GetMethod(
                "Consume",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(consume, Is.Not.Null);
            consume.Invoke(router, new object[] { combatEvent });
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, $"Required D0 presentation asset is missing: {path}");
            return asset;
        }
    }
}
