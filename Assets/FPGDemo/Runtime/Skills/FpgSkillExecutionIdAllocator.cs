using System;

namespace FPG.Demo.Skills
{
    public sealed class FpgSkillExecutionIdAllocator
    {
        private long nextValue = 1L;

        public SkillExecutionId Next()
        {
            SkillExecutionId result = Peek();
            Commit(result);
            return result;
        }

        public void Reset()
        {
            nextValue = 1L;
        }

        public void Commit(SkillExecutionId candidate)
        {
            if (!candidate.IsValid || candidate.Value != nextValue)
            {
                throw new InvalidOperationException(
                    "Skill execution ID candidate is stale or foreign.");
            }

            nextValue++;
        }

        public SkillExecutionId Peek()
        {
            if (nextValue <= 0L || nextValue == long.MaxValue)
            {
                throw new OverflowException(
                    "Skill execution ID capacity was exhausted.");
            }

            return new SkillExecutionId(nextValue);
        }
    }
}
