using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Scene-local marker for the root owned by the 2.5D D0 installer.
    /// It is a presentation-only boundary marker and must never carry physics
    /// or write into combat state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class D0SliceInstallationMarker : MonoBehaviour
    {
        [SerializeField]
        private CombatPresentationProfile presentationProfile;

        [SerializeField]
        private CombatAudioBank audioBank;

        [SerializeField]
        private D0SliceInstallationState installationState;

        public CombatPresentationProfile PresentationProfile => presentationProfile;

        public CombatAudioBank AudioBank => audioBank;

        public D0SliceInstallationState InstallationState => installationState;

        public bool TryValidate(out string error)
        {
            if (presentationProfile == null)
            {
                error = "D0 slice marker requires a combat presentation profile.";
                return false;
            }

            if (audioBank == null)
            {
                error = "D0 slice marker requires a combat audio bank.";
                return false;
            }

            if (installationState == null)
            {
                error = "D0 slice marker requires an installation state asset.";
                return false;
            }

            if (!installationState.ProtectsCombatLab)
            {
                error = "D0 slice marker requires an installed CombatLab ownership state.";
                return false;
            }

            List<Component> forbiddenComponents = new List<Component>();
            GetComponentsInChildren(true, forbiddenComponents);
            for (int index = 0; index < forbiddenComponents.Count; index++)
            {
                Component component = forbiddenComponents[index];
                if (component is Collider
                    || component is Collider2D
                    || component is Rigidbody
                    || component is Rigidbody2D)
                {
                    error = "D0 slice presentation root must not contain collider or rigidbody components.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
