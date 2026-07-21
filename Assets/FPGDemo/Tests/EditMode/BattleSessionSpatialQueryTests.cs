using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class BattleSessionSpatialQueryTests
    {
        [Test]
        public void SpatialModeConsumesFrozenTickInputAndTakesPrecedenceOverLegacyResolver()
        {
            EnemyBodyQueryPort queryPort = new EnemyBodyQueryPort();
            ThrowingLegacyResolver legacyResolver = new ThrowingLegacyResolver();
            using (BattleSession session = new BattleSessionFactory().Create(
                CombatLabHarness.CreateScenario(),
                legacyResolver,
                queryPort,
                null))
            {
                queryPort.TargetId = session.EnemyRuntimeId;
                Assert.That(session.ApplyControl(new SessionControlCommand(
                    new ControlSequence(1),
                    SessionControlCommandType.Start)).IsSuccess, Is.True);

                int lifeBefore = session.GetFinalSnapshot().EnemyLife;
                DomainResult pumped = session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    new FrozenTickInputSource(),
                    out int executedSteps);

                Assert.That(pumped.IsSuccess, Is.True);
                Assert.That(executedSteps, Is.EqualTo(1));
                Assert.That(queryPort.CallCount, Is.EqualTo(1));
                Assert.That(queryPort.LastPoseVersion, Is.EqualTo(7));
                Assert.That(queryPort.LastOrigin, Is.EqualTo(new SpatialVectorKey(100, 200, 300)));
                Assert.That(legacyResolver.CallCount, Is.Zero);
                Assert.That(
                    session.GetFinalSnapshot().EnemyLife,
                    Is.EqualTo(lifeBefore - session.Definition.PlayerWeapon.PrimaryDamage.BaseDamage));
                Assert.That(session.SelectedAttackHits.Count, Is.EqualTo(1));
                SelectedAttackHit selected = session.SelectedAttackHits.GetOldest(0);
                Assert.That(selected.AttackId.IsValid, Is.True);
                Assert.That(selected.ShotId.IsValid, Is.True);
                Assert.That(selected.Tick.Value, Is.EqualTo(0));
                Assert.That(selected.ImpactOrdinal, Is.EqualTo(0));
                Assert.That(selected.QueryStage, Is.EqualTo(AttackQueryStage.Pellet));
                Assert.That(selected.SampleIndex, Is.EqualTo(0));
                Assert.That(selected.TargetId, Is.EqualTo(session.EnemyRuntimeId));
                Assert.That(selected.GeometryId.Value, Is.EqualTo(1));
                Assert.That(selected.ImpactPointKey, Is.EqualTo(new SpatialVectorKey(100, 200, 400)));
            }
        }

        [Test]
        public void TickObserverRunsOnceBeforeEachSpatialQuery()
        {
            OrderedTickObserver observer = new OrderedTickObserver();
            ObserverAwareQueryPort queryPort = new ObserverAwareQueryPort(observer);
            using (BattleSession session = new BattleSessionFactory().Create(
                CombatLabHarness.CreateScenario(),
                null,
                queryPort,
                null))
            {
                queryPort.TargetId = session.EnemyRuntimeId;
                Assert.That(session.ApplyControl(new SessionControlCommand(
                    new ControlSequence(1),
                    SessionControlCommandType.Start)).IsSuccess, Is.True);

                DomainResult pumped = session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    new FrozenTickInputSource(),
                    observer,
                    out int executedSteps);

                Assert.That(pumped.IsSuccess, Is.True);
                Assert.That(executedSteps, Is.EqualTo(1));
                Assert.That(observer.CallCount, Is.EqualTo(1));
                Assert.That(observer.LastTick, Is.EqualTo(new TickIndex(0L)));
                Assert.That(observer.ExecutedTickCountWhenCalled, Is.EqualTo(1L));
                Assert.That(queryPort.CallCount, Is.EqualTo(1));
                Assert.That(queryPort.SawObserverForQueryTick, Is.True,
                    "The scene bridge must synchronize gameplay anchors before the spatial query reads them.");
            }
        }

        [Test]
        public void SelectedHitStreamRejectsOverflowWithoutPartialAppend()
        {
            SelectedAttackHitStream stream = new SelectedAttackHitStream(1);
            SelectedAttackHit[] batch =
            {
                CreateSelectedHit(new AttackId(1), new ShotId(1), 0),
                CreateSelectedHit(new AttackId(2), new ShotId(2), 1)
            };

            Assert.That(stream.TryAppend(batch, batch.Length).RejectReason,
                Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(stream.Count, Is.Zero);
            Assert.That(stream.TryAppend(batch, 1).IsSuccess, Is.True);
            Assert.That(stream.TryAppend(batch, 1).RejectReason,
                Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(stream.Count, Is.EqualTo(1));

            SelectedAttackHit[] undersized = Array.Empty<SelectedAttackHit>();
            Assert.That(stream.CopyTo(undersized, out int required).RejectReason,
                Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(required, Is.EqualTo(1));
        }

        [Test]
        public void SpatialModeRejectsLegacyOnlyInputBeforeAdvancingTheClock()
        {
            using (BattleSession session = new BattleSessionFactory().Create(
                CombatLabHarness.CreateScenario(),
                null,
                new EmptyAttackQueryPort(),
                null))
            {
                Assert.That(session.ApplyControl(new SessionControlCommand(
                    new ControlSequence(1),
                    SessionControlCommandType.Start)).IsSuccess, Is.True);

                DomainResult result = session.Pump(
                    OneGameplayTickWallTime(),
                    new LegacyOnlyInputSource(),
                    out int executedSteps);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.RejectReason, Is.EqualTo(RejectReason.InvalidState));
                Assert.That(executedSteps, Is.Zero);
                Assert.That(session.ExecutedTickCount, Is.Zero);
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Running));
            }
        }

        [Test]
        public void RecordedSessionQueryReplaysToTheSameSnapshotAndDigest()
        {
            ScenarioDefinition scenario = CombatLabHarness.CreateScenario(seed: 0x44UL);
            SpatialPortTranscript transcript = new SpatialPortTranscript(
                operationCapacity: 4,
                queryCandidateCapacity: TargetSelector.DefaultCandidateCapacity);
            EnemyBodyQueryPort inner = new EnemyBodyQueryPort();
            ReplaySummary recordedSummary;
            SelectedAttackHit recordedHit;
            using (BattleSession recorded = new BattleSessionFactory().Create(
                scenario,
                null,
                new RecordingAttackQueryPort(inner, transcript),
                null,
                transcript))
            {
                inner.TargetId = recorded.EnemyRuntimeId;
                Assert.That(recorded.ApplyControl(new SessionControlCommand(
                    new ControlSequence(1),
                    SessionControlCommandType.Start)).IsSuccess, Is.True);
                Assert.That(recorded.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    new FrozenTickInputSource(),
                    out int recordedSteps).IsSuccess, Is.True);
                Assert.That(recordedSteps, Is.EqualTo(1));
                recordedSummary = recorded.GetReplaySummary();
                recordedHit = recorded.SelectedAttackHits.GetOldest(0);
            }

            transcript.ResetReplay();
            ReplaySummary replayedSummary;
            SelectedAttackHit replayedHit;
            using (BattleSession replayed = new BattleSessionFactory().Create(
                scenario,
                null,
                new ReplayAttackQueryPort(transcript),
                null,
                transcript))
            {
                Assert.That(replayed.ApplyControl(new SessionControlCommand(
                    new ControlSequence(1),
                    SessionControlCommandType.Start)).IsSuccess, Is.True);
                Assert.That(replayed.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    new FrozenTickInputSource(),
                    out int replayedSteps).IsSuccess, Is.True);
                Assert.That(replayedSteps, Is.EqualTo(1));
                replayedSummary = replayed.GetReplaySummary();
                replayedHit = replayed.SelectedAttackHits.GetOldest(0);
            }

            Assert.That(transcript.ReplayRemaining, Is.Zero);
            Assert.That(replayedSummary.FinalSnapshot.EnemyLife,
                Is.EqualTo(recordedSummary.FinalSnapshot.EnemyLife));
            Assert.That(replayedSummary.TraceEventCount,
                Is.EqualTo(recordedSummary.TraceEventCount));
            Assert.That(replayedSummary.SpatialDecisionDigest,
                Is.EqualTo(recordedSummary.SpatialDecisionDigest));
            Assert.That(replayedSummary.CanonicalDigest,
                Is.EqualTo(recordedSummary.CanonicalDigest));
            Assert.That(replayedHit.AttackId, Is.EqualTo(recordedHit.AttackId));
            Assert.That(replayedHit.ShotId, Is.EqualTo(recordedHit.ShotId));
            Assert.That(replayedHit.TargetId, Is.EqualTo(recordedHit.TargetId));
            Assert.That(replayedHit.GeometryId, Is.EqualTo(recordedHit.GeometryId));
            Assert.That(replayedHit.ImpactPointKey, Is.EqualTo(recordedHit.ImpactPointKey));
        }

        [Test]
        public void ThrowingQueryFaultsAfterTheCommittedTickWithoutPartialImpact()
        {
            AssertSpatialQueryFault(
                new ThrowingQueryPort(),
                RejectReason.InvariantFault,
                expectAttackSpecificTrace: true);
        }

        [Test]
        public void DroppedQueryFaultsAfterTheCommittedTickWithoutPartialImpact()
        {
            AssertSpatialQueryFault(
                new DroppedQueryPort(),
                RejectReason.BufferCapacity,
                expectAttackSpecificTrace: true);
        }

        [Test]
        public void SpatialRestartMustExplicitlyRecomposeTheQueryPort()
        {
            BattleSessionFactory factory = new BattleSessionFactory();
            EnemyBodyQueryPort initialPort = new EnemyBodyQueryPort();
            BattleSession initial = factory.Create(
                CombatLabHarness.CreateScenario(),
                null,
                initialPort,
                null);
            EnemyBodyQueryPort restartedPort = new EnemyBodyQueryPort();
            BattleSession restarted = factory.Restart(
                initial,
                null,
                restartedPort,
                null,
                null);
            try
            {
                Assert.That(initial.State, Is.EqualTo(BattleSessionState.Disposed));
                restartedPort.TargetId = restarted.EnemyRuntimeId;
                Assert.That(restarted.ApplyControl(new SessionControlCommand(
                    new ControlSequence(1),
                    SessionControlCommandType.Start)).IsSuccess, Is.True);
                Assert.That(restarted.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    new FrozenTickInputSource(),
                    out int executedSteps).IsSuccess, Is.True);
                Assert.That(executedSteps, Is.EqualTo(1));
                Assert.That(restartedPort.CallCount, Is.EqualTo(1));
            }
            finally
            {
                restarted.Dispose();
            }
        }

        private static void AssertSpatialQueryFault(
            IAttackQueryPort queryPort,
            RejectReason expectedReason,
            bool expectAttackSpecificTrace)
        {
            using (BattleSession session = new BattleSessionFactory().Create(
                CombatLabHarness.CreateScenario(),
                null,
                queryPort,
                null))
            {
                Assert.That(session.ApplyControl(new SessionControlCommand(
                    new ControlSequence(1),
                    SessionControlCommandType.Start)).IsSuccess, Is.True);
                int enemyLife = session.GetFinalSnapshot().EnemyLife;

                DomainResult result = session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    new FrozenTickInputSource(),
                    out int executedSteps);

                Assert.That(result.RejectReason, Is.EqualTo(expectedReason));
                Assert.That(executedSteps, Is.EqualTo(1));
                Assert.That(session.ExecutedTickCount, Is.EqualTo(1));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Faulted));
                Assert.That(session.FailureReason, Is.EqualTo(expectedReason));
                Assert.That(session.GetFinalSnapshot().EnemyLife, Is.EqualTo(enemyLife));
                Assert.That(session.PendingImpactCount, Is.Zero);

                bool foundAttackReject = false;
                for (int index = 0; index < session.Trace.Count; index++)
                {
                    CombatEvent combatEvent = session.Trace.GetOldest(index);
                    if (combatEvent.EventType == CombatEventType.InputRejected
                        && combatEvent.AttackId.IsValid
                        && combatEvent.RejectReason == expectedReason)
                    {
                        foundAttackReject = true;
                        break;
                    }
                }

                Assert.That(foundAttackReject, Is.EqualTo(expectAttackSpecificTrace));
            }
        }

        private static long OneGameplayTickWallTime()
        {
            return (TimeSpan.TicksPerSecond + GameplayClock.DefaultTickRate - 1L)
                / GameplayClock.DefaultTickRate;
        }

        private static SelectedAttackHit CreateSelectedHit(
            AttackId attackId,
            ShotId shotId,
            int impactOrdinal)
        {
            return new SelectedAttackHit(
                attackId,
                shotId,
                new TickIndex(0),
                impactOrdinal,
                AttackQueryStage.Direct,
                -1,
                new RuntimeId(2),
                QueryTargetKind.Combatant,
                HitPart.Body,
                new GeometryId(3 + impactOrdinal),
                new SpatialVectorKey(0, 0, 100 + impactOrdinal));
        }

        private sealed class FrozenTickInputSource : IBattleTickInputSource
        {
            public BattleTickInput GetTickInput(TickIndex tick)
            {
                return new BattleTickInput(
                    PlayerInputFrame.Empty(tick, aimHeld: true, primaryHeld: true),
                    new AimPoseSnapshot(
                        tick,
                        new SpatialVectorKey(100, 200, 300),
                        new SpatialVectorKey(0, 0, SpatialContract.DirectionUnits),
                        new SpatialVectorKey(SpatialContract.DirectionUnits, 0, 0),
                        new SpatialVectorKey(0, SpatialContract.DirectionUnits, 0),
                        7));
            }
        }

        private sealed class OrderedTickObserver : IBattleTickObserver
        {
            public int CallCount { get; private set; }
            public TickIndex LastTick { get; private set; }
            public long ExecutedTickCountWhenCalled { get; private set; }

            public void BeforeBattleTick(BattleSession session, TickIndex tick)
            {
                CallCount++;
                LastTick = tick;
                ExecutedTickCountWhenCalled = session.ExecutedTickCount;
            }

            public bool WasCalledFor(TickIndex tick)
            {
                return CallCount > 0 && LastTick == tick;
            }
        }

        private sealed class ObserverAwareQueryPort : IAttackQueryPort
        {
            private readonly OrderedTickObserver observer;

            public ObserverAwareQueryPort(OrderedTickObserver observer)
            {
                this.observer = observer;
            }

            public RuntimeId TargetId { get; set; }
            public int CallCount { get; private set; }
            public bool SawObserverForQueryTick { get; private set; }

            public DomainResult Query(
                in AttackQueryRequest request,
                QueryCandidate[] output,
                out AttackQueryResult result)
            {
                CallCount++;
                SawObserverForQueryTick = observer != null
                    && observer.WasCalledFor(request.TickInput.Tick);
                if (output == null || output.Length == 0)
                {
                    result = new AttackQueryResult(0, 1);
                    return DomainResult.Rejected(RejectReason.BufferCapacity);
                }

                output[0] = new QueryCandidate(
                    AttackQueryStage.Pellet,
                    0,
                    TargetId,
                    QueryTargetKind.Combatant,
                    HitPart.Body,
                    new GeometryId(1),
                    100,
                    new SpatialVectorKey(100, 200, 400),
                    0);
                result = new AttackQueryResult(1, 0);
                return DomainResult.Success;
            }
        }

        private sealed class LegacyOnlyInputSource : IPlayerInputSource
        {
            public PlayerInputFrame GetFrame(TickIndex tick)
            {
                return PlayerInputFrame.Empty(tick, aimHeld: true, primaryHeld: true);
            }
        }

        private sealed class ThrowingLegacyResolver : IAttackResolutionPort
        {
            public int CallCount { get; private set; }

            public int Resolve(
                AttackSnapshot attack,
                PelletSample[] pellets,
                int pelletCount,
                ResolvedAttackHit[] output)
            {
                CallCount++;
                throw new InvalidOperationException("Spatial mode must not call the legacy resolver.");
            }
        }

        private sealed class EnemyBodyQueryPort : IAttackQueryPort
        {
            public RuntimeId TargetId { get; set; }
            public int CallCount { get; private set; }
            public long LastPoseVersion { get; private set; }
            public SpatialVectorKey LastOrigin { get; private set; }

            public DomainResult Query(
                in AttackQueryRequest request,
                QueryCandidate[] output,
                out AttackQueryResult result)
            {
                CallCount++;
                LastPoseVersion = request.TickInput.AimPose.PoseVersion;
                LastOrigin = request.TickInput.AimPose.Origin;
                if (output == null || output.Length == 0)
                {
                    result = new AttackQueryResult(0, 1);
                    return DomainResult.Rejected(RejectReason.BufferCapacity);
                }

                output[0] = new QueryCandidate(
                    AttackQueryStage.Pellet,
                    0,
                    TargetId,
                    QueryTargetKind.Combatant,
                    HitPart.Body,
                    new GeometryId(1),
                    100,
                    new SpatialVectorKey(100, 200, 400),
                    0);
                result = new AttackQueryResult(1, 0);
                return DomainResult.Success;
            }
        }

        private sealed class ThrowingQueryPort : IAttackQueryPort
        {
            public DomainResult Query(
                in AttackQueryRequest request,
                QueryCandidate[] output,
                out AttackQueryResult result)
            {
                result = AttackQueryResult.Empty;
                throw new InvalidOperationException("Injected query failure.");
            }
        }

        private sealed class DroppedQueryPort : IAttackQueryPort
        {
            public DomainResult Query(
                in AttackQueryRequest request,
                QueryCandidate[] output,
                out AttackQueryResult result)
            {
                result = new AttackQueryResult(0, 1);
                return DomainResult.Success;
            }
        }
    }
}
