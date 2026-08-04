using System.Reflection;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class CombatAimViewportMathTests
    {
        private static readonly MethodInfo ApplicationFocusMethod = typeof(CombatAimReticle)
            .GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void SafeAreaClampsAllViewportCoordinatesAndRejectsNonFiniteInput()
        {
            Assert.That(
                CombatAimViewportMath.ClampToSafeArea(new Vector2(-3f, 4f)),
                Is.EqualTo(new Vector2(
                    CombatAimViewportMath.SafeMinimumX,
                    CombatAimViewportMath.SafeMaximumY)));
            Assert.That(
                CombatAimViewportMath.ClampToSafeArea(
                    new Vector2(float.NaN, float.PositiveInfinity)),
                Is.EqualTo(CombatAimViewportMath.Center));
            Assert.That(
                CombatAimViewportMath.IsInsideSafeArea(CombatAimViewportMath.Center),
                Is.True);
            Assert.That(
                CombatAimViewportMath.IsInsideSafeArea(new Vector2(0.01f, 0.5f)),
                Is.False);
        }

        [Test]
        public void MouseDeltaUsesViewportSpaceAndCannotEscapeSafeArea()
        {
            Vector2 moved = CombatAimViewportMath.ApplyMouseDelta(
                CombatAimViewportMath.Center,
                new Vector2(192f, 108f),
                new Vector2(1920f, 1080f),
                1f);
            Assert.That(moved, Is.EqualTo(new Vector2(0.6f, 0.6f)));

            Vector2 clamped = CombatAimViewportMath.ApplyMouseDelta(
                new Vector2(0.91f, 0.87f),
                new Vector2(4000f, 4000f),
                new Vector2(1920f, 1080f),
                1f);
            Assert.That(
                clamped,
                Is.EqualTo(new Vector2(
                    CombatAimViewportMath.SafeMaximumX,
                    CombatAimViewportMath.SafeMaximumY)));
        }

        [Test]
        public void MouseDeltaUsesAuthoredReferenceResolutionInsteadOfOutputResolution()
        {
            Vector2 referenceResolution = new Vector2(1920f, 1080f);
            Vector2 rawDelta = new Vector2(96f, -54f);

            Vector2 at1080p = CombatAimViewportMath.ApplyMouseDelta(
                CombatAimViewportMath.Center,
                rawDelta,
                referenceResolution,
                1f);
            Vector2 at4k = CombatAimViewportMath.ApplyMouseDelta(
                CombatAimViewportMath.Center,
                rawDelta,
                referenceResolution,
                1f);

            Assert.That(at4k, Is.EqualTo(at1080p));
            Assert.That(at1080p, Is.EqualTo(new Vector2(0.55f, 0.45f)));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void GamepadViewportSpeedIsFrameRateIndependent(int frameRate)
        {
            Vector2 value = CombatAimViewportMath.Center;
            float deltaTime = 1f / frameRate;
            for (int frame = 0; frame < frameRate; frame++)
            {
                value = CombatAimViewportMath.ApplyGamepadInput(
                    value,
                    Vector2.right,
                    maximumViewportSpeed: 0.2f,
                    radialDeadzone: 0f,
                    responseExponent: 1f,
                    deltaTime);
            }

            Assert.That(value.x, Is.EqualTo(0.7f).Within(0.0001f));
            Assert.That(value.y, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void GamepadRadialDeadzoneSuppressesDrift()
        {
            Vector2 value = CombatAimViewportMath.ApplyGamepadInput(
                CombatAimViewportMath.Center,
                new Vector2(0.1f, 0.1f),
                maximumViewportSpeed: 1f,
                radialDeadzone: 0.2f,
                responseExponent: 1.5f,
                deltaTime: 1f);

            Assert.That(value, Is.EqualTo(CombatAimViewportMath.Center));
        }

        [Test]
        public void ScreenPointUsesAbsoluteViewportSpaceAndPreservesLastValidValue()
        {
            Vector2 moved = CombatAimViewportMath.ApplyScreenPoint(
                CombatAimViewportMath.Center,
                new Vector2(1440f, 270f),
                new Vector2(1920f, 1080f),
                CombatAimViewportMath.DefaultSafeArea);
            Assert.That(moved, Is.EqualTo(new Vector2(0.75f, 0.25f)));

            Vector2 preserved = CombatAimViewportMath.ApplyScreenPoint(
                moved,
                new Vector2(float.NaN, 20f),
                Vector2.zero,
                CombatAimViewportMath.DefaultSafeArea);
            Assert.That(preserved, Is.EqualTo(moved));
        }

        [Test]
        public void ReticleResetKeepsItsVirtualCursorInTheSafeArea()
        {
            GameObject reticleObject = new GameObject(
                "CombatAimReticleTest",
                typeof(RectTransform));
            reticleObject.SetActive(false);
            CombatAimReticle reticle = reticleObject.AddComponent<CombatAimReticle>();
            try
            {
                reticle.SetViewport(new Vector2(3f, -2f));
                Assert.That(
                    reticle.Viewport,
                    Is.EqualTo(new Vector2(
                        CombatAimViewportMath.SafeMaximumX,
                        CombatAimViewportMath.SafeMinimumY)));
                reticle.SetInputFrozen(true);
                Assert.That(reticle.IsInputFrozen, Is.True);

                reticle.ResetToCenter();
                Assert.That(reticle.TryGetViewport(out Vector2 viewport), Is.True);
                Assert.That(viewport, Is.EqualTo(CombatAimViewportMath.Center));
                Assert.That(reticle.TryValidate(out string error), Is.True, error);
            }
            finally
            {
                Object.DestroyImmediate(reticleObject);
            }
        }

        [Test]
        public void FocusRegainRecentersTheVirtualReticleWithoutChangingItsSafetyContract()
        {
            GameObject reticleObject = new GameObject(
                "CombatAimReticleFocusTest",
                typeof(RectTransform));
            reticleObject.SetActive(false);
            CombatAimReticle reticle = reticleObject.AddComponent<CombatAimReticle>();
            try
            {
                reticle.SetViewport(new Vector2(0.82f, 0.21f));
                Assert.That(reticle.Viewport, Is.Not.EqualTo(CombatAimViewportMath.Center));

                Assert.That(ApplicationFocusMethod, Is.Not.Null);
                ApplicationFocusMethod.Invoke(reticle, new object[] { true });

                Assert.That(reticle.Viewport, Is.EqualTo(CombatAimViewportMath.Center));
                Assert.That(reticle.TryGetViewport(out Vector2 viewport), Is.True);
                Assert.That(CombatAimViewportMath.IsInsideSafeArea(viewport), Is.True);
            }
            finally
            {
                ApplicationFocusMethod?.Invoke(reticle, new object[] { false });
                Object.DestroyImmediate(reticleObject);
            }
        }
    }
}
