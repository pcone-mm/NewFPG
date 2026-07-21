using System;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    public interface IFpgEnemyDefinitionCatalog
    {
        bool TryGet(string enemyDefinitionId, out FpgEnemyDefinitionData definition);
    }

    public interface IFpgEncounterSpawnPointResolver
    {
        DomainResult TryReserve(
            FpgSpawnEntry entry,
            FpgEncounterRunContext runContext,
            int attempt,
            out string pointId,
            out int relaxationLevel);

        void Release(string pointId, RuntimeId runtimeId);
    }

    public interface IFpgEncounterEntityPort
    {
        /// <summary>
        /// Binds an already-prewarmed entity to a planned entry. This method
        /// must never Instantiate, Destroy, resize, or perform hidden lookup.
        /// </summary>
        DomainResult Prepare(FpgSpawnEntry entry, RuntimeId runtimeId, string pointId);

        /// <summary>
        /// Activates gameplay bindings (hitbox, threat owner, health bar) after
        /// the warning window has elapsed.
        /// </summary>
        DomainResult Activate(FpgSpawnEntry entry, RuntimeId runtimeId, string pointId);

        DomainResult Despawn(RuntimeId runtimeId, bool preservePresentationLease);

        void ClearAll();
    }

    public sealed class NullFpgEncounterEntityPort : IFpgEncounterEntityPort
    {
        public static readonly NullFpgEncounterEntityPort Instance = new NullFpgEncounterEntityPort();

        private NullFpgEncounterEntityPort()
        {
        }

        public DomainResult Prepare(FpgSpawnEntry entry, RuntimeId runtimeId, string pointId)
        {
            return runtimeId.IsValid && !string.IsNullOrEmpty(pointId)
                ? DomainResult.Success
                : DomainResult.Rejected(RejectReason.InvalidDefinition);
        }

        public DomainResult Activate(FpgSpawnEntry entry, RuntimeId runtimeId, string pointId)
        {
            return runtimeId.IsValid && !string.IsNullOrEmpty(pointId)
                ? DomainResult.Success
                : DomainResult.Rejected(RejectReason.InvalidDefinition);
        }

        public DomainResult Despawn(RuntimeId runtimeId, bool preservePresentationLease)
        {
            return runtimeId.IsValid ? DomainResult.Success : DomainResult.Rejected(RejectReason.InvalidTarget);
        }

        public void ClearAll()
        {
        }
    }

    public sealed class NullFpgEnemyDefinitionCatalog : IFpgEnemyDefinitionCatalog
    {
        public static readonly NullFpgEnemyDefinitionCatalog Instance = new NullFpgEnemyDefinitionCatalog();

        private NullFpgEnemyDefinitionCatalog()
        {
        }

        public bool TryGet(string enemyDefinitionId, out FpgEnemyDefinitionData definition)
        {
            definition = null;
            return false;
        }
    }

    public readonly struct FpgEncounterRuntimeSnapshot
    {
        public FpgEncounterRuntimeSnapshot(
            FpgEncounterPhase phase,
            int currentWaveIndex,
            int pendingSpawnCount,
            int livingEnemyCount,
            int activeCapWeight,
            TickIndex currentTick,
            FpgEncounterFailureReason failureReason)
        {
            Phase = phase;
            CurrentWaveIndex = currentWaveIndex;
            PendingSpawnCount = pendingSpawnCount;
            LivingEnemyCount = livingEnemyCount;
            ActiveCapWeight = activeCapWeight;
            CurrentTick = currentTick;
            FailureReason = failureReason;
        }

        public FpgEncounterPhase Phase { get; }
        public int CurrentWaveIndex { get; }
        public int PendingSpawnCount { get; }
        public int LivingEnemyCount { get; }
        public int ActiveCapWeight { get; }
        public TickIndex CurrentTick { get; }
        public FpgEncounterFailureReason FailureReason { get; }
    }

    /// <summary>
    /// Pure Hades-style room state machine. It owns plan progress and delegates
    /// all Unity object work to fixed-capacity ports.
    /// </summary>
    public sealed class FpgEncounterRuntime : IDisposable
    {
        private readonly FpgEncounterPlan plan;
        private readonly FpgEncounterProfileData profile;
        private readonly FpgEnemyRoster roster;
        private readonly FpgSpawnQueue spawnQueue;
        private readonly FpgSummonLedger summonLedger;
        private readonly SessionIdAllocator idAllocator;
        private readonly IFpgEnemyDefinitionCatalog definitionCatalog;
        private readonly IFpgEncounterSpawnPointResolver spawnPointResolver;
        private readonly IFpgEncounterEntityPort entityPort;
        private readonly Action<FpgEncounterLifecycleEvent> eventSink;
        private int waveEntryCursor;
        private int currentWaveIndex;
        private int spawnAttempt;
        private TickIndex nextQueueTick;
        private TickIndex waveDelayUntilTick;
        private TickIndex currentTick = TickIndex.Invalid;
        private FpgEncounterPhase phase;
        private FpgEncounterPhase phaseBeforePause;
        private FpgEncounterFailureReason failureReason;
        private TickIndex pauseStartedTick = TickIndex.Invalid;
        private readonly int dynamicSpawnSequenceStart;
        private int dynamicSpawnSequenceCursor;
        private bool waveEntriesIssued;
        private bool disposed;

        public FpgEncounterRuntime(
            FpgEncounterPlan plan,
            FpgEncounterProfileData profile,
            FpgEnemyRoster roster,
            SessionIdAllocator idAllocator,
            IFpgEnemyDefinitionCatalog definitionCatalog,
            IFpgEncounterSpawnPointResolver spawnPointResolver,
            IFpgEncounterEntityPort entityPort = null,
            Action<FpgEncounterLifecycleEvent> eventSink = null,
            FpgSummonLedger summonLedger = null,
            int spawnQueueCapacity = 0)
        {
            if (spawnQueueCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spawnQueueCapacity));
            }

            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.roster = roster ?? throw new ArgumentNullException(nameof(roster));
            this.idAllocator = idAllocator ?? throw new ArgumentNullException(nameof(idAllocator));
            this.definitionCatalog = definitionCatalog ?? NullFpgEnemyDefinitionCatalog.Instance;
            this.spawnPointResolver = spawnPointResolver ?? throw new ArgumentNullException(nameof(spawnPointResolver));
            this.entityPort = entityPort ?? NullFpgEncounterEntityPort.Instance;
            this.eventSink = eventSink;
            this.summonLedger = summonLedger;
            int defaultQueueCapacity = plan.EntryCount;
            if (summonLedger != null)
            {
                defaultQueueCapacity = SaturatingAdd(defaultQueueCapacity, summonLedger.Capacity);
            }

            spawnQueue = new FpgSpawnQueue(
                spawnQueueCapacity > 0 ? spawnQueueCapacity : Math.Max(1, defaultQueueCapacity));
            dynamicSpawnSequenceStart = FindDynamicSpawnSequenceStart(plan);
            dynamicSpawnSequenceCursor = dynamicSpawnSequenceStart;
            phase = FpgEncounterPhase.Preparing;
            currentWaveIndex = -1;
            failureReason = FpgEncounterFailureReason.None;
        }

        public FpgEncounterPlan Plan => plan;
        public FpgEnemyRoster Roster => roster;
        public FpgSummonLedger SummonLedger => summonLedger;
        public FpgEncounterPhase Phase => phase;
        public FpgEncounterFailureReason FailureReason => failureReason;
        public int CurrentWaveIndex => currentWaveIndex;
        public int SpawnQueueCapacity => spawnQueue.Capacity;
        public int AcceptedSummonCount => summonLedger == null ? 0 : summonLedger.Count;
        public int PendingSpawnCount => spawnQueue.Count + (waveEntriesIssued ? 0 : RemainingWaveEntries());
        public TickIndex CurrentTick => currentTick;
        public event Action<FpgEncounterLifecycleEvent> LifecycleEvent;

        public bool IsTerminal => phase == FpgEncounterPhase.Cleared
            || phase == FpgEncounterPhase.Failed
            || phase == FpgEncounterPhase.Faulted
            || phase == FpgEncounterPhase.Disposed;

        public FpgEncounterRuntimeSnapshot GetSnapshot()
        {
            return new FpgEncounterRuntimeSnapshot(
                phase,
                currentWaveIndex,
                PendingSpawnCount,
                roster.LivingCount,
                roster.ActiveCapWeight,
                currentTick,
                failureReason);
        }

        public DomainResult Start(TickIndex tick)
        {
            if (disposed || phase == FpgEncounterPhase.Disposed)
            {
                return DomainResult.Rejected(RejectReason.Disposed);
            }

            if (phase != FpgEncounterPhase.Preparing)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!tick.IsValid || plan.WaveCount == 0)
            {
                return Fail(FpgEncounterFailureReason.InvalidRequest, RejectReason.InvalidDefinition);
            }

            currentTick = tick;
            phaseBeforePause = FpgEncounterPhase.None;
            pauseStartedTick = TickIndex.Invalid;
            currentWaveIndex = 0;
            waveEntryCursor = 0;
            waveEntriesIssued = false;
            nextQueueTick = tick + new TickDuration(profile.WarningDurationTicks);
            phase = profile.WarningDurationTicks > 0
                ? FpgEncounterPhase.Warning
                : FpgEncounterPhase.Spawning;
            Emit(new FpgEncounterLifecycleEvent(
                FpgEncounterLifecycleEventType.Started,
                tick,
                phase,
                waveIndex: currentWaveIndex));
            Emit(new FpgEncounterLifecycleEvent(
                FpgEncounterLifecycleEventType.WaveStarted,
                tick,
                phase,
                waveIndex: currentWaveIndex));
            if (phase == FpgEncounterPhase.Warning)
            {
                Emit(new FpgEncounterLifecycleEvent(
                    FpgEncounterLifecycleEventType.WarningStarted,
                    tick,
                    phase,
                    waveIndex: currentWaveIndex));
            }

            return DomainResult.Success;
        }

        public DomainResult Advance(TickIndex tick)
        {
            if (disposed || phase == FpgEncounterPhase.Disposed)
            {
                return DomainResult.Rejected(RejectReason.Disposed);
            }

            if (!tick.IsValid || (currentTick.IsValid && tick < currentTick))
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            if (phase == FpgEncounterPhase.Preparing || IsTerminal || phase == FpgEncounterPhase.Paused)
            {
                return phase == FpgEncounterPhase.Paused
                    ? DomainResult.Success
                    : DomainResult.Rejected(RejectReason.InvalidState);
            }

            currentTick = tick;
            if (phase == FpgEncounterPhase.Warning && tick.Value >= nextQueueTick.Value)
            {
                phase = FpgEncounterPhase.Spawning;
            }

            if (phase == FpgEncounterPhase.Spawning || phase == FpgEncounterPhase.Combat)
            {
                DomainResult spawnResult = AdvanceSpawning(tick);
                if (!spawnResult.IsSuccess)
                {
                    return spawnResult;
                }
            }

            if (phase == FpgEncounterPhase.WaveDelay && tick >= waveDelayUntilTick)
            {
                StartNextWave(tick);
            }

            TryCompleteWaveOrRoom(tick);
            return phase == FpgEncounterPhase.Failed
                ? DomainResult.Rejected(RejectReason.InvariantFault)
                : DomainResult.Success;
        }

        public DomainResult Pause(TickIndex tick)
        {
            if (phase != FpgEncounterPhase.Warning && phase != FpgEncounterPhase.Spawning
                && phase != FpgEncounterPhase.Combat && phase != FpgEncounterPhase.WaveDelay)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!tick.IsValid || (currentTick.IsValid && tick < currentTick))
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            phaseBeforePause = phase;
            pauseStartedTick = tick;
            currentTick = tick;
            phase = FpgEncounterPhase.Paused;
            Emit(new FpgEncounterLifecycleEvent(FpgEncounterLifecycleEventType.Paused, tick, phase));
            return DomainResult.Success;
        }

        public DomainResult Resume(TickIndex tick)
        {
            if (phase != FpgEncounterPhase.Paused || !tick.IsValid
                || !pauseStartedTick.IsValid || tick < pauseStartedTick)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            long pausedTicks = tick - pauseStartedTick;
            nextQueueTick = ShiftTick(nextQueueTick, pausedTicks);
            waveDelayUntilTick = ShiftTick(waveDelayUntilTick, pausedTicks);
            DomainResult shiftedQueue = spawnQueue.ShiftScheduledTicks(pausedTicks);
            DomainResult shiftedRoster = roster.ShiftWarningTicks(pausedTicks);
            if (!shiftedQueue.IsSuccess || !shiftedRoster.IsSuccess)
            {
                return Fail(FpgEncounterFailureReason.SynchronizerFault, RejectReason.InvariantFault);
            }

            currentTick = tick;
            phase = phaseBeforePause;
            phaseBeforePause = FpgEncounterPhase.None;
            pauseStartedTick = TickIndex.Invalid;
            Emit(new FpgEncounterLifecycleEvent(FpgEncounterLifecycleEventType.Resumed, tick, phase));
            return DomainResult.Success;
        }

        public DomainResult TryQueueSummon(FpgSummonRequest request, TickIndex tick)
        {
            if (summonLedger == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (phase != FpgEncounterPhase.Spawning && phase != FpgEncounterPhase.Combat)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!tick.IsValid || (currentTick.IsValid && tick < currentTick))
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            if (!roster.TryGet(request.OwnerRuntimeId, out FpgEnemySlot owner) || !owner.IsActive)
            {
                return DomainResult.Rejected(RejectReason.OwnerInterrupted);
            }

            if (dynamicSpawnSequenceCursor < 0 || dynamicSpawnSequenceCursor >= int.MaxValue)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            if (!definitionCatalog.TryGet(
                    request.EnemyDefinitionId,
                    out FpgEnemyDefinitionData definition)
                || definition == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            DomainResult ledgerReservation = summonLedger.TryReserve(request);
            if (!ledgerReservation.IsSuccess)
            {
                return ledgerReservation;
            }

            int spawnSequence = dynamicSpawnSequenceCursor;
            string spawnEntryId = BuildSummonSpawnEntryId(request, spawnSequence);
            FpgSpawnEntry entry = new FpgSpawnEntry(
                spawnEntryId,
                definition.EnemyDefinitionId,
                currentWaveIndex,
                spawnSequence,
                definition.SpawnCost,
                definition.CapWeight,
                definition.Role,
                forced: true,
                themeEnemy: false,
                overBudget: false,
                recursionDepth: request.RecursionDepth);

            DomainResult prepared = TryPrepareAndQueueEntry(
                entry,
                tick,
                attempt: 0,
                out FpgSpawnPreparationStage ignoredStage);
            if (!prepared.IsSuccess)
            {
                summonLedger.TryRollback(request);
                return prepared;
            }

            dynamicSpawnSequenceCursor++;
            return DomainResult.Success;
        }

        public DomainResult MarkEnemyDead(RuntimeId runtimeId, TickIndex tick)
        {
            if (!roster.TryGet(runtimeId, out FpgEnemySlot slot))
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            DomainResult result = roster.TryMarkDead(runtimeId);
            if (!result.IsSuccess)
            {
                return result;
            }

            DomainResult despawn = entityPort.Despawn(
                runtimeId,
                preservePresentationLease: true);
            spawnPointResolver.Release(slot.SpawnPointId, runtimeId);
            if (!despawn.IsSuccess)
            {
                return despawn;
            }

            Emit(new FpgEncounterLifecycleEvent(
                FpgEncounterLifecycleEventType.EnemyDied,
                tick,
                phase,
                runtimeId,
                slot.WaveIndex >= 0 ? slot.WaveIndex : currentWaveIndex,
                slot.SpawnEntryId));
            return DomainResult.Success;
        }

        public DomainResult Fail(FpgEncounterFailureReason reason, RejectReason rejectReason = RejectReason.InvariantFault)
        {
            if (phase == FpgEncounterPhase.Cleared || phase == FpgEncounterPhase.Disposed)
            {
                return DomainResult.Rejected(RejectReason.AlreadyTerminal);
            }

            ClearLiveEntities();
            failureReason = reason;
            phase = FpgEncounterPhase.Failed;
            Emit(new FpgEncounterLifecycleEvent(
                FpgEncounterLifecycleEventType.Failed,
                currentTick,
                phase,
                failureReason: reason));
            return DomainResult.Rejected(rejectReason);
        }

        public void Reset()
        {
            if (disposed)
            {
                return;
            }

            ClearLiveEntities();
            waveEntryCursor = 0;
            currentWaveIndex = -1;
            spawnAttempt = 0;
            nextQueueTick = TickIndex.Invalid;
            waveDelayUntilTick = TickIndex.Invalid;
            currentTick = TickIndex.Invalid;
            failureReason = FpgEncounterFailureReason.None;
            phaseBeforePause = FpgEncounterPhase.None;
            pauseStartedTick = TickIndex.Invalid;
            dynamicSpawnSequenceCursor = dynamicSpawnSequenceStart;
            waveEntriesIssued = false;
            phase = FpgEncounterPhase.Preparing;
            Emit(new FpgEncounterLifecycleEvent(FpgEncounterLifecycleEventType.Restarted, currentTick, phase));
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            ClearLiveEntities();
            phase = FpgEncounterPhase.Disposed;
            disposed = true;
            Emit(new FpgEncounterLifecycleEvent(FpgEncounterLifecycleEventType.Disposed, currentTick, phase));
        }

        private DomainResult AdvanceSpawning(TickIndex tick)
        {
            if (currentWaveIndex < 0 || currentWaveIndex >= plan.WaveCount)
            {
                return Fail(FpgEncounterFailureReason.InvalidRequest);
            }

            FpgEncounterWavePlan wave = plan.Waves[currentWaveIndex];
            if (!waveEntriesIssued && tick >= nextQueueTick)
            {
                DomainResult queueResult = QueueNextEntry(wave, tick);
                if (!queueResult.IsSuccess)
                {
                    return queueResult;
                }
            }

            if (spawnQueue.TryPeek(out FpgQueuedSpawn queued)
                && tick >= queued.EarliestActivationTick)
            {
                DomainResult activation = ActivateHead(queued, tick);
                if (!activation.IsSuccess)
                {
                    return activation;
                }
            }

            if (waveEntriesIssued && spawnQueue.Count == 0 && roster.LivingCount > 0)
            {
                phase = FpgEncounterPhase.Combat;
            }

            return DomainResult.Success;
        }

        private DomainResult QueueNextEntry(FpgEncounterWavePlan wave, TickIndex tick)
        {
            if (waveEntryCursor >= wave.Entries.Count)
            {
                waveEntriesIssued = true;
                return DomainResult.Success;
            }

            FpgSpawnEntry entry = wave.Entries[waveEntryCursor];
            DomainResult prepared = TryPrepareAndQueueEntry(
                entry,
                tick,
                spawnAttempt,
                out FpgSpawnPreparationStage failureStage);
            if (!prepared.IsSuccess)
            {
                if (failureStage == FpgSpawnPreparationStage.ConcurrentCap)
                {
                    phase = FpgEncounterPhase.Combat;
                    return DomainResult.Success;
                }

                if (failureStage == FpgSpawnPreparationStage.SpawnPoint)
                {
                    spawnAttempt++;
                    if (spawnAttempt > profile.MaxSpawnWaitTicks)
                    {
                        return Fail(
                            FpgEncounterFailureReason.SpawnPointUnavailable,
                            prepared.RejectReason);
                    }

                    nextQueueTick = tick + new TickDuration(1);
                    return DomainResult.Success;
                }

                return Fail(
                    failureStage == FpgSpawnPreparationStage.Definition
                        ? FpgEncounterFailureReason.InvalidPool
                        : FpgEncounterFailureReason.EntityCapacity,
                    prepared.RejectReason);
            }

            waveEntryCursor++;
            spawnAttempt = 0;
            nextQueueTick = tick + new TickDuration(profile.SpawnIntervalTicks);
            if (waveEntryCursor >= wave.Entries.Count)
            {
                waveEntriesIssued = true;
            }

            return DomainResult.Success;
        }

        private DomainResult TryPrepareAndQueueEntry(
            FpgSpawnEntry entry,
            TickIndex tick,
            int attempt,
            out FpgSpawnPreparationStage failureStage)
        {
            failureStage = FpgSpawnPreparationStage.None;
            if (roster.ReservedCapWeight + entry.CapWeight > profile.MaxConcurrentCapWeight
                || roster.LivingCount >= profile.MaxConcurrentEntities)
            {
                failureStage = FpgSpawnPreparationStage.ConcurrentCap;
                return DomainResult.Rejected(RejectReason.BudgetExceeded);
            }

            if (!definitionCatalog.TryGet(
                    entry.EnemyDefinitionId,
                    out FpgEnemyDefinitionData definition)
                || definition == null)
            {
                failureStage = FpgSpawnPreparationStage.Definition;
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            DomainResult pointResult = spawnPointResolver.TryReserve(
                entry,
                plan.RunContext,
                attempt,
                out string pointId,
                out int ignoredRelaxation);
            if (!pointResult.IsSuccess)
            {
                failureStage = FpgSpawnPreparationStage.SpawnPoint;
                return pointResult;
            }

            RuntimeId runtimeId = idAllocator.NextRuntimeId();
            TickIndex warningUntil = tick + new TickDuration(profile.WarningDurationTicks);
            DomainResult reserve = roster.TryReserve(
                entry,
                runtimeId,
                pointId,
                warningUntil,
                definition.Life,
                definition.Break,
                out FpgEnemySlot ignoredSlot);
            if (!reserve.IsSuccess)
            {
                spawnPointResolver.Release(pointId, runtimeId);
                failureStage = FpgSpawnPreparationStage.Roster;
                return reserve;
            }

            DomainResult prepared = entityPort.Prepare(entry, runtimeId, pointId);
            if (!prepared.IsSuccess)
            {
                roster.TryMarkDead(runtimeId);
                roster.TryRelease(runtimeId);
                spawnPointResolver.Release(pointId, runtimeId);
                failureStage = FpgSpawnPreparationStage.Entity;
                return prepared;
            }

            DomainResult enqueue = spawnQueue.TryEnqueue(new FpgQueuedSpawn(
                entry,
                tick,
                warningUntil,
                attempt));
            if (!enqueue.IsSuccess)
            {
                entityPort.Despawn(runtimeId, false);
                roster.TryMarkDead(runtimeId);
                roster.TryRelease(runtimeId);
                spawnPointResolver.Release(pointId, runtimeId);
                failureStage = FpgSpawnPreparationStage.Queue;
                return enqueue;
            }

            Emit(new FpgEncounterLifecycleEvent(
                FpgEncounterLifecycleEventType.EnemyQueued,
                tick,
                phase,
                runtimeId,
                entry.WaveIndex,
                entry.SpawnEntryId));
            return DomainResult.Success;
        }

        private DomainResult ActivateHead(FpgQueuedSpawn queued, TickIndex tick)
        {
            if (!roster.TryGetBySpawnEntry(queued.Entry.SpawnEntryId, out FpgEnemySlot slot))
            {
                return Fail(FpgEncounterFailureReason.EntityCapacity, RejectReason.InvalidTarget);
            }

            DomainResult activation = roster.TryActivate(slot.RuntimeId, tick);
            if (!activation.IsSuccess)
            {
                return Fail(FpgEncounterFailureReason.EntityCapacity, activation.RejectReason);
            }

            DomainResult entityActivation = entityPort.Activate(queued.Entry, slot.RuntimeId, slot.SpawnPointId);
            if (!entityActivation.IsSuccess)
            {
                roster.TryMarkDead(slot.RuntimeId);
                entityPort.Despawn(slot.RuntimeId, true);
                spawnPointResolver.Release(slot.SpawnPointId, slot.RuntimeId);
                return Fail(FpgEncounterFailureReason.EntityCapacity, entityActivation.RejectReason);
            }

            spawnQueue.TryDequeue(out FpgQueuedSpawn ignored);
            phase = FpgEncounterPhase.Combat;
            Emit(new FpgEncounterLifecycleEvent(
                FpgEncounterLifecycleEventType.EnemyActivated,
                tick,
                phase,
                slot.RuntimeId,
                queued.Entry.WaveIndex,
                slot.SpawnEntryId));
            return DomainResult.Success;
        }

        private void TryCompleteWaveOrRoom(TickIndex tick)
        {
            if (!waveEntriesIssued || spawnQueue.Count != 0 || roster.LivingCount != 0)
            {
                return;
            }

            Emit(new FpgEncounterLifecycleEvent(
                FpgEncounterLifecycleEventType.WaveCleared,
                tick,
                phase,
                waveIndex: currentWaveIndex));
            if (currentWaveIndex >= plan.WaveCount - 1)
            {
                phase = FpgEncounterPhase.Cleared;
                Emit(new FpgEncounterLifecycleEvent(
                    FpgEncounterLifecycleEventType.RoomCleared,
                    tick,
                    phase,
                    waveIndex: currentWaveIndex));
                return;
            }

            phase = FpgEncounterPhase.WaveDelay;
            waveDelayUntilTick = tick + new TickDuration(profile.WaveIntervalTicks);
        }

        private void StartNextWave(TickIndex tick)
        {
            currentWaveIndex++;
            waveEntryCursor = 0;
            waveEntriesIssued = false;
            spawnAttempt = 0;
            nextQueueTick = tick + new TickDuration(profile.WarningDurationTicks);
            phase = profile.WarningDurationTicks > 0
                ? FpgEncounterPhase.Warning
                : FpgEncounterPhase.Spawning;
            Emit(new FpgEncounterLifecycleEvent(
                FpgEncounterLifecycleEventType.WaveStarted,
                tick,
                phase,
                waveIndex: currentWaveIndex));
            if (phase == FpgEncounterPhase.Warning)
            {
                Emit(new FpgEncounterLifecycleEvent(
                    FpgEncounterLifecycleEventType.WarningStarted,
                    tick,
                    phase,
                    waveIndex: currentWaveIndex));
            }
        }

        private void ClearLiveEntities()
        {
            for (int index = 0; index < roster.Capacity; index++)
            {
                FpgEnemySlot slot = roster.GetSlot(index);
                if (!slot.IsReserved || !slot.RuntimeId.IsValid)
                {
                    continue;
                }

                entityPort.Despawn(slot.RuntimeId, preservePresentationLease: true);
                spawnPointResolver.Release(slot.SpawnPointId, slot.RuntimeId);
            }

            spawnQueue.Clear();
            roster.Clear();
            summonLedger?.Clear();
            entityPort.ClearAll();
        }

        private static TickIndex ShiftTick(TickIndex tick, long delta)
        {
            if (!tick.IsValid || delta == 0L)
            {
                return tick;
            }

            return tick.Value > long.MaxValue - delta
                ? new TickIndex(long.MaxValue)
                : new TickIndex(tick.Value + delta);
        }

        private static int FindDynamicSpawnSequenceStart(FpgEncounterPlan sourcePlan)
        {
            int next = 0;
            for (int index = 0; index < sourcePlan.AllEntries.Count; index++)
            {
                int sequence = sourcePlan.AllEntries[index].SpawnSequence;
                if (sequence >= int.MaxValue)
                {
                    return int.MaxValue;
                }

                if (sequence >= next)
                {
                    next = sequence + 1;
                }
            }

            return next;
        }

        private static string BuildSummonSpawnEntryId(
            FpgSummonRequest request,
            int spawnSequence)
        {
            return "summon-r" + request.RequestSequence
                + "-s" + spawnSequence;
        }

        private static int SaturatingAdd(int left, int right)
        {
            if (right <= 0)
            {
                return left;
            }

            return right > int.MaxValue - left ? int.MaxValue : left + right;
        }

        private int RemainingWaveEntries()
        {
            if (currentWaveIndex < 0 || currentWaveIndex >= plan.WaveCount)
            {
                return 0;
            }

            return Math.Max(0, plan.Waves[currentWaveIndex].Entries.Count - waveEntryCursor);
        }

        private enum FpgSpawnPreparationStage
        {
            None = 0,
            ConcurrentCap,
            SpawnPoint,
            Definition,
            Roster,
            Entity,
            Queue
        }

        private void Emit(FpgEncounterLifecycleEvent lifecycleEvent)
        {
            eventSink?.Invoke(lifecycleEvent);
            LifecycleEvent?.Invoke(lifecycleEvent);
        }
    }
}











