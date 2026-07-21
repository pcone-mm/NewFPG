using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;

namespace FPG.Demo.Run
{
    public sealed class BattleSession : IDisposable
    {
        private readonly GameplayClock clock;
        private readonly SessionIdAllocator idAllocator;
        private readonly CombatKernel combatKernel;
        private readonly IAttackResolutionPort attackResolutionPort;
        private readonly IAttackQueryPort attackQueryPort;
        private readonly IProjectileWorldPort projectileWorldPort;
        private readonly ISpatialDigestView spatialDecisionView;
        private readonly ICommittedPlayerShotPresentationSink committedPlayerShotPresentationSink;
        private readonly IUncommittedPlayerShotPresentationSink uncommittedPlayerShotPresentationSink;
        private readonly WeaponReleaseBuffer weaponReleaseBuffer;
        private readonly InputEdgeCommand[] inputEdgeBuffer;
        private readonly QueryCandidate[] queryCandidateBuffer;
        private readonly QueryCandidate[] selectedCandidateBuffer;
        private readonly SelectedAttackHit[] selectedHitWriteBuffer;
        private readonly SelectedAttackHitStream selectedAttackHits;
        private readonly ResolvedAttackHit[] resolvedHitBuffer;
        private readonly QueuedImpact[] dueImpactBuffer;
        private readonly ProjectileRuntime[] projectileSlots;
        private readonly ProjectileRuntime[] projectileAdvanceBuffer;
        private readonly ProjectileRuntime[] projectileRegistrationBuffer;
        private readonly ProjectilePathSnapshot[] projectileRegistrationPathBuffer;
        private readonly ProjectileRuntime[] failedRegistrationProjectileBuffer;
        private readonly bool[] failedRegistrationBudgetReleased;
        private readonly bool[] failedRegistrationWorldReleased;
        private readonly ProjectilePathSnapshot[] projectilePathSlots;
        private readonly RuntimeId[] projectileTargetSlots;
        private readonly ThreatRuntime[] threatAdvanceBuffer;
        private readonly ThreatState[] threatStateBuffer;
        private readonly bool[] projectileBudgetReleased;
        private readonly bool[] projectileWorldRegistered;
        private readonly bool[] projectileWorldReleased;
        private readonly bool useSpatialAttackQuery;
        private readonly EnemyRuntime[] enemyRuntimes;

        private int projectileSlotCount;
        private int enemyRuntimeCount;
        private int enemySpawnCursor;
        private int activeEnemyDefinitionId = 1;
        private int failedRegistrationProjectileCount;
        private long lastControlSequence;
        private long controlCommandCount;
        private ulong controlCommandDigest;
        private long lastThreatCommandSequence;
        private long threatCommandCount;
        private ulong threatCommandDigest;
        private int threatScheduleCursor;
        private int pendingThreatScheduleIndex = -1;
        private int pendingThreatIndex = -1;
        private RuntimeId pendingThreatScheduleRuntimeId = RuntimeId.Invalid;
        private long threatScheduleDecisionCount;
        private ulong threatScheduleDecisionDigest;
        private BattleCompletionReason completionReason;
        private RejectReason failureReason;

        internal BattleSession(
            ScenarioDefinition definition,
            GameplayClock clock,
            SessionIdAllocator idAllocator,
            CombatKernel combatKernel,
            PlayerRuntime player,
            EnemyRuntime enemy,
            IAttackResolutionPort attackResolutionPort,
            IAttackQueryPort attackQueryPort,
            IProjectileWorldPort projectileWorldPort,
            ISpatialDigestView spatialDecisionView,
            ICommittedPlayerShotPresentationSink committedPlayerShotPresentationSink)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.idAllocator = idAllocator ?? throw new ArgumentNullException(nameof(idAllocator));
            this.combatKernel = combatKernel ?? throw new ArgumentNullException(nameof(combatKernel));
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            enemyRuntimes = new EnemyRuntime[1 + definition.EnemySpawnCount];
            enemyRuntimes[0] = Enemy;
            enemyRuntimeCount = 1;
            this.attackResolutionPort = attackResolutionPort ?? throw new ArgumentNullException(nameof(attackResolutionPort));
            this.attackQueryPort = attackQueryPort ?? throw new ArgumentNullException(nameof(attackQueryPort));
            this.projectileWorldPort = projectileWorldPort ?? throw new ArgumentNullException(nameof(projectileWorldPort));
            this.spatialDecisionView = spatialDecisionView;
            this.committedPlayerShotPresentationSink = committedPlayerShotPresentationSink
                ?? NullCommittedPlayerShotPresentationSink.Instance;
            uncommittedPlayerShotPresentationSink = committedPlayerShotPresentationSink
                as IUncommittedPlayerShotPresentationSink;

            int maxHits = Math.Max(WeaponDefinition.PrimaryPelletCount, definition.PlayerWeapon.SecondaryMaxImpactCount);
            weaponReleaseBuffer = new WeaponReleaseBuffer();
            inputEdgeBuffer = new InputEdgeCommand[BattleTickInput.MaxEdgeCommandCount];
            // The query adapter writes raw candidates into this fixed buffer. A
            // deliberately bounded buffer keeps overflow visible as BufferCapacity
            // instead of allowing a hidden allocation on the gameplay hot path.
            queryCandidateBuffer = new QueryCandidate[TargetSelector.DefaultCandidateCapacity];
            selectedCandidateBuffer = new QueryCandidate[maxHits];
            selectedHitWriteBuffer = new SelectedAttackHit[maxHits];
            selectedAttackHits = new SelectedAttackHitStream(definition.ImpactHistoryCapacity);
            resolvedHitBuffer = new ResolvedAttackHit[maxHits];
            dueImpactBuffer = new QueuedImpact[combatKernel.ImpactQueue.Capacity];
            projectileSlots = new ProjectileRuntime[definition.ProjectileCapacity];
            projectileAdvanceBuffer = new ProjectileRuntime[definition.ProjectileCapacity];
            projectileRegistrationBuffer = new ProjectileRuntime[definition.ProjectileCapacity];
            projectileRegistrationPathBuffer = new ProjectilePathSnapshot[definition.ProjectileCapacity];
            failedRegistrationProjectileBuffer = new ProjectileRuntime[definition.ProjectileCapacity];
            failedRegistrationBudgetReleased = new bool[definition.ProjectileCapacity];
            failedRegistrationWorldReleased = new bool[definition.ProjectileCapacity];
            projectilePathSlots = new ProjectilePathSnapshot[definition.ProjectileCapacity];
            projectileTargetSlots = new RuntimeId[definition.ProjectileCapacity];
            threatAdvanceBuffer = new ThreatRuntime[definition.ThreatCapacity];
            threatStateBuffer = new ThreatState[definition.ThreatCapacity];
            projectileBudgetReleased = new bool[definition.ProjectileCapacity];
            projectileWorldRegistered = new bool[definition.ProjectileCapacity];
            projectileWorldReleased = new bool[definition.ProjectileCapacity];
            useSpatialAttackQuery = !(this.attackQueryPort is NullAttackQueryPort);
            State = BattleSessionState.NotStarted;
            failureReason = RejectReason.None;
            controlCommandDigest = StableHash.Mix(0x4650475F4354524CUL);
            threatCommandDigest = StableHash.Mix(0x4650475F54485243UL);
            threatScheduleDecisionDigest = StableHash.Mix(0x4650475F54534348UL);
        }

        public ScenarioDefinition Definition { get; }
        public BattleSessionState State { get; private set; }
        public BattleCompletionReason CompletionReason => completionReason;
        public RejectReason FailureReason => failureReason;
        internal PlayerRuntime Player { get; }
        internal EnemyRuntime Enemy { get; private set; }
        internal CombatKernel CombatKernel => combatKernel;
        internal GameplayClock Clock => clock;
        internal IAttackQueryPort AttackQueryPort => attackQueryPort;
        internal IProjectileWorldPort ProjectileWorldPort => projectileWorldPort;
        internal ISpatialDigestView SpatialDecisionView => spatialDecisionView;
        internal bool UsesSpatialAttackQuery => useSpatialAttackQuery;
        public ICombatTraceView Trace => combatKernel.Trace;
        public ISelectedAttackHitView SelectedAttackHits => selectedAttackHits;
        public TickIndex CurrentTick => clock.CurrentTick;
        public long ExecutedTickCount => clock.ExecutedTickCount;
        public ClockDiagnostics ClockDiagnostics => clock.Diagnostics;
        public bool IsClockPaused => clock.IsPaused;
        public int PendingImpactCount => combatKernel.ImpactQueue.Count;
        public int ConsumedImpactCount => combatKernel.ImpactLedger.Count;
        public bool IsCombatKernelDisposed => combatKernel.IsDisposed;
        public bool RestartRequested { get; private set; }
        public int ProjectileSlotCount => projectileSlotCount;
        public int EnemyRuntimeCount => enemyRuntimeCount;
        public int ActiveEnemyDefinitionId => activeEnemyDefinitionId;
        public int ThreatCount => Enemy.ThreatCount;
        public RuntimeId PlayerRuntimeId => Player.RuntimeId;
        public RuntimeId EnemyRuntimeId => Enemy.RuntimeId;
        public PlayerExposureState PlayerExposureState => Player.Exposure.State;
        public WeaponState PlayerWeaponState => Player.Weapon.State;
        public long ControlCommandCount => controlCommandCount;
        public ulong ControlCommandDigest => controlCommandDigest;
        public long ThreatCommandCount => threatCommandCount;
        public ulong ThreatCommandDigest => threatCommandDigest;
        public int ThreatScheduleCursor => threatScheduleCursor;
        public long PendingThreatScheduleSequence => pendingThreatScheduleIndex >= 0
            && pendingThreatScheduleIndex < Definition.ThreatScheduleCount
            ? Definition.GetThreatScheduleEntry(pendingThreatScheduleIndex).ScheduleSequence
            : 0L;
        public long ThreatScheduleDecisionCount => threatScheduleDecisionCount;
        public ulong ThreatScheduleDecisionDigest => threatScheduleDecisionDigest;

        public event Action<EnemyLifecycleChange> EnemyRuntimeChanged;

        public int ActiveProjectileCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < projectileSlotCount; index++)
                {
                    if (projectileSlots[index] != null && !projectileSlots[index].IsTerminal)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public DomainResult ApplyControl(SessionControlCommand command)
        {
            if (State == BattleSessionState.Disposed)
            {
                return DomainResult.Rejected(RejectReason.Disposed);
            }

            if (!command.Sequence.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (command.Sequence.Value <= lastControlSequence)
            {
                return DomainResult.Rejected(RejectReason.DuplicateSequence);
            }

            lastControlSequence = command.Sequence.Value;
            controlCommandCount++;
            BattleSessionState stateBefore = State;
            DomainResult result;
            if (State == BattleSessionState.Faulted
                && command.Type != SessionControlCommandType.Restart
                && command.Type != SessionControlCommandType.Dispose)
            {
                result = DomainResult.Rejected(failureReason == RejectReason.None
                    ? RejectReason.InvariantFault
                    : failureReason);
            }
            else
            {
                switch (command.Type)
                {
                    case SessionControlCommandType.Start:
                        if (State != BattleSessionState.NotStarted)
                        {
                            result = DomainResult.Rejected(RejectReason.InvalidState);
                            break;
                        }

                        State = BattleSessionState.Running;
                        clock.SetPaused(false);
                        RecordSessionState(stateBefore);
                        result = DomainResult.Success;
                        break;

                    case SessionControlCommandType.Pause:
                        if (State != BattleSessionState.Running)
                        {
                            result = DomainResult.Rejected(RejectReason.InvalidState);
                            break;
                        }

                        State = BattleSessionState.Paused;
                        clock.SetPaused(true);
                        RecordSessionState(stateBefore);
                        result = DomainResult.Success;
                        break;

                    case SessionControlCommandType.Resume:
                        if (State != BattleSessionState.Paused)
                        {
                            result = DomainResult.Rejected(RejectReason.InvalidState);
                            break;
                        }

                        State = BattleSessionState.Running;
                        clock.SetPaused(false);
                        RecordSessionState(stateBefore);
                        result = DomainResult.Success;
                        break;

                    case SessionControlCommandType.Complete:
                        if (State != BattleSessionState.Running)
                        {
                            result = DomainResult.Rejected(RejectReason.InvalidState);
                            break;
                        }

                        Complete(BattleCompletionReason.External);
                        result = State == BattleSessionState.Faulted
                            ? DomainResult.Rejected(failureReason)
                            : DomainResult.Success;
                        break;

                    case SessionControlCommandType.Restart:
                        RestartRequested = true;
                        DisposeInternal(BattleCompletionReason.Restarted);
                        result = DomainResult.Success;
                        break;

                    case SessionControlCommandType.Dispose:
                        Dispose();
                        result = DomainResult.Success;
                        break;

                    default:
                        result = DomainResult.Rejected(RejectReason.InvalidState);
                        break;
                }
            }

            AppendControlCommandDigest(command, result, stateBefore, State);
            return result;
        }

        public DomainResult Pump(
            long elapsedTimeSpanTicks,
            IPlayerInputSource inputSource,
            out int executedSteps)
        {
            return PumpCore(
                elapsedTimeSpanTicks,
                inputSource,
                useSpatialAttackQuery ? inputSource as IBattleTickInputSource : null,
                null,
                out executedSteps);
        }

        public DomainResult PumpWithBattleInput(
            long elapsedTimeSpanTicks,
            IBattleTickInputSource inputSource,
            out int executedSteps)
        {
            return PumpCore(
                elapsedTimeSpanTicks,
                null,
                inputSource,
                null,
                out executedSteps);
        }

        /// <summary>
        /// Pumps battle ticks while allowing a presentation-only observer to
        /// synchronize scene spatial representations immediately before each
        /// tick's queries run. The observer is deliberately outside the domain
        /// state and cannot change the deterministic simulation itself.
        /// </summary>
        public DomainResult PumpWithBattleInput(
            long elapsedTimeSpanTicks,
            IBattleTickInputSource inputSource,
            IBattleTickObserver tickObserver,
            out int executedSteps)
        {
            return PumpCore(
                elapsedTimeSpanTicks,
                null,
                inputSource,
                tickObserver,
                out executedSteps);
        }

        public DomainResult PumpBattleTicks(
            long elapsedTimeSpanTicks,
            IBattleTickInputSource inputSource,
            out int executedSteps)
        {
            return PumpWithBattleInput(
                elapsedTimeSpanTicks,
                inputSource,
                out executedSteps);
        }

        private DomainResult PumpCore(
            long elapsedTimeSpanTicks,
            IPlayerInputSource legacyInputSource,
            IBattleTickInputSource battleTickInputSource,
            IBattleTickObserver tickObserver,
            out int executedSteps)
        {
            executedSteps = 0;
            if (State == BattleSessionState.Disposed)
            {
                return DomainResult.Rejected(RejectReason.Disposed);
            }

            if (State == BattleSessionState.Faulted)
            {
                return DomainResult.Rejected(failureReason == RejectReason.None
                    ? RejectReason.InvariantFault
                    : failureReason);
            }

            if (State != BattleSessionState.Running)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (legacyInputSource == null && battleTickInputSource == null)
            {
                throw new ArgumentNullException("inputSource");
            }

            if (useSpatialAttackQuery && battleTickInputSource == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            DomainResult pump = clock.BeginPump(elapsedTimeSpanTicks, out ClockPumpResult ignored);
            if (!pump.IsSuccess)
            {
                return pump;
            }

            while (State == BattleSessionState.Running && clock.TryPeekStep(out TickIndex tick))
            {
                BattleTickInput tickInput;
                PlayerInputFrame frame;
                try
                {
                    if (battleTickInputSource != null)
                    {
                        tickInput = battleTickInputSource.GetTickInput(tick);
                        if (!tickInput.Tick.IsValid || tickInput.Tick != tick)
                        {
                            RecordInputReject(tick, RejectReason.WrongTick);
                            clock.AbortPump();
                            return DomainResult.Rejected(RejectReason.WrongTick);
                        }

                        if (!tickInput.IsValid)
                        {
                            RecordInputReject(tick, RejectReason.InvalidState);
                            clock.AbortPump();
                            return DomainResult.Rejected(RejectReason.InvalidState);
                        }

                        frame = tickInput.CopyToPlayerInputFrame(inputEdgeBuffer);
                    }
                    else
                    {
                        frame = legacyInputSource.GetFrame(tick);
                        if (frame.Tick != tick)
                        {
                            RecordInputReject(tick, RejectReason.WrongTick);
                            clock.AbortPump();
                            return DomainResult.Rejected(RejectReason.WrongTick);
                        }

                        tickInput = default(BattleTickInput);
                    }
                }
                catch
                {
                    clock.AbortPump();
                    FaultSession(RejectReason.InvariantFault);
                    return DomainResult.Rejected(RejectReason.InvariantFault);
                }

                DomainResult committed = clock.CommitStep(tick);
                if (!committed.IsSuccess)
                {
                    clock.AbortPump();
                    return committed;
                }

                executedSteps++;

                DomainResult step;
                try
                {
                    DomainResult lifecycle = AdvanceEnemyLifecycle(tick);
                    if (!lifecycle.IsSuccess)
                    {
                        step = lifecycle;
                    }
                    else
                    {
                        tickObserver?.BeforeBattleTick(this, tick);
                        step = ExecuteTick(tick, frame, tickInput);
                    }
                }
                catch
                {
                    clock.AbortPump();
                    FaultSession(RejectReason.InvariantFault);
                    return DomainResult.Rejected(RejectReason.InvariantFault);
                }

                if (!step.IsSuccess)
                {
                    clock.AbortPump();
                    FaultSession(step.RejectReason);
                    return step;
                }
            }

            return DomainResult.Success;
        }

        public DomainResult TryAddThreat(ThreatDefinition definition, out int threatIndex)
        {
            ThreatCommand command = new ThreatCommand(
                new ControlSequence(lastThreatCommandSequence + 1L),
                new TickIndex(clock.ExecutedTickCount),
                ThreatCommandType.Add,
                definition);
            return ApplyThreatCommand(command, out threatIndex);
        }

        public DomainResult TryStartThreat(int threatIndex)
        {
            RuntimeId expectedThreatRuntimeId = RuntimeId.Invalid;
            if (threatIndex >= 0 && threatIndex < Enemy.ThreatCount)
            {
                ThreatRuntime threat = Enemy.GetThreat(threatIndex);
                if (threat != null)
                {
                    expectedThreatRuntimeId = threat.RuntimeId;
                }
            }

            ThreatCommand command = new ThreatCommand(
                new ControlSequence(lastThreatCommandSequence + 1L),
                new TickIndex(clock.ExecutedTickCount),
                ThreatCommandType.Start,
                default(ThreatDefinition),
                threatIndex,
                expectedThreatRuntimeId);
            return ApplyThreatCommand(command, out int ignoredThreatIndex);
        }

        public DomainResult ApplyThreatCommand(ThreatCommand command, out int threatIndex)
        {
            return ApplyThreatCommandAtTick(
                command,
                new TickIndex(clock.ExecutedTickCount),
                out threatIndex);
        }

        private DomainResult ApplyThreatCommandAtTick(
            ThreatCommand command,
            TickIndex expectedTick,
            out int threatIndex)
        {
            threatIndex = -1;
            if (State == BattleSessionState.Disposed)
            {
                return DomainResult.Rejected(RejectReason.Disposed);
            }

            if (State == BattleSessionState.Completed || State == BattleSessionState.Faulted)
            {
                return DomainResult.Rejected(RejectReason.AlreadyTerminal);
            }

            if (!command.Sequence.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (command.Sequence.Value <= lastThreatCommandSequence)
            {
                return DomainResult.Rejected(RejectReason.DuplicateSequence);
            }

            lastThreatCommandSequence = command.Sequence.Value;
            threatCommandCount++;
            int reservedBefore = combatKernel.ProjectileBudget.ReservedUnits;
            int activeBefore = combatKernel.ProjectileBudget.ActiveUnits;
            DomainResult result;
            if (command.Tick != expectedTick)
            {
                result = DomainResult.Rejected(RejectReason.WrongTick);
            }
            else
            {
                switch (command.Type)
                {
                    case ThreatCommandType.Add:
                        result = TryAddThreatCore(command.Definition, out threatIndex);
                        break;
                    case ThreatCommandType.Start:
                        threatIndex = command.ThreatIndex;
                        result = TryStartThreatCore(
                            command.ThreatIndex,
                            command.ExpectedThreatRuntimeId,
                            command.Tick);
                        break;
                    default:
                        result = DomainResult.Rejected(RejectReason.InvalidState);
                        break;
                }
            }

            RuntimeId threatRuntimeId = RuntimeId.Invalid;
            AttackId threatAttackId = AttackId.Invalid;
            if (threatIndex >= 0 && threatIndex < Enemy.ThreatCount)
            {
                ThreatRuntime threat = Enemy.GetThreat(threatIndex);
                if (threat != null)
                {
                    threatRuntimeId = threat.RuntimeId;
                    threatAttackId = threat.AttackId;
                }
            }

            AppendThreatCommandDigest(command, result, threatIndex, threatRuntimeId);
            combatKernel.Trace.Record(
                command.Tick.IsValid ? command.Tick : expectedTick,
                result.IsSuccess ? CombatEventType.InputAccepted : CombatEventType.InputRejected,
                Enemy.RuntimeId,
                threatRuntimeId,
                threatAttackId,
                ImpactId.Invalid,
                (int)command.Type,
                threatIndex,
                result.RejectReason,
                threatCommandDigest);
            RecordBudgetChangeIfNeeded(
                command.Tick.IsValid ? command.Tick : expectedTick,
                threatRuntimeId,
                threatAttackId,
                reservedBefore,
                activeBefore);
            return result;
        }

        private DomainResult TryAddThreatCore(ThreatDefinition definition, out int threatIndex)
        {
            threatIndex = -1;
            if (State == BattleSessionState.Paused)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (definition.DefinitionId <= 0 || !definition.Payload.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (definition.Payload.IsSweptProjectile
                && (definition.PayloadCount > projectileSlots.Length
                    || definition.TotalBudgetUnits > combatKernel.ProjectileBudget.Capacity))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            DomainResult capacity = Enemy.ValidateCanAddThreat();
            if (!capacity.IsSuccess)
            {
                return capacity;
            }

            ThreatRuntime threat = new ThreatRuntime(definition, idAllocator.NextRuntimeId());
            DomainResult added = Enemy.TryAddThreat(threat, out threatIndex);
            if (!added.IsSuccess)
            {
                throw new InvalidOperationException(
                    "Threat capacity changed between preflight and commit.");
            }

            return DomainResult.Success;
        }

        private DomainResult TryStartThreatCore(
            int threatIndex,
            RuntimeId expectedThreatRuntimeId,
            TickIndex commandTick)
        {
            if (State != BattleSessionState.Running)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (threatIndex < 0 || threatIndex >= Enemy.ThreatCount)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            ThreatRuntime threat = Enemy.GetThreat(threatIndex);
            if (!expectedThreatRuntimeId.IsValid || threat.RuntimeId != expectedThreatRuntimeId)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            ThreatState previousState = threat.State;
            DomainResult result = threat.TryStart(
                commandTick,
                Enemy.ControlState,
                combatKernel.ProjectileBudget,
                idAllocator);
            if (result.IsSuccess)
            {
                RecordThreatStateChange(commandTick, threat, previousState);
            }

            return result;
        }

        private void AppendThreatCommandDigest(
            ThreatCommand command,
            DomainResult result,
            int threatIndex,
            RuntimeId threatRuntimeId)
        {
            threatCommandDigest = StableHash.Append(
                threatCommandDigest,
                unchecked((ulong)command.Sequence.Value));
            threatCommandDigest = StableHash.Append(
                threatCommandDigest,
                unchecked((ulong)command.Tick.Value));
            threatCommandDigest = StableHash.Append(threatCommandDigest, (ulong)command.Type);
            if (command.Type == ThreatCommandType.Add)
            {
                threatCommandDigest = ScenarioDefinition.AppendThreatDefinition(
                    threatCommandDigest,
                    command.Definition);
            }
            else
            {
                threatCommandDigest = StableHash.Append(
                    threatCommandDigest,
                    unchecked((ulong)command.ThreatIndex));
                threatCommandDigest = StableHash.Append(
                    threatCommandDigest,
                    unchecked((ulong)command.ExpectedThreatRuntimeId.Value));
            }

            threatCommandDigest = StableHash.Append(
                threatCommandDigest,
                unchecked((ulong)threatIndex));
            threatCommandDigest = StableHash.Append(
                threatCommandDigest,
                unchecked((ulong)threatRuntimeId.Value));
            threatCommandDigest = StableHash.Append(
                threatCommandDigest,
                result.IsSuccess ? 1UL : 0UL);
            threatCommandDigest = StableHash.Append(
                threatCommandDigest,
                (ulong)result.RejectReason);
        }

        public ProjectileSnapshot GetProjectileSnapshot(int index)
        {
            if (index < 0 || index >= projectileSlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return projectileSlots[index].GetSnapshot();
        }

        public DomainResult CopyActiveProjectileSnapshots(ProjectileSnapshot[] output, out int count)
        {
            count = 0;
            if (output == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            int required = ActiveProjectileCount;
            if (required > output.Length)
            {
                count = required;
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            for (int index = 0; index < projectileSlotCount; index++)
            {
                ProjectileRuntime projectile = projectileSlots[index];
                if (projectile != null && !projectile.IsTerminal)
                {
                    output[count++] = projectile.GetSnapshot();
                }
            }

            return DomainResult.Success;
        }

        public DomainResult CopySelectedAttackHits(SelectedAttackHit[] output, out int count)
        {
            return selectedAttackHits.CopyTo(output, out count);
        }

        public ThreatSnapshot GetThreatSnapshot(int index)
        {
            return Enemy.GetThreat(index).GetSnapshot();
        }

        public DomainResult CopyThreatSnapshots(ThreatSnapshot[] output, out int count)
        {
            count = 0;
            if (output == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            int required = Enemy.ThreatCount;
            if (output.Length < required)
            {
                count = required;
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            for (int index = 0; index < required; index++)
            {
                output[index] = Enemy.GetThreat(index).GetSnapshot();
            }

            count = required;
            return DomainResult.Success;
        }

        public FinalSnapshot GetFinalSnapshot()
        {
            return new FinalSnapshot(
                State,
                completionReason,
                clock.ExecutedTickCount,
                Player.Combatant.Life,
                Player.Combatant.Barrier,
                Player.Weapon.Magazine.Ammo,
                Enemy.Combatant.Life,
                Enemy.Combatant.Break,
                Enemy.ControlState,
                combatKernel.ProjectileBudget.ReservedUnits,
                combatKernel.ProjectileBudget.ActiveUnits,
                Enemy.Combatant.MaxLife,
                Enemy.Combatant.MaxBreak,
                activeEnemyDefinitionId);
        }

        public ReplaySummary GetReplaySummary()
        {
            int spatialContractVersion = spatialDecisionView == null
                ? SpatialContract.Version
                : spatialDecisionView.ContractVersion;
            long spatialDecisionCount = spatialDecisionView == null
                ? 0L
                : spatialDecisionView.Count;
            ulong spatialDecisionDigest = spatialDecisionView == null
                ? 0UL
                : spatialDecisionView.CanonicalDigest;
            ulong canonicalDigest = StableHash.Append(
                combatKernel.Trace.CanonicalDigest,
                unchecked((ulong)spatialContractVersion));
            canonicalDigest = StableHash.Append(
                canonicalDigest,
                unchecked((ulong)spatialDecisionCount));
            canonicalDigest = StableHash.Append(canonicalDigest, spatialDecisionDigest);
            canonicalDigest = StableHash.Append(
                canonicalDigest,
                unchecked((ulong)threatScheduleDecisionCount));
            canonicalDigest = StableHash.Append(canonicalDigest, threatScheduleDecisionDigest);

            return new ReplaySummary(
                Definition.DefinitionHash,
                Definition.ScenarioSeed,
                controlCommandCount,
                controlCommandDigest,
                threatCommandCount,
                threatCommandDigest,
                threatScheduleDecisionCount,
                threatScheduleDecisionDigest,
                clock.ExecutedTickCount,
                GetFinalSnapshot(),
                combatKernel.Trace.TotalEventCount,
                combatKernel.Trace.DroppedEventCount,
                spatialContractVersion,
                spatialDecisionCount,
                spatialDecisionDigest,
                canonicalDigest);
        }

        public void Dispose()
        {
            DisposeInternal(BattleCompletionReason.Disposed);
        }

        private DomainResult AdvanceEnemyLifecycle(TickIndex tick)
        {
            while (enemySpawnCursor < Definition.EnemySpawnCount)
            {
                EnemySpawnDefinition spawn = Definition.GetEnemySpawnDefinition(enemySpawnCursor);
                if (spawn.SpawnTick > tick)
                {
                    break;
                }

                EnemyRuntime previous = Enemy;
                if (previous == null || previous.Combatant.IsDead)
                {
                    // A player-killed egg is a real terminal encounter. It
                    // must not resurrect into a butterfly after the victory
                    // path has been selected.
                    break;
                }

                RuntimeId previousRuntimeId = previous.RuntimeId;
                int previousLife = previous.Combatant.ForceDeath();
                CaptureThreatStates();
                int reservedBefore = combatKernel.ProjectileBudget.ReservedUnits;
                int activeBefore = combatKernel.ProjectileBudget.ActiveUnits;
                DomainResult markedDead = previous.MarkDead(combatKernel.ProjectileBudget);
                if (!markedDead.IsSuccess)
                {
                    return markedDead;
                }

                RecordCapturedThreatChanges(tick);
                RecordBudgetChangeIfNeeded(
                    tick,
                    previousRuntimeId,
                    AttackId.Invalid,
                    reservedBefore,
                    activeBefore);
                combatKernel.Trace.Record(
                    tick,
                    CombatEventType.Death,
                    previousRuntimeId,
                    previousRuntimeId,
                    AttackId.Invalid,
                    ImpactId.Invalid,
                    previousLife,
                    0);
                combatKernel.Trace.Record(
                    tick,
                    CombatEventType.EnemyDespawned,
                    previousRuntimeId,
                    previousRuntimeId,
                    AttackId.Invalid,
                    ImpactId.Invalid,
                    previousLife,
                    0,
                    RejectReason.None,
                    unchecked((ulong)spawn.DefinitionId));

                EnemyRuntime next = new EnemyRuntime(
                    new CombatantState(
                        idAllocator.NextRuntimeId(),
                        CombatantKind.Enemy,
                        spawn.Life,
                        0,
                        spawn.Break),
                    spawn.GroggyDuration,
                    spawn.ThreatCapacity);
                Enemy = next;
                enemyRuntimes[enemyRuntimeCount++] = next;
                activeEnemyDefinitionId = spawn.DefinitionId;
                enemySpawnCursor++;
                pendingThreatScheduleIndex = -1;
                pendingThreatIndex = -1;
                pendingThreatScheduleRuntimeId = RuntimeId.Invalid;

                combatKernel.Trace.Record(
                    tick,
                    CombatEventType.EnemySpawned,
                    previousRuntimeId,
                    next.RuntimeId,
                    AttackId.Invalid,
                    ImpactId.Invalid,
                    spawn.DefinitionId,
                    spawn.Life,
                    RejectReason.None,
                    unchecked((ulong)spawn.Break));

                EnemyRuntimeChanged?.Invoke(new EnemyLifecycleChange(
                    tick,
                    previousRuntimeId,
                    next.RuntimeId,
                    spawn.DefinitionId));
            }

            return DomainResult.Success;
        }

        private DomainResult ExecuteTick(
            TickIndex tick,
            PlayerInputFrame frame,
            in BattleTickInput tickInput)
        {
            if (frame.Tick != tick)
            {
                RecordInputReject(tick, RejectReason.WrongTick);
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            if (useSpatialAttackQuery && (!tickInput.IsValid || tickInput.Tick != tick))
            {
                RejectReason reason = tickInput.Tick == tick
                    ? RejectReason.InvalidState
                    : RejectReason.WrongTick;
                RecordInputReject(tick, reason);
                return DomainResult.Rejected(reason);
            }

            bool barrierRestored = Player.Combatant.TryRestoreBarrier(tick);
            if (barrierRestored)
            {
                combatKernel.Trace.Record(
                    tick,
                    CombatEventType.ExposureChanged,
                    Player.RuntimeId,
                    Player.RuntimeId,
                    AttackId.Invalid,
                    ImpactId.Invalid,
                    0,
                    Player.Combatant.Barrier);
            }

            if (Enemy.AdvanceStartOfTick(tick))
            {
                combatKernel.Trace.Record(
                    tick,
                    CombatEventType.GroggyEnded,
                    Enemy.RuntimeId,
                    Enemy.RuntimeId,
                    AttackId.Invalid,
                    ImpactId.Invalid,
                    0,
                    Enemy.Combatant.Break);
            }

            PlayerExposureState previousExposure = Player.Exposure.State;
            bool reloadKeepsPlayerWithdrawn = Player.Weapon.State == WeaponState.Reloading
                && (!Player.Weapon.StateUntilTick.IsValid
                    || tick < Player.Weapon.StateUntilTick);
            // Reload is allowed from either posture, but its intent wins over
            // held aim/fire so the same tick first returns behind the barrier.
            bool reloadRequestsWithdrawn =
                reloadKeepsPlayerWithdrawn || frame.HasReloadInput;
            bool shouldExpose = !reloadRequestsWithdrawn
                && !frame.CancelSecondary
                && (frame.AimHeld
                    || frame.PrimaryHeld
                    || frame.HasSecondaryInput
                    || Player.Weapon.State == WeaponState.AltCharging);
            bool exposureChanged;
            DomainResult exposureResult = reloadRequestsWithdrawn
                ? Player.Exposure.ApplyReloadPosture(tick, out exposureChanged)
                : Player.Exposure.ApplyCombatPosture(
                    shouldExpose,
                    tick,
                    Player.Combatant.IsBarrierLocked(tick) || Player.Combatant.Barrier <= 0,
                    out exposureChanged);
            WeaponState weaponBeforeExposureCancel = Player.Weapon.State;
            if (Player.Exposure.State == PlayerExposureState.Withdrawn
                && previousExposure != PlayerExposureState.Withdrawn)
            {
                Player.Weapon.CancelForWithdrawn();
            }

            if (weaponBeforeExposureCancel != Player.Weapon.State)
            {
                combatKernel.Trace.Record(
                    tick,
                    CombatEventType.AttackCanceled,
                    Player.RuntimeId,
                    RuntimeId.Invalid,
                    AttackId.Invalid,
                    ImpactId.Invalid,
                    (int)weaponBeforeExposureCancel,
                    (int)Player.Weapon.State);
            }

            if (exposureChanged)
            {
                combatKernel.Trace.Record(
                    tick,
                    CombatEventType.ExposureChanged,
                    Player.RuntimeId,
                    Player.RuntimeId,
                    AttackId.Invalid,
                    ImpactId.Invalid,
                    (int)previousExposure,
                    (int)Player.Exposure.State,
                    exposureResult.RejectReason);
            }
            else if (!exposureResult.IsSuccess)
            {
                combatKernel.Trace.Record(
                    tick,
                    CombatEventType.InputRejected,
                    Player.RuntimeId,
                    Player.RuntimeId,
                    AttackId.Invalid,
                    ImpactId.Invalid,
                    0,
                    0,
                    exposureResult.RejectReason);
            }

            int ammoBefore = Player.Weapon.Magazine.Ammo;
            WeaponState weaponStateBefore = Player.Weapon.State;
            DomainResult weaponResult = Player.Weapon.ProcessFrame(
                frame,
                Player.Exposure,
                Player.RuntimeId,
                idAllocator,
                Definition.ScenarioSeed,
                weaponReleaseBuffer);
            if (!weaponResult.IsSuccess)
            {
                return weaponResult;
            }

            if (weaponStateBefore != Player.Weapon.State)
            {
                CombatEventType eventType = Player.Weapon.State == WeaponState.Reloading
                    ? CombatEventType.ReloadStarted
                    : weaponStateBefore == WeaponState.Reloading
                        ? CombatEventType.ReloadCompleted
                        : CombatEventType.InputAccepted;
                combatKernel.Trace.Record(
                    tick,
                    eventType,
                    Player.RuntimeId,
                    RuntimeId.Invalid,
                    weaponReleaseBuffer.HasRelease
                        ? weaponReleaseBuffer.Attack.AttackId
                        : AttackId.Invalid,
                    ImpactId.Invalid,
                    (int)weaponStateBefore,
                    (int)Player.Weapon.State);
            }

            if (weaponReleaseBuffer.HasRelease)
            {
                combatKernel.Trace.Record(
                    tick,
                    CombatEventType.ReleaseCommitted,
                    Player.RuntimeId,
                    RuntimeId.Invalid,
                    weaponReleaseBuffer.Attack.AttackId,
                    ImpactId.Invalid,
                    ammoBefore,
                    Player.Weapon.Magazine.Ammo);
                DomainResult resolveResult = ResolvePlayerAttack(tick, tickInput);
                if (!resolveResult.IsSuccess)
                {
                    return resolveResult;
                }
            }

            ResolveDueImpacts(tick);
            DomainResult playerPhaseRelease = ReleaseTerminalProjectileResources();
            if (!playerPhaseRelease.IsSuccess)
            {
                return playerPhaseRelease;
            }
            if (TryCompleteFromCombatants())
            {
                return State == BattleSessionState.Faulted
                    ? DomainResult.Rejected(failureReason)
                    : DomainResult.Success;
            }

            if (State != BattleSessionState.Running)
            {
                return DomainResult.Success;
            }

            DomainResult scheduleAdvance = AdvanceThreatSchedule(tick);
            if (!scheduleAdvance.IsSuccess)
            {
                return scheduleAdvance;
            }
            DomainResult threatAdvance = AdvanceThreats(tick);
            if (!threatAdvance.IsSuccess)
            {
                return threatAdvance;
            }
            DomainResult projectileAdvance = AdvanceProjectiles(tick);
            if (!projectileAdvance.IsSuccess)
            {
                return projectileAdvance;
            }
            ResolveDueImpacts(tick);
            DomainResult enemyPhaseRelease = ReleaseTerminalProjectileResources();
            if (!enemyPhaseRelease.IsSuccess)
            {
                return enemyPhaseRelease;
            }
            if (TryCompleteFromCombatants() && State == BattleSessionState.Faulted)
            {
                return DomainResult.Rejected(failureReason);
            }

            return DomainResult.Success;
        }

        private DomainResult ResolvePlayerAttack(TickIndex tick, in BattleTickInput tickInput)
        {
            return useSpatialAttackQuery
                ? ResolvePlayerAttackQuery(tick, tickInput)
                : ResolvePlayerAttackLegacy(tick);
        }

        private DomainResult ResolvePlayerAttackLegacy(TickIndex tick)
        {
            Array.Clear(resolvedHitBuffer, 0, resolvedHitBuffer.Length);
            int hitCount;
            try
            {
                hitCount = attackResolutionPort.Resolve(
                    weaponReleaseBuffer.Attack,
                    weaponReleaseBuffer.Pellets,
                    weaponReleaseBuffer.PelletCount,
                    resolvedHitBuffer);
            }
            catch
            {
                RecordAttackResolutionReject(tick, RejectReason.InvariantFault);
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            if (hitCount < 0 || hitCount > resolvedHitBuffer.Length
                || hitCount > weaponReleaseBuffer.Attack.MaxImpactCount)
            {
                RecordAttackResolutionReject(tick, RejectReason.BufferCapacity);
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            return CommitPlayerAttackHits(tick, hitCount);
        }

        private DomainResult ResolvePlayerAttackQuery(
            TickIndex tick,
            in BattleTickInput tickInput)
        {
            Array.Clear(queryCandidateBuffer, 0, queryCandidateBuffer.Length);
            Array.Clear(selectedCandidateBuffer, 0, selectedCandidateBuffer.Length);
            Array.Clear(resolvedHitBuffer, 0, resolvedHitBuffer.Length);

            AttackQueryRequest request;
            try
            {
                request = new AttackQueryRequest(
                    tickInput,
                    weaponReleaseBuffer.Attack,
                    weaponReleaseBuffer.Pellets,
                    weaponReleaseBuffer.PelletCount);
            }
            catch
            {
                RecordAttackResolutionReject(tick, RejectReason.InvalidState);
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            AttackQueryResult queryResult;
            DomainResult query;
            try
            {
                query = attackQueryPort.Query(
                    request,
                    queryCandidateBuffer,
                    out queryResult);
            }
            catch
            {
                DiscardUncommittedPlayerShotPresentation();
                RecordAttackResolutionReject(tick, RejectReason.InvariantFault);
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }
            if (!query.IsSuccess)
            {
                DiscardUncommittedPlayerShotPresentation();
                RecordAttackResolutionReject(tick, query.RejectReason);
                return query;
            }

            DomainResult selected = TargetSelector.Select(
                weaponReleaseBuffer.Attack,
                queryCandidateBuffer,
                queryResult,
                selectedCandidateBuffer,
                out int selectedCount);
            if (!selected.IsSuccess)
            {
                DiscardUncommittedPlayerShotPresentation();
                RecordAttackResolutionReject(tick, selected.RejectReason);
                return selected;
            }

            for (int index = 0; index < selectedCount; index++)
            {
                QueryCandidate candidate = selectedCandidateBuffer[index];
                resolvedHitBuffer[index] = new ResolvedAttackHit(
                    candidate.TargetId,
                    candidate.HitPart,
                    weaponReleaseBuffer.Kind == WeaponReleaseKind.Primary
                        ? candidate.SampleIndex
                        : -1,
                    index);
                selectedHitWriteBuffer[index] = new SelectedAttackHit(
                    weaponReleaseBuffer.Attack.AttackId,
                    weaponReleaseBuffer.Attack.ShotId,
                    tick,
                    index,
                    candidate.QueryStage,
                    candidate.SampleIndex,
                    candidate.TargetId,
                    candidate.TargetKind,
                    candidate.HitPart,
                    candidate.GeometryId,
                    candidate.ImpactPointKey);
            }

            if (!selectedAttackHits.CanAppend(selectedHitWriteBuffer, selectedCount))
            {
                DiscardUncommittedPlayerShotPresentation();
                RecordAttackResolutionReject(tick, RejectReason.BufferCapacity);
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            DomainResult committed = CommitPlayerAttackHits(tick, selectedCount);
            if (committed.IsSuccess)
            {
                selectedAttackHits.AppendValidated(selectedHitWriteBuffer, selectedCount);
                PublishCommittedPlayerShotPresentation();
            }
            else
            {
                DiscardUncommittedPlayerShotPresentation();
            }

            return committed;
        }

        /// <summary>
        /// Presentation publication is deliberately post-transaction and
        /// best-effort. It observes a successful spatial attack but must never
        /// turn a presentation failure into a combat failure or alter replay
        /// data.
        /// </summary>
        private void PublishCommittedPlayerShotPresentation()
        {
            try
            {
                committedPlayerShotPresentationSink.PublishCommittedShot(
                    weaponReleaseBuffer.Attack.AttackId,
                    weaponReleaseBuffer.Kind);
            }
            catch (Exception)
            {
                // Presentation is non-authoritative. Observers own their fault
                // counts and must not affect this BattleSession transaction.
            }
        }

        private void DiscardUncommittedPlayerShotPresentation()
        {
            if (uncommittedPlayerShotPresentationSink == null)
            {
                return;
            }

            try
            {
                uncommittedPlayerShotPresentationSink.DiscardUncommittedShot(
                    weaponReleaseBuffer.Attack.AttackId);
            }
            catch (Exception)
            {
                // Like publication, cleanup is strictly non-authoritative.
            }
        }

        private DomainResult CommitPlayerAttackHits(TickIndex tick, int hitCount)
        {
            DomainResult validation = ValidateResolvedHits(hitCount);
            if (!validation.IsSuccess)
            {
                RecordAttackResolutionReject(tick, validation.RejectReason);
                return validation;
            }

            if (combatKernel.ImpactQueue.Count + hitCount > combatKernel.ImpactQueue.Capacity)
            {
                RecordAttackResolutionReject(tick, RejectReason.BufferCapacity);
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            if (combatKernel.ImpactQueue.Count + hitCount
                    > combatKernel.ImpactLedger.RemainingCapacity
                || (weaponReleaseBuffer.Kind == WeaponReleaseKind.Secondary
                    && hitCount > combatKernel.ShotTargetLedger.RemainingCapacity))
            {
                RecordAttackResolutionReject(tick, RejectReason.BufferCapacity);
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            if (weaponReleaseBuffer.Kind == WeaponReleaseKind.Secondary)
            {
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    DomainResult marked = combatKernel.ShotTargetLedger.TryMark(
                        weaponReleaseBuffer.Attack.ShotId,
                        resolvedHitBuffer[hitIndex].TargetId);
                    if (!marked.IsSuccess)
                    {
                        throw new InvalidOperationException(
                            "Shot target ledger changed between preflight and commit.");
                    }
                }
            }

            SortResolvedHits(resolvedHitBuffer, hitCount);
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                ResolvedAttackHit hit = resolvedHitBuffer[hitIndex];
                bool projectileTarget = FindProjectileByRuntimeId(hit.TargetId) != null;
                ImpactIntent intent = new ImpactIntent(
                    idAllocator.NextImpactId(),
                    weaponReleaseBuffer.Attack.AttackId,
                    weaponReleaseBuffer.Attack.ShotId,
                    Player.RuntimeId,
                    hit.TargetId,
                    tick,
                    weaponReleaseBuffer.Attack.DamageSpec,
                    hit.HitPart,
                    projectileTarget
                        ? DamageType.ProjectileIntercept
                        : weaponReleaseBuffer.Kind == WeaponReleaseKind.Secondary
                            ? DamageType.Explosive
                            : DamageType.Normal,
                    weaponReleaseBuffer.Kind == WeaponReleaseKind.Secondary
                        ? CombatTags.Secondary
                        : CombatTags.Primary,
                    hit.PelletIndex,
                    hit.ImpactOrdinal);

                ImpactPhasePriority priority = FindProjectileByRuntimeId(hit.TargetId) != null
                    ? ImpactPhasePriority.PlayerProjectileIntercept
                    : ImpactPhasePriority.PlayerCombatantHit;
                DomainResult queued = combatKernel.ImpactQueue.TryEnqueue(
                    intent,
                    priority,
                    hit.TargetId);
                if (!queued.IsSuccess)
                {
                    return queued;
                }
            }

            return DomainResult.Success;
        }

        private DomainResult ValidateResolvedHits(int hitCount)
        {
            bool primary = weaponReleaseBuffer.Kind == WeaponReleaseKind.Primary;
            for (int index = 0; index < hitCount; index++)
            {
                ResolvedAttackHit hit = resolvedHitBuffer[index];
                if (!hit.TargetId.IsValid)
                {
                    return DomainResult.Rejected(RejectReason.InvalidTarget);
                }

                if (hit.HitPart != HitPart.Body
                    && hit.HitPart != HitPart.Weakpoint
                    && hit.HitPart != HitPart.Projectile)
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                ProjectileRuntime projectile = FindProjectileByRuntimeId(hit.TargetId);
                bool targetsEnemy = hit.TargetId == Enemy.RuntimeId;
                if (!targetsEnemy && projectile == null)
                {
                    return DomainResult.Rejected(RejectReason.InvalidTarget);
                }

                if (projectile != null)
                {
                    if (projectile.Team != Team.Enemy
                        || projectile.IsTerminal
                        || !projectile.Definition.Interceptable
                        || hit.HitPart != HitPart.Projectile)
                    {
                        return DomainResult.Rejected(RejectReason.InvalidTarget);
                    }
                }
                else if (hit.HitPart == HitPart.Projectile)
                {
                    return DomainResult.Rejected(RejectReason.InvalidTarget);
                }

                if (hit.ImpactOrdinal < 0
                    || hit.ImpactOrdinal >= weaponReleaseBuffer.Attack.MaxImpactCount)
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                for (int previous = 0; previous < index; previous++)
                {
                    ResolvedAttackHit prior = resolvedHitBuffer[previous];
                    if (primary && prior.PelletIndex == hit.PelletIndex)
                    {
                        return DomainResult.Rejected(RejectReason.DuplicateImpact);
                    }

                    if (!primary && prior.TargetId == hit.TargetId)
                    {
                        return DomainResult.Rejected(RejectReason.DuplicateImpact);
                    }

                    if (prior.ImpactOrdinal == hit.ImpactOrdinal)
                    {
                        return DomainResult.Rejected(RejectReason.DuplicateImpact);
                    }
                }

                if (primary && (hit.PelletIndex < 0
                    || hit.PelletIndex >= WeaponDefinition.PrimaryPelletCount))
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                if (!primary && hit.PelletIndex != -1)
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }
            }

            if (!primary)
            {
                for (int index = 0; index < hitCount; index++)
                {
                    DomainResult validation = combatKernel.ShotTargetLedger.ValidateCanMark(
                        weaponReleaseBuffer.Attack.ShotId,
                        resolvedHitBuffer[index].TargetId);
                    if (!validation.IsSuccess)
                    {
                        return validation;
                    }
                }
            }

            return DomainResult.Success;
        }

        private void RecordAttackResolutionReject(TickIndex tick, RejectReason reason)
        {
            combatKernel.Trace.Record(
                tick,
                CombatEventType.InputRejected,
                Player.RuntimeId,
                RuntimeId.Invalid,
                weaponReleaseBuffer.Attack.AttackId,
                ImpactId.Invalid,
                0,
                0,
                reason);
        }

        private void RecordInputReject(TickIndex tick, RejectReason reason)
        {
            combatKernel.Trace.Record(
                tick,
                CombatEventType.InputRejected,
                Player.RuntimeId,
                RuntimeId.Invalid,
                AttackId.Invalid,
                ImpactId.Invalid,
                0,
                0,
                reason);
        }

        private void ResolveDueImpacts(TickIndex tick)
        {
            int count = combatKernel.ImpactQueue.DrainDue(tick, dueImpactBuffer);
            for (int index = 0; index < count; index++)
            {
                ResolveImpact(dueImpactBuffer[index].Intent);
            }
        }

        private void ResolveImpact(ImpactIntent intent)
        {
            ImpactResolution resolution;
            ProjectileRuntime projectile = FindProjectileByRuntimeId(intent.TargetId);
            if (projectile != null)
            {
                ProjectileState previousProjectileState = projectile.State;
                resolution = combatKernel.DamageResolver.ResolveProjectile(intent, projectile);
                RecordProjectileStateChange(
                    intent.ImpactTick,
                    projectile,
                    previousProjectileState);
            }
            else if (intent.TargetId == Enemy.RuntimeId)
            {
                bool breakEnabled = Enemy.ControlState == EnemyControlState.Active;
                resolution = combatKernel.DamageResolver.ResolveCombatant(
                    intent,
                    Enemy.Combatant,
                    DefenseSnapshot.Exposed,
                    breakEnabled);
            }
            else if (intent.TargetId == Player.RuntimeId)
            {
                DefenseSnapshot defense = Player.Exposure.CreateDefenseSnapshot(
                    Definition.PerfectRetractWindow,
                    Definition.PerfectRetractMultiplierBasisPoints,
                    Definition.BarrierLockDuration,
                    Definition.BarrierRestoreBasisPoints);
                resolution = combatKernel.DamageResolver.ResolveCombatant(
                    intent,
                    Player.Combatant,
                    defense,
                    false);
            }
            else
            {
                DomainResult ledgerResult = combatKernel.ImpactLedger.TryConsume(intent.ImpactId);
                resolution = ledgerResult.IsSuccess
                    ? ImpactResolution.Rejected(RejectReason.InvalidTarget)
                    : ImpactResolution.Rejected(ledgerResult.RejectReason);
            }

            if (!resolution.Result.IsSuccess)
            {
                combatKernel.Trace.Record(
                    intent.ImpactTick,
                    CombatEventType.ImpactRejected,
                    intent.SourceId,
                    intent.TargetId,
                    intent.AttackId,
                    intent.ImpactId,
                    0,
                    0,
                    resolution.Result.RejectReason,
                    ComputeImpactPayloadHash(intent, resolution));
                return;
            }

            combatKernel.Trace.Record(
                intent.ImpactTick,
                CombatEventType.DamageApplied,
                intent.SourceId,
                intent.TargetId,
                intent.AttackId,
                intent.ImpactId,
                resolution.Packet.ValueBefore,
                resolution.Packet.ValueAfter,
                RejectReason.None,
                ComputeImpactPayloadHash(intent, resolution),
                resolution.Packet.Channel,
                resolution.Packet.AppliedBreakAmount,
                resolution.PerfectRetract);

            if (intent.TargetId == Player.RuntimeId
                && resolution.Packet.Channel == DamageChannel.Life
                && resolution.Packet.AppliedAmount > 0
                && Player.Weapon.CancelReload())
            {
                combatKernel.Trace.Record(
                    intent.ImpactTick,
                    CombatEventType.AttackCanceled,
                    Player.RuntimeId,
                    RuntimeId.Invalid,
                    intent.AttackId,
                    intent.ImpactId,
                    (int)WeaponState.Reloading,
                    (int)Player.Weapon.State);
            }

            if (resolution.PerfectRetract)
            {
                combatKernel.Trace.Record(
                    intent.ImpactTick,
                    CombatEventType.PerfectRetract,
                    intent.SourceId,
                    intent.TargetId,
                    intent.AttackId,
                    intent.ImpactId,
                    resolution.Packet.AppliedAmount,
                    resolution.Packet.ValueAfter);
            }

            if (resolution.BarrierBroken)
            {
                PlayerExposureState previousExposure = Player.Exposure.State;
                Player.Exposure.ForceExposed(intent.ImpactTick, out bool exposureChanged);
                Player.Weapon.CancelForWithdrawn();
                if (exposureChanged)
                {
                    combatKernel.Trace.Record(
                        intent.ImpactTick,
                        CombatEventType.ExposureChanged,
                        Player.RuntimeId,
                        Player.RuntimeId,
                        intent.AttackId,
                        intent.ImpactId,
                        (int)previousExposure,
                        (int)Player.Exposure.State);
                }

                combatKernel.Trace.Record(
                    intent.ImpactTick,
                    CombatEventType.BarrierBroken,
                    intent.SourceId,
                    intent.TargetId,
                    intent.AttackId,
                    intent.ImpactId,
                    resolution.Packet.ValueBefore,
                    resolution.Packet.ValueAfter);
            }

            if (resolution.BreakTriggered && intent.TargetId == Enemy.RuntimeId)
            {
                CaptureThreatStates();
                int reservedBefore = combatKernel.ProjectileBudget.ReservedUnits;
                int activeBefore = combatKernel.ProjectileBudget.ActiveUnits;
                if (Enemy.EnterGroggy(intent.ImpactTick, combatKernel.ProjectileBudget) < 0)
                {
                    throw new InvalidOperationException(
                        "Threat cancellation failed while entering Groggy.");
                }
                RecordCapturedThreatChanges(intent.ImpactTick);
                RecordBudgetChangeIfNeeded(
                    intent.ImpactTick,
                    Enemy.RuntimeId,
                    intent.AttackId,
                    reservedBefore,
                    activeBefore);
                combatKernel.Trace.Record(
                    intent.ImpactTick,
                    CombatEventType.BreakTriggered,
                    intent.SourceId,
                    intent.TargetId,
                    intent.AttackId,
                    intent.ImpactId,
                    resolution.Packet.AppliedBreakAmount,
                    Enemy.Combatant.Break);
                combatKernel.Trace.Record(
                    intent.ImpactTick,
                    CombatEventType.GroggyStarted,
                    Enemy.RuntimeId,
                    Enemy.RuntimeId,
                    intent.AttackId,
                    intent.ImpactId,
                    0,
                    1);
            }

            if (resolution.Death)
            {
                combatKernel.Trace.Record(
                    intent.ImpactTick,
                    CombatEventType.Death,
                    intent.SourceId,
                    intent.TargetId,
                    intent.AttackId,
                    intent.ImpactId,
                    resolution.Packet.ValueBefore,
                    resolution.Packet.ValueAfter);
            }
        }

        private DomainResult AdvanceThreatSchedule(TickIndex tick)
        {
            while (threatScheduleCursor < Definition.ThreatScheduleCount)
            {
                ThreatScheduleEntry entry = Definition.GetThreatScheduleEntry(threatScheduleCursor);
                if (entry.DueTick > tick)
                {
                    return DomainResult.Success;
                }

                if (pendingThreatScheduleIndex >= 0
                    && pendingThreatScheduleIndex != threatScheduleCursor)
                {
                    return DomainResult.Rejected(RejectReason.InvariantFault);
                }

                ThreatRuntime pendingThreat = GetPendingScheduledThreat();
                if (pendingThreat != null)
                {
                    DomainResult started = StartScheduledThreat(tick, entry, pendingThreat);
                    if (started.IsSuccess)
                    {
                        ConsumeThreatScheduleEntry();
                        continue;
                    }

                    if (ShouldRetryThreatScheduleEntry(entry, started))
                    {
                        return DomainResult.Success;
                    }

                    return started;
                }

                ThreatDefinition definition;
                try
                {
                    definition = entry.CreateThreatDefinition();
                }
                catch (Exception exception) when (exception is ArgumentException
                    || exception is InvalidOperationException
                    || exception is OverflowException)
                {
                    DomainResult invalidDefinition = DomainResult.Rejected(RejectReason.InvalidDefinition);
                    RecordThreatScheduleDecision(
                        tick,
                        entry,
                        ThreatScheduleDecisionStage.Preflight,
                        invalidDefinition,
                        -1,
                        null);
                    return invalidDefinition;
                }

                DomainResult preflight = ValidateScheduledThreatStart(definition);
                RecordThreatScheduleDecision(
                    tick,
                    entry,
                    ThreatScheduleDecisionStage.Preflight,
                    preflight,
                    -1,
                    null);
                if (!preflight.IsSuccess)
                {
                    MarkThreatSchedulePending();
                    if (ShouldRetryThreatScheduleEntry(entry, preflight))
                    {
                        return DomainResult.Success;
                    }

                    return preflight;
                }

                DomainResult added = TryAddThreatCore(definition, out int threatIndex);
                ThreatRuntime addedThreat = added.IsSuccess
                    ? Enemy.GetThreat(threatIndex)
                    : null;
                RecordThreatScheduleDecision(
                    tick,
                    entry,
                    ThreatScheduleDecisionStage.Add,
                    added,
                    threatIndex,
                    addedThreat);
                if (!added.IsSuccess)
                {
                    MarkThreatSchedulePending();
                    if (ShouldRetryThreatScheduleEntry(entry, added))
                    {
                        return DomainResult.Success;
                    }

                    return added;
                }

                if (addedThreat == null || !addedThreat.RuntimeId.IsValid)
                {
                    return DomainResult.Rejected(RejectReason.InvariantFault);
                }

                MarkThreatSchedulePending(threatIndex, addedThreat.RuntimeId);
            }

            return DomainResult.Success;
        }

        private DomainResult ValidateScheduledThreatStart(ThreatDefinition definition)
        {
            if (State != BattleSessionState.Running)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            DomainResult capacity = Enemy.ValidateCanAddThreat();
            if (!capacity.IsSuccess)
            {
                return capacity;
            }

            return definition.TotalBudgetUnits > 0
                ? combatKernel.ProjectileBudget.CanReserve(definition.TotalBudgetUnits)
                : DomainResult.Success;
        }

        private ThreatRuntime GetPendingScheduledThreat()
        {
            if (pendingThreatScheduleIndex != threatScheduleCursor
                || pendingThreatIndex < 0
                || !pendingThreatScheduleRuntimeId.IsValid
                || pendingThreatIndex >= Enemy.ThreatCount)
            {
                return null;
            }

            ThreatRuntime threat = Enemy.GetThreat(pendingThreatIndex);
            if (threat == null
                || threat.RuntimeId != pendingThreatScheduleRuntimeId
                || threat.IsTerminal)
            {
                pendingThreatIndex = -1;
                pendingThreatScheduleRuntimeId = RuntimeId.Invalid;
                return null;
            }

            return threat;
        }

        private DomainResult StartScheduledThreat(
            TickIndex tick,
            in ThreatScheduleEntry entry,
            ThreatRuntime threat)
        {
            int reservedBefore = combatKernel.ProjectileBudget.ReservedUnits;
            int activeBefore = combatKernel.ProjectileBudget.ActiveUnits;
            DomainResult started = TryStartThreatCore(
                pendingThreatIndex,
                pendingThreatScheduleRuntimeId,
                tick);
            RecordThreatScheduleDecision(
                tick,
                entry,
                ThreatScheduleDecisionStage.Start,
                started,
                pendingThreatIndex,
                threat);
            RecordBudgetChangeIfNeeded(
                tick,
                threat.RuntimeId,
                threat.AttackId,
                reservedBefore,
                activeBefore);
            return started;
        }

        private void MarkThreatSchedulePending()
        {
            if (pendingThreatScheduleIndex >= 0
                && pendingThreatScheduleIndex != threatScheduleCursor)
            {
                throw new InvalidOperationException(
                    "Threat schedule attempted to overwrite an earlier pending entry.");
            }

            pendingThreatScheduleIndex = threatScheduleCursor;
        }

        private void MarkThreatSchedulePending(int threatIndex, RuntimeId runtimeId)
        {
            MarkThreatSchedulePending();
            pendingThreatIndex = threatIndex;
            pendingThreatScheduleRuntimeId = runtimeId;
        }

        private void ConsumeThreatScheduleEntry()
        {
            threatScheduleCursor++;
            pendingThreatScheduleIndex = -1;
            pendingThreatIndex = -1;
            pendingThreatScheduleRuntimeId = RuntimeId.Invalid;
        }

        private static bool ShouldRetryThreatScheduleEntry(
            in ThreatScheduleEntry entry,
            DomainResult result)
        {
            if (entry.RetryPolicy != ThreatRetryPolicy.HoldPendingNextTick)
            {
                return false;
            }

            return result.RejectReason == RejectReason.OwnerGroggy
                || result.RejectReason == RejectReason.BudgetExceeded
                || result.RejectReason == RejectReason.BufferCapacity;
        }

        private void RecordThreatScheduleDecision(
            TickIndex tick,
            in ThreatScheduleEntry entry,
            ThreatScheduleDecisionStage stage,
            DomainResult result,
            int threatIndex,
            ThreatRuntime threat)
        {
            RuntimeId runtimeId = threat == null ? RuntimeId.Invalid : threat.RuntimeId;
            AttackId attackId = threat == null ? AttackId.Invalid : threat.AttackId;
            threatScheduleDecisionCount++;
            ulong digest = StableHash.Append(
                threatScheduleDecisionDigest,
                unchecked((ulong)threatScheduleDecisionCount));
            digest = entry.AppendStableHash(digest);
            digest = StableHash.Append(digest, unchecked((ulong)tick.Value));
            digest = StableHash.Append(digest, (ulong)stage);
            digest = StableHash.Append(digest, unchecked((ulong)threatIndex));
            digest = StableHash.Append(digest, unchecked((ulong)runtimeId.Value));
            digest = StableHash.Append(digest, result.IsSuccess ? 1UL : 0UL);
            digest = StableHash.Append(digest, (ulong)result.RejectReason);
            threatScheduleDecisionDigest = digest;

            combatKernel.Trace.Record(
                tick,
                CombatEventType.ThreatScheduleDecision,
                Enemy.RuntimeId,
                runtimeId,
                attackId,
                ImpactId.Invalid,
                (int)stage,
                threatIndex,
                result.RejectReason,
                threatScheduleDecisionDigest);
        }

        private enum ThreatScheduleDecisionStage
        {
            Preflight = 0,
            Add,
            Start
        }

        private DomainResult AdvanceThreats(TickIndex tick)
        {
            if (Enemy.ControlState != EnemyControlState.Active)
            {
                return DomainResult.Success;
            }

            int advanceCount = 0;
            for (int index = 0; index < Enemy.ThreatCount; index++)
            {
                ThreatRuntime threat = Enemy.GetThreat(index);
                if (threat == null || threat.IsTerminal || threat.State == ThreatState.Scheduled)
                {
                    continue;
                }

                threatAdvanceBuffer[advanceCount++] = threat;
            }

            SortThreatsByDueTickAndRuntimeId(threatAdvanceBuffer, advanceCount);
            for (int index = 0; index < advanceCount; index++)
            {
                ThreatRuntime threat = threatAdvanceBuffer[index];
                if (threat.State == ThreatState.Telegraph
                    || threat.State == ThreatState.Recovery)
                {
                    ThreatState previousState = threat.State;
                    threat.AdvanceBeforeRelease(tick);
                    RecordThreatStateChange(tick, threat, previousState);
                }

                if (threat.State != ThreatState.Windup)
                {
                    continue;
                }

                ThreatPayloadDefinition payload = threat.Definition.Payload;
                if (payload.IsSweptProjectile
                    && !HasReusableProjectileSlots(payload.PayloadCount))
                {
                    continue;
                }

                if (payload.IsTimedImpact && !CanQueueTimedImpact())
                {
                    continue;
                }

                int reservedBefore = combatKernel.ProjectileBudget.ReservedUnits;
                int activeBefore = combatKernel.ProjectileBudget.ActiveUnits;
                ThreatState stateBeforeRelease = threat.State;
                DomainResult releaseResult = threat.TryCommitRelease(
                    tick,
                    combatKernel.ProjectileBudget,
                    out ThreatRelease release);
                if (!releaseResult.IsSuccess)
                {
                    continue;
                }

                RecordThreatStateChange(tick, threat, stateBeforeRelease);
                RecordBudgetChangeIfNeeded(
                    tick,
                    threat.RuntimeId,
                    threat.AttackId,
                    reservedBefore,
                    activeBefore);

                DomainResult payloadRelease = payload.IsSweptProjectile
                    ? RegisterThreatProjectiles(tick, threat, release)
                    : QueueTimedImpact(tick, threat, release);
                if (!payloadRelease.IsSuccess)
                {
                    return payloadRelease;
                }

                ThreatState stateBeforeRecovery = threat.State;
                DomainResult confirmed = threat.ConfirmPayloadsCreated(tick);
                if (!confirmed.IsSuccess)
                {
                    return confirmed;
                }
                RecordThreatStateChange(tick, threat, stateBeforeRecovery);
            }

            return DomainResult.Success;
        }

        private bool CanQueueTimedImpact()
        {
            return combatKernel.ImpactQueue.Count < combatKernel.ImpactQueue.Capacity
                && combatKernel.ImpactQueue.Count < combatKernel.ImpactLedger.RemainingCapacity;
        }

        private DomainResult QueueTimedImpact(
            TickIndex tick,
            ThreatRuntime threat,
            in ThreatRelease release)
        {
            ThreatPayloadDefinition payload = release.Definition.Payload;
            if (!payload.IsTimedImpact
                || payload.TargetPolicy != ThreatTargetPolicy.PlayerCombatant)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (!CanQueueTimedImpact())
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            ImpactIntent intent = new ImpactIntent(
                idAllocator.NextImpactId(),
                release.AttackId,
                ShotId.Invalid,
                Enemy.RuntimeId,
                Player.RuntimeId,
                tick + payload.ImpactDelay,
                payload.TimedImpactDamage,
                HitPart.Body,
                DamageType.Normal,
                CombatTags.EnemyAttack);
            DomainResult queued = combatKernel.ImpactQueue.TryEnqueue(
                intent,
                ImpactPhasePriority.EnemyImpact,
                threat.RuntimeId);
            if (!queued.IsSuccess)
            {
                throw new InvalidOperationException(
                    "Timed impact queue capacity changed between preflight and commit.");
            }

            return DomainResult.Success;
        }

        private DomainResult RegisterThreatProjectiles(
            TickIndex tick,
            ThreatRuntime threat,
            in ThreatRelease release)
        {
            int registeredCount = 0;
            int payloadCount = release.Definition.PayloadCount;
            for (int payloadIndex = 0; payloadIndex < payloadCount; payloadIndex++)
            {
                ProjectileRuntime projectile = new ProjectileRuntime(
                    idAllocator.NextProjectileId(),
                    idAllocator.NextRuntimeId(),
                    release.AttackId,
                    Enemy.RuntimeId,
                    Team.Enemy,
                    release.Definition.ProjectileDefinition,
                    tick,
                    release.ReservationToken);
                projectileRegistrationBuffer[payloadIndex] = projectile;

                ProjectileSpawnRequest request = new ProjectileSpawnRequest(
                    tick,
                    projectile.ImpactTick,
                    projectile.ProjectileId,
                    projectile.RuntimeId,
                    projectile.AttackId,
                    projectile.OwnerId,
                    Player.RuntimeId,
                    projectile.Team,
                    projectile.Definition.DefinitionId,
                    projectile.Definition.SweepRadiusKey,
                    projectile.Definition.PresentationKey,
                    projectile.Definition.Interceptable);
                DomainResult registered = projectileWorldPort.Register(request, out ProjectilePathSnapshot path);
                if (registered.IsSuccess)
                {
                    registeredCount++;
                }

                if (!registered.IsSuccess || !path.Matches(request))
                {
                    RejectReason reason = registered.IsSuccess
                        ? RejectReason.InvalidState
                        : registered.RejectReason;
                    DomainResult retained = RetainFailedRegistrations(tick, registeredCount);
                    DomainResult retainedRelease = retained.IsSuccess
                        ? ReleaseFailedRegistrationResources()
                        : retained;
                    int unregisteredUnits = checked(
                        (payloadCount - registeredCount)
                        * release.Definition.ProjectileDefinition.BudgetUnits);
                    RollbackActivatedThreatBudget(
                        tick,
                        threat,
                        release,
                        unregisteredUnits);
                    ClearProjectileRegistrationBuffers(payloadCount);
                    return !retained.IsSuccess
                        ? retained
                        : !retainedRelease.IsSuccess
                            ? retainedRelease
                            : DomainResult.Rejected(reason);
                }

                projectileRegistrationPathBuffer[payloadIndex] = path;
            }

            for (int payloadIndex = 0; payloadIndex < payloadCount; payloadIndex++)
            {
                AddProjectile(
                    projectileRegistrationBuffer[payloadIndex],
                    projectileRegistrationPathBuffer[payloadIndex],
                    Player.RuntimeId);
            }

            ClearProjectileRegistrationBuffers(payloadCount);
            return DomainResult.Success;
        }

        private DomainResult RetainFailedRegistrations(TickIndex tick, int registeredCount)
        {
            for (int index = 0; index < registeredCount; index++)
            {
                ProjectileRuntime projectile = projectileRegistrationBuffer[index];
                if (projectile == null)
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                if (failedRegistrationProjectileCount
                    >= failedRegistrationProjectileBuffer.Length)
                {
                    return DomainResult.Rejected(RejectReason.BufferCapacity);
                }

                ProjectileState previousState = projectile.State;
                DomainResult canceled = projectile.TryCancel(
                    tick,
                    ProjectileTerminalReason.OwnerCanceled);
                if (!canceled.IsSuccess)
                {
                    return canceled;
                }

                int failedIndex = failedRegistrationProjectileCount++;
                failedRegistrationProjectileBuffer[failedIndex] = projectile;
                failedRegistrationBudgetReleased[failedIndex] = false;
                failedRegistrationWorldReleased[failedIndex] = false;
                RecordProjectileStateChange(tick, projectile, previousState);
            }

            return DomainResult.Success;
        }

        private void RollbackActivatedThreatBudget(
            TickIndex tick,
            ThreatRuntime threat,
            in ThreatRelease release,
            int units)
        {
            if (units == 0)
            {
                return;
            }

            if (units < 0 || units > release.Definition.TotalBudgetUnits)
            {
                throw new InvalidOperationException(
                    "Failed projectile registration calculated an invalid active budget rollback.");
            }

            int reservedBefore = combatKernel.ProjectileBudget.ReservedUnits;
            int activeBefore = combatKernel.ProjectileBudget.ActiveUnits;
            DomainResult released = combatKernel.ProjectileBudget.ReleaseActive(
                release.ReservationToken,
                units);
            if (!released.IsSuccess)
            {
                throw new InvalidOperationException(
                    "Failed projectile registration did not preserve its active budget reservation.");
            }

            RecordBudgetChangeIfNeeded(
                tick,
                threat.RuntimeId,
                threat.AttackId,
                reservedBefore,
                activeBefore);
        }

        private void ClearProjectileRegistrationBuffers(int count)
        {
            for (int index = 0; index < count; index++)
            {
                projectileRegistrationBuffer[index] = null;
                projectileRegistrationPathBuffer[index] = default(ProjectilePathSnapshot);
            }
        }

        private DomainResult AdvanceProjectiles(TickIndex tick)
        {
            int advanceCount = 0;
            for (int index = 0; index < projectileSlotCount; index++)
            {
                ProjectileRuntime projectile = projectileSlots[index];
                if (projectile == null || projectile.IsTerminal)
                {
                    continue;
                }

                projectileAdvanceBuffer[advanceCount++] = projectile;
            }

            SortProjectilesByRuntimeId(projectileAdvanceBuffer, advanceCount);
            for (int index = 0; index < advanceCount; index++)
            {
                ProjectileRuntime projectile = projectileAdvanceBuffer[index];
                int projectileSlot = FindProjectileSlotByRuntimeId(projectile.RuntimeId);
                if (projectileSlot < 0
                    || !projectileWorldRegistered[projectileSlot]
                    || projectileWorldReleased[projectileSlot])
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                if (projectile.State == ProjectileState.Scheduled)
                {
                    ProjectileState previousState = projectile.State;
                    DomainResult started = projectile.StartTravelling();
                    if (!started.IsSuccess)
                    {
                        return started;
                    }
                    RecordProjectileStateChange(tick, projectile, previousState);
                }

                if (projectile.State != ProjectileState.Travelling)
                {
                    continue;
                }

                if (tick <= projectile.SpawnTick)
                {
                    continue;
                }

                if (tick <= projectile.ImpactTick)
                {
                    ProjectilePathSnapshot path = projectilePathSlots[projectileSlot];
                    if (path.ProjectileId != projectile.ProjectileId
                        || path.RuntimeId != projectile.RuntimeId
                        || path.SpawnTick != projectile.SpawnTick
                        || path.ArrivalTick != projectile.ImpactTick
                        || !projectileTargetSlots[projectileSlot].IsValid)
                    {
                        return DomainResult.Rejected(RejectReason.InvalidState);
                    }

                    DomainResult segment = path.TryGetSegment(
                        tick,
                        out SpatialVectorKey from,
                        out SpatialVectorKey to);
                    if (!segment.IsSuccess)
                    {
                        return segment;
                    }

                    ProjectileSweepRequest request = new ProjectileSweepRequest(
                        tick,
                        projectile.ProjectileId,
                        projectile.RuntimeId,
                        from,
                        to,
                        projectile.Definition.SweepRadiusKey);
                    DomainResult sweep = projectileWorldPort.Sweep(request, out ProjectileSweepHit sweepHit);
                    if (!sweep.IsSuccess)
                    {
                        return sweep;
                    }

                    if (!sweepHit.IsValid)
                    {
                        return DomainResult.Rejected(RejectReason.InvalidState);
                    }

                    switch (sweepHit.Kind)
                    {
                        case ProjectileSweepHitKind.None:
                            if (tick >= projectile.ImpactTick)
                            {
                                ProjectileState previousState = projectile.State;
                                DomainResult missed = projectile.TryMiss(tick);
                                if (!missed.IsSuccess)
                                {
                                    return missed;
                                }

                                RecordProjectileStateChange(tick, projectile, previousState);
                            }
                            break;

                        case ProjectileSweepHitKind.EnvironmentBlocked:
                        {
                            ProjectileState previousState = projectile.State;
                            DomainResult blocked = projectile.TryBlock(tick);
                            if (!blocked.IsSuccess)
                            {
                                return blocked;
                            }

                            RecordProjectileStateChange(tick, projectile, previousState);
                            break;
                        }

                        case ProjectileSweepHitKind.Target:
                        {
                            if (sweepHit.TargetId != projectileTargetSlots[projectileSlot]
                                || sweepHit.HitPart == HitPart.Projectile)
                            {
                                return DomainResult.Rejected(RejectReason.InvalidTarget);
                            }

                            if (combatKernel.ImpactQueue.Count >= combatKernel.ImpactQueue.Capacity
                                || combatKernel.ImpactQueue.Count
                                    >= combatKernel.ImpactLedger.RemainingCapacity)
                            {
                                return DomainResult.Rejected(RejectReason.BufferCapacity);
                            }

                            ImpactIntent intent = new ImpactIntent(
                                idAllocator.NextImpactId(),
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
                                throw new InvalidOperationException(
                                    "Impact queue capacity was exhausted while committing a projectile sweep hit.");
                            }

                            ProjectileState previousState = projectile.State;
                            DomainResult hit = projectile.TryHit(tick);
                            if (!hit.IsSuccess)
                            {
                                throw new InvalidOperationException(
                                    "Projectile changed state between sweep impact enqueue and terminal commit.");
                            }

                            RecordProjectileStateChange(tick, projectile, previousState);
                            break;
                        }

                        default:
                            return DomainResult.Rejected(RejectReason.InvalidState);
                    }

                    continue;
                }

                if (tick >= projectile.ExpireTick)
                {
                    ProjectileState previousState = projectile.State;
                    DomainResult expired = projectile.TryExpire(tick);
                    if (!expired.IsSuccess)
                    {
                        return expired;
                    }
                    RecordProjectileStateChange(tick, projectile, previousState);
                }
            }

            return DomainResult.Success;
        }

        private bool HasReusableProjectileSlots(int requiredCount)
        {
            if (requiredCount <= 0)
            {
                return false;
            }

            int available = projectileSlots.Length - projectileSlotCount;
            for (int index = 0; index < projectileSlotCount; index++)
            {
                ProjectileRuntime projectile = projectileSlots[index];
                if (projectile == null
                    || (projectile.IsTerminal
                        && projectileBudgetReleased[index]
                        && projectileWorldReleased[index]))
                {
                    available++;
                }
            }

            return available >= requiredCount;
        }

        private void AddProjectile(
            ProjectileRuntime projectile,
            in ProjectilePathSnapshot path,
            RuntimeId targetId)
        {
            if (projectile == null
                || !targetId.IsValid
                || path.ProjectileId != projectile.ProjectileId
                || path.RuntimeId != projectile.RuntimeId
                || path.SpawnTick != projectile.SpawnTick
                || path.ArrivalTick != projectile.ImpactTick)
            {
                throw new InvalidOperationException(
                    "A projectile world path must match the runtime committed to its slot.");
            }

            int slot = FindReusableProjectileSlot();
            if (slot < 0)
            {
                throw new InvalidOperationException("Projectile capacity was not validated before release.");
            }

            projectileSlots[slot] = projectile;
            projectilePathSlots[slot] = path;
            projectileTargetSlots[slot] = targetId;
            projectileBudgetReleased[slot] = false;
            projectileWorldRegistered[slot] = true;
            projectileWorldReleased[slot] = false;
            if (slot >= projectileSlotCount)
            {
                projectileSlotCount = slot + 1;
            }

            combatKernel.Trace.Record(
                projectile.SpawnTick,
                CombatEventType.ProjectileStateChanged,
                projectile.OwnerId,
                projectile.RuntimeId,
                projectile.AttackId,
                ImpactId.Invalid,
                -1,
                (int)projectile.State);
        }

        private int FindReusableProjectileSlot()
        {
            for (int index = 0; index < projectileSlotCount; index++)
            {
                if (projectileSlots[index] == null
                    || (projectileSlots[index].IsTerminal
                        && projectileBudgetReleased[index]
                        && projectileWorldReleased[index]))
                {
                    return index;
                }
            }

            return projectileSlotCount < projectileSlots.Length ? projectileSlotCount : -1;
        }

        private int FindProjectileSlotByRuntimeId(RuntimeId runtimeId)
        {
            for (int index = 0; index < projectileSlotCount; index++)
            {
                ProjectileRuntime projectile = projectileSlots[index];
                if (projectile != null && projectile.RuntimeId == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private ProjectileRuntime FindProjectileByRuntimeId(RuntimeId runtimeId)
        {
            int slot = FindProjectileSlotByRuntimeId(runtimeId);
            return slot >= 0 ? projectileSlots[slot] : null;
        }

        private DomainResult ReleaseTerminalProjectileResources()
        {
            int releaseCount = 0;
            for (int index = 0; index < projectileSlotCount; index++)
            {
                ProjectileRuntime projectile = projectileSlots[index];
                if (projectile == null
                    || !projectile.IsTerminal
                    || (projectileBudgetReleased[index] && projectileWorldReleased[index]))
                {
                    continue;
                }

                projectileAdvanceBuffer[releaseCount++] = projectile;
            }

            SortProjectilesByRuntimeId(projectileAdvanceBuffer, releaseCount);
            DomainResult firstFailure = DomainResult.Success;
            for (int index = 0; index < releaseCount; index++)
            {
                ProjectileRuntime projectile = projectileAdvanceBuffer[index];
                int slot = FindProjectileSlotByRuntimeId(projectile.RuntimeId);
                if (slot < 0)
                {
                    if (firstFailure.IsSuccess)
                    {
                        firstFailure = DomainResult.Rejected(RejectReason.InvalidState);
                    }
                    continue;
                }

                if (!projectileWorldReleased[slot])
                {
                    if (!projectileWorldRegistered[slot]
                        || !projectile.TerminalTick.IsValid
                        || projectile.TerminalReason == ProjectileTerminalReason.None)
                    {
                        if (firstFailure.IsSuccess)
                        {
                            firstFailure = DomainResult.Rejected(RejectReason.InvalidState);
                        }
                    }
                    else
                    {
                        DomainResult worldRelease = projectileWorldPort.Release(
                            new ProjectileReleaseRequest(
                                projectile.TerminalTick,
                                projectile.ProjectileId,
                                projectile.RuntimeId,
                                projectile.TerminalReason));
                        if (worldRelease.IsSuccess)
                        {
                            projectileWorldReleased[slot] = true;
                        }
                        else if (firstFailure.IsSuccess)
                        {
                            firstFailure = worldRelease;
                        }
                    }
                }

                if (projectileBudgetReleased[slot])
                {
                    continue;
                }

                int reservedBefore = combatKernel.ProjectileBudget.ReservedUnits;
                int activeBefore = combatKernel.ProjectileBudget.ActiveUnits;
                DomainResult released = combatKernel.ProjectileBudget.ReleaseActive(
                    projectile.ReservationToken,
                    projectile.Definition.BudgetUnits);
                if (!released.IsSuccess)
                {
                    throw new InvalidOperationException(
                        "Projectile budget release did not match an active reservation.");
                }

                projectileBudgetReleased[slot] = true;
                RecordBudgetChangeIfNeeded(
                    projectile.TerminalTick.IsValid
                        ? projectile.TerminalTick
                        : clock.CurrentTick.IsValid
                            ? clock.CurrentTick
                            : projectile.SpawnTick,
                    projectile.RuntimeId,
                    projectile.AttackId,
                    reservedBefore,
                    activeBefore);
            }

            return firstFailure;
        }

        private bool TryCompleteFromCombatants()
        {
            bool playerDead = Player.Combatant.IsDead;
            bool enemyDead = Enemy.Combatant.IsDead;
            if (!playerDead && !enemyDead)
            {
                return false;
            }

            if (playerDead)
            {
                Player.Disable();
            }

            if (enemyDead)
            {
                CaptureThreatStates();
                int reservedBefore = combatKernel.ProjectileBudget.ReservedUnits;
                int activeBefore = combatKernel.ProjectileBudget.ActiveUnits;
                DomainResult markedDead = Enemy.MarkDead(combatKernel.ProjectileBudget);
                if (!markedDead.IsSuccess)
                {
                    throw new InvalidOperationException(
                        "Threat cancellation failed while marking the enemy dead.");
                }
                TickIndex tick = clock.CurrentTick.IsValid
                    ? clock.CurrentTick
                    : new TickIndex(clock.ExecutedTickCount);
                RecordCapturedThreatChanges(tick);
                RecordBudgetChangeIfNeeded(
                    tick,
                    Enemy.RuntimeId,
                    AttackId.Invalid,
                    reservedBefore,
                    activeBefore);
            }

            Complete(playerDead ? BattleCompletionReason.Defeat : BattleCompletionReason.Victory);
            return true;
        }

        private void Complete(BattleCompletionReason reason)
        {
            if (State == BattleSessionState.Completed
                || State == BattleSessionState.Disposed
                || State == BattleSessionState.Faulted)
            {
                return;
            }

            BattleSessionState stateBefore = State;
            completionReason = reason;
            try
            {
                CleanupActiveRuntime();
            }
            catch
            {
                FaultSession(RejectReason.InvariantFault);
                return;
            }
            State = BattleSessionState.Completed;
            clock.SetPaused(true);
            combatKernel.Trace.Record(
                clock.CurrentTick,
                CombatEventType.BattleCompleted,
                RuntimeId.Invalid,
                RuntimeId.Invalid,
                AttackId.Invalid,
                ImpactId.Invalid,
                0,
                (int)reason);
            RecordSessionState(stateBefore);
        }

        private void FaultSession(RejectReason reason)
        {
            if (State == BattleSessionState.Faulted
                || State == BattleSessionState.Disposed)
            {
                return;
            }

            BattleSessionState stateBefore = State;
            failureReason = reason == RejectReason.None
                ? RejectReason.InvariantFault
                : reason;
            completionReason = BattleCompletionReason.Faulted;

            try
            {
                CleanupActiveRuntime();
            }
            catch
            {
                // A fault is terminal even if cleanup itself reports a second
                // invariant failure. The session must not remain usable.
            }
            finally
            {
                combatKernel.Dispose();
            }

            State = BattleSessionState.Faulted;
            clock.SetPaused(true);
            combatKernel.Trace.Record(
                clock.CurrentTick,
                CombatEventType.InputRejected,
                RuntimeId.Invalid,
                RuntimeId.Invalid,
                AttackId.Invalid,
                ImpactId.Invalid,
                0,
                0,
                failureReason);
            RecordSessionState(stateBefore);
        }

        private void DisposeInternal(BattleCompletionReason reason)
        {
            if (State == BattleSessionState.Disposed)
            {
                return;
            }

            BattleSessionState stateBefore = State;
            if (completionReason == BattleCompletionReason.None
                || reason == BattleCompletionReason.Restarted)
            {
                completionReason = reason;
            }

            try
            {
                CleanupActiveRuntime();
            }
            catch
            {
                failureReason = RejectReason.InvariantFault;
            }
            finally
            {
                combatKernel.Dispose();
                State = BattleSessionState.Disposed;
                clock.SetPaused(true);
                RecordSessionState(stateBefore);
            }
        }

        internal void DisposeForRestart()
        {
            RestartRequested = true;
            DisposeInternal(BattleCompletionReason.Restarted);
        }

        private void CleanupActiveRuntime()
        {
            Player.Disable();
            InvalidOperationException firstFailure = CancelNonTerminalThreats();

            for (int index = 0; index < projectileSlotCount; index++)
            {
                ProjectileRuntime projectile = projectileSlots[index];
                if (projectile != null && !projectile.IsTerminal)
                {
                    ProjectileState previousState = projectile.State;
                    DomainResult canceled = projectile.TryCancel(
                        clock.CurrentTick,
                        ProjectileTerminalReason.SessionEnded);
                    if (!canceled.IsSuccess && canceled.RejectReason != RejectReason.AlreadyTerminal)
                    {
                        firstFailure ??= new InvalidOperationException(
                            "Projectile could not enter a terminal state during cleanup.");
                    }
                    RecordProjectileStateChange(
                        clock.CurrentTick.IsValid ? clock.CurrentTick : projectile.SpawnTick,
                        projectile,
                        previousState);
                }
            }

            try
            {
                DomainResult released = ReleaseTerminalProjectileResources();
                if (!released.IsSuccess)
                {
                    firstFailure ??= new InvalidOperationException(
                        "A terminal projectile world proxy could not be released during cleanup.");
                }

                DomainResult failedRegistrationReleased = ReleaseFailedRegistrationResources();
                if (!failedRegistrationReleased.IsSuccess)
                {
                    firstFailure ??= new InvalidOperationException(
                        "A failed projectile registration proxy could not be released during cleanup.");
                }
            }
            catch (InvalidOperationException exception)
            {
                firstFailure ??= exception;
            }
            combatKernel.Dispose();

            if (firstFailure != null)
            {
                throw firstFailure;
            }
        }

        private InvalidOperationException CancelNonTerminalThreats()
        {
            InvalidOperationException firstFailure = null;
            for (int index = 0; index < Enemy.ThreatCount; index++)
            {
                ThreatRuntime threat = Enemy.GetThreat(index);
                if (threat == null || threat.IsTerminal)
                {
                    continue;
                }

                int reservedBefore = combatKernel.ProjectileBudget.ReservedUnits;
                int activeBefore = combatKernel.ProjectileBudget.ActiveUnits;
                ThreatState previousState = threat.State;
                DomainResult canceled = threat.TryCancelForSessionTermination(
                    combatKernel.ProjectileBudget);
                if (!canceled.IsSuccess)
                {
                    firstFailure ??= new InvalidOperationException(
                        "Pending threat could not release its projectile reservation during cleanup.");
                    continue;
                }

                combatKernel.Trace.Record(
                    clock.CurrentTick.IsValid ? clock.CurrentTick : new TickIndex(0L),
                    CombatEventType.ThreatStateChanged,
                    Enemy.RuntimeId,
                    threat.RuntimeId,
                    threat.AttackId,
                    ImpactId.Invalid,
                    (int)previousState,
                    (int)threat.State);
                RecordBudgetChangeIfNeeded(
                    clock.CurrentTick.IsValid ? clock.CurrentTick : new TickIndex(0L),
                    threat.RuntimeId,
                    threat.AttackId,
                    reservedBefore,
                    activeBefore);
            }

            return firstFailure;
        }

        private DomainResult ReleaseFailedRegistrationResources()
        {
            DomainResult firstFailure = DomainResult.Success;
            for (int index = 0; index < failedRegistrationProjectileCount; index++)
            {
                ProjectileRuntime projectile = failedRegistrationProjectileBuffer[index];
                if (projectile == null)
                {
                    if (firstFailure.IsSuccess)
                    {
                        firstFailure = DomainResult.Rejected(RejectReason.InvalidState);
                    }

                    continue;
                }

                if (!failedRegistrationWorldReleased[index])
                {
                    DomainResult worldRelease = projectileWorldPort.Release(
                        new ProjectileReleaseRequest(
                            projectile.TerminalTick,
                            projectile.ProjectileId,
                            projectile.RuntimeId,
                            projectile.TerminalReason));
                    if (worldRelease.IsSuccess)
                    {
                        failedRegistrationWorldReleased[index] = true;
                    }
                    else if (firstFailure.IsSuccess)
                    {
                        firstFailure = worldRelease;
                    }
                }

                if (failedRegistrationBudgetReleased[index])
                {
                    continue;
                }

                int reservedBefore = combatKernel.ProjectileBudget.ReservedUnits;
                int activeBefore = combatKernel.ProjectileBudget.ActiveUnits;
                DomainResult budgetRelease = combatKernel.ProjectileBudget.ReleaseActive(
                    projectile.ReservationToken,
                    projectile.Definition.BudgetUnits);
                if (!budgetRelease.IsSuccess)
                {
                    throw new InvalidOperationException(
                        "A failed projectile registration did not retain its active budget unit.");
                }

                failedRegistrationBudgetReleased[index] = true;
                RecordBudgetChangeIfNeeded(
                    projectile.TerminalTick,
                    projectile.RuntimeId,
                    projectile.AttackId,
                    reservedBefore,
                    activeBefore);
            }

            return firstFailure;
        }

        private void AppendControlCommandDigest(
            SessionControlCommand command,
            DomainResult result,
            BattleSessionState stateBefore,
            BattleSessionState stateAfter)
        {
            controlCommandDigest = StableHash.Append(
                controlCommandDigest,
                unchecked((ulong)command.Sequence.Value));
            controlCommandDigest = StableHash.Append(
                controlCommandDigest,
                (ulong)command.Type);
            controlCommandDigest = StableHash.Append(
                controlCommandDigest,
                (ulong)stateBefore);
            controlCommandDigest = StableHash.Append(
                controlCommandDigest,
                (ulong)stateAfter);
            controlCommandDigest = StableHash.Append(
                controlCommandDigest,
                result.IsSuccess ? 1UL : 0UL);
            controlCommandDigest = StableHash.Append(
                controlCommandDigest,
                (ulong)result.RejectReason);
            controlCommandDigest = StableHash.Append(
                controlCommandDigest,
                (ulong)completionReason);
            return;
        }

        private void RecordSessionState(BattleSessionState previousState)
        {
            ulong payloadHash = StableHash.Mix(0x4650475F53455353UL);
            payloadHash = StableHash.Append(payloadHash, (ulong)completionReason);
            payloadHash = StableHash.Append(payloadHash, (ulong)failureReason);
            payloadHash = StableHash.Append(payloadHash, RestartRequested ? 1UL : 0UL);
            combatKernel.Trace.Record(
                clock.CurrentTick,
                CombatEventType.SessionStateChanged,
                RuntimeId.Invalid,
                RuntimeId.Invalid,
                AttackId.Invalid,
                ImpactId.Invalid,
                (int)previousState,
                (int)State,
                State == BattleSessionState.Faulted
                    ? failureReason
                    : RejectReason.None,
                payloadHash);
        }

        private void RecordThreatStateChange(
            TickIndex tick,
            ThreatRuntime threat,
            ThreatState previousState)
        {
            if (threat == null || threat.State == previousState)
            {
                return;
            }

            combatKernel.Trace.Record(
                tick,
                CombatEventType.ThreatStateChanged,
                Enemy.RuntimeId,
                threat.RuntimeId,
                threat.AttackId,
                ImpactId.Invalid,
                (int)previousState,
                (int)threat.State);
        }

        private void CaptureThreatStates()
        {
            for (int index = 0; index < Enemy.ThreatCount; index++)
            {
                ThreatRuntime threat = Enemy.GetThreat(index);
                threatStateBuffer[index] = threat == null
                    ? ThreatState.Canceled
                    : threat.State;
            }
        }

        private void RecordCapturedThreatChanges(TickIndex tick)
        {
            for (int index = 0; index < Enemy.ThreatCount; index++)
            {
                RecordThreatStateChange(
                    tick,
                    Enemy.GetThreat(index),
                    threatStateBuffer[index]);
            }
        }

        private void RecordProjectileStateChange(
            TickIndex tick,
            ProjectileRuntime projectile,
            ProjectileState previousState)
        {
            if (projectile == null || projectile.State == previousState)
            {
                return;
            }

            combatKernel.Trace.Record(
                tick,
                CombatEventType.ProjectileStateChanged,
                projectile.OwnerId,
                projectile.RuntimeId,
                projectile.AttackId,
                ImpactId.Invalid,
                (int)previousState,
                (int)projectile.State);
        }

        private void RecordBudgetChangeIfNeeded(
            TickIndex tick,
            RuntimeId sourceId,
            AttackId attackId,
            int reservedBefore,
            int activeBefore)
        {
            int reservedAfter = combatKernel.ProjectileBudget.ReservedUnits;
            int activeAfter = combatKernel.ProjectileBudget.ActiveUnits;
            if (reservedBefore == reservedAfter && activeBefore == activeAfter)
            {
                return;
            }

            ulong payloadHash = StableHash.Mix(0x4650475F42554447UL);
            payloadHash = StableHash.Append(payloadHash, unchecked((ulong)activeBefore));
            payloadHash = StableHash.Append(payloadHash, unchecked((ulong)activeAfter));
            combatKernel.Trace.Record(
                tick,
                CombatEventType.BudgetChanged,
                sourceId,
                RuntimeId.Invalid,
                attackId,
                ImpactId.Invalid,
                reservedBefore,
                reservedAfter,
                RejectReason.None,
                payloadHash);
        }

        private static ulong ComputeImpactPayloadHash(
            ImpactIntent intent,
            ImpactResolution resolution)
        {
            ulong hash = StableHash.Mix(0x4650475F494D5046UL);
            hash = StableHash.Append(hash, unchecked((ulong)intent.ShotId.Value));
            hash = StableHash.Append(hash, unchecked((ulong)intent.ImpactTick.Value));
            hash = StableHash.Append(hash, unchecked((ulong)intent.DamageSpec.BaseDamage));
            hash = StableHash.Append(hash, unchecked((ulong)intent.DamageSpec.BreakDamage));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)intent.DamageSpec.WeakpointDamageMultiplierBasisPoints));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)intent.DamageSpec.WeakpointBreakMultiplierBasisPoints));
            hash = StableHash.Append(hash, (ulong)intent.HitPart);
            hash = StableHash.Append(hash, (ulong)intent.DamageType);
            hash = StableHash.Append(hash, (ulong)intent.Tags);
            hash = StableHash.Append(hash, unchecked((ulong)intent.PelletIndex));
            hash = StableHash.Append(hash, unchecked((ulong)intent.ImpactOrdinal));
            hash = StableHash.Append(hash, (ulong)resolution.Result.RejectReason);
            hash = StableHash.Append(hash, (ulong)resolution.Packet.Channel);
            hash = StableHash.Append(hash, unchecked((ulong)resolution.Packet.AppliedAmount));
            hash = StableHash.Append(hash, unchecked((ulong)resolution.Packet.AppliedBreakAmount));
            hash = StableHash.Append(hash, resolution.PerfectRetract ? 1UL : 0UL);
            hash = StableHash.Append(hash, resolution.BarrierBroken ? 1UL : 0UL);
            hash = StableHash.Append(hash, resolution.BreakTriggered ? 1UL : 0UL);
            hash = StableHash.Append(hash, resolution.Death ? 1UL : 0UL);
            return StableHash.Append(hash, resolution.ProjectileDestroyed ? 1UL : 0UL);
        }

        private static void SortResolvedHits(ResolvedAttackHit[] hits, int count)
        {
            for (int index = 1; index < count; index++)
            {
                ResolvedAttackHit candidate = hits[index];
                int destination = index - 1;
                while (destination >= 0 && Compare(candidate, hits[destination]) < 0)
                {
                    hits[destination + 1] = hits[destination];
                    destination--;
                }

                hits[destination + 1] = candidate;
            }
        }

        private static int Compare(ResolvedAttackHit left, ResolvedAttackHit right)
        {
            int ordinal = left.ImpactOrdinal.CompareTo(right.ImpactOrdinal);
            if (ordinal != 0)
            {
                return ordinal;
            }

            int pellet = left.PelletIndex.CompareTo(right.PelletIndex);
            if (pellet != 0)
            {
                return pellet;
            }

            return left.TargetId.CompareTo(right.TargetId);
        }

        private static void SortProjectilesByRuntimeId(ProjectileRuntime[] projectiles, int count)
        {
            for (int index = 1; index < count; index++)
            {
                ProjectileRuntime candidate = projectiles[index];
                int destination = index - 1;
                while (destination >= 0
                    && candidate.RuntimeId.CompareTo(projectiles[destination].RuntimeId) < 0)
                {
                    projectiles[destination + 1] = projectiles[destination];
                    destination--;
                }

                projectiles[destination + 1] = candidate;
            }
        }

        private static void SortThreatsByDueTickAndRuntimeId(ThreatRuntime[] threats, int count)
        {
            for (int index = 1; index < count; index++)
            {
                ThreatRuntime candidate = threats[index];
                int destination = index - 1;
                while (destination >= 0
                    && CompareThreat(candidate, threats[destination]) < 0)
                {
                    threats[destination + 1] = threats[destination];
                    destination--;
                }

                threats[destination + 1] = candidate;
            }
        }

        private static int CompareThreat(ThreatRuntime left, ThreatRuntime right)
        {
            long leftDue = left.StateUntilTick.IsValid
                ? left.StateUntilTick.Value
                : long.MaxValue;
            long rightDue = right.StateUntilTick.IsValid
                ? right.StateUntilTick.Value
                : long.MaxValue;
            int due = leftDue.CompareTo(rightDue);
            if (due != 0)
            {
                return due;
            }

            return left.RuntimeId.CompareTo(right.RuntimeId);
        }
    }
}
