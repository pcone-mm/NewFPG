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
