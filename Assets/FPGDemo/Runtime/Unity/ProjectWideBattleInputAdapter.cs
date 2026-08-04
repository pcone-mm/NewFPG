using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace FPG.Demo.Unity
{
    internal enum ProjectWideAimInputChannel
    {
        None = 0,
        Mouse,
        Gamepad
    }

    internal readonly struct ProjectWideAimInputSnapshot
    {
        public ProjectWideAimInputSnapshot(
            bool aimHeld,
            Vector2 lookDelta,
            Vector2 point,
            bool hasPoint,
            ProjectWideAimInputChannel inputChannel =
                ProjectWideAimInputChannel.Mouse)
        {
            AimHeld = aimHeld;
            LookDelta = lookDelta;
            Point = point;
            HasPoint = hasPoint;
            InputChannel = inputChannel;
        }

        public bool AimHeld { get; }
        public Vector2 LookDelta { get; }
        public Vector2 Point { get; }
        public bool HasPoint { get; }
        public ProjectWideAimInputChannel InputChannel { get; }
    }

    /// <summary>
    /// Reads the project-wide Battle action map and forwards its exact snapshot
    /// to the existing deterministic input source. This bridge never enables or
    /// disables project-wide actions. It can subscribe to Pause.performed only
    /// to forward one early pause edge before MonoBehaviour Update.
    /// </summary>
    internal readonly struct ProjectWideAttackPressSnapshot
    {
        public ProjectWideAttackPressSnapshot(
            bool primaryPressed,
            bool secondaryPressed)
        {
            PrimaryPressed = primaryPressed;
            SecondaryPressed = secondaryPressed;
        }

        public bool PrimaryPressed { get; }
        public bool SecondaryPressed { get; }
        public bool AnyAttackPressed => PrimaryPressed || SecondaryPressed;
    }

    internal sealed class ProjectWideBattleInputAdapter : IDisposable
    {
        private const float MinimumAimInputSquared = 0.0000001f;
        private const string AimActionPath = "Battle/Aim";
        private const string PrimaryActionPath = "Battle/Primary";
        private const string SecondaryActionPath = "Battle/Secondary";
        private const string ReloadActionPath = "Battle/Reload";
        private const string PauseActionPath = "Battle/Pause";
        private const string RestartActionPath = "Battle/Restart";
        private const string LookActionPath = "Player/Look";
        private const string PointActionPath = "UI/Point";
        private const string MoveActionPath = "Player/Move";

        private InputActionAsset actionAsset;
        private InputAction aimAction;
        private InputAction primaryAction;
        private InputAction secondaryAction;
        private InputAction reloadAction;
        private InputAction pauseAction;
        private InputAction restartAction;
        private InputAction lookAction;
        private InputAction pointAction;
        private InputAction moveAction;
        private InputAction subscribedPauseAction;
        private Func<bool> earlyPausePressedHandler;
        private int lastForwardedPauseFrame = -1;
        private ProjectWideAimInputChannel lastAimInputChannel =
            ProjectWideAimInputChannel.Mouse;
        private ulong aimInputEventSequence;
        private ulong lastMouseAimEventSequence;
        private ulong lastGamepadAimEventSequence;
        private bool aimInputEventsSubscribed;

        /// <summary>
        /// Registers the host-owned pause handler used by the Input System's
        /// dynamic update. A true result makes the host skip normal input
        /// capture for that rendered control frame, preventing a double toggle.
        /// </summary>
        public void SetEarlyPausePressedHandler(Func<bool> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (earlyPausePressedHandler == handler)
            {
                return;
            }

            UnsubscribePausePerformed();
            earlyPausePressedHandler = handler;
            lastForwardedPauseFrame = -1;
            TryResolveActions();
        }

        /// <summary>
        /// Removes the host callback without changing the project-wide action
        /// map's enabled state. Hosts call this during shutdown to avoid a stale
        /// scene object receiving an Input System callback.
        /// </summary>
        public void ClearEarlyPausePressedHandler()
        {
            UnsubscribePausePerformed();
            earlyPausePressedHandler = null;
            lastForwardedPauseFrame = -1;
        }

        /// <summary>
        /// Captures one rendered-frame snapshot when the configured project-wide
        /// Battle map is available. A false result lets callers preserve their
        /// legacy device-polling fallback for projects without that map.
        /// </summary>
        public bool TryCapture(UnityBattleInputSource inputSource)
        {
            if (inputSource == null)
            {
                throw new ArgumentNullException(nameof(inputSource));
            }

            if (!TryResolveActions())
            {
                return false;
            }

            Vector2 move = moveAction.ReadValue<Vector2>();
            FPG.Demo.Run.FpgCoverMoveDirection coverMoveDirection =
                !moveAction.WasPressedThisFrame() || Mathf.Abs(move.x) < 0.5f
                    ? FPG.Demo.Run.FpgCoverMoveDirection.None
                    : move.x < 0f
                        ? FPG.Demo.Run.FpgCoverMoveDirection.Left
                        : FPG.Demo.Run.FpgCoverMoveDirection.Right;
            inputSource.Capture(new UnityInputSnapshot(
                aimAction.IsPressed(),
                primaryAction.IsPressed(),
                secondaryAction.WasPressedThisFrame(),
                secondaryAction.WasReleasedThisFrame(),
                reloadAction.WasPressedThisFrame(),
                pauseAction.WasPressedThisFrame(),
                restartAction.WasPressedThisFrame(),
                secondaryAction.IsPressed(),
                coverMoveDirection));
            return true;
        }

        public bool TryReadAttackPresses(
            out ProjectWideAttackPressSnapshot snapshot)
        {
            snapshot = default(ProjectWideAttackPressSnapshot);
            if (!TryResolveActions())
            {
                return false;
            }

            snapshot = new ProjectWideAttackPressSnapshot(
                primaryAction.WasPressedThisFrame(),
                secondaryAction.WasPressedThisFrame());
            return true;
        }

        public bool TryReadAimInput(out ProjectWideAimInputSnapshot snapshot)
        {
            return TryReadAimInput(0f, out snapshot);
        }

        public bool TryReadAimInput(
            float gamepadDeadzone,
            out ProjectWideAimInputSnapshot snapshot)
        {
            snapshot = default(ProjectWideAimInputSnapshot);
            if (!TryResolveAimActions())
            {
                return false;
            }

            float clampedDeadzone = Mathf.Clamp(gamepadDeadzone, 0f, 0.999f);
            Vector2 mouseDelta = Mouse.current == null
                ? Vector2.zero
                : Mouse.current.delta.ReadValue();
            Vector2 gamepadLook = Gamepad.current == null
                ? Vector2.zero
                : Gamepad.current.rightStick.ReadValue();
            bool mouseValid = IsFinite(mouseDelta)
                && mouseDelta.sqrMagnitude > MinimumAimInputSquared;
            bool gamepadValid = IsFinite(gamepadLook)
                && gamepadLook.sqrMagnitude
                    > clampedDeadzone * clampedDeadzone;

            Vector2 look = Vector2.zero;
            ProjectWideAimInputChannel channel =
                ProjectWideAimInputChannel.None;
            if (mouseValid && gamepadValid)
            {
                if (lastMouseAimEventSequence
                    > lastGamepadAimEventSequence)
                {
                    channel = ProjectWideAimInputChannel.Mouse;
                    look = mouseDelta;
                }
                else if (lastGamepadAimEventSequence
                    > lastMouseAimEventSequence)
                {
                    channel = ProjectWideAimInputChannel.Gamepad;
                    look = gamepadLook;
                }
                else if (lastAimInputChannel
                    == ProjectWideAimInputChannel.Gamepad)
                {
                    channel = ProjectWideAimInputChannel.Gamepad;
                    look = gamepadLook;
                }
                else
                {
                    channel = ProjectWideAimInputChannel.Mouse;
                    look = mouseDelta;
                }
            }
            else if (mouseValid)
            {
                channel = ProjectWideAimInputChannel.Mouse;
                look = mouseDelta;
            }
            else if (gamepadValid)
            {
                channel = ProjectWideAimInputChannel.Gamepad;
                look = gamepadLook;
            }
            else
            {
                Vector2 fallback = lookAction.ReadValue<Vector2>();
                if (IsFinite(fallback)
                    && fallback.sqrMagnitude > MinimumAimInputSquared)
                {
                    channel = lookAction.activeControl?.device is Gamepad
                        ? ProjectWideAimInputChannel.Gamepad
                        : ProjectWideAimInputChannel.Mouse;
                    look = fallback;
                }
            }

            if (channel != ProjectWideAimInputChannel.None)
            {
                lastAimInputChannel = channel;
            }

            lastMouseAimEventSequence = 0UL;
            lastGamepadAimEventSequence = 0UL;

            snapshot = new ProjectWideAimInputSnapshot(
                aimAction.IsPressed(),
                look,
                pointAction.ReadValue<Vector2>(),
                pointAction.activeControl != null,
                channel);
            return true;
        }

        /// <summary>
        /// Reads only the project-wide Restart edge. The early Pause path uses
        /// this after Input System processing has completed so F5 keeps its
        /// existing priority when it arrives in the same rendered frame.
        /// </summary>
        public bool IsRestartPressedThisFrame()
        {
            return TryResolveActions() && restartAction.WasPressedThisFrame();
        }

        private bool TryResolveActions()
        {
            InputActionAsset projectWideActions = InputSystem.actions;
            if (projectWideActions == null)
            {
                ClearCachedActions();
                return false;
            }

            if (ReferenceEquals(actionAsset, projectWideActions)
                && aimAction != null
                && primaryAction != null
                && secondaryAction != null
                && reloadAction != null
                && pauseAction != null
                && restartAction != null
                && moveAction != null)
            {
                EnsurePausePerformedSubscription();
                return true;
            }

            ClearCachedActions();
            actionAsset = projectWideActions;
            aimAction = actionAsset.FindAction(AimActionPath);
            primaryAction = actionAsset.FindAction(PrimaryActionPath);
            secondaryAction = actionAsset.FindAction(SecondaryActionPath);
            reloadAction = actionAsset.FindAction(ReloadActionPath);
            pauseAction = actionAsset.FindAction(PauseActionPath);
            restartAction = actionAsset.FindAction(RestartActionPath);
            moveAction = actionAsset.FindAction(MoveActionPath);
            bool resolved = aimAction != null
                && primaryAction != null
                && secondaryAction != null
                && reloadAction != null
                && pauseAction != null
                && restartAction != null
                && moveAction != null;
            if (!resolved)
            {
                ClearCachedActions();
                return false;
            }

            EnsurePausePerformedSubscription();
            return true;
        }

        private bool TryResolveAimActions()
        {
            InputActionAsset projectWideActions = InputSystem.actions;
            if (projectWideActions == null)
            {
                ClearCachedActions();
                return false;
            }

            if (!ReferenceEquals(actionAsset, projectWideActions))
            {
                ClearCachedActions();
                actionAsset = projectWideActions;
            }

            if (aimAction == null)
            {
                aimAction = actionAsset.FindAction(AimActionPath);
            }

            if (lookAction == null)
            {
                lookAction = actionAsset.FindAction(LookActionPath);
            }

            if (pointAction == null)
            {
                pointAction = actionAsset.FindAction(PointActionPath);
            }

            bool resolved = aimAction != null
                && lookAction != null
                && pointAction != null;
            if (resolved)
            {
                EnsureAimInputEventSubscription();
            }

            return resolved;
        }

        private void ClearCachedActions()
        {
            UnsubscribePausePerformed();
            actionAsset = null;
            aimAction = null;
            primaryAction = null;
            secondaryAction = null;
            reloadAction = null;
            pauseAction = null;
            restartAction = null;
            lookAction = null;
            pointAction = null;
            moveAction = null;
            lastForwardedPauseFrame = -1;
            lastAimInputChannel = ProjectWideAimInputChannel.Mouse;
            lastMouseAimEventSequence = 0UL;
            lastGamepadAimEventSequence = 0UL;
            UnsubscribeAimInputEvents();
        }

        public void Dispose()
        {
            ClearCachedActions();
            earlyPausePressedHandler = null;
        }

        private void EnsureAimInputEventSubscription()
        {
            if (aimInputEventsSubscribed)
            {
                return;
            }

            InputSystem.onEvent += HandleAimInputEvent;
            aimInputEventsSubscribed = true;
        }

        private void UnsubscribeAimInputEvents()
        {
            if (!aimInputEventsSubscribed)
            {
                return;
            }

            InputSystem.onEvent -= HandleAimInputEvent;
            aimInputEventsSubscribed = false;
        }

        private void HandleAimInputEvent(
            InputEventPtr eventPtr,
            InputDevice device)
        {
            if (!eventPtr.valid || device == null)
            {
                return;
            }

            if (device is Mouse mouse
                && mouse.delta.ReadValueFromEvent(
                    eventPtr,
                    out Vector2 mouseDelta)
                && IsFinite(mouseDelta)
                && mouseDelta.sqrMagnitude > MinimumAimInputSquared)
            {
                lastMouseAimEventSequence = NextAimInputEventSequence();
                return;
            }

            if (device is Gamepad gamepad
                && gamepad.rightStick.ReadValueFromEvent(
                    eventPtr,
                    out Vector2 gamepadLook)
                && IsFinite(gamepadLook)
                && gamepadLook.sqrMagnitude > MinimumAimInputSquared)
            {
                lastGamepadAimEventSequence = NextAimInputEventSequence();
            }
        }

        private ulong NextAimInputEventSequence()
        {
            aimInputEventSequence++;
            if (aimInputEventSequence == 0UL)
            {
                aimInputEventSequence = 1UL;
            }

            return aimInputEventSequence;
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }

        private void EnsurePausePerformedSubscription()
        {
            if (earlyPausePressedHandler == null
                || pauseAction == null
                || !UsesDynamicInputUpdate())
            {
                UnsubscribePausePerformed();
                return;
            }

            if (ReferenceEquals(subscribedPauseAction, pauseAction))
            {
                return;
            }

            UnsubscribePausePerformed();
            pauseAction.performed += HandlePausePerformed;
            subscribedPauseAction = pauseAction;
        }

        private static bool UsesDynamicInputUpdate()
        {
            return InputSystem.settings != null
                && InputSystem.settings.updateMode
                    == InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        }

        private void UnsubscribePausePerformed()
        {
            if (subscribedPauseAction != null)
            {
                subscribedPauseAction.performed -= HandlePausePerformed;
                subscribedPauseAction = null;
            }
        }

        private void HandlePausePerformed(InputAction.CallbackContext callbackContext)
        {
            if (!UsesDynamicInputUpdate()
                || !ReferenceEquals(callbackContext.action, subscribedPauseAction)
                || lastForwardedPauseFrame == Time.frameCount
                // Restart already wins over Pause in BattleSessionHost.Update.
                // Preserve that ordering when both controls arrive together.
                || (restartAction != null && restartAction.WasPressedThisFrame()))
            {
                return;
            }

            Func<bool> handler = earlyPausePressedHandler;
            if (handler != null && handler())
            {
                lastForwardedPauseFrame = Time.frameCount;
            }
        }
    }
}
