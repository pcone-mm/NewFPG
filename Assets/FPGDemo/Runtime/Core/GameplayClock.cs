using System;

namespace FPG.Demo.Core
{
    public readonly struct ClockPumpResult
    {
        public ClockPumpResult(int stepsAvailable, long droppedAccumulatorUnits)
        {
            StepsAvailable = stepsAvailable;
            DroppedAccumulatorUnits = droppedAccumulatorUnits;
        }

        public int StepsAvailable { get; }

        public long DroppedAccumulatorUnits { get; }
    }

    public sealed class ClockDiagnostics
    {
        public long PumpCount { get; internal set; }

        public long CatchUpStepCount { get; internal set; }

        public long DroppedAccumulatorUnits { get; internal set; }

        public int MaxDebtTicksObserved { get; internal set; }

        internal void Reset()
        {
            PumpCount = 0L;
            CatchUpStepCount = 0L;
            DroppedAccumulatorUnits = 0L;
            MaxDebtTicksObserved = 0;
        }
    }

    public sealed class GameplayClock
    {
        public const int DefaultTickRate = 60;
        public const int DefaultMaxCatchUpSteps = 4;
        public const int DefaultMaxDebtTicks = 8;

        private readonly int tickRate;
        private readonly int maxCatchUpSteps;
        private readonly int maxDebtTicks;
        private readonly long maxDebtAccumulatorUnits;

        private long accumulatorUnits;
        private long nextTickValue;
        private int remainingStepsThisPump;

        public GameplayClock(
            int tickRate = DefaultTickRate,
            int maxCatchUpSteps = DefaultMaxCatchUpSteps,
            int maxDebtTicks = DefaultMaxDebtTicks)
        {
            if (tickRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate));
            }

            if (maxCatchUpSteps <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCatchUpSteps));
            }

            if (maxDebtTicks < maxCatchUpSteps)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDebtTicks));
            }

            this.tickRate = tickRate;
            this.maxCatchUpSteps = maxCatchUpSteps;
            this.maxDebtTicks = maxDebtTicks;
            maxDebtAccumulatorUnits = checked(TimeSpan.TicksPerSecond * (long)maxDebtTicks);
            Diagnostics = new ClockDiagnostics();
        }

        public int TickRate => tickRate;

        public int MaxCatchUpSteps => maxCatchUpSteps;

        public int MaxDebtTicks => maxDebtTicks;

        public bool IsPaused { get; private set; }

        public TickIndex CurrentTick => nextTickValue == 0L ? TickIndex.Invalid : new TickIndex(nextTickValue - 1L);

        public long ExecutedTickCount => nextTickValue;

        public int PendingDebtTicks => (int)(accumulatorUnits / TimeSpan.TicksPerSecond);

        public long AccumulatorUnits => accumulatorUnits;

        public ClockDiagnostics Diagnostics { get; }

        public DomainResult BeginPump(long elapsedTimeSpanTicks, out ClockPumpResult result)
        {
            result = new ClockPumpResult(0, 0L);

            if (elapsedTimeSpanTicks < 0L)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (remainingStepsThisPump != 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            Diagnostics.PumpCount++;

            if (IsPaused)
            {
                return DomainResult.Success;
            }

            long addedUnits = 0L;
            if (elapsedTimeSpanTicks > 0L)
            {
                if (elapsedTimeSpanTicks > long.MaxValue / tickRate)
                {
                    addedUnits = long.MaxValue;
                }
                else
                {
                    addedUnits = elapsedTimeSpanTicks * tickRate;
                }
            }

            long combinedUnits;
            if (addedUnits == long.MaxValue || accumulatorUnits > long.MaxValue - addedUnits)
            {
                combinedUnits = long.MaxValue;
            }
            else
            {
                combinedUnits = accumulatorUnits + addedUnits;
            }

            long droppedUnits = 0L;
            if (combinedUnits > maxDebtAccumulatorUnits)
            {
                droppedUnits = combinedUnits - maxDebtAccumulatorUnits;
                combinedUnits = maxDebtAccumulatorUnits;
                Diagnostics.DroppedAccumulatorUnits += droppedUnits;
            }

            accumulatorUnits = combinedUnits;
            int debtTicks = PendingDebtTicks;
            if (debtTicks > Diagnostics.MaxDebtTicksObserved)
            {
                Diagnostics.MaxDebtTicksObserved = debtTicks;
            }

            remainingStepsThisPump = Math.Min(debtTicks, maxCatchUpSteps);
            if (remainingStepsThisPump > 1)
            {
                Diagnostics.CatchUpStepCount += remainingStepsThisPump - 1;
            }

            result = new ClockPumpResult(remainingStepsThisPump, droppedUnits);
            return DomainResult.Success;
        }

        public bool TryConsumeStep(out TickIndex tick)
        {
            if (!TryPeekStep(out tick))
            {
                return false;
            }

            DomainResult committed = CommitStep(tick);
            if (!committed.IsSuccess)
            {
                throw new InvalidOperationException("Peeked gameplay step could not be committed.");
            }

            return true;
        }

        public bool TryPeekStep(out TickIndex tick)
        {
            if (remainingStepsThisPump <= 0)
            {
                tick = TickIndex.Invalid;
                return false;
            }

            tick = new TickIndex(nextTickValue);
            return true;
        }

        public DomainResult CommitStep(TickIndex tick)
        {
            if (remainingStepsThisPump <= 0 || tick.Value != nextTickValue)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            accumulatorUnits -= TimeSpan.TicksPerSecond;
            remainingStepsThisPump--;
            nextTickValue++;
            return DomainResult.Success;
        }

        /// <summary>
        /// Reverts the most recently committed step while the caller is still
        /// completing the current pump. Callers may use this only when no
        /// gameplay state was committed; BattleSession terminal faults retain
        /// their already-committed tick for forensic replay and do not call it.
        /// </summary>
        public DomainResult RollbackStep(TickIndex tick)
        {
            if (!tick.IsValid || nextTickValue != tick.Value + 1L)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            if (accumulatorUnits > long.MaxValue - TimeSpan.TicksPerSecond)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            nextTickValue--;
            accumulatorUnits += TimeSpan.TicksPerSecond;
            remainingStepsThisPump++;
            return DomainResult.Success;
        }

        public void AbortPump()
        {
            remainingStepsThisPump = 0;
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            remainingStepsThisPump = 0;
        }

        public void Reset()
        {
            accumulatorUnits = 0L;
            nextTickValue = 0L;
            remainingStepsThisPump = 0;
            IsPaused = false;
            Diagnostics.Reset();
        }
    }
}
