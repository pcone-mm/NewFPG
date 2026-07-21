using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    public readonly struct SelectedAttackHit
    {
        public SelectedAttackHit(
            AttackId attackId,
            ShotId shotId,
            TickIndex tick,
            int impactOrdinal,
            AttackQueryStage queryStage,
            int sampleIndex,
            RuntimeId targetId,
            QueryTargetKind targetKind,
            HitPart hitPart,
            GeometryId geometryId,
            SpatialVectorKey impactPointKey)
        {
            if (!attackId.IsValid || !shotId.IsValid || !tick.IsValid
                || impactOrdinal < 0 || !targetId.IsValid || !geometryId.IsValid
                || !Enum.IsDefined(typeof(AttackQueryStage), queryStage)
                || !Enum.IsDefined(typeof(QueryTargetKind), targetKind)
                || !Enum.IsDefined(typeof(HitPart), hitPart)
                || targetKind == QueryTargetKind.EnvironmentBlocker
                || (queryStage == AttackQueryStage.Pellet ? sampleIndex < 0 : sampleIndex != -1)
                || (targetKind == QueryTargetKind.Projectile
                    ? hitPart != HitPart.Projectile
                    : hitPart == HitPart.Projectile))
            {
                throw new ArgumentException("Selected attack hit fields do not form a valid spatial selection.");
            }

            AttackId = attackId;
            ShotId = shotId;
            Tick = tick;
            ImpactOrdinal = impactOrdinal;
            QueryStage = queryStage;
            SampleIndex = sampleIndex;
            TargetId = targetId;
            TargetKind = targetKind;
            HitPart = hitPart;
            GeometryId = geometryId;
            ImpactPointKey = impactPointKey;
        }

        public AttackId AttackId { get; }
        public ShotId ShotId { get; }
        public TickIndex Tick { get; }
        public int ImpactOrdinal { get; }
        public AttackQueryStage QueryStage { get; }
        public int SampleIndex { get; }
        public RuntimeId TargetId { get; }
        public QueryTargetKind TargetKind { get; }
        public HitPart HitPart { get; }
        public GeometryId GeometryId { get; }
        public SpatialVectorKey ImpactPointKey { get; }

        public bool IsValid => AttackId.IsValid
            && ShotId.IsValid
            && Tick.IsValid
            && ImpactOrdinal >= 0
            && TargetId.IsValid
            && GeometryId.IsValid
            && TargetKind != QueryTargetKind.EnvironmentBlocker
            && (QueryStage == AttackQueryStage.Pellet ? SampleIndex >= 0 : SampleIndex == -1)
            && (TargetKind == QueryTargetKind.Projectile
                ? HitPart == HitPart.Projectile
                : HitPart != HitPart.Projectile);
    }

    public interface ISelectedAttackHitView
    {
        int Capacity { get; }
        int Count { get; }
        SelectedAttackHit GetOldest(int index);
        DomainResult CopyTo(SelectedAttackHit[] output, out int count);
    }

    public sealed class SelectedAttackHitStream : ISelectedAttackHitView
    {
        private readonly SelectedAttackHit[] entries;

        public SelectedAttackHitStream(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            entries = new SelectedAttackHit[capacity];
        }

        public int Capacity => entries.Length;
        public int Count { get; private set; }
        internal int RemainingCapacity => entries.Length - Count;

        public SelectedAttackHit GetOldest(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return entries[index];
        }

        public DomainResult CopyTo(SelectedAttackHit[] output, out int count)
        {
            count = Count;
            if (output == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (output.Length < Count)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            for (int index = 0; index < Count; index++)
            {
                output[index] = entries[index];
            }

            return DomainResult.Success;
        }

        public DomainResult TryAppend(SelectedAttackHit[] source, int count)
        {
            if (!CanAppend(source, count))
            {
                return source == null || count < 0 || count > source.Length
                    ? DomainResult.Rejected(RejectReason.InvalidState)
                    : DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            for (int index = 0; index < count; index++)
            {
                if (!source[index].IsValid)
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }
            }

            AppendValidated(source, count);
            return DomainResult.Success;
        }

        internal bool CanAppend(SelectedAttackHit[] source, int count)
        {
            return source != null
                && count >= 0
                && count <= source.Length
                && count <= RemainingCapacity;
        }

        internal void AppendValidated(SelectedAttackHit[] source, int count)
        {
            for (int index = 0; index < count; index++)
            {
                entries[Count++] = source[index];
            }
        }
    }
}
