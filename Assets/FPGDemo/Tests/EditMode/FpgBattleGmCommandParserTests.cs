using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgBattleGmCommandParserTests
    {
        [TestCase("gm.god on", FpgBattleGmSwitchOperation.On)]
        [TestCase("gm.god off", FpgBattleGmSwitchOperation.Off)]
        [TestCase("gm.god toggle", FpgBattleGmSwitchOperation.Toggle)]
        [TestCase("GM.GOD TOGGLE", FpgBattleGmSwitchOperation.Toggle)]
        public void ParsesGodSwitches(
            string input,
            FpgBattleGmSwitchOperation expected)
        {
            Assert.That(
                FpgBattleGmCommandParser.TryParse(
                    input,
                    out FpgBattleGmCommand command,
                    out string error),
                Is.True,
                error);
            Assert.That(command.Kind, Is.EqualTo(FpgBattleGmCommandKind.God));
            Assert.That(command.Operation, Is.EqualTo(expected));
        }

        [TestCase("gm.ai on", FpgBattleGmSwitchOperation.On)]
        [TestCase("gm.ai off", FpgBattleGmSwitchOperation.Off)]
        [TestCase("gm.ai toggle", FpgBattleGmSwitchOperation.Toggle)]
        public void ParsesAiSwitches(
            string input,
            FpgBattleGmSwitchOperation expected)
        {
            Assert.That(
                FpgBattleGmCommandParser.TryParse(
                    input,
                    out FpgBattleGmCommand command,
                    out string error),
                Is.True,
                error);
            Assert.That(command.Kind, Is.EqualTo(FpgBattleGmCommandKind.Ai));
            Assert.That(command.Operation, Is.EqualTo(expected));
        }

        [Test]
        public void SpawnDefaultsCountAndSpawnPoint()
        {
            Assert.That(
                FpgBattleGmCommandParser.TryParse(
                    "gm.spawn burstbug",
                    out FpgBattleGmCommand command,
                    out string error),
                Is.True,
                error);
            Assert.That(command.Kind, Is.EqualTo(FpgBattleGmCommandKind.Spawn));
            Assert.That(command.EnemyDefinitionId, Is.EqualTo("burstbug"));
            Assert.That(command.Count, Is.EqualTo(1));
            Assert.That(command.SpawnPointId, Is.Empty);
        }

        [Test]
        public void SpawnParsesCountAndPointWithoutArtificialMaximum()
        {
            Assert.That(
                FpgBattleGmCommandParser.TryParse(
                    "gm.spawn hudie 2147483647 enemy-any-04",
                    out FpgBattleGmCommand command,
                    out string error),
                Is.True,
                error);
            Assert.That(command.Count, Is.EqualTo(int.MaxValue));
            Assert.That(command.SpawnPointId, Is.EqualTo("enemy-any-04"));
        }

        [TestCase("gm.god yes")]
        [TestCase("gm.ai")]
        [TestCase("gm.spawn")]
        [TestCase("gm.spawn luan 0")]
        [TestCase("gm.spawn luan -1")]
        [TestCase("gm.spawn luan nope")]
        [TestCase("gm.spawn luan 1 enemy-any-01 extra")]
        [TestCase("spawn luan")]
        public void RejectsInvalidGrammar(string input)
        {
            Assert.That(
                FpgBattleGmCommandParser.TryParse(
                    input,
                    out _,
                    out string error),
                Is.False);
            Assert.That(error, Is.Not.Empty);
        }
    }
}
