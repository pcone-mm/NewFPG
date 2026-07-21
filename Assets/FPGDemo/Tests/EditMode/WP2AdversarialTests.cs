using System;
using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class WP2AdversarialTests
    {
        [Test]
        public void ThrowingAttackPortCommitsTheFaultTickAsTerminalForensicState()
        {
            using (BattleSession session = CombatLabHarness.CreateSession(
                attackResolutionPort: new ThrowingPort()))
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);

                DomainResult result = PumpPrimary(session, out int executedSteps);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.RejectReason, Is.EqualTo(RejectReason.InvariantFault));
                Assert.That(executedSteps, Is.EqualTo(1));
                Assert.That(session.ExecutedTickCount, Is.EqualTo(1L));
                Assert.That(session.CurrentTick, Is.EqualTo(new TickIndex(0L)));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Faulted));
                Assert.That(session.CompletionReason, Is.EqualTo(BattleCompletionReason.Faulted));
                Assert.That(session.FailureReason, Is.EqualTo(RejectReason.InvariantFault));
                Assert.That(session.PendingImpactCount, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
                Assert.That(session.GetFinalSnapshot().PlayerAmmo, Is.EqualTo(7));
                Assert.That(
                    HasTraceEvent(session.Trace, CombatEventType.ReleaseCommitted),
                    Is.True);
                Assert.That(
                    HasSessionStateTrace(session.Trace, BattleSessionState.Faulted),
                    Is.True);
                CombatEvent faultEvent = FindSessionStateTrace(session.Trace, BattleSessionState.Faulted);
                Assert.That(faultEvent.Tick, Is.EqualTo(new TickIndex(0L)));
                Assert.That(faultEvent.ValueBefore, Is.EqualTo((int)BattleSessionState.Running));
                Assert.That(faultEvent.RejectReason, Is.EqualTo(RejectReason.InvariantFault));
                Assert.That(
                    FindTraceIndex(session.Trace, CombatEventType.ReleaseCommitted),
                    Is.LessThan(FindTraceIndex(session.Trace, CombatEventType.InputRejected)));

                DomainResult repeated = session.Pump(
                    0L,
                    new EmptyInputSource(),
                    out int repeatedSteps);
                Assert.That(repeated.IsSuccess, Is.False);
                Assert.That(repeated.RejectReason, Is.EqualTo(RejectReason.InvariantFault));
                Assert.That(repeatedSteps, Is.Zero);
            }
        }

        [Test]
        public void OutOfRangeHitCountFaultsBeforeAnyImpactIsQueued()
        {
            using (BattleSession session = CombatLabHarness.CreateSession(
                attackResolutionPort: new CountPort(-1)))
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);

                DomainResult result = PumpPrimary(session, out int executedSteps);

                Assert.That(result.RejectReason, Is.EqualTo(RejectReason.BufferCapacity));
                Assert.That(executedSteps, Is.EqualTo(1));
                Assert.That(session.ExecutedTickCount, Is.EqualTo(1L));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Faulted));
                Assert.That(session.PendingImpactCount, Is.Zero);
                Assert.That(session.ConsumedImpactCount, Is.Zero);
            }
        }

        [Test]
        public void InvalidOrDuplicatePrimaryHitsFaultTheWholeAttackAtomically()
        {
            AssertPortFaults(new InvalidTargetPort(), RejectReason.InvalidTarget);
            AssertPortFaults(new DuplicatePelletPort(), RejectReason.DuplicateImpact);
        }

        [Test]
        public void SecondaryCannotResolveTheSameTargetTwice()
        {
            using (BattleSession session = CombatLabHarness.CreateSession(
                attackResolutionPort: new DuplicateSecondaryTargetPort()))
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                long oneTick = (TimeSpan.TicksPerSecond + GameplayClock.DefaultTickRate - 1L)
                    / GameplayClock.DefaultTickRate;

                DomainResult pressed = session.Pump(
                    oneTick,
                    new SecondaryInputSource(InputEdgeType.SecondaryPressed, 1L),
                    out int pressedSteps);
                Assert.That(pressed.IsSuccess, Is.True);
                Assert.That(pressedSteps, Is.EqualTo(1));

                DomainResult released = session.Pump(
                    oneTick,
                    new SecondaryInputSource(InputEdgeType.SecondaryReleased, 2L),
                    out int releasedSteps);
                Assert.That(released.RejectReason, Is.EqualTo(RejectReason.DuplicateImpact));
                Assert.That(releasedSteps, Is.EqualTo(1));
                Assert.That(session.ExecutedTickCount, Is.EqualTo(2L));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Faulted));
                Assert.That(session.PendingImpactCount, Is.Zero);
                Assert.That(session.ConsumedImpactCount, Is.Zero);
            }
        }

        [Test]
        public void TerminalThreatSlotsAreReusableBeyondTheConfiguredConcurrentCapacity()
        {
            BattleSession session = new BattleSessionFactory().Create(
                CombatLabHarness.CreateScenario(
                    projectileBudgetCapacity: 1,
                    projectileCapacity: 1,
                    threatCapacity: 1),
                new NullAttackResolutionPort(),
                null,
                CombatLabHarness.CreateProjectileWorldPort());
            try
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                ThreatDefinition definition = CombatLabHarness.CreateThreatDefinition(
                    projectileDamage: 0,
                    telegraphTicks: 0,
                    windupTicks: 0,
                    recoveryTicks: 0,
                    flightTicks: 1);

                for (int cycle = 0; cycle < 4; cycle++)
                {
                    Assert.That(session.TryAddThreat(definition, out int threatIndex).IsSuccess, Is.True);
                    Assert.That(threatIndex, Is.Zero);
                    Assert.That(session.TryStartThreat(threatIndex).IsSuccess, Is.True);
                    CombatLabHarness.PumpTicks(session, 2);
                    Assert.That(session.GetThreatSnapshot(threatIndex).IsTerminal, Is.True);
                    Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.Zero);
                    Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
                }

                Assert.That(session.ThreatCount, Is.EqualTo(1));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Running));
            }
            finally
            {
                session.Dispose();
            }
        }

        [Test]
        public void ThreatCommandDigestMakesRuntimeThreatInjectionPartOfReplayIdentity()
        {
            ScenarioDefinition scenario = CombatLabHarness.CreateScenario();
            ReplaySummary first = RunSingleThreatCommand(
                scenario,
                CombatLabHarness.CreateThreatDefinition(projectileDamage: 10));
            ReplaySummary repeated = RunSingleThreatCommand(
                scenario,
                CombatLabHarness.CreateThreatDefinition(projectileDamage: 10));
            ReplaySummary changed = RunSingleThreatCommand(
                scenario,
                CombatLabHarness.CreateThreatDefinition(projectileDamage: 11));

            Assert.That(repeated.DefinitionHash, Is.EqualTo(first.DefinitionHash));
            Assert.That(repeated.ThreatCommandDigest, Is.EqualTo(first.ThreatCommandDigest));
            Assert.That(repeated.CanonicalDigest, Is.EqualTo(first.CanonicalDigest));
            Assert.That(changed.DefinitionHash, Is.EqualTo(first.DefinitionHash));
            Assert.That(changed.ThreatCommandDigest, Is.Not.EqualTo(first.ThreatCommandDigest));
            Assert.That(changed.CanonicalDigest, Is.Not.EqualTo(first.CanonicalDigest));
            Assert.That(changed.ThreatCommandCount, Is.EqualTo(1L));
        }

        [Test]
        public void SameTickThreatReleaseUsesStableRuntimeIdInsteadOfStartCallOrder()
        {
            BattleSession session = new BattleSessionFactory().Create(
                CombatLabHarness.CreateScenario(
                    projectileBudgetCapacity: 2,
                    projectileCapacity: 2,
                    threatCapacity: 2),
                new NullAttackResolutionPort(),
                null,
                CombatLabHarness.CreateProjectileWorldPort());
            try
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                ThreatDefinition definition = CombatLabHarness.CreateThreatDefinition(
                    projectileDamage: 0,
                    telegraphTicks: 0,
                    windupTicks: 0,
                    recoveryTicks: 1,
                    flightTicks: 2);
                Assert.That(session.TryAddThreat(definition, out int firstIndex).IsSuccess, Is.True);
                Assert.That(session.TryAddThreat(definition, out int secondIndex).IsSuccess, Is.True);
                ThreatSnapshot firstBeforeStart = session.GetThreatSnapshot(firstIndex);
                ThreatSnapshot secondBeforeStart = session.GetThreatSnapshot(secondIndex);
                Assert.That(firstBeforeStart.RuntimeId.CompareTo(secondBeforeStart.RuntimeId), Is.LessThan(0));

                Assert.That(session.TryStartThreat(secondIndex).IsSuccess, Is.True);
                Assert.That(session.TryStartThreat(firstIndex).IsSuccess, Is.True);
                ThreatSnapshot firstStarted = session.GetThreatSnapshot(firstIndex);
                ThreatSnapshot secondStarted = session.GetThreatSnapshot(secondIndex);
                Assert.That(secondStarted.AttackId.CompareTo(firstStarted.AttackId), Is.LessThan(0));

                CombatLabHarness.PumpOneTick(session);

                Assert.That(session.ProjectileSlotCount, Is.EqualTo(2));
                Assert.That(session.GetProjectileSnapshot(0).AttackId, Is.EqualTo(firstStarted.AttackId));
                Assert.That(session.GetProjectileSnapshot(1).AttackId, Is.EqualTo(secondStarted.AttackId));
            }
            finally
            {
                session.Dispose();
            }
        }

        [Test]
        public void SecondaryCanCommitTheLastAvailableShotTargetLedgerEntry()
        {
            ScenarioDefinition scenario = CombatLabHarness.CreateScenario(
                shotTargetHistoryCapacity: 1);
            using (BattleSession session = new BattleSessionFactory().Create(
                scenario,
                new SingleSecondaryTargetPort()))
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                long oneTick = (TimeSpan.TicksPerSecond + GameplayClock.DefaultTickRate - 1L)
                    / GameplayClock.DefaultTickRate;

                Assert.That(session.Pump(
                    oneTick,
                    new SecondaryInputSource(InputEdgeType.SecondaryPressed, 1L),
                    out int pressedSteps).IsSuccess, Is.True);
                Assert.That(pressedSteps, Is.EqualTo(1));

                DomainResult release = session.Pump(
                    oneTick,
                    new SecondaryInputSource(InputEdgeType.SecondaryReleased, 2L),
                    out int releasedSteps);

                Assert.That(release.IsSuccess, Is.True);
                Assert.That(releasedSteps, Is.EqualTo(1));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Running));
                Assert.That(session.GetFinalSnapshot().EnemyLife, Is.EqualTo(96));
                Assert.That(
                    GetNonPublicProperty<CombatKernel>(session, "CombatKernel").ShotTargetLedger.Count,
                    Is.EqualTo(1));
            }
        }

        [Test]
        public void StaleThreatHandleCannotStartAReplacementInTheSameSlot()
        {
            BattleSession session = new BattleSessionFactory().Create(
                CombatLabHarness.CreateScenario(
                    projectileBudgetCapacity: 1,
                    projectileCapacity: 1,
                    threatCapacity: 1),
                new NullAttackResolutionPort(),
                null,
                CombatLabHarness.CreateProjectileWorldPort());
            try
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                ThreatDefinition definition = CombatLabHarness.CreateThreatDefinition(
                    projectileDamage: 0,
                    telegraphTicks: 0,
                    windupTicks: 0,
                    recoveryTicks: 0,
                    flightTicks: 1);

                Assert.That(session.TryAddThreat(definition, out int firstIndex).IsSuccess, Is.True);
                ThreatSnapshot first = session.GetThreatSnapshot(firstIndex);
                Assert.That(session.TryStartThreat(firstIndex).IsSuccess, Is.True);
                ThreatSnapshot firstStarted = session.GetThreatSnapshot(firstIndex);
                CombatLabHarness.PumpTicks(session, 2);

                Assert.That(session.TryAddThreat(definition, out int replacementIndex).IsSuccess, Is.True);
                ThreatSnapshot replacement = session.GetThreatSnapshot(replacementIndex);
                Assert.That(replacementIndex, Is.EqualTo(firstIndex));
                Assert.That(replacement.RuntimeId, Is.Not.EqualTo(first.RuntimeId));

                ThreatCommand staleStart = new ThreatCommand(
                    new ControlSequence(session.ThreatCommandCount + 1L),
                    new TickIndex(session.ExecutedTickCount),
                    ThreatCommandType.Start,
                    default(ThreatDefinition),
                    replacementIndex,
                    first.RuntimeId);
                long commandCountBeforeStale = session.ThreatCommandCount;
                ulong digestBeforeStale = session.ThreatCommandDigest;
                DomainResult stale = session.ApplyThreatCommand(staleStart, out int ignored);

                Assert.That(stale.RejectReason, Is.EqualTo(RejectReason.InvalidTarget));
                Assert.That(session.ThreatCommandCount, Is.EqualTo(commandCountBeforeStale + 1L));
                Assert.That(session.ThreatCommandDigest, Is.Not.EqualTo(digestBeforeStale));
                Assert.That(
                    session.GetThreatSnapshot(replacementIndex).State,
                    Is.EqualTo(ThreatState.Scheduled));
                Assert.That(session.TryStartThreat(replacementIndex).IsSuccess, Is.True);
                Assert.That(
                    session.GetThreatSnapshot(replacementIndex).AttackId.Value,
                    Is.EqualTo(firstStarted.AttackId.Value + 1L));
            }
            finally
            {
                session.Dispose();
            }
        }

        [Test]
        public void RejectedThreatAddDoesNotConsumeARuntimeId()
        {
            BattleSession session = new BattleSessionFactory().Create(
                CombatLabHarness.CreateScenario(
                    projectileBudgetCapacity: 1,
                    projectileCapacity: 1,
                    threatCapacity: 1),
                new NullAttackResolutionPort(),
                null,
                CombatLabHarness.CreateProjectileWorldPort());
            try
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                ThreatDefinition definition = CombatLabHarness.CreateThreatDefinition(
                    projectileDamage: 0,
                    telegraphTicks: 0,
                    windupTicks: 0,
                    recoveryTicks: 0,
                    flightTicks: 1);

                Assert.That(session.TryAddThreat(definition, out int firstIndex).IsSuccess, Is.True);
                ThreatSnapshot first = session.GetThreatSnapshot(firstIndex);
                Assert.That(
                    session.TryAddThreat(definition, out int rejectedIndex).RejectReason,
                    Is.EqualTo(RejectReason.BufferCapacity));
                Assert.That(rejectedIndex, Is.EqualTo(-1));

                Assert.That(session.TryStartThreat(firstIndex).IsSuccess, Is.True);
                CombatLabHarness.PumpTicks(session, 2);
                Assert.That(session.TryAddThreat(definition, out int secondIndex).IsSuccess, Is.True);
                ThreatSnapshot second = session.GetThreatSnapshot(secondIndex);

                // One released projectile consumes the only intervening RuntimeId.
                // A rejected Add must not introduce another gap.
                Assert.That(second.RuntimeId.Value, Is.EqualTo(first.RuntimeId.Value + 2L));
            }
            finally
            {
                session.Dispose();
            }
        }

        [Test]
        public void AttackPortCannotReuseAHitLeftInTheSharedOutputBuffer()
        {
            using (BattleSession session = CombatLabHarness.CreateSession(
                attackResolutionPort: new FirstWriteThenSparsePort()))
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                Assert.That(PumpPrimary(session, out int firstSteps).IsSuccess, Is.True);
                Assert.That(firstSteps, Is.EqualTo(1));
                Assert.That(session.GetFinalSnapshot().EnemyLife, Is.EqualTo(110));

                CombatLabHarness.PumpTicks(session, 2);
                DomainResult second = PumpPrimary(session, out int secondSteps);

                Assert.That(second.RejectReason, Is.EqualTo(RejectReason.InvalidTarget));
                Assert.That(secondSteps, Is.EqualTo(1));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Faulted));
                Assert.That(session.GetFinalSnapshot().EnemyLife, Is.EqualTo(110));
            }
        }

        [Test]
        public void UndefinedHitPartFromAttackPortFaultsTheSession()
        {
            AssertPortFaults(new InvalidHitPartPort(), RejectReason.InvalidState);
        }

        [Test]
        public void ResolvedImpactOrdinalParticipatesInTheCanonicalTraceDigest()
        {
            ReplaySummary first = RunSinglePrimary(new SinglePrimaryHitPort(0));
            ReplaySummary changed = RunSinglePrimary(new SinglePrimaryHitPort(1));

            Assert.That(changed.FinalSnapshot.EnemyLife, Is.EqualTo(first.FinalSnapshot.EnemyLife));
            Assert.That(changed.CanonicalDigest, Is.Not.EqualTo(first.CanonicalDigest));
        }

        [Test]
        public void RestartAndDisposeCommandsHaveDistinctReplayIdentity()
        {
            ReplaySummary restarted;
            ReplaySummary disposed;
            using (BattleSession first = CombatLabHarness.CreateSession())
            using (BattleSession second = CombatLabHarness.CreateSession())
            {
                Assert.That(first.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                Assert.That(second.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                Assert.That(first.ApplyControl(Control(2L, SessionControlCommandType.Restart)).IsSuccess, Is.True);
                Assert.That(second.ApplyControl(Control(2L, SessionControlCommandType.Dispose)).IsSuccess, Is.True);
                restarted = first.GetReplaySummary();
                disposed = second.GetReplaySummary();
            }

            Assert.That(restarted.ControlCommandCount, Is.EqualTo(2L));
            Assert.That(disposed.ControlCommandCount, Is.EqualTo(2L));
            Assert.That(restarted.ControlCommandDigest, Is.Not.EqualTo(disposed.ControlCommandDigest));
            Assert.That(restarted.CanonicalDigest, Is.Not.EqualTo(disposed.CanonicalDigest));
            Assert.That(
                restarted.FinalSnapshot.CompletionReason,
                Is.EqualTo(BattleCompletionReason.Restarted));
            Assert.That(
                disposed.FinalSnapshot.CompletionReason,
                Is.EqualTo(BattleCompletionReason.Disposed));
        }

        [Test]
        public void ControlDigestIncludesCommandTypeEvenWhenBothCommandsAreRejected()
        {
            ReplaySummary pause;
            ReplaySummary resume;
            using (BattleSession first = CombatLabHarness.CreateSession())
            using (BattleSession second = CombatLabHarness.CreateSession())
            {
                Assert.That(
                    first.ApplyControl(Control(1L, SessionControlCommandType.Pause)).IsSuccess,
                    Is.False);
                Assert.That(
                    second.ApplyControl(Control(1L, SessionControlCommandType.Resume)).IsSuccess,
                    Is.False);
                pause = first.GetReplaySummary();
                resume = second.GetReplaySummary();
            }

            Assert.That(pause.ControlCommandCount, Is.EqualTo(1L));
            Assert.That(resume.ControlCommandCount, Is.EqualTo(1L));
            Assert.That(pause.ControlCommandDigest, Is.Not.EqualTo(resume.ControlCommandDigest));
        }

        [Test]
        public void FaultedSessionCanBeDisposedThroughTheControlContract()
        {
            using (BattleSession session = CombatLabHarness.CreateSession(
                attackResolutionPort: new ThrowingPort()))
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                Assert.That(PumpPrimary(session, out int ignored).IsSuccess, Is.False);

                DomainResult dispose = session.ApplyControl(
                    Control(2L, SessionControlCommandType.Dispose));

                Assert.That(dispose.IsSuccess, Is.True);
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Disposed));
                Assert.That(session.IsCombatKernelDisposed, Is.True);
                Assert.That(session.ControlCommandCount, Is.EqualTo(2L));
                Assert.That(session.GetFinalSnapshot().CompletionReason, Is.EqualTo(BattleCompletionReason.Faulted));
            }
        }

        [Test]
        public void SecondaryCleanupFaultStillForcesKernelDisposalAndBudgetReset()
        {
            using (BattleSession session = CombatLabHarness.CreateSession(
                attackResolutionPort: new ThrowingPort()))
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                ThreatDefinition definition = CombatLabHarness.CreateThreatDefinition(
                    telegraphTicks: 100,
                    windupTicks: 1,
                    recoveryTicks: 1);
                Assert.That(session.TryAddThreat(definition, out int threatIndex).IsSuccess, Is.True);
                Assert.That(session.TryStartThreat(threatIndex).IsSuccess, Is.True);

                EnemyRuntime enemy = GetNonPublicProperty<EnemyRuntime>(session, "Enemy");
                CombatKernel kernel = GetNonPublicProperty<CombatKernel>(session, "CombatKernel");
                ThreatRuntime threat = enemy.GetThreat(threatIndex);
                Assert.That(
                    kernel.ProjectileBudget.ReleaseReservation(threat.ReservationToken).IsSuccess,
                    Is.True);

                DomainResult fault = PumpPrimary(session, out int faultSteps);

                Assert.That(fault.RejectReason, Is.EqualTo(RejectReason.InvariantFault));
                Assert.That(faultSteps, Is.EqualTo(1));
                Assert.That(session.ExecutedTickCount, Is.EqualTo(1L));
                Assert.That(session.CurrentTick, Is.EqualTo(new TickIndex(0L)));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Faulted));
                Assert.That(session.IsCombatKernelDisposed, Is.True);
                Assert.That(session.PendingImpactCount, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
            }
        }

        [Test]
        public void ExternalCompleteCleanupFaultTransitionsToFaultedAndDisposesKernel()
        {
            using (BattleSession session = CombatLabHarness.CreateSession())
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                ThreatDefinition definition = CombatLabHarness.CreateThreatDefinition(
                    telegraphTicks: 100,
                    windupTicks: 1,
                    recoveryTicks: 1);
                Assert.That(session.TryAddThreat(definition, out int threatIndex).IsSuccess, Is.True);
                Assert.That(session.TryStartThreat(threatIndex).IsSuccess, Is.True);

                EnemyRuntime enemy = GetNonPublicProperty<EnemyRuntime>(session, "Enemy");
                CombatKernel kernel = GetNonPublicProperty<CombatKernel>(session, "CombatKernel");
                Assert.That(
                    kernel.ProjectileBudget.ReleaseReservation(
                        enemy.GetThreat(threatIndex).ReservationToken).IsSuccess,
                    Is.True);

                DomainResult complete = session.ApplyControl(
                    Control(2L, SessionControlCommandType.Complete));

                Assert.That(complete.RejectReason, Is.EqualTo(RejectReason.InvariantFault));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Faulted));
                Assert.That(session.CompletionReason, Is.EqualTo(BattleCompletionReason.Faulted));
                Assert.That(session.IsCombatKernelDisposed, Is.True);
                Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
            }
        }

        [Test]
        public void GroggyCancellationRecordsEachThreatAndTheBudgetChange()
        {
            using (BattleSession session = new BattleSessionFactory().Create(
                CombatLabHarness.CreateScenario(
                    projectileBudgetCapacity: 2,
                    projectileCapacity: 2,
                    threatCapacity: 2),
                new EightPelletEnemyPort()))
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                ThreatDefinition definition = CombatLabHarness.CreateThreatDefinition(
                    telegraphTicks: 100,
                    windupTicks: 1,
                    recoveryTicks: 1);
                Assert.That(session.TryAddThreat(definition, out int firstIndex).IsSuccess, Is.True);
                Assert.That(session.TryAddThreat(definition, out int secondIndex).IsSuccess, Is.True);
                ThreatSnapshot firstThreat = session.GetThreatSnapshot(firstIndex);
                ThreatSnapshot secondThreat = session.GetThreatSnapshot(secondIndex);
                EnemyRuntime enemy = GetNonPublicProperty<EnemyRuntime>(session, "Enemy");
                Assert.That(session.TryStartThreat(firstIndex).IsSuccess, Is.True);
                Assert.That(session.TryStartThreat(secondIndex).IsSuccess, Is.True);
                int traceStart = session.Trace.Count;

                Assert.That(PumpPrimary(session, out int executedSteps).IsSuccess, Is.True);

                Assert.That(executedSteps, Is.EqualTo(1));
                Assert.That(
                    session.GetThreatSnapshot(firstIndex).State,
                    Is.EqualTo(ThreatState.Canceled));
                Assert.That(
                    session.GetThreatSnapshot(secondIndex).State,
                    Is.EqualTo(ThreatState.Canceled));
                Assert.That(
                    HasThreatStateTraceFrom(
                        session.Trace,
                        traceStart,
                        firstThreat.RuntimeId,
                        ThreatState.Canceled),
                    Is.True);
                Assert.That(
                    HasThreatStateTraceFrom(
                        session.Trace,
                        traceStart,
                        secondThreat.RuntimeId,
                        ThreatState.Canceled),
                    Is.True);
                Assert.That(
                    HasBudgetChangeFrom(
                        session.Trace,
                        traceStart,
                        enemy.RuntimeId,
                        2,
                        0),
                    Is.True);
                Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.Zero);
            }
        }

        private static void AssertPortFaults(IAttackResolutionPort port, RejectReason expected)
        {
            using (BattleSession session = CombatLabHarness.CreateSession(attackResolutionPort: port))
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);

                DomainResult result = PumpPrimary(session, out int executedSteps);

                Assert.That(result.RejectReason, Is.EqualTo(expected));
                Assert.That(executedSteps, Is.EqualTo(1));
                Assert.That(session.ExecutedTickCount, Is.EqualTo(1L));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Faulted));
                Assert.That(session.PendingImpactCount, Is.Zero);
                Assert.That(session.ConsumedImpactCount, Is.Zero);
            }
        }

        private static ReplaySummary RunSingleThreatCommand(
            ScenarioDefinition scenario,
            ThreatDefinition definition)
        {
            using (BattleSession session = new BattleSessionFactory().Create(
                scenario,
                new NullAttackResolutionPort()))
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                ThreatCommand command = new ThreatCommand(
                    new ControlSequence(1L),
                    new TickIndex(0L),
                    ThreatCommandType.Add,
                    definition);
                Assert.That(session.ApplyThreatCommand(command, out int threatIndex).IsSuccess, Is.True);
                Assert.That(threatIndex, Is.Zero);
                return session.GetReplaySummary();
            }
        }

        private static ReplaySummary RunSinglePrimary(IAttackResolutionPort port)
        {
            using (BattleSession session = CombatLabHarness.CreateSession(
                attackResolutionPort: port))
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                Assert.That(PumpPrimary(session, out int steps).IsSuccess, Is.True);
                Assert.That(steps, Is.EqualTo(1));
                return session.GetReplaySummary();
            }
        }

        private static bool HasTraceEvent(ICombatTraceView trace, CombatEventType eventType)
        {
            for (int index = 0; index < trace.Count; index++)
            {
                if (trace.GetOldest(index).EventType == eventType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasSessionStateTrace(
            ICombatTraceView trace,
            BattleSessionState state)
        {
            for (int index = 0; index < trace.Count; index++)
            {
                CombatEvent combatEvent = trace.GetOldest(index);
                if (combatEvent.EventType == CombatEventType.SessionStateChanged
                    && combatEvent.ValueAfter == (int)state)
                {
                    return true;
                }
            }

            return false;
        }

        private static CombatEvent FindSessionStateTrace(
            ICombatTraceView trace,
            BattleSessionState state)
        {
            for (int index = 0; index < trace.Count; index++)
            {
                CombatEvent combatEvent = trace.GetOldest(index);
                if (combatEvent.EventType == CombatEventType.SessionStateChanged
                    && combatEvent.ValueAfter == (int)state)
                {
                    return combatEvent;
                }
            }

            Assert.Fail("Expected a SessionStateChanged event for " + state + ".");
            return default(CombatEvent);
        }

        private static int FindTraceIndex(
            ICombatTraceView trace,
            CombatEventType eventType)
        {
            for (int index = 0; index < trace.Count; index++)
            {
                if (trace.GetOldest(index).EventType == eventType)
                {
                    return index;
                }
            }

            Assert.Fail("Expected a trace event for " + eventType + ".");
            return -1;
        }

        private static bool HasThreatStateTrace(
            ICombatTraceView trace,
            RuntimeId threatRuntimeId,
            ThreatState state)
        {
            for (int index = 0; index < trace.Count; index++)
            {
                CombatEvent combatEvent = trace.GetOldest(index);
                if (combatEvent.EventType == CombatEventType.ThreatStateChanged
                    && combatEvent.TargetId == threatRuntimeId
                    && combatEvent.ValueAfter == (int)state)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasThreatStateTraceFrom(
            ICombatTraceView trace,
            int startIndex,
            RuntimeId threatRuntimeId,
            ThreatState state)
        {
            for (int index = Math.Max(0, startIndex); index < trace.Count; index++)
            {
                CombatEvent combatEvent = trace.GetOldest(index);
                if (combatEvent.EventType == CombatEventType.ThreatStateChanged
                    && combatEvent.TargetId == threatRuntimeId
                    && combatEvent.ValueAfter == (int)state
                    && combatEvent.ValueBefore != (int)state)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasBudgetChangeFrom(
            ICombatTraceView trace,
            int startIndex,
            RuntimeId sourceId,
            int valueBefore,
            int valueAfter)
        {
            for (int index = Math.Max(0, startIndex); index < trace.Count; index++)
            {
                CombatEvent combatEvent = trace.GetOldest(index);
                if (combatEvent.EventType == CombatEventType.BudgetChanged
                    && combatEvent.SourceId == sourceId
                    && combatEvent.ValueBefore == valueBefore
                    && combatEvent.ValueAfter == valueAfter)
                {
                    return true;
                }
            }

            return false;
        }

        private static T GetNonPublicProperty<T>(object instance, string propertyName)
            where T : class
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (T)property.GetValue(instance, null);
        }

        private static DomainResult PumpPrimary(BattleSession session, out int executedSteps)
        {
            return session.Pump(
                (TimeSpan.TicksPerSecond + GameplayClock.DefaultTickRate - 1L)
                    / GameplayClock.DefaultTickRate,
                new PrimaryInputSource(),
                out executedSteps);
        }

        private static SessionControlCommand Control(long sequence, SessionControlCommandType type)
        {
            return new SessionControlCommand(new ControlSequence(sequence), type);
        }

        private sealed class PrimaryInputSource : IPlayerInputSource
        {
            public PlayerInputFrame GetFrame(TickIndex tick)
            {
                return PlayerInputFrame.Empty(tick, aimHeld: true, primaryHeld: true);
            }
        }

        private sealed class EmptyInputSource : IPlayerInputSource
        {
            public PlayerInputFrame GetFrame(TickIndex tick)
            {
                return PlayerInputFrame.Empty(tick);
            }
        }

        private sealed class SecondaryInputSource : IPlayerInputSource
        {
            private readonly InputEdgeType type;
            private readonly long sequence;

            public SecondaryInputSource(InputEdgeType type, long sequence)
            {
                this.type = type;
                this.sequence = sequence;
            }

            public PlayerInputFrame GetFrame(TickIndex tick)
            {
                InputEdgeCommand[] commands =
                {
                    new InputEdgeCommand(new InputSequence(sequence), type)
                };
                return new PlayerInputFrame(tick, true, false, commands, 1);
            }
        }

        private sealed class ThrowingPort : IAttackResolutionPort
        {
            public int Resolve(
                AttackSnapshot attack,
                PelletSample[] pellets,
                int pelletCount,
                ResolvedAttackHit[] output)
            {
                throw new InvalidOperationException("Synthetic port failure.");
            }
        }

        private sealed class CountPort : IAttackResolutionPort
        {
            private readonly int count;

            public CountPort(int count)
            {
                this.count = count;
            }

            public int Resolve(
                AttackSnapshot attack,
                PelletSample[] pellets,
                int pelletCount,
                ResolvedAttackHit[] output)
            {
                return count;
            }
        }

        private sealed class InvalidTargetPort : IAttackResolutionPort
        {
            public int Resolve(
                AttackSnapshot attack,
                PelletSample[] pellets,
                int pelletCount,
                ResolvedAttackHit[] output)
            {
                output[0] = new ResolvedAttackHit(RuntimeId.Invalid, HitPart.Body, 0, 0);
                return 1;
            }
        }

        private sealed class DuplicatePelletPort : IAttackResolutionPort
        {
            public int Resolve(
                AttackSnapshot attack,
                PelletSample[] pellets,
                int pelletCount,
                ResolvedAttackHit[] output)
            {
                RuntimeId enemy = new RuntimeId(2L);
                output[0] = new ResolvedAttackHit(enemy, HitPart.Body, 0, 0);
                output[1] = new ResolvedAttackHit(enemy, HitPart.Body, 0, 1);
                return 2;
            }
        }

        private sealed class DuplicateSecondaryTargetPort : IAttackResolutionPort
        {
            public int Resolve(
                AttackSnapshot attack,
                PelletSample[] pellets,
                int pelletCount,
                ResolvedAttackHit[] output)
            {
                RuntimeId enemy = new RuntimeId(2L);
                output[0] = new ResolvedAttackHit(enemy, HitPart.Body, -1, 0);
                output[1] = new ResolvedAttackHit(enemy, HitPart.Weakpoint, -1, 1);
                return 2;
            }
        }

        private sealed class SingleSecondaryTargetPort : IAttackResolutionPort
        {
            public int Resolve(
                AttackSnapshot attack,
                PelletSample[] pellets,
                int pelletCount,
                ResolvedAttackHit[] output)
            {
                output[0] = new ResolvedAttackHit(
                    new RuntimeId(2L),
                    HitPart.Body,
                    -1,
                    0);
                return 1;
            }
        }

        private sealed class FirstWriteThenSparsePort : IAttackResolutionPort
        {
            private int callCount;

            public int Resolve(
                AttackSnapshot attack,
                PelletSample[] pellets,
                int pelletCount,
                ResolvedAttackHit[] output)
            {
                callCount++;
                if (callCount == 1)
                {
                    output[0] = new ResolvedAttackHit(
                        new RuntimeId(2L),
                        HitPart.Body,
                        0,
                        0);
                }

                return 1;
            }
        }

        private sealed class InvalidHitPartPort : IAttackResolutionPort
        {
            public int Resolve(
                AttackSnapshot attack,
                PelletSample[] pellets,
                int pelletCount,
                ResolvedAttackHit[] output)
            {
                output[0] = new ResolvedAttackHit(
                    new RuntimeId(2L),
                    (HitPart)999,
                    0,
                    0);
                return 1;
            }
        }

        private sealed class SinglePrimaryHitPort : IAttackResolutionPort
        {
            private readonly int impactOrdinal;

            public SinglePrimaryHitPort(int impactOrdinal)
            {
                this.impactOrdinal = impactOrdinal;
            }

            public int Resolve(
                AttackSnapshot attack,
                PelletSample[] pellets,
                int pelletCount,
                ResolvedAttackHit[] output)
            {
                output[0] = new ResolvedAttackHit(
                    new RuntimeId(2L),
                    HitPart.Body,
                    0,
                    impactOrdinal);
                return 1;
            }
        }

        private sealed class EightPelletEnemyPort : IAttackResolutionPort
        {
            public int Resolve(
                AttackSnapshot attack,
                PelletSample[] pellets,
                int pelletCount,
                ResolvedAttackHit[] output)
            {
                for (int index = 0; index < WeaponDefinition.PrimaryPelletCount; index++)
                {
                    output[index] = new ResolvedAttackHit(
                        new RuntimeId(2L),
                        HitPart.Body,
                        index,
                        index);
                }

                return WeaponDefinition.PrimaryPelletCount;
            }
        }
    }
}
