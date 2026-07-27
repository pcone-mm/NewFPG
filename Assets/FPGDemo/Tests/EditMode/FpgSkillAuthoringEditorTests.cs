using System.Collections.Generic;
using System.Linq;
using FPG.Demo.Editor.SkillAuthoring;
using FPG.Demo.Player;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgSkillAuthoringEditorTests
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
        public void EventKeySeparatesTypedActionsAndPresentationTracks()
        {
            FpgSkillEventKey attack = new FpgSkillEventKey(
                FpgSkillEventTrackKind.GameplayAction,
                FpgSkillActionKind.Attack,
                0);
            FpgSkillEventKey projectile = new FpgSkillEventKey(
                FpgSkillEventTrackKind.GameplayAction,
                FpgSkillActionKind.LaunchProjectile,
                0);
            FpgSkillEventKey firstTrack = new FpgSkillEventKey(
                FpgSkillEventTrackKind.PresentationVfx,
                FpgSkillActionKind.None,
                0,
                0);
            FpgSkillEventKey secondTrack = new FpgSkillEventKey(
                FpgSkillEventTrackKind.PresentationVfx,
                FpgSkillActionKind.None,
                1,
                0);

            Assert.That(
                new HashSet<FpgSkillEventKey>
                {
                    attack,
                    projectile,
                    firstTrack,
                    secondTrack
                },
                Has.Count.EqualTo(4));
            Assert.That(firstTrack.PresentationTrackIndex, Is.Zero);
            Assert.That(secondTrack.PresentationTrackIndex, Is.EqualTo(1));
        }

        [Test]
        public void EventSelectionSupportsPrimaryToggleAndRetention()
        {
            FpgSkillEventSelection selection = new FpgSkillEventSelection();
            FpgSkillEventKey attack = new FpgSkillEventKey(
                FpgSkillEventTrackKind.GameplayAction,
                FpgSkillActionKind.Attack,
                0);
            FpgSkillEventKey shake = new FpgSkillEventKey(
                FpgSkillEventTrackKind.PresentationCameraShake,
                FpgSkillActionKind.None,
                0,
                0);

            selection.SetSingle(attack);
            selection.Add(shake);
            Assert.That(selection.PrimaryEventKey, Is.EqualTo(shake));
            selection.Toggle(attack);
            Assert.That(selection.Items, Is.EqualTo(new[] { shake }));
            selection.Retain(new HashSet<FpgSkillEventKey> { attack });
            Assert.That(selection.Count, Is.Zero);
        }

        [Test]
        public void PresentationTrackCrudPreservesStableIdentityAndOrder()
        {
            WithSkill((skill, serialized) =>
            {
                int first = FpgSkillSerializedAdapter
                    .AddActivePresentationTrack(serialized, 0);
                int second = FpgSkillSerializedAdapter
                    .AddActivePresentationTrack(serialized, 0);
                Assert.That(first, Is.Zero);
                Assert.That(second, Is.EqualTo(1));
                Assert.That(
                    FpgSkillSerializedAdapter.RenameActivePresentationTrack(
                        serialized,
                        0,
                        first,
                        "Cast"),
                    Is.True);
                Assert.That(
                    FpgSkillSerializedAdapter.RenameActivePresentationTrack(
                        serialized,
                        0,
                        second,
                        "Release"),
                    Is.True);

                List<FpgSkillActivePresentationTrackRecord> before =
                    ReadTracks(serialized);
                string castId = before[0].Id;
                string releaseId = before[1].Id;
                Assert.That(castId, Is.Not.EqualTo(releaseId));

                Assert.That(
                    FpgSkillSerializedAdapter.MoveActivePresentationTrack(
                        serialized,
                        0,
                        first,
                        1,
                        out int movedIndex),
                    Is.True);
                Assert.That(movedIndex, Is.EqualTo(1));
                List<FpgSkillActivePresentationTrackRecord> after =
                    ReadTracks(serialized);
                Assert.That(after[0].Id, Is.EqualTo(releaseId));
                Assert.That(after[1].Id, Is.EqualTo(castId));
                Assert.That(after[1].Name, Is.EqualTo("Cast"));

                Assert.That(
                    FpgSkillSerializedAdapter.DeleteActivePresentationTrack(
                        serialized,
                        0,
                        0),
                    Is.True);
                Assert.That(ReadTracks(serialized).Single().Id, Is.EqualTo(castId));
            });
        }

        [Test]
        public void NonEmptyPresentationTrackCannotBeDeleted()
        {
            WithSkill((skill, serialized) =>
            {
                int track = FpgSkillSerializedAdapter
                    .AddActivePresentationTrack(serialized, 0);
                FpgSkillEventKey shake = FpgSkillSerializedAdapter
                    .AddActivePresentationEvent(
                        serialized,
                        0,
                        track,
                        FpgSkillEventTrackKind.PresentationCameraShake,
                        4);

                Assert.That(shake.IsValid, Is.True);
                Assert.That(
                    FpgSkillSerializedAdapter.CanDeleteActivePresentationTrack(
                        FpgSkillSerializedAdapter.GetSequence(serialized, 0),
                        track),
                    Is.False);
                Assert.That(
                    FpgSkillSerializedAdapter.DeleteActivePresentationTrack(
                        serialized,
                        0,
                        track),
                    Is.False);
            });
        }

        [Test]
        public void PresentationTrackAddAndRenameSupportUndoRedo()
        {
            WithSkill((skill, serialized) =>
            {
                FpgSkillSerializedAdapter.AddActivePresentationTrack(
                    serialized,
                    0);
                Assert.That(ReadTracks(serialized), Has.Count.EqualTo(1));

                Undo.PerformUndo();
                serialized.UpdateIfRequiredOrScript();
                Assert.That(ReadTracks(serialized), Is.Empty);
                Undo.PerformRedo();
                serialized.UpdateIfRequiredOrScript();
                Assert.That(ReadTracks(serialized), Has.Count.EqualTo(1));

                Assert.That(
                    FpgSkillSerializedAdapter.RenameActivePresentationTrack(
                        serialized,
                        0,
                        0,
                        "Cast"),
                    Is.True);
                Assert.That(ReadTracks(serialized)[0].Name, Is.EqualTo("Cast"));
                Undo.PerformUndo();
                serialized.UpdateIfRequiredOrScript();
                Assert.That(ReadTracks(serialized)[0].Name, Is.Not.EqualTo("Cast"));
                Undo.PerformRedo();
                serialized.UpdateIfRequiredOrScript();
                Assert.That(ReadTracks(serialized)[0].Name, Is.EqualTo("Cast"));
            });
        }

        [Test]
        public void PresentationEventMovesAcrossTracksWithoutChangingIdentity()
        {
            WithSkill((skill, serialized) =>
            {
                int sourceTrack = FpgSkillSerializedAdapter
                    .AddActivePresentationTrack(serialized, 0);
                int targetTrack = FpgSkillSerializedAdapter
                    .AddActivePresentationTrack(serialized, 0);
                FpgSkillEventKey source = FpgSkillSerializedAdapter
                    .AddActivePresentationEvent(
                        serialized,
                        0,
                        sourceTrack,
                        FpgSkillEventTrackKind.PresentationCameraShake,
                        7);
                SerializedProperty sourceProperty =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        source);
                string eventId = sourceProperty
                    .FindPropertyRelative("eventId").stringValue;
                int ordinal = sourceProperty
                    .FindPropertyRelative("authoredOrdinal").intValue;
                sourceProperty.FindPropertyRelative("presentation")
                    .FindPropertyRelative("strength").floatValue = 0.75f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                FpgSkillEventKey moved = FpgSkillSerializedAdapter
                    .MoveActivePresentationEventToTrack(
                        serialized,
                        0,
                        source,
                        targetTrack);

                Assert.That(moved.IsValid, Is.True);
                Assert.That(moved.PresentationTrackIndex, Is.EqualTo(targetTrack));
                Assert.That(ReadTracks(serialized)[sourceTrack].EventCount, Is.Zero);
                Assert.That(ReadTracks(serialized)[targetTrack].EventCount, Is.EqualTo(1));
                SerializedProperty movedProperty =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        moved);
                Assert.That(
                    movedProperty.FindPropertyRelative("eventId").stringValue,
                    Is.EqualTo(eventId));
                Assert.That(
                    movedProperty.FindPropertyRelative("authoredOrdinal").intValue,
                    Is.EqualTo(ordinal));
                Assert.That(
                    movedProperty.FindPropertyRelative("presentation")
                        .FindPropertyRelative("strength").floatValue,
                    Is.EqualTo(0.75f).Within(0.001f));
            });
        }

        [Test]
        public void PresentationClipboardPreservesTypedDataAndCreatesNewIds()
        {
            WithSkill((skill, serialized) =>
            {
                int track = FpgSkillSerializedAdapter
                    .AddActivePresentationTrack(serialized, 0);
                FpgSkillEventKey vfx = AddPresentation(
                    serialized,
                    track,
                    FpgSkillEventTrackKind.PresentationVfx,
                    2);
                FpgSkillEventKey audio = AddPresentation(
                    serialized,
                    track,
                    FpgSkillEventTrackKind.PresentationAudio,
                    4);
                FpgSkillEventKey shake = AddPresentation(
                    serialized,
                    track,
                    FpgSkillEventTrackKind.PresentationCameraShake,
                    6);
                GetPresentation(serialized, vfx)
                    .FindPropertyRelative("scale").vector3Value =
                    new Vector3(2f, 3f, 4f);
                GetPresentation(serialized, audio)
                    .FindPropertyRelative("volume").floatValue = 0.35f;
                GetPresentation(serialized, shake)
                    .FindPropertyRelative("strength").floatValue = 0.8f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                string[] sourceIds = new[] { vfx, audio, shake }
                    .Select(key => GetEventId(serialized, key))
                    .ToArray();

                FpgSkillEventClipboard clipboard =
                    new FpgSkillEventClipboard();
                Assert.That(
                    FpgSkillSerializedAdapter.CopyEvents(
                        serialized,
                        0,
                        new[] { vfx, audio, shake },
                        clipboard),
                    Is.True);
                List<FpgSkillEventKey> pasted =
                    FpgSkillSerializedAdapter.PasteEvents(
                        serialized,
                        0,
                        clipboard,
                        12);

                Assert.That(pasted, Has.Count.EqualTo(3));
                Assert.That(
                    pasted.Select(key => key.PresentationTrackIndex),
                    Is.All.EqualTo(track));
                foreach (FpgSkillEventKey pastedKey in pasted)
                {
                    Assert.That(
                        sourceIds,
                        Does.Not.Contain(GetEventId(serialized, pastedKey)));
                }
                Assert.That(
                    GetPresentation(serialized, pasted[0])
                        .FindPropertyRelative("scale").vector3Value,
                    Is.EqualTo(new Vector3(2f, 3f, 4f)));
                Assert.That(
                    GetPresentation(serialized, pasted[1])
                        .FindPropertyRelative("volume").floatValue,
                    Is.EqualTo(0.35f).Within(0.001f));
                Assert.That(
                    GetPresentation(serialized, pasted[2])
                        .FindPropertyRelative("strength").floatValue,
                    Is.EqualTo(0.8f).Within(0.001f));
            });
        }

        [Test]
        public void TypedActionsUseDistinctArraysAndDuplicateWithNewIdentity()
        {
            WithSkill((skill, serialized) =>
            {
                FpgSkillEventKey attack = FpgSkillSerializedAdapter.AddAction(
                    serialized,
                    0,
                    2,
                    FpgSkillActionKind.Attack,
                    (int)FpgSkillAttackMode.PelletRays);
                FpgSkillEventKey projectile =
                    FpgSkillSerializedAdapter.AddAction(
                        serialized,
                        0,
                        4,
                        FpgSkillActionKind.LaunchProjectile,
                        (int)FpgSkillProjectileImpactMode.AreaAtFirstSurface);
                FpgSkillEventKey reload = FpgSkillSerializedAdapter.AddAction(
                    serialized,
                    0,
                    6,
                    FpgSkillActionKind.CommitReload);
                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(serialized, 0);

                Assert.That(attack.IsValid && projectile.IsValid && reload.IsValid);
                Assert.That(sequence.FindPropertyRelative("attackEvents").arraySize, Is.EqualTo(1));
                Assert.That(sequence.FindPropertyRelative("projectileEvents").arraySize, Is.EqualTo(1));
                Assert.That(sequence.FindPropertyRelative("reloadEvents").arraySize, Is.EqualTo(1));
                Assert.That(sequence.FindPropertyRelative("logicEvents"), Is.Null);
                Assert.That(serialized.FindProperty("payloadSlots"), Is.Null);

                SerializedProperty source =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        projectile);
                source.FindPropertyRelative("projectileCount").intValue = 3;
                SerializedProperty flightVfx =
                    source.FindPropertyRelative("flightVfx");
                flightVfx.managedReferenceValue =
                    new FpgVfxPresentationDefinition();
                flightVfx
                    .FindPropertyRelative("scale").vector3Value =
                    new Vector3(1.5f, 2f, 1f);
                string sourceId = source.FindPropertyRelative("eventId")
                    .stringValue;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                FpgSkillEventKey duplicate =
                    FpgSkillSerializedAdapter.DuplicateEvent(
                        serialized,
                        0,
                        projectile,
                        30);
                SerializedProperty copy =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        duplicate);
                Assert.That(duplicate.ActionKind, Is.EqualTo(FpgSkillActionKind.LaunchProjectile));
                Assert.That(copy.FindPropertyRelative("eventId").stringValue, Is.Not.EqualTo(sourceId));
                Assert.That(copy.FindPropertyRelative("projectileCount").intValue, Is.EqualTo(3));
                Assert.That(
                    copy.FindPropertyRelative("flightVfx")
                        .FindPropertyRelative("scale").vector3Value,
                    Is.EqualTo(new Vector3(1.5f, 2f, 1f)));
            });
        }

        [Test]
        public void PresentationBindingRejectsEarlyOrMissingActionAndAllowsDelay()
        {
            WithSkill((skill, serialized) =>
            {
                FpgSkillEventKey attack = FpgSkillSerializedAdapter.AddAction(
                    serialized,
                    0,
                    5,
                    FpgSkillActionKind.Attack,
                    (int)FpgSkillAttackMode.PelletRays);
                int track = FpgSkillSerializedAdapter
                    .AddActivePresentationTrack(serialized, 0);
                FpgSkillEventKey shake = AddPresentation(
                    serialized,
                    track,
                    FpgSkillEventTrackKind.PresentationCameraShake,
                    4);
                SerializedProperty binding =
                    FpgSkillSerializedAdapter.GetEventProperty(
                            serialized,
                            0,
                            shake)
                        .FindPropertyRelative("boundGameplayEventId");
                binding.stringValue = GetEventId(serialized, attack);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(HasValidationError(serialized, shake), Is.True);
                Assert.That(
                    FpgSkillSerializedAdapter.SetEventTick(
                        serialized,
                        0,
                        shake,
                        7),
                    Is.True);
                Assert.That(HasValidationError(serialized, shake), Is.False);

                binding = FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        shake)
                    .FindPropertyRelative("boundGameplayEventId");
                binding.stringValue = "action.missing";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(HasValidationError(serialized, shake), Is.True);
            });
        }

        [Test]
        public void TrackOrganizationDoesNotChangeExecutionOrderOrGameplayHash()
        {
            WithSkill((skill, serialized) =>
            {
                FpgSkillSerializedAdapter.AddAction(
                    serialized,
                    0,
                    3,
                    FpgSkillActionKind.Attack,
                    (int)FpgSkillAttackMode.PelletRays);
                int firstTrack = FpgSkillSerializedAdapter
                    .AddActivePresentationTrack(serialized, 0);
                int secondTrack = FpgSkillSerializedAdapter
                    .AddActivePresentationTrack(serialized, 0);
                FpgSkillEventKey first = AddPresentation(
                    serialized,
                    firstTrack,
                    FpgSkillEventTrackKind.PresentationCameraShake,
                    9);
                FpgSkillEventKey second = AddPresentation(
                    serialized,
                    secondTrack,
                    FpgSkillEventTrackKind.PresentationCameraShake,
                    6);
                GetPresentation(serialized, first)
                    .FindPropertyRelative("strength").floatValue = 0.25f;
                GetPresentation(serialized, second)
                    .FindPropertyRelative("strength").floatValue = 0.5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                string firstEventId = GetEventId(serialized, first);
                string[] executionOrder = ReadEvents(serialized)
                    .Select(item => item.EventId)
                    .ToArray();
                FpgCompiledPlayerSkillDefinition before = Compile(skill);

                Assert.That(
                    FpgSkillSerializedAdapter.RenameActivePresentationTrack(
                        serialized,
                        0,
                        firstTrack,
                        "Late"),
                    Is.True);
                Assert.That(
                    FpgSkillSerializedAdapter.MoveActivePresentationTrack(
                        serialized,
                        0,
                        firstTrack,
                        1,
                        out _),
                    Is.True);
                FpgCompiledPlayerSkillDefinition reorganized = Compile(skill);

                Assert.That(
                    ReadEvents(serialized).Select(item => item.EventId),
                    Is.EqualTo(executionOrder));
                Assert.That(reorganized.GameplayHash, Is.EqualTo(before.GameplayHash));
                Assert.That(reorganized.PresentationHash, Is.EqualTo(before.PresentationHash));

                FpgSkillEventRecord movedFirst = ReadEvents(serialized)
                    .Single(item => item.EventId == firstEventId);
                GetPresentation(serialized, movedFirst.Key)
                    .FindPropertyRelative("strength").floatValue = 0.9f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                FpgCompiledPlayerSkillDefinition changed = Compile(skill);
                Assert.That(changed.GameplayHash, Is.EqualTo(before.GameplayHash));
                Assert.That(changed.PresentationHash, Is.Not.EqualTo(before.PresentationHash));
            });
        }

        private static FpgSkillEventKey AddPresentation(
            SerializedObject serialized,
            int trackIndex,
            FpgSkillEventTrackKind track,
            int tick)
        {
            FpgSkillEventKey key = FpgSkillSerializedAdapter
                .AddActivePresentationEvent(
                    serialized,
                    0,
                    trackIndex,
                    track,
                    tick);
            Assert.That(key.IsValid, Is.True);
            return key;
        }

        private static SerializedProperty GetPresentation(
            SerializedObject serialized,
            FpgSkillEventKey key)
        {
            return FpgSkillSerializedAdapter.GetEventProperty(
                    serialized,
                    0,
                    key)
                .FindPropertyRelative("presentation");
        }

        private static string GetEventId(
            SerializedObject serialized,
            FpgSkillEventKey key)
        {
            serialized.UpdateIfRequiredOrScript();
            return FpgSkillSerializedAdapter.GetEventProperty(
                    serialized,
                    0,
                    key)
                .FindPropertyRelative("eventId").stringValue;
        }

        private static List<FpgSkillActivePresentationTrackRecord> ReadTracks(
            SerializedObject serialized)
        {
            serialized.UpdateIfRequiredOrScript();
            return FpgSkillSerializedAdapter.ReadActivePresentationTracks(
                FpgSkillSerializedAdapter.GetSequence(serialized, 0));
        }

        private static List<FpgSkillEventRecord> ReadEvents(
            SerializedObject serialized)
        {
            serialized.UpdateIfRequiredOrScript();
            SerializedProperty sequence =
                FpgSkillSerializedAdapter.GetSequence(serialized, 0);
            return FpgSkillSerializedAdapter.ReadEvents(
                sequence,
                FpgSkillSerializedAdapter.GetDurationTicks(sequence));
        }

        private static bool HasValidationError(
            SerializedObject serialized,
            FpgSkillEventKey key)
        {
            List<FpgSkillEventRecord> events = ReadEvents(serialized);
            return FpgSkillSerializedAdapter.Validate(
                    serialized,
                    0,
                    events,
                    30,
                    includeRuntimeValidation: false)
                .Any(item =>
                    item.EventKey == key
                    && item.Severity == FpgSkillIssueSeverity.Error);
        }

        private static FpgCompiledPlayerSkillDefinition Compile(
            FpgPlayerSkillDefinition skill)
        {
            Assert.That(
                skill.TryCompile(
                    out FpgCompiledPlayerSkillDefinition compiled,
                    out string error),
                Is.True,
                error);
            return compiled;
        }

        private static void WithSkill(
            System.Action<FpgPlayerSkillDefinition, SerializedObject> action)
        {
            FpgPlayerSkillDefinition skill = CreateSkill();
            try
            {
                action(skill, new SerializedObject(skill));
            }
            finally
            {
                Undo.ClearUndo(skill);
                Object.DestroyImmediate(skill);
            }
        }

        private static FpgPlayerSkillDefinition CreateSkill()
        {
            FpgPlayerSkillDefinition skill =
                ScriptableObject.CreateInstance<FpgPlayerSkillDefinition>();
            SerializedObject serialized = new SerializedObject(skill);
            serialized.FindProperty("skillId").stringValue =
                "player.editor.v3.test";
            serialized.FindProperty("displayName").stringValue =
                "Editor V3 Test";
            serialized.FindProperty("authoringSchemaVersion").intValue =
                FpgSkillTimelineDefinition.CurrentAuthoringSchemaVersion;
            serialized.FindProperty("secondaryTriggerMode").enumValueIndex =
                (int)SecondaryTriggerMode.ImmediateRepeatWhileHeld;
            SerializedProperty sequences = serialized.FindProperty("sequences");
            sequences.arraySize = 1;
            SerializedProperty sequence = sequences.GetArrayElementAtIndex(0);
            sequence.FindPropertyRelative("kind").enumValueIndex =
                (int)FpgSkillSequenceKind.Execute;
            sequence.FindPropertyRelative("durationTicks").intValue = 30;
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
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return skill;
        }
    }
}
