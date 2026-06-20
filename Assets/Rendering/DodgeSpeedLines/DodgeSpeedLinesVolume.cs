using UnityEngine;
using UnityEngine.Rendering;

namespace NewFPG.Rendering
{
    /// <summary>Volume Profile settings consumed by <see cref="DodgeSpeedLinesRendererFeature"/>.</summary>
    [System.Serializable]
    [VolumeComponentMenu("NewFPG/Dodge Speed Lines")]
    public sealed class DodgeSpeedLinesVolume : VolumeComponent
    {
        [Tooltip("Shows the effect continuously while enabled. Toggle this from a PPV manager or animation event bridge.")]
        public BoolParameter effectEnabled = new BoolParameter(false, true);

        public ClampedFloatParameter intensity = new ClampedFloatParameter(1f, 0f, 1f, true);
        public ClampedFloatParameter opacity = new ClampedFloatParameter(0.82f, 0f, 1f, true);
        public MinFloatParameter noiseScale = new MinFloatParameter(32f, 0.01f, true);
        public MinFloatParameter animationSpeed = new MinFloatParameter(2f, 0f, true);
        public ClampedFloatParameter lineThreshold = new ClampedFloatParameter(0.3f, 0f, 1f, true);
        public MinFloatParameter radialScale = new MinFloatParameter(1f, 0.01f, true);
        public ColorParameter lineColor = new ColorParameter(new Color(0.72f, 0.9f, 1f, 1f), false, true, true, true);

        public bool IsActive()
        {
            return active && effectEnabled.value && intensity.value > 0f && opacity.value > 0f;
        }
    }
}
