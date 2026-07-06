using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NewFPG.Combat
{
    public sealed class DamageNumberView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private RectTransform digitsRoot;
        [SerializeField] private Text fallbackText;
        [SerializeField, Min(0f)] private float fallbackRiseDistance = 72f;
        [SerializeField, Min(0f)] private float fallbackDriftDistance = 26f;

        private readonly List<Image> digitImages = new List<Image>();
        private Vector2 startAnchoredPosition;
        private Vector2 drift;
        private HitTipStyleConfig style;
        private HitTipAnimationConfig animationConfig;
        private Color baseColor = Color.white;
        private Color highlightColor = Color.white;
        private Color backgroundBaseColor = Color.white;
        private float elapsed;
        private float lifetime = 0.85f;
        private bool playing;
        private bool usingSpriteLayout;

        public bool IsPlaying => playing;
        public float LastBackgroundWidth { get; private set; }
        public float LastDigitsWidth { get; private set; }
        public float LastScale { get; private set; } = 1f;
        public float LastHighlight { get; private set; }

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
        }

        public void Initialize(Text text, CanvasGroup group)
        {
            fallbackText = text;
            canvasGroup = group;
            CacheReferences();
            gameObject.SetActive(false);
        }

        public void Initialize(Image background, RectTransform digitsParent, CanvasGroup group, Text fallback)
        {
            backgroundImage = background;
            digitsRoot = digitsParent;
            canvasGroup = group;
            fallbackText = fallback;
            CacheReferences();
            SetVisualMode(false);
            gameObject.SetActive(false);
        }

        public void Play(string text, Vector2 screenPosition, Color color, float lifetime)
        {
            CacheReferences();
            style = null;
            animationConfig = null;
            usingSpriteLayout = false;
            this.lifetime = Mathf.Max(0.05f, lifetime);
            baseColor = color;
            highlightColor = Color.white;
            BeginPlay(screenPosition);

            if (fallbackText != null)
            {
                fallbackText.text = text;
                fallbackText.color = color;
            }

            SetVisualMode(false);
            LastBackgroundWidth = rectTransform != null ? rectTransform.sizeDelta.x : 0f;
            LastDigitsWidth = 0f;
            LastHighlight = 0f;
        }

        public void Play(HitTipRequest request, Vector2 screenPosition, HitTipStyleConfig style, HitTipAnimationConfig animation)
        {
            if (style == null || !style.IsValid)
            {
                Play(request.Amount.ToString("0"), screenPosition, request.HasColorOverride ? request.ColorOverride : Color.white, 0.85f);
                return;
            }

            CacheReferences();
            this.style = style;
            animationConfig = animation;
            lifetime = animationConfig != null ? animationConfig.Lifetime : 0.85f;
            baseColor = request.HasColorOverride ? request.ColorOverride : style.BaseColor;
            highlightColor = style.HighlightColor;
            backgroundBaseColor = Color.white;
            usingSpriteLayout = true;
            BeginPlay(screenPosition);
            BuildSpriteLayout(Mathf.RoundToInt(request.Amount).ToString());
            SetVisualMode(true);
            ApplySpriteColors(0f);
        }

        public bool Tick(float deltaTime)
        {
            if (!playing)
            {
                return false;
            }

            elapsed += Mathf.Max(0f, deltaTime);
            float ratio = Mathf.Clamp01(elapsed / lifetime);
            if (usingSpriteLayout)
            {
                TickSpriteLayout(ratio);
            }
            else
            {
                TickFallbackText(ratio);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(1f - Mathf.Pow(ratio, 1.8f));
            }

            if (ratio < 1f)
            {
                return true;
            }

            Stop();
            return false;
        }

        public void Stop()
        {
            playing = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            gameObject.SetActive(false);
        }

        private void BeginPlay(Vector2 screenPosition)
        {
            gameObject.SetActive(true);
            elapsed = 0f;
            playing = true;
            LastScale = 1f;

            if (usingSpriteLayout && animationConfig != null)
            {
                Vector2 horizontalRange = animationConfig.RandomHorizontalOffsetRange;
                Vector2 verticalRange = animationConfig.RandomVerticalOffsetRange;
                drift = new Vector2(
                    UnityEngine.Random.Range(horizontalRange.x, horizontalRange.y),
                    UnityEngine.Random.Range(verticalRange.x, verticalRange.y));
            }
            else
            {
                drift = new Vector2(
                    UnityEngine.Random.Range(-fallbackDriftDistance, fallbackDriftDistance),
                    UnityEngine.Random.Range(8f, 22f));
            }

            if (rectTransform != null)
            {
                if (rectTransform.parent is RectTransform parentRect)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parentRect,
                        screenPosition,
                        null,
                        out Vector2 localPoint);
                    rectTransform.anchoredPosition = localPoint;
                }
                else
                {
                    rectTransform.position = screenPosition;
                }

                startAnchoredPosition = rectTransform.anchoredPosition;
                rectTransform.localScale = usingSpriteLayout ? Vector3.one : Vector3.one * 0.55f;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        private void TickSpriteLayout(float ratio)
        {
            float verticalOffset = animationConfig != null
                ? animationConfig.EvaluateVerticalOffset(ratio)
                : fallbackRiseDistance * (1f - Mathf.Pow(1f - ratio, 2f));
            float easedOut = 1f - Mathf.Pow(1f - ratio, 2f);
            LastScale = animationConfig != null ? animationConfig.EvaluateScale(ratio) : 1f;

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = startAnchoredPosition
                    + new Vector2(drift.x * easedOut, drift.y * easedOut)
                    + Vector2.up * verticalOffset;
                rectTransform.localScale = Vector3.one * LastScale;
            }

            ApplySpriteColors(animationConfig != null ? animationConfig.EvaluateHighlight(ratio) : 0f);
        }

        private void TickFallbackText(float ratio)
        {
            float easedOut = 1f - Mathf.Pow(1f - ratio, 2f);
            float scale = Mathf.Lerp(0.55f, 1.15f, Mathf.Clamp01(ratio / 0.18f));
            if (ratio > 0.25f)
            {
                scale = Mathf.Lerp(scale, 0.9f, Mathf.Clamp01((ratio - 0.25f) / 0.75f));
            }

            LastScale = scale;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = startAnchoredPosition
                    + drift * easedOut
                    + Vector2.up * (fallbackRiseDistance * easedOut);
                rectTransform.localScale = Vector3.one * scale;
            }
        }

        private void BuildSpriteLayout(string amountText)
        {
            EnsureDigitCapacity(amountText.Length);
            float digitHeight = style.DigitHeight;
            float totalWidth = 0f;
            for (int i = 0; i < amountText.Length; i++)
            {
                int digit = amountText[i] - '0';
                Sprite sprite = style.GetDigitSprite(digit);
                Image image = digitImages[i];
                image.gameObject.SetActive(sprite != null);
                image.sprite = sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;

                float width = ResolveSpriteWidth(sprite, digitHeight);
                RectTransform imageRect = image.rectTransform;
                imageRect.anchorMin = new Vector2(0f, 0.5f);
                imageRect.anchorMax = new Vector2(0f, 0.5f);
                imageRect.pivot = new Vector2(0f, 0.5f);
                imageRect.sizeDelta = new Vector2(width, digitHeight);
                imageRect.anchoredPosition = new Vector2(totalWidth, 0f);
                totalWidth += width;
                if (i < amountText.Length - 1)
                {
                    totalWidth += style.DigitSpacing;
                }
            }

            for (int i = amountText.Length; i < digitImages.Count; i++)
            {
                digitImages[i].gameObject.SetActive(false);
            }

            LastDigitsWidth = Mathf.Max(0f, totalWidth);
            Vector2 backgroundMinSize = style.BackgroundMinSize;
            float backgroundWidth = Mathf.Max(backgroundMinSize.x, LastDigitsWidth + style.BackgroundHorizontalPadding * 2f);
            LastBackgroundWidth = backgroundWidth;

            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(backgroundWidth, backgroundMinSize.y);
            }

            if (backgroundImage != null)
            {
                backgroundImage.sprite = style.BackgroundSprite;
                backgroundImage.type = Image.Type.Sliced;
                backgroundImage.raycastTarget = false;
                backgroundImage.rectTransform.sizeDelta = new Vector2(backgroundWidth, backgroundMinSize.y);
                Stretch(backgroundImage.rectTransform);
            }

            if (digitsRoot != null)
            {
                digitsRoot.sizeDelta = new Vector2(LastDigitsWidth, digitHeight);
                digitsRoot.anchorMin = new Vector2(0.5f, 0.5f);
                digitsRoot.anchorMax = new Vector2(0.5f, 0.5f);
                digitsRoot.pivot = new Vector2(0.5f, 0.5f);
                digitsRoot.anchoredPosition = Vector2.zero;
            }
        }

        private void ApplySpriteColors(float highlight)
        {
            LastHighlight = Mathf.Clamp01(highlight);
            Color color = Color.Lerp(baseColor, highlightColor, LastHighlight);
            if (backgroundImage != null)
            {
                backgroundImage.color = Color.Lerp(backgroundBaseColor, highlightColor, LastHighlight * 0.25f);
            }

            for (int i = 0; i < digitImages.Count; i++)
            {
                if (digitImages[i] != null)
                {
                    digitImages[i].color = color;
                }
            }
        }

        private void EnsureDigitCapacity(int count)
        {
            if (digitsRoot == null)
            {
                GameObject rootObject = new GameObject("Digits", typeof(RectTransform));
                rootObject.transform.SetParent(transform, false);
                digitsRoot = rootObject.GetComponent<RectTransform>();
            }

            while (digitImages.Count < count)
            {
                GameObject digitObject = new GameObject("Digit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                digitObject.transform.SetParent(digitsRoot, false);
                Image image = digitObject.GetComponent<Image>();
                image.raycastTarget = false;
                digitImages.Add(image);
            }
        }

        private void SetVisualMode(bool spriteMode)
        {
            if (backgroundImage != null)
            {
                backgroundImage.gameObject.SetActive(spriteMode);
            }

            if (digitsRoot != null)
            {
                digitsRoot.gameObject.SetActive(spriteMode);
            }

            if (fallbackText != null)
            {
                fallbackText.gameObject.SetActive(!spriteMode);
            }
        }

        private void CacheReferences()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (fallbackText == null)
            {
                fallbackText = GetComponentInChildren<Text>(true);
            }

            if (backgroundImage == null)
            {
                Transform background = transform.Find("Background");
                backgroundImage = background != null ? background.GetComponent<Image>() : null;
            }

            if (digitsRoot == null)
            {
                Transform digits = transform.Find("Digits");
                digitsRoot = digits != null ? digits.GetComponent<RectTransform>() : null;
            }

            if (digitsRoot != null && digitImages.Count == 0)
            {
                digitImages.AddRange(digitsRoot.GetComponentsInChildren<Image>(true));
            }
        }

        private static float ResolveSpriteWidth(Sprite sprite, float targetHeight)
        {
            if (sprite == null || sprite.rect.height <= 0f)
            {
                return targetHeight;
            }

            return targetHeight * sprite.rect.width / sprite.rect.height;
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
