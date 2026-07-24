using FPG.Demo.Combat;
using FPG.Demo.Player;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class CombatPresentationProfileTests
    {
        private const string InstalledProfilePath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_CombatPresentationProfile.asset";
        private const string FeiPresentationPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/FPG_Fei_Presentation.asset";
        private const string FeiWeaponPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/FPG_Fei_Weapon.asset";
        private const string BurstbugPresentationPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Burstbug_Behavior.asset";

        [Test]
        public void StaticDefaultsLockOnlyGlobalCombatPresentationLanguage()
        {
            CombatPresentationProfile profile =
                ScriptableObject.CreateInstance<CombatPresentationProfile>();
            try
            {
                Assert.That(profile.TryValidateStatic(out string error), Is.True, error);
                AssertGlobalContract(profile);

                SerializedObject serialized = new SerializedObject(profile);
                Assert.That(serialized.FindProperty("player"), Is.Null);
                Assert.That(serialized.FindProperty("enemy"), Is.Null);
                Assert.That(serialized.FindProperty("timing"), Is.Null);
                Assert.That(serialized.FindProperty("shotRules"), Is.Null);
                Assert.That(serialized.FindProperty("playerPresentationOverride"), Is.Null);
                Assert.That(serialized.FindProperty("enemyPresentationOverride"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void InstalledProfileKeepsTheSameGlobalContract()
        {
            CombatPresentationProfile profile =
                AssetDatabase.LoadAssetAtPath<CombatPresentationProfile>(InstalledProfilePath);

            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.TryValidate(out string error), Is.True, error);
            AssertGlobalContract(profile);
        }

[Test]
        public void ActorStateAndWeaponSkillContentLiveInTheirFormalDefinitions()
        {
            D0ActorPresentationDefinition fei =
                LoadRequired<D0ActorPresentationDefinition>(FeiPresentationPath);
            D0WeaponDefinition weapon =
                LoadRequired<D0WeaponDefinition>(FeiWeaponPath);
            FpgEnemyBehaviorDefinition burstbug =
                LoadRequired<FpgEnemyBehaviorDefinition>(BurstbugPresentationPath);

            Assert.That(fei.TryGetPlayer(out PlayerActorPresentationDefinition player), Is.True);
            Assert.That(player.IdleAnimation, Is.EqualTo("b_idle"));
            Assert.That(player.HitAnimation, Is.EqualTo("hit"));
            Assert.That(player.DefeatAnimation, Is.EqualTo("death"));
            Assert.That(player.VictoryAnimation, Is.EqualTo("victory"));

            Assert.That(weapon.TryValidatePresentation(out string weaponError), Is.True, weaponError);
            Assert.That(weapon.PrimaryPresentation.AnimationName, Is.EqualTo("attack_play1"));
            Assert.That(weapon.PrimaryPresentation.AlternateAnimationName, Is.EqualTo("attack_play2"));
            Assert.That(
                weapon.PrimaryPresentation.SocketId,
                Is.EqualTo(D0ActorSocketRegistry.PrimaryMuzzleId));
            Assert.That(weapon.SecondaryPresentation.ReleaseAnimation, Is.EqualTo("defense_play"));
            Assert.That(
                weapon.SecondaryPresentation.Shot.SocketId,
                Is.EqualTo(D0ActorSocketRegistry.SecondaryMuzzleId));
            Assert.That(weapon.ReloadPresentation.PlayAnimation, Is.EqualTo("reload_play"));

            Assert.That(burstbug.TryValidate(out string behaviorError), Is.True, behaviorError);
            Assert.That(burstbug.EntryAnimation, Is.EqualTo("normal_enter"));
            Assert.That(burstbug.IdleAnimation, Is.EqualTo("normal_idle"));
            Assert.That(burstbug.DeathAnimation, Is.EqualTo("normal_death"));
        }

        [Test]
        public void FeiWeaponUsesImmediateRepeatSecondaryTriggerMode()
        {
            D0WeaponDefinition weapon =
                LoadRequired<D0WeaponDefinition>(FeiWeaponPath);

            Assert.That(
                weapon.SecondaryTriggerMode,
                Is.EqualTo(SecondaryTriggerMode.ImmediateRepeatWhileHeld));
            Assert.That(
                weapon.TryCreate(out WeaponDefinition runtimeWeapon, out string definitionError),
                Is.True,
                definitionError);
            Assert.That(
                runtimeWeapon.SecondaryTriggerMode,
                Is.EqualTo(SecondaryTriggerMode.ImmediateRepeatWhileHeld));
        }

        [Test]
        public void FeiWeaponQueryContractMigratesWithZeroPenetrationAndIndependentLimits()
        {
            D0WeaponDefinition weapon =
                LoadRequired<D0WeaponDefinition>(FeiWeaponPath);

            Assert.That(
                weapon.PrimaryQueryMode,
                Is.EqualTo(AttackQueryMode.FirstSurfacePenetration));
            Assert.That(weapon.PrimaryAdditionalPenetrationCount, Is.Zero);
            Assert.That(
                weapon.SecondaryQueryMode,
                Is.EqualTo(AttackQueryMode.AreaAtFirstSurface));
            Assert.That(weapon.SecondaryEnemyMaxImpactCount, Is.EqualTo(4));
            Assert.That(weapon.SecondaryProjectileMaxImpactCount, Is.EqualTo(4));

            Assert.That(
                weapon.TryCreate(out WeaponDefinition runtimeWeapon, out string error),
                Is.True,
                error);
            Assert.That(
                runtimeWeapon.PrimaryQueryMode,
                Is.EqualTo(AttackQueryMode.FirstSurfacePenetration));
            Assert.That(runtimeWeapon.PrimaryAdditionalPenetrationCount, Is.Zero);
            Assert.That(runtimeWeapon.PrimaryMaxImpactCount, Is.EqualTo(8));
            Assert.That(
                runtimeWeapon.PrimaryAllowedTargetKinds,
                Is.EqualTo(WeaponDefinition.PlayerAttackTargetKinds));
            Assert.That(
                runtimeWeapon.SecondaryQueryMode,
                Is.EqualTo(AttackQueryMode.AreaAtFirstSurface));
            Assert.That(runtimeWeapon.SecondaryAreaCombatantLimit, Is.EqualTo(4));
            Assert.That(runtimeWeapon.SecondaryAreaProjectileLimit, Is.EqualTo(4));
            Assert.That(runtimeWeapon.SecondaryQueryMaxImpactCount, Is.EqualTo(8));
            Assert.That(
                runtimeWeapon.SecondaryAllowedTargetKinds,
                Is.EqualTo(WeaponDefinition.PlayerAttackTargetKinds));
        }

        [Test]
        public void StaticValidationRejectsDuplicateThreatPresentationKeys()
        {
            CombatPresentationProfile profile =
                ScriptableObject.CreateInstance<CombatPresentationProfile>();
            try
            {
                SerializedObject serialized = new SerializedObject(profile);
                SerializedProperty threats = serialized.FindProperty("threatDefinitions");
                threats.GetArrayElementAtIndex(1)
                    .FindPropertyRelative("presentationKey").intValue =
                    CombatPresentationProfile.FastThreatPresentationKey;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(profile.TryValidateStatic(out string error), Is.False);
                Assert.That(error, Does.Contain("duplicated"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void StaticValidationRejectsAudioPoolBelowTheConcurrencyBudget()
        {
            CombatPresentationProfile profile =
                ScriptableObject.CreateInstance<CombatPresentationProfile>();
            try
            {
                SerializedObject serialized = new SerializedObject(profile);
                serialized.FindProperty("poolCapacities")
                    .FindPropertyRelative("audioSourceCapacity").intValue =
                    CombatPresentationProfile.RequiredAudioSourceCapacity - 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(profile.TryValidateStatic(out string error), Is.False);
                Assert.That(error, Does.Contain("capacities"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        private static void AssertGlobalContract(CombatPresentationProfile profile)
        {
            Assert.That(profile.ThreatDefinitionCount, Is.EqualTo(3));
            Assert.That(profile.HitDefinitionCount, Is.EqualTo(3));

            Assert.That(
                profile.TryGetThreatDefinition(
                    CombatPresentationProfile.FastThreatPresentationKey,
                    out CombatThreatPresentationDefinition fast),
                Is.True);
            Assert.That(fast.Kind, Is.EqualTo(CombatThreatPresentationKind.FastUninterceptable));
            Assert.That(fast.TelegraphShape, Is.EqualTo(CombatThreatTelegraphShape.SourcePulse));
            Assert.That(fast.ShowsInterceptionMarker, Is.False);

            Assert.That(
                profile.TryGetThreatDefinition(
                    CombatPresentationProfile.InterceptableVolleyThreatPresentationKey,
                    out CombatThreatPresentationDefinition volley),
                Is.True);
            Assert.That(volley.Kind, Is.EqualTo(CombatThreatPresentationKind.InterceptableVolley));
            Assert.That(volley.TelegraphShape, Is.EqualTo(CombatThreatTelegraphShape.ProjectileOutline));
            Assert.That(volley.ShowsInterceptionMarker, Is.True);

            Assert.That(
                profile.TryGetThreatDefinition(
                    CombatPresentationProfile.HeavyWeakpointThreatPresentationKey,
                    out CombatThreatPresentationDefinition heavy),
                Is.True);
            Assert.That(heavy.Kind, Is.EqualTo(CombatThreatPresentationKind.HeavyWeakpoint));
            Assert.That(heavy.TelegraphShape, Is.EqualTo(CombatThreatTelegraphShape.WeakpointLock));
            Assert.That(heavy.AllowsWeakpointInterrupt, Is.True);

            Assert.That(
                profile.TryGetHitDefinition(
                    CombatHitPresentationKind.Body,
                    out CombatHitPresentationDefinition body),
                Is.True);
            Assert.That(body.FeedbackShape, Is.EqualTo(CombatHitFeedbackShape.Burst));
            Assert.That(
                profile.TryGetHitDefinition(
                    CombatHitPresentationKind.Weakpoint,
                    out CombatHitPresentationDefinition weakpoint),
                Is.True);
            Assert.That(weakpoint.FeedbackShape, Is.EqualTo(CombatHitFeedbackShape.Diamond));
            Assert.That(
                profile.TryGetHitDefinition(
                    CombatHitPresentationKind.Intercept,
                    out CombatHitPresentationDefinition intercept),
                Is.True);
            Assert.That(intercept.FeedbackShape, Is.EqualTo(CombatHitFeedbackShape.Shatter));

            Assert.That(profile.Sorting.BackgroundOrder, Is.LessThan(profile.Sorting.ActorOrder));
            Assert.That(profile.Sorting.ActorOrder, Is.LessThan(profile.Sorting.WorldEffectsOrder));
            Assert.That(profile.Sorting.WorldEffectsOrder, Is.LessThan(profile.Sorting.ScreenEffectsOrder));
            Assert.That(profile.Sorting.ScreenEffectsOrder, Is.LessThan(profile.Sorting.HudOrder));
            Assert.That(profile.Sorting.HudOrder, Is.LessThan(profile.Sorting.ReticleOrder));
            Assert.That(profile.Sorting.ReticleOrder, Is.LessThan(profile.Sorting.DevelopmentOverlayOrder));
            Assert.That(
                profile.PoolCapacities.EnemyProjectileCapacity,
                Is.GreaterThanOrEqualTo(
                    CombatPresentationProfile.RequiredEnemyProjectileCapacity));
            Assert.That(
                profile.PoolCapacities.AudioSourceCapacity,
                Is.GreaterThanOrEqualTo(
                    CombatPresentationProfile.RequiredAudioSourceCapacity));
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }
    }
}
