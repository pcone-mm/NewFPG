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
                FpgResolvedCameraShot shot = new FpgResolvedCameraShot(
                    new Pose(
                        new Vector3(2f, 4f, -8f),
                        Quaternion.Euler(2f, 0f, 0f)),
                    new Pose(Vector3.zero, Quaternion.identity),
                    65f,
                    0.1f,
                    80f);
                Assert.That(feedback.TryApplyImmediateShot(
                    shot,
                    out string applyError), Is.True, applyError);

                Vector3 baselinePosition = camera.transform.localPosition;
                Quaternion baselineRotation = camera.transform.localRotation;
                Assert.That(feedback.TryAddShake(0.75f, 0.5f), Is.True);
                Assert.That(feedback.TryAddShake(0.75f, 0.5f), Is.True);

                InvokePrivate(feedback, "AdvanceShakes", 0.01f);
                InvokePrivate(feedback, "ApplyCurrentShotWithFeedback");

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
                InvokePrivate(feedback, "ApplyCurrentShotWithFeedback");
                Assert.That(feedback.CurrentShakeStrength, Is.GreaterThan(0f));

                Vector3 pausedPosition = camera.transform.localPosition;
                Quaternion pausedRotation = camera.transform.localRotation;
                float pausedStrength = feedback.CurrentShakeStrength;

                feedback.SetPaused(true);

                Assert.That(feedback.CurrentShakeStrength,
                    Is.EqualTo(pausedStrength));
                Assert.That(camera.transform.localPosition,
                    Is.EqualTo(pausedPosition));
                Assert.That(
                    Quaternion.Angle(
                        camera.transform.localRotation,
                        pausedRotation),
                    Is.LessThan(0.0001f));
                feedback.SetPaused(false);
                feedback.ClearPresentationShake();

                for (int index = 0; index < 20; index++)
                {
                    Assert.That(feedback.TryAddShake(0.5f, 0.05f), Is.True);
                    InvokePrivate(feedback, "AdvanceShakes", 0.06f);
                    InvokePrivate(feedback, "ApplyCurrentShotWithFeedback");
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

        [Test]
        public void ShotTransitionBlendsBaseAndCancelRestoresCommittedShot()
        {
            GameObject feedbackObject = new GameObject("CameraFeedbackTest");
            GameObject rigObject = new GameObject("CameraRigTest");
            GameObject cameraObject = new GameObject("CameraTest");
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

                FpgResolvedCameraShot source = new FpgResolvedCameraShot(
                    new Pose(Vector3.zero, Quaternion.identity),
                    new Pose(
                        new Vector3(0.1f, 0.2f, 0.3f),
                        Quaternion.identity),
                    50f,
                    0.1f,
                    60f);
                FpgResolvedCameraShot target = new FpgResolvedCameraShot(
                    new Pose(
                        new Vector3(10f, 4f, -2f),
                        Quaternion.Euler(0f, 90f, 0f)),
                    new Pose(
                        new Vector3(1.1f, 1.2f, 1.3f),
                        Quaternion.Euler(10f, 20f, 30f)),
                    70f,
                    0.3f,
                    100f);

                Assert.That(feedback.TryApplyImmediateShot(
                    source,
                    out string applyError), Is.True, applyError);
                Assert.That(feedback.TryBeginShotTransition(
                    source,
                    target,
                    1f,
                    out string beginError), Is.True, beginError);
                SetPrivateField(feedback, "currentKick", 0.25f);

                feedback.AdvanceShotTransition(0.5f);
                InvokePrivate(feedback, "ApplyCurrentShotWithFeedback");
                Assert.That(rigObject.transform.position.x,
                    Is.EqualTo(5f).Within(0.0001f));
                Assert.That(camera.fieldOfView,
                    Is.EqualTo(60f).Within(0.0001f));
                Assert.That(camera.transform.localPosition,
                    Is.EqualTo(
                        feedback.CurrentBaseShot.CameraLocalPose.position
                        + feedback.CurrentBaseShot.CameraLocalPose.rotation
                            * (Vector3.back * 0.25f)));

                feedback.SetPaused(true);
                feedback.AdvanceShotTransition(0.5f);
                Assert.That(feedback.TransitionProgress,
                    Is.EqualTo(0.5f).Within(0.0001f));
                feedback.SetPaused(false);

                feedback.CancelShotTransition();
                Assert.That(feedback.IsTransitioning, Is.False);
                Assert.That(rigObject.transform.position,
                    Is.EqualTo(source.RigWorldPose.position));
                Assert.That(camera.fieldOfView,
                    Is.EqualTo(source.FieldOfView).Within(0.0001f));
                Assert.That(camera.transform.localPosition,
                    Is.EqualTo(
                        source.CameraLocalPose.position
                        + source.CameraLocalPose.rotation
                            * (Vector3.back * 0.25f)));

                Assert.That(feedback.TryBeginShotTransition(
                    source,
                    target,
                    1f,
                    out beginError), Is.True, beginError);
                feedback.AdvanceShotTransition(1f);
                Assert.That(feedback.TryCommitShotTransition(
                    out string commitError), Is.True, commitError);
                Assert.That(feedback.IsTransitioning, Is.False);
                Assert.That(rigObject.transform.position,
                    Is.EqualTo(target.RigWorldPose.position));
                Assert.That(camera.fieldOfView,
                    Is.EqualTo(target.FieldOfView).Within(0.0001f));
                Assert.That(camera.transform.localPosition,
                    Is.EqualTo(
                        target.CameraLocalPose.position
                        + target.CameraLocalPose.rotation
                            * (Vector3.back * 0.25f)));
            }
            finally
            {
                Object.DestroyImmediate(feedbackObject);
                Object.DestroyImmediate(rigObject);
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

        private static void SetPrivateField(
            FpgFormalPlayerCameraFeedback feedback,
            string fieldName,
            object value)
        {
            FieldInfo field = typeof(FpgFormalPlayerCameraFeedback)
                .GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(feedback, value);
        }
    }
}
