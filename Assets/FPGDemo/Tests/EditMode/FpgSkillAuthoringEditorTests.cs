using System;
using System.Collections.Generic;
using System.Linq;
using FPG.Demo.Editor.SkillAuthoring;
using FPG.Demo.Core;
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
        public void EventSelectionSupportsAddTogglePrimaryAndRetention()
        {
            FpgSkillEventSelection selection = new FpgSkillEventSelection();

            selection.SetSingle(10);
            selection.Add(20);
            selection.Add(30);
            Assert.That(selection.Items, Is.EqualTo(new[] { 10, 20, 30 }));
            Assert.That(selection.PrimaryEventIndex, Is.EqualTo(30));

            selection.Toggle(20);
            Assert.That(selection.Items, Is.EqualTo(new[] { 10, 30 }));
            selection.MakePrimary(10);
            Assert.That(selection.PrimaryEventIndex, Is.EqualTo(10));

            selection.Retain(new HashSet<int> { 30 });
            Assert.That(selection.Items, Is.EqualTo(new[] { 30 }));
            Assert.That(selection.PrimaryEventIndex, Is.EqualTo(30));
        }

        [Test]
        public void TimelineDragCreateNormalizesRangeAndTrackSemantics()
        {
            FpgSkillTimelineView timeline = new FpgSkillTimelineView();
            timeline.SetModel(20, Array.Empty<FpgSkillTimelineEventViewModel>());
            List<FpgSkillTimelineCreateRequest> requests =
                new List<FpgSkillTimelineCreateRequest>();
            timeline.EventCreateRequested += requests.Add;

            timeline.RequestCreateFromDrag(
                FpgSkillEventTrackKind.Warning,
                14,
                9);
            timeline.RequestCreateFromDrag(
                FpgSkillEventTrackKind.Logic,
                4,
                11);

            Assert.That(requests.Count, Is.EqualTo(2));
            Assert.That(requests[0].Track, Is.EqualTo(FpgSkillEventTrackKind.Warning));
            Assert.That(requests[0].Tick, Is.EqualTo(9));
            Assert.That(requests[0].DurationTicks, Is.EqualTo(5));
            Assert.That(requests[1].Track, Is.EqualTo(FpgSkillEventTrackKind.Logic));
            Assert.That(requests[1].Tick, Is.EqualTo(4));
            Assert.That(requests[1].DurationTicks, Is.Zero);
        }

        [Test]
        public void TimelineScrubDragUpdatesPlayheadContinuouslyAndClamps()
        {
            FpgSkillTimelineView timeline = new FpgSkillTimelineView();
            timeline.SetModel(
                20,
                Array.Empty<FpgSkillTimelineEventViewModel>());
            List<int> sampledTicks = new List<int>();
            timeline.PlayheadChanged += sampledTicks.Add;

            timeline.BeginScrubAtTick(7, 5);
            timeline.ContinueScrubAtTick(7, 12);
            timeline.ContinueScrubAtTick(7, 99);
            timeline.ContinueScrubAtTick(7, -10);
            timeline.EndScrub(7);
            timeline.ContinueScrubAtTick(7, 8);

            Assert.That(timeline.PlayheadTick, Is.Zero);
            CollectionAssert.AreEqual(
                new[] { 5, 12, 20, 0 },
                sampledTicks);
        }


        [Test]
        public void CopyPastePreservesRelativeTimelineDataAndRegeneratesEventIds()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(30, 2, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence = serialized.FindProperty("sequences")
                    .GetArrayElementAtIndex(0);
                ConfigureLogicEvent(
                    sequence,
                    0,
                    "event.logic.source",
                    2,
                    3,
                    "payload.a");
                ConfigureCue(
                    sequence,
                    0,
                    "event.cue.source",
                    5,
                    5,
                    "cue.original");
                serialized.ApplyModifiedPropertiesWithoutUndo();

                List<FpgSkillPayloadRecord> payloads =
                    FpgSkillSerializedAdapter.ReadPayloads(sequence);
                List<FpgSkillEventRecord> sourceEvents =
                    FpgSkillSerializedAdapter.ReadEvents(sequence, payloads, 30);
                int[] sourceKeys = sourceEvents
                    .Where(item => item.EventId.EndsWith(".source", StringComparison.Ordinal))
                    .Select(item => item.Index)
                    .ToArray();
                FpgSkillEventClipboard clipboard = new FpgSkillEventClipboard();

                Assert.That(
                    FpgSkillSerializedAdapter.CopyEvents(
                        serialized,
                        0,
                        sourceKeys,
                        clipboard),
                    Is.True);
                Assert.That(clipboard.Count, Is.EqualTo(2));

                sequence.FindPropertyRelative("presentationCues")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("cueId").stringValue = "cue.changed";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                List<int> pastedKeys = FpgSkillSerializedAdapter.PasteEvents(
                    serialized,
                    0,
                    clipboard,
                    10);
                Assert.That(pastedKeys.Count, Is.EqualTo(2));

                serialized.UpdateIfRequiredOrScript();
                sequence = serialized.FindProperty("sequences")
                    .GetArrayElementAtIndex(0);
                payloads = FpgSkillSerializedAdapter.ReadPayloads(sequence);
                List<FpgSkillEventRecord> allEvents =
                    FpgSkillSerializedAdapter.ReadEvents(sequence, payloads, 30);
                List<FpgSkillEventRecord> pasted = pastedKeys
                    .Select(key => allEvents.Single(item => item.Index == key))
                    .OrderBy(item => item.Tick)
                    .ToList();

                CollectionAssert.AreEqual(new[] { 10, 13 }, pasted.Select(item => item.Tick));
                CollectionAssert.AreEqual(
                    new[] { FpgSkillEventTrackKind.Logic, FpgSkillEventTrackKind.Presentation },
                    pasted.Select(item => item.Track));
                Assert.That(
                    pasted[1].AuthoredOrdinal - pasted[0].AuthoredOrdinal,
                    Is.EqualTo(2));
                Assert.That(
                    pasted.Select(item => item.EventId).Distinct().Count(),
                    Is.EqualTo(2));
                Assert.That(
                    pasted.All(item => sourceEvents.All(source =>
                        !string.Equals(source.EventId, item.EventId, StringComparison.Ordinal))),
                    Is.True);

                FpgSkillEventRecord pastedCue = pasted.Single(item =>
                    item.Track == FpgSkillEventTrackKind.Presentation);
                SerializedProperty pastedCueProperty =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        pastedCue.Index);
                Assert.That(
                    pastedCueProperty.FindPropertyRelative("cueId").stringValue,
                    Is.EqualTo("cue.original"));
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void BatchMoveClampsAtSequenceBoundsAndKeepsRelativeOffsets()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(12, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence = serialized.FindProperty("sequences")
                    .GetArrayElementAtIndex(0);
                ConfigureLogicEvent(
                    sequence,
                    0,
                    "event.logic",
                    3,
                    0,
                    "payload.a");
                ConfigureWarning(
                    sequence,
                    0,
                    "event.warning",
                    7,
                    9,
                    1);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                List<FpgSkillEventRecord> initial = ReadEvents(serialized, 0);
                int[] keys = initial.Select(item => item.Index).ToArray();
                Assert.That(
                    FpgSkillSerializedAdapter.MoveEventsByDelta(
                        serialized,
                        0,
                        keys,
                        10,
                        out int forwardDelta),
                    Is.True);
                Assert.That(forwardDelta, Is.EqualTo(3));

                List<FpgSkillEventRecord> forward = ReadEvents(serialized, 0);
                Assert.That(
                    forward.Single(item => item.Track == FpgSkillEventTrackKind.Logic).Tick,
                    Is.EqualTo(6));
                FpgSkillEventRecord warning = forward.Single(item =>
                    item.Track == FpgSkillEventTrackKind.Warning);
                Assert.That(warning.Tick, Is.EqualTo(10));
                Assert.That(warning.DurationTicks, Is.EqualTo(2));

                Assert.That(
                    FpgSkillSerializedAdapter.MoveEventsByDelta(
                        serialized,
                        0,
                        keys,
                        -20,
                        out int backwardDelta),
                    Is.True);
                Assert.That(backwardDelta, Is.EqualTo(-6));
                List<FpgSkillEventRecord> backward = ReadEvents(serialized, 0);
                Assert.That(
                    backward.Single(item => item.Track == FpgSkillEventTrackKind.Logic).Tick,
                    Is.Zero);
                warning = backward.Single(item =>
                    item.Track == FpgSkillEventTrackKind.Warning);
                Assert.That(warning.Tick, Is.EqualTo(4));
                Assert.That(warning.DurationTicks, Is.EqualTo(2));
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void TimelineBlockMoveExtendsAnimationSequenceAndSupportsUndoRedo()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(10, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(serialized, 0);
                sequence.FindPropertyRelative("animationStartTick")
                    .intValue = 2;
                sequence.FindPropertyRelative("animationEndTick")
                    .intValue = 7;
                ConfigurePhase(
                    sequence,
                    0,
                    "phase.test",
                    FpgSkillPhaseKind.Active,
                    1,
                    4);
                ConfigureLogicEvent(
                    sequence,
                    0,
                    "event.stays",
                    4,
                    0,
                    "payload.a");
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    FpgSkillSerializedAdapter.MoveTimelineBlockByDelta(
                        serialized,
                        0,
                        FpgSkillTimelineBlockKind.Animation,
                        0,
                        99,
                        out int animationDelta),
                    Is.True);
                Assert.That(animationDelta, Is.EqualTo(99));
                AssertTimelineRange(serialized, "animationStartTick",
                    "animationEndTick", 101, 106);
                AssertSequenceDurationAndLogicTick(serialized, 106, 4);

                Undo.PerformUndo();
                serialized.UpdateIfRequiredOrScript();
                AssertTimelineRange(serialized, "animationStartTick",
                    "animationEndTick", 2, 7);
                AssertSequenceDurationAndLogicTick(serialized, 10, 4);

                Undo.PerformRedo();
                serialized.UpdateIfRequiredOrScript();
                AssertTimelineRange(serialized, "animationStartTick",
                    "animationEndTick", 101, 106);
                AssertSequenceDurationAndLogicTick(serialized, 106, 4);

                Assert.That(
                    FpgSkillSerializedAdapter.MoveTimelineBlockByDelta(
                        serialized,
                        0,
                        FpgSkillTimelineBlockKind.Phase,
                        0,
                        -99,
                        out int phaseDelta),
                    Is.True);
                Assert.That(phaseDelta, Is.EqualTo(-1));
                SerializedProperty phase =
                    FpgSkillSerializedAdapter.GetPhaseProperty(
                        serialized,
                        0,
                        0);
                Assert.That(
                    phase.FindPropertyRelative("startTick").intValue,
                    Is.Zero);
                Assert.That(
                    phase.FindPropertyRelative("endTick").intValue,
                    Is.EqualTo(3));
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void PhaseResizeClampsToNeighborsAllowsZeroAndSupportsUndo()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(12, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(serialized, 0);
                ConfigurePhase(
                    sequence,
                    0,
                    "phase.startup",
                    FpgSkillPhaseKind.Startup,
                    0,
                    2);
                ConfigurePhase(
                    sequence,
                    1,
                    "phase.active",
                    FpgSkillPhaseKind.Active,
                    4,
                    8);
                ConfigurePhase(
                    sequence,
                    2,
                    "phase.recovery",
                    FpgSkillPhaseKind.Recovery,
                    10,
                    12);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    FpgSkillSerializedAdapter.EditTimelineBlockRange(
                        serialized,
                        0,
                        FpgSkillTimelineBlockKind.Phase,
                        1,
                        FpgSkillTimelineBlockEditMode.ResizeStart,
                        0,
                        8,
                        out int leftStart,
                        out int leftEnd),
                    Is.True);
                Assert.That(leftStart, Is.EqualTo(2));
                Assert.That(leftEnd, Is.EqualTo(8));

                Undo.IncrementCurrentGroup();
                Assert.That(
                    FpgSkillSerializedAdapter.EditTimelineBlockRange(
                        serialized,
                        0,
                        FpgSkillTimelineBlockKind.Phase,
                        1,
                        FpgSkillTimelineBlockEditMode.ResizeEnd,
                        2,
                        99,
                        out int rightStart,
                        out int rightEnd),
                    Is.True);
                Assert.That(rightStart, Is.EqualTo(2));
                Assert.That(rightEnd, Is.EqualTo(10));
                AssertPhaseRange(serialized, 1, 2, 10);

                Undo.PerformUndo();
                serialized.UpdateIfRequiredOrScript();
                AssertPhaseRange(serialized, 1, 2, 8);
                Undo.PerformUndo();
                serialized.UpdateIfRequiredOrScript();
                AssertPhaseRange(serialized, 1, 4, 8);

                Assert.That(
                    FpgSkillSerializedAdapter.EditTimelineBlockRange(
                        serialized,
                        0,
                        FpgSkillTimelineBlockKind.Phase,
                        1,
                        FpgSkillTimelineBlockEditMode.ResizeStart,
                        8,
                        8,
                        out int zeroStart,
                        out int zeroEnd),
                    Is.True);
                Assert.That(zeroStart, Is.EqualTo(8));
                Assert.That(zeroEnd, Is.EqualTo(8));
                AssertPhaseRange(serialized, 1, 8, 8);
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }


        [Test]
        public void FitIntervalSupportsBothResizeEdgesAndSequenceExtension()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(10, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(serialized, 0);
                sequence.FindPropertyRelative("animationStartTick")
                    .intValue = 2;
                sequence.FindPropertyRelative("animationEndTick")
                    .intValue = 7;
                sequence.FindPropertyRelative(
                    "animationPlaybackMode").enumValueIndex =
                    (int)FpgSkillAnimationPlaybackMode.FitInterval;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    FpgSkillSerializedAdapter.EditTimelineBlockRange(
                        serialized,
                        0,
                        FpgSkillTimelineBlockKind.Animation,
                        0,
                        FpgSkillTimelineBlockEditMode.ResizeStart,
                        -20,
                        7,
                        out int leftStart,
                        out int leftEnd),
                    Is.True);
                Assert.That(leftStart, Is.Zero);
                Assert.That(leftEnd, Is.EqualTo(7));

                Assert.That(
                    FpgSkillSerializedAdapter.EditTimelineBlockRange(
                        serialized,
                        0,
                        FpgSkillTimelineBlockKind.Animation,
                        0,
                        FpgSkillTimelineBlockEditMode.ResizeEnd,
                        0,
                        15,
                        out int rightStart,
                        out int rightEnd),
                    Is.True);
                Assert.That(rightStart, Is.Zero);
                Assert.That(rightEnd, Is.EqualTo(15));
                AssertTimelineRange(serialized, "animationStartTick",
                    "animationEndTick", 0, 15);
                AssertSequenceDurationAndLogicTick(serialized, 15, -1);
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }


        [Test]
        public void NaturalSpeedRejectsResizeButMoveCanExtendSequence()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(10, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(serialized, 0);
                sequence.FindPropertyRelative("animationStartTick")
                    .intValue = 2;
                sequence.FindPropertyRelative("animationEndTick")
                    .intValue = 7;
                sequence.FindPropertyRelative(
                    "animationPlaybackMode").enumValueIndex =
                    (int)FpgSkillAnimationPlaybackMode.NaturalSpeed;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                FpgSkillTimelineBlockViewModel naturalBlock =
                    FpgSkillSerializedAdapter.ReadTimelineBlocks(
                            sequence,
                            10,
                            5)
                        .Single(item =>
                            item.Kind
                                == FpgSkillTimelineBlockKind.Animation);
                Assert.That(naturalBlock.CanResize, Is.False);
                Assert.That(naturalBlock.AllowSequenceExtension, Is.True);

                Assert.That(
                    FpgSkillSerializedAdapter.EditTimelineBlockRange(
                        serialized,
                        0,
                        FpgSkillTimelineBlockKind.Animation,
                        0,
                        FpgSkillTimelineBlockEditMode.ResizeEnd,
                        2,
                        12,
                        out int rejectedStart,
                        out int rejectedEnd),
                    Is.False);
                Assert.That(rejectedStart, Is.EqualTo(2));
                Assert.That(rejectedEnd, Is.EqualTo(7));
                AssertTimelineRange(serialized, "animationStartTick",
                    "animationEndTick", 2, 7);

                Assert.That(
                    FpgSkillSerializedAdapter.EditTimelineBlockRange(
                        serialized,
                        0,
                        FpgSkillTimelineBlockKind.Animation,
                        0,
                        FpgSkillTimelineBlockEditMode.Move,
                        8,
                        13,
                        out int movedStart,
                        out int movedEnd),
                    Is.True);
                Assert.That(movedStart, Is.EqualTo(8));
                Assert.That(movedEnd, Is.EqualTo(13));
                AssertTimelineRange(serialized, "animationStartTick",
                    "animationEndTick", 8, 13);
                AssertSequenceDurationAndLogicTick(serialized, 13, -1);
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }


        [Test]
        public void AnimationResizeMaterializesLegacyEndAndKeepsEventTicks()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(10, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(serialized, 0);
                sequence.FindPropertyRelative(
                    "animationPlaybackMode").enumValueIndex =
                    (int)FpgSkillAnimationPlaybackMode.FitInterval;
                sequence.FindPropertyRelative(
                    "animationEndTick").intValue = 0;
                ConfigureLogicEvent(
                    sequence,
                    0,
                    "event.stays",
                    4,
                    0,
                    "payload.a");
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    FpgSkillSerializedAdapter.EditTimelineBlockRange(
                        serialized,
                        0,
                        FpgSkillTimelineBlockKind.Animation,
                        0,
                        FpgSkillTimelineBlockEditMode.ResizeEnd,
                        0,
                        14,
                        out int appliedStart,
                        out int appliedEnd),
                    Is.True);
                Assert.That(appliedStart, Is.Zero);
                Assert.That(appliedEnd, Is.EqualTo(14));
                AssertTimelineRange(serialized, "animationStartTick",
                    "animationEndTick", 0, 14);
                AssertSequenceDurationAndLogicTick(serialized, 14, 4);

                Undo.PerformUndo();
                serialized.UpdateIfRequiredOrScript();
                AssertTimelineRange(serialized, "animationStartTick",
                    "animationEndTick", 0, 0);
                AssertSequenceDurationAndLogicTick(serialized, 10, 4);
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void NaturalSpeedViewAndMoveUseCompleteSourceClip()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(11, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(serialized, 0);
                sequence.FindPropertyRelative("mainAnimation").stringValue =
                    "attack_play1";
                sequence.FindPropertyRelative(
                    "animationPlaybackMode").enumValueIndex =
                    (int)FpgSkillAnimationPlaybackMode.NaturalSpeed;
                sequence.FindPropertyRelative("loop").boolValue = false;
                sequence.FindPropertyRelative(
                    "animationStartTick").intValue = 0;
                sequence.FindPropertyRelative(
                    "animationEndTick").intValue = 11;
                sequence.FindPropertyRelative(
                    "sourceAnimationDurationTicks").intValue = 16;
                ConfigureLogicEvent(
                    sequence,
                    0,
                    "event.primary",
                    0,
                    0,
                    "payload.a");
                serialized.ApplyModifiedPropertiesWithoutUndo();

                List<FpgSkillTimelineBlockViewModel> blocks =
                    FpgSkillSerializedAdapter.ReadTimelineBlocks(
                        sequence,
                        11);
                FpgSkillTimelineBlockViewModel animation =
                    blocks.Single(item =>
                        item.Kind == FpgSkillTimelineBlockKind.Animation);
                Assert.That(animation.StartTick, Is.Zero);
                Assert.That(animation.EndTick, Is.EqualTo(16));
                Assert.That(animation.Label, Does.Contain("attack_play1"));
                Assert.That(animation.Label, Does.Contain("16帧@60Hz"));
                Assert.That(animation.Label, Does.Not.Contain("区间"));
                Assert.That(
                    animation.Tooltip,
                    Does.Contain("完整片段 Tick 0-16"));
                Assert.That(
                    animation.Tooltip,
                    Does.Contain("当前序列截止 Tick 11"));
                Assert.That(animation.IsInvalid, Is.True);
                Assert.That(animation.CanResize, Is.False);

                List<FpgSkillPayloadRecord> payloads =
                    FpgSkillSerializedAdapter.ReadPayloads(sequence);
                List<FpgSkillEventRecord> authoredEvents =
                    ReadEvents(serialized, 0);
                List<FpgSkillValidationItem> validation =
                    FpgSkillSerializedAdapter.Validate(
                        serialized,
                        0,
                        payloads,
                        authoredEvents,
                        11);
                Assert.That(
                    validation.Any(item =>
                        item.Severity == FpgSkillIssueSeverity.Warning
                        && item.Message.Contains("完整片段")
                        && item.Message.Contains("超出当前序列")),
                    Is.True);

                Assert.That(
                    FpgSkillSerializedAdapter.EditTimelineBlockRange(
                        serialized,
                        0,
                        FpgSkillTimelineBlockKind.Animation,
                        0,
                        FpgSkillTimelineBlockEditMode.Move,
                        2,
                        18,
                        out int movedStart,
                        out int movedEnd),
                    Is.True);
                Assert.That(movedStart, Is.EqualTo(2));
                Assert.That(movedEnd, Is.EqualTo(18));
                AssertTimelineRange(serialized, "animationStartTick",
                    "animationEndTick", 2, 18);
                AssertSequenceDurationAndLogicTick(serialized, 18, 0);
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }




        [Test]
        public void PayloadDeletionIsProtectedUntilReferencesAreReplaced()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(20, 2, 2);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequences = serialized.FindProperty("sequences");
                ConfigureLogicEvent(
                    sequences.GetArrayElementAtIndex(0),
                    0,
                    "event.first",
                    2,
                    0,
                    "payload.a");
                ConfigureLogicEvent(
                    sequences.GetArrayElementAtIndex(1),
                    0,
                    "event.second",
                    4,
                    0,
                    "payload.a");
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    FpgSkillSerializedAdapter.CanDeletePayload(
                        serialized,
                        0,
                        0,
                        out int references),
                    Is.False);
                Assert.That(references, Is.EqualTo(2));
                Assert.That(
                    FpgSkillSerializedAdapter.DeletePayload(serialized, 0, 0),
                    Is.False);

                Assert.That(
                    FpgSkillSerializedAdapter.ReplacePayloadReferences(
                        serialized,
                        0,
                        0,
                        1),
                    Is.EqualTo(2));
                Assert.That(
                    FpgSkillSerializedAdapter.CanDeletePayload(
                        serialized,
                        0,
                        0,
                        out references),
                    Is.True);
                Assert.That(references, Is.Zero);
                Assert.That(
                    FpgSkillSerializedAdapter.DeletePayload(serialized, 0, 0),
                    Is.True);

                serialized.UpdateIfRequiredOrScript();
                Assert.That(serialized.FindProperty("payloadSlots").arraySize, Is.EqualTo(1));
                Assert.That(
                    serialized.FindProperty("payloadSlots")
                        .GetArrayElementAtIndex(0)
                        .FindPropertyRelative("slotId").stringValue,
                    Is.EqualTo("payload.b"));
                for (int index = 0; index < 2; index++)
                {
                    Assert.That(
                        serialized.FindProperty("sequences")
                            .GetArrayElementAtIndex(index)
                            .FindPropertyRelative("logicEvents")
                            .GetArrayElementAtIndex(0)
                            .FindPropertyRelative("payloadSlotId").stringValue,
                        Is.EqualTo("payload.b"));
                }
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void SerializedAdapterMutationSupportsUndoAndRedo()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(20, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence = FpgSkillSerializedAdapter.GetSequence(
                    serialized,
                    0);
                FpgSkillPayloadRecord payload =
                    FpgSkillSerializedAdapter.ReadPayloads(sequence)[0];

                int eventIndex = FpgSkillSerializedAdapter.AddEvent(
                    serialized,
                    0,
                    6,
                    payload,
                    FpgSkillEventTrackKind.Logic);
                Assert.That(eventIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(
                    serialized.FindProperty("sequences")
                        .GetArrayElementAtIndex(0)
                        .FindPropertyRelative("logicEvents").arraySize,
                    Is.EqualTo(1));

                Undo.PerformUndo();
                serialized.UpdateIfRequiredOrScript();
                Assert.That(
                    serialized.FindProperty("sequences")
                        .GetArrayElementAtIndex(0)
                        .FindPropertyRelative("logicEvents").arraySize,
                    Is.Zero);

                Undo.PerformRedo();
                serialized.UpdateIfRequiredOrScript();
                SerializedProperty events = serialized.FindProperty("sequences")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("logicEvents");
                Assert.That(events.arraySize, Is.EqualTo(1));
                Assert.That(
                    events.GetArrayElementAtIndex(0)
                        .FindPropertyRelative("eventId").stringValue,
                    Is.Not.Empty);
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void ScrubAndPreviewUseAbsoluteTicksAndClampOneToFourTargets()
        {
            FpgSkillEditorSession session = new FpgSkillEditorSession();
            session.SetDuration(100);
            Assert.That(session.ScrubAbsolute(80), Is.EqualTo(80));
            Assert.That(session.ScrubAbsolute(12), Is.EqualTo(12));
            Assert.That(session.ScrubAbsolute(-5), Is.Zero);
            Assert.That(session.ScrubAbsolute(500), Is.EqualTo(100));

            Assert.That(session.SetTargetCount(0), Is.EqualTo(1));
            for (int count = 1; count <= 4; count++)
            {
                Assert.That(session.SetTargetCount(count), Is.EqualTo(count));
            }
            Assert.That(session.SetTargetCount(8), Is.EqualTo(4));

            FpgSkillPreviewView preview = new FpgSkillPreviewView();
            preview.SetTargetCount(0);
            Assert.That(preview.TargetCount, Is.EqualTo(1));
            for (int count = 1; count <= 4; count++)
            {
                preview.SetTargetCount(count);
                Assert.That(preview.TargetCount, Is.EqualTo(count));
            }
            preview.SetTargetCount(9);
            Assert.That(preview.TargetCount, Is.EqualTo(4));
            preview.SetTickState(45, null);
            preview.SetTickState(3, null);
            Assert.That(preview.LastSampledTick, Is.EqualTo(3));
        }

        [Test]
        public void AnimationSourceDurationBaselineIsExplicitUndoableAndKeepsEventTicks()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(20, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence = FpgSkillSerializedAdapter.GetSequence(
                    serialized,
                    0);
                ConfigureLogicEvent(
                    sequence,
                    0,
                    "event.animation.baseline",
                    7,
                    0,
                    "payload.a");
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    FpgSkillSerializedAdapter.GetAnimationSourceDurationTicks(
                        sequence),
                    Is.Zero);
                Assert.That(
                    FpgSkillSerializedAdapter.SetAnimationSourceDurationTicks(
                        serialized,
                        0,
                        42),
                    Is.True);
                Assert.That(
                    serialized.FindProperty("sequences")
                        .GetArrayElementAtIndex(0)
                        .FindPropertyRelative("sourceAnimationDurationTicks")
                        .intValue,
                    Is.EqualTo(42));
                Assert.That(
                    serialized.FindProperty("sequences")
                        .GetArrayElementAtIndex(0)
                        .FindPropertyRelative("logicEvents")
                        .GetArrayElementAtIndex(0)
                        .FindPropertyRelative("tick")
                        .intValue,
                    Is.EqualTo(7));

                Undo.PerformUndo();
                serialized.UpdateIfRequiredOrScript();
                Assert.That(
                    serialized.FindProperty("sequences")
                        .GetArrayElementAtIndex(0)
                        .FindPropertyRelative("sourceAnimationDurationTicks")
                        .intValue,
                    Is.Zero);
                Assert.That(
                    serialized.FindProperty("sequences")
                        .GetArrayElementAtIndex(0)
                        .FindPropertyRelative("logicEvents")
                        .GetArrayElementAtIndex(0)
                        .FindPropertyRelative("tick")
                        .intValue,
                    Is.EqualTo(7));
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void AnimationSourceDurationValidationWarnsWithoutMovingEvents()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(60, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence = FpgSkillSerializedAdapter.GetSequence(
                    serialized,
                    0);
                List<FpgSkillPayloadRecord> payloads =
                    FpgSkillSerializedAdapter.ReadPayloads(sequence);
                List<FpgSkillEventRecord> events =
                    FpgSkillSerializedAdapter.ReadEvents(sequence, payloads, 60);

                List<FpgSkillValidationItem> uninitialized =
                    FpgSkillSerializedAdapter.Validate(
                        serialized,
                        0,
                        payloads,
                        events,
                        60,
                        45);
                Assert.That(uninitialized.Any(item =>
                    item.Severity == FpgSkillIssueSeverity.Warning
                    && item.Message.Contains("尚未初始化")
                    && item.Message.Contains("45 Tick")), Is.True);

                Assert.That(
                    FpgSkillSerializedAdapter.SetAnimationSourceDurationTicks(
                        serialized,
                        0,
                        30),
                    Is.True);
                List<FpgSkillValidationItem> changed =
                    FpgSkillSerializedAdapter.Validate(
                        serialized,
                        0,
                        payloads,
                        events,
                        60,
                        45);
                Assert.That(changed.Any(item =>
                    item.Severity == FpgSkillIssueSeverity.Warning
                    && item.Message.Contains("基准 30 Tick")
                    && item.Message.Contains("实测 45 Tick")
                    && item.Message.Contains("不会移动任何逻辑事件")), Is.True);

                Assert.That(
                    FpgSkillSerializedAdapter.SetAnimationSourceDurationTicks(
                        serialized,
                        0,
                        45),
                    Is.True);
                List<FpgSkillValidationItem> matched =
                    FpgSkillSerializedAdapter.Validate(
                        serialized,
                        0,
                        payloads,
                        events,
                        60,
                        45);
                Assert.That(matched.Any(item =>
                    item.Message.Contains("源动画长度")), Is.False);
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void PreviewMeasuresSpineAnimationDurationInSixtyHertzTicks()
        {
            const string PrefabPath =
                "Assets/FPGDemo/Presentation/Characters/Fei/Spine/"
                + "D0_Fei_30048_StraightAlpha.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);

            FpgCompiledSkillSequence sequence =
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    120,
                    1,
                    true,
                    FpgSkillAnimationPlaybackMode.NaturalSpeed,
                    0,
                    120,
                    Array.Empty<FpgCompiledSkillEvent>());
            FpgSkillPreviewView preview = new FpgSkillPreviewView();
            int measuredTicks = -1;
            preview.AnimationDurationMeasured += ticks => measuredTicks = ticks;
            try
            {
                preview.SetAnimation("idle", sequence);
                preview.SetPreviewPrefab(prefab);

                Assert.That(measuredTicks, Is.GreaterThan(0));
                Assert.That(
                    preview.MeasuredAnimationDurationTicks,
                    Is.EqualTo(measuredTicks));
            }
            finally
            {
                preview.SetPreviewPrefab(null);
            }
        }

        [Test]
        public void PreviewExecutionUsesFormalRuntimeForForwardAndBackwardScrubs()
        {
            FpgCompiledSkillSequence sequence = new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                2,
                1,
                false,
                new[]
                {
                    new FpgCompiledSkillEvent(
                        101,
                        0,
                        FpgSkillEventKind.GameplayPayload,
                        11,
                        0,
                        0),
                    new FpgCompiledSkillEvent(
                        202,
                        2,
                        FpgSkillEventKind.GameplayPayload,
                        12,
                        0,
                        0)
                });
            FpgSkillPreviewExecution execution =
                new FpgSkillPreviewExecution();

            Assert.That(execution.Bind(sequence, out string error), Is.True, error);
            Assert.That(execution.AdvanceTo(0, out error), Is.True, error);
            Assert.That(execution.ResultCount, Is.EqualTo(1));
            Assert.That(execution.GetResult(0).EventId, Is.EqualTo(101));

            Assert.That(execution.AdvanceTo(2, out error), Is.True, error);
            Assert.That(execution.ResultCount, Is.EqualTo(1));
            Assert.That(execution.GetResult(0).EventId, Is.EqualTo(202));

            Assert.That(execution.AdvanceTo(1, out error), Is.True, error);
            Assert.That(execution.ResultCount, Is.Zero);
            Assert.That(execution.AdvanceTo(2, out error), Is.True, error);
            Assert.That(execution.GetResult(0).EventId, Is.EqualTo(202));
        }

        [Test]
        public void PreviewAnimationSamplingUsesFormalAnimationTime()
        {
            const string PrefabPath =
                "Assets/FPGDemo/Presentation/Characters/Fei/Spine/"
                + "D0_Fei_30048_StraightAlpha.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            FpgCompiledSkillSequence sequence = new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                120,
                1,
                true,
                FpgSkillAnimationPlaybackMode.NaturalSpeed,
                0,
                10,
                Array.Empty<FpgCompiledSkillEvent>());
            FpgSkillPreviewView preview = new FpgSkillPreviewView();
            try
            {
                preview.SetAnimation("idle", sequence);
                preview.SetPreviewPrefab(prefab);
                preview.SetTickState(75, null);

                double expected = FpgSkillAnimationTime.EvaluateSeconds(
                    sequence,
                    75,
                    0d,
                    preview.MeasuredAnimationDurationSeconds);
                Assert.That(
                    preview.LastSampledAnimationSeconds,
                    Is.EqualTo(expected).Within(0.00001d));
            }
            finally
            {
                preview.SetPreviewPrefab(null);
            }
        }

        [Test]
        public void PresentationCueGameplayBindingCompilesAndInvalidReferenceIsLocated()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(20, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence = FpgSkillSerializedAdapter.GetSequence(
                    serialized,
                    0);
                ConfigureLogicEvent(
                    sequence,
                    0,
                    "event.gameplay",
                    7,
                    0,
                    "payload.a");
                ConfigureCue(
                    sequence,
                    0,
                    "event.cue",
                    7,
                    1,
                    "cue.bound");
                SerializedProperty binding = sequence
                    .FindPropertyRelative("presentationCues")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("bindGameplayEventId");
                binding.stringValue = "event.gameplay";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledPlayerSkillDefinition compiled,
                        out string compileError),
                    Is.True,
                    compileError);
                Assert.That(
                    compiled.Timeline.TryGetSequence(
                        FpgSkillSequenceKind.Execute,
                        out FpgCompiledSkillSequence compiledSequence),
                    Is.True);
                FpgCompiledSkillEvent gameplay = compiledSequence.Events
                    .Single(item =>
                        item.Kind == FpgSkillEventKind.GameplayPayload);
                FpgCompiledSkillEvent cue = compiledSequence.Events.Single(item =>
                    item.Kind == FpgSkillEventKind.PresentationCue);
                Assert.That(
                    cue.BoundGameplayEventId,
                    Is.EqualTo(gameplay.EventId));

                binding.stringValue = "event.missing";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                List<FpgSkillPayloadRecord> payloads =
                    FpgSkillSerializedAdapter.ReadPayloads(sequence);
                List<FpgSkillEventRecord> events =
                    FpgSkillSerializedAdapter.ReadEvents(sequence, payloads, 20);
                FpgSkillEventRecord authoredCue = events.Single(item =>
                    item.Track == FpgSkillEventTrackKind.Presentation);
                List<FpgSkillValidationItem> validation =
                    FpgSkillSerializedAdapter.Validate(
                        serialized,
                        0,
                        payloads,
                        events,
                        20);
                Assert.That(validation.Any(item =>
                    item.Severity == FpgSkillIssueSeverity.Error
                    && item.EventIndex == authoredCue.Index
                    && item.Tick == 7
                    && item.Message.Contains("event.missing")), Is.True);
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void PresentationCueGameplayBindingRejectsDifferentTick()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(20, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence = FpgSkillSerializedAdapter.GetSequence(
                    serialized,
                    0);
                ConfigureLogicEvent(
                    sequence,
                    0,
                    "event.gameplay",
                    7,
                    0,
                    "payload.a");
                ConfigureCue(
                    sequence,
                    0,
                    "event.cue",
                    8,
                    1,
                    "cue.bound");
                sequence.FindPropertyRelative("presentationCues")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("bindGameplayEventId")
                    .stringValue = "event.gameplay";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledPlayerSkillDefinition _,
                        out string compileError),
                    Is.False);
                Assert.That(compileError, Does.Contain("same Tick"));
                Assert.That(compileError, Does.Contain("event.cue"));
                Assert.That(compileError, Does.Contain("event.gameplay"));

                List<FpgSkillPayloadRecord> payloads =
                    FpgSkillSerializedAdapter.ReadPayloads(sequence);
                List<FpgSkillEventRecord> events =
                    FpgSkillSerializedAdapter.ReadEvents(sequence, payloads, 20);
                FpgSkillEventRecord authoredCue = events.Single(item =>
                    item.Track == FpgSkillEventTrackKind.Presentation);
                List<FpgSkillValidationItem> validation =
                    FpgSkillSerializedAdapter.Validate(
                        serialized,
                        0,
                        payloads,
                        events,
                        20);
                Assert.That(validation.Any(item =>
                    item.Severity == FpgSkillIssueSeverity.Error
                    && item.EventIndex == authoredCue.Index
                    && item.Tick == 8
                    && item.Message.Contains("same Tick")), Is.True);
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void FormalCompilerRejectsCueBindingAtDifferentTick()
        {
            FpgCompiledSkillEvent gameplay = new FpgCompiledSkillEvent(
                100,
                3,
                FpgSkillEventKind.GameplayPayload,
                401,
                0,
                0,
                targetSource: FpgSkillTargetSource.CurrentAim);
            FpgCompiledSkillEvent cue = new FpgCompiledSkillEvent(
                101,
                4,
                FpgSkillEventKind.PresentationCue,
                0,
                201,
                0,
                boundGameplayEventId: 100);

            Assert.That(
                FpgSkillCompiler.TryCompileSequence(
                    FpgSkillSequenceKind.Execute,
                    10,
                    301,
                    false,
                    new[] { gameplay, cue },
                    out FpgCompiledSkillSequence _,
                    out FpgSkillValidationResult validation),
                Is.False);
            Assert.That(
                validation.Error,
                Is.EqualTo(
                    FpgSkillValidationError.InvalidBoundGameplayEventId));
            Assert.That(validation.EventIndex, Is.EqualTo(1));
            Assert.That(validation.Value, Is.EqualTo(100));
        }

        [Test]
        public void FormalCompilerRejectsCueBindingWithoutGameplayEvent()
        {
            FpgCompiledSkillEvent cue = new FpgCompiledSkillEvent(
                101,
                4,
                FpgSkillEventKind.PresentationCue,
                0,
                201,
                0,
                0,
                0,
                FpgSkillTargetSource.CurrentAim,
                0,
                0,
                0,
                999);

            Assert.That(
                FpgSkillCompiler.TryCompileSequence(
                    FpgSkillSequenceKind.Execute,
                    10,
                    301,
                    false,
                    new[] { cue },
                    out FpgCompiledSkillSequence _,
                    out FpgSkillValidationResult validation),
                Is.False);
            Assert.That(
                validation.Error,
                Is.EqualTo(
                    FpgSkillValidationError.InvalidBoundGameplayEventId));
            Assert.That(validation.EventIndex, Is.EqualTo(0));
            Assert.That(validation.Value, Is.EqualTo(999));
        }

        [Test]
        public void PreviewPrefabValidationBlocksMissingAnimationButOnlyWarnsWhenUnselected()
        {
            const string PrefabPath =
                "Assets/FPGDemo/Presentation/Characters/Fei/Spine/"
                + "D0_Fei_30048_StraightAlpha.prefab";
            FpgPlayerSkillDefinition skill = CreateSkill(60, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence = FpgSkillSerializedAdapter.GetSequence(
                    serialized,
                    0);
                sequence.FindPropertyRelative("mainAnimation").stringValue =
                    "animation.does.not.exist";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                List<FpgSkillPayloadRecord> payloads =
                    FpgSkillSerializedAdapter.ReadPayloads(sequence);
                List<FpgSkillEventRecord> events =
                    FpgSkillSerializedAdapter.ReadEvents(sequence, payloads, 60);

                List<FpgSkillValidationItem> withoutPrefab =
                    FpgSkillSerializedAdapter.Validate(
                        serialized,
                        0,
                        payloads,
                        events,
                        60);
                Assert.That(withoutPrefab.Any(item =>
                    item.Severity == FpgSkillIssueSeverity.Warning
                    && item.Message.Contains("未选择预览 Prefab")), Is.True);
                Assert.That(withoutPrefab.Any(item =>
                    item.Severity == FpgSkillIssueSeverity.Error
                    && item.Message.Contains("Spine SkeletonData")), Is.False);

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPath);
                Assert.That(prefab, Is.Not.Null);
                List<FpgSkillValidationItem> withPrefab =
                    FpgSkillSerializedAdapter.Validate(
                        serialized,
                        0,
                        payloads,
                        events,
                        60,
                        -1,
                        prefab);
                Assert.That(withPrefab.Any(item =>
                    item.Severity == FpgSkillIssueSeverity.Error
                    && item.Message.Contains("mainAnimation")
                    && item.Message.Contains("animation.does.not.exist")), Is.True);

                sequence.FindPropertyRelative("mainAnimation").stringValue = "idle";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                List<FpgSkillValidationItem> validAnimation =
                    FpgSkillSerializedAdapter.Validate(
                        serialized,
                        0,
                        payloads,
                        events,
                        60,
                        -1,
                        prefab);
                Assert.That(validAnimation.Any(item =>
                    item.Severity == FpgSkillIssueSeverity.Error
                    && item.Message.Contains("mainAnimation")), Is.False);
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void PreviewPrefabValidationLocatesUnresolvedEventSockets()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(20, 1, 1);
            GameObject previewRoot = new GameObject("Socket Preview");
            GameObject anchor = new GameObject("Resolved Socket");
            anchor.transform.SetParent(previewRoot.transform, false);
            D0ActorSocketRegistry registry =
                previewRoot.AddComponent<D0ActorSocketRegistry>();
            Assert.That(
                registry.TryReplaceBindings(
                    new[]
                    {
                        new D0ActorSocketBinding(
                            "socket.resolved",
                            anchor.transform)
                    },
                    out string registryError),
                Is.True,
                registryError);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence = FpgSkillSerializedAdapter.GetSequence(
                    serialized,
                    0);
                ConfigureCue(
                    sequence,
                    0,
                    "event.socket",
                    9,
                    0,
                    "cue.socket");
                SerializedProperty socket = sequence
                    .FindPropertyRelative("presentationCues")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("socketId");
                socket.stringValue = "socket.missing";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                List<FpgSkillPayloadRecord> payloads =
                    FpgSkillSerializedAdapter.ReadPayloads(sequence);
                List<FpgSkillEventRecord> events =
                    FpgSkillSerializedAdapter.ReadEvents(sequence, payloads, 20);
                FpgSkillEventRecord authored = events.Single();
                List<FpgSkillValidationItem> missing =
                    FpgSkillSerializedAdapter.Validate(
                        serialized,
                        0,
                        payloads,
                        events,
                        20,
                        -1,
                        previewRoot);
                Assert.That(missing.Any(item =>
                    item.Severity == FpgSkillIssueSeverity.Error
                    && item.EventIndex == authored.Index
                    && item.Tick == 9
                    && item.Message.Contains("socket.missing")), Is.True);

                socket.stringValue = "socket.resolved";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                events = FpgSkillSerializedAdapter.ReadEvents(
                    sequence,
                    payloads,
                    20);
                List<FpgSkillValidationItem> resolved =
                    FpgSkillSerializedAdapter.Validate(
                        serialized,
                        0,
                        payloads,
                        events,
                        20,
                        -1,
                        previewRoot);
                Assert.That(resolved.Any(item =>
                    item.Message.Contains("Socket")
                    && item.Message.Contains("无法由当前预览 Prefab")), Is.False);
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(previewRoot);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void PayloadPreviewShowsShapeImpactDamageCapacityAndHitParts()
        {
            FpgPlayerSkillDefinition player = CreateSkill(20, 2, 1);
            FpgEnemyAttackDefinition enemy =
                ScriptableObject.CreateInstance<FpgEnemyAttackDefinition>();
            try
            {
                SerializedObject playerSerialized = new SerializedObject(player);
                SerializedProperty playerPayloads =
                    playerSerialized.FindProperty("payloadSlots");
                SerializedProperty ray = playerPayloads.GetArrayElementAtIndex(0);
                ray.FindPropertyRelative("kind").enumValueIndex =
                    (int)FpgPlayerSkillPayloadKind.PelletRay;
                ray.FindPropertyRelative("baseDamage").intValue = 10;
                ray.FindPropertyRelative("breakDamage").intValue = 3;
                ray.FindPropertyRelative(
                    "weakpointDamageMultiplierBasisPoints").intValue = 15000;
                ray.FindPropertyRelative(
                    "weakpointBreakMultiplierBasisPoints").intValue = 20000;
                ray.FindPropertyRelative("pelletCount").intValue = 2;
                ray.FindPropertyRelative(
                    "additionalPenetrationCount").intValue = 1;

                SerializedProperty area = playerPayloads.GetArrayElementAtIndex(1);
                area.FindPropertyRelative("kind").enumValueIndex =
                    (int)FpgPlayerSkillPayloadKind.AreaAtFirstSurface;
                area.FindPropertyRelative("areaCombatantLimit").intValue = 3;
                area.FindPropertyRelative("areaProjectileLimit").intValue = 2;
                playerSerialized.ApplyModifiedPropertiesWithoutUndo();

                List<FpgSkillPayloadRecord> playerPreview =
                    FpgSkillSerializedAdapter.ReadPayloads(
                        playerSerialized.FindProperty("sequences")
                            .GetArrayElementAtIndex(0));
                Assert.That(playerPreview[0].HitShape, Is.EqualTo("射线"));
                Assert.That(playerPreview[0].MaxHitCount, Is.EqualTo(4));
                Assert.That(playerPreview[0].WeakpointDamage, Is.EqualTo(15));
                Assert.That(playerPreview[0].WeakpointBreakDamage, Is.EqualTo(6));
                Assert.That(playerPreview[1].HitShape, Is.EqualTo("范围"));
                Assert.That(playerPreview[1].MaxHitCount, Is.EqualTo(5));

                SerializedObject enemySerialized = new SerializedObject(enemy);
                SerializedProperty projectile = enemySerialized
                    .FindProperty("payloadSlots")
                    .GetArrayElementAtIndex(0);
                projectile.FindPropertyRelative("kind").enumValueIndex =
                    (int)FpgEnemySkillPayloadKind.Projectile;
                projectile.FindPropertyRelative("projectileCount").intValue = 3;
                projectile.FindPropertyRelative("projectileFlightTicks").intValue = 12;
                projectile.FindPropertyRelative("baseDamage").intValue = 7;
                projectile.FindPropertyRelative("breakDamage").intValue = 2;
                projectile.FindPropertyRelative(
                    "weakpointDamageMultiplierBasisPoints").intValue = 12000;
                projectile.FindPropertyRelative(
                    "weakpointBreakMultiplierBasisPoints").intValue = 15000;
                enemySerialized.FindProperty("sequences").arraySize = 1;
                enemySerialized.ApplyModifiedPropertiesWithoutUndo();

                List<FpgSkillPayloadRecord> enemyPreview =
                    FpgSkillSerializedAdapter.ReadPayloads(
                        enemySerialized.FindProperty("sequences")
                            .GetArrayElementAtIndex(0));
                FpgSkillPayloadRecord projectilePreview = enemyPreview[0];
                Assert.That(projectilePreview.HitShape, Is.EqualTo("弹道"));
                Assert.That(projectilePreview.ImpactDelayTicks, Is.EqualTo(12));
                Assert.That(projectilePreview.MaxHitCount, Is.EqualTo(3));
                string summary = projectilePreview.BuildPreviewSummary(5);
                Assert.That(summary, Does.Contain("预计命中 Tick 17"));
                Assert.That(summary, Does.Contain("Body 生命 7 / 削韧 2"));
                Assert.That(summary, Does.Contain("Weakpoint 生命 8 / 削韧 3"));

                FpgSkillPreviewView preview = new FpgSkillPreviewView();
                preview.SetTickState(
                    5,
                    new[]
                    {
                        new FpgSkillTimelineEventViewModel
                        {
                            Label = "Projectile",
                            PayloadPreview = summary
                        }
                    });
                Assert.That(preview.OverlayText, Does.Contain("预计命中 Tick 17"));
                Assert.That(preview.OverlayText, Does.Contain("Weakpoint"));
            }
            finally
            {
                Undo.ClearUndo(player);
                Undo.ClearUndo(enemy);
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void ValidationErrorsCarryEventAndTickLocation()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(20, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence = serialized.FindProperty("sequences")
                    .GetArrayElementAtIndex(0);
                ConfigureLogicEvent(
                    sequence,
                    0,
                    "event.missing.payload",
                    7,
                    0,
                    "payload.does.not.exist");
                serialized.ApplyModifiedPropertiesWithoutUndo();

                List<FpgSkillPayloadRecord> payloads =
                    FpgSkillSerializedAdapter.ReadPayloads(sequence);
                List<FpgSkillEventRecord> events =
                    FpgSkillSerializedAdapter.ReadEvents(sequence, payloads, 20);
                List<FpgSkillValidationItem> validation =
                    FpgSkillSerializedAdapter.Validate(
                        serialized,
                        0,
                        payloads,
                        events,
                        20);
                FpgSkillEventRecord authored = events.Single();

                Assert.That(validation.Any(item =>
                    item.Severity == FpgSkillIssueSeverity.Error
                    && item.EventIndex == authored.Index
                    && item.Tick == 7), Is.True);

                FpgSkillEditorSession session = new FpgSkillEditorSession();
                session.SetDuration(20);
                FpgSkillValidationItem located = validation.First(item =>
                    item.EventIndex == authored.Index && item.Tick == 7);
                FpgSkillEditorLocation location = session.Locate(located);
                Assert.That(location.EventIndex, Is.EqualTo(authored.Index));
                Assert.That(location.Tick, Is.EqualTo(7));
                Assert.That(session.CurrentTick, Is.EqualTo(7));
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void SameTickEventOrderMoveIsUndoableAndRedoable()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(20, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(serialized, 0);
                ConfigureLogicEvent(
                    sequence,
                    0,
                    "event.order.first",
                    5,
                    0,
                    "payload.a");
                ConfigureLogicEvent(
                    sequence,
                    1,
                    "event.order.second",
                    5,
                    1,
                    "payload.a");
                serialized.ApplyModifiedPropertiesWithoutUndo();

                List<FpgSkillEventRecord> initial =
                    ReadEvents(serialized, 0);
                int firstKey = initial.Single(item =>
                    item.EventId == "event.order.first").Index;

                Assert.That(
                    FpgSkillSerializedAdapter.MoveEventOrder(
                        serialized,
                        0,
                        firstKey,
                        1),
                    Is.True);
                List<FpgSkillEventRecord> moved =
                    ReadEvents(serialized, 0);
                Assert.That(
                    moved.Single(item =>
                        item.EventId == "event.order.first")
                        .AuthoredOrdinal,
                    Is.EqualTo(1));
                Assert.That(
                    moved.Single(item =>
                        item.EventId == "event.order.second")
                        .AuthoredOrdinal,
                    Is.Zero);

                Undo.PerformUndo();
                serialized.UpdateIfRequiredOrScript();
                List<FpgSkillEventRecord> undone =
                    ReadEvents(serialized, 0);
                Assert.That(
                    undone.Single(item =>
                        item.EventId == "event.order.first")
                        .AuthoredOrdinal,
                    Is.Zero);
                Assert.That(
                    undone.Single(item =>
                        item.EventId == "event.order.second")
                        .AuthoredOrdinal,
                    Is.EqualTo(1));

                Undo.PerformRedo();
                serialized.UpdateIfRequiredOrScript();
                List<FpgSkillEventRecord> redone =
                    ReadEvents(serialized, 0);
                Assert.That(
                    redone.Single(item =>
                        item.EventId == "event.order.first")
                        .AuthoredOrdinal,
                    Is.EqualTo(1));
                Assert.That(
                    redone.Single(item =>
                        item.EventId == "event.order.second")
                        .AuthoredOrdinal,
                    Is.Zero);
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void TimelineExposesFrameLabelsCapabilitiesAndPhaseBoundaries()
        {
            FpgPlayerSkillDefinition skill = CreateSkill(30, 1, 1);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(serialized, 0);
                sequence.FindPropertyRelative("mainAnimation").stringValue =
                    "attack";
                sequence.FindPropertyRelative(
                    "animationPlaybackMode").enumValueIndex =
                    (int)FpgSkillAnimationPlaybackMode.FitInterval;
                sequence.FindPropertyRelative(
                    "animationStartTick").intValue = 2;
                sequence.FindPropertyRelative(
                    "animationEndTick").intValue = 20;
                ConfigurePhase(
                    sequence,
                    0,
                    "phase.startup",
                    FpgSkillPhaseKind.Startup,
                    0,
                    4);
                ConfigurePhase(
                    sequence,
                    1,
                    "phase.active",
                    FpgSkillPhaseKind.Active,
                    4,
                    9);
                ConfigurePhase(
                    sequence,
                    2,
                    "phase.invalid",
                    FpgSkillPhaseKind.Recovery,
                    9,
                    31);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                List<FpgSkillTimelineBlockViewModel> blocks =
                    FpgSkillSerializedAdapter.ReadTimelineBlocks(
                        sequence,
                        30,
                        24);
                Assert.That(blocks.Count, Is.EqualTo(4));
                FpgSkillTimelineBlockViewModel animation =
                    blocks.Single(item =>
                        item.Kind == FpgSkillTimelineBlockKind.Animation);
                Assert.That(animation.StartTick, Is.EqualTo(2));
                Assert.That(animation.EndTick, Is.EqualTo(20));
                Assert.That(animation.Label, Does.Contain("attack"));
                Assert.That(animation.Label, Does.Contain("源24帧@60Hz"));
                Assert.That(animation.Label, Does.Contain("区间18帧"));
                Assert.That(animation.Tooltip, Does.Contain("Fit"));
                Assert.That(animation.MinimumStartTick, Is.Zero);
                Assert.That(animation.MaximumEndTick, Is.EqualTo(int.MaxValue));
                Assert.That(animation.CanResize, Is.True);
                Assert.That(animation.AllowSequenceExtension, Is.True);

                FpgSkillTimelineBlockViewModel active = blocks.Single(item =>
                    item.Kind == FpgSkillTimelineBlockKind.Phase
                    && item.Index == 1);
                Assert.That(active.Label, Does.Contain("生效"));
                Assert.That(active.Label, Does.Contain("5帧"));
                Assert.That(active.Label, Does.Not.Contain("phase.active"));
                Assert.That(active.Tooltip, Does.Contain("不会直接触发伤害"));
                Assert.That(active.MinimumStartTick, Is.EqualTo(4));
                Assert.That(active.MaximumEndTick, Is.EqualTo(9));
                Assert.That(active.CanResize, Is.True);
                Assert.That(active.AllowSequenceExtension, Is.False);
                Assert.That(active.IsInvalid, Is.False);
                Assert.That(
                    blocks.Single(item =>
                        item.Kind == FpgSkillTimelineBlockKind.Phase
                        && item.Index == 2).IsInvalid,
                    Is.True);
                Assert.That(
                    FpgSkillSerializedAdapter.GetPhaseProperty(
                        serialized,
                        0,
                        1),
                    Is.Not.Null);

                FpgSkillTimelineView timeline =
                    new FpgSkillTimelineView();
                bool selected = false;
                timeline.BlockSelected += (kind, index) =>
                {
                    selected = kind == FpgSkillTimelineBlockKind.Phase
                        && index == 1;
                };
                timeline.SetModel(
                    30,
                    Array.Empty<FpgSkillTimelineEventViewModel>(),
                    blocks);
                timeline.SelectBlock(
                    FpgSkillTimelineBlockKind.Phase,
                    1,
                    true);
                Assert.That(selected, Is.True);
                Assert.That(
                    timeline.SelectedBlockKind,
                    Is.EqualTo(FpgSkillTimelineBlockKind.Phase));
                Assert.That(timeline.SelectedBlockIndex, Is.EqualTo(1));
            }
            finally
            {
                Undo.ClearUndo(skill);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void PreviewSceneCreatesMainAndThreeDeputyBodyWeakpointTargets()
        {
            const string PrefabPath =
                "Assets/FPGDemo/Presentation/Characters/Fei/Spine/"
                + "D0_Fei_30048_StraightAlpha.prefab";
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);

            FpgSkillPreviewView preview = new FpgSkillPreviewView();
            try
            {
                preview.SetTargetCount(4);
                preview.SetPreviewPrefab(prefab);

                Assert.That(preview.HasIsolatedPreviewScene, Is.True);
                Assert.That(preview.PreviewSceneTargetCount, Is.EqualTo(4));
                Assert.That(preview.PreviewTargetCount, Is.EqualTo(4));
                Assert.That(
                    preview.GetPreviewTarget(0).Label,
                    Is.EqualTo("主假人"));
                Assert.That(
                    preview.GetPreviewTarget(3).Label,
                    Is.EqualTo("副假人 3"));
                for (int index = 0; index < 4; index++)
                {
                    FpgSkillPreviewTarget target =
                        preview.GetPreviewTarget(index);
                    Assert.That(target.BodyRadius, Is.GreaterThan(0f));
                    Assert.That(
                        target.WeakpointRadius,
                        Is.GreaterThan(0f));
                    Assert.That(
                        target.BodyCenter,
                        Is.Not.EqualTo(target.WeakpointCenter));
                }
            }
            finally
            {
                preview.SetPreviewPrefab(null);
            }
        }

        [Test]
        public void PreviewSimulationUsesCompiledTicksAndReportsBodyAndWeakpoint()
        {
            FpgCompiledSkillEvent weakpointEvent =
                new FpgCompiledSkillEvent(
                    101,
                    5,
                    FpgSkillEventKind.GameplayPayload,
                    11,
                    0,
                    0,
                    sortOrder: 0,
                    targetSource: FpgSkillTargetSource.CurrentAim);
            FpgCompiledSkillEvent bodyEvent =
                new FpgCompiledSkillEvent(
                    102,
                    5,
                    FpgSkillEventKind.GameplayPayload,
                    11,
                    0,
                    0,
                    sortOrder: 1,
                    targetSource: FpgSkillTargetSource.CurrentTarget);
            FpgCompiledSkillSequence sequence =
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    10,
                    1,
                    false,
                    new[] { weakpointEvent, bodyEvent });
            List<FpgSkillCompiledTriggerRecord> triggers =
                new List<FpgSkillCompiledTriggerRecord>
                {
                    new FpgSkillCompiledTriggerRecord
                    {
                        CompiledEventId = 101,
                        Tick = 5,
                        AuthoredOrdinal = 0,
                        EventIndex = 0,
                        Name = "Weakpoint Shot",
                        CompiledEvent = weakpointEvent
                    },
                    new FpgSkillCompiledTriggerRecord
                    {
                        CompiledEventId = 102,
                        Tick = 5,
                        AuthoredOrdinal = 1,
                        EventIndex = 1,
                        Name = "Body Shot",
                        CompiledEvent = bodyEvent
                    }
                };
            List<FpgSkillEventRecord> authored =
                new List<FpgSkillEventRecord>
                {
                    new FpgSkillEventRecord
                    {
                        Index = 0,
                        Tick = 5,
                        AuthoredOrdinal = 0,
                        PayloadIndex = 0,
                        Name = "Weakpoint Shot",
                        Track = FpgSkillEventTrackKind.Logic
                    },
                    new FpgSkillEventRecord
                    {
                        Index = 1,
                        Tick = 5,
                        AuthoredOrdinal = 1,
                        PayloadIndex = 0,
                        Name = "Body Shot",
                        Track = FpgSkillEventTrackKind.Logic
                    }
                };
            List<FpgSkillPayloadRecord> payloads =
                new List<FpgSkillPayloadRecord>
                {
                    new FpgSkillPayloadRecord
                    {
                        Index = 0,
                        Name = "Pellet",
                        PreviewKind =
                            FpgSkillPreviewPayloadKind.PlayerPelletRay,
                        PelletCount = 1,
                        AdditionalPenetrationCount = 0,
                        BaseDamage = 10,
                        BreakDamage = 3,
                        WeakpointDamage = 18,
                        WeakpointBreakDamage = 6,
                        MaxHitCount = 1
                    }
                };
            PreviewPoseProvider provider = new PreviewPoseProvider();

            FpgSkillPreviewSimulationFrame before =
                FpgSkillPreviewSimulator.Evaluate(
                    sequence,
                    4,
                    triggers,
                    authored,
                    payloads,
                    provider);
            Assert.That(before.EventResults, Is.Empty);
            Assert.That(before.Geometries, Is.Empty);

            FpgSkillPreviewSimulationFrame atTick =
                FpgSkillPreviewSimulator.Evaluate(
                    sequence,
                    5,
                    triggers,
                    authored,
                    payloads,
                    provider);
            Assert.That(provider.PreviewTargetCount, Is.EqualTo(4));
            Assert.That(atTick.EventResults.Count, Is.EqualTo(2));
            Assert.That(
                atTick.Geometries.Count(item =>
                    item.Kind == FpgSkillPreviewGeometryKind.Ray),
                Is.EqualTo(2));
            Assert.That(
                atTick.Hits.Any(item =>
                    item.Part == FpgSkillPreviewHitPart.Body),
                Is.True);
            Assert.That(
                atTick.Hits.Any(item =>
                    item.Part == FpgSkillPreviewHitPart.Weakpoint),
                Is.True);
            Assert.That(
                atTick.EventResults.Any(item =>
                    item.BuildSummary().Contains("Body")),
                Is.True);
            Assert.That(
                atTick.EventResults.Any(item =>
                    item.BuildSummary().Contains("Weakpoint")),
                Is.True);
        }

        [Test]
        public void PreviewSimulationMapsEveryV1PayloadKindToGeometry()
        {
            Assert.That(
                EvaluatePreviewPayload(
                    FpgSkillPreviewPayloadKind.PlayerPelletRay,
                    2,
                    0).Geometries.Any(item =>
                    item.Kind == FpgSkillPreviewGeometryKind.Ray),
                Is.True);
            Assert.That(
                EvaluatePreviewPayload(
                    FpgSkillPreviewPayloadKind.PlayerAreaAtFirstSurface,
                    2,
                    0).Geometries.Any(item =>
                    item.Kind == FpgSkillPreviewGeometryKind.Area),
                Is.True);
            Assert.That(
                EvaluatePreviewPayload(
                    FpgSkillPreviewPayloadKind.EnemyProjectile,
                    3,
                    3).Geometries.Any(item =>
                    item.Kind == FpgSkillPreviewGeometryKind.Projectile),
                Is.True);
            Assert.That(
                EvaluatePreviewPayload(
                    FpgSkillPreviewPayloadKind.EnemyTimedImpact,
                    5,
                    3).Geometries.Any(item =>
                    item.Kind == FpgSkillPreviewGeometryKind.TimedImpact),
                Is.True);
            Assert.That(
                EvaluatePreviewPayload(
                    FpgSkillPreviewPayloadKind.EnemySummon,
                    2,
                    0).Geometries.Any(item =>
                    item.Kind == FpgSkillPreviewGeometryKind.Summon),
                Is.True);
        }

        private static FpgSkillPreviewSimulationFrame EvaluatePreviewPayload(
            FpgSkillPreviewPayloadKind kind,
            int currentTick,
            int impactDelayTicks)
        {
            int eventId = 200 + (int)kind;
            FpgCompiledSkillEvent compiledEvent =
                new FpgCompiledSkillEvent(
                    eventId,
                    2,
                    FpgSkillEventKind.GameplayPayload,
                    11,
                    0,
                    0,
                    targetSource: FpgSkillTargetSource.CurrentAim);
            FpgCompiledSkillSequence sequence =
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    10,
                    1,
                    false,
                    new[] { compiledEvent });
            return FpgSkillPreviewSimulator.Evaluate(
                sequence,
                currentTick,
                new[]
                {
                    new FpgSkillCompiledTriggerRecord
                    {
                        CompiledEventId = eventId,
                        Tick = 2,
                        AuthoredOrdinal = 0,
                        EventIndex = 0,
                        CompiledEvent = compiledEvent
                    }
                },
                new[]
                {
                    new FpgSkillEventRecord
                    {
                        Index = 0,
                        Tick = 2,
                        AuthoredOrdinal = 0,
                        PayloadIndex = 0,
                        Track = FpgSkillEventTrackKind.Logic
                    }
                },
                new[]
                {
                    new FpgSkillPayloadRecord
                    {
                        Index = 0,
                        Name = kind.ToString(),
                        PreviewKind = kind,
                        ImpactDelayTicks = impactDelayTicks,
                        PelletCount = 1,
                        ProjectileCount = 2,
                        AreaCombatantLimit = 4,
                        BaseDamage = 10,
                        BreakDamage = 2,
                        WeakpointDamage = 15,
                        WeakpointBreakDamage = 4,
                        MaxHitCount = 4
                    }
                },
                new PreviewPoseProvider());
        }

        private sealed class PreviewPoseProvider :
            IFpgSkillPreviewPoseProvider
        {
            private readonly FpgSkillPreviewTarget[] targets =
            {
                new FpgSkillPreviewTarget(
                    0,
                    "主假人",
                    new Vector3(4f, 0f, 0f),
                    0.55f,
                    new Vector3(4f, 1.2f, 0f),
                    0.28f),
                new FpgSkillPreviewTarget(
                    1,
                    "副假人 1",
                    new Vector3(6f, 2.2f, 0f),
                    0.55f,
                    new Vector3(6f, 3.4f, 0f),
                    0.28f),
                new FpgSkillPreviewTarget(
                    2,
                    "副假人 2",
                    new Vector3(7f, -2.2f, 0f),
                    0.55f,
                    new Vector3(7f, -1f, 0f),
                    0.28f),
                new FpgSkillPreviewTarget(
                    3,
                    "副假人 3",
                    new Vector3(9f, 2.8f, 0f),
                    0.55f,
                    new Vector3(9f, 4f, 0f),
                    0.28f)
            };

            public int PreviewTargetCount => targets.Length;

            public FpgSkillPreviewTarget GetPreviewTarget(int index)
            {
                return targets[index];
            }

            public bool TryResolvePreviewOrigin(
                string socketId,
                out Vector3 position,
                out Vector3 forward)
            {
                position = Vector3.zero;
                forward = Vector3.right;
                return true;
            }
        }

        
        private static FpgPlayerSkillDefinition CreateSkill(
            int durationTicks,
            int payloadCount,
            int sequenceCount)
        {
            FpgPlayerSkillDefinition skill =
                ScriptableObject.CreateInstance<FpgPlayerSkillDefinition>();
            SerializedObject serialized = new SerializedObject(skill);
            serialized.FindProperty("skillId").stringValue = "player.editor.test";
            serialized.FindProperty("displayName").stringValue = "Editor Test";

            SerializedProperty payloads = serialized.FindProperty("payloadSlots");
            payloads.arraySize = payloadCount;
            for (int index = 0; index < payloadCount; index++)
            {
                payloads.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("slotId").stringValue =
                    index == 0 ? "payload.a" : "payload.b";
            }

            SerializedProperty sequences = serialized.FindProperty("sequences");
            sequences.arraySize = sequenceCount;
            for (int index = 0; index < sequenceCount; index++)
            {
                SerializedProperty sequence = sequences.GetArrayElementAtIndex(index);
                sequence.FindPropertyRelative("kind").enumValueIndex =
                    index == 0
                        ? (int)FpgSkillSequenceKind.Execute
                        : (int)FpgSkillSequenceKind.Release;
                sequence.FindPropertyRelative("durationTicks").intValue = durationTicks;
                sequence.FindPropertyRelative("mainAnimation").stringValue = "idle";
                sequence.FindPropertyRelative("animationEndTick").intValue =
                    durationTicks;
                sequence.FindPropertyRelative("phases").arraySize = 0;
                sequence.FindPropertyRelative("logicEvents").arraySize = 0;
                sequence.FindPropertyRelative("presentationCues").arraySize = 0;
                sequence.FindPropertyRelative("warnings").arraySize = 0;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return skill;
        }

        private static void ConfigureLogicEvent(
            SerializedProperty sequence,
            int index,
            string eventId,
            int tick,
            int authoredOrdinal,
            string payloadSlotId)
        {
            SerializedProperty events = sequence.FindPropertyRelative("logicEvents");
            events.arraySize = Mathf.Max(events.arraySize, index + 1);
            SerializedProperty item = events.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("eventId").stringValue = eventId;
            item.FindPropertyRelative("tick").intValue = tick;
            item.FindPropertyRelative("authoredOrdinal").intValue = authoredOrdinal;
            item.FindPropertyRelative("payloadSlotId").stringValue = payloadSlotId;
            item.FindPropertyRelative("socketId").stringValue = string.Empty;
            item.FindPropertyRelative("targetSource").enumValueIndex =
                (int)FpgSkillTargetSource.CurrentAim;
            item.FindPropertyRelative("targetOffset").vector3Value = Vector3.zero;
        }

        private static void ConfigureCue(
            SerializedProperty sequence,
            int index,
            string eventId,
            int tick,
            int authoredOrdinal,
            string cueId)
        {
            SerializedProperty cues = sequence.FindPropertyRelative("presentationCues");
            cues.arraySize = Mathf.Max(cues.arraySize, index + 1);
            SerializedProperty item = cues.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("eventId").stringValue = eventId;
            item.FindPropertyRelative("tick").intValue = tick;
            item.FindPropertyRelative("authoredOrdinal").intValue = authoredOrdinal;
            item.FindPropertyRelative("cueId").stringValue = cueId;
            item.FindPropertyRelative("socketId").stringValue = string.Empty;
            item.FindPropertyRelative("bindGameplayEventId").stringValue =
                string.Empty;
        }

        private static void ConfigureWarning(
            SerializedProperty sequence,
            int index,
            string eventId,
            int startTick,
            int endTick,
            int authoredOrdinal)
        {
            SerializedProperty warnings = sequence.FindPropertyRelative("warnings");
            warnings.arraySize = Mathf.Max(warnings.arraySize, index + 1);
            SerializedProperty item = warnings.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("eventId").stringValue = eventId;
            item.FindPropertyRelative("warningId").stringValue = "warning.test";
            item.FindPropertyRelative("startTick").intValue = startTick;
            item.FindPropertyRelative("endTick").intValue = endTick;
            item.FindPropertyRelative("authoredOrdinal").intValue = authoredOrdinal;
            item.FindPropertyRelative("socketId").stringValue = string.Empty;
        }

        private static void ConfigurePhase(
            SerializedProperty sequence,
            int index,
            string phaseId,
            FpgSkillPhaseKind kind,
            int startTick,
            int endTick)
        {
            SerializedProperty phases =
                sequence.FindPropertyRelative("phases");
            phases.arraySize = Mathf.Max(phases.arraySize, index + 1);
            SerializedProperty phase =
                phases.GetArrayElementAtIndex(index);
            phase.FindPropertyRelative("phaseId").stringValue = phaseId;
            phase.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            phase.FindPropertyRelative("startTick").intValue = startTick;
            phase.FindPropertyRelative("endTick").intValue = endTick;
        }


        private static List<FpgSkillEventRecord> ReadEvents(
            SerializedObject serialized,
            int sequenceIndex)
        {
            serialized.UpdateIfRequiredOrScript();
            SerializedProperty sequence = FpgSkillSerializedAdapter.GetSequence(
                serialized,
                sequenceIndex);
            List<FpgSkillPayloadRecord> payloads =
                FpgSkillSerializedAdapter.ReadPayloads(sequence);
            return FpgSkillSerializedAdapter.ReadEvents(
                sequence,
                payloads,
                FpgSkillSerializedAdapter.GetDurationTicks(sequence));
        }
    

        private static void AssertTimelineRange(
            SerializedObject serialized,
            string startPropertyName,
            string endPropertyName,
            int expectedStart,
            int expectedEnd)
        {
            serialized.UpdateIfRequiredOrScript();
            SerializedProperty sequence =
                FpgSkillSerializedAdapter.GetSequence(serialized, 0);
            Assert.That(
                sequence.FindPropertyRelative(startPropertyName).intValue,
                Is.EqualTo(expectedStart));
            Assert.That(
                sequence.FindPropertyRelative(endPropertyName).intValue,
                Is.EqualTo(expectedEnd));
        }


        private static void AssertPhaseRange(
            SerializedObject serialized,
            int phaseIndex,
            int expectedStart,
            int expectedEnd)
        {
            serialized.UpdateIfRequiredOrScript();
            SerializedProperty phase =
                FpgSkillSerializedAdapter.GetPhaseProperty(
                    serialized,
                    0,
                    phaseIndex);
            Assert.That(phase, Is.Not.Null);
            Assert.That(
                phase.FindPropertyRelative("startTick").intValue,
                Is.EqualTo(expectedStart));
            Assert.That(
                phase.FindPropertyRelative("endTick").intValue,
                Is.EqualTo(expectedEnd));
        }


        private static void AssertSequenceDurationAndLogicTick(
            SerializedObject serialized,
            int expectedDuration,
            int expectedLogicTick)
        {
            serialized.UpdateIfRequiredOrScript();
            SerializedProperty sequence =
                FpgSkillSerializedAdapter.GetSequence(serialized, 0);
            Assert.That(
                sequence.FindPropertyRelative("durationTicks").intValue,
                Is.EqualTo(expectedDuration));
            SerializedProperty logicEvents =
                sequence.FindPropertyRelative("logicEvents");
            if (expectedLogicTick < 0)
            {
                Assert.That(logicEvents.arraySize, Is.Zero);
                return;
            }

            Assert.That(logicEvents.arraySize, Is.GreaterThan(0));
            Assert.That(
                logicEvents.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("tick").intValue,
                Is.EqualTo(expectedLogicTick));
        }
}
}
