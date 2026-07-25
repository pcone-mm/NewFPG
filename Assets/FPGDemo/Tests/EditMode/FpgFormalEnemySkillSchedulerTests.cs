using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgFormalEnemySkillSchedulerTests
    {
        [Test]
        public void InsufficientWholeSequenceCapacityStartsNoPresentationOrWarning()
        {
            SchedulerFixture fixture = new SchedulerFixture(
                projectileCapacity: 1,
                impactCapacity: 4,
                projectileBudgetCapacity: 4,
                projectileReservationCapacity: 4);
            FpgEnemyAttackDefinition skill = CreateProjectileSkill(
                "enemy.capacity",
                durationTicks: 2,
                cooldownTicks: 3,
                eventTicks: new[] { 0, 2 });
            FpgEnemyDefinition enemy = CreateEnemy(skill);
            try
            {
                fixture.Register(enemy);
                int startCount = 0;
                int eventCount = 0;
                fixture.Scheduler.SkillStarted += _ => startCount++;
                fixture.Scheduler.TimelineEvent += _ => eventCount++;

                DomainResult ticked = fixture.Scheduler.Tick(
                    new TickIndex(0L));

                Assert.That(ticked.IsSuccess, Is.True);
                Assert.That(startCount, Is.Zero);
                Assert.That(eventCount, Is.Zero);
                Assert.That(fixture.Port.PendingAttackCount, Is.Zero);
                Assert.That(
                    fixture.Port.ActiveEnemySkillCapacityReservationCount,
                    Is.Zero);
                Assert.That(
                    fixture.Kernel.ProjectileBudget.ReservedUnits,
                    Is.Zero);
            }
            finally
            {
                fixture.Dispose();
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void InjectedSessionExecutionAllocatorContinuesMonotonically()
        {
            FpgSkillExecutionIdAllocator executionIds =
                new FpgSkillExecutionIdAllocator();
            Assert.That(executionIds.Next().Value, Is.EqualTo(1L));
            SchedulerFixture fixture = new SchedulerFixture(
                projectileCapacity: 2,
                impactCapacity: 8,
                projectileBudgetCapacity: 2,
                projectileReservationCapacity: 2,
                executionIds: executionIds);
            FpgEnemyAttackDefinition skill = CreateTimedImpactSkill(
                "enemy.shared-execution-ids",
                durationTicks: 0,
                cooldownTicks: 1,
                new AuthoredEvent(0, 0));
            FpgEnemyDefinition enemy = CreateEnemy(skill);
            try
            {
                fixture.Register(enemy);
                SkillExecutionId startedExecutionId =
                    SkillExecutionId.Invalid;
                fixture.Scheduler.SkillStarted += value =>
                    startedExecutionId = value.ExecutionId;

                AssertTickAndProcess(fixture, 0L);

                Assert.That(startedExecutionId.Value, Is.EqualTo(2L));
                Assert.That(executionIds.Peek().Value, Is.EqualTo(3L));

                fixture.Scheduler.Clear();
                Assert.That(executionIds.Peek().Value, Is.EqualTo(3L));
            }
            finally
            {
                fixture.Dispose();
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }


        [Test]
        public void ExecuteTimelineTriggersEveryAttackInAuthoredOrderWithIndependentIds()
        {
            SchedulerFixture fixture = new SchedulerFixture(
                projectileCapacity: 4,
                impactCapacity: 12,
                projectileBudgetCapacity: 4,
                projectileReservationCapacity: 4,
                perEnemyThreatCapacity: 3,
                attackScheduleCapacity: 4);
            FpgEnemyAttackDefinition skill = CreateTimedImpactSkill(
                "enemy.combo",
                durationTicks: 3,
                cooldownTicks: 4,
                new AuthoredEvent(0, 2),
                new AuthoredEvent(0, 5),
                new AuthoredEvent(3, 1));
            FpgEnemyDefinition enemy = CreateEnemy(skill);
            try
            {
                fixture.Register(enemy);
                List<SkillExecutionId> starts = new List<SkillExecutionId>();
                List<int> sortOrders = new List<int>();
                List<long> eventTicks = new List<long>();
                fixture.Scheduler.SkillStarted += value =>
                    starts.Add(value.ExecutionId);
                fixture.Scheduler.TimelineEvent += value =>
                {
                    if (value.HasGameplayPayload
                        && value.Outcome == FpgSkillEventOutcome.Triggered)
                    {
                        sortOrders.Add(value.Event.SortOrder);
                        eventTicks.Add(value.RuntimeEvent.Tick.Value);
                    }
                };

                AssertTickAndProcess(fixture, 0L);
                Assert.That(starts.Count, Is.EqualTo(1));
                Assert.That(starts[0].Value, Is.EqualTo(1L));
                CollectionAssert.AreEqual(new[] { 2, 5 }, sortOrders);
                CollectionAssert.AreEqual(new long[] { 0L, 0L }, eventTicks);

                AssertTickAndProcess(fixture, 1L);
                AssertTickAndProcess(fixture, 2L);
                CollectionAssert.AreEqual(new[] { 2, 5 }, sortOrders);

                AssertTickAndProcess(fixture, 3L);
                CollectionAssert.AreEqual(new[] { 2, 5, 1 }, sortOrders);
                CollectionAssert.AreEqual(
                    new long[] { 0L, 0L, 3L },
                    eventTicks);
                Assert.That(
                    CountDistinctAttackStarts(fixture.Kernel.Trace),
                    Is.EqualTo(3));

                AssertTickAndProcess(fixture, 4L);
                AssertTickAndProcess(fixture, 5L);
                AssertTickAndProcess(fixture, 6L);
                Assert.That(starts.Count, Is.EqualTo(1));

                AssertTickAndProcess(fixture, 7L);
                Assert.That(starts.Count, Is.EqualTo(2));
                Assert.That(starts[1].Value, Is.EqualTo(2L));
            }
            finally
            {
                fixture.Dispose();
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void HardInterruptReleasesFutureReservationsButKeepsReleasedPayloadAndCooldown()
        {
            SchedulerFixture fixture = new SchedulerFixture(
                projectileCapacity: 3,
                impactCapacity: 8,
                projectileBudgetCapacity: 3,
                projectileReservationCapacity: 4,
                attackScheduleCapacity: 4);
            FpgEnemyAttackDefinition skill = CreateProjectileSkill(
                "enemy.interrupt",
                durationTicks: 3,
                cooldownTicks: 4,
                eventTicks: new[] { 0, 2 });
            FpgEnemyDefinition enemy = CreateEnemy(skill);
            try
            {
                fixture.Register(enemy);
                List<FpgFormalEnemySkillStartedEvent> starts =
                    new List<FpgFormalEnemySkillStartedEvent>();
                int canceledGameplayEvents = 0;
                fixture.Scheduler.SkillStarted += starts.Add;
                fixture.Scheduler.TimelineEvent += value =>
                {
                    if (value.HasGameplayPayload
                        && value.Outcome == FpgSkillEventOutcome.Canceled)
                    {
                        canceledGameplayEvents++;
                    }
                };

                AssertTickAndProcess(fixture, 0L);
                Assert.That(starts.Count, Is.EqualTo(1));
                Assert.That(fixture.Port.ActiveProjectileCount, Is.EqualTo(1));
                Assert.That(
                    fixture.Kernel.ProjectileBudget.ActiveUnits,
                    Is.EqualTo(1));
                Assert.That(
                    fixture.Kernel.ProjectileBudget.ReservedUnits,
                    Is.EqualTo(1));

                Assert.That(
                    fixture.EnemyRuntime.EnterGroggy(
                        new TickIndex(1L),
                        fixture.Kernel.ProjectileBudget),
                    Is.GreaterThanOrEqualTo(0));
                Assert.That(
                    fixture.Scheduler.Tick(new TickIndex(1L)).IsSuccess,
                    Is.True);

                Assert.That(canceledGameplayEvents, Is.EqualTo(1));
                Assert.That(
                    fixture.Scheduler.SequenceFrameCount,
                    Is.EqualTo(1));
                FpgFormalEnemySkillSequenceFrame canceledFrame =
                    fixture.Scheduler.GetSequenceFrame(0);
                Assert.That(
                    canceledFrame.State,
                    Is.EqualTo(FpgSkillExecutionState.Canceled));
                Assert.That(canceledFrame.RelativeTick, Is.EqualTo(1));
                Assert.That(canceledFrame.IsTerminal, Is.True);
                Assert.That(
                    fixture.Port.ActiveEnemySkillCapacityReservationCount,
                    Is.Zero);
                Assert.That(
                    fixture.Kernel.ProjectileBudget.ReservedUnits,
                    Is.Zero);
                Assert.That(
                    fixture.Kernel.ProjectileBudget.ActiveUnits,
                    Is.EqualTo(1));
                Assert.That(fixture.Port.ActiveProjectileCount, Is.EqualTo(1));

                Assert.That(
                    fixture.Scheduler.Tick(new TickIndex(2L)).IsSuccess,
                    Is.True);
                Assert.That(
                    fixture.Scheduler.Tick(new TickIndex(3L)).IsSuccess,
                    Is.True);
                Assert.That(
                    fixture.EnemyRuntime.AdvanceStartOfTick(
                        new TickIndex(4L)),
                    Is.True);
                for (long tick = 4L; tick < 7L; tick++)
                {
                    Assert.That(
                        fixture.Scheduler.Tick(new TickIndex(tick)).IsSuccess,
                        Is.True);
                }

                Assert.That(starts.Count, Is.EqualTo(1));
                Assert.That(
                    fixture.Scheduler.Tick(new TickIndex(7L)).IsSuccess,
                    Is.True);
                Assert.That(starts.Count, Is.EqualTo(2));
                Assert.That(starts[1].ExecutionId.Value, Is.EqualTo(2L));
            }
            finally
            {
                fixture.Dispose();
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void SequenceFramesCarryAbsoluteStateAndResolvedAnimation()
        {
            SchedulerFixture fixture = new SchedulerFixture(
                projectileCapacity: 2,
                impactCapacity: 8,
                projectileBudgetCapacity: 2,
                projectileReservationCapacity: 2);
            FpgEnemyAttackDefinition skill = CreateTimedImpactSkill(
                "enemy.sequence.frame",
                durationTicks: 3,
                cooldownTicks: 4,
                new AuthoredEvent(0, 0));
            FpgEnemyDefinition enemy = CreateEnemy(skill);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty execute = serialized
                    .FindProperty("sequences")
                    .GetArrayElementAtIndex(0);
                SerializedProperty variants = execute
                    .FindPropertyRelative("alternateAnimations");
                variants.arraySize = 1;
                variants.GetArrayElementAtIndex(0).stringValue =
                    "enemy_combo_alt";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(skill.TryValidate(out string error), Is.True, error);

                fixture.Register(enemy);
                AssertTickAndProcess(fixture, 0L);

                Assert.That(
                    fixture.Scheduler.SequenceFrameCount,
                    Is.EqualTo(1));
                FpgFormalEnemySkillSequenceFrame frame =
                    fixture.Scheduler.GetSequenceFrame(0);
                Assert.That(
                    frame.State,
                    Is.EqualTo(FpgSkillExecutionState.Running));
                Assert.That(frame.RelativeTick, Is.Zero);
                Assert.That(frame.ExecutionId.Value, Is.EqualTo(1L));
                Assert.That(
                    frame.ResolvedAnimationId,
                    Is.EqualTo(
                        frame.CompiledSequence.ResolveAnimation(
                            frame.ExecutionId)));
                Assert.That(
                    FpgEnemySkillPresentationResolver
                        .TryResolveAnimationName(
                            skill,
                            FpgSkillSequenceKind.Execute,
                            frame.ResolvedAnimationId,
                            out string animationName),
                    Is.True);
                Assert.That(
                    animationName,
                    Does.StartWith("enemy_combo"));

                AssertTickAndProcess(fixture, 1L);
                frame = fixture.Scheduler.GetSequenceFrame(0);
                Assert.That(frame.RelativeTick, Is.EqualTo(1));
                Assert.That(
                    frame.State,
                    Is.EqualTo(FpgSkillExecutionState.Running));

                AssertTickAndProcess(fixture, 2L);
                AssertTickAndProcess(fixture, 3L);
                frame = fixture.Scheduler.GetSequenceFrame(0);
                Assert.That(frame.RelativeTick, Is.EqualTo(3));
                Assert.That(
                    frame.State,
                    Is.EqualTo(FpgSkillExecutionState.Completed));
                Assert.That(frame.IsTerminal, Is.True);
            }
            finally
            {
                fixture.Dispose();
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void PreparedRegistrationAndRunningTickAllocateNoManagedMemory()
        {
            SchedulerFixture fixture = new SchedulerFixture(
                projectileCapacity: 2,
                impactCapacity: 8,
                projectileBudgetCapacity: 2,
                projectileReservationCapacity: 2);
            FpgEnemyAttackDefinition skill = CreateTimedImpactSkill(
                "enemy.no.alloc",
                durationTicks: 3,
                cooldownTicks: 4,
                new AuthoredEvent(0, 0));
            FpgEnemyDefinition enemy = CreateEnemy(skill);
            try
            {
                Assert.That(
                    fixture.Scheduler.TryPrepareEnemyDefinition(enemy)
                        .IsSuccess,
                    Is.True);

                GC.GetAllocatedBytesForCurrentThread();
                long beforeRegistration =
                    GC.GetAllocatedBytesForCurrentThread();
                DomainResult registered =
                    fixture.Scheduler.TryRegisterEnemy(
                        fixture.EnemyId,
                        0,
                        new TickIndex(0L),
                        0,
                        enemy);
                long registrationBytes =
                    GC.GetAllocatedBytesForCurrentThread()
                    - beforeRegistration;
                Assert.That(registered.IsSuccess, Is.True);
                Assert.That(registrationBytes, Is.Zero);

                AssertTickAndProcess(fixture, 0L);
                GC.GetAllocatedBytesForCurrentThread();
                long beforeTick = GC.GetAllocatedBytesForCurrentThread();
                DomainResult ticked = fixture.Scheduler.Tick(
                    new TickIndex(1L));
                long tickBytes = GC.GetAllocatedBytesForCurrentThread()
                    - beforeTick;
                Assert.That(ticked.IsSuccess, Is.True);
                Assert.That(tickBytes, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void TimelineWarningsAndCuesResolveForPresentationBinding()
        {
            SchedulerFixture fixture = new SchedulerFixture(
                projectileCapacity: 2,
                impactCapacity: 8,
                projectileBudgetCapacity: 2,
                projectileReservationCapacity: 2);
            FpgEnemyAttackDefinition skill = CreateTimedImpactSkill(
                "enemy.presentation.events",
                durationTicks: 3,
                cooldownTicks: 4,
                new AuthoredEvent(3, 0));
            FpgEnemyDefinition enemy = CreateEnemy(skill);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty execute = serialized
                    .FindProperty("sequences")
                    .GetArrayElementAtIndex(0);
                SerializedProperty cues = execute
                    .FindPropertyRelative("presentationCues");
                cues.arraySize = 1;
                SerializedProperty cue = cues.GetArrayElementAtIndex(0);
                cue.FindPropertyRelative("eventId").stringValue =
                    "event.cue.attack";
                cue.FindPropertyRelative("tick").intValue = 1;
                cue.FindPropertyRelative("cueId").stringValue =
                    "vfx.enemy.attack";
                cue.FindPropertyRelative("authoredOrdinal").intValue = 0;
                cue.FindPropertyRelative("socketId").stringValue =
                    "enemy.muzzle";

                SerializedProperty warnings = execute
                    .FindPropertyRelative("warnings");
                warnings.arraySize = 1;
                SerializedProperty warning =
                    warnings.GetArrayElementAtIndex(0);
                warning.FindPropertyRelative("eventId").stringValue =
                    "event.warning.attack";
                warning.FindPropertyRelative("warningId").stringValue =
                    "warning.enemy.attack";
                warning.FindPropertyRelative("startTick").intValue = 0;
                warning.FindPropertyRelative("endTick").intValue = 2;
                warning.FindPropertyRelative("authoredOrdinal").intValue = 0;
                warning.FindPropertyRelative("socketId").stringValue =
                    "enemy.muzzle";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(skill.TryValidate(out string error), Is.True, error);

                List<FpgFormalEnemySkillTimelineEvent> presented =
                    new List<FpgFormalEnemySkillTimelineEvent>();
                fixture.Scheduler.TimelineEvent += presented.Add;
                fixture.Register(enemy);
                AssertTickAndProcess(fixture, 0L);
                AssertTickAndProcess(fixture, 1L);
                AssertTickAndProcess(fixture, 2L);

                FpgFormalEnemySkillTimelineEvent warningStarted =
                    presented.Find(value => value.Event.Kind
                        == FpgSkillEventKind.WarningStarted);
                FpgFormalEnemySkillTimelineEvent cueEvent =
                    presented.Find(value => value.Event.Kind
                        == FpgSkillEventKind.PresentationCue);
                FpgFormalEnemySkillTimelineEvent warningEnded =
                    presented.Find(value => value.Event.Kind
                        == FpgSkillEventKind.WarningEnded);

                Assert.That(
                    FpgEnemySkillPresentationResolver.TryResolveWarning(
                        skill,
                        warningStarted.RuntimeEvent.SequenceKind,
                        warningStarted.Event,
                        out FpgResolvedEnemySkillWarning resolvedStart),
                    Is.True);
                Assert.That(
                    resolvedStart.WarningName,
                    Is.EqualTo("warning.enemy.attack"));
                Assert.That(
                    FpgEnemySkillPresentationResolver.TryResolveCue(
                        skill,
                        cueEvent.RuntimeEvent.SequenceKind,
                        cueEvent.Event,
                        out FpgResolvedEnemySkillCue resolvedCue),
                    Is.True);
                Assert.That(
                    resolvedCue.CueName,
                    Is.EqualTo("vfx.enemy.attack"));
                Assert.That(
                    FpgEnemySkillPresentationResolver.TryResolveWarning(
                        skill,
                        warningEnded.RuntimeEvent.SequenceKind,
                        warningEnded.Event,
                        out FpgResolvedEnemySkillWarning resolvedEnd),
                    Is.True);


                GameObject bridgeObject =
                    new GameObject("EnemySkillFeedbackBridge");
                bridgeObject.SetActive(false);
                try
                {
                    FpgFormalCombatFeedbackBridge bridge =
                        bridgeObject.AddComponent<
                            FpgFormalCombatFeedbackBridge>();
                    RecordingEnemySkillPresentationConsumer consumer =
                        new RecordingEnemySkillPresentationConsumer();
                    Type bridgeType =
                        typeof(FpgFormalCombatFeedbackBridge);
                    System.Reflection.BindingFlags flags =
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic;
                    bridgeType.GetField(
                            "enemySkillPresentationConsumer",
                            flags)
                        .SetValue(bridge, consumer);
                    System.Reflection.FieldInfo warningBindingsField =
                        bridgeType.GetField(
                            "enemySkillWarnings",
                            flags);
                    Array warningBindings = Array.CreateInstance(
                        warningBindingsField.FieldType.GetElementType(),
                        4);
                    warningBindingsField.SetValue(
                        bridge,
                        warningBindings);

                    bridgeType.GetMethod(
                            "PresentEnemySkillCue",
                            flags)
                        .Invoke(bridge, new object[] { cueEvent });
                    bridgeType.GetMethod(
                            "PresentEnemySkillWarning",
                            flags)
                        .Invoke(
                            bridge,
                            new object[] { warningStarted });
                    bridgeType.GetMethod(
                            "PresentEnemySkillWarning",
                            flags)
                        .Invoke(
                            bridge,
                            new object[] { warningEnded });

                    Assert.That(consumer.CueCount, Is.EqualTo(1));
                    Assert.That(
                        consumer.WarningStates,
                        Is.EqualTo(new[] { true, false }));
                    Assert.That(
                        bridge.EnemySkillTimelineFaultCount,
                        Is.Zero);
                    bridgeType.GetMethod(
                            "ClearEnemySkillWarnings",
                            flags)
                        .Invoke(bridge, Array.Empty<object>());
                    Assert.That(consumer.ClearCount, Is.EqualTo(1));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bridgeObject);
                }
                Assert.That(
                    resolvedEnd.WarningName,
                    Is.EqualTo(resolvedStart.WarningName));
            }
            finally
            {
                fixture.Dispose();
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }
        [Test]
        public void BoundCueRequiresMatchingSuccessfulGameplayCommit()
        {
            SchedulerFixture committedFixture = new SchedulerFixture(
                projectileCapacity: 2,
                impactCapacity: 8,
                projectileBudgetCapacity: 2,
                projectileReservationCapacity: 2);
            FpgEnemyAttackDefinition committedSkill =
                CreateTimedImpactSkill(
                    "enemy.bound.cue.committed",
                    durationTicks: 1,
                    cooldownTicks: 4,
                    new AuthoredEvent(0, 0));
            FpgEnemyDefinition committedEnemy =
                CreateEnemy(committedSkill);
            try
            {
                ConfigurePresentationCue(
                    committedSkill,
                    tick: 0,
                    cueId: "vfx.enemy.committed",
                    bindGameplayEventId: "event.attack.0");
                int triggeredCues = 0;
                committedFixture.Scheduler.TimelineEvent += value =>
                {
                    if (value.Event.Kind
                            == FpgSkillEventKind.PresentationCue
                        && value.Outcome
                            == FpgSkillEventOutcome.Triggered)
                    {
                        triggeredCues++;
                    }
                };

                committedFixture.Register(committedEnemy);
                AssertTickAndProcess(committedFixture, 0L);
                Assert.That(triggeredCues, Is.EqualTo(1));
            }
            finally
            {
                committedFixture.Dispose();
                UnityEngine.Object.DestroyImmediate(committedEnemy);
                UnityEngine.Object.DestroyImmediate(committedSkill);
            }

            SchedulerFixture canceledFixture = new SchedulerFixture(
                projectileCapacity: 2,
                impactCapacity: 8,
                projectileBudgetCapacity: 2,
                projectileReservationCapacity: 2);
            FpgEnemyAttackDefinition canceledSkill =
                CreateTimedImpactSkill(
                    "enemy.bound.cue.canceled",
                    durationTicks: 3,
                    cooldownTicks: 4,
                    new AuthoredEvent(2, 0));
            FpgEnemyDefinition canceledEnemy =
                CreateEnemy(canceledSkill);
            try
            {
                ConfigurePresentationCue(
                    canceledSkill,
                    tick: 2,
                    cueId: "vfx.enemy.canceled",
                    bindGameplayEventId: "event.attack.0");
                int triggeredCues = 0;
                int canceledCues = 0;
                canceledFixture.Scheduler.TimelineEvent += value =>
                {
                    if (value.Event.Kind
                        != FpgSkillEventKind.PresentationCue)
                    {
                        return;
                    }

                    if (value.Outcome
                        == FpgSkillEventOutcome.Triggered)
                    {
                        triggeredCues++;
                    }
                    else if (value.Outcome
                        == FpgSkillEventOutcome.Canceled)
                    {
                        canceledCues++;
                    }
                };

                canceledFixture.Register(canceledEnemy);
                AssertTickAndProcess(canceledFixture, 0L);
                Assert.That(
                    canceledFixture.EnemyRuntime.EnterGroggy(
                        new TickIndex(1L),
                        canceledFixture.Kernel.ProjectileBudget),
                    Is.GreaterThanOrEqualTo(0));
                Assert.That(
                    canceledFixture.Scheduler.Tick(
                        new TickIndex(1L)).IsSuccess,
                    Is.True);
                Assert.That(triggeredCues, Is.Zero);
                Assert.That(canceledCues, Is.EqualTo(1));
            }
            finally
            {
                canceledFixture.Dispose();
                UnityEngine.Object.DestroyImmediate(canceledEnemy);
                UnityEngine.Object.DestroyImmediate(canceledSkill);
            }
        }
        [Test]
        public void GameplayEventsResampleSpatialPathAndEmitCorrelatedTrace()
        {
            RecordingProjectileWorldPort projectileWorld =
                new RecordingProjectileWorldPort();
            SchedulerFixture fixture = new SchedulerFixture(
                projectileCapacity: 3,
                impactCapacity: 12,
                projectileBudgetCapacity: 3,
                projectileReservationCapacity: 3,
                perEnemyThreatCapacity: 3,
                projectileWorldPort: projectileWorld);
            FpgEnemyAttackDefinition skill = CreateProjectileSkill(
                "enemy.spatial.correlation",
                durationTicks: 2,
                cooldownTicks: 4,
                eventTicks: new[] { 0, 0, 2 });
            FpgEnemyDefinition enemy = CreateEnemy(skill);
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty logic = serialized
                    .FindProperty("sequences")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("logicEvents");
                logic.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("socketId").stringValue =
                    "enemy.left";
                logic.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("targetOffset").vector3Value =
                    new Vector3(0.1f, 0f, 0f);
                logic.GetArrayElementAtIndex(1)
                    .FindPropertyRelative("socketId").stringValue =
                    "enemy.right";
                logic.GetArrayElementAtIndex(1)
                    .FindPropertyRelative("targetOffset").vector3Value =
                    new Vector3(0.2f, 0f, 0f);
                logic.GetArrayElementAtIndex(2)
                    .FindPropertyRelative("socketId").stringValue =
                    "enemy.left";
                logic.GetArrayElementAtIndex(2)
                    .FindPropertyRelative("targetOffset").vector3Value =
                    new Vector3(0.1f, 0f, 0f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(
                    skill.TryValidate(out string validationError),
                    Is.True,
                    validationError);

                fixture.Register(enemy);
                AssertTickAndProcess(fixture, 0L);
                AssertTickAndProcess(fixture, 1L);
                AssertTickAndProcess(fixture, 2L);

                Assert.That(
                    fixture.SpatialSampler.SampleTicks.Count,
                    Is.EqualTo(3));
                Assert.That(
                    fixture.SpatialSampler.SampleTicks[0].Value,
                    Is.EqualTo(0L));
                Assert.That(
                    fixture.SpatialSampler.SampleTicks[1].Value,
                    Is.EqualTo(0L));
                Assert.That(
                    fixture.SpatialSampler.SampleTicks[2].Value,
                    Is.EqualTo(2L));

                Assert.That(projectileWorld.Spawns.Count, Is.EqualTo(3));
                for (int index = 0;
                    index < projectileWorld.Spawns.Count;
                    index++)
                {
                    Assert.That(
                        projectileWorld.Spawns[index].HasExplicitPath,
                        Is.True);
                }

                Assert.That(
                    projectileWorld.Spawns[0].ExplicitStart,
                    Is.Not.EqualTo(
                        projectileWorld.Spawns[1].ExplicitStart));
                Assert.That(
                    projectileWorld.Spawns[0].ExplicitEnd,
                    Is.Not.EqualTo(
                        projectileWorld.Spawns[1].ExplicitEnd));
                Assert.That(
                    projectileWorld.Spawns[0].ExplicitStart,
                    Is.EqualTo(
                        projectileWorld.Spawns[2].ExplicitStart));
                Assert.That(
                    projectileWorld.Spawns[0].ExplicitEnd,
                    Is.Not.EqualTo(
                        projectileWorld.Spawns[2].ExplicitEnd));

                int committedCount = 0;
                long executionId = 0L;
                HashSet<int> gameplayEventIds = new HashSet<int>();
                for (int index = 0;
                    index < fixture.Kernel.Trace.Count;
                    index++)
                {
                    CombatEvent trace =
                        fixture.Kernel.Trace.GetOldest(index);
                    if (trace.EventType
                        != CombatEventType.SkillGameplayCommitted)
                    {
                        continue;
                    }

                    committedCount++;
                    Assert.That(trace.HasSkillCorrelation, Is.True);
                    if (executionId == 0L)
                    {
                        executionId = trace.SkillExecutionId;
                    }

                    Assert.That(
                        trace.SkillExecutionId,
                        Is.EqualTo(executionId));
                    gameplayEventIds.Add(trace.GameplayEventId);
                }

                Assert.That(committedCount, Is.EqualTo(3));
                Assert.That(executionId, Is.EqualTo(1L));
                Assert.That(gameplayEventIds.Count, Is.EqualTo(3));
            }
            finally
            {
                fixture.Dispose();
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void FailedRuntimeStartDoesNotConsumeEnemyExecutionId()
        {
            SchedulerFixture fixture = new SchedulerFixture(
                projectileCapacity: 2,
                impactCapacity: 4,
                projectileBudgetCapacity: 2,
                projectileReservationCapacity: 2);
            FpgEnemyAttackDefinition skill = CreateTimedImpactSkill(
                "enemy.execution.transaction",
                durationTicks: 0,
                cooldownTicks: 4,
                new AuthoredEvent(0, 0));
            FpgEnemyDefinition enemy = CreateEnemy(skill);
            try
            {
                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledEnemySkillDefinition compiled,
                        out string compileError),
                    Is.True,
                    compileError);
                Assert.That(
                    compiled.Timeline.TryGetSequence(
                        FpgSkillSequenceKind.Execute,
                        out FpgCompiledSkillSequence execute),
                    Is.True);
                fixture.Register(enemy);

                System.Reflection.FieldInfo patternsField =
                    typeof(FpgFormalEnemyAttackScheduler).GetField(
                        "patterns",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic);
                Assert.That(patternsField, Is.Not.Null);
                Array patterns = (Array)patternsField.GetValue(
                    fixture.Scheduler);
                object pattern = null;
                for (int index = 0; index < patterns.Length; index++)
                {
                    object candidate = patterns.GetValue(index);
                    System.Reflection.FieldInfo isUsed =
                        candidate.GetType().GetField(
                            "IsUsed",
                            System.Reflection.BindingFlags.Instance
                                | System.Reflection.BindingFlags.Public
                                | System.Reflection.BindingFlags.NonPublic);
                    Assert.That(isUsed, Is.Not.Null);
                    if ((bool)isUsed.GetValue(candidate))
                    {
                        pattern = candidate;
                        break;
                    }
                }

                Assert.That(pattern, Is.Not.Null);
                System.Reflection.PropertyInfo executeProperty =
                    pattern.GetType().GetProperty(
                        "Execute",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.Public
                            | System.Reflection.BindingFlags.NonPublic);
                Assert.That(executeProperty, Is.Not.Null);
                executeProperty.SetValue(
                    pattern,
                    default(FpgCompiledSkillSequence));

                List<SkillExecutionId> starts =
                    new List<SkillExecutionId>();
                fixture.Scheduler.SkillStarted += value =>
                    starts.Add(value.ExecutionId);
                Assert.That(
                    fixture.Scheduler.Tick(new TickIndex(0L)).IsSuccess,
                    Is.False);

                executeProperty.SetValue(pattern, execute);
                Assert.That(
                    fixture.Scheduler.Tick(new TickIndex(1L)).IsSuccess,
                    Is.True);
                Assert.That(starts.Count, Is.EqualTo(1));
                Assert.That(starts[0].Value, Is.EqualTo(1L));
            }
            finally
            {
                fixture.Dispose();
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void SummonCommittedQuotaSurvivesOwnerUnregister()
        {
            SchedulerFixture fixture = new SchedulerFixture(
                projectileCapacity: 2,
                impactCapacity: 4,
                projectileBudgetCapacity: 2,
                projectileReservationCapacity: 2);
            FpgEnemyDefinition candidate =
                CreateSummonCandidate("enemy.summon.candidate.unregister");
            FpgEnemyAttackDefinition skill = CreateSummonSkill(
                "enemy.summon.unregister",
                durationTicks: 0,
                cooldownTicks: 4,
                candidate,
                maxPerOwner: 2,
                maxPerEncounter: 2,
                new AuthoredEvent(0, 0));
            FpgEnemyDefinition enemy = CreateEnemy(skill);
            try
            {
                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledEnemySkillDefinition compiled,
                        out string compileError),
                    Is.True,
                    compileError);
                int actionStableId =
                    compiled.PayloadSlots[0].SummonPayload.ActionStableId;
                fixture.Register(enemy);

                Assert.That(
                    fixture.Scheduler.Tick(new TickIndex(0L)).IsSuccess,
                    Is.True);
                Assert.That(
                    fixture.Scheduler.TryGetSummonQuotaState(
                        actionStableId,
                        out int committed,
                        out int reserved),
                    Is.True);
                Assert.That(committed, Is.EqualTo(1));
                Assert.That(reserved, Is.Zero);

                Assert.That(
                    fixture.Scheduler.TryUnregisterEnemy(fixture.EnemyId)
                        .IsSuccess,
                    Is.True);
                Assert.That(
                    fixture.Scheduler.TryGetSummonQuotaState(
                        actionStableId,
                        out committed,
                        out reserved),
                    Is.True);
                Assert.That(committed, Is.EqualTo(1));
                Assert.That(reserved, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(skill);
                UnityEngine.Object.DestroyImmediate(candidate);
            }
        }

        [Test]
        public void SimultaneousSummonExecutionsCannotOverReserveGlobalQuota()
        {
            SchedulerFixture fixture = new SchedulerFixture(
                projectileCapacity: 2,
                impactCapacity: 4,
                projectileBudgetCapacity: 2,
                projectileReservationCapacity: 2);
            FpgEnemyDefinition candidate =
                CreateSummonCandidate("enemy.summon.candidate.concurrent");
            FpgEnemyAttackDefinition skill = CreateSummonSkill(
                "enemy.summon.concurrent",
                durationTicks: 1,
                cooldownTicks: 4,
                candidate,
                maxPerOwner: 1,
                maxPerEncounter: 1,
                new AuthoredEvent(1, 0));
            FpgEnemyDefinition enemy = CreateEnemy(skill);
            try
            {
                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledEnemySkillDefinition compiled,
                        out string compileError),
                    Is.True,
                    compileError);
                int actionStableId =
                    compiled.PayloadSlots[0].SummonPayload.ActionStableId;
                fixture.Register(enemy);
                fixture.RegisterAdditional(enemy);

                Assert.That(
                    fixture.Scheduler.Tick(new TickIndex(0L)).IsSuccess,
                    Is.True);
                Assert.That(
                    fixture.Scheduler.TryGetSummonQuotaState(
                        actionStableId,
                        out int committed,
                        out int reserved),
                    Is.True);
                Assert.That(committed, Is.Zero);
                Assert.That(reserved, Is.EqualTo(1));
                Assert.That(fixture.Port.PendingAttackCount, Is.Zero);

                Assert.That(
                    fixture.Scheduler.Tick(new TickIndex(1L)).IsSuccess,
                    Is.True);
                Assert.That(
                    fixture.Scheduler.TryGetSummonQuotaState(
                        actionStableId,
                        out committed,
                        out reserved),
                    Is.True);
                Assert.That(committed, Is.EqualTo(1));
                Assert.That(reserved, Is.Zero);
                Assert.That(fixture.Port.PendingAttackCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(skill);
                UnityEngine.Object.DestroyImmediate(candidate);
            }
        }

        [Test]
        public void InterruptedSummonExecutionReleasesOnlyUntriggeredQuota()
        {
            SchedulerFixture fixture = new SchedulerFixture(
                projectileCapacity: 2,
                impactCapacity: 4,
                projectileBudgetCapacity: 2,
                projectileReservationCapacity: 2);
            FpgEnemyDefinition candidate =
                CreateSummonCandidate("enemy.summon.candidate.interrupt");
            FpgEnemyAttackDefinition skill = CreateSummonSkill(
                "enemy.summon.interrupt",
                durationTicks: 2,
                cooldownTicks: 4,
                candidate,
                maxPerOwner: 2,
                maxPerEncounter: 2,
                new AuthoredEvent(0, 0),
                new AuthoredEvent(2, 1));
            FpgEnemyDefinition enemy = CreateEnemy(skill);
            try
            {
                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledEnemySkillDefinition compiled,
                        out string compileError),
                    Is.True,
                    compileError);
                int actionStableId =
                    compiled.PayloadSlots[0].SummonPayload.ActionStableId;
                fixture.Register(enemy);

                Assert.That(
                    fixture.Scheduler.Tick(new TickIndex(0L)).IsSuccess,
                    Is.True);
                Assert.That(
                    fixture.Scheduler.TryGetSummonQuotaState(
                        actionStableId,
                        out int committed,
                        out int reserved),
                    Is.True);
                Assert.That(committed, Is.EqualTo(1));
                Assert.That(reserved, Is.EqualTo(1));

                Assert.That(
                    fixture.EnemyRuntime.EnterGroggy(
                        new TickIndex(1L),
                        fixture.Kernel.ProjectileBudget),
                    Is.GreaterThanOrEqualTo(0));
                Assert.That(
                    fixture.Scheduler.Tick(new TickIndex(1L)).IsSuccess,
                    Is.True);
                Assert.That(
                    fixture.Scheduler.TryGetSummonQuotaState(
                        actionStableId,
                        out committed,
                        out reserved),
                    Is.True);
                Assert.That(committed, Is.EqualTo(1));
                Assert.That(reserved, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(skill);
                UnityEngine.Object.DestroyImmediate(candidate);
            }
        }

        private static void AssertTickAndProcess(
            SchedulerFixture fixture,
            long tickValue)
        {
            TickIndex tick = new TickIndex(tickValue);
            Assert.That(fixture.Scheduler.Tick(tick).IsSuccess, Is.True);
            Assert.That(
                fixture.Port.Process(
                    FpgBattleTickPhase.EnemyAttackDirector,
                    tick,
                    fixture.Roster).IsSuccess,
                Is.True);
            Assert.That(
                fixture.Port.Process(
                    FpgBattleTickPhase.ThreatAndProjectileAdvance,
                    tick,
                    fixture.Roster).IsSuccess,
                Is.True);
        }

        private static int CountDistinctAttackStarts(ICombatTraceView trace)
        {
            HashSet<long> ids = new HashSet<long>();
            for (int index = 0; index < trace.Count; index++)
            {
                CombatEvent value = trace.GetOldest(index);
                if (value.EventType == CombatEventType.ThreatScheduleDecision
                    && value.AttackId.IsValid)
                {
                    ids.Add(value.AttackId.Value);
                }
            }

            return ids.Count;
        }

        private static FpgEnemyAttackDefinition CreateTimedImpactSkill(
            string skillId,
            int durationTicks,
            int cooldownTicks,
            params AuthoredEvent[] authoredEvents)
        {
            FpgEnemyAttackDefinition skill =
                ScriptableObject.CreateInstance<FpgEnemyAttackDefinition>();
            SerializedObject serialized = new SerializedObject(skill);
            ConfigureSkillIdentity(serialized, skillId, cooldownTicks);
            SerializedProperty payload = serialized.FindProperty("payloadSlots")
                .GetArrayElementAtIndex(0);
            payload.FindPropertyRelative("slotId").stringValue = "payload.impact";
            payload.FindPropertyRelative("kind").enumValueIndex =
                (int)FpgEnemySkillPayloadKind.TimedImpact;
            payload.FindPropertyRelative("threatDefinitionId").intValue = 101;
            payload.FindPropertyRelative("baseDamage").intValue = 5;
            payload.FindPropertyRelative("breakDamage").intValue = 1;
            payload.FindPropertyRelative(
                "weakpointDamageMultiplierBasisPoints").intValue = 10000;
            payload.FindPropertyRelative(
                "weakpointBreakMultiplierBasisPoints").intValue = 10000;
            payload.FindPropertyRelative("timedImpactTargetPolicy")
                .enumValueIndex = (int)ThreatTargetPolicy.PlayerCombatant;
            payload.FindPropertyRelative("timedImpactDelayTicks").intValue = 20;
            payload.FindPropertyRelative("timedImpactPresentationKey")
                .intValue = 21;
            ConfigureExecute(
                serialized,
                durationTicks,
                "enemy_combo",
                "payload.impact",
                authoredEvents);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(skill.TryValidate(out string error), Is.True, error);
            return skill;
        }

        private static FpgEnemyAttackDefinition CreateProjectileSkill(
            string skillId,
            int durationTicks,
            int cooldownTicks,
            int[] eventTicks)
        {
            AuthoredEvent[] authored = new AuthoredEvent[eventTicks.Length];
            for (int index = 0; index < eventTicks.Length; index++)
            {
                authored[index] = new AuthoredEvent(eventTicks[index], index);
            }

            FpgEnemyAttackDefinition skill =
                ScriptableObject.CreateInstance<FpgEnemyAttackDefinition>();
            SerializedObject serialized = new SerializedObject(skill);
            ConfigureSkillIdentity(serialized, skillId, cooldownTicks);
            SerializedProperty payload = serialized.FindProperty("payloadSlots")
                .GetArrayElementAtIndex(0);
            payload.FindPropertyRelative("slotId").stringValue =
                "payload.projectile";
            payload.FindPropertyRelative("kind").enumValueIndex =
                (int)FpgEnemySkillPayloadKind.Projectile;
            payload.FindPropertyRelative("threatDefinitionId").intValue = 201;
            payload.FindPropertyRelative("baseDamage").intValue = 5;
            payload.FindPropertyRelative("breakDamage").intValue = 1;
            payload.FindPropertyRelative(
                "weakpointDamageMultiplierBasisPoints").intValue = 10000;
            payload.FindPropertyRelative(
                "weakpointBreakMultiplierBasisPoints").intValue = 10000;
            payload.FindPropertyRelative("projectileDefinitionId").intValue = 301;
            payload.FindPropertyRelative("projectileCount").intValue = 1;
            payload.FindPropertyRelative("projectileFlightTicks").intValue = 20;
            payload.FindPropertyRelative("projectileLifetimeTicks").intValue = 30;
            payload.FindPropertyRelative("projectileMaxHitPoints").intValue = 0;
            payload.FindPropertyRelative("projectileInterceptable").boolValue = false;
            payload.FindPropertyRelative("projectileBudgetUnits").intValue = 1;
            payload.FindPropertyRelative("projectilePresentationKey").intValue = 31;
            payload.FindPropertyRelative("projectileSweepRadiusKey").intValue = 32;
            ConfigureExecute(
                serialized,
                durationTicks,
                "enemy_projectile",
                "payload.projectile",
                authored);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(skill.TryValidate(out string error), Is.True, error);
            return skill;
        }

        private static FpgEnemyAttackDefinition CreateSummonSkill(
            string skillId,
            int durationTicks,
            int cooldownTicks,
            FpgEnemyDefinition candidate,
            int maxPerOwner,
            int maxPerEncounter,
            params AuthoredEvent[] authoredEvents)
        {
            FpgEnemyAttackDefinition skill =
                ScriptableObject.CreateInstance<FpgEnemyAttackDefinition>();
            SerializedObject serialized = new SerializedObject(skill);
            ConfigureSkillIdentity(serialized, skillId, cooldownTicks);
            SerializedProperty payload = serialized.FindProperty("payloadSlots")
                .GetArrayElementAtIndex(0);
            payload.FindPropertyRelative("slotId").stringValue =
                "payload.summon";
            payload.FindPropertyRelative("displayName").stringValue =
                "Summon";
            payload.FindPropertyRelative("kind").enumValueIndex =
                (int)FpgEnemySkillPayloadKind.Summon;
            SerializedProperty candidates =
                payload.FindPropertyRelative("summonCandidates");
            candidates.arraySize = 1;
            candidates.GetArrayElementAtIndex(0).objectReferenceValue =
                candidate;
            SerializedProperty weights =
                payload.FindPropertyRelative("summonCandidateWeights");
            weights.arraySize = 1;
            weights.GetArrayElementAtIndex(0).intValue = 1;
            payload.FindPropertyRelative("summonOccupancyMode")
                .enumValueIndex =
                (int)FpgSummonOccupancyMode.AdditionalEntity;
            payload.FindPropertyRelative("summonPlacementMode")
                .enumValueIndex =
                (int)FpgSummonPlacementMode.EncounterSpawnPoint;
            payload.FindPropertyRelative("summonOwnerOutcome")
                .enumValueIndex =
                (int)FpgSummonOwnerOutcome.RemainAlive;
            payload.FindPropertyRelative("maxSummonsPerOwner").intValue =
                maxPerOwner;
            payload.FindPropertyRelative(
                "maxTotalSummonsPerEncounter").intValue =
                maxPerEncounter;
            payload.FindPropertyRelative("maxSummonRecursionDepth")
                .intValue = 2;
            ConfigureExecute(
                serialized,
                durationTicks,
                "enemy_summon",
                "payload.summon",
                authoredEvents);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(skill.TryValidate(out string error), Is.True, error);
            return skill;
        }

        private static FpgEnemyDefinition CreateSummonCandidate(
            string enemyDefinitionId)
        {
            FpgEnemyDefinition candidate =
                ScriptableObject.CreateInstance<FpgEnemyDefinition>();
            SerializedObject serialized = new SerializedObject(candidate);
            serialized.FindProperty("enemyDefinitionId").stringValue =
                enemyDefinitionId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return candidate;
        }
        private static void ConfigurePresentationCue(
            FpgEnemyAttackDefinition skill,
            int tick,
            string cueId,
            string bindGameplayEventId)
        {
            SerializedObject serialized = new SerializedObject(skill);
            SerializedProperty execute = serialized
                .FindProperty("sequences")
                .GetArrayElementAtIndex(0);
            SerializedProperty cues = execute
                .FindPropertyRelative("presentationCues");
            cues.arraySize = 1;
            SerializedProperty cue = cues.GetArrayElementAtIndex(0);
            cue.FindPropertyRelative("eventId").stringValue =
                "event.cue.bound";
            cue.FindPropertyRelative("tick").intValue = tick;
            cue.FindPropertyRelative("cueId").stringValue = cueId;
            cue.FindPropertyRelative("authoredOrdinal").intValue = 1;
            cue.FindPropertyRelative("socketId").stringValue =
                "enemy.muzzle";
            cue.FindPropertyRelative("bindGameplayEventId")
                .stringValue = bindGameplayEventId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(skill.TryValidate(out string error), Is.True, error);
        }
        private static void ConfigureSkillIdentity(
            SerializedObject serialized,
            string skillId,
            int cooldownTicks)
        {
            serialized.FindProperty("skillId").stringValue = skillId;
            serialized.FindProperty("displayName").stringValue = skillId;
            serialized.FindProperty("priority").intValue = 0;
            serialized.FindProperty("firstReadyOffsetTicks").intValue = 0;
            serialized.FindProperty("sequenceCooldownTicks").intValue =
                cooldownTicks;
        }

        private static void ConfigureExecute(
            SerializedObject serialized,
            int durationTicks,
            string animation,
            string payloadSlotId,
            AuthoredEvent[] authoredEvents)
        {
            SerializedProperty sequences = serialized.FindProperty("sequences");
            sequences.arraySize = 1;
            SerializedProperty execute = sequences.GetArrayElementAtIndex(0);
            execute.FindPropertyRelative("kind").enumValueIndex =
                (int)FpgSkillSequenceKind.Execute;
            execute.FindPropertyRelative("durationTicks").intValue =
                durationTicks;
            execute.FindPropertyRelative("mainAnimation").stringValue = animation;
            execute.FindPropertyRelative("loop").boolValue = false;
            execute.FindPropertyRelative("phases").arraySize = 0;
            execute.FindPropertyRelative("presentationCues").arraySize = 0;
            execute.FindPropertyRelative("warnings").arraySize = 0;

            SerializedProperty logic =
                execute.FindPropertyRelative("logicEvents");
            logic.arraySize = authoredEvents.Length;
            for (int index = 0; index < authoredEvents.Length; index++)
            {
                AuthoredEvent authored = authoredEvents[index];
                SerializedProperty value = logic.GetArrayElementAtIndex(index);
                value.FindPropertyRelative("eventId").stringValue =
                    "event.attack." + index;
                value.FindPropertyRelative("tick").intValue = authored.Tick;
                value.FindPropertyRelative("payloadSlotId").stringValue =
                    payloadSlotId;
                value.FindPropertyRelative("authoredOrdinal").intValue =
                    authored.Ordinal;
                value.FindPropertyRelative("socketId").stringValue =
                    string.Equals(
                        payloadSlotId,
                        "payload.projectile",
                        StringComparison.Ordinal)
                        ? "enemy.muzzle"
                        : string.Empty;
                value.FindPropertyRelative("targetSource").enumValueIndex =
                    (int)FpgSkillTargetSource.CurrentTarget;
                value.FindPropertyRelative("targetOffset").vector3Value =
                    Vector3.zero;
            }
        }

        private static FpgEnemyDefinition CreateEnemy(
            FpgEnemyAttackDefinition skill)
        {
            FpgEnemyDefinition enemy =
                ScriptableObject.CreateInstance<FpgEnemyDefinition>();
            SerializedObject serialized = new SerializedObject(enemy);
            serialized.FindProperty("enemyDefinitionId").stringValue =
                "enemy.scheduler.test";
            SerializedProperty attacks = serialized.FindProperty("attackPatterns");
            attacks.arraySize = 1;
            attacks.GetArrayElementAtIndex(0).objectReferenceValue = skill;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        private sealed class RecordingEnemySkillPresentationConsumer :
            IFpgFormalEnemySkillPresentationConsumer
        {
            public int CueCount { get; private set; }
            public int ClearCount { get; private set; }
            public List<bool> WarningStates { get; } =
                new List<bool>();

            public bool TryPresentEnemySkillCue(
                in FpgFormalEnemySkillCuePresentationEvent cueEvent)
            {
                CueCount++;
                return true;
            }

            public bool TrySetEnemySkillWarning(
                in FpgFormalEnemySkillWarningPresentationEvent warningEvent)
            {
                WarningStates.Add(warningEvent.IsActive);
                return true;
            }

            public void ClearEnemySkillWarnings()
            {
                ClearCount++;
            }
        }

        private readonly struct AuthoredEvent
        {
            public AuthoredEvent(int tick, int ordinal)
            {
                Tick = tick;
                Ordinal = ordinal;
            }

            public int Tick { get; }
            public int Ordinal { get; }
        }

        private sealed class SchedulerFixture : IDisposable
        {
            private readonly SessionIdAllocator ids = new SessionIdAllocator();

            public SchedulerFixture(
                int projectileCapacity,
                int impactCapacity,
                int projectileBudgetCapacity,
                int projectileReservationCapacity,
                int perEnemyThreatCapacity = 2,
                int attackScheduleCapacity = 4,
                IFpgSummonRequestSink summonSink = null,
                IProjectileWorldPort projectileWorldPort = null,
                FpgSkillExecutionIdAllocator executionIds = null)
            {
                Kernel = new CombatKernel(
                    projectileBudgetCapacity,
                    impactCapacity,
                    shotTargetCapacity: 4,
                    impactQueueCapacity: impactCapacity,
                    traceCapacity: 256,
                    projectileReservationCapacity:
                        projectileReservationCapacity);
                RuntimeId playerId = ids.NextRuntimeId();
                PlayerRuntime player = new PlayerRuntime(
                    new CombatantState(
                        playerId,
                        CombatantKind.Player,
                        100,
                        20,
                        0),
                    new ExposureRuntime(PlayerExposureState.Exposed),
                    new WeaponRuntime(CreateWeaponDefinition()));
                Port = new FpgMultiEnemyCombatPort(
                    Kernel,
                    player,
                    ids,
                    new FpgMultiEnemyCombatCapacity(
                        enemyCapacity: 2,
                        playerHitCommandCapacity: 8,
                        attackScheduleCapacity: attackScheduleCapacity,
                        projectileCapacity: projectileCapacity,
                        threatAdvanceCapacity: Math.Max(
                            perEnemyThreatCapacity,
                            attackScheduleCapacity),
                        perEnemyThreatCapacity: perEnemyThreatCapacity,
                        summonCapacity: 4,
                        maxTotalSummons: 4,
                        maxSummonRecursionDepth: 2,
                        vitalsEventCapacity: 32,
                        damageFeedbackCapacity: 32),
                    new TickDuration(3),
                    projectileWorldPort
                        ?? new FpgEmptyProjectileWorldPort(),
                    summonSink ?? new RejectingSummonSink());
                EnemyId = ids.NextRuntimeId();
                Assert.That(
                    Port.TryRegisterEnemy(
                        new FpgEnemyCombatantRegistration(
                            EnemyId,
                            0,
                            100,
                            20,
                            new TickDuration(3),
                            new TickIndex(0L))).IsSuccess,
                    Is.True);
                Assert.That(
                    Port.TryGetEnemyRuntime(EnemyId, out EnemyRuntime runtime),
                    Is.True);
                EnemyRuntime = runtime;
                Roster = new FpgEnemyRoster(2);
                SpatialSampler = new FixedSpatialSampler();
                Scheduler = new FpgFormalEnemyAttackScheduler(
                    Port,
                    new FpgEncounterRunContext(
                        123UL,
                        "scheduler-tests",
                        0,
                        FpgEncounterRunContext.BasisPointsOne,
                        0),
                    SpatialSampler,
                    ownerCapacity: 2,
                    patternCapacity: 4,
                    executionIds: executionIds);
            }

            public CombatKernel Kernel { get; }
            public FpgMultiEnemyCombatPort Port { get; }
            public FpgFormalEnemyAttackScheduler Scheduler { get; }
            public FixedSpatialSampler SpatialSampler { get; }
            public FpgEnemyRoster Roster { get; }
            public RuntimeId EnemyId { get; }
            public EnemyRuntime EnemyRuntime { get; }

            public void Register(FpgEnemyDefinition enemy)
            {
                Assert.That(
                    Scheduler.TryRegisterEnemy(
                        EnemyId,
                        0,
                        new TickIndex(0L),
                        0,
                        enemy).IsSuccess,
                    Is.True);
            }

            public RuntimeId RegisterAdditional(
                FpgEnemyDefinition enemy,
                int spawnSequence = 1)
            {
                RuntimeId runtimeId = ids.NextRuntimeId();
                Assert.That(
                    Port.TryRegisterEnemy(
                        new FpgEnemyCombatantRegistration(
                            runtimeId,
                            spawnSequence,
                            100,
                            20,
                            new TickDuration(3),
                            new TickIndex(0L))).IsSuccess,
                    Is.True);
                Assert.That(
                    Scheduler.TryRegisterEnemy(
                        runtimeId,
                        spawnSequence,
                        new TickIndex(0L),
                        0,
                        enemy).IsSuccess,
                    Is.True);
                return runtimeId;
            }
            public void Dispose()
            {
                Scheduler.Clear();
                Port.ClearAll();
                Kernel.Dispose();
            }

            private static WeaponDefinition CreateWeaponDefinition()
            {
                return new WeaponDefinition(
                    1,
                    8,
                    1,
                    new TickDuration(2),
                    new DamageSpec(1, 0),
                    2,
                    new TickDuration(3),
                    new DamageSpec(2, 0),
                    new TickDuration(2),
                    4);
            }
        }

        private sealed class FixedSpatialSampler :
            IFpgFormalEnemyAttackSpatialSampler
        {
            public List<TickIndex> SampleTicks { get; } =
                new List<TickIndex>();

            public DomainResult TrySample(
                TickIndex tick,
                RuntimeId ownerRuntimeId,
                RuntimeId currentTargetRuntimeId,
                string socketName,
                in FpgCompiledSkillEvent skillEvent,
                out FpgEnemyAttackSpatialContext context)
            {
                SampleTicks.Add(tick);
                context = new FpgEnemyAttackSpatialContext(
                    tick,
                    skillEvent.TargetSource,
                    skillEvent.SocketId,
                    skillEvent.Offset,
                    currentTargetRuntimeId,
                    new SpatialVectorKey(
                        skillEvent.SocketId,
                        0,
                        0),
                    new SpatialVectorKey(
                        checked(
                            (int)tick.Value
                            + 1000
                            + skillEvent.Offset.XMillimeters),
                        skillEvent.Offset.YMillimeters,
                        skillEvent.Offset.ZMillimeters));
                return DomainResult.Success;
            }
        }

        private sealed class RecordingProjectileWorldPort :
            IProjectileWorldPort
        {
            public List<ProjectileSpawnRequest> Spawns { get; } =
                new List<ProjectileSpawnRequest>();

            public DomainResult Register(
                in ProjectileSpawnRequest request,
                out ProjectilePathSnapshot path)
            {
                Spawns.Add(request);
                path = new ProjectilePathSnapshot(
                    request.ProjectileId,
                    request.RuntimeId,
                    request.Tick,
                    request.ArrivalTick,
                    request.ExplicitStart,
                    request.ExplicitEnd);
                return DomainResult.Success;
            }

            public DomainResult Sweep(
                in ProjectileSweepRequest request,
                out ProjectileSweepHit hit)
            {
                hit = ProjectileSweepHit.None;
                return DomainResult.Success;
            }

            public DomainResult Release(
                in ProjectileReleaseRequest request)
            {
                return DomainResult.Success;
            }
        }
        private sealed class RejectingSummonSink : IFpgSummonRequestSink
        {
            public FpgSummonQueueAck TryQueueSummon(
                FpgSummonRequest request,
                TickIndex tick)
            {
                return FpgSummonQueueAck.Rejected(
                    RejectReason.InvalidState);
            }
        }
    }
}
