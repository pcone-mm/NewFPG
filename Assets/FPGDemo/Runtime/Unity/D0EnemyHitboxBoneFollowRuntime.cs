using System;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace FPG.Demo.Unity
{
    internal readonly struct D0EnemyHitboxBoneFollowTarget
    {
        public D0EnemyHitboxBoneFollowTarget(
            Transform target,
            string boneName,
            bool followBoneRotation,
            Vector3 positionOffset,
            Quaternion rotationOffset)
        {
            Target = target;
            BoneName = boneName;
            FollowBoneRotation = followBoneRotation;
            PositionOffset = positionOffset;
            RotationOffset = rotationOffset;
        }

        public Transform Target { get; }
        public string BoneName { get; }
        public bool FollowBoneRotation { get; }
        public Vector3 PositionOffset { get; }
        public Quaternion RotationOffset { get; }
    }

    /// <summary>
    /// Runtime-only bridge for the small set of hit parts that explicitly opt
    /// into Spine bone following. Authored offsets are resolved against the
    /// Spine setup pose; no follower components or Spine objects are serialized
    /// into the prefab.
    /// </summary>
    internal sealed class D0EnemyHitboxBoneFollowRuntime : IDisposable
    {
        private struct Binding
        {
            public Transform Target;
            public Transform AuthoredParent;
            public string BoneName;
            public bool FollowBoneRotation;
            public Bone Bone;
            public Vector3 PositionOffset;
            public Quaternion RotationOffset;
            public Vector3 AdditionalPositionOffset;
            public Quaternion AdditionalRotationOffset;
            public bool HasAdditionalRotationOffset;
            public Vector3 AuthoredLocalPosition;
            public Quaternion AuthoredLocalRotation;
            public Vector3 AuthoredWorldPosition;
            public Quaternion AuthoredWorldRotation;
        }

        private readonly SkeletonAnimation skeletonAnimation;
        private readonly Binding[] bindings;
        private Skeleton cachedSkeleton;
        private bool active;
        private bool disposed;
        private bool boneResolutionFailed;

        private D0EnemyHitboxBoneFollowRuntime(
            SkeletonAnimation skeletonAnimation,
            Binding[] bindings)
        {
            this.skeletonAnimation = skeletonAnimation;
            this.bindings = bindings;
        }

        public static bool TryCreate(
            SkeletonAnimation skeletonAnimation,
            D0EnemyHitboxBoneFollowTarget[] targets,
            out D0EnemyHitboxBoneFollowRuntime runtime,
            out string error)
        {
            runtime = null;
            if (targets == null || targets.Length == 0)
            {
                error = string.Empty;
                return true;
            }

            if (skeletonAnimation == null)
            {
                error = "Enemy hitbox bone following requires a SkeletonAnimation.";
                return false;
            }

            skeletonAnimation.Initialize(false);
            Skeleton skeleton = skeletonAnimation.Skeleton;
            if (skeleton == null)
            {
                error = "Enemy hitbox bone following could not initialize the Spine Skeleton.";
                return false;
            }

            skeleton.UpdateWorldTransform();
            SkeletonData skeletonData = skeleton.Data;
            if (skeletonData == null)
            {
                error = "Enemy hitbox bone following could not load Spine SkeletonData.";
                return false;
            }

            Skeleton setupSkeleton;
            try
            {
                setupSkeleton = new Skeleton(skeletonData);
                setupSkeleton.SetToSetupPose();
                setupSkeleton.UpdateWorldTransform();
            }
            catch (Exception exception)
            {
                error = "Enemy hitbox bone following could not build its setup-pose reference: "
                    + exception.Message;
                return false;
            }

            var bindings = new Binding[targets.Length];
            for (int index = 0; index < targets.Length; index++)
            {
                D0EnemyHitboxBoneFollowTarget target = targets[index];
                if (target.Target == null || string.IsNullOrWhiteSpace(target.BoneName))
                {
                    error = $"Enemy hitbox bone-follow target {index} is incomplete.";
                    return false;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (targets[previous].Target == target.Target)
                    {
                        error = $"Enemy hitbox bone-follow target {index} duplicates another target Transform.";
                        return false;
                    }
                }

                Bone bone = skeleton.FindBone(target.BoneName);
                if (bone == null)
                {
                    error = $"Enemy hitbox bone '{target.BoneName}' was not found at runtime.";
                    return false;
                }

                Bone setupBone = setupSkeleton.FindBone(target.BoneName);
                if (setupBone == null)
                {
                    error = $"Enemy hitbox bone '{target.BoneName}' was not found in the setup-pose reference.";
                    return false;
                }

                if (!TryGetBoneWorldPose(
                        skeletonAnimation.transform,
                        setupBone,
                        out Vector3 bonePosition,
                        out Quaternion boneRotation))
                {
                    error = $"Enemy hitbox bone '{target.BoneName}' has a degenerate setup pose.";
                    return false;
                }

                Vector3 positionOffset = Quaternion.Inverse(boneRotation)
                    * (target.Target.position - bonePosition);
                Quaternion rotationOffset = Quaternion.Inverse(boneRotation)
                    * target.Target.rotation;
                if (!IsFinite(positionOffset) || !IsFinite(rotationOffset))
                {
                    error = $"Enemy hitbox bone-follow target {index} produced a non-finite authored offset.";
                    return false;
                }

                if (!IsFinite(target.PositionOffset)
                    || !IsFinite(target.RotationOffset))
                {
                    error = $"Enemy hitbox bone-follow target {index} has a non-finite configured offset.";
                    return false;
                }

                bindings[index] = new Binding
                {
                    Target = target.Target,
                    AuthoredParent = target.Target.parent,
                    BoneName = target.BoneName,
                    FollowBoneRotation = target.FollowBoneRotation,
                    Bone = bone,
                    PositionOffset = positionOffset,
                    RotationOffset = rotationOffset,
                    AdditionalPositionOffset = target.PositionOffset,
                    AdditionalRotationOffset = target.RotationOffset,
                    HasAdditionalRotationOffset = Mathf.Abs(Quaternion.Dot(
                        target.RotationOffset,
                        Quaternion.identity)) < 0.999999f,
                    AuthoredLocalPosition = target.Target.localPosition,
                    AuthoredLocalRotation = target.Target.localRotation,
                    AuthoredWorldPosition = target.Target.position,
                    AuthoredWorldRotation = target.Target.rotation
                };
            }

            runtime = new D0EnemyHitboxBoneFollowRuntime(
                skeletonAnimation,
                bindings)
            {
                cachedSkeleton = skeleton
            };
            error = string.Empty;
            return true;
        }

        public void Activate()
        {
            if (active || disposed)
            {
                return;
            }

            skeletonAnimation.OnRebuild += HandleSkeletonRebuild;
            skeletonAnimation.UpdateComplete += HandleUpdateComplete;
            active = true;
            Refresh();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            if (active)
            {
                skeletonAnimation.UpdateComplete -= HandleUpdateComplete;
                skeletonAnimation.OnRebuild -= HandleSkeletonRebuild;
            }

            active = false;
            disposed = true;
            RestoreAuthoredPoses();
        }

        private void RestoreAuthoredPoses()
        {
            for (int index = 0; index < bindings.Length; index++)
            {
                Binding binding = bindings[index];
                if (binding.Target == null)
                {
                    continue;
                }

                if (binding.Target.parent == binding.AuthoredParent)
                {
                    binding.Target.SetLocalPositionAndRotation(
                        binding.AuthoredLocalPosition,
                        binding.AuthoredLocalRotation);
                }
                else
                {
                    binding.Target.SetPositionAndRotation(
                        binding.AuthoredWorldPosition,
                        binding.AuthoredWorldRotation);
                }
            }
        }

        private void HandleSkeletonRebuild(SkeletonRenderer renderer)
        {
            boneResolutionFailed = false;
            if (TryResolveBones(true))
            {
                Refresh();
            }
        }

        private void HandleUpdateComplete(ISkeletonAnimation animated)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (boneResolutionFailed)
            {
                return;
            }

            if (cachedSkeleton != skeletonAnimation.Skeleton && !TryResolveBones(true))
            {
                return;
            }

            Transform skeletonTransform = skeletonAnimation.transform;
            for (int index = 0; index < bindings.Length; index++)
            {
                Binding binding = bindings[index];
                if (binding.Target == null || binding.Bone == null
                    || !TryGetBoneWorldPose(
                        skeletonTransform,
                        binding.Bone,
                        out Vector3 bonePosition,
                        out Quaternion boneRotation))
                {
                    continue;
                }

                Vector3 targetPosition = bonePosition
                    + boneRotation * (
                        binding.PositionOffset
                        + binding.AdditionalPositionOffset);
                if (binding.FollowBoneRotation)
                {
                    binding.Target.SetPositionAndRotation(
                        targetPosition,
                        boneRotation
                        * binding.RotationOffset
                        * binding.AdditionalRotationOffset);
                }
                else if (binding.HasAdditionalRotationOffset)
                {
                    binding.Target.SetPositionAndRotation(
                        targetPosition,
                        binding.AuthoredWorldRotation
                        * binding.AdditionalRotationOffset);
                }
                else
                {
                    binding.Target.position = targetPosition;
                }
            }
        }

        private bool TryResolveBones(bool reportFailure)
        {
            Skeleton skeleton = skeletonAnimation.Skeleton;
            if (skeleton == null)
            {
                cachedSkeleton = null;
                boneResolutionFailed = true;
                if (reportFailure)
                {
                    Debug.LogError(
                        "Enemy hitbox bone following stopped because the rebuilt Spine Skeleton is unavailable.",
                        skeletonAnimation);
                }

                return false;
            }

            for (int index = 0; index < bindings.Length; index++)
            {
                Bone bone = skeleton.FindBone(bindings[index].BoneName);
                if (bone == null)
                {
                    cachedSkeleton = null;
                    boneResolutionFailed = true;
                    if (reportFailure)
                    {
                        Debug.LogError(
                            $"Enemy hitbox bone following stopped because rebuilt Skeleton is missing bone '{bindings[index].BoneName}'.",
                            skeletonAnimation);
                    }

                    return false;
                }

                Binding binding = bindings[index];
                binding.Bone = bone;
                bindings[index] = binding;
            }

            cachedSkeleton = skeleton;
            boneResolutionFailed = false;
            return true;
        }

        private static bool TryGetBoneWorldPose(
            Transform skeletonTransform,
            Bone bone,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = skeletonTransform.TransformPoint(
                new Vector3(bone.WorldX, bone.WorldY, 0f));

            // Derive the rotation from the transformed Spine X axis. This
            // preserves X/Y flips without trying to copy bone scale or shear.
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
