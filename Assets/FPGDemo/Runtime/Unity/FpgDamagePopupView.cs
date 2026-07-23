using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgDamagePopupView : MonoBehaviour
    {
        private const int DigitCapacity = 10;

        [SerializeField] private RectTransform root;
        [SerializeField] private Image background;
        [SerializeField] private RectTransform digitsRoot;
        [SerializeField] private Image[] digitImages = new Image[DigitCapacity];
        [SerializeField] private CanvasGroup canvasGroup;

        private float elapsed;
        private float duration;

        public bool IsActive { get; private set; }
        public RectTransform RectTransform => root;
        public Image Background => background;
        public RectTransform DigitsRoot => digitsRoot;
        public IReadOnlyList<Image> DigitImages => digitImages;
        public int VisibleDigitCount { get; private set; }
        public float LastDigitsWidth { get; private set; }
        public float LastBackgroundWidth { get; private set; }

        public bool TryValidate(out string error)
        {
            if (root == null || root != transform
                || background == null || digitsRoot == null
                || canvasGroup == null || canvasGroup.transform != transform
                || digitImages == null || digitImages.Length != DigitCapacity
                || !background.transform.IsChildOf(root)
                || !digitsRoot.IsChildOf(root))
            {
                error = "Formal damage-popup view requires its root, background, "
                    + "digits root, ten digit Images and CanvasGroup.";
                return false;
            }

            for (int index = 0; index < digitImages.Length; index++)
            {
                Image image = digitImages[index];
                if (image == null || !image.transform.IsChildOf(digitsRoot))
                {
                    error = "Formal damage-popup digit slots must contain exactly "
                        + DigitCapacity + " Images under the digits root.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public bool TryShow(
            Vector2 anchoredPosition,
            int value,
            FpgDamagePopupSpriteStyle style,
            float visibleDuration)
        {
            if (!TryValidate(out _) || value < 0 || style == null
                || !IsFinitePositive(visibleDuration)
                || !IsFinitePositive(style.DigitHeight)
                || !IsFinite(style.DigitSpacing)
                || !IsFiniteNonNegative(style.BackgroundHorizontalPadding)
                || !IsFinitePositive(style.BackgroundMinSize.x)
                || !IsFinitePositive(style.BackgroundMinSize.y)
                || style.BackgroundSprite == null)
            {
                return false;
            }

            int digitCount = ResolveDigitCount(value);
            float digitsWidth = style.DigitSpacing * (digitCount - 1);
            int remaining = value;
            for (int index = digitCount - 1; index >= 0; index--)
            {
                int digit = remaining % 10;
                remaining /= 10;
                Sprite sprite = style.GetDigitSprite(digit);
                if (sprite == null || sprite.rect.width <= 0f
                    || sprite.rect.height <= 0f)
                {
                    return false;
                }

                digitsWidth += ResolveSpriteWidth(sprite, style.DigitHeight);
            }

            float backgroundWidth = Mathf.Max(
                style.BackgroundMinSize.x,
                digitsWidth + style.BackgroundHorizontalPadding * 2f);
            ApplyRootGeometry(
                anchoredPosition,
                digitsWidth,
                backgroundWidth,
                style);
            ApplyDigits(value, digitCount, style);

            elapsed = 0f;
            duration = visibleDuration;
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            IsActive = true;
            gameObject.SetActive(true);
            return true;
        }

        public void Advance(float unscaledDeltaTime, bool paused)
        {
            if (!IsActive || paused)
            {
                return;
            }

            elapsed += Mathf.Max(0f, unscaledDeltaTime);
            float normalized = duration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = 1f - normalized;
            if (normalized >= 1f)
            {
                Release();
            }
        }

        public void Release()
        {
            IsActive = false;
            elapsed = 0f;
            duration = 0f;
            VisibleDigitCount = 0;
            LastDigitsWidth = 0f;
            LastBackgroundWidth = 0f;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (background != null)
            {
                background.sprite = null;
                background.gameObject.SetActive(false);
            }

            if (digitImages != null)
            {
                for (int index = 0; index < digitImages.Length; index++)
                {
                    Image image = digitImages[index];
                    if (image == null)
                    {
                        continue;
                    }

                    image.sprite = null;
                    image.gameObject.SetActive(false);
                }
            }

            if (digitsRoot != null)
            {
                digitsRoot.gameObject.SetActive(false);
            }

            gameObject.SetActive(false);
        }

        private void ApplyRootGeometry(
            Vector2 anchoredPosition,
            float digitsWidth,
            float backgroundWidth,
            FpgDamagePopupSpriteStyle style)
        {
            root.anchoredPosition = anchoredPosition;
            root.sizeDelta = new Vector2(
                backgroundWidth,
                style.BackgroundMinSize.y);

            background.sprite = style.BackgroundSprite;
            background.type = Image.Type.Sliced;
            background.preserveAspect = false;
            background.raycastTarget = false;
            background.color = Color.white;
            Stretch(background.rectTransform);
            background.gameObject.SetActive(true);

            digitsRoot.anchorMin = new Vector2(0.5f, 0.5f);
            digitsRoot.anchorMax = new Vector2(0.5f, 0.5f);
            digitsRoot.pivot = new Vector2(0.5f, 0.5f);
            digitsRoot.anchoredPosition = Vector2.zero;
            digitsRoot.sizeDelta = new Vector2(digitsWidth, style.DigitHeight);
            digitsRoot.gameObject.SetActive(true);

            LastDigitsWidth = digitsWidth;
            LastBackgroundWidth = backgroundWidth;
        }

        private void ApplyDigits(
            int value,
            int digitCount,
            FpgDamagePopupSpriteStyle style)
        {
            int remaining = value;
            for (int index = digitCount - 1; index >= 0; index--)
            {
                int digit = remaining % 10;
                remaining /= 10;
                Image image = digitImages[index];
                Sprite sprite = style.GetDigitSprite(digit);
                float width = ResolveSpriteWidth(sprite, style.DigitHeight);
                RectTransform imageRect = image.rectTransform;
                imageRect.anchorMin = new Vector2(0f, 0.5f);
                imageRect.anchorMax = new Vector2(0f, 0.5f);
                imageRect.pivot = new Vector2(0f, 0.5f);
                imageRect.sizeDelta = new Vector2(width, style.DigitHeight);
                imageRect.anchoredPosition = Vector2.zero;
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = Color.white;
            }

            float offset = 0f;
            for (int index = 0; index < digitCount; index++)
            {
                Image image = digitImages[index];
                image.rectTransform.anchoredPosition = new Vector2(offset, 0f);
                image.gameObject.SetActive(true);
                offset += image.rectTransform.sizeDelta.x;
                if (index < digitCount - 1)
                {
                    offset += style.DigitSpacing;
                }
            }

            for (int index = digitCount; index < digitImages.Length; index++)
            {
                Image image = digitImages[index];
                image.sprite = null;
                image.gameObject.SetActive(false);
            }

            VisibleDigitCount = digitCount;
        }

        private static int ResolveDigitCount(int value)
        {
            int count = 1;
            while (value >= 10)
            {
                value /= 10;
                count++;
            }

            return count;
        }

        private static float ResolveSpriteWidth(Sprite sprite, float targetHeight)
        {
            return targetHeight * sprite.rect.width / sprite.rect.height;
        }

        private static void Stretch(RectTransform target)
        {
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
