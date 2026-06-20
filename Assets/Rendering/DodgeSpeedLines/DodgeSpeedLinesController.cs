using UnityEngine;
using UnityEngine.Rendering;

namespace NewFPG.Rendering
{
    /// <summary>
    /// PPV manager for <see cref="DodgeSpeedLinesVolume"/>.
    /// Attach this to the Global Volume object, not to a character mesh.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("NewFPG/Rendering/Dodge Speed Lines Controller")]
    public sealed class DodgeSpeedLinesController : MonoBehaviour
    {
        [SerializeField] private Volume targetVolume;

        private DodgeSpeedLinesVolume settings;

        public bool EffectEnabled => settings != null && settings.effectEnabled.value;

        private void Reset()
        {
            targetVolume = GetComponent<Volume>();
        }

        private void Awake()
        {
            ResolveSettings();
        }

        /// <summary>Animation Event-friendly method that enables the Volume effect.</summary>
        public void EnableEffect()
        {
            SetEffectEnabled(true);
        }

        /// <summary>Animation Event-friendly method that disables the Volume effect.</summary>
        public void DisableEffect()
        {
            SetEffectEnabled(false);
        }

        /// <summary>Sets the Volume override used by the renderer feature.</summary>
        public void SetEffectEnabled(bool enabled)
        {
            if (!ResolveSettings())
            {
                return;
            }

            settings.effectEnabled.Override(enabled);
        }

        private bool ResolveSettings()
        {
            if (settings != null)
            {
                return true;
            }

            if (targetVolume == null)
            {
                targetVolume = GetComponent<Volume>();
            }

            if (targetVolume == null || targetVolume.profile == null)
            {
                Debug.LogWarning("Dodge Speed Lines requires a Volume with a Dodge Speed Lines override.", this);
                return false;
            }

            if (!targetVolume.profile.TryGet(out settings))
            {
                settings = targetVolume.profile.Add<DodgeSpeedLinesVolume>(true);
            }

            return true;
        }
    }
}
