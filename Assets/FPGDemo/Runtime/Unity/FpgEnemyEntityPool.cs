using System;
using System.Collections.Generic;
using FPG.Demo.Core;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Fixed-capacity formal enemy entity pool. All prefab instantiation is
    /// restricted to TryPrewarm (the Preparing phase); Acquire/Release only
    /// activate and deactivate already-created objects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgEnemyEntityPool : MonoBehaviour
    {
        [SerializeField, Min(1)]
        private int capacity = 32;

        [SerializeField]
        private Transform poolRoot;

        private PoolSlot[] slots;
        private Dictionary<RuntimeId, int> activeByRuntimeId;
        private RuntimeId[] activeScratch;
        private bool prepared;
        private bool combatLocked;

        public int Capacity => capacity;
        public int ActiveCount => activeByRuntimeId == null ? 0 : activeByRuntimeId.Count;
        public bool IsPrepared => prepared;
        public bool IsCombatLocked => combatLocked;

        private void Awake()
        {
            TryInitialize(out _);
        }

        public bool TryInitialize(out string error)
        {
            if (capacity <= 0)
            {
                error = "Formal enemy entity pool capacity must be positive.";
                return false;
            }

            slots = new PoolSlot[capacity];
            activeByRuntimeId = new Dictionary<RuntimeId, int>(capacity);
            activeScratch = new RuntimeId[capacity];
            prepared = false;
            combatLocked = false;
            error = string.Empty;
            return true;
        }

        public bool TryPrewarm(
            IReadOnlyList<FpgEnemyPoolWarmupRequest> requests,
            out string error)
        {
            if (combatLocked)
            {
                error = "Formal entity pool cannot prewarm after combat has started.";
                return false;
            }

            if (requests == null || requests.Count == 0)
            {
                error = "Formal entity pool requires at least one warmup request.";
                return false;
            }

            if (!TryInitialize(out error))
            {
                return false;
            }

            int total = 0;
            for (int index = 0; index < requests.Count; index++)
            {
                FpgEnemyPoolWarmupRequest request = requests[index];
                if (request.Definition == null || request.Count <= 0)
                {
                    error = $"Formal entity warmup request {index} is invalid.";
                    return false;
                }

                if (!request.Definition.TryValidate(out error))
                {
                    error = $"Enemy '{request.Definition.EnemyDefinitionId}' is invalid: {error}";
                    return false;
                }

                if (request.Count > capacity - total)
                {
                    error = "Formal entity pool capacity is smaller than the planned warmup count.";
                    return false;
                }

                total += request.Count;
            }

            int nextSlot = 0;
            try
            {
                for (int requestIndex = 0; requestIndex < requests.Count; requestIndex++)
                {
                    FpgEnemyPoolWarmupRequest request = requests[requestIndex];
                    for (int copy = 0; copy < request.Count; copy++)
                    {
                        GameObject instance = Instantiate(
                            request.Definition.EntityPrefab,
                            poolRoot == null ? transform : poolRoot,
                            false);
                        instance.name = $"FormalEnemyPool[{nextSlot}] {request.Definition.EnemyDefinitionId}";
                        instance.SetActive(false);

                        IFpgFormalEnemyEntityBinder binder =
                            instance.GetComponent<IFpgFormalEnemyEntityBinder>();
                        slots[nextSlot] = new PoolSlot(instance, request.Definition, binder);
                        if (binder == null)
                        {
                            throw new InvalidOperationException(
                                $"Enemy '{request.Definition.EnemyDefinitionId}' prefab requires an IFpgFormalEnemyEntityBinder.");
                        }

                        if (binder.SocketRegistry == null)
                        {
                            throw new InvalidOperationException(
                                $"Enemy '{request.Definition.EnemyDefinitionId}' prefab requires a D0ActorSocketRegistry.");
                        }
                        nextSlot++;
                    }
                }
            }
            catch (Exception exception)
            {
                DisposeInstances();
                error = $"Formal entity pool prewarm failed: {exception.Message}";
                return false;
            }

            prepared = true;
            error = string.Empty;
            return true;
        }

        public bool TryAcquire(
            FpgEnemyDefinition definition,
            RuntimeId runtimeId,
            Pose pose,
            int spawnSequence,
            out FpgEnemyEntityHandle handle,
            out string error)
        {
            handle = default(FpgEnemyEntityHandle);
            if (!prepared || !combatLocked)
            {
                error = "Formal entity acquire requires a prepared pool in combat phase.";
                return false;
            }

            if (definition == null || !runtimeId.IsValid)
            {
                error = "Formal entity acquire requires a definition and valid RuntimeId.";
                return false;
            }

            if (activeByRuntimeId.ContainsKey(runtimeId))
            {
                error = $"Formal RuntimeId '{runtimeId}' is already active in the entity pool.";
                return false;
            }

            for (int index = 0; index < slots.Length; index++)
            {
                PoolSlot slot = slots[index];
                if (slot.InUse
                    || slot.Instance == null
                    || slot.Binder == null
                    || slot.Definition != definition)
                {
                    continue;
                }

                Transform target = slot.Instance.transform;
                target.SetPositionAndRotation(pose.position, pose.rotation);
                slot.Instance.SetActive(true);

                if (!slot.Binder.TryBindFormalRuntime(
                        runtimeId,
                        spawnSequence,
                        definition,
                        out error))
                {
                    slot.Binder.UnbindFormalRuntime();
                    slot.Instance.SetActive(false);
                    return false;
                }

                slot.Binder.SetFormalGameplayEnabled(false);
                slot.InUse = true;
                slot.RuntimeId = runtimeId;
                slots[index] = slot;
                activeByRuntimeId.Add(runtimeId, index);
                handle = new FpgEnemyEntityHandle(
                    index,
                    runtimeId,
                    definition,
                    slot.Instance,
                    slot.Binder);
                error = string.Empty;
                return true;
            }

            error = $"Formal entity pool has no free instance for '{definition.EnemyDefinitionId}'.";
            return false;
        }

        public bool TrySetGameplayEnabled(RuntimeId runtimeId, bool enabled)
        {
            if (!TryGetSlot(runtimeId, out int slotIndex))
            {
                return false;
            }

            IFpgFormalEnemyEntityBinder binder = slots[slotIndex].Binder;
            if (binder == null)
            {
                return false;
            }

            binder.SetFormalGameplayEnabled(enabled);
            return true;
        }

        public bool TryRelease(RuntimeId runtimeId)
        {
            if (!TryGetSlot(runtimeId, out int slotIndex))
            {
                return false;
            }

            PoolSlot slot = slots[slotIndex];
            if (slot.Binder != null)
            {
                slot.Binder.SetFormalGameplayEnabled(false);
                slot.Binder.UnbindFormalRuntime();
            }

            if (slot.Instance != null)
            {
                slot.Instance.SetActive(false);
            }

            slot.InUse = false;
            slot.RuntimeId = RuntimeId.Invalid;
            slots[slotIndex] = slot;
            activeByRuntimeId.Remove(runtimeId);
            return true;
        }

        public bool TryGet(RuntimeId runtimeId, out FpgEnemyEntityHandle handle)
        {
            if (TryGetSlot(runtimeId, out int slotIndex))
            {
                PoolSlot slot = slots[slotIndex];
                handle = new FpgEnemyEntityHandle(
                    slotIndex,
                    runtimeId,
                    slot.Definition,
                    slot.Instance,
                    slot.Binder);
                return true;
            }

            handle = default(FpgEnemyEntityHandle);
            return false;
        }

        public void BeginCombat()
        {
            if (!prepared)
            {
                throw new InvalidOperationException("Formal entity pool must be prewarmed before combat.");
            }

            combatLocked = true;
        }

        public void EndCombat()
        {
            combatLocked = false;
        }

        public void ClearActive()
        {
            if (activeByRuntimeId == null || activeByRuntimeId.Count == 0)
            {
                return;
            }

            int activeCount = 0;
            foreach (RuntimeId runtimeId in activeByRuntimeId.Keys)
            {
                activeScratch[activeCount++] = runtimeId;
            }

            for (int index = 0; index < activeCount; index++)
            {
                TryRelease(activeScratch[index]);
                activeScratch[index] = RuntimeId.Invalid;
            }
        }

        public void Dispose()
        {
            ClearActive();
            combatLocked = false;
            DisposeInstances();
            prepared = false;
            activeByRuntimeId?.Clear();
        }

        private bool TryGetSlot(RuntimeId runtimeId, out int slotIndex)
        {
            if (activeByRuntimeId != null
                && runtimeId.IsValid
                && activeByRuntimeId.TryGetValue(runtimeId, out slotIndex))
            {
                return slotIndex >= 0
                    && slotIndex < slots.Length
                    && slots[slotIndex].InUse;
            }

            slotIndex = -1;
            return false;
        }

        private void DisposeInstances()
        {
            if (slots == null)
            {
                return;
            }

            for (int index = slots.Length - 1; index >= 0; index--)
            {
                GameObject instance = slots[index].Instance;
                if (instance == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(instance);
                }
                else
                {
                    DestroyImmediate(instance);
                }
            }

            slots = new PoolSlot[capacity];
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private struct PoolSlot
        {
            public PoolSlot(
                GameObject instance,
                FpgEnemyDefinition definition,
                IFpgFormalEnemyEntityBinder binder)
            {
                Instance = instance;
                Definition = definition;
                Binder = binder;
                RuntimeId = RuntimeId.Invalid;
                InUse = false;
            }

            public GameObject Instance;
            public FpgEnemyDefinition Definition;
            public IFpgFormalEnemyEntityBinder Binder;
            public RuntimeId RuntimeId;
            public bool InUse;
        }
    }
}


