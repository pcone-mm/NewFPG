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
        private const string ProjectileBlockerProxyName =
            "__ProjectileBlockerProxy";
        private const float ProjectileBlockerForwardOffset = 0.75f;

        [Serializable]
        private sealed class HealthStage
        {
            [SerializeField, Range(1, 100)]
            private int minDurabilityPercentInclusive = 1;

            [SerializeField]
            private GameObject visualRoot;

            public int MinDurabilityPercentInclusive =>
                Mathf.Clamp(minDurabilityPercentInclusive, 1, 100);

            public bool HasValidThreshold =>
                minDurabilityPercentInclusive >= 1
                && minDurabilityPercentInclusive <= 100;

            public GameObject VisualRoot => visualRoot;
        }

        private readonly struct ResolvedHealthStage
        {
            public ResolvedHealthStage(
                int authoredIndex,
                int minDurabilityPercentInclusive,
                GameObject visualRoot)
            {
                AuthoredIndex = authoredIndex;
                MinDurabilityPercentInclusive =
                    minDurabilityPercentInclusive;
                VisualRoot = visualRoot;
            }

            public int AuthoredIndex { get; }
            public int MinDurabilityPercentInclusive { get; }
            public GameObject VisualRoot { get; }
        }

        private readonly struct ResolvedBlockingColliderGroup
        {
            public ResolvedBlockingColliderGroup(
                int healthStageIndex,
                Collider[] colliders)
            {
                HealthStageIndex = healthStageIndex;
                Colliders = colliders ?? Array.Empty<Collider>();
            }

            public int HealthStageIndex { get; }
            public Collider[] Colliders { get; }
        }

        [SerializeField]
        private GameObject intactRoot;

        [SerializeField]
        private GameObject destroyedRoot;

        [SerializeField]
        private HealthStage[] healthStages = Array.Empty<HealthStage>();

        [FormerlySerializedAs("blockingColliders")]
        [SerializeField, HideInInspector]
        private Collider[] legacyBlockingColliders = Array.Empty<Collider>();

        [NonSerialized]
        private Collider[] resolvedBlockingColliders = Array.Empty<Collider>();

        [NonSerialized]
        private ResolvedBlockingColliderGroup[] resolvedBlockingColliderGroups =
            Array.Empty<ResolvedBlockingColliderGroup>();

        [NonSerialized]
        private bool blockingCollidersResolved;

        public bool IsDestroyed { get; private set; }
        public int ActiveHealthStageIndex { get; private set; } = -1;
        public int HealthStageCount => ResolveHealthStages().Length;
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

            if (snapshot.IsDestroyed)
            {
                ApplyDestroyed(true);
            }
            else
            {
                int healthPercent = Mathf.Clamp(
                    Mathf.CeilToInt(
                        snapshot.Durability * 100f
                        / snapshot.MaxDurability),
                    1,
                    100);
                ApplyHealthPercent(healthPercent);
            }
        }

        public void ApplyDestroyed(bool destroyed)
        {
            if (!destroyed)
            {
                ApplyHealthPercent(100);
                return;
            }

            IsDestroyed = true;
            ActiveHealthStageIndex = -1;
            if (intactRoot != null)
            {
                intactRoot.SetActive(false);
            }

            if (destroyedRoot != null)
            {
                destroyedRoot.SetActive(true);
            }

            SetAuthoredStageVisualsActive(-1);
            EnsureBlockingCollidersResolved();
            SetAllBlockingCollidersEnabled(false);
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

            if (!TryResolveValidHealthStages(
                    out ResolvedHealthStage[] stages,
                    out error))
            {
                return false;
            }

            List<MeshFilter> blockingMeshes =
                new List<MeshFilter>(stages.Length);
            for (int stageIndex = 0; stageIndex < stages.Length; stageIndex++)
            {
                MeshFilter[] stageMeshes = ResolveBlockingSourceMeshes(
                    stages[stageIndex].VisualRoot);
                if (stageMeshes.Length == 0)
                {
                    error =
                        $"Cover view health stage {stageIndex} requires at least one visual mesh or shadow-proxy mesh.";
                    return false;
                }

                blockingMeshes.AddRange(stageMeshes);
            }

            if (blockingMeshes.Count == 0)
            {
                error =
                    "Cover view requires at least one health-stage visual mesh or shadow-proxy mesh.";
                return false;
            }

            for (int index = 0; index < blockingMeshes.Count; index++)
            {
                MeshFilter meshFilter = blockingMeshes[index];
                if (meshFilter.sharedMesh == null)
                {
                    string source = IsShadowProxy(meshFilter)
                        ? "shadow proxy"
                        : "visual mesh";
                    error =
                        $"Cover view {source} '{meshFilter.name}' requires a non-empty shared Mesh.";
                    return false;
                }

                MeshCollider meshCollider = meshFilter.GetComponent<MeshCollider>();

                if (meshCollider == null)
                {
                    string source = IsShadowProxy(meshFilter)
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

            Collider[] expectedAuthoredColliders =
                new Collider[blockingMeshes.Count];
            for (int index = 0; index < blockingMeshes.Count; index++)
            {
                expectedAuthoredColliders[index] =
                    blockingMeshes[index].GetComponent<MeshCollider>();
            }

            Collider[] allColliders = GetComponentsInChildren<Collider>(true);
            List<Collider> authoredColliders =
                new List<Collider>(allColliders.Length);
            for (int index = 0; index < allColliders.Length; index++)
            {
                if (!IsRuntimeProjectileBlocker(allColliders[index]))
                {
                    authoredColliders.Add(allColliders[index]);
                }
            }

            if (!ContainsSameColliders(
                    authoredColliders.ToArray(),
                    expectedAuthoredColliders))
            {
                error =
                    "Cover view blockers must match every health-stage source mesh and no additional Collider.";
                return false;
            }

            ResolveBlockingColliders();
            if (resolvedBlockingColliders.Length != blockingMeshes.Count)
            {
                error =
                    "Cover view could not resolve one projectile blocker per health-stage source mesh.";
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
            ResolvedHealthStage[] stages = ResolveHealthStages();
            List<Collider> colliders = new List<Collider>();
            List<ResolvedBlockingColliderGroup> groups =
                new List<ResolvedBlockingColliderGroup>(stages.Length);
            for (int stageIndex = 0; stageIndex < stages.Length; stageIndex++)
            {
                MeshFilter[] blockingMeshes = ResolveBlockingSourceMeshes(
                    stages[stageIndex].VisualRoot);
                List<Collider> stageColliders =
                    new List<Collider>(blockingMeshes.Length);
                for (int meshIndex = 0;
                    meshIndex < blockingMeshes.Length;
                    meshIndex++)
                {
                    MeshCollider collider =
                        blockingMeshes[meshIndex].GetComponent<MeshCollider>();
                    if (collider != null)
                    {
                        Collider resolvedCollider =
                            ResolveProjectileBlockingCollider(collider);
                        if (resolvedCollider != null)
                        {
                            colliders.Add(resolvedCollider);
                            stageColliders.Add(resolvedCollider);
                        }
                    }
                }

                groups.Add(new ResolvedBlockingColliderGroup(
                    stageIndex,
                    stageColliders.ToArray()));
            }

            resolvedBlockingColliders = colliders.ToArray();
            resolvedBlockingColliderGroups = groups.ToArray();
            blockingCollidersResolved = true;
        }

        private Collider ResolveProjectileBlockingCollider(
            MeshCollider authoredCollider)
        {
            if (authoredCollider == null
                || !Application.isPlaying
                || !gameObject.scene.IsValid()
                || !gameObject.scene.isLoaded)
            {
                return authoredCollider;
            }

            Transform source = authoredCollider.transform;
            Transform proxyTransform = null;
            for (int childIndex = 0;
                childIndex < source.childCount;
                childIndex++)
            {
                Transform child = source.GetChild(childIndex);
                if (child.name == ProjectileBlockerProxyName)
                {
                    proxyTransform = child;
                    break;
                }
            }

            bool created = proxyTransform == null;
            if (created)
            {
                GameObject proxy = new GameObject(ProjectileBlockerProxyName);
                proxyTransform = proxy.transform;
                proxyTransform.SetParent(source, false);
            }

            GameObject proxyObject = proxyTransform.gameObject;
            proxyObject.layer = authoredCollider.gameObject.layer;
            proxyObject.SetActive(true);
            proxyTransform.localPosition = source.InverseTransformVector(
                Vector3.forward * ProjectileBlockerForwardOffset);
            proxyTransform.localRotation = Quaternion.identity;
            proxyTransform.localScale = Vector3.one;

            MeshCollider runtimeCollider =
                proxyObject.GetComponent<MeshCollider>();
            if (runtimeCollider == null)
            {
                runtimeCollider = proxyObject.AddComponent<MeshCollider>();
            }

            runtimeCollider.sharedMesh = authoredCollider.sharedMesh;
            runtimeCollider.sharedMaterial = authoredCollider.sharedMaterial;
            runtimeCollider.convex = authoredCollider.convex;
            runtimeCollider.cookingOptions = authoredCollider.cookingOptions;
            runtimeCollider.isTrigger = authoredCollider.isTrigger;
            runtimeCollider.contactOffset = authoredCollider.contactOffset;
            runtimeCollider.includeLayers = authoredCollider.includeLayers;
            runtimeCollider.excludeLayers = authoredCollider.excludeLayers;
            runtimeCollider.layerOverridePriority =
                authoredCollider.layerOverridePriority;
            runtimeCollider.providesContacts = authoredCollider.providesContacts;
            if (created)
            {
                runtimeCollider.enabled = authoredCollider.enabled;
            }

            authoredCollider.enabled = false;
            return runtimeCollider;
        }

        private MeshFilter[] ResolveBlockingSourceMeshes(GameObject visualRoot)
        {
            if (visualRoot == null)
            {
                return Array.Empty<MeshFilter>();
            }

            MeshFilter[] candidates =
                visualRoot.GetComponentsInChildren<MeshFilter>(true);
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

        private void ApplyHealthPercent(int healthPercent)
        {
            ResolvedHealthStage[] stages = ResolveHealthStages();
            int activeStageIndex = SelectHealthStageIndex(stages, healthPercent);

            IsDestroyed = false;
            ActiveHealthStageIndex = activeStageIndex;
            if (intactRoot != null)
            {
                intactRoot.SetActive(true);
            }

            if (destroyedRoot != null)
            {
                destroyedRoot.SetActive(false);
            }

            SetAuthoredStageVisualsActive(activeStageIndex);
            EnsureBlockingCollidersResolved();
            SetActiveBlockingColliderGroup(activeStageIndex);
        }

        private int SelectHealthStageIndex(
            ResolvedHealthStage[] stages,
            int healthPercent)
        {
            if (stages == null || stages.Length == 0)
            {
                return -1;
            }

            int selectedIndex = -1;
            int selectedThreshold = int.MinValue;
            int lowestIndex = 0;
            int lowestThreshold = int.MaxValue;
            for (int index = 0; index < stages.Length; index++)
            {
                int threshold = stages[index].MinDurabilityPercentInclusive;
                if (threshold < lowestThreshold)
                {
                    lowestThreshold = threshold;
                    lowestIndex = index;
                }

                if (healthPercent >= threshold
                    && threshold > selectedThreshold)
                {
                    selectedThreshold = threshold;
                    selectedIndex = index;
                }
            }

            return selectedIndex >= 0 ? selectedIndex : lowestIndex;
        }

        private void SetAuthoredStageVisualsActive(int activeStageIndex)
        {
            if (!HasAuthoredHealthStages())
            {
                return;
            }

            ResolvedHealthStage[] stages = ResolveHealthStages();
            for (int index = 0; index < stages.Length; index++)
            {
                GameObject root = stages[index].VisualRoot;
                if (root != null)
                {
                    root.SetActive(index == activeStageIndex);
                }
            }
        }

        private void SetActiveBlockingColliderGroup(int activeStageIndex)
        {
            for (int groupIndex = 0;
                groupIndex < resolvedBlockingColliderGroups.Length;
                groupIndex++)
            {
                ResolvedBlockingColliderGroup group =
                    resolvedBlockingColliderGroups[groupIndex];
                bool enabled = group.HealthStageIndex == activeStageIndex;
                Collider[] colliders = group.Colliders;
                for (int colliderIndex = 0;
                    colliderIndex < colliders.Length;
                    colliderIndex++)
                {
                    if (colliders[colliderIndex] != null)
                    {
                        colliders[colliderIndex].enabled = enabled;
                    }
                }
            }
        }

        private void SetAllBlockingCollidersEnabled(bool enabled)
        {
            for (int index = 0;
                index < resolvedBlockingColliders.Length;
                index++)
            {
                if (resolvedBlockingColliders[index] != null)
                {
                    resolvedBlockingColliders[index].enabled = enabled;
                }
            }
        }

        private bool TryResolveValidHealthStages(
            out ResolvedHealthStage[] stages,
            out string error)
        {
            stages = ResolveHealthStages();
            if (!HasAuthoredHealthStages())
            {
                error = string.Empty;
                return stages.Length > 0;
            }

            if (stages.Length == 0)
            {
                error = "Cover view requires at least one health stage.";
                return false;
            }

            HashSet<int> thresholds = new HashSet<int>();
            bool hasLowestStage = false;
            for (int index = 0; index < healthStages.Length; index++)
            {
                HealthStage stage = healthStages[index];
                if (stage == null)
                {
                    error =
                        $"Cover view health stage {index} is missing its serialized entry.";
                    return false;
                }

                if (!stage.HasValidThreshold)
                {
                    error =
                        $"Cover view health stage {index} threshold must be between 1 and 100.";
                    return false;
                }

                if (!thresholds.Add(stage.MinDurabilityPercentInclusive))
                {
                    error =
                        $"Cover view health stage threshold {stage.MinDurabilityPercentInclusive}% is duplicated.";
                    return false;
                }

                if (stage.MinDurabilityPercentInclusive == 1)
                {
                    hasLowestStage = true;
                }

                GameObject visualRoot = stage.VisualRoot;
                if (visualRoot == null)
                {
                    error =
                        $"Cover view health stage {index} requires a visual root.";
                    return false;
                }

                if (visualRoot == intactRoot || visualRoot == destroyedRoot)
                {
                    error =
                        $"Cover view health stage {index} must use a distinct child visual root.";
                    return false;
                }

                if (!IsVisualRootOwnedByView(visualRoot.transform)
                    || !visualRoot.transform.IsChildOf(intactRoot.transform))
                {
                    error =
                        $"Cover view health stage {index} visual root must belong below the intact visual root.";
                    return false;
                }
            }

            if (!hasLowestStage)
            {
                error =
                    "Cover view health stages require a 1% minimum stage for every positive durability value.";
                return false;
            }

            for (int leftIndex = 0; leftIndex < stages.Length; leftIndex++)
            {
                Transform left = stages[leftIndex].VisualRoot.transform;
                for (int rightIndex = leftIndex + 1;
                    rightIndex < stages.Length;
                    rightIndex++)
                {
                    Transform right = stages[rightIndex].VisualRoot.transform;
                    if (left == right
                        || left.IsChildOf(right)
                        || right.IsChildOf(left))
                    {
                        error =
                            "Cover view health stage visual roots must be distinct sibling branches.";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        private ResolvedHealthStage[] ResolveHealthStages()
        {
            if (!HasAuthoredHealthStages())
            {
                return intactRoot == null
                    ? Array.Empty<ResolvedHealthStage>()
                    : new[]
                    {
                        new ResolvedHealthStage(0, 1, intactRoot)
                    };
            }

            List<ResolvedHealthStage> resolved =
                new List<ResolvedHealthStage>(healthStages.Length);
            for (int index = 0; index < healthStages.Length; index++)
            {
                HealthStage stage = healthStages[index];
                if (stage != null && stage.VisualRoot != null)
                {
                    resolved.Add(new ResolvedHealthStage(
                        index,
                        stage.MinDurabilityPercentInclusive,
                        stage.VisualRoot));
                }
            }

            return resolved.ToArray();
        }

        private bool HasAuthoredHealthStages()
        {
            return healthStages != null && healthStages.Length > 0;
        }

        private static bool ContainsSameColliders(
            Collider[] authoredColliders,
            Collider[] resolvedColliders)
        {
            if (authoredColliders == null || resolvedColliders == null
                || authoredColliders.Length != resolvedColliders.Length)
            {
                return false;
            }

            HashSet<int> resolvedIds = new HashSet<int>();
            for (int index = 0; index < resolvedColliders.Length; index++)
            {
                if (resolvedColliders[index] == null)
                {
                    return false;
                }

                resolvedIds.Add(resolvedColliders[index].GetInstanceID());
            }

            for (int index = 0; index < authoredColliders.Length; index++)
            {
                if (authoredColliders[index] == null
                    || !resolvedIds.Contains(
                        authoredColliders[index].GetInstanceID()))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsShadowProxy(MeshFilter meshFilter)
        {
            return meshFilter != null
                && meshFilter.name == ShadowCasterProxyName;
        }

        private static bool IsRuntimeProjectileBlocker(Collider collider)
        {
            return collider != null
                && collider.name == ProjectileBlockerProxyName;
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
