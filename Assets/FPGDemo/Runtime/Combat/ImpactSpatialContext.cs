using System;
using FPG.Demo.Core;

namespace FPG.Demo.Combat
{
    /// <summary>
    /// Optional, deterministic geometry carried by an impact. Unity adapters
    /// translate the fixed-point key at the presentation boundary.
    /// </summary>
    public readonly struct ImpactSpatialContext
    {
        public ImpactSpatialContext(
            SpatialVectorKey impactPointKey,
            GeometryId geometryId,
            QueryTargetKind targetKind,
            HitPart hitPart)
        {
            if (!geometryId.IsValid
                || !Enum.IsDefined(typeof(QueryTargetKind), targetKind)
                || !Enum.IsDefined(typeof(HitPart), hitPart)
                || targetKind == QueryTargetKind.EnvironmentBlocker
                || (targetKind == QueryTargetKind.Projectile
                    ? hitPart != HitPart.Projectile
                    : hitPart == HitPart.Projectile))
            {
                throw new ArgumentException(
                    "Impact spatial context requires a valid damage target and geometry.");
            }

            HasValue = true;
            ImpactPointKey = impactPointKey;
            GeometryId = geometryId;
            TargetKind = targetKind;
            HitPart = hitPart;
        }

        public bool HasValue { get; }
        public SpatialVectorKey ImpactPointKey { get; }
        public GeometryId GeometryId { get; }
        public QueryTargetKind TargetKind { get; }
        public HitPart HitPart { get; }

        public bool IsValid => !HasValue
            || (GeometryId.IsValid
                && TargetKind != QueryTargetKind.EnvironmentBlocker
                && (TargetKind == QueryTargetKind.Projectile
                    ? HitPart == HitPart.Projectile
                    : HitPart != HitPart.Projectile));
    }
}
