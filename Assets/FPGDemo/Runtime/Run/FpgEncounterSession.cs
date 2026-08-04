using System;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    public interface IFpgEncounterCombatTickPort
    {
        bool IsPlayerAlive { get; }

        /// <summary>
        /// Runs one owner-aware combat phase. Implementations must use
        /// RuntimeId for every target, hitbox, threat and projectile lookup.
        /// </summary>
        DomainResult Process(FpgBattleTickPhase phase, TickIndex tick, FpgEnemyRoster roster);

        void ClearAll();
    }

    public sealed class NullFpgEncounterCombatTickPort : IFpgEncounterCombatTickPort
    {
        public static readonly NullFpgEncounterCombatTickPort Instance = new NullFpgEncounterCombatTickPort();

        private NullFpgEncounterCombatTickPort()
        {
        }

        public bool IsPlayerAlive => true;

        public DomainResult Process(FpgBattleTickPhase phase, TickIndex tick, FpgEnemyRoster roster)
        {
            return tick.IsValid ? DomainResult.Success : DomainResult.Rejected(RejectReason.InvalidState);
        }

        public void ClearAll()
        {
        }
    }

    public enum FpgEncounterSessionState
    {
        NotStarted = 0,
        Running,
        Paused,
        Cleared,
        Defeated,
        Faulted,
        Disposed
    }

    public readonly struct FpgEncounterSessionSnapshot
    {
        public FpgEncounterSessionSnapshot(
            FpgEncounterSessionState state,
            FpgEncounterRuntimeSnapshot runtime,
            bool playerAlive,
            long executedTickCount)
        {
            State = state;
            Runtime = runtime;
            PlayerAlive = playerAlive;
            ExecutedTickCount = executedTickCount;
        }

        public FpgEncounterSessionState State { get; }
        public FpgEncounterRuntimeSnapshot Runtime { get; }
        public bool PlayerAlive { get; }
        public long ExecutedTickCount { get; }
    }

    /// <summary>
    /// Formal multi-enemy session. It is intentionally separate from the old
    /// single-enemy BattleSession public contract.
    /// </summary>
    public sealed class FpgEncounterSession : IDisposable
    {
        private static readonly FpgBattleTickPhase[] TickOrder =
        {
            FpgBattleTickPhase.LifecycleBoundary,
            FpgBattleTickPhase.EnemyRecovery,
            FpgBattleTickPhase.PlayerAttackAndHit,
            FpgBattleTickPhase.DeathAndThreatCleanup,
            FpgBattleTickPhase.EnemyAttackDirector,
            FpgBattleTickPhase.ThreatAndProjectileAdvance,
            FpgBattleTickPhase.ImpactResolution,
            FpgBattleTickPhase.EncounterCompletion
        };

        private readonly FpgRoomRunRequest request;
        private readonly FpgEncounterRuntime runtime;
        private readonly IFpgBattleTickSynchronizer synchronizer;
        private readonly IFpgEncounterCombatTickPort combatPort;
        private readonly IFpgPlayerRoomSnapshotPort playerRoomSnapshotPort;
        private readonly Action<FpgEncounterLifecycleEvent> lifecycleEventSink;
        private bool entrySnapshotCaptured;
        private bool disposed;
        private long executedTickCount;

        public FpgEncounterSession(
            FpgRoomRunRequest request,
            FpgEncounterRuntime runtime,
            IFpgBattleTickSynchronizer synchronizer = null,
            IFpgEncounterCombatTickPort combatPort = null,
            Action<FpgEncounterLifecycleEvent> lifecycleEventSink = null,
            IFpgPlayerRoomSnapshotPort playerRoomSnapshotPort = null)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Formal room request is invalid.", nameof(request));
            }

            this.request = request;
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.synchronizer = synchronizer ?? NullFpgBattleTickSynchronizer.Instance;
            this.combatPort = combatPort ?? NullFpgEncounterCombatTickPort.Instance;
            this.playerRoomSnapshotPort = playerRoomSnapshotPort
                ?? NullFpgPlayerRoomSnapshotPort.Instance;
            this.lifecycleEventSink = lifecycleEventSink;
            runtime.LifecycleEvent += ForwardLifecycleEvent;
            State = FpgEncounterSessionState.NotStarted;
        }

        public FpgRoomRunRequest Request => request;
        public FpgEncounterRuntime Runtime => runtime;
        public FpgEnemyRoster Roster => runtime.Roster;
        public FpgEncounterSessionState State { get; private set; }
        public long ExecutedTickCount => executedTickCount;
        public TickIndex CurrentTick => runtime.CurrentTick;

        public event Action<FpgEncounterLifecycleEvent> LifecycleEvent;

        public FpgEncounterSessionSnapshot GetSnapshot()
        {
            return new FpgEncounterSessionSnapshot(
                State,
                runtime.GetSnapshot(),
                combatPort.IsPlayerAlive,
                executedTickCount);
        }

        public DomainResult Start(TickIndex tick)
        {
            if (disposed || State == FpgEncounterSessionState.Disposed)
            {
                return DomainResult.Rejected(RejectReason.Disposed);
            }

            if (State != FpgEncounterSessionState.NotStarted)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!entrySnapshotCaptured)
            {
                DomainResult captured = playerRoomSnapshotPort.CaptureEntrySnapshot();
                if (!captured.IsSuccess)
                {
                    return captured;
                }

                entrySnapshotCaptured = true;
            }

            DomainResult result = runtime.Start(tick);
            if (result.IsSuccess)
            {
                State = FpgEncounterSessionState.Running;
            }

            return result;
        }

        public DomainResult Advance(TickIndex tick)
        {
            if (disposed || State == FpgEncounterSessionState.Disposed)
            {
                return DomainResult.Rejected(RejectReason.Disposed);
            }

            if (State == FpgEncounterSessionState.Paused)
            {
                return DomainResult.Success;
            }

            if (State != FpgEncounterSessionState.Running)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!tick.IsValid || (runtime.CurrentTick.IsValid && tick < runtime.CurrentTick))
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            for (int index = 0; index < TickOrder.Length; index++)
            {
                FpgBattleTickPhase phase = TickOrder[index];
                DomainResult synchronized = synchronizer.Synchronize(phase, tick);
                if (!synchronized.IsSuccess)
                {
                    return Fault(tick, synchronized.RejectReason);
                }

                DomainResult phaseResult;
                if (phase == FpgBattleTickPhase.LifecycleBoundary)
                {
                    phaseResult = runtime.Advance(tick);
                }
                else
                {
                    phaseResult = combatPort.Process(phase, tick, runtime.Roster);
                }

                if (!phaseResult.IsSuccess)
                {
                    return Fault(tick, phaseResult.RejectReason);
                }

                if (phase == FpgBattleTickPhase.EncounterCompletion)
                {
                    DomainResult completion = EvaluateCompletion(tick);
                    if (!completion.IsSuccess)
                    {
                        return completion;
                    }
                }
            }

            executedTickCount++;
            return DomainResult.Success;
        }

        public DomainResult Pause(TickIndex tick)
        {
            if (State != FpgEncounterSessionState.Running)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            DomainResult result = runtime.Pause(tick);
            if (result.IsSuccess)
            {
                State = FpgEncounterSessionState.Paused;
            }

            return result;
        }

        public DomainResult Resume(TickIndex tick)
        {
            if (State != FpgEncounterSessionState.Paused)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            DomainResult result = runtime.Resume(tick);
            if (result.IsSuccess)
            {
                State = FpgEncounterSessionState.Running;
            }

            return result;
        }

        public DomainResult Restart()
        {
            if (disposed)
            {
                return DomainResult.Rejected(RejectReason.Disposed);
            }

            // Clear authoritative encounter/combat state without publishing a
            // successful restart until the entry snapshot is restored.
            runtime.Reset(emitRestarted: false);
            combatPort.ClearAll();
            if (entrySnapshotCaptured)
            {
                DomainResult restored = playerRoomSnapshotPort.RestoreEntrySnapshot();
                if (!restored.IsSuccess)
                {
                    State = FpgEncounterSessionState.Faulted;
                    runtime.Fail(FpgEncounterFailureReason.External, restored.RejectReason);
                    return restored;
                }
            }

            runtime.EmitRestarted();
            executedTickCount = 0L;
            State = FpgEncounterSessionState.NotStarted;
            return DomainResult.Success;
        }

        public DomainResult QueueSummon(FpgSummonRequest request, TickIndex tick)
        {
            if (State != FpgEncounterSessionState.Running)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            return runtime.TryQueueSummon(request, tick);
        }

        public DomainResult TryQueueSummon(FpgSummonRequest request, TickIndex tick)
        {
            return QueueSummon(request, tick);
        }

        public DomainResult TryQueueExternalSpawn(
            string enemyDefinitionId,
            FpgSpawnPlacement placement,
            TickIndex tick,
            out RuntimeId runtimeId)
        {
            runtimeId = RuntimeId.Invalid;
            if (State != FpgEncounterSessionState.Running)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            return runtime.TryQueueExternalSpawn(
                enemyDefinitionId,
                placement,
                tick,
                out runtimeId);
        }

        public DomainResult MarkEnemyDead(RuntimeId runtimeId, TickIndex tick)
        {
            if (State != FpgEncounterSessionState.Running && State != FpgEncounterSessionState.Paused)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            return runtime.MarkEnemyDead(runtimeId, tick);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            combatPort.ClearAll();
            runtime.Dispose();
            runtime.LifecycleEvent -= ForwardLifecycleEvent;
            State = FpgEncounterSessionState.Disposed;
            disposed = true;
        }

        private DomainResult EvaluateCompletion(TickIndex tick)
        {
            if (!combatPort.IsPlayerAlive)
            {
                DomainResult defeated = runtime.CompleteDefeat(tick);
                if (!defeated.IsSuccess)
                {
                    State = FpgEncounterSessionState.Faulted;
                    return defeated;
                }

                State = FpgEncounterSessionState.Defeated;
                return DomainResult.Success;
            }

            DomainResult completed = runtime.CompleteTick(tick);
            if (!completed.IsSuccess)
            {
                State = FpgEncounterSessionState.Faulted;
                return completed;
            }

            if (runtime.Phase == FpgEncounterPhase.Cleared)
            {
                State = FpgEncounterSessionState.Cleared;
                return DomainResult.Success;
            }

            if (runtime.Phase == FpgEncounterPhase.Failed || runtime.Phase == FpgEncounterPhase.Faulted)
            {
                State = FpgEncounterSessionState.Faulted;
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            return DomainResult.Success;
        }

        private DomainResult Fault(TickIndex tick, RejectReason reason)
        {
            State = FpgEncounterSessionState.Faulted;
            runtime.Fail(FpgEncounterFailureReason.SynchronizerFault, reason);
            return DomainResult.Rejected(reason);
        }

        private void ForwardLifecycleEvent(FpgEncounterLifecycleEvent lifecycleEvent)
        {
            if (lifecycleEvent.Type == FpgEncounterLifecycleEventType.WaveCleared)
            {
                playerRoomSnapshotPort.KeepAcrossWave();
            }

            lifecycleEventSink?.Invoke(lifecycleEvent);
            LifecycleEvent?.Invoke(lifecycleEvent);
        }
    }
}


