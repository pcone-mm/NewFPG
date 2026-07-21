using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class PlayerShotVisualAggregationTests
    {
        [Test]
        public void PrimaryRepresentativeUsesPriorityThenLowerFrozenSampleIndex()
        {
            PlayerShotPresentationSnapshot snapshot = CreatePrimarySnapshot(
                new TrajectorySpec(PlayerShotTerminalKind.Combatant, HitPart.Body),
                new TrajectorySpec(PlayerShotTerminalKind.Projectile, HitPart.Projectile),
                new TrajectorySpec(PlayerShotTerminalKind.Combatant, HitPart.Weakpoint),
                new TrajectorySpec(PlayerShotTerminalKind.Combatant, HitPart.Weakpoint),
                new TrajectorySpec(PlayerShotTerminalKind.EnvironmentBlocker, HitPart.Body),
                new TrajectorySpec(PlayerShotTerminalKind.Miss, HitPart.Body));

            Assert.That(
                PlayerShotVisualAggregation.TryGetPrimaryRepresentative(
                    snapshot,
                    out PlayerShotTrajectory selected),
                Is.True);
            Assert.That(selected.SampleIndex, Is.EqualTo(2));
            Assert.That(selected.TerminalKind, Is.EqualTo(PlayerShotTerminalKind.Combatant));
            Assert.That(selected.HitPart, Is.EqualTo(HitPart.Weakpoint));
            Assert.That(selected.TerminalPoint, Is.EqualTo(new SpatialVectorKey(3200, 700, 5800)));
        }

        [Test]
        public void PrimaryRepresentativeLeavesTheFrozenTrajectoriesUnchanged()
        {
            PlayerShotPresentationSnapshot snapshot = CreatePrimarySnapshot(
                new TrajectorySpec(PlayerShotTerminalKind.Miss, HitPart.Body),
                new TrajectorySpec(PlayerShotTerminalKind.Projectile, HitPart.Projectile));
            PlayerShotTrajectory before0 = snapshot.GetTrajectory(0);
            PlayerShotTrajectory before1 = snapshot.GetTrajectory(1);

            Assert.That(
                PlayerShotVisualAggregation.TryGetPrimaryRepresentative(
                    snapshot,
                    out PlayerShotTrajectory selected),
                Is.True);

            Assert.That(selected.SampleIndex, Is.EqualTo(1));
            AssertTrajectory(snapshot.GetTrajectory(0), before0);
            AssertTrajectory(snapshot.GetTrajectory(1), before1);
        }

        [Test]
        public void PrimaryRepresentativeRejectsSecondarySnapshots()
        {
            PlayerShotPresentationSnapshot snapshot = CreateSecondarySnapshot(
                PlayerShotTerminalKind.Combatant,
                HitPart.Body);

            Assert.That(
                PlayerShotVisualAggregation.TryGetPrimaryRepresentative(
                    snapshot,
                    out PlayerShotTrajectory selected),
                Is.False);
            Assert.That(selected.SampleIndex, Is.EqualTo(0));
        }

        [TestCase(PlayerShotTerminalKind.Combatant, HitPart.Body)]
        [TestCase(PlayerShotTerminalKind.Projectile, HitPart.Projectile)]
        public void SecondaryBurstAnchorUsesFrozenDirectTargetTerminal(
            PlayerShotTerminalKind terminalKind,
            HitPart hitPart)
        {
            PlayerShotPresentationSnapshot snapshot = CreateSecondarySnapshot(terminalKind, hitPart);
            PlayerShotTrajectory directTrajectory = snapshot.GetTrajectory(0);

            Assert.That(
                PlayerShotVisualAggregation.TryGetSecondaryBurstAnchor(snapshot, out SpatialVectorKey anchor),
                Is.True);
            Assert.That(anchor, Is.EqualTo(directTrajectory.TerminalPoint));
            Assert.That(anchor, Is.Not.EqualTo(snapshot.SecondaryAreaCenter));
        }

        [TestCase(PlayerShotTerminalKind.Miss)]
        [TestCase(PlayerShotTerminalKind.EnvironmentBlocker)]
        public void SecondaryBurstAnchorFallsBackToCommittedAreaCenter(
            PlayerShotTerminalKind terminalKind)
        {
            PlayerShotPresentationSnapshot snapshot = CreateSecondarySnapshot(terminalKind, HitPart.Body);

            Assert.That(
                PlayerShotVisualAggregation.TryGetSecondaryBurstAnchor(snapshot, out SpatialVectorKey anchor),
                Is.True);
            Assert.That(anchor, Is.EqualTo(snapshot.SecondaryAreaCenter));
        }

        [Test]
        public void SecondaryBurstAnchorRejectsPrimarySnapshots()
        {
            PlayerShotPresentationSnapshot snapshot = CreatePrimarySnapshot(
                new TrajectorySpec(PlayerShotTerminalKind.Miss, HitPart.Body));

            Assert.That(
                PlayerShotVisualAggregation.TryGetSecondaryBurstAnchor(snapshot, out SpatialVectorKey anchor),
                Is.False);
            Assert.That(anchor, Is.EqualTo(SpatialVectorKey.Zero));
        }

        private static PlayerShotPresentationSnapshot CreatePrimarySnapshot(
            params TrajectorySpec[] trajectories)
        {
            Assert.That(trajectories, Is.Not.Null);
            Assert.That(trajectories.Length, Is.InRange(1, AttackQueryRequest.MaxPelletCount));

            long identity = 100L + trajectories.Length;
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
                trajectories.Length,
                trajectories.Length,
                1,
                1);
            PelletSample[] samples = new PelletSample[trajectories.Length];
            for (int index = 0; index < samples.Length; index++)
            {
                samples[index] = new PelletSample(attack.ShotId, index, 0x7FFFFF, 0x7FFFFF);
            }

            AttackQueryRequest request = new AttackQueryRequest(
                CreateTickInput(tick, identity),
                attack,
                samples,
                samples.Length);
            PlayerShotQueryCapture capture = new PlayerShotQueryCapture(
                request,
                trajectories.Length,
                SpatialVectorKey.Zero,
                0);
            for (int index = 0; index < trajectories.Length; index++)
            {
                capture.SetTrajectory(
                    index,
                    CreateTrajectory(request.TickInput.AimPose.Origin, index, trajectories[index]));
            }

            return Commit(capture, WeaponReleaseKind.Primary);
        }

        private static PlayerShotPresentationSnapshot CreateSecondarySnapshot(
            PlayerShotTerminalKind terminalKind,
            HitPart hitPart)
        {
            const long Identity = 200L;
            SpatialVectorKey secondaryAreaCenter = new SpatialVectorKey(9000, 1200, 11000);
            TickIndex tick = new TickIndex(Identity);
            AttackSnapshot attack = new AttackSnapshot(
                new AttackId(Identity),
                new ShotId(Identity),
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
                CreateTickInput(tick, Identity),
                attack,
                null,
                0);
            PlayerShotQueryCapture capture = new PlayerShotQueryCapture(
                request,
                1,
                secondaryAreaCenter,
                2500);
            capture.SetTrajectory(
                0,
                CreateTrajectory(request.TickInput.AimPose.Origin, -1, new TrajectorySpec(terminalKind, hitPart)));

            return Commit(capture, WeaponReleaseKind.Secondary);
        }

        private static PlayerShotPresentationSnapshot Commit(
            in PlayerShotQueryCapture capture,
            WeaponReleaseKind releaseKind)
        {
            FixedPlayerShotPresentationFeed feed = new FixedPlayerShotPresentationFeed(1);
            Assert.That(feed.TryRecordCommitted(capture, releaseKind), Is.True);

            PlayerShotPresentationEvent[] events = new PlayerShotPresentationEvent[1];
            Assert.That(feed.CopyEventsAfter(0L, events, out bool hasGap), Is.EqualTo(1));
            Assert.That(hasGap, Is.False);
            return events[0].Snapshot;
        }

        private static PlayerShotTrajectory CreateTrajectory(
            SpatialVectorKey start,
            int sampleIndex,
            TrajectorySpec specification)
        {
            SpatialVectorKey terminalPoint = sampleIndex < 0
                ? new SpatialVectorKey(6400, 800, 9200)
                : new SpatialVectorKey(
                    1200 + sampleIndex * 1000,
                    500 + sampleIndex * 100,
                    4000 + sampleIndex * 900);
            RuntimeId targetId = RuntimeId.Invalid;
            GeometryId geometryId = GeometryId.Invalid;
            HitPart hitPart = specification.HitPart;

            switch (specification.TerminalKind)
            {
                case PlayerShotTerminalKind.Combatant:
                    targetId = new RuntimeId(100 + sampleIndex + 1);
                    geometryId = new GeometryId(1000 + sampleIndex + 1);
                    break;
                case PlayerShotTerminalKind.Projectile:
                    targetId = new RuntimeId(100 + sampleIndex + 1);
                    geometryId = new GeometryId(1000 + sampleIndex + 1);
                    hitPart = HitPart.Projectile;
                    break;
                case PlayerShotTerminalKind.EnvironmentBlocker:
                    geometryId = new GeometryId(1000 + sampleIndex + 1);
                    hitPart = HitPart.Body;
                    break;
                default:
                    hitPart = HitPart.Body;
                    break;
            }

            return new PlayerShotTrajectory(
                sampleIndex,
                start,
                terminalPoint,
                specification.TerminalKind,
                targetId,
                hitPart,
                geometryId);
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

        private static void AssertTrajectory(PlayerShotTrajectory actual, PlayerShotTrajectory expected)
        {
            Assert.That(actual.SampleIndex, Is.EqualTo(expected.SampleIndex));
            Assert.That(actual.Start, Is.EqualTo(expected.Start));
            Assert.That(actual.TerminalPoint, Is.EqualTo(expected.TerminalPoint));
            Assert.That(actual.TerminalKind, Is.EqualTo(expected.TerminalKind));
            Assert.That(actual.TargetId, Is.EqualTo(expected.TargetId));
            Assert.That(actual.HitPart, Is.EqualTo(expected.HitPart));
            Assert.That(actual.GeometryId, Is.EqualTo(expected.GeometryId));
        }

        private readonly struct TrajectorySpec
        {
            public TrajectorySpec(PlayerShotTerminalKind terminalKind, HitPart hitPart)
            {
                TerminalKind = terminalKind;
                HitPart = hitPart;
            }

            public PlayerShotTerminalKind TerminalKind { get; }
            public HitPart HitPart { get; }
        }
    }
}
