using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Presentation-only bar geometry. The fill is resized from left to right
    /// through RectTransform anchors, so rendering does not depend on an Image
    /// sprite or Image.Type.Filled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgFormalBarView : MonoBehaviour
    {
        [SerializeField] private RectTransform fillRect;
        [SerializeField, Range(0f, 1f)] private float normalizedValue = 1f;
        [SerializeField, Min(0.01f)] private float transitionDuration = 0.14f;

        public RectTransform FillRect => fillRect;
        public float NormalizedValue => normalizedValue;
        public float TargetNormalizedValue { get; private set; } = 1f;
        public float TransitionDuration => transitionDuration;
        public bool IsPaused { get; private set; }

        private float transitionStartValue = 1f;
        private float transitionElapsed;

        public bool TryValidate(out string error)
        {
            if (fillRect == null)
            {
                error = "Formal bar requires a fill RectTransform.";
                return false;
            }

            if (fillRect == transform || !fillRect.IsChildOf(transform))
            {
                error = "Formal bar fill must be a child of the bar view.";
                return false;
            }

            if (!(fillRect.parent is RectTransform))
            {
                error = "Formal bar fill requires a RectTransform parent.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool SetValue(int value, int maximum, bool immediate = false)
        {
            float target = maximum <= 0
                ? 0f
                : Mathf.Clamp01(Mathf.Max(0, value) / (float)maximum);
            return immediate
                ? SetNormalizedValue(target)
                : SetTargetNormalizedValue(target);
        }

        public bool SetNormalizedValue(float value)
        {
            if (fillRect == null)
            {
                return false;
            }

            normalizedValue = Mathf.Clamp01(value);
            TargetNormalizedValue = normalizedValue;
            transitionStartValue = normalizedValue;
            transitionElapsed = transitionDuration;
            ApplyGeometry();
            return true;
        }

        public bool SetTargetNormalizedValue(float value)
        {
            if (fillRect == null)
            {
                return false;
            }

            float target = Mathf.Clamp01(value);
            if (Mathf.Approximately(target, TargetNormalizedValue))
            {
                return true;
            }

            transitionStartValue = normalizedValue;
            TargetNormalizedValue = target;
            transitionElapsed = 0f;
            return true;
        }

        public bool TrySetTransitionDuration(float duration)
        {
            if (duration <= 0f || float.IsNaN(duration)
                || float.IsInfinity(duration))
            {
                return false;
            }

            transitionDuration = duration;
            return true;
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
        }

        public void Advance(float unscaledDeltaTime)
        {
            if (IsPaused || Mathf.Approximately(normalizedValue, TargetNormalizedValue))
            {
                return;
            }

            transitionElapsed += Mathf.Max(0f, unscaledDeltaTime);
            float progress = transitionDuration <= 0f
                ? 1f
                : Mathf.Clamp01(transitionElapsed / transitionDuration);
            normalizedValue = Mathf.Lerp(
                transitionStartValue,
                TargetNormalizedValue,
                Mathf.SmoothStep(0f, 1f, progress));
            ApplyGeometry();
        }

        public void TrySetFillColor(Color color)
        {
            UnityEngine.UI.Graphic graphic =
                fillRect == null ? null : fillRect.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null)
            {
                graphic.color = color;
            }
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        private void OnEnable()
        {
            if (fillRect != null)
            {
                TargetNormalizedValue = normalizedValue;
                transitionStartValue = normalizedValue;
                transitionElapsed = transitionDuration;
                ApplyGeometry();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            normalizedValue = Mathf.Clamp01(normalizedValue);
            TargetNormalizedValue = Mathf.Clamp01(TargetNormalizedValue);
            transitionDuration = Mathf.Max(0.01f, transitionDuration);
            if (fillRect != null)
            {
                ApplyGeometry();
            }
        }
#endif

        private void ApplyGeometry()
        {
            Vector2 anchorMin = fillRect.anchorMin;
            Vector2 anchorMax = fillRect.anchorMax;
            anchorMin.x = 0f;
            anchorMax.x = normalizedValue;
            fillRect.anchorMin = anchorMin;
            fillRect.anchorMax = anchorMax;

            Vector2 offsetMin = fillRect.offsetMin;
            Vector2 offsetMax = fillRect.offsetMax;
            offsetMin.x = 0f;
            offsetMax.x = 0f;
            fillRect.offsetMin = offsetMin;
            fillRect.offsetMax = offsetMax;
        }
    }
}
