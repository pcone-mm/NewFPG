using System;
using FPG.Demo.Player;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgBootSecondaryModeSelector : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private Button immediateModeButton;

        [SerializeField]
        private Button chargeModeButton;

        private Action<SecondaryTriggerMode> selectionHandler;
        private bool listenersBound;

        public CanvasGroup CanvasGroup => canvasGroup;
        public Button ImmediateModeButton => immediateModeButton;
        public Button ChargeModeButton => chargeModeButton;
        public bool IsVisible { get; private set; }

        public bool TryValidateAuthoring(out string error)
        {
            if (canvasGroup == null)
            {
                error =
                    "Boot secondary-mode selector requires an explicit CanvasGroup.";
                return false;
            }

            if (immediateModeButton == null || chargeModeButton == null)
            {
                error =
                    "Boot secondary-mode selector requires immediate and charge buttons.";
                return false;
            }

            if (ReferenceEquals(immediateModeButton, chargeModeButton))
            {
                error =
                    "Boot secondary-mode selector requires two distinct buttons.";
                return false;
            }

            if (!OwnsTransform(canvasGroup.transform))
            {
                error =
                    "Boot secondary-mode selector CanvasGroup must be on its root or a child.";
                return false;
            }

            if (!immediateModeButton.transform.IsChildOf(canvasGroup.transform)
                || !chargeModeButton.transform.IsChildOf(canvasGroup.transform))
            {
                error =
                    "Boot secondary-mode buttons must be children of the blocking CanvasGroup.";
                return false;
            }

            Canvas canvas = canvasGroup.GetComponentInParent<Canvas>(true);
            if (canvas == null)
            {
                error = "Boot secondary-mode selector must belong to a Canvas.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryShow(
            FpgPlayableCharacterSelection selection,
            Action<SecondaryTriggerMode> onSelected,
            out string error)
        {
            if (!TryValidateAuthoring(out error))
            {
                return false;
            }

            if (onSelected == null)
            {
                error =
                    "Boot secondary-mode selector requires a selection callback.";
                return false;
            }

            if (!selection.TryValidate(out error))
            {
                error = "Secondary-mode selection is invalid: " + error;
                return false;
            }

            bool supportsImmediate = selection.SupportsSecondaryMode(
                SecondaryTriggerMode.ImmediateRepeatWhileHeld);
            bool supportsCharge = selection.SupportsSecondaryMode(
                SecondaryTriggerMode.ChargeRelease);
            if (!supportsImmediate || !supportsCharge)
            {
                error =
                    "Boot secondary-mode selector requires both immediate and charge modes.";
                return false;
            }

            BindListeners();
            selectionHandler = onSelected;
            immediateModeButton.gameObject.SetActive(true);
            chargeModeButton.gameObject.SetActive(true);
            immediateModeButton.interactable = true;
            chargeModeButton.interactable = true;
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            IsVisible = true;
            error = string.Empty;
            return true;
        }

        public void Hide()
        {
            selectionHandler = null;
            IsVisible = false;
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void Awake()
        {
            BindListeners();
            Hide();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            selectionHandler = null;
            UnbindListeners();
        }

        private void HandleImmediateSelected()
        {
            CommitSelection(SecondaryTriggerMode.ImmediateRepeatWhileHeld);
        }

        private void HandleChargeSelected()
        {
            CommitSelection(SecondaryTriggerMode.ChargeRelease);
        }

        private void CommitSelection(SecondaryTriggerMode mode)
        {
            if (!IsVisible || selectionHandler == null)
            {
                return;
            }

            Action<SecondaryTriggerMode> handler = selectionHandler;
            Hide();
            handler(mode);
        }

        private void BindListeners()
        {
            if (listenersBound
                || immediateModeButton == null
                || chargeModeButton == null)
            {
                return;
            }

            immediateModeButton.onClick.AddListener(HandleImmediateSelected);
            chargeModeButton.onClick.AddListener(HandleChargeSelected);
            listenersBound = true;
        }

        private void UnbindListeners()
        {
            if (!listenersBound)
            {
                return;
            }

            if (immediateModeButton != null)
            {
                immediateModeButton.onClick.RemoveListener(
                    HandleImmediateSelected);
            }

            if (chargeModeButton != null)
            {
                chargeModeButton.onClick.RemoveListener(HandleChargeSelected);
            }

            listenersBound = false;
        }

        private bool OwnsTransform(Transform candidate)
        {
            return candidate != null
                && (candidate == transform || candidate.IsChildOf(transform));
        }
    }
}
