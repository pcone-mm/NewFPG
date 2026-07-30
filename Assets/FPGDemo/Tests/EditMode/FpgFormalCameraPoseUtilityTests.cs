using System.Reflection;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgFormalCameraPoseUtilityTests
    {
        [Test]
        public void ResolveShotUsesPlayerPoseAndCoverProfile()
        {
            FpgCoverCameraProfile profile =
                ScriptableObject.CreateInstance<FpgCoverCameraProfile>();
            try
            {
                SetField(profile, "cameraRigLocalPosition",
                    new Vector3(1f, 2f, 3f));
                SetField(profile, "cameraRigLocalEulerAngles",
                    new Vector3(10f, 20f, 30f));
                SetField(profile, "cameraLocalPosition",
                    new Vector3(0.1f, 0.2f, 0.3f));
                SetField(profile, "cameraLocalEulerAngles",
                    new Vector3(4f, 5f, 6f));
                SetField(profile, "fieldOfView", 72f);
                SetField(profile, "nearClipPlane", 0.25f);
                SetField(profile, "farClipPlane", 125f);

                Pose playerPose = new Pose(
                    new Vector3(10f, -2f, 5f),
                    Quaternion.Euler(0f, 90f, 0f));
                Assert.That(FpgFormalCameraPoseUtility.TryResolveShot(
                    playerPose,
                    profile,
                    out FpgResolvedCameraShot shot,
                    out string error), Is.True, error);

                Vector3 expectedRigPosition = playerPose.position
                    + playerPose.rotation * profile.CameraRigLocalPosition;
                Quaternion expectedRigRotation = playerPose.rotation
                    * Quaternion.Euler(
                        profile.CameraRigLocalEulerAngles);
                Assert.That(Vector3.Distance(
                    shot.RigWorldPose.position,
                    expectedRigPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(
                    shot.RigWorldPose.rotation,
                    expectedRigRotation), Is.LessThan(0.0001f));
                Assert.That(shot.CameraLocalPose.position,
                    Is.EqualTo(profile.CameraLocalPosition));
                Assert.That(Quaternion.Angle(
                    shot.CameraLocalPose.rotation,
                    Quaternion.Euler(profile.CameraLocalEulerAngles)),
                    Is.LessThan(0.0001f));
                Assert.That(shot.FieldOfView, Is.EqualTo(72f));
                Assert.That(shot.NearClipPlane, Is.EqualTo(0.25f));
                Assert.That(shot.FarClipPlane, Is.EqualTo(125f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void InterpolateBlendsPosesAndLensAndApplyUsesSameCamera()
        {
            FpgResolvedCameraShot source = new FpgResolvedCameraShot(
                new Pose(Vector3.zero, Quaternion.identity),
                new Pose(Vector3.zero, Quaternion.identity),
                40f,
                0.1f,
                80f);
            FpgResolvedCameraShot target = new FpgResolvedCameraShot(
                new Pose(
                    new Vector3(8f, 4f, -2f),
                    Quaternion.Euler(0f, 90f, 0f)),
                new Pose(
                    new Vector3(2f, 1f, 0.5f),
                    Quaternion.Euler(20f, 0f, 0f)),
                80f,
                0.3f,
                120f);

            Assert.That(FpgFormalCameraPoseUtility.TryInterpolate(
                source,
                target,
                0.5f,
                out FpgResolvedCameraShot midpoint,
                out string error), Is.True, error);
            Assert.That(midpoint.RigWorldPose.position,
                Is.EqualTo(new Vector3(4f, 2f, -1f)));
            Assert.That(midpoint.CameraLocalPose.position,
                Is.EqualTo(new Vector3(1f, 0.5f, 0.25f)));
            Assert.That(midpoint.FieldOfView, Is.EqualTo(60f));
            Assert.That(midpoint.NearClipPlane,
                Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(midpoint.FarClipPlane, Is.EqualTo(100f));
            Assert.That(Quaternion.Angle(
                midpoint.RigWorldPose.rotation,
                Quaternion.Euler(0f, 45f, 0f)), Is.LessThan(0.001f));

            GameObject rigObject = new GameObject("CameraRigTest");
            GameObject cameraObject = new GameObject("CameraTest");
            try
            {
                cameraObject.transform.SetParent(rigObject.transform, false);
                Camera camera = cameraObject.AddComponent<Camera>();
                Assert.That(FpgFormalCameraPoseUtility.TryApplyShot(
                    midpoint,
                    rigObject.transform,
                    camera,
                    out error), Is.True, error);
                Assert.That(rigObject.transform.position,
                    Is.EqualTo(midpoint.RigWorldPose.position));
                Assert.That(camera.transform.localPosition,
                    Is.EqualTo(midpoint.CameraLocalPose.position));
                Assert.That(camera.fieldOfView,
                    Is.EqualTo(midpoint.FieldOfView));
                Assert.That(camera.nearClipPlane,
                    Is.EqualTo(midpoint.NearClipPlane));
                Assert.That(camera.farClipPlane,
                    Is.EqualTo(midpoint.FarClipPlane));
            }
            finally
            {
                Object.DestroyImmediate(rigObject);
            }
        }

        private static void SetField(
            FpgCoverCameraProfile profile,
            string fieldName,
            object value)
        {
            FieldInfo field = typeof(FpgCoverCameraProfile).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(profile, value);
        }
    }
}
