using System;

namespace FPG.Demo.Skills
{
    public static class FpgSkillAnimationTime
    {
        public static double EvaluateSeconds(
            FpgCompiledSkillSequence sequence,
            FpgResolvedSkillTimingSnapshot timing,
            int relativeTick,
            double interpolation,
            double naturalDurationSeconds)
        {
            // Fixed cooldown sequences preserve their authored presentation
            // mapping exactly. Only character attack-speed schedules remap
            // their execution-local ticks to authored animation time.
            if (!timing.IsValid || !timing.UsesCharacterAttackSpeed)
            {
                return EvaluateSeconds(
                    sequence,
                    relativeTick,
                    interpolation,
                    naturalDurationSeconds);
            }

            if (!sequence.IsValid
                || timing.SourceGameplayHash != sequence.GameplayHash
                || relativeTick < 0
                || relativeTick > timing.ResolvedDurationTicks
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

            double authoredTick = ResolveAuthoredAnimationTick(
                timing,
                relativeTick + interpolation);
            return EvaluateAuthoredSeconds(
                sequence,
                authoredTick,
                naturalDurationSeconds);
        }

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

            return EvaluateAuthoredSeconds(
                sequence,
                relativeTick + interpolation,
                naturalDurationSeconds);
        }

        private static double EvaluateAuthoredSeconds(
            FpgCompiledSkillSequence sequence,
            double authoredTick,
            double naturalDurationSeconds)
        {
            double localTick = Math.Max(
                0d,
                authoredTick - sequence.AnimationStartTick);
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

        private static double ResolveAuthoredAnimationTick(
            FpgResolvedSkillTimingSnapshot timing,
            double resolvedTick)
        {
            int authoredAttackFrame = timing.AuthoredAttackFrameTick;
            int resolvedAttackFrame = timing.WindupTicks;

            // This branch includes the exact attack frame. Consequently, the
            // animation reaches its authored attack frame on the same 60 Hz
            // tick as the gameplay attack event.
            if (resolvedTick <= resolvedAttackFrame)
            {
                return resolvedAttackFrame <= 0
                    ? authoredAttackFrame
                    : Math.Min(
                        authoredAttackFrame,
                        resolvedTick * authoredAttackFrame
                            / resolvedAttackFrame);
            }

            // Once the gameplay attack frame has occurred, keep advancing at
            // the authored 60 Hz rate. A later execution replaces this clip at
            // its actual start tick; otherwise the clip plays to its natural
            // end.
            return authoredAttackFrame
                + resolvedTick - resolvedAttackFrame;
        }
    }
}
