using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgBootCharacterChoice : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Character Choice")]
        [SerializeField]
        private D0CharacterDefinition character;

        [SerializeField]
        [Tooltip("Authored visual-only root shown while this character can be selected.")]
        private GameObject previewRoot;

        [Header("Shot Target")]
        [SerializeField]
        private Collider[] hitColliders = Array.Empty<Collider>();

        [SerializeField]
        private Renderer[] statusRenderers = Array.Empty<Renderer>();

        [SerializeField]
        private Color availableColor = new Color(0.15f, 0.7f, 1f, 1f);

        [SerializeField]
        private Color selectedColor = new Color(0.35f, 1f, 0.45f, 1f);

        [SerializeField]
        private Color unavailableColor = new Color(0.22f, 0.24f, 0.28f, 1f);

        private MaterialPropertyBlock propertyBlock;

        public D0CharacterDefinition Character => character;
        public GameObject PreviewRoot => previewRoot;
        public bool IsSelectable { get; private set; } = true;
        public bool IsSelected { get; private set; }

        public bool TryResolveSelection(
            FpgPlayableCharacterCatalog catalog,
            out FpgPlayableCharacterSelection selection,
            out string error)
        {
            selection = default;
            error = string.Empty;
            if (catalog == null)
            {
                error = "Boot character choice requires a playable character catalog.";
                return false;
            }

            if (character == null
                || !catalog.TryResolve(character, out selection, out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Boot character choice requires a catalog character.";
                }

                return false;
            }

            if (previewRoot == null
                || previewRoot.transform != transform
                    && !previewRoot.transform.IsChildOf(transform))
            {
                error = "Boot character choice requires a visual-only preview root under the choice root.";
                return false;
            }

            if (previewRoot.GetComponentInChildren<D0ActorEntityView>(true) != null)
            {
                error = "Boot character choice preview must not contain a D0 actor Entity.";
                return false;
            }

            Collider[] colliders = hitColliders ?? Array.Empty<Collider>();
            if (colliders.Length == 0)
            {
                error = "Boot character choice requires at least one shot collider.";
                return false;
            }

            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null
                    || collider.transform != transform
                        && !collider.transform.IsChildOf(transform))
                {
                    error =
                        $"Boot character choice collider {index} is missing or outside the choice root.";
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
            if (value)
            {
                IsSelected = false;
            }

            Collider[] colliders = hitColliders ?? Array.Empty<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = value;
                }
            }

            ApplyColor(value
                ? availableColor
                : IsSelected ? selectedColor : unavailableColor);
            SetVisible(value);
        }

        public void MarkSelected()
        {
            IsSelected = true;
            SetSelectable(false);
        }

        public void SetVisible(bool visible)
        {
            if (previewRoot != null && previewRoot.activeSelf != visible)
            {
                previewRoot.SetActive(visible);
            }
        }

        private void OnEnable()
        {
            ApplyColor(IsSelectable
                ? availableColor
                : IsSelected ? selectedColor : unavailableColor);
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
