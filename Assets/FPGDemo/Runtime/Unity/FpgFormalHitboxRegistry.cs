using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Minimal lookup surface used by formal attack-query adapters. The
    /// returned hitbox shape matches the existing spatial query contract while
    /// keeping formal storage independent from the frozen D0 registry.
    /// </summary>
    public interface IFpgFormalHitboxLookup
    {
        int Count { get; }
        int Capacity { get; }

        bool TryResolve(Collider collider, out RegisteredHitbox hitbox);
        bool TryResolve(GeometryId geometryId, out RegisteredHitbox hitbox);
    }

    public readonly struct FpgFormalHitboxBinding
    {
        internal FpgFormalHitboxBinding(
            RegisteredHitbox hitbox,
            int spawnSequence,
            int hitPartOrdinal)
        {
            Hitbox = hitbox;
            SpawnSequence = spawnSequence;
            HitPartOrdinal = hitPartOrdinal;
        }

        public RegisteredHitbox Hitbox { get; }
        public Collider Collider => Hitbox.Collider;
        public RuntimeId RuntimeId => Hitbox.RuntimeId;
        public HitPart HitPart => Hitbox.HitPart;
        public GeometryId GeometryId => Hitbox.GeometryId;
        public int SpawnSequence { get; }
        public int HitPartOrdinal { get; }
        public bool IsValid => Hitbox.IsValid
            && SpawnSequence >= 0
            && HitPartOrdinal >= 0;
    }

    /// <summary>
    /// Fixed-capacity hitbox registry for formal multi-enemy encounters.
    /// Geometry identity is derived solely from deterministic spawn identity;
    /// Unity instance IDs are deliberately not part of the contract.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgFormalHitboxRegistry : MonoBehaviour, IFpgFormalHitboxLookup
    {
        [SerializeField, Min(1)]
        private int capacity = 64;

        private FpgFormalHitboxBinding[] bindings = Array.Empty<FpgFormalHitboxBinding>();
        private HitboxRegistry externalGeometryRegistry;
        private int count;
        private bool initialized;

        public int Count => count;
        public int Capacity => capacity;
        public bool IsInitialized => initialized;

        private void Awake()
        {
            TryInitialize(out _);
        }

        /// <summary>
        /// Allocates the fixed backing store. This belongs to Preparing and
        /// must not be called as a recovery path from a combat tick.
        /// </summary>
        public bool TryInitialize(out string error)
        {
            if (capacity <= 0)
            {
                initialized = false;
                error = "Formal hitbox registry capacity must be positive.";
                return false;
            }

            if (!initialized || bindings.Length != capacity)
            {
                bindings = new FpgFormalHitboxBinding[capacity];
            }
            else
            {
                Array.Clear(bindings, 0, bindings.Length);
            }

            count = 0;
            initialized = true;
            error = string.Empty;
            return true;
        }

        public bool CanRegister(int requiredCount)
        {
            return initialized
                && requiredCount >= 0
                && requiredCount <= bindings.Length - count;
        }

        public bool TrySetExternalGeometryRegistry(
            HitboxRegistry registry,
            out string error)
        {
            if (count != 0 || registry == null || !registry.IsReadyForQueries)
            {
                error = "External geometry registry must be ready before formal hitboxes register.";
                return false;
            }

            externalGeometryRegistry = registry;
            error = string.Empty;
            return true;
        }

        public bool TryValidateGeometryIds(
            int spawnSequence,
            int hitPartCount,
            out string error)
        {
            if (!initialized || externalGeometryRegistry == null
                || !externalGeometryRegistry.IsReadyForQueries)
            {
                error = "Formal geometry validation requires initialized formal and static registries.";
                return false;
            }

            if (spawnSequence < 0
                || spawnSequence > FpgFormalGeometryId.MaxSpawnSequence
                || hitPartCount <= 0
                || hitPartCount > FpgFormalGeometryId.MaxHitPartOrdinal + 1)
            {
                error = "Formal SpawnSequence or hit-part count exceeds the injective GeometryId bounds.";
                return false;
            }

            for (int ordinal = 0; ordinal < hitPartCount; ordinal++)
            {
                if (!FpgFormalGeometryId.TryDeriveCombatGeometryId(
                        spawnSequence,
                        ordinal,
                        out GeometryId geometryId))
                {
                    error = "Formal GeometryId packing rejected a preflight sequence.";
                    return false;
                }

                if (externalGeometryRegistry.TryResolve(geometryId, out _))
                {
                    error = "Formal GeometryId " + geometryId
                        + " conflicts with a static or projectile-reserved binding.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public DomainResult TryRegister(
            RuntimeId runtimeId,
            int spawnSequence,
            int hitPartOrdinal,
            Collider collider,
            HitPart hitPart,
            out GeometryId geometryId)
        {
            geometryId = FpgFormalGeometryId.DeriveCombatGeometryId(
                spawnSequence,
                hitPartOrdinal);
            if (!initialized)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!runtimeId.IsValid
                || spawnSequence < 0
                || hitPartOrdinal < 0
                || collider == null
                || !geometryId.IsValid
                || !Enum.IsDefined(typeof(HitPart), hitPart)
                || hitPart == HitPart.Projectile)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (externalGeometryRegistry != null
                && externalGeometryRegistry.TryResolve(geometryId, out _))
            {
                return DomainResult.Rejected(RejectReason.DuplicateSequence);
            }

            for (int index = 0; index < count; index++)
            {
                FpgFormalHitboxBinding existing = bindings[index];
                if (ReferenceEquals(existing.Collider, collider)
                    || existing.GeometryId == geometryId)
                {
                    return DomainResult.Rejected(RejectReason.DuplicateSequence);
                }
            }

            if (count >= bindings.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            RegisteredHitbox hitbox = new RegisteredHitbox(
                collider,
                runtimeId,
                QueryTargetKind.Combatant,
                hitPart,
                geometryId,
                Team.Enemy,
                collider.isTrigger);
            bindings[count++] = new FpgFormalHitboxBinding(
                hitbox,
                spawnSequence,
                hitPartOrdinal);
            return DomainResult.Success;
        }

        public int Unregister(RuntimeId runtimeId)
        {
            if (!initialized || !runtimeId.IsValid || count == 0)
            {
                return 0;
            }

            int removed = 0;
            int writeIndex = 0;
            for (int readIndex = 0; readIndex < count; readIndex++)
            {
                FpgFormalHitboxBinding binding = bindings[readIndex];
                if (binding.RuntimeId == runtimeId)
                {
                    removed++;
                    continue;
                }

                if (writeIndex != readIndex)
                {
                    bindings[writeIndex] = binding;
                }
                writeIndex++;
            }

            Array.Clear(bindings, writeIndex, count - writeIndex);
            count = writeIndex;
            return removed;
        }

        public bool TryUnregister(Collider collider)
        {
            if (!initialized || collider == null)
            {
                return false;
            }

            for (int index = 0; index < count; index++)
            {
                if (!ReferenceEquals(bindings[index].Collider, collider))
                {
                    continue;
                }

                RemoveAt(index);
                return true;
            }

            return false;
        }

        public bool TryResolve(Collider collider, out RegisteredHitbox hitbox)
        {
            if (initialized && collider != null)
            {
                for (int index = 0; index < count; index++)
                {
                    if (ReferenceEquals(bindings[index].Collider, collider))
                    {
                        hitbox = bindings[index].Hitbox;
                        return true;
                    }
                }
            }

            hitbox = default(RegisteredHitbox);
            return false;
        }

        public bool TryResolve(GeometryId geometryId, out RegisteredHitbox hitbox)
        {
            if (initialized && geometryId.IsValid)
            {
                for (int index = 0; index < count; index++)
                {
                    if (bindings[index].GeometryId == geometryId)
                    {
                        hitbox = bindings[index].Hitbox;
                        return true;
                    }
                }
            }

            hitbox = default(RegisteredHitbox);
            return false;
        }

        public bool TryGetBinding(
            GeometryId geometryId,
            out FpgFormalHitboxBinding binding)
        {
            if (initialized && geometryId.IsValid)
            {
                for (int index = 0; index < count; index++)
                {
                    if (bindings[index].GeometryId == geometryId)
                    {
                        binding = bindings[index];
                        return true;
                    }
                }
            }

            binding = default(FpgFormalHitboxBinding);
            return false;
        }

        public void Clear()
        {
            if (bindings.Length > 0)
            {
                Array.Clear(bindings, 0, bindings.Length);
            }
            count = 0;
        }

        private void RemoveAt(int index)
        {
            int moveCount = count - index - 1;
            if (moveCount > 0)
            {
                Array.Copy(bindings, index + 1, bindings, index, moveCount);
            }

            count--;
            bindings[count] = default(FpgFormalHitboxBinding);
        }

        private void OnDestroy()
        {
            Clear();
            externalGeometryRegistry = null;
            initialized = false;
        }
    }
}



