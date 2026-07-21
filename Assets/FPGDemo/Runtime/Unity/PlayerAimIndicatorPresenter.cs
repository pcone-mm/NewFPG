using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum PlayerAimIndicatorState
    {
        Resting = 0,
        Aiming = 1,
        Shooting = 2,
        HitConfirmed = 3
    }

    /// <summary>
    /// Presentation-only bridge for the player aim indicator. It observes the
    /// committed shot feed and selected-hit stream, and never reads raw input,
    /// performs a query or mutates the battle session.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatAimReticle))]
    [RequireComponent(typeof(LayeredAimIndicatorGraphic))]
    public sealed class PlayerAimIndicatorPresenter : MonoBehaviour
    {
        [SerializeField]
        private BattleSessionHost sessionHost;

        [SerializeField]
        private LayeredAimIndicatorGraphic indicatorGraphic;

        private D0WeaponDefinition weaponDefinition;

        private readonly PlayerShotPresentationCursor shotCursor =
            new PlayerShotPresentationCursor();
        private readonly SelectedAttackHitCursor selectedHitCursor =
            new SelectedAttackHitCursor();

        private IPlayerShotPresentationFeed boundShotFeed;
        private BattleSession boundSession;
        private PlayerShotPresentationEvent[] shotBuffer;
        private SelectedAttackHit[] selectedHitBuffer;
        private PlayerAimIndicatorPresentationDefinition style;
        private float shotRemaining;
        private float hitRemaining;
        private bool isAiming;

        public BattleSessionHost SessionHost => sessionHost;
        public LayeredAimIndicatorGraphic IndicatorGraphic => indicatorGraphic;
        public D0WeaponDefinition WeaponDefinition => weaponDefinition;
        public PlayerAimIndicatorPresentationDefinition Style => style;
        public IPlayerShotPresentationFeed BoundShotFeed => boundShotFeed;
        public BattleSession BoundSession => boundSession;
        public bool IsAiming => isAiming;
        public bool IsShotFeedbackActive => shotRemaining > 0f;
        public bool IsHitFeedbackActive => hitRemaining > 0f;
        public float ShotRemaining => shotRemaining;
        public float HitRemaining => hitRemaining;
        public int ShotFeedbackCount { get; private set; }
        public int HitFeedbackCount { get; private set; }
        public int PresentationFaultCount { get; private set; }
        public int ShotFeedGapCount => shotCursor.GapCount;

        public PlayerAimIndicatorState CurrentState
        {
            get
            {
                if (IsHitFeedbackActive)
                {
                    return PlayerAimIndicatorState.HitConfirmed;
                }

                if (IsShotFeedbackActive)
                {
                    return PlayerAimIndicatorState.Shooting;
                }

                return isAiming
                    ? PlayerAimIndicatorState.Aiming
                    : PlayerAimIndicatorState.Resting;
            }
        }

        private void Awake()
        {
            CacheGraphic();
            TryApplyConfiguredStyle(out _);
            WriteVisual();
        }

        private void OnEnable()
        {
            CacheGraphic();
            ResetBindings();
            TryApplyConfiguredStyle(out _);
            WriteVisual();
        }

        private void OnDisable()
        {
            ResetBindings();
            isAiming = false;
            WriteVisual();
            if (indicatorGraphic != null)
            {
                indicatorGraphic.enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (indicatorGraphic == null || style == null
                || sessionHost == null)
            {
                return;
            }

            try
            {
                RefreshBindings();
                if (boundSession != null)
                {
                    ConsumeCommittedShots();
                    ConsumeCommittedHits();
                }

                BattleSessionState sessionState = boundSession == null
                    ? BattleSessionState.Disposed
                    : boundSession.State;
                bool presentsCombatPosture = sessionHost.IsInitialized
                    && (sessionState == BattleSessionState.Running
                        || sessionState == BattleSessionState.Paused);
                isAiming = presentsCombatPosture
                    && boundSession.PlayerExposureState == PlayerExposureState.Exposed;
                bool advanceTransientFeedback =
                    sessionState != BattleSessionState.Paused
                    && sessionState != BattleSessionState.Disposed;
                AdvancePresentation(
                    Time.unscaledDeltaTime,
                    isAiming,
                    advanceTransientFeedback);
            }
            catch (System.Exception)
            {
                PresentationFaultCount++;
            }
        }

        public void Configure(
            BattleSessionHost nextSessionHost,
            LayeredAimIndicatorGraphic nextGraphic,
            D0WeaponDefinition nextWeaponDefinition)
        {
            sessionHost = nextSessionHost;
            indicatorGraphic = nextGraphic;
            weaponDefinition = nextWeaponDefinition;
            ResetBindings();
            TryApplyConfiguredStyle(out _);
            WriteVisual();
        }

        public bool TryBindWeapon(
            D0WeaponDefinition nextWeaponDefinition,
            out string error)
        {
            error = string.Empty;
            if (nextWeaponDefinition == null
                || !nextWeaponDefinition.TryValidatePresentation(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error =
                        "Player aim indicator requires a weapon definition.";
                }

                return false;
            }

            weaponDefinition = nextWeaponDefinition;
            return TryApplyConfiguredStyle(out error);
        }

        public bool TryValidate(out string error)
        {
            if (sessionHost == null || indicatorGraphic == null)
            {
                if (indicatorGraphic != null)
                {
                    indicatorGraphic.enabled = false;
                }

                error = "Player aim indicator requires a BattleSessionHost and layered graphic.";
                return false;
            }

            if (indicatorGraphic.gameObject != gameObject)
            {
                indicatorGraphic.enabled = false;
                error = "Player aim indicator graphic must share the reticle GameObject.";
                return false;
            }

            if (GetComponent<CombatAimReticle>() == null)
            {
                indicatorGraphic.enabled = false;
                error = "Player aim indicator must share a CombatAimReticle.";
                return false;
            }

            if (!TryResolveStyle(
                    out PlayerAimIndicatorPresentationDefinition resolvedStyle,
                    out error))
            {
                indicatorGraphic.enabled = false;
                return false;
            }

            if (!indicatorGraphic.TryApplyStyle(resolvedStyle, out error)
                || !indicatorGraphic.TryValidate(out error))
            {
                indicatorGraphic.enabled = false;
                return false;
            }

            indicatorGraphic.enabled = isActiveAndEnabled;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Public presentation seam for editor preview and future event routers.
        /// Runtime feed consumption calls the same path.
        /// </summary>
        public void PlayShotFeedback()
        {
            if (style == null && !TryApplyConfiguredStyle(out _))
            {
                return;
            }

            shotRemaining = style.ShotDuration;
            ShotFeedbackCount++;
            WriteVisual();
        }

        /// <summary>
        /// Plays one aggregate confirmation for an AttackId, regardless of how
        /// many pellets or area targets were selected by that attack.
        /// </summary>
        public void PlayHitFeedback()
        {
            if (style == null && !TryApplyConfiguredStyle(out _))
            {
                return;
            }

            hitRemaining = style.HitDuration;
            HitFeedbackCount++;
            WriteVisual();
        }

        public void AdvancePresentation(
            float deltaTime,
            bool aiming,
            bool advanceTransientFeedback)
        {
            isAiming = aiming;
            if (advanceTransientFeedback)
            {
                float safeDeltaTime = Mathf.Max(0f, deltaTime);
                shotRemaining = Mathf.Max(0f, shotRemaining - safeDeltaTime);
                hitRemaining = Mathf.Max(0f, hitRemaining - safeDeltaTime);
            }

            WriteVisual();
        }

        public void ClearTransientFeedback()
        {
            shotRemaining = 0f;
            hitRemaining = 0f;
            WriteVisual();
        }

        private void CacheGraphic()
        {
            if (indicatorGraphic == null)
            {
                indicatorGraphic = GetComponent<LayeredAimIndicatorGraphic>();
            }

            if (indicatorGraphic != null)
            {
                indicatorGraphic.raycastTarget = false;
            }
        }

        private bool TryApplyConfiguredStyle(out string error)
        {
            CacheGraphic();
            if (!TryResolveStyle(
                    out PlayerAimIndicatorPresentationDefinition resolvedStyle,
                    out error))
            {
                style = null;
                if (indicatorGraphic != null)
                {
                    indicatorGraphic.enabled = false;
                }

                return false;
            }

            if (indicatorGraphic == null
                || !indicatorGraphic.TryApplyStyle(resolvedStyle, out error))
            {
                style = null;
                if (indicatorGraphic != null)
                {
                    indicatorGraphic.enabled = false;
                }

                if (string.IsNullOrEmpty(error))
                {
                    error = "Player aim indicator is missing its layered graphic.";
                }

                return false;
            }

            style = resolvedStyle;
            indicatorGraphic.enabled = isActiveAndEnabled;
            error = string.Empty;
            return true;
        }

        private bool TryResolveStyle(
            out PlayerAimIndicatorPresentationDefinition resolvedStyle,
            out string error)
        {
            resolvedStyle =
                weaponDefinition == null ? null : weaponDefinition.AimIndicator;
            if (resolvedStyle == null)
            {
                error =
                    "Player aim indicator requires weapon-owned style data.";
                return false;
            }

            return resolvedStyle.TryValidate(out error);
        }

        private void RefreshBindings()
        {
            IPlayerShotPresentationFeed nextShotFeed =
                sessionHost.PlayerShotPresentationFeed;
            if (!ReferenceEquals(boundShotFeed, nextShotFeed))
            {
                boundShotFeed = nextShotFeed;
                shotCursor.Reset();
                if (boundShotFeed != null)
                {
                    if (shotBuffer == null
                        || shotBuffer.Length < boundShotFeed.EventCapacity)
                    {
                        shotBuffer =
                            new PlayerShotPresentationEvent[boundShotFeed.EventCapacity];
                    }

                    // The feed is installed before gameplay starts. Reading
                    // from sequence zero preserves a shot committed in the same
                    // frame as the first presenter bind.
                }

                ClearTransientFeedback();
            }

            BattleSession nextSession = sessionHost.Session;
            if (!ReferenceEquals(boundSession, nextSession))
            {
                boundSession = nextSession;
                selectedHitCursor.Reset();
                if (boundSession != null)
                {
                    int capacity = boundSession.SelectedAttackHits.Capacity;
                    if (selectedHitBuffer == null
                        || selectedHitBuffer.Length < capacity)
                    {
                        selectedHitBuffer = new SelectedAttackHit[capacity];
                    }

                    DrainSelectedHitBaseline();
                }

                ClearTransientFeedback();
            }
        }

        private void ConsumeCommittedShots()
        {
            if (boundShotFeed == null || shotBuffer == null)
            {
                return;
            }

            int count = shotCursor.CopyUnread(
                boundShotFeed,
                shotBuffer,
                out bool hasGap);
            if (hasGap)
            {
                shotCursor.ResolveGap(boundShotFeed);
                return;
            }

            for (int index = 0; index < count; index++)
            {
                PlayerShotPresentationEvent shotEvent = shotBuffer[index];
                PlayShotFeedback();
                shotCursor.Commit(shotEvent);
            }
        }

        private void ConsumeCommittedHits()
        {
            if (boundSession == null || selectedHitBuffer == null)
            {
                return;
            }

            int count = selectedHitCursor.CopyUnread(
                boundSession.SelectedAttackHits,
                selectedHitBuffer);
            int index = 0;
            while (index < count)
            {
                AttackId attackId = selectedHitBuffer[index].AttackId;
                bool hasValidHit = false;
                while (index < count
                    && selectedHitBuffer[index].AttackId == attackId)
                {
                    hasValidHit |= selectedHitBuffer[index].IsValid;
                    selectedHitCursor.CommitOne();
                    index++;
                }

                if (hasValidHit)
                {
                    PlayHitFeedback();
                }
            }
        }

        private void DrainSelectedHitBaseline()
        {
            if (boundSession == null || selectedHitBuffer == null)
            {
                return;
            }

            int count = selectedHitCursor.CopyUnread(
                boundSession.SelectedAttackHits,
                selectedHitBuffer);
            for (int index = 0; index < count; index++)
            {
                selectedHitCursor.CommitOne();
            }
        }

        private void ResetBindings()
        {
            boundShotFeed = null;
            boundSession = null;
            shotCursor.Reset();
            selectedHitCursor.Reset();
            ClearTransientFeedback();
        }

        private void WriteVisual()
        {
            if (indicatorGraphic == null || style == null)
            {
                return;
            }

            Color baseColor = isAiming ? style.AimingColor : style.RestingColor;
            float shotProgress = style.ShotDuration <= 0f
                ? 1f
                : 1f - Mathf.Clamp01(shotRemaining / style.ShotDuration);
            float shotPulse = shotRemaining <= 0f
                ? 0f
                : EvaluateShotPulse(shotProgress);
            float radius = Mathf.Lerp(
                style.BaseRadius,
                style.ShotRadius,
                shotPulse);
            Color ringColor = Color.Lerp(
                baseColor,
                style.ShotColor,
                shotPulse);
            float glowAlpha = isAiming
                ? style.AimingGlowAlpha
                : 0f;

            float hitProgress = style.HitDuration <= 0f
                ? 1f
                : 1f - Mathf.Clamp01(hitRemaining / style.HitDuration);
            float hitAlpha = hitRemaining <= 0f
                ? 0f
                : 1f - SmoothStep01(hitProgress);
            indicatorGraphic.SetPresentation(
                radius,
                style.RingThickness,
                ringColor,
                glowAlpha,
                hitAlpha,
                hitProgress);
        }

        private static float EvaluateShotPulse(float progress)
        {
            progress = Mathf.Clamp01(progress);
            const float expansionFraction = 0.28f;
            if (progress < expansionFraction)
            {
                return SmoothStep01(progress / expansionFraction);
            }

            return 1f - SmoothStep01(
                (progress - expansionFraction) / (1f - expansionFraction));
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
