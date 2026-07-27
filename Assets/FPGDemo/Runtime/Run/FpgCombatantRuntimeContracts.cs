using System;
using FPG.Demo.Core;
using FPG.Demo.Skills;

namespace FPG.Demo.Run
{
    public enum FpgBattleTickPhase
    {
        LifecycleBoundary = 0,
        EnemyRecovery,
        PlayerAttackAndHit,
        DeathAndThreatCleanup,
        EnemyAttackDirector,
        ThreatAndProjectileAdvance,
        ImpactResolution,
        EncounterCompletion
    }

    /// <summary>
    /// Unity-side binding synchronizes one explicit phase at a time. A failed
    /// binding is terminal for the formal session; callers must not continue
    /// the tick after a rejected result.
    /// </summary>
    public interface IFpgBattleTickSynchronizer
    {
        DomainResult Synchronize(FpgBattleTickPhase phase, TickIndex tick);
    }

    public sealed class NullFpgBattleTickSynchronizer : IFpgBattleTickSynchronizer
    {
        public static readonly NullFpgBattleTickSynchronizer Instance = new NullFpgBattleTickSynchronizer();

        private NullFpgBattleTickSynchronizer()
        {
        }

        public DomainResult Synchronize(FpgBattleTickPhase phase, TickIndex tick)
        {
            return phase >= FpgBattleTickPhase.LifecycleBoundary && tick.IsValid
                ? DomainResult.Success
                : DomainResult.Rejected(RejectReason.InvalidState);
        }
    }

    public readonly struct FpgAttackScheduleRequest
    {
        public FpgAttackScheduleRequest(
            RuntimeId ownerRuntimeId,
            TickIndex readyTick,
            int priority,
            long scheduleSequence,
            string attackPatternId,
            SkillExecutionId skillExecutionId = default(SkillExecutionId),
            int gameplayEventId = 0)
        {
            if (!ownerRuntimeId.IsValid || !readyTick.IsValid || scheduleSequence < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerRuntimeId));
            }

            if (gameplayEventId < 0
                || skillExecutionId.IsValid != (gameplayEventId > 0))
            {
                throw new ArgumentException(
                    "Attack schedule skill correlation requires both a valid execution and gameplay event ID.",
                    nameof(gameplayEventId));
            }

            OwnerRuntimeId = ownerRuntimeId;
            ReadyTick = readyTick;
            Priority = priority;
            ScheduleSequence = scheduleSequence;
            AttackPatternId = attackPatternId ?? string.Empty;
            SkillExecutionId = skillExecutionId;
            GameplayEventId = gameplayEventId;
        }

        public RuntimeId OwnerRuntimeId { get; }
        public TickIndex ReadyTick { get; }
        public int Priority { get; }
        public long ScheduleSequence { get; }
        public string AttackPatternId { get; }
        public SkillExecutionId SkillExecutionId { get; }
        public int GameplayEventId { get; }
        public bool HasSkillCorrelation => SkillExecutionId.IsValid;
    }

    public interface IFpgAttackOwnerEligibility
    {
        bool CanAttack(RuntimeId ownerRuntimeId);
    }

    public interface IFpgAttackScheduleEligibility
    {
        bool CanProcessScheduledAttack(
            FpgAttackScheduleRequest request,
            int spawnSequence);
    }

    /// <summary>
    /// Fixed attack director queue. Ordering is the formal contract:
    /// (ReadyTick, SpawnSequence, Priority, ScheduleSequence).
    /// </summary>
    public sealed class FpgOwnerAwareAttackSchedule
    {
        private readonly FpgAttackScheduleRequest[] requests;
        private readonly int[] spawnSequences;
        private int count;

        public FpgOwnerAwareAttackSchedule(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            requests = new FpgAttackScheduleRequest[capacity];
            spawnSequences = new int[capacity];
        }

        public int Capacity => requests.Length;
        public int Count => count;

        public DomainResult TrySchedule(FpgAttackScheduleRequest request, int spawnSequence)
        {
            if (spawnSequence < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (count >= requests.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            requests[count] = request;
            spawnSequences[count] = spawnSequence;
            count++;
            return DomainResult.Success;
        }

        public bool TryPeekDue(TickIndex currentTick, out FpgAttackScheduleRequest request, out int spawnSequence)
        {
            int best = FindBestDue(currentTick);
            if (best < 0)
            {
                request = default(FpgAttackScheduleRequest);
                spawnSequence = -1;
                return false;
            }

            request = requests[best];
            spawnSequence = spawnSequences[best];
            return true;
        }

        public bool TryDequeueDue(TickIndex currentTick, out FpgAttackScheduleRequest request, out int spawnSequence)
        {
            int best = FindBestDue(currentTick);
            if (best < 0)
            {
                request = default(FpgAttackScheduleRequest);
                spawnSequence = -1;
                return false;
            }

            request = requests[best];
            spawnSequence = spawnSequences[best];
            RemoveAt(best);
            return true;
        }

        public bool TryDequeueDue(
            TickIndex currentTick,
            IFpgAttackOwnerEligibility eligibility,
            out FpgAttackScheduleRequest request,
            out int spawnSequence)
        {
            if (eligibility == null)
            {
                return TryDequeueDue(currentTick, out request, out spawnSequence);
            }

            int best = FindBestDue(currentTick, eligibility);
            if (best < 0)
            {
                request = default(FpgAttackScheduleRequest);
                spawnSequence = -1;
                return false;
            }

            request = requests[best];
            spawnSequence = spawnSequences[best];
            RemoveAt(best);
            return true;
        }

        public bool TryDequeueDueForSchedule(
            TickIndex currentTick,
            IFpgAttackScheduleEligibility eligibility,
            out FpgAttackScheduleRequest request,
            out int spawnSequence)
        {
            if (eligibility == null)
            {
                return TryDequeueDue(
                    currentTick,
                    out request,
                    out spawnSequence);
            }

            int best = FindBestDue(currentTick, eligibility);
            if (best < 0)
            {
                request = default(FpgAttackScheduleRequest);
                spawnSequence = -1;
                return false;
            }

            request = requests[best];
            spawnSequence = spawnSequences[best];
            RemoveAt(best);
            return true;
        }

        public int CancelOwner(RuntimeId ownerRuntimeId)
        {
            int canceled = 0;
            for (int index = count - 1; index >= 0; index--)
            {
                if (requests[index].OwnerRuntimeId != ownerRuntimeId)
                {
                    continue;
                }

                count--;
                requests[index] = requests[count];
                spawnSequences[index] = spawnSequences[count];
                requests[count] = default(FpgAttackScheduleRequest);
                canceled++;
            }

            return canceled;
        }

        public bool TryCancel(long scheduleSequence)
        {
            if (scheduleSequence < 0L)
            {
                return false;
            }

            for (int index = 0; index < count; index++)
            {
                if (requests[index].ScheduleSequence != scheduleSequence)
                {
                    continue;
                }

                RemoveAt(index);
                return true;
            }

            return false;
        }

        public void Clear()
        {
            Array.Clear(requests, 0, requests.Length);
            Array.Clear(spawnSequences, 0, spawnSequences.Length);
            count = 0;
        }

        private int FindBestDue(TickIndex currentTick)
        {
            if (!currentTick.IsValid)
            {
                return -1;
            }

            int best = -1;
            for (int index = 0; index < count; index++)
            {
                if (requests[index].ReadyTick > currentTick)
                {
                    continue;
                }

                if (best < 0 || Compare(index, best) < 0)
                {
                    best = index;
                }
            }

            return best;
        }

        private int FindBestDue(
            TickIndex currentTick,
            IFpgAttackOwnerEligibility eligibility)
        {
            if (!currentTick.IsValid)
            {
                return -1;
            }

            int best = -1;
            for (int index = 0; index < count; index++)
            {
                FpgAttackScheduleRequest candidate = requests[index];
                if (candidate.ReadyTick > currentTick
                    || !eligibility.CanAttack(candidate.OwnerRuntimeId))
                {
                    continue;
                }

                if (best < 0 || Compare(index, best) < 0)
                {
                    best = index;
                }
            }

            return best;
        }

        private int FindBestDue(
            TickIndex currentTick,
            IFpgAttackScheduleEligibility eligibility)
        {
            if (!currentTick.IsValid)
            {
                return -1;
            }

            int best = -1;
            for (int index = 0; index < count; index++)
            {
                FpgAttackScheduleRequest candidate = requests[index];
                if (candidate.ReadyTick > currentTick
                    || !eligibility.CanProcessScheduledAttack(
                        candidate,
                        spawnSequences[index]))
                {
                    continue;
                }

                if (best < 0 || Compare(index, best) < 0)
                {
                    best = index;
                }
            }

            return best;
        }

        private void RemoveAt(int index)
        {
            count--;
            if (index < count)
            {
                requests[index] = requests[count];
                spawnSequences[index] = spawnSequences[count];
            }

            requests[count] = default(FpgAttackScheduleRequest);
            spawnSequences[count] = 0;
        }

        private int Compare(int left, int right)
        {
            FpgAttackScheduleRequest a = requests[left];
            FpgAttackScheduleRequest b = requests[right];
            int comparison = a.ReadyTick.Value.CompareTo(b.ReadyTick.Value);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = spawnSequences[left].CompareTo(spawnSequences[right]);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = a.Priority.CompareTo(b.Priority);
            return comparison != 0
                ? comparison
                : a.ScheduleSequence.CompareTo(b.ScheduleSequence);
        }
    }

    public enum FpgThreatOwnerState
    {
        Scheduled = 0,
        Telegraph,
        Windup,
        Released,
        Recovery,
        Completed,
        Canceled
    }

    public readonly struct FpgOwnerAwareThreatSnapshot
    {
        public FpgOwnerAwareThreatSnapshot(
            RuntimeId threatRuntimeId,
            RuntimeId ownerRuntimeId,
            FpgThreatOwnerState state,
            TickIndex stateUntilTick,
            bool hasReleased)
        {
            ThreatRuntimeId = threatRuntimeId;
            OwnerRuntimeId = ownerRuntimeId;
            State = state;
            StateUntilTick = stateUntilTick;
            HasReleased = hasReleased;
        }

        public RuntimeId ThreatRuntimeId { get; }
        public RuntimeId OwnerRuntimeId { get; }
        public FpgThreatOwnerState State { get; }
        public TickIndex StateUntilTick { get; }
        public bool HasReleased { get; }
    }

    public sealed class FpgOwnerAwareThreatRegistry
    {
        private readonly FpgOwnerAwareThreatSnapshot[] threats;
        private int count;

        public FpgOwnerAwareThreatRegistry(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            threats = new FpgOwnerAwareThreatSnapshot[capacity];
        }

        public int Capacity => threats.Length;
        public int Count => count;

        public DomainResult TryRegister(FpgOwnerAwareThreatSnapshot threat)
        {
            if (!threat.ThreatRuntimeId.IsValid || !threat.OwnerRuntimeId.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (Find(threat.ThreatRuntimeId) >= 0)
            {
                return DomainResult.Rejected(RejectReason.DuplicateSequence);
            }

            if (count >= threats.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            threats[count++] = threat;
            return DomainResult.Success;
        }

        public int CancelOwner(RuntimeId ownerRuntimeId)
        {
            int canceled = 0;
            for (int index = 0; index < count; index++)
            {
                FpgOwnerAwareThreatSnapshot threat = threats[index];
                if (threat.OwnerRuntimeId != ownerRuntimeId || threat.State == FpgThreatOwnerState.Canceled
                    || threat.State == FpgThreatOwnerState.Completed)
                {
                    continue;
                }

                threats[index] = new FpgOwnerAwareThreatSnapshot(
                    threat.ThreatRuntimeId,
                    threat.OwnerRuntimeId,
                    FpgThreatOwnerState.Canceled,
                    threat.StateUntilTick,
                    threat.HasReleased);
                canceled++;
            }

            return canceled;
        }

        public int CopyOwner(RuntimeId ownerRuntimeId, FpgOwnerAwareThreatSnapshot[] destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            int copied = 0;
            for (int index = 0; index < count && copied < destination.Length; index++)
            {
                if (threats[index].OwnerRuntimeId == ownerRuntimeId)
                {
                    destination[copied++] = threats[index];
                }
            }

            return copied;
        }

        public void Clear()
        {
            Array.Clear(threats, 0, threats.Length);
            count = 0;
        }

        private int Find(RuntimeId runtimeId)
        {
            for (int index = 0; index < count; index++)
            {
                if (threats[index].ThreatRuntimeId == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }
    }

    public readonly struct FpgPoseSnapshot
    {
        public FpgPoseSnapshot(long positionKey, long rotationKey, TickIndex tick)
        {
            PositionKey = positionKey;
            RotationKey = rotationKey;
            Tick = tick;
        }

        public long PositionKey { get; }
        public long RotationKey { get; }
        public TickIndex Tick { get; }
    }

    public readonly struct FpgCombatantAnchor
    {
        public FpgCombatantAnchor(
            RuntimeId runtimeId,
            object gameplayAnchor,
            object projectileAnchor,
            object weakpointAnchor,
            object actorAnchor,
            FpgPoseSnapshot lastPose,
            TickIndex presentationLeaseUntilTick)
        {
            RuntimeId = runtimeId;
            GameplayAnchor = gameplayAnchor;
            ProjectileAnchor = projectileAnchor;
            WeakpointAnchor = weakpointAnchor;
            ActorAnchor = actorAnchor;
            LastPose = lastPose;
            PresentationLeaseUntilTick = presentationLeaseUntilTick;
        }

        public RuntimeId RuntimeId { get; }
        public object GameplayAnchor { get; }
        public object ProjectileAnchor { get; }
        public object WeakpointAnchor { get; }
        public object ActorAnchor { get; }
        public FpgPoseSnapshot LastPose { get; }
        public TickIndex PresentationLeaseUntilTick { get; }
    }

    /// <summary>
    /// RuntimeId-to-presentation anchor map. Anchors survive death until their
    /// presentation lease expires, preventing late FX from binding to a new
    /// entity that reused the same pooled view.
    /// </summary>
    public sealed class FpgCombatantAnchorMap
    {
        private readonly FpgCombatantAnchor[] anchors;
        private int count;

        public FpgCombatantAnchorMap(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            anchors = new FpgCombatantAnchor[capacity];
        }

        public int Capacity => anchors.Length;
        public int Count => count;

        public DomainResult TryBind(FpgCombatantAnchor anchor)
        {
            if (!anchor.RuntimeId.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            int existing = Find(anchor.RuntimeId);
            if (existing >= 0)
            {
                anchors[existing] = anchor;
                return DomainResult.Success;
            }

            if (count >= anchors.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            anchors[count++] = anchor;
            return DomainResult.Success;
        }

        public bool TryGet(RuntimeId runtimeId, out FpgCombatantAnchor anchor)
        {
            int index = Find(runtimeId);
            if (index < 0)
            {
                anchor = default(FpgCombatantAnchor);
                return false;
            }

            anchor = anchors[index];
            return true;
        }

        public DomainResult TryUpdatePose(RuntimeId runtimeId, FpgPoseSnapshot pose)
        {
            int index = Find(runtimeId);
            if (index < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            FpgCombatantAnchor current = anchors[index];
            anchors[index] = new FpgCombatantAnchor(
                current.RuntimeId,
                current.GameplayAnchor,
                current.ProjectileAnchor,
                current.WeakpointAnchor,
                current.ActorAnchor,
                pose,
                current.PresentationLeaseUntilTick);
            return DomainResult.Success;
        }

        public DomainResult TryUnbind(RuntimeId runtimeId, TickIndex leaseUntilTick)
        {
            int index = Find(runtimeId);
            if (index < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            FpgCombatantAnchor current = anchors[index];
            anchors[index] = new FpgCombatantAnchor(
                current.RuntimeId,
                null,
                null,
                null,
                current.ActorAnchor,
                current.LastPose,
                leaseUntilTick);
            return DomainResult.Success;
        }

        public void AdvanceLease(TickIndex currentTick)
        {
            for (int index = count - 1; index >= 0; index--)
            {
                FpgCombatantAnchor anchor = anchors[index];
                if (!anchor.PresentationLeaseUntilTick.IsValid
                    || currentTick < anchor.PresentationLeaseUntilTick)
                {
                    continue;
                }

                count--;
                anchors[index] = anchors[count];
                anchors[count] = default(FpgCombatantAnchor);
            }
        }

        public void Clear()
        {
            Array.Clear(anchors, 0, anchors.Length);
            count = 0;
        }

        private int Find(RuntimeId runtimeId)
        {
            if (!runtimeId.IsValid)
            {
                return -1;
            }

            for (int index = 0; index < count; index++)
            {
                if (anchors[index].RuntimeId == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}

