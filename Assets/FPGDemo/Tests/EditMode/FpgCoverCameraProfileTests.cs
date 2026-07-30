using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgCoverCameraProfileTests
    {
        [Test]
        public void DefaultsMatchTheFeiCoverCameraContract()
        {
            FpgCoverCameraProfile profile =
                ScriptableObject.CreateInstance<FpgCoverCameraProfile>();
            try
            {
                Assert.That(profile.CameraRigLocalPosition,
                    Is.EqualTo(new Vector3(0f, 5.74f, -9.96f)));
                Assert.That(profile.CameraRigLocalEulerAngles,
                    Is.EqualTo(new Vector3(0.86f, 0f, 0f)));
                Assert.That(profile.CameraLocalPosition, Is.EqualTo(Vector3.zero));
                Assert.That(profile.CameraLocalEulerAngles, Is.EqualTo(Vector3.zero));
                Assert.That(profile.FieldOfView, Is.EqualTo(65f));
                Assert.That(profile.NearClipPlane, Is.EqualTo(0.1f));
                Assert.That(profile.FarClipPlane, Is.EqualTo(80f));
                Assert.That(profile.PlayerViewportAnchor,
                    Is.EqualTo(new Vector2(0.5f, 0.22f)));
                Assert.That(profile.FocusViewportAnchor,
                    Is.EqualTo(new Vector2(0.5f, 0.56f)));
                Assert.That(profile.TryValidate(out string error), Is.True, error);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ValidationRejectsNonFiniteCameraTransforms()
        {
            FpgCoverCameraProfile profile =
                ScriptableObject.CreateInstance<FpgCoverCameraProfile>();
            try
            {
                SerializedObject serialized = new SerializedObject(profile);
                serialized.FindProperty("cameraRigLocalPosition").vector3Value =
                    new Vector3(float.NaN, 0f, 0f);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(profile.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("finite camera transform"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [TestCase("fieldOfView", 1f)]
        [TestCase("fieldOfView", 179f)]
        [TestCase("nearClipPlane", 0f)]
        [TestCase("farClipPlane", 0.1f)]
        public void ValidationRejectsInvalidLensValues(
            string propertyName,
            float value)
        {
            FpgCoverCameraProfile profile =
                ScriptableObject.CreateInstance<FpgCoverCameraProfile>();
            try
            {
                SerializedObject serialized = new SerializedObject(profile);
                serialized.FindProperty(propertyName).floatValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(profile.TryValidate(out string error), Is.False);
                Assert.That(error, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [TestCase("playerViewportAnchor", -0.01f, 0.5f)]
        [TestCase("focusViewportAnchor", 0.5f, 1.01f)]
        public void ValidationRejectsViewportAnchorsOutsideTheNormalizedRange(
            string propertyName,
            float x,
            float y)
        {
            FpgCoverCameraProfile profile =
                ScriptableObject.CreateInstance<FpgCoverCameraProfile>();
            try
            {
                SerializedObject serialized = new SerializedObject(profile);
                serialized.FindProperty(propertyName).vector2Value =
                    new Vector2(x, y);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(profile.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("normalized viewport"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
