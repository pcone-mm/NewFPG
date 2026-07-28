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
                List<FpgEnemyAttackSpatialContext> spatialContexts =
                    new List<FpgEnemyAttackSpatialContext>();
                fixture.Scheduler.SkillStarted += value =>
                    starts.Add(value.ExecutionId);
                fixture.Scheduler.TimelineEvent += value =>
                {
                    if (value.HasGameplayAction
                        && value.Outcome == FpgSkillEventOutcome.Triggered)
                    {
                        sortOrders.Add(value.Event.SortOrder);
                        eventTicks.Add(value.RuntimeEvent.Tick.Value);
                        spatialContexts.Add(value.SpatialContext);
                    }
                };

                AssertTickAndProcess(fixture, 0L);
                Assert.That(starts.Count, Is.EqualTo(1));
                Assert.That(starts[0].Value, Is.EqualTo(1L));
                CollectionAssert.AreEqual(new[] { 2, 5 }, sortOrders);
                CollectionAssert.AreEqual(new long[] { 0L, 0L }, eventTicks);
                Assert.That(spatialContexts[0].IsValid, Is.True);
                Assert.That(spatialContexts[1].IsValid, Is.True);
                Assert.That(spatialContexts[0].SampleTick,
                    Is.EqualTo(new TickIndex(0L)));

                AssertTickAndProcess(fixture, 1L);
                AssertTickAndProcess(fixture, 2L);
                CollectionAssert.AreEqual(new[] { 2, 5 }, sortOrders);

                AssertTickAndProcess(fixture, 3L);
                CollectionAssert.AreEqual(new[] { 2, 5, 1 }, sortOrders);
                CollectionAssert.AreEqual(
                    new long[] { 0L, 0L, 3L },
                    eventTicks);
                Assert.That(spatialContexts[2].SampleTick,
                    Is.EqualTo(new TickIndex(3L)));
                Assert.That(spatialContexts[2].IsValid, Is.True);
                Assert.That(spatialContexts[2].Target,
                    Is.EqualTo(new SpatialVectorKey(1003, 0, 0)));
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
                    if (value.HasGameplayAction
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
        public void MotionAuthorityReceivesCompletedFrameSynchronously()
        {
            RecordingMotionAuthority motionAuthority =
                new RecordingMotionAuthority();
            RecordingPhysicsBackend physics =
                new RecordingPhysicsBackend();
            SchedulerFixture fixture = new SchedulerFixture(
                projectileCapacity: 2,
                impactCapacity: 8,
                projectileBudgetCapacity: 2,
                projectileReservationCapacity: 2,
                motionAuthority: motionAuthority,
                physics: physics);
            FpgEnemyAttackDefinition skill = CreateTimedImpactSkill(
                "enemy.motion.completed",
                durationTicks: 1,
                cooldownTicks: 4,
                new AuthoredEvent(0, 0));
            FpgEnemyDefinition enemy = CreateEnemy(skill);
            try
            {
                fixture.Register(enemy);
                TickIndex startTick = new TickIndex(0L);
                Assert.That(
                    fixture.Scheduler.Tick(startTick).IsSuccess,
                    Is.True);
                Assert.That(motionAuthority.StartFrames, Has.Count.EqualTo(1));
                Assert.That(motionAuthority.TerminalFrames, Is.Empty);
                Assert.That(physics.SyncCount, Is.EqualTo(1));
                Assert.That(
                    fixture.Port.Process(
                        FpgBattleTickPhase.EnemyAttackDirector,
                        startTick,
                        fixture.Roster).IsSuccess,
                    Is.True);

                TickIndex terminalTick = new TickIndex(1L);
                Assert.That(
                    fixture.Scheduler.Tick(terminalTick).IsSuccess,
                    Is.True);
                Assert.That(
                    motionAuthority.TerminalFrames,
                    Has.Count.EqualTo(1));
                FpgFormalEnemySkillSequenceFrame terminal =
                    motionAuthority.TerminalFrames[0];
                Assert.That(
                    terminal.State,
                    Is.EqualTo(FpgSkillExecutionState.Completed));
                Assert.That(terminal.Tick, Is.EqualTo(terminalTick));
                Assert.That(terminal.RelativeTick, Is.EqualTo(1));
                Assert.That(
                    fixture.Scheduler.GetSequenceFrame(0).State,
                    Is.EqualTo(FpgSkillExecutionState.Completed));
            }
            finally
            {
                fixture.Dispose();
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void MotionAuthorityReceivesCanceledFrameSynchronously()
        {
            RecordingMotionAuthority motionAuthority =
                new RecordingMotionAuthority();
            RecordingPhysicsBackend physics =
                new RecordingPhysicsBackend();
            SchedulerFixture fixture = new SchedulerFixture(
                projectileCapacity: 2,
                impactCapacity: 8,
                projectileBudgetCapacity: 2,
                projectileReservationCapacity: 2,
                motionAuthority: motionAuthority,
                physics: physics);
            FpgEnemyAttackDefinition skill = CreateTimedImpactSkill(
                "enemy.motion.canceled",
                durationTicks: 3,
                cooldownTicks: 4,
                new AuthoredEvent(0, 0));
            FpgEnemyDefinition enemy = CreateEnemy(skill);
            try
            {
                fixture.Register(enemy);
                AssertTickAndProcess(fixture, 0L);
                Assert.That(
                    fixture.EnemyRuntime.EnterGroggy(
                        new TickIndex(1L),
                        fixture.Kernel.ProjectileBudget),
                    Is.GreaterThanOrEqualTo(0));

                TickIndex cancelTick = new TickIndex(1L);
                Assert.That(
                    fixture.Scheduler.Tick(cancelTick).IsSuccess,
                    Is.True);
                Assert.That(
                    motionAuthority.TerminalFrames,
                    Has.Count.EqualTo(1));
                FpgFormalEnemySkillSequenceFrame terminal =
                    motionAuthority.TerminalFrames[0];
                Assert.That(
                    terminal.State,
                    Is.EqualTo(FpgSkillExecutionState.Canceled));
                Assert.That(terminal.Tick, Is.EqualTo(cancelTick));
                Assert.That(terminal.RelativeTick, Is.EqualTo(1));
                Assert.That(
                    fixture.Scheduler.GetSequenceFrame(0).State,
                    Is.EqualTo(FpgSkillExecutionState.Canceled));
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
                    .FindPropertyRelative("projectileEvents");
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
                    Assert.That(
                        projectileWorld.Spawns[index].PresentationKind,
                        Is.EqualTo(
                            FpgThreatPresentationKind.FastUninterceptable));
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
        public void ProjectileThreatPresentationKindMustMatchInterceptability()
        {
            FpgEnemyAttackDefinition skill = CreateProjectileSkill(
                "enemy.presentation-kind.contract",
                durationTicks: 1,
                cooldownTicks: 2,
                eventTicks: new[] { 0 });
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty projectile = serialized
                    .FindProperty("sequences")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("projectileEvents")
                    .GetArrayElementAtIndex(0);
                projectile.FindPropertyRelative("projectileInterceptable")
                    .boolValue = true;
                projectile.FindPropertyRelative("projectileMaxHitPoints")
                    .intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(skill.TryValidate(out string fastError), Is.False);
                Assert.That(
                    fastError,
                    Does.Contain("does not match projectile interceptability"));

                serialized.Update();
                projectile = serialized.FindProperty("sequences")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("projectileEvents")
                    .GetArrayElementAtIndex(0);
                projectile.FindPropertyRelative("threatPresentationKind")
                    .enumValueIndex =
                    (int)FpgThreatPresentationKind.InterceptableVolley;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(
                    skill.TryValidate(out string volleyError),
                    Is.True,
                    volleyError);

                serialized.Update();
                projectile = serialized.FindProperty("sequences")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("projectileEvents")
                    .GetArrayElementAtIndex(0);
                projectile.FindPropertyRelative("threatPresentationKind")
                    .enumValueIndex =
                    (int)FpgThreatPresentationKind.HeavyWeakpoint;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(skill.TryValidate(out string heavyError), Is.False);
                Assert.That(
                    heavyError,
                    Does.Contain("does not match projectile interceptability"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        private static void AssertTickAndProcess(
            SchedulerFixture fixture,
            long tickValue)
        {
            TickIndex tick = new TickIndex(tickValue);
            DomainResult schedulerResult = fixture.Scheduler.Tick(tick);
            Assert.That(
                schedulerResult.IsSuccess,
                Is.True,
                schedulerResult.RejectReason.ToString());
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
            serialized.FindProperty("authoringSchemaVersion").intValue =
                FpgSkillTimelineDefinition.CurrentAuthoringSchemaVersion;
            ConfigureExecute(
                serialized,
                durationTicks,
                "enemy_combo");
            SerializedProperty attacks = serialized.FindProperty("sequences")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("attackEvents");
            attacks.arraySize = authoredEvents.Length;
            for (int index = 0; index < authoredEvents.Length; index++)
            {
                AuthoredEvent authored = authoredEvents[index];
                SerializedProperty attack = attacks.GetArrayElementAtIndex(index);
                attack.FindPropertyRelative("eventId").stringValue =
                    "event.attack." + index;
                attack.FindPropertyRelative("tick").intValue = authored.Tick;
                attack.FindPropertyRelative("authoredOrdinal").intValue =
                    authored.Ordinal;
                attack.FindPropertyRelative("targetSource").enumValueIndex =
                    (int)FpgSkillTargetSource.CurrentTarget;
                attack.FindPropertyRelative("mode").enumValueIndex =
                    (int)FpgSkillAttackMode.BoundTarget;
                attack.FindPropertyRelative("ammoCost").intValue = 0;
                attack.FindPropertyRelative("baseDamage").intValue = 5;
                attack.FindPropertyRelative("breakDamage").intValue = 1;
                attack.FindPropertyRelative(
                    "weakpointDamageMultiplierBasisPoints").intValue = 10000;
                attack.FindPropertyRelative(
                    "weakpointBreakMultiplierBasisPoints").intValue = 10000;
                attack.FindPropertyRelative("threatDefinitionId").intValue = 101;
                attack.FindPropertyRelative("boundTargetPolicy").enumValueIndex =
                    (int)ThreatTargetPolicy.PlayerCombatant;
                attack.FindPropertyRelative("delayTicks").intValue = 20;
                attack.FindPropertyRelative("threatPresentationKind")
                    .enumValueIndex =
                    (int)FpgThreatPresentationKind.HeavyWeakpoint;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(skill.TryValidate(out string error), Is.True, error);
            return skill;
        }

        private static FpgEnemyAttackDefinition CreateTypedTimedImpactSkill(
            string skillId,
            int durationTicks,
            int cooldownTicks,
            int eventTick,
            int delayTicks)
        {
            FpgEnemyAttackDefinition skill =
                ScriptableObject.CreateInstance<FpgEnemyAttackDefinition>();
            SerializedObject serialized = new SerializedObject(skill);
            ConfigureSkillIdentity(serialized, skillId, cooldownTicks);
            serialized.FindProperty("authoringSchemaVersion").intValue =
                FpgSkillTimelineDefinition.CurrentAuthoringSchemaVersion;
            ConfigureExecute(
                serialized,
                durationTicks,
                "enemy_targeted");
            SerializedProperty execute = serialized.FindProperty("sequences")
                .GetArrayElementAtIndex(0);
            SerializedProperty attacks =
                execute.FindPropertyRelative("attackEvents");
            attacks.arraySize = 1;
            SerializedProperty attack = attacks.GetArrayElementAtIndex(0);
            attack.FindPropertyRelative("eventId").stringValue =
                "event.targeted";
            attack.FindPropertyRelative("tick").intValue = eventTick;
            attack.FindPropertyRelative("authoredOrdinal").intValue = 0;
            attack.FindPropertyRelative("socketId").stringValue =
                string.Empty;
            attack.FindPropertyRelative("targetSource").enumValueIndex =
                (int)FpgSkillTargetSource.CurrentTarget;
            attack.FindPropertyRelative("targetOffset").vector3Value =
                Vector3.zero;
            attack.FindPropertyRelative("mode").enumValueIndex =
                (int)FpgSkillAttackMode.BoundTarget;
            attack.FindPropertyRelative("ammoCost").intValue = 0;
            attack.FindPropertyRelative("baseDamage").intValue = 5;
            attack.FindPropertyRelative("breakDamage").intValue = 1;
            attack.FindPropertyRelative(
                "weakpointDamageMultiplierBasisPoints").intValue = 10000;
            attack.FindPropertyRelative(
                "weakpointBreakMultiplierBasisPoints").intValue = 10000;
            attack.FindPropertyRelative("threatDefinitionId").intValue = 101;
            attack.FindPropertyRelative("boundTargetPolicy").enumValueIndex =
                (int)ThreatTargetPolicy.PlayerCombatant;
            attack.FindPropertyRelative("delayTicks").intValue = delayTicks;
            attack.FindPropertyRelative("threatPresentationKind")
                .enumValueIndex =
                (int)FpgThreatPresentationKind.HeavyWeakpoint;
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
            FpgEnemyAttackDefinition skill =
                ScriptableObject.CreateInstance<FpgEnemyAttackDefinition>();
            SerializedObject serialized = new SerializedObject(skill);
            ConfigureSkillIdentity(serialized, skillId, cooldownTicks);
            serialized.FindProperty("authoringSchemaVersion").intValue =
                FpgSkillTimelineDefinition.CurrentAuthoringSchemaVersion;
            ConfigureExecute(
                serialized,
                durationTicks,
                "enemy_projectile");
            SerializedProperty sequence = serialized.FindProperty("sequences")
                .GetArrayElementAtIndex(0);
            sequence.FindPropertyRelative("attackEvents").arraySize = 0;
            sequence.FindPropertyRelative("reloadEvents").arraySize = 0;
            sequence.FindPropertyRelative("summonEvents").arraySize = 0;
            SerializedProperty projectiles =
                sequence.FindPropertyRelative("projectileEvents");
            projectiles.arraySize = eventTicks.Length;
            for (int index = 0; index < eventTicks.Length; index++)
            {
                SerializedProperty projectile =
                    projectiles.GetArrayElementAtIndex(index);
                projectile.FindPropertyRelative("eventId").stringValue =
                    "event.attack." + index;
                projectile.FindPropertyRelative("tick").intValue =
                    eventTicks[index];
                projectile.FindPropertyRelative("authoredOrdinal").intValue =
                    index;
                projectile.FindPropertyRelative("socketId").stringValue =
                    "enemy.muzzle";
                projectile.FindPropertyRelative("targetSource").enumValueIndex =
                    (int)FpgSkillTargetSource.CurrentTarget;
                projectile.FindPropertyRelative("targetOffset").vector3Value =
                    Vector3.zero;
                projectile.FindPropertyRelative("impactMode").enumValueIndex =
                    (int)FpgSkillProjectileImpactMode.BoundTarget;
                projectile.FindPropertyRelative("ammoCost").intValue = 0;
                projectile.FindPropertyRelative("baseDamage").intValue = 5;
                projectile.FindPropertyRelative("breakDamage").intValue = 1;
                projectile.FindPropertyRelative(
                    "weakpointDamageMultiplierBasisPoints").intValue = 10000;
                projectile.FindPropertyRelative(
                    "weakpointBreakMultiplierBasisPoints").intValue = 10000;
                projectile.FindPropertyRelative("threatDefinitionId").intValue =
                    201;
                projectile.FindPropertyRelative("projectileDefinitionId")
                    .intValue = 301;
                projectile.FindPropertyRelative("projectileCount").intValue = 1;
                projectile.FindPropertyRelative("projectileFlightTicks")
                    .intValue = 20;
                projectile.FindPropertyRelative("projectileLifetimeTicks")
                    .intValue = 30;
                projectile.FindPropertyRelative("projectileMaxHitPoints")
                    .intValue = 0;
                projectile.FindPropertyRelative("projectileInterceptable")
                    .boolValue = false;
                projectile.FindPropertyRelative("projectileBudgetUnits")
                    .intValue = 1;
                projectile.FindPropertyRelative("projectileSweepRadiusKey")
                    .intValue = 32;
                projectile.FindPropertyRelative("threatPresentationKind")
                    .enumValueIndex =
                    (int)FpgThreatPresentationKind.FastUninterceptable;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(skill.TryValidate(out string error), Is.True, error);
            return skill;
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
            string animation)
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
            execute.FindPropertyRelative("warnings").arraySize = 0;
            execute.FindPropertyRelative("activePresentationTracks")
                .arraySize = 0;
            execute.FindPropertyRelative("attackEvents").arraySize = 0;
            execute.FindPropertyRelative("projectileEvents").arraySize = 0;
            execute.FindPropertyRelative("reloadEvents").arraySize = 0;
            execute.FindPropertyRelative("summonEvents").arraySize = 0;
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
            public int ClearCount { get; private set; }
            public List<bool> WarningStates { get; } =
                new List<bool>();

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
                FpgSkillExecutionIdAllocator executionIds = null,
                IFpgFormalEnemyMotionAuthority motionAuthority = null,
                IUnityPhysicsQueryBackend physics = null)
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
                    executionIds: executionIds,
                    motionAuthority: motionAuthority,
                    physics: physics);
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

        private sealed class RecordingMotionAuthority :
            IFpgFormalEnemyMotionAuthority
        {
            public List<FpgFormalEnemySkillSequenceFrame> StartFrames
            {
                get;
            } = new List<FpgFormalEnemySkillSequenceFrame>();

            public List<FpgFormalEnemySkillSequenceFrame> TerminalFrames
            {
                get;
            } = new List<FpgFormalEnemySkillSequenceFrame>();

            public DomainResult AdvanceMotion(TickIndex tick)
            {
                return tick.IsValid
                    ? DomainResult.Success
                    : DomainResult.Rejected(RejectReason.WrongTick);
            }

            public DomainResult StartSkillMotion(
                in FpgFormalEnemySkillSequenceFrame frame)
            {
                StartFrames.Add(frame);
                return DomainResult.Success;
            }

            public DomainResult ApplySkillMotionFrame(
                in FpgFormalEnemySkillSequenceFrame frame)
            {
                TerminalFrames.Add(frame);
                return DomainResult.Success;
            }
        }

        private sealed class RecordingPhysicsBackend :
            IUnityPhysicsQueryBackend
        {
            public int Capacity => 1;
            public int SyncCount { get; private set; }

            public void SyncTransforms()
            {
                SyncCount++;
            }

            public NonAllocPhysicsQueryResult RaycastNonAlloc(
                Vector3 origin,
                Vector3 direction,
                UnityPhysicsHit[] output,
                float maxDistance,
                int layerMask,
                QueryTriggerInteraction triggerInteraction)
            {
                return new NonAllocPhysicsQueryResult(0, false);
            }

            public NonAllocPhysicsQueryResult SphereCastNonAlloc(
                Vector3 origin,
                float radius,
                Vector3 direction,
                UnityPhysicsHit[] output,
                float maxDistance,
                int layerMask,
                QueryTriggerInteraction triggerInteraction)
            {
                return new NonAllocPhysicsQueryResult(0, false);
            }

            public NonAllocPhysicsQueryResult OverlapSphereNonAlloc(
                Vector3 position,
                float radius,
                Collider[] output,
                int layerMask,
                QueryTriggerInteraction triggerInteraction)
            {
                return new NonAllocPhysicsQueryResult(0, false);
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
