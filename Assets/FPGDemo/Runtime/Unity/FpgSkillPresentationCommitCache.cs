using System;
using FPG.Demo.Skills;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Fixed-capacity successful-action ledger retained until an execution is
    /// terminal. Delayed active presentation bindings query this ledger.
    /// </summary>
    public sealed class FpgSkillPresentationCommitCache
    {
        private readonly Entry[] entries;

        public FpgSkillPresentationCommitCache(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            entries = new Entry[capacity];
        }

        public int Capacity => entries.Length;
        public int Count { get; private set; }
        public int RejectCount { get; private set; }

        public bool TryRecordSuccess(
            SkillExecutionId executionId,
            int gameplayEventId)
        {
            if (!executionId.IsValid || gameplayEventId <= 0)
            {
                RejectCount++;
                return false;
            }

            int freeIndex = -1;
            for (int index = 0; index < entries.Length; index++)
            {
                if (!entries[index].ExecutionId.IsValid)
                {
                    if (freeIndex < 0)
                    {
                        freeIndex = index;
                    }

                    continue;
                }

                if (entries[index].ExecutionId == executionId
                    && entries[index].GameplayEventId == gameplayEventId)
                {
                    return true;
                }
            }

            if (freeIndex < 0)
            {
                RejectCount++;
                return false;
            }

            entries[freeIndex] = new Entry(executionId, gameplayEventId);
            Count++;
            return true;
        }

        public bool WasSuccessful(
            SkillExecutionId executionId,
            int gameplayEventId)
        {
            if (!executionId.IsValid || gameplayEventId <= 0)
            {
                return false;
            }

            for (int index = 0; index < entries.Length; index++)
            {
                if (entries[index].ExecutionId == executionId
                    && entries[index].GameplayEventId == gameplayEventId)
                {
                    return true;
                }
            }

            return false;
        }

        public void ReleaseExecution(SkillExecutionId executionId)
        {
            if (!executionId.IsValid)
            {
                return;
            }

            for (int index = 0; index < entries.Length; index++)
            {
                if (entries[index].ExecutionId != executionId)
                {
                    continue;
                }

                entries[index] = default(Entry);
                Count--;
            }
        }

        public void Clear()
        {
            Array.Clear(entries, 0, entries.Length);
            Count = 0;
            RejectCount = 0;
        }

        private readonly struct Entry
        {
            public Entry(
                SkillExecutionId executionId,
                int gameplayEventId)
            {
                ExecutionId = executionId;
                GameplayEventId = gameplayEventId;
            }

            public SkillExecutionId ExecutionId { get; }
            public int GameplayEventId { get; }
        }
    }
}
