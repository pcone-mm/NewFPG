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

    public enum FpgAttackIntentSource
    {
        None = 0,
        PrimaryPressed,
        SecondaryPressed,
        HeldRepeat
    }

    public readonly struct PendingAttackIntent
    {
        internal PendingAttackIntent(
            FpgPlayerSkillSlot slot,
            FpgSkillSequenceKind sequenceKind,
            ulong skillGameplayHash,
            TickIndex issuedTick,
            TickIndex expiresTick,
            InputSequence inputSequence,
            FpgAttackIntentSource source)
        {
            Slot = slot;
            SequenceKind = sequenceKind;
            SkillGameplayHash = skillGameplayHash;
            IssuedTick = issuedTick;
            ExpiresTick = expiresTick;
            InputSequence = inputSequence;
            Source = source;
        }

        public FpgPlayerSkillSlot Slot { get; }
        public FpgSkillSequenceKind SequenceKind { get; }
        public ulong SkillGameplayHash { get; }
        public TickIndex IssuedTick { get; }
        public TickIndex ExpiresTick { get; }
        public InputSequence InputSequence { get; }
        public FpgAttackIntentSource Source { get; }
        public bool IsValid => (Slot == FpgPlayerSkillSlot.Primary
                || Slot == FpgPlayerSkillSlot.Secondary)
            && SequenceKind != FpgSkillSequenceKind.None
            && SkillGameplayHash != 0UL
            && IssuedTick.IsValid
            && ExpiresTick.IsValid
            && ExpiresTick >= IssuedTick
            && InputSequence.IsValid
            && Source != FpgAttackIntentSource.None;

        public bool IsExpiredAt(TickIndex tick)
        {
            return IsValid && tick.IsValid && tick > ExpiresTick;
        }
    }

    public readonly struct FpgPlayerSkillExecutionEvent
    {
        internal FpgPlayerSkillExecutionEvent(
            FpgPlayerSkillSlot slot,
            FpgSkillEventResult runtimeEvent,
            FpgCompiledPlayerSkillAction action,
            bool hasGameplayAction,
            FpgResolvedSkillTimingSnapshot timing =
                default(FpgResolvedSkillTimingSnapshot))
        {
            Slot = slot;
            RuntimeEvent = runtimeEvent;
            Action = action;
            HasGameplayAction = hasGameplayAction;
            Timing = timing;
        }

        public FpgPlayerSkillSlot Slot { get; }
        public FpgSkillEventResult RuntimeEvent { get; }
        public FpgCompiledSkillEvent Event => RuntimeEvent.Event;
        public FpgSkillEventOutcome Outcome => RuntimeEvent.Outcome;
        public bool HasGameplayAction { get; }
        public FpgCompiledPlayerSkillAction Action { get; }
        public FpgResolvedSkillTimingSnapshot Timing { get; }
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
            : this(
                slot,
                sequence,
                executionId,
                startTick,
                tick,
                state,
                default(FpgResolvedSkillTimingSnapshot))
        {
        }

        internal FpgPlayerSkillSequenceFrame(
            FpgPlayerSkillSlot slot,
            FpgCompiledSkillSequence sequence,
            SkillExecutionId executionId,
            TickIndex startTick,
            TickIndex tick,
            FpgSkillExecutionState state,
            FpgResolvedSkillTimingSnapshot timing)
        {
            Slot = slot;
            Sequence = sequence;
            ExecutionId = executionId;
            StartTick = startTick;
            Tick = tick;
            RelativeTick = checked((int)(tick.Value - startTick.Value));
            State = state;
            ResolvedAnimationId = sequence.ResolveAnimation(executionId);
            Timing = timing;
        }

        public FpgPlayerSkillSlot Slot { get; }
        public FpgCompiledSkillSequence Sequence { get; }
        public SkillExecutionId ExecutionId { get; }
        public TickIndex StartTick { get; }
        public TickIndex Tick { get; }
        public int RelativeTick { get; }
        public FpgSkillExecutionState State { get; }
        public int ResolvedAnimationId { get; }
        public FpgResolvedSkillTimingSnapshot Timing { get; }
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
        private readonly struct PendingReloadIntent
        {
            public PendingReloadIntent(
                TickIndex issuedTick,
                TickIndex expiresTick,
                InputSequence inputSequence)
            {
                IssuedTick = issuedTick;
                ExpiresTick = expiresTick;
                InputSequence = inputSequence;
            }

            public TickIndex IssuedTick { get; }
            public TickIndex ExpiresTick { get; }
            public InputSequence InputSequence { get; }
            public bool IsValid => IssuedTick.IsValid
                && ExpiresTick.IsValid
                && ExpiresTick >= IssuedTick
                && InputSequence.IsValid;

            public bool IsExpiredAt(TickIndex tick)
            {
                return IsValid && tick.IsValid && tick > ExpiresTick;
            }
        }

        private readonly FpgCompiledPlayerSkillDefinition primary;
        private readonly FpgCompiledPlayerSkillDefinition secondary;
        private readonly FpgCompiledPlayerSkillDefinition reload;
        private readonly SecondaryTriggerMode secondaryTriggerMode;
        private readonly FpgAttackSpeedProfile attackSpeedProfile;
        private readonly IAttackSpeedBonusProvider attackSpeedBonusProvider;
        private readonly int inputBufferTicks;
        private readonly FpgSkillExecutionRuntime runtime;
        private readonly FpgPlayerSkillExecutionEvent[] results;
        private readonly FpgPlayerSkillSequenceFrame[] sequenceFrames =
            new FpgPlayerSkillSequenceFrame[3];

        private FpgCompiledPlayerSkillDefinition activeDefinition;
        private FpgPlayerSkillSlot activeSlot;
        private FpgSkillSequenceKind activeSequenceKind;
        private FpgResolvedSkillSchedule activeSchedule;
        private FpgResolvedSkillTimingSnapshot activeTiming;
        private PendingAttackIntent pendingAttackIntent;
        private PendingReloadIntent pendingReloadIntent;
        private FpgSkillExecutionIdAllocator executionIds;
        private bool ownsExecutionIds;
        private bool secondaryEndPending;
        private int resultCount;
        private int sequenceFrameCount;
        private long nextSyntheticInputSequence = 1L;
        private bool primaryHeldLastTick;

        private FpgPlayerSkillExecutionController(
            FpgCompiledPlayerSkillDefinition primary,
            FpgCompiledPlayerSkillDefinition secondary,
            FpgCompiledPlayerSkillDefinition reload,
            SecondaryTriggerMode secondaryTriggerMode,
            FpgAttackSpeedProfile attackSpeedProfile,
            IAttackSpeedBonusProvider attackSpeedBonusProvider,
            int inputBufferTicks,
            int runtimeCapacity,
            int resultCapacity)
        {
            this.primary = primary;
            this.secondary = secondary;
            this.reload = reload;
            this.secondaryTriggerMode = secondaryTriggerMode;
            this.attackSpeedProfile = attackSpeedProfile;
            this.attackSpeedBonusProvider = attackSpeedBonusProvider;
            this.inputBufferTicks = inputBufferTicks;
            executionIds = new FpgSkillExecutionIdAllocator();
            ownsExecutionIds = true;
            runtime = new FpgSkillExecutionRuntime(runtimeCapacity);
            results = new FpgPlayerSkillExecutionEvent[resultCapacity];
        }

        public bool IsExecuting => runtime.IsRunning;
        public FpgPlayerSkillSlot ActiveSlot => activeSlot;
        public FpgSkillSequenceKind ActiveSequenceKind => activeSequenceKind;
        public TickIndex NextTick => runtime.NextTick;
        public TickIndex ActiveStartTick => runtime.IsRunning
            ? runtime.StartTick
            : TickIndex.Invalid;
        public int ResultCount => resultCount;
        public int ResultCapacity => results.Length;
        public int SequenceFrameCount => sequenceFrameCount;
        public TickIndex PlannedLastAttackTick { get; private set; } =
            TickIndex.Invalid;
        public TickIndex PlannedAllowWithdrawTick { get; private set; } =
            TickIndex.Invalid;
        public TickIndex ActionLockedUntilTick { get; private set; } =
            TickIndex.Invalid;
        public TickIndex RecastLockedUntilTick { get; private set; } =
            TickIndex.Invalid;
        public FpgResolvedSkillTimingSnapshot ActiveTiming => activeTiming;
        public TickIndex AttackFrameTick => activeTiming.IsValid
            ? activeTiming.AttackFrameTick
            : TickIndex.Invalid;
        public TickIndex SameAttackReadyTick => activeTiming.IsValid
            ? activeTiming.SameAttackReadyTick
            : TickIndex.Invalid;
        public TickIndex DifferentAttackInterruptTick => activeTiming.IsValid
            ? activeTiming.DifferentAttackInterruptTick
            : TickIndex.Invalid;
        public FpgAttackPhase AttackPhaseAt(TickIndex tick) =>
            activeTiming.GetPhase(tick);
        public bool HasPendingAttackIntent => pendingAttackIntent.IsValid;
        public PendingAttackIntent PendingAttackIntent => pendingAttackIntent;
        public bool HasPendingReloadIntent => pendingReloadIntent.IsValid;
        public bool IsSecondaryEndPending => secondaryEndPending;
        public int ChargeProgressTicks => secondary.ChargeProgressTicks;
        public int ReloadDurationTicks
        {
            get
            {
                return reload.Timeline.TryGetSequence(
                    FpgSkillSequenceKind.Execute,
                    out FpgCompiledSkillSequence sequence)
                        ? checked(sequence.DurationTicks + 1)
                        : 0;
            }
        }

        public int GetRequiredAmmo(FpgPlayerSkillSlot slot)
        {
            FpgCompiledPlayerSkillDefinition definition;
            FpgSkillSequenceKind sequenceKind;
            if (slot == FpgPlayerSkillSlot.Primary)
            {
                definition = primary;
                sequenceKind = FpgSkillSequenceKind.Execute;
            }
            else if (slot == FpgPlayerSkillSlot.Secondary)
            {
                definition = secondary;
                sequenceKind = secondaryTriggerMode
                        == SecondaryTriggerMode.ChargeRelease
                    && secondary.Timeline.TryGetSequence(
                        FpgSkillSequenceKind.Release,
                        out _)
                        ? FpgSkillSequenceKind.Release
                        : FpgSkillSequenceKind.Execute;
            }
            else
            {
                return 0;
            }

            return definition.TryGetSequenceSummary(
                sequenceKind,
                out FpgCompiledPlayerSkillSequenceSummary summary)
                    ? summary.TotalAmmoCost
                    : 0;
        }

        public bool RequiresExposureAt(TickIndex tick)
        {
            if (!tick.IsValid
                || !runtime.IsRunning
                || !runtime.StartTick.IsValid
                || tick < runtime.StartTick
                || (!PlannedAllowWithdrawTick.IsValid
                    && !PlannedLastAttackTick.IsValid)
                || tick > (PlannedAllowWithdrawTick.IsValid
                    ? PlannedAllowWithdrawTick
                    : PlannedLastAttackTick))
            {
                return false;
            }

            if (activeSlot == FpgPlayerSkillSlot.Primary)
            {
                return activeSequenceKind == FpgSkillSequenceKind.Execute;
            }

            return activeSlot == FpgPlayerSkillSlot.Secondary
                && (activeSequenceKind == FpgSkillSequenceKind.Execute
                    || activeSequenceKind == FpgSkillSequenceKind.Release);
        }

        public float GetSecondaryChargeProgress(
            WeaponRuntime weapon,
            TickIndex tick)
        {
            if (weapon == null
                || secondaryTriggerMode != SecondaryTriggerMode.ChargeRelease
                || weapon.State != WeaponState.AltCharging
                || !weapon.SecondaryChargeStartedTick.IsValid
                || !tick.IsValid
                || secondary.ChargeProgressTicks <= 0)
            {
                return 0f;
            }

            long elapsed = tick.Value
                - weapon.SecondaryChargeStartedTick.Value;
            if (elapsed <= 0L)
            {
                return 0f;
            }

            return Math.Min(
                1f,
                (float)elapsed / secondary.ChargeProgressTicks);
        }

        public static bool TryCreate(
            FpgCompiledPlayerSkillDefinition primary,
            FpgCompiledPlayerSkillDefinition secondary,
            FpgCompiledPlayerSkillDefinition reload,
            SecondaryTriggerMode secondaryTriggerMode,
            out FpgPlayerSkillExecutionController controller,
            out string error)
        {
            return TryCreate(
                primary,
                secondary,
                reload,
                secondaryTriggerMode,
                new FpgAttackSpeedProfile(1d, 1d, 2.5d),
                StaticAttackSpeedBonusProvider.Zero,
                4,
                out controller,
                out error);
        }

        public static bool TryCreate(
            FpgCompiledPlayerSkillDefinition primary,
            FpgCompiledPlayerSkillDefinition secondary,
            FpgCompiledPlayerSkillDefinition reload,
            SecondaryTriggerMode secondaryTriggerMode,
            FpgAttackSpeedProfile attackSpeedProfile,
            IAttackSpeedBonusProvider attackSpeedBonusProvider,
            int inputBufferTicks,
            out FpgPlayerSkillExecutionController controller,
            out string error)
        {
            controller = null;
            if (primary == null || secondary == null || reload == null
                || !attackSpeedProfile.IsValid
                || attackSpeedBonusProvider == null
                || inputBufferTicks < 0
                || inputBufferTicks > 32
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
                    attackSpeedProfile,
                    attackSpeedBonusProvider,
                    inputBufferTicks,
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

            ExpireCompletedTiming(frame.Tick);
            ExpirePendingAttackIntent(frame.Tick);
            ExpirePendingReloadIntent(frame.Tick);
            bool secondaryEndPendingAtFrameStart = secondaryEndPending;
            DomainResult currentTick = TickActiveExecution(frame.Tick);
            if (!currentTick.IsSuccess)
            {
                return currentTick;
            }

            if (frame.PrimaryHeld && !primaryHeldLastTick)
            {
                SetPendingAttackIntent(
                    FpgPlayerSkillSlot.Primary,
                    primary,
                    FpgSkillSequenceKind.Execute,
                    frame.Tick,
                    NextSyntheticInputSequence(),
                    FpgAttackIntentSource.PrimaryPressed);
            }
            primaryHeldLastTick = frame.PrimaryHeld;

            if (frame.CancelSecondary)
            {
                ClearPendingAttackIntent();
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
                        SetPendingAttackIntent(
                            FpgPlayerSkillSlot.Secondary,
                            secondary,
                            ResolveSecondaryPressedSequenceKind(),
                            frame.Tick,
                            command.Sequence,
                            FpgAttackIntentSource.SecondaryPressed);
                        break;

                    case InputEdgeType.SecondaryReleased:
                        if (secondaryTriggerMode
                            == SecondaryTriggerMode.ChargeRelease)
                        {
                            if (pendingAttackIntent.IsValid
                                && pendingAttackIntent.Slot
                                    == FpgPlayerSkillSlot.Secondary
                                && pendingAttackIntent.Source
                                    == FpgAttackIntentSource.SecondaryPressed)
                            {
                                ClearPendingAttackIntent();
                            }

                            handled = TryReleaseSecondaryCharge(
                                frame.Tick,
                                player);
                        }
                        break;

                    case InputEdgeType.ReloadPressed:
                        SetPendingReloadIntent(frame.Tick, command.Sequence);
                        break;
                }

                if (!handled.IsSuccess)
                {
                    return handled;
                }
            }

            DomainResult pendingReload = TryConsumePendingReloadIntent(
                frame.Tick,
                player,
                out bool reloadBlocksAttacks);
            if (!pendingReload.IsSuccess)
            {
                return pendingReload;
            }

            if (reloadBlocksAttacks)
            {
                return runtime.IsRunning && runtime.NextTick == frame.Tick
                    ? TickActiveExecution(frame.Tick)
                    : DomainResult.Success;
            }

            DomainResult pending = TryConsumePendingAttackIntent(
                frame.Tick,
                player,
                out bool actionStartedThisTick);
            if (!pending.IsSuccess)
            {
                return pending;
            }

            bool pendingDifferentAttack = pendingAttackIntent.IsValid
                && runtime.IsRunning
                && pendingAttackIntent.Slot != activeSlot;
            if (!actionStartedThisTick
                && !pendingDifferentAttack
                && secondaryTriggerMode
                    == SecondaryTriggerMode.ImmediateRepeatWhileHeld
                && frame.SecondaryHeld)
            {
                DomainResult started = TryStartAction(
                    FpgPlayerSkillSlot.Secondary,
                    secondary,
                    FpgSkillSequenceKind.Execute,
                    frame.Tick,
                    player,
                    out actionStartedThisTick);
                if (!started.IsSuccess)
                {
                    return started;
                }
            }

            if (!actionStartedThisTick
                && !pendingDifferentAttack
                && frame.PrimaryHeld)
            {
                DomainResult started = TryStartAction(
                    FpgPlayerSkillSlot.Primary,
                    primary,
                    FpgSkillSequenceKind.Execute,
                    frame.Tick,
                    player,
                    out actionStartedThisTick);
                if (!started.IsSuccess)
                {
                    return started;
                }
            }

            if (!runtime.IsRunning
                && player.Weapon.State == WeaponState.AltCharging)
            {
                DomainResult continued =
                    TryStartChargeContinuation(frame.Tick);
                if (!continued.IsSuccess)
                {
                    return continued;
                }
            }

            if (!runtime.IsRunning
                && secondaryEndPending
                && secondaryEndPendingAtFrameStart)
            {
                DomainResult end =
                    TryStartSecondaryCancelTimeline(frame.Tick);
                if (!end.IsSuccess)
                {
                    return end;
                }
            }

            return runtime.IsRunning && runtime.NextTick == frame.Tick
                ? TickActiveExecution(frame.Tick)
                : DomainResult.Success;
        }

        private DomainResult TickActiveExecution(TickIndex tick)
        {
            if (!runtime.IsRunning)
            {
                return DomainResult.Success;
            }

            FpgSkillRuntimeResult ticked = runtime.Tick(tick);
            if (!ticked.IsSuccess)
            {
                return MapRuntimeFailure(ticked.Error);
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

            if (!runtime.IsTerminal)
            {
                return DomainResult.Success;
            }

            FpgSkillSequenceKind continuation = FpgSkillSequenceKind.None;
            bool hasSecondaryContinuation =
                activeSlot == FpgPlayerSkillSlot.Secondary
                && runtime.State == FpgSkillExecutionState.Completed
                && FpgSecondarySkillLifecycleRules
                    .TryGetContinuationAfterCompletion(
                        activeSequenceKind,
                        out continuation);
            if (hasSecondaryContinuation
                && continuation == FpgSkillSequenceKind.Cancel)
            {
                secondaryEndPending = true;
            }
            else if (activeSequenceKind == FpgSkillSequenceKind.Cancel)
            {
                secondaryEndPending = false;
            }

            bool preserveRecoveryTiming = activeTiming.IsValid
                && activeTiming.SameAttackReadyTick > tick;
            ClearActive(preserveRecoveryTiming);
            return DomainResult.Success;
        }

        private void ExpireCompletedTiming(TickIndex tick)
        {
            if (!runtime.IsRunning
                && activeTiming.IsValid
                && tick.IsValid
                && tick >= activeTiming.SameAttackReadyTick)
            {
                activeTiming = default(FpgResolvedSkillTimingSnapshot);
            }
        }

        private void ExpirePendingAttackIntent(TickIndex tick)
        {
            if (pendingAttackIntent.IsExpiredAt(tick))
            {
                ClearPendingAttackIntent();
            }
        }

        private void ExpirePendingReloadIntent(TickIndex tick)
        {
            if (pendingReloadIntent.IsExpiredAt(tick))
            {
                ClearPendingReloadIntent();
            }
        }

        private void SetPendingAttackIntent(
            FpgPlayerSkillSlot slot,
            FpgCompiledPlayerSkillDefinition definition,
            FpgSkillSequenceKind sequenceKind,
            TickIndex issuedTick,
            InputSequence inputSequence,
            FpgAttackIntentSource source)
        {
            if (definition == null || !issuedTick.IsValid
                || !inputSequence.IsValid)
            {
                return;
            }

            long expiresValue = issuedTick.Value > long.MaxValue
                    - inputBufferTicks
                ? long.MaxValue
                : issuedTick.Value + inputBufferTicks;
            pendingAttackIntent = new PendingAttackIntent(
                slot,
                sequenceKind,
                definition.GameplayHash,
                issuedTick,
                new TickIndex(expiresValue),
                inputSequence,
                source);
        }

        private InputSequence NextSyntheticInputSequence()
        {
            long value = nextSyntheticInputSequence;
            nextSyntheticInputSequence = value == long.MaxValue
                ? 1L
                : value + 1L;
            return new InputSequence(value);
        }

        private void SetPendingReloadIntent(
            TickIndex issuedTick,
            InputSequence inputSequence)
        {
            if (!issuedTick.IsValid || !inputSequence.IsValid)
            {
                return;
            }

            long expiresValue = issuedTick.Value > long.MaxValue
                    - inputBufferTicks
                ? long.MaxValue
                : issuedTick.Value + inputBufferTicks;
            pendingReloadIntent = new PendingReloadIntent(
                issuedTick,
                new TickIndex(expiresValue),
                inputSequence);
        }

        private DomainResult TryConsumePendingReloadIntent(
            TickIndex tick,
            PlayerRuntime player,
            out bool blocksAttacks)
        {
            blocksAttacks = false;
            ExpirePendingReloadIntent(tick);
            if (!pendingReloadIntent.IsValid)
            {
                return DomainResult.Success;
            }

            if (reload == null)
            {
                ClearPendingReloadIntent();
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (player.Weapon.State == WeaponState.Reloading
                || runtime.IsRunning
                    && activeSlot == FpgPlayerSkillSlot.Reload
                || player.Weapon.Magazine.Ammo
                    >= player.Weapon.Magazine.Capacity)
            {
                ClearPendingReloadIntent();
                return DomainResult.Success;
            }

            blocksAttacks = true;
            DomainResult result = TryStartAction(
                FpgPlayerSkillSlot.Reload,
                reload,
                FpgSkillSequenceKind.Execute,
                tick,
                player,
                out bool actionStarted);
            if (!result.IsSuccess)
            {
                ClearPendingReloadIntent();
                return result;
            }

            if (actionStarted)
            {
                ClearPendingReloadIntent();
                ClearPendingAttackIntent();
            }

            return DomainResult.Success;
        }

        private FpgSkillSequenceKind ResolveSecondaryPressedSequenceKind()
        {
            if (secondaryTriggerMode
                == SecondaryTriggerMode.ImmediateRepeatWhileHeld)
            {
                return FpgSkillSequenceKind.Execute;
            }

            return secondary.Timeline.TryGetSequence(
                FpgSkillSequenceKind.ChargeEnter,
                out _)
                    ? FpgSkillSequenceKind.ChargeEnter
                    : FpgSkillSequenceKind.ChargeLoop;
        }

        private DomainResult TryConsumePendingAttackIntent(
            TickIndex tick,
            PlayerRuntime player,
            out bool actionStarted)
        {
            actionStarted = false;
            ExpirePendingAttackIntent(tick);
            if (!pendingAttackIntent.IsValid)
            {
                return DomainResult.Success;
            }

            FpgCompiledPlayerSkillDefinition definition =
                pendingAttackIntent.Slot == FpgPlayerSkillSlot.Primary
                    ? primary
                    : pendingAttackIntent.Slot
                        == FpgPlayerSkillSlot.Secondary
                            ? secondary
                            : null;
            if (definition == null
                || definition.GameplayHash
                    != pendingAttackIntent.SkillGameplayHash)
            {
                ClearPendingAttackIntent();
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            int requiredAmmo = GetRequiredAmmo(pendingAttackIntent.Slot);
            bool insufficientAmmo =
                requiredAmmo > player.Weapon.Magazine.Ammo;

            bool differentActiveAttack = runtime.IsRunning
                && (activeSlot != pendingAttackIntent.Slot
                    || !ReferenceEquals(activeDefinition, definition)
                    || activeSequenceKind
                        != pendingAttackIntent.SequenceKind);
            if (differentActiveAttack
                && !IsInterruptibleSecondaryEndTimeline()
                && !activeTiming.CanInterruptWithDifferentAttackAt(tick))
            {
                if (insufficientAmmo)
                {
                    ClearPendingAttackIntent();
                }

                return DomainResult.Success;
            }

            DomainResult result;
            if (pendingAttackIntent.Slot == FpgPlayerSkillSlot.Secondary
                && secondaryTriggerMode == SecondaryTriggerMode.ChargeRelease
                && pendingAttackIntent.Source
                    == FpgAttackIntentSource.SecondaryPressed)
            {
                result = TryBeginSecondaryCharge(tick, player);
                actionStarted = result.IsSuccess
                    && player.Weapon.State == WeaponState.AltCharging
                    && runtime.IsRunning
                    && activeSlot == FpgPlayerSkillSlot.Secondary
                    && runtime.StartTick == tick;
            }
            else
            {
                result = TryStartAction(
                    pendingAttackIntent.Slot,
                    definition,
                    pendingAttackIntent.SequenceKind,
                    tick,
                    player,
                    out actionStarted);
            }

            if (actionStarted || !result.IsSuccess || insufficientAmmo)
            {
                ClearPendingAttackIntent();
            }

            return result;
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
            secondaryEndPending = false;
            ClearPendingInputIntents();
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
                        FpgSkillExecutionState.Canceled,
                        frame.Timing);
            }

            runtime.Reset();
            ClearActive();
            secondaryEndPending = false;
            ClearPendingInputIntents();
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
            secondaryEndPending = false;
            ClearPendingInputIntents();
            ClearFrameResults();
            if (ownsExecutionIds)
            {
                executionIds.Reset();
            }
            PlannedLastAttackTick = TickIndex.Invalid;
            PlannedAllowWithdrawTick = TickIndex.Invalid;
            ActionLockedUntilTick = TickIndex.Invalid;
            RecastLockedUntilTick = TickIndex.Invalid;
            primaryHeldLastTick = false;
            nextSyntheticInputSequence = 1L;
        }

        public void ClearPendingAttackIntent()
        {
            pendingAttackIntent = default(PendingAttackIntent);
        }

        public void ClearPendingInputIntents()
        {
            ClearPendingAttackIntent();
            ClearPendingReloadIntent();
        }

        private void ClearPendingReloadIntent()
        {
            pendingReloadIntent = default(PendingReloadIntent);
        }

        private DomainResult TryBeginSecondaryCharge(
            TickIndex tick,
            PlayerRuntime player)
        {
            FpgSkillSequenceKind kind = secondary.Timeline.TryGetSequence(
                FpgSkillSequenceKind.ChargeEnter,
                out _)
                    ? FpgSkillSequenceKind.ChargeEnter
                    : FpgSkillSequenceKind.ChargeLoop;
            if (!secondary.Timeline.TryGetSequence(kind, out _))
            {
                return DomainResult.Rejected(
                    RejectReason.InvalidDefinition);
            }

            bool canInterruptSecondaryEnd =
                IsInterruptibleSecondaryEndTimeline()
                || secondaryEndPending;
            bool canInterruptDifferentAttack = runtime.IsRunning
                && activeTiming.CanInterruptWithDifferentAttackAt(tick)
                && (activeSlot != FpgPlayerSkillSlot.Secondary
                    || activeSequenceKind != kind);
            bool canInterruptCurrent = canInterruptSecondaryEnd
                || canInterruptDifferentAttack;
            if (!TryResolveSchedule(
                    secondary,
                    kind,
                    tick,
                    player.RuntimeId,
                    out FpgResolvedSkillSchedule schedule))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            DomainResult preflight = PrepareTimelineStart(
                schedule,
                tick,
                runtime.IsRunning && canInterruptCurrent,
                out SkillExecutionId executionId);
            if (!preflight.IsSuccess)
            {
                return preflight;
            }

            WeaponRuntimeSnapshot weaponSnapshot =
                player.Weapon.CaptureRoomSnapshot();
            DomainResult begin = player.Weapon.TryBeginSkillSecondaryCharge(
                tick,
                player.Exposure,
                canInterruptCurrent);
            if (!begin.IsSuccess)
            {
                return DomainResult.Success;
            }

            DomainResult interrupted = canInterruptSecondaryEnd
                ? InterruptSecondaryEndTimeline(tick)
                : InterruptActiveTimeline(
                    tick,
                    runtime.IsRunning && canInterruptCurrent);
            if (!interrupted.IsSuccess)
            {
                player.Weapon.RestoreRoomSnapshot(weaponSnapshot);
                return interrupted;
            }

            DomainResult started = StartTimeline(
                FpgPlayerSkillSlot.Secondary,
                secondary,
                kind,
                tick,
                schedule,
                executionId);
            if (!started.IsSuccess)
            {
                player.Weapon.RestoreRoomSnapshot(weaponSnapshot);
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
                secondaryEndPending = false;
                return DomainResult.Success;
            }

            DomainResult started = StartTimeline(
                FpgPlayerSkillSlot.Secondary,
                secondary,
                FpgSkillSequenceKind.Cancel,
                tick);
            if (started.IsSuccess)
            {
                secondaryEndPending = false;
            }

            return started;
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
            return TryStartAction(
                slot,
                definition,
                sequenceKind,
                tick,
                player,
                out _);
        }

        private DomainResult TryStartAction(
            FpgPlayerSkillSlot slot,
            FpgCompiledPlayerSkillDefinition definition,
            FpgSkillSequenceKind sequenceKind,
            TickIndex tick,
            PlayerRuntime player,
            out bool actionStarted)
        {
            actionStarted = false;
            bool canInterruptSecondaryEnd =
                slot != FpgPlayerSkillSlot.Reload
                && (IsInterruptibleSecondaryEndTimeline()
                    || secondaryEndPending);
            bool sameActiveAction = runtime.IsRunning
                && activeSlot == slot
                && ReferenceEquals(activeDefinition, definition)
                && activeSequenceKind == sequenceKind;
            bool canRestartAttackSpeedAction = sameActiveAction
                && activeTiming.UsesCharacterAttackSpeed
                && activeTiming.IsSameAttackReadyAt(tick)
                && HasReachedWeaponRecastBoundary(slot, tick, player.Weapon);
            bool canRestartImmediateSecondary = sameActiveAction
                && slot == FpgPlayerSkillSlot.Secondary
                && sequenceKind == FpgSkillSequenceKind.Execute
                && IsInterruptibleImmediateSecondaryExecuteTimeline();
            bool canInterruptReloadAfterAttack =
                slot == FpgPlayerSkillSlot.Reload
                && CanReloadInterruptAttackAt(tick, player.Weapon);
            bool canInterruptDifferentAttack = runtime.IsRunning
                && slot != FpgPlayerSkillSlot.Reload
                && !sameActiveAction
                && activeTiming.CanInterruptWithDifferentAttackAt(tick);
            bool canInterruptActiveTimeline = canInterruptSecondaryEnd
                || canRestartAttackSpeedAction
                || canRestartImmediateSecondary
                || canInterruptDifferentAttack
                || runtime.IsRunning && canInterruptReloadAfterAttack;
            if (runtime.IsRunning && !canInterruptActiveTimeline)
            {
                return DomainResult.Success;
            }

            if (definition.TryGetTimingDefinition(
                    sequenceKind,
                    out FpgCompiledSkillTimingDefinition targetTiming)
                && !targetTiming.IsFixed
                && IsBeforeWeaponRecastBoundary(
                    slot,
                    tick,
                    player.Weapon))
            {
                return DomainResult.Success;
            }

            if (!definition.Timeline.TryGetSequence(
                    sequenceKind,
                    out FpgCompiledSkillSequence sequence)
                || !definition.TryGetSequenceSummary(
                    sequenceKind,
                    out FpgCompiledPlayerSkillSequenceSummary summary)
                || !TryResolveSchedule(
                    definition,
                    sequenceKind,
                    tick,
                    player.RuntimeId,
                    out FpgResolvedSkillSchedule schedule))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            DomainResult preflight = PrepareTimelineStart(
                schedule,
                tick,
                runtime.IsRunning && canInterruptActiveTimeline,
                out SkillExecutionId executionId);
            if (!preflight.IsSuccess)
            {
                return preflight;
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
                int sequenceLock = checked(schedule.DurationTicks + 1);
                int cooldownLock = summary.LastAttackTick < 0
                    ? 0
                    : checked(
                        summary.LastAttackTick
                        + definition.SequenceCooldownTicks);
                int lockOffset = Math.Max(1, sequenceLock);
                lockedUntil = new TickIndex(checked(tick.Value + lockOffset));
                recastLockedUntil = schedule.Timing.UsesCharacterAttackSpeed
                    ? schedule.Timing.SameAttackReadyTick
                    : summary.LastAttackTick < 0
                        ? TickIndex.Invalid
                        : new TickIndex(checked(tick.Value + cooldownLock));
            }
            catch (OverflowException)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            WeaponRuntimeSnapshot weaponSnapshot =
                player.Weapon.CaptureRoomSnapshot();
            DomainResult begin = player.Weapon.TryBeginSkillAction(
                actionKind,
                tick,
                lockedUntil,
                summary.TotalAmmoCost,
                player.Exposure,
                canInterruptActiveTimeline
                    || canInterruptReloadAfterAttack);
            if (!begin.IsSuccess)
            {
                return DomainResult.Success;
            }

            DomainResult interrupted;
            if (canInterruptSecondaryEnd)
            {
                interrupted = InterruptSecondaryEndTimeline(tick);
            }
            else
            {
                interrupted = InterruptActiveTimeline(
                    tick,
                    runtime.IsRunning && canInterruptActiveTimeline);
            }
            if (!interrupted.IsSuccess)
            {
                player.Weapon.RestoreRoomSnapshot(weaponSnapshot);
                return interrupted;
            }

            DomainResult started = StartTimeline(
                slot,
                definition,
                sequenceKind,
                tick,
                schedule,
                executionId);
            if (!started.IsSuccess)
            {
                player.Weapon.RestoreRoomSnapshot(weaponSnapshot);
                return started;
            }

            ActionLockedUntilTick = lockedUntil;
            RecastLockedUntilTick = recastLockedUntil;
            PlannedLastAttackTick = schedule.Timing.UsesCharacterAttackSpeed
                ? schedule.Timing.AttackFrameTick
                : summary.LastAttackTick < 0
                    ? TickIndex.Invalid
                    : new TickIndex(tick.Value + summary.LastAttackTick);
            PlannedAllowWithdrawTick = sequence.AllowWithdrawTick < 0
                ? PlannedLastAttackTick
                : new TickIndex(
                    checked(tick.Value + sequence.AllowWithdrawTick));
            actionStarted = true;
            return DomainResult.Success;
        }

        private bool CanReloadInterruptAttackAt(
            TickIndex tick,
            WeaponRuntime weapon)
        {
            if (!tick.IsValid || !PlannedLastAttackTick.IsValid
                || tick <= PlannedLastAttackTick || weapon == null)
            {
                return false;
            }

            if (runtime.IsRunning)
            {
                return activeSlot == FpgPlayerSkillSlot.Primary
                        && activeSequenceKind == FpgSkillSequenceKind.Execute
                    || activeSlot == FpgPlayerSkillSlot.Secondary
                        && (activeSequenceKind == FpgSkillSequenceKind.Execute
                            || activeSequenceKind
                                == FpgSkillSequenceKind.Release);
            }

            return weapon.State == WeaponState.PrimaryRecovery
                || weapon.State == WeaponState.AltRecovery;
        }

        private static bool HasReachedWeaponRecastBoundary(
            FpgPlayerSkillSlot slot,
            TickIndex tick,
            WeaponRuntime weapon)
        {
            TickIndex boundary = slot == FpgPlayerSkillSlot.Primary
                ? weapon.PrimaryRecastLockedUntilTick
                : slot == FpgPlayerSkillSlot.Secondary
                    ? weapon.SecondaryRecastLockedUntilTick
                    : TickIndex.Invalid;
            return boundary.IsValid && tick >= boundary;
        }

        private static bool IsBeforeWeaponRecastBoundary(
            FpgPlayerSkillSlot slot,
            TickIndex tick,
            WeaponRuntime weapon)
        {
            TickIndex boundary = slot == FpgPlayerSkillSlot.Primary
                ? weapon.PrimaryRecastLockedUntilTick
                : slot == FpgPlayerSkillSlot.Secondary
                    ? weapon.SecondaryRecastLockedUntilTick
                    : TickIndex.Invalid;
            return boundary.IsValid && tick < boundary;
        }

        private bool IsInterruptibleSecondaryEndTimeline()
        {
            return runtime.IsRunning
                && activeSlot == FpgPlayerSkillSlot.Secondary
                && activeSequenceKind == FpgSkillSequenceKind.Cancel;
        }

        private bool IsInterruptibleImmediateSecondaryExecuteTimeline()
        {
            return secondaryTriggerMode
                    == SecondaryTriggerMode.ImmediateRepeatWhileHeld
                && runtime.IsRunning
                && activeSlot == FpgPlayerSkillSlot.Secondary
                && activeSequenceKind == FpgSkillSequenceKind.Execute;
        }

        private DomainResult InterruptSecondaryEndTimeline(TickIndex tick)
        {
            bool wasPending = secondaryEndPending;
            DomainResult interrupted = InterruptActiveTimeline(
                tick,
                IsInterruptibleSecondaryEndTimeline());
            secondaryEndPending = interrupted.IsSuccess ? false : wasPending;
            return interrupted;
        }

        private DomainResult InterruptImmediateSecondaryExecuteTimeline(
            TickIndex tick)
        {
            return InterruptActiveTimeline(
                tick,
                IsInterruptibleImmediateSecondaryExecuteTimeline());
        }

        private DomainResult InterruptActiveTimeline(
            TickIndex tick,
            bool canInterrupt)
        {
            if (!canInterrupt)
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

            if (appended.IsSuccess)
            {
                ClearActive();
            }

            return appended;
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
                TickIndex.Invalid);
        }

        private DomainResult StartTimeline(
            FpgPlayerSkillSlot slot,
            FpgCompiledPlayerSkillDefinition definition,
            FpgSkillSequenceKind sequenceKind,
            TickIndex tick)
        {
            if (!TryResolveSchedule(
                    definition,
                    sequenceKind,
                    tick,
                    RuntimeId.Invalid,
                    out FpgResolvedSkillSchedule schedule))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            return StartTimeline(
                slot,
                definition,
                sequenceKind,
                tick,
                schedule);
        }

        private DomainResult StartTimeline(
            FpgPlayerSkillSlot slot,
            FpgCompiledPlayerSkillDefinition definition,
            FpgSkillSequenceKind sequenceKind,
            TickIndex tick,
            FpgResolvedSkillSchedule schedule)
        {
            DomainResult preflight = PrepareTimelineStart(
                schedule,
                tick,
                allowReplacingRunning: false,
                out SkillExecutionId executionId);
            return preflight.IsSuccess
                ? StartTimeline(
                    slot,
                    definition,
                    sequenceKind,
                    tick,
                    schedule,
                    executionId)
                : preflight;
        }

        private DomainResult StartTimeline(
            FpgPlayerSkillSlot slot,
            FpgCompiledPlayerSkillDefinition definition,
            FpgSkillSequenceKind sequenceKind,
            TickIndex tick,
            FpgResolvedSkillSchedule schedule,
            SkillExecutionId executionId)
        {
            if (!definition.Timeline.TryGetSequence(
                    sequenceKind,
                    out FpgCompiledSkillSequence sequence)
                || schedule == null
                || !schedule.IsValid
                || schedule.Sequence.GameplayHash != sequence.GameplayHash
                || schedule.Timing.StartTick != tick
                || executionIds == null
                || !executionId.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            FpgSkillRuntimeResult validation = runtime.ValidateStart(
                schedule,
                executionId,
                tick);
            if (!validation.IsSuccess)
            {
                return MapRuntimeFailure(validation.Error);
            }

            FpgSkillRuntimeResult started = runtime.Start(
                schedule,
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
            activeSchedule = schedule;
            activeTiming = schedule.Timing;
            return DomainResult.Success;
        }

        private DomainResult PrepareTimelineStart(
            FpgResolvedSkillSchedule schedule,
            TickIndex tick,
            bool allowReplacingRunning,
            out SkillExecutionId executionId)
        {
            executionId = SkillExecutionId.Invalid;
            if (executionIds == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            try
            {
                executionId = executionIds.Peek();
            }
            catch (OverflowException)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            FpgSkillRuntimeResult validation = runtime.ValidateStart(
                schedule,
                executionId,
                tick,
                allowReplacingRunning);
            return validation.IsSuccess
                ? DomainResult.Success
                : MapRuntimeFailure(validation.Error);
        }

        private bool TryResolveSchedule(
            FpgCompiledPlayerSkillDefinition definition,
            FpgSkillSequenceKind sequenceKind,
            TickIndex tick,
            RuntimeId ownerId,
            out FpgResolvedSkillSchedule schedule)
        {
            schedule = null;
            if (definition == null
                || !definition.Timeline.TryGetSequence(
                    sequenceKind,
                    out FpgCompiledSkillSequence sequence)
                || !definition.TryGetTimingDefinition(
                    sequenceKind,
                    out FpgCompiledSkillTimingDefinition timingDefinition))
            {
                return false;
            }

            double bonusAttackSpeed = 0d;
            if (!timingDefinition.IsFixed)
            {
                try
                {
                    bonusAttackSpeed = attackSpeedBonusProvider
                        .GetBonusAttackSpeed(ownerId, tick);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return FpgAttackTimingResolver.TryResolve(
                sequence,
                timingDefinition,
                definition.SequenceCooldownTicks,
                attackSpeedProfile,
                bonusAttackSpeed,
                tick,
                out schedule,
                out _);
        }

        private DomainResult CancelChargeTimeline(TickIndex tick)
        {
            if (!runtime.IsRunning
                || activeSlot != FpgPlayerSkillSlot.Secondary
                || !FpgSecondarySkillLifecycleRules.IsChargeStage(
                    activeSequenceKind))
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
                    hasPayload,
                    activeTiming);
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
                        runtime.State,
                        activeTiming);
                return DomainResult.Success;
            }
            catch (OverflowException)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }
        }

        private void ClearActive(bool preserveTiming = false)
        {
            activeDefinition = null;
            activeSlot = FpgPlayerSkillSlot.None;
            activeSequenceKind = FpgSkillSequenceKind.None;
            activeSchedule = null;
            if (!preserveTiming)
            {
                activeTiming = default(FpgResolvedSkillTimingSnapshot);
            }
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
