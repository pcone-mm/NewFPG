using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Applies a safe viewport offset relative to the layer's authored
    /// Transform. The base position is captured in memory at runtime so the
    /// Transform remains the single authoring source of truth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class D0ForestParallaxLayer : MonoBehaviour
    {
        private Vector3 runtimeBaseLocalPosition;
        private bool hasRuntimeBaseLocalPosition;

        [SerializeField]
        private Vector2 viewportOffsetMultiplier;

        public Vector3 BaseLocalPosition => hasRuntimeBaseLocalPosition
            ? runtimeBaseLocalPosition
            : transform.localPosition;

        public Vector2 ViewportOffsetMultiplier => viewportOffsetMultiplier;

        public void Configure(Vector3 basePosition, Vector2 offsetMultiplier)
        {
            runtimeBaseLocalPosition = basePosition;
            hasRuntimeBaseLocalPosition = true;
            viewportOffsetMultiplier = offsetMultiplier;
            ResetToBasePosition();
        }

        public void ApplyViewport(Vector2 viewport)
        {
            CaptureBaseLocalPosition();
            transform.localPosition = runtimeBaseLocalPosition + ComputeOffset(
                viewport,
                viewportOffsetMultiplier);
        }

        public void ResetToBasePosition()
        {
            CaptureBaseLocalPosition();
            transform.localPosition = runtimeBaseLocalPosition;
        }

        internal void CaptureBaseLocalPosition()
        {
            if (hasRuntimeBaseLocalPosition)
            {
                return;
            }

            runtimeBaseLocalPosition = transform.localPosition;
            hasRuntimeBaseLocalPosition = true;
        }

        public static Vector3 ComputeOffset(Vector2 viewport, Vector2 multiplier)
        {
            Vector2 centered = CombatAimViewportMath.ClampToSafeArea(viewport)
                - CombatAimViewportMath.Center;
            return new Vector3(
                -centered.x * multiplier.x,
                -centered.y * multiplier.y,
                0f);
        }
    }
}
