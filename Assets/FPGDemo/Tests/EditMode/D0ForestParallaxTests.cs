using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0ForestParallaxTests
    {
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
    }
}
