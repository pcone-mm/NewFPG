using FPG.Demo.Core;
using FPG.Demo.Skills;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal sealed class FpgSkillPreviewExecution
    {
        private FpgCompiledSkillSequence sequence;
        private FpgSkillExecutionRuntime runtime;
        private int currentTick = -1;

        public bool IsBound => sequence.IsValid && runtime != null;
        public int CurrentTick => currentTick;
        public int ResultCount => runtime == null ? 0 : runtime.ResultCount;

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
            if (!IsBound || tick < 0 || tick > sequence.DurationTicks)
            {
                error = "技能预览 Tick 超出正式编译序列范围。";
                return false;
            }

            if (tick <= currentTick && !StartRuntime(out error))
            {
                return false;
            }

            while (currentTick < tick)
            {
                TickIndex nextTick = runtime.NextTick;
                FpgSkillRuntimeResult result = runtime.Tick(nextTick);
                if (!result.IsSuccess)
                {
                    error = "技能预览执行失败：" + result.Error;
                    return false;
                }

                currentTick = checked((int)nextTick.Value);
            }

            return true;
        }

        public FpgSkillEventResult GetResult(int index)
        {
            return runtime.GetResult(index);
        }

        public void Reset()
        {
            sequence = default(FpgCompiledSkillSequence);
            runtime = null;
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
