using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FormalCombatPresentationStreamTests
    {
        [Test]
        public void VitalsChangesAreOrderedAndGapResyncUsesLatestSnapshot()
        {
            CombatantState combatant = new CombatantState(
                new RuntimeId(2L),
                CombatantKind.Enemy,
                100,
                0,
                0);
            FixedFpgVitalsStream stream = new FixedFpgVitalsStream(2, 2);

            Assert.That(
                stream.TryPublish(
                    combatant,
                    new TickIndex(0L),
                    FpgVitalsChangeReason.Spawn,
                    force: true),
                Is.True);

            DamageResolver resolver = new DamageResolver(new ImpactLedger(4));
            ImpactResolution damage = resolver.ResolveCombatant(
                CreateIntent(1L, combatant.RuntimeId, new TickIndex(1L), 20),
                combatant,
                DefenseSnapshot.Exposed,
                false);
            Assert.That(damage.Result.IsSuccess, Is.True);
            Assert.That(
                stream.TryPublish(
                    combatant,
                    new TickIndex(1L),
                    FpgVitalsChangeReason.Damage),
                Is.True);

            combatant.ForceDeath();
            Assert.That(
                stream.TryPublish(
                    combatant,
                    new TickIndex(2L),
                    FpgVitalsChangeReason.Death),
                Is.True);

            FpgVitalsSnapshot[] changes = new FpgVitalsSnapshot[2];
            int count = stream.CopyChangesAfter(0L, changes, out bool hasGap);

            AssertAll(() =>
            {
                Assert.That(count, Is.EqualTo(2));
                Assert.That(hasGap, Is.True);
                Assert.That(changes[0].Sequence, Is.EqualTo(2L));
                Assert.That(changes[1].Sequence, Is.EqualTo(3L));
                Assert.That(changes[1].Revision, Is.EqualTo(3L));
                Assert.That(stream.DroppedEventCount, Is.EqualTo(1));
            });

            Assert.That(
                stream.TryGetLatest(combatant.RuntimeId, out FpgVitalsSnapshot latest),
                Is.True);
            AssertAll(() =>
            {
                Assert.That(latest.Sequence, Is.EqualTo(3L));
                Assert.That(latest.Life, Is.Zero);
                Assert.That(latest.Dead, Is.True);
                Assert.That(latest.Reason, Is.EqualTo(FpgVitalsChangeReason.Death));
            });
        }

        [Test]
        public void VitalsPublishesBarrierRestoreAndRestartAsCompleteSnapshots()
        {
            CombatantState player = new CombatantState(
                new RuntimeId(1L),
                CombatantKind.Player,
                100,
                40,
                0);
            FixedFpgVitalsStream stream = new FixedFpgVitalsStream(1, 8);
            Assert.That(
                stream.TryPublish(
                    player,
                    new TickIndex(0L),
                    FpgVitalsChangeReason.Spawn,
                    force: true),
                Is.True);

            DamageResolver resolver = new DamageResolver(new ImpactLedger(4));
            DefenseSnapshot withdrawn = new DefenseSnapshot(
                ExposureMode.Withdrawn,
                new TickIndex(0L),
                new TickDuration(0),
                DamageSpec.BasisPoints,
                new TickDuration(2),
                5000);
            ImpactResolution damage = resolver.ResolveCombatant(
                CreateIntent(
                    1L,
                    player.RuntimeId,
                    new TickIndex(1L),
                    50),
                player,
                withdrawn,
                false);
            AssertAll(() =>
            {
                Assert.That(damage.Result.IsSuccess, Is.True);
                Assert.That(damage.Packet.Channel, Is.EqualTo(DamageChannel.Barrier));
                Assert.That(player.Life, Is.EqualTo(100));
                Assert.That(player.Barrier, Is.Zero);
            });
            Assert.That(
                stream.TryPublish(
                    player,
                    new TickIndex(1L),
                    FpgVitalsChangeReason.Damage),
                Is.True);

            Assert.That(player.TryRestoreBarrier(new TickIndex(3L)), Is.True);
            Assert.That(player.Barrier, Is.EqualTo(20));
            Assert.That(
                stream.TryPublish(
                    player,
                    new TickIndex(3L),
                    FpgVitalsChangeReason.BarrierRestore),
                Is.True);
            Assert.That(
                stream.TryGetLatest(
                    player.RuntimeId,
                    out FpgVitalsSnapshot restored),
                Is.True);
            AssertAll(() =>
            {
                Assert.That(restored.Sequence, Is.EqualTo(3L));
                Assert.That(restored.Revision, Is.EqualTo(3L));
                Assert.That(restored.Life, Is.EqualTo(100));
                Assert.That(restored.Barrier, Is.EqualTo(20));
                Assert.That(
                    restored.Reason,
                    Is.EqualTo(FpgVitalsChangeReason.BarrierRestore));
            });

            CombatantResourceSnapshot fullResources =
                new CombatantResourceSnapshot(
                    player.RuntimeId,
                    player.MaxLife,
                    player.MaxBarrier,
                    player.MaxBreak,
                    TickIndex.Invalid,
                    DamageSpec.BasisPoints);
            Assert.That(player.RestoreResources(fullResources).IsSuccess, Is.True);
            stream.Clear();
            Assert.That(
                stream.TryPublish(
                    player,
                    new TickIndex(0L),
                    FpgVitalsChangeReason.Restart,
                    force: true),
                Is.True);
            Assert.That(
                stream.TryGetLatest(
                    player.RuntimeId,
                    out FpgVitalsSnapshot restarted),
                Is.True);
            AssertAll(() =>
            {
                Assert.That(restarted.Sequence, Is.EqualTo(1L));
                Assert.That(restarted.Revision, Is.EqualTo(1L));
                Assert.That(restarted.Life, Is.EqualTo(100));
                Assert.That(restarted.Barrier, Is.EqualTo(40));
                Assert.That(restarted.Dead, Is.False);
                Assert.That(
                    restarted.Reason,
                    Is.EqualTo(FpgVitalsChangeReason.Restart));
            });
        }

        [Test]
        public void DamageFeedbackKeepsEveryImpactAndItsSpatialContext()
        {
            CombatantState target = new CombatantState(
                new RuntimeId(2L),
                CombatantKind.Enemy,
                100,
                0,
                0);
            DamageResolver resolver = new DamageResolver(new ImpactLedger(4));
            FixedResolvedDamageFeedbackStream stream =
                new FixedResolvedDamageFeedbackStream(4);
            ImpactIntent body = CreateIntent(
                1L,
                target.RuntimeId,
                new TickIndex(1L),
                10,
                HitPart.Body,
                new ImpactSpatialContext(
                    new SpatialVectorKey(100, 200, 300),
                    new GeometryId(10),
                    QueryTargetKind.Combatant,
                    HitPart.Body));
            ImpactIntent weakpoint = CreateIntent(
                2L,
                target.RuntimeId,
                new TickIndex(1L),
                10,
                HitPart.Weakpoint,
                new ImpactSpatialContext(
                    new SpatialVectorKey(110, 210, 310),
                    new GeometryId(11),
                    QueryTargetKind.Combatant,
                    HitPart.Weakpoint));

            ImpactResolution first = resolver.ResolveCombatant(
                body,
                target,
                DefenseSnapshot.Exposed,
                false);
            ImpactResolution second = resolver.ResolveCombatant(
                weakpoint,
                target,
                DefenseSnapshot.Exposed,
                false);

            Assert.That(stream.TryRecord(body, first), Is.True);
            Assert.That(stream.TryRecord(weakpoint, second), Is.True);
            FpgResolvedDamageFeedback[] output = new FpgResolvedDamageFeedback[4];
            int count = stream.CopyAfter(0L, output, out bool hasGap);

            AssertAll(() =>
            {
                Assert.That(count, Is.EqualTo(2));
                Assert.That(hasGap, Is.False);
                Assert.That(output[0].ImpactId, Is.EqualTo(body.ImpactId));
                Assert.That(output[1].ImpactId, Is.EqualTo(weakpoint.ImpactId));
                Assert.That(output[0].AppliedDamage, Is.EqualTo(10));
                Assert.That(output[1].AppliedDamage, Is.EqualTo(10));
                Assert.That(output[0].SpatialContext.ImpactPointKey,
                    Is.EqualTo(new SpatialVectorKey(100, 200, 300)));
                Assert.That(output[1].SpatialContext.GeometryId,
                    Is.EqualTo(new GeometryId(11)));
                Assert.That(output[0].Tags, Is.EqualTo(CombatTags.Primary));
                Assert.That(output[1].Tags, Is.EqualTo(CombatTags.Primary));
                Assert.That(output[1].IsWeakpoint, Is.True);
            });
        }

        [Test]
        public void FeedbackOverflowCreatesGapWithoutRejectingNewCombatResults()
        {
            CombatantState target = new CombatantState(
                new RuntimeId(2L),
                CombatantKind.Enemy,
                100,
                0,
                0);
            DamageResolver resolver = new DamageResolver(new ImpactLedger(4));
            FixedResolvedDamageFeedbackStream stream =
                new FixedResolvedDamageFeedbackStream(1);
            ImpactIntent firstIntent = CreateIntent(
                1L,
                target.RuntimeId,
                new TickIndex(1L),
                5);
            ImpactIntent secondIntent = CreateIntent(
                2L,
                target.RuntimeId,
                new TickIndex(2L),
                7);
            ImpactResolution first = resolver.ResolveCombatant(
                firstIntent,
                target,
                DefenseSnapshot.Exposed,
                false);
            ImpactResolution second = resolver.ResolveCombatant(
                secondIntent,
                target,
                DefenseSnapshot.Exposed,
                false);

            Assert.That(stream.TryRecord(firstIntent, first), Is.True);
            Assert.That(stream.TryRecord(secondIntent, second), Is.True);
            FpgResolvedDamageFeedback[] output = new FpgResolvedDamageFeedback[1];
            int count = stream.CopyAfter(0L, output, out bool hasGap);

            AssertAll(() =>
            {
                Assert.That(target.Life, Is.EqualTo(88));
                Assert.That(count, Is.EqualTo(1));
                Assert.That(hasGap, Is.True);
                Assert.That(output[0].ImpactId, Is.EqualTo(secondIntent.ImpactId));
                Assert.That(stream.DroppedEventCount, Is.EqualTo(1));
            });
        }

        private static ImpactIntent CreateIntent(
            long impactId,
            RuntimeId targetId,
            TickIndex tick,
            int damage,
            HitPart hitPart = HitPart.Body,
            ImpactSpatialContext spatialContext = default(ImpactSpatialContext))
        {
            return new ImpactIntent(
                new ImpactId(impactId),
                new AttackId(1L),
                new ShotId(1L),
                new RuntimeId(1L),
                targetId,
                tick,
                new DamageSpec(damage, 0),
                hitPart,
                DamageType.Normal,
                CombatTags.Primary,
                impactOrdinal: (int)impactId - 1,
                spatialContext: spatialContext);
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }
}
