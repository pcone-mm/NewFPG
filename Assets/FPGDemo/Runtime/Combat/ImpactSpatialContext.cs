using System;
using FPG.Demo.Core;

namespace FPG.Demo.Combat
{
    /// <summary>
    /// Optional deterministic contact data carried by an impact. Query-backed
    /// contacts also carry geometry; authored targeted impacts carry the
    /// sampled fixed-point contact without inventing a geometry identifier.
    /// </summary>
    public readonly struct ImpactSpatialContext
    {
        public ImpactSpatialContext(
            SpatialVectorKey impactPointKey,
            QueryTargetKind targetKind,
            HitPart hitPart)
        {
            if (!IsValidTarget(targetKind, hitPart))
            {
                throw new ArgumentException(
                    "Impact spatial context requires a valid damage target.");
            }

            HasValue = true;
            ImpactPointKey = impactPointKey;
            GeometryId = GeometryId.Invalid;
            TargetKind = targetKind;
            HitPart = hitPart;
        }

        public ImpactSpatialContext(
            SpatialVectorKey impactPointKey,
            GeometryId geometryId,
            QueryTargetKind targetKind,
            HitPart hitPart)
        {
            if (!geometryId.IsValid
                || !IsValidTarget(targetKind, hitPart))
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
        public bool HasGeometry => GeometryId.IsValid;

        public bool IsValid => !HasValue
            || IsValidTarget(TargetKind, HitPart);

        private static bool IsValidTarget(
            QueryTargetKind targetKind,
            HitPart hitPart)
        {
            return Enum.IsDefined(typeof(QueryTargetKind), targetKind)
                && Enum.IsDefined(typeof(HitPart), hitPart)
                && (targetKind == QueryTargetKind.Combatant
                    || targetKind == QueryTargetKind.Projectile)
                && (targetKind == QueryTargetKind.Projectile
                    ? hitPart == HitPart.Projectile
                    : hitPart != HitPart.Projectile);
        }
    }
}
