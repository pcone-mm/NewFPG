using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Authored Boot target for one room choice. GameBootstrap owns input and
    /// scene loading; this component only owns the selected room and hit target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgBootRoomEntrance : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Room Choice")]
        [SerializeField]
        private FpgRoomDefinition roomDefinition;

        [Header("Shot Target")]
        [SerializeField]
        private Collider[] hitColliders = Array.Empty<Collider>();

        [SerializeField]
        private Renderer[] statusRenderers = Array.Empty<Renderer>();

        [SerializeField]
        private Color availableColor = new Color(0.15f, 0.7f, 1f, 1f);

        [SerializeField]
        private Color selectedColor = new Color(0.35f, 1f, 0.45f, 1f);

        private MaterialPropertyBlock propertyBlock;

        public FpgRoomDefinition RoomDefinition => roomDefinition;

        public bool IsSelectable { get; private set; } = true;

        public bool TryValidate(out string error)
        {
            if (roomDefinition == null)
            {
                error = "Boot room entrance requires a room definition.";
                return false;
            }

            FpgRoomValidationResult validation = roomDefinition.Validate();
            if (!validation.IsValid)
            {
                error = validation.FirstError == null
                    ? $"Room '{roomDefinition.RoomId}' is invalid."
                    : validation.FirstError.Message;
                return false;
            }

            Collider[] colliders = hitColliders ?? Array.Empty<Collider>();
            if (colliders.Length == 0)
            {
                error = "Boot room entrance requires at least one shot collider.";
                return false;
            }

            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] == null
                    || !colliders[index].transform.IsChildOf(transform)
                        && colliders[index].transform != transform)
                {
                    error = $"Boot room entrance collider {index} is missing or outside the entrance root.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public bool OwnsCollider(Collider candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            Collider[] colliders = hitColliders ?? Array.Empty<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        public void SetSelectable(bool value)
        {
            IsSelectable = value;
            Collider[] colliders = hitColliders ?? Array.Empty<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = value;
                }
            }

            ApplyColor(value ? availableColor : selectedColor);
        }

        public void MarkSelected()
        {
            SetSelectable(false);
        }

        private void OnEnable()
        {
            ApplyColor(IsSelectable ? availableColor : selectedColor);
        }

        private void ApplyColor(Color color)
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
    }
}
