using System;
using FPG.Demo.Core;
using FPG.Demo.Skills;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal sealed class FpgSkillPreviewExecution
    {
        private FpgCompiledSkillSequence sequence;
        private FpgSkillExecutionRuntime runtime;
        private FpgSkillEventResult[] resultBuffer =
            Array.Empty<FpgSkillEventResult>();
        private int resultCount;
        private int currentTick = -1;

        public bool IsBound => sequence.IsValid && runtime != null;
        public int CurrentTick => currentTick;
        public int ResultCount => resultCount;

        public bool Bind(
            FpgCompiledSkillSequence compiledSequence,
            out string error)
        {
            Reset();
            if (!compiledSequence.IsValid)
            {
                error = "技能预览执行器收到无效的编译序列。";
                return false;
            }

            sequence = compiledSequence;
            runtime = new FpgSkillExecutionRuntime(sequence.EventCount);
            resultBuffer = sequence.EventCount == 0
                ? Array.Empty<FpgSkillEventResult>()
                : new FpgSkillEventResult[sequence.EventCount];
            if (!StartRuntime(out error))
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
            if (!IsBound || tick < 0 || tick > sequence.DurationTicks)
            {
                error = "技能预览 Tick 超出正式编译序列范围。";
                return false;
            }

            if (tick == currentTick)
            {
                return true;
            }

            bool captureResults = tick > currentTick;
            if (!captureResults && !StartRuntime(out error))
            {
                return false;
            }

            while (currentTick < tick)
            {
                TickIndex nextTick = runtime.NextTick;
                FpgSkillRuntimeResult result = runtime.Tick(nextTick);
                if (!result.IsSuccess)
                {
                    ClearPendingResults();
                    error = "技能预览执行失败：" + result.Error;
                    return false;
                }

                if (captureResults)
                {
                    for (int index = 0; index < runtime.ResultCount; index++)
                    {
                        resultBuffer[resultCount++] = runtime.GetResult(index);
                    }
                }

                currentTick = checked((int)nextTick.Value);
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

        public void ClearPendingResults()
        {
            resultCount = 0;
        }

        public void Reset()
        {
            runtime?.Reset();
            sequence = default(FpgCompiledSkillSequence);
            runtime = null;
            resultBuffer = Array.Empty<FpgSkillEventResult>();
            resultCount = 0;
            currentTick = -1;
        }

        private bool StartRuntime(out string error)
        {
            runtime.Reset();
            FpgSkillRuntimeResult result = runtime.Start(
                sequence,
                new SkillExecutionId(1L),
                new TickIndex(0L));
            if (!result.IsSuccess)
            {
                error = "技能预览执行器启动失败：" + result.Error;
                return false;
            }

            currentTick = -1;
            error = string.Empty;
            return true;
        }
    }
}
