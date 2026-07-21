using FPG.Demo.Core;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0RuntimePerformanceStressDriverTests
    {
        private const string ScenarioConfigPath =
            "Assets/FPGDemo/Config/BattleScenarioConfig.asset";

        [Test]
        public void StressScenarioOwnsThreatSlotsWithoutMutatingThePlayableScenario()
        {
            BattleScenarioConfig config =
                AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(ScenarioConfigPath);
            Assert.That(config, Is.Not.Null);
            Assert.That(config.TryCreateDefinition(out ScenarioDefinition playable, out string error), Is.True, error);
            Assert.That(playable.ThreatScheduleCount, Is.GreaterThan(0));

            ScenarioDefinition stress =
                D0RuntimePerformanceStressDriver.CreateScenarioDefinitionForStress(playable);

            Assert.That(stress.ThreatScheduleCount, Is.Zero);
            Assert.That(stress.ProjectileCapacity, Is.EqualTo(playable.ProjectileCapacity));
            Assert.That(stress.ProjectileBudgetCapacity, Is.EqualTo(playable.ProjectileBudgetCapacity));
            Assert.That(stress.ThreatCapacity, Is.EqualTo(playable.ThreatCapacity));
            Assert.That(stress.EnemyLife, Is.GreaterThan(playable.EnemyLife));
            Assert.That(stress.EnemyBreak, Is.GreaterThan(playable.EnemyBreak));
            Assert.That(playable.ThreatScheduleCount, Is.GreaterThan(0));
        }

        [Test]
        public void StressTranscriptCapacityCoversOneMinuteOfThirtyTwoProjectileSweeps()
        {
            int oneMinuteSweepCount = 32 * GameplayClock.DefaultTickRate * 60;

            Assert.That(
                D0RuntimePerformanceStressDriver.StressTranscriptOperationCapacity,
                Is.GreaterThan(oneMinuteSweepCount));
        }
    }
}
