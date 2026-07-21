using System.Reflection;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0LuanHudieDirectSummonTests
    {
        private const string ScenarioPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/CombatLab/D0_CombatLab_FeiVsLuanSummonsHudie.asset";
        private const string SummonPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_SummonHudie.asset";
        private const string AudioBankPath =
            "Assets/FPGDemo/Config/D0Slice/CombatAudioBank.asset";

        [Test]
        public void InstalledScenarioOwnsTheDirectExecutableSummonContract()
        {
            D0CombatScenarioDefinition scenario =
                LoadRequired<D0CombatScenarioDefinition>(ScenarioPath);
            D0LuanSummonHudieDefinition summon =
                LoadRequired<D0LuanSummonHudieDefinition>(SummonPath);
            CombatAudioBank audioBank =
                LoadRequired<CombatAudioBank>(AudioBankPath);

            Assert.That(scenario.LuanSummonHudie, Is.SameAs(summon));
            Assert.That(summon.SummonTick, Is.EqualTo(240));
            Assert.That(summon.AppearanceTick, Is.EqualTo(284));
            Assert.That(
                summon.SummonSocketId,
                Is.EqualTo(D0ActorSocketRegistry.DefaultAttackOriginId));
            Assert.That(
                summon.AppearanceSocketId,
                Is.EqualTo(D0ActorSocketRegistry.DefaultAttackOriginId));
            Assert.That(summon.SummonVfxPrefab, Is.Not.Null);
            Assert.That(summon.AppearanceVfxPrefab, Is.Not.Null);
            Assert.That(
                audioBank.TryGetCueEntry(summon.SummonAudioCue, out _),
                Is.True);
            Assert.That(
                audioBank.TryGetCueEntry(summon.AppearanceAudioCue, out _),
                Is.True);
            Assert.That(summon.TryValidate(out string summonError), Is.True, summonError);
            Assert.That(scenario.TryValidate(out string scenarioError), Is.True, scenarioError);
        }

        [Test]
        public void TimelineConsumesTick240AndTick284ExactlyOnce()
        {
            D0CombatScenarioDefinition scenario =
                LoadRequired<D0CombatScenarioDefinition>(ScenarioPath);
            D0LuanHudieSummonPresentationTimeline timeline =
                new D0LuanHudieSummonPresentationTimeline();
            int luanDefinitionId =
                scenario.Encounter.InitialSpawnSlot.DefinitionId;
            int hudieDefinitionId =
                scenario.Encounter.GetSpawnSlot(1).DefinitionId;

            Assert.That(
                timeline.TryConsumeSummon(scenario, 239, luanDefinitionId),
                Is.False);
            Assert.That(
                timeline.TryConsumeSummon(scenario, 240, hudieDefinitionId),
                Is.False);
            Assert.That(
                timeline.TryConsumeSummon(scenario, 240, luanDefinitionId),
                Is.True);
            Assert.That(
                timeline.TryConsumeSummon(scenario, 240, luanDefinitionId),
                Is.False);

            Assert.That(
                timeline.TryConsumeAppearance(scenario, 283, hudieDefinitionId),
                Is.False);
            Assert.That(
                timeline.TryConsumeAppearance(scenario, 284, luanDefinitionId),
                Is.False);
            Assert.That(
                timeline.TryConsumeAppearance(scenario, 284, hudieDefinitionId),
                Is.True);
            Assert.That(
                timeline.TryConsumeAppearance(scenario, 500, hudieDefinitionId),
                Is.False);
            Assert.That(timeline.SummonConsumeCount, Is.EqualTo(1));
            Assert.That(timeline.AppearanceConsumeCount, Is.EqualTo(1));
        }

        [Test]
        public void SummonVfxPoolsAreConcreteAndPrewarmedBeforeCombat()
        {
            D0LuanSummonHudieDefinition summon =
                LoadRequired<D0LuanSummonHudieDefinition>(SummonPath);
            GameObject root = new GameObject("DirectSummonVfxWorldTest");
            GameObject sourceObject = new GameObject("Socket");
            try
            {
                D0CombatVfxWorld world = root.AddComponent<D0CombatVfxWorld>();
                Assert.That(
                    world.TryPrepareForScenario(
                        null,
                        null,
                        new[] { summon },
                        out string error),
                    Is.True,
                    error);
                Assert.That(world.PrewarmedInstanceCount, Is.EqualTo(2));
                world.BeginCombat();

                Assert.That(
                    world.TryAcquire(
                        summon.SummonVfxKey,
                        sourceObject.transform,
                        out GameObject summonVfx),
                    Is.True);
                Assert.That(
                    world.TryAcquire(
                        summon.AppearanceVfxKey,
                        sourceObject.transform,
                        out GameObject appearanceVfx),
                    Is.True);
                Assert.That(summonVfx, Is.Not.Null);
                Assert.That(appearanceVfx, Is.Not.Null);
                Assert.That(world.HotPathInstantiateCount, Is.Zero);
                Assert.That(world.HotPathDestroyCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(sourceObject);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EnemyBehaviorSceneBindingsAreRuntimeOnly()
        {
            string[] fieldNames =
            {
                "sessionHost",
                "behaviorProfile",
                "encounter",
                "visualRoot",
                "gameplayAnchor",
                "animationMotionSource",
                "summonAnimationMotionSkill"
            };

            for (int index = 0; index < fieldNames.Length; index++)
            {
                FieldInfo field = typeof(D0EnemyBehaviorController).GetField(
                    fieldNames[index],
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, fieldNames[index]);
                Assert.That(
                    field.GetCustomAttribute<SerializeField>(),
                    Is.Null,
                    fieldNames[index]);
            }
        }

        private static T LoadRequired<T>(string path)
            where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }
    }
}
