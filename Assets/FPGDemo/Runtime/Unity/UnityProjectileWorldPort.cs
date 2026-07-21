using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [Serializable]
    public struct UnityProjectileWorldSettings
    {
        [SerializeField]
        private int hitboxLayerMask;

        [SerializeField]
        private int blockerLayerMask;

        public UnityProjectileWorldSettings(int hitboxLayerMask, int blockerLayerMask)
        {
            this.hitboxLayerMask = hitboxLayerMask;
            this.blockerLayerMask = blockerLayerMask;

            if (!IsValid)
            {
                throw new ArgumentException(
                    "Projectile world settings require separate non-empty hitbox/blocker layer masks.");
            }
        }

        public int HitboxLayerMask => hitboxLayerMask;
        public int BlockerLayerMask => blockerLayerMask;
        public int PhysicsLayerMask => hitboxLayerMask | blockerLayerMask;
        public bool IsValid => hitboxLayerMask != 0
            && blockerLayerMask != 0
            && (hitboxLayerMask & blockerLayerMask) == 0;

        public static UnityProjectileWorldSettings Default => new UnityProjectileWorldSettings(
            1 << 29,
            1 << 28);
    }

    public sealed class UnityProjectileWorldPort : IProjectileWorldPort
    {
        private readonly HitboxRegistry registry;
        private readonly Transform playerAnchor;
        private Transform enemyAnchor;
        private Transform enemyProjectileSpawnAnchor;
        private readonly UnityProjectileWorldSettings settings;
        private readonly IUnityPhysicsQueryBackend physics;
        private readonly UnityPhysicsHit[] hitBuffer;
        private readonly ProjectileSlot[] slots;
        private readonly ProjectileCollisionProxyPool collisionProxyPool;

        private RuntimeId playerRuntimeId;
        private RuntimeId enemyRuntimeId;
        private bool sessionBound;
        private int activeCount;

        public UnityProjectileWorldPort(
            HitboxRegistry registry,
            IUnityPhysicsQueryBackend physics,
            Transform playerAnchor,
            Transform enemyAnchor,
            UnityProjectileWorldSettings settings,
            int projectileCapacity,
            ProjectileCollisionProxyPool collisionProxyPool = null)
            : this(
                registry,
                playerAnchor,
                enemyAnchor,
                enemyAnchor,
                settings,
                projectileCapacity,
                physics,
                collisionProxyPool)
        {
        }

        public UnityProjectileWorldPort(
            HitboxRegistry registry,
            IUnityPhysicsQueryBackend physics,
            Transform playerAnchor,
            Transform enemyAnchor,
            Transform enemyProjectileSpawnAnchor,
            UnityProjectileWorldSettings settings,
            int projectileCapacity,
            ProjectileCollisionProxyPool collisionProxyPool = null)
            : this(
                registry,
                playerAnchor,
                enemyAnchor,
                enemyProjectileSpawnAnchor,
                settings,
                projectileCapacity,
                physics,
                collisionProxyPool)
        {
        }

        public UnityProjectileWorldPort(
            HitboxRegistry registry,
            Transform playerAnchor,
            Transform enemyAnchor,
            UnityProjectileWorldSettings settings,
            int projectileCapacity,
            IUnityPhysicsQueryBackend physics = null,
            ProjectileCollisionProxyPool collisionProxyPool = null)
            : this(
                registry,
                playerAnchor,
                enemyAnchor,
                enemyAnchor,
                settings,
                projectileCapacity,
                physics,
                collisionProxyPool)
        {
        }

        public UnityProjectileWorldPort(
            HitboxRegistry registry,
            Transform playerAnchor,
            Transform enemyAnchor,
            Transform enemyProjectileSpawnAnchor,
            UnityProjectileWorldSettings settings,
            int projectileCapacity,
            IUnityPhysicsQueryBackend physics = null,
            ProjectileCollisionProxyPool collisionProxyPool = null)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (playerAnchor == null)
            {
                throw new ArgumentNullException(nameof(playerAnchor));
            }

            if (enemyAnchor == null)
            {
                throw new ArgumentNullException(nameof(enemyAnchor));
            }

            if (playerAnchor == enemyAnchor)
            {
                throw new ArgumentException("Player and enemy projectile anchors must be distinct.");
            }

            if (!settings.IsValid)
            {
                throw new ArgumentException("Projectile world settings are invalid.", nameof(settings));
            }

            if (projectileCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileCapacity));
            }

            if (collisionProxyPool != null
                && (collisionProxyPool.Capacity != projectileCapacity
                    || collisionProxyPool.HitboxLayerMask != settings.HitboxLayerMask))
            {
                throw new ArgumentException(
                    "Projectile collision proxy pool capacity and hitbox layer must match the projectile world settings.",
                    nameof(collisionProxyPool));
            }

            this.registry = registry;
            this.playerAnchor = playerAnchor;
            this.enemyAnchor = enemyAnchor;
            this.enemyProjectileSpawnAnchor = enemyProjectileSpawnAnchor ?? enemyAnchor;
            this.settings = settings;
            this.physics = physics ?? new UnityPhysicsQueryBackend();
            if (this.physics.Capacity < SpatialContract.AttackQueryCandidateCapacity)
            {
                throw new ArgumentException(
                    "The Physics backend capacity is below the spatial query contract capacity.",
                    nameof(physics));
            }

            hitBuffer = new UnityPhysicsHit[SpatialContract.AttackQueryCandidateCapacity];
            slots = new ProjectileSlot[projectileCapacity];
            this.collisionProxyPool = collisionProxyPool;
        }

        public int Capacity => slots.Length;
        public int ActiveCount => activeCount;
        public bool IsSessionBound => sessionBound;
        public RuntimeId PlayerRuntimeId => playerRuntimeId;
        public RuntimeId EnemyRuntimeId => enemyRuntimeId;
        public ProjectileCollisionProxyPool CollisionProxyPool => collisionProxyPool;
        public Transform EnemyAnchor => enemyAnchor;
        public Transform EnemyProjectileSpawnAnchor => enemyProjectileSpawnAnchor;

        public bool BindSession(RuntimeId playerId, RuntimeId enemyId)
        {
            return BindSession(playerId, enemyId, out string ignored);
        }

        public bool BindSession(RuntimeId playerId, RuntimeId enemyId, out string error)
        {
            if (!playerId.IsValid || !enemyId.IsValid || playerId == enemyId)
            {
                error = "Player and enemy RuntimeIds must be valid and distinct.";
                return false;
            }

            if (!AreRegistrationAnchorsReady)
            {
                error = "Registry and projectile anchors must be ready before binding a session.";
                return false;
            }

            if (sessionBound)
            {
                bool sameSession = playerRuntimeId == playerId && enemyRuntimeId == enemyId;
                error = sameSession
                    ? string.Empty
                    : "Projectile world is bound to another session; reset it before rebinding.";
                return sameSession;
            }

            if (collisionProxyPool != null
                && !collisionProxyPool.TryPrepare(registry, out error))
            {
                return false;
            }

            playerRuntimeId = playerId;
            enemyRuntimeId = enemyId;
            sessionBound = true;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Switches the owner/target lookup for future projectile requests to
        /// a newly spawned enemy. Existing slots are deliberately preserved:
        /// their paths were quantized at registration and must never start
        /// following the replacement enemy.
        /// </summary>
        public bool TryRebindEnemyRuntimeId(
            RuntimeId nextEnemyRuntimeId,
            out string error)
        {
            if (!sessionBound)
            {
                error = "Projectile world must be bound to a session before enemy rebinding.";
                return false;
            }

            if (!nextEnemyRuntimeId.IsValid || nextEnemyRuntimeId == playerRuntimeId)
            {
                error = "A spawned enemy RuntimeId must be valid and distinct from the player RuntimeId.";
                return false;
            }

            enemyRuntimeId = nextEnemyRuntimeId;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Rebinds the scene anchors used by future projectile registrations.
        /// Existing projectile slots keep their quantized paths and therefore
        /// do not follow the replacement enemy.
        /// </summary>
        public bool TryRebindEnemyAnchors(
            Transform nextEnemyAnchor,
            Transform nextEnemyProjectileSpawnAnchor,
            out string error)
        {
            if (nextEnemyAnchor == null || nextEnemyProjectileSpawnAnchor == null)
            {
                error = "Replacement enemy entity must provide gameplay and projectile anchors.";
                return false;
            }

            if (nextEnemyAnchor == playerAnchor
                || nextEnemyProjectileSpawnAnchor == playerAnchor
                || nextEnemyAnchor == nextEnemyProjectileSpawnAnchor)
            {
                error = "Replacement enemy anchors must be distinct from the player and each other.";
                return false;
            }

            enemyAnchor = nextEnemyAnchor;
            enemyProjectileSpawnAnchor = nextEnemyProjectileSpawnAnchor;
            error = string.Empty;
            return true;
        }

        public bool ResetForSession(RuntimeId playerId, RuntimeId enemyId)
        {
            return ResetForSession(playerId, enemyId, out string ignored);
        }

        public bool ResetForSession(RuntimeId playerId, RuntimeId enemyId, out string error)
        {
            if (!playerId.IsValid || !enemyId.IsValid || playerId == enemyId)
            {
                error = "Player and enemy RuntimeIds must be valid and distinct.";
                return false;
            }

            if (!AreRegistrationAnchorsReady)
            {
                error = "Registry and projectile anchors must be ready before resetting a session.";
                return false;
            }

            if (activeCount > 0)
            {
                error = "Active projectile proxies must be released before resetting the session.";
                return false;
            }

            if (collisionProxyPool != null && collisionProxyPool.ActiveCount > 0)
            {
                error = "Active projectile collision proxies must be released before resetting the session.";
                return false;
            }

            ClearSlots();
            sessionBound = false;
            playerRuntimeId = RuntimeId.Invalid;
            enemyRuntimeId = RuntimeId.Invalid;
            return BindSession(playerId, enemyId, out error);
        }

        public DomainResult Register(
            in ProjectileSpawnRequest request,
            out ProjectilePathSnapshot path)
        {
            path = default(ProjectilePathSnapshot);
            if (!IsRegistrationOperational || !IsSpawnRequestValid(request))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!TryGetProjectileOrigin(request.OwnerId, out Transform owner)
                || !TryGetTargetAnchor(request.TargetId, out Transform target)
                || request.OwnerId == request.TargetId)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            Team expectedOwnerTeam = request.OwnerId == playerRuntimeId
                ? Team.Player
                : Team.Enemy;
            if (request.Team != expectedOwnerTeam)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            int reusableIndex = -1;
            for (int index = 0; index < slots.Length; index++)
            {
                ProjectileSlot slot = slots[index];
                if (slot.State == ProjectileSlotState.None)
                {
                    if (reusableIndex < 0)
                    {
                        reusableIndex = index;
                    }

                    continue;
                }

                bool sameProjectileId = slot.ProjectileId == request.ProjectileId;
                bool sameRuntimeId = slot.RuntimeId == request.RuntimeId;
                if (sameProjectileId && sameRuntimeId)
                {
                    return DomainResult.Rejected(slot.State == ProjectileSlotState.Released
                        ? RejectReason.AlreadyTerminal
                        : RejectReason.InvalidState);
                }

                if (sameProjectileId || sameRuntimeId)
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                if (slot.State == ProjectileSlotState.Released && reusableIndex < 0)
                {
                    reusableIndex = index;
                }
            }

            if (activeCount >= slots.Length || reusableIndex < 0)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            if (!TryQuantizePosition(owner.position, out SpatialVectorKey start)
                || !TryQuantizePosition(target.position, out SpatialVectorKey end))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            // A zero-length frozen path cannot be evaluated by SphereCast.  It
            // must not silently become a Missed terminal state at arrival;
            // callers need to author distinct quantized projectile anchors.
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
                if (collisionProxyPool == null)
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                DomainResult acquired = collisionProxyPool.Acquire(request, path);
                if (!acquired.IsSuccess)
                {
                    return acquired;
                }
            }

            slots[reusableIndex] = new ProjectileSlot(
                request.ProjectileId,
                request.RuntimeId,
                request.TargetId,
                request.SweepRadiusKey,
                path,
                request.Interceptable);
            activeCount++;
            return DomainResult.Success;
        }

        public DomainResult Sweep(
            in ProjectileSweepRequest request,
            out ProjectileSweepHit hit)
        {
            hit = ProjectileSweepHit.None;
            if (!IsSweepOperational || !IsSweepRequestValid(request))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            int slotIndex = FindSlot(request.ProjectileId, request.RuntimeId);
            if (slotIndex < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            ProjectileSlot slot = slots[slotIndex];
            if (slot.State == ProjectileSlotState.Released)
            {
                return DomainResult.Rejected(RejectReason.AlreadyTerminal);
            }

            DomainResult segmentResult = slot.Path.TryGetSegment(
                request.Tick,
                out SpatialVectorKey expectedFrom,
                out SpatialVectorKey expectedTo);
            if (!segmentResult.IsSuccess)
            {
                return segmentResult;
            }

            if (request.From != expectedFrom || request.To != expectedTo
                || request.SweepRadiusKey != slot.SweepRadiusKey)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            Vector3 origin = ToPosition(request.From);
            Vector3 destination = ToPosition(request.To);
            Vector3 displacement = destination - origin;
            float maxDistance = displacement.magnitude;
            if (!IsFinite(origin) || !IsFinite(destination) || !IsFinite(maxDistance))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (maxDistance <= 0.0000001f)
            {
                return MoveCollisionProxy(slot, request.To);
            }

            float radius = request.SweepRadiusKey
                / (float)SpatialContract.DistanceUnitsPerMeter;
            if (!IsFinite(radius) || radius <= 0f)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            NonAllocPhysicsQueryResult batch = physics.SphereCastNonAlloc(
                origin,
                radius,
                displacement / maxDistance,
                hitBuffer,
                maxDistance,
                settings.PhysicsLayerMask,
                QueryTriggerInteraction.Collide);
            DomainResult batchValidation = ValidateBatch(batch, hitBuffer.Length);
            if (!batchValidation.IsSuccess)
            {
                return batchValidation;
            }

            bool hasBest = false;
            SweepCandidate best = default(SweepCandidate);
            for (int index = 0; index < batch.Count; index++)
            {
                if (!TryCreateCandidate(
                    hitBuffer[index],
                    slot.TargetId,
                    maxDistance,
                    out SweepCandidate candidate))
                {
                    continue;
                }

                if (!hasBest || CompareCandidate(candidate, best) < 0)
                {
                    best = candidate;
                    hasBest = true;
                }
            }

            if (!hasBest)
            {
                return MoveCollisionProxy(slot, request.To);
            }

            DomainResult moved = MoveCollisionProxy(slot, best.Point);
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
            if (!sessionBound || !IsReleaseRequestValid(request))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            int slotIndex = FindSlot(request.ProjectileId, request.RuntimeId);
            if (slotIndex < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            ProjectileSlot slot = slots[slotIndex];
            if (slot.State == ProjectileSlotState.Released)
            {
                return DomainResult.Rejected(RejectReason.AlreadyTerminal);
            }

            if (request.Tick < slot.Path.SpawnTick)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            if (slot.Interceptable)
            {
                if (collisionProxyPool == null)
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                DomainResult releasedProxy = collisionProxyPool.Release(slot.RuntimeId);
                if (!releasedProxy.IsSuccess)
                {
                    return releasedProxy;
                }
            }

            slot.State = ProjectileSlotState.Released;
            slots[slotIndex] = slot;
            activeCount--;
            return DomainResult.Success;
        }

        private bool AreRegistrationAnchorsReady => registry != null
            && registry.IsReadyForQueries
            && playerAnchor != null
            && enemyAnchor != null
            && enemyProjectileSpawnAnchor != null;

        private bool IsRegistrationOperational => sessionBound
            && AreRegistrationAnchorsReady;

        private bool IsSweepOperational => sessionBound
            && registry != null
            && registry.IsReadyForQueries;

        private void ClearSlots()
        {
            Array.Clear(slots, 0, slots.Length);
            activeCount = 0;
        }

        private DomainResult MoveCollisionProxy(
            in ProjectileSlot slot,
            SpatialVectorKey position)
        {
            if (!slot.Interceptable)
            {
                return DomainResult.Success;
            }

            return collisionProxyPool == null
                ? DomainResult.Rejected(RejectReason.InvalidState)
                : collisionProxyPool.Move(slot.RuntimeId, position);
        }

        private bool TryGetProjectileOrigin(RuntimeId runtimeId, out Transform anchor)
        {
            if (runtimeId == playerRuntimeId)
            {
                anchor = playerAnchor;
                return anchor != null;
            }

            if (runtimeId == enemyRuntimeId)
            {
                anchor = enemyProjectileSpawnAnchor;
                return anchor != null;
            }

            anchor = null;
            return false;
        }

        private bool TryGetTargetAnchor(RuntimeId runtimeId, out Transform anchor)
        {
            if (runtimeId == playerRuntimeId)
            {
                anchor = playerAnchor;
                return anchor != null;
            }

            if (runtimeId == enemyRuntimeId)
            {
                anchor = enemyAnchor;
                return anchor != null;
            }

            anchor = null;
            return false;
        }

        private int FindSlot(ProjectileId projectileId, RuntimeId runtimeId)
        {
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index].State != ProjectileSlotState.None
                    && slots[index].ProjectileId == projectileId
                    && slots[index].RuntimeId == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private bool TryCreateCandidate(
            in UnityPhysicsHit physicsHit,
            RuntimeId frozenTargetId,
            float maxDistance,
            out SweepCandidate candidate)
        {
            candidate = default(SweepCandidate);
            Collider collider = physicsHit.Collider;
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy
                || !registry.TryResolve(collider, out RegisteredHitbox registered)
                || !IsLayerIncluded(collider.gameObject.layer, registered.TargetKind)
                || collider.isTrigger && !registered.AllowTrigger
                || !IsFinite(physicsHit.Distance) || physicsHit.Distance < 0f
                || physicsHit.Distance > maxDistance
                || !IsFinite(physicsHit.Point)
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

            if (registered.RuntimeId != frozenTargetId)
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

        private bool IsLayerIncluded(int layer, QueryTargetKind targetKind)
        {
            int expectedMask = targetKind == QueryTargetKind.EnvironmentBlocker
                ? settings.BlockerLayerMask
                : settings.HitboxLayerMask;
            return layer >= 0 && layer < 32
                && (expectedMask & (1 << layer)) != 0;
        }

        private static DomainResult ValidateBatch(
            in NonAllocPhysicsQueryResult batch,
            int capacity)
        {
            if (batch.Count < 0 || batch.Count > capacity)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            return batch.MayBeTruncated || batch.Count >= capacity
                ? DomainResult.Rejected(RejectReason.BufferCapacity)
                : DomainResult.Success;
        }

        private static int CompareCandidate(in SweepCandidate left, in SweepCandidate right)
        {
            int distance = left.DistanceKey.CompareTo(right.DistanceKey);
            if (distance != 0)
            {
                return distance;
            }

            int kind = KindPriority(left.Kind).CompareTo(KindPriority(right.Kind));
            if (kind != 0)
            {
                return kind;
            }

            int geometry = left.GeometryId.CompareTo(right.GeometryId);
            if (geometry != 0)
            {
                return geometry;
            }

            int target = left.TargetId.CompareTo(right.TargetId);
            if (target != 0)
            {
                return target;
            }

            int hitPart = left.HitPart.CompareTo(right.HitPart);
            if (hitPart != 0)
            {
                return hitPart;
            }

            int pointX = left.Point.X.CompareTo(right.Point.X);
            if (pointX != 0)
            {
                return pointX;
            }

            int pointY = left.Point.Y.CompareTo(right.Point.Y);
            return pointY != 0
                ? pointY
                : left.Point.Z.CompareTo(right.Point.Z);
        }

        private static int KindPriority(ProjectileSweepHitKind kind)
        {
            return kind == ProjectileSweepHitKind.EnvironmentBlocked ? 0 : 1;
        }

        private static bool IsSpawnRequestValid(in ProjectileSpawnRequest request)
        {
            return request.Tick.IsValid
                && request.ArrivalTick.IsValid
                && request.ArrivalTick > request.Tick
                && request.ProjectileId.IsValid
                && request.RuntimeId.IsValid
                && request.AttackId.IsValid
                && request.OwnerId.IsValid
                && request.TargetId.IsValid
                 && request.Team != Team.Neutral
                 && Enum.IsDefined(typeof(Team), request.Team)
                 && (!request.Interceptable || request.Team == Team.Enemy)
                 && request.DefinitionId > 0
                && request.SweepRadiusKey > 0
                && request.PresentationKey > 0;
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

        private static Vector3 ToPosition(SpatialVectorKey key)
        {
            float scale = 1f / SpatialContract.PositionUnitsPerMeter;
            return new Vector3(key.X * scale, key.Y * scale, key.Z * scale);
        }

        private static bool TryQuantizeDistance(float distance, out int key)
        {
            return TryQuantize(distance, SpatialContract.DistanceUnitsPerMeter, out key);
        }

        private static bool TryQuantizePosition(Vector3 position, out SpatialVectorKey key)
        {
            key = default(SpatialVectorKey);
            if (!TryQuantize(position.x, SpatialContract.PositionUnitsPerMeter, out int x)
                || !TryQuantize(position.y, SpatialContract.PositionUnitsPerMeter, out int y)
                || !TryQuantize(position.z, SpatialContract.PositionUnitsPerMeter, out int z))
            {
                return false;
            }

            key = new SpatialVectorKey(x, y, z);
            return true;
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

            key = checked((int)Math.Round(scaled, MidpointRounding.AwayFromZero));
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private enum ProjectileSlotState
        {
            None = 0,
            Active,
            Released
        }

        private struct ProjectileSlot
        {
            public ProjectileSlot(
                ProjectileId projectileId,
                RuntimeId runtimeId,
                RuntimeId targetId,
                int sweepRadiusKey,
                ProjectilePathSnapshot path,
                bool interceptable)
            {
                ProjectileId = projectileId;
                RuntimeId = runtimeId;
                TargetId = targetId;
                SweepRadiusKey = sweepRadiusKey;
                Path = path;
                Interceptable = interceptable;
                State = ProjectileSlotState.Active;
            }

            public ProjectileId ProjectileId;
            public RuntimeId RuntimeId;
            public RuntimeId TargetId;
            public int SweepRadiusKey;
            public ProjectilePathSnapshot Path;
            public bool Interceptable;
            public ProjectileSlotState State;
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
