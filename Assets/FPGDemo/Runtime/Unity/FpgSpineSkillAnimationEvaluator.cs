using FPG.Demo.Skills;
using Spine;
using Spine.Unity;

namespace FPG.Demo.Unity
{
    public sealed class FpgSpineSkillAnimationEvaluator
    {
        private readonly SkeletonAnimation skeletonAnimation;
        private TrackEntry trackEntry;
        private string animationName = string.Empty;

        public FpgSpineSkillAnimationEvaluator(
            SkeletonAnimation skeletonAnimation)
        {
            this.skeletonAnimation = skeletonAnimation;
        }

        public bool TryEvaluate(
            string nextAnimationName,
            FpgCompiledSkillSequence sequence,
            int relativeTick,
            double interpolation,
            out string error)
        {
            return TryEvaluate(
                nextAnimationName,
                sequence,
                default(FpgResolvedSkillTimingSnapshot),
                relativeTick,
                interpolation,
                out error);
        }

        public bool TryEvaluate(
            string nextAnimationName,
            FpgCompiledSkillSequence sequence,
            FpgResolvedSkillTimingSnapshot timing,
            int relativeTick,
            double interpolation,
            out string error)
        {
            if (timing.IsValid
                && (timing.SourceGameplayHash != sequence.GameplayHash
                    || relativeTick < 0
                    || relativeTick > timing.ResolvedDurationTicks))
            {
                error =
                    "Resolved Spine skill evaluation requires timing from the same compiled sequence.";
                return false;
            }

            if (skeletonAnimation == null
                || skeletonAnimation.AnimationState == null
                || skeletonAnimation.Skeleton == null
                || string.IsNullOrWhiteSpace(nextAnimationName)
                || !sequence.IsValid)
            {
                error =
                    "Absolute Spine skill evaluation requires a loaded skeleton, animation, and compiled sequence.";
                return false;
            }

            Spine.Animation animation = skeletonAnimation.Skeleton.Data
                .FindAnimation(nextAnimationName);
            if (animation == null)
            {
                error =
                    $"Spine animation '{nextAnimationName}' is unavailable.";
                return false;
            }

            if (trackEntry == null
                || skeletonAnimation.AnimationState.GetCurrent(0) != trackEntry
                || !string.Equals(
                    animationName,
                    nextAnimationName,
                    System.StringComparison.Ordinal))
            {
                trackEntry = skeletonAnimation.AnimationState.SetAnimation(
                    0,
                    nextAnimationName,
                    sequence.Loop);
                animationName = nextAnimationName;
            }

            double seconds = FpgSkillAnimationTime.EvaluateSeconds(
                sequence,
                timing,
                relativeTick,
                interpolation,
                animation.Duration);
            trackEntry.TrackTime = (float)seconds;
            skeletonAnimation.AnimationState.Apply(
                skeletonAnimation.Skeleton);
            skeletonAnimation.Skeleton.UpdateWorldTransform();
            error = string.Empty;
            return true;
        }

        public void Reset()
        {
            trackEntry = null;
            animationName = string.Empty;
        }
    }
}
