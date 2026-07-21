using System;
using FPG.Demo.Combat;

namespace FPG.Demo.Run
{
    /// <summary>
    /// Read-only consumer cursor for the bounded combat trace. The cursor never
    /// records, resets or otherwise mutates the authoritative trace.
    /// </summary>
    public sealed class CombatTraceCursor
    {
        private long lastSeenSequence = -1L;

        public long LastSeenSequence => lastSeenSequence;
        public int GapCount { get; private set; }

        public void Reset()
        {
            lastSeenSequence = -1L;
            GapCount = 0;
        }

        public int CopyUnread(
            ICombatTraceView trace,
            CombatEvent[] output,
            out bool hasGap)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            hasGap = false;
            if (trace.Count == 0)
            {
                return 0;
            }

            CombatEvent oldest = trace.GetOldest(0);
            hasGap = oldest.Sequence > lastSeenSequence + 1L;
            if (hasGap)
            {
                return 0;
            }

            int unreadCount = 0;
            for (int index = 0; index < trace.Count; index++)
            {
                if (trace.GetOldest(index).Sequence > lastSeenSequence)
                {
                    unreadCount++;
                }
            }

            if (output.Length < unreadCount)
            {
                throw new ArgumentException(
                    "Output does not have enough capacity for retained combat trace events.",
                    nameof(output));
            }

            int written = 0;
            for (int index = 0; index < trace.Count; index++)
            {
                CombatEvent item = trace.GetOldest(index);
                if (item.Sequence > lastSeenSequence)
                {
                    output[written++] = item;
                }
            }

            return written;
        }

        public void Commit(CombatEvent item)
        {
            if (item.Sequence > lastSeenSequence)
            {
                lastSeenSequence = item.Sequence;
            }
        }

        public void ResolveGap(ICombatTraceView trace)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (trace.Count > 0)
            {
                lastSeenSequence = trace.GetOldest(trace.Count - 1).Sequence;
            }

            GapCount++;
        }
    }

    /// <summary>
    /// Read-only cursor for the non-overwriting selected-hit stream. A new
    /// session/rebind explicitly resets this cursor; a reduced source count is
    /// treated as the same safe reset condition.
    /// </summary>
    public sealed class SelectedAttackHitCursor
    {
        private int consumedCount;

        public int ConsumedCount => consumedCount;

        public void Reset()
        {
            consumedCount = 0;
        }

        public int CopyUnread(ISelectedAttackHitView stream, SelectedAttackHit[] output)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (stream.Count < consumedCount)
            {
                consumedCount = 0;
            }

            int unreadCount = stream.Count - consumedCount;
            if (output.Length < unreadCount)
            {
                throw new ArgumentException(
                    "Output does not have enough capacity for unread selected attack hits.",
                    nameof(output));
            }

            for (int index = 0; index < unreadCount; index++)
            {
                output[index] = stream.GetOldest(consumedCount + index);
            }

            return unreadCount;
        }

        public void CommitOne()
        {
            if (consumedCount < int.MaxValue)
            {
                consumedCount++;
            }
        }
    }

    /// <summary>
    /// Read-only cursor for a bounded player-shot presentation feed. A ring
    /// gap is resolved by jumping to the current tail rather than replaying
    /// stale shots after a consumer is rebound or temporarily disabled.
    /// </summary>
    public sealed class PlayerShotPresentationCursor
    {
        private long lastSeenSequence = -1L;

        public long LastSeenSequence => lastSeenSequence;
        public int GapCount { get; private set; }

        public void Reset()
        {
            lastSeenSequence = -1L;
            GapCount = 0;
        }

        public int CopyUnread(
            IPlayerShotPresentationFeed feed,
            PlayerShotPresentationEvent[] output,
            out bool hasGap)
        {
            if (feed == null)
            {
                throw new ArgumentNullException(nameof(feed));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            int count = feed.CopyEventsAfter(lastSeenSequence, output, out hasGap);
            return hasGap ? 0 : count;
        }

        public void Commit(PlayerShotPresentationEvent item)
        {
            if (item.Sequence > lastSeenSequence)
            {
                lastSeenSequence = item.Sequence;
            }
        }

        /// <summary>
        /// Establishes a clean read baseline for a freshly bound presentation
        /// consumer. Unlike <see cref="ResolveGap"/>, this is not data loss:
        /// it deliberately declines to replay transient shots that existed
        /// before the consumer was enabled or rebound.
        /// </summary>
        public void SetBaseline(IPlayerShotPresentationFeed feed)
        {
            if (feed == null)
            {
                throw new ArgumentNullException(nameof(feed));
            }

            lastSeenSequence = feed.LastSequence;
        }

        public void ResolveGap(IPlayerShotPresentationFeed feed)
        {
            if (feed == null)
            {
                throw new ArgumentNullException(nameof(feed));
            }

            lastSeenSequence = feed.LastSequence;
            GapCount++;
        }
    }
}
