using System;
using System.Linq;
using FPG.Demo.Editor.SkillAuthoring;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgSkillPreviewExecutionTests
    {
        [Test]
        public void ForwardAdvanceCollectsEveryResultAcrossSkippedTicksOnce()
        {
            FpgSkillPreviewExecution execution = CreateBoundExecution();

            Assert.That(execution.AdvanceTo(0, out string error), Is.True, error);
            Assert.That(ResultIds(execution), Is.EqualTo(new[] { 101 }));

            execution.ClearPendingResults();
            Assert.That(execution.AdvanceTo(3, out error), Is.True, error);
            Assert.That(
                ResultIds(execution),
                Is.EqualTo(new[] { 201, 202, 203 }));
        }

        [Test]
        public void SameTickAndBackwardAdvanceRebuildWithoutResults()
        {
            FpgSkillPreviewExecution execution = CreateBoundExecution();

            Assert.That(execution.AdvanceTo(3, out string error), Is.True, error);
            Assert.That(execution.ResultCount, Is.EqualTo(4));

            Assert.That(execution.AdvanceTo(3, out error), Is.True, error);
            Assert.That(execution.ResultCount, Is.Zero);

            Assert.That(execution.AdvanceTo(0, out error), Is.True, error);
            Assert.That(execution.CurrentTick, Is.Zero);
            Assert.That(execution.ResultCount, Is.Zero);

            Assert.That(execution.AdvanceTo(2, out error), Is.True, error);
            Assert.That(ResultIds(execution), Is.EqualTo(new[] { 201, 202 }));
        }

        [Test]
        public void ClearingPendingResultsKeepsTheReconstructedRuntimePosition()
        {
            FpgSkillPreviewExecution execution = CreateBoundExecution();

            Assert.That(execution.AdvanceTo(1, out string error), Is.True, error);
            Assert.That(execution.ResultCount, Is.EqualTo(2));

            execution.ClearPendingResults();
            Assert.That(execution.ResultCount, Is.Zero);
            Assert.That(execution.CurrentTick, Is.EqualTo(1));

            Assert.That(execution.AdvanceTo(3, out error), Is.True, error);
            Assert.That(ResultIds(execution), Is.EqualTo(new[] { 202, 203 }));
        }

        [Test]
        public void ResetDropsBindingAndPendingResults()
        {
            FpgSkillPreviewExecution execution = CreateBoundExecution();
            Assert.That(execution.AdvanceTo(1, out string error), Is.True, error);

            execution.Reset();

            Assert.That(execution.IsBound, Is.False);
            Assert.That(execution.CurrentTick, Is.EqualTo(-1));
            Assert.That(execution.ResultCount, Is.Zero);
            Assert.That(
                () => execution.GetResult(0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TimelineReportsDirectManipulationDuringScrub()
        {
            FpgSkillTimelineView timeline = new FpgSkillTimelineView();

            Assert.That(timeline.IsDirectManipulationActive, Is.False);
            timeline.BeginScrubAtTick(17, 2);
            Assert.That(timeline.IsDirectManipulationActive, Is.True);
            timeline.EndScrub(17);
            Assert.That(timeline.IsDirectManipulationActive, Is.False);
        }

        [Test]
        public void AudioPresentationPreviewUsesTwoDimensionalSourceAndCleansUp()
        {
            FpgPlayerSkillDefinition skill =
                ScriptableObject.CreateInstance<FpgPlayerSkillDefinition>();
            AudioClip clip = AudioClip.Create(
                "SkillPreviewSilent",
                64,
                1,
                44100,
                false);
            FpgSkillPreviewView view = new FpgSkillPreviewView();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequences =
                    serialized.FindProperty("sequences");
                sequences.arraySize = 1;
                SerializedProperty sequence =
                    sequences.GetArrayElementAtIndex(0);
                sequence.FindPropertyRelative("durationTicks").intValue = 10;
                sequence.FindPropertyRelative("activePresentationTracks")
                    .arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                int track =
                    FpgSkillSerializedAdapter.AddActivePresentationTrack(
                        serialized,
                        0);
                FpgSkillEventKey audio =
                    FpgSkillSerializedAdapter.AddActivePresentationEvent(
                        serialized,
                        0,
                        track,
                        FpgSkillEventTrackKind.PresentationAudio,
                        1);
                SerializedProperty eventProperty =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        audio);
                SerializedProperty presentation =
                    eventProperty.FindPropertyRelative("presentation");
                presentation.FindPropertyRelative("clip")
                    .objectReferenceValue = clip;
                presentation.FindPropertyRelative("volume").floatValue =
                    0.35f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    view.TryPlayActivePresentation(
                        eventProperty,
                        FpgSkillEventTrackKind.PresentationAudio,
                        true,
                        out string error),
                    Is.True,
                    error);
                Assert.That(view.PreviewAudioSource, Is.Not.Null);
                Assert.That(view.PreviewAudioSource.spatialBlend, Is.Zero);
                Assert.That(view.PreviewAudioSource.dopplerLevel, Is.Zero);

                view.ClearPresentationPreview();
                Assert.That(view.PreviewAudioSource, Is.Null);
                Assert.That(view.ActivePresentationVfxCount, Is.Zero);
                Assert.That(view.ActivePresentationShakeCount, Is.Zero);
            }
            finally
            {
                view.ClearPresentationPreview();
                UnityEngine.Object.DestroyImmediate(clip);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void VfxAndShakePreviewUseTheIsolatedSceneAndCleanUp()
        {
            FpgPlayerSkillDefinition skill =
                ScriptableObject.CreateInstance<FpgPlayerSkillDefinition>();
            GameObject actor = new GameObject("PreviewActor");
            GameObject effect = new GameObject("PreviewEffect");
            FpgSkillPreviewView view = new FpgSkillPreviewView();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty sequences =
                    serialized.FindProperty("sequences");
                sequences.arraySize = 1;
                SerializedProperty sequence =
                    sequences.GetArrayElementAtIndex(0);
                sequence.FindPropertyRelative("durationTicks").intValue = 10;
                sequence.FindPropertyRelative("activePresentationTracks")
                    .arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                int track =
                    FpgSkillSerializedAdapter.AddActivePresentationTrack(
                        serialized,
                        0);
                FpgSkillEventKey vfx =
                    FpgSkillSerializedAdapter.AddActivePresentationEvent(
                        serialized,
                        0,
                        track,
                        FpgSkillEventTrackKind.PresentationVfx,
                        1);
                FpgSkillEventKey shake =
                    FpgSkillSerializedAdapter.AddActivePresentationEvent(
                        serialized,
                        0,
                        track,
                        FpgSkillEventTrackKind.PresentationCameraShake,
                        2);
                SerializedProperty vfxEvent =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        vfx);
                SerializedProperty vfxPresentation =
                    vfxEvent.FindPropertyRelative("presentation");
                vfxPresentation.FindPropertyRelative("prefab")
                    .objectReferenceValue = effect;
                vfxPresentation.FindPropertyRelative("durationSeconds")
                    .floatValue = 0.5f;
                SerializedProperty shakeEvent =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serialized,
                        0,
                        shake);
                SerializedProperty shakePresentation =
                    shakeEvent.FindPropertyRelative("presentation");
                shakePresentation.FindPropertyRelative("strength")
                    .floatValue = 0.5f;
                shakePresentation.FindPropertyRelative("durationSeconds")
                    .floatValue = 0.5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                view.SetPreviewPrefab(actor);
                Assert.That(
                    view.TryPlayActivePresentation(
                        vfxEvent,
                        FpgSkillEventTrackKind.PresentationVfx,
                        true,
                        out string vfxError),
                    Is.True,
                    vfxError);
                Assert.That(
                    view.TryPlayActivePresentation(
                        shakeEvent,
                        FpgSkillEventTrackKind.PresentationCameraShake,
                        true,
                        out string shakeError),
                    Is.True,
                    shakeError);
                Assert.That(view.ActivePresentationVfxCount, Is.EqualTo(1));
                Assert.That(view.ActivePresentationShakeCount, Is.EqualTo(1));

                view.UpdatePresentationPreview();
                view.ClearPresentationPreview();
                Assert.That(view.ActivePresentationVfxCount, Is.Zero);
                Assert.That(view.ActivePresentationShakeCount, Is.Zero);
            }
            finally
            {
                view.SetPreviewPrefab(null);
                UnityEngine.Object.DestroyImmediate(effect);
                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        private static FpgSkillPreviewExecution CreateBoundExecution()
        {
            FpgCompiledSkillSequence sequence = new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                3,
                1,
                false,
                new[]
                {
                    new FpgCompiledSkillEvent(
                        101,
                        0,
                        FpgSkillActionKind.Attack,
                        0,
                        sortOrder: 0),
                    ActivePresentationEvent(
                        201,
                        1,
                        FpgActivePresentationKind.Vfx,
                        1,
                        sortOrder: 1,
                        boundGameplayEventId: 101),
                    ActivePresentationEvent(
                        202,
                        2,
                        FpgActivePresentationKind.Audio,
                        2,
                        sortOrder: 2),
                    ActivePresentationEvent(
                        203,
                        3,
                        FpgActivePresentationKind.CameraShake,
                        3,
                        sortOrder: 3)
                });
            FpgSkillPreviewExecution execution =
                new FpgSkillPreviewExecution();
            Assert.That(execution.Bind(sequence, out string error), Is.True, error);
            return execution;
        }

        private static FpgCompiledSkillEvent ActivePresentationEvent(
            int eventId,
            int tick,
            FpgActivePresentationKind kind,
            int handle,
            int sortOrder,
            int boundGameplayEventId = 0)
        {
            return new FpgCompiledSkillEvent(
                eventId,
                tick,
                kind,
                new FpgPresentationHandle(handle),
                presentationTrackId: 11,
                presentationContentHash: (ulong)(1000 + eventId),
                sortOrder: sortOrder,
                boundGameplayEventId: boundGameplayEventId);
        }

        private static int[] ResultIds(FpgSkillPreviewExecution execution)
        {
            return Enumerable.Range(0, execution.ResultCount)
                .Select(index => execution.GetResult(index).EventId)
                .ToArray();
        }
    }
}
