using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// One-draw-call formal aim indicator. Durable state, shot feedback, hit
    /// feedback and progress/range rings are independent mesh layers.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LayeredAimIndicatorGraphic : MaskableGraphic
    {
        private const int RingSegments = 64;
        private const int MinimumArcSegments = 3;

        private bool layeredStyleApplied;
        private FpgAimIndicatorBaseState baseState =
            FpgAimIndicatorBaseState.Normal;
        private float crosshairGap = 4f;
        private float crosshairArmLength = 7f;
        private float crosshairThickness = 1.5f;
        private float prohibitedRadius = 14f;
        private float prohibitedThickness = 2f;
        private float reloadRadius = 21f;
        private float reloadThickness = 2f;
        private float reloadProgress;
        private float reloadLoopPhase;
        private float primarySpreadRadius;
        private float primarySpreadThickness = 1f;
        private float secondaryRangeRadius;
        private float secondaryRangeThickness = 1.5f;
        private float secondaryChargeProgress;
        private bool secondaryRangeVisible;
        private float shotAlpha;
        private float shotProgress;
        private float shotRadius = 23f;
        private float hitAlpha;
        private float hitProgress;
        private float hitMarkerRadius = 27f;
        private float hitMarkerThickness = 2.6f;
        private float hitMarkerArcDegrees = 24f;
        private float hitExpansion = 4f;
        private float ringRadius = 15f;
        private float ringThickness = 2f;
        private float aimingGlowAlpha;
        private Color ringColor = Color.white;
        private Color normalColor = Color.white;
        private Color enemyColor = new Color(0.22f, 0.68f, 1f, 1f);
        private Color unavailableColor = new Color(0.65f, 0.68f, 0.72f, 0.72f);
        private Color currentCoverBlockedColor = new Color(1f, 0.24f, 0.18f, 1f);
        private Color reloadColor = Color.white;
        private Color shotColor = Color.white;
        private Color hitColor = new Color(1f, 0.13f, 0.10f, 1f);
        private Color primarySpreadColor = new Color(1f, 1f, 1f, 0.34f);
        private Color secondaryRangeColor = new Color(0.22f, 0.72f, 1f, 0.52f);

        public bool HasLayeredStyle => layeredStyleApplied;
        public FpgAimIndicatorBaseState BaseState => baseState;
        public float RingRadius => ringRadius;
        public float RingThickness => ringThickness;
        public float AimingGlowAlpha => aimingGlowAlpha;
        public float ShotAlpha => shotAlpha;
        public float ShotProgress => shotProgress;
        public float HitAlpha => hitAlpha;
        public float HitProgress => hitProgress;
        public float ReloadProgress => reloadProgress;
        public float ReloadLoopPhase => reloadLoopPhase;
        public float PrimarySpreadRadius => primarySpreadRadius;
        public float SecondaryRangeRadius => secondaryRangeRadius;
        public float SecondaryChargeProgress => secondaryChargeProgress;
        public bool IsSecondaryRangeVisible => secondaryRangeVisible;
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

            normalColor = style.NormalColor;
            enemyColor = style.EnemyColor;
            unavailableColor = style.UnavailableColor;
            currentCoverBlockedColor = style.CurrentCoverBlockedColor;
            reloadColor = style.ReloadColor;
            shotColor = style.ShotColor;
            hitColor = style.HitColor;
            primarySpreadColor = style.PrimarySpreadColor;
            secondaryRangeColor = style.SecondaryRangeColor;
            crosshairGap = style.CrosshairGap;
            crosshairArmLength = style.CrosshairArmLength;
            crosshairThickness = style.CrosshairThickness;
            prohibitedRadius = style.ProhibitedRadius;
            prohibitedThickness = style.ProhibitedThickness;
            reloadRadius = style.ReloadRadius;
            reloadThickness = style.ReloadThickness;
            primarySpreadThickness = style.PrimarySpreadThickness;
            secondaryRangeThickness = style.SecondaryRangeThickness;
            ringRadius = style.BaseRadius;
            ringThickness = style.RingThickness;
            shotRadius = style.ShotRadius;
            hitMarkerRadius = style.HitMarkerRadius;
            hitMarkerThickness = style.HitMarkerThickness;
            hitMarkerArcDegrees = style.HitMarkerArcDegrees;
            hitExpansion = style.HitExpansion;
            layeredStyleApplied = true;
            SetVerticesDirty();
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Compatibility entry used by the original ring-only implementation.
        /// </summary>
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

        public void SetLayeredPresentation(
            FpgAimIndicatorBaseState nextBaseState,
            float nextShotAlpha,
            float nextShotProgress,
            float nextHitAlpha,
            float nextHitProgress,
            float nextReloadProgress,
            float nextReloadLoopPhase,
            float nextPrimarySpreadRadius,
            bool nextSecondaryRangeVisible,
            float nextSecondaryRangeRadius,
            float nextSecondaryChargeProgress)
        {
            if (!System.Enum.IsDefined(
                    typeof(FpgAimIndicatorBaseState),
                    nextBaseState))
            {
                nextBaseState = FpgAimIndicatorBaseState.Hidden;
            }

            nextShotAlpha = ClampFinite01(nextShotAlpha);
            nextShotProgress = ClampFinite01(nextShotProgress);
            nextHitAlpha = ClampFinite01(nextHitAlpha);
            nextHitProgress = ClampFinite01(nextHitProgress);
            nextReloadProgress = ClampFinite01(nextReloadProgress);
            nextReloadLoopPhase = Mathf.Repeat(
                IsFinite(nextReloadLoopPhase) ? nextReloadLoopPhase : 0f,
                1f);
            nextPrimarySpreadRadius = ClampFiniteNonNegative(
                nextPrimarySpreadRadius);
            nextSecondaryRangeRadius = ClampFiniteNonNegative(
                nextSecondaryRangeRadius);
            nextSecondaryChargeProgress = ClampFinite01(
                nextSecondaryChargeProgress);

            if (baseState == nextBaseState
                && Mathf.Approximately(shotAlpha, nextShotAlpha)
                && Mathf.Approximately(shotProgress, nextShotProgress)
                && Mathf.Approximately(hitAlpha, nextHitAlpha)
                && Mathf.Approximately(hitProgress, nextHitProgress)
                && Mathf.Approximately(reloadProgress, nextReloadProgress)
                && Mathf.Approximately(reloadLoopPhase, nextReloadLoopPhase)
                && Mathf.Approximately(
                    primarySpreadRadius,
                    nextPrimarySpreadRadius)
                && secondaryRangeVisible == nextSecondaryRangeVisible
                && Mathf.Approximately(
                    secondaryRangeRadius,
                    nextSecondaryRangeRadius)
                && Mathf.Approximately(
                    secondaryChargeProgress,
                    nextSecondaryChargeProgress))
            {
                return;
            }

            baseState = nextBaseState;
            shotAlpha = nextShotAlpha;
            shotProgress = nextShotProgress;
            hitAlpha = nextHitAlpha;
            hitProgress = nextHitProgress;
            reloadProgress = nextReloadProgress;
            reloadLoopPhase = nextReloadLoopPhase;
            primarySpreadRadius = nextPrimarySpreadRadius;
            secondaryRangeVisible = nextSecondaryRangeVisible;
            secondaryRangeRadius = nextSecondaryRangeRadius;
            secondaryChargeProgress = nextSecondaryChargeProgress;
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
                || !IsFinitePositive(crosshairArmLength)
                || !IsFinitePositive(crosshairThickness)
                || !IsFinitePositive(prohibitedRadius)
                || !IsFinitePositive(prohibitedThickness)
                || !IsFinitePositive(reloadRadius)
                || !IsFinitePositive(reloadThickness)
                || !IsFinitePositive(primarySpreadThickness)
                || !IsFinitePositive(secondaryRangeThickness)
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
            Vector2 center = rectTransform.rect.center;
            if (!layeredStyleApplied)
            {
                PopulateLegacyMesh(vertexHelper, center);
                return;
            }

            if (baseState == FpgAimIndicatorBaseState.Hidden)
            {
                return;
            }

            if (primarySpreadRadius > primarySpreadThickness)
            {
                AddRingSegment(
                    vertexHelper,
                    center,
                    primarySpreadRadius,
                    primarySpreadThickness,
                    0f,
                    360f,
                    RingSegments,
                    primarySpreadColor);
            }

            if (secondaryRangeVisible
                && secondaryRangeRadius > secondaryRangeThickness)
            {
                Color rangeBackground = secondaryRangeColor;
                rangeBackground.a *= 0.36f;
                AddRingSegment(
                    vertexHelper,
                    center,
                    secondaryRangeRadius,
                    secondaryRangeThickness,
                    0f,
                    360f,
                    RingSegments,
                    rangeBackground);
                AddProgressArc(
                    vertexHelper,
                    center,
                    secondaryRangeRadius,
                    secondaryRangeThickness * 1.45f,
                    secondaryChargeProgress,
                    -90f,
                    secondaryRangeColor);
            }

            switch (baseState)
            {
                case FpgAimIndicatorBaseState.Reloading:
                    PopulateReload(vertexHelper, center);
                    break;
                case FpgAimIndicatorBaseState.CurrentCoverBlocked:
                    PopulateProhibited(vertexHelper, center);
                    break;
                case FpgAimIndicatorBaseState.Unavailable:
                    PopulateCrosshair(vertexHelper, center, unavailableColor);
                    break;
                case FpgAimIndicatorBaseState.Enemy:
                    PopulateCrosshair(vertexHelper, center, enemyColor);
                    break;
                default:
                    PopulateCrosshair(vertexHelper, center, normalColor);
                    break;
            }

            PopulateShot(vertexHelper, center);
            PopulateHit(vertexHelper, center);
        }

        private void PopulateLegacyMesh(VertexHelper vertexHelper, Vector2 center)
        {
            if (ringRadius <= 0f || ringThickness <= 0f)
            {
                return;
            }

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
            PopulateHit(vertexHelper, center);
        }

        private void PopulateCrosshair(
            VertexHelper vertexHelper,
            Vector2 center,
            Color crosshairColor)
        {
            float inner = crosshairGap;
            float outer = crosshairGap + crosshairArmLength;
            AddLine(vertexHelper, center + Vector2.left * inner,
                center + Vector2.left * outer, crosshairThickness, crosshairColor);
            AddLine(vertexHelper, center + Vector2.right * inner,
                center + Vector2.right * outer, crosshairThickness, crosshairColor);
            AddLine(vertexHelper, center + Vector2.down * inner,
                center + Vector2.down * outer, crosshairThickness, crosshairColor);
            AddLine(vertexHelper, center + Vector2.up * inner,
                center + Vector2.up * outer, crosshairThickness, crosshairColor);
        }

        private void PopulateProhibited(
            VertexHelper vertexHelper,
            Vector2 center)
        {
            AddRingSegment(
                vertexHelper,
                center,
                prohibitedRadius,
                prohibitedThickness,
                0f,
                360f,
                RingSegments,
                currentCoverBlockedColor);
            float diagonal = prohibitedRadius * 0.70f;
            AddLine(
                vertexHelper,
                center + new Vector2(-diagonal, diagonal),
                center + new Vector2(diagonal, -diagonal),
                prohibitedThickness,
                currentCoverBlockedColor);
        }

        private void PopulateReload(VertexHelper vertexHelper, Vector2 center)
        {
            Color background = reloadColor;
            background.a *= 0.18f;
            AddRingSegment(
                vertexHelper,
                center,
                reloadRadius,
                reloadThickness,
                0f,
                360f,
                RingSegments,
                background);
            AddProgressArc(
                vertexHelper,
                center,
                reloadRadius,
                reloadThickness,
                reloadProgress,
                -90f,
                reloadColor);

            Color sweep = reloadColor;
            sweep.a *= 0.42f;
            float sweepStart = reloadLoopPhase * 360f - 90f;
            AddRingSegment(
                vertexHelper,
                center,
                reloadRadius + reloadThickness * 1.25f,
                reloadThickness * 0.65f,
                sweepStart,
                sweepStart + 28f,
                6,
                sweep);
        }

        private void PopulateShot(VertexHelper vertexHelper, Vector2 center)
        {
            if (shotAlpha <= 0.001f)
            {
                return;
            }

            Color pulseColor = shotColor;
            pulseColor.a *= shotAlpha;
            float radius = Mathf.Lerp(
                ringRadius,
                shotRadius,
                SmoothStep01(shotProgress));
            AddRingSegment(
                vertexHelper,
                center,
                radius,
                ringThickness,
                0f,
                360f,
                RingSegments,
                pulseColor);
        }

        private void PopulateHit(VertexHelper vertexHelper, Vector2 center)
        {
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

        private static void AddProgressArc(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            float thickness,
            float progress,
            float startDegrees,
            Color colorValue)
        {
            progress = Mathf.Clamp01(progress);
            if (progress <= 0.0001f)
            {
                return;
            }

            int segments = Mathf.Max(
                MinimumArcSegments,
                Mathf.CeilToInt(RingSegments * progress));
            AddRingSegment(
                vertexHelper,
                center,
                radius,
                thickness,
                startDegrees,
                startDegrees + 360f * progress,
                segments,
                colorValue);
        }

        private static void AddLine(
            VertexHelper vertexHelper,
            Vector2 start,
            Vector2 end,
            float thickness,
            Color colorValue)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude <= 0.000001f || thickness <= 0f)
            {
                return;
            }

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized
                * (thickness * 0.5f);
            int firstVertex = vertexHelper.currentVertCount;
            Color32 vertexColor = colorValue;
            vertexHelper.AddVert(start - normal, vertexColor, Vector2.zero);
            vertexHelper.AddVert(start + normal, vertexColor, Vector2.zero);
            vertexHelper.AddVert(end + normal, vertexColor, Vector2.zero);
            vertexHelper.AddVert(end - normal, vertexColor, Vector2.zero);
            vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
            vertexHelper.AddTriangle(firstVertex, firstVertex + 2, firstVertex + 3);
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

        private static float ClampFinite01(float value)
        {
            return IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        private static float ClampFiniteNonNegative(float value)
        {
            return IsFinite(value) ? Mathf.Max(0f, value) : 0f;
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
