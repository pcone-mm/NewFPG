using System;
using System.Collections.Generic;
using FPG.Demo.Core;
using FPG.Demo.Skills;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal enum FpgSkillPreviewMode
    {
        CurrentSequence = 0,
        ImmediateSecondary,
        ChargedSecondaryLifecycle
    }

    internal sealed class FpgSkillPreviewExecution
    {
        private readonly HashSet<int> committedGameplayEventIds =
            new HashSet<int>();
        private readonly List<FpgSkillSequenceKind> startedStages =
            new List<FpgSkillSequenceKind>(4);
        private FpgCompiledSkillSequence currentSequencePlan;
        private FpgCompiledSkillSequence chargeEnter;
        private FpgCompiledSkillSequence chargeLoop;
        private FpgCompiledSkillSequence release;
        private FpgCompiledSkillSequence cancel;
        private FpgSkillExecutionRuntime runtime;
        private FpgSkillEventResult[] resultBuffer =
            Array.Empty<FpgSkillEventResult>();
        private int resultCount;
        private int currentTick = -1;
        private int currentStageStartTick;
        private int chargeReleaseTick;
        private long nextExecutionId = 1L;
        private Func<FpgSkillEventResult, bool> gameplayCommitEvaluator =
            DefaultGameplayCommitEvaluator;

        public bool IsBound => runtime != null && currentSequencePlan.IsValid;
        public FpgSkillPreviewMode Mode { get; private set; }
        public int CurrentTick => currentTick;
        public int DurationTicks { get; private set; }
        public int ResultCount => resultCount;
        public int StartedStageCount => startedStages.Count;
        public FpgSkillSequenceKind CurrentSequenceKind =>
            runtime == null || !currentSequencePlan.IsValid
                ? FpgSkillSequenceKind.None
                : currentSequencePlan.Kind;
        public FpgCompiledSkillSequence CurrentSequence => currentSequencePlan;
        public int CurrentSequenceTick => currentTick < currentStageStartTick
            ? -1
            : currentTick - currentStageStartTick;

        public bool Bind(
            FpgCompiledSkillSequence compiledSequence,
            out string error)
        {
            return Bind(
                compiledSequence,
                DefaultGameplayCommitEvaluator,
                out error);
        }

        public bool Bind(
            FpgCompiledSkillSequence compiledSequence,
            Func<FpgSkillEventResult, bool> commitEvaluator,
            out string error)
        {
            return BindSingle(
                FpgSkillPreviewMode.CurrentSequence,
                compiledSequence,
                commitEvaluator,
                out error);
        }

        public bool BindImmediate(
            FpgCompiledSkillSequence execute,
            out string error)
        {
            return BindImmediate(
                execute,
                DefaultGameplayCommitEvaluator,
                out error);
        }

        public bool BindImmediate(
            FpgCompiledSkillSequence execute,
            Func<FpgSkillEventResult, bool> commitEvaluator,
            out string error)
        {
            if (!execute.IsValid
                || execute.Kind != FpgSkillSequenceKind.Execute)
            {
                Reset();
                error = "Immediate secondary preview requires a valid Execute sequence.";
                return false;
            }

            return BindSingle(
                FpgSkillPreviewMode.ImmediateSecondary,
                execute,
                commitEvaluator,
                out error);
        }

        public bool BindChargedLifecycle(
            FpgCompiledSkillSequence compiledChargeEnter,
            FpgCompiledSkillSequence compiledChargeLoop,
            FpgCompiledSkillSequence compiledRelease,
            FpgCompiledSkillSequence compiledCancel,
            int fullChargeTick,
            out string error)
        {
            return BindChargedLifecycle(
                compiledChargeEnter,
                compiledChargeLoop,
                compiledRelease,
                compiledCancel,
                fullChargeTick,
                DefaultGameplayCommitEvaluator,
                out error);
        }

        public bool BindChargedLifecycle(
            FpgCompiledSkillSequence compiledChargeEnter,
            FpgCompiledSkillSequence compiledChargeLoop,
            FpgCompiledSkillSequence compiledRelease,
            FpgCompiledSkillSequence compiledCancel,
            int fullChargeTick,
            Func<FpgSkillEventResult, bool> commitEvaluator,
            out string error)
        {
            Reset();
            if (!HasKind(
                    compiledChargeEnter,
                    FpgSkillSequenceKind.ChargeEnter)
                || !HasKind(
                    compiledChargeLoop,
                    FpgSkillSequenceKind.ChargeLoop)
                || !HasKind(
                    compiledRelease,
                    FpgSkillSequenceKind.Release)
                || !HasKind(
                    compiledCancel,
                    FpgSkillSequenceKind.Cancel))
            {
                error = "Charged preview requires ChargeEnter, ChargeLoop, Release and Cancel sequences.";
                return false;
            }

            if (!compiledChargeLoop.HoldUntilCanceled)
            {
                error = "Charged preview requires ChargeLoop to hold until canceled.";
                return false;
            }

            if (fullChargeTick <= compiledChargeEnter.DurationTicks)
            {
                error = "The full-charge tick must occur after ChargeEnter so ChargeLoop can run.";
                return false;
            }

            long duration = (long)fullChargeTick
                + compiledRelease.DurationTicks
                + 1L
                + compiledCancel.DurationTicks;
            if (duration > int.MaxValue)
            {
                error = "Charged preview duration exceeds the supported tick range.";
                return false;
            }

            Mode = FpgSkillPreviewMode.ChargedSecondaryLifecycle;
            gameplayCommitEvaluator = commitEvaluator
                ?? DefaultGameplayCommitEvaluator;
            chargeEnter = compiledChargeEnter;
            chargeLoop = compiledChargeLoop;
            release = compiledRelease;
            cancel = compiledCancel;
            chargeReleaseTick = fullChargeTick;
            DurationTicks = (int)duration;
            int maximumStageEvents = Math.Max(
                Math.Max(chargeEnter.EventCount, chargeLoop.EventCount),
                Math.Max(release.EventCount, cancel.EventCount));
            int totalEvents = checked(
                chargeEnter.EventCount
                + chargeLoop.EventCount
                + release.EventCount
                + cancel.EventCount);
            runtime = new FpgSkillExecutionRuntime(maximumStageEvents);
            resultBuffer = totalEvents == 0
                ? Array.Empty<FpgSkillEventResult>()
                : new FpgSkillEventResult[totalEvents];
            if (!RestartRuntime(out error))
            {
                Reset();
                return false;
            }

            return true;
        }

        public bool AdvanceTo(int tick, out string error)
        {
            error = string.Empty;
            ClearPendingResults();
            if (!IsBound || tick < 0 || tick > DurationTicks)
            {
                error = "Skill preview tick is outside the compiled preview range.";
                return false;
            }

            if (tick == currentTick)
            {
                return true;
            }

            bool captureResults = tick > currentTick;
            if (!captureResults && !RestartRuntime(out error))
            {
                return false;
            }

            while (currentTick < tick)
            {
                int nextTick = currentTick + 1;
                if (!TickRuntime(nextTick, captureResults, out error))
                {
                    ClearPendingResults();
                    return false;
                }

                currentTick = nextTick;
            }

            return true;
        }

        public FpgSkillEventResult GetResult(int index)
        {
            if (index < 0 || index >= resultCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return resultBuffer[index];
        }

        public FpgSkillSequenceKind GetStartedStage(int index)
        {
            if (index < 0 || index >= startedStages.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return startedStages[index];
        }

        public bool WasGameplayEventCommitted(int compiledEventId)
        {
            return compiledEventId > 0
                && committedGameplayEventIds.Contains(compiledEventId);
        }

        public bool CanPresent(in FpgCompiledSkillEvent skillEvent)
        {
            if (skillEvent.Kind != FpgSkillEventKind.ActivePresentation)
            {
                return true;
            }

            bool commitSucceeded = skillEvent.BoundGameplayEventId > 0
                && committedGameplayEventIds.Contains(
                    skillEvent.BoundGameplayEventId);
            return FpgSkillPresentationCommitRules.CanPresent(
                skillEvent,
                commitSucceeded);
        }

        public bool TryGetStageStartTick(
            FpgSkillSequenceKind kind,
            out int startTick)
        {
            startTick = 0;
            switch (Mode)
            {
                case FpgSkillPreviewMode.CurrentSequence:
                case FpgSkillPreviewMode.ImmediateSecondary:
                    return currentSequencePlan.Kind == kind;

                case FpgSkillPreviewMode.ChargedSecondaryLifecycle:
                    switch (kind)
                    {
                        case FpgSkillSequenceKind.ChargeEnter:
                            return true;
                        case FpgSkillSequenceKind.ChargeLoop:
                            startTick = chargeEnter.DurationTicks + 1;
                            return true;
                        case FpgSkillSequenceKind.Release:
                            startTick = chargeReleaseTick;
                            return true;
                        case FpgSkillSequenceKind.Cancel:
                            startTick = checked(
                                chargeReleaseTick
                                + release.DurationTicks
                                + 1);
                            return true;
                        default:
                            return false;
                    }

                default:
                    return false;
            }
        }

        public void ClearPendingResults()
        {
            resultCount = 0;
        }

        public void Reset()
        {
            runtime?.Reset();
            Mode = FpgSkillPreviewMode.CurrentSequence;
            currentSequencePlan = default(FpgCompiledSkillSequence);
            chargeEnter = default(FpgCompiledSkillSequence);
            chargeLoop = default(FpgCompiledSkillSequence);
            release = default(FpgCompiledSkillSequence);
            cancel = default(FpgCompiledSkillSequence);
            runtime = null;
            resultBuffer = Array.Empty<FpgSkillEventResult>();
            resultCount = 0;
            currentTick = -1;
            currentStageStartTick = 0;
            chargeReleaseTick = 0;
            DurationTicks = 0;
            nextExecutionId = 1L;
            gameplayCommitEvaluator = DefaultGameplayCommitEvaluator;
            committedGameplayEventIds.Clear();
            startedStages.Clear();
        }

        private bool BindSingle(
            FpgSkillPreviewMode mode,
            FpgCompiledSkillSequence compiledSequence,
            Func<FpgSkillEventResult, bool> commitEvaluator,
            out string error)
        {
            Reset();
            if (!compiledSequence.IsValid)
            {
                error = "Skill preview received an invalid compiled sequence.";
                return false;
            }

            Mode = mode;
            gameplayCommitEvaluator = commitEvaluator
                ?? DefaultGameplayCommitEvaluator;
            currentSequencePlan = compiledSequence;
            DurationTicks = compiledSequence.DurationTicks;
            runtime = new FpgSkillExecutionRuntime(compiledSequence.EventCount);
            resultBuffer = compiledSequence.EventCount == 0
                ? Array.Empty<FpgSkillEventResult>()
                : new FpgSkillEventResult[compiledSequence.EventCount];
            if (!RestartRuntime(out error))
            {
                Reset();
                return false;
            }

            return true;
        }

        private bool RestartRuntime(out string error)
        {
            runtime.Reset();
            committedGameplayEventIds.Clear();
            startedStages.Clear();
            resultCount = 0;
            currentTick = -1;
            currentStageStartTick = 0;
            nextExecutionId = 1L;
            FpgCompiledSkillSequence first =
                Mode == FpgSkillPreviewMode.ChargedSecondaryLifecycle
                    ? chargeEnter
                    : currentSequencePlan;
            return StartStage(first, 0, out error);
        }

        private bool TickRuntime(
            int tick,
            bool captureResults,
            out string error)
        {
            TickIndex simulationTick = new TickIndex(tick);
            if (Mode == FpgSkillPreviewMode.ChargedSecondaryLifecycle)
            {
                if (runtime.IsRunning
                    && FpgSecondarySkillLifecycleRules.IsChargeStage(
                        currentSequencePlan.Kind)
                    && tick == chargeReleaseTick)
                {
                    FpgSkillRuntimeResult canceled =
                        runtime.CancelRemaining(simulationTick);
                    if (!canceled.IsSuccess)
                    {
                        error = "Skill preview could not release ChargeLoop: "
                            + canceled.Error;
                        return false;
                    }

                    if (!StartStage(release, tick, out error))
                    {
                        return false;
                    }
                }
                else if (runtime.IsTerminal)
                {
                    if (FpgSecondarySkillLifecycleRules
                        .TryGetContinuationAfterCompletion(
                            currentSequencePlan.Kind,
                            out FpgSkillSequenceKind continuation))
                    {
                        FpgCompiledSkillSequence next = continuation
                            == FpgSkillSequenceKind.ChargeLoop
                                ? chargeLoop
                                : continuation == FpgSkillSequenceKind.Cancel
                                    ? cancel
                                    : default(FpgCompiledSkillSequence);
                        if (!next.IsValid)
                        {
                            error = "Skill preview resolved an invalid lifecycle continuation.";
                            return false;
                        }

                        if (!StartStage(next, tick, out error))
                        {
                            return false;
                        }
                    }
                }
            }

            FpgSkillRuntimeResult result = runtime.Tick(simulationTick);
            if (!result.IsSuccess)
            {
                error = "Skill preview execution failed: " + result.Error;
                return false;
            }

            ConsumeRuntimeResults(captureResults);
            error = string.Empty;
            return true;
        }

        private bool StartStage(
            FpgCompiledSkillSequence sequence,
            int tick,
            out string error)
        {
            FpgSkillRuntimeResult result = runtime.Start(
                sequence,
                new SkillExecutionId(nextExecutionId++),
                new TickIndex(tick));
            if (!result.IsSuccess)
            {
                error = "Skill preview stage could not start: " + result.Error;
                return false;
            }

            currentSequencePlan = sequence;
            currentStageStartTick = tick;
            startedStages.Add(sequence.Kind);
            error = string.Empty;
            return true;
        }

        private void ConsumeRuntimeResults(bool captureResults)
        {
            for (int index = 0; index < runtime.ResultCount; index++)
            {
                FpgSkillEventResult result = runtime.GetResult(index);
                if (result.Outcome != FpgSkillEventOutcome.Triggered)
                {
                    continue;
                }

                FpgCompiledSkillEvent skillEvent = result.Event;
                if (skillEvent.Kind == FpgSkillEventKind.GameplayAction)
                {
                    if (gameplayCommitEvaluator(result))
                    {
                        committedGameplayEventIds.Add(skillEvent.EventId);
                    }
                }
                else if (!CanPresent(skillEvent))
                {
                    continue;
                }

                if (captureResults)
                {
                    resultBuffer[resultCount++] = result;
                }
            }
        }

        private static bool HasKind(
            FpgCompiledSkillSequence sequence,
            FpgSkillSequenceKind kind)
        {
            return sequence.IsValid && sequence.Kind == kind;
        }

        private static bool DefaultGameplayCommitEvaluator(
            FpgSkillEventResult _)
        {
            return true;
        }
    }
}
