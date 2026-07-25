using System;

namespace FPG.Demo.Skills
{
    public static class FpgSkillAnimationTime
    {
        public static double EvaluateSeconds(
            FpgCompiledSkillSequence sequence,
            int relativeTick,
            double interpolation,
            double naturalDurationSeconds)
        {
            if (!sequence.IsValid
                || relativeTick < 0
                || double.IsNaN(interpolation)
                || double.IsInfinity(interpolation)
                || interpolation < 0d
                || interpolation >= 1d
                || double.IsNaN(naturalDurationSeconds)
                || double.IsInfinity(naturalDurationSeconds)
                || naturalDurationSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(relativeTick));
            }

            double absoluteTick = relativeTick + interpolation;
            double localTick = Math.Max(
                0d,
                absoluteTick - sequence.AnimationStartTick);
            if (sequence.AnimationPlaybackMode
                == FpgSkillAnimationPlaybackMode.FitInterval)
            {
                int intervalTicks =
                    sequence.AnimationEndTick - sequence.AnimationStartTick;
                if (intervalTicks <= 0 || naturalDurationSeconds <= 0d)
                {
                    return 0d;
                }

                double normalized = Math.Min(1d, localTick / intervalTicks);
                return normalized * naturalDurationSeconds;
            }

            double seconds =
                localTick / FpgSkillRuntimeConstants.TickRate;
            if (naturalDurationSeconds <= 0d)
            {
                return seconds;
            }

            if (!sequence.Loop)
            {
                return Math.Min(seconds, naturalDurationSeconds);
            }

            return seconds % naturalDurationSeconds;
        }
    }
}
