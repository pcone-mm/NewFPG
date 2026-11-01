using System;
using FPG.Demo.Core;
using FPG.Demo.Skills;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Fixed-tick, allocation-free-per-tick enemy AI state machine. It owns
    /// control/brain state, skill eligibility and move requests; concrete
    /// movement and skill timeline implementations are injected through ports.
    /// </summary>
    public sealed class FpgEnemyAiRuntime
    {
        private readonly RuntimeId runtimeId;
        private readonly int spawnSequence;
        private readonly FpgEnemyDefinition definition;
        private readonly IFpgEnemyAiMovementExecutor movement;
        private readonly IFpgEnemyAiSkillExecutor skillExecutor;
        private readonly TickIndex[] nextSkillReadyTicks;

        private TickIndex activationTick;
        private TickIndex lastTick = TickIndex.Invalid;
        private TickIndex stateEnterTick = TickIndex.Invalid;
        private TickIndex nextMoveRetryTick = TickIndex.Invalid;
        private TickIndex movePhaseEndTick = TickIndex.Invalid;
        private TickIndex controlEndTick = TickIndex.Invalid;
        private TickIndex hitReactEndTick = TickIndex.Invalid;
        private FpgEnemyAiControlState controlState;
        private FpgEnemyAiBrainState brainState;
        private FpgEnemyAiTransitionReason lastTransitionReason;
        private FpgEnemyAiCurrentSkillPolicy currentSkillPolicy;
        private bool allowMovement = true;
        private bool allowNewSkill = true;
        private bool hasMovementGoal;
        private bool mustReposition;
        private int activePatternOrdinal = -1;
        private SkillExecutionId skillExecutionId = SkillExecutionId.Invalid;

        public FpgEnemyAiRuntime(
            RuntimeId runtimeId,
            int spawnSequence,
            TickIndex activationTick,
            FpgEnemyDefinition definition,
            IFpgEnemyAiMovementExecutor movement,
            IFpgEnemyAiSkillExecutor skillExecutor)
        {
            if (!runtimeId.IsValid || spawnSequence < 0
                || !activationTick.IsValid)
            {
                throw new ArgumentException("Enemy AI identity is invalid.");
            }

            this.definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            if (definition.AttackPatternCount <= 0 || definition.Behavior == null)
            {
                throw new ArgumentException(
                    "Enemy AI requires a behavior definition and at least one skill.",
                    nameof(definition));
            }
            this.movement = movement
                ?? throw new ArgumentNullException(nameof(movement));
            this.skillExecutor = skillExecutor
                ?? throw new ArgumentNullException(nameof(skillExecutor));

            this.runtimeId = runtimeId;
            this.spawnSequence = spawnSequence;
            this.activationTick = activationTick;
            nextSkillReadyTicks = new TickIndex[definition.AttackPatternCount];
            Reset(activationTick);
        }

        public RuntimeId RuntimeId => runtimeId;
        public int SpawnSequence => spawnSequence;
        public FpgEnemyDefinition Definition => definition;
        public FpgEnemyAiControlState ControlState => controlState;
        public FpgEnemyAiBrainState BrainState => brainState;
        public FpgEnemyAiMovementState MovementState => movement.State;
        public TickIndex LastTick => lastTick;
        public FpgEnemyAiTransitionReason LastTransitionReason => lastTransitionReason;
        public int ActivePatternOrdinal => activePatternOrdinal;
        public event Action<FpgEnemyAiTransition> Transitioned;
        public event Action<FpgEnemyAiMoveRequest> MoveRequested;
        public event Action<FpgEnemyAiSkillRequest> SkillRequested;

        public FpgEnemyAiSnapshot GetSnapshot()
        {
            TickIndex nextDecision = nextSkillReadyTicks.Length == 0
                ? nextMoveRetryTick
                : GetNextDecisionTick();
            movement.TryGetMovementGoal(
                out FpgEnemyAiMovementGoal movementGoal);
            return new FpgEnemyAiSnapshot(
                runtimeId,
                controlState,
                brainState,
                activePatternOrdinal >= 0
                    && activePatternOrdinal < definition.AttackPatternCount
                    ? definition.GetAttackPattern(activePatternOrdinal).SkillId
                    : string.Empty,
                skillExecutionId,
                stateEnterTick,
                nextDecision,
                hasMovementGoal,
                movementGoal,
                movement.State,
                controlEndTick,
                lastTransitionReason);
        }

        public DomainResult Advance(
            TickIndex tick,
            in FpgEnemyAiObservation observation)
        {
            if (!tick.IsValid
                || (lastTick.IsValid && tick.Value != lastTick.Value + 1L)
                || tick < activationTick)
            {
                lastTransitionReason = FpgEnemyAiTransitionReason.InvalidTick;
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            if (controlState == FpgEnemyAiControlState.Dead)
            {
                return DomainResult.Success;
            }

            lastTick = tick;
            ExpireControlIfNeeded(tick);

            if (!observation.IsAlive)
            {
                MarkDead(tick);
                return DomainResult.Success;
            }

            DomainResult skillResult = AdvanceRunningSkill(tick);
            if (!skillResult.IsSuccess)
            {
                return skillResult;
            }

            if (brainState == FpgEnemyAiBrainState.Entry)
            {
                TransitionBrain(
                    FpgEnemyAiBrainState.LocomotionIdle,
                    tick,
                    FpgEnemyAiTransitionReason.EntryCompleted);
            }

            if (controlState == FpgEnemyAiControlState.HitReact
                || (controlState == FpgEnemyAiControlState.ControlLocked
                    && !allowMovement))
            {
                lastTransitionReason = controlState == FpgEnemyAiControlState.HitReact
                    ? FpgEnemyAiTransitionReason.HitReact
                    : FpgEnemyAiTransitionReason.MovementLocked;
                return DomainResult.Success;
            }

            switch (brainState)
            {
                case FpgEnemyAiBrainState.LocomotionIdle:
                    AdvanceIdle(tick, observation);
                    break;

                case FpgEnemyAiBrainState.LocomotionReposition:
                    AdvanceReposition(tick, observation);
                    break;

                case FpgEnemyAiBrainState.LocomotionWaitingForValidPosition:
                    AdvanceWaitingForPosition(tick, observation);
                    break;

                case FpgEnemyAiBrainState.AttackDecision:
                    AdvanceAttackDecision(tick, observation);
                    break;

                case FpgEnemyAiBrainState.Recovery:
                    AdvanceRecovery(tick);
                    break;

                case FpgEnemyAiBrainState.SkillExecuting:
                    // AdvanceRunningSkill handled the executor above.
                    break;

                case FpgEnemyAiBrainState.Fault:
                case FpgEnemyAiBrainState.Entry:
                    break;
            }

            return DomainResult.Success;
        }

        /// <summary>
        /// Scheduler-facing alias for <see cref="Advance"/>. Keeping the
        /// method named Tick makes it possible for a formal encounter host to
        /// drive every enemy from the same fixed-tick loop.
        /// </summary>
        public DomainResult Tick(
            TickIndex tick,
            in FpgEnemyAiObservation observation)
        {
            return Advance(tick, in observation);
        }

        public DomainResult Tick(
            TickIndex tick,
            IFpgEnemyAiContext context)
        {
            if (context == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            if (!context.TrySample(
                    tick,
                    runtimeId,
                    definition,
                    out FpgEnemyAiObservation observation))
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            return Advance(tick, in observation);
        }

        /// <summary>
        /// Starts or refreshes ordinary HitReact. A running skill is allowed
        /// to continue; movement and new skill requests remain locked.
        /// </summary>
        public DomainResult NotifyHit(TickIndex tick, int durationTicks)
        {
            if (!tick.IsValid || durationTicks < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (controlState == FpgEnemyAiControlState.Dead)
            {
                return DomainResult.Rejected(RejectReason.AlreadyTerminal);
            }

            if (!TryAddTicks(tick, durationTicks, out TickIndex endTick))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (currentSkillPolicy == FpgEnemyAiCurrentSkillPolicy.Freeze)
            {
                skillExecutor.InterruptSkill(
                    FpgEnemyAiCurrentSkillPolicy.Continue,
                    tick,
                    TickIndex.Invalid);
            }

            SetControlState(
                FpgEnemyAiControlState.HitReact,
                tick,
                FpgEnemyAiTransitionReason.HitReact);
            hitReactEndTick = endTick;
            controlEndTick = endTick;
            allowMovement = false;
            allowNewSkill = false;
            currentSkillPolicy = FpgEnemyAiCurrentSkillPolicy.Continue;
            mustReposition = true;
            hasMovementGoal = false;
            movement.CancelMovement();
            return DomainResult.Success;
        }

        public DomainResult ApplyControlLock(
            TickIndex tick,
            in FpgEnemyAiControlLock controlLock)
        {
            if (!tick.IsValid || controlLock.DurationTicks < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (controlState == FpgEnemyAiControlState.Dead)
            {
                return DomainResult.Rejected(RejectReason.AlreadyTerminal);
            }

            if (!TryAddTicks(
                    tick,
                    controlLock.DurationTicks,
                    out TickIndex endTick))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            SetControlState(
                FpgEnemyAiControlState.ControlLocked,
                tick,
                FpgEnemyAiTransitionReason.ControlLocked);
            controlEndTick = endTick;
            allowMovement = controlLock.AllowMovement;
            allowNewSkill = controlLock.AllowNewSkill;
            currentSkillPolicy = controlLock.CurrentSkillPolicy;
            if (!allowMovement)
            {
                hasMovementGoal = false;
                movement.CancelMovement();
            }

            skillExecutor.InterruptSkill(
                currentSkillPolicy,
                tick,
                currentSkillPolicy == FpgEnemyAiCurrentSkillPolicy.Freeze
                    ? endTick
                    : TickIndex.Invalid);

            return DomainResult.Success;
        }

        public DomainResult SetControlLock(
            TickIndex tick,
            in FpgEnemyAiControlLock controlLock)
        {
            return ApplyControlLock(tick, in controlLock);
        }

        /// <summary>
        /// Allows a scheduler adapter to publish a skill start when the skill
        /// was accepted outside the runtime's direct executor call.
        /// </summary>
        public DomainResult NotifySkillStarted(
            TickIndex tick,
            int patternOrdinal,
            SkillExecutionId executionId)
        {
            if (!tick.IsValid || patternOrdinal < 0
                || patternOrdinal >= definition.AttackPatternCount
                || !executionId.IsValid
                || controlState == FpgEnemyAiControlState.Dead)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            activePatternOrdinal = patternOrdinal;
            skillExecutionId = executionId;
            TransitionBrain(
                FpgEnemyAiBrainState.SkillExecuting,
                tick,
                FpgEnemyAiTransitionReason.EntryCompleted);
            return DomainResult.Success;
        }

        /// <summary>
        /// Completes a skill from an external scheduler callback. The cooldown
        /// and post-attack movement phase stay independent, as authored in the
        /// enemy behavior and skill assets.
        /// </summary>
        public DomainResult NotifySkillCompleted(
            TickIndex tick,
            bool interrupted = false)
        {
            if (!tick.IsValid || controlState == FpgEnemyAiControlState.Dead)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            DomainResult result = CompleteSkill(tick, interrupted);
            if (!result.IsSuccess)
            {
                return result;
            }

            return DomainResult.Success;
        }

        public DomainResult NotifyMovementCompleted(TickIndex tick)
        {
            if (!tick.IsValid || controlState == FpgEnemyAiControlState.Dead)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            hasMovementGoal = false;
            nextMoveRetryTick = TickIndex.Invalid;
            if (controlState == FpgEnemyAiControlState.HitReact
                || (controlState == FpgEnemyAiControlState.ControlLocked
                    && !allowMovement))
            {
                mustReposition = true;
                return DomainResult.Success;
            }

            mustReposition = false;
            if (brainState == FpgEnemyAiBrainState.LocomotionReposition
                || brainState
                    == FpgEnemyAiBrainState.LocomotionWaitingForValidPosition)
            {
                TransitionBrain(
                    FpgEnemyAiBrainState.LocomotionIdle,
                    tick,
                    FpgEnemyAiTransitionReason.PositionSatisfied);
            }

            return DomainResult.Success;
        }

        public DomainResult NotifyMovementNoValidPosition(TickIndex tick)
        {
            if (!tick.IsValid || controlState == FpgEnemyAiControlState.Dead)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            hasMovementGoal = false;
            TransitionBrain(
                FpgEnemyAiBrainState.LocomotionWaitingForValidPosition,
                tick,
                FpgEnemyAiTransitionReason.NoValidPosition);
            ScheduleMoveRetry(tick);
            return DomainResult.Success;
        }

        public DomainResult MarkDead(TickIndex tick)
        {
            if (!tick.IsValid)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            if (controlState == FpgEnemyAiControlState.Dead)
            {
                return DomainResult.Success;
            }

            SetControlState(
                FpgEnemyAiControlState.Dead,
                tick,
                FpgEnemyAiTransitionReason.Dead);
            movement.CancelMovement();
            skillExecutor.InterruptSkill(
                FpgEnemyAiCurrentSkillPolicy.Interrupt,
                tick,
                TickIndex.Invalid);
            hasMovementGoal = false;
            activePatternOrdinal = -1;
            skillExecutionId = SkillExecutionId.Invalid;
            return DomainResult.Success;
        }

        /// <summary>
        /// Clears runtime state for a pooled enemy and applies a new activation
        /// tick. Authoring assets and injected executors remain untouched.
        /// </summary>
        public DomainResult Reset(TickIndex newActivationTick)
        {
            if (!newActivationTick.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            movement.CancelMovement();
            skillExecutor.InterruptSkill(
                FpgEnemyAiCurrentSkillPolicy.Interrupt,
                newActivationTick,
                TickIndex.Invalid);
            activationTick = newActivationTick;
            lastTick = TickIndex.Invalid;
            stateEnterTick = newActivationTick;
            nextMoveRetryTick = newActivationTick;
            movePhaseEndTick = TickIndex.Invalid;
            controlEndTick = TickIndex.Invalid;
            hitReactEndTick = TickIndex.Invalid;
            controlState = FpgEnemyAiControlState.Active;
            brainState = FpgEnemyAiBrainState.Entry;
            lastTransitionReason = FpgEnemyAiTransitionReason.Reset;
            currentSkillPolicy = FpgEnemyAiCurrentSkillPolicy.Continue;
            allowMovement = true;
            allowNewSkill = true;
            hasMovementGoal = false;
            mustReposition = false;
            activePatternOrdinal = -1;
            skillExecutionId = SkillExecutionId.Invalid;
            for (int index = 0; index < nextSkillReadyTicks.Length; index++)
            {
                FpgEnemyAttackDefinition attack =
                    definition.GetAttackPattern(index);
                if (!TryAddTicks(
                        newActivationTick,
                        attack.FirstReadyOffsetTicks,
                        out nextSkillReadyTicks[index]))
                {
                    return DomainResult.Rejected(RejectReason.InvalidDefinition);
                }
            }

            return DomainResult.Success;
        }

        private DomainResult AdvanceRunningSkill(TickIndex tick)
        {
            bool running = skillExecutor.State
                == FpgEnemyAiSkillExecutorState.Running;
            if (running
                && (controlState != FpgEnemyAiControlState.ControlLocked
                    || currentSkillPolicy
                        == FpgEnemyAiCurrentSkillPolicy.Continue
                    || currentSkillPolicy
                        == FpgEnemyAiCurrentSkillPolicy.Interrupt))
            {
                skillExecutor.AdvanceSkill(tick);
            }

            FpgEnemyAiSkillExecutorState state = skillExecutor.State;
            if (state == FpgEnemyAiSkillExecutorState.Faulted)
            {
                TransitionBrain(
                    FpgEnemyAiBrainState.Fault,
                    tick,
                    FpgEnemyAiTransitionReason.SkillFaulted);
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            if (brainState != FpgEnemyAiBrainState.SkillExecuting)
            {
                return DomainResult.Success;
            }

            if (state == FpgEnemyAiSkillExecutorState.Completed
                || state == FpgEnemyAiSkillExecutorState.Interrupted)
            {
                return CompleteSkill(
                    tick,
                    state == FpgEnemyAiSkillExecutorState.Interrupted);
            }

            if (state == FpgEnemyAiSkillExecutorState.Idle)
            {
                // An external scheduler may publish SkillStarted before its
                // executor exposes Running. Keep the brain in SkillExecuting
                // until the corresponding completion callback arrives.
                lastTransitionReason = FpgEnemyAiTransitionReason.SkillRunning;
            }

            return DomainResult.Success;
        }

        private void AdvanceIdle(
            TickIndex tick,
            in FpgEnemyAiObservation observation)
        {
            if (!observation.HasTarget)
            {
                lastTransitionReason = FpgEnemyAiTransitionReason.NoTarget;
                return;
            }

            if (definition.Behavior.UsesStationaryMovement)
            {
                mustReposition = false;
                movement.CancelMovement();
                if (CanConsiderSkill(tick, observation))
                {
                    TransitionBrain(
                        FpgEnemyAiBrainState.AttackDecision,
                        tick,
                        FpgEnemyAiTransitionReason.EntryCompleted);
                    AdvanceAttackDecision(tick, observation);
                    return;
                }

                lastTransitionReason = GetSkillBlockReason(
                    tick,
                    in observation);
                return;
            }

            if (definition.Behavior.UsesLateralChaseMovement)
            {
                if (mustReposition)
                {
                    TransitionBrain(
                        FpgEnemyAiBrainState.LocomotionReposition,
                        tick,
                        FpgEnemyAiTransitionReason.OutsideAttackRange);
                    return;
                }

                if (CanConsiderSkill(tick, observation))
                {
                    TransitionBrain(
                        FpgEnemyAiBrainState.AttackDecision,
                        tick,
                        FpgEnemyAiTransitionReason.EntryCompleted);
                    AdvanceAttackDecision(tick, observation);
                    return;
                }

                if (observation.ShouldReposition
                    || !IsAttackDistanceValid(observation))
                {
                    TransitionBrain(
                        FpgEnemyAiBrainState.LocomotionReposition,
                        tick,
                        FpgEnemyAiTransitionReason.OutsideAttackRange);
                    return;
                }

                lastTransitionReason = GetSkillBlockReason(
                    tick,
                    in observation);
                return;
            }

            if (mustReposition || observation.ShouldReposition
                || !IsAttackDistanceValid(observation))
            {
                TransitionBrain(
                    FpgEnemyAiBrainState.LocomotionReposition,
                    tick,
                    FpgEnemyAiTransitionReason.OutsideAttackRange);
                return;
            }

            if (CanConsiderSkill(tick, observation))
            {
                TransitionBrain(
                    FpgEnemyAiBrainState.AttackDecision,
                    tick,
                    FpgEnemyAiTransitionReason.EntryCompleted);
                AdvanceAttackDecision(tick, observation);
                return;
            }

            lastTransitionReason = GetSkillBlockReason(
                tick,
                in observation);
        }

        private void AdvanceReposition(
            TickIndex tick,
            in FpgEnemyAiObservation observation)
        {
            if (!observation.CanMove || !allowMovement)
            {
                movement.CancelMovement();
                hasMovementGoal = false;
                lastTransitionReason = FpgEnemyAiTransitionReason.MovementLocked;
                return;
            }

            if (definition.Behavior.UsesLateralChaseMovement
                && !mustReposition
                && CanConsiderSkill(tick, observation))
            {
                TransitionBrain(
                    FpgEnemyAiBrainState.AttackDecision,
                    tick,
                    FpgEnemyAiTransitionReason.EntryCompleted);
                AdvanceAttackDecision(tick, observation);
                return;
            }

            if (observation.PositionSatisfiesAttack
                && !observation.ShouldReposition
                && !mustReposition)
            {
                movement.CancelMovement();
                hasMovementGoal = false;
                TransitionBrain(
                    FpgEnemyAiBrainState.LocomotionIdle,
                    tick,
                    FpgEnemyAiTransitionReason.PositionSatisfied);
                return;
            }

            movement.AdvanceMovement(tick);
            FpgEnemyAiMovementState movementState = movement.State;
            if (movementState == FpgEnemyAiMovementState.Faulted)
            {
                TransitionBrain(
                    FpgEnemyAiBrainState.Fault,
                    tick,
                    FpgEnemyAiTransitionReason.MovementFaulted);
                return;
            }

            if (movementState == FpgEnemyAiMovementState.Moving)
            {
                hasMovementGoal = true;
                return;
            }

            if (movementState == FpgEnemyAiMovementState.Idle)
            {
                if (hasMovementGoal)
                {
                    movement.CancelMovement();
                    hasMovementGoal = false;
                    mustReposition = false;
                    nextMoveRetryTick = TickIndex.Invalid;
                    TransitionBrain(
                        FpgEnemyAiBrainState.LocomotionIdle,
                        tick,
                        FpgEnemyAiTransitionReason.MovementCompleted);
                    return;
                }
            }

            if (movementState == FpgEnemyAiMovementState.NoValidPosition)
            {
                hasMovementGoal = false;
                TransitionBrain(
                    FpgEnemyAiBrainState.LocomotionWaitingForValidPosition,
                    tick,
                    FpgEnemyAiTransitionReason.NoValidPosition);
                ScheduleMoveRetry(tick);
                return;
            }

            if (observation.PositionSatisfiesAttack
                && !observation.ShouldReposition
                && !mustReposition)
            {
                movement.CancelMovement();
                hasMovementGoal = false;
                mustReposition = false;
                TransitionBrain(
                    FpgEnemyAiBrainState.LocomotionIdle,
                    tick,
                    FpgEnemyAiTransitionReason.PositionSatisfied);
                return;
            }

            if (!hasMovementGoal
                && (nextMoveRetryTick == TickIndex.Invalid
                    || tick >= nextMoveRetryTick))
            {
                FpgEnemyAiMoveRequest request = new FpgEnemyAiMoveRequest(
                    runtimeId,
                    tick,
                    FpgEnemyAiTransitionReason.OutsideAttackRange,
                    mustReposition,
                    definition.Behavior == null
                        ? 0f
                        : definition.Behavior.MoveSpeed);
                MoveRequested?.Invoke(request);
                if (movement.TryBeginMove(in request))
                {
                    hasMovementGoal = true;
                    nextMoveRetryTick = TickIndex.Invalid;
                }
                else if (movement.State
                    == FpgEnemyAiMovementState.Faulted)
                {
                    TransitionBrain(
                        FpgEnemyAiBrainState.Fault,
                        tick,
                        FpgEnemyAiTransitionReason.MovementFaulted);
                }
                else
                {
                    hasMovementGoal = false;
                    TransitionBrain(
                        FpgEnemyAiBrainState.LocomotionWaitingForValidPosition,
                        tick,
                        FpgEnemyAiTransitionReason.NoValidPosition);
                    ScheduleMoveRetry(tick);
                }
            }
        }

        private void AdvanceWaitingForPosition(
            TickIndex tick,
            in FpgEnemyAiObservation observation)
        {
            if (definition.Behavior.UsesLateralChaseMovement
                && !mustReposition
                && CanConsiderSkill(tick, observation))
            {
                TransitionBrain(
                    FpgEnemyAiBrainState.AttackDecision,
                    tick,
                    FpgEnemyAiTransitionReason.EntryCompleted);
                AdvanceAttackDecision(tick, observation);
                return;
            }

            if (observation.PositionSatisfiesAttack
                && !observation.ShouldReposition
                && !mustReposition)
            {
                mustReposition = false;
                TransitionBrain(
                    FpgEnemyAiBrainState.LocomotionIdle,
                    tick,
                    FpgEnemyAiTransitionReason.PositionSatisfied);
                return;
            }

            if (nextMoveRetryTick != TickIndex.Invalid
                && tick < nextMoveRetryTick)
            {
                lastTransitionReason = FpgEnemyAiTransitionReason.NoValidPosition;
                return;
            }

            TransitionBrain(
                FpgEnemyAiBrainState.LocomotionReposition,
                tick,
                FpgEnemyAiTransitionReason.NoValidPosition);
            AdvanceReposition(tick, observation);
        }

        private void AdvanceAttackDecision(
            TickIndex tick,
            in FpgEnemyAiObservation observation)
        {
            if (!CanConsiderSkill(tick, observation))
            {
                TransitionBrain(
                    FpgEnemyAiBrainState.LocomotionIdle,
                    tick,
                    GetSkillBlockReason(tick, in observation));
                return;
            }

            int selectedOrdinal = SelectSkill(tick);
            if (selectedOrdinal < 0)
            {
                TransitionBrain(
                    FpgEnemyAiBrainState.LocomotionIdle,
                    tick,
                    FpgEnemyAiTransitionReason.NoAvailableSkill);
                return;
            }

            if (skillExecutor.State == FpgEnemyAiSkillExecutorState.Running)
            {
                TransitionBrain(
                    FpgEnemyAiBrainState.SkillExecuting,
                    tick,
                    FpgEnemyAiTransitionReason.SkillRunning);
                return;
            }

            FpgEnemyAiSkillRequest request = new FpgEnemyAiSkillRequest(
                runtimeId,
                tick,
                selectedOrdinal,
                definition.GetAttackPattern(selectedOrdinal));
            SkillRequested?.Invoke(request);
            if (!skillExecutor.TryStartSkill(in request))
            {
                lastTransitionReason = FpgEnemyAiTransitionReason.NoAvailableSkill;
                return;
            }

            movement.CancelMovement();
            hasMovementGoal = false;
            activePatternOrdinal = selectedOrdinal;
            skillExecutionId = skillExecutor.ExecutionId;
            TransitionBrain(
                FpgEnemyAiBrainState.SkillExecuting,
                tick,
                FpgEnemyAiTransitionReason.EntryCompleted);
        }

        private void AdvanceRecovery(TickIndex tick)
        {
            TickIndex earliestSkillReady = TickIndex.Invalid;
            for (int index = 0; index < nextSkillReadyTicks.Length; index++)
            {
                if (!earliestSkillReady.IsValid
                    || nextSkillReadyTicks[index] < earliestSkillReady)
                {
                    earliestSkillReady = nextSkillReadyTicks[index];
                }
            }

            TickIndex earliest = movePhaseEndTick;
            if (!earliest.IsValid
                || (earliestSkillReady.IsValid
                    && earliestSkillReady > earliest))
            {
                earliest = earliestSkillReady;
            }

            if (earliest.IsValid && tick < earliest)
            {
                lastTransitionReason = FpgEnemyAiTransitionReason.WaitingForCooldown;
                return;
            }

            FpgEnemyAiBrainState nextState =
                definition.Behavior.UsesStationaryMovement
                    ? FpgEnemyAiBrainState.LocomotionIdle
                    : FpgEnemyAiBrainState.LocomotionReposition;
            TransitionBrain(
                nextState,
                tick,
                FpgEnemyAiTransitionReason.SkillCompleted);
        }

        private DomainResult CompleteSkill(TickIndex tick, bool interrupted)
        {
            if (activePatternOrdinal >= 0
                && activePatternOrdinal < nextSkillReadyTicks.Length)
            {
                FpgEnemyAttackDefinition attack =
                    definition.GetAttackPattern(activePatternOrdinal);
                if (!TryAddTicks(
                        tick,
                        attack.SequenceCooldownTicks,
                        out nextSkillReadyTicks[activePatternOrdinal]))
                {
                    TransitionBrain(
                        FpgEnemyAiBrainState.Fault,
                        tick,
                        FpgEnemyAiTransitionReason.SkillFaulted);
                    return DomainResult.Rejected(RejectReason.InvariantFault);
                }
            }

            if (!TryAddTicks(
                    tick,
                    definition.Behavior == null
                        ? 0
                        : definition.Behavior.MoveDurationTicks,
                    out movePhaseEndTick))
            {
                TransitionBrain(
                    FpgEnemyAiBrainState.Fault,
                    tick,
                    FpgEnemyAiTransitionReason.SkillFaulted);
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            mustReposition = true;
            TransitionBrain(
                FpgEnemyAiBrainState.Recovery,
                tick,
                interrupted
                    ? FpgEnemyAiTransitionReason.SkillInterrupted
                    : FpgEnemyAiTransitionReason.SkillCompleted);
            return DomainResult.Success;
        }

        private bool CanConsiderSkill(
            TickIndex tick,
            in FpgEnemyAiObservation observation)
        {
            return controlState == FpgEnemyAiControlState.Active
                && allowNewSkill
                && observation.CanStartSkill
                && observation.HasTarget
                && observation.PositionSatisfiesAttack
                && IsAttackDistanceValid(observation)
                && observation.HasLineOfSight
                && skillExecutor.State != FpgEnemyAiSkillExecutorState.Running
                && IsAnySkillReady(tick);
        }

        private FpgEnemyAiTransitionReason GetSkillBlockReason(
            TickIndex tick,
            in FpgEnemyAiObservation observation)
        {
            if (controlState == FpgEnemyAiControlState.HitReact)
            {
                return FpgEnemyAiTransitionReason.HitReact;
            }

            if (controlState == FpgEnemyAiControlState.ControlLocked)
            {
                return FpgEnemyAiTransitionReason.ControlLocked;
            }

            if (!observation.HasTarget)
            {
                return FpgEnemyAiTransitionReason.NoTarget;
            }

            if (!observation.PositionSatisfiesAttack
                || !IsAttackDistanceValid(observation))
            {
                return FpgEnemyAiTransitionReason.OutsideAttackRange;
            }

            if (!observation.HasLineOfSight)
            {
                return FpgEnemyAiTransitionReason.NoLineOfSight;
            }

            if (!allowNewSkill || !observation.CanStartSkill)
            {
                return FpgEnemyAiTransitionReason.MovementLocked;
            }

            if (skillExecutor.State == FpgEnemyAiSkillExecutorState.Running)
            {
                return FpgEnemyAiTransitionReason.SkillRunning;
            }

            if (!IsAnySkillReady(tick))
            {
                return FpgEnemyAiTransitionReason.WaitingForCooldown;
            }

            return FpgEnemyAiTransitionReason.NoAvailableSkill;
        }

        private int SelectSkill(TickIndex tick)
        {
            int selected = -1;
            int selectedPriority = int.MinValue;
            for (int index = 0; index < nextSkillReadyTicks.Length; index++)
            {
                if (tick < nextSkillReadyTicks[index])
                {
                    continue;
                }

                int priority = definition.GetAttackPattern(index).Priority;
                if (selected < 0 || priority > selectedPriority)
                {
                    selected = index;
                    selectedPriority = priority;
                }
            }

            return selected;
        }

        private bool IsAnySkillReady(TickIndex tick)
        {
            for (int index = 0; index < nextSkillReadyTicks.Length; index++)
            {
                if (tick >= nextSkillReadyTicks[index])
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsAttackDistanceValid(
            in FpgEnemyAiObservation observation)
        {
            if (!observation.HasTarget)
            {
                return false;
            }

            FpgEnemyBehaviorDefinition behavior = definition.Behavior;
            if (behavior != null && behavior.PositionQuery != null)
            {
                return behavior.PositionQuery.IsAttackDistanceValid(
                    observation.TargetDistance);
            }

            if (behavior != null
                && behavior.Mode == FpgEnemyBehaviorMode.LateralChase)
            {
                return observation.TargetDistance
                    <= behavior.LateralAttackDistance;
            }

            return observation.PositionSatisfiesAttack;
        }

        private void ExpireControlIfNeeded(TickIndex tick)
        {
            if (controlState == FpgEnemyAiControlState.HitReact
                && hitReactEndTick.IsValid
                && tick >= hitReactEndTick)
            {
                SetControlState(
                    FpgEnemyAiControlState.Active,
                    tick,
                    FpgEnemyAiTransitionReason.EntryCompleted);
                hitReactEndTick = TickIndex.Invalid;
                controlEndTick = TickIndex.Invalid;
                allowMovement = true;
                allowNewSkill = true;
                hasMovementGoal = false;
                mustReposition = true;
                movement.CancelMovement();
                if (brainState != FpgEnemyAiBrainState.SkillExecuting
                    && brainState != FpgEnemyAiBrainState.Recovery)
                {
                    TransitionBrain(
                        definition.Behavior.UsesStationaryMovement
                            ? FpgEnemyAiBrainState.LocomotionIdle
                            : FpgEnemyAiBrainState.LocomotionReposition,
                        tick,
                        FpgEnemyAiTransitionReason.HitReact);
                }

                if (definition.Behavior.UsesStationaryMovement)
                {
                    mustReposition = false;
                }
            }

            if (controlState == FpgEnemyAiControlState.ControlLocked
                && controlEndTick.IsValid
                && tick >= controlEndTick)
            {
                SetControlState(
                    FpgEnemyAiControlState.Active,
                    tick,
                    FpgEnemyAiTransitionReason.EntryCompleted);
                controlEndTick = TickIndex.Invalid;
                allowMovement = true;
                allowNewSkill = true;
                currentSkillPolicy = FpgEnemyAiCurrentSkillPolicy.Continue;
            }
        }

        private void ScheduleMoveRetry(TickIndex tick)
        {
            int retryTicks = definition.Behavior != null
                && definition.Behavior.PositionQuery != null
                ? definition.Behavior.PositionQuery.RetryIntervalTicks
                : 1;
            TryAddTicks(tick, retryTicks, out nextMoveRetryTick);
        }

        private TickIndex GetNextDecisionTick()
        {
            TickIndex next = nextMoveRetryTick;
            for (int index = 0; index < nextSkillReadyTicks.Length; index++)
            {
                if (!next.IsValid || nextSkillReadyTicks[index] < next)
                {
                    next = nextSkillReadyTicks[index];
                }
            }

            if (controlEndTick.IsValid
                && (!next.IsValid || controlEndTick < next))
            {
                next = controlEndTick;
            }

            return next;
        }

        private void SetControlState(
            FpgEnemyAiControlState next,
            TickIndex tick,
            FpgEnemyAiTransitionReason reason)
        {
            if (controlState == next)
            {
                lastTransitionReason = reason;
                return;
            }

            FpgEnemyAiControlState previous = controlState;
            controlState = next;
            lastTransitionReason = reason;
            Transitioned?.Invoke(new FpgEnemyAiTransition(
                runtimeId,
                tick,
                previous,
                next,
                brainState,
                brainState,
                reason));
        }

        private void TransitionBrain(
            FpgEnemyAiBrainState next,
            TickIndex tick,
            FpgEnemyAiTransitionReason reason)
        {
            if (brainState == next)
            {
                lastTransitionReason = reason;
                return;
            }

            FpgEnemyAiBrainState previous = brainState;
            brainState = next;
            stateEnterTick = tick;
            lastTransitionReason = reason;
            Transitioned?.Invoke(new FpgEnemyAiTransition(
                runtimeId,
                tick,
                controlState,
                controlState,
                previous,
                next,
                reason));
        }

        private static bool TryAddTicks(
            TickIndex tick,
            int offset,
            out TickIndex result)
        {
            result = TickIndex.Invalid;
            if (!tick.IsValid || offset < 0
                || tick.Value > long.MaxValue - offset)
            {
                return false;
            }

            result = new TickIndex(tick.Value + offset);
            return true;
        }
    }
}
