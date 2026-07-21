using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Unity implementation of the pure spawn-point resolver. Occupancy and
    /// RuntimeId ownership are fixed arrays; selection itself is delegated to
    /// the deterministic FPG.Run selector.
    /// </summary>
    public sealed class FpgRoomSpawnPointResolver : IFpgEncounterSpawnPointResolver
    {
        private const int DefaultCapacity = 64;

        private readonly FpgSpawnPointRuntimeCandidate[] candidates;
        private readonly string[] pointIds;
        private readonly RuntimeId[] occupants;
        private readonly FpgSpawnPointRuntimeCandidate[] selectionBuffer;
        private readonly FpgEncounterProfileData profile;
        private FpgRoomInstance roomInstance;
        private Transform playerAnchor;
        private Transform entryAnchor;
        private FpgEncounterRunContext runContext;
        private int candidateCount;
        private bool configured;

        public FpgRoomSpawnPointResolver(
            FpgEncounterProfileData profile,
            int capacity = DefaultCapacity)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            candidates = new FpgSpawnPointRuntimeCandidate[capacity];
            pointIds = new string[capacity];
            occupants = new RuntimeId[capacity];
            selectionBuffer = new FpgSpawnPointRuntimeCandidate[capacity];
            worldPoses = new Pose[capacity];
        }

        public int CandidateCount => candidateCount;
        public bool IsConfigured => configured;

        public bool TryConfigure(
            FpgRoomDefinition room,
            FpgRoomInstance instance,
            FpgEncounterRunContext context,
            Transform player,
            Transform entry,
            out string error)
        {
            if (room == null || instance == null || !context.IsValid)
            {
                error = "Formal spawn resolver requires room, instance and valid run context.";
                configured = false;
                return false;
            }

            if (room.EnemySpawnPoints.Count > candidates.Length)
            {
                error = "Formal spawn resolver capacity is smaller than room spawn-point count.";
                configured = false;
                return false;
            }

            roomInstance = instance;
            runContext = context;
            playerAnchor = player;
            entryAnchor = entry;
            candidateCount = room.EnemySpawnPoints.Count;
            for (int index = 0; index < candidateCount; index++)
            {
                FpgRoomEnemySpawnPoint marker = room.EnemySpawnPoints[index];
                if (marker == null
                    || string.IsNullOrWhiteSpace(marker.MarkerId)
                    || !instance.TryResolveEnemySpawnPose(marker.MarkerId, out Pose pose))
                {
                    error = $"Formal spawn point {index} is missing or has no world pose.";
                    configured = false;
                    return false;
                }

                FpgEnemyRole role = ToRunRole(marker.Role);
                FpgSpawnPointCandidate point = new FpgSpawnPointCandidate(
                    marker.MarkerId,
                    role,
                    StableHashForId(marker.MarkerId),
                    1);
                pointIds[index] = marker.MarkerId;
                occupants[index] = RuntimeId.Invalid;
                candidates[index] = new FpgSpawnPointRuntimeCandidate(point, 0, 0, 0);
                // Pose is refreshed through the room instance below; the
                // runtime candidate carries the authored distance keys only.
                worldPoses[index] = pose;
            }

            for (int index = candidateCount; index < candidates.Length; index++)
            {
                pointIds[index] = string.Empty;
                occupants[index] = RuntimeId.Invalid;
            }

            configured = true;
            RefreshDistances();
            error = string.Empty;
            return true;
        }

        public void RefreshDistances()
        {
            if (!configured)
            {
                return;
            }

            Vector3 playerPosition = playerAnchor == null
                ? (entryAnchor == null ? Vector3.zero : entryAnchor.position)
                : playerAnchor.position;
            Vector3 entryPosition = entryAnchor == null ? playerPosition : entryAnchor.position;
            for (int index = 0; index < candidateCount; index++)
            {
                FpgSpawnPointRuntimeCandidate source = candidates[index];
                int playerDistance = Mathf.Max(
                    0,
                    Mathf.CeilToInt(Vector3.Distance(worldPoses[index].position, playerPosition)));
                int entryDistance = Mathf.Max(
                    0,
                    Mathf.CeilToInt(Vector3.Distance(worldPoses[index].position, entryPosition)));
                candidates[index] = new FpgSpawnPointRuntimeCandidate(
                    source.Point,
                    playerDistance,
                    entryDistance,
                    occupants[index].IsValid ? 1 : 0);
            }
        }

        public DomainResult TryReserve(
            FpgSpawnEntry entry,
            FpgEncounterRunContext context,
            int attempt,
            out string pointId,
            out int relaxationLevel)
        {
            pointId = string.Empty;
            relaxationLevel = -1;
            if (!configured || !context.IsValid || attempt < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            RefreshDistances();
            Array.Copy(candidates, selectionBuffer, candidateCount);
            FpgSpawnPointSelectionOptions options = new FpgSpawnPointSelectionOptions(
                profile.SpawnSafetyDistanceUnits,
                profile.EntrySafetyDistanceUnits,
                profile.SoftDistanceRelaxationStepUnits,
                profile.SoftDistanceRelaxationAttempts,
                1);
            FpgSpawnPointSelectionResult selected = FpgSpawnPointSelector.Select(
                entry.Role,
                selectionBuffer,
                options,
                context,
                entry.SpawnSequence,
                attempt);
            if (!selected.IsSuccess || selected.CandidateIndex < 0 || selected.CandidateIndex >= candidateCount)
            {
                return selected.Result;
            }

            int index = selected.CandidateIndex;
            if (occupants[index].IsValid)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            occupants[index] = RuntimeId.Invalid;
            pointId = pointIds[index];
            relaxationLevel = selected.RelaxationLevel;
            return DomainResult.Success;
        }

        public bool TryCommitReservation(string pointId, RuntimeId runtimeId)
        {
            if (!runtimeId.IsValid || string.IsNullOrEmpty(pointId))
            {
                return false;
            }

            int index = FindPoint(pointId);
            if (index < 0 || occupants[index].IsValid)
            {
                return false;
            }

            occupants[index] = runtimeId;
            return true;
        }

        public void Release(string pointId, RuntimeId runtimeId)
        {
            int index = FindPoint(pointId);
            if (index < 0)
            {
                return;
            }

            if (!runtimeId.IsValid || occupants[index] == runtimeId)
            {
                occupants[index] = RuntimeId.Invalid;
            }
        }

        public bool TryGetWorldPose(string pointId, out Pose pose)
        {
            int index = FindPoint(pointId);
            if (index >= 0)
            {
                pose = worldPoses[index];
                return true;
            }

            pose = default(Pose);
            return false;
        }

        private readonly Pose[] worldPoses;

        private int FindPoint(string pointId)
        {
            if (string.IsNullOrEmpty(pointId))
            {
                return -1;
            }

            for (int index = 0; index < candidateCount; index++)
            {
                if (string.Equals(pointIds[index], pointId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static FpgEnemyRole ToRunRole(FpgRoomEnemySpawnRole role)
        {
            switch (role)
            {
                case FpgRoomEnemySpawnRole.Melee:
                    return FpgEnemyRole.Melee;
                case FpgRoomEnemySpawnRole.Ranged:
                    return FpgEnemyRole.Ranged;
                case FpgRoomEnemySpawnRole.Support:
                    return FpgEnemyRole.Support;
                default:
                    return FpgEnemyRole.Any;
            }
        }

        private static long StableHashForId(string id)
        {
            ulong hash = StableHash.Mix(0x4650475F53505253UL);
            for (int index = 0; index < id.Length; index++)
            {
                hash = StableHash.Append(hash, id[index]);
            }

            return unchecked((long)(hash & 0x7FFFFFFFFFFFFFFFUL));
        }
    }

    /// <summary>
    /// Fixed-capacity entity port used by FpgEncounterRuntime. It binds pool
    /// instances, explicit anchors and stable hitboxes at Prepare, enables
    /// gameplay and a per-entity health bar at Activate, and never creates or
    /// destroys objects during a battle tick.
    /// </summary>
    public sealed class FpgUnityEncounterEntityPort : IFpgEncounterEntityPort
    {
        private const int DefaultPresentationLeaseTicks = 12;

        private readonly FpgEnemyEntityPool entityPool;
        private readonly FpgCombatantAnchorMap anchorMap;
        private readonly FpgEnemyDefinitionCatalog catalog;
        private readonly FpgRoomSpawnPointResolver resolver;
        private readonly FpgFormalHitboxRegistry hitboxRegistry;
        private readonly FpgOverheadHealthBarPool healthBarPool;
        private readonly RuntimeBinding[] bindings;
        private readonly int presentationLeaseTicks;

        public FpgUnityEncounterEntityPort(
            FpgEnemyEntityPool entityPool,
            FpgCombatantAnchorMap anchorMap,
            FpgEnemyDefinitionCatalog catalog,
            FpgRoomSpawnPointResolver resolver,
            int capacity,
            FpgFormalHitboxRegistry hitboxRegistry = null,
            FpgOverheadHealthBarPool healthBarPool = null,
            int presentationLeaseTicks = DefaultPresentationLeaseTicks)
        {
            this.entityPool = entityPool ?? throw new ArgumentNullException(nameof(entityPool));
            this.anchorMap = anchorMap ?? throw new ArgumentNullException(nameof(anchorMap));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (presentationLeaseTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(presentationLeaseTicks));
            }

            this.hitboxRegistry = hitboxRegistry;
            this.healthBarPool = healthBarPool;
            this.presentationLeaseTicks = presentationLeaseTicks;
            bindings = new RuntimeBinding[capacity];
        }

        public int Capacity => bindings.Length;
        public IFpgFormalHitboxLookup HitboxLookup => hitboxRegistry;

        public DomainResult Prepare(FpgSpawnEntry entry, RuntimeId runtimeId, string pointId)
        {
            if (!runtimeId.IsValid
                || entry.SpawnSequence < 0
                || string.IsNullOrEmpty(pointId)
                || !resolver.TryGetWorldPose(pointId, out Pose pose))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (FindBinding(runtimeId) >= 0)
            {
                return DomainResult.Rejected(RejectReason.DuplicateSequence);
            }

            int bindingIndex = FindFreeBinding();
            if (bindingIndex < 0)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            FpgEnemyDefinition definition = FindDefinition(entry.EnemyDefinitionId);
            if (definition == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            if (!entityPool.TryAcquire(
                    definition,
                    runtimeId,
                    pose,
                    entry.SpawnSequence,
                    out FpgEnemyEntityHandle handle,
                    out _))
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            IFpgFormalEnemyEntityBinder binder = handle.Binder;
            if (!handle.IsValid || binder.HitPartCount <= 0)
            {
                entityPool.TryRelease(runtimeId);
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (hitboxRegistry != null
                && (!hitboxRegistry.IsInitialized
                    || !hitboxRegistry.CanRegister(binder.HitPartCount)))
            {
                entityPool.TryRelease(runtimeId);
                return DomainResult.Rejected(
                    hitboxRegistry.IsInitialized
                        ? RejectReason.BufferCapacity
                        : RejectReason.InvalidState);
            }

            bool anchorRegistered = anchorMap.TryRegister(
                runtimeId,
                binder.GameplayAnchor,
                binder.ProjectileAnchor,
                binder.WeakpointAnchor,
                handle.Instance,
                out _);
            if (!anchorRegistered)
            {
                entityPool.TryRelease(runtimeId);
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            int registeredHitPartCount = 0;
            if (hitboxRegistry != null)
            {
                for (int ordinal = 0; ordinal < binder.HitPartCount; ordinal++)
                {
                    if (!binder.TryGetHitPart(
                            ordinal,
                            out Collider collider,
                            out HitPart hitPart))
                    {
                        RollbackPrepare(runtimeId, pointId, anchorRegistered);
                        return DomainResult.Rejected(RejectReason.InvalidDefinition);
                    }

                    DomainResult registered = hitboxRegistry.TryRegister(
                        runtimeId,
                        entry.SpawnSequence,
                        ordinal,
                        collider,
                        hitPart,
                        out _);
                    if (!registered.IsSuccess)
                    {
                        RollbackPrepare(runtimeId, pointId, anchorRegistered);
                        return registered;
                    }

                    registeredHitPartCount++;
                }
            }

            if (!resolver.TryCommitReservation(pointId, runtimeId))
            {
                RollbackPrepare(runtimeId, pointId, anchorRegistered);
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            bindings[bindingIndex] = new RuntimeBinding(
                runtimeId,
                pointId,
                entry.SpawnSequence,
                definition,
                binder,
                registeredHitPartCount);
            return DomainResult.Success;
        }

        public DomainResult Activate(FpgSpawnEntry entry, RuntimeId runtimeId, string pointId)
        {
            int index = FindBinding(runtimeId);
            if (index < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            RuntimeBinding binding = bindings[index];
            if (binding.SpawnSequence != entry.SpawnSequence
                || !string.Equals(binding.PointId, pointId, StringComparison.Ordinal))
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            if (binding.Activated)
            {
                return DomainResult.Success;
            }

            if (healthBarPool != null)
            {
                if (!healthBarPool.TryBind(
                        runtimeId,
                        binding.Binder.OverheadHealthBarAnchor,
                        binding.Definition.Life,
                        binding.Definition.Life))
                {
                    return DomainResult.Rejected(RejectReason.BufferCapacity);
                }

                binding.HealthBarBound = true;
            }

            if (!entityPool.TrySetGameplayEnabled(runtimeId, true))
            {
                if (binding.HealthBarBound)
                {
                    healthBarPool.TryRelease(runtimeId);
                    binding.HealthBarBound = false;
                }

                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            binding.Activated = true;
            bindings[index] = binding;
            return DomainResult.Success;
        }

        public DomainResult Despawn(RuntimeId runtimeId, bool preservePresentationLease)
        {
            int index = FindBinding(runtimeId);
            if (index < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            RuntimeBinding binding = bindings[index];

            // Capture/freeze the last gameplay pose before disabling or
            // recycling the pooled actor.
            bool anchorRemoved = anchorMap.TryUnregister(
                runtimeId,
                preservePresentationLease,
                preservePresentationLease ? presentationLeaseTicks : 0);
            bool gameplayDisabled = entityPool.TrySetGameplayEnabled(runtimeId, false);

            int removedHitParts = hitboxRegistry == null
                ? 0
                : hitboxRegistry.Unregister(runtimeId);
            bool hitboxesRemoved = hitboxRegistry == null
                || removedHitParts == binding.RegisteredHitPartCount;

            bool healthBarRemoved = !binding.HealthBarBound
                || healthBarPool != null && healthBarPool.TryRelease(runtimeId);

            // All external references are gone before the entity becomes
            // available to another RuntimeId.
            bool entityReleased = entityPool.TryRelease(runtimeId);
            resolver.Release(binding.PointId, runtimeId);
            bindings[index] = default(RuntimeBinding);

            return anchorRemoved
                && gameplayDisabled
                && hitboxesRemoved
                && healthBarRemoved
                && entityReleased
                    ? DomainResult.Success
                    : DomainResult.Rejected(RejectReason.InvariantFault);
        }

        public bool TryUpdateHealth(RuntimeId runtimeId, int life, int maxLife)
        {
            int index = FindBinding(runtimeId);
            return index >= 0
                && bindings[index].Activated
                && bindings[index].HealthBarBound
                && healthBarPool != null
                && maxLife > 0
                && healthBarPool.TryUpdate(runtimeId, life, maxLife);
        }

        public void ClearAll()
        {
            for (int index = 0; index < bindings.Length; index++)
            {
                RuntimeId runtimeId = bindings[index].RuntimeId;
                if (runtimeId.IsValid)
                {
                    Despawn(runtimeId, false);
                }
            }
        }

        private void RollbackPrepare(
            RuntimeId runtimeId,
            string pointId,
            bool anchorRegistered)
        {
            if (hitboxRegistry != null)
            {
                hitboxRegistry.Unregister(runtimeId);
            }

            if (anchorRegistered)
            {
                anchorMap.TryUnregister(runtimeId, false, 0);
            }

            entityPool.TryRelease(runtimeId);
            resolver.Release(pointId, runtimeId);
        }

        private int FindFreeBinding()
        {
            for (int index = 0; index < bindings.Length; index++)
            {
                if (!bindings[index].RuntimeId.IsValid)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindBinding(RuntimeId runtimeId)
        {
            if (!runtimeId.IsValid)
            {
                return -1;
            }

            for (int index = 0; index < bindings.Length; index++)
            {
                if (bindings[index].RuntimeId == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private FpgEnemyDefinition FindDefinition(string id)
        {
            IReadOnlyList<FpgEnemyDefinition> definitions = catalog.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                if (definitions[index] != null
                    && string.Equals(
                        definitions[index].EnemyDefinitionId,
                        id,
                        StringComparison.Ordinal))
                {
                    return definitions[index];
                }
            }

            return null;
        }

        private struct RuntimeBinding
        {
            public RuntimeBinding(
                RuntimeId runtimeId,
                string pointId,
                int spawnSequence,
                FpgEnemyDefinition definition,
                IFpgFormalEnemyEntityBinder binder,
                int registeredHitPartCount)
            {
                RuntimeId = runtimeId;
                PointId = pointId ?? string.Empty;
                SpawnSequence = spawnSequence;
                Definition = definition;
                Binder = binder;
                RegisteredHitPartCount = registeredHitPartCount;
                Activated = false;
                HealthBarBound = false;
            }

            public RuntimeId RuntimeId;
            public string PointId;
            public int SpawnSequence;
            public FpgEnemyDefinition Definition;
            public IFpgFormalEnemyEntityBinder Binder;
            public int RegisteredHitPartCount;
            public bool Activated;
            public bool HealthBarBound;
        }
    }
}

