using System;
using FPG.Demo.Core;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Result of a movement executor tick. The executor owns planning and
    /// collision checks; a view only applies the returned pose.
    /// </summary>
    public enum FpgEnemyMovementExecutionState
    {
        Idle = 0,
        Moving = 1,
        NoValidPosition = 2,
        Blocked = 3
    }

    /// <summary>
    /// Context supplied by the formal encounter host. References are borrowed
    /// for the duration of the encounter and are never owned by an executor.
    /// </summary>
    public readonly struct FpgEnemyMovementContext
    {
        public FpgEnemyMovementContext(
            FpgRoomNavigationVolume navigationVolume,
            Transform target,
            Camera worldCamera,
            Vector3[] agentPositions,
            bool[] agentActive,
            int agentCount,
            int ownAgentIndex)
        {
            NavigationVolume = navigationVolume;
            Target = target;
            WorldCamera = worldCamera;
            AgentPositions = agentPositions;
            AgentActive = agentActive;
            AgentCount = Mathf.Max(0, agentCount);
            OwnAgentIndex = ownAgentIndex;
        }

        public FpgRoomNavigationVolume NavigationVolume { get; }
        public Transform Target { get; }
        public Camera WorldCamera { get; }
        public Vector3[] AgentPositions { get; }
        public bool[] AgentActive { get; }
        public int AgentCount { get; }
        public int OwnAgentIndex { get; }
    }

    /// <summary>
    /// Decision supplied by AI. RequestReposition is deliberately separate
    /// from AllowMovement so control states can hold an existing goal without
    /// silently issuing a new query.
    /// </summary>
    public readonly struct FpgEnemyMovementRequest
    {
        public FpgEnemyMovementRequest(
            bool allowMovement,
            bool requestReposition,
            float moveSpeed,
            bool resetGoal = false,
            bool hasAvoidPosition = false,
            Vector3 avoidPosition = default(Vector3))
        {
            AllowMovement = allowMovement;
            RequestReposition = requestReposition;
            MoveSpeed = moveSpeed;
            ResetGoal = resetGoal;
            HasAvoidPosition = hasAvoidPosition;
            AvoidPosition = avoidPosition;
        }

        public bool AllowMovement { get; }
        public bool RequestReposition { get; }
        public float MoveSpeed { get; }
        public bool ResetGoal { get; }
        public bool HasAvoidPosition { get; }
        public Vector3 AvoidPosition { get; }

        public static FpgEnemyMovementRequest Hold =>
            new FpgEnemyMovementRequest(false, false, 0f);
    }

    /// <summary>
    /// Pure movement output. Position is the next safe gameplay-anchor pose;
    /// Delta is the displacement applied during this tick.
    /// </summary>
    public readonly struct FpgEnemyMovementSnapshot
    {
        public FpgEnemyMovementSnapshot(
            TickIndex tick,
            Vector3 position,
            Vector3 delta,
            FpgEnemyMovementExecutionState state,
            bool hasGoal,
            Vector3 facingReferenceRight,
            Vector3 goal = default(Vector3))
        {
            Tick = tick;
            Position = position;
            Delta = delta;
            State = state;
            HasGoal = hasGoal;
            FacingReferenceRight = facingReferenceRight;
            Goal = goal;
        }

        public TickIndex Tick { get; }
        public Vector3 Position { get; }
        public Vector3 Delta { get; }
        public FpgEnemyMovementExecutionState State { get; }
        public bool HasGoal { get; }
        public Vector3 Goal { get; }
        public Vector3 FacingReferenceRight { get; }
        public bool IsMoving => Delta.sqrMagnitude > 0.0000001f;
    }

    /// <summary>
    /// Reusable movement boundary for formal enemy AI. This class does not
    /// mutate a Transform and does not inspect attack state; those decisions
    /// belong to the AI runtime. Existing Sprite/Spine views can continue to
    /// use their legacy motion path while new AI code adopts this executor.
    /// </summary>
    public interface IFpgEnemyMovementExecutor
    {
        FpgEnemyMovementExecutionState State { get; }
        bool HasGoal { get; }
        Vector3 Goal { get; }

        void ConfigureContext(in FpgEnemyMovementContext context);
        bool CanStartAttack(Vector3 currentPosition);
        FpgEnemyMovementSnapshot Advance(
            TickIndex tick,
            Vector3 currentPosition,
            in FpgEnemyMovementRequest request);
        void ClearGoal();
        void Reset();
    }

    /// <summary>
    /// Presentation boundary for a movement snapshot. Implementations apply a
    /// final pose and facing only; they do not choose destinations or attacks.
    /// </summary>
    public interface IFpgEnemyMovementSnapshotReceiver
    {
        DomainResult ApplyMovementSnapshot(
            in FpgEnemyMovementSnapshot snapshot);
        DomainResult ApplyMovementAnimation(
            string animationName,
            float elapsedSeconds);
        void SetMovementFacing(
            Vector3 movement,
            Vector3 facingReferenceRight);
    }

    /// <summary>
    /// Stationary strategy for fixed-position behaviors and Patrol assets that
    /// do not yet author a patrol route. It still participates in the shared
    /// movement contract so views never decide attack eligibility.
    /// </summary>
    public sealed class FpgEnemyStationaryMovementExecutor :
        IFpgEnemyMovementExecutor
    {
        public FpgEnemyMovementExecutionState State { get; private set; } =
            FpgEnemyMovementExecutionState.Idle;

        public bool HasGoal => false;
        public Vector3 Goal => Vector3.zero;

        public bool TryGetMovementGoal(
            out FpgEnemyAiMovementGoal goal)
        {
            goal = FpgEnemyAiMovementGoal.Invalid;
            return false;
        }

        public void ConfigureContext(in FpgEnemyMovementContext context)
        {
        }

        public bool CanStartAttack(Vector3 currentPosition)
        {
            return true;
        }

        public FpgEnemyMovementSnapshot Advance(
            TickIndex tick,
            Vector3 currentPosition,
            in FpgEnemyMovementRequest request)
        {
            State = tick.IsValid
                ? FpgEnemyMovementExecutionState.Idle
                : FpgEnemyMovementExecutionState.Blocked;
            Vector3 referenceRight = Vector3.right;
            return new FpgEnemyMovementSnapshot(
                tick,
                currentPosition,
                Vector3.zero,
                State,
                false,
                referenceRight);
        }

        public void ClearGoal()
        {
        }

        public void Reset()
        {
            State = FpgEnemyMovementExecutionState.Idle;
        }
    }

    /// <summary>
    /// Position-query implementation shared by Sprite and Spine AI runtimes.
    /// It preserves the existing deterministic candidate scoring and navigation
    /// checks while leaving pose application to a view or motion bridge.
    /// </summary>
    public sealed class FpgEnemyPositionQueryMovementExecutor :
        IFpgEnemyMovementExecutor
    {
        private readonly FpgEnemyPositionQueryDefinition definition;
        private readonly FpgEnemyPositionQueryRuntime queryRuntime;
        private FpgEnemyMovementContext context;
        private bool contextConfigured;
        private bool hasGoal;
        private Vector3 goal;
        private bool hasAvoidPosition;
        private Vector3 avoidPosition;
        private TickIndex lastAdvanceTick = TickIndex.Invalid;

        public FpgEnemyPositionQueryMovementExecutor(
            FpgEnemyPositionQueryDefinition definition)
        {
            this.definition = definition;
            queryRuntime = definition == null
                ? null
                : new FpgEnemyPositionQueryRuntime(definition);
        }

        public FpgEnemyMovementExecutionState State { get; private set; } =
            FpgEnemyMovementExecutionState.Idle;

        public bool HasGoal => hasGoal;
        public Vector3 Goal => goal;

        public bool TryGetMovementGoal(
            out FpgEnemyAiMovementGoal movementGoal)
        {
            if (!hasGoal)
            {
                movementGoal = FpgEnemyAiMovementGoal.Invalid;
                return false;
            }

            movementGoal = new FpgEnemyAiMovementGoal(
                goal.x,
                goal.y,
                goal.z);
            return true;
        }

        public void ConfigureContext(in FpgEnemyMovementContext nextContext)
        {
            context = nextContext;
            contextConfigured = true;
        }

        public bool CanStartAttack(Vector3 currentPosition)
        {
            if (!contextConfigured
                || definition == null
                || context.NavigationVolume == null
                || context.Target == null)
            {
                return false;
            }

            Vector3 origin = currentPosition;
            Vector3 target = context.Target.position;
            if (definition.SampleAroundCurrentPosition)
            {
                origin.y = 0f;
                target.y = 0f;
            }

            return definition.IsAttackDistanceValid(
                Vector3.Distance(origin, target));
        }

        public FpgEnemyMovementSnapshot Advance(
            TickIndex tick,
            Vector3 currentPosition,
            in FpgEnemyMovementRequest request)
        {
            Vector3 referenceRight = context.WorldCamera != null
                ? context.WorldCamera.transform.right
                : Vector3.right;
            if (!tick.IsValid)
            {
                State = FpgEnemyMovementExecutionState.Blocked;
                return new FpgEnemyMovementSnapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    State,
                    hasGoal,
                    referenceRight,
                    hasGoal ? goal : Vector3.zero);
            }

            if (lastAdvanceTick.IsValid && tick.Value < lastAdvanceTick.Value)
            {
                State = FpgEnemyMovementExecutionState.Blocked;
                return new FpgEnemyMovementSnapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    State,
                    hasGoal,
                    referenceRight,
                    hasGoal ? goal : Vector3.zero);
            }

            if (lastAdvanceTick == tick)
            {
                return new FpgEnemyMovementSnapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    State,
                    hasGoal,
                    referenceRight,
                    hasGoal ? goal : Vector3.zero);
            }

            lastAdvanceTick = tick;
            if (request.ResetGoal)
            {
                ClearGoal();
            }

            if (request.HasAvoidPosition)
            {
                hasAvoidPosition = true;
                avoidPosition = request.AvoidPosition;
            }

            if (!request.AllowMovement || request.MoveSpeed <= 0f)
            {
                State = FpgEnemyMovementExecutionState.Idle;
                return new FpgEnemyMovementSnapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    State,
                    hasGoal,
                    referenceRight,
                    hasGoal ? goal : Vector3.zero);
            }

            if (!contextConfigured
                || definition == null
                || queryRuntime == null
                || context.NavigationVolume == null
                || context.Target == null)
            {
                State = FpgEnemyMovementExecutionState.NoValidPosition;
                return new FpgEnemyMovementSnapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    State,
                    hasGoal,
                    referenceRight,
                    hasGoal ? goal : Vector3.zero);
            }

            if (hasGoal
                && Vector3.Distance(currentPosition, goal)
                    <= 0.12f)
            {
                ClearGoal();
                State = FpgEnemyMovementExecutionState.Idle;
                return new FpgEnemyMovementSnapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    State,
                    false,
                    referenceRight,
                    Vector3.zero);
            }

            if (request.RequestReposition && !hasGoal)
            {
                hasGoal = queryRuntime.TryFindBest(
                    context.NavigationVolume,
                    currentPosition,
                    context.Target.position,
                    context.AgentPositions,
                    context.AgentActive,
                    context.AgentCount,
                    context.OwnAgentIndex,
                    hasAvoidPosition,
                    avoidPosition,
                    context.WorldCamera,
                    out Vector3 nextGoal);
                if (hasGoal)
                {
                    goal = nextGoal;
                }

            }

            if (!hasGoal)
            {
                State = request.RequestReposition
                    ? FpgEnemyMovementExecutionState.NoValidPosition
                    : FpgEnemyMovementExecutionState.Idle;
                return new FpgEnemyMovementSnapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    State,
                    false,
                    referenceRight,
                    Vector3.zero);
            }

            if (!context.NavigationVolume.TryGetSafeGroundStep(
                    currentPosition,
                    goal,
                    request.MoveSpeed / FpgSkillRuntimeConstants.TickRate,
                    definition.AgentRadius,
                    out Vector3 next))
            {
                hasGoal = false;
                State = FpgEnemyMovementExecutionState.Blocked;
                return new FpgEnemyMovementSnapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    State,
                    false,
                    referenceRight,
                    Vector3.zero);
            }

            if (!HasAgentClearance(currentPosition, next))
            {
                hasGoal = false;
                State = FpgEnemyMovementExecutionState.Blocked;
                return new FpgEnemyMovementSnapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    State,
                    false,
                    referenceRight,
                    Vector3.zero);
            }

            Vector3 delta = next - currentPosition;
            State = delta.sqrMagnitude > 0.0000001f
                ? FpgEnemyMovementExecutionState.Moving
                : FpgEnemyMovementExecutionState.Idle;
            return new FpgEnemyMovementSnapshot(
                tick,
                next,
                delta,
                State,
                hasGoal,
                    referenceRight,
                    goal);
        }

        public void ClearGoal()
        {
            hasGoal = false;
            goal = Vector3.zero;
            queryRuntime?.ResetCandidates();
        }

        public void Reset()
        {
            ClearGoal();
            context = default(FpgEnemyMovementContext);
            contextConfigured = false;
            hasAvoidPosition = false;
            avoidPosition = Vector3.zero;
            lastAdvanceTick = TickIndex.Invalid;
            State = FpgEnemyMovementExecutionState.Idle;
        }

        private bool HasAgentClearance(Vector3 current, Vector3 next)
        {
            Vector3[] positions = context.AgentPositions;
            if (positions == null)
            {
                return true;
            }

            int count = Mathf.Min(context.AgentCount, positions.Length);
            for (int index = 0; index < count; index++)
            {
                if (index == context.OwnAgentIndex
                    || (context.AgentActive != null
                        && index < context.AgentActive.Length
                        && !context.AgentActive[index]))
                {
                    continue;
                }

                Vector3 agent = positions[index];
                Vector3 currentOnAgentPlane = current;
                Vector3 nextOnAgentPlane = next;
                currentOnAgentPlane.y = agent.y;
                nextOnAgentPlane.y = agent.y;
                if (Vector3.Distance(nextOnAgentPlane, agent)
                        < definition.MinimumAgentSeparation
                    || Vector3.Distance(currentOnAgentPlane, agent)
                        < definition.MinimumAgentSeparation
                        && Vector3.Distance(nextOnAgentPlane, agent)
                            < Vector3.Distance(currentOnAgentPlane, agent))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Target-facing ground movement used by LateralChase behavior. The AI
    /// decides when to request movement; this executor only produces a safe
    /// fixed-tick step toward the configured stop distance.
    /// </summary>
    public sealed class FpgEnemyLateralChaseMovementExecutor :
        IFpgEnemyMovementExecutor
    {
        private readonly FpgEnemyBehaviorDefinition behavior;
        private FpgEnemyMovementContext context;
        private bool contextConfigured;
        private TickIndex lastAdvanceTick = TickIndex.Invalid;

        public FpgEnemyLateralChaseMovementExecutor(
            FpgEnemyBehaviorDefinition behavior)
        {
            this.behavior = behavior;
        }

        public FpgEnemyMovementExecutionState State { get; private set; } =
            FpgEnemyMovementExecutionState.Idle;

        public bool HasGoal { get; private set; }
        public Vector3 Goal { get; private set; }

        public bool TryGetMovementGoal(
            out FpgEnemyAiMovementGoal movementGoal)
        {
            if (!HasGoal)
            {
                movementGoal = FpgEnemyAiMovementGoal.Invalid;
                return false;
            }

            movementGoal = new FpgEnemyAiMovementGoal(
                Goal.x,
                Goal.y,
                Goal.z);
            return true;
        }

        public void ConfigureContext(in FpgEnemyMovementContext nextContext)
        {
            context = nextContext;
            contextConfigured = true;
        }

        public bool CanStartAttack(Vector3 currentPosition)
        {
            if (behavior == null || context.Target == null)
            {
                return false;
            }

            return PlanarDistance(currentPosition, context.Target.position)
                <= behavior.LateralAttackDistance;
        }

        public FpgEnemyMovementSnapshot Advance(
            TickIndex tick,
            Vector3 currentPosition,
            in FpgEnemyMovementRequest request)
        {
            Vector3 referenceRight = context.WorldCamera != null
                ? context.WorldCamera.transform.right
                : Vector3.right;
            if (!tick.IsValid
                || (lastAdvanceTick.IsValid
                    && tick.Value < lastAdvanceTick.Value))
            {
                State = FpgEnemyMovementExecutionState.Blocked;
                return Snapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    referenceRight);
            }

            if (lastAdvanceTick == tick)
            {
                return Snapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    referenceRight);
            }

            lastAdvanceTick = tick;
            if (request.ResetGoal)
            {
                ClearGoal();
            }

            if (!request.AllowMovement)
            {
                State = FpgEnemyMovementExecutionState.Idle;
                return Snapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    referenceRight);
            }

            if (!contextConfigured || behavior == null || context.Target == null)
            {
                State = FpgEnemyMovementExecutionState.NoValidPosition;
                return Snapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    referenceRight);
            }

            Vector3 target = context.Target.position;
            Vector3 planarDelta = target - currentPosition;
            planarDelta.y = 0f;
            float distance = planarDelta.magnitude;
            if (distance <= behavior.LateralStopDistance)
            {
                ClearGoal();
                State = FpgEnemyMovementExecutionState.Idle;
                return Snapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    referenceRight);
            }

            if (request.MoveSpeed <= 0f || distance <= 0.0001f)
            {
                State = FpgEnemyMovementExecutionState.NoValidPosition;
                return Snapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    referenceRight);
            }

            Vector3 direction = planarDelta / distance;
            Vector3 desired = target
                - direction * behavior.LateralStopDistance;
            desired.y = currentPosition.y;
            float stepDistance = request.MoveSpeed
                / FpgSkillRuntimeConstants.TickRate;
            Vector3 next = Vector3.MoveTowards(
                currentPosition,
                desired,
                stepDistance);
            next.y = currentPosition.y;

            FpgRoomNavigationVolume navigation = context.NavigationVolume;
            const float agentRadius = 0.3f;
            if (navigation != null
                && (!navigation.TryGetSafeGroundStep(
                        currentPosition,
                        desired,
                        stepDistance,
                        agentRadius,
                        out next)
                    || !navigation.Contains(
                        new Vector3(next.x, currentPosition.y, next.z),
                        agentRadius)))
            {
                ClearGoal();
                State = FpgEnemyMovementExecutionState.Blocked;
                return Snapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    referenceRight);
            }

            next.y = currentPosition.y;
            Vector3 delta = next - currentPosition;
            if (delta.sqrMagnitude <= 0.0000001f)
            {
                ClearGoal();
                State = FpgEnemyMovementExecutionState.Blocked;
                return Snapshot(
                    tick,
                    currentPosition,
                    Vector3.zero,
                    referenceRight);
            }

            HasGoal = true;
            Goal = desired;
            State = FpgEnemyMovementExecutionState.Moving;
            return Snapshot(tick, next, delta, referenceRight);
        }

        public void ClearGoal()
        {
            HasGoal = false;
            Goal = Vector3.zero;
        }

        public void Reset()
        {
            ClearGoal();
            context = default(FpgEnemyMovementContext);
            contextConfigured = false;
            lastAdvanceTick = TickIndex.Invalid;
            State = FpgEnemyMovementExecutionState.Idle;
        }

        private FpgEnemyMovementSnapshot Snapshot(
            TickIndex tick,
            Vector3 position,
            Vector3 delta,
            Vector3 referenceRight)
        {
            return new FpgEnemyMovementSnapshot(
                tick,
                position,
                delta,
                State,
                HasGoal,
                referenceRight,
                Goal);
        }

        private static float PlanarDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }
    }
}
