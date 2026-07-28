using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Skills;

namespace FPG.Demo.Run
{
    public enum FpgSkillImpactPresentationEventType
    {
        Contact = 0,
        GroupCompleted
    }

    public enum FpgSkillImpactPresentationGroupKind
    {
        ImmediateAttack = 0,
        Projectile
    }

    public enum FpgSkillImpactContactKind
    {
        TargetImpact = 0,
        EnvironmentBlocked,
        Intercepted
    }

    public static class FpgSkillImpactPresentationRules
    {
        public static bool TryResolveProjectileContactKind(
            ProjectileTerminalReason reason,
            out FpgSkillImpactContactKind contactKind)
        {
            switch (reason)
            {
                case ProjectileTerminalReason.TargetImpact:
                    contactKind = FpgSkillImpactContactKind.TargetImpact;
                    return true;
                case ProjectileTerminalReason.EnvironmentBlocked:
                    contactKind =
                        FpgSkillImpactContactKind.EnvironmentBlocked;
                    return true;
                case ProjectileTerminalReason.Intercepted:
                    contactKind = FpgSkillImpactContactKind.Intercepted;
                    return true;
                default:
                    contactKind = default(FpgSkillImpactContactKind);
                    return false;
            }
        }
    }

    public readonly struct FpgSkillImpactCorrelation :
        IEquatable<FpgSkillImpactCorrelation>
    {
        public FpgSkillImpactCorrelation(
            RuntimeId sourceRuntimeId,
            SkillExecutionId skillExecutionId,
            int gameplayEventId)
        {
            if (!sourceRuntimeId.IsValid
                || !skillExecutionId.IsValid
                || gameplayEventId <= 0)
            {
                throw new ArgumentException(
                    "Skill impact correlation requires a source, execution and gameplay event.");
            }

            SourceRuntimeId = sourceRuntimeId;
            SkillExecutionId = skillExecutionId;
            GameplayEventId = gameplayEventId;
        }

        public RuntimeId SourceRuntimeId { get; }
        public SkillExecutionId SkillExecutionId { get; }
        public int GameplayEventId { get; }
        public bool IsValid => SourceRuntimeId.IsValid
            && SkillExecutionId.IsValid
            && GameplayEventId > 0;

        public bool Equals(FpgSkillImpactCorrelation other)
        {
            return SourceRuntimeId == other.SourceRuntimeId
                && SkillExecutionId == other.SkillExecutionId
                && GameplayEventId == other.GameplayEventId;
        }

        public override bool Equals(object obj)
        {
            return obj is FpgSkillImpactCorrelation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourceRuntimeId.GetHashCode();
                hash = (hash * 397) ^ SkillExecutionId.GetHashCode();
                return (hash * 397) ^ GameplayEventId;
            }
        }

        public static bool operator ==(
            FpgSkillImpactCorrelation left,
            FpgSkillImpactCorrelation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            FpgSkillImpactCorrelation left,
            FpgSkillImpactCorrelation right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct FpgSkillImpactContact
    {
        public FpgSkillImpactContact(
            FpgSkillImpactCorrelation correlation,
            FpgSkillImpactPresentationGroupKind groupKind,
            TickIndex tick,
            AttackId attackId,
            ProjectileId projectileId,
            ImpactId impactId,
            RuntimeId targetRuntimeId,
            FpgSkillImpactContactKind contactKind,
            SpatialVectorKey contactPoint,
            HitPart hitPart,
            int contactOrdinal)
        {
            Correlation = correlation;
            GroupKind = groupKind;
            Tick = tick;
            AttackId = attackId;
            ProjectileId = projectileId;
            ImpactId = impactId;
            TargetRuntimeId = targetRuntimeId;
            ContactKind = contactKind;
            ContactPoint = contactPoint;
            HitPart = hitPart;
            ContactOrdinal = contactOrdinal;

            if (!IsValid)
            {
                throw new ArgumentException(
                    "Skill impact contact fields are inconsistent.");
            }
        }

        public FpgSkillImpactCorrelation Correlation { get; }
        public FpgSkillImpactPresentationGroupKind GroupKind { get; }
        public TickIndex Tick { get; }
        public AttackId AttackId { get; }
        public ProjectileId ProjectileId { get; }
        public ImpactId ImpactId { get; }
        public RuntimeId TargetRuntimeId { get; }
        public FpgSkillImpactContactKind ContactKind { get; }
        public SpatialVectorKey ContactPoint { get; }
        public HitPart HitPart { get; }
        public int ContactOrdinal { get; }

        public bool IsValid
        {
            get
            {
                if (!Correlation.IsValid
                    || !Tick.IsValid
                    || !AttackId.IsValid
                    || !Enum.IsDefined(
                        typeof(FpgSkillImpactPresentationGroupKind),
                        GroupKind)
                    || !Enum.IsDefined(
                        typeof(FpgSkillImpactContactKind),
                        ContactKind)
                    || !Enum.IsDefined(typeof(HitPart), HitPart)
                    || ContactOrdinal < 0
                    || GroupKind == FpgSkillImpactPresentationGroupKind.Projectile
                        && !ProjectileId.IsValid)
                {
                    return false;
                }

                switch (ContactKind)
                {
                    case FpgSkillImpactContactKind.TargetImpact:
                        return TargetRuntimeId.IsValid
                            && (HitPart != HitPart.Projectile
                                || ProjectileId.IsValid);
                    case FpgSkillImpactContactKind.EnvironmentBlocked:
                        return !TargetRuntimeId.IsValid
                            && HitPart == HitPart.Body;
                    case FpgSkillImpactContactKind.Intercepted:
                        return TargetRuntimeId.IsValid
                            && ProjectileId.IsValid
                            && HitPart == HitPart.Projectile;
                    default:
                        return false;
                }
            }
        }
    }

    public readonly struct FpgSkillImpactGroupCompletion
    {
        public FpgSkillImpactGroupCompletion(
            FpgSkillImpactCorrelation correlation,
            FpgSkillImpactPresentationGroupKind groupKind,
            TickIndex tick,
            AttackId attackId)
        {
            Correlation = correlation;
            GroupKind = groupKind;
            Tick = tick;
            AttackId = attackId;

            if (!IsValid)
            {
                throw new ArgumentException(
                    "Skill impact group completion fields are invalid.");
            }
        }

        public FpgSkillImpactCorrelation Correlation { get; }
        public FpgSkillImpactPresentationGroupKind GroupKind { get; }
        public TickIndex Tick { get; }
        public AttackId AttackId { get; }
        public bool IsValid => Correlation.IsValid
            && Tick.IsValid
            && AttackId.IsValid
            && Enum.IsDefined(
                typeof(FpgSkillImpactPresentationGroupKind),
                GroupKind);
    }

    public readonly struct FpgSkillImpactPresentationEvent
    {
        private FpgSkillImpactPresentationEvent(
            long sequence,
            FpgSkillImpactPresentationEventType type,
            FpgSkillImpactContact contact,
            FpgSkillImpactGroupCompletion completion)
        {
            if (sequence <= 0L
                || !Enum.IsDefined(
                    typeof(FpgSkillImpactPresentationEventType),
                    type)
                || type == FpgSkillImpactPresentationEventType.Contact
                    && !contact.IsValid
                || type == FpgSkillImpactPresentationEventType.GroupCompleted
                    && !completion.IsValid)
            {
                throw new ArgumentException(
                    "Skill impact presentation event is invalid.");
            }

            Sequence = sequence;
            Type = type;
            Contact = contact;
            Completion = completion;
        }

        public long Sequence { get; }
        public FpgSkillImpactPresentationEventType Type { get; }
        public FpgSkillImpactContact Contact { get; }
        public FpgSkillImpactGroupCompletion Completion { get; }
        public TickIndex Tick => Type == FpgSkillImpactPresentationEventType.Contact
            ? Contact.Tick
            : Completion.Tick;
        public FpgSkillImpactCorrelation Correlation =>
            Type == FpgSkillImpactPresentationEventType.Contact
                ? Contact.Correlation
                : Completion.Correlation;
        public FpgSkillImpactPresentationGroupKind GroupKind =>
            Type == FpgSkillImpactPresentationEventType.Contact
                ? Contact.GroupKind
                : Completion.GroupKind;

        internal static FpgSkillImpactPresentationEvent ForContact(
            long sequence,
            in FpgSkillImpactContact contact)
        {
            return new FpgSkillImpactPresentationEvent(
                sequence,
                FpgSkillImpactPresentationEventType.Contact,
                contact,
                default(FpgSkillImpactGroupCompletion));
        }

        internal static FpgSkillImpactPresentationEvent ForCompletion(
            long sequence,
            in FpgSkillImpactGroupCompletion completion)
        {
            return new FpgSkillImpactPresentationEvent(
                sequence,
                FpgSkillImpactPresentationEventType.GroupCompleted,
                default(FpgSkillImpactContact),
                completion);
        }
    }

    public interface IFpgSkillImpactPresentationView
    {
        int Capacity { get; }
        int DroppedEventCount { get; }
        long FirstRetainedSequence { get; }
        long LastSequence { get; }

        int CopyAfter(
            long lastSeenSequence,
            FpgSkillImpactPresentationEvent[] output,
            out bool hasGap);
    }

    public sealed class FixedFpgSkillImpactPresentationStream :
        IFpgSkillImpactPresentationView
    {
        private readonly FpgSkillImpactPresentationEvent[] entries;
        private int start;
        private int count;
        private long nextSequence = 1L;

        public FixedFpgSkillImpactPresentationStream(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            entries = new FpgSkillImpactPresentationEvent[capacity];
        }

        public int Capacity => entries.Length;
        public int Count => count;
        public int DroppedEventCount { get; private set; }
        public int RejectedWriteCount { get; private set; }
        public long FirstRetainedSequence => count == 0
            ? nextSequence
            : entries[start].Sequence;
        public long LastSequence => nextSequence - 1L;

        public bool TryRecordContact(in FpgSkillImpactContact contact)
        {
            if (!contact.IsValid)
            {
                RejectedWriteCount++;
                return false;
            }

            Append(FpgSkillImpactPresentationEvent.ForContact(
                nextSequence++,
                contact));
            return true;
        }

        public bool TryRecordGroupCompletion(
            in FpgSkillImpactGroupCompletion completion)
        {
            if (!completion.IsValid)
            {
                RejectedWriteCount++;
                return false;
            }

            Append(FpgSkillImpactPresentationEvent.ForCompletion(
                nextSequence++,
                completion));
            return true;
        }

        public int CopyAfter(
            long lastSeenSequence,
            FpgSkillImpactPresentationEvent[] output,
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
                if (entries[(start + index) % entries.Length].Sequence
                    > lastSeenSequence)
                {
                    available++;
                }
            }

            if (output.Length < available)
            {
                throw new ArgumentException(
                    "Output does not have enough capacity for retained skill impact events.",
                    nameof(output));
            }

            int copied = 0;
            for (int index = 0; index < count; index++)
            {
                FpgSkillImpactPresentationEvent candidate =
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

        private void Append(FpgSkillImpactPresentationEvent item)
        {
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
        }
    }
}
