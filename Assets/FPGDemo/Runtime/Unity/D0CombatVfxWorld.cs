using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// One fixed-capacity VFX pool owned by D0CombatVfxWorld.  The prefab is
    /// optional for procedural effects that are rendered by an existing
    /// presentation module; such entries still participate in scenario
    /// validation and capacity accounting.
    /// </summary>
    [Serializable]
    public sealed class D0CombatVfxPoolDefinition
    {
        [SerializeField]
        private string key;

        [SerializeField]
        private GameObject prefab;

        [SerializeField, Min(1)]
        private int capacity = 1;

        [SerializeField, Min(0.01f)]
        private float duration = 1f;

        [SerializeField]
        private string animationName = "animation";

        [SerializeField]
        private int sortingOrderOffset;

        public string Key => key;
        public GameObject Prefab => prefab;
        public int Capacity => capacity;
        public float Duration => duration;
        public string AnimationName => animationName;
        public int SortingOrderOffset => sortingOrderOffset;

        public D0CombatVfxPoolDefinition()
        {
        }

        public D0CombatVfxPoolDefinition(
            string key,
            GameObject prefab,
            int capacity,
            float duration,
            string animationName,
            int sortingOrderOffset)
        {
            this.key = key;
            this.prefab = prefab;
            this.capacity = capacity;
            this.duration = duration;
            this.animationName = animationName;
            this.sortingOrderOffset = sortingOrderOffset;
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                error = "Combat VFX pool key must be non-empty.";
                return false;
            }

            if (capacity <= 0)
            {
                error = $"Combat VFX pool '{key}' capacity must be positive.";
                return false;
            }

            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f)
            {
                error = $"Combat VFX pool '{key}' duration must be finite and positive.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(animationName))
            {
                error = $"Combat VFX pool '{key}' animation name must be non-empty.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Scenario-scoped fixed-capacity VFX world.  Definitions are scanned and
    /// all concrete prefabs are instantiated during TryPrepare, before the
    /// combat session starts.  Acquire/Release/Advance only toggle and reuse
    /// already-created objects, so the combat hot path performs no allocation.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class D0CombatVfxWorld : MonoBehaviour
    {
        [Header("D0 VFX world")]
        [SerializeField]
        private Transform poolRoot;

        [SerializeField]
        private D0CombatVfxPoolDefinition[] pools =
            Array.Empty<D0CombatVfxPoolDefinition>();

        [SerializeField]
        private bool prepareOnEnable;

        [SerializeField]
        private bool automaticallyAdvance = true;

        private readonly Dictionary<string, RuntimePool> runtimePools =
            new Dictionary<string, RuntimePool>(StringComparer.Ordinal);
        private readonly Dictionary<GameObject, RuntimeSlot> slotsByObject =
            new Dictionary<GameObject, RuntimeSlot>();
        private readonly List<D0CombatVfxPoolDefinition> runtimeDefinitions =
            new List<D0CombatVfxPoolDefinition>();

        private Transform generatedRoot;
        private bool prepared;
        private bool combatActive;
        private float presentationTime;
        private int globalActiveCapacity = int.MaxValue;

        public Transform PoolRoot => poolRoot;
        public IReadOnlyList<D0CombatVfxPoolDefinition> Pools =>
            pools ?? Array.Empty<D0CombatVfxPoolDefinition>();
        public bool IsPrepared => prepared;
        public bool IsCombatActive => combatActive;
        public int PoolCount => runtimePools.Count;
        public int PrewarmedInstanceCount { get; private set; }
        public int ActiveInstanceCount { get; private set; }
        public int PrepareInstantiateCount { get; private set; }
        public int HotPathInstantiateCount { get; private set; }
        public int HotPathDestroyCount { get; private set; }
        public int AcquireRejectCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public bool TrySetGlobalActiveCapacity(
            int capacity,
            out string error)
        {
            if (capacity <= 0)
            {
                error = "Combat VFX global active capacity must be positive.";
                return false;
            }

            if (prepared && globalActiveCapacity != capacity)
            {
                error =
                    "Combat VFX global active capacity cannot change after preparation.";
                return false;
            }

            globalActiveCapacity = capacity;
            error = string.Empty;
            return true;
        }

        private void Update()
        {
            if (automaticallyAdvance && combatActive)
            {
                Advance(Time.unscaledDeltaTime);
            }
        }

        private void OnEnable()
        {
            if (prepareOnEnable && !prepared)
            {
                if (!TryPrepare(out string error))
                {
                    Debug.LogError($"[{nameof(D0CombatVfxWorld)}] {error}", this);
                }
            }
        }

        private void OnDisable()
        {
            combatActive = false;
            ClearActive();
        }

        private void OnDestroy()
        {
            // Destruction is lifecycle cleanup only. No combat code calls this
            // path; hot-path counters deliberately remain zero.
            DestroyGeneratedRoot();
        }

        public bool TryValidate(out string error)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            D0CombatVfxPoolDefinition[] configured =
                pools ?? Array.Empty<D0CombatVfxPoolDefinition>();
            for (int index = 0; index < configured.Length; index++)
            {
                D0CombatVfxPoolDefinition definition = configured[index];
                if (definition == null)
                {
                    error = $"Combat VFX pool entry {index} is missing.";
                    return false;
                }

                if (!definition.TryValidate(out error))
                {
                    error = $"Combat VFX pool entry {index} is invalid: {error}";
                    return false;
                }

                if (!seen.Add(definition.Key))
                {
                    error = $"Combat VFX pool key '{definition.Key}' is duplicated.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Prewarms all configured pools. Calling this again after success is
        /// idempotent and does not instantiate a second set of views.
        /// </summary>
        public bool TryPrepare(out string error)
        {
            if (prepared)
            {
                error = string.Empty;
                return true;
            }

            if (!TryValidate(out error))
            {
                return false;
            }

            runtimePools.Clear();
            slotsByObject.Clear();
            SeedRuntimeDefinitionsFromSerialized();

            Transform nextGeneratedRoot = null;
            try
            {
                bool hasConcretePrefab = false;
                for (int index = 0; index < runtimeDefinitions.Count; index++)
                {
                    if (runtimeDefinitions[index].Prefab != null)
                    {
                        hasConcretePrefab = true;
                        break;
                    }
                }

                if (hasConcretePrefab)
                {
                    GameObject rootObject = new GameObject("D0CombatVfxPools");
                    nextGeneratedRoot = rootObject.transform;
                    Transform parent = poolRoot == null ? transform : poolRoot;
                    nextGeneratedRoot.SetParent(parent, false);
                }

                for (int index = 0; index < runtimeDefinitions.Count; index++)
                {
                    D0CombatVfxPoolDefinition definition = runtimeDefinitions[index];
                    RuntimePool pool = new RuntimePool(definition);
                    runtimePools.Add(definition.Key, pool);
                    if (definition.Prefab == null)
                    {
                        continue;
                    }

                    for (int slotIndex = 0; slotIndex < definition.Capacity; slotIndex++)
                    {
                        GameObject instance = Instantiate(
                            definition.Prefab,
                            nextGeneratedRoot,
                            false);
                        instance.name = $"{definition.Key}_{slotIndex:00}";
                        instance.SetActive(false);
                        RuntimeSlot slot = pool.AddSlot(instance);
                        slotsByObject.Add(instance, slot);
                        PrewarmedInstanceCount++;
                        PrepareInstantiateCount++;
                    }
                }

                generatedRoot = nextGeneratedRoot;
                prepared = true;
                combatActive = false;
                presentationTime = 0f;
                ActiveInstanceCount = 0;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                if (nextGeneratedRoot != null)
                {
                    DestroyObject(nextGeneratedRoot.gameObject);
                }

                runtimePools.Clear();
                slotsByObject.Clear();
                runtimeDefinitions.Clear();
                PrewarmedInstanceCount = 0;
                error = $"Combat VFX world could not prewarm its pools: {exception.Message}";
                return false;
            }
        }

        public bool TryPrepareForScenario(
            IEnumerable<D0CombatVfxAssetReference> references,
            out string error)
        {
            if (references != null)
            {
                foreach (D0CombatVfxAssetReference reference in references)
                {
                    if (!TryRegisterReference(reference, out error))
                    {
                        return false;
                    }
                }
            }

            return TryPrepare(out error);
        }

        public bool TryRegisterReference(
            D0CombatVfxAssetReference reference,
            out string error)
        {
            error = string.Empty;
            if (reference == null || !reference.TryValidate(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Combat VFX reference is missing.";
                }

                return false;
            }

            SeedRuntimeDefinitionsFromSerialized();

            if (prepared)
            {
                if (runtimePools.ContainsKey(reference.Key))
                {
                    error = string.Empty;
                    return true;
                }

                error = "Combat VFX references must be registered before TryPrepare.";
                return false;
            }

            for (int index = 0; index < runtimeDefinitions.Count; index++)
            {
                D0CombatVfxPoolDefinition existing = runtimeDefinitions[index];
                if (!string.Equals(existing.Key, reference.Key, StringComparison.Ordinal))
                {
                    continue;
                }

                if (existing.Prefab != null
                    && reference.Prefab != null
                    && existing.Prefab != reference.Prefab)
                {
                    error = $"Combat VFX key '{reference.Key}' maps to multiple prefabs.";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            runtimeDefinitions.Add(new D0CombatVfxPoolDefinition(
                reference.Key,
                reference.Prefab,
                reference.PrewarmCapacity,
                reference.Duration,
                reference.AnimationName,
                reference.SortingOrderOffset));
            error = string.Empty;
            return true;
        }

        public void BeginCombat()
        {
            combatActive = prepared;
        }

        public void EndCombat()
        {
            combatActive = false;
            ClearActive();
        }

        public bool TryAcquire(
            string key,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 worldScale,
            out GameObject instance)
        {
            return TryAcquireCore(
                key,
                worldPosition,
                worldRotation,
                worldScale,
                holdUntilRelease: false,
                out instance);
        }

        /// <summary>
        /// Borrows an instance until TryRelease is called. Projectile flight
        /// presentation uses this path so every terminal reason owns cleanup.
        /// </summary>
        public bool TryAcquireHeld(
            string key,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 worldScale,
            out GameObject instance)
        {
            return TryAcquireCore(
                key,
                worldPosition,
                worldRotation,
                worldScale,
                holdUntilRelease: true,
                out instance);
        }

        private bool TryAcquireCore(
            string key,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 worldScale,
            bool holdUntilRelease,
            out GameObject instance)
        {
            instance = null;
            if (!prepared
                || ActiveInstanceCount >= globalActiveCapacity
                || string.IsNullOrWhiteSpace(key)
                || !runtimePools.TryGetValue(key, out RuntimePool pool)
                || !pool.TryAcquire(
                    worldPosition,
                        worldRotation,
                        worldScale,
                        presentationTime,
                        holdUntilRelease,
                        out RuntimeSlot slot))
            {
                AcquireRejectCount++;
                return false;
            }

            instance = slot.Instance;
            slotsByObject[instance] = slot;
            ActiveInstanceCount++;
            return true;
        }

        public bool TryAcquire(
            string key,
            Transform source,
            out GameObject instance)
        {
            if (source == null)
            {
                instance = null;
                AcquireRejectCount++;
                return false;
            }

            return TryAcquire(
                key,
                source.position,
                source.rotation,
                source.lossyScale,
                out instance);
        }

        /// <summary>
        /// Presents a registered dependency. Concrete entries borrow a
        /// prewarmed object; logical entries are already rendered by their
        /// owning procedural module and therefore complete without an object.
        /// </summary>
        public bool TryPresent(
            string key,
            Transform source,
            out GameObject instance)
        {
            instance = null;
            if (!prepared
                || source == null
                || string.IsNullOrWhiteSpace(key)
                || !runtimePools.TryGetValue(key, out RuntimePool pool))
            {
                AcquireRejectCount++;
                return false;
            }

            if (pool.Definition.Prefab == null)
            {
                return true;
            }

            return TryAcquire(key, source, out instance);
        }

        public bool TryRelease(GameObject instance)
        {
            if (instance == null || !slotsByObject.TryGetValue(instance, out RuntimeSlot slot))
            {
                return false;
            }

            if (!slot.Active)
            {
                return false;
            }

            slot.Deactivate();
            ActiveInstanceCount = Mathf.Max(0, ActiveInstanceCount - 1);
            ReleaseCount++;
            return true;
        }

        public void Advance(float unscaledDeltaTime)
        {
            if (!prepared || unscaledDeltaTime <= 0f)
            {
                return;
            }

            presentationTime += Mathf.Max(0f, unscaledDeltaTime);
            foreach (RuntimePool pool in runtimePools.Values)
            {
                pool.Advance(presentationTime, this);
            }
        }

        public void ClearActive()
        {
            foreach (RuntimePool pool in runtimePools.Values)
            {
                pool.Clear(this);
            }

            ActiveInstanceCount = 0;
        }

        public bool TryGetPool(
            string key,
            out D0CombatVfxPoolDefinition definition)
        {
            if (runtimePools.TryGetValue(key ?? string.Empty, out RuntimePool pool))
            {
                definition = pool.Definition;
                return true;
            }

            for (int index = 0; index < runtimeDefinitions.Count; index++)
            {
                if (string.Equals(runtimeDefinitions[index].Key, key, StringComparison.Ordinal))
                {
                    definition = runtimeDefinitions[index];
                    return true;
                }
            }

            definition = null;
            return false;
        }

        private void SeedRuntimeDefinitionsFromSerialized()
        {
            D0CombatVfxPoolDefinition[] configured =
                pools ?? Array.Empty<D0CombatVfxPoolDefinition>();
            for (int index = 0; index < configured.Length; index++)
            {
                D0CombatVfxPoolDefinition definition = configured[index];
                if (definition == null)
                {
                    continue;
                }

                bool alreadyRegistered = false;
                for (int registeredIndex = 0;
                    registeredIndex < runtimeDefinitions.Count;
                    registeredIndex++)
                {
                    if (string.Equals(
                            runtimeDefinitions[registeredIndex].Key,
                            definition.Key,
                            StringComparison.Ordinal))
                    {
                        alreadyRegistered = true;
                        break;
                    }
                }

                if (!alreadyRegistered)
                {
                    runtimeDefinitions.Add(definition);
                }
            }
        }

        private void DestroyGeneratedRoot()
        {
            if (generatedRoot == null)
            {
                return;
            }

            DestroyObject(generatedRoot.gameObject);
            generatedRoot = null;
            prepared = false;
            runtimePools.Clear();
            slotsByObject.Clear();
            runtimeDefinitions.Clear();
            PrewarmedInstanceCount = 0;
            ActiveInstanceCount = 0;
        }

        private void OnSlotExpired(RuntimeSlot slot)
        {
            if (slot == null || !slot.Active)
            {
                return;
            }

            slot.Deactivate();
            ActiveInstanceCount = Mathf.Max(0, ActiveInstanceCount - 1);
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private sealed class RuntimePool
        {
            private readonly List<RuntimeSlot> slots = new List<RuntimeSlot>();

            public RuntimePool(D0CombatVfxPoolDefinition definition)
            {
                Definition = definition;
            }

            public D0CombatVfxPoolDefinition Definition { get; }

            public RuntimeSlot AddSlot(GameObject instance)
            {
                RuntimeSlot slot = new RuntimeSlot(instance, Definition.Duration);
                slots.Add(slot);
                return slot;
            }

            public bool TryAcquire(
                Vector3 worldPosition,
                Quaternion worldRotation,
                Vector3 worldScale,
                float now,
                bool holdUntilRelease,
                out RuntimeSlot slot)
            {
                for (int index = 0; index < slots.Count; index++)
                {
                    RuntimeSlot candidate = slots[index];
                    if (candidate.Active)
                    {
                        continue;
                    }

                    candidate.Activate(
                        worldPosition,
                        worldRotation,
                        worldScale,
                        now,
                        holdUntilRelease);
                    slot = candidate;
                    return true;
                }

                slot = null;
                return false;
            }

            public void Advance(float now, D0CombatVfxWorld owner)
            {
                for (int index = 0; index < slots.Count; index++)
                {
                    RuntimeSlot slot = slots[index];
                    if (slot.Active && !slot.Held && now >= slot.ExpireAt)
                    {
                        owner.OnSlotExpired(slot);
                    }
                }
            }

            public void Clear(D0CombatVfxWorld owner)
            {
                for (int index = 0; index < slots.Count; index++)
                {
                    RuntimeSlot slot = slots[index];
                    if (slot.Active)
                    {
                        owner.OnSlotExpired(slot);
                    }
                }
            }
        }

        private sealed class RuntimeSlot
        {
            private readonly float duration;
            private readonly ParticleSystem[] particleSystems;

            public RuntimeSlot(GameObject instance, float duration)
            {
                Instance = instance;
                this.duration = duration;
                particleSystems = instance == null
                    ? Array.Empty<ParticleSystem>()
                    : instance.GetComponentsInChildren<ParticleSystem>(true);
            }

            public GameObject Instance { get; }
            public bool Active { get; private set; }
            public bool Held { get; private set; }
            public float ExpireAt { get; private set; }

            public void Activate(
                Vector3 position,
                Quaternion rotation,
                Vector3 scale,
                float now,
                bool holdUntilRelease)
            {
                Transform target = Instance.transform;
                target.SetPositionAndRotation(position, rotation);
                target.localScale = scale;
                Held = holdUntilRelease;
                ExpireAt = holdUntilRelease
                    ? float.PositiveInfinity
                    : now + duration;
                Active = true;
                Instance.SetActive(true);
                for (int index = 0; index < particleSystems.Length; index++)
                {
                    ParticleSystem particleSystem = particleSystems[index];
                    if (particleSystem == null)
                    {
                        continue;
                    }

                    particleSystem.Clear(true);
                    particleSystem.Play(true);
                }
            }

            public void Deactivate()
            {
                Active = false;
                Held = false;
                ExpireAt = 0f;
                if (Instance != null)
                {
                    for (int index = 0; index < particleSystems.Length; index++)
                    {
                        ParticleSystem particleSystem = particleSystems[index];
                        if (particleSystem != null)
                        {
                            particleSystem.Stop(
                                true,
                                ParticleSystemStopBehavior.StopEmittingAndClear);
                        }
                    }

                    Instance.SetActive(false);
                }
            }
        }
    }
}
