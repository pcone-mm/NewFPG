using System;
using FPG.Demo.Skills;
using Spine;
using Spine.Unity;
using Spine.Unity.AnimationTools;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SkeletonAnimation))]
    [DefaultExecutionOrder(-1000)]
    public sealed class FpgEntitySkeletonRootMotionBridge : SkeletonRootMotion
    {
        private const int MainTrackFlag = 1;

        [NonSerialized]
        private SkeletonAnimation skeletonAnimation;

        [NonSerialized]
        private Transform entityRoot;

        [NonSerialized]
        private bool officialStartCompleted;

        [NonSerialized]
        private bool callbacksSubscribed;

        [NonSerialized]
        private bool motionEnabled;

        [NonSerialized]
        private bool hasCapturedWorldPosition;

        [NonSerialized]
        private Vector3 capturedWorldPosition;

        [NonSerialized]
        private Vector3 authoredLocalPosition;

        [NonSerialized]
        private Quaternion authoredLocalRotation = Quaternion.identity;

        [NonSerialized]
        private Vector3 authoredLocalScale = Vector3.one;

        public string RootMotionBoneName => rootMotionBoneName ?? string.Empty;
        public bool MotionEnabled => motionEnabled;

        private void Awake()
        {
            animationTrackFlags = 0;
            if (Application.isPlaying
                && !TryEnsureOfficialStart(out string error))
            {
                Debug.LogError(error, this);
            }
        }

        protected override void Start()
        {
            if (!TryEnsureOfficialStart(out string error))
            {
                Debug.LogError(error, this);
            }
        }

        protected override void FixedUpdate()
        {
            if (!officialStartCompleted
                || skeletonComponent == null
                || skeletonComponent.Skeleton == null)
            {
                return;
            }

            base.FixedUpdate();
        }

        protected override void OnDisable()
        {
            hasCapturedWorldPosition = false;
            base.OnDisable();
        }

        private void OnDestroy()
        {
            UnsubscribeCallbacks();
        }

        public bool TryInitializeForEntity(
            Transform nextEntityRoot,
            out string error)
        {
            if (nextEntityRoot == null
                || nextEntityRoot == transform
                || !transform.IsChildOf(nextEntityRoot))
            {
                error =
                    "Entity root motion requires a parent Entity transform distinct from VisualRoot.";
                return false;
            }

            if (rigidBody != null || rigidBody2D != null)
            {
                error =
                    "Entity root motion bridge must not bind a Rigidbody or Rigidbody2D.";
                return false;
            }

            if (!TryEnsureOfficialStart(out error))
            {
                return false;
            }

            entityRoot = nextEntityRoot;
            authoredLocalPosition = transform.localPosition;
            authoredLocalRotation = transform.localRotation;
            authoredLocalScale = transform.localScale;
            SetMotionEnabled(false);
            error = string.Empty;
            return true;
        }

        public void SetMotionEnabled(bool enabled)
        {
            motionEnabled = enabled;
            animationTrackFlags = enabled ? MainTrackFlag : 0;
            hasCapturedWorldPosition = false;
        }

        public void ResetForPool()
        {
            SetMotionEnabled(false);
            if (entityRoot != null)
            {
                transform.localPosition = authoredLocalPosition;
                transform.localRotation = authoredLocalRotation;
                transform.localScale = authoredLocalScale;
            }

            entityRoot = null;
        }

        public bool TryValidateConfiguration(
            SkeletonData data,
            FpgEnemyBehaviorDefinition behavior,
            out string error)
        {
            if (data == null || behavior == null)
            {
                error =
                    "Entity root motion validation requires skeleton data and behavior.";
                return false;
            }

            if (!transformPositionX || !transformPositionY)
            {
                error =
                    "Entity root motion bridge must extract both Spine X and Y translation.";
                return false;
            }

            if (rigidBody != null || rigidBody2D != null)
            {
                error =
                    "Entity root motion bridge must not bind a Rigidbody or Rigidbody2D.";
                return false;
            }

            if (!AreFiniteRootMotionSettings())
            {
                error = "Entity root motion bridge has non-finite scale settings.";
                return false;
            }

            int boneIndex = string.IsNullOrWhiteSpace(rootMotionBoneName)
                ? -1
                : data.FindBoneIndex(rootMotionBoneName);
            if (boneIndex < 0)
            {
                error = "Entity root motion bone '" + RootMotionBoneName
                    + "' is missing from the Spine skeleton.";
                return false;
            }

            for (int index = 0;
                index < behavior.AnimationRootMotionRuleCount;
                index++)
            {
                FpgAnimationRootMotionRule rule =
                    behavior.GetAnimationRootMotionRule(index);
                Spine.Animation animation =
                    data.FindAnimation(rule.AnimationName);
                if (animation == null)
                {
                    error = "Root-motion animation '" + rule.AnimationName
                        + "' is missing from the Spine skeleton.";
                    return false;
                }

                if (!rule.Enabled)
                {
                    continue;
                }

                TranslateTimeline timeline =
                    animation.FindTranslateTimelineForBone(boneIndex);
                if (timeline == null)
                {
                    error = "Root-motion animation '" + rule.AnimationName
                        + "' has no TranslateTimeline for bone '"
                        + RootMotionBoneName + "'.";
                    return false;
                }

                if (!IsFinite(animation.Duration)
                    || !AreFiniteTimelineFrames(timeline.Frames))
                {
                    error = "Root-motion animation '" + rule.AnimationName
                        + "' has non-finite timeline values.";
                    return false;
                }

                float officialSampleDuration =
                    1f / FpgSkillRuntimeConstants.TickRate;
                if (animation.Duration <= officialSampleDuration)
                {
                    error = "Root-motion animation '" + rule.AnimationName
                        + "' must be longer than one official SkeletonRootMotion "
                        + "sample at 60 Hz (1/60 second); shorter or equal "
                        + "durations can skip loop displacement.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private bool TryEnsureOfficialStart(out string error)
        {
            skeletonAnimation = skeletonAnimation != null
                ? skeletonAnimation
                : GetComponent<SkeletonAnimation>();
            if (skeletonAnimation == null)
            {
                error =
                    "Entity root motion bridge requires SkeletonAnimation on the same GameObject.";
                return false;
            }

            skeletonAnimation.Initialize(false);
            if (skeletonAnimation.Skeleton == null
                || skeletonAnimation.AnimationState == null)
            {
                error =
                    "Entity root motion bridge requires an initialized SkeletonAnimation.";
                return false;
            }

            if (!officialStartCompleted)
            {
                base.Start();
                officialStartCompleted = true;
            }

            SubscribeCallbacks();
            error = string.Empty;
            return true;
        }

        private void SubscribeCallbacks()
        {
            if (callbacksSubscribed || skeletonAnimation == null)
            {
                return;
            }

            skeletonAnimation.BeforeApply += HandleBeforeApply;
            skeletonAnimation.UpdateComplete += HandleUpdateComplete;
            callbacksSubscribed = true;
        }

        private void UnsubscribeCallbacks()
        {
            if (!callbacksSubscribed || skeletonAnimation == null)
            {
                return;
            }

            skeletonAnimation.BeforeApply -= HandleBeforeApply;
            skeletonAnimation.UpdateComplete -= HandleUpdateComplete;
            callbacksSubscribed = false;
        }

        private void HandleBeforeApply(ISkeletonAnimation animated)
        {
            if (!motionEnabled || entityRoot == null)
            {
                hasCapturedWorldPosition = false;
                return;
            }

            capturedWorldPosition = transform.position;
            hasCapturedWorldPosition = true;
        }

        private void HandleUpdateComplete(ISkeletonAnimation animated)
        {
            if (!hasCapturedWorldPosition)
            {
                return;
            }

            hasCapturedWorldPosition = false;
            Vector3 worldDelta = transform.position - capturedWorldPosition;
            transform.localPosition = authoredLocalPosition;
            if (!IsFinite(worldDelta.x)
                || !IsFinite(worldDelta.y)
                || !IsFinite(worldDelta.z))
            {
                return;
            }

            entityRoot.position += worldDelta;
        }

        private bool AreFiniteRootMotionSettings()
        {
            return IsFinite(rootMotionScaleX)
                && IsFinite(rootMotionScaleY)
                && IsFinite(rootMotionTranslateXPerY)
                && IsFinite(rootMotionTranslateYPerX);
        }

        private static bool AreFiniteTimelineFrames(float[] frames)
        {
            if (frames == null
                || frames.Length < TranslateTimeline.ENTRIES
                || frames.Length % TranslateTimeline.ENTRIES != 0)
            {
                return false;
            }

            for (int index = 0; index < frames.Length; index++)
            {
                if (!IsFinite(frames[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
