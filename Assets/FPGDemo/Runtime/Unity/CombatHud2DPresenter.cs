using System.Text;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FPG.Demo.Unity
{
    /// <summary>
    /// D0's formal 2.5D HUD. It is a view over FinalSnapshot, threat snapshots
    /// and committed combat trace entries; it never mutates a BattleSession or
    /// makes targeting/physics decisions. The legacy BattleHudPresenter remains
    /// available for non-D0 scene compatibility, while this component owns the
    /// player-facing D0 layout and terminal result surface.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatHud2DPresenter : MonoBehaviour
    {
        public const string DevelopmentPrompt =
            "LMB PRIMARY   RMB AREA ATTACK / HOLD AIM   R RELOAD   ESC PAUSE   F5 RESTART";

        private const string NoThreatLabel = "THREAT | CLEAR";
        private const string ReadyLabel = "READY";
        private const string ReloadLabel = "RELOADING";
        private const string ChargingLabel = "CHARGING";
        private const string RecoveringLabel = "CAST RECOVERY";
        private const string LockedLabel = "LOCKED";
        private const float TerminalFadeSeconds = 0.22f;
        private static readonly Color ClearThreatIndicatorColor =
            new Color(0.42f, 0.72f, 0.82f, 0.75f);
        private static readonly Color ClearThreatTextColor =
            new Color(0.66f, 0.88f, 0.94f, 0.92f);
        private static readonly Color ReadyActionColor =
            new Color(0.68f, 1f, 0.85f, 1f);

        [Header("D0 profile")]
        [SerializeField]
        private CombatPresentationProfile presentationProfile;

        [Header("Top enemy readout")]
        [SerializeField]
        private Image enemyLifeFill;

        [SerializeField]
        private Image enemyBreakFill;

        [SerializeField]
        private Image threatIndicator;

        [SerializeField]
        private Text enemyNameText;

        [SerializeField]
        private Text enemyLifeText;

        [SerializeField]
        private Text enemyBreakText;

        [SerializeField]
        private Text threatText;

        [Header("Bottom player readout")]
        [SerializeField]
        private Image playerLifeFill;

        [SerializeField]
        private Image playerBarrierFill;

        [SerializeField]
        private Image ammoFill;

        [SerializeField]
        private Image actionFill;

        [SerializeField]
        private Text playerNameText;

        [SerializeField]
        private Text playerLifeText;

        [SerializeField]
        private Text playerBarrierText;

        [SerializeField]
        private Text ammoText;

        [SerializeField]
        private Text actionText;

        [Header("Terminal presentation")]
        [SerializeField]
        private GameObject terminalPanel;

        [SerializeField]
        private CanvasGroup terminalCanvasGroup;

        [SerializeField]
        private Text terminalTitleText;

        [SerializeField]
        private Text terminalPromptText;

        [SerializeField]
        private D0TerminalScreenFxPresenter terminalScreenFx;

        [Header("Development overlay")]
        [SerializeField]
        private GameObject developmentOverlay;

        [SerializeField]
        private Text developmentText;

        [SerializeField]
        private BattleSessionDiagnosticsPresenter diagnosticsPresenter;

        private readonly StringBuilder textBuilder = new StringBuilder(64);

        private int lastPlayerLife = int.MinValue;
        private int lastPlayerBarrier = int.MinValue;
        private int lastPlayerAmmo = int.MinValue;
        private int lastEnemyLife = int.MinValue;
        private int lastEnemyBreak = int.MinValue;
        private int lastThreatKey = int.MinValue;
        private BattleSessionState lastState = (BattleSessionState)(-1);
        private WeaponState weaponState = WeaponState.Ready;
        private WeaponState lastWeaponState = (WeaponState)(-1);
        private BattleCompletionReason terminalReason;
        private float terminalFadeElapsed;
        private bool terminalLatched;
        private bool developmentOverlayVisible;

        public CombatPresentationProfile PresentationProfile => presentationProfile;
        public bool IsTerminalLatched => terminalLatched;
        public BattleCompletionReason TerminalReason => terminalReason;
        public bool IsTerminalPanelVisible => terminalPanel != null && terminalPanel.activeSelf;
        public bool IsDevelopmentOverlayVisible => developmentOverlayVisible;
        public D0TerminalScreenFxPresenter TerminalScreenFx => terminalScreenFx;
        public string CurrentThreatLabel => threatText == null ? string.Empty : threatText.text;
        public string CurrentActionLabel => actionText == null ? string.Empty : actionText.text;

        public bool TryValidate(out string error)
        {
            if (presentationProfile == null)
            {
                error = "Combat HUD requires a valid CombatPresentationProfile.";

                return false;
            }

            if (!presentationProfile.TryValidateStatic(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Combat HUD requires a valid CombatPresentationProfile.";
                }

                return false;
            }

            if (enemyLifeFill == null || enemyBreakFill == null || threatIndicator == null
                || enemyNameText == null || enemyLifeText == null || enemyBreakText == null
                || threatText == null || playerLifeFill == null || playerBarrierFill == null
                || ammoFill == null || actionFill == null || playerNameText == null
                || playerLifeText == null || playerBarrierText == null || ammoText == null
                || actionText == null || terminalPanel == null || terminalCanvasGroup == null
                || terminalTitleText == null || terminalPromptText == null
                || terminalScreenFx == null || developmentOverlay == null
                || developmentText == null || diagnosticsPresenter == null)
            {
                error = "Combat HUD requires all authored UI, terminal FX and diagnostics references.";
                return false;
            }

            if (!terminalScreenFx.TryValidate(out error))
            {
                error = "Combat HUD terminal screen FX is invalid: " + error;
                return false;
            }

            if (GetComponentsInChildren<Collider>(true).Length > 0
                || GetComponentsInChildren<Collider2D>(true).Length > 0
                || GetComponentsInChildren<Rigidbody>(true).Length > 0
                || GetComponentsInChildren<Rigidbody2D>(true).Length > 0)
            {
                error = "Combat HUD must not contain Collider or Rigidbody components.";
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

        /// <summary>
        /// Updates durable HUD state from the domain's already-created read
        /// models. Threat input is copied by BattlePresentationCoordinator into
        /// its fixed buffer before this call.
        /// </summary>
        public void Refresh(
            in FinalSnapshot snapshot,
            ScenarioDefinition definition,
            ThreatSnapshot[] threats,
            int threatCount,
            TickIndex currentTick)
        {
            if (definition == null)
            {
                Clear();
                return;
            }

            if (snapshot.PlayerLife != lastPlayerLife)
            {
                SetFill(playerLifeFill, snapshot.PlayerLife, definition.PlayerLife);
                SetValueText(playerLifeText, "LIFE", snapshot.PlayerLife, definition.PlayerLife);
                lastPlayerLife = snapshot.PlayerLife;
            }

            if (snapshot.PlayerBarrier != lastPlayerBarrier)
            {
                SetFill(playerBarrierFill, snapshot.PlayerBarrier, definition.PlayerBarrier);
                SetValueText(playerBarrierText, "BARRIER", snapshot.PlayerBarrier, definition.PlayerBarrier);
                lastPlayerBarrier = snapshot.PlayerBarrier;
            }

            if (snapshot.PlayerAmmo != lastPlayerAmmo)
            {
                SetFill(ammoFill, snapshot.PlayerAmmo, definition.PlayerWeapon.MagazineCapacity);
                SetValueText(ammoText, "AMMO", snapshot.PlayerAmmo, definition.PlayerWeapon.MagazineCapacity);
                lastPlayerAmmo = snapshot.PlayerAmmo;
            }

            int enemyMaxLife = snapshot.EnemyMaxLife > 0
                ? snapshot.EnemyMaxLife
                : definition.EnemyLife;
            int enemyMaxBreak = snapshot.EnemyMaxBreak > 0
                ? snapshot.EnemyMaxBreak
                : definition.EnemyBreak;
            if (snapshot.EnemyLife != lastEnemyLife)
            {
                SetFill(enemyLifeFill, snapshot.EnemyLife, enemyMaxLife);
                SetValueText(enemyLifeText, "HP", snapshot.EnemyLife, enemyMaxLife);
                lastEnemyLife = snapshot.EnemyLife;
            }

            if (snapshot.EnemyBreak != lastEnemyBreak)
            {
                SetFill(enemyBreakFill, snapshot.EnemyBreak, enemyMaxBreak);
                SetValueText(enemyBreakText, "BREAK", snapshot.EnemyBreak, enemyMaxBreak);
                lastEnemyBreak = snapshot.EnemyBreak;
            }

            SetText(enemyNameText, "BURSTBUG");
            SetText(playerNameText, "FEI_30048");
            RefreshThreat(threats, threatCount, currentTick);
            RefreshWeaponState();

            if (snapshot.State == BattleSessionState.Completed)
            {
                LatchTerminal(snapshot.CompletionReason);
            }

            lastState = snapshot.State;
            if (developmentOverlayVisible)
            {
                RefreshDevelopmentText(snapshot.State);
            }
        }

        /// <summary>
        /// The trace supplies temporary player-weapon state transitions that
        /// are deliberately absent from FinalSnapshot. It is consumed after the
        /// event has committed to the domain and cannot affect its outcome.
        /// </summary>
        public void ConsumeCombatTrace(in CombatEvent combatEvent, RuntimeId playerRuntimeId)
        {
            if (!playerRuntimeId.IsValid || combatEvent.SourceId != playerRuntimeId)
            {
                return;
            }

            switch (combatEvent.EventType)
            {
                case CombatEventType.InputAccepted:
                    SetWeaponState((WeaponState)combatEvent.ValueAfter);
                    break;

                case CombatEventType.ReloadStarted:
                    SetWeaponState(WeaponState.Reloading);
                    break;

                case CombatEventType.ReloadCompleted:
                    SetWeaponState(WeaponState.Ready);
                    break;

                case CombatEventType.AttackCanceled:
                    SetWeaponState(WeaponState.Ready);
                    break;
            }
        }

        public void Advance(float deltaTime, bool paused)
        {
            if (!paused)
            {
                RefreshActionIndicator();
                if (terminalLatched)
                {
                    terminalFadeElapsed += Mathf.Max(0f, deltaTime);
                    if (terminalCanvasGroup != null)
                    {
                        terminalCanvasGroup.alpha = Mathf.Clamp01(
                            terminalFadeElapsed / TerminalFadeSeconds);
                    }
                }
            }

            terminalScreenFx?.Advance(deltaTime, paused);
        }

        public void ToggleDevelopmentOverlay()
        {
            SetDevelopmentOverlayVisible(!developmentOverlayVisible);
        }

        public void SetDevelopmentOverlayVisible(bool visible)
        {
            developmentOverlayVisible = visible;
            if (developmentOverlay != null && developmentOverlay.activeSelf != visible)
            {
                developmentOverlay.SetActive(visible);
            }

            if (visible)
            {
                RefreshDevelopmentText(lastState);
            }
        }

        public void Clear()
        {
            SetFill(playerLifeFill, 0, 1);
            SetFill(playerBarrierFill, 0, 1);
            SetFill(ammoFill, 0, 1);
            SetFill(actionFill, 0, 1);
            SetFill(enemyLifeFill, 0, 1);
            SetFill(enemyBreakFill, 0, 1);
            if (threatIndicator != null)
            {
                threatIndicator.color = new Color(0.42f, 0.72f, 0.82f, 0.75f);
            }

            SetText(enemyNameText, "BURSTBUG");
            SetText(enemyLifeText, "HP --");
            SetText(enemyBreakText, "BREAK --");
            SetText(threatText, NoThreatLabel);
            SetText(playerNameText, "FEI_30048");
            SetText(playerLifeText, "LIFE --");
            SetText(playerBarrierText, "BARRIER --");
            SetText(ammoText, "AMMO --");
            SetText(actionText, ReadyLabel);
            if (actionText != null)
            {
                actionText.color = ReadyActionColor;
            }

            if (actionFill != null)
            {
                actionFill.color = ReadyActionColor;
            }

            if (threatText != null)
            {
                threatText.color = ClearThreatTextColor;
            }

            SetText(terminalTitleText, string.Empty);
            SetText(terminalPromptText, string.Empty);
            if (terminalPanel != null && terminalPanel.activeSelf)
            {
                terminalPanel.SetActive(false);
            }

            if (terminalCanvasGroup != null)
            {
                terminalCanvasGroup.alpha = 0f;
                terminalCanvasGroup.blocksRaycasts = false;
                terminalCanvasGroup.interactable = false;
            }

            terminalScreenFx?.Clear();
            terminalLatched = false;
            terminalReason = default(BattleCompletionReason);
            terminalFadeElapsed = 0f;
            weaponState = WeaponState.Ready;
            lastWeaponState = (WeaponState)(-1);
            lastPlayerLife = int.MinValue;
            lastPlayerBarrier = int.MinValue;
            lastPlayerAmmo = int.MinValue;
            lastEnemyLife = int.MinValue;
            lastEnemyBreak = int.MinValue;
            lastThreatKey = int.MinValue;
            lastState = (BattleSessionState)(-1);
            SetDevelopmentOverlayVisible(false);
        }

        private void Update()
        {
            if (IsDevelopmentOverlayTogglePressed())
            {
                ToggleDevelopmentOverlay();
            }
        }

        private static bool IsDevelopmentOverlayTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.f3Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F3);
#else
            return false;
#endif
        }

        private void RefreshThreat(
            ThreatSnapshot[] threats,
            int threatCount,
            TickIndex currentTick)
        {
            if (threats == null || threatCount <= 0)
            {
                SetThreatClear();
                return;
            }

            int safeCount = Mathf.Min(threatCount, threats.Length);
            CombatThreatPresentationDefinition selected = null;
            int selectedPriority = int.MinValue;
            for (int index = 0; index < safeCount; index++)
            {
                ThreatSnapshot threat = threats[index];
                if (threat.IsTerminal || !IsThreatActive(threat.State)
                    || !presentationProfile.TryGetThreatDefinition(
                        threat.PresentationKey,
                        out CombatThreatPresentationDefinition definition))
                {
                    continue;
                }

                int priority = GetThreatPriority(definition.Kind);
                if (priority > selectedPriority)
                {
                    selected = definition;
                    selectedPriority = priority;
                }
            }

            if (selected == null)
            {
                SetThreatClear();
                return;
            }

            if (lastThreatKey == selected.PresentationKey)
            {
                return;
            }

            lastThreatKey = selected.PresentationKey;
            if (threatIndicator != null)
            {
                threatIndicator.color = selected.PrimaryColor;
            }

            if (threatText != null)
            {
                threatText.color = selected.PrimaryColor;
            }

            switch (selected.Kind)
            {
                case CombatThreatPresentationKind.FastUninterceptable:
                    SetText(threatText, "DANGER | DIRECT");
                    break;

                case CombatThreatPresentationKind.InterceptableVolley:
                    SetText(threatText, "INTERCEPT | x3");
                    break;

                case CombatThreatPresentationKind.HeavyWeakpoint:
                    SetText(threatText, "WEAKPOINT | BREAK");
                    break;

                default:
                    SetThreatClear();
                    break;
            }
        }

        private void SetThreatClear()
        {
            if (lastThreatKey == 0)
            {
                return;
            }

            lastThreatKey = 0;
            if (threatIndicator != null)
            {
                threatIndicator.color = ClearThreatIndicatorColor;
            }

            if (threatText != null)
            {
                threatText.color = ClearThreatTextColor;
            }

            SetText(threatText, NoThreatLabel);
        }

        private static bool IsThreatActive(ThreatState state)
        {
            return state == ThreatState.Telegraph
                || state == ThreatState.Windup
                || state == ThreatState.ReleaseCommitted;
        }

        private static int GetThreatPriority(CombatThreatPresentationKind kind)
        {
            switch (kind)
            {
                case CombatThreatPresentationKind.HeavyWeakpoint:
                    return 3;
                case CombatThreatPresentationKind.FastUninterceptable:
                    return 2;
                case CombatThreatPresentationKind.InterceptableVolley:
                    return 1;
                default:
                    return 0;
            }
        }

        private void SetWeaponState(WeaponState nextWeaponState)
        {
            weaponState = nextWeaponState;
        }

        private void RefreshWeaponState()
        {
            if (weaponState == lastWeaponState)
            {
                return;
            }

            lastWeaponState = weaponState;
            switch (weaponState)
            {
                case WeaponState.AltCharging:
                    SetText(actionText, ChargingLabel);
                    if (actionText != null)
                    {
                        actionText.color = new Color(0.72f, 0.95f, 1f, 1f);
                    }

                    break;

                case WeaponState.Reloading:
                    SetText(actionText, ReloadLabel);
                    if (actionText != null)
                    {
                        actionText.color = new Color(1f, 0.78f, 0.32f, 1f);
                    }

                    break;

                case WeaponState.PrimaryRecovery:
                case WeaponState.AltRecovery:
                    SetText(actionText, RecoveringLabel);
                    if (actionText != null)
                    {
                        actionText.color = new Color(0.72f, 0.82f, 1f, 1f);
                    }

                    break;

                case WeaponState.Disabled:
                    SetText(actionText, LockedLabel);
                    if (actionText != null)
                    {
                        actionText.color = new Color(1f, 0.38f, 0.32f, 1f);
                    }

                    break;

                default:
                    SetText(actionText, ReadyLabel);
                    if (actionText != null)
                    {
                        actionText.color = ReadyActionColor;
                    }

                    break;
            }
        }

        private void RefreshActionIndicator()
        {
            if (actionFill == null)
            {
                return;
            }

            // Weapon phase is not part of FinalSnapshot yet. Until a
            // deterministic phase/progress read model exists, the action
            // surface is deliberately a static status light rather than a
            // PingPong animation that could be mistaken for real charge or
            // reload progress.
            actionFill.fillAmount = 1f;
            switch (weaponState)
            {
                case WeaponState.AltCharging:
                    actionFill.color = new Color(0.72f, 0.95f, 1f, 1f);
                    break;

                case WeaponState.Reloading:
                    actionFill.color = new Color(1f, 0.78f, 0.32f, 1f);
                    break;

                case WeaponState.PrimaryRecovery:
                case WeaponState.AltRecovery:
                    actionFill.color = new Color(0.72f, 0.82f, 1f, 1f);
                    break;

                case WeaponState.Disabled:
                    actionFill.color = new Color(1f, 0.38f, 0.32f, 1f);
                    break;

                default:
                    actionFill.color = ReadyActionColor;
                    break;
            }
        }

        private void LatchTerminal(BattleCompletionReason reason)
        {
            if (terminalLatched || (reason != BattleCompletionReason.Victory
                && reason != BattleCompletionReason.Defeat))
            {
                return;
            }

            terminalLatched = true;
            terminalReason = reason;
            terminalFadeElapsed = 0f;
            if (terminalPanel != null)
            {
                terminalPanel.SetActive(true);
            }

            if (terminalCanvasGroup != null)
            {
                terminalCanvasGroup.alpha = 0f;
                terminalCanvasGroup.blocksRaycasts = false;
                terminalCanvasGroup.interactable = false;
            }

            bool victory = reason == BattleCompletionReason.Victory;
            SetText(terminalTitleText, victory ? "VICTORY" : "DEFEAT");
            SetText(
                terminalPromptText,
                victory ? "BURSTBUG DISPERSED  |  F5 RESTART"
                    : "FEI HAS FALLEN  |  F5 RESTART");
            if (terminalTitleText != null)
            {
                terminalTitleText.color = victory
                    ? new Color(1f, 0.86f, 0.32f, 1f)
                    : new Color(1f, 0.32f, 0.26f, 1f);
            }

            terminalScreenFx?.Show(reason);
        }

        private void RefreshDevelopmentText(BattleSessionState state)
        {
            if (developmentText == null)
            {
                return;
            }

            // BattleSessionDiagnosticsPresenter deliberately stops rebuilding
            // its legacy IMGUI text while that surface is hidden.  The formal
            // F3 overlay is its active consumer, so refresh on demand here
            // instead of paying a string allocation every gameplay frame.
            diagnosticsPresenter?.RefreshText();

            textBuilder.Length = 0;
            textBuilder.Append("DEV OVERLAY  |  F3 HIDE");
            textBuilder.Append('\n').Append("STATE: ").Append(state.ToString().ToUpperInvariant());
            textBuilder.Append('\n').Append(DevelopmentPrompt);
            if (diagnosticsPresenter != null && !string.IsNullOrEmpty(diagnosticsPresenter.CurrentText))
            {
                textBuilder.Append('\n').Append(diagnosticsPresenter.CurrentText);
            }

            SetText(developmentText, textBuilder.ToString());
        }

        private void SetValueText(Text text, string prefix, int value, int maximum)
        {
            if (text == null)
            {
                return;
            }

            textBuilder.Length = 0;
            textBuilder.Append(prefix).Append(' ').Append(value).Append(" / ").Append(maximum);
            SetText(text, textBuilder.ToString());
        }

        private static void SetFill(Image image, int value, int maximum)
        {
            if (image != null)
            {
                image.fillAmount = maximum <= 0 ? 0f : Mathf.Clamp01(value / (float)maximum);
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
