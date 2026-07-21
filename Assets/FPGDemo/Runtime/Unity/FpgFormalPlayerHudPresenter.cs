using FPG.Demo.Player;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Minimal formal-room player HUD. It renders only the player read model;
    /// enemy bars, threat telemetry and terminal overlays belong to other
    /// presentation surfaces.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgFormalPlayerHudPresenter : MonoBehaviour
    {
        [Header("Player bars")]
        [SerializeField] private Image lifeFill;
        [SerializeField] private Image barrierFill;
        [SerializeField] private Image ammoFill;

        [Header("Player values")]
        [SerializeField] private Text lifeText;
        [SerializeField] private Text barrierText;
        [SerializeField] private Text ammoText;
        [SerializeField] private Text stateText;

        private FpgFormalPlayerPresentationSnapshot snapshot =
            FpgFormalPlayerPresentationSnapshot.Unavailable;
        private int lastLife = int.MinValue;
        private int lastBarrier = int.MinValue;
        private int lastAmmo = int.MinValue;
        private FpgFormalPlayerPresentationState lastState =
            (FpgFormalPlayerPresentationState)(-1);

        public FpgFormalPlayerPresentationSnapshot Snapshot => snapshot;
        public FpgFormalPlayerPresentationState CurrentState =>
            snapshot.PresentationState;

        public bool TryValidate(out string error)
        {
            if (lifeFill == null || barrierFill == null || ammoFill == null
                || lifeText == null || barrierText == null || ammoText == null
                || stateText == null)
            {
                error = "Formal player HUD requires life, barrier, ammo and state references.";
                return false;
            }

            if (GetComponentsInChildren<Collider>(true).Length > 0
                || GetComponentsInChildren<Collider2D>(true).Length > 0
                || GetComponentsInChildren<Rigidbody>(true).Length > 0
                || GetComponentsInChildren<Rigidbody2D>(true).Length > 0)
            {
                error = "Formal player HUD must not contain Collider or Rigidbody components.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryPrepare(out string error)
        {
            if (!TryValidate(out error))
            {
                return false;
            }

            Clear();
            error = string.Empty;
            return true;
        }

        public void Refresh(in FpgFormalPlayerPresentationSnapshot nextSnapshot)
        {
            snapshot = nextSnapshot;
            if (!snapshot.IsValid)
            {
                ClearVisuals();
                return;
            }

            if (snapshot.Life != lastLife)
            {
                SetFill(lifeFill, snapshot.Life, snapshot.MaxLife);
                SetText(lifeText, FormatValue("LIFE", snapshot.Life, snapshot.MaxLife));
                lastLife = snapshot.Life;
            }

            if (snapshot.Barrier != lastBarrier)
            {
                SetFill(barrierFill, snapshot.Barrier, snapshot.MaxBarrier);
                SetText(
                    barrierText,
                    FormatValue("BARRIER", snapshot.Barrier, snapshot.MaxBarrier));
                lastBarrier = snapshot.Barrier;
            }

            if (snapshot.Ammo != lastAmmo)
            {
                SetFill(ammoFill, snapshot.Ammo, snapshot.MagazineCapacity);
                SetText(
                    ammoText,
                    FormatValue("AMMO", snapshot.Ammo, snapshot.MagazineCapacity));
                lastAmmo = snapshot.Ammo;
            }

            if (snapshot.PresentationState != lastState
                || snapshot.WeaponState != snapshotForStateWeapon)
            {
                SetText(stateText, FormatState(snapshot));
                lastState = snapshot.PresentationState;
                snapshotForStateWeapon = snapshot.WeaponState;
            }
        }

        public void Clear()
        {
            snapshot = FpgFormalPlayerPresentationSnapshot.Unavailable;
            ClearVisuals();
        }

        private WeaponState snapshotForStateWeapon = (WeaponState)(-1);

        private void ClearVisuals()
        {
            SetFill(lifeFill, 0, 1);
            SetFill(barrierFill, 0, 1);
            SetFill(ammoFill, 0, 1);
            SetText(lifeText, "LIFE --");
            SetText(barrierText, "BARRIER --");
            SetText(ammoText, "AMMO --");
            SetText(stateText, "PLAYER UNAVAILABLE");
            lastLife = int.MinValue;
            lastBarrier = int.MinValue;
            lastAmmo = int.MinValue;
            lastState = (FpgFormalPlayerPresentationState)(-1);
            snapshotForStateWeapon = (WeaponState)(-1);
        }

        private static string FormatValue(string label, int value, int maximum)
        {
            return label + " " + Mathf.Max(0, value) + " / " + Mathf.Max(0, maximum);
        }

        private static string FormatState(
            in FpgFormalPlayerPresentationSnapshot value)
        {
            switch (value.PresentationState)
            {
                case FpgFormalPlayerPresentationState.Preparing:
                    return "PREPARING";
                case FpgFormalPlayerPresentationState.Paused:
                    return "PAUSED";
                case FpgFormalPlayerPresentationState.Victory:
                    return "VICTORY";
                case FpgFormalPlayerPresentationState.Defeat:
                    return "DEFEAT";
                case FpgFormalPlayerPresentationState.Faulted:
                    return "FAULTED";
                case FpgFormalPlayerPresentationState.Active:
                    switch (value.WeaponState)
                    {
                        case WeaponState.Reloading:
                            return "RELOADING";
                        case WeaponState.AltCharging:
                            return "CHARGING";
                        case WeaponState.PrimaryRecovery:
                        case WeaponState.AltRecovery:
                            return "RECOVERING";
                        default:
                            return value.ExposureState == PlayerExposureState.Withdrawn
                                ? "GUARDED"
                                : "READY";
                    }
                default:
                    return "PLAYER UNAVAILABLE";
            }
        }

        private static void SetFill(Image image, int value, int maximum)
        {
            if (image != null)
            {
                image.fillAmount = maximum <= 0
                    ? 0f
                    : Mathf.Clamp01(value / (float)maximum);
            }
        }

        private static void SetText(Text text, string value)
        {
            if (text != null && text.text != value)
            {
                text.text = value;
            }
        }
    }
}
