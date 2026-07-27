using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class BattleSessionProjectileWorldTests
    {
        [Test]
        public void ThreatReleaseRegistersOnSpawnTickAndSweepsFromTheNextTick()
        {
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort();
            using (BattleSession session = CreateSession(world))
            {
                StartZeroPhaseThreat(session, flightTicks: 2);

                CombatLabHarness.PumpOneTick(session);

                Assert.That(world.RegisterCount, Is.EqualTo(1));
                Assert.That(world.SweepCount, Is.Zero);
                Assert.That(world.ReleaseCount, Is.Zero);
                Assert.That(world.GetRegisterCall(0).Tick.Value, Is.Zero);
                Assert.That(session.GetProjectileSnapshot(0).State, Is.EqualTo(ProjectileState.Travelling));

                CombatLabHarness.PumpOneTick(session);

                Assert.That(world.SweepCount, Is.EqualTo(1));
                Assert.That(world.GetSweepCall(0).Tick.Value, Is.EqualTo(1));
                Assert.That(world.GetSweepCall(0).From.Z, Is.Zero);
                Assert.That(world.GetSweepCall(0).To.Z, Is.EqualTo(1000));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void EnemyThreatForwardsInterceptabilityToProjectileSpawnRequest(bool interceptable)
        {
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort();
            using (BattleSession session = CreateSession(world))
            {
                StartZeroPhaseThreat(session, flightTicks: 2, interceptable: interceptable);
                CombatLabHarness.PumpOneTick(session);

                Assert.That(world.RegisterCount, Is.EqualTo(1));
                Assert.That(world.GetRegisterCall(0).Interceptable, Is.EqualTo(interceptable));
            }
        }

        [Test]
        public void TargetSweepCommitsImpactAndReleasesWorldAndBudgetExactlyOnce()
        {
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort();
            using (BattleSession session = CreateSession(world))
            {
                StartZeroPhaseThreat(session, flightTicks: 2);
                CombatLabHarness.PumpTicks(session, 3);

                ProjectileSnapshot projectile = session.GetProjectileSnapshot(0);
                Assert.That(projectile.State, Is.EqualTo(ProjectileState.Hit));
                Assert.That(projectile.TerminalReason, Is.EqualTo(ProjectileTerminalReason.TargetImpact));
                Assert.That(session.ConsumedImpactCount, Is.EqualTo(1));
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
                Assert.That(world.ReleaseCount, Is.EqualTo(1));
                Assert.That(world.GetReleaseCall(0).Reason, Is.EqualTo(ProjectileTerminalReason.TargetImpact));
                Assert.That(world.ActiveRegistrationCount, Is.Zero);

                CombatLabHarness.PumpTicks(session, 2);
                Assert.That(world.ReleaseCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void FailedWorldReleaseKeepsItsReservationForFaultCleanupRetry()
        {
            SpatialPortTranscript transcript = new SpatialPortTranscript(8, 1);
            ScriptedProjectileWorldPort inner = CombatLabHarness.CreateProjectileWorldPort();
            inner.FailReleaseCall = 1;
            using (BattleSession session = CreateSession(
                new RecordingProjectileWorldPort(inner, transcript)))
            {
                StartZeroPhaseThreat(session, flightTicks: 2);
                CombatLabHarness.PumpTicks(session, 2);

                DomainResult result = PumpOneTick(session, out int executedSteps);

                Assert.That(result.RejectReason, Is.EqualTo(RejectReason.InvalidState));
                Assert.That(executedSteps, Is.EqualTo(1));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Faulted));
                Assert.That(inner.ReleaseCount, Is.EqualTo(2));
                Assert.That(inner.ActiveRegistrationCount, Is.Zero);
                Assert.That(transcript.ReservedProjectileReleaseCount, Is.Zero);
                Assert.That(transcript.Count, Is.EqualTo(5));
            }
        }

        [Test]
        public void EnvironmentBlockTerminatesWithoutPlayerDamage()
        {
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort(
                ScriptedProjectileSweepMode.EnvironmentAtFirstSweep);
            using (BattleSession session = CreateSession(world))
            {
                FinalSnapshot before = session.GetFinalSnapshot();
                StartZeroPhaseThreat(session, flightTicks: 3);
                CombatLabHarness.PumpTicks(session, 2);

                ProjectileSnapshot projectile = session.GetProjectileSnapshot(0);
                FinalSnapshot after = session.GetFinalSnapshot();
                Assert.That(projectile.TerminalReason, Is.EqualTo(ProjectileTerminalReason.EnvironmentBlocked));
                Assert.That(after.PlayerLife, Is.EqualTo(before.PlayerLife));
                Assert.That(after.PlayerBarrier, Is.EqualTo(before.PlayerBarrier));
                Assert.That(session.ConsumedImpactCount, Is.Zero);
                Assert.That(world.ReleaseCount, Is.EqualTo(1));
                Assert.That(world.GetReleaseCall(0).Reason, Is.EqualTo(ProjectileTerminalReason.EnvironmentBlocked));
            }
        }

        [Test]
        public void NoSweepHitAtArrivalBecomesMissedWithoutDamage()
        {
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort(
                ScriptedProjectileSweepMode.None);
            using (BattleSession session = CreateSession(world))
            {
                StartZeroPhaseThreat(session, flightTicks: 2);
                CombatLabHarness.PumpTicks(session, 3);

                ProjectileSnapshot projectile = session.GetProjectileSnapshot(0);
                Assert.That(projectile.State, Is.EqualTo(ProjectileState.Expired));
                Assert.That(projectile.TerminalReason, Is.EqualTo(ProjectileTerminalReason.Missed));
                Assert.That(world.SweepCount, Is.EqualTo(2));
                Assert.That(world.ReleaseCount, Is.EqualTo(1));
                Assert.That(session.ConsumedImpactCount, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
            }
        }

        [Test]
        public void PlayerInterceptBeforeEnemyPhasePreventsSweepAndEnemyImpact()
        {
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort(
                ScriptedProjectileSweepMode.TargetAtFirstSweep);
            SingleProjectileInterceptPort attackPort = new SingleProjectileInterceptPort();
            using (BattleSession session = CreateSession(world, attackPort))
            {
                StartZeroPhaseThreat(session, flightTicks: 3);
                CombatLabHarness.PumpOneTick(session);
                attackPort.TargetId = session.GetProjectileSnapshot(0).RuntimeId;
                FinalSnapshot before = session.GetFinalSnapshot();

                CombatLabHarness.PumpOneTick(
                    session,
                    tick => PlayerInputFrame.Empty(tick, aimHeld: true, primaryHeld: true));

                ProjectileSnapshot projectile = session.GetProjectileSnapshot(0);
                FinalSnapshot after = session.GetFinalSnapshot();
                Assert.That(projectile.State, Is.EqualTo(ProjectileState.Destroyed));
                Assert.That(projectile.TerminalReason, Is.EqualTo(ProjectileTerminalReason.Intercepted));
                Assert.That(world.SweepCount, Is.Zero);
                Assert.That(world.ReleaseCount, Is.EqualTo(1));
                Assert.That(world.GetReleaseCall(0).Reason, Is.EqualTo(ProjectileTerminalReason.Intercepted));
                Assert.That(after.PlayerLife, Is.EqualTo(before.PlayerLife));
                Assert.That(after.PlayerBarrier, Is.EqualTo(before.PlayerBarrier));
            }
        }

        [Test]
        public void PartialMultiProjectileRegisterFailureRollsBackWorldAndBudget()
        {
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort();
            world.FailRegisterCall = 2;
            using (BattleSession session = CreateSession(
                world,
                projectileBudgetCapacity: 2,
                projectileCapacity: 2))
            {
                StartZeroPhaseThreat(session, flightTicks: 3, payloadCount: 2);

                DomainResult pump = PumpOneTick(session, out int executedSteps);

                Assert.That(pump.RejectReason, Is.EqualTo(RejectReason.BufferCapacity));
                Assert.That(executedSteps, Is.EqualTo(1));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Faulted));
                Assert.That(session.ProjectileSlotCount, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
                Assert.That(world.RegisterCount, Is.EqualTo(2));
                Assert.That(world.ReleaseCount, Is.EqualTo(1));
                Assert.That(world.GetReleaseCall(0).Reason, Is.EqualTo(ProjectileTerminalReason.OwnerCanceled));
                Assert.That(world.ActiveRegistrationCount, Is.Zero);
            }
        }

        [Test]
        public void FailedRollbackReleaseRemainsTrackedUntilFaultCleanupRetriesIt()
        {
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort();
            world.FailRegisterCall = 2;
            world.FailReleaseCall = 1;
            using (BattleSession session = CreateSession(
                world,
                projectileBudgetCapacity: 2,
                projectileCapacity: 2))
            {
                StartZeroPhaseThreat(session, flightTicks: 3, payloadCount: 2);

                DomainResult pump = PumpOneTick(session, out int executedSteps);

                Assert.That(pump.RejectReason, Is.EqualTo(RejectReason.InvalidState));
                Assert.That(executedSteps, Is.EqualTo(1));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Faulted));
                Assert.That(session.ProjectileSlotCount, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
                Assert.That(world.RegisterCount, Is.EqualTo(2));
                Assert.That(world.ReleaseCount, Is.EqualTo(2));
                Assert.That(world.GetReleaseCall(0).Reason,
                    Is.EqualTo(ProjectileTerminalReason.OwnerCanceled));
                Assert.That(world.GetReleaseCall(1).Reason,
                    Is.EqualTo(ProjectileTerminalReason.OwnerCanceled));
                Assert.That(world.ActiveRegistrationCount, Is.Zero);
            }
        }

        [Test]
        public void DisposeReleasesTravellingProjectileAsSessionEnded()
        {
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort(
                ScriptedProjectileSweepMode.None);
            BattleSession session = CreateSession(world);
            StartZeroPhaseThreat(session, flightTicks: 10);
            CombatLabHarness.PumpOneTick(session);

            session.Dispose();
            session.Dispose();

            Assert.That(world.ReleaseCount, Is.EqualTo(1));
            Assert.That(world.GetReleaseCall(0).Reason, Is.EqualTo(ProjectileTerminalReason.SessionEnded));
            Assert.That(world.ActiveRegistrationCount, Is.Zero);
            Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
        }

        [Test]
        public void RecordedProjectileSequenceReplaysToTheSameSummary()
        {
            ScenarioDefinition scenario = CombatLabHarness.CreateScenario(
                projectileBudgetCapacity: 1,
                projectileCapacity: 1);
            SpatialPortTranscript transcript = new SpatialPortTranscript(8, 1);
            ScriptedProjectileWorldPort inner = CombatLabHarness.CreateProjectileWorldPort();
            ReplaySummary recorded = RunProjectileSequence(
                scenario,
                new RecordingProjectileWorldPort(inner, transcript),
                transcript);

            transcript.ResetReplay();
            ReplaySummary replayed = RunProjectileSequence(
                scenario,
                new ReplayProjectileWorldPort(transcript),
                transcript);

            Assert.That(transcript.ReplayRemaining, Is.Zero);
            Assert.That(replayed.FinalSnapshot.PlayerLife, Is.EqualTo(recorded.FinalSnapshot.PlayerLife));
            Assert.That(replayed.FinalSnapshot.PlayerBarrier, Is.EqualTo(recorded.FinalSnapshot.PlayerBarrier));
            Assert.That(replayed.FinalSnapshot.ActiveProjectileUnits, Is.Zero);
            Assert.That(replayed.SpatialDecisionCount, Is.EqualTo(4));
            Assert.That(replayed.SpatialDecisionDigest, Is.EqualTo(recorded.SpatialDecisionDigest));
            Assert.That(replayed.CanonicalDigest, Is.EqualTo(recorded.CanonicalDigest));
        }

        [Test]
        public void ReservedReleaseSurvivesTranscriptPressureWithoutCallingSweep()
        {
            SpatialPortTranscript transcript = new SpatialPortTranscript(2, 1);
            ScriptedProjectileWorldPort inner = CombatLabHarness.CreateProjectileWorldPort();
            RecordingProjectileWorldPort recording = new RecordingProjectileWorldPort(inner, transcript);
            ProjectileSpawnRequest spawn = CreateSpawnRequest();
            Assert.That(recording.Register(spawn, out ProjectilePathSnapshot path).IsSuccess, Is.True);
            Assert.That(inner.RegisterCount, Is.EqualTo(1));
            Assert.That(transcript.ReservedProjectileReleaseCount, Is.EqualTo(1));

            ProjectileSweepRequest sweep = new ProjectileSweepRequest(
                new TickIndex(1),
                spawn.ProjectileId,
                spawn.RuntimeId,
                path.Start,
                path.PositionAtTick(new TickIndex(1)),
                spawn.SweepRadiusKey);
            Assert.That(recording.Sweep(sweep, out ProjectileSweepHit ignoredHit).RejectReason,
                Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(recording.Release(new ProjectileReleaseRequest(
                new TickIndex(1),
                spawn.ProjectileId,
                spawn.RuntimeId,
                ProjectileTerminalReason.SessionEnded)).IsSuccess,
                Is.True);
            Assert.That(inner.SweepCount, Is.Zero);
            Assert.That(inner.ReleaseCount, Is.EqualTo(1));
            Assert.That(inner.ActiveRegistrationCount, Is.Zero);
            Assert.That(transcript.Count, Is.EqualTo(3));
            Assert.That(transcript.ReservedProjectileReleaseCount, Is.Zero);

            transcript.ResetReplay();
            ReplayProjectileWorldPort replay = new ReplayProjectileWorldPort(transcript);
            Assert.That(replay.Register(spawn, out ProjectilePathSnapshot replayPath).IsSuccess,
                Is.True);
            Assert.That(replayPath.Matches(spawn), Is.True);
            Assert.That(replay.Sweep(sweep, out ProjectileSweepHit replayHit).RejectReason,
                Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(replayHit.Kind, Is.EqualTo(ProjectileSweepHitKind.None));
            Assert.That(replay.Release(new ProjectileReleaseRequest(
                new TickIndex(1),
                spawn.ProjectileId,
                spawn.RuntimeId,
                ProjectileTerminalReason.SessionEnded)).IsSuccess,
                Is.True);
            Assert.That(transcript.ReplayRemaining, Is.Zero);
        }

        [Test]
        public void TranscriptWithoutLifecycleCapacityRejectsBeforeRegisterMutation()
        {
            SpatialPortTranscript transcript = new SpatialPortTranscript(1, 1);
            ScriptedProjectileWorldPort inner = CombatLabHarness.CreateProjectileWorldPort();
            RecordingProjectileWorldPort recording = new RecordingProjectileWorldPort(inner, transcript);

            DomainResult result = recording.Register(
                CreateSpawnRequest(),
                out ProjectilePathSnapshot ignoredPath);

            Assert.That(result.RejectReason, Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(inner.RegisterCount, Is.Zero);
            Assert.That(inner.ActiveRegistrationCount, Is.Zero);
            Assert.That(transcript.Count, Is.EqualTo(1));
            Assert.That(transcript.ReservedProjectileReleaseCount, Is.Zero);

            transcript.ResetReplay();
            ReplayProjectileWorldPort replay = new ReplayProjectileWorldPort(transcript);
            Assert.That(replay.Register(
                CreateSpawnRequest(),
                out ProjectilePathSnapshot replayPath).RejectReason,
                Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(replayPath, Is.EqualTo(default(ProjectilePathSnapshot)));
            Assert.That(transcript.ReplayRemaining, Is.Zero);
        }

        [Test]
        public void RecordedMismatchedPathIsRolledBackWithoutLosingTheProxy()
        {
            SpatialPortTranscript transcript = new SpatialPortTranscript(4, 1);
            ScriptedProjectileWorldPort inner = CombatLabHarness.CreateProjectileWorldPort();
            inner.ReturnMismatchedPath = true;
            using (BattleSession session = CreateSession(
                new RecordingProjectileWorldPort(inner, transcript)))
            {
                StartZeroPhaseThreat(session, flightTicks: 2);

                DomainResult result = PumpOneTick(session, out int executedSteps);

                Assert.That(result.RejectReason, Is.EqualTo(RejectReason.InvalidState));
                Assert.That(executedSteps, Is.EqualTo(1));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Faulted));
                Assert.That(inner.RegisterCount, Is.EqualTo(1));
                Assert.That(inner.ReleaseCount, Is.EqualTo(1));
                Assert.That(inner.ActiveRegistrationCount, Is.Zero);
                Assert.That(transcript.Count, Is.EqualTo(2));
                Assert.That(transcript.ReservedProjectileReleaseCount, Is.Zero);
            }
        }

        private static BattleSession CreateSession(
            IProjectileWorldPort world,
            IAttackResolutionPort attackPort = null,
            int projectileBudgetCapacity = 4,
            int projectileCapacity = 4)
        {
            return new BattleSessionFactory().Create(
                CombatLabHarness.CreateScenario(
                    projectileBudgetCapacity: projectileBudgetCapacity,
                    projectileCapacity: projectileCapacity),
                attackPort ?? new NullAttackResolutionPort(),
                null,
                world);
        }

        private static void StartZeroPhaseThreat(
            BattleSession session,
            int flightTicks,
            int payloadCount = 1,
            bool interceptable = true)
        {
            Assert.That(session.ApplyControl(
                new SessionControlCommand(
                    new ControlSequence(1),
                    SessionControlCommandType.Start)).IsSuccess, Is.True);
            ThreatDefinition definition = CombatLabHarness.CreateThreatDefinition(
                payloadCount: payloadCount,
                telegraphTicks: 0,
                windupTicks: 0,
                recoveryTicks: 0,
                flightTicks: flightTicks,
                interceptable: interceptable);
            Assert.That(session.TryAddThreat(definition, out int threatIndex).IsSuccess, Is.True);
            Assert.That(session.TryStartThreat(threatIndex).IsSuccess, Is.True);
        }

        private static DomainResult PumpOneTick(BattleSession session, out int executedSteps)
        {
            long elapsed = (TimeSpan.TicksPerSecond + GameplayClock.DefaultTickRate - 1L)
                / GameplayClock.DefaultTickRate;
            return session.Pump(elapsed, new EmptyInputSource(), out executedSteps);
        }

        private static ReplaySummary RunProjectileSequence(
            ScenarioDefinition scenario,
            IProjectileWorldPort world,
            ISpatialDigestView digestView)
        {
            using (BattleSession session = new BattleSessionFactory().Create(
                scenario,
                new NullAttackResolutionPort(),
                null,
                world,
                digestView))
            {
                StartZeroPhaseThreat(session, flightTicks: 2);
                CombatLabHarness.PumpTicks(session, 3);
                return session.GetReplaySummary();
            }
        }

        private static ProjectileSpawnRequest CreateSpawnRequest()
        {
            return new ProjectileSpawnRequest(
                new TickIndex(0),
                new TickIndex(2),
                new ProjectileId(1),
                new RuntimeId(3),
                new AttackId(1),
                new RuntimeId(2),
                new RuntimeId(1),
                Team.Enemy,
                301,
                1,
                false);
        }

        private sealed class EmptyInputSource : IPlayerInputSource
        {
            public PlayerInputFrame GetFrame(TickIndex tick) => PlayerInputFrame.Empty(tick);
        }

        private sealed class SingleProjectileInterceptPort : IAttackResolutionPort
        {
            public RuntimeId TargetId { get; set; } = RuntimeId.Invalid;

            public int Resolve(
                AttackSnapshot attack,
                PelletSample[] pellets,
                int pelletCount,
                ResolvedAttackHit[] output)
            {
                if (!TargetId.IsValid || output == null || output.Length == 0)
                {
                    return 0;
                }

                output[0] = new ResolvedAttackHit(TargetId, HitPart.Projectile, 0, 0);
                return 1;
            }
        }
    }
}
