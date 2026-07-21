using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Editor-authored ownership record for the 2.5D D0 combat slice.
    /// This asset deliberately carries no per-run state; its only runtime-safe
    /// purpose is to describe which scene installation is protected from the
    /// legacy greybox installer.
    /// </summary>
    [CreateAssetMenu(
        fileName = "D0SliceInstallationState",
        menuName = "FPG Demo/D0 Slice Installation State")]
    public sealed class D0SliceInstallationState : ScriptableObject
    {
        [SerializeField]
        private bool installationComplete;

        [SerializeField]
        private string ownedScenePath = "Assets/FPGDemo/Scenes/CombatLab.unity";

        [SerializeField]
        private CombatPresentationProfile presentationProfile;

        [SerializeField]
        private CombatAudioBank audioBank;

        [SerializeField, Min(0)]
        private int installationRevision;

        public bool InstallationComplete => installationComplete;

        public string OwnedScenePath => ownedScenePath;

        public CombatPresentationProfile PresentationProfile => presentationProfile;

        public CombatAudioBank AudioBank => audioBank;

        public int InstallationRevision => installationRevision;

        /// <summary>
        /// The legacy installer may use this as an early, fail-closed guard
        /// before it opens or mutates CombatLab.
        /// </summary>
        public bool ProtectsCombatLab => installationComplete
            && string.Equals(
                ownedScenePath,
                "Assets/FPGDemo/Scenes/CombatLab.unity",
                System.StringComparison.Ordinal);

        public bool TryValidate(out string error)
        {
            if (!installationComplete)
            {
                error = "D0 slice installation is not complete.";
                return false;
            }

            if (!ProtectsCombatLab)
            {
                error = "D0 slice installation must explicitly own CombatLab.";
                return false;
            }

            if (presentationProfile == null)
            {
                error = "D0 slice installation requires a combat presentation profile.";
                return false;
            }

            if (audioBank == null)
            {
                error = "D0 slice installation requires a combat audio bank.";
                return false;
            }

            if (installationRevision <= 0)
            {
                error = "D0 slice installation revision must be positive after installation.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
