using System;
using FPG.Demo.Core;

namespace FPG.Demo.Skills
{
    public readonly struct FpgSkillEventResult
    {
        internal FpgSkillEventResult(
            SkillExecutionId executionId,
            FpgSkillSequenceKind sequenceKind,
            FpgCompiledSkillEvent skillEvent,
            TickIndex scheduledTick,
            TickIndex tick,
            FpgSkillEventOutcome outcome)
        {
            ExecutionId = executionId;
            SequenceKind = sequenceKind;
            Event = skillEvent;
            ScheduledTick = scheduledTick;
            Tick = tick;
            Outcome = outcome;
        }

        public SkillExecutionId ExecutionId { get; }

        public FpgSkillSequenceKind SequenceKind { get; }

        public FpgCompiledSkillEvent Event { get; }

        public int EventId => Event.EventId;

        public TickIndex ScheduledTick { get; }

        public TickIndex Tick { get; }

        public FpgSkillEventOutcome Outcome { get; }
    }

    public sealed class FpgSkillExecutionRuntime
    {
        private readonly FpgSkillEventResult[] resultBuffer;
        private FpgCompiledSkillSequence sequence;
        private SkillExecutionId executionId;
        private TickIndex startTick;
        private TickIndex nextTick;
        private int nextEventIndex;
        private int resultCount;

        public FpgSkillExecutionRuntime(int resultCapacity)
        {
            if (resultCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(resultCapacity));
            }

            resultBuffer = resultCapacity == 0
                ? Array.Empty<FpgSkillEventResult>()
                : new FpgSkillEventResult[resultCapacity];
            sequence = default(FpgCompiledSkillSequence);
            executionId = SkillExecutionId.Invalid;
            startTick = TickIndex.Invalid;
            nextTick = TickIndex.Invalid;
            nextEventIndex = 0;
            resultCount = 0;
            State = FpgSkillExecutionState.Idle;
        }

        public FpgSkillExecutionState State { get; private set; }

        public bool IsRunning => State == FpgSkillExecutionState.Running;

        public bool IsTerminal => State == FpgSkillExecutionState.Completed
            || State == FpgSkillExecutionState.Canceled;

        public SkillExecutionId ExecutionId => executionId;

        public TickIndex StartTick => startTick;

        public TickIndex NextTick => nextTick;

        public int ResultCapacity => resultBuffer.Length;

        public int ResultCount => resultCount;

        public int RemainingEventCount => sequence.EventCount - nextEventIndex;

        public FpgSkillRuntimeResult Start(
            FpgCompiledSkillSequence compiledSequence,
            SkillExecutionId newExecutionId,
            TickIndex newStartTick)
        {
            ClearResults();

            if (State == FpgSkillExecutionState.Running)
            {
                return FpgSkillRuntimeResult.Rejected(FpgSkillRuntimeError.AlreadyRunning, State);
            }

            if (!newExecutionId.IsValid)
            {
                return FpgSkillRuntimeResult.Rejected(FpgSkillRuntimeError.InvalidExecutionId, State);
            }

            if (!compiledSequence.IsValid)
            {
                return FpgSkillRuntimeResult.Rejected(FpgSkillRuntimeError.InvalidSequence, State);
            }

            if (!newStartTick.IsValid)
            {
                return FpgSkillRuntimeResult.Rejected(FpgSkillRuntimeError.InvalidTick, State);
            }

            long requiredEndOffset = compiledSequence.DurationTicks;
            if (compiledSequence.HoldUntilCanceled)
            {
                requiredEndOffset = checked(requiredEndOffset + 1L);
            }

            if (newStartTick.Value > long.MaxValue - requiredEndOffset)
            {
                return FpgSkillRuntimeResult.Rejected(FpgSkillRuntimeError.TickRangeOverflow, State);
            }

            if (compiledSequence.EventCount > resultBuffer.Length)
            {
                return FpgSkillRuntimeResult.Rejected(FpgSkillRuntimeError.ResultCapacityExceeded, State);
            }

            sequence = compiledSequence;
            executionId = newExecutionId;
            startTick = newStartTick;
            nextTick = newStartTick;
            nextEventIndex = 0;
            State = FpgSkillExecutionState.Running;
            return FpgSkillRuntimeResult.Success(State, 0);
        }

        public FpgSkillRuntimeResult Tick(TickIndex tick)
        {
            ClearResults();
            FpgSkillRuntimeResult readiness = ValidateRunningTick(tick);
            if (!readiness.IsSuccess)
            {
                return readiness;
            }

            long relativeTick = tick.Value - startTick.Value;
            if (sequence.HoldUntilCanceled
                && relativeTick >= sequence.DurationTicks
                && tick.Value == long.MaxValue)
            {
                return FpgSkillRuntimeResult.Rejected(
                    FpgSkillRuntimeError.TickRangeOverflow,
                    State);
            }

            if (relativeTick <= sequence.DurationTicks)
            {
                int authoredTick = checked((int)relativeTick);
                while (nextEventIndex < sequence.EventCount)
                {
                    FpgCompiledSkillEvent skillEvent = sequence.GetEvent(nextEventIndex);
                    if (skillEvent.Tick != authoredTick)
                    {
                        break;
                    }

                    resultBuffer[resultCount++] = new FpgSkillEventResult(
                        executionId,
                        sequence.Kind,
                        skillEvent,
                        tick,
                        tick,
                        FpgSkillEventOutcome.Triggered);
                    nextEventIndex++;
                }
            }

            if (relativeTick == sequence.DurationTicks
                && !sequence.HoldUntilCanceled)
            {
                State = FpgSkillExecutionState.Completed;
                nextTick = TickIndex.Invalid;
            }
            else
            {
                nextTick = new TickIndex(tick.Value + 1L);
            }

            return FpgSkillRuntimeResult.Success(State, resultCount);
        }

        public FpgSkillRuntimeResult CancelRemaining(TickIndex tick)
        {
            ClearResults();
            FpgSkillRuntimeResult readiness = ValidateRunningTick(tick);
            if (!readiness.IsSuccess)
            {
                return readiness;
            }

            while (nextEventIndex < sequence.EventCount)
            {
                FpgCompiledSkillEvent skillEvent = sequence.GetEvent(nextEventIndex++);
                TickIndex scheduledTick = new TickIndex(startTick.Value + skillEvent.Tick);
                resultBuffer[resultCount++] = new FpgSkillEventResult(
                    executionId,
                    sequence.Kind,
                    skillEvent,
                    scheduledTick,
                    tick,
                    FpgSkillEventOutcome.Canceled);
            }

            State = FpgSkillExecutionState.Canceled;
            nextTick = TickIndex.Invalid;
            return FpgSkillRuntimeResult.Success(State, resultCount);
        }

        public FpgSkillEventResult GetResult(int index)
        {
            if (index < 0 || index >= resultCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return resultBuffer[index];
        }

        public int CopyResultsTo(FpgSkillEventResult[] destination, int destinationIndex = 0)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (destinationIndex < 0 || destinationIndex > destination.Length - resultCount)
            {
                throw new ArgumentOutOfRangeException(nameof(destinationIndex));
            }

            Array.Copy(resultBuffer, 0, destination, destinationIndex, resultCount);
            return resultCount;
        }

        public void Reset()
        {
            sequence = default(FpgCompiledSkillSequence);
            executionId = SkillExecutionId.Invalid;
            startTick = TickIndex.Invalid;
            nextTick = TickIndex.Invalid;
            nextEventIndex = 0;
            ClearResults();
            State = FpgSkillExecutionState.Idle;
        }

        private FpgSkillRuntimeResult ValidateRunningTick(TickIndex tick)
        {
            if (State == FpgSkillExecutionState.Completed || State == FpgSkillExecutionState.Canceled)
            {
                return FpgSkillRuntimeResult.Rejected(FpgSkillRuntimeError.AlreadyTerminal, State);
            }

            if (State != FpgSkillExecutionState.Running)
            {
                return FpgSkillRuntimeResult.Rejected(FpgSkillRuntimeError.NotRunning, State);
            }

            if (!tick.IsValid)
            {
                return FpgSkillRuntimeResult.Rejected(FpgSkillRuntimeError.InvalidTick, State);
            }

            if (tick != nextTick)
            {
                return FpgSkillRuntimeResult.Rejected(FpgSkillRuntimeError.WrongTick, State);
            }

            return FpgSkillRuntimeResult.Success(State, 0);
        }

        private void ClearResults()
        {
            resultCount = 0;
        }
    }
}
