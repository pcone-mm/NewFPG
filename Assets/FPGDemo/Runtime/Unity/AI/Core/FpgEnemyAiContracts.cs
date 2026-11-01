using System;
using FPG.Demo.Core;
using FPG.Demo.Skills;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Orthogonal control state owned by <see cref="FpgEnemyAiRuntime"/>.
    /// Control state gates movement and new skill requests while the brain
    /// state continues to describe the normal behaviour flow.
    /// </summary>
    public enum FpgEnemyAiControlState
    {
        Active = 0,
        HitReact,
        ControlLocked,
        Dead
    }

    /// <summary>
    /// Behaviour state owned by the formal enemy AI. Locomotion is represented
    /// by three explicit states so a failed position query is observable.
    /// </summary>
    public enum FpgEnemyAiBrainState
    {
        Entry = 0,
        LocomotionIdle,
        LocomotionReposition,
        LocomotionWaitingForValidPosition,
        AttackDecision,
        SkillExecuting,
        Recovery,
        Fault
    }

    public enum FpgEnemyAiTransitionReason
    {
        None = 0,
        EntryCompleted,
        WaitingForCooldown,
        OutsideAttackRange,
        NoLineOfSight,
        MovementLocked,
        HitReact,
        ControlLocked,
        SkillRunning,
        NoValidPosition,
        PositionSatisfied,
        SkillCompleted,
        SkillInterrupted,
        SkillFaulted,
        NoAvailableSkill,
        NoTarget,
        Dead,
        Reset,
        InvalidTick,
        InvalidDefinition,
        MovementCompleted,
        MovementFaulted
    }

    public enum FpgEnemyAiMovementState
    {
        Idle = 0,
        Moving,
        NoValidPosition,
        Faulted
    }

    public enum FpgEnemyAiSkillExecutorState
    {
        Idle = 0,
        Running,
        Completed,
        Interrupted,
        Faulted
    }

    public enum FpgEnemyAiCurrentSkillPolicy
    {
        Continue = 0,
        Freeze,
        Interrupt
    }

    /// <summary>
    /// A control lock is deliberately policy based. This lets future freeze,
    /// stun and bind effects share one contract without adding branches to the
    /// AI runtime for every effect type.
    /// </summary>
    public readonly struct FpgEnemyAiControlLock
    {
        public FpgEnemyAiControlLock(
            bool allowMovement,
            bool allowNewSkill,
            FpgEnemyAiCurrentSkillPolicy currentSkillPolicy,
            int durationTicks)
        {
            if (durationTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(durationTicks));
            }

            AllowMovement = allowMovement;
            AllowNewSkill = allowNewSkill;
            CurrentSkillPolicy = currentSkillPolicy;
            DurationTicks = durationTicks;
        }

        public bool AllowMovement { get; }
        public bool AllowNewSkill { get; }
        public FpgEnemyAiCurrentSkillPolicy CurrentSkillPolicy { get; }
        public int DurationTicks { get; }
    }

    /// <summary>
    /// World facts sampled by the integration layer for one fixed tick. The
    /// runtime never queries Unity objects itself, which keeps the state
    /// machine deterministic and straightforward to test.
    /// </summary>
    public readonly struct FpgEnemyAiObservation
    {
        public FpgEnemyAiObservation(
            bool isAlive,
            bool hasTarget,
            float targetDistance,
            bool hasLineOfSight,
            bool positionSatisfiesAttack,
            bool shouldReposition,
            bool canMove = true,
            bool canStartSkill = true)
        {
            if (float.IsNaN(targetDistance) || float.IsInfinity(targetDistance)
                || targetDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(targetDistance));
            }

            IsAlive = isAlive;
            HasTarget = hasTarget;
            TargetDistance = targetDistance;
            HasLineOfSight = hasLineOfSight;
            PositionSatisfiesAttack = positionSatisfiesAttack;
            ShouldReposition = shouldReposition;
            CanMove = canMove;
            CanStartSkill = canStartSkill;
        }

        public bool IsAlive { get; }
        public bool HasTarget { get; }
        public float TargetDistance { get; }
        public bool HasLineOfSight { get; }
        public bool PositionSatisfiesAttack { get; }
        public bool ShouldReposition { get; }
        public bool CanMove { get; }
        public bool CanStartSkill { get; }
    }

    /// <summary>
    /// Optional world adapter for hosts that prefer the AI to pull its
    /// per-tick facts from a formal port. Implementations should only read
    /// already-sampled gameplay state and must not mutate the AI runtime.
    /// </summary>
    public interface IFpgEnemyAiContext
    {
        bool TrySample(
            TickIndex tick,
            RuntimeId runtimeId,
            FpgEnemyDefinition definition,
            out FpgEnemyAiObservation observation);
    }

    public readonly struct FpgEnemyAiMoveRequest
    {
        public FpgEnemyAiMoveRequest(
            RuntimeId runtimeId,
            TickIndex tick,
            FpgEnemyAiTransitionReason reason,
            bool forceReposition,
            float moveSpeed = 0f)
        {
            if (!runtimeId.IsValid || !tick.IsValid)
            {
                throw new ArgumentException("Enemy move request is invalid.");
            }

            if (float.IsNaN(moveSpeed) || float.IsInfinity(moveSpeed)
                || moveSpeed < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(moveSpeed));
            }

            RuntimeId = runtimeId;
            Tick = tick;
            Reason = reason;
            ForceReposition = forceReposition;
            MoveSpeed = moveSpeed;
        }

        public RuntimeId RuntimeId { get; }
        public TickIndex Tick { get; }
        public FpgEnemyAiTransitionReason Reason { get; }
        public bool ForceReposition { get; }
        public float MoveSpeed { get; }
    }

    /// <summary>
    /// Runtime movement target projected into the core assembly without
    /// coupling the deterministic AI contracts to UnityEngine.Vector3.
    /// </summary>
    public readonly struct FpgEnemyAiMovementGoal
    {
        internal FpgEnemyAiMovementGoal(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
            IsValid = true;
        }

        public static FpgEnemyAiMovementGoal Invalid =>
            default(FpgEnemyAiMovementGoal);

        public bool IsValid { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
    }

    public readonly struct FpgEnemyAiSkillRequest
    {
        public FpgEnemyAiSkillRequest(
            RuntimeId runtimeId,
            TickIndex tick,
            int patternOrdinal,
            FpgEnemyAttackDefinition definition)
        {
            if (!runtimeId.IsValid || !tick.IsValid || patternOrdinal < 0
                || definition == null)
            {
                throw new ArgumentException("Enemy skill request is invalid.");
            }

            RuntimeId = runtimeId;
            Tick = tick;
            PatternOrdinal = patternOrdinal;
            Definition = definition;
        }

        public RuntimeId RuntimeId { get; }
        public TickIndex Tick { get; }
        public int PatternOrdinal { get; }
        public FpgEnemyAttackDefinition Definition { get; }
        public string SkillId => Definition.SkillId;
    }

    public readonly struct FpgEnemyAiTransition
    {
        internal FpgEnemyAiTransition(
            RuntimeId runtimeId,
            TickIndex tick,
            FpgEnemyAiControlState previousControl,
            FpgEnemyAiControlState nextControl,
            FpgEnemyAiBrainState previousBrain,
            FpgEnemyAiBrainState nextBrain,
            FpgEnemyAiTransitionReason reason)
        {
            RuntimeId = runtimeId;
            Tick = tick;
            PreviousControl = previousControl;
            NextControl = nextControl;
            PreviousBrain = previousBrain;
            NextBrain = nextBrain;
            Reason = reason;
        }

        public RuntimeId RuntimeId { get; }
        public TickIndex Tick { get; }
        public FpgEnemyAiControlState PreviousControl { get; }
        public FpgEnemyAiControlState NextControl { get; }
        public FpgEnemyAiBrainState PreviousBrain { get; }
        public FpgEnemyAiBrainState NextBrain { get; }
        public FpgEnemyAiTransitionReason Reason { get; }
    }

    /// <summary>
    /// Compact diagnostic snapshot intended for BattleTest and debug panels.
    /// </summary>
    public readonly struct FpgEnemyAiSnapshot
    {
        internal FpgEnemyAiSnapshot(
            RuntimeId runtimeId,
            FpgEnemyAiControlState controlState,
            FpgEnemyAiBrainState brainState,
            string currentSkillId,
            SkillExecutionId skillExecutionId,
            TickIndex stateEnterTick,
            TickIndex nextDecisionTick,
            bool hasMovementGoal,
            FpgEnemyAiMovementGoal movementGoal,
            FpgEnemyAiMovementState movementState,
            TickIndex controlEndTick,
            FpgEnemyAiTransitionReason lastTransitionReason)
        {
            RuntimeId = runtimeId;
            ControlState = controlState;
            BrainState = brainState;
            CurrentSkillId = currentSkillId ?? string.Empty;
            SkillExecutionId = skillExecutionId;
            StateEnterTick = stateEnterTick;
            NextDecisionTick = nextDecisionTick;
            HasMovementGoal = hasMovementGoal;
            MovementGoal = movementGoal;
            MovementState = movementState;
            ControlEndTick = controlEndTick;
            LastTransitionReason = lastTransitionReason;
        }

        public RuntimeId RuntimeId { get; }
        public FpgEnemyAiControlState ControlState { get; }
        public FpgEnemyAiBrainState BrainState { get; }
        public string CurrentSkillId { get; }
        public SkillExecutionId SkillExecutionId { get; }
        public TickIndex StateEnterTick { get; }
        public TickIndex NextDecisionTick { get; }
        public bool HasMovementGoal { get; }
        public FpgEnemyAiMovementGoal MovementGoal { get; }
        public FpgEnemyAiMovementState MovementState { get; }
        public TickIndex ControlEndTick { get; }
        public FpgEnemyAiTransitionReason LastTransitionReason { get; }
    }

    /// <summary>
    /// Movement executor boundary. It owns candidate sampling, navigation and
    /// transform updates; the AI only decides when a move should be requested.
    /// </summary>
    public interface IFpgEnemyAiMovementExecutor
    {
        FpgEnemyAiMovementState State { get; }

        bool TryGetMovementGoal(out FpgEnemyAiMovementGoal goal);

        bool TryBeginMove(in FpgEnemyAiMoveRequest request);

        void AdvanceMovement(TickIndex tick);

        void CancelMovement();
    }

    /// <summary>
    /// Skill executor boundary. Implementations may wrap the existing formal
    /// attack scheduler while keeping skill timeline work outside AI decisions.
    /// </summary>
    public interface IFpgEnemyAiSkillExecutor
    {
        FpgEnemyAiSkillExecutorState State { get; }

        SkillExecutionId ExecutionId { get; }

        bool TryStartSkill(in FpgEnemyAiSkillRequest request);

        void AdvanceSkill(TickIndex tick);

        void InterruptSkill(
            FpgEnemyAiCurrentSkillPolicy policy,
            TickIndex tick,
            TickIndex freezeUntilTick);
    }

}
