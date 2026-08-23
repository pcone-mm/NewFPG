using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Skills;

namespace FPG.Demo.Run
{
    /// <summary>
    /// Concrete pure-C# combat port for the formal encounter path. It reuses
    /// CombatKernel, PlayerRuntime, EnemyRuntime, ThreatRuntime and
    /// ProjectileRuntime while keeping every collection fixed-capacity and
    /// every combatant lookup keyed by RuntimeId.
    /// </summary>
    public sealed class FpgMultiEnemyCombatPort :
        IFpgEncounterCombatTickPort,
        IFpgAttackOwnerEligibility,
        IFpgAttackScheduleEligibility
    {
        private readonly CombatKernel combatKernel;
        private readonly PlayerRuntime player;
        private readonly SessionIdAllocator idAllocator;
        private readonly FpgMultiEnemyCombatCapacity capacity;
        private readonly TickDuration defaultGroggyDuration;
        private readonly EnemyBinding[] enemies;
        private readonly FpgPlayerHitCommand[] playerHitCommands;
        private readonly FpgPlayerHitCommand[] playerHitDueBuffer;
        private readonly FpgOwnerAwareAttackSchedule attackSchedule;
        private readonly ScheduledPayload[] scheduledPayloads;
        private readonly EnemySkillCapacityReservationEntry[]
            enemySkillCapacityReservations;
        private readonly ProjectileBinding[] projectiles;
        private readonly ThreatAdvanceBinding[] threatAdvanceBuffer;
        private readonly ThreatExecutionBinding[] threatExecutionBindings;
        private readonly QueuedImpact[] dueImpactBuffer;
        private readonly bool[] dueImpactIsProjectile;
        private readonly ImpactId[] projectileOriginImpactIds;
        private readonly IProjectileWorldPort projectileWorldPort;
        private readonly IPlayerProjectileAreaQueryPort playerProjectileAreaQueryPort;
        private readonly IFpgSummonRequestSink summonRequestSink;
        private readonly QueryCandidate[] playerProjectileAreaCandidates;
        private readonly QueryCandidate[] playerProjectileAreaSelected;
        private readonly FixedFpgVitalsStream vitalsStream;
        private readonly FixedResolvedDamageFeedbackStream damageFeedbackStream;
        private FpgCoverRuntime coverRuntime;
        private IFpgCoverGeometryResolver coverGeometryResolver;
        private readonly FixedFpgSkillImpactPresentationStream
            skillImpactPresentationStream;

        private TickIndex currentTick = TickIndex.Invalid;
        private int enemyCount;
        private int playerHitCommandCount;
        private long lastPlayerHitCommandSequence = -1L;
        private long nextEnemySkillCapacityReservation = 1L;

        public FpgMultiEnemyCombatPort(
            CombatKernel combatKernel,
            PlayerRuntime player,
            SessionIdAllocator idAllocator,
            FpgMultiEnemyCombatCapacity capacity,
            TickDuration defaultGroggyDuration,
            IProjectileWorldPort projectileWorldPort,
            IFpgSummonRequestSink summonRequestSink,
            FpgPlayerDefensePolicy? playerDefense = null,
            IPlayerProjectileAreaQueryPort playerProjectileAreaQueryPort = null)
        {
            this.combatKernel = combatKernel ?? throw new ArgumentNullException(nameof(combatKernel));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.idAllocator = idAllocator ?? throw new ArgumentNullException(nameof(idAllocator));
            this.projectileWorldPort = projectileWorldPort
                ?? throw new ArgumentNullException(nameof(projectileWorldPort));
            this.summonRequestSink = summonRequestSink
                ?? throw new ArgumentNullException(nameof(summonRequestSink));
            this.playerProjectileAreaQueryPort = playerProjectileAreaQueryPort
                ?? NullPlayerProjectileAreaQueryPort.Instance;
            if (projectileWorldPort is NullProjectileWorldPort)
            {
                throw new ArgumentException(
                    "Formal combat requires a concrete projectile world port. Tests may pass FpgEmptyProjectileWorldPort explicitly.",
                    nameof(projectileWorldPort));
            }
            if (combatKernel.IsDisposed)
            {
                throw new ArgumentException("Formal combat port requires a live CombatKernel.", nameof(combatKernel));
            }

            if (defaultGroggyDuration.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultGroggyDuration));
            }

            this.capacity = capacity;
            this.defaultGroggyDuration = defaultGroggyDuration;
            enemies = new EnemyBinding[capacity.EnemyCapacity];
            playerHitCommands = new FpgPlayerHitCommand[capacity.PlayerHitCommandCapacity];
            playerHitDueBuffer = new FpgPlayerHitCommand[capacity.PlayerHitCommandCapacity];
            attackSchedule = new FpgOwnerAwareAttackSchedule(capacity.AttackScheduleCapacity);
            scheduledPayloads = new ScheduledPayload[capacity.AttackScheduleCapacity];
            enemySkillCapacityReservations =
                new EnemySkillCapacityReservationEntry[
                    capacity.AttackScheduleCapacity];
            projectiles = new ProjectileBinding[capacity.ProjectileCapacity];
            threatAdvanceBuffer = new ThreatAdvanceBinding[capacity.ThreatAdvanceCapacity];
            threatExecutionBindings = new ThreatExecutionBinding[
                capacity.ThreatAdvanceCapacity];
            dueImpactBuffer = new QueuedImpact[combatKernel.ImpactQueue.Capacity];
            dueImpactIsProjectile = new bool[
                combatKernel.ImpactQueue.Capacity];
            projectileOriginImpactIds = new ImpactId[
                combatKernel.ImpactQueue.Capacity];
            playerProjectileAreaCandidates = new QueryCandidate[
                TargetSelector.DefaultCandidateCapacity];
            playerProjectileAreaSelected = new QueryCandidate[
                TargetSelector.DefaultCandidateCapacity];
            vitalsStream = new FixedFpgVitalsStream(
                capacity.EnemyCapacity + 1,
                capacity.VitalsEventCapacity);
            damageFeedbackStream = new FixedResolvedDamageFeedbackStream(
                capacity.DamageFeedbackCapacity);
            skillImpactPresentationStream =
                new FixedFpgSkillImpactPresentationStream(
                    capacity.SkillImpactPresentationCapacity);
            PublishVitals(
                player.Combatant,
                new TickIndex(0L),
                FpgVitalsChangeReason.Spawn,
                force: true);
        }

        public bool IsPlayerAlive => !player.Combatant.IsDead;
        public bool CanTargetPlayer => IsPlayerAlive
            && (coverRuntime == null || coverRuntime.CanBeTargeted);
        public FpgCoverRuntime Covers => coverRuntime;
        public int EnemyRuntimeCount => enemyCount;
        public int PendingPlayerHitCount => playerHitCommandCount;
        public int PendingAttackCount => attackSchedule.Count;
        public int ActiveProjectileCount => CountActiveProjectiles();
        public int ActiveEnemySkillCapacityReservationCount =>
            CountEnemySkillCapacityReservations();
        public TickIndex CurrentTick => currentTick;
        public CombatKernel CombatKernel => combatKernel;
        public PlayerRuntime Player => player;
        public IFpgVitalsView Vitals => vitalsStream;
        public IFpgResolvedDamageFeedbackView DamageFeedback => damageFeedbackStream;
        public IFpgSkillImpactPresentationView SkillImpactPresentation =>
            skillImpactPresentationStream;
        public int PresentationCallbackFaultCount { get; private set; }
        public bool IsPlayerInvincible { get; set; }
        public bool IsEnemyAiEnabled { get; set; } = true;

        public DomainResult TryBindCoverRuntime(FpgCoverRuntime covers)
        {
            if (coverRuntime != null || covers == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            coverRuntime = covers;
            return DomainResult.Success;
        }

        public DomainResult TryBindCoverRuntime(
            FpgCoverRuntime covers,
            IFpgCoverGeometryResolver geometryResolver)
        {
            if (coverRuntime != null
                || covers == null
                || geometryResolver == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            coverRuntime = covers;
            coverGeometryResolver = geometryResolver;
            return DomainResult.Success;
        }

        /// <summary>
        /// Closes a successfully committed immediate action that produced no
        /// impact intents. This writes presentation state only.
        /// </summary>
        public bool TryCompleteImmediateSkillPresentationGroup(
            RuntimeId sourceRuntimeId,
            SkillExecutionId skillExecutionId,
            int gameplayEventId,
            TickIndex tick,
            AttackId attackId)
        {
            try
            {
                return skillImpactPresentationStream.TryRecordGroupCompletion(
                    new FpgSkillImpactGroupCompletion(
                        new FpgSkillImpactCorrelation(
                            sourceRuntimeId,
                            skillExecutionId,
                            gameplayEventId),
                        FpgSkillImpactPresentationGroupKind.ImmediateAttack,
                        tick,
                        attackId));
            }
            catch (Exception)
            {
                IncrementPresentationCallbackFaultCount();
                return false;
            }
        }

        /// <summary>
        /// Publishes a presentation-only environment contact for an immediate
        /// attack. It never enters the player hit queue or changes combat state.
        /// </summary>
        public bool TryPublishImmediateEnvironmentContact(
            RuntimeId sourceRuntimeId,
            SkillExecutionId skillExecutionId,
            int gameplayEventId,
            TickIndex tick,
            AttackId attackId,
            SpatialVectorKey contactPoint,
            int contactOrdinal)
        {
            if (!sourceRuntimeId.IsValid
                || !skillExecutionId.IsValid
                || gameplayEventId <= 0
                || !tick.IsValid
                || !attackId.IsValid
                || contactOrdinal < 0)
            {
                return false;
            }

            try
            {
                return skillImpactPresentationStream.TryRecordContact(
                    new FpgSkillImpactContact(
                        new FpgSkillImpactCorrelation(
                            sourceRuntimeId,
                            skillExecutionId,
                            gameplayEventId),
                        FpgSkillImpactPresentationGroupKind.ImmediateAttack,
                        tick,
                        attackId,
                        ProjectileId.Invalid,
                        ImpactId.Invalid,
                        RuntimeId.Invalid,
                        FpgSkillImpactContactKind.EnvironmentBlocked,
                        contactPoint,
                        HitPart.Body,
                        contactOrdinal));
            }
            catch (Exception)
            {
                IncrementPresentationCallbackFaultCount();
                return false;
            }
        }

        public event Action<FpgEnemyDiedEvent> EnemyDied;
        public event Action<FpgEnemyAttackStartedEvent> EnemyAttackStarted;
        public event Action<FpgCombatHealthChangedEvent> HealthChanged;
        public event Action<FpgSummonRequest> SummonRequested;

        public DomainResult TrySubmitPlayerHit(FpgPlayerHitCommand command)
        {
            if (command.Intent.SourceId != player.RuntimeId)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            if (command.CommandSequence <= lastPlayerHitCommandSequence)
            {
                return DomainResult.Rejected(
                    command.CommandSequence == lastPlayerHitCommandSequence
                        ? RejectReason.DuplicateSequence
                        : RejectReason.ExpiredSequence);
            }

            if (currentTick.IsValid && command.Intent.ImpactTick < currentTick)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            if (playerHitCommandCount >= playerHitCommands.Length
                || !CanQueueImpacts(playerHitCommandCount + 1))
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            playerHitCommands[playerHitCommandCount++] = command;
            lastPlayerHitCommandSequence = command.CommandSequence;
            return DomainResult.Success;
        }

        public DomainResult ValidatePlayerHitBatch(
            RuntimeId sourceId,
            TickIndex impactTick,
            QueryCandidate[] candidates,
            int candidateCount,
            long firstCommandSequence)
        {
            if (sourceId != player.RuntimeId)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            if (!impactTick.IsValid || candidates == null
                || candidateCount < 0 || candidateCount > candidates.Length)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (currentTick.IsValid && impactTick < currentTick)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            if (candidateCount == 0)
            {
                return DomainResult.Success;
            }

            if (firstCommandSequence < 0
                || firstCommandSequence > long.MaxValue - candidateCount
                || playerHitCommandCount > playerHitCommands.Length - candidateCount
                || !CanQueueImpacts(playerHitCommandCount + candidateCount))
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            if (firstCommandSequence <= lastPlayerHitCommandSequence)
            {
                return DomainResult.Rejected(
                    firstCommandSequence == lastPlayerHitCommandSequence
                        ? RejectReason.DuplicateSequence
                        : RejectReason.ExpiredSequence);
            }

            for (int index = 0; index < candidateCount; index++)
            {
                QueryCandidate candidate = candidates[index];
                if (!candidate.IsValid
                    || (candidate.TargetKind != QueryTargetKind.Combatant
                        && candidate.TargetKind != QueryTargetKind.Projectile)
                    || !IsPlayerHitTargetLive(candidate.TargetId))
                {
                    return DomainResult.Rejected(RejectReason.InvalidTarget);
                }
            }

            return DomainResult.Success;
        }

        public DomainResult TrySubmitPlayerHits(
            FpgPlayerHitCommand[] commands,
            int commandCount)
        {
            if (commands == null || commandCount < 0 || commandCount > commands.Length)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (commandCount == 0)
            {
                return DomainResult.Success;
            }

            if (playerHitCommandCount > playerHitCommands.Length - commandCount
                || !CanQueueImpacts(playerHitCommandCount + commandCount))
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            long firstSequence = commands[0].CommandSequence;
            if (firstSequence < 0 || firstSequence > long.MaxValue - commandCount
                || firstSequence <= lastPlayerHitCommandSequence)
            {
                return DomainResult.Rejected(
                    firstSequence == lastPlayerHitCommandSequence
                        ? RejectReason.DuplicateSequence
                        : firstSequence < lastPlayerHitCommandSequence
                            ? RejectReason.ExpiredSequence
                            : RejectReason.BufferCapacity);
            }

            TickIndex impactTick = commands[0].Intent.ImpactTick;
            for (int index = 0; index < commandCount; index++)
            {
                FpgPlayerHitCommand command = commands[index];
                if (command.CommandSequence != firstSequence + index
                    || command.Intent.SourceId != player.RuntimeId
                    || command.Intent.ImpactTick != impactTick)
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                if (currentTick.IsValid && command.Intent.ImpactTick < currentTick)
                {
                    return DomainResult.Rejected(RejectReason.WrongTick);
                }

                if (!IsPlayerHitTargetLive(command.Intent.TargetId))
                {
                    return DomainResult.Rejected(RejectReason.InvalidTarget);
                }

                for (int prior = 0; prior < index; prior++)
                {
                    if (commands[prior].Intent.ImpactId == command.Intent.ImpactId)
                    {
                        return DomainResult.Rejected(RejectReason.DuplicateImpact);
                    }
                }
            }

            Array.Copy(
                commands,
                0,
                playerHitCommands,
                playerHitCommandCount,
                commandCount);
            playerHitCommandCount += commandCount;
            lastPlayerHitCommandSequence =
                commands[commandCount - 1].CommandSequence;
            return DomainResult.Success;
        }

        public DomainResult TryCompensatePlayerHitBatch(
            FpgPlayerHitCommand[] commands,
            int commandCount)
        {
            if (commands == null
                || commandCount <= 0
                || commandCount > commands.Length
                || commandCount > playerHitCommandCount)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            int firstStoredIndex = playerHitCommandCount - commandCount;
            for (int index = 0; index < commandCount; index++)
            {
                FpgPlayerHitCommand expected = commands[index];
                FpgPlayerHitCommand stored =
                    playerHitCommands[firstStoredIndex + index];
                if (stored.CommandSequence != expected.CommandSequence
                    || stored.Intent.ImpactId != expected.Intent.ImpactId
                    || stored.SkillExecutionId != expected.SkillExecutionId
                    || stored.GameplayEventId != expected.GameplayEventId)
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }
            }

            Array.Clear(
                playerHitCommands,
                firstStoredIndex,
                commandCount);
            playerHitCommandCount = firstStoredIndex;
            lastPlayerHitCommandSequence =
                commands[0].CommandSequence - 1L;
            return DomainResult.Success;
        }

        public DomainResult TryReserveEnemySkillCapacity(
            RuntimeId ownerRuntimeId,
            int attackEventCount,
            int projectileCapacity,
            int impactCapacity,
            int summonCapacity,
            int maxConcurrentThreats,
            out FpgEnemySkillCapacityReservation reservation)
        {
            reservation = FpgEnemySkillCapacityReservation.Invalid;
            if (!ownerRuntimeId.IsValid
                || attackEventCount <= 0
                || projectileCapacity < 0
                || impactCapacity < 0
                || summonCapacity < 0
                || maxConcurrentThreats < 0
                || projectileCapacity > impactCapacity)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (!CanAttack(ownerRuntimeId))
            {
                return DomainResult.Rejected(RejectReason.OwnerInterrupted);
            }

            int freeEntry = FindFreeEnemySkillCapacityReservation();
            if (freeEntry < 0
                || nextEnemySkillCapacityReservation <= 0L
                || nextEnemySkillCapacityReservation == long.MaxValue)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            long scheduledDemand = attackSchedule.Count;
            long projectileDemand = CountActiveProjectiles()
                + CountScheduledProjectileCapacity();
            long impactDemand = combatKernel.ImpactQueue.Count
                + CountActiveProjectileImpactCapacity()
                + CountScheduledImpactCapacity();
            long summonDemand = CountScheduledSummonCapacity();
            long globalThreatDemand = CountActiveThreats()
                + CountScheduledThreatCapacity();
            long ownerThreatDemand = CountActiveThreats(ownerRuntimeId)
                + CountScheduledThreatCapacity(ownerRuntimeId);
            for (int index = 0;
                index < enemySkillCapacityReservations.Length;
                index++)
            {
                EnemySkillCapacityReservationEntry entry =
                    enemySkillCapacityReservations[index];
                if (!entry.IsUsed)
                {
                    continue;
                }

                scheduledDemand += entry.RemainingAttackEvents;
                projectileDemand += entry.RemainingProjectileCapacity;
                impactDemand += entry.RemainingImpactCapacity;
                summonDemand += entry.RemainingSummonCapacity;
                globalThreatDemand += entry.MaxConcurrentThreats;
                if (entry.OwnerRuntimeId == ownerRuntimeId)
                {
                    ownerThreatDemand += entry.MaxConcurrentThreats;
                }
            }

            if (scheduledDemand + attackEventCount
                    > capacity.AttackScheduleCapacity
                || projectileDemand + projectileCapacity
                    > capacity.ProjectileCapacity
                || impactDemand + impactCapacity
                    > combatKernel.ImpactQueue.Capacity
                || impactDemand + impactCapacity
                    > combatKernel.ImpactLedger.RemainingCapacity
                || summonDemand + summonCapacity > capacity.SummonCapacity
                || globalThreatDemand + maxConcurrentThreats
                    > capacity.ThreatAdvanceCapacity
                || ownerThreatDemand + maxConcurrentThreats
                    > capacity.PerEnemyThreatCapacity)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            reservation = new FpgEnemySkillCapacityReservation(
                nextEnemySkillCapacityReservation++);
            enemySkillCapacityReservations[freeEntry] =
                new EnemySkillCapacityReservationEntry(
                    reservation,
                    ownerRuntimeId,
                    attackEventCount,
                    projectileCapacity,
                    impactCapacity,
                    summonCapacity,
                    maxConcurrentThreats);
            return DomainResult.Success;
        }

        public DomainResult CompleteEnemySkillCapacity(
            FpgEnemySkillCapacityReservation reservation)
        {
            int index = FindEnemySkillCapacityReservation(reservation);
            if (index < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            EnemySkillCapacityReservationEntry entry =
                enemySkillCapacityReservations[index];
            if (entry.RemainingAttackEvents != 0
                || entry.RemainingProjectileCapacity != 0
                || entry.RemainingImpactCapacity != 0
                || entry.RemainingSummonCapacity != 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            enemySkillCapacityReservations[index] =
                default(EnemySkillCapacityReservationEntry);
            return DomainResult.Success;
        }

        public DomainResult ReleaseEnemySkillCapacity(
            FpgEnemySkillCapacityReservation reservation)
        {
            int index = FindEnemySkillCapacityReservation(reservation);
            if (index < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            enemySkillCapacityReservations[index] =
                default(EnemySkillCapacityReservationEntry);
            return DomainResult.Success;
        }

        public DomainResult TrySubmitEnemyAttack(FpgEnemyAttackCommand command)
        {
            if (!command.Payload.IsValid
                || !command.Schedule.OwnerRuntimeId.IsValid
                || command.SpawnSequence < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (FindScheduledPayload(command.Schedule.ScheduleSequence) >= 0)
            {
                return DomainResult.Rejected(RejectReason.DuplicateSequence);
            }

            if (command.Payload.Kind
                    == FpgEnemyAttackPayloadKind.SelfDestructOwner
                && command.Payload.HasSelfDestructDependency
                && !IsValidSelfDestructDependency(command))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            int reservationIndex = -1;
            if (command.CapacityReservation.IsValid)
            {
                reservationIndex = FindEnemySkillCapacityReservation(
                    command.CapacityReservation);
                if (reservationIndex < 0
                    || !CanConsumeEnemySkillCapacity(
                        enemySkillCapacityReservations[reservationIndex],
                        command))
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }
            }
            else if (attackSchedule.Count
                    + CountRemainingReservedAttackEvents()
                    >= capacity.AttackScheduleCapacity
                || !CanAcceptUnreservedEnemyAttack(command))
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            if (IsCommittedTargetedAttack(command))
            {
                return CommitTargetedAttack(command, reservationIndex);
            }

            int payloadIndex = FindFreeScheduledPayload();
            if (payloadIndex < 0)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            DomainResult scheduled = attackSchedule.TrySchedule(command.Schedule, command.SpawnSequence);
            if (!scheduled.IsSuccess)
            {
                return scheduled;
            }

            scheduledPayloads[payloadIndex] = new ScheduledPayload(command);
            if (reservationIndex >= 0)
            {
                EnemySkillCapacityReservationEntry entry =
                    enemySkillCapacityReservations[reservationIndex];
                ConsumeEnemySkillCapacity(ref entry, command.Payload);
                enemySkillCapacityReservations[reservationIndex] = entry;
            }

            combatKernel.Trace.Record(
                command.Schedule.ReadyTick,
                CombatEventType.SkillGameplayCommitted,
                command.Schedule.OwnerRuntimeId,
                command.SpatialContext.TargetRuntimeId,
                AttackId.Invalid,
                ImpactId.Invalid,
                (int)command.Payload.Kind,
                0,
                skillExecutionId: command.SkillExecutionId.Value,
                gameplayEventId: command.GameplayEventId);
            return DomainResult.Success;
        }

        private DomainResult CommitTargetedAttack(
            FpgEnemyAttackCommand command,
            int reservationIndex)
        {
            ThreatPayloadDefinition payload = command.Payload.Threat.Payload;
            AttackId attackId = idAllocator.NextAttackId();
            ImpactIntent intent = new ImpactIntent(
                idAllocator.NextImpactId(),
                attackId,
                ShotId.Invalid,
                command.Schedule.OwnerRuntimeId,
                command.SpatialContext.TargetRuntimeId,
                command.Schedule.ReadyTick + payload.ImpactDelay,
                payload.TimedImpactDamage,
                HitPart.Body,
                DamageType.Normal,
                CombatTags.EnemyAttack,
                impactOrdinal: 0,
                spatialContext: new ImpactSpatialContext(
                    command.SpatialContext.Target,
                    QueryTargetKind.Combatant,
                    HitPart.Body));
            DomainResult queued = combatKernel.ImpactQueue.TryEnqueue(
                intent,
                ImpactPhasePriority.EnemyImpact,
                command.Schedule.OwnerRuntimeId,
                command.SkillExecutionId.Value,
                command.GameplayEventId);
            if (!queued.IsSuccess)
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            if (reservationIndex >= 0)
            {
                EnemySkillCapacityReservationEntry entry =
                    enemySkillCapacityReservations[reservationIndex];
                ConsumeEnemySkillCapacity(ref entry, command.Payload);
                enemySkillCapacityReservations[reservationIndex] = entry;
            }

            combatKernel.Trace.Record(
                command.Schedule.ReadyTick,
                CombatEventType.SkillGameplayCommitted,
                command.Schedule.OwnerRuntimeId,
                command.SpatialContext.TargetRuntimeId,
                AttackId.Invalid,
                ImpactId.Invalid,
                (int)command.Payload.Kind,
                0,
                skillExecutionId: command.SkillExecutionId.Value,
                gameplayEventId: command.GameplayEventId);
            combatKernel.Trace.Record(
                command.Schedule.ReadyTick,
                CombatEventType.ThreatScheduleDecision,
                command.Schedule.OwnerRuntimeId,
                command.SpatialContext.TargetRuntimeId,
                attackId,
                ImpactId.Invalid,
                command.SpawnSequence,
                command.Payload.Threat.DefinitionId,
                skillExecutionId: command.SkillExecutionId.Value,
                gameplayEventId: command.GameplayEventId);
            PublishEnemyAttackStarted(
                command.Schedule,
                command.SpawnSequence,
                command.Payload.Kind,
                command.Schedule.ReadyTick);
            return DomainResult.Success;
        }

        private static bool IsCommittedTargetedAttack(
            FpgEnemyAttackCommand command)
        {
            if (!command.HasSkillCorrelation
                || command.Payload.Kind
                    != FpgEnemyAttackPayloadKind.Threat)
            {
                return false;
            }

            ThreatDefinition threat = command.Payload.Threat;
            return threat.Payload.IsTimedImpact
                && threat.TelegraphDuration.Value == 0
                && threat.WindupDuration.Value == 0
                && threat.RecoveryDuration.Value == 0;
        }

        public DomainResult TryCompensateSummonAttack(long scheduleSequence)
        {
            int payloadIndex = FindScheduledPayload(scheduleSequence);
            if (payloadIndex < 0
                || scheduledPayloads[payloadIndex].Payload.Kind
                    != FpgEnemyAttackPayloadKind.Summon
                || !attackSchedule.TryCancel(scheduleSequence))
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            ScheduledPayload scheduled = scheduledPayloads[payloadIndex];
            int reservationIndex = FindEnemySkillCapacityReservation(
                scheduled.CapacityReservation);
            if (reservationIndex >= 0)
            {
                EnemySkillCapacityReservationEntry entry =
                    enemySkillCapacityReservations[reservationIndex];
                RestoreEnemySkillCapacity(ref entry, scheduled.Payload);
                enemySkillCapacityReservations[reservationIndex] = entry;
            }

            ResolveSelfDestructDependencies(
                scheduled,
                SelfDestructDependencyState.Skipped);
            scheduledPayloads[payloadIndex] = default(ScheduledPayload);
            return DomainResult.Success;
        }

        /// <summary>
        /// Optional explicit activation hook. The port also discovers active
        /// roster entries at EnemyRecovery, so lifecycle adapters may either
        /// register eagerly or let Process synchronize them on the same tick.
        /// </summary>
        public DomainResult TryRegisterEnemy(FpgEnemyCombatantRegistration registration)
        {
            return RegisterEnemy(registration);
        }

        public bool TryGetEnemyRuntime(RuntimeId runtimeId, out EnemyRuntime runtime)
        {
            int index = FindEnemy(runtimeId);
            runtime = index < 0 ? null : enemies[index].Runtime;
            return runtime != null;
        }

        public bool TryGetProjectile(RuntimeId runtimeId, out ProjectileRuntime projectile)
        {
            int index = FindProjectile(runtimeId, includeTerminal: false);
            projectile = index < 0 ? null : projectiles[index].Runtime;
            return projectile != null;
        }

        public DomainResult TrySpawnPlayerAreaProjectile(
            in FpgPlayerAreaProjectileRequest request,
            out RuntimeId projectileRuntimeId)
        {
            projectileRuntimeId = RuntimeId.Invalid;
            if (!currentTick.IsValid || request.Tick != currentTick)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            if (player.Combatant.IsDead
                || request.Attack.OwnerId != player.RuntimeId
                || request.Attack.Team != Team.Player
                || request.Definition.Interceptable)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            int free = FindFreeProjectile();
            if (free < 0
                || !CanQueueImpacts(request.Attack.MaxImpactCount))
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            DomainResult reserved = combatKernel.ProjectileBudget.TryReserve(
                request.Definition.BudgetUnits,
                out ReservationToken reservationToken);
            if (!reserved.IsSuccess)
            {
                return reserved;
            }

            ProjectileRuntime projectile = new ProjectileRuntime(
                idAllocator.NextProjectileId(),
                idAllocator.NextRuntimeId(),
                request.Attack.AttackId,
                player.RuntimeId,
                Team.Player,
                request.Definition,
                request.Tick,
                reservationToken);
            ProjectileSpawnRequest spawnRequest = new ProjectileSpawnRequest(
                request.Tick,
                projectile.ImpactTick,
                projectile.ProjectileId,
                projectile.RuntimeId,
                projectile.AttackId,
                projectile.OwnerId,
                RuntimeId.Invalid,
                projectile.Team,
                projectile.Definition.DefinitionId,
                projectile.Definition.SweepRadiusKey,
                false,
                ProjectileTargetingMode.FirstSurface,
                request.Start,
                request.End);
            DomainResult registered = projectileWorldPort.Register(
                spawnRequest,
                out ProjectilePathSnapshot path);
            if (!registered.IsSuccess)
            {
                return ReleasePlayerProjectileReservation(
                    reservationToken,
                    registered);
            }

            if (!path.Matches(spawnRequest))
            {
                DomainResult worldRelease = projectileWorldPort.Release(
                    new ProjectileReleaseRequest(
                        request.Tick,
                        projectile.ProjectileId,
                        projectile.RuntimeId,
                        ProjectileTerminalReason.SessionEnded));
                DomainResult budgetRelease = combatKernel.ProjectileBudget
                    .ReleaseReservation(reservationToken);
                if (!worldRelease.IsSuccess)
                {
                    return worldRelease;
                }

                return budgetRelease.IsSuccess
                    ? DomainResult.Rejected(RejectReason.InvalidState)
                    : budgetRelease;
            }

            DomainResult travelling = projectile.StartTravelling();
            if (!travelling.IsSuccess)
            {
                projectileWorldPort.Release(new ProjectileReleaseRequest(
                    request.Tick,
                    projectile.ProjectileId,
                    projectile.RuntimeId,
                    ProjectileTerminalReason.SessionEnded));
                return ReleasePlayerProjectileReservation(
                    reservationToken,
                    travelling);
            }

            DomainResult activated = combatKernel.ProjectileBudget.Activate(
                reservationToken);
            if (!activated.IsSuccess)
            {
                projectile.TryCancel(
                    request.Tick,
                    ProjectileTerminalReason.OwnerCanceled);
                projectileWorldPort.Release(new ProjectileReleaseRequest(
                    request.Tick,
                    projectile.ProjectileId,
                    projectile.RuntimeId,
                    ProjectileTerminalReason.OwnerCanceled));
                return ReleasePlayerProjectileReservation(
                    reservationToken,
                    activated);
            }

            projectiles[free] = new ProjectileBinding(
                projectile,
                RuntimeId.Invalid,
                path,
                request.SkillExecutionId,
                request.GameplayEventId,
                request.Attack,
                request.Attack.MaxImpactCount,
                CountExistingProjectilesInGroup(
                    player.RuntimeId,
                    request.SkillExecutionId,
                    request.GameplayEventId));
            combatKernel.Trace.Record(
                request.Tick,
                CombatEventType.ProjectileStateChanged,
                projectile.OwnerId,
                projectile.RuntimeId,
                projectile.AttackId,
                ImpactId.Invalid,
                (int)ProjectileState.Scheduled,
                (int)ProjectileState.Travelling,
                skillExecutionId: request.SkillExecutionId.Value,
                gameplayEventId: request.GameplayEventId);
            projectileRuntimeId = projectile.RuntimeId;
            return DomainResult.Success;
        }

        public DomainResult TryCancelPlayerAreaProjectile(
            RuntimeId projectileRuntimeId,
            TickIndex tick)
        {
            if (!currentTick.IsValid || !tick.IsValid || tick != currentTick)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            int index = FindProjectile(projectileRuntimeId, includeTerminal: false);
            if (index < 0 || !projectiles[index].IsPlayerAreaProjectile)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            ProjectileRuntime projectile = projectiles[index].Runtime;
            DomainResult cancelled = projectile.TryCancel(
                tick,
                ProjectileTerminalReason.OwnerCanceled);
            if (!cancelled.IsSuccess)
            {
                return cancelled;
            }

            TryPublishProjectileTerminal(index);

            DomainResult released = ReleaseProjectileResources(index);
            if (!released.IsSuccess)
            {
                return released;
            }

            projectiles[index] = default(ProjectileBinding);
            return DomainResult.Success;
        }

        public DomainResult Process(FpgBattleTickPhase phase, TickIndex tick, FpgEnemyRoster roster)
        {
            if (combatKernel.IsDisposed || roster == null || !tick.IsValid
                || !Enum.IsDefined(typeof(FpgBattleTickPhase), phase))
            {
                ClearState(preserveTrace: true);
                return DomainResult.Rejected(
                    combatKernel.IsDisposed ? RejectReason.Disposed : RejectReason.InvalidState);
            }

            if (currentTick.IsValid && tick < currentTick)
            {
                ClearState(preserveTrace: true);
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            currentTick = tick;
            DomainResult result;
            try
            {
                switch (phase)
                {
                    case FpgBattleTickPhase.LifecycleBoundary:
                        result = SynchronizeRoster(roster, tick);
                        break;
                    case FpgBattleTickPhase.EnemyRecovery:
                        result = ProcessEnemyRecovery(roster, tick);
                        break;
                    case FpgBattleTickPhase.PlayerAttackAndHit:
                        result = ProcessPlayerHits(tick);
                        break;
                    case FpgBattleTickPhase.DeathAndThreatCleanup:
                        result = ProcessDeathAndThreatCleanup(roster, tick);
                        break;
                    case FpgBattleTickPhase.EnemyAttackDirector:
                        result = ProcessEnemyAttackDirector(tick);
                        break;
                    case FpgBattleTickPhase.ThreatAndProjectileAdvance:
                        result = ProcessThreatsAndProjectiles(tick);
                        break;
                    case FpgBattleTickPhase.ImpactResolution:
                        result = ProcessImpactResolution(tick);
                        break;
                    case FpgBattleTickPhase.EncounterCompletion:
                        combatKernel.ImpactLedger.Clear();
                        result = DomainResult.Success;
                        break;
                    default:
                        result = DomainResult.Rejected(RejectReason.InvalidState);
                        break;
                }
            }
            catch (Exception)
            {
                result = DomainResult.Rejected(RejectReason.InvariantFault);
            }

            if (!result.IsSuccess)
            {
                ClearState(preserveTrace: true);
            }

            return result;
        }

        public bool CanAttack(RuntimeId ownerRuntimeId)
        {
            if (!IsEnemyAiEnabled)
            {
                return false;
            }

            int index = FindEnemy(ownerRuntimeId);
            if (index < 0)
            {
                return false;
            }

            EnemyRuntime runtime = enemies[index].Runtime;
            return runtime != null
                && !runtime.Combatant.IsDead
                && runtime.ControlState == EnemyControlState.Active;
        }

        bool IFpgAttackScheduleEligibility.CanProcessScheduledAttack(
            FpgAttackScheduleRequest request,
            int spawnSequence)
        {
            if (!IsEnemyAiEnabled)
            {
                return false;
            }

            if (CanAttack(request.OwnerRuntimeId))
            {
                return true;
            }

            int ownerIndex = FindEnemy(request.OwnerRuntimeId);
            if (ownerIndex < 0)
            {
                return false;
            }

            EnemyRuntime owner = enemies[ownerIndex].Runtime;
            if (owner == null || owner.Combatant.IsDead)
            {
                return false;
            }

            int payloadIndex = FindScheduledPayload(request.ScheduleSequence);
            if (payloadIndex < 0)
            {
                return false;
            }

            ScheduledPayload scheduled = scheduledPayloads[payloadIndex];
            return scheduled.OwnerRuntimeId == request.OwnerRuntimeId
                && scheduled.SpawnSequence == spawnSequence
                && scheduled.BypassesOwnerControlEligibility;
        }

        public void ClearAll()
        {
            ClearState(preserveTrace: false);
        }

        public void ResetPresentationState(TickIndex tick)
        {
            if (!tick.IsValid)
            {
                return;
            }

            vitalsStream.Clear();
            damageFeedbackStream.Clear();
            skillImpactPresentationStream.Clear();
            PublishVitals(
                player.Combatant,
                tick,
                FpgVitalsChangeReason.Restart,
                force: true);
        }

        private DomainResult ProcessEnemyRecovery(FpgEnemyRoster roster, TickIndex tick)
        {
            DomainResult synchronized = SynchronizeRoster(roster, tick);
            if (!synchronized.IsSuccess)
            {
                return synchronized;
            }

            for (int index = 0; index < enemies.Length; index++)
            {
                EnemyRuntime runtime = enemies[index].Runtime;
                if (runtime == null || runtime.Combatant.IsDead)
                {
                    continue;
                }

                if (runtime.AdvanceStartOfTick(tick))
                {
                    combatKernel.Trace.Record(
                        tick,
                        CombatEventType.GroggyEnded,
                        runtime.RuntimeId,
                        runtime.RuntimeId,
                        AttackId.Invalid,
                        ImpactId.Invalid,
                        0,
                        runtime.Combatant.Break);
                }
            }

            return DomainResult.Success;
        }

        private DomainResult SynchronizeRoster(FpgEnemyRoster roster, TickIndex tick)
        {
            for (int index = 0; index < roster.Capacity; index++)
            {
                FpgEnemySlot slot = roster.GetSlot(index);
                if (!slot.IsActive || !slot.RuntimeId.IsValid)
                {
                    continue;
                }

                int existing = FindEnemy(slot.RuntimeId);
                if (existing >= 0)
                {
                    if (enemies[existing].SpawnSequence != slot.SpawnSequence)
                    {
                        return DomainResult.Rejected(RejectReason.InvariantFault);
                    }

                    continue;
                }

                FpgEnemyCombatantRegistration registration = new FpgEnemyCombatantRegistration(
                    slot.RuntimeId,
                    slot.SpawnSequence,
                    Math.Max(1, slot.MaxLife),
                    Math.Max(0, slot.MaxBreak),
                    defaultGroggyDuration,
                    slot.ActivationTick.IsValid ? slot.ActivationTick : tick);
                DomainResult registered = RegisterEnemy(registration);
                if (!registered.IsSuccess)
                {
                    return registered;
                }
            }

            return DomainResult.Success;
        }

        private DomainResult RegisterEnemy(FpgEnemyCombatantRegistration registration)
        {
            int existing = FindEnemy(registration.RuntimeId);
            if (existing >= 0)
            {
                return enemies[existing].SpawnSequence == registration.SpawnSequence
                    ? DomainResult.Success
                    : DomainResult.Rejected(RejectReason.DuplicateSequence);
            }

            int free = FindFreeEnemy();
            if (free < 0)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            EnemyRuntime runtime = new EnemyRuntime(
                new CombatantState(
                    registration.RuntimeId,
                    CombatantKind.Enemy,
                    registration.Life,
                    0,
                    registration.Break),
                registration.GroggyDuration,
                capacity.PerEnemyThreatCapacity);
            enemies[free] = new EnemyBinding(runtime, registration.SpawnSequence);
            enemyCount++;
            combatKernel.Trace.Record(
                registration.ActivationTick,
                CombatEventType.EnemySpawned,
                RuntimeId.Invalid,
                registration.RuntimeId,
                AttackId.Invalid,
                ImpactId.Invalid,
                registration.SpawnSequence,
                registration.Life);
            PublishVitals(
                runtime.Combatant,
                registration.ActivationTick,
                FpgVitalsChangeReason.Spawn,
                force: true);
            return DomainResult.Success;
        }

        private DomainResult ProcessPlayerHits(TickIndex tick)
        {
            int dueCount = 0;
            for (int index = 0; index < playerHitCommandCount; index++)
            {
                FpgPlayerHitCommand command = playerHitCommands[index];
                if (command.Intent.ImpactTick > tick)
                {
                    continue;
                }

                if (!IsPlayerHitTargetLive(command.Intent.TargetId))
                {
                    return DomainResult.Rejected(RejectReason.InvalidTarget);
                }

                playerHitDueBuffer[dueCount++] = command;
            }

            if (!CanQueueImpacts(dueCount))
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            int write = 0;
            for (int index = 0; index < playerHitCommandCount; index++)
            {
                FpgPlayerHitCommand command = playerHitCommands[index];
                if (command.Intent.ImpactTick > tick)
                {
                    playerHitCommands[write++] = command;
                }
            }

            for (int index = write; index < playerHitCommandCount; index++)
            {
                playerHitCommands[index] = default(FpgPlayerHitCommand);
            }

            playerHitCommandCount = write;
            SortPlayerHits(playerHitDueBuffer, dueCount);
            for (int index = 0; index < dueCount; index++)
            {
                FpgPlayerHitCommand command = playerHitDueBuffer[index];
                combatKernel.Trace.Record(
                    tick,
                    CombatEventType.InputAccepted,
                    command.Intent.SourceId,
                    command.Intent.TargetId,
                    command.Intent.AttackId,
                    command.Intent.ImpactId,
                    0,
                    0,
                    skillExecutionId: command.SkillExecutionId.Value,
                    gameplayEventId: command.GameplayEventId);
                DomainResult resolved = ResolveImpact(
                    command.Intent,
                    command.SkillExecutionId.Value,
                    command.GameplayEventId,
                    publishImmediateContact: command.HasSkillCorrelation);
                if (!resolved.IsSuccess)
                {
                    return resolved;
                }

                if (command.HasSkillCorrelation
                    && IsFinalPlayerHitInGroup(
                        command,
                        playerHitDueBuffer,
                        index + 1,
                        dueCount)
                    && IsFinalPlayerHitInGroup(
                        command,
                        playerHitCommands,
                        0,
                        playerHitCommandCount))
                {
                    TryPublishImmediateGroupCompletion(command.Intent,
                        command.SkillExecutionId.Value,
                        command.GameplayEventId);
                }

                playerHitDueBuffer[index] = default(FpgPlayerHitCommand);
            }

            return DomainResult.Success;
        }
        private DomainResult ProcessDeathAndThreatCleanup(FpgEnemyRoster roster, TickIndex tick)
        {
            for (int index = 0; index < enemies.Length; index++)
            {
                EnemyBinding binding = enemies[index];
                if (binding.Runtime == null)
                {
                    continue;
                }

                bool rosterActive = roster.TryGet(binding.RuntimeId, out FpgEnemySlot rosterSlot)
                    && rosterSlot.IsActive;
                bool runtimeDead = binding.Runtime.Combatant.IsDead;
                if (!runtimeDead && rosterActive)
                {
                    continue;
                }

                if (!runtimeDead)
                {
                    binding.Runtime.Combatant.ForceDeath();
                    PublishVitals(
                        binding.Runtime.Combatant,
                        tick,
                        FpgVitalsChangeReason.Death);
                }

                DomainResult marked = MarkEnemyDead(ref binding, tick, RuntimeId.Invalid, AttackId.Invalid);
                if (!marked.IsSuccess)
                {
                    return marked;
                }

                if (!binding.DeathNotified)
                {
                    NotifyEnemyDied(ref binding, tick, RuntimeId.Invalid, AttackId.Invalid);
                }

                if (!rosterActive)
                {
                    enemies[index] = default(EnemyBinding);
                    enemyCount = Math.Max(0, enemyCount - 1);
                }
                else
                {
                    enemies[index] = binding;
                }
            }

            return DomainResult.Success;
        }

        private DomainResult ProcessEnemyAttackDirector(TickIndex tick)
        {
            if (!IsEnemyAiEnabled)
            {
                CancelPendingEnemyAttacksForDisabledAi();
                return DomainResult.Success;
            }

            int attempts = 0;
            while (attempts++ < attackSchedule.Capacity
                && attackSchedule.TryDequeueDueForSchedule(
                    tick,
                    (IFpgAttackScheduleEligibility)this,
                    out FpgAttackScheduleRequest request,
                    out int spawnSequence))
            {
                int payloadIndex = FindScheduledPayload(request.ScheduleSequence);
                if (payloadIndex < 0)
                {
                    return DomainResult.Rejected(RejectReason.InvariantFault);
                }

                ScheduledPayload scheduled = scheduledPayloads[payloadIndex];
                if (scheduled.OwnerRuntimeId != request.OwnerRuntimeId
                    || scheduled.SpawnSequence != spawnSequence)
                {
                    return DomainResult.Rejected(RejectReason.InvariantFault);
                }

                int ownerIndex = FindEnemy(request.OwnerRuntimeId);
                EnemyRuntime owner = ownerIndex < 0
                    ? null
                    : enemies[ownerIndex].Runtime;
                if (owner == null
                    || owner.Combatant.IsDead
                    || (!scheduled.BypassesOwnerControlEligibility
                        && !CanAttack(request.OwnerRuntimeId)))
                {
                    return DomainResult.Rejected(RejectReason.InvalidTarget);
                }

                DomainResult handled;
                switch (scheduled.Payload.Kind)
                {
                    case FpgEnemyAttackPayloadKind.Threat:
                        handled = StartScheduledThreat(
                            ownerIndex,
                            request,
                            scheduled,
                            payloadIndex,
                            tick);
                        break;

                    case FpgEnemyAttackPayloadKind.Summon:
                        handled = DispatchScheduledSummon(
                            request,
                            scheduled,
                            payloadIndex,
                            tick);
                        break;

                    case FpgEnemyAttackPayloadKind.SelfDestructOwner:
                        handled = DispatchScheduledSelfDestruct(
                            request,
                            scheduled,
                            payloadIndex,
                            tick);
                        break;

                    default:
                        return DomainResult.Rejected(
                            RejectReason.InvalidDefinition);
                }

                if (!handled.IsSuccess)
                {
                    return handled;
                }
            }

            return DomainResult.Success;
        }

        private DomainResult StartScheduledThreat(
            int ownerIndex,
            FpgAttackScheduleRequest request,
            ScheduledPayload scheduled,
            int payloadIndex,
            TickIndex tick)
        {
            EnemyRuntime owner = enemies[ownerIndex].Runtime;
            ThreatDefinition definition = scheduled.Payload.Threat;
            DomainResult canAdd = owner.ValidateCanAddThreat();
            if (IsRetryableAttackStart(canAdd))
            {
                return RescheduleForNextTick(request, scheduled.SpawnSequence, tick);
            }

            if (!canAdd.IsSuccess)
            {
                return canAdd;
            }

            int threatBindingIndex = FindFreeThreatExecutionBinding();
            if (CountActiveThreats() >= threatAdvanceBuffer.Length
                || threatBindingIndex < 0)
            {
                return RescheduleForNextTick(request, scheduled.SpawnSequence, tick);
            }

            if (definition.TotalBudgetUnits > 0
                && !scheduled.ProjectileBudgetReservation.IsValid)
            {
                DomainResult canReserve = combatKernel.ProjectileBudget.CanReserve(definition.TotalBudgetUnits);
                if (IsRetryableAttackStart(canReserve))
                {
                    return RescheduleForNextTick(request, scheduled.SpawnSequence, tick);
                }

                if (!canReserve.IsSuccess)
                {
                    return canReserve;
                }
            }

            ThreatRuntime threat = new ThreatRuntime(definition, idAllocator.NextRuntimeId());
            DomainResult added = owner.TryAddThreat(threat);
            if (!added.IsSuccess)
            {
                return added;
            }

            if (scheduled.ProjectileBudgetReservation.IsValid)
            {
                DomainResult released = combatKernel.ProjectileBudget
                    .ReleaseReservation(
                        scheduled.ProjectileBudgetReservation);
                if (!released.IsSuccess)
                {
                    return released;
                }
            }

            DomainResult started = threat.TryStart(
                tick,
                owner.ControlState,
                combatKernel.ProjectileBudget,
                idAllocator);
            if (!started.IsSuccess)
            {
                threat.TryCancelBeforeRelease(combatKernel.ProjectileBudget);
                return started;
            }

            threatExecutionBindings[threatBindingIndex] =
                new ThreatExecutionBinding(
                    owner.RuntimeId,
                    threat.RuntimeId,
                    scheduled.SpatialContext,
                    scheduled.SkillExecutionId,
                    scheduled.GameplayEventId);
            scheduledPayloads[payloadIndex] = default(ScheduledPayload);
            combatKernel.Trace.Record(
                tick,
                CombatEventType.ThreatScheduleDecision,
                owner.RuntimeId,
                threat.RuntimeId,
                threat.AttackId,
                ImpactId.Invalid,
                scheduled.SpawnSequence,
                definition.DefinitionId,
                skillExecutionId: scheduled.SkillExecutionId.Value,
                gameplayEventId: scheduled.GameplayEventId);
            PublishEnemyAttackStarted(
                request,
                scheduled.SpawnSequence,
                scheduled.Payload.Kind,
                tick);
            return DomainResult.Success;
        }

        private DomainResult DispatchScheduledSummon(
            FpgAttackScheduleRequest request,
            ScheduledPayload scheduled,
            int payloadIndex,
            TickIndex tick)
        {
            FpgFormalSummonPayload summon = scheduled.Payload.Summon;
            if (scheduled.PresentationStarted && summon.ReleaseDelayTicks <= 0)
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            if (!scheduled.PresentationStarted && summon.ReleaseDelayTicks > 0)
            {
                if (!TryAddTicks(
                        tick,
                        summon.ReleaseDelayTicks,
                        out TickIndex releaseTick))
                {
                    return DomainResult.Rejected(RejectReason.BufferCapacity);
                }

                DomainResult delayed = RescheduleAt(
                    request,
                    scheduled.SpawnSequence,
                    releaseTick);
                if (!delayed.IsSuccess)
                {
                    return delayed;
                }

                scheduledPayloads[payloadIndex] =
                    scheduled.WithPresentationStarted();
                PublishEnemyAttackStarted(
                    request,
                    scheduled.SpawnSequence,
                    scheduled.Payload.Kind,
                    tick);
                return DomainResult.Success;
            }

            FpgSummonRequest summonRequest = summon.Request;
            FpgSummonQueueAck acknowledgement = summonRequestSink.TryQueueSummon(
                summonRequest,
                tick);
            switch (acknowledgement.Disposition)
            {
                case FpgSummonQueueDisposition.Queued:
                    ResolveSelfDestructDependencies(
                        scheduled,
                        SelfDestructDependencyState.Queued);
                    scheduledPayloads[payloadIndex] = default(ScheduledPayload);
                    if (!scheduled.PresentationStarted)
                    {
                        PublishEnemyAttackStarted(
                            request,
                            scheduled.SpawnSequence,
                            scheduled.Payload.Kind,
                            tick);
                    }

                    PublishSummonRequested(summonRequest);
                    return DomainResult.Success;

                case FpgSummonQueueDisposition.RetryNextTick:
                    return RescheduleForNextTick(request, scheduled.SpawnSequence, tick);

                case FpgSummonQueueDisposition.StaticLimitReached:
                    ResolveSelfDestructDependencies(
                        scheduled,
                        SelfDestructDependencyState.Skipped);
                    scheduledPayloads[payloadIndex] = default(ScheduledPayload);
                    return DomainResult.Success;

                case FpgSummonQueueDisposition.Rejected:
                    ResolveSelfDestructDependencies(
                        scheduled,
                        SelfDestructDependencyState.Skipped);
                    scheduledPayloads[payloadIndex] = default(ScheduledPayload);
                    return acknowledgement.Result.IsSuccess
                        ? DomainResult.Rejected(RejectReason.InvariantFault)
                        : acknowledgement.Result;

                default:
                    return DomainResult.Rejected(RejectReason.InvariantFault);
            }
        }

        private void ResolveSelfDestructDependencies(
            ScheduledPayload summon,
            SelfDestructDependencyState resolution)
        {
            if (resolution != SelfDestructDependencyState.Queued
                && resolution != SelfDestructDependencyState.Skipped)
            {
                return;
            }

            for (int index = 0; index < scheduledPayloads.Length; index++)
            {
                ScheduledPayload dependency = scheduledPayloads[index];
                if (!dependency.IsUsed
                    || dependency.Payload.Kind
                        != FpgEnemyAttackPayloadKind.SelfDestructOwner)
                {
                    continue;
                }

                if (!dependency.Payload.HasSelfDestructDependency
                    || dependency.Payload.SelfDestructDependencyScheduleSequence
                        != summon.ScheduleSequence)
                {
                    continue;
                }

                bool correlationMatches =
                    dependency.OwnerRuntimeId == summon.OwnerRuntimeId
                    && dependency.SpawnSequence == summon.SpawnSequence
                    && dependency.SkillExecutionId
                        == summon.SkillExecutionId;
                scheduledPayloads[index] =
                    dependency.WithSelfDestructDependencyState(
                        correlationMatches
                            ? resolution
                            : SelfDestructDependencyState.Skipped);
            }
        }

        private DomainResult DispatchScheduledSelfDestruct(
            FpgAttackScheduleRequest request,
            ScheduledPayload scheduled,
            int payloadIndex,
            TickIndex tick)
        {
            SelfDestructDependencyState dependencyState =
                scheduled.DependencyState;
            bool hasUnexpectedState = scheduled.Payload.HasSelfDestructDependency
                ? dependencyState == SelfDestructDependencyState.None
                : dependencyState != SelfDestructDependencyState.None;
            if (hasUnexpectedState)
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            switch (dependencyState)
            {
                case SelfDestructDependencyState.Waiting:
                    return RescheduleForNextTick(
                        request,
                        scheduled.SpawnSequence,
                        tick);

                case SelfDestructDependencyState.Skipped:
                    scheduledPayloads[payloadIndex] =
                        default(ScheduledPayload);
                    return DomainResult.Success;

                case SelfDestructDependencyState.None:
                case SelfDestructDependencyState.Queued:
                    scheduledPayloads[payloadIndex] =
                        default(ScheduledPayload);
                    PublishEnemyAttackStarted(
                        request,
                        scheduled.SpawnSequence,
                        scheduled.Payload.Kind,
                        tick);
                    return CommitOwnerSelfDestruct(
                        scheduled.OwnerRuntimeId,
                        tick);

                default:
                    return DomainResult.Rejected(
                        RejectReason.InvariantFault);
            }
        }

        private DomainResult CommitOwnerSelfDestruct(
            RuntimeId ownerRuntimeId,
            TickIndex tick)
        {
            int ownerIndex = FindEnemy(ownerRuntimeId);
            if (ownerIndex < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            EnemyBinding binding = enemies[ownerIndex];
            if (binding.Runtime == null || binding.Runtime.Combatant.IsDead)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            binding.Runtime.Combatant.ForceDeath();
            PublishVitals(
                binding.Runtime.Combatant,
                tick,
                FpgVitalsChangeReason.Death);
            DomainResult marked = MarkEnemyDead(
                ref binding,
                tick,
                RuntimeId.Invalid,
                AttackId.Invalid);
            if (!marked.IsSuccess)
            {
                return marked;
            }

            NotifyEnemyDied(
                ref binding,
                tick,
                RuntimeId.Invalid,
                AttackId.Invalid);
            enemies[ownerIndex] = binding;
            return DomainResult.Success;
        }

        private DomainResult RescheduleForNextTick(
            FpgAttackScheduleRequest request,
            int spawnSequence,
            TickIndex tick)
        {
            if (!TryAddTicks(tick, 1, out TickIndex retryTick))
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            return RescheduleAt(request, spawnSequence, retryTick);
        }

        private DomainResult RescheduleAt(
            FpgAttackScheduleRequest request,
            int spawnSequence,
            TickIndex readyTick)
        {
            FpgAttackScheduleRequest rescheduled = new FpgAttackScheduleRequest(
                request.OwnerRuntimeId,
                readyTick,
                request.Priority,
                request.ScheduleSequence,
                request.AttackPatternId,
                request.SkillExecutionId,
                request.GameplayEventId);
            return attackSchedule.TrySchedule(rescheduled, spawnSequence);
        }

        private static bool TryAddTicks(
            TickIndex start,
            int duration,
            out TickIndex result)
        {
            if (!start.IsValid
                || duration < 0
                || start.Value > long.MaxValue - duration)
            {
                result = TickIndex.Invalid;
                return false;
            }

            result = new TickIndex(start.Value + duration);
            return true;
        }

        private static bool IsRetryableAttackStart(DomainResult result)
        {
            return !result.IsSuccess
                && (result.RejectReason == RejectReason.BufferCapacity
                    || result.RejectReason == RejectReason.BudgetExceeded
                    || result.RejectReason == RejectReason.OwnerGroggy
                    || result.RejectReason == RejectReason.ActionLocked
                    || result.RejectReason == RejectReason.Cooldown);
        }

        private DomainResult ProcessThreatsAndProjectiles(TickIndex tick)
        {
            int threatCount = 0;
            for (int ownerIndex = 0; ownerIndex < enemies.Length; ownerIndex++)
            {
                EnemyBinding binding = enemies[ownerIndex];
                EnemyRuntime owner = binding.Runtime;
                if (owner == null || owner.Combatant.IsDead)
                {
                    continue;
                }

                for (int threatIndex = 0; threatIndex < owner.ThreatCount; threatIndex++)
                {
                    ThreatRuntime threat = owner.GetThreat(threatIndex);
                    if (threat == null)
                    {
                        continue;
                    }

                    int executionIndex = FindThreatExecutionBinding(
                        threat.RuntimeId);
                    if (threat.IsTerminal)
                    {
                        if (executionIndex >= 0)
                        {
                            threatExecutionBindings[executionIndex] =
                                default(ThreatExecutionBinding);
                        }

                        continue;
                    }

                    if (executionIndex < 0)
                    {
                        return DomainResult.Rejected(
                            RejectReason.InvariantFault);
                    }

                    if (threatCount >= threatAdvanceBuffer.Length)
                    {
                        return DomainResult.Rejected(RejectReason.BufferCapacity);
                    }

                    threatAdvanceBuffer[threatCount++] = new ThreatAdvanceBinding(
                        ownerIndex,
                        binding.SpawnSequence,
                        threat,
                        threatExecutionBindings[executionIndex]);
                }
            }

            SortThreats(threatAdvanceBuffer, threatCount);
            for (int index = 0; index < threatCount; index++)
            {
                DomainResult advanced = AdvanceThreat(threatAdvanceBuffer[index], tick);
                threatAdvanceBuffer[index] = default(ThreatAdvanceBinding);
                if (!advanced.IsSuccess)
                {
                    return advanced;
                }
            }

            return AdvanceProjectiles(tick);
        }

        private DomainResult AdvanceThreat(ThreatAdvanceBinding binding, TickIndex tick)
        {
            EnemyRuntime owner = enemies[binding.OwnerIndex].Runtime;
            ThreatRuntime threat = binding.Threat;
            if (owner == null || owner.Combatant.IsDead || threat == null || threat.IsTerminal)
            {
                return DomainResult.Success;
            }

            if (owner.ControlState != EnemyControlState.Active)
            {
                return DomainResult.Success;
            }

            if ((threat.State == ThreatState.Telegraph || threat.State == ThreatState.Recovery)
                && threat.StateUntilTick.IsValid
                && tick >= threat.StateUntilTick)
            {
                DomainResult stateAdvance = threat.AdvanceBeforeRelease(tick);
                if (!stateAdvance.IsSuccess)
                {
                    return stateAdvance;
                }
            }

            if (threat.State != ThreatState.Windup
                || !threat.StateUntilTick.IsValid
                || tick < threat.StateUntilTick)
            {
                return DomainResult.Success;
            }

            ThreatPayloadDefinition payload = threat.Definition.Payload;
            if (payload.IsTimedImpact && !CanQueueImpacts(1))
            {
                return DomainResult.Success;
            }

            if (payload.IsSweptProjectile && CountFreeProjectileSlots() < payload.PayloadCount)
            {
                return DomainResult.Success;
            }

            DomainResult committed = threat.TryCommitRelease(
                tick,
                combatKernel.ProjectileBudget,
                out ThreatRelease release);
            if (!committed.IsSuccess)
            {
                return committed;
            }

            DomainResult payloadResult = payload.IsTimedImpact
                ? QueueTimedImpact(
                    owner.RuntimeId,
                    threat.RuntimeId,
                    release,
                    binding.Execution,
                    tick)
                : CreateProjectiles(
                    owner.RuntimeId,
                    release,
                    binding.Execution,
                    tick);
            if (!payloadResult.IsSuccess)
            {
                return payloadResult;
            }

            DomainResult confirmed = threat.ConfirmPayloadsCreated(tick);
            if (!confirmed.IsSuccess)
            {
                return confirmed;
            }


            if (threat.Definition.RecoveryDuration.Value == 0)
            {
                DomainResult completed = threat.AdvanceBeforeRelease(tick);
                if (!completed.IsSuccess)
                {
                    return completed;
                }
            }

            combatKernel.Trace.Record(
                tick,
                CombatEventType.ThreatStateChanged,
                owner.RuntimeId,
                threat.RuntimeId,
                threat.AttackId,
                ImpactId.Invalid,
                (int)ThreatState.Windup,
                (int)ThreatState.Recovery,
                skillExecutionId: binding.Execution.SkillExecutionId.Value,
                gameplayEventId: binding.Execution.GameplayEventId);
            return DomainResult.Success;
        }

        private DomainResult QueueTimedImpact(
            RuntimeId ownerRuntimeId,
            RuntimeId threatRuntimeId,
            ThreatRelease release,
            ThreatExecutionBinding execution,
            TickIndex tick)
        {
            ThreatPayloadDefinition payload = release.Definition.Payload;
            if (!payload.IsTimedImpact || !CanQueueImpacts(1))
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            ImpactIntent intent = new ImpactIntent(
                idAllocator.NextImpactId(),
                release.AttackId,
                ShotId.Invalid,
                ownerRuntimeId,
                execution.SpatialContext.TargetRuntimeId,
                tick + payload.ImpactDelay,
                payload.TimedImpactDamage,
                HitPart.Body,
                DamageType.Normal,
                CombatTags.EnemyAttack);
            return combatKernel.ImpactQueue.TryEnqueue(
                intent,
                ImpactPhasePriority.EnemyImpact,
                threatRuntimeId,
                execution.SkillExecutionId.Value,
                execution.GameplayEventId);
        }

        private DomainResult CreateProjectiles(
            RuntimeId ownerRuntimeId,
            ThreatRelease release,
            ThreatExecutionBinding execution,
            TickIndex tick)
        {
            ThreatPayloadDefinition payload = release.Definition.Payload;
            if (!payload.IsSweptProjectile || CountFreeProjectileSlots() < payload.PayloadCount)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            for (int payloadIndex = 0; payloadIndex < payload.PayloadCount; payloadIndex++)
            {
                int free = FindFreeProjectile();
                if (free < 0)
                {
                    return DomainResult.Rejected(RejectReason.BufferCapacity);
                }

                ProjectileRuntime projectile = new ProjectileRuntime(
                    idAllocator.NextProjectileId(),
                    idAllocator.NextRuntimeId(),
                    release.AttackId,
                    ownerRuntimeId,
                    Team.Enemy,
                    payload.ProjectileDefinition,
                    tick,
                    release.ReservationToken);
                ProjectileSpawnRequest spawnRequest = new ProjectileSpawnRequest(
                    tick,
                    projectile.ImpactTick,
                    projectile.ProjectileId,
                    projectile.RuntimeId,
                    projectile.AttackId,
                    projectile.OwnerId,
                    execution.SpatialContext.TargetRuntimeId,
                    projectile.Team,
                    projectile.Definition.DefinitionId,
                    projectile.Definition.SweepRadiusKey,
                    projectile.Definition.Interceptable,
                    payload.PresentationKind,
                    ProjectileTargetingMode.LockedTarget,
                    execution.SpatialContext.Origin,
                    execution.SpatialContext.Target,
                    execution.SkillExecutionId,
                    execution.GameplayEventId);
                DomainResult registered = projectileWorldPort.Register(
                    spawnRequest,
                    out ProjectilePathSnapshot path);
                if (!registered.IsSuccess)
                {
                    return registered;
                }

                if (!path.Matches(spawnRequest))
                {
                    DomainResult releasedWorld = projectileWorldPort.Release(
                        new ProjectileReleaseRequest(
                            tick,
                            projectile.ProjectileId,
                            projectile.RuntimeId,
                            ProjectileTerminalReason.SessionEnded));
                    return releasedWorld.IsSuccess
                        ? DomainResult.Rejected(RejectReason.InvalidState)
                        : releasedWorld;
                }

                DomainResult travelling = projectile.StartTravelling();
                if (!travelling.IsSuccess)
                {
                    projectileWorldPort.Release(new ProjectileReleaseRequest(
                        tick,
                        projectile.ProjectileId,
                        projectile.RuntimeId,
                        ProjectileTerminalReason.SessionEnded));
                    return travelling;
                }

                projectiles[free] = new ProjectileBinding(
                    projectile,
                    execution.SpatialContext.TargetRuntimeId,
                    path,
                    execution.SkillExecutionId,
                    execution.GameplayEventId,
                    payloadIndex);
                combatKernel.Trace.Record(
                    tick,
                    CombatEventType.ProjectileStateChanged,
                    ownerRuntimeId,
                    projectile.RuntimeId,
                    projectile.AttackId,
                    ImpactId.Invalid,
                    (int)ProjectileState.Scheduled,
                    (int)ProjectileState.Travelling,
                    skillExecutionId: execution.SkillExecutionId.Value,
                    gameplayEventId: execution.GameplayEventId);
            }

            return DomainResult.Success;
        }

        private DomainResult AdvanceProjectiles(TickIndex tick)
        {
            for (int index = 0; index < projectiles.Length; index++)
            {
                ProjectileBinding binding = projectiles[index];
                ProjectileRuntime projectile = binding.Runtime;
                if (projectile == null)
                {
                    continue;
                }

                if (projectile.IsTerminal)
                {
                    TryPublishProjectileTerminal(index);
                    DomainResult terminalRelease = ReleaseProjectileResources(index);
                    if (!terminalRelease.IsSuccess)
                    {
                        return terminalRelease;
                    }

                    projectiles[index] = default(ProjectileBinding);
                    continue;
                }

                if (projectile.State != ProjectileState.Travelling
                    || tick <= projectile.SpawnTick)
                {
                    continue;
                }

                if (tick <= projectile.ImpactTick)
                {
                    bool validPath = binding.Path.ProjectileId
                            == projectile.ProjectileId
                        && binding.Path.RuntimeId == projectile.RuntimeId
                        && binding.Path.SpawnTick == projectile.SpawnTick
                        && binding.Path.ArrivalTick == projectile.ImpactTick
                        && binding.ImpactCapacityReservation > 0;
                    bool validBinding = binding.IsPlayerAreaProjectile
                        ? !binding.TargetRuntimeId.IsValid
                            && projectile.Team == Team.Player
                            && binding.PlayerAttack.AttackId
                                == projectile.AttackId
                            && binding.PlayerAttack.OwnerId == player.RuntimeId
                            && binding.PlayerAttack.Team == Team.Player
                            && binding.PlayerAttack.IsQueryConfigurationValid
                        : binding.TargetRuntimeId.IsValid;
                    if (!validPath || !validBinding)
                    {
                        return DomainResult.Rejected(RejectReason.InvalidState);
                    }

                    DomainResult segment = binding.Path.TryGetSegment(
                        tick,
                        out SpatialVectorKey from,
                        out SpatialVectorKey to);
                    if (!segment.IsSuccess)
                    {
                        return segment;
                    }

                    ProjectileSweepRequest sweepRequest = new ProjectileSweepRequest(
                        tick,
                        projectile.ProjectileId,
                        projectile.RuntimeId,
                        from,
                        to,
                        projectile.Definition.SweepRadiusKey);
                    DomainResult swept = projectileWorldPort.Sweep(
                        sweepRequest,
                        out ProjectileSweepHit sweepHit);
                    if (!swept.IsSuccess)
                    {
                        return swept;
                    }

                    if (!sweepHit.IsValid)
                    {
                        return DomainResult.Rejected(RejectReason.InvalidState);
                    }

                    if (binding.IsPlayerAreaProjectile)
                    {
                        DomainResult playerAdvanced = AdvancePlayerAreaProjectile(
                            index,
                            binding,
                            tick,
                            sweepHit);
                        if (!playerAdvanced.IsSuccess)
                        {
                            return playerAdvanced;
                        }

                        continue;
                    }

                    ProjectileState previous = projectile.State;
                    ImpactId impactId = ImpactId.Invalid;
                    bool hasTerminalContact = false;
                    SpatialVectorKey terminalContactPoint =
                        default(SpatialVectorKey);
                    RuntimeId terminalContactTarget = RuntimeId.Invalid;
                    HitPart terminalHitPart = HitPart.Body;
                    switch (sweepHit.Kind)
                    {
                        case ProjectileSweepHitKind.None:
                            if (tick >= projectile.ImpactTick)
                            {
                                DomainResult missed = projectile.TryMiss(tick);
                                if (!missed.IsSuccess)
                                {
                                    return missed;
                                }
                            }
                            break;

                        case ProjectileSweepHitKind.EnvironmentBlocked:
                        {
                            DomainResult coverImpact = QueueCoverImpact(
                                projectile,
                                binding,
                                sweepHit,
                                tick,
                                out impactId);
                            if (!coverImpact.IsSuccess)
                            {
                                return coverImpact;
                            }

                            DomainResult blocked = projectile.TryBlock(tick);
                            if (!blocked.IsSuccess)
                            {
                                return blocked;
                            }

                            hasTerminalContact = true;
                            terminalContactPoint = sweepHit.Point;
                            break;
                        }

                        case ProjectileSweepHitKind.Target:
                        {
                            if (sweepHit.TargetId != binding.TargetRuntimeId
                                || sweepHit.HitPart == HitPart.Projectile)
                            {
                                return DomainResult.Rejected(RejectReason.InvalidTarget);
                            }

                            if (!CanQueueImpacts(1, 1))
                            {
                                return DomainResult.Rejected(RejectReason.BufferCapacity);
                            }

                            impactId = idAllocator.NextImpactId();
                            ImpactIntent intent = new ImpactIntent(
                                impactId,
                                projectile.AttackId,
                                ShotId.Invalid,
                                projectile.OwnerId,
                                sweepHit.TargetId,
                                tick,
                                projectile.Definition.DamageSpec,
                                sweepHit.HitPart,
                                DamageType.Normal,
                                CombatTags.EnemyAttack,
                                impactOrdinal: binding.PresentationOrdinal,
                                spatialContext: new ImpactSpatialContext(
                                    sweepHit.Point,
                                    sweepHit.GeometryId,
                                    QueryTargetKind.Combatant,
                                    sweepHit.HitPart));
                            if (!TryMarkProjectileOriginImpact(impactId))
                            {
                                return DomainResult.Rejected(
                                    RejectReason.InvariantFault);
                            }

                            DomainResult queued = combatKernel.ImpactQueue.TryEnqueue(
                                intent,
                                ImpactPhasePriority.EnemyImpact,
                                projectile.RuntimeId,
                                binding.SkillExecutionId.Value,
                                binding.GameplayEventId);
                            if (!queued.IsSuccess)
                            {
                                RemoveProjectileOriginImpact(impactId);
                                return queued;
                            }

                            DomainResult hit = projectile.TryHit(tick);
                            if (!hit.IsSuccess)
                            {
                                return hit;
                            }


                            hasTerminalContact = true;
                            terminalContactPoint = sweepHit.Point;
                            terminalContactTarget = sweepHit.TargetId;
                            terminalHitPart = sweepHit.HitPart;
                            break;
                        }

                        default:
                            return DomainResult.Rejected(RejectReason.InvalidState);
                    }

                    if (projectile.State != previous)
                    {
                        combatKernel.Trace.Record(
                            tick,
                            CombatEventType.ProjectileStateChanged,
                            projectile.OwnerId,
                            projectile.RuntimeId,
                            projectile.AttackId,
                            impactId,
                            (int)previous,
                            (int)projectile.State,
                            skillExecutionId: binding.SkillExecutionId.Value,
                            gameplayEventId: binding.GameplayEventId);
                    }

                    if (projectile.IsTerminal)
                    {
                        projectiles[index] = binding;
                        TryPublishProjectileTerminal(
                            index,
                            hasTerminalContact,
                            terminalContactPoint,
                            terminalContactTarget,
                            terminalHitPart,
                            impactId);
                        DomainResult released = ReleaseProjectileResources(index);
                        if (!released.IsSuccess)
                        {
                            return released;
                        }

                        projectiles[index] = default(ProjectileBinding);
                    }

                    continue;
                }

                if (tick >= projectile.ExpireTick)
                {
                    if (binding.IsPlayerAreaProjectile)
                    {
                        DomainResult playerExpired = ExpirePlayerAreaProjectile(
                            index,
                            binding,
                            tick);
                        if (!playerExpired.IsSuccess)
                        {
                            return playerExpired;
                        }

                        continue;
                    }

                    ProjectileState previous = projectile.State;
                    DomainResult expired = projectile.TryExpire(tick);
                    if (!expired.IsSuccess)
                    {
                        return expired;
                    }

                    combatKernel.Trace.Record(
                        tick,
                        CombatEventType.ProjectileStateChanged,
                        projectile.OwnerId,
                        projectile.RuntimeId,
                        projectile.AttackId,
                        ImpactId.Invalid,
                        (int)previous,
                        (int)projectile.State,
                        skillExecutionId: binding.SkillExecutionId.Value,
                        gameplayEventId: binding.GameplayEventId);
                    projectiles[index] = binding;
                    TryPublishProjectileTerminal(index);
                    DomainResult released = ReleaseProjectileResources(index);
                    if (!released.IsSuccess)
                    {
                        return released;
                    }

                    projectiles[index] = default(ProjectileBinding);
                }
            }

            return DomainResult.Success;
        }

        private void CancelPendingEnemyAttacksForDisabledAi()
        {
            for (int index = 0; index < scheduledPayloads.Length; index++)
            {
                ScheduledPayload scheduled = scheduledPayloads[index];
                if (!scheduled.IsUsed)
                {
                    continue;
                }

                attackSchedule.TryCancel(scheduled.ScheduleSequence);
                if (scheduled.ProjectileBudgetReservation.IsValid)
                {
                    combatKernel.ProjectileBudget.ReleaseReservation(
                        scheduled.ProjectileBudgetReservation);
                }

                int reservationIndex = FindEnemySkillCapacityReservation(
                    scheduled.CapacityReservation);
                if (reservationIndex >= 0)
                {
                    EnemySkillCapacityReservationEntry entry =
                        enemySkillCapacityReservations[reservationIndex];
                    RestoreEnemySkillCapacity(ref entry, scheduled.Payload);
                    enemySkillCapacityReservations[reservationIndex] = entry;
                }

                scheduledPayloads[index] = default(ScheduledPayload);
            }
        }

        private DomainResult QueueCoverImpact(
            ProjectileRuntime projectile,
            ProjectileBinding binding,
            ProjectileSweepHit sweepHit,
            TickIndex tick,
            out ImpactId impactId)
        {
            impactId = ImpactId.Invalid;
            bool validProjectile = projectile != null && projectile.Team == Team.Enemy;
            bool hasCoverRuntime = coverRuntime != null;
            bool hasGeometryResolver = coverGeometryResolver != null;
            bool isEnvironmentHit = sweepHit.Kind == ProjectileSweepHitKind.EnvironmentBlocked;
            string coverId = string.Empty;
            bool resolvedCover = hasGeometryResolver
                && coverGeometryResolver.TryResolveCoverId(
                    sweepHit.GeometryId,
                    out coverId);
            bool intactCover = resolvedCover
                && hasCoverRuntime
                && coverRuntime.TryGetIntactDefenseState(
                    coverId,
                    out _);
            if (!validProjectile || !hasCoverRuntime || !hasGeometryResolver
                || !isEnvironmentHit || !resolvedCover || !intactCover)
            {
                return DomainResult.Success;
            }

            if (!CanQueueImpacts(1, 1))
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            impactId = idAllocator.NextImpactId();
            ImpactIntent intent = new ImpactIntent(
                impactId,
                projectile.AttackId,
                ShotId.Invalid,
                projectile.OwnerId,
                player.RuntimeId,
                tick,
                projectile.Definition.DamageSpec,
                HitPart.Body,
                DamageType.Normal,
                CombatTags.EnemyAttack,
                impactOrdinal: binding.PresentationOrdinal,
                spatialContext: new ImpactSpatialContext(
                    sweepHit.Point,
                    sweepHit.GeometryId,
                    QueryTargetKind.EnvironmentBlocker,
                    HitPart.Body));
            if (!TryMarkProjectileOriginImpact(impactId))
            {
                impactId = ImpactId.Invalid;
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            DomainResult queued = combatKernel.ImpactQueue.TryEnqueue(
                intent,
                ImpactPhasePriority.EnemyImpact,
                projectile.RuntimeId,
                binding.SkillExecutionId.Value,
                binding.GameplayEventId);
            if (!queued.IsSuccess)
            {
                RemoveProjectileOriginImpact(impactId);
                impactId = ImpactId.Invalid;
            }

            return queued;
        }

        private DomainResult AdvancePlayerAreaProjectile(
            int projectileIndex,
            ProjectileBinding binding,
            TickIndex tick,
            ProjectileSweepHit sweepHit)
        {
            ProjectileRuntime projectile = binding.Runtime;
            ProjectileState previous = projectile.State;
            SpatialVectorKey terminalPoint = default(SpatialVectorKey);
            switch (sweepHit.Kind)
            {
                case ProjectileSweepHitKind.None:
                    if (tick < projectile.ImpactTick)
                    {
                        return DomainResult.Success;
                    }

                    terminalPoint = binding.Path.End;
                    DomainResult missed = projectile.TryMiss(tick);
                    if (!missed.IsSuccess)
                    {
                        return missed;
                    }
                    break;

                case ProjectileSweepHitKind.EnvironmentBlocked:
                    terminalPoint = sweepHit.Point;
                    DomainResult blocked = projectile.TryBlock(tick);
                    if (!blocked.IsSuccess)
                    {
                        return blocked;
                    }
                    break;

                case ProjectileSweepHitKind.Target:
                    if (!sweepHit.TargetId.IsValid)
                    {
                        return DomainResult.Rejected(RejectReason.InvalidTarget);
                    }

                    terminalPoint = sweepHit.Point;
                    DomainResult hit = projectile.TryHit(tick);
                    if (!hit.IsSuccess)
                    {
                        return hit;
                    }
                    break;

                default:
                    return DomainResult.Rejected(RejectReason.InvalidState);
            }

            return projectile.IsTerminal
                ? ResolvePlayerAreaProjectileTerminal(
                    projectileIndex,
                    binding,
                    previous,
                    tick,
                    terminalPoint,
                    sweepHit.Kind != ProjectileSweepHitKind.None,
                    sweepHit.Kind == ProjectileSweepHitKind.Target
                        ? sweepHit.TargetId
                        : RuntimeId.Invalid,
                    sweepHit.Kind == ProjectileSweepHitKind.Target
                        ? sweepHit.HitPart
                        : HitPart.Body)
                : DomainResult.Success;
        }

        private DomainResult ExpirePlayerAreaProjectile(
            int projectileIndex,
            ProjectileBinding binding,
            TickIndex tick)
        {
            ProjectileRuntime projectile = binding.Runtime;
            ProjectileState previous = projectile.State;
            DomainResult expired = projectile.TryExpire(tick);
            if (!expired.IsSuccess)
            {
                return expired;
            }

            return ResolvePlayerAreaProjectileTerminal(
                projectileIndex,
                binding,
                previous,
                tick,
                binding.Path.End,
                false,
                RuntimeId.Invalid,
                HitPart.Body);
        }

        private DomainResult ResolvePlayerAreaProjectileTerminal(
            int projectileIndex,
            ProjectileBinding binding,
            ProjectileState previous,
            TickIndex tick,
            SpatialVectorKey terminalPoint,
            bool hasTerminalContact,
            RuntimeId terminalContactTarget,
            HitPart terminalHitPart)
        {
            ProjectileRuntime projectile = binding.Runtime;
            if (projectile == null || !projectile.IsTerminal
                || !projectile.TerminalTick.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            DomainResult queued = QueuePlayerAreaProjectileImpacts(
                binding,
                tick,
                terminalPoint);
            if (!queued.IsSuccess)
            {
                return queued;
            }

            combatKernel.Trace.Record(
                tick,
                CombatEventType.ProjectileStateChanged,
                projectile.OwnerId,
                projectile.RuntimeId,
                projectile.AttackId,
                ImpactId.Invalid,
                (int)previous,
                (int)projectile.State,
                skillExecutionId: binding.SkillExecutionId.Value,
                gameplayEventId: binding.GameplayEventId);
            projectiles[projectileIndex] = binding;
            TryPublishProjectileTerminal(
                projectileIndex,
                hasTerminalContact,
                terminalPoint,
                terminalContactTarget,
                terminalHitPart,
                ImpactId.Invalid);
            DomainResult released = ReleaseProjectileResources(projectileIndex);
            if (!released.IsSuccess)
            {
                return released;
            }

            projectiles[projectileIndex] = default(ProjectileBinding);
            return DomainResult.Success;
        }

        private DomainResult QueuePlayerAreaProjectileImpacts(
            ProjectileBinding binding,
            TickIndex tick,
            SpatialVectorKey terminalPoint)
        {
            Array.Clear(
                playerProjectileAreaCandidates,
                0,
                playerProjectileAreaCandidates.Length);
            Array.Clear(
                playerProjectileAreaSelected,
                0,
                playerProjectileAreaSelected.Length);
            PlayerProjectileAreaQueryRequest request;
            try
            {
                request = new PlayerProjectileAreaQueryRequest(
                    tick,
                    binding.PlayerAttack,
                    terminalPoint);
            }
            catch (Exception)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            DomainResult queried = playerProjectileAreaQueryPort.QueryAreaAtPoint(
                request,
                playerProjectileAreaCandidates,
                out AttackQueryResult queryResult);
            if (!queried.IsSuccess)
            {
                return queried;
            }

            DomainResult selected = TargetSelector.Select(
                binding.PlayerAttack,
                playerProjectileAreaCandidates,
                queryResult,
                playerProjectileAreaSelected,
                out int selectedCount);
            if (!selected.IsSuccess)
            {
                return selected;
            }

            if (selectedCount > binding.ImpactCapacityReservation
                || !CanQueueImpacts(selectedCount))
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            ProjectileRuntime projectile = binding.Runtime;
            for (int index = 0; index < selectedCount; index++)
            {
                QueryCandidate candidate = playerProjectileAreaSelected[index];
                if (!IsPlayerHitTargetLive(candidate.TargetId))
                {
                    return DomainResult.Rejected(RejectReason.InvalidTarget);
                }

                bool isProjectile = candidate.TargetKind
                    == QueryTargetKind.Projectile;
                ImpactIntent intent = new ImpactIntent(
                    idAllocator.NextImpactId(),
                    binding.PlayerAttack.AttackId,
                    binding.PlayerAttack.ShotId,
                    player.RuntimeId,
                    candidate.TargetId,
                    tick,
                    binding.PlayerAttack.DamageSpec,
                    candidate.HitPart,
                    isProjectile
                        ? DamageType.ProjectileIntercept
                        : DamageType.Explosive,
                    CombatTags.Secondary,
                    -1,
                    index,
                    new ImpactSpatialContext(
                        candidate.ImpactPointKey,
                        candidate.GeometryId,
                        candidate.TargetKind,
                        candidate.HitPart));
                if (!TryMarkProjectileOriginImpact(intent.ImpactId))
                {
                    return DomainResult.Rejected(RejectReason.InvariantFault);
                }

                DomainResult queued = combatKernel.ImpactQueue.TryEnqueue(
                    intent,
                    isProjectile
                        ? ImpactPhasePriority.PlayerProjectileIntercept
                        : ImpactPhasePriority.PlayerCombatantHit,
                    projectile.RuntimeId,
                    binding.SkillExecutionId.Value,
                    binding.GameplayEventId);
                if (!queued.IsSuccess)
                {
                    RemoveProjectileOriginImpact(intent.ImpactId);
                    return queued;
                }
            }

            return DomainResult.Success;
        }
        private DomainResult ProcessImpactResolution(TickIndex tick)
        {
            int count = combatKernel.ImpactQueue.DrainDue(tick, dueImpactBuffer);
            for (int index = 0; index < count; index++)
            {
                dueImpactIsProjectile[index] =
                    ContainsProjectileOriginImpact(
                        dueImpactBuffer[index].Intent.ImpactId);
            }

            for (int index = 0; index < count; index++)
            {
                QueuedImpact queued = dueImpactBuffer[index];
                ImpactIntent intent = queued.Intent;
                bool projectileOrigin = dueImpactIsProjectile[index];
                DomainResult resolved = ResolveImpact(
                    intent,
                    queued.SkillExecutionId,
                    queued.GameplayEventId,
                    queued.HasSkillCorrelation && !projectileOrigin);
                if (!resolved.IsSuccess)
                {
                    return resolved;
                }

                if (projectileOrigin)
                {
                    RemoveProjectileOriginImpact(intent.ImpactId);
                }
                else if (queued.HasSkillCorrelation
                    && IsFinalImmediateImpactInDueBatch(
                        queued,
                        dueImpactBuffer,
                        dueImpactIsProjectile,
                        index + 1,
                        count))
                {
                    TryPublishImmediateGroupCompletion(
                        intent,
                        queued.SkillExecutionId,
                        queued.GameplayEventId);
                }

                dueImpactBuffer[index] = default(QueuedImpact);
                dueImpactIsProjectile[index] = false;
            }

            return DomainResult.Success;
        }

        private DomainResult ResolveImpact(
            ImpactIntent intent,
            long skillExecutionId = 0L,
            int gameplayEventId = 0,
            bool publishImmediateContact = false)
        {
            if (intent.SpatialContext.HasValue
                && intent.SpatialContext.TargetKind
                    == QueryTargetKind.EnvironmentBlocker)
            {
                return ResolveCoverImpact(
                    intent,
                    skillExecutionId,
                    gameplayEventId,
                    publishImmediateContact);
            }

            int projectileIndex = FindProjectile(intent.TargetId, includeTerminal: false);
            if (projectileIndex >= 0)
            {
                ProjectileBinding projectileBinding = projectiles[projectileIndex];
                ProjectileRuntime projectile = projectileBinding.Runtime;
                ImpactResolution projectileResolution = combatKernel.DamageResolver.ResolveProjectile(
                    intent,
                    projectile);
                if (!projectileResolution.Result.IsSuccess)
                {
                    return projectileResolution.Result;
                }

                RecordResolution(
                    intent,
                    projectileResolution,
                    skillExecutionId,
                    gameplayEventId,
                    publishImmediateContact,
                    projectile.ProjectileId);
                if (projectileResolution.ProjectileDestroyed)
                {
                    TryPublishProjectileTerminal(
                        projectileIndex,
                        intent.SpatialContext.HasValue,
                        intent.SpatialContext.ImpactPointKey,
                        projectile.RuntimeId,
                        HitPart.Projectile,
                        intent.ImpactId);
                    DomainResult released = ReleaseProjectileResources(projectileIndex);
                    if (!released.IsSuccess)
                    {
                        return released;
                    }

                    projectiles[projectileIndex] = default(ProjectileBinding);
                }

                return DomainResult.Success;
            }

            int enemyIndex = FindEnemy(intent.TargetId);
            if (enemyIndex >= 0)
            {
                EnemyBinding binding = enemies[enemyIndex];
                if (binding.Runtime.Combatant.IsDead)
                {
                    return ConsumeStaleImpact(
                        intent,
                        skillExecutionId,
                        gameplayEventId);
                }

                ImpactResolution resolution = combatKernel.DamageResolver.ResolveCombatant(
                    intent,
                    binding.Runtime.Combatant,
                    DefenseSnapshot.Exposed,
                    binding.Runtime.ControlState == EnemyControlState.Active);
                if (!resolution.Result.IsSuccess)
                {
                    return resolution.Result;
                }

                RecordResolution(
                    intent,
                    resolution,
                    skillExecutionId,
                    gameplayEventId,
                    publishImmediateContact);
                if (resolution.BreakTriggered)
                {
                    int canceled = binding.Runtime.EnterGroggy(intent.ImpactTick, combatKernel.ProjectileBudget);
                    if (canceled < 0)
                    {
                        return DomainResult.Rejected(RejectReason.InvariantFault);
                    }

                    combatKernel.Trace.Record(
                        intent.ImpactTick,
                        CombatEventType.GroggyStarted,
                        intent.SourceId,
                        intent.TargetId,
                        intent.AttackId,
                        intent.ImpactId,
                        binding.Runtime.Combatant.MaxBreak,
                        0,
                        skillExecutionId: skillExecutionId,
                        gameplayEventId: gameplayEventId);
                }

                PublishVitals(
                    binding.Runtime.Combatant,
                    intent.ImpactTick,
                    resolution.Death
                        ? FpgVitalsChangeReason.Death
                        : FpgVitalsChangeReason.Damage);
                PublishHealthChanged(binding.Runtime, intent.ImpactTick, resolution);
                if (resolution.Death)
                {
                    DomainResult dead = MarkEnemyDead(
                        ref binding,
                        intent.ImpactTick,
                        intent.SourceId,
                        intent.AttackId);
                    if (!dead.IsSuccess)
                    {
                        return dead;
                    }

                    NotifyEnemyDied(
                        ref binding,
                        intent.ImpactTick,
                        intent.SourceId,
                        intent.AttackId);
                }

                enemies[enemyIndex] = binding;
                return DomainResult.Success;
            }

            if (intent.TargetId == player.RuntimeId)
            {
                if (player.Combatant.IsDead)
                {
                    return ConsumeStaleImpact(
                        intent,
                        skillExecutionId,
                        gameplayEventId);
                }

                if (coverRuntime != null && coverRuntime.IsTraversing)
                {
                    return ConsumeStaleImpact(
                        intent,
                        skillExecutionId,
                        gameplayEventId);
                }

                if (IsPlayerInvincible)
                {
                    DomainResult consumed = combatKernel.ImpactLedger.TryConsume(
                        intent.ImpactId);
                    if (!consumed.IsSuccess)
                    {
                        return consumed;
                    }

                    int life = player.Combatant.Life;
                    ImpactResolution invincibleResolution = ImpactResolution.Accepted(
                        new DamagePacket(
                            intent.ImpactId,
                            DamageChannel.Life,
                            appliedAmount: 0,
                            appliedBreakAmount: 0,
                            valueBefore: life,
                            valueAfter: life),
                        perfectRetract: false,
                        barrierBroken: false,
                        breakTriggered: false,
                        death: false,
                        projectileDestroyed: false);
                    RecordResolution(
                        intent,
                        invincibleResolution,
                        skillExecutionId,
                        gameplayEventId,
                        publishImmediateContact);
                    return DomainResult.Success;
                }

                ImpactResolution resolution = combatKernel.DamageResolver.ResolveCombatant(
                    intent,
                    player.Combatant,
                    DefenseSnapshot.Exposed,
                    false);
                if (!resolution.Result.IsSuccess)
                {
                    return resolution.Result;
                }

                RecordResolution(
                    intent,
                    resolution,
                    skillExecutionId,
                    gameplayEventId,
                    publishImmediateContact);
                PublishVitals(
                    player.Combatant,
                    intent.ImpactTick,
                    resolution.Death
                        ? FpgVitalsChangeReason.Death
                        : FpgVitalsChangeReason.Damage);
                PublishHealthChanged(player.Combatant, intent.ImpactTick, resolution);
                return DomainResult.Success;
            }

            return ConsumeStaleImpact(
                intent,
                skillExecutionId,
                gameplayEventId);
        }

        private DomainResult ResolveCoverImpact(
            ImpactIntent intent,
            long skillExecutionId,
            int gameplayEventId,
            bool publishImmediateContact)
        {
            if (coverRuntime == null
                || coverGeometryResolver == null
                || !coverGeometryResolver.TryResolveCoverId(
                    intent.SpatialContext.GeometryId,
                    out string coverId)
                || !coverRuntime.TryGetIntactDefenseState(
                    coverId,
                    out CombatantState coverDefense))
            {
                return ConsumeStaleImpact(
                    intent,
                    skillExecutionId,
                    gameplayEventId);
            }

            ImpactResolution resolution = combatKernel.DamageResolver.ResolveCombatant(
                intent,
                coverDefense,
                new DefenseSnapshot(
                    ExposureMode.Withdrawn,
                    TickIndex.Invalid,
                    TickDuration.Zero,
                    DamageSpec.BasisPoints),
                false);
            if (!resolution.Result.IsSuccess)
            {
                return resolution.Result;
            }

            if (resolution.BarrierBroken
                && coverRuntime.IsCurrentCover(coverId))
            {
                player.Exposure.ForceExposed(
                    intent.ImpactTick,
                    out _);
            }

            RecordResolution(
                intent,
                resolution,
                skillExecutionId,
                gameplayEventId,
                publishImmediateContact);
            return DomainResult.Success;
        }

        private DomainResult ConsumeStaleImpact(
            ImpactIntent intent,
            long skillExecutionId,
            int gameplayEventId)
        {
            DomainResult consumed = combatKernel.ImpactLedger.TryConsume(intent.ImpactId);
            if (!consumed.IsSuccess)
            {
                return consumed;
            }

            combatKernel.Trace.Record(
                intent.ImpactTick,
                CombatEventType.ImpactRejected,
                intent.SourceId,
                intent.TargetId,
                intent.AttackId,
                intent.ImpactId,
                0,
                0,
                RejectReason.InvalidTarget,
                skillExecutionId: skillExecutionId,
                gameplayEventId: gameplayEventId);
            return DomainResult.Success;
        }

        private void RecordResolution(
            ImpactIntent intent,
            ImpactResolution resolution,
            long skillExecutionId,
            int gameplayEventId,
            bool publishImmediateContact = false,
            ProjectileId contactedProjectileId = default(ProjectileId))
        {
            DamagePacket packet = resolution.Packet;
            combatKernel.Trace.Record(
                intent.ImpactTick,
                CombatEventType.ImpactAccepted,
                intent.SourceId,
                intent.TargetId,
                intent.AttackId,
                intent.ImpactId,
                packet.ValueBefore,
                packet.ValueAfter,
                RejectReason.None,
                0UL,
                packet.Channel,
                packet.AppliedBreakAmount,
                resolution.PerfectRetract,
                skillExecutionId,
                gameplayEventId);
            combatKernel.Trace.Record(
                intent.ImpactTick,
                CombatEventType.DamageApplied,
                intent.SourceId,
                intent.TargetId,
                intent.AttackId,
                intent.ImpactId,
                packet.ValueBefore,
                packet.ValueAfter,
                RejectReason.None,
                0UL,
                packet.Channel,
                packet.AppliedBreakAmount,
                resolution.PerfectRetract,
                skillExecutionId,
                gameplayEventId);
            try
            {
                damageFeedbackStream.TryRecord(intent, resolution);
            }
            catch (Exception)
            {
                // Presentation feedback is diagnostic-only and cannot fail combat.
            }

            if (publishImmediateContact)
            {
                TryPublishAcceptedImmediateContact(
                    intent,
                    resolution,
                    skillExecutionId,
                    gameplayEventId,
                    contactedProjectileId);
            }

        }

        private void PublishVitals(
            CombatantState combatant,
            TickIndex tick,
            FpgVitalsChangeReason reason,
            bool force = false)
        {
            try
            {
                vitalsStream.TryPublish(combatant, tick, reason, force);
            }
            catch (Exception)
            {
                // Presentation state is recoverable from CombatantState.
            }
        }

        private void PublishHealthChanged(
            EnemyRuntime runtime,
            TickIndex tick,
            ImpactResolution resolution)
        {
            CombatantState combatant = runtime.Combatant;
            PublishHealthChangedEvent(new FpgCombatHealthChangedEvent(
                combatant.RuntimeId,
                CombatantKind.Enemy,
                tick,
                combatant.Life,
                combatant.MaxLife,
                combatant.Break,
                combatant.MaxBreak,
                resolution.Packet,
                resolution.BreakTriggered,
                runtime.ControlState == EnemyControlState.Groggy,
                combatant.IsDead));
        }

        private void PublishHealthChanged(
            CombatantState combatant,
            TickIndex tick,
            ImpactResolution resolution)
        {
            PublishHealthChangedEvent(new FpgCombatHealthChangedEvent(
                combatant.RuntimeId,
                combatant.Kind,
                tick,
                combatant.Life,
                combatant.MaxLife,
                combatant.Break,
                combatant.MaxBreak,
                resolution.Packet,
                resolution.BreakTriggered,
                false,
                combatant.IsDead));
        }

        private void PublishEnemyAttackStarted(
            FpgAttackScheduleRequest request,
            int spawnSequence,
            FpgEnemyAttackPayloadKind payloadKind,
            TickIndex tick)
        {
            Action<FpgEnemyAttackStartedEvent> callbacks = EnemyAttackStarted;
            if (callbacks == null)
            {
                return;
            }

            FpgEnemyAttackStartedEvent started;
            try
            {
                started = new FpgEnemyAttackStartedEvent(
                    request.OwnerRuntimeId,
                    spawnSequence,
                    request.AttackPatternId,
                    tick,
                    request.ScheduleSequence,
                    payloadKind);
            }
            catch (Exception)
            {
                IncrementPresentationCallbackFaultCount();
                return;
            }

            Delegate[] subscribers = callbacks.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<FpgEnemyAttackStartedEvent>)subscribers[index])(
                        started);
                }
                catch (Exception)
                {
                    IncrementPresentationCallbackFaultCount();
                }
            }
        }

        private void PublishSummonRequested(FpgSummonRequest request)
        {
            Action<FpgSummonRequest> callbacks = SummonRequested;
            if (callbacks == null)
            {
                return;
            }

            Delegate[] subscribers = callbacks.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<FpgSummonRequest>)subscribers[index])(request);
                }
                catch (Exception)
                {
                    IncrementPresentationCallbackFaultCount();
                }
            }
        }

        private void PublishHealthChangedEvent(FpgCombatHealthChangedEvent changed)
        {
            Action<FpgCombatHealthChangedEvent> callbacks = HealthChanged;
            if (callbacks == null)
            {
                return;
            }

            Delegate[] subscribers = callbacks.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<FpgCombatHealthChangedEvent>)subscribers[index])(changed);
                }
                catch (Exception)
                {
                    IncrementPresentationCallbackFaultCount();
                }
            }
        }

        private void IncrementPresentationCallbackFaultCount()
        {
            if (PresentationCallbackFaultCount < int.MaxValue)
            {
                PresentationCallbackFaultCount++;
            }
        }

        private void TryPublishAcceptedImmediateContact(
            ImpactIntent intent,
            ImpactResolution resolution,
            long skillExecutionId,
            int gameplayEventId,
            ProjectileId contactedProjectileId)
        {
            if (!resolution.Result.IsSuccess
                || skillExecutionId <= 0L
                || gameplayEventId <= 0
                || !intent.SpatialContext.HasValue
                || intent.ImpactOrdinal < 0)
            {
                return;
            }

            try
            {
                FpgSkillImpactCorrelation correlation =
                    new FpgSkillImpactCorrelation(
                        intent.SourceId,
                        new SkillExecutionId(skillExecutionId),
                        gameplayEventId);
                FpgSkillImpactContactKind contactKind =
                    intent.HitPart == HitPart.Projectile
                        && resolution.ProjectileDestroyed
                        ? FpgSkillImpactContactKind.Intercepted
                        : FpgSkillImpactContactKind.TargetImpact;
                skillImpactPresentationStream.TryRecordContact(
                    new FpgSkillImpactContact(
                        correlation,
                        FpgSkillImpactPresentationGroupKind.ImmediateAttack,
                        intent.ImpactTick,
                        intent.AttackId,
                        contactedProjectileId,
                        intent.ImpactId,
                        intent.TargetId,
                        contactKind,
                        intent.SpatialContext.ImpactPointKey,
                        intent.HitPart,
                        intent.ImpactOrdinal));
            }
            catch (Exception)
            {
                IncrementPresentationCallbackFaultCount();
            }
        }

        private void TryPublishImmediateGroupCompletion(
            ImpactIntent intent,
            long skillExecutionId,
            int gameplayEventId)
        {
            if (skillExecutionId <= 0L || gameplayEventId <= 0)
            {
                return;
            }

            try
            {
                skillImpactPresentationStream.TryRecordGroupCompletion(
                    new FpgSkillImpactGroupCompletion(
                        new FpgSkillImpactCorrelation(
                            intent.SourceId,
                            new SkillExecutionId(skillExecutionId),
                            gameplayEventId),
                        FpgSkillImpactPresentationGroupKind.ImmediateAttack,
                        intent.ImpactTick,
                        intent.AttackId));
            }
            catch (Exception)
            {
                IncrementPresentationCallbackFaultCount();
            }
        }

        private void TryPublishProjectileTerminal(
            int projectileIndex,
            bool hasContactPoint = false,
            SpatialVectorKey contactPoint = default(SpatialVectorKey),
            RuntimeId contactTarget = default(RuntimeId),
            HitPart hitPart = HitPart.Body,
            ImpactId impactId = default(ImpactId))
        {
            if (projectileIndex < 0 || projectileIndex >= projectiles.Length)
            {
                return;
            }

            ProjectileBinding binding = projectiles[projectileIndex];
            ProjectileRuntime projectile = binding.Runtime;
            if (projectile == null
                || !projectile.IsTerminal
                || binding.PresentationTerminalPublished)
            {
                return;
            }

            binding.PresentationTerminalPublished = true;
            projectiles[projectileIndex] = binding;
            if (!binding.SkillExecutionId.IsValid
                || binding.GameplayEventId <= 0)
            {
                return;
            }

            try
            {
                FpgSkillImpactCorrelation correlation =
                    new FpgSkillImpactCorrelation(
                        projectile.OwnerId,
                        binding.SkillExecutionId,
                        binding.GameplayEventId);
                bool collisionEligible =
                    FpgSkillImpactPresentationRules
                        .TryResolveProjectileContactKind(
                            projectile.TerminalReason,
                            out FpgSkillImpactContactKind contactKind);
                if (projectile.TerminalReason
                    == ProjectileTerminalReason.EnvironmentBlocked)
                {
                    contactTarget = RuntimeId.Invalid;
                    hitPart = HitPart.Body;
                }
                else if (projectile.TerminalReason
                    == ProjectileTerminalReason.Intercepted)
                {
                    contactTarget = projectile.RuntimeId;
                    hitPart = HitPart.Projectile;
                }

                if (collisionEligible && hasContactPoint)
                {
                    skillImpactPresentationStream.TryRecordContact(
                        new FpgSkillImpactContact(
                            correlation,
                            FpgSkillImpactPresentationGroupKind.Projectile,
                            projectile.TerminalTick,
                            projectile.AttackId,
                            projectile.ProjectileId,
                            impactId,
                            contactTarget,
                            contactKind,
                            contactPoint,
                            hitPart,
                            binding.PresentationOrdinal));
                }

                if (!HasOutstandingProjectileInGroup(
                    projectileIndex,
                    projectile.OwnerId,
                    binding.SkillExecutionId,
                    binding.GameplayEventId))
                {
                    skillImpactPresentationStream.TryRecordGroupCompletion(
                        new FpgSkillImpactGroupCompletion(
                            correlation,
                            FpgSkillImpactPresentationGroupKind.Projectile,
                            projectile.TerminalTick,
                            projectile.AttackId));
                }
            }
            catch (Exception)
            {
                IncrementPresentationCallbackFaultCount();
            }
        }

        private bool HasOutstandingProjectileInGroup(
            int terminalIndex,
            RuntimeId sourceRuntimeId,
            SkillExecutionId skillExecutionId,
            int gameplayEventId)
        {
            for (int index = 0; index < projectiles.Length; index++)
            {
                if (index == terminalIndex)
                {
                    continue;
                }

                ProjectileBinding candidate = projectiles[index];
                if (candidate.Runtime != null
                    && !candidate.PresentationTerminalPublished
                    && candidate.Runtime.OwnerId == sourceRuntimeId
                    && candidate.SkillExecutionId == skillExecutionId
                    && candidate.GameplayEventId == gameplayEventId)
                {
                    return true;
                }
            }

            return false;
        }

        private int CountExistingProjectilesInGroup(
            RuntimeId sourceRuntimeId,
            SkillExecutionId skillExecutionId,
            int gameplayEventId)
        {
            int count = 0;
            for (int index = 0; index < projectiles.Length; index++)
            {
                ProjectileBinding binding = projectiles[index];
                if (binding.Runtime != null
                    && binding.Runtime.OwnerId == sourceRuntimeId
                    && binding.SkillExecutionId == skillExecutionId
                    && binding.GameplayEventId == gameplayEventId)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsFinalPlayerHitInGroup(
            FpgPlayerHitCommand current,
            FpgPlayerHitCommand[] commands,
            int startIndex,
            int count)
        {
            for (int index = startIndex; index < count; index++)
            {
                FpgPlayerHitCommand candidate = commands[index];
                if (candidate.HasSkillCorrelation
                    && candidate.Intent.SourceId == current.Intent.SourceId
                    && candidate.SkillExecutionId == current.SkillExecutionId
                    && candidate.GameplayEventId == current.GameplayEventId)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFinalImmediateImpactInDueBatch(
            QueuedImpact current,
            QueuedImpact[] impacts,
            bool[] projectileOrigins,
            int startIndex,
            int count)
        {
            for (int index = startIndex; index < count; index++)
            {
                QueuedImpact candidate = impacts[index];
                if (!projectileOrigins[index]
                    && candidate.SkillExecutionId == current.SkillExecutionId
                    && candidate.GameplayEventId == current.GameplayEventId
                    && candidate.Intent.SourceId == current.Intent.SourceId)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryMarkProjectileOriginImpact(ImpactId impactId)
        {
            if (!impactId.IsValid
                || ContainsProjectileOriginImpact(impactId))
            {
                return false;
            }

            for (int index = 0; index < projectileOriginImpactIds.Length;
                index++)
            {
                if (!projectileOriginImpactIds[index].IsValid)
                {
                    projectileOriginImpactIds[index] = impactId;
                    return true;
                }
            }

            return false;
        }

        private bool ContainsProjectileOriginImpact(ImpactId impactId)
        {
            if (!impactId.IsValid)
            {
                return false;
            }

            for (int index = 0; index < projectileOriginImpactIds.Length;
                index++)
            {
                if (projectileOriginImpactIds[index] == impactId)
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveProjectileOriginImpact(ImpactId impactId)
        {
            for (int index = 0; index < projectileOriginImpactIds.Length;
                index++)
            {
                if (projectileOriginImpactIds[index] == impactId)
                {
                    projectileOriginImpactIds[index] = ImpactId.Invalid;
                    return;
                }
            }
        }

        private DomainResult MarkEnemyDead(
            ref EnemyBinding binding,
            TickIndex tick,
            RuntimeId sourceRuntimeId,
            AttackId attackId)
        {
            DomainResult marked = binding.Runtime.MarkDead(combatKernel.ProjectileBudget);
            if (!marked.IsSuccess)
            {
                return marked;
            }

            attackSchedule.CancelOwner(binding.RuntimeId);
            RemoveScheduledPayloads(binding.RuntimeId);
            RemoveThreatExecutionBindings(binding.RuntimeId);
            binding.DeathTick = tick;
            binding.DeathSourceRuntimeId = sourceRuntimeId;
            binding.DeathAttackId = attackId;
            return DomainResult.Success;
        }

        private void NotifyEnemyDied(
            ref EnemyBinding binding,
            TickIndex tick,
            RuntimeId sourceRuntimeId,
            AttackId attackId)
        {
            if (binding.DeathNotified)
            {
                return;
            }

            binding.DeathNotified = true;
            RuntimeId source = sourceRuntimeId.IsValid
                ? sourceRuntimeId
                : binding.DeathSourceRuntimeId;
            AttackId resolvedAttack = attackId.IsValid ? attackId : binding.DeathAttackId;
            TickIndex resolvedTick = tick.IsValid ? tick : binding.DeathTick;
            combatKernel.Trace.Record(
                resolvedTick,
                CombatEventType.Death,
                source,
                binding.RuntimeId,
                resolvedAttack,
                ImpactId.Invalid,
                binding.Runtime.Combatant.MaxLife,
                0);
            EnemyDied?.Invoke(new FpgEnemyDiedEvent(
                binding.RuntimeId,
                source,
                resolvedAttack,
                resolvedTick));
        }

        private bool IsPlayerHitTargetLive(RuntimeId runtimeId)
        {
            int enemyIndex = FindEnemy(runtimeId);
            if (enemyIndex >= 0)
            {
                EnemyRuntime runtime = enemies[enemyIndex].Runtime;
                return runtime != null && !runtime.Combatant.IsDead;
            }

            return FindProjectile(runtimeId, includeTerminal: false) >= 0;
        }

        private bool CanQueueImpacts(
            int additionalCount,
            int releasingProjectileCredits = 0)
        {
            int activeProjectileImpactCapacity =
                CountActiveProjectileImpactCapacity();
            if (additionalCount < 0
                || releasingProjectileCredits < 0
                || releasingProjectileCredits
                    > activeProjectileImpactCapacity)
            {
                return false;
            }

            long demand = combatKernel.ImpactQueue.Count
                + activeProjectileImpactCapacity
                - releasingProjectileCredits
                + CountScheduledImpactCapacity()
                + CountRemainingReservedImpactCapacity()
                + additionalCount;
            return demand <= combatKernel.ImpactQueue.Capacity
                && demand <= combatKernel.ImpactLedger.RemainingCapacity;
        }

        private DomainResult ReleasePlayerProjectileReservation(
            ReservationToken reservationToken,
            DomainResult failure)
        {
            DomainResult released = combatKernel.ProjectileBudget
                .ReleaseReservation(reservationToken);
            return released.IsSuccess ? failure : released;
        }

        private DomainResult ReleaseProjectileResources(int projectileIndex)
        {
            if (projectileIndex < 0 || projectileIndex >= projectiles.Length)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            ProjectileBinding binding = projectiles[projectileIndex];
            ProjectileRuntime projectile = binding.Runtime;
            if (projectile == null || !projectile.IsTerminal
                || !projectile.TerminalTick.IsValid
                || projectile.TerminalReason == ProjectileTerminalReason.None)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!binding.WorldReleased)
            {
                DomainResult worldRelease = projectileWorldPort.Release(
                    new ProjectileReleaseRequest(
                        projectile.TerminalTick,
                        projectile.ProjectileId,
                        projectile.RuntimeId,
                        projectile.TerminalReason));
                if (!worldRelease.IsSuccess)
                {
                    return worldRelease;
                }

                binding.WorldReleased = true;
                projectiles[projectileIndex] = binding;
            }

            if (!binding.BudgetReleased)
            {
                if (!projectile.ReservationToken.IsValid)
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                DomainResult budgetRelease = combatKernel.ProjectileBudget.ReleaseActive(
                    projectile.ReservationToken,
                    projectile.Definition.BudgetUnits);
                if (!budgetRelease.IsSuccess)
                {
                    return budgetRelease;
                }

                binding.BudgetReleased = true;
                projectiles[projectileIndex] = binding;
            }

            return DomainResult.Success;
        }
        private void RemoveScheduledPayloads(RuntimeId ownerRuntimeId)
        {
            for (int index = 0; index < scheduledPayloads.Length; index++)
            {
                if (scheduledPayloads[index].IsUsed
                    && scheduledPayloads[index].OwnerRuntimeId == ownerRuntimeId)
                {
                    ReservationToken budgetReservation =
                        scheduledPayloads[index].ProjectileBudgetReservation;
                    if (budgetReservation.IsValid)
                    {
                        combatKernel.ProjectileBudget.ReleaseReservation(
                            budgetReservation);
                    }

                    scheduledPayloads[index] = default(ScheduledPayload);
                }
            }
        }

        private void ClearState(bool preserveTrace)
        {
            for (int index = 0; index < enemies.Length; index++)
            {
                EnemyRuntime runtime = enemies[index].Runtime;
                if (runtime != null && !runtime.Combatant.IsDead)
                {
                    runtime.Combatant.ForceDeath();
                    runtime.MarkDead(combatKernel.ProjectileBudget);
                }

                enemies[index] = default(EnemyBinding);
            }

            skillImpactPresentationStream.Clear();
            for (int index = 0; index < projectiles.Length; index++)
            {
                ProjectileBinding binding = projectiles[index];
                ProjectileRuntime projectile = binding.Runtime;
                if (projectile == null)
                {
                    continue;
                }

                if (!projectile.IsTerminal)
                {
                    TickIndex cancelTick = currentTick.IsValid ? currentTick : projectile.SpawnTick;
                    projectile.TryCancel(cancelTick, ProjectileTerminalReason.SessionEnded);
                }

                TryPublishProjectileTerminal(index);

                if (!binding.WorldReleased && projectile.TerminalTick.IsValid
                    && projectile.TerminalReason != ProjectileTerminalReason.None)
                {
                    projectileWorldPort.Release(new ProjectileReleaseRequest(
                        projectile.TerminalTick,
                        projectile.ProjectileId,
                        projectile.RuntimeId,
                        projectile.TerminalReason));
                }

                projectiles[index] = default(ProjectileBinding);
            }

            Array.Clear(playerHitCommands, 0, playerHitCommands.Length);
            Array.Clear(playerHitDueBuffer, 0, playerHitDueBuffer.Length);
            Array.Clear(
                playerProjectileAreaCandidates,
                0,
                playerProjectileAreaCandidates.Length);
            Array.Clear(
                playerProjectileAreaSelected,
                0,
                playerProjectileAreaSelected.Length);
            Array.Clear(scheduledPayloads, 0, scheduledPayloads.Length);
            Array.Clear(
                enemySkillCapacityReservations,
                0,
                enemySkillCapacityReservations.Length);
            Array.Clear(threatAdvanceBuffer, 0, threatAdvanceBuffer.Length);
            Array.Clear(
                threatExecutionBindings,
                0,
                threatExecutionBindings.Length);
            Array.Clear(dueImpactBuffer, 0, dueImpactBuffer.Length);
            Array.Clear(
                dueImpactIsProjectile,
                0,
                dueImpactIsProjectile.Length);
            Array.Clear(
                projectileOriginImpactIds,
                0,
                projectileOriginImpactIds.Length);
            attackSchedule.Clear();

            combatKernel.ImpactQueue.Clear();
            combatKernel.ImpactLedger.Clear();
            combatKernel.ShotTargetLedger.Clear();
            combatKernel.ProjectileBudget.CancelAll();
            if (!preserveTrace)
            {
                combatKernel.Trace.Reset();
            }

            vitalsStream.Clear();
            damageFeedbackStream.Clear();

            enemyCount = 0;
            playerHitCommandCount = 0;
            lastPlayerHitCommandSequence = -1L;
            nextEnemySkillCapacityReservation = 1L;
            currentTick = TickIndex.Invalid;
            PresentationCallbackFaultCount = 0;
        }

        private int FindEnemy(RuntimeId runtimeId)
        {
            if (!runtimeId.IsValid)
            {
                return -1;
            }

            for (int index = 0; index < enemies.Length; index++)
            {
                if (enemies[index].RuntimeId == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeEnemy()
        {
            for (int index = 0; index < enemies.Length; index++)
            {
                if (enemies[index].Runtime == null)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindThreatExecutionBinding(RuntimeId threatRuntimeId)
        {
            if (!threatRuntimeId.IsValid)
            {
                return -1;
            }

            for (int index = 0;
                index < threatExecutionBindings.Length;
                index++)
            {
                if (threatExecutionBindings[index].IsUsed
                    && threatExecutionBindings[index].ThreatRuntimeId
                        == threatRuntimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeThreatExecutionBinding()
        {
            for (int index = 0;
                index < threatExecutionBindings.Length;
                index++)
            {
                if (!threatExecutionBindings[index].IsUsed)
                {
                    return index;
                }
            }

            return -1;
        }

        private void RemoveThreatExecutionBindings(
            RuntimeId ownerRuntimeId)
        {
            for (int index = 0;
                index < threatExecutionBindings.Length;
                index++)
            {
                if (threatExecutionBindings[index].OwnerRuntimeId
                    == ownerRuntimeId)
                {
                    threatExecutionBindings[index] =
                        default(ThreatExecutionBinding);
                }
            }
        }

        private bool IsValidSelfDestructDependency(
            FpgEnemyAttackCommand command)
        {
            long dependencySequence =
                command.Payload.SelfDestructDependencyScheduleSequence;
            if (!command.HasSkillCorrelation
                || dependencySequence >= command.Schedule.ScheduleSequence)
            {
                return false;
            }

            int dependencyIndex = FindScheduledPayload(dependencySequence);
            if (dependencyIndex < 0)
            {
                return false;
            }

            ScheduledPayload dependency =
                scheduledPayloads[dependencyIndex];
            return dependency.OwnerRuntimeId == command.Schedule.OwnerRuntimeId
                && dependency.SpawnSequence == command.SpawnSequence
                && dependency.Payload.Kind
                    == FpgEnemyAttackPayloadKind.Summon
                && dependency.SkillExecutionId
                    == command.SkillExecutionId;
        }

        private int FindScheduledPayload(long scheduleSequence)
        {
            for (int index = 0; index < scheduledPayloads.Length; index++)
            {
                if (scheduledPayloads[index].IsUsed
                    && scheduledPayloads[index].ScheduleSequence == scheduleSequence)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeScheduledPayload()
        {
            for (int index = 0; index < scheduledPayloads.Length; index++)
            {
                if (!scheduledPayloads[index].IsUsed)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindEnemySkillCapacityReservation(
            FpgEnemySkillCapacityReservation reservation)
        {
            if (!reservation.IsValid)
            {
                return -1;
            }

            for (int index = 0;
                index < enemySkillCapacityReservations.Length;
                index++)
            {
                if (enemySkillCapacityReservations[index].IsUsed
                    && enemySkillCapacityReservations[index].Reservation
                        == reservation)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeEnemySkillCapacityReservation()
        {
            for (int index = 0;
                index < enemySkillCapacityReservations.Length;
                index++)
            {
                if (!enemySkillCapacityReservations[index].IsUsed)
                {
                    return index;
                }
            }

            return -1;
        }

        private int CountEnemySkillCapacityReservations()
        {
            int count = 0;
            for (int index = 0;
                index < enemySkillCapacityReservations.Length;
                index++)
            {
                if (enemySkillCapacityReservations[index].IsUsed)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountRemainingReservedAttackEvents()
        {
            int count = 0;
            for (int index = 0;
                index < enemySkillCapacityReservations.Length;
                index++)
            {
                if (enemySkillCapacityReservations[index].IsUsed)
                {
                    count = checked(
                        count
                        + enemySkillCapacityReservations[index]
                            .RemainingAttackEvents);
                }
            }

            return count;
        }

        private int CountRemainingReservedImpactCapacity()
        {
            int count = 0;
            for (int index = 0;
                index < enemySkillCapacityReservations.Length;
                index++)
            {
                if (enemySkillCapacityReservations[index].IsUsed)
                {
                    count = checked(
                        count
                        + enemySkillCapacityReservations[index]
                            .RemainingImpactCapacity);
                }
            }

            return count;
        }

        private int CountScheduledProjectileCapacity()
        {
            int count = 0;
            for (int index = 0; index < scheduledPayloads.Length; index++)
            {
                if (scheduledPayloads[index].IsUsed)
                {
                    count = checked(
                        count
                        + GetProjectileCapacity(
                            scheduledPayloads[index].Payload));
                }
            }

            return count;
        }

        private int CountScheduledImpactCapacity()
        {
            int count = 0;
            for (int index = 0; index < scheduledPayloads.Length; index++)
            {
                if (scheduledPayloads[index].IsUsed)
                {
                    count = checked(
                        count
                        + GetImpactCapacity(
                            scheduledPayloads[index].Payload));
                }
            }

            return count;
        }

        private int CountScheduledSummonCapacity()
        {
            int count = 0;
            for (int index = 0; index < scheduledPayloads.Length; index++)
            {
                if (scheduledPayloads[index].IsUsed
                    && scheduledPayloads[index].Payload.Kind
                        == FpgEnemyAttackPayloadKind.Summon)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountScheduledThreatCapacity()
        {
            int count = 0;
            for (int index = 0; index < scheduledPayloads.Length; index++)
            {
                if (scheduledPayloads[index].IsUsed
                    && (!scheduledPayloads[index].CapacityReservation.IsValid
                        || FindEnemySkillCapacityReservation(
                            scheduledPayloads[index].CapacityReservation) < 0)
                    && scheduledPayloads[index].Payload.Kind
                        == FpgEnemyAttackPayloadKind.Threat)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountScheduledThreatCapacity(RuntimeId ownerRuntimeId)
        {
            int count = 0;
            for (int index = 0; index < scheduledPayloads.Length; index++)
            {
                if (scheduledPayloads[index].IsUsed
                    && scheduledPayloads[index].OwnerRuntimeId
                        == ownerRuntimeId
                    && (!scheduledPayloads[index].CapacityReservation.IsValid
                        || FindEnemySkillCapacityReservation(
                            scheduledPayloads[index].CapacityReservation) < 0)
                    && scheduledPayloads[index].Payload.Kind
                        == FpgEnemyAttackPayloadKind.Threat)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountActiveThreats(RuntimeId ownerRuntimeId)
        {
            int ownerIndex = FindEnemy(ownerRuntimeId);
            EnemyRuntime owner = ownerIndex < 0
                ? null
                : enemies[ownerIndex].Runtime;
            if (owner == null || owner.Combatant.IsDead)
            {
                return 0;
            }

            int count = 0;
            for (int threatIndex = 0;
                threatIndex < owner.ThreatCount;
                threatIndex++)
            {
                ThreatRuntime threat = owner.GetThreat(threatIndex);
                if (threat != null && !threat.IsTerminal)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool CanConsumeEnemySkillCapacity(
            EnemySkillCapacityReservationEntry entry,
            FpgEnemyAttackCommand command)
        {
            if (!entry.IsUsed
                || entry.OwnerRuntimeId != command.Schedule.OwnerRuntimeId
                || entry.RemainingAttackEvents <= 0)
            {
                return false;
            }

            int projectiles = GetProjectileCapacity(command.Payload);
            int impacts = GetImpactCapacity(command.Payload);
            int summons = GetSummonCapacity(command.Payload);
            bool requiresBudget = projectiles > 0
                && command.Payload.Threat.TotalBudgetUnits > 0;
            return projectiles <= entry.RemainingProjectileCapacity
                && impacts <= entry.RemainingImpactCapacity
                && summons <= entry.RemainingSummonCapacity
                && requiresBudget
                    == command.ProjectileBudgetReservation.IsValid;
        }

        private bool CanAcceptUnreservedEnemyAttack(
            FpgEnemyAttackCommand command)
        {
            int projectileCapacity = GetProjectileCapacity(command.Payload);
            int impactCapacity = GetImpactCapacity(command.Payload);
            int summonCapacity = GetSummonCapacity(command.Payload);
            long projectileDemand = CountActiveProjectiles()
                + CountScheduledProjectileCapacity()
                + projectileCapacity;
            long summonDemand = CountScheduledSummonCapacity()
                + summonCapacity;
            for (int index = 0;
                index < enemySkillCapacityReservations.Length;
                index++)
            {
                EnemySkillCapacityReservationEntry entry =
                    enemySkillCapacityReservations[index];
                if (!entry.IsUsed)
                {
                    continue;
                }

                projectileDemand += entry.RemainingProjectileCapacity;
                summonDemand += entry.RemainingSummonCapacity;
            }

            if (projectileDemand > capacity.ProjectileCapacity
                || summonDemand > capacity.SummonCapacity
                || !CanQueueImpacts(impactCapacity))
            {
                return false;
            }

            if (command.Payload.Kind != FpgEnemyAttackPayloadKind.Threat)
            {
                return true;
            }

            long globalThreatDemand = CountActiveThreats()
                + CountScheduledThreatCapacity()
                + 1L;
            long ownerThreatDemand = CountActiveThreats(
                    command.Schedule.OwnerRuntimeId)
                + CountScheduledThreatCapacity(
                    command.Schedule.OwnerRuntimeId)
                + 1L;
            for (int index = 0;
                index < enemySkillCapacityReservations.Length;
                index++)
            {
                EnemySkillCapacityReservationEntry entry =
                    enemySkillCapacityReservations[index];
                if (!entry.IsUsed)
                {
                    continue;
                }

                globalThreatDemand += entry.MaxConcurrentThreats;
                if (entry.OwnerRuntimeId
                    == command.Schedule.OwnerRuntimeId)
                {
                    ownerThreatDemand += entry.MaxConcurrentThreats;
                }
            }

            return globalThreatDemand <= capacity.ThreatAdvanceCapacity
                && ownerThreatDemand <= capacity.PerEnemyThreatCapacity;
        }

        private static void ConsumeEnemySkillCapacity(
            ref EnemySkillCapacityReservationEntry entry,
            FpgEnemyAttackPayload payload)
        {
            entry.RemainingAttackEvents--;
            entry.RemainingProjectileCapacity -=
                GetProjectileCapacity(payload);
            entry.RemainingImpactCapacity -= GetImpactCapacity(payload);
            entry.RemainingSummonCapacity -= GetSummonCapacity(payload);
        }

        private static void RestoreEnemySkillCapacity(
            ref EnemySkillCapacityReservationEntry entry,
            FpgEnemyAttackPayload payload)
        {
            entry.RemainingAttackEvents++;
            entry.RemainingProjectileCapacity +=
                GetProjectileCapacity(payload);
            entry.RemainingImpactCapacity += GetImpactCapacity(payload);
            entry.RemainingSummonCapacity += GetSummonCapacity(payload);
        }

        private static int GetProjectileCapacity(
            FpgEnemyAttackPayload payload)
        {
            return payload.Kind == FpgEnemyAttackPayloadKind.Threat
                    && payload.Threat.Payload.IsSweptProjectile
                ? payload.Threat.Payload.PayloadCount
                : 0;
        }

        private static int GetImpactCapacity(FpgEnemyAttackPayload payload)
        {
            if (payload.Kind != FpgEnemyAttackPayloadKind.Threat)
            {
                return 0;
            }

            return payload.Threat.Payload.IsSweptProjectile
                ? payload.Threat.Payload.PayloadCount
                : payload.Threat.Payload.IsTimedImpact ? 1 : 0;
        }

        private static int GetSummonCapacity(FpgEnemyAttackPayload payload)
        {
            return payload.Kind == FpgEnemyAttackPayloadKind.Summon ? 1 : 0;
        }

        private int FindProjectile(RuntimeId runtimeId, bool includeTerminal)
        {
            if (!runtimeId.IsValid)
            {
                return -1;
            }

            for (int index = 0; index < projectiles.Length; index++)
            {
                ProjectileRuntime projectile = projectiles[index].Runtime;
                if (projectile != null
                    && projectile.RuntimeId == runtimeId
                    && (includeTerminal || !projectile.IsTerminal))
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeProjectile()
        {
            for (int index = 0; index < projectiles.Length; index++)
            {
                if (projectiles[index].Runtime == null)
                {
                    return index;
                }
            }

            return -1;
        }

        private int CountFreeProjectileSlots()
        {
            int count = 0;
            for (int index = 0; index < projectiles.Length; index++)
            {
                if (projectiles[index].Runtime == null)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountActiveThreats()
        {
            int count = 0;
            for (int ownerIndex = 0; ownerIndex < enemies.Length; ownerIndex++)
            {
                EnemyRuntime owner = enemies[ownerIndex].Runtime;
                if (owner == null || owner.Combatant.IsDead)
                {
                    continue;
                }

                for (int threatIndex = 0; threatIndex < owner.ThreatCount; threatIndex++)
                {
                    ThreatRuntime threat = owner.GetThreat(threatIndex);
                    if (threat != null && !threat.IsTerminal)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private int CountActiveProjectiles()
        {
            int count = 0;
            for (int index = 0; index < projectiles.Length; index++)
            {
                if (projectiles[index].Runtime != null && !projectiles[index].Runtime.IsTerminal)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountActiveProjectileImpactCapacity()
        {
            int count = 0;
            for (int index = 0; index < projectiles.Length; index++)
            {
                ProjectileBinding binding = projectiles[index];
                if (binding.Runtime == null || binding.Runtime.IsTerminal)
                {
                    continue;
                }

                count += binding.ImpactCapacityReservation;
            }

            return count;
        }

        private static void SortPlayerHits(FpgPlayerHitCommand[] values, int count)
        {
            for (int index = 1; index < count; index++)
            {
                FpgPlayerHitCommand candidate = values[index];
                int destination = index - 1;
                while (destination >= 0 && ComparePlayerHit(candidate, values[destination]) < 0)
                {
                    values[destination + 1] = values[destination];
                    destination--;
                }

                values[destination + 1] = candidate;
            }
        }

        private static int ComparePlayerHit(FpgPlayerHitCommand left, FpgPlayerHitCommand right)
        {
            int comparison = left.Priority.CompareTo(right.Priority);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Intent.TargetId.CompareTo(right.Intent.TargetId);
            return comparison != 0
                ? comparison
                : left.CommandSequence.CompareTo(right.CommandSequence);
        }
        private static void SortThreats(ThreatAdvanceBinding[] values, int count)
        {
            for (int index = 1; index < count; index++)
            {
                ThreatAdvanceBinding candidate = values[index];
                int destination = index - 1;
                while (destination >= 0 && CompareThreat(candidate, values[destination]) < 0)
                {
                    values[destination + 1] = values[destination];
                    destination--;
                }

                values[destination + 1] = candidate;
            }
        }

        private static int CompareThreat(ThreatAdvanceBinding left, ThreatAdvanceBinding right)
        {
            long leftTick = left.Threat.StateUntilTick.IsValid
                ? left.Threat.StateUntilTick.Value
                : long.MaxValue;
            long rightTick = right.Threat.StateUntilTick.IsValid
                ? right.Threat.StateUntilTick.Value
                : long.MaxValue;
            int comparison = leftTick.CompareTo(rightTick);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.SpawnSequence.CompareTo(right.SpawnSequence);
            return comparison != 0
                ? comparison
                : left.Threat.RuntimeId.CompareTo(right.Threat.RuntimeId);
        }

        private struct EnemyBinding
        {
            public EnemyBinding(EnemyRuntime runtime, int spawnSequence)
            {
                Runtime = runtime;
                SpawnSequence = spawnSequence;
                DeathNotified = false;
                DeathSourceRuntimeId = RuntimeId.Invalid;
                DeathAttackId = AttackId.Invalid;
                DeathTick = TickIndex.Invalid;
            }

            public EnemyRuntime Runtime;
            public int SpawnSequence;
            public bool DeathNotified;
            public RuntimeId DeathSourceRuntimeId;
            public AttackId DeathAttackId;
            public TickIndex DeathTick;
            public RuntimeId RuntimeId => Runtime == null ? RuntimeId.Invalid : Runtime.RuntimeId;
        }

        private enum SelfDestructDependencyState
        {
            None = 0,
            Waiting,
            Queued,
            Skipped
        }

        private readonly struct ScheduledPayload
        {
            public ScheduledPayload(FpgEnemyAttackCommand command)
                : this(
                    command.Schedule.OwnerRuntimeId,
                    command.Schedule.ScheduleSequence,
                    command.SpawnSequence,
                    command.Payload,
                    command.CapacityReservation,
                    command.ProjectileBudgetReservation,
                    command.SpatialContext,
                    command.SkillExecutionId,
                    command.GameplayEventId,
                    command.Payload.Kind
                            == FpgEnemyAttackPayloadKind.SelfDestructOwner
                        && command.Payload.HasSelfDestructDependency
                        ? SelfDestructDependencyState.Waiting
                        : SelfDestructDependencyState.None,
                    false)
            {
            }

            private ScheduledPayload(
                RuntimeId ownerRuntimeId,
                long scheduleSequence,
                int spawnSequence,
                FpgEnemyAttackPayload payload,
                FpgEnemySkillCapacityReservation capacityReservation,
                ReservationToken projectileBudgetReservation,
                FpgEnemyAttackSpatialContext spatialContext,
                SkillExecutionId skillExecutionId,
                int gameplayEventId,
                SelfDestructDependencyState selfDestructDependencyState,
                bool presentationStarted)
            {
                OwnerRuntimeId = ownerRuntimeId;
                ScheduleSequence = scheduleSequence;
                SpawnSequence = spawnSequence;
                Payload = payload;
                CapacityReservation = capacityReservation;
                ProjectileBudgetReservation = projectileBudgetReservation;
                SpatialContext = spatialContext;
                SkillExecutionId = skillExecutionId;
                GameplayEventId = gameplayEventId;
                DependencyState = selfDestructDependencyState;
                PresentationStarted = presentationStarted;
                IsUsed = true;
            }

            public RuntimeId OwnerRuntimeId { get; }
            public long ScheduleSequence { get; }
            public int SpawnSequence { get; }
            public FpgEnemyAttackPayload Payload { get; }
            public FpgEnemySkillCapacityReservation CapacityReservation { get; }
            public ReservationToken ProjectileBudgetReservation { get; }
            public FpgEnemyAttackSpatialContext SpatialContext { get; }
            public SkillExecutionId SkillExecutionId { get; }
            public int GameplayEventId { get; }
            public SelfDestructDependencyState DependencyState { get; }
            public bool PresentationStarted { get; }
            public bool IsUsed { get; }

            public bool IsCommittedSummon =>
                IsUsed
                && PresentationStarted
                && Payload.Kind == FpgEnemyAttackPayloadKind.Summon
                && Payload.Summon.ReleaseDelayTicks > 0;

            public bool BypassesOwnerControlEligibility =>
                IsCommittedSummon
                || (IsUsed
                    && Payload.Kind
                        == FpgEnemyAttackPayloadKind.SelfDestructOwner
                    && (DependencyState
                            == SelfDestructDependencyState.Queued
                        || DependencyState
                            == SelfDestructDependencyState.Skipped));

            public ScheduledPayload WithPresentationStarted()
            {
                return new ScheduledPayload(
                    OwnerRuntimeId,
                    ScheduleSequence,
                    SpawnSequence,
                    Payload,
                    CapacityReservation,
                    ProjectileBudgetReservation,
                    SpatialContext,
                    SkillExecutionId,
                    GameplayEventId,
                    DependencyState,
                    true);
            }

            public ScheduledPayload WithSelfDestructDependencyState(
                SelfDestructDependencyState state)
            {
                return new ScheduledPayload(
                    OwnerRuntimeId,
                    ScheduleSequence,
                    SpawnSequence,
                    Payload,
                    CapacityReservation,
                    ProjectileBudgetReservation,
                    SpatialContext,
                    SkillExecutionId,
                    GameplayEventId,
                    state,
                    PresentationStarted);
            }
        }

        private struct EnemySkillCapacityReservationEntry
        {
            public EnemySkillCapacityReservationEntry(
                FpgEnemySkillCapacityReservation reservation,
                RuntimeId ownerRuntimeId,
                int remainingAttackEvents,
                int remainingProjectileCapacity,
                int remainingImpactCapacity,
                int remainingSummonCapacity,
                int maxConcurrentThreats)
            {
                Reservation = reservation;
                OwnerRuntimeId = ownerRuntimeId;
                RemainingAttackEvents = remainingAttackEvents;
                RemainingProjectileCapacity = remainingProjectileCapacity;
                RemainingImpactCapacity = remainingImpactCapacity;
                RemainingSummonCapacity = remainingSummonCapacity;
                MaxConcurrentThreats = maxConcurrentThreats;
                IsUsed = true;
            }

            public FpgEnemySkillCapacityReservation Reservation;
            public RuntimeId OwnerRuntimeId;
            public int RemainingAttackEvents;
            public int RemainingProjectileCapacity;
            public int RemainingImpactCapacity;
            public int RemainingSummonCapacity;
            public int MaxConcurrentThreats;
            public bool IsUsed;
        }

        private struct ProjectileBinding
        {
            public ProjectileBinding(
                ProjectileRuntime runtime,
                RuntimeId targetRuntimeId,
                ProjectilePathSnapshot path,
                SkillExecutionId skillExecutionId,
                int gameplayEventId,
                int presentationOrdinal = 0)
            {
                if (presentationOrdinal < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(presentationOrdinal));
                }

                Runtime = runtime;
                TargetRuntimeId = targetRuntimeId;
                Path = path;
                SkillExecutionId = skillExecutionId;
                GameplayEventId = gameplayEventId;
                IsPlayerAreaProjectile = false;
                PlayerAttack = default(AttackSnapshot);
                ImpactCapacityReservation = 1;
                PresentationOrdinal = presentationOrdinal;
                PresentationTerminalPublished = false;
                WorldReleased = false;
                BudgetReleased = false;
            }

            public ProjectileBinding(
                ProjectileRuntime runtime,
                RuntimeId targetRuntimeId,
                ProjectilePathSnapshot path,
                SkillExecutionId skillExecutionId,
                int gameplayEventId,
                AttackSnapshot playerAttack,
                int impactCapacityReservation,
                int presentationOrdinal = 0)
                : this(
                    runtime,
                    targetRuntimeId,
                    path,
                    skillExecutionId,
                    gameplayEventId,
                    presentationOrdinal)
            {
                if (playerAttack.Team != Team.Player
                    || !playerAttack.IsQueryConfigurationValid
                    || playerAttack.QueryMode
                        != AttackQueryMode.AreaAtFirstSurface
                    || impactCapacityReservation <= 0)
                {
                    throw new ArgumentException(
                        "Player projectile binding is invalid.");
                }

                IsPlayerAreaProjectile = true;
                PlayerAttack = playerAttack;
                ImpactCapacityReservation = impactCapacityReservation;
            }

            public ProjectileRuntime Runtime;
            public RuntimeId TargetRuntimeId;
            public ProjectilePathSnapshot Path;
            public SkillExecutionId SkillExecutionId;
            public int GameplayEventId;
            public bool IsPlayerAreaProjectile;
            public AttackSnapshot PlayerAttack;
            public int ImpactCapacityReservation;
            public int PresentationOrdinal;
            public bool PresentationTerminalPublished;
            public bool WorldReleased;
            public bool BudgetReleased;
        }
        private readonly struct ThreatExecutionBinding
        {
            public ThreatExecutionBinding(
                RuntimeId ownerRuntimeId,
                RuntimeId threatRuntimeId,
                FpgEnemyAttackSpatialContext spatialContext,
                SkillExecutionId skillExecutionId,
                int gameplayEventId)
            {
                OwnerRuntimeId = ownerRuntimeId;
                ThreatRuntimeId = threatRuntimeId;
                SpatialContext = spatialContext;
                SkillExecutionId = skillExecutionId;
                GameplayEventId = gameplayEventId;
            }

            public RuntimeId OwnerRuntimeId { get; }
            public RuntimeId ThreatRuntimeId { get; }
            public FpgEnemyAttackSpatialContext SpatialContext { get; }
            public SkillExecutionId SkillExecutionId { get; }
            public int GameplayEventId { get; }
            public bool IsUsed => OwnerRuntimeId.IsValid
                && ThreatRuntimeId.IsValid
                && SpatialContext.IsValid
                && SkillExecutionId.IsValid
                && GameplayEventId > 0;
        }

        private readonly struct ThreatAdvanceBinding
        {
            public ThreatAdvanceBinding(
                int ownerIndex,
                int spawnSequence,
                ThreatRuntime threat,
                ThreatExecutionBinding execution)
            {
                OwnerIndex = ownerIndex;
                SpawnSequence = spawnSequence;
                Threat = threat;
                Execution = execution;
            }

            public int OwnerIndex { get; }
            public int SpawnSequence { get; }
            public ThreatRuntime Threat { get; }
            public ThreatExecutionBinding Execution { get; }
        }    }
}
