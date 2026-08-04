using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Allocation-free viewport math shared by the free reticle and its tests.
    /// The values deliberately live in normalized viewport space so 16:9 UI,
    /// the camera ray and input handling retain one unambiguous coordinate system.
    /// </summary>
    public static class CombatAimViewportMath
    {
        public const float SafeMinimumX = 0.08f;
        public const float SafeMaximumX = 0.92f;
        public const float SafeMinimumY = 0.12f;
        public const float SafeMaximumY = 0.88f;

        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        public static readonly Rect DefaultSafeArea = new Rect(
            SafeMinimumX,
            SafeMinimumY,
            SafeMaximumX - SafeMinimumX,
            SafeMaximumY - SafeMinimumY);

        public static Vector2 ClampToSafeArea(Vector2 viewport)
        {
            return ClampToSafeArea(viewport, DefaultSafeArea);
        }

        public static Vector2 ClampToSafeArea(Vector2 viewport, Rect safeArea)
        {
            if (!IsFinite(viewport))
            {
                return Center;
            }

            if (!IsValidSafeArea(safeArea))
            {
                safeArea = DefaultSafeArea;
            }

            return new Vector2(
                Mathf.Clamp(viewport.x, safeArea.xMin, safeArea.xMax),
                Mathf.Clamp(viewport.y, safeArea.yMin, safeArea.yMax));
        }

        public static Vector2 ApplyMouseDelta(
            Vector2 currentViewport,
            Vector2 mouseDeltaPixels,
            Vector2 screenSizePixels,
            float sensitivity)
        {
            return ApplyMouseDelta(
                currentViewport,
                mouseDeltaPixels,
                screenSizePixels,
                sensitivity,
                DefaultSafeArea);
        }

        public static Vector2 ApplyMouseDelta(
            Vector2 currentViewport,
            Vector2 mouseDeltaPixels,
            Vector2 screenSizePixels,
            float sensitivity,
            Rect safeArea)
        {
            Vector2 safeCurrent = ClampToSafeArea(currentViewport, safeArea);
            if (!IsFinite(mouseDeltaPixels)
                || !IsFinite(screenSizePixels)
                || !IsFinite(sensitivity)
                || screenSizePixels.x <= 0f
                || screenSizePixels.y <= 0f
                || sensitivity <= 0f)
            {
                return safeCurrent;
            }

            Vector2 viewportDelta = new Vector2(
                mouseDeltaPixels.x / screenSizePixels.x,
                mouseDeltaPixels.y / screenSizePixels.y) * sensitivity;
            return ClampToSafeArea(safeCurrent + viewportDelta, safeArea);
        }

        public static Vector2 ApplyGamepadInput(
            Vector2 currentViewport,
            Vector2 stick,
            float maximumViewportSpeed,
            float radialDeadzone,
            float responseExponent,
            float deltaTime)
        {
            return ApplyGamepadInput(
                currentViewport,
                stick,
                maximumViewportSpeed,
                radialDeadzone,
                responseExponent,
                deltaTime,
                DefaultSafeArea);
        }

        public static Vector2 ApplyGamepadInput(
            Vector2 currentViewport,
            Vector2 stick,
            float maximumViewportSpeed,
            float radialDeadzone,
            float responseExponent,
            float deltaTime,
            Rect safeArea)
        {
            Vector2 safeCurrent = ClampToSafeArea(currentViewport, safeArea);
            if (!IsFinite(stick) || !IsFinite(maximumViewportSpeed)
                || !IsFinite(radialDeadzone) || !IsFinite(responseExponent)
                || !IsFinite(deltaTime) || maximumViewportSpeed <= 0f
                || radialDeadzone < 0f || radialDeadzone >= 1f
                || responseExponent <= 0f || deltaTime <= 0f)
            {
                return safeCurrent;
            }

            float magnitude = Mathf.Clamp01(stick.magnitude);
            if (magnitude <= radialDeadzone)
            {
                return safeCurrent;
            }

            float normalizedMagnitude = (magnitude - radialDeadzone)
                / (1f - radialDeadzone);
            float response = Mathf.Pow(
                normalizedMagnitude,
                responseExponent);
            Vector2 viewportDelta = stick.normalized
                * (response * maximumViewportSpeed * deltaTime);
            return ClampToSafeArea(safeCurrent + viewportDelta, safeArea);
        }

        public static Vector2 ApplyScreenPoint(
            Vector2 currentViewport,
            Vector2 screenPointPixels,
            Vector2 screenSizePixels,
            Rect safeArea)
        {
            Vector2 safeCurrent = ClampToSafeArea(currentViewport, safeArea);
            if (!IsFinite(screenPointPixels)
                || !IsFinite(screenSizePixels)
                || screenSizePixels.x <= 0f
                || screenSizePixels.y <= 0f)
            {
                return safeCurrent;
            }

            return ClampToSafeArea(
                new Vector2(
                    screenPointPixels.x / screenSizePixels.x,
                    screenPointPixels.y / screenSizePixels.y),
                safeArea);
        }

        public static bool IsInsideSafeArea(Vector2 viewport)
        {
            return IsInsideSafeArea(viewport, DefaultSafeArea);
        }

        public static bool IsInsideSafeArea(Vector2 viewport, Rect safeArea)
        {
            return IsFinite(viewport)
                && IsValidSafeArea(safeArea)
                && viewport.x >= safeArea.xMin
                && viewport.x <= safeArea.xMax
                && viewport.y >= safeArea.yMin
                && viewport.y <= safeArea.yMax;
        }

        public static bool IsValidSafeArea(Rect safeArea)
        {
            return IsFinite(safeArea.x)
                && IsFinite(safeArea.y)
                && IsFinite(safeArea.width)
                && IsFinite(safeArea.height)
                && safeArea.x >= 0f
                && safeArea.y >= 0f
                && safeArea.width > 0f
                && safeArea.height > 0f
                && safeArea.xMax <= 1f
                && safeArea.yMax <= 1f;
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
