using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Reads the project-wide Battle action map and forwards its exact snapshot
    /// to the existing deterministic input source. This bridge never enables or
    /// disables project-wide actions. It can subscribe to Pause.performed only
    /// to forward one early pause edge before MonoBehaviour Update.
    /// </summary>
    internal sealed class ProjectWideBattleInputAdapter
    {
        private const string AimActionPath = "Battle/Aim";
        private const string PrimaryActionPath = "Battle/Primary";
        private const string SecondaryActionPath = "Battle/Secondary";
        private const string ReloadActionPath = "Battle/Reload";
        private const string PauseActionPath = "Battle/Pause";
        private const string RestartActionPath = "Battle/Restart";

        private InputActionAsset actionAsset;
        private InputAction aimAction;
        private InputAction primaryAction;
        private InputAction secondaryAction;
        private InputAction reloadAction;
        private InputAction pauseAction;
        private InputAction restartAction;
        private InputAction subscribedPauseAction;
        private Func<bool> earlyPausePressedHandler;
        private int lastForwardedPauseFrame = -1;

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

            inputSource.Capture(new UnityInputSnapshot(
                aimAction.IsPressed(),
                primaryAction.IsPressed(),
                secondaryAction.WasPressedThisFrame(),
                secondaryAction.WasReleasedThisFrame(),
                reloadAction.WasPressedThisFrame(),
                pauseAction.WasPressedThisFrame(),
                restartAction.WasPressedThisFrame()));
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
                && restartAction != null)
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
            bool resolved = aimAction != null
                && primaryAction != null
                && secondaryAction != null
                && reloadAction != null
                && pauseAction != null
                && restartAction != null;
            if (!resolved)
            {
                ClearCachedActions();
                return false;
            }

            EnsurePausePerformedSubscription();
            return true;
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
            lastForwardedPauseFrame = -1;
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
