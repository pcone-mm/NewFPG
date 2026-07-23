using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    public enum FpgVitalsChangeReason
    {
        Spawn = 0,
        Damage,
        BarrierRestore,
        Death,
        Restart
    }

    public readonly struct FpgVitalsSnapshot
    {
        public FpgVitalsSnapshot(
            long sequence,
            long revision,
            RuntimeId runtimeId,
            CombatantKind kind,
            TickIndex tick,
            int life,
            int maxLife,
            int barrier,
            int maxBarrier,
            bool dead,
            FpgVitalsChangeReason reason)
        {
            if (sequence <= 0L || revision <= 0L || !runtimeId.IsValid
                || !tick.IsValid || maxLife <= 0 || maxBarrier < 0
                || life < 0 || life > maxLife
                || barrier < 0 || barrier > maxBarrier
                || dead != (life <= 0)
                || !Enum.IsDefined(typeof(FpgVitalsChangeReason), reason))
            {
                throw new ArgumentException("Formal vitals snapshot is invalid.");
            }

            Sequence = sequence;
            Revision = revision;
            RuntimeId = runtimeId;
            Kind = kind;
            Tick = tick;
            Life = life;
            MaxLife = maxLife;
            Barrier = barrier;
            MaxBarrier = maxBarrier;
            Dead = dead;
            Reason = reason;
        }

        public long Sequence { get; }
        public long Revision { get; }
        public RuntimeId RuntimeId { get; }
        public CombatantKind Kind { get; }
        public TickIndex Tick { get; }
        public int Life { get; }
        public int MaxLife { get; }
        public int Barrier { get; }
        public int MaxBarrier { get; }
        public bool Dead { get; }
        public FpgVitalsChangeReason Reason { get; }

        public bool IsValid => Sequence > 0L && Revision > 0L
            && RuntimeId.IsValid && Tick.IsValid && MaxLife > 0
            && MaxBarrier >= 0 && Life >= 0 && Life <= MaxLife
            && Barrier >= 0 && Barrier <= MaxBarrier && Dead == (Life <= 0);
    }

    public interface IFpgVitalsView
    {
        int CombatantCapacity { get; }
        int EventCapacity { get; }
        int DroppedEventCount { get; }
        long FirstRetainedSequence { get; }
        long LastSequence { get; }

        bool TryGetLatest(RuntimeId runtimeId, out FpgVitalsSnapshot snapshot);

        int CopyChangesAfter(
            long lastSeenSequence,
            FpgVitalsSnapshot[] output,
            out bool hasGap);
    }

    public sealed class FixedFpgVitalsStream : IFpgVitalsView
    {
        private readonly FpgVitalsSnapshot[] latest;
        private readonly bool[] latestSlots;
        private readonly FpgVitalsSnapshot[] events;
        private int eventStart;
        private int eventCount;
        private long nextSequence = 1L;

        public FixedFpgVitalsStream(int combatantCapacity, int eventCapacity)
        {
            if (combatantCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(combatantCapacity));
            }

            if (eventCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(eventCapacity));
            }

            latest = new FpgVitalsSnapshot[combatantCapacity];
            latestSlots = new bool[combatantCapacity];
            events = new FpgVitalsSnapshot[eventCapacity];
        }

        public int CombatantCapacity => latest.Length;
        public int EventCapacity => events.Length;
        public int DroppedEventCount { get; private set; }
        public int RejectedWriteCount { get; private set; }
        public long FirstRetainedSequence => eventCount == 0
            ? nextSequence
            : events[eventStart].Sequence;
        public long LastSequence => nextSequence - 1L;

        public bool TryPublish(
            CombatantState combatant,
            TickIndex tick,
            FpgVitalsChangeReason reason,
            bool force = false)
        {
            if (combatant == null || !tick.IsValid
                || !Enum.IsDefined(typeof(FpgVitalsChangeReason), reason))
            {
                RejectedWriteCount++;
                return false;
            }

            int slot = FindSlot(combatant.RuntimeId);
            long revision = 1L;
            if (slot >= 0)
            {
                FpgVitalsSnapshot previous = latest[slot];
                if (!force
                    && previous.Life == combatant.Life
                    && previous.MaxLife == combatant.MaxLife
                    && previous.Barrier == combatant.Barrier
                    && previous.MaxBarrier == combatant.MaxBarrier
                    && previous.Dead == combatant.IsDead)
                {
                    return true;
                }

                revision = previous.Revision == long.MaxValue
                    ? 1L
                    : previous.Revision + 1L;
            }
            else
            {
                slot = FindAvailableSlot();
                if (slot < 0)
                {
                    RejectedWriteCount++;
                    return false;
                }
            }

            long sequence = nextSequence == long.MaxValue ? 1L : nextSequence;
            nextSequence = sequence == long.MaxValue ? 1L : sequence + 1L;
            FpgVitalsSnapshot snapshot = new FpgVitalsSnapshot(
                sequence,
                revision,
                combatant.RuntimeId,
                combatant.Kind,
                tick,
                combatant.Life,
                combatant.MaxLife,
                combatant.Barrier,
                combatant.MaxBarrier,
                combatant.IsDead,
                reason);
            latest[slot] = snapshot;
            latestSlots[slot] = true;
            Append(snapshot);
            return true;
        }

        public bool TryGetLatest(RuntimeId runtimeId, out FpgVitalsSnapshot snapshot)
        {
            int slot = FindSlot(runtimeId);
            snapshot = slot < 0 ? default(FpgVitalsSnapshot) : latest[slot];
            return snapshot.IsValid;
        }

        public int CopyChangesAfter(
            long lastSeenSequence,
            FpgVitalsSnapshot[] output,
            out bool hasGap)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            lastSeenSequence = Math.Max(0L, lastSeenSequence);
            hasGap = lastSeenSequence < FirstRetainedSequence - 1L;
            int available = 0;
            for (int index = 0; index < eventCount; index++)
            {
                FpgVitalsSnapshot candidate = events[(eventStart + index) % events.Length];
                if (candidate.Sequence > lastSeenSequence)
                {
                    available++;
                }
            }

            if (output.Length < available)
            {
                throw new ArgumentException(
                    "Output does not have enough capacity for retained vitals changes.",
                    nameof(output));
            }

            int count = 0;
            for (int index = 0; index < eventCount; index++)
            {
                FpgVitalsSnapshot candidate = events[(eventStart + index) % events.Length];
                if (candidate.Sequence > lastSeenSequence)
                {
                    output[count++] = candidate;
                }
            }

            return count;
        }

        public void Clear()
        {
            Array.Clear(latest, 0, latest.Length);
            Array.Clear(latestSlots, 0, latestSlots.Length);
            Array.Clear(events, 0, events.Length);
            eventStart = 0;
            eventCount = 0;
            nextSequence = 1L;
            DroppedEventCount = 0;
            RejectedWriteCount = 0;
        }

        private int FindSlot(RuntimeId runtimeId)
        {
            if (!runtimeId.IsValid)
            {
                return -1;
            }

            for (int index = 0; index < latest.Length; index++)
            {
                if (latestSlots[index] && latest[index].RuntimeId == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindAvailableSlot()
        {
            int oldestDeadSlot = -1;
            long oldestDeadSequence = long.MaxValue;
            for (int index = 0; index < latest.Length; index++)
            {
                if (!latestSlots[index])
                {
                    return index;
                }

                if (latest[index].Dead && latest[index].Sequence < oldestDeadSequence)
                {
                    oldestDeadSlot = index;
                    oldestDeadSequence = latest[index].Sequence;
                }
            }

            return oldestDeadSlot;
        }

        private void Append(FpgVitalsSnapshot snapshot)
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

            events[writeIndex] = snapshot;
        }
    }

    public readonly struct FpgResolvedDamageFeedback
    {
        public FpgResolvedDamageFeedback(
            long sequence,
            ImpactIntent intent,
            DamagePacket packet,
            bool projectileDestroyed)
        {
            if (sequence <= 0L || !intent.ImpactId.IsValid
                || packet.ImpactId != intent.ImpactId || packet.AppliedAmount <= 0)
            {
                throw new ArgumentException("Resolved damage feedback is invalid.");
            }

            Sequence = sequence;
            Tick = intent.ImpactTick;
            ImpactId = intent.ImpactId;
            AttackId = intent.AttackId;
            ShotId = intent.ShotId;
            SourceId = intent.SourceId;
            TargetId = intent.TargetId;
            HitPart = intent.HitPart;
            DamageType = intent.DamageType;
            AppliedDamage = packet.AppliedAmount;
            Channel = packet.Channel;
            PelletIndex = intent.PelletIndex;
            ImpactOrdinal = intent.ImpactOrdinal;
            SpatialContext = intent.SpatialContext;
            ProjectileDestroyed = projectileDestroyed;
        }

        public long Sequence { get; }
        public TickIndex Tick { get; }
        public ImpactId ImpactId { get; }
        public AttackId AttackId { get; }
        public ShotId ShotId { get; }
        public RuntimeId SourceId { get; }
        public RuntimeId TargetId { get; }
        public HitPart HitPart { get; }
        public DamageType DamageType { get; }
        public int AppliedDamage { get; }
        public DamageChannel Channel { get; }
        public int PelletIndex { get; }
        public int ImpactOrdinal { get; }
        public ImpactSpatialContext SpatialContext { get; }
        public bool ProjectileDestroyed { get; }
        public bool IsWeakpoint => HitPart == HitPart.Weakpoint;
        public bool IsProjectile => Channel == DamageChannel.ProjectileHp;
    }

    public interface IFpgResolvedDamageFeedbackView
    {
        int Capacity { get; }
        int DroppedEventCount { get; }
        long FirstRetainedSequence { get; }
        long LastSequence { get; }

        int CopyAfter(
            long lastSeenSequence,
            FpgResolvedDamageFeedback[] output,
            out bool hasGap);
    }

    public sealed class FixedResolvedDamageFeedbackStream :
        IFpgResolvedDamageFeedbackView
    {
        private readonly FpgResolvedDamageFeedback[] entries;
        private int start;
        private int count;
        private long nextSequence = 1L;

        public FixedResolvedDamageFeedbackStream(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            entries = new FpgResolvedDamageFeedback[capacity];
        }

        public int Capacity => entries.Length;
        public int Count => count;
        public int DroppedEventCount { get; private set; }
        public int RejectedWriteCount { get; private set; }
        public long FirstRetainedSequence => count == 0
            ? nextSequence
            : entries[start].Sequence;
        public long LastSequence => nextSequence - 1L;

        public bool TryRecord(in ImpactIntent intent, in ImpactResolution resolution)
        {
            if (!resolution.Result.IsSuccess
                || resolution.Packet.ImpactId != intent.ImpactId
                || resolution.Packet.AppliedAmount <= 0)
            {
                RejectedWriteCount++;
                return false;
            }

            long sequence = nextSequence == long.MaxValue ? 1L : nextSequence;
            nextSequence = sequence == long.MaxValue ? 1L : sequence + 1L;
            FpgResolvedDamageFeedback item = new FpgResolvedDamageFeedback(
                sequence,
                intent,
                resolution.Packet,
                resolution.ProjectileDestroyed);
            int writeIndex;
            if (count < entries.Length)
            {
                writeIndex = (start + count) % entries.Length;
                count++;
            }
            else
            {
                writeIndex = start;
                start = (start + 1) % entries.Length;
                DroppedEventCount++;
            }

            entries[writeIndex] = item;
            return true;
        }

        public int CopyAfter(
            long lastSeenSequence,
            FpgResolvedDamageFeedback[] output,
            out bool hasGap)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            lastSeenSequence = Math.Max(0L, lastSeenSequence);
            hasGap = lastSeenSequence < FirstRetainedSequence - 1L;
            int available = 0;
            for (int index = 0; index < count; index++)
            {
                if (entries[(start + index) % entries.Length].Sequence > lastSeenSequence)
                {
                    available++;
                }
            }

            if (output.Length < available)
            {
                throw new ArgumentException(
                    "Output does not have enough capacity for retained damage feedback.",
                    nameof(output));
            }

            int copied = 0;
            for (int index = 0; index < count; index++)
            {
                FpgResolvedDamageFeedback candidate =
                    entries[(start + index) % entries.Length];
                if (candidate.Sequence > lastSeenSequence)
                {
                    output[copied++] = candidate;
                }
            }

            return copied;
        }

        public void Clear()
        {
            Array.Clear(entries, 0, entries.Length);
            start = 0;
            count = 0;
            nextSequence = 1L;
            DroppedEventCount = 0;
            RejectedWriteCount = 0;
        }
    }
}
