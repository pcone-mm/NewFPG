using UnityEngine;
using UnityEngine.InputSystem;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Owns the D0 virtual free cursor. The operating-system cursor remains
    /// locked while mouse delta moves this reticle inside the authored safe area.
    /// BattleSessionHost consumes only the normalized viewport coordinate.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class CombatAimReticle : MonoBehaviour, ICombatAimViewportSource
    {
        [SerializeField]
        private BattleSessionHost sessionHost;

        [SerializeField, Min(0.01f)]
        private float pointerSensitivity = 1f;

        private D0ThreeCProfile threeCProfile;

        [SerializeField]
        private bool lockSystemCursor = true;

        [SerializeField]
        private bool resetOnApplicationFocus = true;

        private RectTransform reticleRect;
        private Vector2 viewport = CombatAimViewportMath.Center;
        private Rect safeViewport = new Rect(
            CombatAimViewportMath.SafeMinimumX,
            CombatAimViewportMath.SafeMinimumY,
            CombatAimViewportMath.SafeMaximumX - CombatAimViewportMath.SafeMinimumX,
            CombatAimViewportMath.SafeMaximumY - CombatAimViewportMath.SafeMinimumY);
        private bool inputFrozen;
        private bool systemCursorLocked;

        public BattleSessionHost SessionHost => sessionHost;

        public Vector2 Viewport => viewport;

        public bool IsInputFrozen => inputFrozen;

        public bool IsSystemCursorLocked => systemCursorLocked;

        public D0ThreeCProfile ThreeCProfile => threeCProfile;

        public Rect SafeViewport => safeViewport;

        private void Awake()
        {
            reticleRect = transform as RectTransform;
            ApplyThreeCProfileIfPresent();
            viewport = CombatAimViewportMath.ClampToSafeArea(viewport, safeViewport);
            ApplyViewportToRect();
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
            if (lockSystemCursor && Application.isFocused && !systemCursorLocked)
            {
                SetSystemCursorLocked(true);
            }

            if (inputFrozen
                || (sessionHost != null && !sessionHost.IsSessionRunning))
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude <= 0f)
            {
                return;
            }

            SetViewport(CombatAimViewportMath.ApplyMouseDelta(
                viewport,
                delta,
                new Vector2(Screen.width, Screen.height),
                pointerSensitivity,
                safeViewport));
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
        /// battle session. This is safe to call from the scene installer and on
        /// a restart because it only changes cursor presentation input.
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

            error = string.Empty;
            return true;
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
