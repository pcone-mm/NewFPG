using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public readonly struct ProjectileCollisionProxySnapshot
    {
        internal ProjectileCollisionProxySnapshot(
            RuntimeId runtimeId,
            GeometryId geometryId,
            SpatialVectorKey position,
            int sweepRadiusKey,
            SphereCollider collider)
        {
            RuntimeId = runtimeId;
            GeometryId = geometryId;
            Position = position;
            SweepRadiusKey = sweepRadiusKey;
            Collider = collider;
        }

        public RuntimeId RuntimeId { get; }
        public GeometryId GeometryId { get; }
        public SpatialVectorKey Position { get; }
        public int SweepRadiusKey { get; }
        public SphereCollider Collider { get; }
    }

    public sealed class ProjectileCollisionProxyPool : IDisposable
    {
        public const int FirstGeometryId = 100000;

        private readonly int hitboxLayer;
        private readonly int hitboxLayerMask;
        private readonly Transform parent;
        private readonly ProxySlot[] slots;

        private HitboxRegistry registry;
        private GameObject root;
        private bool prepared;
        private bool disposed;
        private int activeCount;

        public ProjectileCollisionProxyPool(
            int capacity,
            int hitboxLayerMask,
            Transform parent = null)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            hitboxLayer = ResolveSingleLayer(hitboxLayerMask);
            this.hitboxLayerMask = hitboxLayerMask;
            this.parent = parent;
            slots = new ProxySlot[capacity];
        }

        public int Capacity => slots.Length;
        public int HitboxLayerMask => hitboxLayerMask;
        public int ActiveCount => activeCount;
        public bool IsPrepared => prepared && !disposed;

        public bool TryPrepare(HitboxRegistry targetRegistry, out string error)
        {
            if (disposed)
            {
                error = "Projectile collision proxy pool is disposed.";
                return false;
            }

            if (targetRegistry == null || !targetRegistry.IsReadyForQueries)
            {
                error = "HitboxRegistry must be ready before preparing projectile collision proxies.";
                return false;
            }

            if (prepared && registry != targetRegistry)
            {
                error = "Projectile collision proxy pool is bound to another HitboxRegistry.";
                return false;
            }

            if (!TryValidateRegistryBudgetAndGeometry(targetRegistry, out error))
            {
                return false;
            }

            if (prepared)
            {
                error = string.Empty;
                return true;
            }

            registry = targetRegistry;
            try
            {
                root = new GameObject("ProjectileCollisionProxies")
                {
                    hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave
                };
                if (parent != null)
                {
                    root.transform.SetParent(parent, false);
                }

                for (int index = 0; index < slots.Length; index++)
                {
                    GameObject proxyObject = new GameObject($"ProjectileCollisionProxy_{index:D2}")
                    {
                        layer = hitboxLayer,
                        hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave
                    };
                    proxyObject.transform.SetParent(root.transform, false);

                    SphereCollider collider = proxyObject.AddComponent<SphereCollider>();
                    collider.isTrigger = false;
                    collider.enabled = false;
                    GeometryId geometryId = new GeometryId(checked(FirstGeometryId + index));
                    HitboxBinding binding = new HitboxBinding(
                        collider,
                        RuntimeId.Invalid,
                        QueryTargetKind.Projectile,
                        HitPart.Projectile,
                        geometryId,
                        Team.Enemy);
                    slots[index] = new ProxySlot(collider, binding, geometryId);
                }

                prepared = true;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                DestroyRoot();
                registry = null;
                error = $"Unable to prepare projectile collision proxies: {exception.Message}";
                return false;
            }
        }

        public DomainResult Acquire(
            in ProjectileSpawnRequest request,
            in ProjectilePathSnapshot path)
        {
            if (!IsPrepared || registry == null || !registry.IsReadyForQueries)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!request.Interceptable || request.Team != Team.Enemy || !path.Matches(request))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            int index = FindFreeSlot();
            if (index < 0)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            ProxySlot slot = slots[index];
            if (!slot.Binding.TryRebindExplicitDynamic(request.RuntimeId))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            float radius = request.SweepRadiusKey
                / (float)SpatialContract.DistanceUnitsPerMeter;
            if (!IsFinite(radius) || radius <= 0f)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            slot.Collider.transform.position = ToPosition(path.Start);
            slot.Collider.radius = radius;
            slot.Collider.enabled = true;
            DomainResult registered = registry.Register(slot.Binding);
            if (!registered.IsSuccess)
            {
                slot.Collider.enabled = false;
                return registered;
            }

            slot.RuntimeId = request.RuntimeId;
            slot.Position = path.Start;
            slot.SweepRadiusKey = request.SweepRadiusKey;
            slot.Active = true;
            slots[index] = slot;
            activeCount++;
            return DomainResult.Success;
        }

        public DomainResult Move(RuntimeId runtimeId, SpatialVectorKey position)
        {
            int index = FindActiveSlot(runtimeId);
            if (index < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            ProxySlot slot = slots[index];
            if (slot.Collider == null || !slot.Collider.enabled)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            slot.Collider.transform.position = ToPosition(position);
            slot.Position = position;
            slots[index] = slot;
            return DomainResult.Success;
        }

        public DomainResult Release(RuntimeId runtimeId)
        {
            int index = FindActiveSlot(runtimeId);
            if (index < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            ProxySlot slot = slots[index];
            DomainResult unregistered = registry == null
                ? DomainResult.Rejected(RejectReason.InvalidState)
                : registry.Unregister(slot.Collider);
            if (!unregistered.IsSuccess)
            {
                return unregistered;
            }

            slot.Collider.enabled = false;
            slot.RuntimeId = RuntimeId.Invalid;
            slot.Position = default(SpatialVectorKey);
            slot.SweepRadiusKey = 0;
            slot.Active = false;
            slots[index] = slot;
            activeCount--;
            return DomainResult.Success;
        }

        public bool TryGetActiveProxy(
            RuntimeId runtimeId,
            out ProjectileCollisionProxySnapshot snapshot)
        {
            snapshot = default(ProjectileCollisionProxySnapshot);
            int index = FindActiveSlot(runtimeId);
            if (index < 0)
            {
                return false;
            }

            ProxySlot slot = slots[index];
            snapshot = new ProjectileCollisionProxySnapshot(
                slot.RuntimeId,
                slot.GeometryId,
                slot.Position,
                slot.SweepRadiusKey,
                slot.Collider);
            return true;
        }

        public void ForceReleaseAll()
        {
            for (int index = 0; index < slots.Length; index++)
            {
                ProxySlot slot = slots[index];
                if (!slot.Active)
                {
                    continue;
                }

                if (registry != null && registry.IsInitialized)
                {
                    registry.Unregister(slot.Collider);
                }

                if (slot.Collider != null)
                {
                    slot.Collider.enabled = false;
                }

                slot.RuntimeId = RuntimeId.Invalid;
                slot.Position = default(SpatialVectorKey);
                slot.SweepRadiusKey = 0;
                slot.Active = false;
                slots[index] = slot;
            }

            activeCount = 0;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            ForceReleaseAll();
            DestroyRoot();
            registry = null;
            prepared = false;
            disposed = true;
        }

        private static int ResolveSingleLayer(int layerMask)
        {
            if (layerMask == 0 || (layerMask & (layerMask - 1)) != 0)
            {
                throw new ArgumentException(
                    "Projectile collision proxies require exactly one hitbox layer.",
                    nameof(layerMask));
            }

            for (int layer = 0; layer < 32; layer++)
            {
                if (layerMask == (1 << layer))
                {
                    return layer;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(layerMask));
        }

        private int FindFreeSlot()
        {
            for (int index = 0; index < slots.Length; index++)
            {
                if (!slots[index].Active)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindActiveSlot(RuntimeId runtimeId)
        {
            if (!runtimeId.IsValid)
            {
                return -1;
            }

            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index].Active && slots[index].RuntimeId == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private bool TryValidateRegistryBudgetAndGeometry(
            HitboxRegistry targetRegistry,
            out string error)
        {
            long nonProxyRegistryCount = targetRegistry.Count;
            for (int index = 0; index < slots.Length; index++)
            {
                ProxySlot slot = slots[index];
                GeometryId reservedGeometryId = slot.GeometryId.IsValid
                    ? slot.GeometryId
                    : new GeometryId(checked(FirstGeometryId + index));
                if (!targetRegistry.TryResolve(reservedGeometryId, out RegisteredHitbox registered))
                {
                    if (slot.Active)
                    {
                        error = "An active projectile collision proxy is missing from HitboxRegistry.";
                        return false;
                    }

                    continue;
                }

                if (registered.Collider != slot.Collider)
                {
                    error = "A static or external hitbox uses a reserved projectile collision proxy GeometryId.";
                    return false;
                }

                if (!slot.Active)
                {
                    error = "An inactive projectile collision proxy is still registered in HitboxRegistry.";
                    return false;
                }

                nonProxyRegistryCount--;
            }

            if (nonProxyRegistryCount < 0L
                || nonProxyRegistryCount + slots.Length >= SpatialContract.AttackQueryCandidateCapacity)
            {
                error = "Static hitboxes plus projectile collision proxies must stay below the attack-query candidate capacity.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void DestroyRoot()
        {
            if (root == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            root = null;
        }

        private static Vector3 ToPosition(SpatialVectorKey key)
        {
            float scale = 1f / SpatialContract.PositionUnitsPerMeter;
            return new Vector3(key.X * scale, key.Y * scale, key.Z * scale);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private struct ProxySlot
        {
            public ProxySlot(
                SphereCollider collider,
                HitboxBinding binding,
                GeometryId geometryId)
            {
                Collider = collider;
                Binding = binding;
                GeometryId = geometryId;
                RuntimeId = default(RuntimeId);
                Position = default(SpatialVectorKey);
                SweepRadiusKey = 0;
                Active = false;
            }

            public SphereCollider Collider;
            public HitboxBinding Binding;
            public GeometryId GeometryId;
            public RuntimeId RuntimeId;
            public SpatialVectorKey Position;
            public int SweepRadiusKey;
            public bool Active;
        }
    }
}
