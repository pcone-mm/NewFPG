using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// One-shot D0 threat presentation actions derived from an already-committed
    /// threat state transition. They are intentionally visual/audio commands,
    /// not additional battle states.
    /// </summary>
    public enum D0ThreatPresentationCommand
    {
        None = 0,
        BeginTelegraph,
        EscalateTelegraph,
        ReleaseFast,
        ReleaseVolley,
        ReleaseHeavy,
        Cancel,
        Complete
    }

    /// <summary>
    /// Immutable hand-off from the trace router to the Unity presentation
    /// bridge. The supplied runtime id and presentation key originate from a
    /// copied <see cref="ThreatSnapshot"/>; this value never becomes part of
    /// the combat transcript.
    /// </summary>
    public readonly struct D0ThreatPresentationSignal
    {
        public D0ThreatPresentationSignal(
            RuntimeId threatRuntimeId,
            int presentationKey,
            CombatThreatPresentationKind kind,
            D0ThreatPresentationCommand command,
            CombatAudioCue audioCue)
        {
            ThreatRuntimeId = threatRuntimeId;
            PresentationKey = presentationKey;
            Kind = kind;
            Command = command;
            AudioCue = audioCue;
        }

        public RuntimeId ThreatRuntimeId { get; }
        public int PresentationKey { get; }
        public CombatThreatPresentationKind Kind { get; }
        public D0ThreatPresentationCommand Command { get; }
        public CombatAudioCue AudioCue { get; }
        public bool IsValid => ThreatRuntimeId.IsValid
            && PresentationKey > 0
            && Command != D0ThreatPresentationCommand.None;
    }

    /// <summary>
    /// Pure mapping between the existing committed threat trace and D0's three
    /// presentation families. It does not own a cursor, access Unity scene
    /// objects, mutate battle state, or infer damage from visuals.
    /// </summary>
    public static class D0ThreatPresentationRouting
    {
        public static bool RequiresPersistentTelegraph(in ThreatSnapshot snapshot)
        {
            return !snapshot.IsTerminal
                && (snapshot.State == ThreatState.Telegraph
                    || snapshot.State == ThreatState.Windup)
                && TryGetKind(snapshot.PresentationKey, out _);
        }

        public static bool TryGetKind(
            int presentationKey,
            out CombatThreatPresentationKind kind)
        {
            switch (presentationKey)
            {
                case CombatPresentationProfile.FastThreatPresentationKey:
                    kind = CombatThreatPresentationKind.FastUninterceptable;
                    return true;

                case CombatPresentationProfile.InterceptableVolleyThreatPresentationKey:
                    kind = CombatThreatPresentationKind.InterceptableVolley;
                    return true;

                case CombatPresentationProfile.HeavyWeakpointThreatPresentationKey:
                    kind = CombatThreatPresentationKind.HeavyWeakpoint;
                    return true;

                default:
                    kind = default(CombatThreatPresentationKind);
                    return false;
            }
        }

        /// <summary>
        /// Resolves a single <see cref="CombatEventType.ThreatStateChanged"/>
        /// record. The trace itself carries only the threat runtime id, so the
        /// caller must supply the matching snapshot-cached presentation key.
        /// This is deliberate: no new domain trace payload is required.
        /// </summary>
        public static bool TryResolve(
            in CombatEvent combatEvent,
            RuntimeId enemyRuntimeId,
            RuntimeId threatRuntimeId,
            int presentationKey,
            out D0ThreatPresentationSignal signal)
        {
            signal = default(D0ThreatPresentationSignal);
            if (combatEvent.EventType != CombatEventType.ThreatStateChanged
                || !enemyRuntimeId.IsValid
                || !threatRuntimeId.IsValid
                || combatEvent.SourceId != enemyRuntimeId
                || combatEvent.TargetId != threatRuntimeId
                || !TryGetKind(presentationKey, out CombatThreatPresentationKind kind)
                || !TryReadThreatState(combatEvent.ValueAfter, out ThreatState nextState))
            {
                return false;
            }

            D0ThreatPresentationCommand command;
            switch (nextState)
            {
                case ThreatState.Telegraph:
                    command = D0ThreatPresentationCommand.BeginTelegraph;
                    break;

                case ThreatState.Windup:
                    command = D0ThreatPresentationCommand.EscalateTelegraph;
                    break;

                case ThreatState.ReleaseCommitted:
                    command = ResolveReleaseCommand(kind);
                    break;

                case ThreatState.Canceled:
                    command = D0ThreatPresentationCommand.Cancel;
                    break;

                case ThreatState.Completed:
                    command = D0ThreatPresentationCommand.Complete;
                    break;

                // ReleaseCommitted is immediately followed by Recovery in the
                // domain. Recovery is durable state only and must not replay a
                // release effect or audio cue.
                case ThreatState.Scheduled:
                case ThreatState.Recovery:
                default:
                    return false;
            }

            signal = new D0ThreatPresentationSignal(
                threatRuntimeId,
                presentationKey,
                kind,
                command,
                ResolveAudioCue(kind, command));
            return true;
        }

        private static D0ThreatPresentationCommand ResolveReleaseCommand(
            CombatThreatPresentationKind kind)
        {
            switch (kind)
            {
                case CombatThreatPresentationKind.FastUninterceptable:
                    return D0ThreatPresentationCommand.ReleaseFast;

                case CombatThreatPresentationKind.InterceptableVolley:
                    return D0ThreatPresentationCommand.ReleaseVolley;

                case CombatThreatPresentationKind.HeavyWeakpoint:
                    return D0ThreatPresentationCommand.ReleaseHeavy;

                default:
                    return D0ThreatPresentationCommand.None;
            }
        }

        private static CombatAudioCue ResolveAudioCue(
            CombatThreatPresentationKind kind,
            D0ThreatPresentationCommand command)
        {
            bool telegraph = command == D0ThreatPresentationCommand.BeginTelegraph;
            bool release = command == D0ThreatPresentationCommand.ReleaseFast
                || command == D0ThreatPresentationCommand.ReleaseVolley
                || command == D0ThreatPresentationCommand.ReleaseHeavy;
            if (!telegraph && !release)
            {
                return CombatAudioCue.None;
            }

            switch (kind)
            {
                case CombatThreatPresentationKind.FastUninterceptable:
                    return telegraph
                        ? CombatAudioCue.EnemyFastThreatTelegraph
                        : CombatAudioCue.EnemyFastThreatRelease;

                case CombatThreatPresentationKind.InterceptableVolley:
                    return telegraph
                        ? CombatAudioCue.EnemyInterceptableThreatTelegraph
                        : CombatAudioCue.EnemyInterceptableThreatRelease;

                case CombatThreatPresentationKind.HeavyWeakpoint:
                    return telegraph
                        ? CombatAudioCue.EnemyHeavyThreatTelegraph
                        : CombatAudioCue.EnemyHeavyThreatRelease;

                default:
                    return CombatAudioCue.None;
            }
        }

        private static bool TryReadThreatState(int value, out ThreatState state)
        {
            if (value < (int)ThreatState.Scheduled || value > (int)ThreatState.Canceled)
            {
                state = default(ThreatState);
                return false;
            }

            state = (ThreatState)value;
            return true;
        }
    }
}
