using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// One-draw-call UGUI visual for the player aim indicator. The base ring,
    /// aiming glow and outer hit segments are separate mesh layers so a shot
    /// can expand the ring without moving the hit marker.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LayeredAimIndicatorGraphic : MaskableGraphic
    {
        private const int RingSegments = 64;
        private const int MinimumArcSegments = 3;

        private float ringRadius = 15f;
        private float ringThickness = 2f;
        private float aimingGlowAlpha;
        private float hitAlpha;
        private float hitProgress;
        private float hitMarkerRadius = 27f;
        private float hitMarkerThickness = 2.6f;
        private float hitMarkerArcDegrees = 24f;
        private float hitExpansion = 4f;
        private Color ringColor = new Color(0.48f, 0.82f, 0.92f, 0.56f);
        private Color hitColor = new Color(1f, 0.13f, 0.10f, 1f);

        public float RingRadius => ringRadius;
        public float RingThickness => ringThickness;
        public float AimingGlowAlpha => aimingGlowAlpha;
        public float HitAlpha => hitAlpha;
        public float HitProgress => hitProgress;
        public Color RingColor => ringColor;
        public Color HitColor => hitColor;

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            color = Color.white;
            SetVerticesDirty();
        }

        public bool TryApplyStyle(
            PlayerAimIndicatorPresentationDefinition style,
            out string error)
        {
            if (style == null)
            {
                error = "Layered aim indicator requires a style.";
                return false;
            }

            if (!style.TryValidate(out error))
            {
                return false;
            }

            hitMarkerRadius = style.HitMarkerRadius;
            hitMarkerThickness = style.HitMarkerThickness;
            hitMarkerArcDegrees = style.HitMarkerArcDegrees;
            hitExpansion = style.HitExpansion;
            hitColor = style.HitColor;
            SetVerticesDirty();
            error = string.Empty;
            return true;
        }

        public void SetPresentation(
            float nextRingRadius,
            float nextRingThickness,
            Color nextRingColor,
            float nextAimingGlowAlpha,
            float nextHitAlpha,
            float nextHitProgress)
        {
            nextRingRadius = Mathf.Max(0f, nextRingRadius);
            nextRingThickness = Mathf.Max(0f, nextRingThickness);
            nextAimingGlowAlpha = Mathf.Clamp01(nextAimingGlowAlpha);
            nextHitAlpha = Mathf.Clamp01(nextHitAlpha);
            nextHitProgress = Mathf.Clamp01(nextHitProgress);

            if (Mathf.Approximately(ringRadius, nextRingRadius)
                && Mathf.Approximately(ringThickness, nextRingThickness)
                && ringColor.Equals(nextRingColor)
                && Mathf.Approximately(aimingGlowAlpha, nextAimingGlowAlpha)
                && Mathf.Approximately(hitAlpha, nextHitAlpha)
                && Mathf.Approximately(hitProgress, nextHitProgress))
            {
                return;
            }

            ringRadius = nextRingRadius;
            ringThickness = nextRingThickness;
            ringColor = nextRingColor;
            aimingGlowAlpha = nextAimingGlowAlpha;
            hitAlpha = nextHitAlpha;
            hitProgress = nextHitProgress;
            SetVerticesDirty();
        }

        public bool TryValidate(out string error)
        {
            if (!(transform is RectTransform))
            {
                error = "Layered aim indicator requires a RectTransform.";
                return false;
            }

            float expandedHitOuterEdge =
                hitMarkerRadius + hitExpansion + hitMarkerThickness * 0.5f;
            if (!IsFinitePositive(ringRadius)
                || !IsFinitePositive(ringThickness)
                || ringThickness >= ringRadius * 2f
                || !IsFinitePositive(hitMarkerRadius)
                || !IsFinitePositive(hitMarkerThickness)
                || hitMarkerThickness >= hitMarkerRadius * 2f
                || !IsFinite(hitMarkerArcDegrees)
                || hitMarkerArcDegrees < 4f || hitMarkerArcDegrees > 60f
                || !IsFinite(hitExpansion) || hitExpansion < 0f
                || !IsFinite(expandedHitOuterEdge))
            {
                error = "Layered aim indicator geometry is invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (ringRadius <= 0f || ringThickness <= 0f)
            {
                return;
            }

            Vector2 center = rectTransform.rect.center;
            if (aimingGlowAlpha > 0.001f)
            {
                Color glowColor = ringColor;
                glowColor.a *= aimingGlowAlpha;
                AddRingSegment(
                    vertexHelper,
                    center,
                    ringRadius,
                    ringThickness * 2.8f,
                    0f,
                    360f,
                    RingSegments,
                    glowColor);
            }

            AddRingSegment(
                vertexHelper,
                center,
                ringRadius,
                ringThickness,
                0f,
                360f,
                RingSegments,
                ringColor);

            if (hitAlpha <= 0.001f)
            {
                return;
            }

            Color markerColor = hitColor;
            markerColor.a *= hitAlpha;
            float markerRadius = hitMarkerRadius
                + hitExpansion * SmoothStep01(hitProgress);
            int markerSegments = Mathf.Max(
                MinimumArcSegments,
                Mathf.CeilToInt(hitMarkerArcDegrees / 4f));
            float halfArc = hitMarkerArcDegrees * 0.5f;
            for (int index = 0; index < 4; index++)
            {
                float centerAngle = 45f + index * 90f;
                AddRingSegment(
                    vertexHelper,
                    center,
                    markerRadius,
                    hitMarkerThickness,
                    centerAngle - halfArc,
                    centerAngle + halfArc,
                    markerSegments,
                    markerColor);
            }
        }

        private static void AddRingSegment(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            float thickness,
            float startDegrees,
            float endDegrees,
            int segmentCount,
            Color colorValue)
        {
            float halfThickness = Mathf.Max(0.01f, thickness * 0.5f);
            float innerRadius = Mathf.Max(0f, radius - halfThickness);
            float outerRadius = radius + halfThickness;
            int firstVertex = vertexHelper.currentVertCount;
            Color32 vertexColor = colorValue;
            for (int index = 0; index <= segmentCount; index++)
            {
                float fraction = index / (float)segmentCount;
                float angle = Mathf.Lerp(startDegrees, endDegrees, fraction)
                    * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertexHelper.AddVert(
                    center + direction * outerRadius,
                    vertexColor,
                    Vector2.zero);
                vertexHelper.AddVert(
                    center + direction * innerRadius,
                    vertexColor,
                    Vector2.zero);
            }

            for (int index = 0; index < segmentCount; index++)
            {
                int current = firstVertex + index * 2;
                vertexHelper.AddTriangle(current, current + 2, current + 1);
                vertexHelper.AddTriangle(current + 2, current + 3, current + 1);
            }
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
