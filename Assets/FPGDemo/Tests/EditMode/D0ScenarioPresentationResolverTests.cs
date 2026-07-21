using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0ScenarioPresentationResolverTests
    {
        private const string ScenarioConfigPath =
            "Assets/FPGDemo/Config/BattleScenarioConfig.asset";

        private const string FeiPresentationPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_Presentation.asset";

        private const string LuanPresentationPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_Presentation.asset";

        private const string HudiePresentationPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/D0_Hudie_Presentation.asset";

        private const string BurstbugPresentationPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Presentation.asset";

        [Test]
        public void InstalledLuanHudieScenarioResolvesFeiAndLuanActorDefinitions()
        {
            BattleScenarioConfig config = LoadRequired<BattleScenarioConfig>(ScenarioConfigPath);
            D0ActorPresentationDefinition fei =
                LoadRequired<D0ActorPresentationDefinition>(FeiPresentationPath);
            D0ActorPresentationDefinition luan =
                LoadRequired<D0ActorPresentationDefinition>(LuanPresentationPath);
            D0ActorPresentationDefinition hudie =
                LoadRequired<D0ActorPresentationDefinition>(HudiePresentationPath);

            Assert.That(
                D0ScenarioPresentationResolver.TryResolve(
                    config,
                    out D0ActorPresentationDefinition player,
                    out D0ActorPresentationDefinition enemy,
                    out string error),
                Is.True,
                error);
            Assert.That(player, Is.SameAs(fei));
            Assert.That(enemy, Is.SameAs(luan));
            Assert.That(enemy.ActorId, Is.EqualTo("luan"));
            Assert.That(enemy, Is.Not.SameAs(hudie),
                "Hudie owns the summoned attack presentation, not the encounter's initial actor slot.");
            Assert.That(config.AuthoredScenario.EncounterContract,
                Is.EqualTo(D0EncounterContract.LuanHudieSingleProjectile));
            Assert.That(config.AuthoredScenario.Encounter.InitialSpawnSlot.Enemy.ActorPresentation,
                Is.SameAs(luan));
            Assert.That(config.AuthoredScenario.LuanSummonHudie.HudieEnemy.ActorPresentation,
                Is.SameAs(hudie));
            Assert.That(
                enemy.TryGetEnemyEffects(out D0EnemyEffectPresentationDefinition effects),
                Is.False,
                "Luan must not inherit Burstbug's character-specific VFX pools.");
            Assert.That(effects, Is.Null);
            Assert.That(
                hudie.TryGetEnemyEffects(out D0EnemyEffectPresentationDefinition hudieEffects),
                Is.False,
                "Hudie attack VFX belongs to its attack definition.");
            Assert.That(hudieEffects, Is.Null);
        }

        [Test]
        public void BurstbugEnemyEffectsRejectDuplicateSlots()
        {
            D0ActorPresentationDefinition clone = Object.Instantiate(
                LoadRequired<D0ActorPresentationDefinition>(BurstbugPresentationPath));
            try
            {
                SerializedObject serialized = new SerializedObject(clone);
                SerializedProperty pools = RequireEnemyEffectPools(serialized);
                pools.GetArrayElementAtIndex(1).FindPropertyRelative("slot").enumValueIndex =
                    (int)D0EnemyEffectSlot.DeathLayerF4;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(clone.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("unique"));
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void BurstbugEnemyEffectsRejectInvalidPoolCapacity()
        {
            D0ActorPresentationDefinition clone = Object.Instantiate(
                LoadRequired<D0ActorPresentationDefinition>(BurstbugPresentationPath));
            try
            {
                SerializedObject serialized = new SerializedObject(clone);
                SerializedProperty pools = RequireEnemyEffectPools(serialized);
                pools.GetArrayElementAtIndex(0).FindPropertyRelative("prewarmCapacity").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(clone.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("capacity"));
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void LegacyScenarioUsesProfileFallbackWithoutActorOverrides()
        {
            BattleScenarioConfig config = ScriptableObject.CreateInstance<BattleScenarioConfig>();
            try
            {
                Assert.That(
                    D0ScenarioPresentationResolver.TryResolve(
                        config,
                        out D0ActorPresentationDefinition player,
                        out D0ActorPresentationDefinition enemy,
                        out string error),
                    Is.True,
                    error);
                Assert.That(player, Is.Null);
                Assert.That(enemy, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ActorPresenterUsesTheResolvedRuntimeOverrideAndRejectsTheWrongKind()
        {
            D0ActorPresentationDefinition fei =
                LoadRequired<D0ActorPresentationDefinition>(FeiPresentationPath);
            D0ActorPresentationDefinition burstbug =
                LoadRequired<D0ActorPresentationDefinition>(BurstbugPresentationPath);
            GameObject actor = new GameObject("D0 presentation override test");
            try
            {
                Actor2DPresenter presenter = actor.AddComponent<Actor2DPresenter>();
                SerializedObject serialized = new SerializedObject(presenter);
                serialized.FindProperty("playerActor").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(presenter.TrySetRuntimePresentationOverride(fei, out string error), Is.True, error);
                Assert.That(presenter.RuntimePresentationOverride, Is.SameAs(fei));
                Assert.That(fei.TryGetPlayer(out PlayerActorPresentationDefinition expected), Is.True);
                Assert.That(presenter.ActivePlayerPresentation, Is.SameAs(expected));

                Assert.That(
                    presenter.TrySetRuntimePresentationOverride(burstbug, out string mismatchError),
                    Is.False);
                Assert.That(mismatchError, Does.Contain("Player"));
                Assert.That(presenter.RuntimePresentationOverride, Is.SameAs(fei));
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, $"Required asset is missing: {path}");
            return asset;
        }

        private static SerializedProperty RequireEnemyEffectPools(SerializedObject serialized)
        {
            SerializedProperty effects = serialized.FindProperty("enemyEffects");
            Assert.That(effects, Is.Not.Null);
            SerializedProperty pools = effects.FindPropertyRelative("pools");
            Assert.That(pools, Is.Not.Null);
            Assert.That(pools.arraySize, Is.EqualTo(6));
            return pools;
        }
    }
}
