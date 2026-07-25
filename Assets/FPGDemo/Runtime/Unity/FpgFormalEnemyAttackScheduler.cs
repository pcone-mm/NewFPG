using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using FPG.Demo.Skills;

namespace FPG.Demo.Unity
{
    public readonly struct FpgFormalEnemySkillStartedEvent
    {
        internal FpgFormalEnemySkillStartedEvent(
            RuntimeId ownerRuntimeId,
            int spawnSequence,
            FpgEnemyAttackDefinition definition,
            SkillExecutionId executionId,
            TickIndex startTick,
            TickIndex plannedEndTick)
        {
            OwnerRuntimeId = ownerRuntimeId;
            SpawnSequence = spawnSequence;
            Definition = definition;
            ExecutionId = executionId;
            StartTick = startTick;
            PlannedEndTick = plannedEndTick;
        }

        public RuntimeId OwnerRuntimeId { get; }
        public int SpawnSequence { get; }
        public FpgEnemyAttackDefinition Definition { get; }
        public SkillExecutionId ExecutionId { get; }
        public TickIndex StartTick { get; }
        public TickIndex PlannedEndTick { get; }
    }

    public readonly struct FpgFormalEnemySkillTimelineEvent
    {
        internal FpgFormalEnemySkillTimelineEvent(
            RuntimeId ownerRuntimeId,
            int spawnSequence,
            FpgEnemyAttackDefinition definition,
            FpgSkillEventResult runtimeEvent,
            FpgCompiledEnemySkillPayloadSlot payload,
            bool hasGameplayPayload)
        {
            OwnerRuntimeId = ownerRuntimeId;
            SpawnSequence = spawnSequence;
            Definition = definition;
            RuntimeEvent = runtimeEvent;
            Payload = payload;
            HasGameplayPayload = hasGameplayPayload;
        }

        public RuntimeId OwnerRuntimeId { get; }
        public int SpawnSequence { get; }
        public FpgEnemyAttackDefinition Definition { get; }
        public FpgSkillEventResult RuntimeEvent { get; }
        public FpgCompiledSkillEvent Event => RuntimeEvent.Event;
        public FpgSkillEventOutcome Outcome => RuntimeEvent.Outcome;
        public bool HasGameplayPayload { get; }
        public FpgCompiledEnemySkillPayloadSlot Payload { get; }
    }

    public readonly struct FpgFormalEnemySkillSequenceFrame
    {
        internal FpgFormalEnemySkillSequenceFrame(
            RuntimeId ownerRuntimeId,
            int spawnSequence,
            FpgEnemyAttackDefinition definition,
            FpgCompiledSkillSequence compiledSequence,
            SkillExecutionId executionId,
            TickIndex startTick,
            TickIndex tick,
            int relativeTick,
            FpgSkillExecutionState state)
        {
            OwnerRuntimeId = ownerRuntimeId;
            SpawnSequence = spawnSequence;
            Definition = definition;
            CompiledSequence = compiledSequence;
            ExecutionId = executionId;
            StartTick = startTick;
            Tick = tick;
            RelativeTick = relativeTick;
            State = state;
            ResolvedAnimationId = compiledSequence.ResolveAnimation(
                executionId);
        }

        public RuntimeId OwnerRuntimeId { get; }
        public int SpawnSequence { get; }
        public FpgEnemyAttackDefinition Definition { get; }
        public FpgCompiledSkillSequence CompiledSequence { get; }
        public SkillExecutionId ExecutionId { get; }
        public TickIndex StartTick { get; }
        public TickIndex Tick { get; }
        public int RelativeTick { get; }
        public FpgSkillExecutionState State { get; }
        public int ResolvedAnimationId { get; }
        public bool IsTerminal => State == FpgSkillExecutionState.Completed
            || State == FpgSkillExecutionState.Canceled;
    }

    /// <summary>
    /// Compiles every enemy attack as one Execute sequence, reserves its whole
    /// gameplay footprint before presentation starts, and submits each authored
    /// gameplay event on its exact timeline tick.
    /// </summary>
    public sealed class FpgFormalEnemyAttackScheduler
    {
        private const ulong SummonSelectionDomain = 0x4650475F53554D4DUL;

        private readonly FpgMultiEnemyCombatPort combatPort;
        private readonly FpgEncounterRunContext runContext;
        private readonly IFpgFormalEnemyAttackSpatialSampler spatialSampler;
        private readonly OwnerState[] owners;
        private readonly PatternState[] patterns;
        private readonly FpgFormalEnemySkillSequenceFrame[] sequenceFrames;
        private readonly Dictionary<FpgEnemyDefinition, PreparedEnemyState>
            preparedEnemies;
        private readonly FpgSkillExecutionIdAllocator executionIds;
        private readonly bool ownsExecutionIds;
        private SummonActionQuotaState[] summonActionQuotas =
            Array.Empty<SummonActionQuotaState>();
        private SummonOwnerQuotaState[] summonOwnerQuotas =
            Array.Empty<SummonOwnerQuotaState>();

        private TickIndex lastTick = TickIndex.Invalid;
        private long nextScheduleSequence;
        private int ownerCount;
        private int patternCount;
        private int sequenceFrameCount;
        private int preparedEventCapacity;
        private int preparedPayloadCapacity;
        private int preparedSummonQuotaCapacity;

        public FpgFormalEnemyAttackScheduler(
            FpgMultiEnemyCombatPort combatPort,
            FpgEncounterRunContext runContext,
            IFpgFormalEnemyAttackSpatialSampler spatialSampler,
            int ownerCapacity,
            int patternCapacity,
            FpgSkillExecutionIdAllocator executionIds = null)
        {
            this.combatPort = combatPort
                ?? throw new ArgumentNullException(nameof(combatPort));
            this.spatialSampler = spatialSampler
                ?? throw new ArgumentNullException(nameof(spatialSampler));
            if (!runContext.IsValid)
            {
                throw new ArgumentException(
                    "Formal attack scheduler requires a valid run context.",
                    nameof(runContext));
            }

            if (ownerCapacity <= 0 || patternCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerCapacity));
            }

            this.runContext = runContext;
            ownsExecutionIds = executionIds == null;
            this.executionIds = executionIds
                ?? new FpgSkillExecutionIdAllocator();
            owners = new OwnerState[ownerCapacity];
            patterns = new PatternState[patternCapacity];
            for (int index = 0; index < patterns.Length; index++)
            {
                patterns[index] = new PatternState();
            }

            sequenceFrames = new FpgFormalEnemySkillSequenceFrame[
                checked(patternCapacity * 2)];
            preparedEnemies =
                new Dictionary<FpgEnemyDefinition, PreparedEnemyState>(
                    ownerCapacity);
        }

        public int OwnerCapacity => owners.Length;
        public int PatternCapacity => patterns.Length;
        public int RegisteredOwnerCount => ownerCount;
        public int RegisteredPatternCount => patternCount;
        public int RegisteredSummonActionCount => CountSummonActionIds();
        public int SequenceFrameCapacity => sequenceFrames.Length;
        public int SequenceFrameCount => sequenceFrameCount;
        public TickIndex LastTick => lastTick;
        public int PresentationCallbackFaultCount { get; private set; }
        public int ActiveSummonQuotaActionCount =>
            CountActiveSummonQuotaActions();

        public event Action<FpgFormalEnemySkillStartedEvent> SkillStarted;
        public event Action<FpgFormalEnemySkillTimelineEvent> TimelineEvent;

        public FpgFormalEnemySkillSequenceFrame GetSequenceFrame(int index)
        {
            if (index < 0 || index >= sequenceFrameCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return sequenceFrames[index];
        }

        public bool TryGetSummonQuotaState(
            int actionStableId,
            out int committed,
            out int reserved)
        {
            int index = FindSummonActionQuota(actionStableId);
            if (index < 0)
            {
                committed = 0;
                reserved = 0;
                return false;
            }

            committed = summonActionQuotas[index].Committed;
            reserved = summonActionQuotas[index].Reserved;
            return true;
        }

        public bool TryGetOwnerSummonQuotaState(
            RuntimeId ownerRuntimeId,
            int actionStableId,
            out int committed,
            out int reserved)
        {
            int index = FindSummonOwnerQuota(
                ownerRuntimeId,
                actionStableId);
            if (index < 0)
            {
                committed = 0;
                reserved = 0;
                return false;
            }

            committed = summonOwnerQuotas[index].Committed;
            reserved = summonOwnerQuotas[index].Reserved;
            return true;
        }
        public DomainResult TryPrepareEnemyDefinition(
            FpgEnemyDefinition definition)
        {
            if (definition == null || definition.AttackPatternCount <= 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (preparedEnemies.ContainsKey(definition))
            {
                return DomainResult.Success;
            }

            if (lastTick.IsValid || ownerCount > 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            PreparedPatternState[] preparedPatterns =
                new PreparedPatternState[definition.AttackPatternCount];
            int requiredEventCapacity = preparedEventCapacity;
            int requiredPayloadCapacity = preparedPayloadCapacity;
            int additionalSummonQuotaCapacity = 0;
            for (int ordinal = 0;
                ordinal < definition.AttackPatternCount;
                ordinal++)
            {
                FpgEnemyAttackDefinition attack =
                    definition.GetAttackPattern(ordinal);
                if (attack == null
                    || !attack.TryCompile(
                        out FpgCompiledEnemySkillDefinition compiledAttack,
                        out _)
                    || !compiledAttack.Timeline.TryGetSequence(
                        FpgSkillSequenceKind.Execute,
                        out FpgCompiledSkillSequence execute))
                {
                    return DomainResult.Rejected(
                        RejectReason.InvalidDefinition);
                }

                preparedPatterns[ordinal] = new PreparedPatternState(
                    attack,
                    compiledAttack,
                    execute);
                requiredEventCapacity = Math.Max(
                    requiredEventCapacity,
                    execute.EventCount);
                requiredPayloadCapacity = Math.Max(
                    requiredPayloadCapacity,
                    compiledAttack.PayloadSlotCount);
                for (int payloadIndex = 0;
                    payloadIndex < compiledAttack.PayloadSlotCount;
                    payloadIndex++)
                {
                    FpgCompiledEnemySkillPayloadSlot payload =
                        compiledAttack.PayloadSlots[payloadIndex];
                    if (payload.Kind == FpgEnemySkillPayloadKind.Summon
                        && payload.SummonPayload.OccupancyMode
                            == FpgSummonOccupancyMode.AdditionalEntity)
                    {
                        if (additionalSummonQuotaCapacity == int.MaxValue)
                        {
                            return DomainResult.Rejected(
                                RejectReason.BufferCapacity);
                        }

                        additionalSummonQuotaCapacity++;
                    }
                }
            }

            if (requiredEventCapacity > preparedEventCapacity
                || requiredPayloadCapacity > preparedPayloadCapacity)
            {
                for (int index = 0; index < patterns.Length; index++)
                {
                    patterns[index].EnsureCapacity(
                        requiredEventCapacity,
                        requiredPayloadCapacity);
                }

                preparedEventCapacity = requiredEventCapacity;
                preparedPayloadCapacity = requiredPayloadCapacity;
            }

            if (additionalSummonQuotaCapacity > 0)
            {
                if (preparedSummonQuotaCapacity
                    > int.MaxValue - additionalSummonQuotaCapacity)
                {
                    return DomainResult.Rejected(
                        RejectReason.BufferCapacity);
                }

                int requiredSummonQuotaCapacity =
                    preparedSummonQuotaCapacity
                    + additionalSummonQuotaCapacity;
                if (!EnsureSummonQuotaCapacity(
                        requiredSummonQuotaCapacity))
                {
                    return DomainResult.Rejected(
                        RejectReason.BufferCapacity);
                }

                preparedSummonQuotaCapacity =
                    requiredSummonQuotaCapacity;
            }

            preparedEnemies.Add(
                definition,
                new PreparedEnemyState(preparedPatterns));
            return DomainResult.Success;
        }
        public DomainResult TryRegisterEnemy(
            RuntimeId runtimeId,
            int spawnSequence,
            TickIndex activationTick,
            int recursionDepth,
            FpgEnemyDefinition definition)
        {
            if (!runtimeId.IsValid
                || spawnSequence < 0
                || !activationTick.IsValid
                || recursionDepth < 0
                || definition == null
                || definition.AttackPatternCount <= 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (FindOwner(runtimeId) >= 0
                || FindOwnerBySpawnSequence(spawnSequence) >= 0)
            {
                return DomainResult.Rejected(RejectReason.DuplicateSequence);
            }

            int ownerIndex = FindFreeOwner();
            if (ownerIndex < 0
                || CountFreePatterns() < definition.AttackPatternCount)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            if (!preparedEnemies.TryGetValue(
                    definition,
                    out PreparedEnemyState prepared))
            {
                DomainResult preparedResult =
                    TryPrepareEnemyDefinition(definition);
                if (!preparedResult.IsSuccess
                    || !preparedEnemies.TryGetValue(
                        definition,
                        out prepared))
                {
                    return preparedResult.IsSuccess
                        ? DomainResult.Rejected(
                            RejectReason.InvalidDefinition)
                        : preparedResult;
                }
            }

            if (prepared.PatternCount != definition.AttackPatternCount)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            for (int ordinal = 0;
                ordinal < definition.AttackPatternCount;
                ordinal++)
            {
                PreparedPatternState preparedPattern =
                    prepared.GetPattern(ordinal);
                if (preparedPattern.Attack
                        != definition.GetAttackPattern(ordinal)
                    || !TryAddTicks(
                        activationTick,
                        preparedPattern.Attack.FirstReadyOffsetTicks,
                        out _))
                {
                    return DomainResult.Rejected(
                        RejectReason.InvalidDefinition);
                }
            }

            owners[ownerIndex] = new OwnerState(
                runtimeId,
                spawnSequence,
                activationTick,
                recursionDepth,
                definition);
            ownerCount++;

            for (int ordinal = 0;
                ordinal < definition.AttackPatternCount;
                ordinal++)
            {
                int patternIndex = FindFreePattern();
                PreparedPatternState preparedPattern =
                    prepared.GetPattern(ordinal);
                TryAddTicks(
                    activationTick,
                    preparedPattern.Attack.FirstReadyOffsetTicks,
                    out TickIndex firstReadyTick);
                patterns[patternIndex].Initialize(
                    ownerIndex,
                    ordinal,
                    preparedPattern,
                    firstReadyTick);
                patternCount++;
            }

            return DomainResult.Success;
        }

        public DomainResult TryUnregisterEnemy(RuntimeId runtimeId)
        {
            int ownerIndex = FindOwner(runtimeId);
            if (ownerIndex < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            for (int index = 0; index < patterns.Length; index++)
            {
                PatternState pattern = patterns[index];
                if (!pattern.IsUsed || pattern.OwnerIndex != ownerIndex)
                {
                    continue;
                }

                if (pattern.Runtime.IsRunning)
                {
                    DomainResult interrupted = InterruptPattern(
                        pattern,
                        pattern.Runtime.NextTick,
                        appendFrame: false);
                    if (!interrupted.IsSuccess)
                    {
                        return interrupted;
                    }
                }
                else
                {
                    ReleasePatternReservations(pattern);
                    ClearOwnerActivePattern(pattern);
                }

                pattern.Release();
                patternCount--;
            }

            owners[ownerIndex] = default(OwnerState);
            ownerCount--;
            return DomainResult.Success;
        }

        public DomainResult Tick(TickIndex tick)
        {
            if (!tick.IsValid
                || (lastTick.IsValid && tick.Value != lastTick.Value + 1L))
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            lastTick = tick;
            if (sequenceFrameCount > 0)
            {
                Array.Clear(sequenceFrames, 0, sequenceFrameCount);
                sequenceFrameCount = 0;
            }

            for (int index = 0; index < patterns.Length; index++)
            {
                PatternState pattern = patterns[index];
                if (!pattern.IsUsed || !pattern.Runtime.IsRunning)
                {
                    continue;
                }

                OwnerState owner = owners[pattern.OwnerIndex];
                DomainResult advanced = combatPort.CanAttack(owner.RuntimeId)
                    ? AdvancePattern(pattern, tick)
                    : InterruptPattern(pattern, tick);
                if (!advanced.IsSuccess)
                {
                    return advanced;
                }
            }

            int processed = 0;
            while (processed++ < patterns.Length)
            {
                int patternIndex = FindBestDuePattern(tick);
                if (patternIndex < 0)
                {
                    return DomainResult.Success;
                }

                PatternState pattern = patterns[patternIndex];
                pattern.LastProcessedTick = tick;
                StartPatternResult started = TryStartPattern(pattern, tick);
                if (started == StartPatternResult.Fault)
                {
                    return DomainResult.Rejected(RejectReason.InvariantFault);
                }

                if (started == StartPatternResult.Started)
                {
                    DomainResult advanced = AdvancePattern(pattern, tick);
                    if (!advanced.IsSuccess)
                    {
                        return advanced;
                    }
                }
            }

            return DomainResult.Success;
        }

        public void Clear()
        {
            for (int index = 0; index < patterns.Length; index++)
            {
                PatternState pattern = patterns[index];
                if (pattern.IsUsed)
                {
                    ReleasePatternReservations(pattern);
                    pattern.Release();
                }
            }

            Array.Clear(owners, 0, owners.Length);
            Array.Clear(
                summonActionQuotas,
                0,
                summonActionQuotas.Length);
            Array.Clear(
                summonOwnerQuotas,
                0,
                summonOwnerQuotas.Length);
            if (ownsExecutionIds)
            {
                executionIds.Reset();
            }
            lastTick = TickIndex.Invalid;
            nextScheduleSequence = 0L;
            ownerCount = 0;
            patternCount = 0;
            if (sequenceFrameCount > 0)
            {
                Array.Clear(sequenceFrames, 0, sequenceFrameCount);
                sequenceFrameCount = 0;
            }
            PresentationCallbackFaultCount = 0;
        }

        private StartPatternResult TryStartPattern(
            PatternState pattern,
            TickIndex tick)
        {
            OwnerState owner = owners[pattern.OwnerIndex];
            if (!owner.IsUsed
                || owner.ActivePatternIndex >= 0
                || !combatPort.CanAttack(owner.RuntimeId)
                || pattern.Occurrence == long.MaxValue
                || nextScheduleSequence < 0L
                || nextScheduleSequence
                    > long.MaxValue - pattern.GameplayEventCount)
            {
                return pattern.Occurrence == long.MaxValue
                        || nextScheduleSequence < 0L
                        || nextScheduleSequence
                            > long.MaxValue - pattern.GameplayEventCount
                    ? StartPatternResult.Fault
                    : StartPatternResult.Deferred;
            }

            if (!TryAddTicks(
                    tick,
                    pattern.Execute.DurationTicks,
                    out TickIndex plannedEndTick)
                || !TryAddTicks(
                    plannedEndTick,
                    pattern.Compiled.SequenceCooldownTicks,
                    out TickIndex nextReadyTick))
            {
                return StartPatternResult.Fault;
            }

            DomainResult capacity = combatPort.TryReserveEnemySkillCapacity(
                owner.RuntimeId,
                pattern.GameplayEventCount,
                pattern.Compiled.TotalProjectileCapacity,
                pattern.Compiled.TotalImpactCapacity,
                pattern.Compiled.TotalSummonCapacity,
                pattern.MaxConcurrentThreats,
                out FpgEnemySkillCapacityReservation capacityReservation);
            if (!capacity.IsSuccess)
            {
                return IsCapacityDeferral(capacity.RejectReason)
                    ? StartPatternResult.Deferred
                    : StartPatternResult.Fault;
            }

            pattern.CapacityReservation = capacityReservation;
            DomainResult budget = ReserveProjectileBudgets(pattern);
            if (!budget.IsSuccess)
            {
                ReleasePatternReservations(pattern);
                return IsCapacityDeferral(budget.RejectReason)
                    ? StartPatternResult.Deferred
                    : StartPatternResult.Fault;
            }

            SummonQuotaReserveResult quota =
                TryReserveSummonQuotas(pattern, owner);
            if (quota != SummonQuotaReserveResult.Reserved)
            {
                ReleasePatternReservations(pattern);
                return quota == SummonQuotaReserveResult.Fault
                    ? StartPatternResult.Fault
                    : StartPatternResult.Deferred;
            }

            SkillExecutionId executionId;
            try
            {
                executionId = executionIds.Peek();
            }
            catch (OverflowException)
            {
                ReleasePatternReservations(pattern);
                return StartPatternResult.Fault;
            }

            FpgSkillRuntimeResult runtimeStart = pattern.Runtime.Start(
                pattern.Execute,
                executionId,
                tick);
            if (!runtimeStart.IsSuccess)
            {
                pattern.Runtime.Reset();
                ReleasePatternReservations(pattern);
                return StartPatternResult.Fault;
            }

            try
            {
                executionIds.Commit(executionId);
            }
            catch (InvalidOperationException)
            {
                pattern.Runtime.Reset();
                ReleasePatternReservations(pattern);
                return StartPatternResult.Fault;
            }

            pattern.BeginExecution();
            pattern.NextReadyTick = nextReadyTick;
            pattern.Occurrence++;
            owner.ActivePatternIndex = FindPattern(pattern);
            owner.LastStartTick = tick;
            owners[pattern.OwnerIndex] = owner;
            PublishSkillStarted(
                new FpgFormalEnemySkillStartedEvent(
                    owner.RuntimeId,
                    owner.SpawnSequence,
                    pattern.Attack,
                    executionId,
                    tick,
                    plannedEndTick));
            return StartPatternResult.Started;
        }
        private DomainResult AdvancePattern(
            PatternState pattern,
            TickIndex tick)
        {
            FpgSkillRuntimeResult ticked = pattern.Runtime.Tick(tick);
            if (!ticked.IsSuccess)
            {
                return MapRuntimeFailure(ticked.Error);
            }

            for (int resultIndex = 0;
                resultIndex < pattern.Runtime.ResultCount;
                resultIndex++)
            {
                FpgSkillEventResult result =
                    pattern.Runtime.GetResult(resultIndex);
                DomainResult submitted =
                    SubmitTriggeredGameplayEvent(pattern, result);
                if (!submitted.IsSuccess)
                {
                    return submitted;
                }
            }

            for (int resultIndex = 0;
                resultIndex < pattern.Runtime.ResultCount;
                resultIndex++)
            {
                FpgSkillEventResult result =
                    pattern.Runtime.GetResult(resultIndex);
                DomainResult handled = HandleTimelineEvent(
                    pattern,
                    result);
                if (!handled.IsSuccess)
                {
                    return handled;
                }
            }

            if (!TryAppendSequenceFrame(pattern, tick))
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            if (pattern.Runtime.IsTerminal)
            {
                DomainResult completed = combatPort.CompleteEnemySkillCapacity(
                    pattern.CapacityReservation);
                if (!completed.IsSuccess)
                {
                    return completed;
                }

                pattern.CapacityReservation =
                    FpgEnemySkillCapacityReservation.Invalid;
                ReleaseSummonQuotaReservations(pattern);
                ClearOwnerActivePattern(pattern);
            }

            return DomainResult.Success;
        }

        private DomainResult InterruptPattern(
            PatternState pattern,
            TickIndex tick,
            bool appendFrame = true)
        {
            FpgSkillRuntimeResult canceled =
                pattern.Runtime.CancelRemaining(tick);
            if (!canceled.IsSuccess)
            {
                return MapRuntimeFailure(canceled.Error);
            }

            OwnerState owner = owners[pattern.OwnerIndex];
            for (int index = 0;
                index < pattern.Runtime.ResultCount;
                index++)
            {
                FpgSkillEventResult result = pattern.Runtime.GetResult(index);
                FpgCompiledEnemySkillPayloadSlot payload =
                    default(FpgCompiledEnemySkillPayloadSlot);
                bool hasPayload = result.Event.Kind
                    == FpgSkillEventKind.GameplayPayload
                    && pattern.Compiled.TryGetPayloadSlot(
                        result.Event.PayloadSlotId,
                        out payload);
                PublishTimelineEvent(
                    new FpgFormalEnemySkillTimelineEvent(
                        owner.RuntimeId,
                        owner.SpawnSequence,
                        pattern.Attack,
                        result,
                        hasPayload
                            ? payload
                            : default(FpgCompiledEnemySkillPayloadSlot),
                        hasPayload));
            }

            if (appendFrame
                && !TryAppendSequenceFrame(pattern, tick))
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            ReleasePatternReservations(pattern);
            ClearOwnerActivePattern(pattern);
            return DomainResult.Success;
        }

        private DomainResult SubmitTriggeredGameplayEvent(
            PatternState pattern,
            FpgSkillEventResult result)
        {
            if (result.Event.Kind != FpgSkillEventKind.GameplayPayload
                || result.Outcome
                    != FpgSkillEventOutcome.Triggered)
            {
                return DomainResult.Success;
            }

            if (!pattern.Compiled.TryGetPayloadSlot(
                    result.Event.PayloadSlotId,
                    out FpgCompiledEnemySkillPayloadSlot payload))
            {
                return DomainResult.Rejected(
                    RejectReason.InvalidDefinition);
            }

            OwnerState owner = owners[pattern.OwnerIndex];
            DomainResult submitted = SubmitGameplayEvent(
                pattern,
                owner,
                result.Event,
                payload);
            if (submitted.IsSuccess)
            {
                pattern.MarkGameplayEventSucceeded(
                    result.Event.EventId);
            }

            return submitted;
        }

        private DomainResult HandleTimelineEvent(
            PatternState pattern,
            FpgSkillEventResult result)
        {
            OwnerState owner = owners[pattern.OwnerIndex];
            FpgCompiledEnemySkillPayloadSlot payload =
                default(FpgCompiledEnemySkillPayloadSlot);
            bool hasPayload = result.Event.Kind
                == FpgSkillEventKind.GameplayPayload;
            if (hasPayload
                && !pattern.Compiled.TryGetPayloadSlot(
                    result.Event.PayloadSlotId,
                    out payload))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (result.Event.Kind == FpgSkillEventKind.PresentationCue
                && result.Outcome == FpgSkillEventOutcome.Triggered
                && result.Event.BoundGameplayEventId != 0
                && !pattern.HasSuccessfulGameplayEvent(
                    result.Event.BoundGameplayEventId))
            {
                return DomainResult.Success;
            }

            PublishTimelineEvent(
                new FpgFormalEnemySkillTimelineEvent(
                    owner.RuntimeId,
                    owner.SpawnSequence,
                    pattern.Attack,
                    result,
                    payload,
                    hasPayload));
            return DomainResult.Success;
        }

        private DomainResult SubmitGameplayEvent(
            PatternState pattern,
            OwnerState owner,
            FpgCompiledSkillEvent skillEvent,
            FpgCompiledEnemySkillPayloadSlot payloadSlot)
        {
            if (nextScheduleSequence < 0L
                || nextScheduleSequence == long.MaxValue)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            TickIndex eventTick = new TickIndex(
                pattern.Runtime.StartTick.Value + skillEvent.Tick);
            if (!FpgEnemySkillGameplayEventResolver.TryResolveSocketName(
                    pattern.Attack,
                    skillEvent,
                    out string socketName))
            {
                return DomainResult.Rejected(
                    RejectReason.InvalidDefinition);
            }

            DomainResult sampled = spatialSampler.TrySample(
                eventTick,
                owner.RuntimeId,
                combatPort.Player.RuntimeId,
                socketName,
                skillEvent,
                out FpgEnemyAttackSpatialContext spatialContext);
            if (!sampled.IsSuccess)
            {
                return sampled;
            }

            long scheduleSequence = nextScheduleSequence;
            DomainResult payloadResult = BuildPayload(
                pattern,
                owner,
                skillEvent,
                payloadSlot,
                scheduleSequence,
                out FpgEnemyAttackPayload payload);
            if (!payloadResult.IsSuccess)
            {
                return payloadResult;
            }

            ReservationToken budgetReservation =
                pattern.EventBudgetReservations[
                    pattern.FindEventIndex(skillEvent.EventId)];
            FpgAttackScheduleRequest schedule = new FpgAttackScheduleRequest(
                owner.RuntimeId,
                eventTick,
                pattern.Attack.Priority,
                scheduleSequence,
                pattern.Attack.SkillId,
                pattern.Runtime.ExecutionId,
                skillEvent.EventId);
            DomainResult submitted = combatPort.TrySubmitEnemyAttack(
                new FpgEnemyAttackCommand(
                    schedule,
                    owner.SpawnSequence,
                    payload,
                    pattern.CapacityReservation,
                    budgetReservation,
                    spatialContext));
            if (!submitted.IsSuccess)
            {
                return submitted;
            }

            if (payloadSlot.Kind == FpgEnemySkillPayloadKind.Summon
                && !CommitSummonQuotaReservation(
                    pattern,
                    payloadSlot.SummonPayload.ActionStableId))
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            nextScheduleSequence++;
            int eventIndex = pattern.FindEventIndex(skillEvent.EventId);
            pattern.EventBudgetReservations[eventIndex] =
                default(ReservationToken);

            return DomainResult.Success;
        }
        private DomainResult BuildPayload(
            PatternState pattern,
            OwnerState owner,
            FpgCompiledSkillEvent skillEvent,
            FpgCompiledEnemySkillPayloadSlot payloadSlot,
            long scheduleSequence,
            out FpgEnemyAttackPayload payload)
        {
            payload = default(FpgEnemyAttackPayload);
            if (payloadSlot.Kind == FpgEnemySkillPayloadKind.Projectile
                || payloadSlot.Kind == FpgEnemySkillPayloadKind.TimedImpact)
            {
                ThreatDefinition threat = new ThreatDefinition(
                    payloadSlot.ThreatDefinitionId,
                    TickDuration.Zero,
                    TickDuration.Zero,
                    TickDuration.Zero,
                    payloadSlot.ThreatPayload);
                payload = FpgEnemyAttackPayload.ForThreat(threat);
                return DomainResult.Success;
            }

            if (payloadSlot.Kind != FpgEnemySkillPayloadKind.Summon
                || payloadSlot.SummonPayload == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            FpgCompiledEnemySummonPayload summon =
                payloadSlot.SummonPayload;
            if (owner.RecursionDepth >= summon.MaxRecursionDepth
                || owner.RecursionDepth == int.MaxValue)
            {
                return DomainResult.Rejected(RejectReason.OwnerInterrupted);
            }

            long occurrence = CountSummonOccurrences(
                summon.ActionStableId);
            ulong ownerKey = StableHash.Combine(
                unchecked((ulong)owner.SpawnSequence),
                SummonSelectionDomain,
                unchecked((ulong)pattern.Compiled.Timeline.SkillId),
                unchecked((ulong)skillEvent.EventId));
            ulong random = runContext.DeriveSeed(
                SummonSelectionDomain,
                ownerKey,
                unchecked((ulong)occurrence));
            ulong selectedWeight = random % summon.TotalCandidateWeight;
            FpgCompiledEnemySummonCandidate selected =
                default(FpgCompiledEnemySummonCandidate);
            for (int index = 0; index < summon.CandidateCount; index++)
            {
                FpgCompiledEnemySummonCandidate candidate =
                    summon.GetCandidate(index);
                ulong weight = unchecked((ulong)candidate.Weight);
                if (selectedWeight < weight)
                {
                    selected = candidate;
                    break;
                }

                selectedWeight -= weight;
            }

            if (selected.Definition == null)
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            FpgSummonRequest request = new FpgSummonRequest(
                owner.RuntimeId,
                selected.EnemyDefinitionId,
                owner.RecursionDepth + 1,
                scheduleSequence,
                summon.ActionId,
                summon.MaxSummonsPerOwner,
                summon.OccupancyMode,
                summon.PlacementMode);
            payload = FpgEnemyAttackPayload.ForSummon(
                new FpgFormalSummonPayload(
                    request,
                    summon.MaxSummonsPerOwner,
                    0,
                    summon.OwnerOutcome));
            return DomainResult.Success;
        }

        private DomainResult ReserveProjectileBudgets(PatternState pattern)
        {
            for (int eventIndex = 0;
                eventIndex < pattern.Execute.EventCount;
                eventIndex++)
            {
                FpgCompiledSkillEvent skillEvent =
                    pattern.Execute.GetEvent(eventIndex);
                if (skillEvent.Kind != FpgSkillEventKind.GameplayPayload
                    || !pattern.Compiled.TryGetPayloadSlot(
                        skillEvent.PayloadSlotId,
                        out FpgCompiledEnemySkillPayloadSlot payload)
                    || payload.Kind != FpgEnemySkillPayloadKind.Projectile)
                {
                    continue;
                }

                DomainResult reserved = combatPort.CombatKernel
                    .ProjectileBudget.TryReserve(
                        payload.ThreatPayload.TotalBudgetUnits,
                        out ReservationToken token);
                if (!reserved.IsSuccess)
                {
                    return reserved;
                }

                pattern.EventBudgetReservations[eventIndex] = token;
            }

            return DomainResult.Success;
        }

        private void ReleasePatternReservations(PatternState pattern)
        {
            ReleaseSummonQuotaReservations(pattern);
            for (int index = 0;
                index < pattern.EventBudgetReservations.Length;
                index++)
            {
                ReservationToken token =
                    pattern.EventBudgetReservations[index];
                if (token.IsValid)
                {
                    combatPort.CombatKernel.ProjectileBudget
                        .ReleaseReservation(token);
                    pattern.EventBudgetReservations[index] =
                        default(ReservationToken);
                }
            }

            if (pattern.CapacityReservation.IsValid)
            {
                combatPort.ReleaseEnemySkillCapacity(
                    pattern.CapacityReservation);
                pattern.CapacityReservation =
                    FpgEnemySkillCapacityReservation.Invalid;
            }
        }

        private SummonQuotaReserveResult TryReserveSummonQuotas(
            PatternState pattern,
            OwnerState owner)
        {
            pattern.ClearSummonQuotaReservations();
            for (int payloadIndex = 0;
                payloadIndex < pattern.Compiled.PayloadSlotCount;
                payloadIndex++)
            {
                FpgCompiledEnemySkillPayloadSlot payload =
                    pattern.Compiled.PayloadSlots[payloadIndex];
                if (payload.Kind != FpgEnemySkillPayloadKind.Summon)
                {
                    continue;
                }

                int planned = pattern.CountExecuteEvents(payload.SlotId);
                if (planned == 0)
                {
                    continue;
                }

                FpgCompiledEnemySummonPayload summon =
                    payload.SummonPayload;
                if (owner.RecursionDepth >= summon.MaxRecursionDepth)
                {
                    pattern.IsDisabled = true;
                    pattern.ClearSummonQuotaReservations();
                    return SummonQuotaReserveResult.Disabled;
                }

                if (summon.OccupancyMode
                    == FpgSummonOccupancyMode.ReplaceOwner)
                {
                    continue;
                }

                if (summon.OccupancyMode
                        != FpgSummonOccupancyMode.AdditionalEntity
                    || summon.MaxSummonsPerOwner <= 0
                    || summon.MaxTotalSummonsPerEncounter <= 0)
                {
                    pattern.ClearSummonQuotaReservations();
                    return SummonQuotaReserveResult.Fault;
                }

                int actionQuotaIndex = FindSummonActionQuota(
                    summon.ActionStableId);
                if (actionQuotaIndex >= 0)
                {
                    SummonActionQuotaState actionQuota =
                        summonActionQuotas[actionQuotaIndex];
                    if (actionQuota.MaxTotal
                        != summon.MaxTotalSummonsPerEncounter)
                    {
                        pattern.ClearSummonQuotaReservations();
                        return SummonQuotaReserveResult.Fault;
                    }

                    if ((long)actionQuota.Committed + planned
                        > actionQuota.MaxTotal)
                    {
                        pattern.IsDisabled = true;
                        pattern.ClearSummonQuotaReservations();
                        return SummonQuotaReserveResult.Disabled;
                    }

                    if ((long)actionQuota.Committed
                            + actionQuota.Reserved
                            + planned
                        > actionQuota.MaxTotal)
                    {
                        pattern.ClearSummonQuotaReservations();
                        return SummonQuotaReserveResult.Deferred;
                    }
                }
                else
                {
                    actionQuotaIndex = FindFreeSummonActionQuota(pattern);
                    if (actionQuotaIndex < 0)
                    {
                        pattern.ClearSummonQuotaReservations();
                        return SummonQuotaReserveResult.Fault;
                    }
                }

                int ownerQuotaIndex = FindSummonOwnerQuota(
                    owner.RuntimeId,
                    summon.ActionStableId);
                if (ownerQuotaIndex >= 0)
                {
                    SummonOwnerQuotaState ownerQuota =
                        summonOwnerQuotas[ownerQuotaIndex];
                    if (ownerQuota.MaxPerOwner
                        != summon.MaxSummonsPerOwner)
                    {
                        pattern.ClearSummonQuotaReservations();
                        return SummonQuotaReserveResult.Fault;
                    }

                    if ((long)ownerQuota.Committed + planned
                        > ownerQuota.MaxPerOwner)
                    {
                        pattern.IsDisabled = true;
                        pattern.ClearSummonQuotaReservations();
                        return SummonQuotaReserveResult.Disabled;
                    }

                    if ((long)ownerQuota.Committed
                            + ownerQuota.Reserved
                            + planned
                        > ownerQuota.MaxPerOwner)
                    {
                        pattern.ClearSummonQuotaReservations();
                        return SummonQuotaReserveResult.Deferred;
                    }
                }
                else
                {
                    ownerQuotaIndex = FindFreeSummonOwnerQuota(pattern);
                    if (ownerQuotaIndex < 0)
                    {
                        pattern.ClearSummonQuotaReservations();
                        return SummonQuotaReserveResult.Fault;
                    }
                }

                if (!pattern.TryAddSummonQuotaReservation(
                        new SummonQuotaReservation(
                            summon.ActionStableId,
                            planned,
                            actionQuotaIndex,
                            ownerQuotaIndex)))
                {
                    pattern.ClearSummonQuotaReservations();
                    return SummonQuotaReserveResult.Fault;
                }
            }

            for (int index = 0;
                index < pattern.SummonQuotaReservationCount;
                index++)
            {
                SummonQuotaReservation reservation =
                    pattern.GetSummonQuotaReservation(index);
                ref SummonActionQuotaState actionQuota =
                    ref summonActionQuotas[reservation.ActionQuotaIndex];
                if (!actionQuota.IsUsed)
                {
                    FpgCompiledEnemySummonPayload summon =
                        FindCompiledSummon(
                            pattern,
                            reservation.ActionStableId);
                    if (summon == null)
                    {
                        pattern.ClearSummonQuotaReservations();
                        return SummonQuotaReserveResult.Fault;
                    }

                    actionQuota.ActionStableId =
                        reservation.ActionStableId;
                    actionQuota.MaxTotal =
                        summon.MaxTotalSummonsPerEncounter;
                    actionQuota.IsUsed = true;
                }

                ref SummonOwnerQuotaState ownerQuota =
                    ref summonOwnerQuotas[reservation.OwnerQuotaIndex];
                if (!ownerQuota.IsUsed)
                {
                    FpgCompiledEnemySummonPayload summon =
                        FindCompiledSummon(
                            pattern,
                            reservation.ActionStableId);
                    if (summon == null)
                    {
                        pattern.ClearSummonQuotaReservations();
                        return SummonQuotaReserveResult.Fault;
                    }

                    ownerQuota.OwnerRuntimeId = owner.RuntimeId;
                    ownerQuota.ActionStableId =
                        reservation.ActionStableId;
                    ownerQuota.MaxPerOwner = summon.MaxSummonsPerOwner;
                    ownerQuota.IsUsed = true;
                }

                actionQuota.Reserved += reservation.RemainingReserved;
                ownerQuota.Reserved += reservation.RemainingReserved;
            }

            return SummonQuotaReserveResult.Reserved;
        }

        private bool CommitSummonQuotaReservation(
            PatternState pattern,
            int actionStableId)
        {
            if (!pattern.TryCommitSummonQuotaReservation(
                    actionStableId,
                    out SummonQuotaReservation reservation)
                || reservation.ActionQuotaIndex < 0
                || reservation.ActionQuotaIndex
                    >= summonActionQuotas.Length
                || reservation.OwnerQuotaIndex < 0
                || reservation.OwnerQuotaIndex
                    >= summonOwnerQuotas.Length)
            {
                return false;
            }

            ref SummonActionQuotaState actionQuota =
                ref summonActionQuotas[reservation.ActionQuotaIndex];
            ref SummonOwnerQuotaState ownerQuota =
                ref summonOwnerQuotas[reservation.OwnerQuotaIndex];
            if (!actionQuota.IsUsed
                || !ownerQuota.IsUsed
                || actionQuota.ActionStableId != actionStableId
                || ownerQuota.ActionStableId != actionStableId
                || actionQuota.Reserved <= 0
                || ownerQuota.Reserved <= 0
                || actionQuota.Committed >= actionQuota.MaxTotal
                || ownerQuota.Committed >= ownerQuota.MaxPerOwner)
            {
                return false;
            }

            actionQuota.Reserved--;
            actionQuota.Committed++;
            ownerQuota.Reserved--;
            ownerQuota.Committed++;
            return true;
        }

        private void ReleaseSummonQuotaReservations(PatternState pattern)
        {
            for (int index = 0;
                index < pattern.SummonQuotaReservationCount;
                index++)
            {
                SummonQuotaReservation reservation =
                    pattern.GetSummonQuotaReservation(index);
                if (reservation.RemainingReserved <= 0
                    || reservation.ActionQuotaIndex < 0
                    || reservation.ActionQuotaIndex
                        >= summonActionQuotas.Length
                    || reservation.OwnerQuotaIndex < 0
                    || reservation.OwnerQuotaIndex
                        >= summonOwnerQuotas.Length)
                {
                    continue;
                }

                ref SummonActionQuotaState actionQuota =
                    ref summonActionQuotas[reservation.ActionQuotaIndex];
                ref SummonOwnerQuotaState ownerQuota =
                    ref summonOwnerQuotas[reservation.OwnerQuotaIndex];
                actionQuota.Reserved = Math.Max(
                    0,
                    actionQuota.Reserved
                        - reservation.RemainingReserved);
                ownerQuota.Reserved = Math.Max(
                    0,
                    ownerQuota.Reserved
                        - reservation.RemainingReserved);
            }

            pattern.ClearSummonQuotaReservations();
        }

        private long CountSummonOccurrences(int actionStableId)
        {
            int index = FindSummonActionQuota(actionStableId);
            return index < 0
                ? 0L
                : summonActionQuotas[index].Committed;
        }

        private bool EnsureSummonQuotaCapacity(int actionCapacity)
        {
            if (actionCapacity <= summonActionQuotas.Length)
            {
                return true;
            }

            long ownerCapacity = (long)actionCapacity * owners.Length;
            if (actionCapacity <= 0
                || ownerCapacity <= 0L
                || ownerCapacity > int.MaxValue)
            {
                return false;
            }

            Array.Resize(ref summonActionQuotas, actionCapacity);
            Array.Resize(
                ref summonOwnerQuotas,
                checked((int)ownerCapacity));
            return true;
        }

        private int FindSummonActionQuota(int actionStableId)
        {
            for (int index = 0;
                index < summonActionQuotas.Length;
                index++)
            {
                if (summonActionQuotas[index].IsUsed
                    && summonActionQuotas[index].ActionStableId
                        == actionStableId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindSummonOwnerQuota(
            RuntimeId ownerRuntimeId,
            int actionStableId)
        {
            for (int index = 0;
                index < summonOwnerQuotas.Length;
                index++)
            {
                if (summonOwnerQuotas[index].IsUsed
                    && summonOwnerQuotas[index].OwnerRuntimeId
                        == ownerRuntimeId
                    && summonOwnerQuotas[index].ActionStableId
                        == actionStableId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeSummonActionQuota(PatternState pattern)
        {
            for (int index = 0;
                index < summonActionQuotas.Length;
                index++)
            {
                if (!summonActionQuotas[index].IsUsed
                    && !PatternUsesActionQuotaIndex(pattern, index))
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeSummonOwnerQuota(PatternState pattern)
        {
            for (int index = 0;
                index < summonOwnerQuotas.Length;
                index++)
            {
                if (!summonOwnerQuotas[index].IsUsed
                    && !PatternUsesOwnerQuotaIndex(pattern, index))
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool PatternUsesActionQuotaIndex(
            PatternState pattern,
            int quotaIndex)
        {
            for (int index = 0;
                index < pattern.SummonQuotaReservationCount;
                index++)
            {
                if (pattern.GetSummonQuotaReservation(index)
                        .ActionQuotaIndex
                    == quotaIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PatternUsesOwnerQuotaIndex(
            PatternState pattern,
            int quotaIndex)
        {
            for (int index = 0;
                index < pattern.SummonQuotaReservationCount;
                index++)
            {
                if (pattern.GetSummonQuotaReservation(index)
                        .OwnerQuotaIndex
                    == quotaIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private static FpgCompiledEnemySummonPayload FindCompiledSummon(
            PatternState pattern,
            int actionStableId)
        {
            for (int index = 0;
                index < pattern.Compiled.PayloadSlotCount;
                index++)
            {
                FpgCompiledEnemySkillPayloadSlot payload =
                    pattern.Compiled.PayloadSlots[index];
                if (payload.Kind == FpgEnemySkillPayloadKind.Summon
                    && payload.SummonPayload.ActionStableId
                        == actionStableId)
                {
                    return payload.SummonPayload;
                }
            }

            return null;
        }

        private int CountActiveSummonQuotaActions()
        {
            int count = 0;
            for (int index = 0;
                index < summonActionQuotas.Length;
                index++)
            {
                if (summonActionQuotas[index].IsUsed)
                {
                    count++;
                }
            }

            return count;
        }
        private int CountSummonActionIds()
        {
            int count = 0;
            for (int patternIndex = 0;
                patternIndex < patterns.Length;
                patternIndex++)
            {
                PatternState pattern = patterns[patternIndex];
                if (!pattern.IsUsed)
                {
                    continue;
                }

                for (int payloadIndex = 0;
                    payloadIndex < pattern.Compiled.PayloadSlotCount;
                    payloadIndex++)
                {
                    FpgCompiledEnemySkillPayloadSlot payload =
                        pattern.Compiled.PayloadSlots[payloadIndex];
                    if (payload.Kind != FpgEnemySkillPayloadKind.Summon)
                    {
                        continue;
                    }

                    int actionId = payload.SummonPayload.ActionStableId;
                    bool seen = false;
                    for (int previousPattern = 0;
                        previousPattern <= patternIndex && !seen;
                        previousPattern++)
                    {
                        PatternState previous = patterns[previousPattern];
                        if (!previous.IsUsed)
                        {
                            continue;
                        }

                        int payloadLimit = previousPattern == patternIndex
                            ? payloadIndex
                            : previous.Compiled.PayloadSlotCount;
                        for (int previousPayload = 0;
                            previousPayload < payloadLimit;
                            previousPayload++)
                        {
                            FpgCompiledEnemySkillPayloadSlot candidate =
                                previous.Compiled.PayloadSlots[
                                    previousPayload];
                            seen = candidate.Kind
                                    == FpgEnemySkillPayloadKind.Summon
                                && candidate.SummonPayload.ActionStableId
                                    == actionId;
                            if (seen)
                            {
                                break;
                            }
                        }
                    }

                    if (!seen)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private int FindBestDuePattern(TickIndex tick)
        {
            int best = -1;
            for (int index = 0; index < patterns.Length; index++)
            {
                PatternState candidate = patterns[index];
                if (!candidate.IsUsed
                    || candidate.IsDisabled
                    || candidate.Runtime.IsRunning
                    || candidate.NextReadyTick > tick
                    || candidate.LastProcessedTick == tick)
                {
                    continue;
                }

                OwnerState owner = owners[candidate.OwnerIndex];
                if (!owner.IsUsed
                    || owner.ActivePatternIndex >= 0
                    || owner.LastStartTick == tick
                    || !combatPort.CanAttack(owner.RuntimeId))
                {
                    continue;
                }

                if (best < 0
                    || Compare(candidate, patterns[best]) < 0)
                {
                    best = index;
                }
            }

            return best;
        }

        private int Compare(PatternState left, PatternState right)
        {
            int comparison = left.NextReadyTick.Value.CompareTo(
                right.NextReadyTick.Value);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = owners[left.OwnerIndex].SpawnSequence.CompareTo(
                owners[right.OwnerIndex].SpawnSequence);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Attack.Priority.CompareTo(
                right.Attack.Priority);
            return comparison != 0
                ? comparison
                : left.PatternOrdinal.CompareTo(right.PatternOrdinal);
        }

        private bool TryAppendSequenceFrame(
            PatternState pattern,
            TickIndex tick)
        {
            if (sequenceFrameCount >= sequenceFrames.Length
                || !tick.IsValid
                || !pattern.Runtime.StartTick.IsValid
                || tick.Value < pattern.Runtime.StartTick.Value)
            {
                return false;
            }

            long relativeValue =
                tick.Value - pattern.Runtime.StartTick.Value;
            if (relativeValue < 0L
                || relativeValue > pattern.Execute.DurationTicks
                || relativeValue > int.MaxValue)
            {
                return false;
            }

            OwnerState owner = owners[pattern.OwnerIndex];
            sequenceFrames[sequenceFrameCount++] =
                new FpgFormalEnemySkillSequenceFrame(
                    owner.RuntimeId,
                    owner.SpawnSequence,
                    pattern.Attack,
                    pattern.Execute,
                    pattern.Runtime.ExecutionId,
                    pattern.Runtime.StartTick,
                    tick,
                    (int)relativeValue,
                    pattern.Runtime.State);
            return true;
        }

        private void ClearOwnerActivePattern(PatternState pattern)
        {
            OwnerState owner = owners[pattern.OwnerIndex];
            if (owner.IsUsed)
            {
                owner.ActivePatternIndex = -1;
                owners[pattern.OwnerIndex] = owner;
            }
        }

        private int FindPattern(PatternState pattern)
        {
            for (int index = 0; index < patterns.Length; index++)
            {
                if (ReferenceEquals(patterns[index], pattern))
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindOwner(RuntimeId runtimeId)
        {
            if (!runtimeId.IsValid)
            {
                return -1;
            }

            for (int index = 0; index < owners.Length; index++)
            {
                if (owners[index].IsUsed
                    && owners[index].RuntimeId == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindOwnerBySpawnSequence(int spawnSequence)
        {
            for (int index = 0; index < owners.Length; index++)
            {
                if (owners[index].IsUsed
                    && owners[index].SpawnSequence == spawnSequence)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeOwner()
        {
            for (int index = 0; index < owners.Length; index++)
            {
                if (!owners[index].IsUsed)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreePattern()
        {
            for (int index = 0; index < patterns.Length; index++)
            {
                if (!patterns[index].IsUsed)
                {
                    return index;
                }
            }

            return -1;
        }

        private int CountFreePatterns()
        {
            return patterns.Length - patternCount;
        }

        private void PublishSkillStarted(
            FpgFormalEnemySkillStartedEvent started)
        {
            Action<FpgFormalEnemySkillStartedEvent> callbacks = SkillStarted;
            if (callbacks == null)
            {
                return;
            }

            try
            {
                callbacks(started);
            }
            catch (Exception)
            {
                IncrementPresentationCallbackFaultCount();
            }
        }

        private void PublishTimelineEvent(
            FpgFormalEnemySkillTimelineEvent skillEvent)
        {
            Action<FpgFormalEnemySkillTimelineEvent> callbacks = TimelineEvent;
            if (callbacks == null)
            {
                return;
            }

            try
            {
                callbacks(skillEvent);
            }
            catch (Exception)
            {
                IncrementPresentationCallbackFaultCount();
            }
        }

        private void IncrementPresentationCallbackFaultCount()
        {
            if (PresentationCallbackFaultCount < int.MaxValue)
            {
                PresentationCallbackFaultCount++;
            }
        }

        private static bool IsCapacityDeferral(RejectReason reason)
        {
            return reason == RejectReason.BufferCapacity
                || reason == RejectReason.BudgetExceeded
                || reason == RejectReason.OwnerGroggy
                || reason == RejectReason.OwnerInterrupted
                || reason == RejectReason.ActionLocked
                || reason == RejectReason.Cooldown;
        }

        private static DomainResult MapRuntimeFailure(
            FpgSkillRuntimeError error)
        {
            switch (error)
            {
                case FpgSkillRuntimeError.WrongTick:
                case FpgSkillRuntimeError.InvalidTick:
                    return DomainResult.Rejected(RejectReason.WrongTick);

                case FpgSkillRuntimeError.ResultCapacityExceeded:
                    return DomainResult.Rejected(RejectReason.BufferCapacity);

                default:
                    return DomainResult.Rejected(RejectReason.InvalidState);
            }
        }

        private static bool TryAddTicks(
            TickIndex start,
            int duration,
            out TickIndex result)
        {
            if (!start.IsValid
                || duration < 0
                || start.Value > long.MaxValue - duration)
            {
                result = TickIndex.Invalid;
                return false;
            }

            result = new TickIndex(start.Value + duration);
            return true;
        }

        private enum SummonQuotaReserveResult
        {
            Reserved = 0,
            Deferred,
            Disabled,
            Fault
        }

        private struct SummonQuotaReservation
        {
            public SummonQuotaReservation(
                int actionStableId,
                int remainingReserved,
                int actionQuotaIndex,
                int ownerQuotaIndex)
            {
                ActionStableId = actionStableId;
                RemainingReserved = remainingReserved;
                ActionQuotaIndex = actionQuotaIndex;
                OwnerQuotaIndex = ownerQuotaIndex;
            }

            public int ActionStableId;
            public int RemainingReserved;
            public int ActionQuotaIndex;
            public int OwnerQuotaIndex;
            public bool IsValid => ActionStableId > 0
                && RemainingReserved > 0
                && ActionQuotaIndex >= 0
                && OwnerQuotaIndex >= 0;
        }

        private struct SummonActionQuotaState
        {
            public int ActionStableId;
            public int MaxTotal;
            public int Reserved;
            public int Committed;
            public bool IsUsed;
        }

        private struct SummonOwnerQuotaState
        {
            public RuntimeId OwnerRuntimeId;
            public int ActionStableId;
            public int MaxPerOwner;
            public int Reserved;
            public int Committed;
            public bool IsUsed;
        }
        private enum StartPatternResult
        {
            Deferred = 0,
            Started,
            Fault
        }

        private struct OwnerState
        {
            public OwnerState(
                RuntimeId runtimeId,
                int spawnSequence,
                TickIndex activationTick,
                int recursionDepth,
                FpgEnemyDefinition definition)
            {
                RuntimeId = runtimeId;
                SpawnSequence = spawnSequence;
                ActivationTick = activationTick;
                RecursionDepth = recursionDepth;
                Definition = definition;
                ActivePatternIndex = -1;
                LastStartTick = TickIndex.Invalid;
                IsUsed = true;
            }

            public RuntimeId RuntimeId;
            public int SpawnSequence;
            public TickIndex ActivationTick;
            public int RecursionDepth;
            public FpgEnemyDefinition Definition;
            public int ActivePatternIndex;
            public TickIndex LastStartTick;
            public bool IsUsed;
        }

        private sealed class PreparedEnemyState
        {
            private readonly PreparedPatternState[] patterns;

            public PreparedEnemyState(PreparedPatternState[] patterns)
            {
                this.patterns = patterns
                    ?? throw new ArgumentNullException(nameof(patterns));
            }

            public int PatternCount => patterns.Length;

            public PreparedPatternState GetPattern(int index)
            {
                return patterns[index];
            }
        }

        private readonly struct PreparedPatternState
        {
            public PreparedPatternState(
                FpgEnemyAttackDefinition attack,
                FpgCompiledEnemySkillDefinition compiled,
                FpgCompiledSkillSequence execute)
            {
                Attack = attack
                    ?? throw new ArgumentNullException(nameof(attack));
                Compiled = compiled
                    ?? throw new ArgumentNullException(nameof(compiled));
                Execute = execute;

                int gameplayEventCount = 0;
                int maxConcurrentThreats = 0;
                int currentThreatTick = -1;
                int currentThreatCount = 0;
                for (int eventIndex = 0;
                    eventIndex < execute.EventCount;
                    eventIndex++)
                {
                    FpgCompiledSkillEvent skillEvent =
                        execute.GetEvent(eventIndex);
                    if (skillEvent.Kind
                        != FpgSkillEventKind.GameplayPayload)
                    {
                        continue;
                    }

                    gameplayEventCount++;
                    if (!compiled.TryGetPayloadSlot(
                        skillEvent.PayloadSlotId,
                        out FpgCompiledEnemySkillPayloadSlot payload)
                        || payload.Kind == FpgEnemySkillPayloadKind.Summon)
                    {
                        continue;
                    }

                    if (currentThreatTick != skillEvent.Tick)
                    {
                        currentThreatTick = skillEvent.Tick;
                        currentThreatCount = 0;
                    }

                    currentThreatCount++;
                    maxConcurrentThreats = Math.Max(
                        maxConcurrentThreats,
                        currentThreatCount);
                }

                GameplayEventCount = gameplayEventCount;
                MaxConcurrentThreats = maxConcurrentThreats;
            }

            public FpgEnemyAttackDefinition Attack { get; }
            public FpgCompiledEnemySkillDefinition Compiled { get; }
            public FpgCompiledSkillSequence Execute { get; }
            public int GameplayEventCount { get; }
            public int MaxConcurrentThreats { get; }
        }

        private sealed class PatternState
        {
            private long[] payloadOccurrences = Array.Empty<long>();
            private SummonQuotaReservation[] summonQuotaReservations =
                Array.Empty<SummonQuotaReservation>();
            private bool[] gameplayEventSucceeded = Array.Empty<bool>();

            public int OwnerIndex { get; private set; }
            public int PatternOrdinal { get; private set; }
            public FpgEnemyAttackDefinition Attack { get; private set; }
            public FpgCompiledEnemySkillDefinition Compiled { get; private set; }
            public FpgCompiledSkillSequence Execute { get; private set; }
            public FpgSkillExecutionRuntime Runtime { get; private set; }
            public ReservationToken[] EventBudgetReservations { get; private set; } =
                Array.Empty<ReservationToken>();
            public int GameplayEventCount { get; private set; }
            public int MaxConcurrentThreats { get; private set; }
            public TickIndex NextReadyTick;
            public TickIndex LastProcessedTick = TickIndex.Invalid;
            public long Occurrence;
            public bool IsDisabled;
            public bool IsUsed;
            public FpgEnemySkillCapacityReservation CapacityReservation;
            public int SummonQuotaReservationCount { get; private set; }

            public void EnsureCapacity(
                int eventCapacity,
                int payloadCapacity)
            {
                if (IsUsed
                    || eventCapacity < 0
                    || payloadCapacity < 0)
                {
                    throw new InvalidOperationException(
                        "Enemy pattern buffers can only grow while unbound.");
                }

                if (Runtime == null
                    || Runtime.ResultCapacity < eventCapacity)
                {
                    Runtime = new FpgSkillExecutionRuntime(eventCapacity);
                }

                if (EventBudgetReservations.Length < eventCapacity)
                {
                    EventBudgetReservations =
                        new ReservationToken[eventCapacity];
                }

                if (gameplayEventSucceeded.Length < eventCapacity)
                {
                    gameplayEventSucceeded = new bool[eventCapacity];
                }

                if (payloadOccurrences.Length < payloadCapacity)
                {
                    payloadOccurrences = new long[payloadCapacity];
                }

                if (summonQuotaReservations.Length < payloadCapacity)
                {
                    summonQuotaReservations =
                        new SummonQuotaReservation[payloadCapacity];
                }
            }

            public void Initialize(
                int ownerIndex,
                int patternOrdinal,
                in PreparedPatternState prepared,
                TickIndex nextReadyTick)
            {
                if (IsUsed
                    || Runtime == null
                    || Runtime.ResultCapacity < prepared.Execute.EventCount
                    || EventBudgetReservations.Length
                        < prepared.Execute.EventCount
                    || gameplayEventSucceeded.Length
                        < prepared.Execute.EventCount
                    || payloadOccurrences.Length
                        < prepared.Compiled.PayloadSlotCount
                    || summonQuotaReservations.Length
                        < prepared.Compiled.PayloadSlotCount)
                {
                    throw new InvalidOperationException(
                        "Enemy pattern slot was not prewarmed for this skill.");
                }

                Runtime.Reset();
                Array.Clear(
                    EventBudgetReservations,
                    0,
                    EventBudgetReservations.Length);
                Array.Clear(
                    payloadOccurrences,
                    0,
                    payloadOccurrences.Length);
                Array.Clear(
                    gameplayEventSucceeded,
                    0,
                    gameplayEventSucceeded.Length);
                Array.Clear(
                    summonQuotaReservations,
                    0,
                    summonQuotaReservations.Length);
                SummonQuotaReservationCount = 0;
                OwnerIndex = ownerIndex;
                PatternOrdinal = patternOrdinal;
                Attack = prepared.Attack;
                Compiled = prepared.Compiled;
                Execute = prepared.Execute;
                GameplayEventCount = prepared.GameplayEventCount;
                MaxConcurrentThreats = prepared.MaxConcurrentThreats;
                NextReadyTick = nextReadyTick;
                LastProcessedTick = TickIndex.Invalid;
                Occurrence = 0L;
                IsDisabled = false;
                CapacityReservation =
                    FpgEnemySkillCapacityReservation.Invalid;
                IsUsed = true;
            }

            public void Release()
            {
                Runtime?.Reset();
                Array.Clear(
                    EventBudgetReservations,
                    0,
                    EventBudgetReservations.Length);
                Array.Clear(
                    payloadOccurrences,
                    0,
                    payloadOccurrences.Length);
                Array.Clear(
                    gameplayEventSucceeded,
                    0,
                    gameplayEventSucceeded.Length);
                Array.Clear(
                    summonQuotaReservations,
                    0,
                    summonQuotaReservations.Length);
                SummonQuotaReservationCount = 0;
                OwnerIndex = -1;
                PatternOrdinal = -1;
                Attack = null;
                Compiled = null;
                Execute = default(FpgCompiledSkillSequence);
                GameplayEventCount = 0;
                MaxConcurrentThreats = 0;
                NextReadyTick = TickIndex.Invalid;
                LastProcessedTick = TickIndex.Invalid;
                Occurrence = 0L;
                IsDisabled = false;
                CapacityReservation =
                    FpgEnemySkillCapacityReservation.Invalid;
                IsUsed = false;
            }

            public void BeginExecution()
            {
                Array.Clear(
                    gameplayEventSucceeded,
                    0,
                    gameplayEventSucceeded.Length);
            }

            public void MarkGameplayEventSucceeded(int eventId)
            {
                int index = FindEventIndex(eventId);
                if (index >= 0)
                {
                    gameplayEventSucceeded[index] = true;
                }
            }

            public bool HasSuccessfulGameplayEvent(int eventId)
            {
                int index = FindEventIndex(eventId);
                return index >= 0
                    && gameplayEventSucceeded[index];
            }

            public int FindEventIndex(int eventId)
            {
                for (int index = 0; index < Execute.EventCount; index++)
                {
                    if (Execute.GetEvent(index).EventId == eventId)
                    {
                        return index;
                    }
                }

                return -1;
            }

            public int CountExecuteEvents(int payloadSlotId)
            {
                int count = 0;
                for (int index = 0; index < Execute.EventCount; index++)
                {
                    FpgCompiledSkillEvent skillEvent =
                        Execute.GetEvent(index);
                    if (skillEvent.Kind
                            == FpgSkillEventKind.GameplayPayload
                        && skillEvent.PayloadSlotId == payloadSlotId)
                    {
                        count++;
                    }
                }

                return count;
            }

            public void ClearSummonQuotaReservations()
            {
                if (SummonQuotaReservationCount > 0)
                {
                    Array.Clear(
                        summonQuotaReservations,
                        0,
                        SummonQuotaReservationCount);
                    SummonQuotaReservationCount = 0;
                }
            }

            public bool TryAddSummonQuotaReservation(
                in SummonQuotaReservation reservation)
            {
                if (!reservation.IsValid
                    || SummonQuotaReservationCount
                        >= summonQuotaReservations.Length)
                {
                    return false;
                }

                for (int index = 0;
                    index < SummonQuotaReservationCount;
                    index++)
                {
                    if (summonQuotaReservations[index].ActionStableId
                        == reservation.ActionStableId)
                    {
                        return false;
                    }
                }

                summonQuotaReservations[SummonQuotaReservationCount++] =
                    reservation;
                return true;
            }

            public SummonQuotaReservation GetSummonQuotaReservation(
                int index)
            {
                if (index < 0 || index >= SummonQuotaReservationCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return summonQuotaReservations[index];
            }

            public bool TryCommitSummonQuotaReservation(
                int actionStableId,
                out SummonQuotaReservation reservation)
            {
                for (int index = 0;
                    index < SummonQuotaReservationCount;
                    index++)
                {
                    SummonQuotaReservation candidate =
                        summonQuotaReservations[index];
                    if (candidate.ActionStableId != actionStableId
                        || candidate.RemainingReserved <= 0)
                    {
                        continue;
                    }

                    candidate.RemainingReserved--;
                    summonQuotaReservations[index] = candidate;
                    reservation = candidate;
                    return true;
                }

                reservation = default(SummonQuotaReservation);
                return false;
            }
            public void IncrementPayloadOccurrence(int payloadSlotId)
            {
                for (int index = 0;
                    index < Compiled.PayloadSlotCount;
                    index++)
                {
                    if (Compiled.PayloadSlots[index].SlotId
                        == payloadSlotId)
                    {
                        payloadOccurrences[index]++;
                        return;
                    }
                }
            }

            public long CountPayloadOccurrences(int actionStableId)
            {
                long count = 0L;
                for (int index = 0;
                    index < Compiled.PayloadSlotCount;
                    index++)
                {
                    FpgCompiledEnemySkillPayloadSlot payload =
                        Compiled.PayloadSlots[index];
                    if (payload.Kind == FpgEnemySkillPayloadKind.Summon
                        && payload.SummonPayload.ActionStableId
                            == actionStableId)
                    {
                        count = checked(
                            count + payloadOccurrences[index]);
                    }
                }

                return count;
            }
        }
    }
}
