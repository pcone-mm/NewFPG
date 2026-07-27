using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Skills;

namespace FPG.Demo.Unity
{
    public enum FpgPlayerSkillSlot
    {
        None = 0,
        Primary,
        Secondary,
        Reload
    }

    public readonly struct FpgPlayerSkillExecutionEvent
    {
        internal FpgPlayerSkillExecutionEvent(
            FpgPlayerSkillSlot slot,
            FpgSkillEventResult runtimeEvent,
            FpgCompiledPlayerSkillAction action,
            bool hasGameplayAction)
        {
            Slot = slot;
            RuntimeEvent = runtimeEvent;
            Action = action;
            HasGameplayAction = hasGameplayAction;
        }

        public FpgPlayerSkillSlot Slot { get; }
        public FpgSkillEventResult RuntimeEvent { get; }
        public FpgCompiledSkillEvent Event => RuntimeEvent.Event;
        public FpgSkillEventOutcome Outcome => RuntimeEvent.Outcome;
        public bool HasGameplayAction { get; }
        public FpgCompiledPlayerSkillAction Action { get; }
    }

    public readonly struct FpgPlayerSkillSequenceFrame
    {
        internal FpgPlayerSkillSequenceFrame(
            FpgPlayerSkillSlot slot,
            FpgCompiledSkillSequence sequence,
            SkillExecutionId executionId,
            TickIndex startTick,
            TickIndex tick,
            FpgSkillExecutionState state)
        {
            Slot = slot;
            Sequence = sequence;
            ExecutionId = executionId;
            StartTick = startTick;
            Tick = tick;
            RelativeTick = checked((int)(tick.Value - startTick.Value));
            State = state;
            ResolvedAnimationId = sequence.ResolveAnimation(executionId);
        }

        public FpgPlayerSkillSlot Slot { get; }
        public FpgCompiledSkillSequence Sequence { get; }
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
    /// Owns the deterministic player skill timeline state. It plans action
    /// locks from precompiled summaries and emits every event scheduled for the
    /// current tick without allocating in the tick path.
    /// </summary>
    public sealed class FpgPlayerSkillExecutionController
    {
        private readonly FpgCompiledPlayerSkillDefinition primary;
        private readonly FpgCompiledPlayerSkillDefinition secondary;
        private readonly FpgCompiledPlayerSkillDefinition reload;
        private readonly SecondaryTriggerMode secondaryTriggerMode;
        private readonly FpgSkillExecutionRuntime runtime;
        private readonly FpgPlayerSkillExecutionEvent[] results;
        private readonly FpgPlayerSkillSequenceFrame[] sequenceFrames =
            new FpgPlayerSkillSequenceFrame[3];

        private FpgCompiledPlayerSkillDefinition activeDefinition;
        private FpgPlayerSkillSlot activeSlot;
        private FpgSkillSequenceKind activeSequenceKind;
        private FpgSkillExecutionIdAllocator executionIds;
        private bool ownsExecutionIds;
        private int resultCount;
        private int sequenceFrameCount;

        private FpgPlayerSkillExecutionController(
            FpgCompiledPlayerSkillDefinition primary,
            FpgCompiledPlayerSkillDefinition secondary,
            FpgCompiledPlayerSkillDefinition reload,
            SecondaryTriggerMode secondaryTriggerMode,
            int runtimeCapacity,
            int resultCapacity)
        {
            this.primary = primary;
            this.secondary = secondary;
            this.reload = reload;
            this.secondaryTriggerMode = secondaryTriggerMode;
            executionIds = new FpgSkillExecutionIdAllocator();
            ownsExecutionIds = true;
            runtime = new FpgSkillExecutionRuntime(runtimeCapacity);
            results = new FpgPlayerSkillExecutionEvent[resultCapacity];
        }

        public bool IsExecuting => runtime.IsRunning;
        public FpgPlayerSkillSlot ActiveSlot => activeSlot;
        public FpgSkillSequenceKind ActiveSequenceKind => activeSequenceKind;
        public TickIndex NextTick => runtime.NextTick;
        public int ResultCount => resultCount;
        public int ResultCapacity => results.Length;
        public int SequenceFrameCount => sequenceFrameCount;
        public TickIndex PlannedLastAttackTick { get; private set; } =
            TickIndex.Invalid;
        public TickIndex ActionLockedUntilTick { get; private set; } =
            TickIndex.Invalid;
        public TickIndex RecastLockedUntilTick { get; private set; } =
            TickIndex.Invalid;

        public static bool TryCreate(
            FpgCompiledPlayerSkillDefinition primary,
            FpgCompiledPlayerSkillDefinition secondary,
            FpgCompiledPlayerSkillDefinition reload,
            SecondaryTriggerMode secondaryTriggerMode,
            out FpgPlayerSkillExecutionController controller,
            out string error)
        {
            controller = null;
            if (primary == null || secondary == null || reload == null
                || !Enum.IsDefined(
                    typeof(SecondaryTriggerMode),
                    secondaryTriggerMode)
                || primary.MaximumImpactCount
                    > TargetSelector.DefaultCandidateCapacity
                || secondary.MaximumImpactCount
                    > TargetSelector.DefaultCandidateCapacity
                || reload.MaximumImpactCount
                    > TargetSelector.DefaultCandidateCapacity
                || primary.MaximumPelletCount
                    > WeaponDefinition.PrimaryPelletCount
                || secondary.MaximumPelletCount
                    > WeaponDefinition.PrimaryPelletCount
                || reload.MaximumPelletCount
                    > WeaponDefinition.PrimaryPelletCount)
            {
                error = "Player skill execution requires valid compiled skills within the formal query capacities.";
                return false;
            }

            FpgSkillSequenceKind secondaryActionKind =
                secondaryTriggerMode == SecondaryTriggerMode.ChargeRelease
                    && secondary.Timeline.TryGetSequence(
                        FpgSkillSequenceKind.Release,
                        out _)
                    ? FpgSkillSequenceKind.Release
                    : FpgSkillSequenceKind.Execute;
            if (!TryValidateAction(
                    primary,
                    FpgSkillSequenceKind.Execute,
                    FpgPlayerSkillActionKind.PelletRay,
                    FpgPlayerSkillActionKind.None,
                    "Primary",
                    out error)
                || !TryValidateAction(
                    secondary,
                    secondaryActionKind,
                    FpgPlayerSkillActionKind.AreaAtFirstSurface,
                    FpgPlayerSkillActionKind.ProjectileAreaAtFirstSurface,
                    "Secondary",
                    out error)
                || !TryValidateReload(reload, out error)
                || !TryValidateAllSpatialMetadata(primary, false, out error)
                || !TryValidateAllSpatialMetadata(secondary, true, out error)
                || !TryValidateAllSpatialMetadata(reload, false, out error))
            {
                return false;
            }

            try
            {
                int runtimeCapacity = 0;
                int resultCapacity = 0;
                AccumulateCapacities(
                    primary,
                    ref runtimeCapacity,
                    ref resultCapacity);
                AccumulateCapacities(
                    secondary,
                    ref runtimeCapacity,
                    ref resultCapacity);
                AccumulateCapacities(
                    reload,
                    ref runtimeCapacity,
                    ref resultCapacity);
                controller = new FpgPlayerSkillExecutionController(
                    primary,
                    secondary,
                    reload,
                    secondaryTriggerMode,
                    runtimeCapacity,
                    Math.Max(1, resultCapacity));
                error = string.Empty;
                return true;
            }
            catch (OverflowException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TryBindExecutionIdAllocator(
            FpgSkillExecutionIdAllocator allocator,
            out string error)
        {
            if (allocator == null)
            {
                error = "Player skill execution requires an execution ID allocator.";
                return false;
            }

            if (ReferenceEquals(executionIds, allocator))
            {
                error = string.Empty;
                return true;
            }

            if (runtime.IsRunning)
            {
                error = "Execution ID allocator cannot change during an active skill.";
                return false;
            }

            executionIds = allocator;
            ownsExecutionIds = false;
            error = string.Empty;
            return true;
        }


        public FpgPlayerSkillExecutionEvent GetResult(int index)
        {
            if (index < 0 || index >= resultCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return results[index];
        }

        public FpgPlayerSkillSequenceFrame GetSequenceFrame(int index)
        {
            if (index < 0 || index >= sequenceFrameCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return sequenceFrames[index];
        }

        public DomainResult ProcessFrame(
            PlayerInputFrame frame,
            PlayerRuntime player)
        {
            ClearFrameResults();
            if (player == null || !frame.Tick.IsValid
                || player.Weapon.Definition.SecondaryTriggerMode
                    != secondaryTriggerMode)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            DomainResult advanced = player.Weapon.AdvanceSkillFrame(frame.Tick);
            if (!advanced.IsSuccess)
            {
                return advanced;
            }

            if (frame.CancelSecondary)
            {
                bool wasCharging =
                    player.Weapon.State == WeaponState.AltCharging;
                DomainResult canceled = CancelChargeTimeline(frame.Tick);
                if (!canceled.IsSuccess)
                {
                    return canceled;
                }

                player.Weapon.CancelSkillSecondaryCharge();
                if (wasCharging)
                {
                    DomainResult cancelSequence =
                        TryStartSecondaryCancelTimeline(frame.Tick);
                    if (!cancelSequence.IsSuccess)
                    {
                        return cancelSequence;
                    }
                }
            }

            bool immediateSecondaryRequested = false;
            for (int commandIndex = 0;
                commandIndex < frame.EdgeCommandCount;
                commandIndex++)
            {
                InputEdgeCommand command = frame.EdgeCommands[commandIndex];
                if (!player.Weapon.TryAcceptSkillInputCommand(command))
                {
                    continue;
                }

                DomainResult handled = DomainResult.Success;
                switch (command.Type)
                {
                    case InputEdgeType.SecondaryPressed:
                        if (secondaryTriggerMode
                            == SecondaryTriggerMode.ImmediateRepeatWhileHeld)
                        {
                            immediateSecondaryRequested = true;
                        }
                        else if (!frame.CancelSecondary)
                        {
                            handled = TryBeginSecondaryCharge(
                                frame.Tick,
                                player);
                        }
                        break;

                    case InputEdgeType.SecondaryReleased:
                        if (secondaryTriggerMode
                            == SecondaryTriggerMode.ChargeRelease)
                        {
                            handled = TryReleaseSecondaryCharge(
                                frame.Tick,
                                player);
                        }
                        break;

                    case InputEdgeType.ReloadPressed:
                        handled = TryStartAction(
                            FpgPlayerSkillSlot.Reload,
                            reload,
                            FpgSkillSequenceKind.Execute,
                            frame.Tick,
                            player);
                        break;

                    default:
                        break;
                }

                if (!handled.IsSuccess)
                {
                    return handled;
                }
            }

            if (secondaryTriggerMode
                    == SecondaryTriggerMode.ImmediateRepeatWhileHeld
                && (frame.SecondaryHeld || immediateSecondaryRequested)
                && !runtime.IsRunning)
            {
                DomainResult started = TryStartAction(
                    FpgPlayerSkillSlot.Secondary,
                    secondary,
                    FpgSkillSequenceKind.Execute,
                    frame.Tick,
                    player);
                if (!started.IsSuccess)
                {
                    return started;
                }
            }

            if (frame.PrimaryHeld && !runtime.IsRunning)
            {
                DomainResult started = TryStartAction(
                    FpgPlayerSkillSlot.Primary,
                    primary,
                    FpgSkillSequenceKind.Execute,
                    frame.Tick,
                    player);
                if (!started.IsSuccess)
                {
                    return started;
                }
            }

            if (!runtime.IsRunning
                && player.Weapon.State == WeaponState.AltCharging)
            {
                DomainResult continued = TryStartChargeContinuation(frame.Tick);
                if (!continued.IsSuccess)
                {
                    return continued;
                }
            }

            if (!runtime.IsRunning)
            {
                return DomainResult.Success;
            }

            FpgSkillRuntimeResult ticked = runtime.Tick(frame.Tick);
            if (!ticked.IsSuccess)
            {
                return MapRuntimeFailure(ticked.Error);
            }

            DomainResult appended = AppendRuntimeResults();
            if (!appended.IsSuccess)
            {
                return appended;
            }

            DomainResult sequenceFrame = AppendSequenceFrame(frame.Tick);
            if (!sequenceFrame.IsSuccess)
            {
                return sequenceFrame;
            }

            if (runtime.IsTerminal)
            {
                ClearActive();
            }

            return DomainResult.Success;
        }

        public DomainResult HardInterrupt(
            TickIndex tick,
            WeaponRuntime weapon)
        {
            ClearFrameResults();
            if (weapon == null || !tick.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            bool wasRunning = runtime.IsRunning;
            FpgPlayerSkillSlot interruptedSlot = activeSlot;
            FpgSkillSequenceKind interruptedSequenceKind = activeSequenceKind;
            if (runtime.IsRunning)
            {
                FpgSkillRuntimeResult canceled = runtime.CancelRemaining(tick);
                if (!canceled.IsSuccess)
                {
                    return MapRuntimeFailure(canceled.Error);
                }

                DomainResult appended = AppendRuntimeResults();
                if (!appended.IsSuccess)
                {
                    return appended;
                }


                DomainResult sequenceFrame = AppendSequenceFrame(tick);
                if (!sequenceFrame.IsSuccess)
                {
                    return sequenceFrame;
                }
            }

            ClearActive();
            if (wasRunning)
            {
                ApplyInterruptedWeaponState(
                    weapon,
                    tick,
                    interruptedSlot,
                    interruptedSequenceKind);
            }
            else if (weapon.State == WeaponState.AltCharging)
            {
                weapon.CancelSkillSecondaryCharge();
            }
            return DomainResult.Success;
        }

        public void AbortAfterProcessedTick(WeaponRuntime weapon)
        {
            FpgPlayerSkillSlot interruptedSlot = FpgPlayerSkillSlot.None;
            FpgSkillSequenceKind interruptedSequenceKind =
                FpgSkillSequenceKind.None;
            TickIndex interruptTick = weapon == null
                ? TickIndex.Invalid
                : weapon.LastProcessedTick;
            if (sequenceFrameCount > 0)
            {
                FpgPlayerSkillSequenceFrame frame =
                    sequenceFrames[sequenceFrameCount - 1];
                interruptedSlot = frame.Slot;
                interruptedSequenceKind = frame.Sequence.Kind;
                interruptTick = frame.Tick;
                sequenceFrames[sequenceFrameCount - 1] =
                    new FpgPlayerSkillSequenceFrame(
                        frame.Slot,
                        frame.Sequence,
                        frame.ExecutionId,
                        frame.StartTick,
                        frame.Tick,
                        FpgSkillExecutionState.Canceled);
            }

            runtime.Reset();
            ClearActive();
            ClearEventResults();
            if (weapon != null)
            {
                ApplyInterruptedWeaponState(
                    weapon,
                    interruptTick,
                    interruptedSlot,
                    interruptedSequenceKind);
            }
        }

        public void Reset()
        {
            runtime.Reset();
            ClearActive();
            ClearFrameResults();
            if (ownsExecutionIds)
            {
                executionIds.Reset();
            }
            PlannedLastAttackTick = TickIndex.Invalid;
            ActionLockedUntilTick = TickIndex.Invalid;
            RecastLockedUntilTick = TickIndex.Invalid;
        }

        private DomainResult TryBeginSecondaryCharge(
            TickIndex tick,
            PlayerRuntime player)
        {
            DomainResult begin = player.Weapon.TryBeginSkillSecondaryCharge(
                tick,
                player.Exposure);
            if (!begin.IsSuccess)
            {
                return DomainResult.Success;
            }

            FpgSkillSequenceKind kind = secondary.Timeline.TryGetSequence(
                FpgSkillSequenceKind.ChargeEnter,
                out _)
                    ? FpgSkillSequenceKind.ChargeEnter
                    : FpgSkillSequenceKind.ChargeLoop;
            if (!secondary.Timeline.TryGetSequence(kind, out _))
            {
                return DomainResult.Success;
            }

            DomainResult started = StartTimeline(
                FpgPlayerSkillSlot.Secondary,
                secondary,
                kind,
                tick);
            if (!started.IsSuccess)
            {
                player.Weapon.CancelSkillSecondaryCharge();
            }

            return started;
        }

        private DomainResult TryReleaseSecondaryCharge(
            TickIndex tick,
            PlayerRuntime player)
        {
            bool wasCharging = player.Weapon.State == WeaponState.AltCharging;
            DomainResult canceled = CancelChargeTimeline(tick);
            if (!canceled.IsSuccess)
            {
                return canceled;
            }

            DomainResult finished = player.Weapon.TryFinishSkillSecondaryCharge(
                tick,
                out bool charged);
            if (!finished.IsSuccess)
            {
                return DomainResult.Success;
            }

            if (!charged)
            {
                return wasCharging
                    ? TryStartSecondaryCancelTimeline(tick)
                    : DomainResult.Success;
            }

            FpgSkillSequenceKind kind = secondary.Timeline.TryGetSequence(
                FpgSkillSequenceKind.Release,
                out _)
                    ? FpgSkillSequenceKind.Release
                    : FpgSkillSequenceKind.Execute;
            return TryStartAction(
                FpgPlayerSkillSlot.Secondary,
                secondary,
                kind,
                tick,
                player);
        }

        private DomainResult TryStartSecondaryCancelTimeline(TickIndex tick)
        {
            if (!secondary.Timeline.TryGetSequence(
                    FpgSkillSequenceKind.Cancel,
                    out _))
            {
                return DomainResult.Success;
            }

            return StartTimeline(
                FpgPlayerSkillSlot.Secondary,
                secondary,
                FpgSkillSequenceKind.Cancel,
                tick);
        }

        private DomainResult TryStartChargeContinuation(TickIndex tick)
        {
            if (!secondary.Timeline.TryGetSequence(
                    FpgSkillSequenceKind.ChargeLoop,
                    out _))
            {
                return DomainResult.Success;
            }

            return StartTimeline(
                FpgPlayerSkillSlot.Secondary,
                secondary,
                FpgSkillSequenceKind.ChargeLoop,
                tick);
        }

        private DomainResult TryStartAction(
            FpgPlayerSkillSlot slot,
            FpgCompiledPlayerSkillDefinition definition,
            FpgSkillSequenceKind sequenceKind,
            TickIndex tick,
            PlayerRuntime player)
        {
            if (runtime.IsRunning)
            {
                return DomainResult.Success;
            }

            if (!definition.Timeline.TryGetSequence(
                    sequenceKind,
                    out FpgCompiledSkillSequence sequence)
                || !definition.TryGetSequenceSummary(
                    sequenceKind,
                    out FpgCompiledPlayerSkillSequenceSummary summary))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            WeaponSkillActionKind actionKind = slot == FpgPlayerSkillSlot.Primary
                ? WeaponSkillActionKind.Primary
                : slot == FpgPlayerSkillSlot.Secondary
                    ? WeaponSkillActionKind.Secondary
                    : WeaponSkillActionKind.Reload;
            TickIndex lockedUntil;
            TickIndex recastLockedUntil;
            try
            {
                int sequenceLock = checked(sequence.DurationTicks + 1);
                int cooldownLock = summary.LastAttackTick < 0
                    ? 0
                    : checked(
                        summary.LastAttackTick
                        + definition.SequenceCooldownTicks);
                int lockOffset = Math.Max(
                    1,
                    Math.Max(sequenceLock, cooldownLock));
                lockedUntil = new TickIndex(checked(tick.Value + lockOffset));
                recastLockedUntil = summary.LastAttackTick < 0
                    ? TickIndex.Invalid
                    : new TickIndex(checked(tick.Value + cooldownLock));
            }
            catch (OverflowException)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            DomainResult begin = player.Weapon.TryBeginSkillAction(
                actionKind,
                tick,
                lockedUntil,
                summary.TotalAmmoCost,
                player.Exposure);
            if (!begin.IsSuccess)
            {
                return DomainResult.Success;
            }

            DomainResult started = StartTimeline(
                slot,
                definition,
                sequenceKind,
                tick);
            if (!started.IsSuccess)
            {
                player.Weapon.CancelSkillAction();
                return started;
            }

            ActionLockedUntilTick = lockedUntil;
            RecastLockedUntilTick = recastLockedUntil;
            PlannedLastAttackTick = summary.LastAttackTick < 0
                ? TickIndex.Invalid
                : new TickIndex(tick.Value + summary.LastAttackTick);
            return DomainResult.Success;
        }

        private void ApplyInterruptedWeaponState(
            WeaponRuntime weapon,
            TickIndex interruptTick,
            FpgPlayerSkillSlot slot,
            FpgSkillSequenceKind sequenceKind)
        {
            WeaponSkillActionKind actionKind = WeaponSkillActionKind.None;
            if (slot == FpgPlayerSkillSlot.Primary
                && sequenceKind == FpgSkillSequenceKind.Execute)
            {
                actionKind = WeaponSkillActionKind.Primary;
            }
            else if (slot == FpgPlayerSkillSlot.Secondary
                && (sequenceKind == FpgSkillSequenceKind.Execute
                    || sequenceKind == FpgSkillSequenceKind.Release))
            {
                actionKind = WeaponSkillActionKind.Secondary;
            }

            weapon.InterruptSkillAction(
                actionKind,
                interruptTick,
                RecastLockedUntilTick);
        }

        private DomainResult StartTimeline(
            FpgPlayerSkillSlot slot,
            FpgCompiledPlayerSkillDefinition definition,
            FpgSkillSequenceKind sequenceKind,
            TickIndex tick)
        {
            if (!definition.Timeline.TryGetSequence(
                    sequenceKind,
                    out FpgCompiledSkillSequence sequence)
                || executionIds == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            SkillExecutionId executionId;
            try
            {
                executionId = executionIds.Peek();
            }
            catch (OverflowException)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            FpgSkillRuntimeResult started = runtime.Start(
                sequence,
                executionId,
                tick);
            if (!started.IsSuccess)
            {
                return MapRuntimeFailure(started.Error);
            }

            try
            {
                executionIds.Commit(executionId);
            }
            catch (InvalidOperationException)
            {
                runtime.Reset();
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            activeSlot = slot;
            activeDefinition = definition;
            activeSequenceKind = sequenceKind;
            return DomainResult.Success;
        }

        private DomainResult CancelChargeTimeline(TickIndex tick)
        {
            if (!runtime.IsRunning
                || activeSlot != FpgPlayerSkillSlot.Secondary
                || (activeSequenceKind != FpgSkillSequenceKind.ChargeEnter
                    && activeSequenceKind != FpgSkillSequenceKind.ChargeLoop))
            {
                return DomainResult.Success;
            }

            FpgSkillRuntimeResult canceled = runtime.CancelRemaining(tick);
            if (!canceled.IsSuccess)
            {
                return MapRuntimeFailure(canceled.Error);
            }

            DomainResult appended = AppendRuntimeResults();
            if (appended.IsSuccess)
            {
                appended = AppendSequenceFrame(tick);
            }
            ClearActive();
            return appended;
        }

        private DomainResult AppendRuntimeResults()
        {
            for (int index = 0; index < runtime.ResultCount; index++)
            {
                if (resultCount >= results.Length || activeDefinition == null)
                {
                    return DomainResult.Rejected(RejectReason.BufferCapacity);
                }

                FpgSkillEventResult runtimeEvent = runtime.GetResult(index);
                bool hasPayload = runtimeEvent.Event.Kind
                    == FpgSkillEventKind.GameplayAction;
                FpgCompiledPlayerSkillAction payload =
                    default(FpgCompiledPlayerSkillAction);
                if (hasPayload
                    && !activeDefinition.TryResolveAction(
                        runtimeEvent.Event,
                        out payload))
                {
                    return DomainResult.Rejected(RejectReason.InvalidDefinition);
                }

                results[resultCount++] = new FpgPlayerSkillExecutionEvent(
                    activeSlot,
                    runtimeEvent,
                    payload,
                    hasPayload);
            }

            return DomainResult.Success;
        }

        private DomainResult AppendSequenceFrame(TickIndex tick)
        {
            if (sequenceFrameCount >= sequenceFrames.Length
                || activeDefinition == null
                || !activeDefinition.Timeline.TryGetSequence(
                    activeSequenceKind,
                    out FpgCompiledSkillSequence sequence)
                || !runtime.ExecutionId.IsValid
                || !runtime.StartTick.IsValid
                || !tick.IsValid)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            try
            {
                sequenceFrames[sequenceFrameCount++] =
                    new FpgPlayerSkillSequenceFrame(
                        activeSlot,
                        sequence,
                        runtime.ExecutionId,
                        runtime.StartTick,
                        tick,
                        runtime.State);
                return DomainResult.Success;
            }
            catch (OverflowException)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }
        }

        private void ClearActive()
        {
            activeDefinition = null;
            activeSlot = FpgPlayerSkillSlot.None;
            activeSequenceKind = FpgSkillSequenceKind.None;
        }

        private void ClearEventResults()
        {
            resultCount = 0;
        }

        private void ClearFrameResults()
        {
            ClearEventResults();
            sequenceFrameCount = 0;
        }

        private static DomainResult MapRuntimeFailure(FpgSkillRuntimeError error)
        {
            switch (error)
            {
                case FpgSkillRuntimeError.WrongTick:
                    return DomainResult.Rejected(RejectReason.WrongTick);
                case FpgSkillRuntimeError.ResultCapacityExceeded:
                    return DomainResult.Rejected(RejectReason.BufferCapacity);
                case FpgSkillRuntimeError.InvalidSequence:
                case FpgSkillRuntimeError.InvalidExecutionId:
                case FpgSkillRuntimeError.TickRangeOverflow:
                    return DomainResult.Rejected(RejectReason.InvalidDefinition);
                default:
                    return DomainResult.Rejected(RejectReason.InvalidState);
            }
        }

        private static bool TryValidateAction(
            FpgCompiledPlayerSkillDefinition definition,
            FpgSkillSequenceKind kind,
            FpgPlayerSkillActionKind payloadKind,
            FpgPlayerSkillActionKind alternatePayloadKind,
            string label,
            out string error)
        {
            if (!definition.Timeline.TryGetSequence(
                    kind,
                    out FpgCompiledSkillSequence sequence)
                || !definition.TryGetSequenceSummary(
                    kind,
                    out FpgCompiledPlayerSkillSequenceSummary summary)
                || summary.AttackEventCount <= 0
                || summary.ReloadCommitEventCount != 0
                || summary.TotalAmmoCost <= 0)
            {
                error = label + " skill has no valid attack sequence.";
                return false;
            }

            for (int eventIndex = 0;
                eventIndex < sequence.EventCount;
                eventIndex++)
            {
                FpgCompiledSkillEvent skillEvent = sequence.GetEvent(eventIndex);
                if (skillEvent.Kind == FpgSkillEventKind.GameplayAction
                    && (!definition.TryResolveAction(
                            skillEvent,
                            out FpgCompiledPlayerSkillAction payload)
                        || (payload.Kind != payloadKind
                            && payload.Kind != alternatePayloadKind)))
                {
                    error = label + " skill contains an incompatible gameplay payload.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateReload(
            FpgCompiledPlayerSkillDefinition definition,
            out string error)
        {
            if (!definition.TryGetSequenceSummary(
                    FpgSkillSequenceKind.Execute,
                    out FpgCompiledPlayerSkillSequenceSummary summary)
                || summary.AttackEventCount != 0
                || summary.ReloadCommitEventCount <= 0
                || summary.TotalAmmoCost != 0)
            {
                error = "Reload skill requires reload commits and no attacks.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateAllSpatialMetadata(
            FpgCompiledPlayerSkillDefinition definition,
            bool rejectChargeGameplay,
            out string error)
        {
            for (int sequenceIndex = 0;
                sequenceIndex < definition.Timeline.SequenceCount;
                sequenceIndex++)
            {
                FpgCompiledSkillSequence sequence =
                    definition.Timeline.GetSequence(sequenceIndex);
                for (int eventIndex = 0;
                    eventIndex < sequence.EventCount;
                    eventIndex++)
                {
                    FpgCompiledSkillEvent skillEvent =
                        sequence.GetEvent(eventIndex);
                    if (skillEvent.Kind != FpgSkillEventKind.GameplayAction)
                    {
                        continue;
                    }

                    if (!definition.TryResolveAction(
                            skillEvent,
                            out FpgCompiledPlayerSkillAction payload))
                    {
                        error = "Player skill references a missing compiled payload.";
                        return false;
                    }

                    if (rejectChargeGameplay
                        && (sequence.Kind == FpgSkillSequenceKind.ChargeEnter
                            || sequence.Kind == FpgSkillSequenceKind.ChargeLoop))
                    {
                        error = "Player charge timelines cannot contain gameplay payloads.";
                        return false;
                    }

                    FpgSkillTargetSource expected =
                        payload.Kind == FpgPlayerSkillActionKind.ReloadCommit
                            ? FpgSkillTargetSource.Self
                            : FpgSkillTargetSource.CurrentAim;
                    if (skillEvent.TargetSource != expected)
                    {
                        error = payload.Kind == FpgPlayerSkillActionKind.ReloadCommit
                            ? "Player reload events must target Self."
                            : "Player attack events must target CurrentAim.";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        private static void AccumulateCapacities(
            FpgCompiledPlayerSkillDefinition definition,
            ref int runtimeCapacity,
            ref int resultCapacity)
        {
            for (int index = 0;
                index < definition.Timeline.SequenceCount;
                index++)
            {
                int count = definition.Timeline.GetSequence(index).EventCount;
                runtimeCapacity = Math.Max(runtimeCapacity, count);
                resultCapacity = checked(resultCapacity + count);
            }
        }
    }
}
