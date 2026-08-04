using FPG.Demo.Core;
using FPG.Demo.Skills;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    [Category("AttackTiming")]
    public sealed class FpgAttackTimingTests
    {
        [Test]
        public void FeiBaselineResolvesZeroWindupAndFortyOneTickPeriod()
        {
            FpgCompiledSkillSequence sequence = CreateSequence(
                durationTicks: 40,
                AttackEvent(10, 0));

            Assert.That(
                FpgAttackTimingResolver.TryResolve(
                    sequence,
                    CharacterTiming(1d, 40, 0),
                    12,
                    new FpgAttackSpeedProfile(60d / 41d, 1d, 2.5d),
                    0d,
                    new TickIndex(100L),
                    out FpgResolvedSkillSchedule schedule,
                    out string error),
                Is.True,
                error);

            FpgResolvedSkillTimingSnapshot timing = schedule.Timing;
            Assert.That(timing.WindupTicks, Is.Zero);
            Assert.That(timing.IntervalTicks, Is.EqualTo(41));
            Assert.That(timing.RecoveryTicks, Is.EqualTo(41));
            Assert.That(timing.AttackFrameTick, Is.EqualTo(new TickIndex(100L)));
            Assert.That(timing.SameAttackReadyTick, Is.EqualTo(new TickIndex(141L)));
            Assert.That(
                timing.DifferentAttackInterruptTick,
                Is.EqualTo(new TickIndex(140L)));
            Assert.That(schedule.DurationTicks, Is.EqualTo(40));
        }

        [Test]
        public void RatioAndCapAreAppliedBeforePeriodAndWindupResolution()
        {
            FpgCompiledSkillSequence sequence = CreateSequence(
                durationTicks: 39,
                AttackEvent(10, 10));

            Assert.That(
                FpgAttackTimingResolver.TryResolve(
                    sequence,
                    CharacterTiming(1d, 20, 10),
                    1,
                    new FpgAttackSpeedProfile(1d, 0.5d, 2d),
                    10d,
                    new TickIndex(0L),
                    out FpgResolvedSkillSchedule schedule,
                    out string error),
                Is.True,
                error);

            Assert.That(schedule.Timing.EffectiveAttackSpeed, Is.EqualTo(2d));
            Assert.That(schedule.Timing.IntervalTicks, Is.EqualTo(30));
            Assert.That(schedule.Timing.WindupTicks, Is.EqualTo(8));
            Assert.That(schedule.Timing.RecoveryTicks, Is.EqualTo(22));
            Assert.That(schedule.Timing.BonusAttackSpeed, Is.EqualTo(10d));
        }

        [Test]
        public void WindupCoefficientZeroKeepsAuthoredWindupAndOneFullyScalesIt()
        {
            FpgCompiledSkillSequence sequence = CreateSequence(
                durationTicks: 59,
                AttackEvent(10, 20));
            FpgAttackSpeedProfile profile = new FpgAttackSpeedProfile(
                2d,
                0d,
                2d);

            AssertResolved(sequence, CharacterTiming(0d, 40, 20), profile,
                out FpgResolvedSkillSchedule authoredWindup);
            AssertResolved(sequence, CharacterTiming(1d, 40, 20), profile,
                out FpgResolvedSkillSchedule scaledWindup);

            Assert.That(authoredWindup.Timing.WindupTicks, Is.EqualTo(20));
            Assert.That(scaledWindup.Timing.WindupTicks, Is.EqualTo(10));
            Assert.That(authoredWindup.Timing.IntervalTicks, Is.EqualTo(30));
            Assert.That(scaledWindup.Timing.IntervalTicks, Is.EqualTo(30));
        }

        [Test]
        public void DeterministicCeilingDoesNotTurnFortyOneTicksIntoFortyTwo()
        {
            FpgCompiledSkillSequence sequence = CreateSequence(
                durationTicks: 40,
                AttackEvent(10, 0));

            AssertResolved(
                sequence,
                CharacterTiming(1d, 40, 0),
                new FpgAttackSpeedProfile(60d / 41d, 0d, 2.5d),
                out FpgResolvedSkillSchedule schedule);

            Assert.That(schedule.Timing.IntervalTicks, Is.EqualTo(41));
        }

        [Test]
        public void ResolverLimitsEffectiveSpeedToPreserveOneRecoveryTick()
        {
            FpgCompiledSkillSequence sequence = CreateSequence(
                durationTicks: 20,
                AttackEvent(10, 20));

            AssertResolved(
                sequence,
                CharacterTiming(0d, 20, 20),
                new FpgAttackSpeedProfile(10d, 0d, 10d),
                out FpgResolvedSkillSchedule schedule);

            Assert.That(schedule.Timing.WindupTicks, Is.EqualTo(20));
            Assert.That(schedule.Timing.IntervalTicks, Is.EqualTo(21));
            Assert.That(schedule.Timing.RecoveryTicks, Is.EqualTo(1));
            Assert.That(
                schedule.Timing.EffectiveAttackSpeed,
                Is.EqualTo(60d / 21d).Within(0.0000001d));
        }

        [Test]
        public void FixedSchedulePreservesAuthoredTicksAndGameplayHash()
        {
            FpgCompiledSkillSequence sequence = CreateSequence(
                durationTicks: 5,
                AttackEvent(10, 0),
                AttackEvent(20, 5));
            ulong gameplayHash = sequence.GameplayHash;

            Assert.That(
                FpgAttackTimingResolver.TryResolve(
                    sequence,
                    FpgCompiledSkillTimingDefinition.Fixed,
                    2,
                    new FpgAttackSpeedProfile(1d, 1d, 2.5d),
                    0d,
                    new TickIndex(25L),
                    out FpgResolvedSkillSchedule schedule,
                    out string error),
                Is.True,
                error);

            Assert.That(schedule.Timing.Mode, Is.EqualTo(FpgAttackTimingMode.FixedCooldown));
            Assert.That(schedule.GetResolvedTick(0), Is.Zero);
            Assert.That(schedule.GetResolvedTick(1), Is.EqualTo(5));
            Assert.That(schedule.Sequence.GameplayHash, Is.EqualTo(gameplayHash));
            Assert.That(schedule.Timing.TimingSnapshotHash, Is.Not.Zero);
        }

        [Test]
        public void BoundActivePresentationSharesResolvedAttackTickAndSortOrder()
        {
            FpgCompiledSkillEvent presentation =
                new FpgCompiledSkillEvent(
                    20,
                    20,
                    FpgActivePresentationKind.Vfx,
                    new FpgPresentationHandle(1),
                    1,
                    10UL,
                    sortOrder: 10,
                    boundGameplayEventId: 10);
            FpgCompiledSkillEvent attack = AttackEvent(10, 20, 5);
            FpgCompiledSkillSequence sequence = CreateSequence(
                durationTicks: 39,
                presentation,
                attack);

            AssertResolved(
                sequence,
                CharacterTiming(1d, 30, 20),
                new FpgAttackSpeedProfile(2d, 0d, 2d),
                out FpgResolvedSkillSchedule schedule);

            Assert.That(schedule.GetEvent(0).EventId, Is.EqualTo(10));
            Assert.That(schedule.GetEvent(1).EventId, Is.EqualTo(20));
            Assert.That(schedule.GetResolvedTick(0), Is.EqualTo(15));
            Assert.That(schedule.GetResolvedTick(1), Is.EqualTo(15));
        }

        [Test]
        public void RuntimeUsesResolvedScheduledTickButKeepsAuthoredEventTick()
        {
            FpgCompiledSkillSequence sequence = CreateSequence(
                durationTicks: 39,
                AttackEvent(10, 20));
            AssertResolved(
                sequence,
                CharacterTiming(1d, 30, 20),
                new FpgAttackSpeedProfile(2d, 0d, 2d),
                out FpgResolvedSkillSchedule schedule,
                startTick: 100L);
            FpgSkillExecutionRuntime runtime = new FpgSkillExecutionRuntime(2);

            Assert.That(
                runtime.Start(
                    schedule,
                    new SkillExecutionId(1L),
                    new TickIndex(100L)).IsSuccess,
                Is.True);
            for (long tick = 100L; tick < 115L; tick++)
            {
                Assert.That(runtime.Tick(new TickIndex(tick)).IsSuccess, Is.True);
                Assert.That(runtime.ResultCount, Is.Zero);
            }

            Assert.That(runtime.Tick(new TickIndex(115L)).IsSuccess, Is.True);
            Assert.That(runtime.ResultCount, Is.EqualTo(1));
            Assert.That(runtime.GetResult(0).Event.Tick, Is.EqualTo(20));
            Assert.That(
                runtime.GetResult(0).ScheduledTick,
                Is.EqualTo(new TickIndex(115L)));
        }

        [Test]
        public void CharacterTimingRejectsMultiActionGameplaySequences()
        {
            FpgCompiledSkillSequence multipleAttacks = CreateSequence(
                durationTicks: 20,
                AttackEvent(10, 5),
                AttackEvent(20, 10));
            FpgCompiledSkillSequence mixedActions = CreateSequence(
                durationTicks: 20,
                AttackEvent(10, 5),
                new FpgCompiledSkillEvent(
                    20,
                    10,
                    FpgSkillActionKind.LaunchProjectile,
                    0,
                    1,
                    targetSource: FpgSkillTargetSource.CurrentAim));

            Assert.That(
                FpgAttackTimingResolver.TryResolve(
                    multipleAttacks,
                    CharacterTiming(1d, 20, 5),
                    1,
                    new FpgAttackSpeedProfile(1d, 1d, 2.5d),
                    0d,
                    new TickIndex(0L),
                    out FpgResolvedSkillSchedule multipleSchedule,
                    out _),
                Is.False);
            Assert.That(multipleSchedule, Is.Null);
            Assert.That(
                FpgAttackTimingResolver.TryResolve(
                    mixedActions,
                    CharacterTiming(1d, 20, 5),
                    1,
                    new FpgAttackSpeedProfile(1d, 1d, 2.5d),
                    0d,
                    new TickIndex(0L),
                    out FpgResolvedSkillSchedule mixedSchedule,
                    out _),
                Is.False);
            Assert.That(mixedSchedule, Is.Null);
        }

        [Test]
        public void FixedTimingRejectsInterruptMarkerOutsideSequence()
        {
            FpgCompiledSkillSequence sequence = CreateSequence(
                durationTicks: 5,
                AttackEvent(10, 0));
            FpgCompiledSkillTimingDefinition invalidTiming =
                new FpgCompiledSkillTimingDefinition(
                    FpgAttackTimingMode.FixedCooldown,
                    1d,
                    6,
                    0);

            Assert.That(
                FpgAttackTimingResolver.TryResolve(
                    sequence,
                    invalidTiming,
                    1,
                    new FpgAttackSpeedProfile(1d, 1d, 2.5d),
                    0d,
                    new TickIndex(0L),
                    out FpgResolvedSkillSchedule schedule,
                    out _),
                Is.False);
            Assert.That(schedule, Is.Null);
        }

        [Test]
        public void FixedTimingReportsReadyTickOverflow()
        {
            FpgCompiledSkillSequence sequence = CreateSequence(
                durationTicks: 0,
                AttackEvent(10, 0));

            Assert.That(
                FpgAttackTimingResolver.TryResolve(
                    sequence,
                    FpgCompiledSkillTimingDefinition.Fixed,
                    0,
                    new FpgAttackSpeedProfile(1d, 1d, 2.5d),
                    0d,
                    new TickIndex(long.MaxValue),
                    out FpgResolvedSkillSchedule schedule,
                    out string error),
                Is.False);
            Assert.That(schedule, Is.Null);
            Assert.That(error, Does.Contain("overflows"));
        }

        private static void AssertResolved(
            FpgCompiledSkillSequence sequence,
            FpgCompiledSkillTimingDefinition timing,
            FpgAttackSpeedProfile profile,
            out FpgResolvedSkillSchedule schedule,
            long startTick = 0L)
        {
            Assert.That(
                FpgAttackTimingResolver.TryResolve(
                    sequence,
                    timing,
                    1,
                    profile,
                    0d,
                    new TickIndex(startTick),
                    out schedule,
                    out string error),
                Is.True,
                error);
        }

        private static FpgCompiledSkillTimingDefinition CharacterTiming(
            double coefficient,
            int interruptTick,
            int attackFrameTick)
        {
            return new FpgCompiledSkillTimingDefinition(
                FpgAttackTimingMode.CharacterAttackSpeed,
                coefficient,
                interruptTick,
                attackFrameTick);
        }

        private static FpgCompiledSkillSequence CreateSequence(
            int durationTicks,
            params FpgCompiledSkillEvent[] events)
        {
            return new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                durationTicks,
                1001,
                false,
                events);
        }

        private static FpgCompiledSkillEvent AttackEvent(
            int eventId,
            int tick,
            int sortOrder = 0)
        {
            return new FpgCompiledSkillEvent(
                eventId,
                tick,
                FpgSkillActionKind.Attack,
                0,
                sortOrder,
                targetSource: FpgSkillTargetSource.CurrentAim);
        }
    }
}
