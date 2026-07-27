using System.Reflection;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgFormalPlayerCameraFeedbackTests
    {
        [Test]
        public void SkillShakesAddClampAndReturnToBaselineWithoutDrift()
        {
            GameObject feedbackObject = new GameObject("CameraFeedbackTest");
            GameObject rigObject = new GameObject("CameraRigTest");
            GameObject cameraObject = new GameObject("CameraTest");
            GameObject playerObject = new GameObject("PlayerTest");
            D0ThreeCProfile threeC =
                ScriptableObject.CreateInstance<D0ThreeCProfile>();
            CombatPresentationProfile presentation =
                ScriptableObject.CreateInstance<CombatPresentationProfile>();
            try
            {
                cameraObject.transform.SetParent(rigObject.transform, false);
                Camera camera = cameraObject.AddComponent<Camera>();
                FpgFormalPlayerCameraFeedback feedback =
                    feedbackObject.AddComponent<FpgFormalPlayerCameraFeedback>();
                Assert.That(feedback.TryPrepare(
                    threeC,
                    camera,
                    rigObject.transform,
                    presentation.CameraShake,
                    4,
                    out string prepareError), Is.True, prepareError);
                Assert.That(feedback.TryApplyFixedSceneRig(
                    playerObject.transform,
                    out string applyError), Is.True, applyError);

                Vector3 baselinePosition = camera.transform.localPosition;
                Quaternion baselineRotation = camera.transform.localRotation;
                Assert.That(feedback.TryAddShake(0.75f, 0.5f), Is.True);
                Assert.That(feedback.TryAddShake(0.75f, 0.5f), Is.True);

                InvokePrivate(feedback, "AdvanceShakes", 0.01f);
                InvokePrivate(feedback, "ApplyCameraLocalOffset");

                Assert.That(feedback.CurrentShakeStrength, Is.EqualTo(1f)
                    .Within(0.0001f));
                Assert.That(
                    Vector3.Distance(
                        baselinePosition,
                        camera.transform.localPosition),
                    Is.LessThanOrEqualTo(
                        presentation.CameraShake.MaximumPositionOffset
                        + 0.0001f));

                feedback.ClearPresentationShake();
                Assert.That(camera.transform.localPosition,
                    Is.EqualTo(baselinePosition));
                Assert.That(
                    Quaternion.Angle(
                        camera.transform.localRotation,
                        baselineRotation),
                    Is.LessThan(0.0001f));

                Assert.That(feedback.TryAddShake(0.8f, 0.5f), Is.True);
                InvokePrivate(feedback, "AdvanceShakes", 0.01f);
                InvokePrivate(feedback, "ApplyCameraLocalOffset");
                Assert.That(feedback.CurrentShakeStrength, Is.GreaterThan(0f));

                feedback.SetPaused(true);

                Assert.That(feedback.CurrentShakeStrength, Is.Zero);
                Assert.That(camera.transform.localPosition,
                    Is.EqualTo(baselinePosition));
                Assert.That(
                    Quaternion.Angle(
                        camera.transform.localRotation,
                        baselineRotation),
                    Is.LessThan(0.0001f));
                feedback.SetPaused(false);

                for (int index = 0; index < 20; index++)
                {
                    Assert.That(feedback.TryAddShake(0.5f, 0.05f), Is.True);
                    InvokePrivate(feedback, "AdvanceShakes", 0.06f);
                    InvokePrivate(feedback, "ApplyCameraLocalOffset");
                    feedback.ClearPresentationShake();
                }

                Assert.That(camera.transform.localPosition,
                    Is.EqualTo(baselinePosition));
                Assert.That(
                    Quaternion.Angle(
                        camera.transform.localRotation,
                        baselineRotation),
                    Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(feedbackObject);
                Object.DestroyImmediate(rigObject);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(threeC);
                Object.DestroyImmediate(presentation);
            }
        }

        private static void InvokePrivate(
            FpgFormalPlayerCameraFeedback feedback,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = typeof(FpgFormalPlayerCameraFeedback)
                .GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(feedback, arguments);
        }
    }
}
