using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class PlayerShotPresentationTransactionTests
    {
        [Test]
        public void SuccessfulSpatialTransactionPublishesTheFrozenShotAfterCommit()
        {
            FixedPlayerShotPresentationFeed feed = new FixedPlayerShotPresentationFeed(4);
            PlayerShotPresentationBridge bridge = new PlayerShotPresentationBridge(feed);
            CapturingEnemyQueryPort queryPort = new CapturingEnemyQueryPort(bridge);
            using (BattleSession session = new BattleSessionFactory().Create(
                CombatLabHarness.CreateScenario(),
                null,
                queryPort,
                null,
                null,
                bridge))
            {
                queryPort.TargetId = session.EnemyRuntimeId;
                Assert.That(Start(session).IsSuccess, Is.True);
                int enemyLifeBefore = session.GetFinalSnapshot().EnemyLife;

                DomainResult pumped = session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    new PrimaryInputSource(),
                    out int executedSteps);

                PlayerShotPresentationEvent[] events = new PlayerShotPresentationEvent[1];
                int eventCount = feed.CopyEventsAfter(0L, events, out bool hasGap);

                Assert.That(pumped.IsSuccess, Is.True);
                Assert.That(executedSteps, Is.EqualTo(1));
                Assert.That(queryPort.CaptureCount, Is.EqualTo(1));
                Assert.That(queryPort.LastCaptureAccepted, Is.True);
                Assert.That(bridge.PendingCount, Is.Zero);
                Assert.That(hasGap, Is.False);
                Assert.That(eventCount, Is.EqualTo(1));
                Assert.That(events[0].Snapshot.AttackId, Is.EqualTo(session.SelectedAttackHits.GetOldest(0).AttackId));
                Assert.That(events[0].Snapshot.GetTrajectory(0).TerminalKind,
                    Is.EqualTo(PlayerShotTerminalKind.Combatant));
                Assert.That(events[0].Snapshot.GetTrajectory(0).TargetId,
                    Is.EqualTo(session.EnemyRuntimeId));
                Assert.That(session.GetFinalSnapshot().EnemyLife,
                    Is.EqualTo(enemyLifeBefore - session.Definition.PlayerWeapon.PrimaryDamage.BaseDamage));
            }
        }

        [Test]
        public void FailedSpatialTransactionDiscardsCaptureWithoutPublishingAVisualShot()
        {
            FixedPlayerShotPresentationFeed feed = new FixedPlayerShotPresentationFeed(4);
            PlayerShotPresentationBridge bridge = new PlayerShotPresentationBridge(feed);
            CapturingEnemyQueryPort queryPort = new CapturingEnemyQueryPort(bridge)
            {
                CandidateCount = 2
            };
            using (BattleSession session = new BattleSessionFactory().Create(
                CombatLabHarness.CreateScenario(impactHistoryCapacity: 1),
                null,
                queryPort,
                null,
                null,
                bridge))
            {
                queryPort.TargetId = session.EnemyRuntimeId;
                Assert.That(Start(session).IsSuccess, Is.True);
                int enemyLifeBefore = session.GetFinalSnapshot().EnemyLife;

                DomainResult pumped = session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    new PrimaryInputSource(),
                    out int executedSteps);

                Assert.That(pumped.IsSuccess, Is.False);
                Assert.That(pumped.RejectReason, Is.EqualTo(RejectReason.BufferCapacity));
                Assert.That(executedSteps, Is.EqualTo(1));
                Assert.That(queryPort.CaptureCount, Is.EqualTo(1));
                Assert.That(bridge.PendingCount, Is.Zero);
                Assert.That(feed.LastSequence, Is.Zero);
                Assert.That(session.SelectedAttackHits.Count, Is.Zero);
                Assert.That(session.GetFinalSnapshot().EnemyLife, Is.EqualTo(enemyLifeBefore));
            }
        }

        [Test]
        public void QueryRejectionAfterCaptureDiscardsThePendingShot()
        {
            FixedPlayerShotPresentationFeed feed = new FixedPlayerShotPresentationFeed(4);
            PlayerShotPresentationBridge bridge = new PlayerShotPresentationBridge(feed);
            CapturingEnemyQueryPort queryPort = new CapturingEnemyQueryPort(bridge)
            {
                RejectAfterCapture = true
            };
            using (BattleSession session = new BattleSessionFactory().Create(
                CombatLabHarness.CreateScenario(),
                null,
                queryPort,
                null,
                null,
                bridge))
            {
                queryPort.TargetId = session.EnemyRuntimeId;
                Assert.That(Start(session).IsSuccess, Is.True);

                DomainResult pumped = session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    new PrimaryInputSource(),
                    out int executedSteps);

                Assert.That(pumped.IsSuccess, Is.False);
                Assert.That(pumped.RejectReason, Is.EqualTo(RejectReason.BufferCapacity));
                Assert.That(executedSteps, Is.EqualTo(1));
                Assert.That(queryPort.LastCaptureAccepted, Is.True);
                Assert.That(bridge.PendingCount, Is.Zero);
                Assert.That(feed.LastSequence, Is.Zero);
            }
        }

        private static DomainResult Start(BattleSession session)
        {
            return session.ApplyControl(new SessionControlCommand(
                new ControlSequence(1),
                SessionControlCommandType.Start));
        }

        private static long OneGameplayTickWallTime()
        {
            return (TimeSpan.TicksPerSecond + GameplayClock.DefaultTickRate - 1L)
                / GameplayClock.DefaultTickRate;
        }

        private sealed class PrimaryInputSource : IBattleTickInputSource
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
                        1));
            }
        }

        private sealed class CapturingEnemyQueryPort : IAttackQueryPort
        {
            private readonly IPlayerShotQueryCaptureSink captureSink;

            public CapturingEnemyQueryPort(IPlayerShotQueryCaptureSink captureSink)
            {
                this.captureSink = captureSink;
            }

            public RuntimeId TargetId { get; set; }
            public int CandidateCount { get; set; } = 1;
            public bool RejectAfterCapture { get; set; }
            public int CaptureCount { get; private set; }
            public bool LastCaptureAccepted { get; private set; }

            public DomainResult Query(
                in AttackQueryRequest request,
                QueryCandidate[] output,
                out AttackQueryResult result)
            {
                CaptureCount++;
                if (output == null || output.Length < CandidateCount
                    || CandidateCount <= 0 || CandidateCount > request.PelletCount)
                {
                    result = AttackQueryResult.Empty;
                    return DomainResult.Rejected(RejectReason.BufferCapacity);
                }

                PlayerShotQueryCapture capture = new PlayerShotQueryCapture(
                    request,
                    request.PelletCount,
                    SpatialVectorKey.Zero,
                    0);
                for (int index = 0; index < request.PelletCount; index++)
                {
                    bool selected = index < CandidateCount;
                    SpatialVectorKey point = new SpatialVectorKey(100, 200, 400 + index);
                    capture.SetTrajectory(index, new PlayerShotTrajectory(
                        index,
                        request.TickInput.AimPose.Origin,
                        selected ? point : new SpatialVectorKey(100, 200, 20000),
                        selected ? PlayerShotTerminalKind.Combatant : PlayerShotTerminalKind.Miss,
                        selected ? TargetId : RuntimeId.Invalid,
                        HitPart.Body,
                        selected ? new GeometryId(100 + index) : GeometryId.Invalid));
                }

                LastCaptureAccepted = captureSink.TryCaptureSuccessfulQuery(capture);
                if (RejectAfterCapture)
                {
                    result = AttackQueryResult.Empty;
                    return DomainResult.Rejected(RejectReason.BufferCapacity);
                }

                for (int index = 0; index < CandidateCount; index++)
                {
                    output[index] = new QueryCandidate(
                        AttackQueryStage.Pellet,
                        index,
                        TargetId,
                        QueryTargetKind.Combatant,
                        HitPart.Body,
                        new GeometryId(100 + index),
                        100 + index,
                        new SpatialVectorKey(100, 200, 400 + index),
                        index);
                }

                result = new AttackQueryResult(CandidateCount, 0);
                return DomainResult.Success;
            }
        }
    }
}
