using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Pure, presentation-only routing from committed D0 combat records to
    /// stable audio cue identifiers. It deliberately owns no Unity object,
    /// combat state, cursor, or playback policy so its decisions can be
    /// covered in EditMode without a scene.
    /// </summary>
    public static class CombatAudioCueRouting
    {
        /// <summary>
        /// Emits the free-reticle lock feedback only on the visual transition
        /// into the weakpoint. This is intentionally based on presentation
        /// state rather than a combat query, so cursor movement cannot affect
        /// hit resolution or replay determinism.
        /// </summary>
        public static bool TryGetReticleLockCue(
            bool wasLocked,
            bool isLocked,
            out CombatAudioCue cue)
        {
            if (!wasLocked && isLocked)
            {
                cue = CombatAudioCue.ReticleTargetLock;
                return true;
            }

            cue = CombatAudioCue.None;
            return false;
        }

        /// <summary>
        /// Emits one countdown tick when the visible, positive heavy-threat
        /// countdown changes. Zero is excluded because the heavy-release cue
        /// owns the release boundary and must remain the final warning.
        /// </summary>
        public static bool TryGetHeavyCountdownCue(
            int previousDisplayedSeconds,
            int currentDisplayedSeconds,
            out CombatAudioCue cue)
        {
            if (currentDisplayedSeconds > 0
                && currentDisplayedSeconds != previousDisplayedSeconds)
            {
                cue = CombatAudioCue.EnemyDangerTick;
                return true;
            }

            cue = CombatAudioCue.None;
            return false;
        }

        /// <summary>
        /// Routes one already-recorded trace event. The caller supplies the
        /// bound runtime ids so the same event type cannot accidentally play a
        /// player feedback cue for another combatant.
        /// </summary>
        public static bool TryGetTraceCue(
            in CombatEvent combatEvent,
            RuntimeId playerRuntimeId,
            RuntimeId enemyRuntimeId,
            out CombatAudioCue cue)
        {
            cue = CombatAudioCue.None;
            switch (combatEvent.EventType)
            {
                case CombatEventType.DamageApplied:
                    if (combatEvent.TargetId == playerRuntimeId
                        && combatEvent.ValueBefore > combatEvent.ValueAfter)
                    {
                        cue = CombatAudioCue.PlayerDamaged;
                        return true;
                    }

                    return false;

                case CombatEventType.BarrierBroken:
                    if (combatEvent.TargetId == playerRuntimeId)
                    {
                        cue = CombatAudioCue.PlayerBarrierBroken;
                        return true;
                    }

                    return false;

                case CombatEventType.BreakTriggered:
                    if (combatEvent.TargetId == enemyRuntimeId)
                    {
                        cue = CombatAudioCue.EnemyBreak;
                        return true;
                    }

                    return false;

                case CombatEventType.BattleCompleted:
                    switch ((BattleCompletionReason)combatEvent.ValueAfter)
                    {
                        case BattleCompletionReason.Victory:
                            cue = CombatAudioCue.Victory;
                            return true;

                        case BattleCompletionReason.Defeat:
                            cue = CombatAudioCue.Defeat;
                            return true;

                        default:
                            return false;
                    }

                default:
                    return false;
            }
        }

        /// <summary>
        /// Routes an explicit threat state transition using the semantic kind
        /// held by the read-only threat snapshot. Threat state records carry no
        /// presentation fields themselves, so the presenter maintains a small
        /// snapshot cache before calling this method.
        /// </summary>
        public static bool TryGetThreatTransitionCue(
            FpgThreatPresentationKind presentationKind,
            ThreatState previousState,
            ThreatState currentState,
            out CombatAudioCue cue)
        {
            cue = CombatAudioCue.None;
            if (previousState == currentState)
            {
                return false;
            }

            bool telegraph = currentState == ThreatState.Telegraph;
            bool release = currentState == ThreatState.ReleaseCommitted;
            if (!telegraph && !release)
            {
                return false;
            }

            switch (presentationKind)
            {
                case FpgThreatPresentationKind.FastUninterceptable:
                    cue = telegraph
                        ? CombatAudioCue.EnemyFastThreatTelegraph
                        : CombatAudioCue.EnemyFastThreatRelease;
                    return true;

                case FpgThreatPresentationKind.InterceptableVolley:
                    cue = telegraph
                        ? CombatAudioCue.EnemyInterceptableThreatTelegraph
                        : CombatAudioCue.EnemyInterceptableThreatRelease;
                    return true;

                case FpgThreatPresentationKind.HeavyWeakpoint:
                    cue = telegraph
                        ? CombatAudioCue.EnemyHeavyThreatTelegraph
                        : CombatAudioCue.EnemyHeavyThreatRelease;
                    return true;

                default:
                    return false;
            }
        }
    }
}
