using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Unity
{
    public enum FpgRoomExitRuntimeState
    {
        Hidden = 0,
        Available = 1,
        Consumed = 2
    }

    [DisallowMultipleComponent]
    public sealed class FpgRoomExitRuntime : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private string exitId;
        [SerializeField] private Collider[] lockColliders = Array.Empty<Collider>();
        [SerializeField] private Behaviour[] interactionBehaviours = Array.Empty<Behaviour>();
        [SerializeField] private Renderer[] statusRenderers = Array.Empty<Renderer>();
        [SerializeField] private Text destinationLabel;
        [SerializeField] private string destinationLabelPrefix = "前往：";
        [SerializeField] private Color lockedColor = new Color(0.9f, 0.15f, 0.1f, 1f);
        [SerializeField] private Color unlockedColor = new Color(0.15f, 0.9f, 0.3f, 1f);

        private bool configured;
        private FpgExitOffer offer;
        private MaterialPropertyBlock propertyBlock;
        private FpgRoomExitRuntimeState state = FpgRoomExitRuntimeState.Hidden;

        public string ExitId => exitId;
        public bool IsConfigured => configured;
        public bool IsLocked => state != FpgRoomExitRuntimeState.Available;
        public FpgRoomExitRuntimeState State => state;
        public FpgExitOffer Offer => offer;
        public IReadOnlyList<Collider> AttackColliders => lockColliders ?? Array.Empty<Collider>();

        public event Action<FpgRoomExitRuntime> Selected;

        public bool TryConfigure(
            string stableExitId,
            Pose worldPose,
            Transform parent,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(stableExitId))
            {
                error = "Room exit runtime requires a stable exit ID.";
                return false;
            }

            if (parent != null)
            {
                transform.SetParent(parent, true);
            }

            transform.SetPositionAndRotation(worldPose.position, worldPose.rotation);
            exitId = stableExitId;
            configured = true;
            Hide();
            error = string.Empty;
            return true;
        }

        public bool TryReveal(FpgExitOffer nextOffer, out string error)
        {
            if (!configured)
            {
                error = "Room exit runtime must be configured before it is revealed.";
                return false;
            }

            if (nextOffer == null || !nextOffer.IsValid
                || !string.Equals(nextOffer.ExitId, exitId, StringComparison.Ordinal))
            {
                error = "Room exit offer must be valid and match the configured exit ID.";
                return false;
            }

            if (state != FpgRoomExitRuntimeState.Hidden || offer != null)
            {
                error = "Room exit destination can only be bound once per reveal.";
                return false;
            }

            if (!HasAttackCollider())
            {
                error = "Room exit requires at least one attack collider.";
                return false;
            }

            offer = nextOffer;
            state = FpgRoomExitRuntimeState.Available;
            ApplyState();
            error = string.Empty;
            return true;
        }

        public void SetLocked(bool value)
        {
            if (value)
            {
                Hide();
                return;
            }

            if (configured
                && offer != null
                && state == FpgRoomExitRuntimeState.Available)
            {
                ApplyState();
            }
        }

        public void Hide()
        {
            offer = null;
            state = FpgRoomExitRuntimeState.Hidden;
            ApplyState();
        }

        public bool TrySelect()
        {
            if (!configured || offer == null
                || state != FpgRoomExitRuntimeState.Available)
            {
                return false;
            }

            state = FpgRoomExitRuntimeState.Consumed;
            ApplyState();
            Selected?.Invoke(this);
            return true;
        }

        internal void ConsumeSilently()
        {
            if (state == FpgRoomExitRuntimeState.Hidden)
            {
                return;
            }

            state = FpgRoomExitRuntimeState.Consumed;
            ApplyState();
        }

        public void BindComponents(Collider[] colliders, Behaviour[] interactions)
        {
            lockColliders = colliders ?? Array.Empty<Collider>();
            interactionBehaviours = interactions ?? Array.Empty<Behaviour>();
            ApplyState();
        }

        public void BindStatusRenderers(Renderer[] renderers)
        {
            statusRenderers = renderers ?? Array.Empty<Renderer>();
            ApplyState();
        }

        public void BindDestinationLabel(Text label)
        {
            destinationLabel = label;
            ApplyState();
        }

        private bool HasAttackCollider()
        {
            Collider[] colliders = lockColliders ?? Array.Empty<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyState()
        {
            bool available = configured
                && offer != null
                && state == FpgRoomExitRuntimeState.Available;
            Collider[] colliders = lockColliders ?? Array.Empty<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider != null)
                {
                    collider.enabled = available;
                }
            }

            Behaviour[] interactions = interactionBehaviours ?? Array.Empty<Behaviour>();
            for (int index = 0; index < interactions.Length; index++)
            {
                Behaviour behaviour = interactions[index];
                if (behaviour != null && behaviour != destinationLabel)
                {
                    behaviour.enabled = available;
                }
            }

            ApplyStatusColor(available ? unlockedColor : lockedColor);
            Renderer[] renderers = statusRenderers ?? Array.Empty<Renderer>();
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer != null)
                {
                    renderer.enabled = available;
                }
            }

            if (destinationLabel != null)
            {
                destinationLabel.text = available
                    ? destinationLabelPrefix + offer.DestinationDisplayName
                    : string.Empty;
                destinationLabel.enabled = available;
            }
        }

        private void ApplyStatusColor(Color color)
        {
            Renderer[] renderers = statusRenderers ?? Array.Empty<Renderer>();
            propertyBlock ??= new MaterialPropertyBlock();
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetColor(ColorId, color);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
