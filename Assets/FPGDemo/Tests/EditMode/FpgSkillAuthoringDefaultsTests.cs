using FPG.Demo.Editor.SkillAuthoring;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgSkillAuthoringDefaultsTests
    {
        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
        }

        [Test]
        public void NewPlayerPayloadAndEventAreImmediatelyValid()
        {
            FpgPlayerSkillDefinition skill =
                ScriptableObject.CreateInstance<FpgPlayerSkillDefinition>();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                serialized.FindProperty("payloadSlots").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                int payloadIndex =
                    FpgSkillSerializedAdapter.AddPayload(serialized, 0);
                Assert.That(payloadIndex, Is.Zero);

                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(serialized, 0);
                FpgSkillPayloadRecord payload =
                    FpgSkillSerializedAdapter.ReadPayloads(sequence)[0];
                Assert.That(
                    payload.PreviewKind,
                    Is.EqualTo(FpgSkillPreviewPayloadKind.PlayerPelletRay));
                Assert.That(
                    skill.PayloadSlots[0].TryValidate(out string payloadError),
                    Is.True,
                    payloadError);

                int eventKey = FpgSkillSerializedAdapter.AddEvent(
                    serialized,
                    0,
                    0,
                    payload,
                    FpgSkillEventTrackKind.Logic);
                Assert.That(eventKey, Is.GreaterThanOrEqualTo(0));

                serialized.UpdateIfRequiredOrScript();
                SerializedProperty eventProperty =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        eventKey);
                Assert.That(
                    eventProperty.FindPropertyRelative("targetSource").intValue,
                    Is.EqualTo((int)FpgSkillTargetSource.CurrentAim));
                Assert.That(
                    eventProperty.FindPropertyRelative("eventId").stringValue,
                    Is.Not.Empty);
                Assert.That(
                    eventProperty.FindPropertyRelative("payloadSlotId").stringValue,
                    Is.EqualTo(payload.Id));
                Assert.That(
                    skill.TryValidate(out string skillError),
                    Is.True,
                    skillError);
            }
            finally
            {
                Undo.ClearUndo(skill);
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void NewEnemyPayloadAndEventAreImmediatelyValid()
        {
            FpgEnemyAttackDefinition skill =
                ScriptableObject.CreateInstance<FpgEnemyAttackDefinition>();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                serialized.FindProperty("payloadSlots").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                int payloadIndex =
                    FpgSkillSerializedAdapter.AddPayload(serialized, 0);
                Assert.That(payloadIndex, Is.Zero);

                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(serialized, 0);
                FpgSkillPayloadRecord payload =
                    FpgSkillSerializedAdapter.ReadPayloads(sequence)[0];
                Assert.That(
                    payload.PreviewKind,
                    Is.EqualTo(FpgSkillPreviewPayloadKind.EnemyProjectile));
                Assert.That(
                    skill.PayloadSlots[0].TryValidate(out string payloadError),
                    Is.True,
                    payloadError);

                int eventKey = FpgSkillSerializedAdapter.AddEvent(
                    serialized,
                    0,
                    0,
                    payload,
                    FpgSkillEventTrackKind.Logic);
                Assert.That(eventKey, Is.GreaterThanOrEqualTo(0));

                serialized.UpdateIfRequiredOrScript();
                SerializedProperty eventProperty =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        eventKey);
                Assert.That(
                    eventProperty.FindPropertyRelative("targetSource").intValue,
                    Is.EqualTo((int)FpgSkillTargetSource.CurrentTarget));
                FpgSkillLogicEventDefinition skillEvent =
                    skill.Sequences[0].LogicEvents[0];
                Assert.That(
                    FpgEnemySkillSpatialPolicy.TryValidate(
                        skill.PayloadSlots[0].Kind,
                        skillEvent,
                        out string spatialError),
                    Is.True,
                    spatialError);
                Assert.That(
                    skill.TryValidate(out string skillError),
                    Is.True,
                    skillError);
            }
            finally
            {
                Undo.ClearUndo(skill);
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void ReloadCommitEventDefaultsToSelf()
        {
            FpgPlayerSkillDefinition skill =
                ScriptableObject.CreateInstance<FpgPlayerSkillDefinition>();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty payloadProperty =
                    serialized.FindProperty("payloadSlots")
                        .GetArrayElementAtIndex(0);
                payloadProperty.FindPropertyRelative("kind").enumValueIndex =
                    (int)FpgPlayerSkillPayloadKind.ReloadCommit;
                payloadProperty.FindPropertyRelative("ammoCost").intValue = 0;
                payloadProperty.FindPropertyRelative("baseDamage").intValue = 0;
                payloadProperty.FindPropertyRelative("breakDamage").intValue = 0;
                payloadProperty.FindPropertyRelative("queryMode").enumValueIndex = 0;
                payloadProperty.FindPropertyRelative("allowedTargetKinds").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(serialized, 0);
                FpgSkillPayloadRecord payload =
                    FpgSkillSerializedAdapter.ReadPayloads(sequence)[0];
                Assert.That(
                    payload.PreviewKind,
                    Is.EqualTo(FpgSkillPreviewPayloadKind.PlayerReload));

                int eventKey = FpgSkillSerializedAdapter.AddEvent(
                    serialized,
                    0,
                    0,
                    payload,
                    FpgSkillEventTrackKind.Logic);
                Assert.That(eventKey, Is.GreaterThanOrEqualTo(0));

                serialized.UpdateIfRequiredOrScript();
                SerializedProperty eventProperty =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        eventKey);
                Assert.That(
                    eventProperty.FindPropertyRelative("targetSource").intValue,
                    Is.EqualTo((int)FpgSkillTargetSource.Self));
                Assert.That(
                    skill.TryValidate(out string skillError),
                    Is.True,
                    skillError);
            }
            finally
            {
                Undo.ClearUndo(skill);
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void StablePayloadIdWinsWhenLegacyIndexConflicts()
        {
            FpgLegacyPayloadIndexTestAsset skill =
                ScriptableObject.CreateInstance<FpgLegacyPayloadIndexTestAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty eventProperty = serialized
                    .FindProperty("sequences")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("logicEvents")
                    .GetArrayElementAtIndex(0);
                Assert.That(
                    eventProperty.FindPropertyRelative(
                        "payloadSlotId").stringValue,
                    Is.EqualTo("payload.b"));
                Assert.That(
                    eventProperty.FindPropertyRelative("payloadIndex").intValue,
                    Is.Zero);

                Assert.That(
                    FpgSkillSerializedAdapter.SetPayloadKindAndNormalize(
                        serialized,
                        0,
                        0,
                        (int)FpgPlayerSkillPayloadKind.ReloadCommit),
                    Is.True);
                serialized.UpdateIfRequiredOrScript();
                eventProperty = serialized
                    .FindProperty("sequences")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("logicEvents")
                    .GetArrayElementAtIndex(0);
                Assert.That(
                    eventProperty.FindPropertyRelative("targetSource").intValue,
                    Is.EqualTo((int)FpgSkillTargetSource.CurrentAim));

                Assert.That(
                    FpgSkillSerializedAdapter.SetPayloadKindAndNormalize(
                        serialized,
                        0,
                        1,
                        (int)FpgPlayerSkillPayloadKind.ReloadCommit),
                    Is.True);
                serialized.UpdateIfRequiredOrScript();
                eventProperty = serialized
                    .FindProperty("sequences")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("logicEvents")
                    .GetArrayElementAtIndex(0);
                Assert.That(
                    eventProperty.FindPropertyRelative("targetSource").intValue,
                    Is.EqualTo((int)FpgSkillTargetSource.Self));
                Assert.That(
                    eventProperty.FindPropertyRelative(
                        "payloadSlotId").stringValue,
                    Is.EqualTo("payload.b"));
                Assert.That(
                    eventProperty.FindPropertyRelative("payloadIndex").intValue,
                    Is.Zero);
            }
            finally
            {
                Undo.ClearUndo(skill);
                Object.DestroyImmediate(skill);
            }
        }


        [Test]
        public void EnemyProjectileToTimedImpactClearsSpatialMetadata()
        {
            FpgEnemyAttackDefinition skill =
                ScriptableObject.CreateInstance<FpgEnemyAttackDefinition>();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty payloadProperty =
                    FpgSkillSerializedAdapter.GetPayloadProperty(
                        serialized,
                        0,
                        0);
                payloadProperty.FindPropertyRelative(
                    "projectileCount").intValue = int.MaxValue;
                payloadProperty.FindPropertyRelative(
                    "projectileBudgetUnits").intValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(
                    FpgSkillSerializedAdapter.SetPayloadKindAndNormalize(
                        serialized,
                        0,
                        0,
                        (int)FpgEnemySkillPayloadKind.Projectile),
                    Is.True);
                Assert.That(
                    skill.PayloadSlots[0].TryValidate(out string projectileError),
                    Is.True,
                    projectileError);

                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(serialized, 0);
                FpgSkillPayloadRecord payload =
                    FpgSkillSerializedAdapter.ReadPayloads(sequence)[0];
                int eventKey = FpgSkillSerializedAdapter.AddEvent(
                    serialized,
                    0,
                    0,
                    payload,
                    FpgSkillEventTrackKind.Logic);
                Assert.That(eventKey, Is.GreaterThanOrEqualTo(0));

                serialized.UpdateIfRequiredOrScript();
                SerializedProperty eventProperty =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        eventKey);
                eventProperty.FindPropertyRelative("targetSource").intValue =
                    (int)FpgSkillTargetSource.SocketForward;
                eventProperty.FindPropertyRelative("socketId").stringValue =
                    "weapon.muzzle";
                eventProperty.FindPropertyRelative("targetOffset").vector3Value =
                    new Vector3(1f, 2f, 3f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(
                    FpgEnemySkillSpatialPolicy.TryValidate(
                        skill.PayloadSlots[0].Kind,
                        skill.Sequences[0].LogicEvents[0],
                        out string initialSpatialError),
                    Is.True,
                    initialSpatialError);

                Assert.That(
                    FpgSkillSerializedAdapter.SetPayloadKindAndNormalize(
                        serialized,
                        0,
                        0,
                        (int)FpgEnemySkillPayloadKind.TimedImpact),
                    Is.True);
                serialized.UpdateIfRequiredOrScript();
                eventProperty =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        eventKey);
                Assert.That(
                    eventProperty.FindPropertyRelative("targetSource").intValue,
                    Is.EqualTo((int)FpgSkillTargetSource.CurrentTarget));
                Assert.That(
                    eventProperty.FindPropertyRelative("socketId").stringValue,
                    Is.Empty);
                Assert.That(
                    eventProperty.FindPropertyRelative("targetOffset").vector3Value,
                    Is.EqualTo(Vector3.zero));
                Assert.That(
                    skill.PayloadSlots[0].TryValidate(out string impactError),
                    Is.True,
                    impactError);
                Assert.That(
                    FpgEnemySkillSpatialPolicy.TryValidate(
                        skill.PayloadSlots[0].Kind,
                        skill.Sequences[0].LogicEvents[0],
                        out string spatialError),
                    Is.True,
                    spatialError);
            }
            finally
            {
                Undo.ClearUndo(skill);
                Object.DestroyImmediate(skill);
            }
        }


        [Test]
        public void ReplacingEventPayloadWithReloadSynchronizesSelfTarget()
        {
            FpgPlayerSkillDefinition skill =
                ScriptableObject.CreateInstance<FpgPlayerSkillDefinition>();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                Assert.That(
                    FpgSkillSerializedAdapter.AddPayload(serialized, 0),
                    Is.EqualTo(1));
                Assert.That(
                    FpgSkillSerializedAdapter.SetPayloadKindAndNormalize(
                        serialized,
                        0,
                        1,
                        (int)FpgPlayerSkillPayloadKind.ReloadCommit),
                    Is.True);

                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(serialized, 0);
                System.Collections.Generic.List<FpgSkillPayloadRecord> payloads =
                    FpgSkillSerializedAdapter.ReadPayloads(sequence);
                int eventKey = FpgSkillSerializedAdapter.AddEvent(
                    serialized,
                    0,
                    0,
                    payloads[0],
                    FpgSkillEventTrackKind.Logic);
                Assert.That(eventKey, Is.GreaterThanOrEqualTo(0));

                Assert.That(
                    FpgSkillSerializedAdapter.SetEventPayloadReference(
                        serialized,
                        0,
                        eventKey,
                        1),
                    Is.True);
                serialized.UpdateIfRequiredOrScript();
                SerializedProperty eventProperty =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        eventKey);
                Assert.That(
                    eventProperty.FindPropertyRelative(
                        "payloadSlotId").stringValue,
                    Is.EqualTo(payloads[1].Id));
                Assert.That(
                    eventProperty.FindPropertyRelative("targetSource").intValue,
                    Is.EqualTo((int)FpgSkillTargetSource.Self));
                Assert.That(
                    skill.PayloadSlots[1].TryValidate(out string payloadError),
                    Is.True,
                    payloadError);
            }
            finally
            {
                Undo.ClearUndo(skill);
                Object.DestroyImmediate(skill);
            }
        }


        [Test]
        public void PlayerPayloadKindSwitchKeepsPayloadAndTargetsValid()
        {
            FpgPlayerSkillDefinition skill =
                ScriptableObject.CreateInstance<FpgPlayerSkillDefinition>();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(serialized, 0);
                FpgSkillPayloadRecord payload =
                    FpgSkillSerializedAdapter.ReadPayloads(sequence)[0];
                int eventKey = FpgSkillSerializedAdapter.AddEvent(
                    serialized,
                    0,
                    0,
                    payload,
                    FpgSkillEventTrackKind.Logic);
                Assert.That(eventKey, Is.GreaterThanOrEqualTo(0));
                Assert.That(
                    skill.PayloadSlots[0].TryValidate(out string pelletError),
                    Is.True,
                    pelletError);

                Assert.That(
                    FpgSkillSerializedAdapter.SetPayloadKindAndNormalize(
                        serialized,
                        0,
                        0,
                        (int)FpgPlayerSkillPayloadKind.ReloadCommit),
                    Is.True);
                Assert.That(
                    skill.PayloadSlots[0].Kind,
                    Is.EqualTo(FpgPlayerSkillPayloadKind.ReloadCommit));
                Assert.That(
                    skill.PayloadSlots[0].TryValidate(out string reloadError),
                    Is.True,
                    reloadError);
                serialized.UpdateIfRequiredOrScript();
                SerializedProperty eventProperty =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        eventKey);
                Assert.That(
                    eventProperty.FindPropertyRelative("targetSource").intValue,
                    Is.EqualTo((int)FpgSkillTargetSource.Self));

                SerializedProperty payloadProperty =
                    FpgSkillSerializedAdapter.GetPayloadProperty(
                        serialized,
                        0,
                        0);
                payloadProperty.FindPropertyRelative(
                    "areaCombatantLimit").intValue = int.MaxValue;
                payloadProperty.FindPropertyRelative(
                    "areaProjectileLimit").intValue = int.MaxValue;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    FpgSkillSerializedAdapter.SetPayloadKindAndNormalize(
                        serialized,
                        0,
                        0,
                        (int)FpgPlayerSkillPayloadKind.AreaAtFirstSurface),
                    Is.True);
                Assert.That(
                    skill.PayloadSlots[0].Kind,
                    Is.EqualTo(FpgPlayerSkillPayloadKind.AreaAtFirstSurface));
                Assert.That(
                    skill.PayloadSlots[0].TryValidate(out string areaError),
                    Is.True,
                    areaError);
                serialized.UpdateIfRequiredOrScript();
                eventProperty =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        eventKey);
                Assert.That(
                    eventProperty.FindPropertyRelative("targetSource").intValue,
                    Is.EqualTo((int)FpgSkillTargetSource.CurrentAim));
                payloadProperty =
                    FpgSkillSerializedAdapter.GetPayloadProperty(
                        serialized,
                        0,
                        0);
                int combatantLimit = payloadProperty.FindPropertyRelative(
                    "areaCombatantLimit").intValue;
                int projectileLimit = payloadProperty.FindPropertyRelative(
                    "areaProjectileLimit").intValue;
                Assert.That(
                    projectileLimit,
                    Is.LessThanOrEqualTo(int.MaxValue - combatantLimit));
            }
            finally
            {
                Undo.ClearUndo(skill);
                Object.DestroyImmediate(skill);
            }
        }

    }
}
