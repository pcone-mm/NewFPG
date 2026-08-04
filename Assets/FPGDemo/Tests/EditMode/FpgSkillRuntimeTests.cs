using System;
using FPG.Demo.Core;
using FPG.Demo.Skills;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgSkillRuntimeTests
    {
        [Test]
        public void CompilerRejectsDuplicateEventIds()
        {
            FpgCompiledSkillEvent[] events =
            {
                PayloadEvent(7, 0),
                PayloadEvent(7, 1)
            };

            bool compiled = FpgSkillCompiler.TryCompileSequence(
                FpgSkillSequenceKind.Execute,
                2,
                1001,
                false,
                events,
                out FpgCompiledSkillSequence ignored,
                out FpgSkillValidationResult validation);

            Assert.That(compiled, Is.False);
            Assert.That(validation.Error, Is.EqualTo(FpgSkillValidationError.DuplicateEventId));
            Assert.That(validation.EventIndex, Is.EqualTo(1));
            Assert.That(validation.Value, Is.EqualTo(7));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void CompilerRejectsEventTicksOutsideInclusiveRange(int tick)
        {
            bool compiled = FpgSkillCompiler.TryCompileSequence(
                FpgSkillSequenceKind.Execute,
                2,
                1001,
                false,
                new[] { PayloadEvent(1, tick) },
                out FpgCompiledSkillSequence ignored,
                out FpgSkillValidationResult validation);

            Assert.That(compiled, Is.False);
            Assert.That(validation.Error, Is.EqualTo(FpgSkillValidationError.EventTickOutOfRange));
            Assert.That(validation.Value, Is.EqualTo(tick));
        }

        [Test]
        public void RuntimeEmitsTickZeroAndEndpointEvents()
        {
            FpgCompiledSkillSequence sequence = Sequence(
                2,
                PayloadEvent(10, 0),
                PayloadEvent(20, 2));
            FpgSkillExecutionRuntime runtime = new FpgSkillExecutionRuntime(sequence.EventCount);

            Assert.That(runtime.Start(sequence, new SkillExecutionId(41L), new TickIndex(100L)).IsSuccess, Is.True);

            FpgSkillRuntimeResult tickZero = runtime.Tick(new TickIndex(100L));
            Assert.That(tickZero.IsSuccess, Is.True);
            Assert.That(tickZero.EventResultCount, Is.EqualTo(1));
            Assert.That(runtime.GetResult(0).EventId, Is.EqualTo(10));
            Assert.That(runtime.GetResult(0).Outcome, Is.EqualTo(FpgSkillEventOutcome.Triggered));

            Assert.That(runtime.Tick(new TickIndex(101L)).EventResultCount, Is.Zero);

            FpgSkillRuntimeResult endpoint = runtime.Tick(new TickIndex(102L));
            Assert.That(endpoint.EventResultCount, Is.EqualTo(1));
            Assert.That(endpoint.State, Is.EqualTo(FpgSkillExecutionState.Completed));
            Assert.That(runtime.GetResult(0).EventId, Is.EqualTo(20));
            Assert.That(runtime.GetResult(0).ScheduledTick, Is.EqualTo(new TickIndex(102L)));
            Assert.That(runtime.State, Is.EqualTo(FpgSkillExecutionState.Completed));
        }

        [Test]
        public void HoldSequenceEmitsEventsOnceAndRunsUntilCanceled()
        {
            FpgCompiledSkillSequence sequence = HoldSequence(
                2,
                WarningEvent(10, 0),
                WarningEvent(20, 2));
            FpgSkillExecutionRuntime runtime =
                new FpgSkillExecutionRuntime(sequence.EventCount);

            Assert.That(
                runtime.Start(
                    sequence,
                    new SkillExecutionId(51L),
                    new TickIndex(100L)).IsSuccess,
                Is.True);

            Assert.That(runtime.Tick(new TickIndex(100L)).EventResultCount,
                Is.EqualTo(1));
            Assert.That(runtime.GetResult(0).EventId, Is.EqualTo(10));
            Assert.That(runtime.Tick(new TickIndex(101L)).EventResultCount,
                Is.Zero);

            FpgSkillRuntimeResult endpoint =
                runtime.Tick(new TickIndex(102L));
            Assert.That(endpoint.EventResultCount, Is.EqualTo(1));
            Assert.That(endpoint.State, Is.EqualTo(FpgSkillExecutionState.Running));
            Assert.That(runtime.GetResult(0).EventId, Is.EqualTo(20));
            Assert.That(runtime.RemainingEventCount, Is.Zero);

            FpgSkillRuntimeResult held = runtime.Tick(new TickIndex(103L));
            Assert.That(held.EventResultCount, Is.Zero);
            Assert.That(held.State, Is.EqualTo(FpgSkillExecutionState.Running));

            FpgSkillRuntimeResult canceled =
                runtime.CancelRemaining(new TickIndex(104L));
            Assert.That(canceled.EventResultCount, Is.Zero);
            Assert.That(canceled.State, Is.EqualTo(FpgSkillExecutionState.Canceled));
        }

        [Test]
        public void AnimationLoopDoesNotHoldExecutionOpen()
        {
            FpgCompiledSkillSequence sequence = new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                0,
                1001,
                true,
                Array.Empty<FpgCompiledSkillEvent>());
            FpgSkillExecutionRuntime runtime = new FpgSkillExecutionRuntime(0);

            runtime.Start(
                sequence,
                new SkillExecutionId(52L),
                new TickIndex(10L));
            FpgSkillRuntimeResult result = runtime.Tick(new TickIndex(10L));

            Assert.That(result.State, Is.EqualTo(FpgSkillExecutionState.Completed));
        }

        [Test]
        public void CompilerRejectsGameplayActionsInHoldSequence()
        {
            bool compiled = FpgSkillCompiler.TryCompileSequence(
                FpgSkillSequenceKind.Execute,
                1,
                1001,
                false,
                new[] { PayloadEvent(1, 0) },
                true,
                out FpgCompiledSkillSequence ignored,
                out FpgSkillValidationResult validation);

            Assert.That(compiled, Is.False);
            Assert.That(
                validation.Error,
                Is.EqualTo(
                    FpgSkillValidationError.HoldSequenceHasGameplayActions));
        }

        [Test]
        public void GameplayHashIncludesHoldUntilCanceled()
        {
            FpgCompiledSkillEvent[] events = { WarningEvent(1, 0) };
            FpgCompiledSkillSequence completed = new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                0,
                1001,
                false,
                events);
            FpgCompiledSkillSequence held = new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                0,
                1001,
                false,
                events,
                holdUntilCanceled: true);

            Assert.That(held.HoldUntilCanceled, Is.True);
            Assert.That(held.GameplayHash, Is.Not.EqualTo(completed.GameplayHash));
        }

        [Test]
        public void SameTickEventsUseAuthoredSortOrder()
        {
            FpgCompiledSkillSequence sequence = Sequence(
                0,
                PayloadEvent(30, 0, 2),
                PayloadEvent(20, 0, 1),
                PayloadEvent(10, 0, 0));
            FpgSkillExecutionRuntime runtime = new FpgSkillExecutionRuntime(3);

            runtime.Start(sequence, new SkillExecutionId(1L), new TickIndex(0L));
            FpgSkillRuntimeResult result = runtime.Tick(new TickIndex(0L));

            Assert.That(result.EventResultCount, Is.EqualTo(3));
            Assert.That(runtime.GetResult(0).EventId, Is.EqualTo(10));
            Assert.That(runtime.GetResult(1).EventId, Is.EqualTo(20));
            Assert.That(runtime.GetResult(2).EventId, Is.EqualTo(30));
            Assert.That(result.State, Is.EqualTo(FpgSkillExecutionState.Completed));
        }

        [Test]
        public void CancelRemainingReturnsCanceledEventsInCompiledOrder()
        {
            FpgCompiledSkillSequence sequence = Sequence(
                3,
                PayloadEvent(1, 0),
                PayloadEvent(3, 2),
                PayloadEvent(2, 1));
            FpgSkillExecutionRuntime runtime = new FpgSkillExecutionRuntime(3);

            runtime.Start(sequence, new SkillExecutionId(9L), new TickIndex(50L));
            runtime.Tick(new TickIndex(50L));
            FpgSkillRuntimeResult canceled = runtime.CancelRemaining(new TickIndex(51L));

            Assert.That(canceled.IsSuccess, Is.True);
            Assert.That(canceled.State, Is.EqualTo(FpgSkillExecutionState.Canceled));
            Assert.That(canceled.EventResultCount, Is.EqualTo(2));
            Assert.That(runtime.GetResult(0).EventId, Is.EqualTo(2));
            Assert.That(runtime.GetResult(0).ScheduledTick, Is.EqualTo(new TickIndex(51L)));
            Assert.That(runtime.GetResult(0).Tick, Is.EqualTo(new TickIndex(51L)));
            Assert.That(runtime.GetResult(0).Outcome, Is.EqualTo(FpgSkillEventOutcome.Canceled));
            Assert.That(runtime.GetResult(1).EventId, Is.EqualTo(3));
            Assert.That(runtime.GetResult(1).ScheduledTick, Is.EqualTo(new TickIndex(52L)));
            Assert.That(runtime.RemainingEventCount, Is.Zero);
        }

        [Test]
        public void GameplayHashIsCanonicalAndSensitiveToEventData()
        {
            FpgCompiledSkillSequence first = Sequence(
                2,
                PayloadEvent(2, 2),
                PayloadEvent(1, 0));
            FpgCompiledSkillSequence reordered = Sequence(
                2,
                PayloadEvent(1, 0),
                PayloadEvent(2, 2));
            FpgCompiledSkillSequence changed = Sequence(
                2,
                PayloadEvent(1, 0),
                new FpgCompiledSkillEvent(
                    2,
                    2,
                    FpgSkillActionKind.Attack,
                    99));

            FpgCompiledSkillDefinition definition = new FpgCompiledSkillDefinition(77, new[] { first });
            FpgCompiledSkillDefinition reorderedDefinition = new FpgCompiledSkillDefinition(77, new[] { reordered });
            FpgCompiledSkillDefinition changedDefinition = new FpgCompiledSkillDefinition(77, new[] { changed });

            Assert.That(FpgSkillRuntimeConstants.TickRate, Is.EqualTo(60));
            Assert.That(FpgSkillRuntimeConstants.GameplayHashVersion, Is.EqualTo(5));
            Assert.That(FpgSkillRuntimeConstants.PresentationHashVersion, Is.EqualTo(1));
            Assert.That(first.GameplayHash, Is.EqualTo(reordered.GameplayHash));
            Assert.That(definition.GameplayHash, Is.EqualTo(reorderedDefinition.GameplayHash));
            Assert.That(changed.GameplayHash, Is.Not.EqualTo(first.GameplayHash));
            Assert.That(changedDefinition.GameplayHash, Is.Not.EqualTo(definition.GameplayHash));
        }

        [TestCase(FpgSkillActionKind.Attack)]
        [TestCase(FpgSkillActionKind.LaunchProjectile)]
        [TestCase(FpgSkillActionKind.CommitReload)]
        [TestCase(FpgSkillActionKind.SummonActors)]
        [TestCase(FpgSkillActionKind.SelfDestructOwner)]
        public void GameplayActionAcceptsEveryTypedActionKind(
            FpgSkillActionKind actionKind)
        {
            FpgCompiledSkillEvent action = new FpgCompiledSkillEvent(
                1,
                0,
                actionKind,
                2);

            Assert.That(
                action.Kind,
                Is.EqualTo(FpgSkillEventKind.GameplayAction));
            Assert.That(action.ActionKind, Is.EqualTo(actionKind));
            Assert.That(action.ActionIndex, Is.EqualTo(2));
            Assert.That(action.IsValid, Is.True);
        }

        [Test]
        public void BoundSelfDestructRequiresEarlierSameTickSummon()
        {
            FpgCompiledSkillEvent summon = new FpgCompiledSkillEvent(
                10,
                2,
                FpgSkillActionKind.SummonActors,
                0,
                sortOrder: 0);
            FpgCompiledSkillEvent selfDestruct =
                new FpgCompiledSkillEvent(
                    20,
                    2,
                    FpgSkillActionKind.SelfDestructOwner,
                    0,
                    sortOrder: 1,
                    boundGameplayEventId: 10);
            FpgCompiledSkillEvent wrongKind = new FpgCompiledSkillEvent(
                10,
                2,
                FpgSkillActionKind.Attack,
                0,
                sortOrder: 0);
            FpgCompiledSkillEvent laterTick = new FpgCompiledSkillEvent(
                20,
                3,
                FpgSkillActionKind.SelfDestructOwner,
                0,
                sortOrder: 1,
                boundGameplayEventId: 10);

            Assert.DoesNotThrow(
                () => Sequence(2, summon, selfDestruct));
            Assert.Throws<ArgumentException>(
                () => Sequence(2, wrongKind, selfDestruct));
            Assert.Throws<ArgumentException>(
                () => Sequence(3, summon, laterTick));
        }

        [Test]
        public void GameplayActionRejectsInvalidActionKind()
        {
            FpgCompiledSkillEvent action = new FpgCompiledSkillEvent(
                1,
                0,
                (FpgSkillActionKind)99,
                0);

            bool compiled = FpgSkillCompiler.TryCompileSequence(
                FpgSkillSequenceKind.Execute,
                0,
                1001,
                false,
                new[] { action },
                out FpgCompiledSkillSequence ignored,
                out FpgSkillValidationResult validation);

            Assert.That(compiled, Is.False);
            Assert.That(
                validation.Error,
                Is.EqualTo(FpgSkillValidationError.InvalidActionKind));
            Assert.That(validation.Value, Is.EqualTo(99));
        }

        [Test]
        public void GameplayActionRejectsNoneActionKind()
        {
            FpgCompiledSkillEvent action = new FpgCompiledSkillEvent(
                1,
                0,
                FpgSkillActionKind.None,
                0);

            bool compiled = FpgSkillCompiler.TryCompileSequence(
                FpgSkillSequenceKind.Execute,
                0,
                1001,
                false,
                new[] { action },
                out FpgCompiledSkillSequence ignored,
                out FpgSkillValidationResult validation);

            Assert.That(compiled, Is.False);
            Assert.That(
                validation.Error,
                Is.EqualTo(FpgSkillValidationError.InvalidActionKind));
            Assert.That(validation.Value, Is.Zero);
        }

        [Test]
        public void GameplayActionRejectsNegativeActionIndex()
        {
            FpgCompiledSkillEvent action = new FpgCompiledSkillEvent(
                1,
                0,
                FpgSkillActionKind.Attack,
                -1);

            bool compiled = FpgSkillCompiler.TryCompileSequence(
                FpgSkillSequenceKind.Execute,
                0,
                1001,
                false,
                new[] { action },
                out FpgCompiledSkillSequence ignored,
                out FpgSkillValidationResult validation);

            Assert.That(compiled, Is.False);
            Assert.That(
                validation.Error,
                Is.EqualTo(FpgSkillValidationError.InvalidActionIndex));
            Assert.That(validation.Value, Is.EqualTo(-1));
        }

        [Test]
        public void GameplayHashIncludesActionKindAndIndex()
        {
            FpgCompiledSkillSequence baseline = Sequence(
                0,
                new FpgCompiledSkillEvent(
                    1,
                    0,
                    FpgSkillActionKind.Attack,
                    0));
            FpgCompiledSkillSequence changedKind = Sequence(
                0,
                new FpgCompiledSkillEvent(
                    1,
                    0,
                    FpgSkillActionKind.LaunchProjectile,
                    0));
            FpgCompiledSkillSequence changedIndex = Sequence(
                0,
                new FpgCompiledSkillEvent(
                    1,
                    0,
                    FpgSkillActionKind.Attack,
                    1));

            Assert.That(
                changedKind.GameplayHash,
                Is.Not.EqualTo(baseline.GameplayHash));
            Assert.That(
                changedIndex.GameplayHash,
                Is.Not.EqualTo(baseline.GameplayHash));
        }

        [Test]
        public void GameplayActionEqualityIncludesActionKindAndIndex()
        {
            FpgCompiledSkillEvent baseline = new FpgCompiledSkillEvent(
                1,
                0,
                FpgSkillActionKind.Attack,
                0);
            FpgCompiledSkillEvent changedKind = new FpgCompiledSkillEvent(
                1,
                0,
                FpgSkillActionKind.LaunchProjectile,
                0);
            FpgCompiledSkillEvent changedIndex = new FpgCompiledSkillEvent(
                1,
                0,
                FpgSkillActionKind.Attack,
                1);

            Assert.That(changedKind, Is.Not.EqualTo(baseline));
            Assert.That(changedIndex, Is.Not.EqualTo(baseline));
            Assert.That(
                changedKind.GetHashCode(),
                Is.Not.EqualTo(baseline.GetHashCode()));
            Assert.That(
                changedIndex.GetHashCode(),
                Is.Not.EqualTo(baseline.GetHashCode()));
        }

        [Test]
        public void ActivePresentationChangesOnlyPresentationHash()
        {
            FpgCompiledSkillEvent gameplay = new FpgCompiledSkillEvent(
                10,
                1,
                FpgSkillActionKind.Attack,
                0);
            FpgCompiledSkillSequence first = Sequence(
                3,
                gameplay,
                ActivePresentationEvent(20, 2, 30, 40, 100UL, 10));
            FpgCompiledSkillSequence changed = Sequence(
                3,
                gameplay,
                ActivePresentationEvent(20, 2, 31, 40, 101UL, 10));

            Assert.That(changed.GameplayHash, Is.EqualTo(first.GameplayHash));
            Assert.That(
                changed.PresentationHash,
                Is.Not.EqualTo(first.PresentationHash));
        }

        [Test]
        public void ActivePresentationBindingAllowsLaterTickAndRejectsEarlierTick()
        {
            FpgCompiledSkillEvent gameplay = new FpgCompiledSkillEvent(
                10,
                2,
                FpgSkillActionKind.Attack,
                0);
            FpgCompiledSkillEvent later = ActivePresentationEvent(
                20,
                3,
                30,
                40,
                100UL,
                10);
            FpgCompiledSkillEvent earlier = ActivePresentationEvent(
                20,
                1,
                30,
                40,
                100UL,
                10);

            Assert.DoesNotThrow(() => Sequence(3, gameplay, later));
            Assert.Throws<ArgumentException>(
                () => Sequence(3, gameplay, earlier));
        }

        [Test]
        public void DefinitionRejectsPresentationHandleCollision()
        {
            FpgCompiledSkillSequence sequence = Sequence(
                2,
                ActivePresentationEvent(20, 0, 30, 40, 100UL, 0),
                ActivePresentationEvent(21, 1, 30, 40, 101UL, 0));

            Assert.Throws<ArgumentException>(
                () => new FpgCompiledSkillDefinition(77, new[] { sequence }));
        }

        [Test]
        public void RuntimeRejectsASequenceThatExceedsItsPreallocatedResultCapacity()
        {
            FpgCompiledSkillSequence sequence = Sequence(
                1,
                PayloadEvent(1, 0),
                PayloadEvent(2, 1));
            FpgSkillExecutionRuntime runtime = new FpgSkillExecutionRuntime(1);

            FpgSkillRuntimeResult result = runtime.Start(
                sequence,
                new SkillExecutionId(1L),
                new TickIndex(0L));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(FpgSkillRuntimeError.ResultCapacityExceeded));
            Assert.That(runtime.State, Is.EqualTo(FpgSkillExecutionState.Idle));
        }

        [Test]
        public void LegacyStartReportsIdentityScheduleTickOverflow()
        {
            FpgCompiledSkillSequence sequence = Sequence(
                0,
                PayloadEvent(1, 0));
            FpgSkillExecutionRuntime runtime = new FpgSkillExecutionRuntime(1);

            FpgSkillRuntimeResult result = runtime.Start(
                sequence,
                new SkillExecutionId(1L),
                new TickIndex(long.MaxValue));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(
                result.Error,
                Is.EqualTo(FpgSkillRuntimeError.TickRangeOverflow));
            Assert.That(runtime.State, Is.EqualTo(FpgSkillExecutionState.Idle));
        }

        private static FpgCompiledSkillSequence Sequence(
            int durationTicks,
            params FpgCompiledSkillEvent[] events)
        {
            return new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                durationTicks,
                1001,
                false,
                events);
        }

        private static FpgCompiledSkillSequence HoldSequence(
            int durationTicks,
            params FpgCompiledSkillEvent[] events)
        {
            return new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                durationTicks,
                1001,
                false,
                events,
                holdUntilCanceled: true);
        }

        private static FpgCompiledSkillEvent WarningEvent(
            int eventId,
            int tick)
        {
            return new FpgCompiledSkillEvent(
                eventId,
                tick,
                FpgSkillEventKind.WarningStarted,
                eventId);
        }

        private static FpgCompiledSkillEvent PayloadEvent(
            int eventId,
            int tick,
            int sortOrder = 0)
        {
            return new FpgCompiledSkillEvent(
                eventId,
                tick,
                FpgSkillActionKind.Attack,
                eventId,
                sortOrder);
        }

        private static FpgCompiledSkillEvent ActivePresentationEvent(
            int eventId,
            int tick,
            int handle,
            int trackId,
            ulong contentHash,
            int boundGameplayEventId)
        {
            return new FpgCompiledSkillEvent(
                eventId,
                tick,
                FpgActivePresentationKind.Vfx,
                new FpgPresentationHandle(handle),
                trackId,
                contentHash,
                boundGameplayEventId: boundGameplayEventId);
        }


        [Test]
        public void ExecutionIdPeekDoesNotAdvanceAndCommitRejectsStaleCandidate()
        {
            FpgSkillExecutionIdAllocator allocator =
                new FpgSkillExecutionIdAllocator();

            SkillExecutionId candidate = allocator.Peek();
            Assert.That(candidate.Value, Is.EqualTo(1L));
            Assert.That(allocator.Peek(), Is.EqualTo(candidate));
            Assert.Throws<InvalidOperationException>(
                () => allocator.Commit(new SkillExecutionId(2L)));

            allocator.Commit(candidate);
            Assert.That(allocator.Peek().Value, Is.EqualTo(2L));
            Assert.Throws<InvalidOperationException>(
                () => allocator.Commit(candidate));
        }

        [Test]
        public void ExecutionIdsAreMonotonicAndResetPerSession()
        {
            FpgSkillExecutionIdAllocator allocator =
                new FpgSkillExecutionIdAllocator();

            Assert.That(allocator.Next().Value, Is.EqualTo(1L));
            Assert.That(allocator.Next().Value, Is.EqualTo(2L));

            allocator.Reset();
            Assert.That(allocator.Next().Value, Is.EqualTo(1L));
        }
    }
}
