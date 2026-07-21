using System.Reflection;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class PlaytestBoundsAndImpactViewTests
    {
        [Test]
        public void PlayerBoundsClampsPlanarExitWithoutChangingHeight()
        {
            GameObject player = new GameObject("PlayerBoundsTest");
            CharacterController characterController = player.AddComponent<CharacterController>();
            CombatLabPlayerBounds bounds = player.AddComponent<CombatLabPlayerBounds>();
            try
            {
                ConfigureBounds(bounds, characterController, new Vector2(-2f, -3f), new Vector2(2f, 3f), -4f);
                player.transform.position = new Vector3(0f, 1f, 0f);
                Assert.That(bounds.CaptureInitialSafePosition(out string captureError), Is.True, captureError);

                player.transform.position = new Vector3(4.5f, 1.75f, -4.5f);
                Assert.That(bounds.TryEnforceBounds(out bool resetToSafePosition, out string error), Is.True, error);

                Assert.That(resetToSafePosition, Is.False);
                Assert.That(player.transform.position, Is.EqualTo(new Vector3(2f, 1.75f, -3f)));
                Assert.That(bounds.BoundaryClampCount, Is.EqualTo(1));
                Assert.That(bounds.FallResetCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void PlayerBoundsResetsFallToCapturedSafePosition()
        {
            GameObject player = new GameObject("PlayerFallResetTest");
            CharacterController characterController = player.AddComponent<CharacterController>();
            CombatLabPlayerBounds bounds = player.AddComponent<CombatLabPlayerBounds>();
            try
            {
                ConfigureBounds(bounds, characterController, new Vector2(-2f, -3f), new Vector2(2f, 3f), -4f);
                player.transform.position = new Vector3(0.5f, 1f, -0.5f);
                Assert.That(bounds.CaptureInitialSafePosition(out string captureError), Is.True, captureError);

                player.transform.position = new Vector3(1.8f, -4.01f, 2.7f);
                Assert.That(bounds.TryEnforceBounds(out bool resetToSafePosition, out string error), Is.True, error);

                Assert.That(resetToSafePosition, Is.True);
                Assert.That(player.transform.position, Is.EqualTo(new Vector3(0.5f, 1f, -0.5f)));
                Assert.That(bounds.FallResetCount, Is.EqualTo(1));
                Assert.That(bounds.BoundaryClampCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void ImpactViewRequiresInjectedCameraAndFacesItForLegacyHorizontalSpriteChildren()
        {
            GameObject cameraObject = new GameObject("ImpactCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            GameObject impactObject = new GameObject("ImpactView");
            ImpactView impactView = impactObject.AddComponent<ImpactView>();
            GameObject visualObject = new GameObject("LegacyHorizontalSprite", typeof(SpriteRenderer));
            try
            {
                cameraObject.transform.position = new Vector3(0f, 2f, -8f);
                cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                visualObject.transform.SetParent(impactObject.transform, false);
                visualObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                Assert.That(impactView.TryPrepare(null, out string missingCameraError), Is.False);
                Assert.That(missingCameraError, Does.Contain("billboard camera"));
                Assert.That(impactView.TryPrepare(camera, out string prepareError), Is.True, prepareError);

                impactView.Activate(Vector3.zero, Color.red, 1f);
                AssertFacesCamera(visualObject.transform, camera, impactObject.transform.position);

                cameraObject.transform.position = new Vector3(6f, 1.5f, -5f);
                impactView.SetLifetimeVisual(1, 12);
                AssertFacesCamera(visualObject.transform, camera, impactObject.transform.position);
                Assert.That(impactView.BillboardCamera, Is.SameAs(camera));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(impactObject);
            }
        }

        [Test]
        public void ImpactViewHonorsAnExplicitCameraFacingSurfaceOffset()
        {
            GameObject cameraObject = new GameObject("ImpactOffsetCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            GameObject impactObject = new GameObject("ImpactOffsetView");
            ImpactView impactView = impactObject.AddComponent<ImpactView>();
            GameObject visualObject = new GameObject("ImpactOffsetSprite", typeof(SpriteRenderer));
            try
            {
                cameraObject.transform.position = new Vector3(0f, 0f, -5f);
                cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                visualObject.transform.SetParent(impactObject.transform, false);

                Assert.That(impactView.TryPrepare(camera, out string prepareError), Is.True, prepareError);
                impactView.Activate(Vector3.zero, Color.red, 1f, 0.72f);

                Assert.That(impactView.CameraFacingOffset, Is.EqualTo(0.72f).Within(0.0001f));
                Assert.That(
                    impactObject.transform.position.z,
                    Is.LessThan(-0.7f),
                    "An explicit player-feedback offset must lift the world-space sprite out of the avatar surface toward the gameplay camera.");
                AssertFacesCamera(visualObject.transform, camera, impactObject.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(impactObject);
            }
        }

        [Test]
        public void CoordinatorResolvesImpactCameraBeforeHostContextIsCommitted()
        {
            GameObject contextObject = new GameObject("ImpactPresentationContext");
            BattleSceneContext context = contextObject.AddComponent<BattleSceneContext>();
            GameObject cameraObject = new GameObject("ImpactPresentationCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            GameObject hostObject = new GameObject("ImpactPresentationHost");
            BattleSessionHost host = hostObject.AddComponent<BattleSessionHost>();
            GameObject coordinatorObject = new GameObject("ImpactPresentationCoordinator");
            BattlePresentationCoordinator coordinator = coordinatorObject.AddComponent<BattlePresentationCoordinator>();
            try
            {
                SerializedObject contextSerialized = new SerializedObject(context);
                contextSerialized.FindProperty("mainCamera").objectReferenceValue = camera;
                contextSerialized.ApplyModifiedPropertiesWithoutUndo();
                SerializedObject coordinatorSerialized = new SerializedObject(coordinator);
                coordinatorSerialized.FindProperty("sessionHost").objectReferenceValue = host;
                coordinatorSerialized.ApplyModifiedPropertiesWithoutUndo();

                MethodInfo resolver = typeof(BattlePresentationCoordinator).GetMethod(
                    "TryResolveImpactBillboardCamera",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(resolver, Is.Not.Null);
                object[] arguments = { null, null };

                Assert.That((bool)resolver.Invoke(coordinator, arguments), Is.True);
                Assert.That(arguments[0], Is.SameAs(camera));
                Assert.That(arguments[1], Is.EqualTo(string.Empty));
            }
            finally
            {
                Object.DestroyImmediate(contextObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(hostObject);
                Object.DestroyImmediate(coordinatorObject);
            }
        }

        private static void ConfigureBounds(
            CombatLabPlayerBounds bounds,
            CharacterController characterController,
            Vector2 minimum,
            Vector2 maximum,
            float fallResetHeight)
        {
            SerializedObject serialized = new SerializedObject(bounds);
            serialized.FindProperty("characterController").objectReferenceValue = characterController;
            serialized.FindProperty("minimumPlanarPosition").vector2Value = minimum;
            serialized.FindProperty("maximumPlanarPosition").vector2Value = maximum;
            serialized.FindProperty("fallResetHeight").floatValue = fallResetHeight;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertFacesCamera(Transform visual, Camera camera, Vector3 impactPosition)
        {
            Vector3 toCamera = (camera.transform.position - impactPosition).normalized;
            Assert.That(
                Vector3.Dot(visual.forward, toCamera),
                Is.GreaterThan(0.999f),
                "The impact sprite must face the explicitly injected gameplay camera.");
        }
    }
}
