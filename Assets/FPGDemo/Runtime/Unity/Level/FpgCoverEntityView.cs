using System;
using System.Collections.Generic;
using FPG.Demo.Run;
using UnityEngine;
using UnityEngine.Serialization;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgCoverEntityView : MonoBehaviour
    {
        private const string ShadowCasterProxyName = "__ShadowCasterProxy";

        [SerializeField]
        private GameObject intactRoot;

        [SerializeField]
        private GameObject destroyedRoot;

        [FormerlySerializedAs("blockingColliders")]
        [SerializeField, HideInInspector]
        private Collider[] legacyBlockingColliders = Array.Empty<Collider>();

        [NonSerialized]
        private Collider[] resolvedBlockingColliders = Array.Empty<Collider>();

        [NonSerialized]
        private bool blockingCollidersResolved;

        public bool IsDestroyed { get; private set; }
        public int BlockingColliderCount
        {
            get
            {
                EnsureBlockingCollidersResolved();
                return resolvedBlockingColliders.Length;
            }
        }

        public bool TryGetBlockingCollider(int index, out Collider collider)
        {
            EnsureBlockingCollidersResolved();
            if (index >= 0 && index < resolvedBlockingColliders.Length)
            {
                collider = resolvedBlockingColliders[index];
                return collider != null;
            }

            collider = null;
            return false;
        }

        public void ApplySnapshot(in FpgCoverSnapshot snapshot)
        {
            if (!snapshot.IsValid)
            {
                throw new ArgumentException(
                    "Cover view requires a valid snapshot.",
                    nameof(snapshot));
            }

            ApplyDestroyed(snapshot.IsDestroyed);
        }

        public void ApplyDestroyed(bool destroyed)
        {
            IsDestroyed = destroyed;
            if (intactRoot != null)
            {
                intactRoot.SetActive(!destroyed);
            }

            if (destroyedRoot != null)
            {
                destroyedRoot.SetActive(destroyed);
            }

            EnsureBlockingCollidersResolved();
            for (int index = 0; index < resolvedBlockingColliders.Length; index++)
            {
                if (resolvedBlockingColliders[index] != null)
                {
                    resolvedBlockingColliders[index].enabled = !destroyed;
                }
            }
        }

        public bool TryValidate(out string error)
        {
            if (intactRoot == null || destroyedRoot == null)
            {
                error = "Cover view requires intact and destroyed visual roots.";
                return false;
            }

            if (intactRoot == destroyedRoot)
            {
                error = "Cover view requires distinct intact and destroyed visual roots.";
                return false;
            }

            if (!IsVisualRootOwnedByView(intactRoot.transform))
            {
                error = "Cover view intact visual root must belong to the cover Prefab.";
                return false;
            }

            if (!IsVisualRootOwnedByView(destroyedRoot.transform))
            {
                error = "Cover view destroyed visual root must belong to the cover Prefab.";
                return false;
            }

            MeshFilter[] blockingMeshes = ResolveBlockingSourceMeshes();
            if (blockingMeshes.Length == 0)
            {
                error =
                    "Cover view requires at least one intact visual mesh or shadow-proxy mesh.";
                return false;
            }

            bool usesShadowProxy = IsShadowProxy(blockingMeshes[0]);
            for (int index = 0; index < blockingMeshes.Length; index++)
            {
                MeshFilter meshFilter = blockingMeshes[index];
                if (meshFilter.sharedMesh == null)
                {
                    string source = usesShadowProxy
                        ? "shadow proxy"
                        : "visual mesh";
                    error =
                        $"Cover view {source} '{meshFilter.name}' requires a non-empty shared Mesh.";
                    return false;
                }

                MeshCollider meshCollider = meshFilter.GetComponent<MeshCollider>();

                if (meshCollider == null)
                {
                    string source = usesShadowProxy
                        ? "shadow proxy"
                        : "visual mesh";
                    error =
                        $"Cover view {source} '{meshFilter.name}' requires a matching MeshCollider.";
                    return false;
                }

                if (meshCollider.isTrigger)
                {
                    error = $"Cover view blocker entry {index} must not be a Trigger.";
                    return false;
                }

                if (meshCollider.sharedMesh == null
                    || meshCollider.sharedMesh != meshFilter.sharedMesh)
                {
                    error =
                        $"Cover view blocker entry {index} must use the MeshFilter mesh on the same object.";
                    return false;
                }

                if (meshCollider.convex)
                {
                    error =
                        $"Cover view blocker entry {index} must preserve the authored mesh instead of using a convex hull.";
                    return false;
                }
            }

            ResolveBlockingColliders();
            Collider[] authoredColliders = GetComponentsInChildren<Collider>(true);
            if (authoredColliders.Length != blockingMeshes.Length
                || resolvedBlockingColliders.Length != blockingMeshes.Length)
            {
                error = usesShadowProxy
                    ? "Cover view blockers must match every shadow-proxy mesh and no additional Collider."
                    : "Cover view blockers must match every renderable intact visual mesh and no additional Collider.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool IsOwnedByView(Transform candidate)
        {
            return candidate != null && candidate.IsChildOf(transform);
        }

        private bool IsVisualRootOwnedByView(Transform candidate)
        {
            return candidate != transform && IsOwnedByView(candidate);
        }

        private void EnsureBlockingCollidersResolved()
        {
            if (!blockingCollidersResolved)
            {
                ResolveBlockingColliders();
            }
        }

        private void ResolveBlockingColliders()
        {
            MeshFilter[] blockingMeshes = ResolveBlockingSourceMeshes();
            List<Collider> colliders = new List<Collider>(blockingMeshes.Length);
            for (int index = 0; index < blockingMeshes.Length; index++)
            {
                MeshCollider collider =
                    blockingMeshes[index].GetComponent<MeshCollider>();
                if (collider != null)
                {
                    colliders.Add(collider);
                }
            }

            resolvedBlockingColliders = colliders.ToArray();
            blockingCollidersResolved = true;
        }

        private MeshFilter[] ResolveBlockingSourceMeshes()
        {
            if (intactRoot == null)
            {
                return Array.Empty<MeshFilter>();
            }

            MeshFilter[] candidates =
                intactRoot.GetComponentsInChildren<MeshFilter>(true);
            bool hasShadowProxy = Array.Exists(candidates, IsShadowProxy);
            List<MeshFilter> resolved = new List<MeshFilter>(candidates.Length);
            for (int index = 0; index < candidates.Length; index++)
            {
                MeshFilter candidate = candidates[index];
                if (candidate == null)
                {
                    continue;
                }

                if (hasShadowProxy
                    ? IsShadowProxy(candidate)
                    : candidate.GetComponent<MeshRenderer>() != null)
                {
                    resolved.Add(candidate);
                }
            }

            resolved.Sort(CompareMeshHierarchyOrder);
            return resolved.ToArray();
        }

        private static bool IsShadowProxy(MeshFilter meshFilter)
        {
            return meshFilter != null
                && meshFilter.name == ShadowCasterProxyName;
        }

        private static int CompareMeshHierarchyOrder(
            MeshFilter left,
            MeshFilter right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return string.CompareOrdinal(
                BuildHierarchySortKey(left.transform),
                BuildHierarchySortKey(right.transform));
        }

        private static string BuildHierarchySortKey(Transform candidate)
        {
            string key = string.Empty;
            while (candidate != null)
            {
                key = candidate.GetSiblingIndex().ToString("D6") + "/" + key;
                candidate = candidate.parent;
            }

            return key;
        }

        private void Awake()
        {
            ResolveBlockingColliders();
            ApplyDestroyed(false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            blockingCollidersResolved = false;
        }
#endif
    }
}
