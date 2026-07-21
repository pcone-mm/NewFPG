using System;
using FPG.Demo.Core;

namespace FPG.Demo.Combat
{
    public enum AttackQueryStage
    {
        Direct = 0,
        Area,
        Pellet
    }

    public enum QueryTargetKind
    {
        EnvironmentBlocker = 0,
        Combatant,
        Projectile
    }

    public readonly struct QueryCandidate
    {
        public QueryCandidate(
            AttackQueryStage queryStage,
            int sampleIndex,
            RuntimeId targetId,
            QueryTargetKind targetKind,
            HitPart hitPart,
            GeometryId geometryId,
            int distanceKey,
            SpatialVectorKey impactPointKey,
            int queryOrdinal)
        {
            if (!Enum.IsDefined(typeof(AttackQueryStage), queryStage))
            {
                throw new ArgumentOutOfRangeException(nameof(queryStage));
            }

            if (sampleIndex < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleIndex));
            }

            if (queryStage == AttackQueryStage.Pellet ? sampleIndex < 0 : sampleIndex != -1)
            {
                throw new ArgumentException("Pellet candidates require a sample index; direct/area candidates do not.", nameof(sampleIndex));
            }

            if (!Enum.IsDefined(typeof(QueryTargetKind), targetKind))
            {
                throw new ArgumentOutOfRangeException(nameof(targetKind));
            }

            if (!Enum.IsDefined(typeof(HitPart), hitPart))
            {
                throw new ArgumentOutOfRangeException(nameof(hitPart));
            }

            if (!geometryId.IsValid || distanceKey < 0 || queryOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(geometryId));
            }

            if (targetKind == QueryTargetKind.EnvironmentBlocker)
            {
                if (targetId.IsValid)
                {
                    throw new ArgumentException("Environment blockers cannot carry a combat runtime id.", nameof(targetId));
                }
            }
            else if (!targetId.IsValid)
            {
                throw new ArgumentException("Non-environment candidates require a runtime id.", nameof(targetId));
            }

            if (targetKind == QueryTargetKind.Projectile && hitPart != HitPart.Projectile
                || targetKind == QueryTargetKind.Combatant && hitPart == HitPart.Projectile
                || targetKind == QueryTargetKind.EnvironmentBlocker && hitPart != HitPart.Body)
            {
                throw new ArgumentException("Candidate target kind and hit part do not match.", nameof(hitPart));
            }

            QueryStage = queryStage;
            SampleIndex = sampleIndex;
            TargetId = targetId;
            TargetKind = targetKind;
            HitPart = hitPart;
            GeometryId = geometryId;
            DistanceKey = distanceKey;
            ImpactPointKey = impactPointKey;
            QueryOrdinal = queryOrdinal;
        }

        public AttackQueryStage QueryStage { get; }
        public int SampleIndex { get; }
        public RuntimeId TargetId { get; }
        public QueryTargetKind TargetKind { get; }
        public HitPart HitPart { get; }
        public GeometryId GeometryId { get; }
        public int DistanceKey { get; }
        public SpatialVectorKey ImpactPointKey { get; }
        public int QueryOrdinal { get; }
        public bool IsValid
        {
            get
            {
                if (!Enum.IsDefined(typeof(AttackQueryStage), QueryStage)
                    || !Enum.IsDefined(typeof(QueryTargetKind), TargetKind)
                    || !Enum.IsDefined(typeof(FPG.Demo.Combat.HitPart), HitPart)
                    || !GeometryId.IsValid
                    || DistanceKey < 0
                    || QueryOrdinal < 0
                    || (QueryStage == AttackQueryStage.Pellet ? SampleIndex < 0 : SampleIndex != -1))
                {
                    return false;
                }

                if (TargetKind == QueryTargetKind.EnvironmentBlocker)
                {
                    return !TargetId.IsValid && HitPart == FPG.Demo.Combat.HitPart.Body;
                }

                if (!TargetId.IsValid)
                {
                    return false;
                }

                return TargetKind == QueryTargetKind.Projectile
                    ? HitPart == FPG.Demo.Combat.HitPart.Projectile
                    : HitPart != FPG.Demo.Combat.HitPart.Projectile;
            }
        }
    }

    public readonly struct AttackQueryResult
    {
        public AttackQueryResult(
            int candidateCount,
            int droppedCandidateCount)
        {
            if (candidateCount < 0 || droppedCandidateCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(candidateCount));
            }

            CandidateCount = candidateCount;
            DroppedCandidateCount = droppedCandidateCount;
        }

        public int CandidateCount { get; }
        public int DroppedCandidateCount { get; }

        public static AttackQueryResult Empty => new AttackQueryResult(0, 0);
    }
}
