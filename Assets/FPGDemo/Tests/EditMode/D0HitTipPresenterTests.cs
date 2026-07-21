using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0HitTipPresenterTests
    {
        [Test]
        public void FixedPoolUsesNormalAndCriticalBindingsAndClearResetsEveryView()
        {
            GameObject poolRootObject = new GameObject("D0HitTipPoolRoot", typeof(RectTransform));
            GameObject presenterObject = new GameObject("D0HitTipPresenter", typeof(RectTransform));
            D0HitTipPresenter presenter = presenterObject.AddComponent<D0HitTipPresenter>();
            Sprite normalSprite = CreateSprite(Color.white, out Texture2D normalTexture);
            Sprite criticalSprite = CreateSprite(Color.yellow, out Texture2D criticalTexture);
            try
            {
                Assert.That(
                    presenter.TryPrepare(
                        poolRootObject.GetComponent<RectTransform>(),
                        normalSprite,
                        criticalSprite,
                        null,
                        2,
                        out string prepareError),
                    Is.True,
                    prepareError);
                Assert.That(presenter.Capacity, Is.EqualTo(2));
                Assert.That(poolRootObject.transform.childCount, Is.EqualTo(2));

                Assert.That(
                    presenter.TryShow(D0HitTipKind.Body, 12, new Vector2(0.5f, 0.5f)),
                    Is.True);
                Image firstBackground = GetBackground(poolRootObject.transform, 0);
                Text firstValue = GetValue(poolRootObject.transform, 0);
                Assert.That(firstBackground.sprite, Is.SameAs(normalSprite));
                Assert.That(firstValue.text, Is.EqualTo("12"));
                Assert.That(presenter.ActiveCount, Is.EqualTo(1));

                Assert.That(
                    presenter.TryShow(D0HitTipKind.Weakpoint, 48, new Vector2(0.6f, 0.5f)),
                    Is.True);
                Assert.That(presenter.TryShow(D0HitTipKind.Intercept, 7, new Vector2(0.7f, 0.5f)), Is.False);
                Assert.That(presenter.SpawnRejectCount, Is.EqualTo(1));

                presenter.Clear();

                Assert.That(presenter.ActiveCount, Is.Zero);
                Assert.That(presenter.SpawnRejectCount, Is.Zero);
                for (int index = 0; index < poolRootObject.transform.childCount; index++)
                {
                    Transform view = poolRootObject.transform.GetChild(index);
                    Assert.That(view.gameObject.activeSelf, Is.False);
                    Assert.That(GetBackground(poolRootObject.transform, index).sprite, Is.Null);
                    Assert.That(GetValue(poolRootObject.transform, index).text, Is.Empty);
                }

                Assert.That(
                    presenter.TryShow(D0HitTipKind.Weakpoint, 99, new Vector2(0.5f, 0.5f)),
                    Is.True);
                Assert.That(GetBackground(poolRootObject.transform, 0).sprite, Is.SameAs(criticalSprite));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(poolRootObject);
                UnityEngine.Object.DestroyImmediate(normalSprite);
                UnityEngine.Object.DestroyImmediate(criticalSprite);
                UnityEngine.Object.DestroyImmediate(normalTexture);
                UnityEngine.Object.DestroyImmediate(criticalTexture);
            }
        }

        private static Image GetBackground(Transform poolRoot, int index)
        {
            return poolRoot.GetChild(index).GetComponentInChildren<Image>(true);
        }

        private static Text GetValue(Transform poolRoot, int index)
        {
            return poolRoot.GetChild(index).GetComponentInChildren<Text>(true);
        }

        private static Sprite CreateSprite(Color color, out Texture2D texture)
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.SetPixel(1, 0, color);
            texture.SetPixel(0, 1, color);
            texture.SetPixel(1, 1, color);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
        }
    }
}
