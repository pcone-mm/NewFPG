using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0ForestParallaxTests
    {
        private sealed class AimSource : ICombatAimViewportSource
        {
            public bool TryGetViewport(out Vector2 viewport)
            {
                viewport = CombatAimViewportMath.Center;
                return true;
            }
        }

        [Test]
        public void LayerOffsetIsZeroAtCenterAndUsesOnlySafeViewportDistance()
        {
            Assert.That(
                D0ForestParallaxLayer.ComputeOffset(
                    CombatAimViewportMath.Center,
                    new Vector2(4f, 2f)),
                Is.EqualTo(Vector3.zero));

            Vector3 offset = D0ForestParallaxLayer.ComputeOffset(
                new Vector2(
                    CombatAimViewportMath.SafeMaximumX,
                    CombatAimViewportMath.SafeMaximumY),
                new Vector2(4f, 2f));
            Assert.That(offset.x, Is.EqualTo(-1.68f).Within(0.0001f));
            Assert.That(offset.y, Is.EqualTo(-0.76f).Within(0.0001f));
            Assert.That(offset.z, Is.Zero);
        }

        [Test]
        public void LayerReturnsToItsAuthoredBasePosition()
        {
            GameObject root = new GameObject("D0ForestLayerTestRoot");
            GameObject child = new GameObject("Layer");
            child.transform.SetParent(root.transform, false);
            D0ForestParallaxLayer layer = child.AddComponent<D0ForestParallaxLayer>();
            try
            {
                Vector3 basePosition = new Vector3(2f, -1f, 12f);
                layer.Configure(basePosition, new Vector2(3f, 1f));
                layer.ApplyViewport(new Vector2(
                    CombatAimViewportMath.SafeMaximumX,
                    CombatAimViewportMath.SafeMaximumY));
                Assert.That(child.transform.localPosition, Is.Not.EqualTo(basePosition));

                layer.ResetToBasePosition();
                Assert.That(child.transform.localPosition, Is.EqualTo(basePosition));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PresentationBindingUsesExplicitAimSourceAndUnbindResetsLayer()
        {
            GameObject root = new GameObject("D0ForestBindingTestRoot");
            GameObject layerObject = new GameObject("Layer");
            layerObject.transform.SetParent(root.transform, false);
            GameObject cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            try
            {
                D0ForestParallaxLayer layer =
                    layerObject.AddComponent<D0ForestParallaxLayer>();
                Vector3 basePosition = new Vector3(2f, -1f, 12f);
                layer.Configure(basePosition, new Vector2(3f, 1f));
                D0ForestParallax parallax =
                    root.AddComponent<D0ForestParallax>();
                parallax.Configure(
                    (ICombatAimViewportSource)null,
                    new[] { layer });

                Camera camera = cameraObject.AddComponent<Camera>();
                Light mainLight = root.AddComponent<Light>();
                mainLight.type = LightType.Directional;
                AimSource source = new AimSource();
                FpgRoomArtPresentationContext context =
                    new FpgRoomArtPresentationContext(
                        camera,
                        mainLight,
                        source);

                Assert.That(
                    parallax.TryBind(context, out string error),
                    Is.True,
                    error);
                Assert.That(parallax.AimViewportSource, Is.SameAs(source));

                layer.ApplyViewport(new Vector2(
                    CombatAimViewportMath.SafeMaximumX,
                    CombatAimViewportMath.SafeMaximumY));
                Assert.That(layerObject.transform.localPosition, Is.Not.EqualTo(basePosition));

                parallax.Unbind();
                Assert.That(parallax.AimViewportSource, Is.Null);
                Assert.That(layerObject.transform.localPosition, Is.EqualTo(basePosition));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
