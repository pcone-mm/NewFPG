using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;

namespace FPG.Demo.Enemy
{
    public enum ThreatPayloadKind
    {
        SweptProjectile = 0,
        TimedImpact
    }

    public enum ThreatTargetPolicy
    {
        PlayerCombatant = 0
    }

    public enum ThreatRetryPolicy
    {
        HoldPendingNextTick = 0
    }

    public readonly struct ThreatPayloadDefinition
    {
        private ThreatPayloadDefinition(
            ThreatPayloadKind kind,
            ProjectileDefinition projectileDefinition,
            DamageSpec timedImpactDamage,
            ThreatTargetPolicy targetPolicy,
            TickDuration impactDelay,
            int payloadCount,
            int presentationKey,
            int totalBudgetUnits)
        {
            Kind = kind;
            ProjectileDefinition = projectileDefinition;
            TimedImpactDamage = timedImpactDamage;
            TargetPolicy = targetPolicy;
            ImpactDelay = impactDelay;
            PayloadCount = payloadCount;
            PresentationKey = presentationKey;
            TotalBudgetUnits = totalBudgetUnits;
        }

        public ThreatPayloadKind Kind { get; }
        public ProjectileDefinition ProjectileDefinition { get; }
        public DamageSpec TimedImpactDamage { get; }
        public ThreatTargetPolicy TargetPolicy { get; }
        public TickDuration ImpactDelay { get; }
        public int PayloadCount { get; }
        public int PresentationKey { get; }
        public bool IsSweptProjectile => Kind == ThreatPayloadKind.SweptProjectile;
        public bool IsTimedImpact => Kind == ThreatPayloadKind.TimedImpact;
        public int TotalBudgetUnits { get; }
        public bool IsValid => Enum.IsDefined(typeof(ThreatPayloadKind), Kind)
            && PayloadCount > 0
            && PresentationKey > 0
            && (IsSweptProjectile ? ProjectileDefinition.DefinitionId > 0 : Enum.IsDefined(typeof(ThreatTargetPolicy), TargetPolicy));

        public static ThreatPayloadDefinition SweptProjectile(ProjectileDefinition projectileDefinition, int payloadCount)
        {
            if (payloadCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(payloadCount));
            }

            int totalBudgetUnits = checked(projectileDefinition.BudgetUnits * payloadCount);

            return new ThreatPayloadDefinition(
                ThreatPayloadKind.SweptProjectile,
                projectileDefinition,
                default(DamageSpec),
                ThreatTargetPolicy.PlayerCombatant,
                TickDuration.Zero,
                payloadCount,
                projectileDefinition.PresentationKey,
                totalBudgetUnits);
        }

        public static ThreatPayloadDefinition TimedImpact(
            DamageSpec damage,
            ThreatTargetPolicy targetPolicy,
            TickDuration impactDelay,
            int presentationKey)
        {
            if (!Enum.IsDefined(typeof(ThreatTargetPolicy), targetPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(targetPolicy));
            }

            if (presentationKey <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(presentationKey));
            }

            return new ThreatPayloadDefinition(
                ThreatPayloadKind.TimedImpact,
                default(ProjectileDefinition),
                damage,
                targetPolicy,
                impactDelay,
                1,
                presentationKey,
                0);
        }

        public ulong AppendStableHash(ulong hash)
        {
            hash = StableHash.Append(hash, (ulong)Kind);
            hash = StableHash.Append(hash, unchecked((ulong)PayloadCount));
            hash = StableHash.Append(hash, unchecked((ulong)PresentationKey));
            hash = StableHash.Append(hash, unchecked((ulong)ImpactDelay.Value));
            hash = StableHash.Append(hash, (ulong)TargetPolicy);
            hash = StableHash.Append(hash, unchecked((ulong)TimedImpactDamage.BaseDamage));
            hash = StableHash.Append(hash, unchecked((ulong)TimedImpactDamage.BreakDamage));
            hash = StableHash.Append(hash, unchecked((ulong)TimedImpactDamage.WeakpointDamageMultiplierBasisPoints));
            hash = StableHash.Append(hash, unchecked((ulong)TimedImpactDamage.WeakpointBreakMultiplierBasisPoints));
            if (IsSweptProjectile)
            {
                hash = StableHash.Append(hash, unchecked((ulong)ProjectileDefinition.DefinitionId));
                hash = StableHash.Append(hash, unchecked((ulong)ProjectileDefinition.FlightDuration.Value));
                hash = StableHash.Append(hash, unchecked((ulong)ProjectileDefinition.ExpireDuration.Value));
                hash = StableHash.Append(hash, unchecked((ulong)ProjectileDefinition.DamageSpec.BaseDamage));
                hash = StableHash.Append(hash, unchecked((ulong)ProjectileDefinition.DamageSpec.BreakDamage));
                hash = StableHash.Append(hash, unchecked((ulong)ProjectileDefinition.DamageSpec.WeakpointDamageMultiplierBasisPoints));
                hash = StableHash.Append(hash, unchecked((ulong)ProjectileDefinition.DamageSpec.WeakpointBreakMultiplierBasisPoints));
                hash = StableHash.Append(hash, unchecked((ulong)ProjectileDefinition.MaxHitPoints));
                hash = StableHash.Append(hash, ProjectileDefinition.Interceptable ? 1UL : 0UL);
                hash = StableHash.Append(hash, unchecked((ulong)ProjectileDefinition.BudgetUnits));
                hash = StableHash.Append(hash, unchecked((ulong)ProjectileDefinition.PresentationKey));
                hash = StableHash.Append(hash, unchecked((ulong)ProjectileDefinition.SweepRadiusKey));
            }

            return hash;
        }
    }
}
