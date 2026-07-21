using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class BattleSessionTests
    {
        [Test]
        public void ControlsStartPauseResumeAndRejectDuplicateSequence()
        {
            using (BattleSession session = CombatLabHarness.CreateSession())
            {
                Assert.That(session.State, Is.EqualTo(BattleSessionState.NotStarted));
                Assert.That(session.ApplyControl(Control(1, SessionControlCommandType.Start)).IsSuccess, Is.True);
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Running));

                Assert.That(session.ApplyControl(Control(2, SessionControlCommandType.Pause)).IsSuccess, Is.True);
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Paused));

                DomainResult duplicate = session.ApplyControl(
                    Control(2, SessionControlCommandType.Resume));
                Assert.That(duplicate.IsSuccess, Is.False);
                Assert.That(duplicate.RejectReason, Is.EqualTo(RejectReason.DuplicateSequence));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Paused));

                Assert.That(session.ApplyControl(Control(3, SessionControlCommandType.Resume)).IsSuccess, Is.True);
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Running));
            }
        }

        [Test]
        public void ZeroControlSequenceIsInvalidAndDoesNotConsumeTheFirstValidSequence()
        {
            using (BattleSession session = CombatLabHarness.CreateSession())
            {
                DomainResult invalid = session.ApplyControl(
                    Control(0, SessionControlCommandType.Start));

                Assert.That(invalid.IsSuccess, Is.False);
                Assert.That(invalid.RejectReason, Is.EqualTo(RejectReason.InvalidState));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.NotStarted));
                Assert.That(
                    session.ApplyControl(Control(1, SessionControlCommandType.Start)).IsSuccess,
                    Is.True);
            }
        }

        [Test]
        public void PumpRejectsWrongTickWithoutAdvancingCombatState()
        {
            using (BattleSession session = CombatLabHarness.CreateSession())
            {
                session.ApplyControl(Control(1, SessionControlCommandType.Start));
                FinalSnapshot before = session.GetFinalSnapshot();

                DomainResult result = session.Pump(
                    TimeSpan.TicksPerSecond,
                    new ConstantInputSource(PlayerInputFrame.Empty(new TickIndex(99))),
                    out int executedSteps);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.RejectReason, Is.EqualTo(RejectReason.WrongTick));
                Assert.That(executedSteps, Is.Zero);
                Assert.That(session.ExecutedTickCount, Is.Zero);
                Assert.That(session.CurrentTick, Is.EqualTo(TickIndex.Invalid));
                FinalSnapshot after = session.GetFinalSnapshot();
                Assert.That(after.PlayerLife, Is.EqualTo(before.PlayerLife));
                Assert.That(after.PlayerBarrier, Is.EqualTo(before.PlayerBarrier));
                Assert.That(after.PlayerAmmo, Is.EqualTo(before.PlayerAmmo));
                Assert.That(after.EnemyLife, Is.EqualTo(before.EnemyLife));
            }
        }

        [Test]
        public void RejectedInputTickIsRetriedWithoutSkippingGameplayTime()
        {
            using (BattleSession session = CombatLabHarness.CreateSession())
            {
                session.ApplyControl(Control(1, SessionControlCommandType.Start));
                DomainResult rejected = session.Pump(
                    166667L,
                    new ConstantInputSource(PlayerInputFrame.Empty(new TickIndex(99))),
                    out int rejectedSteps);

                Assert.That(rejected.IsSuccess, Is.False);
                Assert.That(rejectedSteps, Is.Zero);
                Assert.That(session.ExecutedTickCount, Is.Zero);
                Assert.That(session.CurrentTick, Is.EqualTo(TickIndex.Invalid));

                DomainResult retried = session.Pump(
                    0L,
                    new TickEchoInputSource(),
                    out int retriedSteps);
                Assert.That(retried.IsSuccess, Is.True);
                Assert.That(retriedSteps, Is.EqualTo(1));
                Assert.That(session.ExecutedTickCount, Is.EqualTo(1));
                Assert.That(session.CurrentTick, Is.EqualTo(new TickIndex(0)));
            }
        }

        [Test]
        public void RestartFactoryDisposesOldSessionAndCreatesFreshDeterministicState()
        {
            BattleSessionFactory factory = new BattleSessionFactory();
            BattleSession oldSession = factory.Create(
                CombatLabHarness.CreateScenario(seed: 77UL),
                new NullAttackResolutionPort());
            oldSession.ApplyControl(Control(1, SessionControlCommandType.Start));
            CombatLabHarness.PumpOneTick(
                oldSession,
                tick => PlayerInputFrame.Empty(tick, aimHeld: true, primaryHeld: true));
            Assert.That(oldSession.GetFinalSnapshot().PlayerAmmo,
                Is.LessThan(oldSession.Definition.PlayerWeapon.MagazineCapacity));

            BattleSession restarted = factory.Restart(oldSession, new NullAttackResolutionPort());
            try
            {
                Assert.That(oldSession.State, Is.EqualTo(BattleSessionState.Disposed));
                Assert.That(restarted.State, Is.EqualTo(BattleSessionState.NotStarted));
                FinalSnapshot fresh = restarted.GetFinalSnapshot();
                ReplaySummary replay = restarted.GetReplaySummary();
                Assert.That(fresh.ExecutedTickCount, Is.Zero);
                Assert.That(fresh.PlayerAmmo,
                    Is.EqualTo(restarted.Definition.PlayerWeapon.MagazineCapacity));
                Assert.That(fresh.ReservedProjectileUnits, Is.Zero);
                Assert.That(fresh.ActiveProjectileUnits, Is.Zero);
                Assert.That(replay.TraceEventCount, Is.Zero);
            }
            finally
            {
                restarted.Dispose();
            }
        }

        [Test]
        public void PrimaryCanExposeAndFireWithoutAimThenReturnsToBarrierWhenIdle()
        {
            using (BattleSession session = CombatLabHarness.CreateSession())
            {
                session.ApplyControl(Control(1, SessionControlCommandType.Start));
                CombatLabHarness.PumpOneTick(session);
                Assert.That(session.PlayerExposureState, Is.EqualTo(PlayerExposureState.Withdrawn));

                int ammoBefore = session.GetFinalSnapshot().PlayerAmmo;
                CombatLabHarness.PumpOneTick(
                    session,
                    tick => PlayerInputFrame.Empty(tick, primaryHeld: true));

                Assert.That(session.PlayerExposureState, Is.EqualTo(PlayerExposureState.Exposed));
                Assert.That(session.GetFinalSnapshot().PlayerAmmo, Is.EqualTo(ammoBefore - 1));

                CombatLabHarness.PumpOneTick(session);
                Assert.That(session.PlayerExposureState, Is.EqualTo(PlayerExposureState.Withdrawn));
            }
        }

        [Test]
        public void SecondaryCanChargeAndReleaseWithoutAimThenReturnsToBarrier()
        {
            using (BattleSession session = CombatLabHarness.CreateSession(
                secondaryMinimumChargeTicks: 29))
            {
                session.ApplyControl(Control(1, SessionControlCommandType.Start));
                CombatLabHarness.PumpOneTick(session);
                Assert.That(session.PlayerExposureState, Is.EqualTo(PlayerExposureState.Withdrawn));

                int ammoBefore = session.GetFinalSnapshot().PlayerAmmo;
                CombatLabHarness.PumpOneTick(
                    session,
                    tick => new PlayerInputFrame(
                        tick,
                        false,
                        false,
                        new[] { new InputEdgeCommand(new InputSequence(1L), InputEdgeType.SecondaryPressed) },
                        1));
                Assert.That(session.PlayerExposureState, Is.EqualTo(PlayerExposureState.Exposed));
                Assert.That(session.GetFinalSnapshot().PlayerAmmo, Is.EqualTo(ammoBefore));

                for (int tick = 0; tick < 28; tick++)
                {
                    CombatLabHarness.PumpOneTick(session);
                    Assert.That(session.PlayerExposureState, Is.EqualTo(PlayerExposureState.Exposed));
                }

                CombatLabHarness.PumpOneTick(
                    session,
                    tick => new PlayerInputFrame(
                        tick,
                        false,
                        false,
                        new[] { new InputEdgeCommand(new InputSequence(2L), InputEdgeType.SecondaryReleased) },
                        1));
                Assert.That(session.GetFinalSnapshot().PlayerAmmo, Is.EqualTo(ammoBefore - 2));

                CombatLabHarness.PumpOneTick(session);
                Assert.That(session.PlayerExposureState, Is.EqualTo(PlayerExposureState.Withdrawn));
            }
        }

        [Test]
        public void ReloadInputWithdrawsExposedPlayerAndKeepsThemBehindBarrier()
        {
            using (BattleSession session = CombatLabHarness.CreateSession())
            {
                session.ApplyControl(Control(1, SessionControlCommandType.Start));
                CombatLabHarness.PumpOneTick(
                    session,
                    tick => PlayerInputFrame.Empty(tick, primaryHeld: true));
                CombatLabHarness.PumpTicks(
                    session,
                    (int)session.Definition.PlayerWeapon.PrimaryInterval.Value);

                Assert.That(session.PlayerWeaponState, Is.EqualTo(WeaponState.Ready));
                Assert.That(session.PlayerExposureState, Is.EqualTo(PlayerExposureState.Withdrawn));
                Assert.That(session.GetFinalSnapshot().PlayerBarrier, Is.GreaterThan(0));

                CombatLabHarness.PumpOneTick(
                    session,
                    tick => PlayerInputFrame.Empty(tick, aimHeld: true));
                Assert.That(session.PlayerExposureState, Is.EqualTo(PlayerExposureState.Exposed));

                CombatLabHarness.PumpOneTick(
                    session,
                    tick => new PlayerInputFrame(
                        tick,
                        true,
                        false,
                        new[]
                        {
                            new InputEdgeCommand(
                                new InputSequence(1L),
                                InputEdgeType.ReloadPressed)
                        },
                        1));

                Assert.That(session.PlayerWeaponState, Is.EqualTo(WeaponState.Reloading));
                Assert.That(session.PlayerExposureState, Is.EqualTo(PlayerExposureState.Withdrawn));

                CombatLabHarness.PumpOneTick(
                    session,
                    tick => PlayerInputFrame.Empty(
                        tick,
                        aimHeld: true,
                        primaryHeld: true));

                Assert.That(session.PlayerWeaponState, Is.EqualTo(WeaponState.Reloading));
                Assert.That(session.PlayerExposureState, Is.EqualTo(PlayerExposureState.Withdrawn));
            }
        }

        [Test]
        public void DisposeIsIdempotentAndReleasesThreatReservation()
        {
            BattleSession session = CombatLabHarness.CreateSession(projectileBudgetCapacity: 3);
            session.ApplyControl(Control(1, SessionControlCommandType.Start));
            Assert.That(
                session.TryAddThreat(
                    CombatLabHarness.CreateThreatDefinition(payloadCount: 3),
                    out int threatIndex).IsSuccess,
                Is.True);
            Assert.That(session.TryStartThreat(threatIndex).IsSuccess, Is.True);
            Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.EqualTo(3));

            session.Dispose();
            session.Dispose();

            Assert.That(session.State, Is.EqualTo(BattleSessionState.Disposed));
            FinalSnapshot disposed = session.GetFinalSnapshot();
            Assert.That(disposed.ReservedProjectileUnits, Is.Zero);
            Assert.That(disposed.ActiveProjectileUnits, Is.Zero);
            Assert.That(session.PendingImpactCount, Is.Zero);
            DomainResult pump = session.Pump(
                TimeSpan.TicksPerSecond,
                new ConstantInputSource(PlayerInputFrame.Empty(new TickIndex(0))),
                out int executedSteps);
            Assert.That(pump.IsSuccess, Is.False);
            Assert.That(pump.RejectReason, Is.EqualTo(RejectReason.Disposed));
            Assert.That(executedSteps, Is.Zero);
        }

        [Test]
        public void CompleteCancelsThreatsAndClearsPendingRuntimeQueues()
        {
            using (BattleSession session = CombatLabHarness.CreateSession(projectileBudgetCapacity: 3))
            {
                session.ApplyControl(Control(1, SessionControlCommandType.Start));
                Assert.That(
                    session.TryAddThreat(
                        CombatLabHarness.CreateThreatDefinition(payloadCount: 3),
                        out int threatIndex).IsSuccess,
                    Is.True);
                Assert.That(session.TryStartThreat(threatIndex).IsSuccess, Is.True);
                Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.EqualTo(3));

                Assert.That(
                    session.ApplyControl(Control(2, SessionControlCommandType.Complete)).IsSuccess,
                    Is.True);

                Assert.That(session.State, Is.EqualTo(BattleSessionState.Completed));
                Assert.That(session.CompletionReason, Is.EqualTo(BattleCompletionReason.External));
                Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
                Assert.That(session.PendingImpactCount, Is.Zero);
                Assert.That(session.GetThreatSnapshot(threatIndex).State,
                    Is.EqualTo(ThreatState.Canceled));
            }
        }

        [Test]
        public void SameSeedAndInputsProduceSameSnapshotAndTraceDigest()
        {
            ReplaySummary first = RunDeterministicPrimarySequence(1234UL);
            ReplaySummary second = RunDeterministicPrimarySequence(1234UL);

            Assert.That(second.DefinitionHash, Is.EqualTo(first.DefinitionHash));
            Assert.That(second.ScenarioSeed, Is.EqualTo(first.ScenarioSeed));
            Assert.That(second.ExecutedTickCount, Is.EqualTo(first.ExecutedTickCount));
            Assert.That(second.TraceEventCount, Is.EqualTo(first.TraceEventCount));
            Assert.That(second.CanonicalDigest, Is.EqualTo(first.CanonicalDigest));
            AssertSnapshotsEqual(first.FinalSnapshot, second.FinalSnapshot);
        }

        [Test]
        public void TraceDigestDependsOnGameplayTicksNotWallTimePartition()
        {
            ReplaySummary onePump = RunEmptyTicks(new[]
            {
                166667L
            });
            ReplaySummary splitPump = RunEmptyTicks(new[] { 83333L, 83334L });

            Assert.That(onePump.ExecutedTickCount, Is.EqualTo(splitPump.ExecutedTickCount));
            Assert.That(onePump.CanonicalDigest, Is.EqualTo(splitPump.CanonicalDigest));
            AssertSnapshotsEqual(onePump.FinalSnapshot, splitPump.FinalSnapshot);
        }

        [Test]
        public void HarnessRunsSixHundredTicksWithoutStateOrTimeDrift()
        {
            using (BattleSession session = CombatLabHarness.CreateSession())
            {
                session.ApplyControl(Control(1, SessionControlCommandType.Start));

                CombatLabHarness.PumpTicks(session, 600);

                Assert.That(session.ExecutedTickCount, Is.EqualTo(600));
                Assert.That(session.CurrentTick, Is.EqualTo(new TickIndex(599)));
                Assert.That(session.PendingImpactCount, Is.Zero);
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Running));
            }
        }

        [Test]
        public void ProjectileCapacityIsReusableAfterAProjectileReachesTerminalState()
        {
            using (BattleSession session = CombatLabHarness.CreateSession(
                projectileBudgetCapacity: 1,
                projectileCapacity: 1))
            {
                session.ApplyControl(Control(1, SessionControlCommandType.Start));
                ThreatDefinition threatDefinition = CombatLabHarness.CreateThreatDefinition(
                    flightTicks: 1,
                    telegraphTicks: 0,
                    windupTicks: 0,
                    recoveryTicks: 0);

                Assert.That(session.TryAddThreat(threatDefinition, out int firstThreat).IsSuccess, Is.True);
                Assert.That(session.TryStartThreat(firstThreat).IsSuccess, Is.True);
                CombatLabHarness.PumpTicks(session, 2);
                Assert.That(session.GetProjectileSnapshot(0).IsTerminal, Is.True);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);

                Assert.That(session.TryAddThreat(threatDefinition, out int secondThreat).IsSuccess, Is.True);
                Assert.That(session.TryStartThreat(secondThreat).IsSuccess, Is.True);
                CombatLabHarness.PumpTicks(session, 2);

                Assert.That(session.ProjectileSlotCount, Is.EqualTo(1));
                Assert.That(session.GetProjectileSnapshot(0).ProjectileId.Value, Is.EqualTo(2));
                Assert.That(session.GetProjectileSnapshot(0).IsTerminal, Is.True);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
            }
        }

        [Test]
        public void ThreatAtProjectileCapacityCannotPartiallyReleaseAnotherPayload()
        {
            using (BattleSession session = CombatLabHarness.CreateSession(
                projectileBudgetCapacity: 2,
                projectileCapacity: 1))
            {
                session.ApplyControl(Control(1, SessionControlCommandType.Start));
                DomainResult add = session.TryAddThreat(
                    CombatLabHarness.CreateThreatDefinition(payloadCount: 2),
                    out int threatIndex);

                Assert.That(add.IsSuccess, Is.False);
                Assert.That(add.RejectReason, Is.EqualTo(RejectReason.InvalidDefinition));
                Assert.That(threatIndex, Is.EqualTo(-1));
                Assert.That(session.ThreatCount, Is.Zero);
                Assert.That(session.ProjectileSlotCount, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
            }
        }

        [Test]
        public void ZeroDurationThreatPhasesCommitAndCompleteOnDeterministicTicks()
        {
            using (BattleSession session = CombatLabHarness.CreateSession(
                projectileBudgetCapacity: 1,
                projectileCapacity: 1))
            {
                session.ApplyControl(Control(1, SessionControlCommandType.Start));
                ThreatDefinition definition = CombatLabHarness.CreateThreatDefinition(
                    telegraphTicks: 0,
                    windupTicks: 0,
                    recoveryTicks: 0,
                    flightTicks: 1);
                Assert.That(session.TryAddThreat(definition, out int threatIndex).IsSuccess, Is.True);
                Assert.That(session.TryStartThreat(threatIndex).IsSuccess, Is.True);

                CombatLabHarness.PumpOneTick(session);
                ThreatSnapshot released = session.GetThreatSnapshot(threatIndex);
                Assert.That(released.HasReleased, Is.True);
                Assert.That(released.State, Is.EqualTo(ThreatState.Recovery));
                Assert.That(session.ProjectileSlotCount, Is.EqualTo(1));

                CombatLabHarness.PumpOneTick(session);
                Assert.That(session.GetThreatSnapshot(threatIndex).State,
                    Is.EqualTo(ThreatState.Completed));
                Assert.That(session.GetProjectileSnapshot(0).IsTerminal, Is.True);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
            }
        }

        private static ReplaySummary RunDeterministicPrimarySequence(ulong seed)
        {
            using (BattleSession session = CombatLabHarness.CreateSession(seed))
            {
                session.ApplyControl(Control(1, SessionControlCommandType.Start));
                CombatLabHarness.PumpTicks(
                    session,
                    12,
                    tick => PlayerInputFrame.Empty(
                        tick,
                        aimHeld: true,
                        primaryHeld: tick.Value == 0 || tick.Value == 3 || tick.Value == 6));
                return session.GetReplaySummary();
            }
        }

        private static ReplaySummary RunEmptyTicks(long[] elapsedPartitions)
        {
            using (BattleSession session = CombatLabHarness.CreateSession())
            {
                session.ApplyControl(Control(1, SessionControlCommandType.Start));
                IPlayerInputSource source = new TickEchoInputSource();
                for (int index = 0; index < elapsedPartitions.Length; index++)
                {
                    DomainResult result = session.Pump(
                        elapsedPartitions[index],
                        source,
                        out int ignored);
                    Assert.That(result.IsSuccess, Is.True);
                }

                return session.GetReplaySummary();
            }
        }

        private static SessionControlCommand Control(long sequence, SessionControlCommandType type)
        {
            return new SessionControlCommand(new ControlSequence(sequence), type);
        }

        private static void AssertSnapshotsEqual(FinalSnapshot left, FinalSnapshot right)
        {
            Assert.That(right.State, Is.EqualTo(left.State));
            Assert.That(right.CompletionReason, Is.EqualTo(left.CompletionReason));
            Assert.That(right.ExecutedTickCount, Is.EqualTo(left.ExecutedTickCount));
            Assert.That(right.PlayerLife, Is.EqualTo(left.PlayerLife));
            Assert.That(right.PlayerBarrier, Is.EqualTo(left.PlayerBarrier));
            Assert.That(right.PlayerAmmo, Is.EqualTo(left.PlayerAmmo));
            Assert.That(right.EnemyLife, Is.EqualTo(left.EnemyLife));
            Assert.That(right.EnemyBreak, Is.EqualTo(left.EnemyBreak));
            Assert.That(right.ReservedProjectileUnits, Is.EqualTo(left.ReservedProjectileUnits));
            Assert.That(right.ActiveProjectileUnits, Is.EqualTo(left.ActiveProjectileUnits));
        }

        private sealed class ConstantInputSource : IPlayerInputSource
        {
            private readonly PlayerInputFrame frame;

            public ConstantInputSource(PlayerInputFrame frame)
            {
                this.frame = frame;
            }

            public PlayerInputFrame GetFrame(TickIndex tick)
            {
                return frame;
            }
        }

        private sealed class TickEchoInputSource : IPlayerInputSource
        {
            public PlayerInputFrame GetFrame(TickIndex tick)
            {
                return PlayerInputFrame.Empty(tick);
            }
        }
    }
}
