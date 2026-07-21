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
            Quaternion boneRotation = skeletonAnimation.transform.rotation
                * Quaternion.Euler(0f, 0f, bone.WorldRotationX);
            Vector3 worldPosition = skeletonAnimation.transform.TransformPoint(
                new Vector3(bone.WorldX, bone.WorldY, 0f));
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
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (skeletonAnimation != null)
            {
                skeletonAnimation.UpdateComplete += HandleUpdateComplete;
            }
        }

        private void Unsubscribe()
        {
            if (skeletonAnimation != null)
            {
                skeletonAnimation.UpdateComplete -= HandleUpdateComplete;
            }
        }

        private void HandleUpdateComplete(ISkeletonAnimation animated)
        {
            TryRefresh(out _);
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
