using System.Collections.Generic;
using FPG.Demo.Core;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgEnemyAiRuntimeTests
    {
        private readonly List<ScriptableObject> createdAssets =
            new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            createdAssets.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = createdAssets.Count - 1; index >= 0; index--)
            {
                if (createdAssets[index] != null)
                {
                    UnityObject.DestroyImmediate(createdAssets[index]);
                }
            }

            createdAssets.Clear();
        }

        [Test]
        public void SkillWaitsForReadyTickRangeAndLineOfSight()
        {
            Harness harness = CreateHarness(
                FpgEnemyBehaviorMode.FixedPosition,
                0,
                new AttackSetup("ready-skill", 2, 0, 0));

            Step(harness, 0, Observe());
            Step(harness, 1, Observe());
            Assert.That(harness.Skill.StartCalls, Is.Zero);
            Assert.That(
                harness.Runtime.LastTransitionReason,
                Is.EqualTo(FpgEnemyAiTransitionReason.WaitingForCooldown));

            Step(harness, 2, Observe(hasLineOfSight: false));
            Assert.That(harness.Skill.StartCalls, Is.Zero);
            Assert.That(
                harness.Runtime.LastTransitionReason,
                Is.EqualTo(FpgEnemyAiTransitionReason.NoLineOfSight));

            Step(harness, 3, Observe(positionSatisfiesAttack: false));
            Assert.That(harness.Skill.StartCalls, Is.Zero);
            Assert.That(
                harness.Runtime.LastTransitionReason,
                Is.EqualTo(FpgEnemyAiTransitionReason.OutsideAttackRange));

            Step(harness, 4, Observe());
            Assert.That(harness.Skill.StartCalls, Is.EqualTo(1));
            Assert.That(
                harness.Runtime.BrainState,
                Is.EqualTo(FpgEnemyAiBrainState.SkillExecuting));
        }

        [Test]
        public void RepositionWaitsForValidPositionThenRetries()
        {
            Harness harness = CreateHarness(
                FpgEnemyBehaviorMode.LateralChase,
                0,
                new AttackSetup("retry-skill", 100, 0, 0));
            harness.Movement.BeginResults.Enqueue(false);
            harness.Movement.BeginResults.Enqueue(true);
            harness.Movement.FailedStartState =
                FpgEnemyAiMovementState.NoValidPosition;
            FpgEnemyAiObservation outside = Observe(
                targetDistance: 20f,
                positionSatisfiesAttack: false,
                shouldReposition: true);

            Step(harness, 0, outside);
            Step(harness, 1, outside);
            Assert.That(
                harness.Runtime.BrainState,
                Is.EqualTo(
                    FpgEnemyAiBrainState.LocomotionWaitingForValidPosition));
            Assert.That(harness.Movement.BeginCalls, Is.EqualTo(1));

            Step(harness, 2, outside);
            Assert.That(harness.Movement.BeginCalls, Is.EqualTo(2));
            Assert.That(
                harness.Runtime.BrainState,
                Is.EqualTo(FpgEnemyAiBrainState.LocomotionReposition));
            Assert.That(
                harness.Runtime.MovementState,
                Is.EqualTo(FpgEnemyAiMovementState.Moving));
        }

        [Test]
        public void MovementStartFaultEntersFaultInsteadOfPositionRetry()
        {
            Harness harness = CreateHarness(
                FpgEnemyBehaviorMode.LateralChase,
                0,
                new AttackSetup("fault-skill", 100, 0, 0));
            harness.Movement.BeginResults.Enqueue(false);
            harness.Movement.FailedStartState =
                FpgEnemyAiMovementState.Faulted;
            FpgEnemyAiObservation outside = Observe(
                targetDistance: 20f,
                positionSatisfiesAttack: false,
                shouldReposition: true);

            Step(harness, 0, outside);
            Step(harness, 1, outside);

            Assert.That(
                harness.Runtime.BrainState,
                Is.EqualTo(FpgEnemyAiBrainState.Fault));
            Assert.That(
                harness.Runtime.LastTransitionReason,
                Is.EqualTo(FpgEnemyAiTransitionReason.MovementFaulted));
            Assert.That(harness.Movement.BeginCalls, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedHitReactBlocksMovementAndRestartsItsEndTick()
        {
            Harness harness = CreateHarness(
                FpgEnemyBehaviorMode.LateralChase,
                0,
                new AttackSetup("hit-skill", 100, 0, 0));
            FpgEnemyAiObservation outside = Observe(
                targetDistance: 20f,
                positionSatisfiesAttack: false,
                shouldReposition: true);

            Step(harness, 0, outside);
            Step(harness, 1, outside);
            Assert.That(harness.Movement.BeginCalls, Is.EqualTo(1));

            Assert.That(harness.Runtime.NotifyHit(new TickIndex(1), 2).IsSuccess,
                Is.True);
            Step(harness, 2, outside);
            Assert.That(harness.Movement.BeginCalls, Is.EqualTo(1));

            Assert.That(harness.Runtime.NotifyHit(new TickIndex(2), 3).IsSuccess,
                Is.True);
            Step(harness, 3, outside);
            Step(harness, 4, outside);
            Assert.That(
                harness.Runtime.ControlState,
                Is.EqualTo(FpgEnemyAiControlState.HitReact));
            Assert.That(harness.Movement.BeginCalls, Is.EqualTo(1));

            Step(harness, 5, outside);
            Assert.That(
                harness.Runtime.ControlState,
                Is.EqualTo(FpgEnemyAiControlState.Active));
            Assert.That(harness.Movement.BeginCalls, Is.EqualTo(2));
        }

        [Test]
        public void HitReactDoesNotInterruptRunningSkillAndAllowsCompletion()
        {
            Harness harness = CreateHarness(
                FpgEnemyBehaviorMode.FixedPosition,
                0,
                new AttackSetup("hit-skill", 0, 0, 0));
            Step(harness, 0, Observe());
            Assert.That(harness.Skill.StartCalls, Is.EqualTo(1));

            Assert.That(harness.Runtime.NotifyHit(new TickIndex(0), 2).IsSuccess,
                Is.True);
            Step(harness, 1, Observe());
            Assert.That(harness.Skill.AdvanceCalls, Is.EqualTo(1));
            Assert.That(harness.Skill.State, Is.EqualTo(
                FpgEnemyAiSkillExecutorState.Running));

            Assert.That(harness.Runtime.NotifyHit(new TickIndex(1), 3).IsSuccess,
                Is.True);
            Step(harness, 2, Observe());
            Assert.That(harness.Skill.AdvanceCalls, Is.EqualTo(2));
            Assert.That(harness.Skill.StartCalls, Is.EqualTo(1));

            Step(harness, 3, Observe());
            harness.Skill.CompleteOnNextAdvance = true;
            Step(harness, 4, Observe());
            Assert.That(
                harness.Runtime.ControlState,
                Is.EqualTo(FpgEnemyAiControlState.Active));
            Assert.That(
                harness.Runtime.BrainState,
                Is.EqualTo(FpgEnemyAiBrainState.Recovery));
            Assert.That(harness.Skill.StartCalls, Is.EqualTo(1));
        }

        [TestCase(FpgEnemyAiCurrentSkillPolicy.Continue, 1, false)]
        [TestCase(FpgEnemyAiCurrentSkillPolicy.Freeze, 0, true)]
        [TestCase(FpgEnemyAiCurrentSkillPolicy.Interrupt, 0, false)]
        public void ControlLockAppliesCurrentSkillPolicy(
            FpgEnemyAiCurrentSkillPolicy policy,
            int expectedAdvanceCalls,
            bool expectedFrozen)
        {
            Harness harness = CreateHarness(
                FpgEnemyBehaviorMode.FixedPosition,
                0,
                new AttackSetup("control-skill", 0, 0, 0));
            Step(harness, 0, Observe());
            FpgEnemyAiControlLock controlLock = new FpgEnemyAiControlLock(
                true,
                false,
                policy,
                2);

            Assert.That(
                harness.Runtime.ApplyControlLock(
                    new TickIndex(0),
                    in controlLock).IsSuccess,
                Is.True);
            Step(harness, 1, Observe());

            Assert.That(
                harness.Skill.AdvanceCalls,
                Is.EqualTo(expectedAdvanceCalls));
            Assert.That(harness.Skill.IsFrozen, Is.EqualTo(expectedFrozen));
            Assert.That(
                harness.Skill.FreezeUntilTick,
                Is.EqualTo(expectedFrozen ? new TickIndex(2L) : TickIndex.Invalid));
            Assert.That(harness.Skill.StartCalls, Is.EqualTo(1));
            Assert.That(
                harness.Runtime.ControlState,
                Is.EqualTo(FpgEnemyAiControlState.ControlLocked));
        }

        [Test]
        public void FreezeControlLockResumesSkillOnItsEndTick()
        {
            Harness harness = CreateHarness(
                FpgEnemyBehaviorMode.FixedPosition,
                0,
                new AttackSetup("freeze-resume", 0, 0, 0));
            Step(harness, 0, Observe());

            FpgEnemyAiControlLock controlLock = new FpgEnemyAiControlLock(
                true,
                false,
                FpgEnemyAiCurrentSkillPolicy.Freeze,
                2);
            Assert.That(
                harness.Runtime.ApplyControlLock(
                    new TickIndex(0L),
                    in controlLock).IsSuccess,
                Is.True);

            Step(harness, 1, Observe());
            Assert.That(harness.Skill.IsFrozen, Is.True);
            Assert.That(harness.Skill.AdvanceCalls, Is.Zero);

            Step(harness, 2, Observe());
            Assert.That(
                harness.Runtime.ControlState,
                Is.EqualTo(FpgEnemyAiControlState.Active));
            Assert.That(harness.Skill.IsFrozen, Is.False);
            Assert.That(harness.Skill.AdvanceCalls, Is.EqualTo(1));
        }

        [Test]
        public void RecoveryWaitsUntilBothMovePhaseAndSkillCooldownAreReady()
        {
            Harness harness = CreateHarness(
                FpgEnemyBehaviorMode.FixedPosition,
                2,
                new AttackSetup("cooldown-skill", 0, 4, 0));
            Step(harness, 0, Observe());
            harness.Skill.Finish();

            Step(harness, 1, Observe());
            Assert.That(
                harness.Runtime.BrainState,
                Is.EqualTo(FpgEnemyAiBrainState.Recovery));
            Step(harness, 2, Observe());
            Step(harness, 3, Observe());
            Step(harness, 4, Observe());
            Assert.That(harness.Skill.StartCalls, Is.EqualTo(1));
            Assert.That(
                harness.Runtime.LastTransitionReason,
                Is.EqualTo(FpgEnemyAiTransitionReason.WaitingForCooldown));

            Step(harness, 5, Observe());
            Assert.That(
                harness.Runtime.BrainState,
                Is.EqualTo(FpgEnemyAiBrainState.LocomotionIdle));
            Step(harness, 6, Observe());
            Assert.That(harness.Skill.StartCalls, Is.EqualTo(2));
        }

        [Test]
        public void SkillSelectionUsesHighestPriorityAndStableOrdinalForTies()
        {
            Harness harness = CreateHarness(
                FpgEnemyBehaviorMode.FixedPosition,
                0,
                new AttackSetup("lower", 0, 0, 10),
                new AttackSetup("highest-first", 0, 0, 20),
                new AttackSetup("highest-second", 0, 0, 20));

            Step(harness, 0, Observe());

            Assert.That(harness.Skill.StartCalls, Is.EqualTo(1));
            Assert.That(harness.Skill.LastRequest.PatternOrdinal, Is.EqualTo(1));
            Assert.That(harness.Skill.LastRequest.SkillId, Is.EqualTo("highest-first"));
        }

        [Test]
        public void DeadStateStopsFurtherSkillAdvancementAndResetRestoresReadiness()
        {
            Harness harness = CreateHarness(
                FpgEnemyBehaviorMode.FixedPosition,
                0,
                new AttackSetup("pooled-skill", 5, 0, 0));
            Step(harness, 0, Observe());
            Assert.That(harness.Skill.StartCalls, Is.Zero);

            Assert.That(harness.Runtime.MarkDead(new TickIndex(0)).IsSuccess,
                Is.True);
            int advanceCalls = harness.Skill.AdvanceCalls;
            Step(harness, 1, Observe());
            Assert.That(harness.Runtime.ControlState,
                Is.EqualTo(FpgEnemyAiControlState.Dead));
            Assert.That(harness.Runtime.BrainState,
                Is.EqualTo(FpgEnemyAiBrainState.LocomotionIdle));
            Assert.That(harness.Skill.AdvanceCalls, Is.EqualTo(advanceCalls));
            Assert.That(harness.Skill.StartCalls, Is.Zero);

            Assert.That(harness.Runtime.Reset(new TickIndex(10)).IsSuccess,
                Is.True);
            Assert.That(harness.Runtime.ControlState,
                Is.EqualTo(FpgEnemyAiControlState.Active));
            Assert.That(harness.Runtime.BrainState,
                Is.EqualTo(FpgEnemyAiBrainState.Entry));
            Step(harness, 10, Observe());
            Step(harness, 11, Observe());
            Step(harness, 12, Observe());
            Step(harness, 13, Observe());
            Step(harness, 14, Observe());
            Assert.That(harness.Skill.StartCalls, Is.Zero);
            Step(harness, 15, Observe());
            Assert.That(harness.Skill.StartCalls, Is.EqualTo(1));
        }

        private Harness CreateHarness(
            FpgEnemyBehaviorMode mode,
            int moveDurationTicks,
            params AttackSetup[] attackSetups)
        {
            FpgEnemyBehaviorDefinition behavior =
                ScriptableObject.CreateInstance<FpgEnemyBehaviorDefinition>();
            createdAssets.Add(behavior);
            SerializedObject behaviorObject = new SerializedObject(behavior);
            behaviorObject.FindProperty("mode").enumValueIndex = (int)mode;
            behaviorObject.FindProperty("moveDurationTicks").intValue =
                moveDurationTicks;
            behaviorObject.ApplyModifiedPropertiesWithoutUndo();

            FpgEnemyAttackDefinition[] attacks =
                new FpgEnemyAttackDefinition[attackSetups.Length];
            for (int index = 0; index < attackSetups.Length; index++)
            {
                FpgEnemyAttackDefinition attack =
                    ScriptableObject.CreateInstance<FpgEnemyAttackDefinition>();
                createdAssets.Add(attack);
                attacks[index] = attack;

                SerializedObject attackObject = new SerializedObject(attack);
                attackObject.FindProperty("skillId").stringValue =
                    attackSetups[index].SkillId;
                attackObject.FindProperty("priority").intValue =
                    attackSetups[index].Priority;
                attackObject.FindProperty("firstReadyOffsetTicks").intValue =
                    attackSetups[index].FirstReadyOffsetTicks;
                attackObject.FindProperty("sequenceCooldownTicks").intValue =
                    attackSetups[index].SequenceCooldownTicks;
                attackObject.ApplyModifiedPropertiesWithoutUndo();
            }

            FpgEnemyDefinition definition =
                ScriptableObject.CreateInstance<FpgEnemyDefinition>();
            createdAssets.Add(definition);
            SerializedObject definitionObject = new SerializedObject(definition);
            definitionObject.FindProperty("behavior").objectReferenceValue =
                behavior;
            SerializedProperty attackArray =
                definitionObject.FindProperty("attackPatterns");
            attackArray.arraySize = attacks.Length;
            for (int index = 0; index < attacks.Length; index++)
            {
                attackArray.GetArrayElementAtIndex(index).objectReferenceValue =
                    attacks[index];
            }
            definitionObject.ApplyModifiedPropertiesWithoutUndo();

            RecordingMovementExecutor movement =
                new RecordingMovementExecutor();
            RecordingSkillExecutor skill = new RecordingSkillExecutor();
            FpgEnemyAiRuntime runtime = new FpgEnemyAiRuntime(
                new RuntimeId(1L),
                0,
                new TickIndex(0L),
                definition,
                movement,
                skill);
            return new Harness(runtime, movement, skill);
        }

        private static void Step(
            Harness harness,
            long tick,
            FpgEnemyAiObservation observation)
        {
            Assert.That(
                harness.Runtime.Advance(
                    new TickIndex(tick),
                    in observation).IsSuccess,
                Is.True,
                $"AI tick {tick} should succeed.");
        }

        private static FpgEnemyAiObservation Observe(
            float targetDistance = 5f,
            bool hasLineOfSight = true,
            bool positionSatisfiesAttack = true,
            bool shouldReposition = false)
        {
            return new FpgEnemyAiObservation(
                true,
                true,
                targetDistance,
                hasLineOfSight,
                positionSatisfiesAttack,
                shouldReposition);
        }

        private readonly struct AttackSetup
        {
            public AttackSetup(
                string skillId,
                int firstReadyOffsetTicks,
                int sequenceCooldownTicks,
                int priority)
            {
                SkillId = skillId;
                FirstReadyOffsetTicks = firstReadyOffsetTicks;
                SequenceCooldownTicks = sequenceCooldownTicks;
                Priority = priority;
            }

            public string SkillId { get; }
            public int FirstReadyOffsetTicks { get; }
            public int SequenceCooldownTicks { get; }
            public int Priority { get; }
        }

        private sealed class Harness
        {
            public Harness(
                FpgEnemyAiRuntime runtime,
                RecordingMovementExecutor movement,
                RecordingSkillExecutor skill)
            {
                Runtime = runtime;
                Movement = movement;
                Skill = skill;
            }

            public FpgEnemyAiRuntime Runtime { get; }
            public RecordingMovementExecutor Movement { get; }
            public RecordingSkillExecutor Skill { get; }
        }

        private sealed class RecordingMovementExecutor :
            IFpgEnemyAiMovementExecutor
        {
            public FpgEnemyAiMovementState State { get; private set; } =
                FpgEnemyAiMovementState.Idle;
            public Queue<bool> BeginResults { get; } = new Queue<bool>();
            public FpgEnemyAiMovementState FailedStartState { get; set; } =
                FpgEnemyAiMovementState.NoValidPosition;
            public int BeginCalls { get; private set; }
            public int AdvanceCalls { get; private set; }
            public int CancelCalls { get; private set; }

            public bool TryGetMovementGoal(
                out FpgEnemyAiMovementGoal goal)
            {
                goal = FpgEnemyAiMovementGoal.Invalid;
                return false;
            }

            public bool TryBeginMove(in FpgEnemyAiMoveRequest request)
            {
                BeginCalls++;
                bool succeeds = BeginResults.Count == 0
                    || BeginResults.Dequeue();
                State = succeeds
                    ? FpgEnemyAiMovementState.Moving
                    : FailedStartState;
                return succeeds;
            }

            public void AdvanceMovement(TickIndex tick)
            {
                AdvanceCalls++;
                if (State == FpgEnemyAiMovementState.NoValidPosition)
                {
                    State = FpgEnemyAiMovementState.Idle;
                }
            }

            public void CancelMovement()
            {
                CancelCalls++;
                State = FpgEnemyAiMovementState.Idle;
            }
        }

        private sealed class RecordingSkillExecutor :
            IFpgEnemyAiSkillExecutor
        {
            private long nextExecutionId = 1L;

            public FpgEnemyAiSkillExecutorState State { get; private set; } =
                FpgEnemyAiSkillExecutorState.Idle;
            public SkillExecutionId ExecutionId { get; private set; } =
                SkillExecutionId.Invalid;
            public int StartCalls { get; private set; }
            public int AdvanceCalls { get; private set; }
            public bool IsFrozen { get; private set; }
            public TickIndex FreezeUntilTick { get; private set; } =
                TickIndex.Invalid;
            public bool CompleteOnNextAdvance { get; set; }
            public FpgEnemyAiSkillRequest LastRequest { get; private set; }

            public bool TryStartSkill(in FpgEnemyAiSkillRequest request)
            {
                StartCalls++;
                LastRequest = request;
                ExecutionId = new SkillExecutionId(nextExecutionId++);
                State = FpgEnemyAiSkillExecutorState.Running;
                return true;
            }

            public void AdvanceSkill(TickIndex tick)
            {
                if (IsFrozen
                    && FreezeUntilTick.IsValid
                    && tick >= FreezeUntilTick)
                {
                    IsFrozen = false;
                    FreezeUntilTick = TickIndex.Invalid;
                }

                AdvanceCalls++;
                if (CompleteOnNextAdvance)
                {
                    CompleteOnNextAdvance = false;
                    State = FpgEnemyAiSkillExecutorState.Completed;
                }
            }

            public void InterruptSkill(
                FpgEnemyAiCurrentSkillPolicy policy,
                TickIndex tick,
                TickIndex freezeUntilTick)
            {
                IsFrozen = policy == FpgEnemyAiCurrentSkillPolicy.Freeze;
                FreezeUntilTick = IsFrozen
                    ? freezeUntilTick
                    : TickIndex.Invalid;
                if (policy == FpgEnemyAiCurrentSkillPolicy.Interrupt
                    && State == FpgEnemyAiSkillExecutorState.Running)
                {
                    State = FpgEnemyAiSkillExecutorState.Interrupted;
                }
            }

            public void Finish()
            {
                State = FpgEnemyAiSkillExecutorState.Completed;
            }
        }
    }
}
