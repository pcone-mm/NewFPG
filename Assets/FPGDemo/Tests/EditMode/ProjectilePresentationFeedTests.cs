using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class ProjectilePresentationFeedTests
    {
        [Test]
        public void FixedFeedUsesScenarioCapacityAndExplicitRingOverflow()
        {
            FixedProjectilePresentationFeed defaultFeed = new FixedProjectilePresentationFeed(32);
            Assert.That(defaultFeed.ActiveCapacity, Is.EqualTo(32));
            Assert.That(defaultFeed.EventCapacity, Is.EqualTo(128));

            FixedProjectilePresentationFeed feed = new FixedProjectilePresentationFeed(2, 2);
            ProjectileSpawnRequest first = CreateRequest(1, 11, 1);
            ProjectileSpawnRequest second = CreateRequest(2, 12, 2);

            Assert.That(feed.TryRecordSpawn(first, CreatePath(first)), Is.True);
            Assert.That(feed.TryRecordTerminal(new ProjectileReleaseRequest(
                new TickIndex(1),
                first.ProjectileId,
                first.RuntimeId,
                ProjectileTerminalReason.Missed)), Is.True);
            Assert.That(feed.TryRecordSpawn(second, CreatePath(second)), Is.True);

            ProjectilePresentationEvent[] events = new ProjectilePresentationEvent[2];
            int count = feed.CopyEventsAfter(0L, events, out bool hasGap);

            Assert.That(feed.DroppedEventCount, Is.EqualTo(1));
            Assert.That(feed.FirstRetainedSequence, Is.EqualTo(2L));
            Assert.That(feed.LastSequence, Is.EqualTo(3L));
            Assert.That(hasGap, Is.True);
            Assert.That(count, Is.EqualTo(2));
            Assert.That(events[0].Sequence, Is.EqualTo(2L));
            Assert.That(events[0].Type, Is.EqualTo(ProjectilePresentationEventType.Terminal));
            Assert.That(events[1].Sequence, Is.EqualTo(3L));
            Assert.That(events[1].Type, Is.EqualTo(ProjectilePresentationEventType.Spawn));
        }

        [Test]
        public void FixedFeedCopiesActiveStateAndRejectsCapacityWithoutChangingExistingState()
        {
            FixedProjectilePresentationFeed feed = new FixedProjectilePresentationFeed(1, 4);
            ProjectileSpawnRequest first = CreateRequest(1, 11, 1);
            ProjectileSpawnRequest second = CreateRequest(2, 12, 2);
            ProjectilePathSnapshot firstPath = CreatePath(first);

            Assert.That(feed.TryRecordSpawn(first, firstPath), Is.True);
            Assert.That(feed.TryUpdateLastPoint(
                new ProjectileSweepRequest(
                    new TickIndex(1),
                    first.ProjectileId,
                    first.RuntimeId,
                    firstPath.Start,
                    new SpatialVectorKey(0, 0, 1000),
                    first.SweepRadiusKey),
                new SpatialVectorKey(0, 0, 900)), Is.True);
            Assert.That(feed.TryRecordSpawn(second, CreatePath(second)), Is.False);

            ProjectilePresentationState[] states = new ProjectilePresentationState[1];
            int count = feed.CopyActiveStates(states);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(feed.ActiveCount, Is.EqualTo(1));
            Assert.That(feed.RejectedWriteCount, Is.EqualTo(1));
            Assert.That(states[0].Request.RuntimeId, Is.EqualTo(first.RuntimeId));
            Assert.That(states[0].LastPoint, Is.EqualTo(new SpatialVectorKey(0, 0, 900)));
        }

        [Test]
        public void ObservingPortForwardsSuccessfulResultsAndPublishesFrozenLifecycle()
        {
            ScriptedProjectileWorldPort inner = new ScriptedProjectileWorldPort(
                ScriptedProjectileSweepMode.EnvironmentAtFirstSweep);
            FixedProjectilePresentationFeed feed = new FixedProjectilePresentationFeed(1, 4);
            ObservingProjectileWorldPort observing = new ObservingProjectileWorldPort(inner, feed);
            ProjectileSpawnRequest request = CreateRequest(1, 11, 1);

            DomainResult registered = observing.Register(request, out ProjectilePathSnapshot path);
            ProjectileSweepRequest sweep = new ProjectileSweepRequest(
                new TickIndex(1),
                request.ProjectileId,
                request.RuntimeId,
                path.Start,
                path.PositionAtTick(new TickIndex(1)),
                request.SweepRadiusKey);
            DomainResult swept = observing.Sweep(sweep, out ProjectileSweepHit hit);
            DomainResult released = observing.Release(new ProjectileReleaseRequest(
                new TickIndex(1),
                request.ProjectileId,
                request.RuntimeId,
                ProjectileTerminalReason.EnvironmentBlocked));

            ProjectilePresentationEvent[] events = new ProjectilePresentationEvent[4];
            int eventCount = feed.CopyEventsAfter(0L, events, out bool hasGap);

            Assert.That(registered.IsSuccess, Is.True);
            Assert.That(swept.IsSuccess, Is.True);
            Assert.That(released.IsSuccess, Is.True);
            Assert.That(path.Matches(request), Is.True);
            Assert.That(hit.Kind, Is.EqualTo(ProjectileSweepHitKind.EnvironmentBlocked));
            Assert.That(inner.RegisterCount, Is.EqualTo(1));
            Assert.That(inner.SweepCount, Is.EqualTo(1));
            Assert.That(inner.ReleaseCount, Is.EqualTo(1));
            Assert.That(observing.ObservationFaultCount, Is.Zero);
            Assert.That(feed.ActiveCount, Is.Zero);
            Assert.That(hasGap, Is.False);
            Assert.That(eventCount, Is.EqualTo(2));
            Assert.That(events[0].Type, Is.EqualTo(ProjectilePresentationEventType.Spawn));
            Assert.That(events[0].State.Path, Is.EqualTo(path));
            Assert.That(events[1].Type, Is.EqualTo(ProjectilePresentationEventType.Terminal));
            Assert.That(events[1].State.LastPoint, Is.EqualTo(hit.Point));
            Assert.That(events[1].TerminalReason, Is.EqualTo(ProjectileTerminalReason.EnvironmentBlocked));
        }

        [Test]
        public void ObservingPortLeavesFeedUntouchedWhenInnerOperationFails()
        {
            ProjectileSpawnRequest request = CreateRequest(1, 11, 1);
            FixedProjectilePresentationFeed feed = new FixedProjectilePresentationFeed(1, 4);
            ScriptedProjectileWorldPort failedRegister = new ScriptedProjectileWorldPort
            {
                FailRegisterCall = 1
            };
            ObservingProjectileWorldPort observing = new ObservingProjectileWorldPort(failedRegister, feed);

            Assert.That(observing.Register(request, out ProjectilePathSnapshot rejectedPath).RejectReason,
                Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(rejectedPath, Is.EqualTo(default(ProjectilePathSnapshot)));
            Assert.That(feed.ActiveCount, Is.Zero);
            Assert.That(feed.LastSequence, Is.Zero);

            ScriptedProjectileWorldPort failedRelease = new ScriptedProjectileWorldPort
            {
                FailReleaseCall = 1
            };
            FixedProjectilePresentationFeed releaseFeed = new FixedProjectilePresentationFeed(1, 4);
            ObservingProjectileWorldPort releaseObserver = new ObservingProjectileWorldPort(
                failedRelease,
                releaseFeed);
            Assert.That(releaseObserver.Register(request, out ProjectilePathSnapshot path).IsSuccess, Is.True);
            Assert.That(releaseObserver.Release(new ProjectileReleaseRequest(
                new TickIndex(1),
                request.ProjectileId,
                request.RuntimeId,
                ProjectileTerminalReason.OwnerCanceled)).RejectReason,
                Is.EqualTo(RejectReason.InvalidState));
            Assert.That(releaseFeed.ActiveCount, Is.EqualTo(1));
            Assert.That(releaseFeed.LastSequence, Is.EqualTo(1L));
            Assert.That(path.Matches(request), Is.True);
        }

        [Test]
        public void ObserverFaultsNeverChangeInnerWorldResult()
        {
            ScriptedProjectileWorldPort inner = new ScriptedProjectileWorldPort(
                ScriptedProjectileSweepMode.None);
            ObservingProjectileWorldPort observing = new ObservingProjectileWorldPort(
                inner,
                new ThrowingPresentationFeed());
            ProjectileSpawnRequest request = CreateRequest(1, 11, 1);

            DomainResult registered = observing.Register(request, out ProjectilePathSnapshot path);
            DomainResult swept = observing.Sweep(new ProjectileSweepRequest(
                new TickIndex(1),
                request.ProjectileId,
                request.RuntimeId,
                path.Start,
                path.PositionAtTick(new TickIndex(1)),
                request.SweepRadiusKey), out ProjectileSweepHit hit);
            DomainResult released = observing.Release(new ProjectileReleaseRequest(
                new TickIndex(1),
                request.ProjectileId,
                request.RuntimeId,
                ProjectileTerminalReason.OwnerCanceled));

            Assert.That(registered.IsSuccess, Is.True);
            Assert.That(swept.IsSuccess, Is.True);
            Assert.That(released.IsSuccess, Is.True);
            Assert.That(path.Matches(request), Is.True);
            Assert.That(hit.Kind, Is.EqualTo(ProjectileSweepHitKind.None));
            Assert.That(observing.ObservationFaultCount, Is.EqualTo(3));
            Assert.That(inner.RegisterCount, Is.EqualTo(1));
            Assert.That(inner.SweepCount, Is.EqualTo(1));
            Assert.That(inner.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void ObserverDoesNotChangeBattleSummaryOrSpatialDigest()
        {
            ReplaySummary direct = RunProjectileSequence(false);
            ReplaySummary observed = RunProjectileSequence(true);

            Assert.That(observed.CanonicalDigest, Is.EqualTo(direct.CanonicalDigest));
            Assert.That(observed.SpatialDecisionDigest, Is.EqualTo(direct.SpatialDecisionDigest));
            Assert.That(observed.FinalSnapshot.PlayerLife, Is.EqualTo(direct.FinalSnapshot.PlayerLife));
            Assert.That(observed.FinalSnapshot.PlayerBarrier, Is.EqualTo(direct.FinalSnapshot.PlayerBarrier));
            Assert.That(observed.FinalSnapshot.ActiveProjectileUnits, Is.EqualTo(direct.FinalSnapshot.ActiveProjectileUnits));
        }

        private static ReplaySummary RunProjectileSequence(bool useObserver)
        {
            ScenarioDefinition scenario = CombatLabHarness.CreateScenario(
                projectileBudgetCapacity: 1,
                projectileCapacity: 1);
            ScriptedProjectileWorldPort inner = CombatLabHarness.CreateProjectileWorldPort(
                ScriptedProjectileSweepMode.None);
            IProjectileWorldPort world = useObserver
                ? new ObservingProjectileWorldPort(
                    inner,
                    new FixedProjectilePresentationFeed(scenario.ProjectileCapacity))
                : inner;
            using (BattleSession session = new BattleSessionFactory().Create(
                scenario,
                new NullAttackResolutionPort(),
                null,
                world))
            {
                Assert.That(session.ApplyControl(new SessionControlCommand(
                    new ControlSequence(1),
                    SessionControlCommandType.Start)).IsSuccess, Is.True);
                ThreatDefinition definition = CombatLabHarness.CreateThreatDefinition(
                    payloadCount: 1,
                    telegraphTicks: 0,
                    windupTicks: 0,
                    recoveryTicks: 0,
                    flightTicks: 2,
                    interceptable: true);
                Assert.That(session.TryAddThreat(definition, out int threatIndex).IsSuccess, Is.True);
                Assert.That(session.TryStartThreat(threatIndex).IsSuccess, Is.True);
                CombatLabHarness.PumpTicks(session, 3);
                return session.GetReplaySummary();
            }
        }

        private static ProjectileSpawnRequest CreateRequest(int projectileValue, long runtimeValue, int attackValue)
        {
            return new ProjectileSpawnRequest(
                new TickIndex(0),
                new TickIndex(3),
                new ProjectileId(projectileValue),
                new RuntimeId(runtimeValue),
                new AttackId(attackValue),
                new RuntimeId(2),
                new RuntimeId(1),
                Team.Enemy,
                301,
                1,
                1,
                true);
        }

        private static ProjectilePathSnapshot CreatePath(in ProjectileSpawnRequest request)
        {
            return new ProjectilePathSnapshot(
                request.ProjectileId,
                request.RuntimeId,
                request.Tick,
                request.ArrivalTick,
                SpatialVectorKey.Zero,
                new SpatialVectorKey(0, 0, 3000));
        }

        private sealed class ThrowingPresentationFeed : IProjectilePresentationFeedWriter
        {
            public int ActiveCapacity => 1;
            public int ActiveCount => 0;
            public int EventCapacity => 1;
            public int DroppedEventCount => 0;
            public long FirstRetainedSequence => 1L;
            public long LastSequence => 0L;

            public bool TryRecordSpawn(in ProjectileSpawnRequest request, in ProjectilePathSnapshot path)
            {
                throw new InvalidOperationException("Test observation fault.");
            }

            public bool TryUpdateLastPoint(in ProjectileSweepRequest request, SpatialVectorKey point)
            {
                throw new InvalidOperationException("Test observation fault.");
            }

            public bool TryRecordTerminal(in ProjectileReleaseRequest request)
            {
                throw new InvalidOperationException("Test observation fault.");
            }

            public int CopyActiveStates(ProjectilePresentationState[] output) => 0;

            public int CopyEventsAfter(
                long lastSeenSequence,
                ProjectilePresentationEvent[] output,
                out bool hasGap)
            {
                hasGap = false;
                return 0;
            }
        }
    }
}
