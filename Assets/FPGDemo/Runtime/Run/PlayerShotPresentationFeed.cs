using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;

namespace FPG.Demo.Run
{
    /// <summary>
    /// The terminal category of a frozen player-shot ray. This is a
    /// presentation-only classification; combat target selection remains owned
    /// by <see cref="TargetSelector"/>.
    /// </summary>
    public enum PlayerShotTerminalKind
    {
        Miss = 0,
        EnvironmentBlocker,
        Combatant,
        Projectile
    }

    /// <summary>
    /// One exact, frozen visual trajectory from the Unity physics query. The
    /// trajectory starts at the simulation AimPose origin, not the visual
    /// muzzle, so it can never be mistaken for a second combat ray.
    /// </summary>
    public readonly struct PlayerShotTrajectory
    {
        public PlayerShotTrajectory(
            int sampleIndex,
            SpatialVectorKey start,
            SpatialVectorKey terminalPoint,
            PlayerShotTerminalKind terminalKind,
            RuntimeId targetId,
            HitPart hitPart,
            GeometryId geometryId)
        {
            if (sampleIndex < -1
                || !Enum.IsDefined(typeof(PlayerShotTerminalKind), terminalKind)
                || !Enum.IsDefined(typeof(HitPart), hitPart))
            {
                throw new ArgumentOutOfRangeException(nameof(sampleIndex));
            }

            if (terminalKind == PlayerShotTerminalKind.Miss)
            {
                if (targetId.IsValid || geometryId.IsValid)
                {
                    throw new ArgumentException(
                        "Miss trajectories cannot carry a target or geometry.",
                        nameof(targetId));
                }
            }
            else if (!geometryId.IsValid)
            {
                throw new ArgumentException(
                    "Terminal trajectories require a geometry identifier.",
                    nameof(geometryId));
            }
            else if (terminalKind == PlayerShotTerminalKind.EnvironmentBlocker)
            {
                if (targetId.IsValid || hitPart != FPG.Demo.Combat.HitPart.Body)
                {
                    throw new ArgumentException(
                        "Environment blockers cannot carry a combat target.",
                        nameof(targetId));
                }
            }
            else if (!targetId.IsValid
                || terminalKind == PlayerShotTerminalKind.Projectile
                    && hitPart != FPG.Demo.Combat.HitPart.Projectile
                || terminalKind == PlayerShotTerminalKind.Combatant
                    && hitPart == FPG.Demo.Combat.HitPart.Projectile)
            {
                throw new ArgumentException(
                    "Trajectory terminal metadata is inconsistent.",
                    nameof(targetId));
            }

            SampleIndex = sampleIndex;
            Start = start;
            TerminalPoint = terminalPoint;
            TerminalKind = terminalKind;
            TargetId = targetId;
            HitPart = hitPart;
            GeometryId = geometryId;
        }

        public int SampleIndex { get; }
        public SpatialVectorKey Start { get; }
        public SpatialVectorKey TerminalPoint { get; }
        public PlayerShotTerminalKind TerminalKind { get; }
        public RuntimeId TargetId { get; }
        public HitPart HitPart { get; }
        public GeometryId GeometryId { get; }

        public bool IsValid
        {
            get
            {
                if (SampleIndex < -1
                    || !Enum.IsDefined(typeof(PlayerShotTerminalKind), TerminalKind)
                    || !Enum.IsDefined(typeof(HitPart), HitPart))
                {
                    return false;
                }

                if (TerminalKind == PlayerShotTerminalKind.Miss)
                {
                    return !TargetId.IsValid && !GeometryId.IsValid;
                }

                if (!GeometryId.IsValid)
                {
                    return false;
                }

                if (TerminalKind == PlayerShotTerminalKind.EnvironmentBlocker)
                {
                    return !TargetId.IsValid
                        && HitPart == FPG.Demo.Combat.HitPart.Body;
                }

                if (!TargetId.IsValid)
                {
                    return false;
                }

                return TerminalKind == PlayerShotTerminalKind.Projectile
                    ? HitPart == FPG.Demo.Combat.HitPart.Projectile
                    : HitPart != FPG.Demo.Combat.HitPart.Projectile;
            }
        }
    }

    /// <summary>
    /// A bounded, engine-neutral copy of a successful Unity attack query. The
    /// Unity adapter captures it before query buffers are reused; the bridge
    /// publishes it only after BattleSession commits that attack.
    /// </summary>
    public struct PlayerShotQueryCapture
    {
        private PlayerShotTrajectory trajectory0;
        private PlayerShotTrajectory trajectory1;
        private PlayerShotTrajectory trajectory2;
        private PlayerShotTrajectory trajectory3;
        private PlayerShotTrajectory trajectory4;
        private PlayerShotTrajectory trajectory5;
        private PlayerShotTrajectory trajectory6;
        private PlayerShotTrajectory trajectory7;
        private byte initializedTrajectoryMask;

        public PlayerShotQueryCapture(
            in AttackQueryRequest request,
            int trajectoryCount,
            SpatialVectorKey secondaryAreaCenter,
            int secondaryAreaRadiusKey)
        {
            if (!request.TickInput.IsValid
                || !request.Attack.AttackId.IsValid
                || !request.Attack.ShotId.IsValid
                || trajectoryCount <= 0
                || trajectoryCount > AttackQueryRequest.MaxPelletCount
                || request.Attack.QueryPolicy != QueryPolicy.PelletRays
                    && request.Attack.QueryPolicy != QueryPolicy.DirectThenArea)
            {
                throw new ArgumentException(
                    "Player shot captures require a valid player attack query.",
                    nameof(request));
            }

            if (request.Attack.QueryPolicy == QueryPolicy.PelletRays
                && trajectoryCount != request.PelletCount)
            {
                throw new ArgumentException(
                    "Pellet captures must include every queried pellet.",
                    nameof(trajectoryCount));
            }

            if (request.Attack.QueryPolicy == QueryPolicy.DirectThenArea
                && (trajectoryCount != 1 || secondaryAreaRadiusKey <= 0))
            {
                throw new ArgumentException(
                    "Secondary captures require one direct path and an area radius.",
                    nameof(trajectoryCount));
            }

            AttackId = request.Attack.AttackId;
            ShotId = request.Attack.ShotId;
            ReleaseTick = request.Attack.ReleaseTick;
            DefinitionId = request.Attack.DefinitionId;
            QueryPolicy = request.Attack.QueryPolicy;
            AimPose = request.TickInput.AimPose;
            TrajectoryCount = trajectoryCount;
            SecondaryAreaCenter = secondaryAreaCenter;
            SecondaryAreaRadiusKey = secondaryAreaRadiusKey;
            trajectory0 = default(PlayerShotTrajectory);
            trajectory1 = default(PlayerShotTrajectory);
            trajectory2 = default(PlayerShotTrajectory);
            trajectory3 = default(PlayerShotTrajectory);
            trajectory4 = default(PlayerShotTrajectory);
            trajectory5 = default(PlayerShotTrajectory);
            trajectory6 = default(PlayerShotTrajectory);
            trajectory7 = default(PlayerShotTrajectory);
            initializedTrajectoryMask = 0;
        }

        public AttackId AttackId { get; }
        public ShotId ShotId { get; }
        public TickIndex ReleaseTick { get; }
        public int DefinitionId { get; }
        public QueryPolicy QueryPolicy { get; }
        public AimPoseSnapshot AimPose { get; }
        public int TrajectoryCount { get; }
        public SpatialVectorKey SecondaryAreaCenter { get; }
        public int SecondaryAreaRadiusKey { get; }

        public void SetTrajectory(int index, in PlayerShotTrajectory trajectory)
        {
            if (index < 0 || index >= TrajectoryCount || !trajectory.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            bool requiresPelletSample = QueryPolicy == QueryPolicy.PelletRays;
            if (requiresPelletSample
                ? trajectory.SampleIndex != index
                : index != 0 || trajectory.SampleIndex != -1)
            {
                throw new ArgumentException(
                    "Trajectory sample metadata does not match the query policy.",
                    nameof(trajectory));
            }

            switch (index)
            {
                case 0: trajectory0 = trajectory; break;
                case 1: trajectory1 = trajectory; break;
                case 2: trajectory2 = trajectory; break;
                case 3: trajectory3 = trajectory; break;
                case 4: trajectory4 = trajectory; break;
                case 5: trajectory5 = trajectory; break;
                case 6: trajectory6 = trajectory; break;
                default: trajectory7 = trajectory; break;
            }

            initializedTrajectoryMask |= (byte)(1 << index);
        }

        public PlayerShotTrajectory GetTrajectory(int index)
        {
            if (index < 0 || index >= TrajectoryCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            switch (index)
            {
                case 0: return trajectory0;
                case 1: return trajectory1;
                case 2: return trajectory2;
                case 3: return trajectory3;
                case 4: return trajectory4;
                case 5: return trajectory5;
                case 6: return trajectory6;
                default: return trajectory7;
            }
        }

        public bool IsComplete
        {
            get
            {
                byte requiredMask = (byte)((1 << TrajectoryCount) - 1);
                return initializedTrajectoryMask == requiredMask;
            }
        }

        public bool IsValidFor(WeaponReleaseKind kind)
        {
            if (!AttackId.IsValid || !ShotId.IsValid || !ReleaseTick.IsValid
                || DefinitionId <= 0 || !AimPose.IsValid || !IsComplete
                || !Enum.IsDefined(typeof(WeaponReleaseKind), kind))
            {
                return false;
            }

            if (kind == WeaponReleaseKind.Primary)
            {
                if (QueryPolicy != QueryPolicy.PelletRays)
                {
                    return false;
                }
            }
            else if (kind == WeaponReleaseKind.Secondary)
            {
                if (QueryPolicy != QueryPolicy.DirectThenArea
                    || SecondaryAreaRadiusKey <= 0)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            for (int index = 0; index < TrajectoryCount; index++)
            {
                if (!GetTrajectory(index).IsValid)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public readonly struct PlayerShotPresentationSnapshot
    {
        private readonly PlayerShotTrajectory trajectory0;
        private readonly PlayerShotTrajectory trajectory1;
        private readonly PlayerShotTrajectory trajectory2;
        private readonly PlayerShotTrajectory trajectory3;
        private readonly PlayerShotTrajectory trajectory4;
        private readonly PlayerShotTrajectory trajectory5;
        private readonly PlayerShotTrajectory trajectory6;
        private readonly PlayerShotTrajectory trajectory7;

        internal PlayerShotPresentationSnapshot(
            long sequence,
            in PlayerShotQueryCapture capture,
            WeaponReleaseKind releaseKind)
        {
            if (sequence <= 0 || !capture.IsValidFor(releaseKind))
            {
                throw new ArgumentException(
                    "Player shot snapshots require a committed, complete capture.",
                    nameof(capture));
            }

            Sequence = sequence;
            AttackId = capture.AttackId;
            ShotId = capture.ShotId;
            ReleaseTick = capture.ReleaseTick;
            DefinitionId = capture.DefinitionId;
            ReleaseKind = releaseKind;
            AimPose = capture.AimPose;
            TrajectoryCount = capture.TrajectoryCount;
            SecondaryAreaCenter = capture.SecondaryAreaCenter;
            SecondaryAreaRadiusKey = capture.SecondaryAreaRadiusKey;
            trajectory0 = capture.GetTrajectory(0);
            trajectory1 = capture.TrajectoryCount > 1 ? capture.GetTrajectory(1) : default(PlayerShotTrajectory);
            trajectory2 = capture.TrajectoryCount > 2 ? capture.GetTrajectory(2) : default(PlayerShotTrajectory);
            trajectory3 = capture.TrajectoryCount > 3 ? capture.GetTrajectory(3) : default(PlayerShotTrajectory);
            trajectory4 = capture.TrajectoryCount > 4 ? capture.GetTrajectory(4) : default(PlayerShotTrajectory);
            trajectory5 = capture.TrajectoryCount > 5 ? capture.GetTrajectory(5) : default(PlayerShotTrajectory);
            trajectory6 = capture.TrajectoryCount > 6 ? capture.GetTrajectory(6) : default(PlayerShotTrajectory);
            trajectory7 = capture.TrajectoryCount > 7 ? capture.GetTrajectory(7) : default(PlayerShotTrajectory);
        }

        public long Sequence { get; }
        public AttackId AttackId { get; }
        public ShotId ShotId { get; }
        public TickIndex ReleaseTick { get; }
        public int DefinitionId { get; }
        public WeaponReleaseKind ReleaseKind { get; }
        public AimPoseSnapshot AimPose { get; }
        public int TrajectoryCount { get; }
        public SpatialVectorKey SecondaryAreaCenter { get; }
        public int SecondaryAreaRadiusKey { get; }

        public PlayerShotTrajectory GetTrajectory(int index)
        {
            if (index < 0 || index >= TrajectoryCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            switch (index)
            {
                case 0: return trajectory0;
                case 1: return trajectory1;
                case 2: return trajectory2;
                case 3: return trajectory3;
                case 4: return trajectory4;
                case 5: return trajectory5;
                case 6: return trajectory6;
                default: return trajectory7;
            }
        }

        public bool IsValid => Sequence > 0 && AttackId.IsValid && ShotId.IsValid
            && ReleaseTick.IsValid && DefinitionId > 0 && AimPose.IsValid
            && TrajectoryCount > 0
            && Enum.IsDefined(typeof(WeaponReleaseKind), ReleaseKind)
            && ReleaseKind != WeaponReleaseKind.None;
    }

    public readonly struct PlayerShotPresentationEvent
    {
        public PlayerShotPresentationEvent(in PlayerShotPresentationSnapshot snapshot)
        {
            if (!snapshot.IsValid)
            {
                throw new ArgumentException(
                    "Player shot presentation events require a valid snapshot.",
                    nameof(snapshot));
            }

            Snapshot = snapshot;
        }

        public PlayerShotPresentationSnapshot Snapshot { get; }
        public long Sequence => Snapshot.Sequence;
    }

    public interface IPlayerShotPresentationFeed
    {
        int EventCapacity { get; }
        int DroppedEventCount { get; }
        long FirstRetainedSequence { get; }
        long LastSequence { get; }

        int CopyEventsAfter(
            long lastSeenSequence,
            PlayerShotPresentationEvent[] output,
            out bool hasGap);
    }

    public interface IPlayerShotPresentationFeedWriter : IPlayerShotPresentationFeed
    {
        bool TryRecordCommitted(
            in PlayerShotQueryCapture capture,
            WeaponReleaseKind releaseKind);
    }

    /// <summary>
    /// Unity's query adapter calls this only after its exact non-alloc Physics
    /// query returned successfully. Captures are not visible to presentation
    /// until their BattleSession transaction commits.
    /// </summary>
    public interface IPlayerShotQueryCaptureSink
    {
        bool TryCaptureSuccessfulQuery(in PlayerShotQueryCapture capture);
    }

    /// <summary>
    /// BattleSession invokes this non-authoritative notification only after a
    /// spatial player attack and its selected-hit stream have committed.
    /// Implementations must not throw into gameplay or mutate combat state.
    /// </summary>
    public interface ICommittedPlayerShotPresentationSink
    {
        void PublishCommittedShot(AttackId attackId, WeaponReleaseKind releaseKind);
    }

    /// <summary>
    /// Optional best-effort cleanup hook for a query that was captured but
    /// could not finish its BattleSession transaction. It deliberately carries
    /// no combat data and is never allowed to change the rejection outcome.
    /// </summary>
    public interface IUncommittedPlayerShotPresentationSink
    {
        void DiscardUncommittedShot(AttackId attackId);
    }

    public sealed class FixedPlayerShotPresentationFeed : IPlayerShotPresentationFeedWriter
    {
        public const int DefaultEventCapacity = 64;

        private readonly PlayerShotPresentationEvent[] events;
        private int eventStart;
        private int eventCount;
        private long nextSequence = 1L;

        public FixedPlayerShotPresentationFeed(int eventCapacity = DefaultEventCapacity)
        {
            if (eventCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(eventCapacity));
            }

            events = new PlayerShotPresentationEvent[eventCapacity];
        }

        public int EventCapacity => events.Length;
        public int DroppedEventCount { get; private set; }
        public int RejectedWriteCount { get; private set; }

        public long FirstRetainedSequence => eventCount == 0
            ? nextSequence
            : events[eventStart].Sequence;

        public long LastSequence => nextSequence - 1L;

        public bool TryRecordCommitted(
            in PlayerShotQueryCapture capture,
            WeaponReleaseKind releaseKind)
        {
            if (!capture.IsValidFor(releaseKind))
            {
                RejectedWriteCount++;
                return false;
            }

            PlayerShotPresentationSnapshot snapshot;
            try
            {
                snapshot = new PlayerShotPresentationSnapshot(
                    nextSequence,
                    capture,
                    releaseKind);
            }
            catch (Exception)
            {
                RejectedWriteCount++;
                return false;
            }

            Append(new PlayerShotPresentationEvent(snapshot));
            nextSequence++;
            return true;
        }

        public int CopyEventsAfter(
            long lastSeenSequence,
            PlayerShotPresentationEvent[] output,
            out bool hasGap)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (lastSeenSequence < 0L)
            {
                lastSeenSequence = 0L;
            }

            hasGap = lastSeenSequence < FirstRetainedSequence - 1L;
            int available = 0;
            for (int index = 0; index < eventCount; index++)
            {
                if (events[(eventStart + index) % events.Length].Sequence > lastSeenSequence)
                {
                    available++;
                }
            }

            if (output.Length < available)
            {
                throw new ArgumentException(
                    "Output does not have enough capacity for retained player shot events.",
                    nameof(output));
            }

            int written = 0;
            for (int index = 0; index < eventCount; index++)
            {
                PlayerShotPresentationEvent item = events[(eventStart + index) % events.Length];
                if (item.Sequence > lastSeenSequence)
                {
                    output[written++] = item;
                }
            }

            return written;
        }

        private void Append(in PlayerShotPresentationEvent item)
        {
            int writeIndex;
            if (eventCount < events.Length)
            {
                writeIndex = (eventStart + eventCount) % events.Length;
                eventCount++;
            }
            else
            {
                writeIndex = eventStart;
                eventStart = (eventStart + 1) % events.Length;
                DroppedEventCount++;
            }

            events[writeIndex] = item;
        }
    }

    /// <summary>
    /// The bridge owns bounded pending captures between the Unity query adapter
    /// and BattleSession's successful commit notification. It is deliberately
    /// best-effort: every fault is counted and discarded instead of affecting
    /// damage, ammo, simulation time, transcript or replay.
    /// </summary>
    public sealed class PlayerShotPresentationBridge :
        IPlayerShotQueryCaptureSink,
        ICommittedPlayerShotPresentationSink,
        IUncommittedPlayerShotPresentationSink
    {
        private const int DefaultPendingCapacity = 32;

        private readonly IPlayerShotPresentationFeedWriter feed;
        private readonly PlayerShotQueryCapture[] pendingCaptures;
        private readonly bool[] pendingSlots;

        public PlayerShotPresentationBridge(
            IPlayerShotPresentationFeedWriter feed,
            int pendingCapacity = DefaultPendingCapacity)
        {
            this.feed = feed ?? throw new ArgumentNullException(nameof(feed));
            if (pendingCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pendingCapacity));
            }

            pendingCaptures = new PlayerShotQueryCapture[pendingCapacity];
            pendingSlots = new bool[pendingCapacity];
        }

        public IPlayerShotPresentationFeed Feed => feed;
        public int PendingCapacity => pendingCaptures.Length;
        public int PendingCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < pendingSlots.Length; index++)
                {
                    if (pendingSlots[index])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int ObservationFaultCount { get; private set; }
        public int RejectedCaptureCount { get; private set; }
        public int RejectedPublicationCount { get; private set; }

        public bool TryCaptureSuccessfulQuery(in PlayerShotQueryCapture capture)
        {
            try
            {
                if (!capture.AttackId.IsValid || !capture.ShotId.IsValid
                    || !capture.ReleaseTick.IsValid || !capture.AimPose.IsValid
                    || !capture.IsComplete || FindPendingSlot(capture.AttackId) >= 0)
                {
                    RejectedCaptureCount++;
                    return false;
                }

                int slot = FindFreeSlot();
                if (slot < 0)
                {
                    RejectedCaptureCount++;
                    return false;
                }

                pendingCaptures[slot] = capture;
                pendingSlots[slot] = true;
                return true;
            }
            catch (Exception)
            {
                ObservationFaultCount++;
                return false;
            }
        }

        public void PublishCommittedShot(AttackId attackId, WeaponReleaseKind releaseKind)
        {
            int slot = FindPendingSlot(attackId);
            if (slot < 0)
            {
                RejectedPublicationCount++;
                return;
            }

            PlayerShotQueryCapture capture = pendingCaptures[slot];
            pendingCaptures[slot] = default(PlayerShotQueryCapture);
            pendingSlots[slot] = false;
            try
            {
                if (!feed.TryRecordCommitted(capture, releaseKind))
                {
                    RejectedPublicationCount++;
                }
            }
            catch (Exception)
            {
                ObservationFaultCount++;
            }
        }

        public void DiscardUncommittedShot(AttackId attackId)
        {
            int slot = FindPendingSlot(attackId);
            if (slot < 0)
            {
                return;
            }

            pendingCaptures[slot] = default(PlayerShotQueryCapture);
            pendingSlots[slot] = false;
        }

        public void ClearPending()
        {
            for (int index = 0; index < pendingSlots.Length; index++)
            {
                pendingCaptures[index] = default(PlayerShotQueryCapture);
                pendingSlots[index] = false;
            }
        }

        private int FindPendingSlot(AttackId attackId)
        {
            if (!attackId.IsValid)
            {
                return -1;
            }

            for (int index = 0; index < pendingSlots.Length; index++)
            {
                if (pendingSlots[index]
                    && pendingCaptures[index].AttackId == attackId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeSlot()
        {
            for (int index = 0; index < pendingSlots.Length; index++)
            {
                if (!pendingSlots[index])
                {
                    return index;
                }
            }

            return -1;
        }
    }

    internal sealed class NullCommittedPlayerShotPresentationSink :
        ICommittedPlayerShotPresentationSink
    {
        public static readonly NullCommittedPlayerShotPresentationSink Instance =
            new NullCommittedPlayerShotPresentationSink();

        private NullCommittedPlayerShotPresentationSink()
        {
        }

        public void PublishCommittedShot(
            AttackId attackId,
            WeaponReleaseKind releaseKind)
        {
        }
    }
}
