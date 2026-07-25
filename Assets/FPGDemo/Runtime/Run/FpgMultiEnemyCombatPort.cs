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
        private readonly FpgPlayerDefensePolicy playerDefense;
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
        private readonly IProjectileWorldPort projectileWorldPort;
        private readonly IFpgSummonRequestSink summonRequestSink;
        private readonly FixedFpgVitalsStream vitalsStream;
        private readonly FixedResolvedDamageFeedbackStream damageFeedbackStream;

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
            FpgPlayerDefensePolicy? playerDefense = null)
        {
            this.combatKernel = combatKernel ?? throw new ArgumentNullException(nameof(combatKernel));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.idAllocator = idAllocator ?? throw new ArgumentNullException(nameof(idAllocator));
            this.projectileWorldPort = projectileWorldPort
                ?? throw new ArgumentNullException(nameof(projectileWorldPort));
            this.summonRequestSink = summonRequestSink
                ?? throw new ArgumentNullException(nameof(summonRequestSink));
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
            this.playerDefense = playerDefense ?? FpgPlayerDefensePolicy.Default;
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
            vitalsStream = new FixedFpgVitalsStream(
                capacity.EnemyCapacity + 1,
                capacity.VitalsEventCapacity);
            damageFeedbackStream = new FixedResolvedDamageFeedbackStream(
                capacity.DamageFeedbackCapacity);
            PublishVitals(
                player.Combatant,
                new TickIndex(0L),
                FpgVitalsChangeReason.Spawn,
                force: true);
        }

        public bool IsPlayerAlive => !player.Combatant.IsDead;
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
        public int PresentationCallbackFaultCount { get; private set; }

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
                + CountActiveProjectiles()
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
                && scheduled.IsCommittedSummon;
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

            if (player.Combatant.TryRestoreBarrier(tick))
            {
                PublishVitals(
                    player.Combatant,
                    tick,
                    FpgVitalsChangeReason.BarrierRestore);
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
                    command.GameplayEventId);
                playerHitDueBuffer[index] = default(FpgPlayerHitCommand);
                if (!resolved.IsSuccess)
                {
                    return resolved;
                }
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
                    || (!scheduled.IsCommittedSummon
                        && !CanAttack(request.OwnerRuntimeId)))
                {
                    return DomainResult.Rejected(RejectReason.InvalidTarget);
                }

                DomainResult handled = scheduled.Payload.Kind == FpgEnemyAttackPayloadKind.Threat
                    ? StartScheduledThreat(ownerIndex, request, scheduled, payloadIndex, tick)
                    : DispatchScheduledSummon(request, scheduled, payloadIndex, tick);
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
                    return ApplySummonOwnerOutcome(
                        summonRequest.OwnerRuntimeId,
                        summon.OwnerOutcome,
                        tick);

                case FpgSummonQueueDisposition.RetryNextTick:
                    return RescheduleForNextTick(request, scheduled.SpawnSequence, tick);

                case FpgSummonQueueDisposition.StaticLimitReached:
                    scheduledPayloads[payloadIndex] = default(ScheduledPayload);
                    return DomainResult.Success;

                case FpgSummonQueueDisposition.Rejected:
                    return acknowledgement.Result.IsSuccess
                        ? DomainResult.Rejected(RejectReason.InvariantFault)
                        : acknowledgement.Result;

                default:
                    return DomainResult.Rejected(RejectReason.InvariantFault);
            }
        }

        private DomainResult ApplySummonOwnerOutcome(
            RuntimeId ownerRuntimeId,
            FpgSummonOwnerOutcome outcome,
            TickIndex tick)
        {
            if (outcome == FpgSummonOwnerOutcome.RemainAlive)
            {
                return DomainResult.Success;
            }

            if (outcome != FpgSummonOwnerOutcome.DieAfterSuccessfulSummon)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

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
                    projectile.Definition.PresentationKey,
                    projectile.Definition.Interceptable,
                    execution.SpatialContext.Origin,
                    execution.SpatialContext.Target);
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
                    execution.GameplayEventId);
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
                    DomainResult terminalRelease = ReleaseProjectileResources(index);
                    if (!terminalRelease.IsSuccess)
                    {
                        return terminalRelease;
                    }

                    projectiles[index] = default(ProjectileBinding);
                    continue;
                }

                if (projectile.State != ProjectileState.Travelling || tick <= projectile.SpawnTick)
                {
                    continue;
                }

                if (tick <= projectile.ImpactTick)
                {
                    if (binding.Path.ProjectileId != projectile.ProjectileId
                        || binding.Path.RuntimeId != projectile.RuntimeId
                        || binding.Path.SpawnTick != projectile.SpawnTick
                        || binding.Path.ArrivalTick != projectile.ImpactTick
                        || !binding.TargetRuntimeId.IsValid)
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

                    ProjectileState previous = projectile.State;
                    ImpactId impactId = ImpactId.Invalid;
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
                            DomainResult blocked = projectile.TryBlock(tick);
                            if (!blocked.IsSuccess)
                            {
                                return blocked;
                            }
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
                                CombatTags.EnemyAttack);
                            DomainResult queued = combatKernel.ImpactQueue.TryEnqueue(
                                intent,
                                ImpactPhasePriority.EnemyImpact,
                                projectile.RuntimeId,
                                binding.SkillExecutionId.Value,
                                binding.GameplayEventId);
                            if (!queued.IsSuccess)
                            {
                                return queued;
                            }

                            DomainResult hit = projectile.TryHit(tick);
                            if (!hit.IsSuccess)
                            {
                                return hit;
                            }
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
        private DomainResult ProcessImpactResolution(TickIndex tick)
        {
            int count = combatKernel.ImpactQueue.DrainDue(tick, dueImpactBuffer);
            for (int index = 0; index < count; index++)
            {
                QueuedImpact queued = dueImpactBuffer[index];
                ImpactIntent intent = queued.Intent;
                dueImpactBuffer[index] = default(QueuedImpact);
                DomainResult resolved = ResolveImpact(
                    intent,
                    queued.SkillExecutionId,
                    queued.GameplayEventId);
                if (!resolved.IsSuccess)
                {
                    return resolved;
                }
            }

            return DomainResult.Success;
        }

        private DomainResult ResolveImpact(
            ImpactIntent intent,
            long skillExecutionId = 0L,
            int gameplayEventId = 0)
        {
            int projectileIndex = FindProjectile(intent.TargetId, includeTerminal: false);
            if (projectileIndex >= 0)
            {
                ProjectileRuntime projectile = projectiles[projectileIndex].Runtime;
                ImpactResolution projectileResolution = combatKernel.DamageResolver.ResolveProjectile(
                    intent,
                    projectile);
                if (!projectileResolution.Result.IsSuccess)
                {
                    return projectileResolution.Result;
                }

                RecordResolution(intent, projectileResolution, skillExecutionId, gameplayEventId);
                if (projectileResolution.ProjectileDestroyed)
                {
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
                    return ConsumeStaleImpact(intent, skillExecutionId, gameplayEventId);
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

                RecordResolution(intent, resolution, skillExecutionId, gameplayEventId);
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
                    return ConsumeStaleImpact(intent, skillExecutionId, gameplayEventId);
                }

                ImpactResolution resolution = combatKernel.DamageResolver.ResolveCombatant(
                    intent,
                    player.Combatant,
                    playerDefense.CreateSnapshot(player),
                    false);
                if (!resolution.Result.IsSuccess)
                {
                    return resolution.Result;
                }

                RecordResolution(intent, resolution, skillExecutionId, gameplayEventId);
                PublishVitals(
                    player.Combatant,
                    intent.ImpactTick,
                    resolution.Death
                        ? FpgVitalsChangeReason.Death
                        : FpgVitalsChangeReason.Damage);
                PublishHealthChanged(player.Combatant, intent.ImpactTick, resolution);
                return DomainResult.Success;
            }

            return ConsumeStaleImpact(intent, skillExecutionId, gameplayEventId);
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
            int gameplayEventId)
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
            if (additionalCount < 0
                || releasingProjectileCredits < 0
                || releasingProjectileCredits > CountActiveProjectiles())
            {
                return false;
            }

            long demand = combatKernel.ImpactQueue.Count
                + CountActiveProjectiles()
                - releasingProjectileCredits
                + CountScheduledImpactCapacity()
                + CountRemainingReservedImpactCapacity()
                + additionalCount;
            return demand <= combatKernel.ImpactQueue.Capacity
                && demand <= combatKernel.ImpactLedger.RemainingCapacity;
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
            public bool PresentationStarted { get; }
            public bool IsUsed { get; }

            public bool IsCommittedSummon =>
                IsUsed
                && PresentationStarted
                && Payload.Kind == FpgEnemyAttackPayloadKind.Summon
                && Payload.Summon.ReleaseDelayTicks > 0;

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
                    true);
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
                int gameplayEventId)
            {
                Runtime = runtime;
                TargetRuntimeId = targetRuntimeId;
                Path = path;
                SkillExecutionId = skillExecutionId;
                GameplayEventId = gameplayEventId;
                WorldReleased = false;
                BudgetReleased = false;
            }

            public ProjectileRuntime Runtime;
            public RuntimeId TargetRuntimeId;
            public ProjectilePathSnapshot Path;
            public SkillExecutionId SkillExecutionId;
            public int GameplayEventId;
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
