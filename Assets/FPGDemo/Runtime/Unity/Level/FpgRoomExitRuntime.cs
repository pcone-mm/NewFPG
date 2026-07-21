using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Runtime lock/selection boundary for one authored room exit. The room
    /// asset only stores the marker; this component owns collision and
    /// interaction state for the live room instance.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgRoomExitRuntime : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField]
        private string exitId;

        [Tooltip("Blocking colliders: enabled while locked, disabled after the room clears.")]
        [SerializeField]
        private Collider[] lockColliders = Array.Empty<Collider>();

        [SerializeField]
        private Behaviour[] interactionBehaviours = Array.Empty<Behaviour>();

        [SerializeField]
        private Renderer[] statusRenderers = Array.Empty<Renderer>();

        [SerializeField]
        private Color lockedColor = new Color(0.9f, 0.15f, 0.1f, 1f);

        [SerializeField]
        private Color unlockedColor = new Color(0.15f, 0.9f, 0.3f, 1f);

        private bool configured;
        private bool locked = true;
        private MaterialPropertyBlock propertyBlock;

        public string ExitId => exitId;
        public bool IsConfigured => configured;
        public bool IsLocked => locked;

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
            SetLocked(true);
            error = string.Empty;
            return true;
        }

public void SetLocked(bool value)
        {
            locked = value;
            if (lockColliders != null)
            {
                for (int index = 0; index < lockColliders.Length; index++)
                {
                    Collider collider = lockColliders[index];
                    if (collider != null)
                    {
                        collider.enabled = value;
                    }
                }
            }

            if (interactionBehaviours != null)
            {
                for (int index = 0; index < interactionBehaviours.Length; index++)
                {
                    Behaviour behaviour = interactionBehaviours[index];
                    if (behaviour != null)
                    {
                        behaviour.enabled = !value;
                    }
                }
            }

            ApplyStatusColor(value ? lockedColor : unlockedColor);
        }

        public bool TrySelect()
        {
            if (!configured || locked)
            {
                return false;
            }

            Selected?.Invoke(this);
            return true;
        }

        public void BindComponents(
            Collider[] colliders,
            Behaviour[] interactions)
        {
            lockColliders = colliders ?? Array.Empty<Collider>();
            interactionBehaviours = interactions ?? Array.Empty<Behaviour>();
            SetLocked(locked);
        }

public void BindStatusRenderers(Renderer[] renderers)
        {
            statusRenderers = renderers ?? Array.Empty<Renderer>();
            ApplyStatusColor(locked ? lockedColor : unlockedColor);
        }

        private void ApplyStatusColor(Color color)
        {
            Renderer[] renderers = statusRenderers ?? Array.Empty<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

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


        private void OnDisable()
        {
            // A disabled exit must return to a fail-closed state before reuse.
            SetLocked(true);
        }
    }
}


