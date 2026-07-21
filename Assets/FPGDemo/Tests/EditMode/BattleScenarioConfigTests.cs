using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class BattleScenarioConfigTests
    {
        [Test]
        public void DefaultsCreateAValidScenarioDefinition()
        {
            BattleScenarioConfig config = ScriptableObject.CreateInstance<BattleScenarioConfig>();

            try
            {
                Assert.That(config.TryCreateDefinition(out var definition, out string error), Is.True, error);
                Assert.That(definition, Is.Not.Null);
                Assert.That(definition.PlayerLife, Is.EqualTo(config.PlayerLife));
                Assert.That(definition.PlayerBarrier, Is.EqualTo(config.PlayerBarrier));
                Assert.That(definition.EnemyLife, Is.EqualTo(config.EnemyLife));
                Assert.That(definition.EnemyBreak, Is.EqualTo(config.EnemyBreak));
                Assert.That(definition.PlayerWeapon.MagazineCapacity, Is.EqualTo(config.MagazineCapacity));
                Assert.That(config.TryValidateSpatialConfiguration(out string spatialError),
                    Is.True,
                    spatialError);
                Assert.That(config.AttackQuerySettings.IsValid, Is.True);
                Assert.That(config.AttackQuerySettings.MaxDistance, Is.EqualTo(50f));
                Assert.That(config.AttackQuerySettings.PrimarySpreadTangent, Is.EqualTo(0.04f));
                Assert.That(config.AttackQuerySettings.SecondaryAreaRadius, Is.EqualTo(3f));
                Assert.That(config.AttackQuerySettings.HitboxLayerMask, Is.EqualTo(1 << 29));
                Assert.That(config.AttackQuerySettings.BlockerLayerMask, Is.EqualTo(1 << 28));
                Assert.That(config.SpatialTranscriptOperationCapacity,
                    Is.EqualTo(BattleScenarioConfig.DefaultSpatialTranscriptOperationCapacity));
                Assert.That(config.SpatialTranscriptQueryCandidateCapacity,
                    Is.EqualTo(BattleScenarioConfig.DefaultSpatialTranscriptQueryCandidateCapacity));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void InvalidSerializedValueIsRejectedBeforeSessionConstruction()
        {
            BattleScenarioConfig config = ScriptableObject.CreateInstance<BattleScenarioConfig>();

            try
            {
                SerializedObject serialized = new SerializedObject(config);
                serialized.FindProperty("playerLife").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(config.TryCreateDefinition(out var definition, out string error), Is.False);
                Assert.That(definition, Is.Null);
                Assert.That(error, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void LegacyZeroSpatialFieldsUseFrozenDefaults()
        {
            BattleScenarioConfig config = ScriptableObject.CreateInstance<BattleScenarioConfig>();

            try
            {
                SerializedObject serialized = new SerializedObject(config);
                SerializedProperty settings = serialized.FindProperty("attackQuerySettings");
                settings.FindPropertyRelative("maxDistance").floatValue = 0f;
                settings.FindPropertyRelative("primarySpreadTangent").floatValue = 0f;
                settings.FindPropertyRelative("secondaryAreaRadius").floatValue = 0f;
                settings.FindPropertyRelative("hitboxLayerMask").intValue = 0;
                settings.FindPropertyRelative("blockerLayerMask").intValue = 0;
                serialized.FindProperty("spatialTranscriptOperationCapacity").intValue = 0;
                serialized.FindProperty("spatialTranscriptQueryCandidateCapacity").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(config.TryValidateSpatialConfiguration(out string error), Is.True, error);
                AssertSettingsEqual(UnityAttackQuerySettings.Default, config.AttackQuerySettings);
                Assert.That(config.SpatialTranscriptOperationCapacity,
                    Is.EqualTo(BattleScenarioConfig.DefaultSpatialTranscriptOperationCapacity));
                Assert.That(config.SpatialTranscriptQueryCandidateCapacity,
                    Is.EqualTo(BattleScenarioConfig.DefaultSpatialTranscriptQueryCandidateCapacity));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void PartiallyInvalidSpatialSettingsAreNotTreatedAsLegacyDefaults()
        {
            BattleScenarioConfig config = ScriptableObject.CreateInstance<BattleScenarioConfig>();

            try
            {
                SerializedObject serialized = new SerializedObject(config);
                SerializedProperty settings = serialized.FindProperty("attackQuerySettings");
                settings.FindPropertyRelative("maxDistance").floatValue = 0f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(config.TryValidateSpatialConfiguration(out string error), Is.False);
                Assert.That(error, Does.Contain("attack query settings"));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void InvalidTranscriptCapacityIsRejected()
        {
            BattleScenarioConfig config = ScriptableObject.CreateInstance<BattleScenarioConfig>();

            try
            {
                SerializedObject serialized = new SerializedObject(config);
                serialized.FindProperty("spatialTranscriptQueryCandidateCapacity").intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(config.TryValidateSpatialConfiguration(out string error), Is.False);
                Assert.That(error, Does.Contain("query candidate capacity"));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private static void AssertSettingsEqual(
            UnityAttackQuerySettings expected,
            UnityAttackQuerySettings actual)
        {
            Assert.That(actual.MaxDistance, Is.EqualTo(expected.MaxDistance));
            Assert.That(actual.PrimarySpreadTangent, Is.EqualTo(expected.PrimarySpreadTangent));
            Assert.That(actual.SecondaryAreaRadius, Is.EqualTo(expected.SecondaryAreaRadius));
            Assert.That(actual.HitboxLayerMask, Is.EqualTo(expected.HitboxLayerMask));
            Assert.That(actual.BlockerLayerMask, Is.EqualTo(expected.BlockerLayerMask));
        }
    }
}
