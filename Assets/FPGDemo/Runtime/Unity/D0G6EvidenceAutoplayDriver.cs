using System;
using System.Collections;
using FPG.Demo.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Opt-in, Player-only acceptance driver for G6 evidence collection.
    /// It follows the real Unity input override seam used by the D0 performance
    /// harness: BattleSessionHost still owns aim sampling, simulation, rules,
    /// presentation and restart. The driver is never attached in a normal
    /// playable build and never writes a BattleSession directly.
    /// </summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class D0G6EvidenceAutoplayDriver : MonoBehaviour
    {
        public const string EnableArgument = "-d0-g6-autoplay";
        public const int RequiredLoopCount = 10;

        private const long SlowVolleyCaptureTick = 390L;
        private const long HeavyWarningCaptureTick = 540L;
        private const float BootstrapTimeoutSeconds = 15f;
        private const float TickTimeoutSeconds = 15f;
        private const float TerminalPresentationTimeoutSeconds = 3f;

        private GameBootstrap bootstrap;
        private BattleSessionHost host;
        private BattleSceneContext context;
        private D0EvidenceCaptureDriver evidence;
        private UnityBattleInputSource input;
        private Collider enemyBodyCollider;
        private SphereCollider enemyWeakpointCollider;
        private bool failed;
        private bool completed;
        private int completedVictories;
        private int completedDefeats;
        private int restartCount;

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

        public static bool IsRequested()
        {
#if FPG_D0_G6_EVIDENCE_AUTOPLAY
            return true;
#else
            return IsRequested(Environment.GetCommandLineArgs());
#endif
        }

        public static bool IsRequested(string[] arguments)
        {
            if (arguments == null)
            {
                return false;
            }

            for (int index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(
                        arguments[index],
                        EnableArgument,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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

                if (candidate.GetComponent<D0G6EvidenceAutoplayDriver>() == null)
                {
                    candidate.gameObject.AddComponent<D0G6EvidenceAutoplayDriver>();
                }

                SceneManager.sceneLoaded -= TryAttachToBootScene;
                return;
            }
        }

        private void Awake()
        {
            bootstrap = GetComponent<GameBootstrap>();
        }

        private void Start()
        {
            StartCoroutine(RunAcceptanceSequence());
        }

        private void OnDestroy()
        {
            if (host != null)
            {
                host.ClearInputOverrideForD0Performance();
            }
        }

        private IEnumerator RunAcceptanceSequence()
        {
            yield return WaitForBootstrap();
            if (failed)
            {
                yield break;
            }

            host = bootstrap.ActiveHost;
            context = bootstrap.ActiveContext;
            evidence = context == null
                ? null
                : context.transform.root.GetComponentInChildren<D0EvidenceCaptureDriver>(true);
            if (host == null || context == null || evidence == null || !evidence.CaptureActive)
            {
                Fail("missing_evidence_bindings");
                yield break;
            }

            if (!RefreshEnemyTargets()
                || context.CombatAimReticle == null
                || context.MainCamera == null)
            {
                Fail("missing_autoplay_targets");
                yield break;
            }

            BeginFreshInput();
            yield return new WaitForEndOfFrame();
            evidence.CaptureNamedStill("initial");
            evidence.StartVideoCapture();
            evidence.RecordMarker("autoplay_start", "g6", 0);

            for (int loopIndex = 0; loopIndex < RequiredLoopCount && !failed; loopIndex++)
            {
                bool shouldWin = loopIndex % 2 == 0;
                evidence.RecordMarker(
                    "loop_start",
                    shouldWin ? "victory" : "defeat",
                    loopIndex + 1);

                if (shouldWin)
                {
                    yield return RunVictoryLoop(loopIndex == 0);
                }
                else
                {
                    yield return RunDefeatLoop(loopIndex == 1);
                }

                if (failed)
                {
                    yield break;
                }

                if (loopIndex < RequiredLoopCount - 1)
                {
                    if (!host.TryRestart().IsSuccess)
                    {
                        Fail("restart_rejected");
                        yield break;
                    }

                    restartCount++;
                    evidence.RecordMarker("restart_host_path", "f5_equivalent", restartCount);
                    BeginFreshInput();
                    yield return new WaitForEndOfFrame();
                }
            }

            if (evidence.IsVideoRecording)
            {
                evidence.StopVideoCapture();
            }

            completed = !failed;
            evidence.RecordMarker("autoplay_complete", "g6", RequiredLoopCount);
            Debug.Log(
                $"[D0_EVIDENCE] autoplay_complete loops={RequiredLoopCount} "
                + $"victories={completedVictories} defeats={completedDefeats} "
                + $"restarts={restartCount}",
                this);
        }

        private IEnumerator RunVictoryLoop(bool captureFirstLoopEvidence)
        {
            BattleSession session = host.Session;
            if (session == null || session.State != BattleSessionState.Running)
            {
                Fail("victory_session_not_running");
                yield break;
            }

            if (!RefreshEnemyTargets())
            {
                Fail("active_enemy_targets_unavailable");
                yield break;
            }

            if (!SetAimAt(enemyBodyCollider))
            {
                yield break;
            }

            int lifeBeforePrimary = session.GetFinalSnapshot().EnemyLife;
            CaptureInput(aimHeld: true, primaryHeld: true);
            yield return null;
            CaptureInput(aimHeld: true, primaryHeld: false);
            yield return WaitForEnemyLifeBelow(session, lifeBeforePrimary);
            if (failed)
            {
                yield break;
            }

            if (captureFirstLoopEvidence)
            {
                yield return new WaitForEndOfFrame();
                evidence.CaptureNamedStill("primary_hit");
            }

            if (!RefreshEnemyTargets())
            {
                Fail("active_enemy_targets_unavailable");
                yield break;
            }

            if (!SetAimAt(enemyWeakpointCollider))
            {
                yield break;
            }

            yield return FireSecondary(session);
            if (failed)
            {
                yield break;
            }

            if (captureFirstLoopEvidence)
            {
                yield return new WaitForEndOfFrame();
                evidence.CaptureNamedStill("weakpoint_hit");
            }

            yield return WaitForSecondaryRecovery(session);
            yield return FireSecondary(session);
            yield return WaitForSecondaryRecovery(session);
            yield return Reload(session);
            yield return FireSecondary(session);

            yield return WaitForTick(session, SlowVolleyCaptureTick);
            if (failed)
            {
                yield break;
            }

            if (captureFirstLoopEvidence)
            {
                yield return new WaitForEndOfFrame();
                evidence.CaptureNamedStill("interceptable_volley");
            }

            yield return WaitForTick(session, HeavyWarningCaptureTick);
            if (failed)
            {
                yield break;
            }

            if (captureFirstLoopEvidence)
            {
                yield return new WaitForEndOfFrame();
                evidence.CaptureNamedStill("heavy_warning");
            }

            yield return FireSecondary(session);
            if (failed)
            {
                yield break;
            }

            yield return WaitForBreak(session);
            if (failed)
            {
                yield break;
            }

            if (captureFirstLoopEvidence)
            {
                yield return new WaitForEndOfFrame();
                evidence.CaptureNamedStill("break");
            }

            yield return WaitForSecondaryRecovery(session);
            yield return Reload(session);
            yield return FireSecondary(session);
            yield return WaitForSecondaryRecovery(session);
            yield return FireSecondary(session);
            yield return WaitForSecondaryRecovery(session);
            yield return Reload(session);
            yield return FireSecondary(session);
            yield return WaitForTerminal(session, BattleCompletionReason.Victory);
            if (failed)
            {
                yield break;
            }

            yield return WaitForTerminalPresentation();
            if (failed)
            {
                yield break;
            }

            if (captureFirstLoopEvidence)
            {
                evidence.CaptureNamedStill("victory");
                if (evidence.IsVideoRecording)
                {
                    evidence.StopVideoCapture();
                }
            }

            completedVictories++;
            evidence.RecordMarker("loop_terminal", "victory", completedVictories);
        }

        private IEnumerator RunDefeatLoop(bool captureFirstDefeatEvidence)
        {
            BattleSession session = host.Session;
            if (session == null || session.State != BattleSessionState.Running)
            {
                Fail("defeat_session_not_running");
                yield break;
            }

            CaptureInput(aimHeld: true, primaryHeld: false);
            yield return WaitForTerminal(session, BattleCompletionReason.Defeat);
            if (failed)
            {
                yield break;
            }

            yield return WaitForTerminalPresentation();
            if (failed)
            {
                yield break;
            }

            if (captureFirstDefeatEvidence)
            {
                evidence.CaptureNamedStill("defeat");
            }

            completedDefeats++;
            evidence.RecordMarker("loop_terminal", "defeat", completedDefeats);
        }

        private IEnumerator FireSecondary(BattleSession session)
        {
            CaptureInput(aimHeld: true, primaryHeld: false, secondaryPressed: true);
            yield return null;
            if (!EnsureRunning(session, "secondary_charge_interrupted"))
            {
                yield break;
            }

            long releaseTick = session.CurrentTick.Value
                + session.Definition.PlayerWeapon.SecondaryMinimumCharge.Value;
            yield return WaitForTick(session, releaseTick);
            if (failed)
            {
                yield break;
            }

            CaptureInput(aimHeld: true, primaryHeld: false, secondaryReleased: true);
            yield return null;
            CaptureInput(aimHeld: true, primaryHeld: false);
        }

        private IEnumerator Reload(BattleSession session)
        {
            CaptureInput(aimHeld: true, primaryHeld: false, reloadPressed: true);
            yield return null;
            CaptureInput(aimHeld: true, primaryHeld: false);
            yield return WaitForAdditionalTicks(
                session,
                session.Definition.PlayerWeapon.ReloadDuration.Value + 1);
        }

        private IEnumerator WaitForBootstrap()
        {
            float deadline = Time.realtimeSinceStartup + BootstrapTimeoutSeconds;
            while (bootstrap != null
                && bootstrap.State != BootstrapState.Running
                && bootstrap.State != BootstrapState.Failed
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (bootstrap == null || bootstrap.State != BootstrapState.Running)
            {
                Fail("bootstrap_not_running");
            }
        }

        private IEnumerator WaitForEnemyLifeBelow(BattleSession session, int startingLife)
        {
            float deadline = Time.realtimeSinceStartup + TickTimeoutSeconds;
            while (EnsureRunning(session, "primary_session_interrupted")
                && session.GetFinalSnapshot().EnemyLife >= startingLife
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!failed && session.GetFinalSnapshot().EnemyLife >= startingLife)
            {
                Fail("primary_target_not_damaged");
            }
        }

        private IEnumerator WaitForAdditionalTicks(BattleSession session, int additionalTicks)
        {
            long targetTick = session.CurrentTick.Value + additionalTicks;
            yield return WaitForTick(session, targetTick);
        }

        private IEnumerator WaitForSecondaryRecovery(BattleSession session)
        {
            yield return WaitForAdditionalTicks(
                session,
                session.Definition.PlayerWeapon.SecondaryRecovery.Value + 1);
        }

        private IEnumerator WaitForTick(BattleSession session, long targetTick)
        {
            float deadline = Time.realtimeSinceStartup + TickTimeoutSeconds;
            while (EnsureRunning(session, "tick_wait_interrupted")
                && session.CurrentTick.Value < targetTick
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!failed && session.CurrentTick.Value < targetTick)
            {
                Fail("tick_wait_timeout");
            }
        }

        private IEnumerator WaitForBreak(BattleSession session)
        {
            float deadline = Time.realtimeSinceStartup + TickTimeoutSeconds;
            while (EnsureRunning(session, "break_session_interrupted")
                && session.GetFinalSnapshot().EnemyBreak != 0
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!failed && session.GetFinalSnapshot().EnemyBreak != 0)
            {
                Fail("break_not_triggered");
            }
        }

        private IEnumerator WaitForTerminal(
            BattleSession session,
            BattleCompletionReason expectedReason)
        {
            float deadline = Time.realtimeSinceStartup + TickTimeoutSeconds;
            while (session.State == BattleSessionState.Running
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (session.State != BattleSessionState.Completed
                || session.CompletionReason != expectedReason)
            {
                Fail(expectedReason == BattleCompletionReason.Victory
                    ? "victory_not_reached"
                    : "defeat_not_reached");
            }
        }

        private IEnumerator WaitForTerminalPresentation()
        {
            CombatHud2DPresenter hud = context == null ? null : context.D0CombatHud2DPresenter;
            float deadline = Time.realtimeSinceStartup + TerminalPresentationTimeoutSeconds;
            while (hud != null && !hud.IsTerminalLatched && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (hud == null || !hud.IsTerminalLatched)
            {
                Fail("terminal_presentation_not_latched");
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.35f);
        }

        private void BeginFreshInput()
        {
            input = new UnityBattleInputSource();
            host.SetInputOverrideForD0Performance(input);
            CaptureInput(aimHeld: true, primaryHeld: false);
        }

        private void CaptureInput(
            bool aimHeld,
            bool primaryHeld,
            bool secondaryPressed = false,
            bool secondaryReleased = false,
            bool reloadPressed = false)
        {
            input.Capture(new UnityInputSnapshot(
                aimHeld,
                primaryHeld,
                secondaryPressed,
                secondaryReleased,
                reloadPressed,
                pausePressed: false,
                restartPressed: false));
        }

        private bool RefreshEnemyTargets()
        {
            D0EnemyEntityView active = context == null
                || context.EnemyEntityWorld == null
                ? null
                : context.EnemyEntityWorld.ActiveEntity;
            enemyBodyCollider = active == null ? null : active.BodyHitbox;
            enemyWeakpointCollider = active == null
                ? null
                : active.WeakpointHitbox as SphereCollider;
            return enemyBodyCollider != null && enemyWeakpointCollider != null;
        }

        private bool SetAimAt(Collider target)
        {
            if (target == null || context == null || context.MainCamera == null
                || context.CombatAimReticle == null)
            {
                Fail("aim_target_unavailable");
                return false;
            }

            Vector3 viewport = context.MainCamera.WorldToViewportPoint(target.bounds.center);
            if (viewport.z <= 0f)
            {
                Fail("aim_target_behind_camera");
                return false;
            }

            context.CombatAimReticle.SetViewport(new Vector2(viewport.x, viewport.y));
            return true;
        }

        private bool EnsureRunning(BattleSession session, string reason)
        {
            if (session == null || session.State != BattleSessionState.Running)
            {
                Fail(reason);
                return false;
            }

            return true;
        }

        private void Fail(string reason)
        {
            if (failed)
            {
                return;
            }

            failed = true;
            if (evidence != null)
            {
                evidence.RecordMarker("autoplay_failed", reason, restartCount);
                evidence.StopVideoCapture();
            }

            Debug.LogError($"[D0_EVIDENCE] autoplay_failed reason={reason}", this);
        }
    }
}
