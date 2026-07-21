using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class BattleSessionHostSpatialWiringTests
    {
        [Test]
        public void InitializeComposesRecordingUnityQueryAndBindsStaticRegistry()
        {
            using (TestRig rig = TestRig.Create(withStaticBinding: true))
            {
                Assert.That(rig.Host.TryInitialize(rig.Context, rig.Config, out string error),
                    Is.True,
                    error);

                Assert.That(rig.Host.IsInitialized, Is.True);
                Assert.That(rig.Host.IsSpatialQueryReady, Is.True);
                Assert.That(rig.Host.IsProjectileWorldReady, Is.True);
                Assert.That(rig.Host.Session.State, Is.EqualTo(BattleSessionState.Running));
                Assert.That(rig.Host.HitboxRegistry, Is.SameAs(rig.Registry));
                Assert.That(rig.Host.SpatialTranscript, Is.Not.Null);
                Assert.That(rig.Host.ProjectileCollisionProxyPool, Is.Not.Null);
                Assert.That(rig.Host.ProjectileCollisionProxyPool.IsPrepared, Is.True);
                Assert.That(rig.Registry.Count, Is.EqualTo(1));
                Assert.That(rig.Registry.TryResolve(
                    rig.StaticCollider,
                    out RegisteredHitbox registered), Is.True);
                Assert.That(registered.RuntimeId, Is.EqualTo(rig.Host.Session.EnemyRuntimeId));
                Assert.That(rig.Host.Session.GetReplaySummary().SpatialDecisionDigest,
                    Is.EqualTo(rig.Host.SpatialTranscript.CanonicalDigest));
                Assert.That(rig.Host.Session.ControlCommandCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void SessionRunningTracksPauseResumeRestartAndShutdown()
        {
            using (TestRig rig = TestRig.Create(withStaticBinding: true))
            {
                Assert.That(rig.Host.TryInitialize(rig.Context, rig.Config, out string error),
                    Is.True,
                    error);
                Assert.That(rig.Host.IsSessionRunning, Is.True);

                Assert.That(rig.Host.TryPause().IsSuccess, Is.True);
                Assert.That(rig.Host.IsSessionRunning, Is.False);

                Assert.That(rig.Host.TryResume().IsSuccess, Is.True);
                Assert.That(rig.Host.IsSessionRunning, Is.True);

                Assert.That(rig.Host.TryRestart().IsSuccess, Is.True, rig.Host.LastError);
                Assert.That(rig.Host.IsSessionRunning, Is.True);

                rig.Host.Shutdown();
                Assert.That(rig.Host.IsSessionRunning, Is.False);
            }
        }

        [Test]
        public void RestartRecomposesTranscriptAndQueryAndClearsDynamicBindings()
        {
            using (TestRig rig = TestRig.Create(withStaticBinding: true))
            {
                Assert.That(rig.Host.TryInitialize(rig.Context, rig.Config, out string error),
                    Is.True,
                    error);

                GameObject dynamicObject = new GameObject("DynamicHitbox");
                dynamicObject.transform.SetParent(rig.Root.transform, false);
                dynamicObject.layer = LayerFromSingleBitMask(rig.Config.AttackQuerySettings.HitboxLayerMask);
                BoxCollider dynamicCollider = dynamicObject.AddComponent<BoxCollider>();
                Assert.That(rig.Registry.Register(new HitboxBinding(
                    dynamicCollider,
                    new RuntimeId(9001),
                    QueryTargetKind.Combatant,
                    HitPart.Body,
                    new GeometryId(9001),
                    Team.Enemy)).IsSuccess, Is.True);
                Assert.That(rig.Registry.Count, Is.EqualTo(2));

                BattleSession previous = rig.Host.Session;
                SpatialPortTranscript previousTranscript = rig.Host.SpatialTranscript;

                DomainResult restarted = rig.Host.TryRestart();

                Assert.That(restarted.IsSuccess, Is.True, rig.Host.LastError);
                Assert.That(previous.State, Is.EqualTo(BattleSessionState.Disposed));
                Assert.That(previous.CompletionReason, Is.EqualTo(BattleCompletionReason.Restarted));
                Assert.That(previous.ControlCommandCount, Is.EqualTo(2));
                Assert.That(rig.Host.Session, Is.Not.SameAs(previous));
                Assert.That(rig.Host.SpatialTranscript, Is.Not.SameAs(previousTranscript));
                Assert.That(rig.Host.Session.State, Is.EqualTo(BattleSessionState.Running));
                Assert.That(rig.Host.Session.ControlCommandCount, Is.EqualTo(1));
                Assert.That(rig.Host.IsSpatialQueryReady, Is.True);
                Assert.That(rig.Host.IsProjectileWorldReady, Is.True);
                Assert.That(rig.Registry.Count, Is.EqualTo(1));
                Assert.That(rig.Registry.TryResolve(dynamicCollider, out RegisteredHitbox ignored),
                    Is.False);
                Assert.That(rig.Registry.TryResolve(
                    rig.StaticCollider,
                    out RegisteredHitbox rebound), Is.True);
                Assert.That(rebound.RuntimeId, Is.EqualTo(rig.Host.Session.EnemyRuntimeId));
            }
        }

        [Test]
        public void RestartRebuildsChangedCollisionProxyPoolAndClearsActiveProxyBindings()
        {
            using (TestRig rig = TestRig.Create(withStaticBinding: true))
            {
                Assert.That(rig.Host.TryInitialize(rig.Context, rig.Config, out string error),
                    Is.True,
                    error);

                ProjectileCollisionProxyPool originalPool = rig.Host.ProjectileCollisionProxyPool;
                ProjectileSpawnRequest firstRequest = CreateEnemyProjectileRequest(
                    rig.Host.Session,
                    runtimeId: 9001,
                    projectileId: 9001);
                Assert.That(originalPool.Acquire(firstRequest, CreatePath(firstRequest)).IsSuccess, Is.True);
                Assert.That(originalPool.TryGetActiveProxy(
                    firstRequest.RuntimeId,
                    out ProjectileCollisionProxySnapshot firstProxy), Is.True);
                Assert.That(rig.Registry.TryResolve(firstProxy.GeometryId, out RegisteredHitbox activeBinding), Is.True);
                Assert.That(activeBinding.RuntimeId, Is.EqualTo(firstRequest.RuntimeId));

                SetProjectileCapacity(rig.Config, 31);
                Assert.That(rig.Host.TryRestart().IsSuccess, Is.True, rig.Host.LastError);

                ProjectileCollisionProxyPool rebuiltPool = rig.Host.ProjectileCollisionProxyPool;
                Assert.That(rebuiltPool, Is.Not.SameAs(originalPool));
                Assert.That(originalPool.IsPrepared, Is.False);
                Assert.That(rebuiltPool.Capacity, Is.EqualTo(31));
                Assert.That(rebuiltPool.IsPrepared, Is.True);
                Assert.That(rebuiltPool.ActiveCount, Is.Zero);
                Assert.That(rig.Registry.Count, Is.EqualTo(1));
                Assert.That(rig.Registry.TryResolve(firstProxy.GeometryId, out RegisteredHitbox ignored), Is.False);

                ProjectileSpawnRequest replacementRequest = CreateEnemyProjectileRequest(
                    rig.Host.Session,
                    runtimeId: 9002,
                    projectileId: 9002);
                Assert.That(rebuiltPool.Acquire(replacementRequest, CreatePath(replacementRequest)).IsSuccess, Is.True);
                Assert.That(rebuiltPool.TryGetActiveProxy(
                    replacementRequest.RuntimeId,
                    out ProjectileCollisionProxySnapshot replacementProxy), Is.True);
                Assert.That(rig.Registry.TryResolve(replacementProxy.GeometryId, out RegisteredHitbox rebound), Is.True);
                Assert.That(rebound.RuntimeId, Is.EqualTo(replacementRequest.RuntimeId));
            }
        }

        [Test]
        public void InitializeRejectsMissingRegistryInsteadOfFallingBackToNullQuery()
        {
            GameObject root = new GameObject("MissingRegistryRig");
            BattleScenarioConfig config = ScriptableObject.CreateInstance<BattleScenarioConfig>();
            try
            {
                BattleSessionHost host = root.AddComponent<BattleSessionHost>();
                BattleSceneContext context = root.AddComponent<BattleSceneContext>();
                ConfigureContext(context, host, config, null);

                Assert.That(host.TryInitialize(context, config, out string error), Is.False);
                Assert.That(error, Does.Contain("HitboxRegistry"));
                Assert.That(host.Session, Is.Null);
                Assert.That(host.IsInitialized, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void InitializeRejectsRegistryWithoutUsableStaticBindings()
        {
            using (TestRig rig = TestRig.Create(withStaticBinding: false))
            {
                Assert.That(rig.Host.TryInitialize(rig.Context, rig.Config, out string error),
                    Is.False);
                Assert.That(error, Does.Contain("static binding"));
                Assert.That(rig.Host.Session, Is.Null);
                Assert.That(rig.Host.IsInitialized, Is.False);
            }
        }

        private static void ConfigureContext(
            BattleSceneContext context,
            BattleSessionHost host,
            BattleScenarioConfig config,
            HitboxRegistry registry,
            Transform playerAnchor = null,
            Transform enemyAnchor = null,
            Transform enemyProjectileSpawnAnchor = null)
        {
            SerializedObject serialized = new SerializedObject(context);
            serialized.FindProperty("sessionHost").objectReferenceValue = host;
            serialized.FindProperty("scenarioConfig").objectReferenceValue = config;
            serialized.FindProperty("hitboxRegistry").objectReferenceValue = registry;
            serialized.FindProperty("playerAnchor").objectReferenceValue = playerAnchor;
            serialized.FindProperty("enemyAnchor").objectReferenceValue = enemyAnchor;
            serialized.FindProperty("enemyProjectileSpawnAnchor").objectReferenceValue =
                enemyProjectileSpawnAnchor;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureSingleEnemyStaticBinding(
            HitboxRegistry registry,
            Collider collider)
        {
            SerializedObject serialized = new SerializedObject(registry);
            SerializedProperty bindings = serialized.FindProperty("staticBindings");
            bindings.arraySize = 1;
            SerializedProperty binding = bindings.GetArrayElementAtIndex(0);
            binding.FindPropertyRelative("enabled").boolValue = true;
            binding.FindPropertyRelative("collider").objectReferenceValue = collider;
            binding.FindPropertyRelative("targetReference").enumValueIndex =
                (int)HitboxTargetReference.Enemy;
            binding.FindPropertyRelative("targetKind").enumValueIndex =
                (int)QueryTargetKind.Combatant;
            binding.FindPropertyRelative("hitPart").enumValueIndex = (int)HitPart.Body;
            binding.FindPropertyRelative("geometryId").intValue = 2001;
            binding.FindPropertyRelative("team").enumValueIndex = (int)Team.Enemy;
            binding.FindPropertyRelative("allowTrigger").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int LayerFromSingleBitMask(int mask)
        {
            for (int layer = 0; layer < 32; layer++)
            {
                if (mask == 1 << layer)
                {
                    return layer;
                }
            }

            Assert.Fail($"Expected a single-bit layer mask, received {mask}.");
            return 0;
        }

        private static ProjectileSpawnRequest CreateEnemyProjectileRequest(
            BattleSession session,
            long runtimeId,
            long projectileId)
        {
            return new ProjectileSpawnRequest(
                new TickIndex(0),
                new TickIndex(2),
                new ProjectileId(projectileId),
                new RuntimeId(runtimeId),
                new AttackId(projectileId),
                session.EnemyRuntimeId,
                session.PlayerRuntimeId,
                Team.Enemy,
                301,
                250,
                1,
                true);
        }

        private static ProjectilePathSnapshot CreatePath(in ProjectileSpawnRequest request)
        {
            return new ProjectilePathSnapshot(
                request.ProjectileId,
                request.RuntimeId,
                request.Tick,
                request.ArrivalTick,
                SpatialVectorKey.Zero,
                new SpatialVectorKey(0, 0, 2000));
        }

        private static void SetProjectileCapacity(BattleScenarioConfig config, int capacity)
        {
            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("projectileCapacity").intValue = capacity;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class TestRig : System.IDisposable
        {
            private TestRig(
                GameObject root,
                BattleScenarioConfig config,
                BattleSceneContext context,
                BattleSessionHost host,
                HitboxRegistry registry,
                BoxCollider staticCollider,
                BattlePresentationCatalog presentationCatalog)
            {
                Root = root;
                Config = config;
                Context = context;
                Host = host;
                Registry = registry;
                StaticCollider = staticCollider;
                PresentationCatalog = presentationCatalog;
            }

            public GameObject Root { get; }
            public BattleScenarioConfig Config { get; }
            public BattleSceneContext Context { get; }
            public BattleSessionHost Host { get; }
            public HitboxRegistry Registry { get; }
            public BoxCollider StaticCollider { get; }
            public BattlePresentationCatalog PresentationCatalog { get; }

            public static TestRig Create(bool withStaticBinding)
            {
                GameObject root = new GameObject("SpatialHostRig");
                BattleScenarioConfig config = ScriptableObject.CreateInstance<BattleScenarioConfig>();
                BattleSessionHost host = root.AddComponent<BattleSessionHost>();
                BattleSceneContext context = root.AddComponent<BattleSceneContext>();
                HitboxRegistry registry = root.AddComponent<HitboxRegistry>();

                GameObject playerAnchorObject = new GameObject("PlayerAnchor");
                playerAnchorObject.transform.SetParent(root.transform, false);
                GameObject enemyAnchorObject = new GameObject("EnemyAnchor");
                enemyAnchorObject.transform.SetParent(root.transform, false);
                enemyAnchorObject.transform.position = new Vector3(0f, 0f, 5f);
                GameObject enemyProjectileSpawnAnchorObject =
                    new GameObject("EnemyProjectileSpawnAnchor");
                enemyProjectileSpawnAnchorObject.transform.SetParent(
                    enemyAnchorObject.transform,
                    false);
                enemyProjectileSpawnAnchorObject.transform.localPosition = Vector3.up;

                // World-space projectile and impact views explicitly billboard
                // against the scene gameplay camera. Keep this reference in the
                // minimal host rig too, so the normal successful composition
                // path exercises the same contract as CombatLab.
                GameObject cameraObject = new GameObject("MainCamera");
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.position = new Vector3(0f, 1.5f, -4f);
                Camera mainCamera = cameraObject.AddComponent<Camera>();

                GameObject colliderObject = new GameObject("EnemyBodyHitbox");
                colliderObject.transform.SetParent(root.transform, false);
                colliderObject.layer = LayerFromSingleBitMask(config.AttackQuerySettings.HitboxLayerMask);
                colliderObject.transform.position = new Vector3(0f, 0f, 5f);
                BoxCollider staticCollider = colliderObject.AddComponent<BoxCollider>();
                if (withStaticBinding)
                {
                    ConfigureSingleEnemyStaticBinding(registry, staticCollider);
                }

                GameObject viewRootObject = new GameObject("ProjectileViews");
                viewRootObject.transform.SetParent(root.transform, false);
                GameObject viewPrefabObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                viewPrefabObject.name = "ProjectileViewPrefab";
                viewPrefabObject.transform.SetParent(root.transform, false);
                Object.DestroyImmediate(viewPrefabObject.GetComponent<Collider>());
                ProjectileView viewPrefab = viewPrefabObject.AddComponent<ProjectileView>();
                viewPrefabObject.SetActive(false);
                BattlePresentationCatalog presentationCatalog =
                    ScriptableObject.CreateInstance<BattlePresentationCatalog>();
                SerializedObject catalogSerialized = new SerializedObject(presentationCatalog);
                SerializedProperty entries = catalogSerialized.FindProperty("projectileEntries");
                entries.arraySize = 1;
                SerializedProperty entry = entries.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("presentationKey").intValue = 1;
                entry.FindPropertyRelative("viewPrefab").objectReferenceValue = viewPrefab;
                entry.FindPropertyRelative("prewarmCapacity").intValue = 32;
                catalogSerialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject coordinatorObject = new GameObject("BattlePresentationCoordinator");
                coordinatorObject.transform.SetParent(root.transform, false);
                BattlePresentationCoordinator coordinator =
                    coordinatorObject.AddComponent<BattlePresentationCoordinator>();
                SerializedObject coordinatorSerialized = new SerializedObject(coordinator);
                coordinatorSerialized.FindProperty("sessionHost").objectReferenceValue = host;
                coordinatorSerialized.ApplyModifiedPropertiesWithoutUndo();

                ConfigureContext(
                    context,
                    host,
                    config,
                    registry,
                    playerAnchorObject.transform,
                    enemyAnchorObject.transform,
                    enemyProjectileSpawnAnchorObject.transform);
                SerializedObject contextSerialized = new SerializedObject(context);
                contextSerialized.FindProperty("presentationCatalog").objectReferenceValue = presentationCatalog;
                contextSerialized.FindProperty("presentationCoordinator").objectReferenceValue = coordinator;
                contextSerialized.FindProperty("projectileViewRoot").objectReferenceValue = viewRootObject.transform;
                contextSerialized.FindProperty("mainCamera").objectReferenceValue = mainCamera;
                contextSerialized.ApplyModifiedPropertiesWithoutUndo();
                Physics.SyncTransforms();
                return new TestRig(
                    root,
                    config,
                    context,
                    host,
                    registry,
                    staticCollider,
                    presentationCatalog);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                Object.DestroyImmediate(Config);
                Object.DestroyImmediate(PresentationCatalog);
                Physics.SyncTransforms();
            }
        }
    }
}
