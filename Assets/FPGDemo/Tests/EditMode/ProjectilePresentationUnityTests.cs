using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class ProjectilePresentationUnityTests
    {
        [Test]
        public void ProjectileViewValidationRejectsPhysicsComponents()
        {
            GameObject objectWithCollider = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ProjectileView view = objectWithCollider.AddComponent<ProjectileView>();
            try
            {
                Assert.That(ProjectileView.TryValidatePrefab(view, out string error), Is.False);
                Assert.That(error, Does.Contain("Collider"));
            }
            finally
            {
                Object.DestroyImmediate(objectWithCollider);
            }
        }

        [Test]
        public void ProjectileViewPoolPrewarmsFixedCapacityAndNeverExpands()
        {
            GameObject root = new GameObject("ProjectileViewPoolRoot");
            GameObject cameraObject = new GameObject("ProjectilePresentationCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            GameObject prefabObject = CreateVisualPrefab("ProjectileViewPrefab");
            BattlePresentationCatalog catalog = CreateCatalog(prefabObject.GetComponent<ProjectileView>(), 2);
            ProjectileViewPool pool = new ProjectileViewPool();
            try
            {
                ScenarioDefinition definition = CombatLabHarness.CreateScenario(projectileCapacity: 2);
                Assert.That(pool.TryPrepare(
                    definition,
                    catalog,
                    root.transform,
                    camera,
                    out string error), Is.True, error);
                Assert.That(pool.Capacity, Is.EqualTo(2));

                ProjectilePresentationState first = CreateState(1, 11);
                ProjectilePresentationState second = CreateState(2, 12);
                ProjectilePresentationState third = CreateState(3, 13);
                Assert.That(pool.TryAcquire(first, Vector3.zero, out ProjectileView firstView), Is.True);
                Assert.That(pool.TryAcquire(second, Vector3.one, out ProjectileView secondView), Is.True);
                Assert.That(pool.TryAcquire(third, Vector3.up, out ProjectileView rejectedView), Is.False);

                Assert.That(firstView, Is.Not.Null);
                Assert.That(secondView, Is.Not.Null);
                Assert.That(rejectedView, Is.Null);
                Assert.That(pool.ActiveViewCount, Is.EqualTo(2));
                Assert.That(pool.Capacity, Is.EqualTo(2));
                Assert.That(pool.ViewPoolRejectCount, Is.EqualTo(1));
            }
            finally
            {
                pool.Dispose();
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(prefabObject);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProjectileViewRequiresInjectedCameraFacesItAndRestoresPrefabScale()
        {
            GameObject cameraObject = new GameObject("ProjectileCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            GameObject projectileObject = new GameObject("ProjectileView", typeof(SpriteRenderer));
            ProjectileView projectileView = projectileObject.AddComponent<ProjectileView>();
            try
            {
                cameraObject.transform.position = new Vector3(0f, 2f, -8f);
                cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                projectileObject.transform.localScale = Vector3.one * 0.48f;

                Assert.That(projectileView.TryPrepare(null, out string missingCameraError), Is.False);
                Assert.That(missingCameraError, Does.Contain("billboard camera"));
                Assert.That(projectileView.TryPrepare(camera, out string prepareError), Is.True, prepareError);

                ProjectilePresentationState state = CreateState(1, 11);
                projectileView.Activate(state, Vector3.zero);
                Assert.That(projectileView.BillboardCamera, Is.SameAs(camera));
                AssertFacesCamera(projectileView.transform, camera, Vector3.zero);
                Assert.That(projectileView.transform.localScale, Is.EqualTo(Vector3.one * 0.48f));

                projectileView.SetTerminalVisual(ProjectileTerminalReason.Intercepted);
                Assert.That(projectileView.transform.localScale, Is.EqualTo(Vector3.one * 0.648f));

                cameraObject.transform.position = new Vector3(5f, 1.5f, -4f);
                projectileView.SetPosition(new Vector3(0f, 1f, 3f));
                AssertFacesCamera(projectileView.transform, camera, new Vector3(0f, 1f, 3f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(projectileObject);
            }
        }

        [Test]
        public void InterceptableVolleyUsesStablePresentationLanesAndAMarkerWithoutChangingLogicalPaths()
        {
            GameObject root = new GameObject("ProjectileVolleyRoot");
            GameObject cameraObject = new GameObject("ProjectileVolleyCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            GameObject prefabObject = CreateVisualPrefab("ProjectileVolleyPrefab");
            BattlePresentationCatalog catalog = CreateCatalog(
                prefabObject.GetComponent<ProjectileView>(),
                3,
                presentationKey: 2);
            ProjectileViewPool pool = new ProjectileViewPool();
            try
            {
                cameraObject.transform.position = new Vector3(0f, 0f, -8f);
                cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                ScenarioDefinition definition = CombatLabHarness.CreateScenario(projectileCapacity: 3);
                Assert.That(pool.TryPrepare(definition, catalog, root.transform, camera, out string error), Is.True, error);

                ProjectilePresentationState first = CreateState(1, 9, 2, true);
                ProjectilePresentationState second = CreateState(2, 10, 2, true);
                ProjectilePresentationState third = CreateState(3, 11, 2, true);
                Assert.That(pool.TryAcquire(first, Vector3.zero, out ProjectileView firstView), Is.True);
                Assert.That(pool.TryAcquire(second, Vector3.zero, out ProjectileView secondView), Is.True);
                Assert.That(pool.TryAcquire(third, Vector3.zero, out ProjectileView thirdView), Is.True);

                Assert.That(firstView.VolleyLane, Is.EqualTo(-1));
                Assert.That(secondView.VolleyLane, Is.Zero);
                Assert.That(thirdView.VolleyLane, Is.EqualTo(1));
                Assert.That(firstView.ShowsInterceptableMarker, Is.True);
                Assert.That(secondView.ShowsInterceptableMarker, Is.True);
                Assert.That(thirdView.ShowsInterceptableMarker, Is.True);
                Assert.That(firstView.LogicalPosition, Is.EqualTo(Vector3.zero));
                Assert.That(secondView.LogicalPosition, Is.EqualTo(Vector3.zero));
                Assert.That(thirdView.LogicalPosition, Is.EqualTo(Vector3.zero));
                Assert.That(firstView.VisualPosition.x, Is.LessThan(secondView.VisualPosition.x));
                Assert.That(secondView.VisualPosition.x, Is.LessThan(thirdView.VisualPosition.x));

                Assert.That(pool.TryRelease(second.Request.RuntimeId), Is.True);
                Assert.That(secondView.ShowsInterceptableMarker, Is.False);
                pool.ClearBindings();
                Assert.That(pool.ActiveViewCount, Is.Zero);
                Assert.That(firstView.ShowsInterceptableMarker, Is.False);
                Assert.That(thirdView.ShowsInterceptableMarker, Is.False);
            }
            finally
            {
                pool.Dispose();
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(prefabObject);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CoordinatorRendersFrozenTickPositionAndClearsTerminalBinding()
        {
            GameObject root = new GameObject("PresentationCoordinatorRoot");
            GameObject hostObject = new GameObject("Host");
            BattleSessionHost host = hostObject.AddComponent<BattleSessionHost>();
            BattlePresentationCoordinator coordinator = root.AddComponent<BattlePresentationCoordinator>();
            GameObject prefabObject = CreateVisualPrefab("ProjectileViewPrefab");
            BattlePresentationCatalog catalog = CreateCatalog(prefabObject.GetComponent<ProjectileView>(), 1);
            SerializedObject coordinatorSerialized = new SerializedObject(coordinator);
            coordinatorSerialized.FindProperty("sessionHost").objectReferenceValue = host;
            coordinatorSerialized.ApplyModifiedPropertiesWithoutUndo();
            FixedProjectilePresentationFeed feed = new FixedProjectilePresentationFeed(1);
            ScenarioDefinition definition = CombatLabHarness.CreateScenario(projectileCapacity: 1);
            BattleSession session = null;
            try
            {
                Assert.That(coordinator.TryPrepare(definition, catalog, root.transform, out string error), Is.False);
                Assert.That(error, Does.Contain("MainCamera"));

                GameObject contextObject = new GameObject("ProjectilePresentationContext");
                BattleSceneContext context = contextObject.AddComponent<BattleSceneContext>();
                GameObject cameraObject = new GameObject("ProjectilePresentationCamera");
                Camera camera = cameraObject.AddComponent<Camera>();
                try
                {
                    SerializedObject contextSerialized = new SerializedObject(context);
                    contextSerialized.FindProperty("mainCamera").objectReferenceValue = camera;
                    contextSerialized.ApplyModifiedPropertiesWithoutUndo();
                    SetHostContext(host, context);

                    Assert.That(coordinator.TryPrepare(definition, catalog, root.transform, out error), Is.True, error);
                session = new BattleSessionFactory().Create(
                    definition,
                    new NullAttackResolutionPort(),
                    null,
                    new NullProjectileWorldPort());
                Assert.That(session.ApplyControl(new SessionControlCommand(
                    new ControlSequence(1),
                    SessionControlCommandType.Start)).IsSuccess, Is.True);
                CombatLabHarness.PumpOneTick(session);

                ProjectilePresentationState state = CreateState(1, 11);
                Assert.That(feed.TryRecordSpawn(state.Request, state.Path), Is.True);
                Assert.That(coordinator.TryBind(session, feed, out error), Is.True, error);
                Assert.That(coordinator.ProjectileViewPool.TryGet(state.Request.RuntimeId, out ProjectileView view), Is.True);
                Vector3 sourcePosition = ToUnityPosition(
                    state.Path.PositionAtTick(session.CurrentTick));
                Assert.That(
                    Vector3.Distance(view.transform.position, sourcePosition),
                    Is.LessThan(0.001f),
                    "The visual root must remain at the frozen simulation position while billboard rotation preserves readability.");
                Assert.That(
                    view.BillboardCamera,
                    Is.SameAs(camera),
                    "Coordinator-prepared projectile views must receive BattleSceneContext.MainCamera. The direct ProjectileView test covers the resulting facing math.");

                Assert.That(feed.TryRecordTerminal(new ProjectileReleaseRequest(
                    new TickIndex(1),
                    state.Request.ProjectileId,
                    state.Request.RuntimeId,
                    ProjectileTerminalReason.Intercepted)), Is.True);
                typeof(BattlePresentationCoordinator).GetMethod(
                    "LateUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(coordinator, null);

                Assert.That(coordinator.ProjectileViewPool.ActiveViewCount, Is.Zero);
                Assert.That(feed.ActiveCount, Is.Zero);
                }
                finally
                {
                    Object.DestroyImmediate(contextObject);
                    Object.DestroyImmediate(cameraObject);
                }
            }
            finally
            {
                session?.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(hostObject);
                Object.DestroyImmediate(prefabObject);
                Object.DestroyImmediate(catalog);
            }
        }

        private static GameObject CreateVisualPrefab(string name)
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gameObject.name = name;
            Object.DestroyImmediate(gameObject.GetComponent<Collider>());
            gameObject.AddComponent<ProjectileView>();
            return gameObject;
        }

        private static BattlePresentationCatalog CreateCatalog(
            ProjectileView prefab,
            int prewarmCapacity,
            int presentationKey = 1)
        {
            BattlePresentationCatalog catalog = ScriptableObject.CreateInstance<BattlePresentationCatalog>();
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("projectileEntries");
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("presentationKey").intValue = presentationKey;
            entry.FindPropertyRelative("viewPrefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("prewarmCapacity").intValue = prewarmCapacity;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private static ProjectilePresentationState CreateState(
            int projectileValue,
            long runtimeValue,
            int presentationKey = 1,
            bool interceptable = true)
        {
            ProjectileSpawnRequest request = new ProjectileSpawnRequest(
                new TickIndex(0),
                new TickIndex(3),
                new ProjectileId(projectileValue),
                new RuntimeId(runtimeValue),
                new AttackId(projectileValue),
                new RuntimeId(2),
                new RuntimeId(1),
                Team.Enemy,
                301,
                1,
                presentationKey,
                interceptable);
            ProjectilePathSnapshot path = new ProjectilePathSnapshot(
                request.ProjectileId,
                request.RuntimeId,
                request.Tick,
                request.ArrivalTick,
                SpatialVectorKey.Zero,
                new SpatialVectorKey(0, 0, 3000));
            return new ProjectilePresentationState(request, path, path.Start);
        }

        private static Vector3 ToUnityPosition(SpatialVectorKey position)
        {
            return new Vector3(
                position.X / (float)SpatialContract.PositionUnitsPerMeter,
                position.Y / (float)SpatialContract.PositionUnitsPerMeter,
                position.Z / (float)SpatialContract.PositionUnitsPerMeter);
        }

        private static void SetHostContext(BattleSessionHost host, BattleSceneContext context)
        {
            FieldInfo contextField = typeof(BattleSessionHost).GetField(
                "<Context>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(contextField, Is.Not.Null);
            contextField.SetValue(host, context);
        }

        private static void AssertFacesCamera(Transform visual, Camera camera, Vector3 sourcePosition)
        {
            Vector3 toCamera = (camera.transform.position - sourcePosition).normalized;
            Assert.That(
                Vector3.Dot(visual.forward, toCamera),
                Is.GreaterThan(0.999f),
                "The projectile sprite must face the explicitly injected gameplay camera.");
        }
    }
}
