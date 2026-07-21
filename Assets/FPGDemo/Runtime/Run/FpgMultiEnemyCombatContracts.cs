using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;

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
            int maxSummonRecursionDepth)
        {
            if (enemyCapacity <= 0
                || playerHitCommandCapacity <= 0
                || attackScheduleCapacity <= 0
                || projectileCapacity <= 0
                || threatAdvanceCapacity <= 0
                || perEnemyThreatCapacity <= 0
                || summonCapacity <= 0
                || maxTotalSummons < 0
                || maxSummonRecursionDepth < 0)
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

            CommandSequence = commandSequence;
            Intent = intent;
            Priority = priority;
        }

        public long CommandSequence { get; }
        public ImpactIntent Intent { get; }
        public ImpactPhasePriority Priority { get; }
    }

    public enum FpgEnemyAttackPayloadKind
    {
        Threat = 0,
        Summon
    }

    public readonly struct FpgFormalSummonPayload
    {
        public FpgFormalSummonPayload(FpgSummonRequest request, int maxSummonsPerOwner)
        {
            if (!request.OwnerRuntimeId.IsValid
                || string.IsNullOrWhiteSpace(request.EnemyDefinitionId)
                || request.RecursionDepth < 0
                || request.RequestSequence < 0
                || maxSummonsPerOwner <= 0)
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
                    maxSummonsPerOwner);
            MaxSummonsPerOwner = maxSummonsPerOwner;
        }

        public FpgSummonRequest Request { get; }
        public int MaxSummonsPerOwner { get; }
        public bool IsValid => Request.OwnerRuntimeId.IsValid
            && !string.IsNullOrWhiteSpace(Request.EnemyDefinitionId)
            && Request.RecursionDepth >= 0
            && Request.RequestSequence >= 0
            && Request.MaxSummonsPerOwner == MaxSummonsPerOwner
            && MaxSummonsPerOwner > 0;
    }

    /// <summary>
    /// Immutable attack payload submitted by the Unity definition adapter.
    /// Threat payloads reuse the existing ThreatRuntime state machine; summon
    /// payloads use the same generic FpgSummonRequest for every enemy identity.
    /// </summary>
    public readonly struct FpgEnemyAttackPayload
    {
        private FpgEnemyAttackPayload(
            FpgEnemyAttackPayloadKind kind,
            ThreatDefinition threat,
            FpgFormalSummonPayload summon)
        {
            Kind = kind;
            Threat = threat;
            Summon = summon;
        }

        public FpgEnemyAttackPayloadKind Kind { get; }
        public ThreatDefinition Threat { get; }
        public FpgFormalSummonPayload Summon { get; }

        public bool IsValid => Kind == FpgEnemyAttackPayloadKind.Threat
            ? Threat.DefinitionId > 0 && Threat.Payload.IsValid
            : Kind == FpgEnemyAttackPayloadKind.Summon && Summon.IsValid;

        public static FpgEnemyAttackPayload ForThreat(ThreatDefinition threat)
        {
            if (threat.DefinitionId <= 0 || !threat.Payload.IsValid)
            {
                throw new ArgumentException("Formal threat payload is invalid.", nameof(threat));
            }

            return new FpgEnemyAttackPayload(
                FpgEnemyAttackPayloadKind.Threat,
                threat,
                default(FpgFormalSummonPayload));
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
                summon);
        }
    }

    public readonly struct FpgEnemyAttackCommand
    {
        public FpgEnemyAttackCommand(
            FpgAttackScheduleRequest schedule,
            int spawnSequence,
            FpgEnemyAttackPayload payload)
        {
            if (!schedule.OwnerRuntimeId.IsValid
                || !schedule.ReadyTick.IsValid
                || schedule.ScheduleSequence < 0
                || spawnSequence < 0
                || !payload.IsValid
                || (payload.Kind == FpgEnemyAttackPayloadKind.Summon
                    && payload.Summon.Request.OwnerRuntimeId != schedule.OwnerRuntimeId))
            {
                throw new ArgumentException("Formal enemy attack command is invalid.", nameof(schedule));
            }

            Schedule = schedule;
            SpawnSequence = spawnSequence;
            Payload = payload;
        }

        public FpgAttackScheduleRequest Schedule { get; }
        public int SpawnSequence { get; }
        public FpgEnemyAttackPayload Payload { get; }
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
