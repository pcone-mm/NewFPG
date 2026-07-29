using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    [Category("PlayerSkillCorrelation")]
    public sealed class FpgPlayerSkillCorrelationAndPrefabTests
    {
        private const string CharacterPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/FPG_Fei_Character.asset";

        [Test]
        public void CombatTraceStoresExplicitSkillCorrelation()
        {
            SkillExecutionId executionId = new SkillExecutionId(41L);
            const int GameplayEventId = 701;
            CombatTrace trace = new CombatTrace(4);

            CombatEvent combatEvent = trace.Record(
                new TickIndex(7L),
                CombatEventType.SkillGameplayCommitted,
                new RuntimeId(1L),
                RuntimeId.Invalid,
                new AttackId(9L),
                ImpactId.Invalid,
                4,
                3,
                skillExecutionId: executionId.Value,
                gameplayEventId: GameplayEventId);

            Assert.That(combatEvent.HasSkillCorrelation, Is.True);
            Assert.That(combatEvent.SkillExecutionId, Is.EqualTo(executionId.Value));
            Assert.That(combatEvent.GameplayEventId, Is.EqualTo(GameplayEventId));
            Assert.That(combatEvent.PayloadHash, Is.Zero);

            CombatTrace differentCorrelation = new CombatTrace(4);
            differentCorrelation.Record(
                new TickIndex(7L),
                CombatEventType.SkillGameplayCommitted,
                new RuntimeId(1L),
                RuntimeId.Invalid,
                new AttackId(9L),
                ImpactId.Invalid,
                4,
                3,
                skillExecutionId: executionId.Value,
                gameplayEventId: GameplayEventId + 1);

            Assert.That(
                differentCorrelation.CanonicalDigest,
                Is.Not.EqualTo(trace.CanonicalDigest));
        }

        [Test]
        public void RejectedTraceCorrelationDoesNotConsumeSequence()
        {
            CombatTrace trace = new CombatTrace(4);

            Assert.Throws<ArgumentException>(() => trace.Record(
                new TickIndex(3L),
                CombatEventType.SkillGameplayCommitted,
                new RuntimeId(1L),
                RuntimeId.Invalid,
                AttackId.Invalid,
                ImpactId.Invalid,
                0,
                0,
                skillExecutionId: 17L,
                gameplayEventId: 0));
            Assert.That(trace.Count, Is.Zero);
            Assert.That(trace.TotalEventCount, Is.Zero);

            CombatEvent committed = trace.Record(
                new TickIndex(3L),
                CombatEventType.SkillGameplayCommitted,
                new RuntimeId(1L),
                RuntimeId.Invalid,
                AttackId.Invalid,
                ImpactId.Invalid,
                0,
                1,
                skillExecutionId: 17L,
                gameplayEventId: 801);

            Assert.That(committed.Sequence, Is.Zero);
            Assert.That(trace.TotalEventCount, Is.EqualTo(1L));
        }

        [Test]
        public void ActionAndHitSubmissionExposeSameSkillCorrelation()
        {
            SkillExecutionId executionId = new SkillExecutionId(23L);
            const int GameplayEventId = 901;
            FpgFormalPlayerActionEvent action =
                new FpgFormalPlayerActionEvent(
                    1L,
                    new TickIndex(5L),
                    FpgFormalPlayerActionType.PrimaryReleaseCommitted,
                    WeaponReleaseKind.Primary,
                    new AttackId(11L),
                    WeaponState.Ready,
                    WeaponState.PrimaryRecovery,
                    5,
                    4,
                    executionId,
                    GameplayEventId);
            ImpactIntent intent = new ImpactIntent(
                new ImpactId(12L),
                action.AttackId,
                new ShotId(13L),
                new RuntimeId(1L),
                new RuntimeId(2L),
                action.Tick,
                new DamageSpec(10, 0),
                HitPart.Body,
                DamageType.Normal,
                CombatTags.Primary);
            FpgPlayerHitCommand command = new FpgPlayerHitCommand(
                0L,
                intent,
                executionId,
                GameplayEventId);

            Assert.That(action.HasSkillCorrelation, Is.True);
            Assert.That(command.HasSkillCorrelation, Is.True);
            Assert.That(command.SkillExecutionId, Is.EqualTo(action.SkillExecutionId));
            Assert.That(command.GameplayEventId, Is.EqualTo(action.GameplayEventId));
        }

        [Test]
        public void FeiPrefabValidatesEveryPlayerSkillBinding()
        {
            D0CharacterDefinition character = LoadCharacter();

            Assert.That(
                FpgPlayerSkillPresentationResolver.TryValidatePrefabBindings(
                    character.EntityPrefab,
                    character.Weapon.PrimarySkill,
                    character.Weapon.ImmediateSecondarySkill,
                    character.Weapon.ReloadSkill,
                    out string immediateError),
                Is.True,
                immediateError);
            Assert.That(
                FpgPlayerSkillPresentationResolver.TryValidatePrefabBindings(
                    character.EntityPrefab,
                    character.Weapon.PrimarySkill,
                    character.Weapon.ChargeSecondarySkill,
                    character.Weapon.ReloadSkill,
                    out string chargeError),
                Is.True,
                chargeError);
        }

        [Test]
        public void PrefabPreflightRejectsMissingAlternateAnimationAndSocket()
        {
            D0CharacterDefinition character = LoadCharacter();
            FpgPlayerSkillDefinition missingAnimation =
                UnityEngine.Object.Instantiate(character.Weapon.PrimarySkill);
            FpgPlayerSkillDefinition missingSocket =
                UnityEngine.Object.Instantiate(character.Weapon.PrimarySkill);
            try
            {
                SerializedProperty animationVariants = GetFirstSequence(
                        new SerializedObject(missingAnimation))
                    .FindPropertyRelative("alternateAnimations");
                Assert.That(animationVariants, Is.Not.Null);
                Assert.That(animationVariants.arraySize, Is.GreaterThan(0));
                animationVariants.GetArrayElementAtIndex(0).stringValue =
                    "missing.formal.animation";
                animationVariants.serializedObject
                    .ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    FpgPlayerSkillPresentationResolver.TryValidatePrefabBindings(
                        character.EntityPrefab,
                        missingAnimation,
                        character.Weapon.ChargeSecondarySkill,
                        character.Weapon.ReloadSkill,
                        out string animationError),
                    Is.False);
                StringAssert.Contains(
                    "missing.formal.animation",
                    animationError);

                SerializedProperty attackEvents = GetFirstSequence(
                        new SerializedObject(missingSocket))
                    .FindPropertyRelative("attackEvents");
                Assert.That(attackEvents, Is.Not.Null);
                Assert.That(attackEvents.arraySize, Is.GreaterThan(0));
                SerializedProperty socket = attackEvents
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("socketId");
                socket.stringValue = "missing.formal.socket";
                socket.serializedObject.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    FpgPlayerSkillPresentationResolver.TryValidatePrefabBindings(
                        character.EntityPrefab,
                        missingSocket,
                        character.Weapon.ChargeSecondarySkill,
                        character.Weapon.ReloadSkill,
                        out string socketError),
                    Is.False);
                StringAssert.Contains("missing.formal.socket", socketError);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(missingAnimation);
                UnityEngine.Object.DestroyImmediate(missingSocket);
            }
        }

        [Test]
        public void NonEmptyMissingSocketDoesNotFallbackToAimAnchor()
        {
            FpgPlayerEntityView entity = LoadCharacter().EntityPrefab;

            Assert.That(
                FpgPlayerSkillPresentationResolver.TryResolvePresentationSource(
                    entity,
                    "missing.formal.socket",
                    out Transform missingSource),
                Is.False);
            Assert.That(missingSource, Is.Null);

            Assert.That(
                FpgPlayerSkillPresentationResolver.TryResolvePresentationSource(
                    entity,
                    string.Empty,
                    out Transform defaultSource),
                Is.True);
            Assert.That(defaultSource, Is.SameAs(entity.AimAnchor));
        }

        private static D0CharacterDefinition LoadCharacter()
        {
            D0CharacterDefinition character =
                AssetDatabase.LoadAssetAtPath<D0CharacterDefinition>(
                    CharacterPath);
            Assert.That(character, Is.Not.Null, CharacterPath);
            Assert.That(character.EntityPrefab, Is.Not.Null);
            Assert.That(character.Weapon, Is.Not.Null);
            return character;
        }

        private static SerializedProperty GetFirstSequence(
            SerializedObject serialized)
        {
            SerializedProperty sequences =
                serialized.FindProperty("sequences");
            Assert.That(sequences, Is.Not.Null);
            Assert.That(sequences.arraySize, Is.GreaterThan(0));
            return sequences.GetArrayElementAtIndex(0);
        }
    }
}
