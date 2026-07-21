using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class CombatDamageTests
    {
        [Test]
        public void WeakpointDamageUsesFrozenMultipliersAndConsumesImpactOnce()
        {
            CombatantState target = new CombatantState(
                new RuntimeId(2L),
                CombatantKind.Enemy,
                100,
                0,
                30);
            DamageResolver resolver = new DamageResolver(new ImpactLedger(8));
            ImpactIntent intent = CreateIntent(
                1L,
                target.RuntimeId,
                new TickIndex(10L),
                new DamageSpec(10, 4, 15000, 25000),
                HitPart.Weakpoint);

            ImpactResolution first = resolver.ResolveCombatant(
                intent,
                target,
                DefenseSnapshot.Exposed,
                true);

            AssertAll(() =>
            {
                Assert.That(first.Result.IsSuccess, Is.True);
                Assert.That(first.Packet.Channel, Is.EqualTo(DamageChannel.Life));
                Assert.That(first.Packet.AppliedAmount, Is.EqualTo(15));
                Assert.That(first.Packet.AppliedBreakAmount, Is.EqualTo(10));
                Assert.That(first.Packet.ValueBefore, Is.EqualTo(100));
                Assert.That(first.Packet.ValueAfter, Is.EqualTo(85));
                Assert.That(target.Life, Is.EqualTo(85));
                Assert.That(target.Break, Is.EqualTo(20));
                Assert.That(first.BreakTriggered, Is.False);
            });

            ImpactResolution duplicate = resolver.ResolveCombatant(
                intent,
                target,
                DefenseSnapshot.Exposed,
                true);

            AssertAll(() =>
            {
                Assert.That(duplicate.Result.IsSuccess, Is.False);
                Assert.That(duplicate.Result.RejectReason, Is.EqualTo(RejectReason.DuplicateImpact));
                Assert.That(target.Life, Is.EqualTo(85));
                Assert.That(target.Break, Is.EqualTo(20));
                Assert.That(resolver.ImpactLedger.Count, Is.EqualTo(1));
            });
        }

        [Test]
        public void WithdrawnPlayerUsesBarrierWithEndExclusivePerfectWindowAndRestoresAtLockBoundary()
        {
            CombatantState player = new CombatantState(
                new RuntimeId(2L),
                CombatantKind.Player,
                100,
                40,
                0);
            DamageResolver resolver = new DamageResolver(new ImpactLedger(8));
            DefenseSnapshot defense = new DefenseSnapshot(
                ExposureMode.Withdrawn,
                new TickIndex(10L),
                new TickDuration(3),
                5000,
                new TickDuration(5),
                5000);

            ImpactResolution insideWindow = resolver.ResolveCombatant(
                CreateIntent(1L, player.RuntimeId, new TickIndex(12L), new DamageSpec(20, 0)),
                player,
                defense,
                false);
            ImpactResolution atEndExclusive = resolver.ResolveCombatant(
                CreateIntent(2L, player.RuntimeId, new TickIndex(13L), new DamageSpec(20, 0)),
                player,
                defense,
                false);
            ImpactResolution barrierBreak = resolver.ResolveCombatant(
                CreateIntent(3L, player.RuntimeId, new TickIndex(14L), new DamageSpec(20, 0)),
                player,
                defense,
                false);

            AssertAll(() =>
            {
                Assert.That(insideWindow.PerfectRetract, Is.True);
                Assert.That(insideWindow.Packet.Channel, Is.EqualTo(DamageChannel.Barrier));
                Assert.That(insideWindow.Packet.AppliedAmount, Is.EqualTo(10));
                Assert.That(atEndExclusive.PerfectRetract, Is.False);
                Assert.That(atEndExclusive.Packet.AppliedAmount, Is.EqualTo(20));
                Assert.That(barrierBreak.BarrierBroken, Is.True);
                Assert.That(barrierBreak.Packet.AppliedAmount, Is.EqualTo(10));
                Assert.That(player.Life, Is.EqualTo(100));
                Assert.That(player.Barrier, Is.Zero);
                Assert.That(player.BarrierLockUntilTick, Is.EqualTo(new TickIndex(19L)));
                Assert.That(player.IsBarrierLocked(new TickIndex(18L)), Is.True);
                Assert.That(player.IsBarrierLocked(new TickIndex(19L)), Is.False);
            });

            Assert.That(player.TryRestoreBarrier(new TickIndex(18L)), Is.False);
            Assert.That(player.TryRestoreBarrier(new TickIndex(19L)), Is.True);
            AssertAll(() =>
            {
                Assert.That(player.Barrier, Is.EqualTo(20));
                Assert.That(player.BarrierLockUntilTick, Is.EqualTo(TickIndex.Invalid));
            });

            ImpactResolution exposedHit = resolver.ResolveCombatant(
                CreateIntent(4L, player.RuntimeId, new TickIndex(19L), new DamageSpec(15, 0)),
                player,
                DefenseSnapshot.Exposed,
                false);

            AssertAll(() =>
            {
                Assert.That(exposedHit.Packet.Channel, Is.EqualTo(DamageChannel.Life));
                Assert.That(player.Life, Is.EqualTo(85));
                Assert.That(player.Barrier, Is.EqualTo(20));
            });
        }

        [Test]
        public void WithdrawnPlayerWithoutBarrierTakesLifeDamage()
        {
            CombatantState player = new CombatantState(
                new RuntimeId(2L),
                CombatantKind.Player,
                100,
                0,
                0);
            DamageResolver resolver = new DamageResolver(new ImpactLedger(4));
            DefenseSnapshot withdrawn = new DefenseSnapshot(
                ExposureMode.Withdrawn,
                new TickIndex(5L),
                new TickDuration(3),
                5000,
                new TickDuration(5),
                5000);

            ImpactResolution hit = resolver.ResolveCombatant(
                CreateIntent(1L, player.RuntimeId, new TickIndex(5L), new DamageSpec(15, 0)),
                player,
                withdrawn,
                false);

            AssertAll(() =>
            {
                Assert.That(hit.Result.IsSuccess, Is.True);
                Assert.That(hit.Packet.Channel, Is.EqualTo(DamageChannel.Life));
                Assert.That(hit.PerfectRetract, Is.False);
                Assert.That(player.Life, Is.EqualTo(85));
                Assert.That(player.Barrier, Is.Zero);
            });
        }

        [Test]
        public void InvalidTargetStillConsumesTheImpactIdBeforeRejection()
        {
            CombatantState actualTarget = new CombatantState(
                new RuntimeId(2L),
                CombatantKind.Enemy,
                100,
                0,
                0);
            DamageResolver resolver = new DamageResolver(new ImpactLedger(4));

            ImpactResolution invalid = resolver.ResolveCombatant(
                CreateIntent(7L, new RuntimeId(999L), new TickIndex(0L), new DamageSpec(10, 0)),
                actualTarget,
                DefenseSnapshot.Exposed,
                false);
            ImpactResolution replayedWithCorrectTarget = resolver.ResolveCombatant(
                CreateIntent(7L, actualTarget.RuntimeId, new TickIndex(0L), new DamageSpec(10, 0)),
                actualTarget,
                DefenseSnapshot.Exposed,
                false);

            AssertAll(() =>
            {
                Assert.That(invalid.Result.RejectReason, Is.EqualTo(RejectReason.InvalidTarget));
                Assert.That(replayedWithCorrectTarget.Result.RejectReason, Is.EqualTo(RejectReason.DuplicateImpact));
                Assert.That(resolver.ImpactLedger.Count, Is.EqualTo(1));
                Assert.That(actualTarget.Life, Is.EqualTo(100));
            });
        }

        [Test]
        public void BreakTriggersOnlyOnTheTransitionToZeroAndCanBeRestoredFully()
        {
            CombatantState enemy = new CombatantState(
                new RuntimeId(2L),
                CombatantKind.Enemy,
                100,
                0,
                5);
            DamageResolver resolver = new DamageResolver(new ImpactLedger(4));

            ImpactResolution transition = resolver.ResolveCombatant(
                CreateIntent(1L, enemy.RuntimeId, new TickIndex(1L), new DamageSpec(0, 8)),
                enemy,
                DefenseSnapshot.Exposed,
                true);
            ImpactResolution alreadyBroken = resolver.ResolveCombatant(
                CreateIntent(2L, enemy.RuntimeId, new TickIndex(2L), new DamageSpec(0, 8)),
                enemy,
                DefenseSnapshot.Exposed,
                true);

            AssertAll(() =>
            {
                Assert.That(transition.BreakTriggered, Is.True);
                Assert.That(transition.Packet.AppliedBreakAmount, Is.EqualTo(5));
                Assert.That(alreadyBroken.BreakTriggered, Is.False);
                Assert.That(alreadyBroken.Packet.AppliedBreakAmount, Is.Zero);
                Assert.That(enemy.Break, Is.Zero);
            });

            enemy.RestoreBreakFull();
            Assert.That(enemy.Break, Is.EqualTo(enemy.MaxBreak));
        }

        private static ImpactIntent CreateIntent(
            long impactId,
            RuntimeId targetId,
            TickIndex tick,
            DamageSpec damage,
            HitPart hitPart = HitPart.Body)
        {
            return new ImpactIntent(
                new ImpactId(impactId),
                new AttackId(1L),
                new ShotId(1L),
                new RuntimeId(1L),
                targetId,
                tick,
                damage,
                hitPart,
                DamageType.Normal,
                CombatTags.Primary);
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }
}
