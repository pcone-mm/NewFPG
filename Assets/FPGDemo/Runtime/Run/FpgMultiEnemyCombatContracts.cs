using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Skills;

namespace FPG.Demo.Run
{
    /// <summary>
    /// Fixed capacities for the formal multi-enemy combat port. Every hot-path
    /// buffer is allocated by the port constructor and never resized.
    /// </summary>
    public readonly struct FpgMultiEnemyCombatCapacity
    {
        public FpgMultiEnemyCombatCapacity(
            int enemyCapacity,
            int playerHitCommandCapacity,
            int attackScheduleCapacity,
            int projectileCapacity,
            int threatAdvanceCapacity,
            int perEnemyThreatCapacity,
            int summonCapacity,
            int maxTotalSummons,
            int maxSummonRecursionDepth,
            int vitalsEventCapacity = 128,
            int damageFeedbackCapacity = 128,
            int skillImpactPresentationCapacity = 128)
        {
            if (enemyCapacity <= 0
                || playerHitCommandCapacity <= 0
                || attackScheduleCapacity <= 0
                || projectileCapacity <= 0
                || threatAdvanceCapacity <= 0
                || perEnemyThreatCapacity <= 0
                || summonCapacity <= 0
                || maxTotalSummons < 0
                || maxSummonRecursionDepth < 0
                || vitalsEventCapacity <= 0
                || damageFeedbackCapacity <= 0
                || skillImpactPresentationCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyCapacity));
            }

            EnemyCapacity = enemyCapacity;
            PlayerHitCommandCapacity = playerHitCommandCapacity;
            AttackScheduleCapacity = attackScheduleCapacity;
            ProjectileCapacity = projectileCapacity;
            ThreatAdvanceCapacity = threatAdvanceCapacity;
            PerEnemyThreatCapacity = perEnemyThreatCapacity;
            SummonCapacity = summonCapacity;
            MaxTotalSummons = maxTotalSummons;
            MaxSummonRecursionDepth = maxSummonRecursionDepth;
            VitalsEventCapacity = vitalsEventCapacity;
            DamageFeedbackCapacity = damageFeedbackCapacity;
            SkillImpactPresentationCapacity =
                skillImpactPresentationCapacity;
        }

        public int EnemyCapacity { get; }
        public int PlayerHitCommandCapacity { get; }
        public int AttackScheduleCapacity { get; }
        public int ProjectileCapacity { get; }
        public int ThreatAdvanceCapacity { get; }
        public int PerEnemyThreatCapacity { get; }
        public int SummonCapacity { get; }
        public int MaxTotalSummons { get; }
        public int MaxSummonRecursionDepth { get; }
        public int VitalsEventCapacity { get; }
        public int DamageFeedbackCapacity { get; }
        public int SkillImpactPresentationCapacity { get; }
    }

    /// <summary>
    /// Defense tuning needed to project PlayerRuntime exposure into the shared
    /// DamageResolver without coupling the formal port to a room profile asset.
    /// </summary>
    public readonly struct FpgPlayerDefensePolicy
    {
        public FpgPlayerDefensePolicy(
            TickDuration perfectRetractWindow,
            int perfectRetractMultiplierBasisPoints,
            TickDuration barrierLockDuration,
            int barrierRestoreBasisPoints)
        {
            if (perfectRetractMultiplierBasisPoints < 0 || barrierRestoreBasisPoints < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(perfectRetractMultiplierBasisPoints));
            }

            PerfectRetractWindow = perfectRetractWindow;
            PerfectRetractMultiplierBasisPoints = perfectRetractMultiplierBasisPoints;
            BarrierLockDuration = barrierLockDuration;
            BarrierRestoreBasisPoints = barrierRestoreBasisPoints;
        }

        public TickDuration PerfectRetractWindow { get; }
        public int PerfectRetractMultiplierBasisPoints { get; }
        public TickDuration BarrierLockDuration { get; }
        public int BarrierRestoreBasisPoints { get; }

        public static FpgPlayerDefensePolicy Default => new FpgPlayerDefensePolicy(
            TickDuration.Zero,
            DamageSpec.BasisPoints,
            TickDuration.Zero,
            DamageSpec.BasisPoints);

        internal DefenseSnapshot CreateSnapshot(PlayerRuntime player)
        {
            return player.Exposure.CreateDefenseSnapshot(
                PerfectRetractWindow,
                PerfectRetractMultiplierBasisPoints,
                BarrierLockDuration,
                BarrierRestoreBasisPoints);
        }
    }

    /// <summary>
    /// One already-resolved player hit. Geometry and target selection remain in
    /// the adapter; the authoritative command contains RuntimeId identities.
    /// </summary>
    public readonly struct FpgPlayerHitCommand
    {
        public FpgPlayerHitCommand(
            long commandSequence,
            ImpactIntent intent,
            ImpactPhasePriority priority = ImpactPhasePriority.PlayerCombatantHit)
            : this(
                commandSequence,
                intent,
                SkillExecutionId.Invalid,
                0,
                priority)
        {
        }

        public FpgPlayerHitCommand(
            long commandSequence,
            ImpactIntent intent,
            SkillExecutionId skillExecutionId,
            int gameplayEventId,
            ImpactPhasePriority priority = ImpactPhasePriority.PlayerCombatantHit)
        {
            if (commandSequence < 0
                || !intent.ImpactId.IsValid
                || !intent.AttackId.IsValid
                || !intent.SourceId.IsValid
                || !intent.TargetId.IsValid
                || !intent.ImpactTick.IsValid
                || (priority != ImpactPhasePriority.PlayerCombatantHit
                    && priority != ImpactPhasePriority.PlayerProjectileIntercept))
            {
                throw new ArgumentException("Formal player hit command is invalid.", nameof(intent));
            }

            if (gameplayEventId < 0
                || skillExecutionId.IsValid != (gameplayEventId > 0))
            {
                throw new ArgumentException(
                    "Formal player hit correlation requires both a valid skill execution and positive gameplay event ID.",
                    nameof(gameplayEventId));
            }

            CommandSequence = commandSequence;
            Intent = intent;
            SkillExecutionId = skillExecutionId;
            GameplayEventId = gameplayEventId;
            Priority = priority;
        }

        public long CommandSequence { get; }
        public ImpactIntent Intent { get; }
        public SkillExecutionId SkillExecutionId { get; }
        public int GameplayEventId { get; }
        public bool HasSkillCorrelation => SkillExecutionId.IsValid;
        public ImpactPhasePriority Priority { get; }
    }

    /// <summary>
    /// One player-owned projectile that resolves its authored area attack only
    /// when its world sweep reaches a target, blocker, or range endpoint.
    /// </summary>
    public readonly struct FpgPlayerAreaProjectileRequest
    {
        public FpgPlayerAreaProjectileRequest(
            TickIndex tick,
            AttackSnapshot attack,
            ProjectileDefinition definition,
            SpatialVectorKey start,
            SpatialVectorKey end,
            SkillExecutionId skillExecutionId,
            int gameplayEventId)
        {
            if (!tick.IsValid
                || !attack.AttackId.IsValid
                || !attack.ShotId.IsValid
                || !attack.OwnerId.IsValid
                || attack.Team != Team.Player
                || attack.ReleaseTick != tick
                || !attack.IsQueryConfigurationValid
                || attack.QueryPolicy != QueryPolicy.DirectThenArea
                || attack.QueryMode != AttackQueryMode.AreaAtFirstSurface
                || attack.PayloadCount != 1
                || attack.MaxImpactCount <= 0
                || definition.DefinitionId <= 0
                || definition.FlightDuration.Value <= 0
                || definition.ExpireDuration.Value < definition.FlightDuration.Value
                || definition.Interceptable
                || definition.MaxHitPoints != 0
                || definition.BudgetUnits <= 0
                || definition.SweepRadiusKey <= 0
                || start == end
                || !skillExecutionId.IsValid
                || gameplayEventId <= 0)
            {
                throw new ArgumentException(
                    "Player area projectile request is invalid.");
            }

            Tick = tick;
            Attack = attack;
            Definition = definition;
            Start = start;
            End = end;
            SkillExecutionId = skillExecutionId;
            GameplayEventId = gameplayEventId;
        }

        public TickIndex Tick { get; }
        public AttackSnapshot Attack { get; }
        public ProjectileDefinition Definition { get; }
        public SpatialVectorKey Start { get; }
        public SpatialVectorKey End { get; }
        public SkillExecutionId SkillExecutionId { get; }
        public int GameplayEventId { get; }
    }

    public enum FpgEnemyAttackPayloadKind
    {
        Threat = 0,
        Summon,
        SelfDestructOwner
    }

    /// <summary>
    /// Presentation-only notice emitted after an authored enemy attack has
    /// actually started. A delayed summon enters SpawnQueue at its release tick.
    /// </summary>
    public readonly struct FpgEnemyAttackStartedEvent
    {
        public FpgEnemyAttackStartedEvent(
            RuntimeId ownerRuntimeId,
            int spawnSequence,
            string attackPatternId,
            TickIndex tick,
            long scheduleSequence,
            FpgEnemyAttackPayloadKind payloadKind)
        {
            if (!ownerRuntimeId.IsValid
                || spawnSequence < 0
                || string.IsNullOrWhiteSpace(attackPatternId)
                || !tick.IsValid
                || scheduleSequence < 0
                || !Enum.IsDefined(typeof(FpgEnemyAttackPayloadKind), payloadKind))
            {
                throw new ArgumentException(
                    "Formal enemy attack presentation event is invalid.");
            }

            OwnerRuntimeId = ownerRuntimeId;
            SpawnSequence = spawnSequence;
            AttackPatternId = attackPatternId;
            Tick = tick;
            ScheduleSequence = scheduleSequence;
            PayloadKind = payloadKind;
        }

        public RuntimeId OwnerRuntimeId { get; }
        public int SpawnSequence { get; }
        public string AttackPatternId { get; }
        public TickIndex Tick { get; }
        public long ScheduleSequence { get; }
        public FpgEnemyAttackPayloadKind PayloadKind { get; }
    }

    public readonly struct FpgFormalSummonPayload
    {
        public FpgFormalSummonPayload(
            FpgSummonRequest request,
            int maxSummonsPerOwner,
            int releaseDelayTicks = 0)
        {
            if (!request.IsValid
                || maxSummonsPerOwner < 0
                || (request.OccupancyMode == FpgSummonOccupancyMode.AdditionalEntity
                    && maxSummonsPerOwner <= 0)
                || releaseDelayTicks < 0)
            {
                throw new ArgumentException("Formal summon payload is invalid.", nameof(request));
            }

            Request = request.MaxSummonsPerOwner == maxSummonsPerOwner
                ? request
                : new FpgSummonRequest(
                    request.OwnerRuntimeId,
                    request.EnemyDefinitionId,
                    request.RecursionDepth,
                    request.RequestSequence,
                    request.SummonActionId,
                    maxSummonsPerOwner,
                    request.OccupancyMode,
                    request.PlacementMode);
            MaxSummonsPerOwner = maxSummonsPerOwner;
            ReleaseDelayTicks = releaseDelayTicks;
        }

        public FpgSummonRequest Request { get; }
        public int MaxSummonsPerOwner { get; }
        public int ReleaseDelayTicks { get; }
        public bool IsValid => Request.IsValid
            && Request.MaxSummonsPerOwner == MaxSummonsPerOwner
            && MaxSummonsPerOwner >= 0
            && (Request.OccupancyMode != FpgSummonOccupancyMode.AdditionalEntity
                || MaxSummonsPerOwner > 0)
            && ReleaseDelayTicks >= 0;
    }

    /// <summary>
    /// Immutable attack payload submitted by the Unity definition adapter.
    /// Threat payloads reuse the existing ThreatRuntime state machine; summon
    /// payloads use the same generic FpgSummonRequest for every enemy identity,
    /// while owner self-destruct optionally binds to a summon schedule.
    /// </summary>
    public readonly struct FpgEnemyAttackPayload
    {
        private FpgEnemyAttackPayload(
            FpgEnemyAttackPayloadKind kind,
            ThreatDefinition threat,
            FpgFormalSummonPayload summon,
            long selfDestructDependencyScheduleSequence)
        {
            Kind = kind;
            Threat = threat;
            Summon = summon;
            SelfDestructDependencyScheduleSequence =
                selfDestructDependencyScheduleSequence;
        }

        public FpgEnemyAttackPayloadKind Kind { get; }
        public ThreatDefinition Threat { get; }
        public FpgFormalSummonPayload Summon { get; }
        public long SelfDestructDependencyScheduleSequence { get; }
        public bool HasSelfDestructDependency =>
            Kind == FpgEnemyAttackPayloadKind.SelfDestructOwner
            && SelfDestructDependencyScheduleSequence >= 0L;

        public bool IsValid
        {
            get
            {
                switch (Kind)
                {
                    case FpgEnemyAttackPayloadKind.Threat:
                        return Threat.DefinitionId > 0
                            && Threat.Payload.IsValid;

                    case FpgEnemyAttackPayloadKind.Summon:
                        return Summon.IsValid;

                    case FpgEnemyAttackPayloadKind.SelfDestructOwner:
                        return SelfDestructDependencyScheduleSequence
                            >= -1L;

                    default:
                        return false;
                }
            }
        }

        public static FpgEnemyAttackPayload ForThreat(ThreatDefinition threat)
        {
            if (threat.DefinitionId <= 0 || !threat.Payload.IsValid)
            {
                throw new ArgumentException("Formal threat payload is invalid.", nameof(threat));
            }

            return new FpgEnemyAttackPayload(
                FpgEnemyAttackPayloadKind.Threat,
                threat,
                default(FpgFormalSummonPayload),
                -1L);
        }

        public static FpgEnemyAttackPayload ForSummon(FpgFormalSummonPayload summon)
        {
            if (!summon.IsValid)
            {
                throw new ArgumentException("Formal summon payload is invalid.", nameof(summon));
            }

            return new FpgEnemyAttackPayload(
                FpgEnemyAttackPayloadKind.Summon,
                default(ThreatDefinition),
                summon,
                -1L);
        }

        public static FpgEnemyAttackPayload ForSelfDestructOwner(
            long dependencyScheduleSequence)
        {
            if (dependencyScheduleSequence < -1L)
            {
                throw new ArgumentException(
                    "Formal self-destruct dependency is invalid.",
                    nameof(dependencyScheduleSequence));
            }

            return new FpgEnemyAttackPayload(
                FpgEnemyAttackPayloadKind.SelfDestructOwner,
                default(ThreatDefinition),
                default(FpgFormalSummonPayload),
                dependencyScheduleSequence);
        }
    }

    /// <summary>
    /// Spatial metadata and the event-tick snapshot used by one formal enemy
    /// threat. Authored metadata stays available for trace/replay while the
    /// resolved points are the path contract consumed by combat.
    /// </summary>
    public readonly struct FpgEnemyAttackSpatialContext
    {
        public FpgEnemyAttackSpatialContext(
            TickIndex sampleTick,
            FpgSkillTargetSource targetSource,
            int socketId,
            FpgSkillOffset offset,
            RuntimeId targetRuntimeId,
            SpatialVectorKey origin,
            SpatialVectorKey target)
        {
            if (!sampleTick.IsValid
                || !Enum.IsDefined(typeof(FpgSkillTargetSource), targetSource)
                || targetSource == FpgSkillTargetSource.None
                || socketId < 0
                || !targetRuntimeId.IsValid)
            {
                throw new ArgumentException(
                    "Formal enemy attack spatial context is invalid.",
                    nameof(sampleTick));
            }

            SampleTick = sampleTick;
            TargetSource = targetSource;
            SocketId = socketId;
            Offset = offset;
            TargetRuntimeId = targetRuntimeId;
            Origin = origin;
            Target = target;
        }

        public TickIndex SampleTick { get; }
        public FpgSkillTargetSource TargetSource { get; }
        public int SocketId { get; }
        public FpgSkillOffset Offset { get; }
        public RuntimeId TargetRuntimeId { get; }
        public SpatialVectorKey Origin { get; }
        public SpatialVectorKey Target { get; }
        public bool IsValid => SampleTick.IsValid
            && Enum.IsDefined(typeof(FpgSkillTargetSource), TargetSource)
            && TargetSource != FpgSkillTargetSource.None
            && SocketId >= 0
            && TargetRuntimeId.IsValid;
    }

    /// <summary>
    /// Opaque fixed-capacity reservation owned by one formal enemy skill
    /// execution. The combat port consumes one slice for every submitted
    /// gameplay event and releases any untouched remainder on interruption.
    /// </summary>
    public readonly struct FpgEnemySkillCapacityReservation :
        IEquatable<FpgEnemySkillCapacityReservation>
    {
        public static readonly FpgEnemySkillCapacityReservation Invalid =
            new FpgEnemySkillCapacityReservation(0L);

        internal FpgEnemySkillCapacityReservation(long value)
        {
            Value = value;
        }

        public long Value { get; }
        public bool IsValid => Value > 0L;

        public bool Equals(FpgEnemySkillCapacityReservation other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is FpgEnemySkillCapacityReservation other
                && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((int)(Value ^ (Value >> 32)));
        }

        public static bool operator ==(
            FpgEnemySkillCapacityReservation left,
            FpgEnemySkillCapacityReservation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            FpgEnemySkillCapacityReservation left,
            FpgEnemySkillCapacityReservation right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct FpgEnemyAttackCommand
    {

        public FpgEnemyAttackCommand(
            FpgAttackScheduleRequest schedule,
            int spawnSequence,
            FpgEnemyAttackPayload payload,
            FpgEnemySkillCapacityReservation capacityReservation,
            ReservationToken projectileBudgetReservation,
            FpgEnemyAttackSpatialContext spatialContext =
                default(FpgEnemyAttackSpatialContext))
        {
            if (!schedule.OwnerRuntimeId.IsValid
                || !schedule.ReadyTick.IsValid
                || schedule.ScheduleSequence < 0
                || spawnSequence < 0
                || !payload.IsValid
                || (payload.Kind != FpgEnemyAttackPayloadKind.SelfDestructOwner
                    && !spatialContext.IsValid)
                || (payload.Kind == FpgEnemyAttackPayloadKind.Summon
                    && payload.Summon.Request.OwnerRuntimeId != schedule.OwnerRuntimeId))
            {
                throw new ArgumentException("Formal enemy attack command is invalid.", nameof(schedule));
            }

            if (projectileBudgetReservation.IsValid
                && (payload.Kind != FpgEnemyAttackPayloadKind.Threat
                    || !payload.Threat.Payload.IsSweptProjectile
                    || !capacityReservation.IsValid))
            {
                throw new ArgumentException(
                    "Pre-reserved projectile budget requires a formal projectile event.",
                    nameof(projectileBudgetReservation));
            }

            Schedule = schedule;
            SpawnSequence = spawnSequence;
            Payload = payload;
            CapacityReservation = capacityReservation;
            ProjectileBudgetReservation = projectileBudgetReservation;
            SpatialContext = spatialContext;
        }

        public FpgAttackScheduleRequest Schedule { get; }
        public int SpawnSequence { get; }
        public FpgEnemyAttackPayload Payload { get; }
        public FpgEnemySkillCapacityReservation CapacityReservation { get; }
        public ReservationToken ProjectileBudgetReservation { get; }
        public FpgEnemyAttackSpatialContext SpatialContext { get; }
        public SkillExecutionId SkillExecutionId => Schedule.SkillExecutionId;
        public int GameplayEventId => Schedule.GameplayEventId;
        public bool HasSkillCorrelation => Schedule.HasSkillCorrelation;
    }

    public readonly struct FpgEnemyCombatantRegistration
    {
        public FpgEnemyCombatantRegistration(
            RuntimeId runtimeId,
            int spawnSequence,
            int life,
            int breakValue,
            TickDuration groggyDuration,
            TickIndex activationTick)
        {
            if (!runtimeId.IsValid
                || spawnSequence < 0
                || life <= 0
                || breakValue < 0
                || groggyDuration.Value <= 0
                || !activationTick.IsValid)
            {
                throw new ArgumentException("Formal enemy registration is invalid.", nameof(runtimeId));
            }

            RuntimeId = runtimeId;
            SpawnSequence = spawnSequence;
            Life = life;
            Break = breakValue;
            GroggyDuration = groggyDuration;
            ActivationTick = activationTick;
        }

        public RuntimeId RuntimeId { get; }
        public int SpawnSequence { get; }
        public int Life { get; }
        public int Break { get; }
        public TickDuration GroggyDuration { get; }
        public TickIndex ActivationTick { get; }
    }

    public readonly struct FpgCombatHealthChangedEvent
    {
        public FpgCombatHealthChangedEvent(
            RuntimeId runtimeId,
            CombatantKind kind,
            TickIndex tick,
            int life,
            int maxLife,
            int breakValue,
            int maxBreak,
            DamagePacket packet,
            bool breakTriggered,
            bool groggy,
            bool dead)
        {
            RuntimeId = runtimeId;
            Kind = kind;
            Tick = tick;
            Life = life;
            MaxLife = maxLife;
            Break = breakValue;
            MaxBreak = maxBreak;
            Packet = packet;
            BreakTriggered = breakTriggered;
            Groggy = groggy;
            Dead = dead;
        }

        public RuntimeId RuntimeId { get; }
        public CombatantKind Kind { get; }
        public TickIndex Tick { get; }
        public int Life { get; }
        public int MaxLife { get; }
        public int Break { get; }
        public int MaxBreak { get; }
        public DamagePacket Packet { get; }
        public bool BreakTriggered { get; }
        public bool Groggy { get; }
        public bool Dead { get; }
    }

    public readonly struct FpgEnemyDiedEvent
    {
        public FpgEnemyDiedEvent(
            RuntimeId runtimeId,
            RuntimeId sourceRuntimeId,
            AttackId attackId,
            TickIndex tick)
        {
            RuntimeId = runtimeId;
            SourceRuntimeId = sourceRuntimeId;
            AttackId = attackId;
            Tick = tick;
        }

        public RuntimeId RuntimeId { get; }
        public RuntimeId SourceRuntimeId { get; }
        public AttackId AttackId { get; }
        public TickIndex Tick { get; }
    }
}
