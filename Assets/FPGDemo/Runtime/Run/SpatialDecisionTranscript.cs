using System;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    public enum SpatialDecisionKind
    {
        AttackQuery = 0,
        ProjectileRegister,
        ProjectileSweep,
        ProjectileRelease,
        PlayerProjectileAreaQuery
    }

    public readonly struct SpatialDecisionRecord
    {
        public SpatialDecisionRecord(
            long sequence,
            TickIndex tick,
            SpatialDecisionKind kind,
            RuntimeId subjectId,
            GeometryId geometryId,
            RejectReason result,
            ulong payloadHash)
        {
            if (sequence <= 0L || !tick.IsValid || !Enum.IsDefined(typeof(SpatialDecisionKind), kind)
                || !subjectId.IsValid || !Enum.IsDefined(typeof(RejectReason), result))
            {
                throw new ArgumentException("Spatial decision fields must be valid.");
            }

            Sequence = sequence;
            Tick = tick;
            Kind = kind;
            SubjectId = subjectId;
            GeometryId = geometryId;
            Result = result;
            PayloadHash = payloadHash;
        }

        public long Sequence { get; }
        public TickIndex Tick { get; }
        public SpatialDecisionKind Kind { get; }
        public RuntimeId SubjectId { get; }
        public GeometryId GeometryId { get; }
        public RejectReason Result { get; }
        public ulong PayloadHash { get; }

        public ulong AppendStableHash(ulong hash)
        {
            hash = StableHash.Append(hash, unchecked((ulong)Sequence));
            hash = StableHash.Append(hash, unchecked((ulong)Tick.Value));
            hash = StableHash.Append(hash, (ulong)Kind);
            hash = StableHash.Append(hash, unchecked((ulong)SubjectId.Value));
            hash = StableHash.Append(hash, unchecked((ulong)GeometryId.Value));
            hash = StableHash.Append(hash, (ulong)Result);
            return StableHash.Append(hash, PayloadHash);
        }
    }

    public interface ISpatialDigestView
    {
        int Count { get; }
        int ContractVersion { get; }
        ulong CanonicalDigest { get; }
    }

    public interface ISpatialDecisionTranscriptView : ISpatialDigestView
    {
        int Capacity { get; }
        SpatialDecisionRecord Get(int index);
    }

    public sealed class SpatialDecisionTranscript : ISpatialDecisionTranscriptView
    {
        private readonly SpatialDecisionRecord[] records;
        private int count;
        private ulong digest;

        public SpatialDecisionTranscript(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            records = new SpatialDecisionRecord[capacity];
            digest = CreateInitialDigest();
        }

        public int Capacity => records.Length;
        public int Count => count;
        public int ContractVersion => SpatialContract.Version;
        public ulong CanonicalDigest => digest;

        public DomainResult TryRecord(
            TickIndex tick,
            SpatialDecisionKind kind,
            RuntimeId subjectId,
            GeometryId geometryId,
            RejectReason result,
            ulong payloadHash,
            out SpatialDecisionRecord record)
        {
            record = default(SpatialDecisionRecord);
            if (count >= records.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            try
            {
                record = new SpatialDecisionRecord(
                    count + 1L,
                    tick,
                    kind,
                    subjectId,
                    geometryId,
                    result,
                    payloadHash);
            }
            catch (ArgumentException)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            records[count++] = record;
            digest = record.AppendStableHash(digest);
            return DomainResult.Success;
        }

        public SpatialDecisionRecord Get(int index)
        {
            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return records[index];
        }

        public void Reset()
        {
            Array.Clear(records, 0, records.Length);
            count = 0;
            digest = CreateInitialDigest();
        }

        private static ulong CreateInitialDigest()
        {
            ulong hash = StableHash.Mix(0x4650475F53504154UL);
            hash = StableHash.Append(hash, unchecked((ulong)SpatialContract.Version));
            hash = StableHash.Append(hash, unchecked((ulong)SpatialContract.PositionUnitsPerMeter));
            hash = StableHash.Append(hash, unchecked((ulong)SpatialContract.DirectionUnits));
            hash = StableHash.Append(hash, unchecked((ulong)SpatialContract.DistanceUnitsPerMeter));
            return StableHash.Append(
                hash,
                unchecked((ulong)SpatialContract.AttackQueryCandidateCapacity));
        }
    }
}
