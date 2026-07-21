using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;

namespace FPG.Demo.Run
{
    /// <summary>
    /// Concrete pure-C# combat port for the formal encounter path. It reuses
    /// CombatKernel, PlayerRuntime, EnemyRuntime, ThreatRuntime and
    /// ProjectileRuntime while keeping every collection fixed-capacity and
    /// every combatant lookup keyed by RuntimeId.
    /// </summary>
    public sealed class FpgMultiEnemyCombatPort : IFpgEncounterCombatTickPort, IFpgAttackOwnerEligibility
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
        private readonly ProjectileBinding[] projectiles;
        private readonly ThreatAdvanceBinding[] threatAdvanceBuffer;
        private readonly QueuedImpact[] dueImpactBuffer;
        private readonly IProjectileWorldPort projectileWorldPort;
        private readonly IFpgSummonRequestSink summonRequestSink;

        private TickIndex currentTick = TickIndex.Invalid;
        private int enemyCount;
        private int playerHitCommandCount;
        private long lastPlayerHitCommandSequence = -1L;

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
            projectiles = new ProjectileBinding[capacity.ProjectileCapacity];
            threatAdvanceBuffer = new ThreatAdvanceBinding[capacity.ThreatAdvanceCapacity];
            dueImpactBuffer = new QueuedImpact[combatKernel.ImpactQueue.Capacity];

        }

        public bool IsPlayerAlive => !player.Combatant.IsDead;
        public int EnemyRuntimeCount => enemyCount;
        public int PendingPlayerHitCount => playerHitCommandCount;
        public int PendingAttackCount => attackSchedule.Count;
        public int ActiveProjectileCount => CountActiveProjectiles();
        public TickIndex CurrentTick => currentTick;
        public CombatKernel CombatKernel => combatKernel;
        public PlayerRuntime Player => player;

        public event Action<FpgEnemyDiedEvent> EnemyDied;
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

            if (playerHitCommandCount >= playerHitCommands.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            playerHitCommands[playerHitCommandCount++] = command;
            lastPlayerHitCommandSequence = command.CommandSequence;
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

        public void ClearAll()
        {
            ClearState(preserveTrace: false);
        }

        private DomainResult ProcessEnemyRecovery(FpgEnemyRoster roster, TickIndex tick)
        {
            DomainResult synchronized = SynchronizeRoster(roster, tick);
            if (!synchronized.IsSuccess)
            {
                return synchronized;
            }

            player.Combatant.TryRestoreBarrier(tick);
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
                    0);
                DomainResult resolved = ResolveImpact(command.Intent);
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
                && attackSchedule.TryDequeueDue(tick, this, out FpgAttackScheduleRequest request, out int spawnSequence))
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
                if (ownerIndex < 0 || !CanAttack(request.OwnerRuntimeId))
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

            if (CountActiveThreats() >= threatAdvanceBuffer.Length)
            {
                return RescheduleForNextTick(request, scheduled.SpawnSequence, tick);
            }

            if (definition.TotalBudgetUnits > 0)
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

            scheduledPayloads[payloadIndex] = default(ScheduledPayload);
            combatKernel.Trace.Record(
                tick,
                CombatEventType.ThreatScheduleDecision,
                owner.RuntimeId,
                threat.RuntimeId,
                threat.AttackId,
                ImpactId.Invalid,
                scheduled.SpawnSequence,
                definition.DefinitionId);
            return DomainResult.Success;
        }

        private DomainResult DispatchScheduledSummon(
            FpgAttackScheduleRequest request,
            ScheduledPayload scheduled,
            int payloadIndex,
            TickIndex tick)
        {
            FpgSummonRequest summonRequest = scheduled.Payload.Summon.Request;
            FpgSummonQueueAck acknowledgement = summonRequestSink.TryQueueSummon(
                summonRequest,
                tick);
            switch (acknowledgement.Disposition)
            {
                case FpgSummonQueueDisposition.Queued:
                    scheduledPayloads[payloadIndex] = default(ScheduledPayload);
                    SummonRequested?.Invoke(summonRequest);
                    return DomainResult.Success;

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
        private DomainResult RescheduleForNextTick(
            FpgAttackScheduleRequest request,
            int spawnSequence,
            TickIndex tick)
        {
            FpgAttackScheduleRequest retry = new FpgAttackScheduleRequest(
                request.OwnerRuntimeId,
                tick + new TickDuration(1),
                request.Priority,
                request.ScheduleSequence,
                request.AttackPatternId);
            return attackSchedule.TrySchedule(retry, spawnSequence);
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
                    if (threat == null || threat.IsTerminal)
                    {
                        continue;
                    }

                    if (threatCount >= threatAdvanceBuffer.Length)
                    {
                        return DomainResult.Rejected(RejectReason.BufferCapacity);
                    }

                    threatAdvanceBuffer[threatCount++] = new ThreatAdvanceBinding(
                        ownerIndex,
                        binding.SpawnSequence,
                        threat);
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
                ? QueueTimedImpact(owner.RuntimeId, threat.RuntimeId, release, tick)
                : CreateProjectiles(owner.RuntimeId, release, tick);
            if (!payloadResult.IsSuccess)
            {
                return payloadResult;
            }

            DomainResult confirmed = threat.ConfirmPayloadsCreated(tick);
            if (!confirmed.IsSuccess)
            {
                return confirmed;
            }

            combatKernel.Trace.Record(
                tick,
                CombatEventType.ThreatStateChanged,
                owner.RuntimeId,
                threat.RuntimeId,
                threat.AttackId,
                ImpactId.Invalid,
                (int)ThreatState.Windup,
                (int)ThreatState.Recovery);
            return DomainResult.Success;
        }

        private DomainResult QueueTimedImpact(
            RuntimeId ownerRuntimeId,
            RuntimeId threatRuntimeId,
            ThreatRelease release,
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
                player.RuntimeId,
                tick + payload.ImpactDelay,
                payload.TimedImpactDamage,
                HitPart.Body,
                DamageType.Normal,
                CombatTags.EnemyAttack);
            return combatKernel.ImpactQueue.TryEnqueue(
                intent,
                ImpactPhasePriority.EnemyImpact,
                threatRuntimeId);
        }

        private DomainResult CreateProjectiles(
            RuntimeId ownerRuntimeId,
            ThreatRelease release,
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
                    player.RuntimeId,
                    projectile.Team,
                    projectile.Definition.DefinitionId,
                    projectile.Definition.SweepRadiusKey,
                    projectile.Definition.PresentationKey,
                    projectile.Definition.Interceptable);
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
                    player.RuntimeId,
                    path);
                combatKernel.Trace.Record(
                    tick,
                    CombatEventType.ProjectileStateChanged,
                    ownerRuntimeId,
                    projectile.RuntimeId,
                    projectile.AttackId,
                    ImpactId.Invalid,
                    (int)ProjectileState.Scheduled,
                    (int)ProjectileState.Travelling);
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

                            if (!CanQueueImpacts(1))
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
                                projectile.RuntimeId);
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
                            (int)projectile.State);
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
                        (int)projectile.State);
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
                ImpactIntent intent = dueImpactBuffer[index].Intent;
                dueImpactBuffer[index] = default(QueuedImpact);
                DomainResult resolved = ResolveImpact(intent);
                if (!resolved.IsSuccess)
                {
                    return resolved;
                }
            }

            return DomainResult.Success;
        }

        private DomainResult ResolveImpact(ImpactIntent intent)
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

                RecordResolution(intent, projectileResolution);
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
                    return ConsumeStaleImpact(intent);
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

                RecordResolution(intent, resolution);
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
                        0);
                }

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
                    return ConsumeStaleImpact(intent);
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

                RecordResolution(intent, resolution);
                PublishHealthChanged(player.Combatant, intent.ImpactTick, resolution);
                return DomainResult.Success;
            }

            return ConsumeStaleImpact(intent);
        }

        private DomainResult ConsumeStaleImpact(ImpactIntent intent)
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
                RejectReason.InvalidTarget);
            return DomainResult.Success;
        }

        private void RecordResolution(ImpactIntent intent, ImpactResolution resolution)
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
                resolution.PerfectRetract);
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
                resolution.PerfectRetract);
        }

        private void PublishHealthChanged(
            EnemyRuntime runtime,
            TickIndex tick,
            ImpactResolution resolution)
        {
            CombatantState combatant = runtime.Combatant;
            HealthChanged?.Invoke(new FpgCombatHealthChangedEvent(
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
            HealthChanged?.Invoke(new FpgCombatHealthChangedEvent(
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

        private bool CanQueueImpacts(int additionalCount)
        {
            if (additionalCount < 0)
            {
                return false;
            }

            return combatKernel.ImpactQueue.Count + additionalCount <= combatKernel.ImpactQueue.Capacity
                && combatKernel.ImpactQueue.Count + additionalCount
                    <= combatKernel.ImpactLedger.RemainingCapacity;
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
            Array.Clear(threatAdvanceBuffer, 0, threatAdvanceBuffer.Length);
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

            enemyCount = 0;
            playerHitCommandCount = 0;
            lastPlayerHitCommandSequence = -1L;
            currentTick = TickIndex.Invalid;
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
            {
                OwnerRuntimeId = command.Schedule.OwnerRuntimeId;
                ScheduleSequence = command.Schedule.ScheduleSequence;
                SpawnSequence = command.SpawnSequence;
                Payload = command.Payload;
                IsUsed = true;
            }

            public RuntimeId OwnerRuntimeId { get; }
            public long ScheduleSequence { get; }
            public int SpawnSequence { get; }
            public FpgEnemyAttackPayload Payload { get; }
            public bool IsUsed { get; }
        }

        private struct ProjectileBinding
        {
            public ProjectileBinding(
                ProjectileRuntime runtime,
                RuntimeId targetRuntimeId,
                ProjectilePathSnapshot path)
            {
                Runtime = runtime;
                TargetRuntimeId = targetRuntimeId;
                Path = path;
                WorldReleased = false;
                BudgetReleased = false;
            }

            public ProjectileRuntime Runtime;
            public RuntimeId TargetRuntimeId;
            public ProjectilePathSnapshot Path;
            public bool WorldReleased;
            public bool BudgetReleased;
        }

        private readonly struct ThreatAdvanceBinding
        {
            public ThreatAdvanceBinding(int ownerIndex, int spawnSequence, ThreatRuntime threat)
            {
                OwnerIndex = ownerIndex;
                SpawnSequence = spawnSequence;
                Threat = threat;
            }

            public int OwnerIndex { get; }
            public int SpawnSequence { get; }
            public ThreatRuntime Threat { get; }
        }
    }
}
