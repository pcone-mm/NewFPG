using UnityEngine;

namespace NewFPG.Combat
{
    [CreateAssetMenu(fileName = "SO_HTA_Default", menuName = "NewFPG/Combat/Hit Tip Animation Config")]
    public sealed class HitTipAnimationConfig : ScriptableObject
    {
        [SerializeField, Min(0.05f)] private float lifetime = 0.85f;
        [SerializeField] private AnimationCurve verticalOffsetCurve = DefaultVerticalOffsetCurve();
        [SerializeField] private Vector2 randomVerticalOffsetRange = new Vector2(8f, 22f);
        [SerializeField] private Vector2 randomHorizontalOffsetRange = new Vector2(-26f, 26f);
        [SerializeField] private AnimationCurve scaleCurve = DefaultScaleCurve();
        [SerializeField] private AnimationCurve highlightCurve = DefaultHighlightCurve();

        public float Lifetime => Mathf.Max(0.05f, lifetime);
        public Vector2 RandomVerticalOffsetRange => SortRange(randomVerticalOffsetRange);
        public Vector2 RandomHorizontalOffsetRange => SortRange(randomHorizontalOffsetRange);

        public float EvaluateVerticalOffset(float normalizedTime)
        {
            return Evaluate(verticalOffsetCurve, Mathf.Clamp01(normalizedTime), DefaultVerticalOffsetFallback);
        }

        public float EvaluateScale(float normalizedTime)
        {
            return Mathf.Max(0.01f, Evaluate(scaleCurve, Mathf.Clamp01(normalizedTime), DefaultScaleFallback));
        }

        public float EvaluateHighlight(float normalizedTime)
        {
            return Mathf.Clamp01(Evaluate(highlightCurve, Mathf.Clamp01(normalizedTime), DefaultHighlightFallback));
        }

        public void ResetToDefaults()
        {
            lifetime = 0.85f;
            verticalOffsetCurve = DefaultVerticalOffsetCurve();
            randomVerticalOffsetRange = new Vector2(8f, 22f);
            randomHorizontalOffsetRange = new Vector2(-26f, 26f);
            scaleCurve = DefaultScaleCurve();
            highlightCurve = DefaultHighlightCurve();
        }

        private void OnValidate()
        {
            lifetime = Mathf.Max(0.05f, lifetime);
            randomVerticalOffsetRange = SortRange(randomVerticalOffsetRange);
            randomHorizontalOffsetRange = SortRange(randomHorizontalOffsetRange);
            if (verticalOffsetCurve == null || verticalOffsetCurve.length == 0)
            {
                verticalOffsetCurve = DefaultVerticalOffsetCurve();
            }

            if (scaleCurve == null || scaleCurve.length == 0)
            {
                scaleCurve = DefaultScaleCurve();
            }

            if (highlightCurve == null || highlightCurve.length == 0)
            {
                highlightCurve = DefaultHighlightCurve();
            }
        }

        private static Vector2 SortRange(Vector2 range)
        {
            return range.x <= range.y ? range : new Vector2(range.y, range.x);
        }

        private static float Evaluate(AnimationCurve curve, float normalizedTime, System.Func<float, float> fallback)
        {
            return curve == null || curve.length == 0 ? fallback(normalizedTime) : curve.Evaluate(normalizedTime);
        }

        private static float DefaultVerticalOffsetFallback(float normalizedTime)
        {
            float easedOut = 1f - Mathf.Pow(1f - normalizedTime, 2f);
            return 72f * easedOut;
        }

        private static float DefaultScaleFallback(float normalizedTime)
        {
            float scale = Mathf.Lerp(0.55f, 1.15f, Mathf.Clamp01(normalizedTime / 0.18f));
            return normalizedTime > 0.25f
                ? Mathf.Lerp(scale, 0.9f, Mathf.Clamp01((normalizedTime - 0.25f) / 0.75f))
                : scale;
        }

        private static float DefaultHighlightFallback(float normalizedTime)
        {
            return Mathf.Clamp01(1f - normalizedTime * 4f);
        }

        private static AnimationCurve DefaultVerticalOffsetCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.55f, 58f),
                new Keyframe(1f, 72f));
        }

        private static AnimationCurve DefaultScaleCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.55f),
                new Keyframe(0.18f, 1.15f),
                new Keyframe(1f, 0.9f));
        }

        private static AnimationCurve DefaultHighlightCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.18f, 0.25f),
                new Keyframe(1f, 0f));
        }
    }
}
