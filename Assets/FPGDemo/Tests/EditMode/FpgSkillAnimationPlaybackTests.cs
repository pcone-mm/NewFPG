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
