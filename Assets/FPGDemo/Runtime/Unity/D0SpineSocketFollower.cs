using System;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Thin optional bridge from a Spine bone to an authored socket Transform.
    /// It updates after Spine has completed its world transform pass and does
    /// not participate in combat simulation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class D0SpineSocketFollower : MonoBehaviour
    {
        [SerializeField]
        private SkeletonAnimation skeletonAnimation;

        [SerializeField]
        private Transform target;

        [SerializeField]
        private string boneName;

        [SerializeField]
        private Vector3 positionOffset;

        [SerializeField]
        private Vector3 eulerOffset;

        [SerializeField]
        private bool copyRotation = true;

        private bool subscribed;

        public SkeletonAnimation SkeletonAnimation => skeletonAnimation;
        public Transform Target => target;
        public string BoneName => boneName;
        public bool CopyRotation => copyRotation;
        public string LastError { get; private set; } = string.Empty;

        public void Configure(
            SkeletonAnimation nextSkeletonAnimation,
            Transform nextTarget,
            string nextBoneName,
            Vector3 nextPositionOffset,
            Vector3 nextEulerOffset,
            bool nextCopyRotation = true)
        {
            Unsubscribe();
            skeletonAnimation = nextSkeletonAnimation;
            target = nextTarget;
            boneName = nextBoneName;
            positionOffset = nextPositionOffset;
            eulerOffset = nextEulerOffset;
            copyRotation = nextCopyRotation;
            LastError = string.Empty;
            Subscribe();
        }

        public bool TryConfigurePreservingCurrentPose(
            SkeletonAnimation nextSkeletonAnimation,
            Transform nextTarget,
            string nextBoneName,
            bool nextCopyRotation,
            out string error)
        {
            if (nextSkeletonAnimation == null || nextTarget == null
                || string.IsNullOrWhiteSpace(nextBoneName))
            {
                error = "Spine socket binding requires a SkeletonAnimation, target Transform and bone name.";
                LastError = error;
                return false;
            }

            nextSkeletonAnimation.Initialize(false);
            Skeleton skeleton = nextSkeletonAnimation.Skeleton;
            SkeletonData skeletonData = skeleton == null ? null : skeleton.Data;
            if (skeletonData == null || skeleton.FindBone(nextBoneName) == null)
            {
                error = $"Spine socket follower bone '{nextBoneName}' was not found.";
                LastError = error;
                return false;
            }

            try
            {
                var setupSkeleton = new Skeleton(skeletonData);
                setupSkeleton.SetToSetupPose();
                setupSkeleton.UpdateWorldTransform();
                Bone setupBone = setupSkeleton.FindBone(nextBoneName);
                if (setupBone == null
                    || !TryGetBoneWorldPose(
                        nextSkeletonAnimation.transform,
                        setupBone,
                        out Vector3 setupPosition,
                        out Quaternion setupRotation))
                {
                    error = $"Spine socket follower bone '{nextBoneName}' has an invalid setup pose.";
                    LastError = error;
                    return false;
                }

                Vector3 preservedPositionOffset =
                    Quaternion.Inverse(setupRotation)
                    * (nextTarget.position - setupPosition);
                Quaternion preservedRotationOffset =
                    Quaternion.Inverse(setupRotation) * nextTarget.rotation;
                if (!IsFinite(preservedPositionOffset)
                    || !IsFinite(preservedRotationOffset))
                {
                    error = "Spine socket follower produced a non-finite authored offset.";
                    LastError = error;
                    return false;
                }

                Configure(
                    nextSkeletonAnimation,
                    nextTarget,
                    nextBoneName,
                    preservedPositionOffset,
                    preservedRotationOffset.eulerAngles,
                    nextCopyRotation);
                return TryRefresh(out error);
            }
            catch (Exception exception)
            {
                error = "Spine socket follower could not build its setup-pose binding: "
                    + exception.Message;
                LastError = error;
                return false;
            }
        }

        public bool TryValidate(out string error)
        {
            if (skeletonAnimation == null)
            {
                error = "Spine socket follower requires a SkeletonAnimation.";
                LastError = error;
                return false;
            }

            if (target == null)
            {
                error = "Spine socket follower requires a target Transform.";
                LastError = error;
                return false;
            }

            if (string.IsNullOrWhiteSpace(boneName))
            {
                error = "Spine socket follower requires a bone name.";
                LastError = error;
                return false;
            }

            if (skeletonAnimation.Skeleton == null)
            {
                error = "Spine socket follower SkeletonAnimation has not initialized its Skeleton.";
                LastError = error;
                return false;
            }

            if (skeletonAnimation.Skeleton.FindBone(boneName) == null)
            {
                error = $"Spine socket follower bone '{boneName}' was not found.";
                LastError = error;
                return false;
            }

            if (!IsFinite(positionOffset) || !IsFinite(eulerOffset))
            {
                error = "Spine socket follower offsets must be finite.";
                LastError = error;
                return false;
            }

            error = string.Empty;
            LastError = string.Empty;
            return true;
        }

        public bool TryRefresh(out string error)
        {
            if (!TryValidate(out error))
            {
                return false;
            }

            Bone bone = skeletonAnimation.Skeleton.FindBone(boneName);
            if (!TryGetBoneWorldPose(
                    skeletonAnimation.transform,
                    bone,
                    out Vector3 worldPosition,
                    out Quaternion boneRotation))
            {
                error = $"Spine socket follower bone '{boneName}' has an invalid world pose.";
                LastError = error;
                return false;
            }

            worldPosition += boneRotation * positionOffset;
            Quaternion worldRotation = copyRotation
                ? boneRotation * Quaternion.Euler(eulerOffset)
                : target.rotation;
            target.SetPositionAndRotation(worldPosition, worldRotation);
            error = string.Empty;
            LastError = string.Empty;
            return true;
        }

        private void OnEnable()
        {
            Subscribe();
            TryRefresh(out _);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (!subscribed && isActiveAndEnabled && skeletonAnimation != null)
            {
                skeletonAnimation.UpdateComplete += HandleUpdateComplete;
                skeletonAnimation.OnRebuild += HandleSkeletonRebuild;
                subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (subscribed && skeletonAnimation != null)
            {
                skeletonAnimation.UpdateComplete -= HandleUpdateComplete;
                skeletonAnimation.OnRebuild -= HandleSkeletonRebuild;
            }

            subscribed = false;
        }

        private void HandleUpdateComplete(ISkeletonAnimation animated)
        {
            TryRefresh(out _);
        }

        private void HandleSkeletonRebuild(SkeletonRenderer renderer)
        {
            TryRefresh(out _);
        }

        private static bool TryGetBoneWorldPose(
            Transform skeletonTransform,
            Bone bone,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = skeletonTransform.TransformPoint(
                new Vector3(bone.WorldX, bone.WorldY, 0f));

            Vector3 forward = skeletonTransform.TransformDirection(Vector3.forward);
            Vector3 right = skeletonTransform.TransformVector(
                new Vector3(bone.A, bone.C, 0f));
            if (forward.sqrMagnitude <= 0.00000001f
                || right.sqrMagnitude <= 0.00000001f)
            {
                rotation = Quaternion.identity;
                return false;
            }

            forward.Normalize();
            right -= Vector3.Dot(right, forward) * forward;
            if (right.sqrMagnitude <= 0.00000001f)
            {
                rotation = Quaternion.identity;
                return false;
            }

            right.Normalize();
            Vector3 up = Vector3.Cross(forward, right).normalized;
            rotation = Quaternion.LookRotation(forward, up);
            return IsFinite(position) && IsFinite(rotation);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y)
                && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
