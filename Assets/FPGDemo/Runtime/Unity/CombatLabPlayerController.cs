using UnityEngine;
using UnityEngine.InputSystem;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Thin scene-facing controller for the CombatLab player anchor. It owns only
    /// local locomotion (when explicitly enabled) and look presentation; battle input remains owned by
    /// <see cref="UnityBattleInputSource"/> in <see cref="BattleSessionHost"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class CombatLabPlayerController : MonoBehaviour
    {
        [Header("Entity prefab references")]
        [SerializeField]
        private CharacterController characterController;

        [SerializeField]
        private Transform aimAnchor;

        [SerializeField]
        private Transform cameraPivot;

        private BattleSessionHost sessionHost;

        [Header("Locomotion")]
        [SerializeField]
        private bool planarMovementEnabled = false;

        [SerializeField, Min(0f)]
        private float moveSpeed = 5.5f;

        [SerializeField]
        private float gravity = -24f;

        [SerializeField]
        private float groundedVerticalSpeed = -2f;

        [Header("Look")]
        [SerializeField, Min(0f)]
        private float mouseSensitivity = 0.12f;

        [SerializeField, Range(-89f, 0f)]
        private float minimumPitch = -70f;

        [SerializeField, Range(0f, 89f)]
        private float maximumPitch = 70f;

        [SerializeField]
        private bool lockCursorOnFocus = true;

        [SerializeField]
        private bool resetToInitialSpawnWhenSessionChanges = true;

        [Header("2.5D presentation")]
        [SerializeField]
        private bool twoPointFiveDPresentationMode = true;

        [Header("Camera collision")]
        [SerializeField, Min(0.01f)]
        private float cameraCollisionRadius = 0.2f;

        [SerializeField, Min(0f)]
        private float cameraCollisionPadding = 0.08f;

        [SerializeField]
        private LayerMask cameraCollisionLayerMask = Physics.DefaultRaycastLayers;

        private Vector3 initialPosition;
        private Quaternion initialRootRotation;
        private Quaternion initialAimLocalRotation;
        private Quaternion initialCameraPivotLocalRotation;
        private Vector3 initialCameraPivotLocalPosition;
        private Vector3 initialAimLocalEuler;
        // Reused by the third-person boom sweep so camera obstruction handling
        // remains allocation-free during play.
        private readonly RaycastHit[] cameraCollisionHitBuffer = new RaycastHit[8];
        private float pitch;
        private float verticalVelocity;
        private BattleSessionHost observedHost;
        private object observedSession;
        private bool hasInitialSpawn;
        private bool hasInitialAimRotation;
        private bool hasInitialCameraPivotRotation;
        private bool hasInitialCameraPivotPosition;
        private bool hasObservedSession;
        private bool isInitialized;
        private bool isCursorLocked;
        private BattleSessionHost subscribedHost;

        public CharacterController CharacterController => characterController;

        public Transform AimAnchor => aimAnchor;

        public Transform CameraPivot => cameraPivot;

        public BattleSessionHost SessionHost => sessionHost;

        public bool IsSceneServicesBound => sessionHost != null;

        /// <summary>
        /// Whether debug planar locomotion is enabled. Formal CombatLab playtests
        /// keep this disabled while preserving gravity, grounding, look and restart.
        /// </summary>
        public bool PlanarMovementEnabled => planarMovementEnabled;

        public bool IsInitialized => isInitialized;

        public bool IsCursorLocked => isCursorLocked;

        /// <summary>
        /// The D0 slice keeps a fixed frontal camera. In this mode free aim is
        /// owned by CombatAimReticle rather than rotating the player or camera.
        /// </summary>
        public bool UsesTwoPointFiveDPresentation => twoPointFiveDPresentationMode;

        public string LastError { get; private set; } = string.Empty;

        private void Start()
        {
            if (!TryInitialize(out string error))
            {
                Debug.LogError($"[{nameof(CombatLabPlayerController)}] {error}", this);
            }
        }

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            SynchronizeSessionReset();
            if (sessionHost != null && !sessionHost.IsSessionRunning)
            {
                if (!twoPointFiveDPresentationMode && isCursorLocked)
                {
                    SetCursorLocked(false);
                }

                return;
            }

            if (twoPointFiveDPresentationMode)
            {
                // The fixed 2.5D composition deliberately has no player-root
                // rotation, shoulder pitch, boom collision or planar movement.
                // CombatAimReticle samples mouse delta before BattleSessionHost.
                return;
            }

            if (lockCursorOnFocus && Application.isFocused && !isCursorLocked)
            {
                SetCursorLocked(true);
            }

            UpdateLook();
            UpdateMovement();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                // No held-input cache is retained by this component. Clearing the
                // vertical integration and releasing the cursor is sufficient to
                // prevent a stale focus frame from affecting the next update.
                verticalVelocity = 0f;
                SetCursorLocked(false);
                return;
            }

            if (!twoPointFiveDPresentationMode
                && isInitialized
                && lockCursorOnFocus
                && (sessionHost == null || sessionHost.IsSessionRunning))
            {
                SetCursorLocked(true);
            }
        }

        private void OnEnable()
        {
            if (isInitialized)
            {
                SubscribeSessionRestart();
            }
        }

        private void OnDisable()
        {
            UnsubscribeSessionRestart();
            if (!twoPointFiveDPresentationMode)
            {
                SetCursorLocked(false);
            }
        }

        private void LateUpdate()
        {
            if (isInitialized && !twoPointFiveDPresentationMode)
            {
                UpdateCameraBoomCollision();
            }
        }

        private void OnDestroy()
        {
            UnbindSceneServices();
        }

        /// <summary>
        /// Injects scene-owned services after the complete player entity is placed.
        /// The Entity Prefab must not persist this reference.
        /// </summary>
        public bool TryBindSceneServices(BattleSessionHost nextSessionHost, out string error)
        {
            if (nextSessionHost == null)
            {
                error = "CombatLab player scene services require a BattleSessionHost.";
                LastError = error;
                return false;
            }

            if (sessionHost != nextSessionHost)
            {
                UnsubscribeSessionRestart();
                sessionHost = nextSessionHost;
            }

            ObserveCurrentSession();
            if (isActiveAndEnabled)
            {
                SubscribeSessionRestart();
            }

            LastError = string.Empty;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Releases every scene-owned dependency while preserving the authored
        /// player hierarchy and the captured prefab-local pose.
        /// </summary>
        public void UnbindSceneServices()
        {
            UnsubscribeSessionRestart();
            sessionHost = null;
            observedHost = null;
            observedSession = null;
            hasObservedSession = false;
            verticalVelocity = 0f;
            if (!twoPointFiveDPresentationMode && isCursorLocked)
            {
                SetCursorLocked(false);
            }
        }

        /// <summary>
        /// Validates explicit scene references and records the spawn pose used by
        /// safe session restarts. This is public so a scene installer can validate
        /// the component after assigning its serialized references.
        /// </summary>
        public bool TryInitialize(out string error)
        {
            if (isInitialized)
            {
                error = string.Empty;
                return true;
            }

            if (!TryValidateConfiguration(out error))
            {
                LastError = error;
                return false;
            }

            if (!hasInitialSpawn
                || !hasInitialAimRotation
                || !hasInitialCameraPivotRotation
                || !hasInitialCameraPivotPosition)
            {
                CaptureInitialSpawn();
            }

            pitch = ClampPitch(NormalizeSignedAngle(initialAimLocalEuler.x));
            ApplyPitch();
            verticalVelocity = 0f;
            isInitialized = true;
            LastError = string.Empty;
            ObserveCurrentSession();
            SubscribeSessionRestart();
            if (!twoPointFiveDPresentationMode && lockCursorOnFocus && Application.isFocused)
            {
                SetCursorLocked(true);
            }

            return true;
        }

        /// <summary>
        /// Exposes the scene-reference validation needed by the D0 runtime
        /// preview bridge without exposing the controller's serialized state.
        /// </summary>
        public bool TryValidateConfigurationForD0Preview(out string error)
        {
            return TryValidateConfiguration(out error);
        }


        /// <summary>
        /// Applies the authored fixed 2.5D camera pose and replaces the
        /// restart baseline used by this controller. It deliberately does not
        /// recapture the player root or aim anchor.
        /// </summary>
        public bool TryApplyTwoPointFiveDCameraProfile(
            D0ThreeCProfile profile,
            Camera targetCamera,
            out string error)
        {
            if (profile == null)
            {
                error = "D0 camera preview requires a D0 3C profile.";
                return false;
            }

            if (!profile.TryValidate(out error)
                || !TryValidateConfiguration(out error))
            {
                return false;
            }

            if (targetCamera == null)
            {
                error = "D0 camera preview requires a target Camera.";
                return false;
            }

            if (targetCamera.transform.parent != cameraPivot)
            {
                error = "D0 camera preview requires Main Camera to be parented to CameraPivot.";
                return false;
            }

            if (!twoPointFiveDPresentationMode)
            {
                error = "D0 camera preview requires 2.5D presentation mode.";
                return false;
            }

            Quaternion pivotRotation =
                Quaternion.Euler(profile.CameraPivotLocalEulerAngles);
            initialCameraPivotLocalPosition = profile.CameraPivotLocalPosition;
            initialCameraPivotLocalRotation = pivotRotation;
            hasInitialCameraPivotPosition = true;
            hasInitialCameraPivotRotation = true;

            cameraPivot.localPosition = profile.CameraPivotLocalPosition;
            cameraPivot.localRotation = pivotRotation;
            targetCamera.transform.localPosition = profile.CameraLocalPosition;
            targetCamera.transform.localRotation =
                Quaternion.Euler(profile.CameraLocalEulerAngles);
            targetCamera.fieldOfView = profile.CameraFieldOfView;
            targetCamera.nearClipPlane = profile.CameraNearClipPlane;
            targetCamera.farClipPlane = profile.CameraFarClipPlane;
            LastError = string.Empty;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Captures the current root and AimAnchor pose as the restart spawn pose.
        /// Call this after positioning a dynamically-instantiated player.
        /// </summary>
        public void CaptureInitialSpawn()
        {
            initialPosition = transform.position;
            initialRootRotation = transform.rotation;
            hasInitialSpawn = true;

            if (aimAnchor == null)
            {
                hasInitialAimRotation = false;
                hasInitialCameraPivotRotation = false;
                hasInitialCameraPivotPosition = false;
                return;
            }

            if (cameraPivot == null)
            {
                hasInitialCameraPivotRotation = false;
                hasInitialCameraPivotPosition = false;
                return;
            }

            initialAimLocalRotation = aimAnchor.localRotation;
            initialAimLocalEuler = aimAnchor.localEulerAngles;
            pitch = ClampPitch(NormalizeSignedAngle(initialAimLocalEuler.x));
            hasInitialAimRotation = true;

            initialCameraPivotLocalRotation = cameraPivot.localRotation;
            hasInitialCameraPivotRotation = true;
            initialCameraPivotLocalPosition = cameraPivot.localPosition;
            hasInitialCameraPivotPosition = true;
        }

        /// <summary>
        /// Restores the captured initial spawn without depending on collision
        /// resolution. Returns false rather than moving partially when the scene
        /// wiring is invalid.
        /// </summary>
        public bool TryResetToInitialSpawn(out string error)
        {
            if (!TryValidateConfiguration(out error))
            {
                LastError = error;
                return false;
            }

            if (!hasInitialSpawn
                || !hasInitialAimRotation
                || !hasInitialCameraPivotRotation
                || !hasInitialCameraPivotPosition)
            {
                error = "Initial spawn has not been captured.";
                LastError = error;
                return false;
            }

            bool controllerWasEnabled = characterController.enabled;
            characterController.enabled = false;
            transform.SetPositionAndRotation(initialPosition, initialRootRotation);
            cameraPivot.localPosition = initialCameraPivotLocalPosition;
            pitch = ClampPitch(NormalizeSignedAngle(initialAimLocalEuler.x));
            ApplyPitch();
            verticalVelocity = 0f;
            characterController.enabled = controllerWasEnabled;
            LastError = string.Empty;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Explicit UI/installer entry point for a complete restart. Keyboard F5
        /// remains owned by BattleSessionHost, whose successful-restart event
        /// resets this controller synchronously.
        /// </summary>
        public bool TryRestartSessionAndResetPlayer(out string error)
        {
            if (!TryInitialize(out error))
            {
                return false;
            }

            if (sessionHost == null)
            {
                error = "BattleSessionHost is required to restart the session.";
                LastError = error;
                return false;
            }

            if (!sessionHost.TryRestart().IsSuccess)
            {
                error = string.IsNullOrEmpty(sessionHost.LastError)
                    ? "BattleSessionHost rejected restart."
                    : sessionHost.LastError;
                LastError = error;
                return false;
            }

            error = LastError;
            return string.IsNullOrEmpty(error);
        }

        /// <summary>
        /// Allows pause/UI code to release or reacquire the cursor without using a
        /// global lookup. Looking is inactive while the cursor is unlocked.
        /// </summary>
        public void SetCursorLocked(bool locked)
        {
            isCursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
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

            if (aimAnchor == null)
            {
                error = "AimAnchor reference is required.";
                return false;
            }

            if (!aimAnchor.IsChildOf(transform))
            {
                error = "AimAnchor must be parented below the player root.";
                return false;
            }

            if (cameraPivot == null)
            {
                error = "CameraPivot reference is required.";
                return false;
            }

            if (cameraPivot.parent != transform)
            {
                error = "CameraPivot must be a direct child of the player root.";
                return false;
            }

            if (moveSpeed < 0f)
            {
                error = "Move speed must be non-negative.";
                return false;
            }

            if (gravity >= 0f)
            {
                error = "Gravity must be negative.";
                return false;
            }

            if (groundedVerticalSpeed > 0f)
            {
                error = "Grounded vertical speed must be zero or negative.";
                return false;
            }

            if (mouseSensitivity < 0f)
            {
                error = "Mouse sensitivity must be non-negative.";
                return false;
            }

            if (minimumPitch > maximumPitch)
            {
                error = "Minimum pitch must not exceed maximum pitch.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void SynchronizeSessionReset()
        {
            SubscribeSessionRestart();
            if (sessionHost == null)
            {
                hasObservedSession = false;
                observedHost = null;
                observedSession = null;
                return;
            }

            if (observedHost != sessionHost)
            {
                observedHost = sessionHost;
                hasObservedSession = false;
                observedSession = null;
            }

            if (!sessionHost.IsInitialized || sessionHost.Session == null)
            {
                hasObservedSession = false;
                observedSession = null;
                return;
            }

            object currentSession = sessionHost.Session;
            if (!hasObservedSession)
            {
                observedSession = currentSession;
                hasObservedSession = true;
                return;
            }

            if (ReferenceEquals(observedSession, currentSession))
            {
                return;
            }

            observedSession = currentSession;
            if (resetToInitialSpawnWhenSessionChanges
                && !TryResetToInitialSpawn(out string error))
            {
                LastError = error;
                Debug.LogError($"[{nameof(CombatLabPlayerController)}] {error}", this);
            }
        }

        private void ObserveCurrentSession()
        {
            observedHost = sessionHost;
            observedSession = sessionHost != null && sessionHost.IsInitialized
                ? sessionHost.Session
                : null;
            hasObservedSession = observedSession != null;
        }

        private void SubscribeSessionRestart()
        {
            if (subscribedHost == sessionHost)
            {
                return;
            }

            UnsubscribeSessionRestart();
            if (sessionHost != null)
            {
                sessionHost.SessionRestarted += HandleSessionRestarted;
                subscribedHost = sessionHost;
            }
        }

        private void UnsubscribeSessionRestart()
        {
            if (subscribedHost != null)
            {
                subscribedHost.SessionRestarted -= HandleSessionRestarted;
                subscribedHost = null;
            }
        }

        private void HandleSessionRestarted(BattleSessionHost source)
        {
            if (!isInitialized || source != sessionHost)
            {
                return;
            }

            ObserveCurrentSession();
            if (resetToInitialSpawnWhenSessionChanges
                && !TryResetToInitialSpawn(out string error))
            {
                LastError = error;
                Debug.LogError($"[{nameof(CombatLabPlayerController)}] {error}", this);
            }
        }

        private void UpdateLook()
        {
            if (!isCursorLocked || Mouse.current == null)
            {
                return;
            }

            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            if (mouseDelta.sqrMagnitude <= 0f)
            {
                return;
            }

            transform.Rotate(Vector3.up, mouseDelta.x * mouseSensitivity, Space.World);
            pitch = ClampPitch(pitch - mouseDelta.y * mouseSensitivity);
            ApplyPitch();
        }

        private void UpdateMovement()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector2 movementInput = planarMovementEnabled
                ? ReadPlanarMovement()
                : Vector2.zero;
            Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (planarForward.sqrMagnitude <= 0.0001f)
            {
                planarForward = Vector3.forward;
            }

            planarForward.Normalize();
            Vector3 planarRight = Vector3.Cross(Vector3.up, planarForward);
            Vector3 planarVelocity = (planarForward * movementInput.y
                + planarRight * movementInput.x) * moveSpeed;

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = groundedVerticalSpeed;
            }
            else
            {
                verticalVelocity += gravity * deltaTime;
            }

            Vector3 displacement = (planarVelocity + Vector3.up * verticalVelocity) * deltaTime;
            characterController.Move(displacement);
        }

        /// <summary>
        /// Sweeps from the stable gameplay aim anchor to the authored shoulder
        /// position. Only the presentation pivot is shortened: combat aim keeps
        /// its own chest-height origin and never inherits this correction.
        /// </summary>
        private void UpdateCameraBoomCollision()
        {
            if (cameraPivot == null
                || aimAnchor == null
                || !hasInitialCameraPivotPosition)
            {
                return;
            }

            Vector3 boomOrigin = aimAnchor.position;
            Vector3 desiredWorldPosition = transform.TransformPoint(initialCameraPivotLocalPosition);
            Vector3 boomOffset = desiredWorldPosition - boomOrigin;
            float desiredDistance = boomOffset.magnitude;
            if (desiredDistance <= 0.0001f)
            {
                SetCameraPivotLocalPosition(initialCameraPivotLocalPosition);
                return;
            }

            Vector3 boomDirection = boomOffset / desiredDistance;
            float resolvedDistance = desiredDistance;
            bool hasBlockingHit = false;
            if (cameraCollisionRadius > 0f && cameraCollisionLayerMask.value != 0)
            {
                int hitCount = Physics.SphereCastNonAlloc(
                    boomOrigin,
                    cameraCollisionRadius,
                    boomDirection,
                    cameraCollisionHitBuffer,
                    desiredDistance,
                    cameraCollisionLayerMask.value,
                    QueryTriggerInteraction.Ignore);
                for (int index = 0; index < hitCount; index++)
                {
                    RaycastHit hit = cameraCollisionHitBuffer[index];
                    if (hit.collider == null
                        || IsPlayerOwnedCollider(hit.collider)
                        || hit.distance >= resolvedDistance)
                    {
                        continue;
                    }

                    resolvedDistance = hit.distance;
                    hasBlockingHit = true;
                }
            }

            Vector3 resolvedWorldPosition = boomOrigin + boomDirection * Mathf.Max(
                0f,
                hasBlockingHit
                    ? resolvedDistance - cameraCollisionPadding
                    : desiredDistance);
            SetCameraPivotLocalPosition(transform.InverseTransformPoint(resolvedWorldPosition));
        }

        private bool IsPlayerOwnedCollider(Collider collider)
        {
            Transform colliderTransform = collider.transform;
            return colliderTransform == transform || colliderTransform.IsChildOf(transform);
        }

        private void SetCameraPivotLocalPosition(Vector3 position)
        {
            if ((cameraPivot.localPosition - position).sqrMagnitude > 0.000001f)
            {
                cameraPivot.localPosition = position;
            }
        }

        private void ApplyPitch()
        {
            if (twoPointFiveDPresentationMode)
            {
                aimAnchor.localRotation = initialAimLocalRotation;
                cameraPivot.localRotation = initialCameraPivotLocalRotation;
                return;
            }

            Vector3 aimEuler = initialAimLocalRotation.eulerAngles;
            aimEuler.x = pitch;
            aimAnchor.localRotation = Quaternion.Euler(aimEuler);
            Vector3 pivotEuler = initialCameraPivotLocalRotation.eulerAngles;
            pivotEuler.x = pitch;
            cameraPivot.localRotation = Quaternion.Euler(pivotEuler);
        }

        private static Vector2 ReadPlanarMovement()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            float horizontal = (keyboard.dKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed ? 1f : 0f);
            float vertical = (keyboard.wKey.isPressed ? 1f : 0f)
                - (keyboard.sKey.isPressed ? 1f : 0f);
            Vector2 input = new Vector2(horizontal, vertical);
            return input.sqrMagnitude > 1f ? input.normalized : input;
        }

        private float ClampPitch(float value)
        {
            return Mathf.Clamp(value, minimumPitch, maximumPitch);
        }

        private static float NormalizeSignedAngle(float value)
        {
            return Mathf.Repeat(value + 180f, 360f) - 180f;
        }
    }
}
