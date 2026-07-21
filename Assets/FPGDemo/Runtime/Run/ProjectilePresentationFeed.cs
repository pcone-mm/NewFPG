using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    public enum ProjectilePresentationEventType
    {
        Spawn = 0,
        Terminal
    }

    public readonly struct ProjectilePresentationState
    {
        public ProjectilePresentationState(
            ProjectileSpawnRequest request,
            ProjectilePathSnapshot path,
            SpatialVectorKey lastPoint)
        {
            if (!path.Matches(request))
            {
                throw new ArgumentException("Projectile presentation state requires a matching frozen path.", nameof(path));
            }

            Request = request;
            Path = path;
            LastPoint = lastPoint;
        }

        public ProjectileSpawnRequest Request { get; }
        public ProjectilePathSnapshot Path { get; }
        public SpatialVectorKey LastPoint { get; }

        public ProjectilePresentationState WithLastPoint(SpatialVectorKey point)
        {
            return new ProjectilePresentationState(Request, Path, point);
        }
    }

    public readonly struct ProjectilePresentationEvent
    {
        private ProjectilePresentationEvent(
            long sequence,
            ProjectilePresentationEventType type,
            TickIndex tick,
            ProjectilePresentationState state,
            ProjectileTerminalReason terminalReason)
        {
            if (sequence <= 0 || !tick.IsValid
                || !Enum.IsDefined(typeof(ProjectilePresentationEventType), type))
            {
                throw new ArgumentException("Projectile presentation events require a valid sequence, tick and type.");
            }

            if (type == ProjectilePresentationEventType.Spawn
                && terminalReason != ProjectileTerminalReason.None)
            {
                throw new ArgumentException("Projectile spawn events cannot carry a terminal reason.", nameof(terminalReason));
            }

            if (type == ProjectilePresentationEventType.Terminal
                && (!Enum.IsDefined(typeof(ProjectileTerminalReason), terminalReason)
                    || terminalReason == ProjectileTerminalReason.None))
            {
                throw new ArgumentException("Projectile terminal events require a terminal reason.", nameof(terminalReason));
            }

            Sequence = sequence;
            Type = type;
            Tick = tick;
            State = state;
            TerminalReason = terminalReason;
        }

        public long Sequence { get; }
        public ProjectilePresentationEventType Type { get; }
        public TickIndex Tick { get; }
        public ProjectilePresentationState State { get; }
        public ProjectileTerminalReason TerminalReason { get; }

        public static ProjectilePresentationEvent Spawn(
            long sequence,
            TickIndex tick,
            ProjectilePresentationState state)
        {
            return new ProjectilePresentationEvent(
                sequence,
                ProjectilePresentationEventType.Spawn,
                tick,
                state,
                ProjectileTerminalReason.None);
        }

        public static ProjectilePresentationEvent Terminal(
            long sequence,
            TickIndex tick,
            ProjectilePresentationState state,
            ProjectileTerminalReason reason)
        {
            return new ProjectilePresentationEvent(
                sequence,
                ProjectilePresentationEventType.Terminal,
                tick,
                state,
                reason);
        }
    }

    public interface IProjectilePresentationFeed
    {
        int ActiveCapacity { get; }
        int ActiveCount { get; }
        int EventCapacity { get; }
        int DroppedEventCount { get; }
        long FirstRetainedSequence { get; }
        long LastSequence { get; }

        int CopyActiveStates(ProjectilePresentationState[] output);

        int CopyEventsAfter(
            long lastSeenSequence,
            ProjectilePresentationEvent[] output,
            out bool hasGap);
    }

    public interface IProjectilePresentationFeedWriter : IProjectilePresentationFeed
    {
        bool TryRecordSpawn(in ProjectileSpawnRequest request, in ProjectilePathSnapshot path);
        bool TryUpdateLastPoint(in ProjectileSweepRequest request, SpatialVectorKey point);
        bool TryRecordTerminal(in ProjectileReleaseRequest request);
    }

    public sealed class FixedProjectilePresentationFeed : IProjectilePresentationFeedWriter
    {
        private readonly ProjectilePresentationState[] activeStates;
        private readonly bool[] activeSlots;
        private readonly ProjectilePresentationEvent[] events;
        private int eventStart;
        private int eventCount;
        private long nextSequence = 1L;

        public FixedProjectilePresentationFeed(int projectileCapacity)
            : this(projectileCapacity, CalculateDefaultEventCapacity(projectileCapacity))
        {
        }

        public FixedProjectilePresentationFeed(int projectileCapacity, int eventCapacity)
        {
            if (projectileCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileCapacity));
            }

            if (eventCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(eventCapacity));
            }

            activeStates = new ProjectilePresentationState[projectileCapacity];
            activeSlots = new bool[projectileCapacity];
            events = new ProjectilePresentationEvent[eventCapacity];
        }

        public int ActiveCapacity => activeStates.Length;
        public int EventCapacity => events.Length;
        public int DroppedEventCount { get; private set; }
        public int RejectedWriteCount { get; private set; }

        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < activeSlots.Length; index++)
                {
                    if (activeSlots[index])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public long FirstRetainedSequence => eventCount == 0
            ? nextSequence
            : events[eventStart].Sequence;

        public long LastSequence => nextSequence - 1L;

        public bool TryRecordSpawn(in ProjectileSpawnRequest request, in ProjectilePathSnapshot path)
        {
            if (!path.Matches(request)
                || FindActiveSlot(request.ProjectileId, request.RuntimeId) >= 0)
            {
                RejectedWriteCount++;
                return false;
            }

            int slot = FindFreeSlot();
            if (slot < 0)
            {
                RejectedWriteCount++;
                return false;
            }

            ProjectilePresentationState state = new ProjectilePresentationState(request, path, path.Start);
            activeStates[slot] = state;
            activeSlots[slot] = true;
            Append(ProjectilePresentationEvent.Spawn(nextSequence++, request.Tick, state));
            return true;
        }

        public bool TryUpdateLastPoint(in ProjectileSweepRequest request, SpatialVectorKey point)
        {
            int slot = FindActiveSlot(request.ProjectileId, request.RuntimeId);
            if (slot < 0)
            {
                RejectedWriteCount++;
                return false;
            }

            activeStates[slot] = activeStates[slot].WithLastPoint(point);
            return true;
        }

        public bool TryRecordTerminal(in ProjectileReleaseRequest request)
        {
            int slot = FindActiveSlot(request.ProjectileId, request.RuntimeId);
            if (slot < 0)
            {
                RejectedWriteCount++;
                return false;
            }

            ProjectilePresentationState state = activeStates[slot];
            Append(ProjectilePresentationEvent.Terminal(
                nextSequence++,
                request.Tick,
                state,
                request.Reason));
            activeSlots[slot] = false;
            activeStates[slot] = default(ProjectilePresentationState);
            return true;
        }

        public int CopyActiveStates(ProjectilePresentationState[] output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            int activeCount = ActiveCount;
            if (output.Length < activeCount)
            {
                throw new ArgumentException("Output does not have enough capacity for all active projectile states.", nameof(output));
            }

            int count = 0;
            for (int index = 0; index < activeSlots.Length; index++)
            {
                if (!activeSlots[index])
                {
                    continue;
                }

                output[count++] = activeStates[index];
            }

            return count;
        }

        public int CopyEventsAfter(
            long lastSeenSequence,
            ProjectilePresentationEvent[] output,
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

            long firstRetainedSequence = FirstRetainedSequence;
            hasGap = lastSeenSequence < firstRetainedSequence - 1L;
            int available = 0;
            for (int index = 0; index < eventCount; index++)
            {
                ProjectilePresentationEvent candidate = events[(eventStart + index) % events.Length];
                if (candidate.Sequence > lastSeenSequence)
                {
                    available++;
                }
            }

            if (output.Length < available)
            {
                throw new ArgumentException("Output does not have enough capacity for retained projectile events.", nameof(output));
            }

            int count = 0;
            for (int index = 0; index < eventCount; index++)
            {
                ProjectilePresentationEvent candidate = events[(eventStart + index) % events.Length];
                if (candidate.Sequence > lastSeenSequence)
                {
                    output[count++] = candidate;
                }
            }

            return count;
        }

        private static int CalculateDefaultEventCapacity(int projectileCapacity)
        {
            if (projectileCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileCapacity));
            }

            return projectileCapacity > int.MaxValue / 4
                ? int.MaxValue
                : Math.Max(64, projectileCapacity * 4);
        }

        private int FindActiveSlot(ProjectileId projectileId, RuntimeId runtimeId)
        {
            for (int index = 0; index < activeSlots.Length; index++)
            {
                if (activeSlots[index]
                    && activeStates[index].Request.ProjectileId == projectileId
                    && activeStates[index].Request.RuntimeId == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeSlot()
        {
            for (int index = 0; index < activeSlots.Length; index++)
            {
                if (!activeSlots[index])
                {
                    return index;
                }
            }

            return -1;
        }

        private void Append(ProjectilePresentationEvent item)
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
}
