using System;
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
    /// bridge. The supplied runtime id, semantic kind and resource key
    /// originate from a copied <see cref="ThreatSnapshot"/>; this value never
    /// becomes part of the combat transcript.
    /// </summary>
    public readonly struct D0ThreatPresentationSignal
    {
        public D0ThreatPresentationSignal(
            RuntimeId threatRuntimeId,
            int presentationKey,
            FpgThreatPresentationKind kind,
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
        public FpgThreatPresentationKind Kind { get; }
        public D0ThreatPresentationCommand Command { get; }
        public CombatAudioCue AudioCue { get; }
        public bool IsValid => ThreatRuntimeId.IsValid
            && PresentationKey > 0
            && Enum.IsDefined(typeof(FpgThreatPresentationKind), Kind)
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
                && Enum.IsDefined(
                    typeof(FpgThreatPresentationKind),
                    snapshot.PresentationKind);
        }

        /// <summary>
        /// Resolves a single <see cref="CombatEventType.ThreatStateChanged"/>
        /// record. The trace itself carries only the threat runtime id, so the
        /// caller supplies the matching snapshot-cached semantic kind and
        /// resource key. This is deliberate: no new trace payload is required.
        /// </summary>
        public static bool TryResolve(
            in CombatEvent combatEvent,
            RuntimeId enemyRuntimeId,
            RuntimeId threatRuntimeId,
            FpgThreatPresentationKind presentationKind,
            int presentationKey,
            out D0ThreatPresentationSignal signal)
        {
            signal = default(D0ThreatPresentationSignal);
            if (combatEvent.EventType != CombatEventType.ThreatStateChanged
                || !enemyRuntimeId.IsValid
                || !threatRuntimeId.IsValid
                || combatEvent.SourceId != enemyRuntimeId
                || combatEvent.TargetId != threatRuntimeId
                || !Enum.IsDefined(
                    typeof(FpgThreatPresentationKind),
                    presentationKind)
                || presentationKey <= 0
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
                    command = ResolveReleaseCommand(presentationKind);
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
                presentationKind,
                command,
                ResolveAudioCue(presentationKind, command));
            return true;
        }

        private static D0ThreatPresentationCommand ResolveReleaseCommand(
            FpgThreatPresentationKind kind)
        {
            switch (kind)
            {
                case FpgThreatPresentationKind.FastUninterceptable:
                    return D0ThreatPresentationCommand.ReleaseFast;

                case FpgThreatPresentationKind.InterceptableVolley:
                    return D0ThreatPresentationCommand.ReleaseVolley;

                case FpgThreatPresentationKind.HeavyWeakpoint:
                    return D0ThreatPresentationCommand.ReleaseHeavy;

                default:
                    return D0ThreatPresentationCommand.None;
            }
        }

        private static CombatAudioCue ResolveAudioCue(
            FpgThreatPresentationKind kind,
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
                case FpgThreatPresentationKind.FastUninterceptable:
                    return telegraph
                        ? CombatAudioCue.EnemyFastThreatTelegraph
                        : CombatAudioCue.EnemyFastThreatRelease;

                case FpgThreatPresentationKind.InterceptableVolley:
                    return telegraph
                        ? CombatAudioCue.EnemyInterceptableThreatTelegraph
                        : CombatAudioCue.EnemyInterceptableThreatRelease;

                case FpgThreatPresentationKind.HeavyWeakpoint:
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
