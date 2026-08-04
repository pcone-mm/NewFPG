using FPG.Demo.Core;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgSkillAnimationPlaybackTests
    {
        [Test]
        public void CompiledSequenceStoresAbsoluteAnimationInterval()
        {
            FpgCompiledSkillSequence sequence =
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    60,
                    100,
                    false,
                    FpgSkillAnimationPlaybackMode.FitInterval,
                    5,
                    45,
                    new FpgCompiledSkillEvent[0]);

            Assert.That(
                sequence.AnimationPlaybackMode,
                Is.EqualTo(FpgSkillAnimationPlaybackMode.FitInterval));
            Assert.That(sequence.AnimationStartTick, Is.EqualTo(5));
            Assert.That(sequence.AnimationEndTick, Is.EqualTo(45));
        }

        [Test]
        public void AnimationPlaybackModeChangesPresentationHash()
        {
            FpgCompiledSkillSequence natural =
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    60,
                    100,
                    false,
                    FpgSkillAnimationPlaybackMode.NaturalSpeed,
                    0,
                    60,
                    new FpgCompiledSkillEvent[0]);
            FpgCompiledSkillSequence fitted =
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    60,
                    100,
                    false,
                    FpgSkillAnimationPlaybackMode.FitInterval,
                    0,
                    60,
                    new FpgCompiledSkillEvent[0]);

            Assert.That(fitted.GameplayHash, Is.EqualTo(natural.GameplayHash));
            Assert.That(
                fitted.PresentationHash,
                Is.Not.EqualTo(natural.PresentationHash));
        }
    

        [Test]
        public void FitIntervalUsesAbsoluteTickAndInterpolation()
        {
            FpgCompiledSkillSequence sequence =
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    60,
                    100,
                    false,
                    FpgSkillAnimationPlaybackMode.FitInterval,
                    10,
                    50,
                    new FpgCompiledSkillEvent[0]);

            double seconds = FpgSkillAnimationTime.EvaluateSeconds(
                sequence,
                30,
                0.5d,
                2d);

            Assert.That(seconds, Is.EqualTo(1.025d).Within(0.000001d));
        }

        [Test]
        public void NaturalSpeedUsesSixtyHertzWithoutDeltaAccumulation()
        {
            FpgCompiledSkillSequence sequence =
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    60,
                    100,
                    false,
                    FpgSkillAnimationPlaybackMode.NaturalSpeed,
                    0,
                    60,
                    new FpgCompiledSkillEvent[0]);

            double seconds = FpgSkillAnimationTime.EvaluateSeconds(
                sequence,
                30,
                0.5d,
                10d);

            Assert.That(seconds, Is.EqualTo(30.5d / 60d).Within(0.000001d));
        }

        [Test]
        public void ResolvedAttackTimingKeepsZeroWindupAttackOnStartTick()
        {
            FpgCompiledSkillSequence sequence = SequenceWithAttack(40, 0);
            FpgResolvedSkillTimingSnapshot timing = ResolveTiming(
                sequence,
                0,
                60d / 41d);

            double seconds = FpgSkillAnimationTime.EvaluateSeconds(
                sequence,
                timing,
                0,
                0d,
                10d);

            Assert.That(timing.WindupTicks, Is.Zero);
            Assert.That(seconds, Is.Zero);
        }

        [Test]
        public void ResolvedAttackTimingAlignsAttackFrameAndKeepsNaturalRecovery()
        {
            FpgCompiledSkillSequence sequence = SequenceWithAttack(10, 4);
            FpgResolvedSkillTimingSnapshot timing = ResolveTiming(
                sequence,
                4,
                3d);

            double beforeAttack = FpgSkillAnimationTime.EvaluateSeconds(
                sequence,
                timing,
                timing.WindupTicks - 1,
                0.999d,
                10d);
            double attackFrame = FpgSkillAnimationTime.EvaluateSeconds(
                sequence,
                timing,
                timing.WindupTicks,
                0d,
                10d);
            double recoverySample = FpgSkillAnimationTime.EvaluateSeconds(
                sequence,
                timing,
                timing.IntervalTicks - 1,
                0d,
                10d);

            Assert.That(beforeAttack, Is.LessThan(4d / 60d));
            Assert.That(attackFrame, Is.EqualTo(4d / 60d).Within(0.000001d));
            Assert.That(
                recoverySample,
                Is.EqualTo(
                    (timing.AuthoredAttackFrameTick
                        + timing.IntervalTicks - 1
                        - timing.WindupTicks) / 60d)
                    .Within(0.000001d));
        }

        [Test]
        public void ResolvedAttackTimingDoesNotJumpToTerminalAtPeriod()
        {
            FpgCompiledSkillSequence sequence = SequenceWithAttack(40, 0);
            FpgResolvedSkillTimingSnapshot timing = ResolveTiming(
                sequence,
                0,
                6d);

            double seconds = FpgSkillAnimationTime.EvaluateSeconds(
                sequence,
                timing,
                timing.IntervalTicks,
                0d,
                10d);

            Assert.That(
                seconds,
                Is.EqualTo(timing.IntervalTicks / 60d).Within(0.000001d));
            Assert.That(seconds, Is.LessThan(40d / 60d));
        }

        [Test]
        public void FixedResolvedTimingPreservesExistingAnimationMapping()
        {
            FpgCompiledSkillSequence sequence = new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                60,
                100,
                false,
                FpgSkillAnimationPlaybackMode.FitInterval,
                10,
                50,
                new FpgCompiledSkillEvent[0]);
            Assert.That(
                FpgAttackTimingResolver.TryResolve(
                    sequence,
                    FpgCompiledSkillTimingDefinition.Fixed,
                    0,
                    new FpgAttackSpeedProfile(1d, 1d, 2.5d),
                    0d,
                    new TickIndex(1),
                    out FpgResolvedSkillSchedule schedule,
                    out string error),
                Is.True,
                error);

            double legacy = FpgSkillAnimationTime.EvaluateSeconds(
                sequence,
                30,
                0.5d,
                2d);
            double resolved = FpgSkillAnimationTime.EvaluateSeconds(
                sequence,
                schedule.Timing,
                30,
                0.5d,
                2d);

            Assert.That(resolved, Is.EqualTo(legacy).Within(0.000000001d));
        }

        private static FpgCompiledSkillSequence SequenceWithAttack(
            int durationTicks,
            int attackTick)
        {
            return new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                durationTicks,
                100,
                false,
                FpgSkillAnimationPlaybackMode.NaturalSpeed,
                0,
                durationTicks,
                new[]
                {
                    new FpgCompiledSkillEvent(
                        1,
                        attackTick,
                        FpgSkillActionKind.Attack,
                        0)
                });
        }

        private static FpgResolvedSkillTimingSnapshot ResolveTiming(
            FpgCompiledSkillSequence sequence,
            int attackTick,
            double attackSpeed)
        {
            Assert.That(
                FpgAttackTimingResolver.TryResolve(
                    sequence,
                    new FpgCompiledSkillTimingDefinition(
                        FpgAttackTimingMode.CharacterAttackSpeed,
                        1d,
                        sequence.DurationTicks,
                        attackTick),
                    0,
                    new FpgAttackSpeedProfile(attackSpeed, 1d, attackSpeed),
                    0d,
                    new TickIndex(1),
                    out FpgResolvedSkillSchedule schedule,
                    out string error),
                Is.True,
                error);
            return schedule.Timing;
        }


        [Test]
        public void AnimationVariantsParticipateInPresentationHash()
        {
            FpgCompiledSkillSequence baseline =
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    12,
                    100,
                    false,
                    FpgSkillAnimationPlaybackMode.NaturalSpeed,
                    0,
                    12,
                    new FpgCompiledSkillEvent[0]);
            FpgCompiledSkillSequence variant =
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    12,
                    100,
                    false,
                    FpgSkillAnimationPlaybackMode.NaturalSpeed,
                    0,
                    12,
                    new[] { 200 },
                    new FpgCompiledSkillEvent[0]);

            Assert.That(variant.GameplayHash, Is.EqualTo(baseline.GameplayHash));
            Assert.That(
                variant.PresentationHash,
                Is.Not.EqualTo(baseline.PresentationHash));
        }


        [Test]
        public void AnimationVariantsResolveDeterministicallyFromExecutionId()
        {
            FpgCompiledSkillSequence sequence =
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    12,
                    100,
                    false,
                    FpgSkillAnimationPlaybackMode.NaturalSpeed,
                    0,
                    12,
                    new[] { 200 },
                    new FpgCompiledSkillEvent[0]);

            Assert.That(
                sequence.ResolveAnimation(new SkillExecutionId(1)),
                Is.EqualTo(100));
            Assert.That(
                sequence.ResolveAnimation(new SkillExecutionId(2)),
                Is.EqualTo(200));
            Assert.That(
                sequence.ResolveAnimation(new SkillExecutionId(3)),
                Is.EqualTo(100));
        }

        [Test]
        public void FormalAnimationClockClampsRenderInterpolationBelowOne()
        {
            Assert.That(
                FpgFormalPlayerSkillAnimationClock.ResolveInterpolation(
                    renderTime: 10.0125d,
                    fixedTime: 10d,
                    fixedDeltaTime: 0.025d),
                Is.EqualTo(0.5d).Within(0.000001d));
            Assert.That(
                FpgFormalPlayerSkillAnimationClock.ResolveInterpolation(
                    renderTime: 11d,
                    fixedTime: 10d,
                    fixedDeltaTime: 0.025d),
                Is.LessThan(1d));
            Assert.That(
                FpgFormalPlayerSkillAnimationClock.ResolveInterpolation(
                    renderTime: 9d,
                    fixedTime: 10d,
                    fixedDeltaTime: 0.025d),
                Is.Zero);
        }
}
}
