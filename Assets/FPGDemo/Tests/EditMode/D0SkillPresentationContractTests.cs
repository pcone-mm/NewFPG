using System.Collections.Generic;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0SkillPresentationContractTests
    {
        private const string FeiWeaponPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_Weapon.asset";
        private const string SummonPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_SummonHudie.asset";
        private const string LuanPresentationPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_Presentation.asset";
        private const string HudiePresentationPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/D0_Hudie_Presentation.asset";

        private static readonly string[] AttackPaths =
        {
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Attack_Fast.asset",
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Attack_Volley.asset",
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Attack_HeavyBreak.asset",
            "Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/Attacks/D0_Hudie_Attack_Bullet.asset"
        };

        [Test]
        public void FeiWeaponOwnsDistinctStringAddressedSkillSockets()
        {
            D0WeaponDefinition weapon = LoadRequired<D0WeaponDefinition>(FeiWeaponPath);
            Assert.That(weapon.TryValidatePresentation(out string error), Is.True, error);
            Assert.That(
                weapon.PrimaryPresentation.SocketId,
                Is.EqualTo(D0ActorSocketRegistry.PrimaryMuzzleId));
            Assert.That(
                weapon.SecondaryPresentation.Shot.SocketId,
                Is.EqualTo(D0ActorSocketRegistry.SecondaryMuzzleId));
            Assert.That(
                weapon.PrimaryPresentation.SocketId,
                Is.Not.EqualTo(weapon.SecondaryPresentation.Shot.SocketId));
            Assert.That(weapon.PrimaryPresentation.AnimationName, Is.EqualTo("attack_play1"));
            Assert.That(weapon.SecondaryPresentation.ReleaseAnimation, Is.EqualTo("defense_play"));
        }

        [Test]
        public void EveryAuthoredEnemyAttackOwnsAValidPresentationContract()
        {
            for (int index = 0; index < AttackPaths.Length; index++)
            {
                D0EnemyAttackDefinition attack =
                    LoadRequired<D0EnemyAttackDefinition>(AttackPaths[index]);
                Assert.That(attack.Presentation, Is.Not.Null, AttackPaths[index]);
                Assert.That(
                    attack.TryValidatePresentation(out string error),
                    Is.True,
                    AttackPaths[index] + ": " + error);
                Assert.That(
                    attack.SocketId,
                    Is.EqualTo(D0ActorSocketRegistry.DefaultAttackOriginId));
                Assert.That(attack.EffectiveVisualEffectKey, Is.Not.Empty);
                Assert.That(attack.AudioCue, Is.GreaterThan(CombatAudioCue.None));
                Assert.That(attack.AudioCue, Is.LessThan(CombatAudioCue.Count));
                Assert.That(attack.ReleaseMarkerTicks, Is.GreaterThanOrEqualTo(0));
            }

            D0EnemyAttackDefinition hudie =
                LoadRequired<D0EnemyAttackDefinition>(AttackPaths[3]);
            Assert.That(hudie.EffectiveVisualEffectKey, Is.EqualTo("hudie-attack"));
            Assert.That(hudie.VisualEffectPrefab, Is.Null);
        }

        [Test]
        public void SummonDefinitionOwnsTransitionAnimationsAndVfxDependencies()
        {
            D0LuanSummonHudieDefinition summon =
                LoadRequired<D0LuanSummonHudieDefinition>(SummonPath);
            Assert.That(summon.SummonAnimation, Is.EqualTo("die&broken"));
            Assert.That(summon.AppearanceAnimation, Is.EqualTo("appear"));
            Assert.That(
                summon.HudieEnemy.ActorPresentation.TryGetEnemy(
                    out EnemyActorPresentationDefinition hudieState),
                Is.True);
            Assert.That(hudieState.IdleAnimation, Is.EqualTo("idle"));

            List<D0CombatVfxAssetReference> references =
                new List<D0CombatVfxAssetReference>();
            summon.CollectPresentationVfxReferences(references);
            Assert.That(references, Has.Count.EqualTo(2));
            Assert.That(references[0].Key, Is.EqualTo("luan.summon"));
            Assert.That(references[1].Key, Is.EqualTo("hudie.appear"));
            Assert.That(references[0].TryValidate(out string firstError), Is.True, firstError);
            Assert.That(references[1].TryValidate(out string secondError), Is.True, secondError);
        }

        [Test]
        public void LuanAndHudieDoNotCloneBurstbugActorEffectPools()
        {
            AssertHasNoActorEffectPools(LuanPresentationPath);
            AssertHasNoActorEffectPools(HudiePresentationPath);
        }

        private static void AssertHasNoActorEffectPools(string assetPath)
        {
            D0ActorPresentationDefinition presentation =
                LoadRequired<D0ActorPresentationDefinition>(assetPath);
            Assert.That(
                presentation.TryGetEnemyEffects(out D0EnemyEffectPresentationDefinition effects),
                Is.False,
                assetPath);
            Assert.That(effects, Is.Null);
        }

        private static T LoadRequired<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            Assert.That(asset, Is.Not.Null, assetPath);
            return asset;
        }
    }
}
