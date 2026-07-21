using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0ThreatPresentationTests
    {
        [Test]
        public void RoutingMapsCommittedThreatTransitionsWithoutNeedingNewDomainPayloads()
        {
            RuntimeId enemyId = new RuntimeId(2L);
            RuntimeId threatId = new RuntimeId(11L);
            CombatEvent telegraph = CreateThreatTransition(
                enemyId,
                threatId,
                ThreatState.Scheduled,
                ThreatState.Telegraph);
            CombatEvent release = CreateThreatTransition(
                enemyId,
                threatId,
                ThreatState.Windup,
                ThreatState.ReleaseCommitted);
            CombatEvent recovery = CreateThreatTransition(
                enemyId,
                threatId,
                ThreatState.ReleaseCommitted,
                ThreatState.Recovery);

            Assert.That(D0ThreatPresentationRouting.TryResolve(
                telegraph,
                enemyId,
                threatId,
                CombatPresentationProfile.FastThreatPresentationKey,
                out D0ThreatPresentationSignal telegraphSignal), Is.True);
            Assert.That(telegraphSignal.Command,
                Is.EqualTo(D0ThreatPresentationCommand.BeginTelegraph));
            Assert.That(telegraphSignal.AudioCue,
                Is.EqualTo(CombatAudioCue.EnemyFastThreatTelegraph));

            Assert.That(D0ThreatPresentationRouting.TryResolve(
                release,
                enemyId,
                threatId,
                CombatPresentationProfile.InterceptableVolleyThreatPresentationKey,
                out D0ThreatPresentationSignal releaseSignal), Is.True);
            Assert.That(releaseSignal.Command,
                Is.EqualTo(D0ThreatPresentationCommand.ReleaseVolley));
            Assert.That(releaseSignal.AudioCue,
                Is.EqualTo(CombatAudioCue.EnemyInterceptableThreatRelease));

            Assert.That(D0ThreatPresentationRouting.TryResolve(
                recovery,
                enemyId,
                threatId,
                CombatPresentationProfile.HeavyWeakpointThreatPresentationKey,
                out _), Is.False,
                "Recovery must not replay a release effect after the domain immediately confirms payload creation.");
        }

        [Test]
        public void PresenterPrewarmsFixedSlotsRoutesReleaseAndFreezesWhilePaused()
        {
            GameObject root = new GameObject("ThreatPresentationRoot");
            GameObject weakpointRoot = new GameObject("WeakpointPresentationRoot");
            GameObject cameraObject = new GameObject("ThreatPresentationCamera");
            GameObject enemyAnchor = new GameObject("ThreatEnemyAnchor");
            GameObject playerAnchor = new GameObject("ThreatPlayerAnchor");
            GameObject weakpointAnchor = new GameObject("ThreatWeakpointAnchor");
            GameObject reticleObject = new GameObject("ThreatReticle", typeof(RectTransform));
            CombatPresentationProfile profile = ScriptableObject.CreateInstance<CombatPresentationProfile>();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/FPGDemo/Presentation/M_FPG_Projectile.mat");
            try
            {
                Assert.That(material, Is.Not.Null);
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = Vector3.zero;
                cameraObject.transform.rotation = Quaternion.identity;
                enemyAnchor.transform.position = new Vector3(0f, 1f, 8f);
                playerAnchor.transform.position = new Vector3(-1f, 0f, 3f);
                weakpointAnchor.transform.position = new Vector3(0f, 1.4f, 8f);
                weakpointAnchor.AddComponent<SphereCollider>().radius = 0.42f;
                CombatAimReticle reticle = reticleObject.AddComponent<CombatAimReticle>();
                reticle.SetViewport(CombatAimViewportMath.Center);

                D0WeakpointPresentationController weakpoint =
                    weakpointRoot.AddComponent<D0WeakpointPresentationController>();
                weakpoint.Configure(profile, material, camera, weakpointAnchor.transform, reticle);

                ThreatTelegraph2DPresenter presenter =
                    root.AddComponent<ThreatTelegraph2DPresenter>();
                presenter.Configure(
                    profile,
                    material,
                    camera,
                    enemyAnchor.transform,
                    playerAnchor.transform,
                    weakpointAnchor.transform,
                    root.transform,
                    null,
                    weakpoint,
                    profile.PoolCapacities.ThreatTelegraphCapacity);

                Assert.That(presenter.TryValidate(out string validationError), Is.True, validationError);
                Assert.That(presenter.TryPrepare(out string prepareError), Is.True, prepareError);
                int prewarmedChildCount = root.transform.childCount;
                Assert.That(presenter.Capacity,
                    Is.EqualTo(profile.PoolCapacities.ThreatTelegraphCapacity));
                Assert.That(presenter.TryBind(new RuntimeId(1L), new RuntimeId(2L), out string bindError), Is.True, bindError);

                ThreatSnapshot fastTelegraph = CreateSnapshot(
                    new RuntimeId(11L),
                    ThreatState.Telegraph,
                    CombatPresentationProfile.FastThreatPresentationKey,
                    ThreatPayloadKind.SweptProjectile,
                    new TickIndex(60L));
                ThreatSnapshot[] snapshots = { fastTelegraph };
                presenter.Reconcile(snapshots, 1, new TickIndex(36L));

                Assert.That(presenter.ActiveTelegraphCount, Is.EqualTo(1));
                Assert.That(presenter.TryGetView(fastTelegraph.RuntimeId, out ThreatTelegraph2DView view), Is.True);
                Assert.That(view.IsActive, Is.True);
                Assert.That(root.transform.childCount, Is.EqualTo(prewarmedChildCount),
                    "Snapshot reconciliation must reuse the fixed telegraph pool.");

                CombatEvent release = CreateThreatTransition(
                    new RuntimeId(2L),
                    fastTelegraph.RuntimeId,
                    ThreatState.Windup,
                    ThreatState.ReleaseCommitted);
                Assert.That(presenter.ConsumeTrace(release, out D0ThreatPresentationSignal signal), Is.True);
                Assert.That(signal.Command, Is.EqualTo(D0ThreatPresentationCommand.ReleaseFast));
                Assert.That(presenter.ActiveReleaseCount, Is.EqualTo(1));

                presenter.Advance(1f, isRunning: false);
                Assert.That(presenter.ActiveReleaseCount, Is.EqualTo(1),
                    "Paused battle presentation must preserve the release visual instead of advancing it.");
                presenter.Advance(1f, isRunning: true);
                Assert.That(presenter.ActiveReleaseCount, Is.Zero);
                Assert.That(presenter.ActiveTelegraphCount, Is.Zero);
                Assert.That(presenter.PoolRejectCount, Is.Zero);
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(reticleObject);
                Object.DestroyImmediate(weakpointAnchor);
                Object.DestroyImmediate(playerAnchor);
                Object.DestroyImmediate(enemyAnchor);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(weakpointRoot);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void WeakpointControllerUsesViewportLockAndClearsHeavyStateOnBreak()
        {
            GameObject root = new GameObject("WeakpointControllerRoot");
            GameObject cameraObject = new GameObject("WeakpointControllerCamera");
            GameObject anchorObject = new GameObject("WeakpointControllerAnchor");
            GameObject reticleObject = new GameObject("WeakpointControllerReticle", typeof(RectTransform));
            CombatPresentationProfile profile = ScriptableObject.CreateInstance<CombatPresentationProfile>();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/FPGDemo/Presentation/M_FPG_Projectile.mat");
            try
            {
                Assert.That(material, Is.Not.Null);
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = Vector3.zero;
                cameraObject.transform.rotation = Quaternion.identity;
                anchorObject.transform.position = new Vector3(0f, 0f, 8f);
                anchorObject.AddComponent<SphereCollider>().radius = 0.42f;
                CombatAimReticle reticle = reticleObject.AddComponent<CombatAimReticle>();
                reticle.SetViewport(CombatAimViewportMath.Center);

                D0WeakpointPresentationController controller =
                    root.AddComponent<D0WeakpointPresentationController>();
                controller.Configure(profile, material, camera, anchorObject.transform, reticle);
                Assert.That(controller.TryPrepare(out string prepareError), Is.True, prepareError);
                Assert.That(controller.TryBind(new RuntimeId(2L), out string bindError), Is.True, bindError);

                ThreatSnapshot heavy = CreateSnapshot(
                    new RuntimeId(31L),
                    ThreatState.Telegraph,
                    CombatPresentationProfile.HeavyWeakpointThreatPresentationKey,
                    ThreatPayloadKind.TimedImpact,
                    new TickIndex(120L));
                controller.SetHeavyThreat(heavy, new TickIndex(60L));
                controller.Advance(0f, isRunning: true);
                Assert.That(controller.IsHeavyThreatActive, Is.True);
                Assert.That(controller.IsReticleLocked, Is.True,
                    "The visual lock must use the same virtual viewport ray as the gameplay target point.");
                Physics.SyncTransforms();
                Assert.That(
                    Physics.Raycast(camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))),
                    Is.True,
                    "The centered reticle ray must really reach the weakpoint collider in this isolated fixture.");
                Assert.That(controller.DisplayedCountdownSeconds, Is.EqualTo(1));
                Assert.That(controller.ReticleLockCueRequestCount, Is.EqualTo(1),
                    "Entering the weakpoint once requests one lock cue, even when an AudioPresenter is not bound in this scene-isolated test.");
                Assert.That(controller.EnemyDangerTickCueRequestCount, Is.EqualTo(1),
                    "The first positive heavy countdown requests its danger tick.");
                controller.Advance(0f, isRunning: true);
                Assert.That(controller.ReticleLockCueRequestCount, Is.EqualTo(1),
                    "A stable cursor lock must not request a cue every presentation frame.");
                Assert.That(controller.EnemyDangerTickCueRequestCount, Is.EqualTo(1),
                    "An unchanged countdown must not request a cue every presentation frame.");
                reticle.SetViewport(new Vector2(0.62f, 0.5f));
                controller.Advance(0f, isRunning: true);
                Assert.That(controller.IsReticleLocked, Is.False,
                    "A viewport ray which misses the gameplay SphereCollider must not show a false lock.");

                controller.ConsumeSelectedHit(new SelectedAttackHit(
                    new AttackId(7L),
                    new ShotId(9L),
                    new TickIndex(60L),
                    0,
                    AttackQueryStage.Pellet,
                    0,
                    new RuntimeId(2L),
                    QueryTargetKind.Combatant,
                    HitPart.Weakpoint,
                    new GeometryId(2002),
                    new SpatialVectorKey(0, 0, 8000)));
                Assert.That(controller.WeakpointFlashCount, Is.EqualTo(1));

                controller.ConsumeTrace(new CombatEvent(
                    1L,
                    new TickIndex(61L),
                    CombatEventType.BreakTriggered,
                    new RuntimeId(1L),
                    new RuntimeId(2L),
                    new AttackId(7L),
                    ImpactId.Invalid,
                    1,
                    0,
                    RejectReason.None,
                    0UL,
                    DamageChannel.None,
                    0,
                    false));
                Assert.That(controller.IsHeavyThreatActive, Is.False);
                Assert.That(controller.BreakFeedbackCount, Is.EqualTo(1));
                Assert.That(controller.ActiveShardCount, Is.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(reticleObject);
                Object.DestroyImmediate(anchorObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void RebindRestoresOldRendererPropertyBlockAndUsesNewEnemyBreakDuration()
        {
            const string BurstbugEntityPath =
                "Assets/FPGDemo/Presentation/D0Slice/Spine/PF_D0_BurstbugEntity.prefab";
            const string HudieEntityPath =
                "Assets/FPGDemo/Presentation/Hudie/Prefabs/PF_D0_HudieEntity.prefab";
            const string BurstbugPresentationPath =
                "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Presentation.asset";
            const string HudiePresentationPath =
                "Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/D0_Hudie_Presentation.asset";

            GameObject root = new GameObject("WeakpointRebindRoot");
            GameObject cameraObject = new GameObject("WeakpointRebindCamera");
            GameObject reticleObject =
                new GameObject("WeakpointRebindReticle", typeof(RectTransform));
            GameObject oldObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(BurstbugEntityPath));
            GameObject newObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(HudieEntityPath));
            CombatPresentationProfile profile =
                ScriptableObject.CreateInstance<CombatPresentationProfile>();
            D0ActorPresentationDefinition newState = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<D0ActorPresentationDefinition>(
                    HudiePresentationPath));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/FPGDemo/Presentation/M_FPG_Projectile.mat");

            try
            {
                Assert.That(oldObject, Is.Not.Null);
                Assert.That(newObject, Is.Not.Null);
                Assert.That(material, Is.Not.Null);
                D0EnemyEntityView oldEntity =
                    oldObject.GetComponent<D0EnemyEntityView>();
                D0EnemyEntityView newEntity =
                    newObject.GetComponent<D0EnemyEntityView>();
                Assert.That(oldEntity, Is.Not.Null);
                Assert.That(newEntity, Is.Not.Null);

                D0ActorPresentationDefinition oldState =
                    AssetDatabase.LoadAssetAtPath<D0ActorPresentationDefinition>(
                        BurstbugPresentationPath);
                const float NewBreakDuration = 1.37f;
                SerializedObject stateSerialized = new SerializedObject(newState);
                stateSerialized.FindProperty("enemy")
                    .FindPropertyRelative("breakFeedbackDuration").floatValue =
                    NewBreakDuration;
                stateSerialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    oldEntity.ActorPresenter.TryConfigureRuntime(
                        oldEntity.SkeletonAnimation,
                        profile,
                        false,
                        oldEntity.VisualRoot,
                        oldState,
                        out string oldPresenterError),
                    Is.True,
                    oldPresenterError);
                Assert.That(
                    newEntity.ActorPresenter.TryConfigureRuntime(
                        newEntity.SkeletonAnimation,
                        profile,
                        false,
                        newEntity.VisualRoot,
                        newState,
                        out string newPresenterError),
                    Is.True,
                    newPresenterError);

                Renderer oldRenderer =
                    oldEntity.ActorPresenter.GetComponentInChildren<Renderer>(true);
                Assert.That(oldRenderer, Is.Not.Null);
                Color originalColor = new Color(0.17f, 0.33f, 0.61f, 0.79f);
                int probeId = Shader.PropertyToID("_D0RebindProbe");
                MaterialPropertyBlock originalBlock = new MaterialPropertyBlock();
                originalBlock.SetColor("_Color", originalColor);
                originalBlock.SetFloat(probeId, 0.42f);
                oldRenderer.SetPropertyBlock(originalBlock);

                Camera camera = cameraObject.AddComponent<Camera>();
                CombatAimReticle reticle =
                    reticleObject.AddComponent<CombatAimReticle>();
                D0WeakpointPresentationController controller =
                    root.AddComponent<D0WeakpointPresentationController>();
                controller.Configure(
                    profile,
                    material,
                    camera,
                    oldEntity.WeakpointAnchor,
                    reticle,
                    oldEntity.ActorPresenter,
                    oldEntity.WeakpointHitbox as SphereCollider);
                Assert.That(
                    controller.TryPrepare(out string prepareError),
                    Is.True,
                    prepareError);
                RuntimeId oldRuntimeId = new RuntimeId(2L);
                Assert.That(
                    controller.TryBind(oldRuntimeId, out string bindError),
                    Is.True,
                    bindError);

                controller.ConsumeTrace(new CombatEvent(
                    1L,
                    new TickIndex(61L),
                    CombatEventType.BreakTriggered,
                    new RuntimeId(1L),
                    oldRuntimeId,
                    new AttackId(7L),
                    ImpactId.Invalid,
                    1,
                    0,
                    RejectReason.None,
                    0UL,
                    DamageChannel.None,
                    0,
                    false));

                MaterialPropertyBlock desaturated = new MaterialPropertyBlock();
                oldRenderer.GetPropertyBlock(desaturated);
                Assert.That(
                    desaturated.GetColor("_Color"),
                    Is.Not.EqualTo(originalColor));

                controller.RebindEnemyEntity(
                    newEntity.WeakpointAnchor,
                    newEntity.WeakpointHitbox as SphereCollider,
                    newEntity.ActorPresenter);

                MaterialPropertyBlock restored = new MaterialPropertyBlock();
                oldRenderer.GetPropertyBlock(restored);
                Color restoredColor = restored.GetColor("_Color");
                Assert.That(restoredColor.r, Is.EqualTo(originalColor.r).Within(0.0001f));
                Assert.That(restoredColor.g, Is.EqualTo(originalColor.g).Within(0.0001f));
                Assert.That(restoredColor.b, Is.EqualTo(originalColor.b).Within(0.0001f));
                Assert.That(restoredColor.a, Is.EqualTo(originalColor.a).Within(0.0001f));
                Assert.That(
                    restored.GetFloat(probeId),
                    Is.EqualTo(0.42f).Within(0.0001f),
                    "Rebind must restore the complete original MaterialPropertyBlock.");

                FieldInfo durationField =
                    typeof(D0WeakpointPresentationController).GetField(
                        "breakFeedbackDuration",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(durationField, Is.Not.Null);
                Assert.That(
                    (float)durationField.GetValue(controller),
                    Is.EqualTo(NewBreakDuration).Within(0.0001f));
                Assert.That(
                    newEntity.ActorPresenter.ActiveEnemyPresentation
                        .BreakFeedbackDuration,
                    Is.EqualTo(NewBreakDuration).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(newState);
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(newObject);
                Object.DestroyImmediate(oldObject);
                Object.DestroyImmediate(reticleObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TelegraphAudioBindingPropagatesToItsWeakpointPresentation()
        {
            GameObject telegraphRoot = new GameObject("ThreatAudioBindingRoot");
            GameObject weakpointRoot = new GameObject("WeakpointAudioBindingRoot");
            GameObject audioRoot = new GameObject("CombatAudioBindingRoot");
            try
            {
                D0WeakpointPresentationController weakpoint =
                    weakpointRoot.AddComponent<D0WeakpointPresentationController>();
                ThreatTelegraph2DPresenter telegraph =
                    telegraphRoot.AddComponent<ThreatTelegraph2DPresenter>();
                CombatAudioPresenter audio = audioRoot.AddComponent<CombatAudioPresenter>();
                telegraph.Configure(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    telegraphRoot.transform,
                    null,
                    weakpoint);

                telegraph.SetAudioPresenter(audio);

                Assert.That(telegraph.AudioPresenter, Is.SameAs(audio));
                Assert.That(weakpoint.AudioPresenter, Is.SameAs(audio));
            }
            finally
            {
                Object.DestroyImmediate(audioRoot);
                Object.DestroyImmediate(weakpointRoot);
                Object.DestroyImmediate(telegraphRoot);
            }
        }

        private static ThreatSnapshot CreateSnapshot(
            RuntimeId runtimeId,
            ThreatState state,
            int presentationKey,
            ThreatPayloadKind payloadKind,
            TickIndex stateUntilTick)
        {
            return new ThreatSnapshot(
                runtimeId,
                presentationKey + 200,
                state,
                new AttackId(runtimeId.Value + 100L),
                stateUntilTick,
                state == ThreatState.ReleaseCommitted || state == ThreatState.Recovery,
                state == ThreatState.Completed || state == ThreatState.Canceled,
                payloadKind,
                presentationKey,
                ThreatTargetPolicy.PlayerCombatant);
        }

        private static CombatEvent CreateThreatTransition(
            RuntimeId enemyId,
            RuntimeId threatId,
            ThreatState before,
            ThreatState after)
        {
            return new CombatEvent(
                1L,
                new TickIndex(20L),
                CombatEventType.ThreatStateChanged,
                enemyId,
                threatId,
                new AttackId(3L),
                ImpactId.Invalid,
                (int)before,
                (int)after,
                RejectReason.None,
                0UL,
                DamageChannel.None,
                0,
                false);
        }
    }
}
