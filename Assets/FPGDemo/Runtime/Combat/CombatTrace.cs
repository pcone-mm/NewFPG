using System;
using FPG.Demo.Core;

namespace FPG.Demo.Combat
{
    public interface ICombatTraceView
    {
        int Capacity { get; }
        int Count { get; }
        long TotalEventCount { get; }
        long DroppedEventCount { get; }
        ulong CanonicalDigest { get; }
        CombatEvent GetOldest(int index);
    }

    public enum CombatEventType
    {
        None = 0,
        SessionStateChanged,
        InputAccepted,
        InputRejected,
        ExposureChanged,
        ReloadStarted,
        ReloadCompleted,
        ReleaseCommitted,
        AttackCanceled,
        ProjectileStateChanged,
        ImpactAccepted,
        ImpactRejected,
        DamageApplied,
        BarrierBroken,
        BreakTriggered,
        GroggyStarted,
        GroggyEnded,
        Death,
        BudgetChanged,
        BattleCompleted,
        ThreatStateChanged,
        PerfectRetract,
        ThreatScheduleDecision,
        EnemyDespawned,
        EnemySpawned
    }

    public readonly struct CombatEvent
    {
        public CombatEvent(
            long sequence,
            TickIndex tick,
            CombatEventType eventType,
            RuntimeId sourceId,
            RuntimeId targetId,
            AttackId attackId,
            ImpactId impactId,
            int valueBefore,
            int valueAfter,
            RejectReason rejectReason,
            ulong payloadHash,
            DamageChannel damageChannel,
            int appliedBreakAmount,
            bool perfectRetract)
        {
            Sequence = sequence;
            Tick = tick;
            EventType = eventType;
            SourceId = sourceId;
            TargetId = targetId;
            AttackId = attackId;
            ImpactId = impactId;
            ValueBefore = valueBefore;
            ValueAfter = valueAfter;
            RejectReason = rejectReason;
            PayloadHash = payloadHash;
            DamageChannel = damageChannel;
            AppliedBreakAmount = appliedBreakAmount;
            PerfectRetract = perfectRetract;
        }

        public long Sequence { get; }
        public TickIndex Tick { get; }
        public CombatEventType EventType { get; }
        public RuntimeId SourceId { get; }
        public RuntimeId TargetId { get; }
        public AttackId AttackId { get; }
        public ImpactId ImpactId { get; }
        public int ValueBefore { get; }
        public int ValueAfter { get; }
        public RejectReason RejectReason { get; }
        public ulong PayloadHash { get; }
        public DamageChannel DamageChannel { get; }
        public int AppliedBreakAmount { get; }
        public bool PerfectRetract { get; }
    }

    public sealed class CombatTrace : ICombatTraceView
    {
        public const int DefaultCapacity = 512;

        private readonly CombatEvent[] events;
        private int startIndex;
        private int count;
        private long nextSequence;
        private long totalEventCount;
        private ulong digest;

        public CombatTrace(int capacity = DefaultCapacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            events = new CombatEvent[capacity];
            digest = StableHash.Mix(0x4650475F54524143UL);
        }

        public int Capacity => events.Length;
        public int Count => count;
        public long TotalEventCount => totalEventCount;
        public long DroppedEventCount => totalEventCount - count;
        public ulong CanonicalDigest => digest;

        public CombatEvent Record(
            TickIndex tick,
            CombatEventType eventType,
            RuntimeId sourceId,
            RuntimeId targetId,
            AttackId attackId,
            ImpactId impactId,
            int valueBefore,
            int valueAfter,
            RejectReason rejectReason = RejectReason.None,
            ulong payloadHash = 0UL,
            DamageChannel damageChannel = DamageChannel.None,
            int appliedBreakAmount = 0,
            bool perfectRetract = false)
        {
            CombatEvent combatEvent = new CombatEvent(
                nextSequence++,
                tick,
                eventType,
                sourceId,
                targetId,
                attackId,
                impactId,
                valueBefore,
                valueAfter,
                rejectReason,
                payloadHash,
                damageChannel,
                appliedBreakAmount,
                perfectRetract);

            int writeIndex;
            if (count < events.Length)
            {
                writeIndex = (startIndex + count) % events.Length;
                count++;
            }
            else
            {
                writeIndex = startIndex;
                startIndex = (startIndex + 1) % events.Length;
            }

            events[writeIndex] = combatEvent;
            totalEventCount++;
            AppendDigest(combatEvent);
            return combatEvent;
        }

        public CombatEvent GetOldest(int index)
        {
            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return events[(startIndex + index) % events.Length];
        }

        public void Reset()
        {
            startIndex = 0;
            count = 0;
            nextSequence = 0L;
            totalEventCount = 0L;
            digest = StableHash.Mix(0x4650475F54524143UL);
        }

        private void AppendDigest(CombatEvent combatEvent)
        {
            digest = StableHash.Append(digest, (ulong)combatEvent.Sequence);
            digest = StableHash.Append(digest, unchecked((ulong)combatEvent.Tick.Value));
            digest = StableHash.Append(digest, (ulong)combatEvent.EventType);
            digest = StableHash.Append(digest, unchecked((ulong)combatEvent.SourceId.Value));
            digest = StableHash.Append(digest, unchecked((ulong)combatEvent.TargetId.Value));
            digest = StableHash.Append(digest, unchecked((ulong)combatEvent.AttackId.Value));
            digest = StableHash.Append(digest, unchecked((ulong)combatEvent.ImpactId.Value));
            digest = StableHash.Append(digest, unchecked((ulong)(uint)combatEvent.ValueBefore));
            digest = StableHash.Append(digest, unchecked((ulong)(uint)combatEvent.ValueAfter));
            digest = StableHash.Append(digest, (ulong)combatEvent.RejectReason);
            digest = StableHash.Append(digest, combatEvent.PayloadHash);
            digest = StableHash.Append(digest, (ulong)combatEvent.DamageChannel);
            digest = StableHash.Append(
                digest,
                unchecked((ulong)combatEvent.AppliedBreakAmount));
            digest = StableHash.Append(digest, combatEvent.PerfectRetract ? 1UL : 0UL);
        }
    }
}
