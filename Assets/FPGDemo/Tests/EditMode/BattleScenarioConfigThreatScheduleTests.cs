using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class BattleScenarioConfigThreatScheduleTests
    {
        [Test]
        public void CombatLabConfigCreatesTheAuthoredD0Encounter()
        {
            BattleScenarioConfig config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(
                "Assets/FPGDemo/Config/BattleScenarioConfig.asset");

            Assert.That(config, Is.Not.Null);
            Assert.That(config.UsesAuthoredScenario, Is.True);
            Assert.That(config.AuthoredScenario.ScenarioId,
                Is.EqualTo("combatlab-fei-vs-luan-hudie"));
            Assert.That(config.AuthoredScenario.EncounterContract,
                Is.EqualTo(D0EncounterContract.LuanHudieSingleProjectile));
            Assert.That(config.AuthoredScenario.Encounter.Enemy.EnemyId, Is.EqualTo("luan"));
            Assert.That(config.AuthoredScenario.LuanSummonHudie, Is.Not.Null);
            Assert.That(config.AuthoredScenario.LuanSummonHudie.HudieEnemy.EnemyId,
                Is.EqualTo("hudie"));
            Assert.That(config.AuthoredScenario.LuanSummonHudie.HudieEnemy,
                Is.SameAs(config.AuthoredScenario.Encounter.GetSpawnSlot(1).Enemy));
            Assert.That(config.TryCreateDefinition(
                out ScenarioDefinition definition,
                out string error), Is.True, error);
            Assert.That(definition.PlayerLife, Is.EqualTo(100));
            Assert.That(definition.PlayerBarrier, Is.EqualTo(100));
            Assert.That(definition.EnemyLife, Is.EqualTo(800));
            Assert.That(definition.EnemyBreak, Is.EqualTo(160));
            Assert.That(definition.PerfectRetractMultiplierBasisPoints, Is.EqualTo(2500));

            Assert.That(WeaponDefinition.PrimaryPelletCount, Is.EqualTo(8));
            Assert.That(definition.PlayerWeapon.PrimaryInterval.Value, Is.EqualTo(12));
            Assert.That(definition.PlayerWeapon.PrimaryDamage.BaseDamage, Is.EqualTo(4));
            Assert.That(definition.PlayerWeapon.PrimaryDamage.BreakDamage, Is.EqualTo(4));
            Assert.That(definition.PlayerWeapon.PrimaryDamage.WeakpointDamageMultiplierBasisPoints,
                Is.EqualTo(12000));
            Assert.That(definition.PlayerWeapon.PrimaryDamage.WeakpointBreakMultiplierBasisPoints,
                Is.EqualTo(25000));
            Assert.That(definition.PlayerWeapon.SecondaryAmmoCost, Is.EqualTo(2));
            Assert.That(definition.PlayerWeapon.SecondaryMinimumCharge.Value, Is.Zero);
            Assert.That(definition.PlayerWeapon.ReloadDuration.Value, Is.EqualTo(84));
            Assert.That(definition.PlayerWeapon.SecondaryDamage.BaseDamage, Is.EqualTo(28));
            Assert.That(definition.PlayerWeapon.SecondaryDamage.WeakpointDamageMultiplierBasisPoints,
                Is.EqualTo(12000));
            Assert.That(definition.PlayerWeapon.SecondaryDamage.WeakpointBreakMultiplierBasisPoints,
                Is.EqualTo(25000));
            Assert.That(config.AttackQuerySettings.SecondaryAreaRadius, Is.EqualTo(3f));

            Assert.That(definition.ThreatScheduleCount, Is.EqualTo(6));
            AssertHudieProjectileThreat(definition.GetThreatScheduleEntry(0), 1, 390);
            AssertHudieProjectileThreat(definition.GetThreatScheduleEntry(1), 2, 570);
            AssertHudieProjectileThreat(definition.GetThreatScheduleEntry(2), 3, 750);
            AssertHudieProjectileThreat(definition.GetThreatScheduleEntry(3), 4, 930);
            AssertHudieProjectileThreat(definition.GetThreatScheduleEntry(4), 5, 1110);
            AssertHudieProjectileThreat(definition.GetThreatScheduleEntry(5), 6, 1290);
        }

        [Test]
        public void TimedImpactAuthoringRejectsPayloadCountsOtherThanOne()
        {
            BattleScenarioConfig config = ScriptableObject.CreateInstance<BattleScenarioConfig>();
            try
            {
                SerializedObject serialized = new SerializedObject(config);
                SerializedProperty schedule = serialized.FindProperty("threatSchedule");
                schedule.arraySize = 1;
                SerializedProperty entry = schedule.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("scheduleSequence").longValue = 1L;
                entry.FindPropertyRelative("dueTick").intValue = 0;
                entry.FindPropertyRelative("definitionId").intValue = 901;
                entry.FindPropertyRelative("payloadKind").enumValueIndex =
                    (int)ThreatPayloadKind.TimedImpact;
                entry.FindPropertyRelative("payloadCount").intValue = 2;
                entry.FindPropertyRelative("presentationKey").intValue =
                    BattlePresentationCatalog.WeakpointWarningPresentationKey;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(config.TryCreateDefinition(
                    out ScenarioDefinition ignored,
                    out string error), Is.False);
                Assert.That(error, Does.Contain("exactly one payload"));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private static void AssertHudieProjectileThreat(
            ThreatScheduleEntry entry,
            long sequence,
            long dueTick)
        {
            Assert.That(entry.ScheduleSequence, Is.EqualTo(sequence));
            Assert.That(entry.DueTick.Value, Is.EqualTo(dueTick));
            Assert.That(entry.DefinitionId, Is.EqualTo(401));
            Assert.That(entry.TelegraphDuration.Value, Is.Zero);
            Assert.That(entry.WindupDuration.Value, Is.EqualTo(30));
            Assert.That(entry.Payload.Kind, Is.EqualTo(ThreatPayloadKind.SweptProjectile));
            Assert.That(entry.Payload.PayloadCount, Is.EqualTo(1));
            Assert.That(entry.Payload.PresentationKey, Is.EqualTo(1));
            Assert.That(entry.Payload.TotalBudgetUnits, Is.EqualTo(1));
            Assert.That(entry.Payload.ProjectileDefinition.DefinitionId, Is.EqualTo(401));
            Assert.That(entry.Payload.ProjectileDefinition.FlightDuration.Value, Is.EqualTo(36));
            Assert.That(entry.Payload.ProjectileDefinition.DamageSpec.BaseDamage, Is.EqualTo(28));
            Assert.That(entry.Payload.ProjectileDefinition.MaxHitPoints, Is.Zero);
            Assert.That(entry.Payload.ProjectileDefinition.Interceptable, Is.False);
        }

        private static void AssertFastThreat(
            ThreatScheduleEntry entry,
            long sequence,
            long dueTick,
            int definitionId,
            int projectileDefinitionId)
        {
            Assert.That(entry.ScheduleSequence, Is.EqualTo(sequence));
            Assert.That(entry.DueTick.Value, Is.EqualTo(dueTick));
            Assert.That(entry.DefinitionId, Is.EqualTo(definitionId));
            Assert.That(entry.TelegraphDuration.Value, Is.EqualTo(24));
            Assert.That(entry.WindupDuration.Value, Is.EqualTo(12));
            Assert.That(entry.Payload.Kind, Is.EqualTo(ThreatPayloadKind.SweptProjectile));
            Assert.That(entry.Payload.PayloadCount, Is.EqualTo(1));
            Assert.That(entry.Payload.PresentationKey, Is.EqualTo(1));
            Assert.That(entry.Payload.TotalBudgetUnits, Is.EqualTo(1));
            Assert.That(entry.Payload.ProjectileDefinition.DefinitionId,
                Is.EqualTo(projectileDefinitionId));
            Assert.That(entry.Payload.ProjectileDefinition.FlightDuration.Value, Is.EqualTo(36));
            Assert.That(entry.Payload.ProjectileDefinition.DamageSpec.BaseDamage, Is.EqualTo(28));
            Assert.That(entry.Payload.ProjectileDefinition.MaxHitPoints, Is.Zero);
            Assert.That(entry.Payload.ProjectileDefinition.Interceptable, Is.False);
        }

        private static void AssertSlowTripleThreat(
            ThreatScheduleEntry entry,
            long sequence,
            long dueTick,
            int definitionId,
            int projectileDefinitionId)
        {
            Assert.That(entry.ScheduleSequence, Is.EqualTo(sequence));
            Assert.That(entry.DueTick.Value, Is.EqualTo(dueTick));
            Assert.That(entry.DefinitionId, Is.EqualTo(definitionId));
            Assert.That(entry.TelegraphDuration.Value, Is.EqualTo(48));
            Assert.That(entry.WindupDuration.Value, Is.EqualTo(18));
            Assert.That(entry.Payload.Kind, Is.EqualTo(ThreatPayloadKind.SweptProjectile));
            Assert.That(entry.Payload.PayloadCount, Is.EqualTo(3));
            Assert.That(entry.Payload.PresentationKey, Is.EqualTo(2));
            Assert.That(entry.Payload.TotalBudgetUnits, Is.EqualTo(3));
            Assert.That(entry.Payload.ProjectileDefinition.DefinitionId,
                Is.EqualTo(projectileDefinitionId));
            Assert.That(entry.Payload.ProjectileDefinition.FlightDuration.Value, Is.EqualTo(120));
            Assert.That(entry.Payload.ProjectileDefinition.DamageSpec.BaseDamage, Is.EqualTo(12));
            Assert.That(entry.Payload.ProjectileDefinition.MaxHitPoints, Is.EqualTo(4),
                "The standard volley must be decisively interceptable by one Fei primary hit.");
            Assert.That(entry.Payload.ProjectileDefinition.Interceptable, Is.True);
        }

        private static void AssertHeavyWarningThreat(
            ThreatScheduleEntry entry,
            long sequence,
            long dueTick,
            int definitionId)
        {
            Assert.That(entry.ScheduleSequence, Is.EqualTo(sequence));
            Assert.That(entry.DueTick.Value, Is.EqualTo(dueTick));
            Assert.That(entry.DefinitionId, Is.EqualTo(definitionId));
            Assert.That(entry.TelegraphDuration.Value, Is.EqualTo(90));
            Assert.That(entry.WindupDuration.Value, Is.EqualTo(45));
            Assert.That(entry.Payload.Kind, Is.EqualTo(ThreatPayloadKind.TimedImpact));
            Assert.That(entry.Payload.PayloadCount, Is.EqualTo(1));
            Assert.That(entry.Payload.PresentationKey, Is.EqualTo(3));
            Assert.That(entry.Payload.TotalBudgetUnits, Is.Zero);
            Assert.That(entry.Payload.TimedImpactDamage.BaseDamage, Is.EqualTo(120));
            Assert.That(entry.Payload.ImpactDelay.Value, Is.Zero);
        }
    }
}
