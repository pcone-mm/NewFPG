using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Common authored root for every playable actor entity. The prefab owns
    /// its authored hierarchy; character/enemy definitions provide identity and
    /// combat data at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class D0ActorEntityView : MonoBehaviour
    {
        [Header("Authored entity branches")]
        [SerializeField]
        private Transform gameplayAnchor;

        [SerializeField]
        private Transform visualRoot;

        [SerializeField]
        private D0ActorSocketRegistry socketRegistry;

        [SerializeField]
        private Actor2DPresenter actorPresenter;

        [SerializeField]
        private SkeletonAnimation skeletonAnimation;

        [NonSerialized]
        private Transform[] authoredPoseTransforms = Array.Empty<Transform>();

        [NonSerialized]
        private Vector3[] authoredLocalPositions = Array.Empty<Vector3>();

        [NonSerialized]
        private Quaternion[] authoredLocalRotations = Array.Empty<Quaternion>();

        [NonSerialized]
        private Vector3[] authoredLocalScales = Array.Empty<Vector3>();

        [NonSerialized]
        private D0SpineSocketFollower[] runtimeSocketFollowers =
            Array.Empty<D0SpineSocketFollower>();

        public Transform GameplayAnchor => gameplayAnchor;
        public Transform VisualRoot => visualRoot;
        public D0ActorSocketRegistry SocketRegistry => ResolveSocketRegistry();
        public Actor2DPresenter ActorPresenter => ResolveActorPresenter();
        public Actor2DPresenter Actor2DPresenter => ActorPresenter;
        public SkeletonAnimation SkeletonAnimation => ResolveSkeletonAnimation();

        /// <summary>
        /// Validates only the common prefab contract. Simulation identity and
        /// combat values remain outside the entity view.
        /// </summary>
        public virtual bool TryValidate(out string error)
        {
            if (gameplayAnchor == null)
            {
                error = "Actor entity requires a GameplayAnchor.";
                return false;
            }

            if (visualRoot == null)
            {
                error = "Actor entity requires a VisualRoot.";
                return false;
            }

            if (!IsDescendantOrSelf(gameplayAnchor, transform)
                || gameplayAnchor == transform)
            {
                error = "Actor entity GameplayAnchor must be a child branch of the entity root.";
                return false;
            }

            if (!IsDescendantOrSelf(visualRoot, transform)
                || visualRoot == transform)
            {
                error = "Actor entity VisualRoot must be a child branch of the entity root.";
                return false;
            }

            if (gameplayAnchor == visualRoot
                || gameplayAnchor.IsChildOf(visualRoot)
                || visualRoot.IsChildOf(gameplayAnchor))
            {
                error = "Actor entity GameplayAnchor and VisualRoot must be independent branches.";
                return false;
            }

            if (!IsFinite(gameplayAnchor.localPosition)
                || !IsFinite(gameplayAnchor.localRotation)
                || !IsFinite(gameplayAnchor.localScale))
            {
                error = "Actor entity GameplayAnchor local pose must be finite.";
                return false;
            }

            if (SocketRegistry == null)
            {
                error = "Actor entity requires a D0ActorSocketRegistry.";
                return false;
            }

            if (!SocketRegistry.TryValidate(out error))
            {
                return false;
            }

            if (ActorPresenter == null)
            {
                error = "Actor entity requires an Actor2DPresenter.";
                return false;
            }

            if (SkeletonAnimation == null)
            {
                error = "Actor entity requires a SkeletonAnimation below VisualRoot.";
                return false;
            }

            if (!SkeletonAnimation.transform.IsChildOf(visualRoot)
                && SkeletonAnimation.transform != visualRoot)
            {
                error = "Actor entity SkeletonAnimation must be attached below VisualRoot.";
                return false;
            }

            if (!TryValidateSpineSocketBindings(out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryResolveSocket(string socketId, out Transform anchor)
        {
            D0ActorSocketRegistry registry = SocketRegistry;
            if (registry != null)
            {
                return registry.TryResolve(socketId, out anchor);
            }

            anchor = null;
            return false;
        }

        public bool TryBindSpineSocketFollowers(out string error)
        {
            D0ActorSocketRegistry registry = SocketRegistry;
            SkeletonAnimation spine = SkeletonAnimation;
            if (registry == null || spine == null)
            {
                error = "Actor Spine socket binding requires a registry and SkeletonAnimation.";
                return false;
            }

            var followers = new List<D0SpineSocketFollower>();
            IReadOnlyList<D0ActorSocketBinding> bindings = registry.Bindings;
            for (int index = 0; index < bindings.Count; index++)
            {
                D0ActorSocketBinding binding = bindings[index];
                if (binding == null || !binding.FollowsSpineBone)
                {
                    continue;
                }

                D0SpineSocketFollower follower =
                    binding.Anchor.GetComponent<D0SpineSocketFollower>();
                if (follower == null)
                {
                    follower = binding.Anchor.gameObject
                        .AddComponent<D0SpineSocketFollower>();
                }

                if (!follower.TryConfigurePreservingCurrentPose(
                        spine,
                        binding.Anchor,
                        binding.BoneName,
                        true,
                        out error))
                {
                    error = $"Actor socket '{binding.SocketId}' could not bind to Spine bone '{binding.BoneName}': {error}";
                    return false;
                }

                followers.Add(follower);
            }

            runtimeSocketFollowers = followers.ToArray();
            error = string.Empty;
            return true;
        }

        public bool TryRefreshSpineSocketFollowers(out string error)
        {
            D0SpineSocketFollower[] followers = runtimeSocketFollowers;
            for (int index = 0; index < followers.Length; index++)
            {
                D0SpineSocketFollower follower = followers[index];
                if (follower == null)
                {
                    error = "Actor Spine socket follower is unavailable.";
                    return false;
                }

                if (!follower.TryRefresh(out error))
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Captures the complete prefab-authored Transform hierarchy before
        /// runtime placement or animation bridges can mutate local poses.
        /// </summary>
        public void CaptureAuthoredLocalPose()
        {
            authoredPoseTransforms = GetComponentsInChildren<Transform>(true);
            int count = authoredPoseTransforms.Length;
            authoredLocalPositions = new Vector3[count];
            authoredLocalRotations = new Quaternion[count];
            authoredLocalScales = new Vector3[count];
            for (int index = 0; index < count; index++)
            {
                Transform target = authoredPoseTransforms[index];
                authoredLocalPositions[index] = target.localPosition;
                authoredLocalRotations[index] = target.localRotation;
                authoredLocalScales[index] = target.localScale;
            }
        }

        /// <summary>
        /// Restores only Transforms captured from the authored entity. Runtime
        /// children created later are intentionally outside this contract.
        /// </summary>
        public bool RestoreAuthoredLocalPose()
        {
            int count = authoredPoseTransforms == null
                ? 0
                : authoredPoseTransforms.Length;
            if (count == 0
                || authoredLocalPositions == null
                || authoredLocalRotations == null
                || authoredLocalScales == null
                || authoredLocalPositions.Length != count
                || authoredLocalRotations.Length != count
                || authoredLocalScales.Length != count)
            {
                return false;
            }

            for (int index = 0; index < count; index++)
            {
                Transform target = authoredPoseTransforms[index];
                if (target == null)
                {
                    continue;
                }

                target.localPosition = authoredLocalPositions[index];
                target.localRotation = authoredLocalRotations[index];
                target.localScale = authoredLocalScales[index];
            }

            return true;
        }

        public bool HasCapturedAuthoredLocalPose =>
            authoredPoseTransforms != null && authoredPoseTransforms.Length > 0;

        protected Transform AuthoredGameplayAnchor => gameplayAnchor;
        protected Transform AuthoredVisualRoot => visualRoot;

        private D0ActorSocketRegistry ResolveSocketRegistry()
        {
            if (socketRegistry != null)
            {
                return socketRegistry;
            }

            return GetComponentInChildren<D0ActorSocketRegistry>(true);
        }

        private Actor2DPresenter ResolveActorPresenter()
        {
            if (actorPresenter != null)
            {
                return actorPresenter;
            }

            return GetComponentInChildren<Actor2DPresenter>(true);
        }

        private SkeletonAnimation ResolveSkeletonAnimation()
        {
            if (skeletonAnimation != null)
            {
                return skeletonAnimation;
            }

            return visualRoot == null
                ? null
                : visualRoot.GetComponentInChildren<SkeletonAnimation>(true);
        }

        private bool TryValidateSpineSocketBindings(out string error)
        {
            IReadOnlyList<D0ActorSocketBinding> bindings = SocketRegistry.Bindings;
            bool needsSpineBone = false;
            for (int index = 0; index < bindings.Count; index++)
            {
                if (bindings[index] != null && bindings[index].FollowsSpineBone)
                {
                    needsSpineBone = true;
                    break;
                }
            }

            if (!needsSpineBone)
            {
                error = string.Empty;
                return true;
            }

            SkeletonDataAsset dataAsset = SkeletonAnimation.SkeletonDataAsset;
            SkeletonData skeletonData = dataAsset == null
                ? null
                : dataAsset.GetSkeletonData(false);
            if (skeletonData == null)
            {
                error = "Actor Spine socket binding requires readable SkeletonData.";
                return false;
            }

            for (int index = 0; index < bindings.Count; index++)
            {
                D0ActorSocketBinding binding = bindings[index];
                if (binding != null && binding.FollowsSpineBone
                    && skeletonData.FindBone(binding.BoneName) == null)
                {
                    error = $"Actor socket '{binding.SocketId}' cannot find Spine bone '{binding.BoneName}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool IsDescendantOrSelf(Transform candidate, Transform root)
        {
            return candidate != null && root != null
                && (candidate == root || candidate.IsChildOf(root));
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
