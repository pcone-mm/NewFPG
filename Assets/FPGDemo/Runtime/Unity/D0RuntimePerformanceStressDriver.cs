using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Opt-in pressure driver used only alongside <c>-d0-perf-stress</c>.
    /// It fills and sustains the real D0 fixed pools with 32 enemy projectiles,
    /// pooled hit tips, audio cues and committed primary-shot feeds. It never
    /// runs in an ordinary playable build and does not alter any authored
    /// gameplay configuration.
    /// </summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class D0RuntimePerformanceStressDriver : MonoBehaviour
    {
        public const string EnableArgument = "-d0-perf-stress";

        // A 60-second run at the fixed 60 Hz simulation rate records at most
        // 115,200 swept-projectile operations before player-shot and
        // lifecycle records. This allocation happens once before sampling;
        // it avoids reset-time allocation spikes inside the measured window.
        // It is opt-in for the standalone stress run, so authored combat keeps
        // its normal replay-buffer budget.
        public const int StressTranscriptOperationCapacity = 131072;

        private const int ProjectilePressureThreatCount = 8;
        private const int StressProjectileFlightTicks = 4200;
        private const int StressProjectileExpireTicks = 4260;
        private const int StressEnemyLife = 1000000;
        private const int StressEnemyBreak = 1000000;
        private const int PrimaryCycleTicks = 160;
        private const int PrimaryStartTick = 4;
        private const int PrimaryReloadTick = 84;

        private GameBootstrap bootstrap;
        private UnityBattleInputSource input;
        private bool pressureConfigured;
        private bool announced;
        private bool unexpectedStopReported;
        private int primaryTickCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterForRequestedPlayerRun()
        {
            if (!IsRequested())
            {
                return;
            }

            SceneManager.sceneLoaded -= TryAttachToBootScene;
            SceneManager.sceneLoaded += TryAttachToBootScene;
        }

        private static void TryAttachToBootScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Boot" || !scene.isLoaded)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                GameBootstrap candidate = roots[index].GetComponentInChildren<GameBootstrap>(true);
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.GetComponent<D0RuntimePerformanceStressDriver>() == null)
                {
                    candidate.gameObject.AddComponent<D0RuntimePerformanceStressDriver>();
                }

                SceneManager.sceneLoaded -= TryAttachToBootScene;
                return;
            }
        }

        private void Awake()
        {
            bootstrap = GetComponent<GameBootstrap>();
            input = new UnityBattleInputSource();
        }

        private void Update()
        {
            if (bootstrap == null
                || bootstrap.State != BootstrapState.Running
                || bootstrap.ActiveHost == null
                || bootstrap.ActiveContext == null)
            {
                return;
            }

            BattleSessionHost host = bootstrap.ActiveHost;
            BattleSession session = host.Session;
            if (session == null || session.State != BattleSessionState.Running)
            {
                ReportUnexpectedSessionStop();
                return;
            }

            if (!pressureConfigured)
            {
                if (!ConfigurePressure(bootstrap.ActiveContext, session))
                {
                    Debug.LogError("[D0_PERF] stress_driver_failed reason=pressure_setup", this);
                    enabled = false;
                }

                return;
            }

            PreparePrimaryPressureInput(host);
        }

        private bool ConfigurePressure(BattleSceneContext context, BattleSession session)
        {
            if (session.Definition.ProjectileCapacity != 32
                || context.D0HitTipPresenter == null
                || context.D0CombatAudioPresenter == null)
            {
                return false;
            }

            ThreatDefinition threat = CreateFourProjectileThreat();
            for (int index = 0; index < ProjectilePressureThreatCount; index++)
            {
                if (!session.TryAddThreat(threat, out int threatIndex).IsSuccess
                    || !session.TryStartThreat(threatIndex).IsSuccess)
                {
                    return false;
                }
            }

            D0HitTipPresenter hitTips = context.D0HitTipPresenter;
            for (int index = 0; index < hitTips.Capacity; index++)
            {
                float x = 0.08f + (index % 8) * 0.11f;
                float y = 0.18f + (index / 8) * 0.15f;
                hitTips.TryShow(D0HitTipKind.Body, 10 + index, new Vector2(x, y));
            }

            CombatAudioPresenter audio = context.D0CombatAudioPresenter;
            for (int index = 0; index < CombatAudioBank.RequiredCueCount; index++)
            {
                audio.TryPlayPresentationCue(CombatAudioBank.GetRequiredCue(index));
            }

            primaryTickCount = 0;
            pressureConfigured = true;
            if (!announced)
            {
                announced = true;
                Debug.Log("[D0_PERF] stress_driver_started projectile_pressure=32 mode=continuous", this);
            }

            return true;
        }

        private void PreparePrimaryPressureInput(BattleSessionHost host)
        {
            int cycleTick = primaryTickCount % PrimaryCycleTicks;
            bool primaryHeld = cycleTick >= PrimaryStartTick && cycleTick < PrimaryReloadTick;
            bool reloadPressed = cycleTick == PrimaryReloadTick;
            input.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: primaryHeld,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: reloadPressed,
                pausePressed: false,
                restartPressed: false));
            host.SetInputOverrideForD0Performance(input);
            primaryTickCount++;
        }

        private void ReportUnexpectedSessionStop()
        {
            if (unexpectedStopReported)
            {
                return;
            }

            unexpectedStopReported = true;
            Debug.LogError("[D0_PERF] stress_driver_failed reason=session_stopped", this);
            enabled = false;
        }

        private void OnDestroy()
        {
            if (bootstrap != null && bootstrap.ActiveHost != null)
            {
                bootstrap.ActiveHost.ClearInputOverrideForD0Performance();
            }
        }

        private static ThreatDefinition CreateFourProjectileThreat()
        {
            ProjectileDefinition projectile = new ProjectileDefinition(
                definitionId: 901,
                flightDuration: new TickDuration(StressProjectileFlightTicks),
                expireDuration: new TickDuration(StressProjectileExpireTicks),
                damageSpec: new DamageSpec(0, 0),
                maxHitPoints: 1,
                interceptable: true,
                budgetUnits: 1,
                presentationKey: 2,
                sweepRadiusKey: 250);
            return new ThreatDefinition(
                definitionId: 900,
                telegraphDuration: new TickDuration(1),
                windupDuration: new TickDuration(1),
                recoveryDuration: new TickDuration(300),
                projectileDefinition: projectile,
                payloadCount: 4);
        }

        /// <summary>
        /// The performance run owns all eight threat slots, so its scenario
        /// deliberately omits authored timed threats. It also prevents the
        /// fixed combat slice from ending before the continuous one-minute
        /// pressure measurement is complete. This method is never used by a
        /// normal playable session.
        /// </summary>
        public static ScenarioDefinition CreateScenarioDefinitionForStress(ScenarioDefinition source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new ScenarioDefinition(
                source.ScenarioSeed,
                source.PlayerWeapon,
                source.PlayerLife,
                source.PlayerBarrier,
                Math.Max(source.EnemyLife, StressEnemyLife),
                Math.Max(source.EnemyBreak, StressEnemyBreak),
                source.PerfectRetractWindow,
                source.PerfectRetractMultiplierBasisPoints,
                source.BarrierLockDuration,
                source.BarrierRestoreBasisPoints,
                source.EnemyGroggyDuration,
                source.ProjectileBudgetCapacity,
                source.ProjectileCapacity,
                source.ThreatCapacity,
                source.ImpactHistoryCapacity,
                source.ShotTargetHistoryCapacity,
                Array.Empty<ThreatScheduleEntry>());
        }

        public static bool IsRequested()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], EnableArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
