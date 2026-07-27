using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgSkillDefinitionTests
    {
        [Test]
        public void PlayerV3CompilesTypedActionsAndHashesInlineGameplay()
        {
            FpgPlayerSkillDefinition skill =
                ScriptableObject.CreateInstance<FpgPlayerSkillDefinition>();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                ConfigureIdentity(serialized, "player.v3.typed");
                serialized.FindProperty("secondaryTriggerMode").enumValueIndex =
                    (int)SecondaryTriggerMode.ImmediateRepeatWhileHeld;
                SerializedProperty sequence = ConfigureExecute(serialized, 2);
                ConfigurePlayerAttack(sequence, 0, 2);
                ConfigurePlayerProjectile(sequence, 1, 0);
                ConfigureReload(sequence, 2, 1);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledPlayerSkillDefinition compiled,
                        out string error),
                    Is.True,
                    error);
                Assert.That(compiled.AttackActionCount, Is.EqualTo(1));
                Assert.That(compiled.ProjectileActionCount, Is.EqualTo(1));
                Assert.That(compiled.ReloadActionCount, Is.EqualTo(1));
                Assert.That(compiled.MaximumPelletCount, Is.EqualTo(8));
                Assert.That(compiled.MaximumImpactCount, Is.EqualTo(16));

                ulong gameplayHash = compiled.GameplayHash;
                SerializedProperty projectile = sequence
                    .FindPropertyRelative("projectileEvents")
                    .GetArrayElementAtIndex(0);
                projectile.FindPropertyRelative("projectileLifetimeTicks")
                    .intValue = 19;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledPlayerSkillDefinition changed,
                        out string changedError),
                    Is.True,
                    changedError);
                Assert.That(changed.GameplayHash, Is.Not.EqualTo(gameplayHash));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void PlayerV3RejectsEnemyOnlyBoundTargetAttack()
        {
            FpgPlayerSkillDefinition skill =
                ScriptableObject.CreateInstance<FpgPlayerSkillDefinition>();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                ConfigureIdentity(serialized, "player.v3.bound");
                serialized.FindProperty("secondaryTriggerMode").enumValueIndex =
                    (int)SecondaryTriggerMode.ImmediateRepeatWhileHeld;
                SerializedProperty sequence = ConfigureExecute(serialized, 0);
                ConfigureActionHeader(
                    AddElement(sequence, "attackEvents"),
                    "action.bound",
                    0,
                    0,
                    FpgSkillTargetSource.CurrentTarget);
                sequence.FindPropertyRelative("attackEvents")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("mode").enumValueIndex =
                    (int)FpgSkillAttackMode.BoundTarget;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(skill.TryCompile(out _, out string error), Is.False);
                Assert.That(error, Does.Contain("invalid target"));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void V3RejectsAuthoredOrdinalReuseAcrossDifferentTicks()
        {
            FpgPlayerSkillDefinition skill =
                ScriptableObject.CreateInstance<FpgPlayerSkillDefinition>();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                ConfigureIdentity(serialized, "player.v3.ordinal");
                serialized.FindProperty("secondaryTriggerMode").enumValueIndex =
                    (int)SecondaryTriggerMode.ImmediateRepeatWhileHeld;
                SerializedProperty sequence = ConfigureExecute(serialized, 2);
                ConfigurePlayerAttack(sequence, 0, 0);
                ConfigureReload(sequence, 0, 1);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(skill.TryCompile(out _, out string error), Is.False);
                Assert.That(error, Does.Contain("repeats authored ordinal 0"));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void EnemyV3CompilesTypedAttackAndRejectsPlayerAmmoCost()
        {
            FpgEnemyAttackDefinition skill =
                ScriptableObject.CreateInstance<FpgEnemyAttackDefinition>();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                ConfigureIdentity(serialized, "enemy.v3.typed");
                serialized.FindProperty("sequenceCooldownTicks").intValue = 5;
                SerializedProperty sequence = ConfigureExecute(serialized, 1);
                SerializedProperty attack = AddElement(sequence, "attackEvents");
                ConfigureActionHeader(
                    attack,
                    "action.enemy.impact",
                    0,
                    0,
                    FpgSkillTargetSource.CurrentTarget);
                attack.FindPropertyRelative("mode").enumValueIndex =
                    (int)FpgSkillAttackMode.BoundTarget;
                attack.FindPropertyRelative("ammoCost").intValue = 0;
                attack.FindPropertyRelative("baseDamage").intValue = 5;
                attack.FindPropertyRelative("breakDamage").intValue = 1;
                attack.FindPropertyRelative("threatDefinitionId").intValue = 101;
                attack.FindPropertyRelative("boundTargetPolicy").enumValueIndex =
                    (int)ThreatTargetPolicy.PlayerCombatant;
                attack.FindPropertyRelative("threatPresentationKind")
                    .enumValueIndex =
                    (int)FpgThreatPresentationKind.HeavyWeakpoint;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledEnemySkillDefinition compiled,
                        out string error),
                    Is.True,
                    error);
                Assert.That(compiled.AttackActions.Count, Is.EqualTo(1));
                Assert.That(compiled.TotalImpactCapacity, Is.EqualTo(1));

                attack.FindPropertyRelative("ammoCost").intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(skill.TryCompile(out _, out string ammoError), Is.False);
                Assert.That(ammoError, Does.Contain("cannot consume player ammo"));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        private static void ConfigureIdentity(
            SerializedObject serialized,
            string skillId)
        {
            serialized.FindProperty("skillId").stringValue = skillId;
            serialized.FindProperty("displayName").stringValue = skillId;
            serialized.FindProperty("authoringSchemaVersion").intValue =
                FpgSkillTimelineDefinition.CurrentAuthoringSchemaVersion;
        }

        private static SerializedProperty ConfigureExecute(
            SerializedObject serialized,
            int durationTicks)
        {
            SerializedProperty sequences = serialized.FindProperty("sequences");
            sequences.arraySize = 1;
            SerializedProperty sequence = sequences.GetArrayElementAtIndex(0);
            sequence.FindPropertyRelative("kind").enumValueIndex =
                (int)FpgSkillSequenceKind.Execute;
            sequence.FindPropertyRelative("durationTicks").intValue =
                durationTicks;
            sequence.FindPropertyRelative("mainAnimation").stringValue =
                "skill_test";
            sequence.FindPropertyRelative("loop").boolValue = false;
            sequence.FindPropertyRelative("attackEvents").arraySize = 0;
            sequence.FindPropertyRelative("projectileEvents").arraySize = 0;
            sequence.FindPropertyRelative("reloadEvents").arraySize = 0;
            sequence.FindPropertyRelative("summonEvents").arraySize = 0;
            sequence.FindPropertyRelative("activePresentationTracks")
                .arraySize = 0;
            sequence.FindPropertyRelative("warnings").arraySize = 0;
            return sequence;
        }

        private static void ConfigurePlayerAttack(
            SerializedProperty sequence,
            int authoredOrdinal,
            int tick)
        {
            SerializedProperty attack = AddElement(sequence, "attackEvents");
            ConfigureActionHeader(
                attack,
                "action.player.attack",
                tick,
                authoredOrdinal,
                FpgSkillTargetSource.CurrentAim);
            attack.FindPropertyRelative("mode").enumValueIndex =
                (int)FpgSkillAttackMode.PelletRays;
            attack.FindPropertyRelative("ammoCost").intValue = 1;
            attack.FindPropertyRelative("baseDamage").intValue = 4;
            attack.FindPropertyRelative("breakDamage").intValue = 2;
            attack.FindPropertyRelative("pelletCount").intValue = 8;
            attack.FindPropertyRelative("additionalPenetrationCount")
                .intValue = 1;
            attack.FindPropertyRelative("allowedTargetKinds").intValue =
                (int)WeaponDefinition.PlayerAttackTargetKinds;
        }

        private static void ConfigurePlayerProjectile(
            SerializedProperty sequence,
            int authoredOrdinal,
            int tick)
        {
            SerializedProperty action = AddElement(
                sequence,
                "projectileEvents");
            ConfigureActionHeader(
                action,
                "action.player.projectile",
                tick,
                authoredOrdinal,
                FpgSkillTargetSource.CurrentAim);
            action.FindPropertyRelative("impactMode").enumValueIndex =
                (int)FpgSkillProjectileImpactMode.AreaAtFirstSurface;
            action.FindPropertyRelative("ammoCost").intValue = 1;
            action.FindPropertyRelative("baseDamage").intValue = 8;
            action.FindPropertyRelative("breakDamage").intValue = 3;
            action.FindPropertyRelative("threatDefinitionId").intValue = 91;
            action.FindPropertyRelative("projectileDefinitionId").intValue = 701;
            action.FindPropertyRelative("projectileCount").intValue = 1;
            action.FindPropertyRelative("projectileFlightTicks").intValue = 12;
            action.FindPropertyRelative("projectileLifetimeTicks").intValue = 18;
            action.FindPropertyRelative("projectileBudgetUnits").intValue = 2;
            action.FindPropertyRelative("projectileSweepRadiusKey").intValue = 120;
            action.FindPropertyRelative("areaCombatantLimit").intValue = 4;
            action.FindPropertyRelative("areaProjectileLimit").intValue = 3;
            action.FindPropertyRelative("allowedTargetKinds").intValue =
                (int)WeaponDefinition.PlayerAttackTargetKinds;
        }

        private static void ConfigureReload(
            SerializedProperty sequence,
            int authoredOrdinal,
            int tick)
        {
            ConfigureActionHeader(
                AddElement(sequence, "reloadEvents"),
                "action.player.reload",
                tick,
                authoredOrdinal,
                FpgSkillTargetSource.Self);
        }

        private static SerializedProperty AddElement(
            SerializedProperty sequence,
            string arrayName)
        {
            SerializedProperty array = sequence.FindPropertyRelative(arrayName);
            int index = array.arraySize;
            array.InsertArrayElementAtIndex(index);
            return array.GetArrayElementAtIndex(index);
        }

        private static void ConfigureActionHeader(
            SerializedProperty action,
            string eventId,
            int tick,
            int authoredOrdinal,
            FpgSkillTargetSource targetSource)
        {
            action.FindPropertyRelative("eventId").stringValue = eventId;
            action.FindPropertyRelative("tick").intValue = tick;
            action.FindPropertyRelative("authoredOrdinal").intValue =
                authoredOrdinal;
            action.FindPropertyRelative("socketId").stringValue = string.Empty;
            action.FindPropertyRelative("targetSource").enumValueIndex =
                (int)targetSource;
            action.FindPropertyRelative("targetOffset").vector3Value =
                Vector3.zero;
        }
    }
}
