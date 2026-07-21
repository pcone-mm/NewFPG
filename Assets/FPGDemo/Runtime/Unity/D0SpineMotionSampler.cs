using Spine;
using Spine.Unity;
using Spine.Unity.AnimationTools;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Resolves and samples one Spine translate timeline at an explicit absolute
    /// time. It reads its motion-space Transform only for vector conversion and
    /// never advances a SkeletonAnimation or writes any Transform.
    /// </summary>
    public sealed class D0SpineMotionSampler
    {
        private readonly SkeletonDataAsset skeletonDataAsset;
        private readonly Transform motionSpace;

        private D0AnimationMotionSettings settings;
        private TranslateTimeline motionTimeline;
        private Vector2 initialTimelineOffset;
        private float duration;
        private bool isConfigured;

        public D0SpineMotionSampler(SkeletonAnimation skeletonAnimation)
            : this(
                skeletonAnimation == null ? null : skeletonAnimation.SkeletonDataAsset,
                skeletonAnimation == null ? null : skeletonAnimation.transform)
        {
        }

        public D0SpineMotionSampler(
            SkeletonDataAsset skeletonDataAsset,
            Transform motionSpace)
        {
            this.skeletonDataAsset = skeletonDataAsset;
            this.motionSpace = motionSpace;
        }

        public bool IsConfigured => isConfigured;
        public bool Enabled => isConfigured && settings.Enabled;
        public bool PersistEndOffset => isConfigured && settings.PersistEndOffset;
        public float Duration => isConfigured ? duration : 0f;

        public bool TryConfigure(
            D0AnimationMotionSettings nextSettings,
            out string error)
        {
            ClearConfiguration();

            if (!nextSettings.TryValidate(out error))
            {
                return false;
            }

            if (!nextSettings.Enabled)
            {
                settings = nextSettings;
                isConfigured = true;
                error = string.Empty;
                return true;
            }

            if (skeletonDataAsset == null)
            {
                error = "Enabled animation motion requires a SkeletonDataAsset.";
                return false;
            }

            if (motionSpace == null)
            {
                error = "Enabled animation motion requires a motion-space Transform.";
                return false;
            }

            SkeletonData skeletonData = skeletonDataAsset.GetSkeletonData(true);
            if (skeletonData == null)
            {
                error = "Animation motion could not load its SkeletonDataAsset.";
                return false;
            }

            Spine.Animation animation = skeletonData.FindAnimation(nextSettings.AnimationName);
            if (animation == null)
            {
                error = $"Animation motion could not find Spine animation '{nextSettings.AnimationName}'.";
                return false;
            }

            int motionBoneIndex = skeletonData.FindBoneIndex(nextSettings.MotionBoneName);
            if (motionBoneIndex < 0)
            {
                error = $"Animation motion could not find Spine bone '{nextSettings.MotionBoneName}'.";
                return false;
            }

            BoneData motionBone = skeletonData.Bones.Items[motionBoneIndex];
            if (motionBone == null || motionBone.Parent != null)
            {
                error = $"Spine motion bone '{nextSettings.MotionBoneName}' must be an independent top-level marker bone.";
                return false;
            }

            if (TryFindSlotDrivenByMotionBoneHierarchy(
                    skeletonData,
                    motionBoneIndex,
                    out string slotName,
                    out string slotBoneName))
            {
                error = $"Spine motion bone '{nextSettings.MotionBoneName}' must be a marker-only bone, "
                    + $"but slot '{slotName}' is attached to its hierarchy at bone '{slotBoneName}'.";
                return false;
            }

            TranslateTimeline timeline = animation.FindTranslateTimelineForBone(motionBoneIndex);
            if (timeline == null
                || timeline.Frames == null
                || timeline.Frames.Length < TranslateTimeline.ENTRIES
                || timeline.Frames.Length % TranslateTimeline.ENTRIES != 0)
            {
                error = $"Spine animation '{nextSettings.AnimationName}' has no translate timeline "
                    + $"for motion bone '{nextSettings.MotionBoneName}'.";
                return false;
            }

            float[] frames = timeline.Frames;
            float previousTime = float.NegativeInfinity;
            for (int frameOffset = 0;
                 frameOffset < frames.Length;
                 frameOffset += TranslateTimeline.ENTRIES)
            {
                float frameTime = frames[frameOffset];
                float frameX = frames[frameOffset + 1];
                float frameY = frames[frameOffset + 2];
                if (!IsFinite(frameTime) || !IsFinite(frameX) || !IsFinite(frameY))
                {
                    error = $"Spine animation '{nextSettings.AnimationName}' has a non-finite "
                        + $"translate key for motion bone '{nextSettings.MotionBoneName}'.";
                    return false;
                }

                if (frameOffset > 0 && frameTime <= previousTime)
                {
                    error = $"Spine animation '{nextSettings.AnimationName}' must have strictly "
                        + $"increasing translate key times for motion bone '{nextSettings.MotionBoneName}'.";
                    return false;
                }

                previousTime = frameTime;
            }

            if (!IsFinite(animation.Duration) || animation.Duration < 0f)
            {
                error = $"Spine animation '{nextSettings.AnimationName}' has an invalid duration.";
                return false;
            }

            Vector2 firstOffset = timeline.Evaluate(0f);
            if (!IsFinite(firstOffset))
            {
                error = $"Spine animation '{nextSettings.AnimationName}' has an invalid initial motion offset.";
                return false;
            }

            settings = nextSettings;
            motionTimeline = timeline;
            initialTimelineOffset = firstOffset;
            duration = animation.Duration;
            isConfigured = true;
            error = string.Empty;
            return true;
        }

        public bool TrySampleAbsoluteOffset(
            float seconds,
            out Vector3 offset,
            out string error)
        {
            offset = Vector3.zero;
            if (!isConfigured)
            {
                error = "Animation motion sampler must be configured before sampling.";
                return false;
            }

            if (!IsFinite(seconds))
            {
                error = "Animation motion sample time must be finite.";
                return false;
            }

            if (!settings.Enabled)
            {
                error = string.Empty;
                return true;
            }

            if (motionTimeline == null || motionSpace == null)
            {
                error = "Animation motion sampler lost its configured Spine timeline or motion space.";
                return false;
            }

            float sampleTime = Mathf.Clamp(seconds, 0f, duration);
            Vector2 sampledOffset = motionTimeline.Evaluate(sampleTime) - initialTimelineOffset;
            if (!IsFinite(sampledOffset))
            {
                error = "Animation motion timeline produced a non-finite offset.";
                return false;
            }

            offset = motionSpace.TransformVector(
                new Vector3(sampledOffset.x, sampledOffset.y, 0f));
            if (!IsFinite(offset))
            {
                offset = Vector3.zero;
                error = "Animation motion-space conversion produced a non-finite offset.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void ClearConfiguration()
        {
            settings = default(D0AnimationMotionSettings);
            motionTimeline = null;
            initialTimelineOffset = Vector2.zero;
            duration = 0f;
            isConfigured = false;
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool TryFindSlotDrivenByMotionBoneHierarchy(
            SkeletonData skeletonData,
            int motionBoneIndex,
            out string slotName,
            out string slotBoneName)
        {
            slotName = string.Empty;
            slotBoneName = string.Empty;
            if (skeletonData == null
                || motionBoneIndex < 0
                || motionBoneIndex >= skeletonData.Bones.Count)
            {
                return false;
            }

            BoneData motionBone = skeletonData.Bones.Items[motionBoneIndex];
            for (int slotIndex = 0; slotIndex < skeletonData.Slots.Count; slotIndex++)
            {
                SlotData slot = skeletonData.Slots.Items[slotIndex];
                BoneData current = slot == null ? null : slot.BoneData;
                BoneData slotBone = current;
                while (current != null)
                {
                    if (ReferenceEquals(current, motionBone))
                    {
                        slotName = slot.Name;
                        slotBoneName = slotBone.Name;
                        return true;
                    }

                    current = current.Parent;
                }
            }

            return false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
