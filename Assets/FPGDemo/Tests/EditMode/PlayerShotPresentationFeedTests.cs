using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class PlayerShotPresentationFeedTests
    {
        [Test]
        public void BridgePublishesOnlyExplicitlyCommittedCaptures()
        {
            FixedPlayerShotPresentationFeed feed = new FixedPlayerShotPresentationFeed(4);
            PlayerShotPresentationBridge bridge = new PlayerShotPresentationBridge(feed, 2);
            PlayerShotQueryCapture discarded = CreatePrimaryCapture(1);
            PlayerShotQueryCapture committed = CreatePrimaryCapture(2);

            Assert.That(bridge.TryCaptureSuccessfulQuery(discarded), Is.True);
            Assert.That(bridge.PendingCount, Is.EqualTo(1));
            bridge.DiscardUncommittedShot(discarded.AttackId);

            Assert.That(bridge.PendingCount, Is.Zero);
            Assert.That(feed.LastSequence, Is.Zero);
            Assert.That(bridge.TryCaptureSuccessfulQuery(committed), Is.True);
            bridge.PublishCommittedShot(committed.AttackId, WeaponReleaseKind.Primary);

            PlayerShotPresentationEvent[] events = new PlayerShotPresentationEvent[1];
            int count = feed.CopyEventsAfter(0L, events, out bool hasGap);

            Assert.That(bridge.PendingCount, Is.Zero);
            Assert.That(bridge.RejectedPublicationCount, Is.Zero);
            Assert.That(hasGap, Is.False);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(events[0].Sequence, Is.EqualTo(1L));
            Assert.That(events[0].Snapshot.AttackId, Is.EqualTo(committed.AttackId));
            Assert.That(events[0].Snapshot.ReleaseKind, Is.EqualTo(WeaponReleaseKind.Primary));
            Assert.That(events[0].Snapshot.GetTrajectory(0).TerminalKind,
                Is.EqualTo(PlayerShotTerminalKind.Miss));
        }

        [Test]
        public void RejectedWriteDoesNotCreateASequenceGap()
        {
            FixedPlayerShotPresentationFeed feed = new FixedPlayerShotPresentationFeed(2);
            PlayerShotQueryCapture capture = CreatePrimaryCapture(1);

            Assert.That(feed.TryRecordCommitted(capture, WeaponReleaseKind.Secondary), Is.False);
            Assert.That(feed.RejectedWriteCount, Is.EqualTo(1));
            Assert.That(feed.LastSequence, Is.Zero);
            Assert.That(feed.TryRecordCommitted(capture, WeaponReleaseKind.Primary), Is.True);

            PlayerShotPresentationEvent[] events = new PlayerShotPresentationEvent[1];
            Assert.That(feed.CopyEventsAfter(0L, events, out bool hasGap), Is.EqualTo(1));
            Assert.That(hasGap, Is.False);
            Assert.That(events[0].Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void CursorDropsStaleShotsAfterRingGapAndConsumesNewEventsOnce()
        {
            FixedPlayerShotPresentationFeed feed = new FixedPlayerShotPresentationFeed(2);
            Assert.That(feed.TryRecordCommitted(CreatePrimaryCapture(1), WeaponReleaseKind.Primary), Is.True);
            Assert.That(feed.TryRecordCommitted(CreatePrimaryCapture(2), WeaponReleaseKind.Primary), Is.True);
            Assert.That(feed.TryRecordCommitted(CreatePrimaryCapture(3), WeaponReleaseKind.Primary), Is.True);

            PlayerShotPresentationCursor cursor = new PlayerShotPresentationCursor();
            PlayerShotPresentationEvent[] events = new PlayerShotPresentationEvent[2];
            int staleCount = cursor.CopyUnread(feed, events, out bool hasGap);

            Assert.That(staleCount, Is.Zero);
            Assert.That(hasGap, Is.True);
            cursor.ResolveGap(feed);
            Assert.That(cursor.LastSeenSequence, Is.EqualTo(3L));
            Assert.That(cursor.GapCount, Is.EqualTo(1));

            Assert.That(feed.TryRecordCommitted(CreatePrimaryCapture(4), WeaponReleaseKind.Primary), Is.True);
            int freshCount = cursor.CopyUnread(feed, events, out hasGap);

            Assert.That(hasGap, Is.False);
            Assert.That(freshCount, Is.EqualTo(1));
            Assert.That(events[0].Sequence, Is.EqualTo(4L));
            cursor.Commit(events[0]);
            Assert.That(cursor.CopyUnread(feed, events, out hasGap), Is.Zero);
            Assert.That(hasGap, Is.False);
        }

        [Test]
        public void SecondarySnapshotPreservesTheFrozenAreaAnchor()
        {
            PlayerShotQueryCapture capture = CreateSecondaryCapture(5);
            FixedPlayerShotPresentationFeed feed = new FixedPlayerShotPresentationFeed(1);

            Assert.That(feed.TryRecordCommitted(capture, WeaponReleaseKind.Secondary), Is.True);

            PlayerShotPresentationEvent[] events = new PlayerShotPresentationEvent[1];
            Assert.That(feed.CopyEventsAfter(0L, events, out bool hasGap), Is.EqualTo(1));
            Assert.That(hasGap, Is.False);
            Assert.That(events[0].Snapshot.ReleaseKind, Is.EqualTo(WeaponReleaseKind.Secondary));
            Assert.That(events[0].Snapshot.SecondaryAreaCenter,
                Is.EqualTo(new SpatialVectorKey(5000, 1000, 7000)));
            Assert.That(events[0].Snapshot.SecondaryAreaRadiusKey, Is.EqualTo(2500));
            Assert.That(events[0].Snapshot.GetTrajectory(0).SampleIndex, Is.EqualTo(-1));
        }

        private static PlayerShotQueryCapture CreatePrimaryCapture(long identity)
        {
            TickIndex tick = new TickIndex(identity);
            AttackSnapshot attack = new AttackSnapshot(
                new AttackId(identity),
                new ShotId(identity),
                101,
                new RuntimeId(1),
                Team.Player,
                tick,
                new DamageSpec(10, 5),
                QueryPolicy.PelletRays,
                1,
                1,
                1,
                1);
            AttackQueryRequest request = new AttackQueryRequest(
                CreateTickInput(tick, identity),
                attack,
                new[] { new PelletSample(attack.ShotId, 0, 0x7FFFFF, 0x7FFFFF) },
                1);
            PlayerShotQueryCapture capture = new PlayerShotQueryCapture(
                request,
                1,
                SpatialVectorKey.Zero,
                0);
            capture.SetTrajectory(0, new PlayerShotTrajectory(
                0,
                request.TickInput.AimPose.Origin,
                new SpatialVectorKey(1000, 0, 20000),
                PlayerShotTerminalKind.Miss,
                RuntimeId.Invalid,
                HitPart.Body,
                GeometryId.Invalid));
            return capture;
        }

        private static PlayerShotQueryCapture CreateSecondaryCapture(long identity)
        {
            TickIndex tick = new TickIndex(identity);
            AttackSnapshot attack = new AttackSnapshot(
                new AttackId(identity),
                new ShotId(identity),
                101,
                new RuntimeId(1),
                Team.Player,
                tick,
                new DamageSpec(24, 12),
                QueryPolicy.DirectThenArea,
                1,
                4,
                2,
                1);
            AttackQueryRequest request = new AttackQueryRequest(
                CreateTickInput(tick, identity),
                attack,
                null,
                0);
            PlayerShotQueryCapture capture = new PlayerShotQueryCapture(
                request,
                1,
                new SpatialVectorKey(5000, 1000, 7000),
                2500);
            capture.SetTrajectory(0, new PlayerShotTrajectory(
                -1,
                request.TickInput.AimPose.Origin,
                new SpatialVectorKey(5000, 1000, 7000),
                PlayerShotTerminalKind.Miss,
                RuntimeId.Invalid,
                HitPart.Body,
                GeometryId.Invalid));
            return capture;
        }

        private static BattleTickInput CreateTickInput(TickIndex tick, long poseVersion)
        {
            return new BattleTickInput(
                PlayerInputFrame.Empty(tick, aimHeld: true, primaryHeld: true),
                new AimPoseSnapshot(
                    tick,
                    new SpatialVectorKey(1000, 0, 0),
                    new SpatialVectorKey(0, 0, SpatialContract.DirectionUnits),
                    new SpatialVectorKey(SpatialContract.DirectionUnits, 0, 0),
                    new SpatialVectorKey(0, SpatialContract.DirectionUnits, 0),
                    poseVersion));
        }
    }
}
