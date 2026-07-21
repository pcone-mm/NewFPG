using System;
using FPG.Demo.Core;

namespace FPG.Demo.Combat
{
    public enum Team
    {
        Neutral = 0,
        Player,
        Enemy
    }

    public enum CombatantKind
    {
        Player = 0,
        Enemy
    }

    public enum ExposureMode
    {
        Exposed = 0,
        Withdrawn
    }

    public enum DamageChannel
    {
        None = 0,
        Life,
        Barrier,
        ProjectileHp
    }

    public enum HitPart
    {
        Body = 0,
        Weakpoint,
        Projectile
    }

    public enum DamageType
    {
        Normal = 0,
        Explosive,
        ProjectileIntercept
    }

    [Flags]
    public enum CombatTags
    {
        None = 0,
        Primary = 1 << 0,
        Secondary = 1 << 1,
        EnemyAttack = 1 << 2,
        Generated = 1 << 3
    }

    public enum QueryPolicy
    {
        None = 0,
        PelletRays,
        DirectThenArea,
        TimedImpact
    }

    public readonly struct DamageSpec
    {
        public const int BasisPoints = 10000;

        public DamageSpec(
            int baseDamage,
            int breakDamage,
            int weakpointDamageMultiplierBasisPoints = BasisPoints,
            int weakpointBreakMultiplierBasisPoints = BasisPoints)
        {
            if (baseDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDamage));
            }

            if (breakDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(breakDamage));
            }

            if (weakpointDamageMultiplierBasisPoints < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(weakpointDamageMultiplierBasisPoints));
            }

            if (weakpointBreakMultiplierBasisPoints < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(weakpointBreakMultiplierBasisPoints));
            }

            BaseDamage = baseDamage;
            BreakDamage = breakDamage;
            WeakpointDamageMultiplierBasisPoints = weakpointDamageMultiplierBasisPoints;
            WeakpointBreakMultiplierBasisPoints = weakpointBreakMultiplierBasisPoints;
        }

        public int BaseDamage { get; }

        public int BreakDamage { get; }

        public int WeakpointDamageMultiplierBasisPoints { get; }

        public int WeakpointBreakMultiplierBasisPoints { get; }
    }

    public readonly struct AttackSnapshot
    {
        public AttackSnapshot(
            AttackId attackId,
            ShotId shotId,
            int definitionId,
            RuntimeId ownerId,
            Team team,
            TickIndex releaseTick,
            DamageSpec damageSpec,
            QueryPolicy queryPolicy,
            int payloadCount,
            int maxImpactCount,
            int ammoCost,
            int rngVersion)
        {
            AttackId = attackId;
            ShotId = shotId;
            DefinitionId = definitionId;
            OwnerId = ownerId;
            Team = team;
            ReleaseTick = releaseTick;
            DamageSpec = damageSpec;
            QueryPolicy = queryPolicy;
            PayloadCount = payloadCount;
            MaxImpactCount = maxImpactCount;
            AmmoCost = ammoCost;
            RngVersion = rngVersion;
        }

        public AttackId AttackId { get; }
        public ShotId ShotId { get; }
        public int DefinitionId { get; }
        public RuntimeId OwnerId { get; }
        public Team Team { get; }
        public TickIndex ReleaseTick { get; }
        public DamageSpec DamageSpec { get; }
        public QueryPolicy QueryPolicy { get; }
        public int PayloadCount { get; }
        public int MaxImpactCount { get; }
        public int AmmoCost { get; }
        public int RngVersion { get; }
    }

    public readonly struct PelletSample
    {
        public PelletSample(ShotId shotId, int pelletIndex, int spreadU24, int spreadV24)
        {
            ShotId = shotId;
            PelletIndex = pelletIndex;
            SpreadU24 = spreadU24;
            SpreadV24 = spreadV24;
        }

        public ShotId ShotId { get; }
        public int PelletIndex { get; }
        public int SpreadU24 { get; }
        public int SpreadV24 { get; }
    }

    public readonly struct DefenseSnapshot
    {
        public DefenseSnapshot(
            ExposureMode exposure,
            TickIndex withdrawnSinceTick,
            TickDuration perfectWindow,
            int perfectBarrierMultiplierBasisPoints,
            TickDuration barrierLockDuration,
            int barrierRestoreBasisPoints)
        {
            Exposure = exposure;
            WithdrawnSinceTick = withdrawnSinceTick;
            PerfectWindow = perfectWindow;
            PerfectBarrierMultiplierBasisPoints = perfectBarrierMultiplierBasisPoints;
            BarrierLockDuration = barrierLockDuration;
            BarrierRestoreBasisPoints = barrierRestoreBasisPoints;
        }

        public ExposureMode Exposure { get; }
        public TickIndex WithdrawnSinceTick { get; }
        public TickDuration PerfectWindow { get; }
        public int PerfectBarrierMultiplierBasisPoints { get; }
        public TickDuration BarrierLockDuration { get; }
        public int BarrierRestoreBasisPoints { get; }

        public static DefenseSnapshot Exposed => new DefenseSnapshot(
            ExposureMode.Exposed,
            TickIndex.Invalid,
            TickDuration.Zero,
            DamageSpec.BasisPoints,
            TickDuration.Zero,
            DamageSpec.BasisPoints);
    }

    public readonly struct ImpactIntent
    {
        public ImpactIntent(
            ImpactId impactId,
            AttackId attackId,
            ShotId shotId,
            RuntimeId sourceId,
            RuntimeId targetId,
            TickIndex impactTick,
            DamageSpec damageSpec,
            HitPart hitPart,
            DamageType damageType,
            CombatTags tags,
            int pelletIndex = -1,
            int impactOrdinal = -1)
        {
            ImpactId = impactId;
            AttackId = attackId;
            ShotId = shotId;
            SourceId = sourceId;
            TargetId = targetId;
            ImpactTick = impactTick;
            DamageSpec = damageSpec;
            HitPart = hitPart;
            DamageType = damageType;
            Tags = tags;
            PelletIndex = pelletIndex;
            ImpactOrdinal = impactOrdinal;
        }

        public ImpactId ImpactId { get; }
        public AttackId AttackId { get; }
        public ShotId ShotId { get; }
        public RuntimeId SourceId { get; }
        public RuntimeId TargetId { get; }
        public TickIndex ImpactTick { get; }
        public DamageSpec DamageSpec { get; }
        public HitPart HitPart { get; }
        public DamageType DamageType { get; }
        public CombatTags Tags { get; }
        public int PelletIndex { get; }
        public int ImpactOrdinal { get; }
    }

    public readonly struct DamagePacket
    {
        public DamagePacket(
            ImpactId impactId,
            DamageChannel channel,
            int appliedAmount,
            int appliedBreakAmount,
            int valueBefore,
            int valueAfter)
        {
            ImpactId = impactId;
            Channel = channel;
            AppliedAmount = appliedAmount;
            AppliedBreakAmount = appliedBreakAmount;
            ValueBefore = valueBefore;
            ValueAfter = valueAfter;
        }

        public ImpactId ImpactId { get; }
        public DamageChannel Channel { get; }
        public int AppliedAmount { get; }
        public int AppliedBreakAmount { get; }
        public int ValueBefore { get; }
        public int ValueAfter { get; }
    }

    public readonly struct ImpactResolution
    {
        private ImpactResolution(
            DomainResult result,
            DamagePacket packet,
            bool perfectRetract,
            bool barrierBroken,
            bool breakTriggered,
            bool death,
            bool projectileDestroyed)
        {
            Result = result;
            Packet = packet;
            PerfectRetract = perfectRetract;
            BarrierBroken = barrierBroken;
            BreakTriggered = breakTriggered;
            Death = death;
            ProjectileDestroyed = projectileDestroyed;
        }

        public DomainResult Result { get; }
        public DamagePacket Packet { get; }
        public bool PerfectRetract { get; }
        public bool BarrierBroken { get; }
        public bool BreakTriggered { get; }
        public bool Death { get; }
        public bool ProjectileDestroyed { get; }

        public static ImpactResolution Accepted(
            DamagePacket packet,
            bool perfectRetract,
            bool barrierBroken,
            bool breakTriggered,
            bool death,
            bool projectileDestroyed)
        {
            return new ImpactResolution(
                DomainResult.Success,
                packet,
                perfectRetract,
                barrierBroken,
                breakTriggered,
                death,
                projectileDestroyed);
        }

        public static ImpactResolution Rejected(RejectReason reason)
        {
            return new ImpactResolution(
                DomainResult.Rejected(reason),
                default(DamagePacket),
                false,
                false,
                false,
                false,
                false);
        }
    }
}
