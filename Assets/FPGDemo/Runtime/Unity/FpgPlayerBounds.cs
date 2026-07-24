using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Keeps the greybox player inside the authored walkable area without adding
    /// invisible world colliders. It runs after the locomotion controller so a
    /// frame that would leave the arena is corrected before presentation reads it.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FpgPlayerBounds : MonoBehaviour
    {
        [Header("Required scene references")]
        [SerializeField]
        private CharacterController characterController;

        [Header("Playable area")]
        [SerializeField]
        private Vector2 minimumPlanarPosition = new Vector2(-10.5f, -5.5f);

        [SerializeField]
        private Vector2 maximumPlanarPosition = new Vector2(10.5f, 20.5f);

        [SerializeField]
        private float fallResetHeight = -4f;

        [Header("Restart-safe fallback")]
        [SerializeField]
        private Vector3 initialSafePosition;

        [SerializeField]
        private bool hasInitialSafePosition;

        private bool initialized;

        public CharacterController CharacterController => characterController;
        public Vector2 MinimumPlanarPosition => minimumPlanarPosition;
        public Vector2 MaximumPlanarPosition => maximumPlanarPosition;
        public float FallResetHeight => fallResetHeight;
        public Vector3 InitialSafePosition => initialSafePosition;
        public bool HasInitialSafePosition => hasInitialSafePosition;
        public bool IsInitialized => initialized;
        public int BoundaryClampCount { get; private set; }
        public int FallResetCount { get; private set; }
        public string LastError { get; private set; }

        private void Awake()
        {
            if (!TryInitialize(out string error))
            {
                LastError = error;
                Debug.LogError($"[{nameof(FpgPlayerBounds)}] {error}", this);
            }
        }

        private void LateUpdate()
        {
            if (!initialized && !TryInitialize(out string error))
            {
                LastError = error;
                return;
            }

            if (!TryEnforceBounds(out _, out error))
            {
                LastError = error;
            }
        }

        /// <summary>
        /// Captures the current player root as the safe location used after an
        /// out-of-area fall. Installers should call this after placing the player.
        /// </summary>
        public bool CaptureInitialSafePosition(out string error)
        {
            if (!TryInitialize(out error))
            {
                return false;
            }

            Vector3 currentPosition = transform.position;
            if (!IsFinite(currentPosition))
            {
                error = "Player position must be finite before it can be captured as the safe position.";
                LastError = error;
                return false;
            }

            initialSafePosition = ClampPlanar(currentPosition);
            hasInitialSafePosition = true;
            error = string.Empty;
            LastError = string.Empty;
            return true;
        }

        /// <summary>
        /// Applies an immediate arena clamp or fall reset. This is public for
        /// deterministic play-mode and editor verification; normal play invokes it
        /// from <see cref="LateUpdate"/>.
        /// </summary>
        public bool TryEnforceBounds(out bool resetToSafePosition, out string error)
        {
            if (!TryInitialize(out error))
            {
                resetToSafePosition = false;
                return false;
            }

            Vector3 currentPosition = transform.position;
            resetToSafePosition = !IsFinite(currentPosition)
                || currentPosition.y <= fallResetHeight;
            Vector3 desiredPosition = resetToSafePosition
                ? initialSafePosition
                : ClampPlanar(currentPosition);

            if (!IsFinite(desiredPosition))
            {
                error = "Configured safe position must be finite.";
                LastError = error;
                return false;
            }

            bool positionChanged = resetToSafePosition
                || (desiredPosition - currentPosition).sqrMagnitude > 0.000001f;
            if (positionChanged)
            {
                SetPositionWithoutCharacterControllerResolution(desiredPosition);
                if (resetToSafePosition)
                {
                    FallResetCount++;
                }
                else
                {
                    BoundaryClampCount++;
                }
            }

            error = string.Empty;
            LastError = string.Empty;
            return true;
        }

        public bool IsInsidePlayableArea(Vector3 position)
        {
            return IsFinite(position)
                && position.x >= minimumPlanarPosition.x
                && position.x <= maximumPlanarPosition.x
                && position.z >= minimumPlanarPosition.y
                && position.z <= maximumPlanarPosition.y;
        }

        private bool TryInitialize(out string error)
        {
            if (initialized)
            {
                error = string.Empty;
                return true;
            }

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (!TryValidateConfiguration(out error))
            {
                return false;
            }

            if (!hasInitialSafePosition)
            {
                initialSafePosition = ClampPlanar(transform.position);
                hasInitialSafePosition = true;
            }

            initialized = true;
            LastError = string.Empty;
            error = string.Empty;
            return true;
        }

        private bool TryValidateConfiguration(out string error)
        {
            if (characterController == null)
            {
                error = "CharacterController reference is required.";
                return false;
            }

            if (characterController.transform != transform)
            {
                error = "CharacterController must be attached to the player root.";
                return false;
            }

            if (!IsFinite(minimumPlanarPosition) || !IsFinite(maximumPlanarPosition))
            {
                error = "Playable area limits must be finite.";
                return false;
            }

            if (minimumPlanarPosition.x >= maximumPlanarPosition.x
                || minimumPlanarPosition.y >= maximumPlanarPosition.y)
            {
                error = "Playable area minimum limits must be smaller than maximum limits.";
                return false;
            }

            if (!IsFinite(fallResetHeight))
            {
                error = "Fall reset height must be finite.";
                return false;
            }

            if (hasInitialSafePosition && !IsFinite(initialSafePosition))
            {
                error = "Configured safe position must be finite.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private Vector3 ClampPlanar(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, minimumPlanarPosition.x, maximumPlanarPosition.x);
            position.z = Mathf.Clamp(position.z, minimumPlanarPosition.y, maximumPlanarPosition.y);
            return position;
        }

        private void SetPositionWithoutCharacterControllerResolution(Vector3 position)
        {
            bool controllerWasEnabled = characterController.enabled;
            if (controllerWasEnabled)
            {
                characterController.enabled = false;
            }

            transform.position = position;

            if (controllerWasEnabled)
            {
                characterController.enabled = true;
            }
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
