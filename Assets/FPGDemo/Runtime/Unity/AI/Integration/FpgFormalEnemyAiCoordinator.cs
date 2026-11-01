using System;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Skills;
using FPG.Demo.Run;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Composes the fixed-tick AI decision layer with the existing formal
    /// combat scheduler. The scheduler advances timelines; this coordinator
    /// owns when a registered enemy may request its next skill.
    /// </summary>
    public sealed class FpgFormalEnemyAiCoordinator
    {
        private readonly FpgFormalEnemyAttackScheduler scheduler;
        private readonly FpgMultiEnemyCombatPort combatPort;
        private readonly IFpgFormalEnemyMotionAuthority motionAuthority;
        private readonly FpgUnityEncounterEntityPort encounterEntityPort;
        private readonly AiSlot[] slots;
        private bool disposed;

        public FpgFormalEnemyAiCoordinator(
            FpgFormalEnemyAttackScheduler scheduler,
            FpgMultiEnemyCombatPort combatPort,
            IFpgFormalEnemyMotionAuthority motionAuthority,
            int capacity)
        {
            this.scheduler = scheduler
                ?? throw new ArgumentNullException(nameof(scheduler));
            this.combatPort = combatPort
                ?? throw new ArgumentNullException(nameof(combatPort));
            this.motionAuthority = motionAuthority;
            encounterEntityPort = motionAuthority as FpgUnityEncounterEntityPort;
            if (encounterEntityPort != null)
            {
                encounterEntityPort.CommittedEnemyHit +=
                    HandleCommittedEnemyHit;
            }
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            slots = new AiSlot[capacity];
            // Skill selection is now owned by FpgEnemyAiRuntime. Existing
            // scheduler timeline execution remains unchanged.
            scheduler.AutomaticSkillSelectionEnabled = false;
        }

        public int Capacity => slots.Length;
        public bool IsDisposed => disposed;
        public int ActiveRuntimeCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < slots.Length; index++)
                {
                    if (slots[index] != null
                        && slots[index].Runtime != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public event Action<FpgEnemyAiTransition> Transitioned;

        public DomainResult Advance(TickIndex tick)
        {
            if (disposed || !tick.IsValid)
            {
                return DomainResult.Rejected(
                    disposed ? RejectReason.Disposed : RejectReason.WrongTick);
            }

            DomainResult synced = SyncRegisteredEnemies();
            if (!synced.IsSuccess)
            {
                return synced;
            }

            for (int index = 0; index < slots.Length; index++)
            {
                AiSlot slot = slots[index];
                if (slot == null || slot.Runtime == null)
                {
                    continue;
                }

                DomainResult controlSync = SyncAuthoritativeControl(
                    slot,
                    tick);
                if (!controlSync.IsSuccess)
                {
                    return controlSync;
                }

                FpgEnemyAiObservation observation = SampleObservation(
                    slot.Runtime.RuntimeId,
                    slot.Runtime.Definition);
                DomainResult advanced = slot.Runtime.Advance(
                    tick,
                    in observation);
                if (!advanced.IsSuccess)
                {
                    return advanced;
                }

                if (encounterEntityPort != null
                    && slot.Runtime.ControlState
                        != FpgEnemyAiControlState.HitReact
                    && slot.Runtime.BrainState
                        != FpgEnemyAiBrainState.SkillExecuting
                    && (slot.Runtime.MovementState
                            == FpgEnemyAiMovementState.Moving
                        || slot.WasMoving))
                {
                    encounterEntityPort.ApplyAiMovementPresentation(
                        slot.Runtime.RuntimeId,
                        tick,
                        slot.Runtime.MovementState
                            == FpgEnemyAiMovementState.Moving);
                }

                slot.WasMoving = slot.Runtime.MovementState
                    == FpgEnemyAiMovementState.Moving;
            }

            return DomainResult.Success;
        }

        private DomainResult SyncAuthoritativeControl(
            AiSlot slot,
            TickIndex tick)
        {
            if (!combatPort.TryGetEnemyRuntime(
                    slot.Runtime.RuntimeId,
                    out EnemyRuntime combatRuntime)
                || combatRuntime == null
                || combatRuntime.Combatant.IsDead
                || combatRuntime.ControlState != EnemyControlState.Groggy)
            {
                slot.GroggyLockUntilTick = TickIndex.Invalid;
                return DomainResult.Success;
            }

            TickIndex untilTick = combatRuntime.Groggy.UntilTick;
            if (!untilTick.IsValid
                || slot.GroggyLockUntilTick == untilTick)
            {
                return DomainResult.Success;
            }

            long duration = untilTick.Value - tick.Value;
            if (duration < 0L)
            {
                duration = 0L;
            }

            int durationTicks = duration > int.MaxValue
                ? int.MaxValue
                : (int)duration;
            FpgEnemyAiControlLock controlLock = new FpgEnemyAiControlLock(
                allowMovement: false,
                allowNewSkill: false,
                currentSkillPolicy: FpgEnemyAiCurrentSkillPolicy.Interrupt,
                durationTicks: durationTicks);
            DomainResult applied = slot.Runtime.ApplyControlLock(
                tick,
                in controlLock);
            if (applied.IsSuccess)
            {
                slot.GroggyLockUntilTick = untilTick;
            }

            return applied;
        }

        public DomainResult NotifyCommittedEnemyHit(
            RuntimeId runtimeId,
            TickIndex tick,
            int hitReactDurationTicks = 44)
        {
            if (!runtimeId.IsValid || !tick.IsValid || hitReactDurationTicks < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            AiSlot slot = FindSlot(runtimeId);
            return slot == null
                ? DomainResult.Rejected(RejectReason.InvalidTarget)
                : slot.Runtime.NotifyHit(tick, hitReactDurationTicks);
        }

        public DomainResult ApplyControlLock(
            RuntimeId runtimeId,
            TickIndex tick,
            in FpgEnemyAiControlLock controlLock)
        {
            if (disposed || !runtimeId.IsValid || !tick.IsValid)
            {
                return DomainResult.Rejected(
                    disposed ? RejectReason.Disposed : RejectReason.InvalidDefinition);
            }

            AiSlot slot = FindSlot(runtimeId);
            return slot == null
                ? DomainResult.Rejected(RejectReason.InvalidTarget)
                : slot.Runtime.ApplyControlLock(tick, in controlLock);
        }

        public bool TryGetSnapshot(
            RuntimeId runtimeId,
            out FpgEnemyAiSnapshot snapshot)
        {
            AiSlot slot = FindSlot(runtimeId);
            if (slot == null)
            {
                snapshot = default(FpgEnemyAiSnapshot);
                return false;
            }

            snapshot = slot.Runtime.GetSnapshot();
            return true;
        }

        public bool TryGetSnapshot(
            int ordinal,
            out FpgEnemyAiSnapshot snapshot)
        {
            int seen = 0;
            for (int index = 0; index < slots.Length; index++)
            {
                AiSlot slot = slots[index];
                if (slot == null || slot.Runtime == null)
                {
                    continue;
                }

                if (seen++ != ordinal)
                {
                    continue;
                }

                snapshot = slot.Runtime.GetSnapshot();
                return true;
            }

            snapshot = default(FpgEnemyAiSnapshot);
            return false;
        }

        public void Clear()
        {
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index]?.Reset();
                slots[index] = null;
            }

            disposed = false;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            if (encounterEntityPort != null)
            {
                encounterEntityPort.CommittedEnemyHit -=
                    HandleCommittedEnemyHit;
            }

            Clear();
            disposed = true;
        }

        private void HandleCommittedEnemyHit(
            RuntimeId runtimeId,
            TickIndex tick)
        {
            if (disposed)
            {
                return;
            }

            if (FindSlot(runtimeId) == null)
            {
                DomainResult synced = SyncRegisteredEnemies();
                if (!synced.IsSuccess)
                {
                    return;
                }
            }

            // The combat port has already committed the damage and delivered
            // the optional view reaction. AI only receives the authoritative
            // hit notification so it can enter/reset HitReact while any
            // already-running skill remains under the executor's control.
            NotifyCommittedEnemyHit(runtimeId, tick);
        }

        private DomainResult SyncRegisteredEnemies()
        {
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index]?.MarkMissing();
            }

            for (int ordinal = 0;
                ordinal < scheduler.RegisteredEnemyCount;
                ordinal++)
            {
                if (!scheduler.TryGetRegisteredEnemy(
                        ordinal,
                        out RuntimeId runtimeId,
                        out int spawnSequence,
                        out TickIndex activationTick,
                        out FpgEnemyDefinition definition))
                {
                    return DomainResult.Rejected(RejectReason.InvariantFault);
                }

                AiSlot slot = FindSlot(runtimeId);
                if (slot == null)
                {
                    int free = FindFreeSlot();
                    if (free < 0)
                    {
                        return DomainResult.Rejected(RejectReason.BufferCapacity);
                    }

                    try
                    {
                        SkillAdapter skill = new SkillAdapter(
                            runtimeId,
                            scheduler);
                        slot = new AiSlot(
                            new FpgEnemyAiRuntime(
                                runtimeId,
                                spawnSequence,
                                activationTick,
                                definition,
                                new MotionAdapter(
                                    runtimeId,
                                    definition.Behavior != null
                                        && definition.Behavior.UsesExternalMovement,
                                    motionAuthority),
                                skill),
                            skill,
                            transition => Transitioned?.Invoke(transition));
                    }
                    catch (Exception)
                    {
                        return DomainResult.Rejected(RejectReason.InvalidDefinition);
                    }

                    slots[free] = slot;
                }

                slot.Present = true;
            }

            for (int index = 0; index < slots.Length; index++)
            {
                AiSlot slot = slots[index];
                if (slot == null || slot.Present)
                {
                    continue;
                }

                slot.Reset();
                slots[index] = null;
            }

            return DomainResult.Success;
        }

        private FpgEnemyAiObservation SampleObservation(
            RuntimeId runtimeId,
            FpgEnemyDefinition definition)
        {
            // GM AI-off is a pause switch, not an enemy death signal. Keep
            // the runtime alive while decisions are disabled so turning the
            // switch back on resumes the same state machine.
            bool aiEnabled = combatPort.IsEnemyAiEnabled;
            bool alive = combatPort.TryGetEnemyRuntime(
                    runtimeId,
                    out EnemyRuntime combatRuntime)
                && combatRuntime != null
                && !combatRuntime.Combatant.IsDead;
            bool groggy = combatRuntime != null
                && combatRuntime.ControlState == EnemyControlState.Groggy;
            bool hasTarget = combatPort.CanTargetPlayer;
            FpgUnityEncounterEntityPort entityPort = motionAuthority
                as FpgUnityEncounterEntityPort;
            bool positionSatisfied = entityPort != null
                ? entityPort.CanEnemyPositionStartAttack(runtimeId)
                : motionAuthority == null
                    || motionAuthority.CanStartAttack(runtimeId);
            bool hasLineOfSight = positionSatisfied;
            float targetDistance = 0f;
            if (entityPort != null)
            {
                entityPort.TryGetEnemyTargetDistance(
                    runtimeId,
                    out targetDistance);
                hasLineOfSight = entityPort.HasEnemyLineOfSight(runtimeId);
            }

            bool shouldReposition = !positionSatisfied;
            FpgEnemyBehaviorDefinition behavior = definition == null
                ? null
                : definition.Behavior;
            if (behavior != null && behavior.UsesLateralChaseMovement)
            {
                shouldReposition = targetDistance
                    > behavior.LateralStopDistance
                    || !hasLineOfSight;
            }
            else if (behavior != null
                && behavior.UsesPositionQueryMovement)
            {
                shouldReposition = !positionSatisfied || !hasLineOfSight;
            }

            return new FpgEnemyAiObservation(
                alive,
                hasTarget,
                targetDistance,
                hasLineOfSight,
                positionSatisfied,
                shouldReposition,
                canMove: aiEnabled && alive && !groggy,
                canStartSkill: aiEnabled
                    && alive
                    && !groggy
                    && positionSatisfied
                    && hasLineOfSight);
        }

        private AiSlot FindSlot(RuntimeId runtimeId)
        {
            for (int index = 0; index < slots.Length; index++)
            {
                AiSlot slot = slots[index];
                if (slot != null && slot.Runtime.RuntimeId == runtimeId)
                {
                    return slot;
                }
            }

            return null;
        }

        private int FindFreeSlot()
        {
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index] == null)
                {
                    return index;
                }
            }

            return -1;
        }

        private sealed class AiSlot
        {
            public AiSlot(
                FpgEnemyAiRuntime runtime,
                SkillAdapter skill,
                Action<FpgEnemyAiTransition> transitionSink)
            {
                Runtime = runtime;
                Skill = skill;
                Present = true;
                Runtime.Transitioned += transitionSink;
            }

            public readonly FpgEnemyAiRuntime Runtime;
            public readonly SkillAdapter Skill;
            public bool Present;
            public bool WasMoving;
            public TickIndex GroggyLockUntilTick = TickIndex.Invalid;

            public void MarkMissing()
            {
                Present = false;
            }

            public void Reset()
            {
                GroggyLockUntilTick = TickIndex.Invalid;
                Runtime.MarkDead(Runtime.LastTick.IsValid
                    ? Runtime.LastTick
                    : new TickIndex(0));
            }
        }

        private sealed class SkillAdapter : IFpgEnemyAiSkillExecutor
        {
            private readonly RuntimeId runtimeId;
            private readonly FpgFormalEnemyAttackScheduler scheduler;

            public SkillAdapter(
                RuntimeId runtimeId,
                FpgFormalEnemyAttackScheduler scheduler)
            {
                this.runtimeId = runtimeId;
                this.scheduler = scheduler;
            }

            public FpgEnemyAiSkillExecutorState State =>
                Map(scheduler.GetExecutionState(runtimeId));

            public SkillExecutionId ExecutionId =>
                scheduler.GetExecutionSnapshot(runtimeId).ExecutionId;

            public bool TryStartSkill(in FpgEnemyAiSkillRequest request)
            {
                return scheduler.TryStartSkill(
                    new FpgEnemySkillRequest(
                        request.RuntimeId,
                        request.PatternOrdinal,
                        request.Tick)).IsSuccess;
            }

            public void AdvanceSkill(TickIndex tick)
            {
                // FpgFormalEnemyAttackScheduler.Tick has already advanced all
                // active timelines in the EnemyAttackDirector phase. Calling
                // AdvanceSkill here would process a tick twice.
            }

            public void InterruptSkill(
                FpgEnemyAiCurrentSkillPolicy policy,
                TickIndex tick,
                TickIndex freezeUntilTick)
            {
                FpgFormalEnemySkillInterruptPolicy mapped =
                    policy == FpgEnemyAiCurrentSkillPolicy.Interrupt
                        ? FpgFormalEnemySkillInterruptPolicy.Interrupt
                        : policy == FpgEnemyAiCurrentSkillPolicy.Freeze
                            ? FpgFormalEnemySkillInterruptPolicy.Freeze
                            : FpgFormalEnemySkillInterruptPolicy.Continue;
                scheduler.InterruptSkill(
                    runtimeId,
                    mapped,
                    tick,
                    freezeUntilTick);
            }

            private static FpgEnemyAiSkillExecutorState Map(
                FpgFormalEnemySkillExecutionState state)
            {
                switch (state)
                {
                    case FpgFormalEnemySkillExecutionState.Running:
                        return FpgEnemyAiSkillExecutorState.Running;
                    case FpgFormalEnemySkillExecutionState.Completed:
                        return FpgEnemyAiSkillExecutorState.Completed;
                    case FpgFormalEnemySkillExecutionState.Interrupted:
                        return FpgEnemyAiSkillExecutorState.Interrupted;
                    case FpgFormalEnemySkillExecutionState.Faulted:
                        return FpgEnemyAiSkillExecutorState.Faulted;
                    default:
                        return FpgEnemyAiSkillExecutorState.Idle;
                }
            }
        }

        private sealed class MotionAdapter : IFpgEnemyAiMovementExecutor
        {
            private readonly RuntimeId runtimeId;
            private readonly bool usesExternalMovement;
            private readonly IFpgFormalEnemyMotionAuthority motionAuthority;
            private readonly FpgUnityEncounterEntityPort encounterEntityPort;
            private bool requested;
            private bool externalMovement;
            private bool resetGoalPending;
            private FpgEnemyAiMoveRequest lastRequest;

            public MotionAdapter(
                RuntimeId runtimeId,
                bool usesExternalMovement,
                IFpgFormalEnemyMotionAuthority motionAuthority)
            {
                this.runtimeId = runtimeId;
                this.usesExternalMovement = usesExternalMovement;
                this.motionAuthority = motionAuthority;
                encounterEntityPort = motionAuthority
                    as FpgUnityEncounterEntityPort;
            }

            public FpgEnemyAiMovementState State { get; private set; } =
                FpgEnemyAiMovementState.Idle;

            public bool TryBeginMove(in FpgEnemyAiMoveRequest request)
            {
                lastRequest = request;
                resetGoalPending = request.ForceReposition;
                if (usesExternalMovement)
                {
                    externalMovement = encounterEntityPort != null
                        && encounterEntityPort.TryEnableExternalMovement(runtimeId);
                    if (!externalMovement)
                    {
                        requested = false;
                        State = FpgEnemyAiMovementState.Faulted;
                        return false;
                    }
                }

                requested = true;
                State = FpgEnemyAiMovementState.Moving;
                return true;
            }

            public void AdvanceMovement(TickIndex tick)
            {
                if (!requested)
                {
                    State = FpgEnemyAiMovementState.Idle;
                    return;
                }

                if (externalMovement)
                {
                    FpgEnemyAiMoveRequest movementRequest = lastRequest;
                    if (resetGoalPending)
                    {
                        lastRequest = new FpgEnemyAiMoveRequest(
                            lastRequest.RuntimeId,
                            lastRequest.Tick,
                            lastRequest.Reason,
                            forceReposition: false,
                            moveSpeed: lastRequest.MoveSpeed);
                        resetGoalPending = false;
                    }

                    if (encounterEntityPort.TryAdvanceExternalMovement(
                            runtimeId,
                            tick,
                            in movementRequest,
                            out FpgEnemyAiMovementState externalState))
                    {
                        State = externalState;
                    }
                    else
                    {
                        State = FpgEnemyAiMovementState.Faulted;
                    }

                    return;
                }

                bool canAttack = motionAuthority == null
                    || motionAuthority.CanStartAttack(runtimeId);
                State = canAttack
                    ? FpgEnemyAiMovementState.Idle
                    : FpgEnemyAiMovementState.Moving;
            }

            public void CancelMovement()
            {
                if (externalMovement)
                {
                    encounterEntityPort.ClearExternalMovementGoal(runtimeId);
                }

                requested = false;
                resetGoalPending = false;
                State = FpgEnemyAiMovementState.Idle;
            }

            public bool TryGetMovementGoal(
                out FpgEnemyAiMovementGoal goal)
            {
                if (externalMovement
                    && encounterEntityPort != null
                    && encounterEntityPort.TryGetExternalMovementGoal(
                        runtimeId,
                        out goal))
                {
                    return true;
                }

                goal = FpgEnemyAiMovementGoal.Invalid;
                return false;
            }
        }
    }
}
