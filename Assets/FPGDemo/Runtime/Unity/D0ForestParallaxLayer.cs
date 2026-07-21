using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Per-layer authored base position and safe viewport offset. This concrete
    /// MonoBehaviour intentionally lives in its own same-named source file so
    /// Unity serializes stable scene references across domain and scene reloads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class D0ForestParallaxLayer : MonoBehaviour
    {
        [SerializeField]
        private Vector3 baseLocalPosition;

        [SerializeField]
        private Vector2 viewportOffsetMultiplier;

        public Vector3 BaseLocalPosition => baseLocalPosition;

        public Vector2 ViewportOffsetMultiplier => viewportOffsetMultiplier;

        public void Configure(Vector3 basePosition, Vector2 offsetMultiplier)
        {
            baseLocalPosition = basePosition;
            viewportOffsetMultiplier = offsetMultiplier;
            ResetToBasePosition();
        }

        public void ApplyViewport(Vector2 viewport)
        {
            transform.localPosition = baseLocalPosition + ComputeOffset(
                viewport,
                viewportOffsetMultiplier);
        }

        public void ResetToBasePosition()
        {
            transform.localPosition = baseLocalPosition;
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
