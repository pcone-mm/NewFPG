using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Owns the formal virtual free cursor. The operating-system cursor remains
    /// locked while the project-wide Look action moves this reticle inside the
    /// authored safe area.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class CombatAimReticle : MonoBehaviour, ICombatAimViewportSource
    {
        private const float PrimarySpreadReferenceDistance = 20f;

        private D0ThreeCProfile threeCProfile;
        private float mouseReticleSensitivity = 1f;
        private Vector2 mouseReferenceResolution = new Vector2(1920f, 1080f);
        private float gamepadReticleSpeed = 0.65f;
        private float gamepadReticleDeadzone = 0.15f;
        private float gamepadReticleResponseExponent = 1.6f;

        [SerializeField]
        private bool lockSystemCursor = true;

        [SerializeField]
        private bool resetOnApplicationFocus = true;

        [SerializeField]
        [Tooltip("Radial360 Image used only for secondary-charge progress.")]
        private Image chargeProgressImage;

        [SerializeField]
        private LayeredAimIndicatorGraphic layeredGraphic;

        private RectTransform reticleRect;
        private Vector2 viewport = CombatAimViewportMath.Center;
        private Rect safeViewport = new Rect(
            CombatAimViewportMath.SafeMinimumX,
            CombatAimViewportMath.SafeMinimumY,
            CombatAimViewportMath.SafeMaximumX - CombatAimViewportMath.SafeMinimumX,
            CombatAimViewportMath.SafeMaximumY - CombatAimViewportMath.SafeMinimumY);
        private bool inputFrozen;
        private bool systemCursorLocked;
        private bool aimHeld;
        private Graphic[] strokes;
        private Vector2[] strokeBaseSizes;
        private FpgReticlePresentation presentation;
        private FpgReticleTargetState targetState;
        private FpgReticlePulseState pulseState;
        private float pulseTimeRemaining;
        private float shotTimeRemaining;
        private float hitTimeRemaining;
        private bool chargeProgressActive;
        private float chargeProgress;
        private PlayerAimIndicatorPresentationDefinition aimIndicatorStyle;
        private Camera projectionCamera;
        private FpgAimIndicatorBaseState baseState =
            FpgAimIndicatorBaseState.Normal;
        private bool hiddenState;
        private bool reloadingState;
        private bool unavailableState;
        private bool currentCoverBlockedState;
        private bool enemyState;
        private float reloadProgress;
        private float reloadLoopPhase;
        private float primarySpreadTangent;
        private float secondaryAreaRadius;
        private FpgResolvedAimContext resolvedAimContext;
        private long frozenAimVersion;
        private long lastHitAttackId;
        private readonly ProjectWideBattleInputAdapter inputAdapter =
            new ProjectWideBattleInputAdapter();

        public Vector2 Viewport => viewport;

        public bool IsInputFrozen => inputFrozen;

        public bool IsSystemCursorLocked => systemCursorLocked;

        public bool IsAimHeld => aimHeld;

        public D0ThreeCProfile ThreeCProfile => threeCProfile;

        public Rect SafeViewport => safeViewport;

        public FpgReticleTargetState TargetState => targetState;

        public FpgReticlePulseState PulseState => pulseState;

        public float PulseTimeRemaining => pulseTimeRemaining;

        public Image ChargeProgressImage => chargeProgressImage;

        public bool IsChargeProgressActive => chargeProgressActive;

        public float ChargeProgress => chargeProgress;

        public LayeredAimIndicatorGraphic LayeredGraphic => layeredGraphic;

        public FpgAimIndicatorBaseState BaseState => baseState;

        public bool IsShotFeedbackActive => shotTimeRemaining > 0f;

        public bool IsHitFeedbackActive => hitTimeRemaining > 0f;

        public float ShotTimeRemaining => shotTimeRemaining;

        public float HitTimeRemaining => hitTimeRemaining;

        public float ReloadProgress => reloadProgress;

        public float PrimarySpreadTangent => primarySpreadTangent;

        public float SecondaryAreaRadius => secondaryAreaRadius;

        public FpgResolvedAimContext ResolvedAimContext => resolvedAimContext;

        public long FrozenAimVersion => frozenAimVersion;

        private void Awake()
        {
            reticleRect = transform as RectTransform;
            CacheStrokes();
            ApplyThreeCProfileIfPresent();
            viewport = CombatAimViewportMath.ClampToSafeArea(viewport, safeViewport);
            ApplyViewportToRect();
            ApplyFeedbackVisual();
            ApplyChargeProgressVisual();
            ApplyLayeredVisual();
        }

        private void OnEnable()
        {
            ApplyThreeCProfileIfPresent();
            viewport = CombatAimViewportMath.ClampToSafeArea(viewport, safeViewport);
            ApplyViewportToRect();
            ApplyLayeredVisual();
            if (lockSystemCursor && Application.isFocused)
            {
                SetSystemCursorLocked(true);
            }
        }

        private void Update()
        {
            AdvanceFeedback(Time.unscaledDeltaTime, inputFrozen);

            if (lockSystemCursor && Application.isFocused && !systemCursorLocked)
            {
                SetSystemCursorLocked(true);
            }

            if (inputFrozen)
            {
                return;
            }

            if (!inputAdapter.TryReadAimInput(
                    gamepadReticleDeadzone,
                    out ProjectWideAimInputSnapshot input))
            {
                aimHeld = false;
                return;
            }

            aimHeld = input.AimHeld;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            if (input.LookDelta.sqrMagnitude > 0f)
            {
                Vector2 nextViewport = input.InputChannel
                    == ProjectWideAimInputChannel.Gamepad
                        ? CombatAimViewportMath.ApplyGamepadInput(
                            viewport,
                            input.LookDelta,
                            gamepadReticleSpeed,
                            gamepadReticleDeadzone,
                            gamepadReticleResponseExponent,
                            Time.unscaledDeltaTime,
                            safeViewport)
                        : CombatAimViewportMath.ApplyMouseDelta(
                            viewport,
                            input.LookDelta,
                            mouseReferenceResolution,
                            mouseReticleSensitivity,
                            safeViewport);
                SetViewport(nextViewport);
                return;
            }

            if (!systemCursorLocked && input.HasPoint)
            {
                SetViewport(CombatAimViewportMath.ApplyScreenPoint(
                    viewport,
                    input.Point,
                    screenSize,
                    safeViewport));
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                SetSystemCursorLocked(false);
                return;
            }

            if (resetOnApplicationFocus)
            {
                ResetToCenter();
            }

            if (lockSystemCursor)
            {
                SetSystemCursorLocked(true);
            }
        }

        private void OnDisable()
        {
            inputAdapter.Dispose();
            aimHeld = false;
            ResetFeedback();
            SetChargeProgress(false, 0f);
            hiddenState = true;
            ResolveBaseStateAndApply();
            SetSystemCursorLocked(false);
        }

        public bool TryGetViewport(out Vector2 result)
        {
            result = viewport;
            return CombatAimViewportMath.IsInsideSafeArea(result, safeViewport);
        }

        /// <summary>
        /// Explicitly changes the virtual cursor without touching combat state.
        /// This is used by restart/focus lifecycle and test harnesses.
        /// </summary>
        public void SetViewport(Vector2 value)
        {
            viewport = CombatAimViewportMath.ClampToSafeArea(value, safeViewport);
            ApplyViewportToRect();
        }

        /// <summary>
        /// Applies the planner-owned 2.5D free-aim settings without touching the
        /// battle session. This is safe to call during scene binding and on a
        /// restart because it only changes cursor presentation input.
        /// </summary>
        public bool TrySetThreeCProfile(D0ThreeCProfile profile, out string error)
        {
            if (profile == null)
            {
                error = "CombatAimReticle requires a D0 3C profile.";
                return false;
            }

            if (!profile.TryValidate(out error))
            {
                return false;
            }

            threeCProfile = profile;
            mouseReticleSensitivity = profile.MouseReticleSensitivity;
            mouseReferenceResolution = profile.MouseReferenceResolution;
            gamepadReticleSpeed = profile.GamepadReticleSpeed;
            gamepadReticleDeadzone = profile.GamepadReticleDeadzone;
            gamepadReticleResponseExponent =
                profile.GamepadReticleResponseExponent;
            safeViewport = profile.ReticleSafeViewport;
            viewport = CombatAimViewportMath.ClampToSafeArea(viewport, safeViewport);
            ApplyViewportToRect();
            error = string.Empty;
            return true;
        }

        public void ResetToCenter()
        {
            SetViewport(CombatAimViewportMath.Center);
        }

        /// <summary>
        /// Lets a pause or overlay freeze pointer sampling while preserving its
        /// exact visual position for a later resume.
        /// </summary>
        public void SetInputFrozen(bool frozen)
        {
            inputFrozen = frozen;
        }

        public bool TrySetPresentationProfile(
            CombatPresentationProfile profile,
            out string error)
        {
            error = string.Empty;
            if (profile == null)
            {
                error = "CombatAimReticle requires a valid presentation profile.";

                return false;
            }

            if (!profile.TryValidateStatic(out error))
            {

                return false;
            }

            if (aimIndicatorStyle == null)
            {
                SetLegacyGraphicsEnabled(true);
            }
            ApplyFeedbackVisual();
            ApplyChargeProgressVisual();
            error = string.Empty;
            return true;
        }

        public bool TryApplyShootingPreview(
            in FpgShootingTuningSnapshot snapshot,
            out string error)
        {
            if (!snapshot.TryValidate(out error))
            {
                return false;
            }

            if (threeCProfile != null
                && !ReferenceEquals(
                    threeCProfile,
                    snapshot.ThreeCProfile))
            {
                error = "Reticle shooting preview does not match the active 3C profile.";
                return false;
            }

            PlayerAimIndicatorPresentationDefinition previewStyle =
                snapshot.Weapon.AimIndicator;
            LayeredAimIndicatorGraphic graphic = EnsureLayeredGraphic();
            if (previewStyle == null
                || graphic == null
                || !graphic.TryApplyStyle(previewStyle, out error))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Reticle shooting preview requires a valid weapon aim-indicator style."
                    : error;
                return false;
            }

            mouseReticleSensitivity = snapshot.MouseReticleSensitivity;
            mouseReferenceResolution = snapshot.MouseReferenceResolution;
            gamepadReticleSpeed = snapshot.GamepadReticleSpeed;
            gamepadReticleDeadzone = snapshot.GamepadReticleDeadzone;
            gamepadReticleResponseExponent =
                snapshot.GamepadReticleResponseExponent;
            safeViewport = snapshot.ReticleSafeViewport;
            viewport = CombatAimViewportMath.ClampToSafeArea(
                viewport,
                safeViewport);
            primarySpreadTangent = snapshot.PrimarySpreadTangent;
            secondaryAreaRadius = snapshot.SecondaryAreaRadius;
            aimIndicatorStyle = previewStyle;
            ApplyViewportToRect();
            ApplyLayeredVisual();
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Binds the selected weapon's only formal reticle style. Combat-feel
        /// values remain owned by the selected combat-feel asset.
        /// </summary>
        public bool TrySetAimIndicatorPresentation(
            PlayerAimIndicatorPresentationDefinition style,
            D0CombatFeelProfile combatFeel,
            Camera camera,
            out string error)
        {
            error = string.Empty;
            if (style == null || !style.TryValidate(out error))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "CombatAimReticle requires a valid weapon aim-indicator style."
                    : error;
                return false;
            }

            if (combatFeel == null || !combatFeel.TryValidate(out error))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "CombatAimReticle requires a valid combat-feel profile."
                    : error;
                return false;
            }

            if (camera == null || camera.orthographic
                || !IsFinite(camera.fieldOfView)
                || camera.fieldOfView <= 0f || camera.fieldOfView >= 180f)
            {
                error = "CombatAimReticle requires a perspective projection camera.";
                return false;
            }

            LayeredAimIndicatorGraphic graphic = EnsureLayeredGraphic();
            if (graphic == null || !graphic.TryApplyStyle(style, out error))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "CombatAimReticle could not prepare its layered graphic."
                    : error;
                return false;
            }

            aimIndicatorStyle = style;
            projectionCamera = camera;
            primarySpreadTangent = combatFeel.PrimaryBaseSpreadTangent;
            secondaryAreaRadius = combatFeel.SecondaryAreaRadius;
            SetLegacyGraphicsEnabled(false);
            ApplyLayeredVisual();
            error = string.Empty;
            return true;
        }

        public void SetFormalPresentation(
            in FpgFormalPlayerPresentationSnapshot snapshot)
        {
            FpgAimIndicatorBaseState requested = snapshot.AimIndicatorBaseState;
            hiddenState = requested == FpgAimIndicatorBaseState.Hidden;
            reloadingState = requested == FpgAimIndicatorBaseState.Reloading;
            currentCoverBlockedState = requested
                == FpgAimIndicatorBaseState.CurrentCoverBlocked;
            unavailableState = requested
                == FpgAimIndicatorBaseState.Unavailable;
            enemyState = requested == FpgAimIndicatorBaseState.Enemy;
            reloadProgress = reloadingState
                ? Mathf.Clamp01(snapshot.ReloadProgress01)
                : 0f;
            frozenAimVersion = snapshot.FrozenAimVersion;
            if (snapshot.PrimarySpreadTangent > 0f
                || primarySpreadTangent <= 0f)
            {
                primarySpreadTangent = snapshot.PrimarySpreadTangent;
            }
            if (snapshot.SecondaryAreaRadius > 0f)
            {
                secondaryAreaRadius = snapshot.SecondaryAreaRadius;
            }

            SetChargeProgress(
                snapshot.IsSecondaryCharging,
                snapshot.SecondaryChargeProgress);
            ResolveBaseStateAndApply();
        }

        public void SetResolvedAimContext(in FpgResolvedAimContext context)
        {
            resolvedAimContext = context.IsValid
                ? context
                : FpgResolvedAimContext.Invalid;
        }

        public static FpgAimIndicatorBaseState ResolveBaseState(
            bool hidden,
            bool reloading,
            bool currentCoverBlocked,
            bool unavailable,
            bool enemy)
        {
            if (hidden)
            {
                return FpgAimIndicatorBaseState.Hidden;
            }

            if (reloading)
            {
                return FpgAimIndicatorBaseState.Reloading;
            }

            if (currentCoverBlocked)
            {
                return FpgAimIndicatorBaseState.CurrentCoverBlocked;
            }

            if (unavailable)
            {
                return FpgAimIndicatorBaseState.Unavailable;
            }

            return enemy
                ? FpgAimIndicatorBaseState.Enemy
                : FpgAimIndicatorBaseState.Normal;
        }

        public static float CalculateReferencePixelRadius(
            float tangent,
            float verticalFieldOfViewDegrees,
            float referenceHeight)
        {
            if (!IsFinite(tangent) || tangent <= 0f
                || !IsFinite(verticalFieldOfViewDegrees)
                || verticalFieldOfViewDegrees <= 0f
                || verticalFieldOfViewDegrees >= 180f
                || !IsFinite(referenceHeight) || referenceHeight <= 0f)
            {
                return 0f;
            }

            float halfViewTangent = Mathf.Tan(
                verticalFieldOfViewDegrees * 0.5f * Mathf.Deg2Rad);
            return halfViewTangent > 0.000001f
                ? tangent / halfViewTangent * referenceHeight * 0.5f
                : 0f;
        }

        public static float CalculateReferenceDistancePixelRadius(
            float worldRadius,
            float referenceDistance,
            float verticalFieldOfViewDegrees,
            float referenceHeight)
        {
            if (!IsFinite(worldRadius) || worldRadius <= 0f
                || !IsFinite(referenceDistance) || referenceDistance <= 0f)
            {
                return 0f;
            }

            return CalculateReferencePixelRadius(
                worldRadius / referenceDistance,
                verticalFieldOfViewDegrees,
                referenceHeight);
        }

        public void SetChargeProgress(bool active, float normalized)
        {
            chargeProgressActive = active;
            chargeProgress = active && !float.IsNaN(normalized)
                && !float.IsInfinity(normalized)
                    ? Mathf.Clamp01(normalized)
                    : 0f;
            ApplyChargeProgressVisual();
            ApplyLayeredVisual();
        }

        public void SetTargetState(FpgReticleTargetState state)
        {
            if (!System.Enum.IsDefined(typeof(FpgReticleTargetState), state))
            {
                state = FpgReticleTargetState.Idle;
            }

            bool nextBlocked = state == FpgReticleTargetState.Blocked;
            bool nextEnemy = state == FpgReticleTargetState.Hittable;
            if (targetState == state
                && currentCoverBlockedState == nextBlocked
                && enemyState == nextEnemy)
            {
                return;
            }

            targetState = state;
            currentCoverBlockedState = nextBlocked;
            enemyState = nextEnemy;
            ResolveBaseStateAndApply();
            if (pulseState == FpgReticlePulseState.None)
            {
                ApplyFeedbackVisual();
            }
        }

        public void PresentShot()
        {
            float duration = aimIndicatorStyle == null
                ? presentation == null ? 0f : presentation.ShotPulseDuration
                : aimIndicatorStyle.ShotDuration;
            if (duration <= 0f)
            {
                return;
            }

            pulseState = FpgReticlePulseState.Shot;
            pulseTimeRemaining = duration;
            shotTimeRemaining = duration;
            ApplyFeedbackVisual();
            ApplyLayeredVisual();
        }

        public void PresentHit()
        {
            PresentHit(0L);
        }

        public void PresentHit(long attackId)
        {
            if (attackId > 0L && attackId == lastHitAttackId)
            {
                return;
            }

            float duration = aimIndicatorStyle == null
                ? presentation == null ? 0f : presentation.HitPulseDuration
                : aimIndicatorStyle.HitDuration;
            if (duration <= 0f)
            {
                return;
            }

            if (attackId > 0L)
            {
                lastHitAttackId = attackId;
            }
            pulseState = FpgReticlePulseState.Hit;
            pulseTimeRemaining = duration;
            hitTimeRemaining = duration;
            ApplyFeedbackVisual();
            ApplyLayeredVisual();
        }

        public void AdvanceFeedback(float deltaTime, bool paused)
        {
            if (paused || deltaTime <= 0f || float.IsNaN(deltaTime)
                || float.IsInfinity(deltaTime))
            {
                return;
            }

            bool changed = false;
            if (shotTimeRemaining > 0f)
            {
                shotTimeRemaining = Mathf.Max(0f, shotTimeRemaining - deltaTime);
                changed = true;
            }

            if (hitTimeRemaining > 0f)
            {
                hitTimeRemaining = Mathf.Max(0f, hitTimeRemaining - deltaTime);
                changed = true;
            }

            if (reloadingState && aimIndicatorStyle != null
                && aimIndicatorStyle.ReloadSpinDegreesPerSecond > 0f)
            {
                reloadLoopPhase = Mathf.Repeat(
                    reloadLoopPhase
                    + deltaTime * aimIndicatorStyle.ReloadSpinDegreesPerSecond
                        / 360f,
                    1f);
                changed = true;
            }

            FpgReticlePulseState nextPulse = hitTimeRemaining > 0f
                ? FpgReticlePulseState.Hit
                : shotTimeRemaining > 0f
                    ? FpgReticlePulseState.Shot
                    : FpgReticlePulseState.None;
            float nextRemaining = nextPulse == FpgReticlePulseState.Hit
                ? hitTimeRemaining
                : nextPulse == FpgReticlePulseState.Shot
                    ? shotTimeRemaining
                    : 0f;
            if (pulseState != nextPulse
                || !Mathf.Approximately(pulseTimeRemaining, nextRemaining))
            {
                pulseState = nextPulse;
                pulseTimeRemaining = nextRemaining;
                changed = true;
            }

            if (changed)
            {
                ApplyFeedbackVisual();
                ApplyLayeredVisual();
            }
        }

        public void ResetFeedback()
        {
            targetState = FpgReticleTargetState.Idle;
            pulseState = FpgReticlePulseState.None;
            pulseTimeRemaining = 0f;
            shotTimeRemaining = 0f;
            hitTimeRemaining = 0f;
            lastHitAttackId = 0L;
            hiddenState = false;
            reloadingState = false;
            unavailableState = false;
            currentCoverBlockedState = false;
            enemyState = false;
            reloadProgress = 0f;
            reloadLoopPhase = 0f;
            ApplyFeedbackVisual();
            SetChargeProgress(false, 0f);
            ResolveBaseStateAndApply();
        }

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (reticleRect == null && !(transform is RectTransform))
            {
                error = "CombatAimReticle requires a RectTransform.";
                return false;
            }

            if (!IsFinite(mouseReticleSensitivity)
                || mouseReticleSensitivity <= 0f
                || !IsFinite(mouseReferenceResolution.x)
                || mouseReferenceResolution.x <= 0f
                || !IsFinite(mouseReferenceResolution.y)
                || mouseReferenceResolution.y <= 0f
                || !IsFinite(gamepadReticleSpeed)
                || gamepadReticleSpeed <= 0f
                || !IsFinite(gamepadReticleDeadzone)
                || gamepadReticleDeadzone < 0f
                || gamepadReticleDeadzone >= 1f
                || !IsFinite(gamepadReticleResponseExponent)
                || gamepadReticleResponseExponent <= 0f)
            {
                error =
                    "CombatAimReticle mouse and gamepad input settings must be finite and valid.";
                return false;
            }

            if (!CombatAimViewportMath.IsValidSafeArea(safeViewport)
                || !CombatAimViewportMath.IsInsideSafeArea(viewport, safeViewport))
            {
                error = "CombatAimReticle viewport must stay inside its safe area.";
                return false;
            }

            if (chargeProgressImage != null
                && (!chargeProgressImage.transform.IsChildOf(transform)
                    || chargeProgressImage.type != Image.Type.Filled
                    || chargeProgressImage.fillMethod
                        != Image.FillMethod.Radial360))
            {
                error = "CombatAimReticle charge progress Image must be a Radial360 child.";
                return false;
            }

            if (aimIndicatorStyle != null
                && (layeredGraphic == null
                    || projectionCamera == null
                    || !layeredGraphic.TryValidate(out error)))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "CombatAimReticle layered presentation is incomplete."
                    : error;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void ApplyFeedbackVisual()
        {
            if (aimIndicatorStyle != null)
            {
                ApplyLayeredVisual();
                return;
            }

            if (presentation == null)
            {
                return;
            }

            Color color;
            float size;
            switch (pulseState)
            {
                case FpgReticlePulseState.Hit:
                    color = presentation.HitColor;
                    size = presentation.HitPulseSize;
                    break;
                case FpgReticlePulseState.Shot:
                    color = presentation.ShotColor;
                    size = presentation.ShotPulseSize;
                    break;
                default:
                    switch (targetState)
                    {
                        case FpgReticleTargetState.Hittable:
                            color = presentation.HittableColor;
                            size = presentation.HittableSize;
                            break;
                        case FpgReticleTargetState.Blocked:
                            color = presentation.BlockedColor;
                            size = presentation.BlockedSize;
                            break;
                        default:
                            color = presentation.IdleColor;
                            size = presentation.IdleSize;
                            break;
                    }
                    break;
            }

            if (reticleRect == null)
            {
                reticleRect = transform as RectTransform;
            }

            if (reticleRect != null)
            {
                reticleRect.sizeDelta = new Vector2(size, size);
            }

            if (strokes == null || strokes.Length == 0
                || strokeBaseSizes == null
                || strokeBaseSizes.Length != strokes.Length)
            {
                CacheStrokes();
            }

            for (int index = 0; index < strokes.Length; index++)
            {
                Graphic stroke = strokes[index];
                if (stroke == null)
                {
                    continue;
                }

                stroke.color = color;
                RectTransform strokeRect = stroke.rectTransform;
                if (strokeRect == null || strokeRect == reticleRect)
                {
                    continue;
                }

                Vector2 baseSize = strokeBaseSizes[index];
                strokeRect.sizeDelta = Mathf.Abs(baseSize.x)
                    >= Mathf.Abs(baseSize.y)
                        ? new Vector2(size, baseSize.y)
                        : new Vector2(baseSize.x, size);
            }
        }

        private void CacheStrokes()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            int strokeCount = 0;
            for (int index = 0; index < graphics.Length; index++)
            {
                if (graphics[index] != null
                    && graphics[index] != chargeProgressImage
                    && graphics[index] != layeredGraphic)
                {
                    strokeCount++;
                }
            }

            strokes = new Graphic[strokeCount];
            strokeBaseSizes = new Vector2[strokeCount];
            int strokeIndex = 0;
            for (int index = 0; index < graphics.Length; index++)
            {
                Graphic stroke = graphics[index];
                if (stroke == null || stroke == chargeProgressImage
                    || stroke == layeredGraphic)
                {
                    continue;
                }

                strokes[strokeIndex] = stroke;
                strokeBaseSizes[strokeIndex] = stroke.rectTransform.sizeDelta;
                strokeIndex++;
            }
        }

        private void ApplyChargeProgressVisual()
        {
            if (chargeProgressImage == null)
            {
                return;
            }

            if (aimIndicatorStyle != null)
            {
                chargeProgressImage.gameObject.SetActive(false);
                return;
            }

            chargeProgressImage.fillAmount = chargeProgress;
            bool visible = chargeProgressActive && presentation != null;
            chargeProgressImage.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            Color color = presentation.ChargeRingColor;
            color.a *= presentation.ChargeRingAlpha;
            chargeProgressImage.color = color;
            RectTransform chargeRect = chargeProgressImage.rectTransform;
            if (chargeRect != null)
            {
                float size = presentation.ChargeRingSize;
                chargeRect.sizeDelta = new Vector2(size, size);
            }
        }

        private void ResolveBaseStateAndApply()
        {
            baseState = ResolveBaseState(
                hiddenState,
                reloadingState,
                currentCoverBlockedState,
                unavailableState,
                enemyState);
            ApplyLayeredVisual();
        }

        private void ApplyLayeredVisual()
        {
            if (aimIndicatorStyle == null || layeredGraphic == null)
            {
                return;
            }

            Vector2 referenceResolution = ResolveReferenceResolution();
            float referenceHeight = referenceResolution.y;
            float verticalFieldOfView = projectionCamera == null
                ? 60f
                : projectionCamera.fieldOfView;
            bool secondaryVisible = chargeProgressActive
                && baseState != FpgAimIndicatorBaseState.Hidden
                && baseState != FpgAimIndicatorBaseState.Reloading;
            bool spreadVisible = !secondaryVisible
                && baseState != FpgAimIndicatorBaseState.Hidden
                && baseState != FpgAimIndicatorBaseState.Reloading;
            float spreadRadius = 0f;
            if (spreadVisible)
            {
                // Keep the HUD calibrated at 20 m; live target projection makes
                // muzzle-camera parallax change the ring size with distance.
                float referenceWorldRadius = primarySpreadTangent
                    * PrimarySpreadReferenceDistance;
                spreadRadius = CalculateReferenceDistancePixelRadius(
                    referenceWorldRadius,
                    PrimarySpreadReferenceDistance,
                    verticalFieldOfView,
                    referenceHeight);
            }
            float secondaryTangent = secondaryAreaRadius > 0f
                ? secondaryAreaRadius
                    / aimIndicatorStyle.SecondaryReferenceDistance
                : 0f;
            float secondaryRadius = secondaryVisible
                ? CalculateReferencePixelRadius(
                    secondaryTangent,
                    verticalFieldOfView,
                    referenceHeight)
                : 0f;
            float shotDuration = aimIndicatorStyle.ShotDuration;
            float hitDuration = aimIndicatorStyle.HitDuration;
            float shotRatio = shotDuration > 0f
                ? Mathf.Clamp01(shotTimeRemaining / shotDuration)
                : 0f;
            float hitRatio = hitDuration > 0f
                ? Mathf.Clamp01(hitTimeRemaining / hitDuration)
                : 0f;
            layeredGraphic.SetLayeredPresentation(
                baseState,
                shotRatio,
                1f - shotRatio,
                hitRatio,
                1f - hitRatio,
                reloadProgress,
                reloadLoopPhase,
                spreadRadius,
                secondaryVisible,
                secondaryRadius,
                chargeProgress);
        }

        private LayeredAimIndicatorGraphic EnsureLayeredGraphic()
        {
            if (layeredGraphic == null)
            {
                layeredGraphic = GetComponent<LayeredAimIndicatorGraphic>();
            }

            if (layeredGraphic == null)
            {
                layeredGraphic = gameObject.AddComponent<
                    LayeredAimIndicatorGraphic>();
            }

            layeredGraphic.raycastTarget = false;
            CacheStrokes();
            return layeredGraphic;
        }

        private void SetLegacyGraphicsEnabled(bool enabled)
        {
            CacheStrokes();
            for (int index = 0; index < strokes.Length; index++)
            {
                if (strokes[index] != null)
                {
                    strokes[index].enabled = enabled;
                }
            }

            if (chargeProgressImage != null && !enabled)
            {
                chargeProgressImage.gameObject.SetActive(false);
            }
        }

        private Vector2 ResolveReferenceResolution()
        {
            CanvasScaler scaler = GetComponentInParent<CanvasScaler>();
            if (scaler != null && IsFinite(scaler.referenceResolution)
                && scaler.referenceResolution.x > 0f
                && scaler.referenceResolution.y > 0f)
            {
                return scaler.referenceResolution;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas == null
                ? null
                : canvas.transform as RectTransform;
            if (canvasRect != null && IsFinite(canvasRect.rect.size)
                && canvasRect.rect.width > 0f
                && canvasRect.rect.height > 0f)
            {
                return canvasRect.rect.size;
            }

            return threeCProfile != null
                && threeCProfile.MouseReferenceResolution.x > 0f
                && threeCProfile.MouseReferenceResolution.y > 0f
                    ? threeCProfile.MouseReferenceResolution
                    : new Vector2(1920f, 1080f);
        }

        private void ApplyViewportToRect()
        {
            if (reticleRect == null)
            {
                reticleRect = transform as RectTransform;
            }

            if (reticleRect == null)
            {
                return;
            }

            reticleRect.anchorMin = viewport;
            reticleRect.anchorMax = viewport;
            reticleRect.anchoredPosition = Vector2.zero;
        }

        private void ApplyThreeCProfileIfPresent()
        {
            if (threeCProfile == null)
            {
                return;
            }

            if (!threeCProfile.TryValidate(out _))
            {
                return;
            }

            mouseReticleSensitivity = threeCProfile.MouseReticleSensitivity;
            mouseReferenceResolution = threeCProfile.MouseReferenceResolution;
            gamepadReticleSpeed = threeCProfile.GamepadReticleSpeed;
            gamepadReticleDeadzone = threeCProfile.GamepadReticleDeadzone;
            gamepadReticleResponseExponent =
                threeCProfile.GamepadReticleResponseExponent;
            safeViewport = threeCProfile.ReticleSafeViewport;
        }

        private void SetSystemCursorLocked(bool locked)
        {
            if (systemCursorLocked == locked)
            {
                return;
            }

            systemCursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
    }
}
