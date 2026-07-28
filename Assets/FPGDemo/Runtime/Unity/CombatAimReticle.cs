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
        [SerializeField, Min(0.01f)]
        private float pointerSensitivity = 1f;

        private D0ThreeCProfile threeCProfile;

        [SerializeField]
        private bool lockSystemCursor = true;

        [SerializeField]
        private bool resetOnApplicationFocus = true;

        [SerializeField]
        [Tooltip("Radial360 Image used only for secondary-charge progress.")]
        private Image chargeProgressImage;

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
        private bool chargeProgressActive;
        private float chargeProgress;
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

        private void Awake()
        {
            reticleRect = transform as RectTransform;
            CacheStrokes();
            ApplyThreeCProfileIfPresent();
            viewport = CombatAimViewportMath.ClampToSafeArea(viewport, safeViewport);
            ApplyViewportToRect();
            ApplyFeedbackVisual();
            ApplyChargeProgressVisual();
        }

        private void OnEnable()
        {
            ApplyThreeCProfileIfPresent();
            viewport = CombatAimViewportMath.ClampToSafeArea(viewport, safeViewport);
            ApplyViewportToRect();
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
                    out ProjectWideAimInputSnapshot input))
            {
                aimHeld = false;
                return;
            }

            aimHeld = input.AimHeld;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            if (input.LookDelta.sqrMagnitude > 0f)
            {
                SetViewport(CombatAimViewportMath.ApplyMouseDelta(
                    viewport,
                    input.LookDelta,
                    screenSize,
                    pointerSensitivity,
                    safeViewport));
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
            aimHeld = false;
            ResetFeedback();
            SetChargeProgress(false, 0f);
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
            pointerSensitivity = profile.ReticleSensitivity;
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

            presentation = profile.FormalReticle;
            ApplyFeedbackVisual();
            ApplyChargeProgressVisual();
            error = string.Empty;
            return true;
        }

        public void SetChargeProgress(bool active, float normalized)
        {
            chargeProgressActive = active;
            chargeProgress = active && !float.IsNaN(normalized)
                && !float.IsInfinity(normalized)
                    ? Mathf.Clamp01(normalized)
                    : 0f;
            ApplyChargeProgressVisual();
        }

        public void SetTargetState(FpgReticleTargetState state)
        {
            if (!System.Enum.IsDefined(typeof(FpgReticleTargetState), state))
            {
                state = FpgReticleTargetState.Idle;
            }

            if (targetState == state)
            {
                return;
            }

            targetState = state;
            if (pulseState == FpgReticlePulseState.None)
            {
                ApplyFeedbackVisual();
            }
        }

        public void PresentShot()
        {
            if (presentation == null)
            {
                return;
            }

            pulseState = FpgReticlePulseState.Shot;
            pulseTimeRemaining = presentation.ShotPulseDuration;
            ApplyFeedbackVisual();
        }

        public void PresentHit()
        {
            if (presentation == null)
            {
                return;
            }

            pulseState = FpgReticlePulseState.Hit;
            pulseTimeRemaining = presentation.HitPulseDuration;
            ApplyFeedbackVisual();
        }

        public void AdvanceFeedback(float deltaTime, bool paused)
        {
            if (paused || pulseState == FpgReticlePulseState.None
                || deltaTime <= 0f || float.IsNaN(deltaTime)
                || float.IsInfinity(deltaTime))
            {
                return;
            }

            pulseTimeRemaining = Mathf.Max(0f, pulseTimeRemaining - deltaTime);
            if (pulseTimeRemaining > 0f)
            {
                return;
            }

            pulseState = FpgReticlePulseState.None;
            ApplyFeedbackVisual();
        }

        public void ResetFeedback()
        {
            targetState = FpgReticleTargetState.Idle;
            pulseState = FpgReticlePulseState.None;
            pulseTimeRemaining = 0f;
            ApplyFeedbackVisual();
            SetChargeProgress(false, 0f);
        }

        public bool TryValidate(out string error)
        {
            if (reticleRect == null && !(transform is RectTransform))
            {
                error = "CombatAimReticle requires a RectTransform.";
                return false;
            }

            if (pointerSensitivity <= 0f || float.IsNaN(pointerSensitivity)
                || float.IsInfinity(pointerSensitivity))
            {
                error = "CombatAimReticle pointer sensitivity must be finite and positive.";
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

            error = string.Empty;
            return true;
        }

        private void ApplyFeedbackVisual()
        {
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
                    && graphics[index] != chargeProgressImage)
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
                if (stroke == null || stroke == chargeProgressImage)
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

            pointerSensitivity = threeCProfile.ReticleSensitivity;
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
    }
}
