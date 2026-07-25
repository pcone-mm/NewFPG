using FPG.Demo.Skills;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgSkillSpatialMetadataTests
    {
        [Test]
        public void CompilerPreservesTargetSourceAndQuantizedOffset()
        {
            FpgCompiledSkillEvent authored = new FpgCompiledSkillEvent(
                1,
                0,
                FpgSkillEventKind.GameplayPayload,
                2,
                0,
                0,
                0,
                3,
                FpgSkillTargetSource.CurrentTarget,
                125,
                -250,
                500);
            FpgCompiledSkillSequence sequence = new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                0,
                4,
                false,
                new[] { authored });

            FpgCompiledSkillEvent compiled = sequence.GetEvent(0);
            Assert.That(
                compiled.TargetSource,
                Is.EqualTo(FpgSkillTargetSource.CurrentTarget));
            Assert.That(compiled.Offset.XMillimeters, Is.EqualTo(125));
            Assert.That(compiled.Offset.YMillimeters, Is.EqualTo(-250));
            Assert.That(compiled.Offset.ZMillimeters, Is.EqualTo(500));
        }

        [Test]
        public void GameplayHashIncludesSpatialMetadata()
        {
            FpgCompiledSkillSequence first = Build(
                FpgSkillTargetSource.CurrentAim,
                0);
            FpgCompiledSkillSequence changed = Build(
                FpgSkillTargetSource.CurrentTarget,
                100);

            Assert.That(changed.GameplayHash, Is.Not.EqualTo(first.GameplayHash));
        }

        private static FpgCompiledSkillSequence Build(
            FpgSkillTargetSource source,
            int offsetX)
        {
            return new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                0,
                4,
                false,
                new[]
                {
                    new FpgCompiledSkillEvent(
                        1,
                        0,
                        FpgSkillEventKind.GameplayPayload,
                        2,
                        0,
                        0,
                        0,
                        3,
                        source,
                        offsetX,
                        0,
                        0)
                });
        }
    }
}
