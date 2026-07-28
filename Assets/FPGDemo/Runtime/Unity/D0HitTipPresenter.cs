using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Presentation-only categories for D0's floating hit-number UI. These are
    /// deliberately local to FPG.Unity: they describe how an already-resolved
    /// result is shown and do not participate in combat state or hit queries.
    /// </summary>
    public enum D0HitTipKind
    {
        Body = 0,
        Weakpoint = 1,
        Intercept = 2
    }

    /// <summary>
    /// Fixed-capacity UGUI hit-tip pool. It owns no gameplay state and receives
    /// only a resolved value plus a normalized viewport location from a Unity
    /// presentation bridge. Pool construction happens during preparation;
    /// TryShow, Advance and Clear never instantiate, destroy, search, use LINQ,
    /// or build strings by concatenation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class D0HitTipPresenter : MonoBehaviour
    {
        private static readonly Vector2 CenterAnchor = new Vector2(0.5f, 0.5f);
        private static readonly Color TransparentWhite = new Color(1f, 1f, 1f, 0f);

        [Header("Explicit D0 bindings")]
        [SerializeField]
        private RectTransform poolRoot;

        [SerializeField]
        private Sprite normalBackgroundSprite;

        [SerializeField]
        private Sprite criticalBackgroundSprite;

        [SerializeField]
        private Font numberFont;

        [Header("Fixed pool")]
        [SerializeField, Min(1)]
        private int prewarmCapacity = 32;

        [SerializeField]
        private Vector2 tipSize = new Vector2(176f, 72f);

        [Header("Readable D0 styles")]
        [SerializeField]
        private Color bodyNumberColor = new Color(1f, 0.84f, 0.63f, 1f);

        [SerializeField]
        private Color weakpointNumberColor = new Color(1f, 0.83f, 0.22f, 1f);

        [SerializeField]
        private Color interceptNumberColor = new Color(0.66f, 1f, 1f, 1f);

        [SerializeField, Min(0.01f)]
        private float bodyDuration = 0.34f;

        [SerializeField, Min(0.01f)]
        private float weakpointDuration = 0.46f;

        [SerializeField, Min(0.01f)]
        private float interceptDuration = 0.4f;

        private D0HitTipView[] views;
        private bool[] activeSlots;
        private Font resolvedNumberFont;
        private bool prepared;

        public bool IsPrepared => prepared;
        public int Capacity => views == null ? 0 : views.Length;
        public int PrewarmCapacity => prewarmCapacity;
        public int SpawnRejectCount { get; private set; }
        public RectTransform PoolRoot => poolRoot;
        public Sprite NormalBackgroundSprite => normalBackgroundSprite;
        public Sprite CriticalBackgroundSprite => criticalBackgroundSprite;

        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < Capacity; index++)
                {
                    if (activeSlots[index])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void Awake()
        {
            // Scene authoring binds the references explicitly. A missing binding
            // is intentionally silent so a newly added component can be fully
            // configured before it is asked to prepare.
            TryPrepare(out _);
        }

        private void OnDisable()
        {
            Clear();
        }

        /// <summary>
        /// Checks only serialized D0 authoring bindings. It deliberately does
        /// not prewarm the pool in edit mode, which would serialize runtime-only
        /// pooled children into the authored scene.
        /// </summary>
        public bool TryValidate(out string error)
        {
            if (poolRoot == null)
            {
                error = "D0 hit tips require a UI pool root.";
                return false;
            }

            if (normalBackgroundSprite == null || criticalBackgroundSprite == null)
            {
                error = "D0 hit tips require normal and critical background sprites.";
                return false;
            }

            if (prewarmCapacity <= 0)
            {
                error = "D0 hit tips require a positive fixed pool capacity.";
                return false;
            }

            if (!poolRoot.IsChildOf(transform) && poolRoot != transform)
            {
                error = "D0 hit-tip pool root must be the presenter transform or one of its children.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Uses the inspector-bound UI root, normal/critical backgrounds and
        /// fixed capacity. This is the normal D0 runtime entry point.
        /// </summary>
        public bool TryPrepare(out string error)
        {
            return TryPrepare(
                poolRoot,
                normalBackgroundSprite,
                criticalBackgroundSprite,
                numberFont,
                prewarmCapacity,
                out error);
        }

        /// <summary>
        /// Explicit setup overload intended for scene binding and isolated tests.
        /// A null font resolves to Unity's built-in legacy UI font; the
        /// two background sprites must always be bound deliberately.
        /// </summary>
        public bool TryPrepare(
            RectTransform nextPoolRoot,
            Sprite nextNormalBackgroundSprite,
            Sprite nextCriticalBackgroundSprite,
            Font nextNumberFont,
            int nextCapacity,
            out string error)
        {
            if (nextPoolRoot == null
                || nextNormalBackgroundSprite == null
                || nextCriticalBackgroundSprite == null
                || nextCapacity <= 0)
            {
                error = "D0 hit tips require a UI pool root, normal and critical background sprites, and a positive capacity.";
                return false;
            }

            if (prepared)
            {
                if (poolRoot != nextPoolRoot
                    || normalBackgroundSprite != nextNormalBackgroundSprite
                    || criticalBackgroundSprite != nextCriticalBackgroundSprite)
                {
                    error = "Prepared D0 hit-tip bindings cannot be changed at runtime.";
                    return false;
                }

                if (Capacity < nextCapacity)
                {
                    error = "Prepared D0 hit-tip capacity is below the requested fixed capacity.";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            poolRoot = nextPoolRoot;
            normalBackgroundSprite = nextNormalBackgroundSprite;
            criticalBackgroundSprite = nextCriticalBackgroundSprite;
            numberFont = nextNumberFont;
            prewarmCapacity = nextCapacity;
            resolvedNumberFont = numberFont == null
                ? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                : numberFont;
            if (resolvedNumberFont == null)
            {
                error = "D0 hit tips could not resolve a legacy UI font.";
                return false;
            }

            views = new D0HitTipView[prewarmCapacity];
            activeSlots = new bool[prewarmCapacity];
            try
            {
                for (int index = 0; index < prewarmCapacity; index++)
                {
                    views[index] = CreateView(index, resolvedNumberFont);
                }
            }
            catch (Exception exception)
            {
                DestroyPreparedViews();
                error = "D0 hit-tip prewarm failed: " + exception.Message;
                return false;
            }

            prepared = true;
            SpawnRejectCount = 0;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Shows a tip in viewport coordinates (0..1), keeping a damage number
        /// in the D0 HUD layer rather than attaching UI to any combat object.
        /// </summary>
        public bool TryShow(D0HitTipKind kind, int value, Vector2 viewportPosition)
        {
            return TryShow(kind, value, viewportPosition, GetDefaultDuration(kind));
        }

        /// <summary>
        /// Identical to the normal entry point but accepts a profile-owned
        /// duration. This lets a presentation bridge honour a configured hit
        /// feedback duration without making the pool read combat state.
        /// </summary>
        public bool TryShow(
            D0HitTipKind kind,
            int value,
            Vector2 viewportPosition,
            float duration)
        {
            if (!prepared
                || duration <= 0f
                || !IsFinite(viewportPosition)
                || !TryResolveStyle(kind, out D0HitTipStyle style))
            {
                SpawnRejectCount++;
                return false;
            }

            int slot = FindFreeSlot();
            if (slot < 0)
            {
                SpawnRejectCount++;
                return false;
            }

            Vector2 clampedViewport = new Vector2(
                Mathf.Clamp01(viewportPosition.x),
                Mathf.Clamp01(viewportPosition.y));
            views[slot].Activate(clampedViewport, value, style, duration, slot);
            activeSlots[slot] = true;
            return true;
        }

        /// <summary>
        /// Convenience bridge for the existing Unity presentation profile.
        /// CombatHitPresentationKind is authored presentation data in this
        /// assembly, not a domain/combat dependency.
        /// </summary>
        public bool TryShow(CombatHitPresentationKind kind, int value, Vector2 viewportPosition)
        {
            return TryShow(ToD0Kind(kind), value, viewportPosition);
        }

        public bool TryShow(
            CombatHitPresentationKind kind,
            int value,
            Vector2 viewportPosition,
            float duration)
        {
            return TryShow(ToD0Kind(kind), value, viewportPosition, duration);
        }

        /// <summary>
        /// Called by the owning presentation coordinator after it has decided
        /// that transient visuals should advance. This intentionally is not an
        /// Update loop, so pause and terminal-state ownership remain explicit.
        /// </summary>
        public void Advance(float deltaTime)
        {
            if (!prepared || deltaTime < 0f || !IsFinite(deltaTime))
            {
                return;
            }

            for (int index = 0; index < Capacity; index++)
            {
                if (activeSlots[index] && !views[index].Advance(deltaTime))
                {
                    activeSlots[index] = false;
                }
            }
        }

        /// <summary>
        /// Returns every pooled view to its neutral inactive state. It resets
        /// anchors, scale, alpha, sprite, text and diagnostics so F5/restart
        /// cannot retain a transient hit indicator into the next session.
        /// </summary>
        public void Clear()
        {
            if (!prepared)
            {
                return;
            }

            for (int index = 0; index < Capacity; index++)
            {
                views[index].Deactivate();
                activeSlots[index] = false;
            }

            SpawnRejectCount = 0;
        }

        private D0HitTipView CreateView(int index, Font font)
        {
            GameObject viewObject = new GameObject(
                "D0HitTip_" + index.ToString(CultureInfo.InvariantCulture),
                typeof(RectTransform),
                typeof(CanvasGroup));
            RectTransform viewRect = viewObject.GetComponent<RectTransform>();
            viewRect.SetParent(poolRoot, false);
            viewRect.anchorMin = CenterAnchor;
            viewRect.anchorMax = CenterAnchor;
            viewRect.pivot = CenterAnchor;
            viewRect.sizeDelta = tipSize;

            CanvasGroup canvasGroup = viewObject.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            GameObject backgroundObject = new GameObject(
                "Background",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.SetParent(viewRect, false);
            Stretch(backgroundRect);
            Image backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = true;
            backgroundImage.raycastTarget = false;

            GameObject valueObject = new GameObject(
                "Value",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(Outline));
            RectTransform valueRect = valueObject.GetComponent<RectTransform>();
            valueRect.SetParent(viewRect, false);
            Stretch(valueRect);
            Text valueText = valueObject.GetComponent<Text>();
            valueText.font = font;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            valueText.verticalOverflow = VerticalWrapMode.Overflow;
            valueText.supportRichText = false;
            valueText.raycastTarget = false;
            Outline textOutline = valueObject.GetComponent<Outline>();
            textOutline.useGraphicAlpha = false;

            D0HitTipView view = new D0HitTipView(
                viewObject,
                viewRect,
                backgroundRect,
                canvasGroup,
                backgroundImage,
                valueText,
                textOutline);
            view.Deactivate();
            return view;
        }

        private void DestroyPreparedViews()
        {
            if (views != null)
            {
                for (int index = 0; index < views.Length; index++)
                {
                    if (views[index] != null)
                    {
                        DestroyObject(views[index].GameObject);
                    }
                }
            }

            views = null;
            activeSlots = null;
            resolvedNumberFont = null;
            prepared = false;
            SpawnRejectCount = 0;
        }

        private int FindFreeSlot()
        {
            for (int index = 0; index < Capacity; index++)
            {
                if (!activeSlots[index])
                {
                    return index;
                }
            }

            return -1;
        }

        private float GetDefaultDuration(D0HitTipKind kind)
        {
            switch (kind)
            {
                case D0HitTipKind.Body:
                    return bodyDuration;
                case D0HitTipKind.Weakpoint:
                    return weakpointDuration;
                case D0HitTipKind.Intercept:
                    return interceptDuration;
                default:
                    return 0f;
            }
        }

        private bool TryResolveStyle(D0HitTipKind kind, out D0HitTipStyle style)
        {
            switch (kind)
            {
                case D0HitTipKind.Body:
                    style = new D0HitTipStyle(
                        normalBackgroundSprite,
                        bodyNumberColor,
                        new Color(0.15f, 0.035f, 0.015f, 1f),
                        48f,
                        9f,
                        1f,
                        1.1f,
                        0f,
                        30,
                        FontStyle.Bold);
                    return true;

                case D0HitTipKind.Weakpoint:
                    style = new D0HitTipStyle(
                        criticalBackgroundSprite,
                        weakpointNumberColor,
                        new Color(0.22f, 0.08f, 0.005f, 1f),
                        76f,
                        14f,
                        1.08f,
                        1.34f,
                        0f,
                        38,
                        FontStyle.Bold);
                    return true;

                case D0HitTipKind.Intercept:
                    style = new D0HitTipStyle(
                        normalBackgroundSprite,
                        interceptNumberColor,
                        new Color(0.005f, 0.11f, 0.15f, 1f),
                        58f,
                        18f,
                        0.94f,
                        1.18f,
                        45f,
                        32,
                        FontStyle.BoldAndItalic);
                    return true;

                default:
                    style = default(D0HitTipStyle);
                    return false;
            }
        }

        private static D0HitTipKind ToD0Kind(CombatHitPresentationKind kind)
        {
            switch (kind)
            {
                case CombatHitPresentationKind.Weakpoint:
                    return D0HitTipKind.Weakpoint;
                case CombatHitPresentationKind.Intercept:
                    return D0HitTipKind.Intercept;
                default:
                    return D0HitTipKind.Body;
            }
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = CenterAnchor;
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void DestroyObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private readonly struct D0HitTipStyle
        {
            public D0HitTipStyle(
                Sprite backgroundSprite,
                Color numberColor,
                Color outlineColor,
                float risePixels,
                float lateralPixels,
                float baseScale,
                float peakScale,
                float backgroundRotationDegrees,
                int fontSize,
                FontStyle fontStyle)
            {
                BackgroundSprite = backgroundSprite;
                NumberColor = numberColor;
                OutlineColor = outlineColor;
                RisePixels = risePixels;
                LateralPixels = lateralPixels;
                BaseScale = baseScale;
                PeakScale = peakScale;
                BackgroundRotationDegrees = backgroundRotationDegrees;
                FontSize = fontSize;
                FontStyle = fontStyle;
            }

            public Sprite BackgroundSprite { get; }
            public Color NumberColor { get; }
            public Color OutlineColor { get; }
            public float RisePixels { get; }
            public float LateralPixels { get; }
            public float BaseScale { get; }
            public float PeakScale { get; }
            public float BackgroundRotationDegrees { get; }
            public int FontSize { get; }
            public FontStyle FontStyle { get; }
        }

        private sealed class D0HitTipView
        {
            private readonly RectTransform viewRect;
            private readonly RectTransform backgroundRect;
            private readonly CanvasGroup canvasGroup;
            private readonly Image backgroundImage;
            private readonly Text valueText;
            private readonly Outline textOutline;
            private Vector2 startOffset;
            private Vector2 movement;
            private float duration;
            private float elapsed;
            private float baseScale;
            private float peakScale;

            public D0HitTipView(
                GameObject gameObject,
                RectTransform viewRect,
                RectTransform backgroundRect,
                CanvasGroup canvasGroup,
                Image backgroundImage,
                Text valueText,
                Outline textOutline)
            {
                GameObject = gameObject;
                this.viewRect = viewRect;
                this.backgroundRect = backgroundRect;
                this.canvasGroup = canvasGroup;
                this.backgroundImage = backgroundImage;
                this.valueText = valueText;
                this.textOutline = textOutline;
            }

            public GameObject GameObject { get; }

            public void Activate(
                Vector2 viewportPosition,
                int value,
                D0HitTipStyle style,
                float nextDuration,
                int slot)
            {
                duration = Mathf.Max(0.01f, nextDuration);
                elapsed = 0f;
                baseScale = style.BaseScale;
                peakScale = style.PeakScale;
                float lateralDirection = (slot & 1) == 0 ? -1f : 1f;
                startOffset = new Vector2(lateralDirection * style.LateralPixels, 0f);
                movement = new Vector2(lateralDirection * style.LateralPixels * 0.3f, style.RisePixels);

                viewRect.anchorMin = viewportPosition;
                viewRect.anchorMax = viewportPosition;
                viewRect.anchoredPosition = startOffset;
                viewRect.localScale = Vector3.one * baseScale;
                backgroundRect.localRotation = Quaternion.Euler(0f, 0f, style.BackgroundRotationDegrees);
                canvasGroup.alpha = 1f;
                backgroundImage.sprite = style.BackgroundSprite;
                backgroundImage.color = Color.white;
                valueText.text = value.ToString(CultureInfo.InvariantCulture);
                valueText.color = style.NumberColor;
                valueText.fontSize = style.FontSize;
                valueText.fontStyle = style.FontStyle;
                textOutline.effectColor = style.OutlineColor;
                textOutline.effectDistance = new Vector2(1.5f, -1.5f);
                GameObject.SetActive(true);
            }

            public bool Advance(float deltaTime)
            {
                if (!GameObject.activeSelf)
                {
                    return false;
                }

                elapsed += deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float scaleProgress = Mathf.Clamp01(progress / 0.18f);
                float scale = progress < 0.18f
                    ? Mathf.Lerp(baseScale, peakScale, scaleProgress)
                    : Mathf.Lerp(peakScale, baseScale, (progress - 0.18f) / 0.82f);
                viewRect.anchoredPosition = startOffset + movement * progress;
                viewRect.localScale = Vector3.one * scale;
                canvasGroup.alpha = progress < 0.62f
                    ? 1f
                    : Mathf.Clamp01(1f - (progress - 0.62f) / 0.38f);
                if (progress < 1f)
                {
                    return true;
                }

                Deactivate();
                return false;
            }

            public void Deactivate()
            {
                duration = 0f;
                elapsed = 0f;
                startOffset = Vector2.zero;
                movement = Vector2.zero;
                baseScale = 1f;
                peakScale = 1f;
                viewRect.anchorMin = CenterAnchor;
                viewRect.anchorMax = CenterAnchor;
                viewRect.anchoredPosition = Vector2.zero;
                viewRect.localScale = Vector3.one;
                backgroundRect.localRotation = Quaternion.identity;
                canvasGroup.alpha = 0f;
                backgroundImage.sprite = null;
                backgroundImage.color = TransparentWhite;
                valueText.text = string.Empty;
                valueText.color = TransparentWhite;
                valueText.fontSize = 30;
                valueText.fontStyle = FontStyle.Normal;
                textOutline.effectColor = TransparentWhite;
                textOutline.effectDistance = Vector2.zero;
                if (GameObject.activeSelf)
                {
                    GameObject.SetActive(false);
                }
            }
        }
    }
}
