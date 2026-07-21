using System;
using System.Collections.Generic;
using FPG.Demo.Core;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class CoreDeterminismTests
    {
        [Test]
        public void TickDurationRoundsPositiveSecondsUp()
        {
            Assert.That(TickDuration.FromSeconds(0d).Value, Is.Zero);
            Assert.That(TickDuration.FromSeconds(0.001d).Value, Is.EqualTo(1));
            Assert.That(TickDuration.FromSeconds(0.25d).Value, Is.EqualTo(15));
            Assert.That(TickDuration.FromSeconds(0.4d).Value, Is.EqualTo(24));
        }

        [Test]
        public void StableHashUsesTheFrozenGoldenVector()
        {
            ulong sample = DeterministicRandomV1.SampleUInt64(
                123UL,
                RandomDomain.PelletSpread,
                7UL,
                3UL);

            Assert.That(sample, Is.EqualTo(0xB0E3C4D72C5ADBF1UL));
            Assert.That(DeterministicRandomV1.Version, Is.EqualTo(1));
        }

        [Test]
        public void AttackAndShotIdsAdvanceOnlyAfterCommit()
        {
            SessionIdAllocator allocator = new SessionIdAllocator();

            AttackShotReservation first = allocator.ReserveAttackAndShot();
            AttackShotReservation repeated = allocator.ReserveAttackAndShot();

            Assert.That(repeated.AttackId, Is.EqualTo(first.AttackId));
            Assert.That(repeated.ShotId, Is.EqualTo(first.ShotId));
            Assert.That(allocator.Commit(first), Is.True);

            AttackShotReservation second = allocator.ReserveAttackAndShot();
            Assert.That(second.AttackId.Value, Is.EqualTo(first.AttackId.Value + 1L));
            Assert.That(second.ShotId.Value, Is.EqualTo(first.ShotId.Value + 1L));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void GameplayClockExecutesSixtyTicksForOneSecondOfSplitWallTime(int frameRate)
        {
            GameplayClock clock = new GameplayClock();
            List<long> executedTicks = new List<long>();

            long baseDelta = TimeSpan.TicksPerSecond / frameRate;
            long remainder = TimeSpan.TicksPerSecond % frameRate;

            for (int frame = 0; frame < frameRate; frame++)
            {
                long delta = baseDelta + (frame < remainder ? 1L : 0L);
                DomainResult pump = clock.BeginPump(delta, out ClockPumpResult result);
                Assert.That(pump.IsSuccess, Is.True);

                while (clock.TryConsumeStep(out TickIndex tick))
                {
                    executedTicks.Add(tick.Value);
                }
            }

            Assert.That(executedTicks.Count, Is.EqualTo(60));
            Assert.That(executedTicks[0], Is.Zero);
            Assert.That(executedTicks[59], Is.EqualTo(59));
            Assert.That(clock.AccumulatorUnits, Is.Zero);
            Assert.That(clock.Diagnostics.DroppedAccumulatorUnits, Is.Zero);
        }

        [Test]
        public void GameplayClockCapsCatchUpAndRetainsRemainingDebt()
        {
            GameplayClock clock = new GameplayClock();

            DomainResult pump = clock.BeginPump(TimeSpan.TicksPerSecond / 10L, out ClockPumpResult first);
            Assert.That(pump.IsSuccess, Is.True);
            Assert.That(first.StepsAvailable, Is.EqualTo(4));

            int firstSteps = ConsumeAll(clock);
            Assert.That(firstSteps, Is.EqualTo(4));
            Assert.That(clock.PendingDebtTicks, Is.EqualTo(2));

            pump = clock.BeginPump(0L, out ClockPumpResult second);
            Assert.That(pump.IsSuccess, Is.True);
            Assert.That(second.StepsAvailable, Is.EqualTo(2));
            Assert.That(ConsumeAll(clock), Is.EqualTo(2));
        }

        [Test]
        public void PauseIgnoresElapsedWallTimeAndPreservesRemainder()
        {
            GameplayClock clock = new GameplayClock();
            long halfTick = TimeSpan.TicksPerSecond / (GameplayClock.DefaultTickRate * 2L);

            clock.BeginPump(halfTick, out ClockPumpResult beforePause);
            Assert.That(beforePause.StepsAvailable, Is.Zero);
            long remainder = clock.AccumulatorUnits;

            clock.SetPaused(true);
            clock.BeginPump(TimeSpan.TicksPerSecond * 10L, out ClockPumpResult paused);
            Assert.That(paused.StepsAvailable, Is.Zero);
            Assert.That(clock.AccumulatorUnits, Is.EqualTo(remainder));

            clock.SetPaused(false);
            clock.BeginPump(halfTick + 1L, out ClockPumpResult resumed);
            Assert.That(resumed.StepsAvailable, Is.EqualTo(1));
            Assert.That(ConsumeAll(clock), Is.EqualTo(1));
        }

        [Test]
        public void DebtClampDropsWallTimeWithoutSkippingGameplayTickIds()
        {
            GameplayClock clock = new GameplayClock();

            clock.BeginPump(TimeSpan.TicksPerSecond, out ClockPumpResult result);
            Assert.That(result.StepsAvailable, Is.EqualTo(GameplayClock.DefaultMaxCatchUpSteps));
            Assert.That(result.DroppedAccumulatorUnits, Is.GreaterThan(0L));

            List<long> ticks = new List<long>();
            do
            {
                while (clock.TryConsumeStep(out TickIndex tick))
                {
                    ticks.Add(tick.Value);
                }

                clock.BeginPump(0L, out result);
            }
            while (result.StepsAvailable > 0);

            Assert.That(ticks.Count, Is.EqualTo(GameplayClock.DefaultMaxDebtTicks));
            for (int index = 0; index < ticks.Count; index++)
            {
                Assert.That(ticks[index], Is.EqualTo(index));
            }
        }

        [Test]
        public void RolledBackStepReturnsItsDebtAndTickIdForALaterRetry()
        {
            GameplayClock clock = new GameplayClock();
            long oneTick = (TimeSpan.TicksPerSecond + GameplayClock.DefaultTickRate - 1L)
                / GameplayClock.DefaultTickRate;

            Assert.That(clock.BeginPump(oneTick, out ClockPumpResult initial).IsSuccess, Is.True);
            Assert.That(initial.StepsAvailable, Is.EqualTo(1));
            Assert.That(clock.TryPeekStep(out TickIndex tick), Is.True);
            Assert.That(tick, Is.EqualTo(new TickIndex(0L)));
            Assert.That(clock.CommitStep(tick).IsSuccess, Is.True);
            Assert.That(clock.RollbackStep(tick).IsSuccess, Is.True);
            clock.AbortPump();

            Assert.That(clock.ExecutedTickCount, Is.Zero);
            Assert.That(clock.CurrentTick, Is.EqualTo(TickIndex.Invalid));
            Assert.That(clock.PendingDebtTicks, Is.EqualTo(1));
            Assert.That(clock.BeginPump(0L, out ClockPumpResult retried).IsSuccess, Is.True);
            Assert.That(retried.StepsAvailable, Is.EqualTo(1));
            Assert.That(clock.TryConsumeStep(out TickIndex retriedTick), Is.True);
            Assert.That(retriedTick, Is.EqualTo(tick));
        }

        private static int ConsumeAll(GameplayClock clock)
        {
            int count = 0;
            while (clock.TryConsumeStep(out TickIndex ignored))
            {
                count++;
            }

            return count;
        }
    }
}
