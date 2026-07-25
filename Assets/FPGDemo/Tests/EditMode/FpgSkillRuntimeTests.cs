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
                new FpgCompiledSkillEvent(2, 2, FpgSkillEventKind.GameplayPayload, 99, 0, 0));

            FpgCompiledSkillDefinition definition = new FpgCompiledSkillDefinition(77, new[] { first });
            FpgCompiledSkillDefinition reorderedDefinition = new FpgCompiledSkillDefinition(77, new[] { reordered });
            FpgCompiledSkillDefinition changedDefinition = new FpgCompiledSkillDefinition(77, new[] { changed });

            Assert.That(FpgSkillRuntimeConstants.TickRate, Is.EqualTo(60));
            Assert.That(FpgSkillRuntimeConstants.GameplayHashVersion, Is.EqualTo(1));
            Assert.That(first.GameplayHash, Is.EqualTo(reordered.GameplayHash));
            Assert.That(definition.GameplayHash, Is.EqualTo(reorderedDefinition.GameplayHash));
            Assert.That(changed.GameplayHash, Is.Not.EqualTo(first.GameplayHash));
            Assert.That(changedDefinition.GameplayHash, Is.Not.EqualTo(definition.GameplayHash));
        }

        [Test]
        public void CompilerCopiesAndExposesAuthoredPhases()
        {
            FpgCompiledSkillPhase[] phases =
            {
                Phase(101, FpgSkillPhaseKind.Startup, 0, 2),
                Phase(202, FpgSkillPhaseKind.Active, 2, 4),
                Phase(303, FpgSkillPhaseKind.Recovery, 4, 6)
            };

            bool compiled = FpgSkillCompiler.TryCompileSequence(
                FpgSkillSequenceKind.Execute,
                6,
                1001,
                false,
                phases,
                new FpgCompiledSkillEvent[0],
                out FpgCompiledSkillSequence sequence,
                out FpgSkillValidationResult validation);

            Assert.That(compiled, Is.True, validation.ToString());
            Assert.That((int)FpgSkillPhaseKind.Startup, Is.EqualTo(1));
            Assert.That((int)FpgSkillPhaseKind.Active, Is.EqualTo(2));
            Assert.That((int)FpgSkillPhaseKind.Recovery, Is.EqualTo(3));
            Assert.That(sequence.PhaseCount, Is.EqualTo(3));
            Assert.That(sequence.Phases.Count, Is.EqualTo(3));
            Assert.That(
                sequence.GetPhase(1),
                Is.EqualTo(Phase(202, FpgSkillPhaseKind.Active, 2, 4)));

            phases[1] = Phase(999, FpgSkillPhaseKind.Active, 2, 4);
            Assert.That(sequence.GetPhase(1).PhaseId, Is.EqualTo(202));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => sequence.GetPhase(3));
        }

        [Test]
        public void CompilerRejectsDuplicatePhaseIdsAndKinds()
        {
            bool duplicateIdCompiled = FpgSkillCompiler.TryCompileSequence(
                FpgSkillSequenceKind.Execute,
                4,
                1001,
                false,
                new[]
                {
                    Phase(101, FpgSkillPhaseKind.Startup, 0, 2),
                    Phase(101, FpgSkillPhaseKind.Active, 2, 4)
                },
                new FpgCompiledSkillEvent[0],
                out _,
                out FpgSkillValidationResult duplicateIdValidation);

            Assert.That(duplicateIdCompiled, Is.False);
            Assert.That(
                duplicateIdValidation.Error,
                Is.EqualTo(FpgSkillValidationError.DuplicatePhaseId));
            Assert.That(duplicateIdValidation.EventIndex, Is.EqualTo(1));

            bool duplicateKindCompiled = FpgSkillCompiler.TryCompileSequence(
                FpgSkillSequenceKind.Execute,
                4,
                1001,
                false,
                new[]
                {
                    Phase(101, FpgSkillPhaseKind.Active, 0, 2),
                    Phase(202, FpgSkillPhaseKind.Active, 2, 4)
                },
                new FpgCompiledSkillEvent[0],
                out _,
                out FpgSkillValidationResult duplicateKindValidation);

            Assert.That(duplicateKindCompiled, Is.False);
            Assert.That(
                duplicateKindValidation.Error,
                Is.EqualTo(FpgSkillValidationError.DuplicatePhaseKind));
        }

        [Test]
        public void CompilerRejectsInvalidPhaseRangesAndOrdering()
        {
            bool rangeCompiled = FpgSkillCompiler.TryCompileSequence(
                FpgSkillSequenceKind.Execute,
                4,
                1001,
                false,
                new[]
                {
                    Phase(101, FpgSkillPhaseKind.Active, 0, 5)
                },
                new FpgCompiledSkillEvent[0],
                out _,
                out FpgSkillValidationResult rangeValidation);

            Assert.That(rangeCompiled, Is.False);
            Assert.That(
                rangeValidation.Error,
                Is.EqualTo(FpgSkillValidationError.PhaseTickOutOfRange));

            bool orderCompiled = FpgSkillCompiler.TryCompileSequence(
                FpgSkillSequenceKind.Execute,
                4,
                1001,
                false,
                new[]
                {
                    Phase(101, FpgSkillPhaseKind.Startup, 0, 3),
                    Phase(202, FpgSkillPhaseKind.Active, 2, 4)
                },
                new FpgCompiledSkillEvent[0],
                out _,
                out FpgSkillValidationResult orderValidation);

            Assert.That(orderCompiled, Is.False);
            Assert.That(
                orderValidation.Error,
                Is.EqualTo(FpgSkillValidationError.InvalidPhaseOrder));
        }

        [Test]
        public void GameplayHashIsSensitiveToPhaseData()
        {
            FpgCompiledSkillSequence first = new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                4,
                1001,
                false,
                new[]
                {
                    Phase(101, FpgSkillPhaseKind.Active, 1, 3)
                },
                new FpgCompiledSkillEvent[0]);
            FpgCompiledSkillSequence identical = new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                4,
                1001,
                false,
                new[]
                {
                    Phase(101, FpgSkillPhaseKind.Active, 1, 3)
                },
                new FpgCompiledSkillEvent[0]);
            FpgCompiledSkillSequence changed = new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                4,
                1001,
                false,
                new[]
                {
                    Phase(101, FpgSkillPhaseKind.Active, 1, 4)
                },
                new FpgCompiledSkillEvent[0]);

            Assert.That(identical.GameplayHash, Is.EqualTo(first.GameplayHash));
            Assert.That(changed.GameplayHash, Is.Not.EqualTo(first.GameplayHash));
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

        private static FpgCompiledSkillPhase Phase(
            int phaseId,
            FpgSkillPhaseKind kind,
            int startTick,
            int endTick)
        {
            return new FpgCompiledSkillPhase(
                phaseId,
                kind,
                startTick,
                endTick);
        }

        private static FpgCompiledSkillEvent PayloadEvent(
            int eventId,
            int tick,
            int sortOrder = 0)
        {
            return new FpgCompiledSkillEvent(
                eventId,
                tick,
                FpgSkillEventKind.GameplayPayload,
                eventId,
                0,
                0,
                sortOrder);
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
