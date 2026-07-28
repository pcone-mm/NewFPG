using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Applies a small 2.5D viewport-relative offset to authored forest layers.
    /// It is visual-only: it has no collider, does not move the gameplay camera,
    /// and therefore cannot invalidate the aim ray sampled by BattleSessionHost.
    /// </summary>
    [DefaultExecutionOrder(-450)]
    [DisallowMultipleComponent]
    public sealed class D0ForestParallax : MonoBehaviour,
        IFpgRoomArtPresentationBinding
    {
        [SerializeField]
        private D0ForestParallaxLayer[] layers = System.Array.Empty<D0ForestParallaxLayer>();

        private ICombatAimViewportSource aimViewportSource;

        public ICombatAimViewportSource AimViewportSource => aimViewportSource;
        public CombatAimReticle AimReticle => aimViewportSource as CombatAimReticle;

        public int LayerCount => layers == null ? 0 : layers.Length;

        public void Configure(CombatAimReticle source, D0ForestParallaxLayer[] configuredLayers)
        {
            Configure((ICombatAimViewportSource)source, configuredLayers);
        }

        public void Configure(
            ICombatAimViewportSource source,
            D0ForestParallaxLayer[] configuredLayers)
        {
            aimViewportSource = source;
            layers = configuredLayers ?? System.Array.Empty<D0ForestParallaxLayer>();
            ResetVisualState();
        }

        private void Awake()
        {
            ResetVisualState();
        }

        private void LateUpdate()
        {
            if (aimViewportSource == null
                || !aimViewportSource.TryGetViewport(out Vector2 viewport)
                || layers == null)
            {
                return;
            }

            for (int index = 0; index < layers.Length; index++)
            {
                D0ForestParallaxLayer layer = layers[index];
                if (layer != null)
                {
                    layer.ApplyViewport(viewport);
                }
            }
        }

        public bool TryBind(
            FpgRoomArtPresentationContext context,
            out string error)
        {
            if (context == null)
            {
                error = "Forest parallax requires a room art presentation context.";
                return false;
            }

            if (!TryValidateLayers(out error))
            {
                return false;
            }

            aimViewportSource = context.AimViewportSource;
            ResetVisualState();
            error = string.Empty;
            return true;
        }

        public void Unbind()
        {
            aimViewportSource = null;
            ResetVisualState();
        }

        public void ResetVisualState()
        {
            if (layers == null)
            {
                return;
            }

            for (int index = 0; index < layers.Length; index++)
            {
                if (layers[index] != null)
                {
                    layers[index].ResetToBasePosition();
                }
            }
        }

        public bool TryValidate(out string error)
        {
            return TryValidateLayers(out error);
        }

        private bool TryValidateLayers(out string error)
        {
            if (layers == null || layers.Length == 0)
            {
                error = "D0ForestParallax requires at least one layer.";
                return false;
            }

            for (int index = 0; index < layers.Length; index++)
            {
                D0ForestParallaxLayer layer = layers[index];
                if (layer == null || !layer.transform.IsChildOf(transform))
                {
                    error = "D0ForestParallax layers must be non-null children of its stage root.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }

}
