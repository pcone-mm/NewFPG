using System;
using System.Globalization;
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
        [SerializeField] private CombatPresentationProfile presentationProfile;

        [Header("Player bars")]
        [SerializeField] private FpgFormalBarView lifeBar;
        [SerializeField] private FpgFormalBarView barrierBar;
        [SerializeField] private FpgFormalBarView ammoBar;

        [Header("Player values")]
        [SerializeField] private Text lifeText;
        [SerializeField] private Text barrierText;
        [SerializeField] private Text ammoText;
        [SerializeField] private Text stateText;

        private FpgFormalPlayerPresentationSnapshot snapshot =
            FpgFormalPlayerPresentationSnapshot.Unavailable;
        private int lastLife = int.MinValue;
        private int lastMaxLife = int.MinValue;
        private int lastBarrier = int.MinValue;
        private int lastMaxBarrier = int.MinValue;
        private bool lastCoverDestroyed;
        private bool lastCoverMoving;
        private string lastCoverId = string.Empty;
        private int lastAmmo = int.MinValue;
        private int lastMagazineCapacity = int.MinValue;
        private FpgFormalPlayerPresentationState lastState =
            (FpgFormalPlayerPresentationState)(-1);
        private FpgHudResourcePresentation lifePresentation;
        private FpgHudResourcePresentation barrierPresentation;
        private FpgHudResourcePresentation ammoPresentation;

        public FpgFormalPlayerPresentationSnapshot Snapshot => snapshot;
        public FpgFormalPlayerPresentationState CurrentState =>
            snapshot.PresentationState;
        public FpgFormalBarView LifeBar => lifeBar;
        public FpgFormalBarView BarrierBar => barrierBar;
        public FpgFormalBarView AmmoBar => ammoBar;
        public CombatPresentationProfile PresentationProfile =>
            presentationProfile;

        public bool TryValidate(out string error)
        {
            if (presentationProfile == null
                || lifeBar == null || barrierBar == null || ammoBar == null
                || lifeText == null || barrierText == null || ammoText == null
                || stateText == null
                || !(lifeBar.transform is RectTransform)
                || !(barrierBar.transform is RectTransform)
                || !(ammoBar.transform is RectTransform))
            {
                error = "Formal player HUD requires life, barrier, ammo and state references.";
                return false;
            }

            if (!presentationProfile.TryValidateStatic(out error)
                || !TryResolveResourcePresentations(out error))
            {
                return false;
            }

            if (!lifeBar.TryValidate(out error)
                || !barrierBar.TryValidate(out error)
                || !ammoBar.TryValidate(out error))
            {
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

            ApplyResourcePresentations();
            Clear();
            error = string.Empty;
            return true;
        }

        public void Refresh(in FpgFormalPlayerPresentationSnapshot nextSnapshot)
        {
            bool immediate = !snapshot.IsValid;
            snapshot = nextSnapshot;
            if (!snapshot.IsValid)
            {
                ClearVisuals();
                return;
            }

            if (lifePresentation == null && !TryResolveResourcePresentations(out _))
            {
                ClearVisuals();
                return;
            }

            lifeBar.SetPaused(snapshot.IsPaused);
            barrierBar.SetPaused(snapshot.IsPaused);
            ammoBar.SetPaused(snapshot.IsPaused);

            if (snapshot.Life != lastLife || snapshot.MaxLife != lastMaxLife)
            {
                lifeBar.SetValue(snapshot.Life, snapshot.MaxLife, immediate);
                SetText(
                    lifeText,
                    FormatValue(lifePresentation, snapshot.Life, snapshot.MaxLife));
                lastLife = snapshot.Life;
                lastMaxLife = snapshot.MaxLife;
            }

            if (snapshot.CoverDurability != lastBarrier
                || snapshot.MaxCoverDurability != lastMaxBarrier
                || snapshot.IsCoverDestroyed != lastCoverDestroyed
                || snapshot.IsCoverMoving != lastCoverMoving
                || snapshot.CurrentCoverId != lastCoverId)
            {
                barrierBar.SetValue(
                    snapshot.CoverDurability,
                    snapshot.MaxCoverDurability,
                    immediate);
                SetText(
                    barrierText,
                    FormatCoverValue(snapshot));
                lastBarrier = snapshot.CoverDurability;
                lastMaxBarrier = snapshot.MaxCoverDurability;
                lastCoverDestroyed = snapshot.IsCoverDestroyed;
                lastCoverMoving = snapshot.IsCoverMoving;
                lastCoverId = snapshot.CurrentCoverId;
            }

            if (snapshot.Ammo != lastAmmo
                || snapshot.MagazineCapacity != lastMagazineCapacity)
            {
                ammoBar.SetValue(
                    snapshot.Ammo,
                    snapshot.MagazineCapacity,
                    immediate);
                SetText(
                    ammoText,
                    FormatValue(
                        ammoPresentation,
                        snapshot.Ammo,
                        snapshot.MagazineCapacity));
                lastAmmo = snapshot.Ammo;
                lastMagazineCapacity = snapshot.MagazineCapacity;
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
            lifeBar?.SetNormalizedValue(0f);
            barrierBar?.SetNormalizedValue(0f);
            ammoBar?.SetNormalizedValue(0f);
            lifeBar?.SetPaused(false);
            barrierBar?.SetPaused(false);
            ammoBar?.SetPaused(false);
            SetText(lifeText, FormatUnavailable(lifePresentation));
            SetText(barrierText, FormatUnavailable(barrierPresentation));
            SetText(ammoText, FormatUnavailable(ammoPresentation));
            SetText(stateText, "PLAYER UNAVAILABLE");
            lastLife = int.MinValue;
            lastMaxLife = int.MinValue;
            lastBarrier = int.MinValue;
            lastMaxBarrier = int.MinValue;
            lastCoverDestroyed = false;
            lastCoverMoving = false;
            lastCoverId = string.Empty;
            lastAmmo = int.MinValue;
            lastMagazineCapacity = int.MinValue;
            lastState = (FpgFormalPlayerPresentationState)(-1);
            snapshotForStateWeapon = (WeaponState)(-1);
        }

        private bool TryResolveResourcePresentations(out string error)
        {
            if (!presentationProfile.TryGetFormalHudResource(
                    FpgHudResourceKind.Life,
                    out lifePresentation)
                || !presentationProfile.TryGetFormalHudResource(
                    FpgHudResourceKind.Barrier,
                    out barrierPresentation)
                || !presentationProfile.TryGetFormalHudResource(
                    FpgHudResourceKind.Ammo,
                    out ammoPresentation))
            {
                error = "Formal HUD profile has incomplete resource definitions.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void ApplyResourcePresentations()
        {
            ApplyResourcePresentation(lifeBar, lifePresentation);
            ApplyResourcePresentation(barrierBar, barrierPresentation);
            ApplyResourcePresentation(ammoBar, ammoPresentation);
            ApplyResourceOrderToExistingSlots();
        }

        private static void ApplyResourcePresentation(
            FpgFormalBarView bar,
            FpgHudResourcePresentation presentation)
        {
            if (bar == null || presentation == null)
            {
                return;
            }

            bar.TrySetTransitionDuration(presentation.BarEaseDuration);
            bar.TrySetFillColor(presentation.Color);
        }

        private void ApplyResourceOrderToExistingSlots()
        {
            ResourceLayoutEntry[] resources =
            {
                new ResourceLayoutEntry(lifeBar, lifePresentation),
                new ResourceLayoutEntry(barrierBar, barrierPresentation),
                new ResourceLayoutEntry(ammoBar, ammoPresentation)
            };
            float[] slots =
            {
                ((RectTransform)lifeBar.transform).anchoredPosition.y,
                ((RectTransform)barrierBar.transform).anchoredPosition.y,
                ((RectTransform)ammoBar.transform).anchoredPosition.y
            };

            Array.Sort(resources, CompareResourceOrder);
            Array.Sort(slots);
            for (int index = 0; index < resources.Length; index++)
            {
                RectTransform rect =
                    (RectTransform)resources[index].Bar.transform;
                Vector2 position = rect.anchoredPosition;
                position.y = slots[slots.Length - 1 - index];
                rect.anchoredPosition = position;
            }
        }

        private static int CompareResourceOrder(
            ResourceLayoutEntry left,
            ResourceLayoutEntry right)
        {
            return left.Presentation.Order.CompareTo(right.Presentation.Order);
        }

        private static string FormatValue(
            FpgHudResourcePresentation presentation,
            int value,
            int maximum)
        {
            return presentation.Label + " " + string.Format(
                CultureInfo.InvariantCulture,
                presentation.ValueFormat,
                Mathf.Max(0, value),
                Mathf.Max(0, maximum));
        }

        private static string FormatUnavailable(
            FpgHudResourcePresentation presentation)
        {
            return presentation == null ? string.Empty : presentation.Label + " --";
        }

        private static string FormatCoverValue(
            in FpgFormalPlayerPresentationSnapshot value)
        {
            if (value.IsCoverMoving)
            {
                return "COVER — MOVING";
            }

            if (value.IsCoverDestroyed)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "COVER 0 / {0} · DESTROYED",
                    Mathf.Max(0, value.MaxCoverDurability));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "COVER {0} / {1}",
                Mathf.Max(0, value.CoverDurability),
                Mathf.Max(0, value.MaxCoverDurability));
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

        private static void SetText(Text text, string value)
        {
            if (text != null && text.text != value)
            {
                text.text = value;
            }
        }

        private readonly struct ResourceLayoutEntry
        {
            public ResourceLayoutEntry(
                FpgFormalBarView bar,
                FpgHudResourcePresentation presentation)
            {
                Bar = bar;
                Presentation = presentation;
            }

            public FpgFormalBarView Bar { get; }
            public FpgHudResourcePresentation Presentation { get; }
        }
    }
}
