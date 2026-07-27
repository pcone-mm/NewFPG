using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Read-only union of authored static blockers/player bindings and formal
    /// multi-instance enemy hitboxes.
    /// </summary>
    public sealed class FpgCombinedHitboxLookup : IFpgFormalHitboxLookup
    {
        private readonly HitboxRegistry staticRegistry;
        private readonly IFpgFormalHitboxLookup formalRegistry;

        public FpgCombinedHitboxLookup(
            HitboxRegistry staticRegistry,
            IFpgFormalHitboxLookup formalRegistry)
        {
            this.staticRegistry = staticRegistry
                ?? throw new ArgumentNullException(nameof(staticRegistry));
            this.formalRegistry = formalRegistry
                ?? throw new ArgumentNullException(nameof(formalRegistry));
        }

        public int Count => staticRegistry.Count + formalRegistry.Count;
        public int Capacity => staticRegistry.Count + formalRegistry.Capacity;
        public bool IsReady => staticRegistry.IsReadyForQueries;

        public bool TryResolve(Collider collider, out RegisteredHitbox hitbox)
        {
            return formalRegistry.TryResolve(collider, out hitbox)
                || staticRegistry.TryResolve(collider, out hitbox);
        }

        public bool TryResolve(GeometryId geometryId, out RegisteredHitbox hitbox)
        {
            return formalRegistry.TryResolve(geometryId, out hitbox)
                || staticRegistry.TryResolve(geometryId, out hitbox);
        }
    }

    /// <summary>
    /// Fixed-slot projectile world for the formal multi-enemy path. Owner and
    /// target poses are resolved by RuntimeId on every registration boundary;
    /// no single-enemy Transform is cached.
    /// </summary>
    public sealed class FpgFormalProjectileWorldPort : IProjectileWorldPort
    {
        private readonly FpgCombatantAnchorMap anchorMap;
        private readonly FpgCombinedHitboxLookup hitboxLookup;
        private readonly UnityProjectileWorldSettings settings;
        private readonly IUnityPhysicsQueryBackend physics;
        private readonly ProjectileCollisionProxyPool proxyPool;
        private readonly UnityPhysicsHit[] hitBuffer;
        private readonly ProjectileSlot[] slots;
        private int activeCount;

        public FpgFormalProjectileWorldPort(
            FpgCombatantAnchorMap anchorMap,
            FpgCombinedHitboxLookup hitboxLookup,
            UnityProjectileWorldSettings settings,
            int capacity,
            IUnityPhysicsQueryBackend physics,
            ProjectileCollisionProxyPool proxyPool)
        {
            this.anchorMap = anchorMap ?? throw new ArgumentNullException(nameof(anchorMap));
            this.hitboxLookup = hitboxLookup ?? throw new ArgumentNullException(nameof(hitboxLookup));
            if (!settings.IsValid)
            {
                throw new ArgumentException("Formal projectile settings are invalid.", nameof(settings));
            }

            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            this.physics = physics ?? throw new ArgumentNullException(nameof(physics));
            if (physics.Capacity < SpatialContract.AttackQueryCandidateCapacity)
            {
                throw new ArgumentException("Formal projectile physics capacity is too small.", nameof(physics));
            }

            if (proxyPool == null
                || proxyPool.Capacity != capacity
                || proxyPool.HitboxLayerMask != settings.HitboxLayerMask
                || !proxyPool.IsPrepared)
            {
                throw new ArgumentException(
                    "Formal projectile proxy pool must be prepared with matching capacity and layer.",
                    nameof(proxyPool));
            }

            this.settings = settings;
            this.proxyPool = proxyPool;
            hitBuffer = new UnityPhysicsHit[SpatialContract.AttackQueryCandidateCapacity];
            slots = new ProjectileSlot[capacity];
        }

        public int Capacity => slots.Length;
        public int ActiveCount => activeCount;

        public DomainResult Register(
            in ProjectileSpawnRequest request,
            out ProjectilePathSnapshot path)
        {
            path = default(ProjectilePathSnapshot);
            if (!IsOperational || !IsSpawnRequestValid(request))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            Transform owner = null;
            Transform target = null;
            if (request.OwnerId == request.TargetId
                || (!request.HasExplicitPath
                    && (!TryResolveOrigin(request.OwnerId, out owner)
                        || !TryResolveTarget(request.TargetId, out target))))
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            int freeIndex = -1;
            for (int index = 0; index < slots.Length; index++)
            {
                ProjectileSlot slot = slots[index];
                if (slot.Active
                    && (slot.ProjectileId == request.ProjectileId
                        || slot.RuntimeId == request.RuntimeId))
                {
                    return DomainResult.Rejected(RejectReason.DuplicateSequence);
                }

                if (!slot.Active && freeIndex < 0)
                {
                    freeIndex = index;
                }
            }

            if (freeIndex < 0 || activeCount >= slots.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            SpatialVectorKey start = request.HasExplicitPath
                ? request.ExplicitStart
                : default(SpatialVectorKey);
            SpatialVectorKey end = request.HasExplicitPath
                ? request.ExplicitEnd
                : default(SpatialVectorKey);
            if (!request.HasExplicitPath
                && (!TryQuantizePosition(owner.position, out start)
                    || !TryQuantizePosition(target.position, out end)))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (start == end)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            path = new ProjectilePathSnapshot(
                request.ProjectileId,
                request.RuntimeId,
                request.Tick,
                request.ArrivalTick,
                start,
                end);
            if (request.Interceptable)
            {
                DomainResult acquired = proxyPool.Acquire(request, path);
                if (!acquired.IsSuccess)
                {
                    return acquired;
                }
            }

            slots[freeIndex] = new ProjectileSlot(
                request.ProjectileId,
                request.RuntimeId,
                request.TargetId,
                request.Team,
                request.SweepRadiusKey,
                request.Interceptable,
                request.TargetingMode,
                path);
            activeCount++;
            return DomainResult.Success;
        }

        public DomainResult Sweep(
            in ProjectileSweepRequest request,
            out ProjectileSweepHit hit)
        {
            hit = ProjectileSweepHit.None;
            if (!IsOperational || !IsSweepRequestValid(request))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            int slotIndex = Find(request.ProjectileId, request.RuntimeId);
            if (slotIndex < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            ProjectileSlot slot = slots[slotIndex];
            DomainResult segment = slot.Path.TryGetSegment(
                request.Tick,
                out SpatialVectorKey expectedFrom,
                out SpatialVectorKey expectedTo);
            if (!segment.IsSuccess)
            {
                return segment;
            }

            if (request.From != expectedFrom
                || request.To != expectedTo
                || request.SweepRadiusKey != slot.SweepRadiusKey)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            Vector3 origin = ToPosition(request.From);
            Vector3 destination = ToPosition(request.To);
            Vector3 displacement = destination - origin;
            float distance = displacement.magnitude;
            if (!IsFinite(origin) || !IsFinite(destination) || !IsFinite(distance))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (distance <= 0.0000001f)
            {
                return MoveProxy(slot, request.To);
            }

            float radius = request.SweepRadiusKey
                / (float)SpatialContract.DistanceUnitsPerMeter;
            physics.SyncTransforms();
            NonAllocPhysicsQueryResult batch = physics.SphereCastNonAlloc(
                origin,
                radius,
                displacement / distance,
                hitBuffer,
                distance,
                settings.PhysicsLayerMask,
                QueryTriggerInteraction.Collide);
            if (batch.MayBeTruncated || batch.Count >= hitBuffer.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            bool hasBest = false;
            SweepCandidate best = default(SweepCandidate);
            for (int index = 0; index < batch.Count; index++)
            {
                if (!TryCreateCandidate(
                        hitBuffer[index],
                        slot.TargetId,
                        slot.Team,
                        slot.TargetingMode,
                        distance,
                        out SweepCandidate candidate))
                {
                    continue;
                }

                if (!hasBest || Compare(candidate, best) < 0)
                {
                    best = candidate;
                    hasBest = true;
                }
            }

            if (!hasBest)
            {
                return MoveProxy(slot, request.To);
            }

            DomainResult moved = MoveProxy(slot, best.Point);
            if (!moved.IsSuccess)
            {
                return moved;
            }

            hit = best.Kind == ProjectileSweepHitKind.EnvironmentBlocked
                ? ProjectileSweepHit.EnvironmentBlocked(
                    best.GeometryId,
                    best.DistanceKey,
                    best.Point)
                : ProjectileSweepHit.Target(
                    best.TargetId,
                    best.HitPart,
                    best.GeometryId,
                    best.DistanceKey,
                    best.Point);
            return DomainResult.Success;
        }

        public DomainResult Release(in ProjectileReleaseRequest request)
        {
            if (!IsOperational || !IsReleaseRequestValid(request))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            int index = Find(request.ProjectileId, request.RuntimeId);
            if (index < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            ProjectileSlot slot = slots[index];
            if (!request.Tick.IsValid || request.Tick < slot.Path.SpawnTick)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            if (slot.Interceptable)
            {
                DomainResult released = proxyPool.Release(slot.RuntimeId);
                if (!released.IsSuccess)
                {
                    return released;
                }
            }

            slots[index] = default(ProjectileSlot);
            activeCount--;
            return DomainResult.Success;
        }

        public void ClearAll()
        {
            proxyPool.ForceReleaseAll();
            Array.Clear(slots, 0, slots.Length);
            activeCount = 0;
        }

        private bool IsOperational => anchorMap.IsInitialized && hitboxLookup.IsReady;

        private bool TryResolveOrigin(RuntimeId runtimeId, out Transform anchor)
        {
            if (anchorMap.TryGet(runtimeId, out FpgCombatantAnchorSnapshot snapshot))
            {
                anchor = snapshot.ProjectileAnchor == null
                    ? snapshot.GameplayAnchor
                    : snapshot.ProjectileAnchor;
                return anchor != null;
            }

            anchor = null;
            return false;
        }

        private bool TryResolveTarget(RuntimeId runtimeId, out Transform anchor)
        {
            if (anchorMap.TryGet(runtimeId, out FpgCombatantAnchorSnapshot snapshot))
            {
                anchor = snapshot.WeakpointAnchor == null
                    ? snapshot.GameplayAnchor
                    : snapshot.WeakpointAnchor;
                return anchor != null;
            }

            anchor = null;
            return false;
        }

        private DomainResult MoveProxy(in ProjectileSlot slot, SpatialVectorKey point)
        {
            return !slot.Interceptable
                ? DomainResult.Success
                : proxyPool.Move(slot.RuntimeId, point);
        }

        private bool TryCreateCandidate(
            in UnityPhysicsHit physicsHit,
            RuntimeId targetId,
            Team ownerTeam,
            ProjectileTargetingMode targetingMode,
            float maxDistance,
            out SweepCandidate candidate)
        {
            candidate = default(SweepCandidate);
            Collider collider = physicsHit.Collider;
            if (collider == null
                || !collider.enabled
                || !collider.gameObject.activeInHierarchy
                || !hitboxLookup.TryResolve(collider, out RegisteredHitbox registered)
                || !IsLayerIncluded(collider.gameObject.layer, registered.TargetKind)
                || collider.isTrigger && !registered.AllowTrigger
                || registered.Team == ownerTeam
                || !IsFinite(physicsHit.Distance)
                || physicsHit.Distance < 0f
                || physicsHit.Distance > maxDistance
                || !TryQuantizeDistance(physicsHit.Distance, out int distanceKey)
                || !TryQuantizePosition(physicsHit.Point, out SpatialVectorKey point))
            {
                return false;
            }

            if (registered.TargetKind == QueryTargetKind.EnvironmentBlocker)
            {
                candidate = new SweepCandidate(
                    ProjectileSweepHitKind.EnvironmentBlocked,
                    RuntimeId.Invalid,
                    HitPart.Body,
                    registered.GeometryId,
                    distanceKey,
                    point);
                return true;
            }

            if (targetingMode == ProjectileTargetingMode.LockedTarget
                && (registered.TargetKind != QueryTargetKind.Combatant
                    || registered.RuntimeId != targetId))
            {
                return false;
            }

            if (targetingMode == ProjectileTargetingMode.FirstSurface
                && ((registered.TargetKind != QueryTargetKind.Combatant
                        && registered.TargetKind != QueryTargetKind.Projectile)
                    || !registered.RuntimeId.IsValid))
            {
                return false;
            }

            candidate = new SweepCandidate(
                ProjectileSweepHitKind.Target,
                registered.RuntimeId,
                registered.HitPart,
                registered.GeometryId,
                distanceKey,
                point);
            return true;
        }

        private bool IsLayerIncluded(int layer, QueryTargetKind kind)
        {
            int mask = kind == QueryTargetKind.EnvironmentBlocker
                ? settings.BlockerLayerMask
                : settings.HitboxLayerMask;
            return layer >= 0 && layer < 32 && (mask & (1 << layer)) != 0;
        }

        private int Find(ProjectileId projectileId, RuntimeId runtimeId)
        {
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index].Active
                    && slots[index].ProjectileId == projectileId
                    && slots[index].RuntimeId == runtimeId)
                {
                    return index;
                }
            }
            return -1;
        }

        private static bool IsSpawnRequestValid(in ProjectileSpawnRequest request)
        {
            bool lockedTarget = request.TargetingMode
                == ProjectileTargetingMode.LockedTarget;
            bool firstSurfacePlayerPath = request.TargetingMode
                == ProjectileTargetingMode.FirstSurface
                && request.Team == Team.Player
                && !request.TargetId.IsValid
                && request.HasExplicitPath;
            return request.Tick.IsValid
                && request.ArrivalTick.IsValid
                && request.ArrivalTick > request.Tick
                && request.ProjectileId.IsValid
                && request.RuntimeId.IsValid
                && request.AttackId.IsValid
                && request.OwnerId.IsValid
                && (lockedTarget ? request.TargetId.IsValid : firstSurfacePlayerPath)
                && request.Team != Team.Neutral
                && Enum.IsDefined(typeof(Team), request.Team)
                && Enum.IsDefined(
                    typeof(ProjectileTargetingMode),
                    request.TargetingMode)
                && (!request.Interceptable || request.Team == Team.Enemy)
                && request.DefinitionId > 0
                && request.SweepRadiusKey > 0;
        }

        private static bool IsSweepRequestValid(in ProjectileSweepRequest request)
        {
            return request.Tick.IsValid
                && request.ProjectileId.IsValid
                && request.RuntimeId.IsValid
                && request.SweepRadiusKey > 0;
        }

        private static bool IsReleaseRequestValid(in ProjectileReleaseRequest request)
        {
            return request.Tick.IsValid
                && request.ProjectileId.IsValid
                && request.RuntimeId.IsValid
                && Enum.IsDefined(typeof(ProjectileTerminalReason), request.Reason)
                && request.Reason != ProjectileTerminalReason.None;
        }

        private static int Compare(in SweepCandidate left, in SweepCandidate right)
        {
            int value = left.DistanceKey.CompareTo(right.DistanceKey);
            if (value != 0) return value;
            value = KindPriority(left.Kind).CompareTo(KindPriority(right.Kind));
            if (value != 0) return value;
            value = left.GeometryId.CompareTo(right.GeometryId);
            if (value != 0) return value;
            value = left.TargetId.CompareTo(right.TargetId);
            if (value != 0) return value;
            value = left.HitPart.CompareTo(right.HitPart);
            if (value != 0) return value;
            value = left.Point.X.CompareTo(right.Point.X);
            if (value != 0) return value;
            value = left.Point.Y.CompareTo(right.Point.Y);
            return value != 0 ? value : left.Point.Z.CompareTo(right.Point.Z);
        }

        private static int KindPriority(ProjectileSweepHitKind kind)
        {
            return kind == ProjectileSweepHitKind.EnvironmentBlocked ? 0 : 1;
        }

        private static Vector3 ToPosition(SpatialVectorKey key)
        {
            float scale = 1f / SpatialContract.PositionUnitsPerMeter;
            return new Vector3(key.X * scale, key.Y * scale, key.Z * scale);
        }

        private static bool TryQuantizePosition(Vector3 value, out SpatialVectorKey key)
        {
            key = default(SpatialVectorKey);
            if (!TryQuantize(value.x, SpatialContract.PositionUnitsPerMeter, out int x)
                || !TryQuantize(value.y, SpatialContract.PositionUnitsPerMeter, out int y)
                || !TryQuantize(value.z, SpatialContract.PositionUnitsPerMeter, out int z))
            {
                return false;
            }
            key = new SpatialVectorKey(x, y, z);
            return true;
        }

        private static bool TryQuantizeDistance(float value, out int key)
        {
            return TryQuantize(value, SpatialContract.DistanceUnitsPerMeter, out key)
                && key >= 0;
        }

        private static bool TryQuantize(float value, int units, out int key)
        {
            double scaled = value * (double)units;
            if (double.IsNaN(scaled) || double.IsInfinity(scaled)
                || scaled > int.MaxValue || scaled < int.MinValue)
            {
                key = 0;
                return false;
            }
            key = (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private readonly struct ProjectileSlot
        {
            public ProjectileSlot(
                ProjectileId projectileId,
                RuntimeId runtimeId,
                RuntimeId targetId,
                Team team,
                int sweepRadiusKey,
                bool interceptable,
                ProjectileTargetingMode targetingMode,
                ProjectilePathSnapshot path)
            {
                ProjectileId = projectileId;
                RuntimeId = runtimeId;
                TargetId = targetId;
                Team = team;
                SweepRadiusKey = sweepRadiusKey;
                Interceptable = interceptable;
                TargetingMode = targetingMode;
                Path = path;
                Active = true;
            }

            public ProjectileId ProjectileId { get; }
            public RuntimeId RuntimeId { get; }
            public RuntimeId TargetId { get; }
            public Team Team { get; }
            public int SweepRadiusKey { get; }
            public bool Interceptable { get; }
            public ProjectileTargetingMode TargetingMode { get; }
            public ProjectilePathSnapshot Path { get; }
            public bool Active { get; }
        }

        private readonly struct SweepCandidate
        {
            public SweepCandidate(
                ProjectileSweepHitKind kind,
                RuntimeId targetId,
                HitPart hitPart,
                GeometryId geometryId,
                int distanceKey,
                SpatialVectorKey point)
            {
                Kind = kind;
                TargetId = targetId;
                HitPart = hitPart;
                GeometryId = geometryId;
                DistanceKey = distanceKey;
                Point = point;
            }

            public ProjectileSweepHitKind Kind { get; }
            public RuntimeId TargetId { get; }
            public HitPart HitPart { get; }
            public GeometryId GeometryId { get; }
            public int DistanceKey { get; }
            public SpatialVectorKey Point { get; }
        }
    }
}


