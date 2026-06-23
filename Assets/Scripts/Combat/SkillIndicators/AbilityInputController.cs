using NewFPG.Prototype;
using UnityEngine;

namespace NewFPG.Combat.SkillIndicators
{
    [DisallowMultipleComponent]
    public sealed class AbilityInputController : MonoBehaviour
    {
        [SerializeField] private PrototypeFirstPersonWeaponView weaponView;
        [SerializeField] private PlayerWeaponCaster weaponCaster;
        [SerializeField] private SkillIndicatorPreviewRuntime previewRuntime;
        [SerializeField] private SkillIndicatorUiInputBlocker uiInputBlocker;
        [SerializeField] private SkillIndicatorTemporaryArtIndex temporaryArtIndex;
        [SerializeField] private Camera aimCamera;

        private bool enabledForInput;
        private bool pressing;
        private bool previewing;
        private int activeHudWeaponIndex = -1;
        private int activeCasterIndex = -1;
        private float activePressStartedAt;
        private Vector2 activePointerPosition;
        private SkillIndicatorPreviewFrame activePreviewFrame;
        private bool blockingUiInput;

        public bool IsPreviewing => previewing;

        private void Reset()
        {
            weaponView = GetComponent<PrototypeFirstPersonWeaponView>();
            previewRuntime = GetComponent<SkillIndicatorPreviewRuntime>();
            uiInputBlocker = GetComponent<SkillIndicatorUiInputBlocker>();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            CancelActiveInput();
            Unsubscribe();
        }

        private void Update()
        {
            if (!enabledForInput || !pressing || previewing || weaponCaster == null)
            {
                return;
            }

            WeaponDefinition weapon = weaponCaster.GetWeapon(activeCasterIndex);
            SkillIndicatorResolvedConfig config = SkillIndicatorResolvedConfig.From(weapon != null ? weapon.IndicatorConfig : null, weapon);
            if (config.inputMode != SkillIndicatorInputMode.HoldPreview)
            {
                return;
            }

            float held = Time.time - activePressStartedAt;
            if (held >= config.holdEnterDelay)
            {
                ShowPreview(held);
            }
        }

        public void Bind(
            PrototypeFirstPersonWeaponView nextWeaponView,
            PlayerWeaponCaster nextCaster,
            Camera nextAimCamera,
            SkillIndicatorTemporaryArtIndex nextTemporaryArtIndex)
        {
            Unsubscribe();
            weaponView = nextWeaponView != null ? nextWeaponView : weaponView;
            weaponCaster = nextCaster;
            aimCamera = nextAimCamera != null ? nextAimCamera : aimCamera;
            temporaryArtIndex = nextTemporaryArtIndex != null ? nextTemporaryArtIndex : temporaryArtIndex;
            ResolveReferences();
            Subscribe();
        }

        public void SetInputEnabled(bool enabled)
        {
            enabledForInput = enabled;
            if (!enabled)
            {
                CancelActiveInput();
            }
        }

        private void ResolveReferences()
        {
            if (weaponView == null)
            {
                weaponView = GetComponent<PrototypeFirstPersonWeaponView>();
            }

            if (previewRuntime == null)
            {
                previewRuntime = GetComponent<SkillIndicatorPreviewRuntime>();
                if (previewRuntime == null)
                {
                    previewRuntime = gameObject.AddComponent<SkillIndicatorPreviewRuntime>();
                }
            }

            if (uiInputBlocker == null)
            {
                uiInputBlocker = GetComponent<SkillIndicatorUiInputBlocker>();
                if (uiInputBlocker == null)
                {
                    uiInputBlocker = gameObject.AddComponent<SkillIndicatorUiInputBlocker>();
                }
            }

            previewRuntime.Configure(temporaryArtIndex, aimCamera);
        }

        private void Subscribe()
        {
            if (weaponView == null)
            {
                return;
            }

            weaponView.WeaponPointerPressed -= OnWeaponPointerPressed;
            weaponView.WeaponPointerHeld -= OnWeaponPointerHeld;
            weaponView.WeaponPointerReleased -= OnWeaponPointerReleased;
            weaponView.WeaponPointerCancelled -= OnWeaponPointerCancelled;
            weaponView.WeaponPointerPressed += OnWeaponPointerPressed;
            weaponView.WeaponPointerHeld += OnWeaponPointerHeld;
            weaponView.WeaponPointerReleased += OnWeaponPointerReleased;
            weaponView.WeaponPointerCancelled += OnWeaponPointerCancelled;
        }

        private void Unsubscribe()
        {
            if (weaponView == null)
            {
                return;
            }

            weaponView.WeaponPointerPressed -= OnWeaponPointerPressed;
            weaponView.WeaponPointerHeld -= OnWeaponPointerHeld;
            weaponView.WeaponPointerReleased -= OnWeaponPointerReleased;
            weaponView.WeaponPointerCancelled -= OnWeaponPointerCancelled;
        }

        private bool OnWeaponPointerPressed(PrototypeFirstPersonWeaponView.WeaponPointerContext context)
        {
            if (!enabledForInput || weaponCaster == null)
            {
                return true;
            }

            activeHudWeaponIndex = context.weaponIndex;
            activeCasterIndex = ResolveCasterIndex(context.weaponIndex);
            activePressStartedAt = Time.time;
            activePointerPosition = context.currentScreenPosition;
            pressing = true;
            previewing = false;
            BeginUiInputBlock();

            return false;
        }

        private void OnWeaponPointerHeld(PrototypeFirstPersonWeaponView.WeaponPointerContext context)
        {
            if (!pressing || activeHudWeaponIndex != context.weaponIndex)
            {
                return;
            }

            activePointerPosition = context.currentScreenPosition;
            if (previewing)
            {
                ShowPreview(context.holdDuration);
            }
        }

        private void OnWeaponPointerReleased(PrototypeFirstPersonWeaponView.WeaponPointerContext context)
        {
            if (!pressing || activeHudWeaponIndex != context.weaponIndex)
            {
                return;
            }

            activePointerPosition = context.currentScreenPosition;
            bool released = previewing ? CommitPreviewRelease(context.holdDuration) : CommitTapRelease();
            if (released && weaponView != null)
            {
                weaponView.PlayWeaponAttack(activeHudWeaponIndex);
            }

            CancelActiveInput();
        }

        private void OnWeaponPointerCancelled(PrototypeFirstPersonWeaponView.WeaponPointerContext context)
        {
            CancelActiveInput();
        }

        private void ShowPreview(float holdDuration)
        {
            if (weaponCaster == null || previewRuntime == null)
            {
                return;
            }

            WeaponDefinition weapon = weaponCaster.GetWeapon(activeCasterIndex);
            if (weapon == null || !weaponCaster.CanCast(activeCasterIndex))
            {
                previewRuntime.HidePreview();
                previewing = false;
                return;
            }

            previewing = true;
            Transform castOrigin = weaponCaster.CastOrigin;
            activePreviewFrame = previewRuntime.ShowPreview(
                weapon.IndicatorConfig,
                weapon,
                castOrigin,
                castOrigin,
                activePointerPosition,
                true,
                holdDuration);
        }

        private bool CommitTapRelease()
        {
            if (weaponCaster == null)
            {
                return false;
            }

            WeaponDefinition weapon = weaponCaster.GetWeapon(activeCasterIndex);
            SkillIndicatorResolvedConfig config = SkillIndicatorResolvedConfig.From(weapon != null ? weapon.IndicatorConfig : null, weapon);
            if (weapon == null || config.tapPolicy == SkillIndicatorDefaultReleasePolicy.AutoSelectBestTarget)
            {
                return weaponCaster.TryCast(activeCasterIndex);
            }

            SkillIndicatorPreviewFrame frame = previewRuntime != null
                ? ResolveFrameForActiveWeapon(weapon)
                : default;
            return frame.IsValid ? weaponCaster.TryCast(activeCasterIndex, frame.Command) : weaponCaster.TryCast(activeCasterIndex);
        }

        private SkillIndicatorPreviewFrame ResolveFrameForActiveWeapon(WeaponDefinition weapon)
        {
            Transform castOrigin = weaponCaster.CastOrigin;
            return previewRuntime.Resolve(
                weapon.IndicatorConfig,
                weapon,
                castOrigin,
                castOrigin,
                activePointerPosition,
                true,
                Time.time - activePressStartedAt);
        }

        private bool CommitPreviewRelease(float holdDuration)
        {
            if (weaponCaster == null)
            {
                return false;
            }

            WeaponDefinition weapon = weaponCaster.GetWeapon(activeCasterIndex);
            if (weapon == null)
            {
                return false;
            }

            SkillIndicatorPreviewFrame frame = previewRuntime != null && previewRuntime.HasPreview
                ? previewRuntime.CurrentFrame
                : activePreviewFrame;

            if (!frame.IsValid && frame.Config.invalidReleasePolicy == SkillIndicatorInvalidReleasePolicy.FallbackToDefault)
            {
                return weaponCaster.TryCast(activeCasterIndex);
            }

            CastCommandData command = frame.Command;
            command.HoldDuration = holdDuration;
            command.LocalCastSequence = previewRuntime != null ? previewRuntime.NextCastSequence() : 0;
            return weaponCaster.TryCast(activeCasterIndex, command);
        }

        private void CancelActiveInput()
        {
            pressing = false;
            previewing = false;
            activeHudWeaponIndex = -1;
            activeCasterIndex = -1;
            activePressStartedAt = 0f;
            activePointerPosition = Vector2.zero;
            activePreviewFrame = default;
            if (previewRuntime != null)
            {
                previewRuntime.HidePreview();
            }

            EndUiInputBlock();
        }

        private void BeginUiInputBlock()
        {
            if (blockingUiInput)
            {
                return;
            }

            ResolveReferences();
            uiInputBlocker.BeginBlock();
            blockingUiInput = true;
        }

        private void EndUiInputBlock()
        {
            if (!blockingUiInput)
            {
                return;
            }

            if (uiInputBlocker != null)
            {
                uiInputBlocker.EndBlock();
            }

            blockingUiInput = false;
        }

        private int ResolveCasterIndex(int hudWeaponIndex)
        {
            if (weaponCaster != null && weaponCaster.WeaponCount == 1)
            {
                return 0;
            }

            return hudWeaponIndex;
        }
    }
}
