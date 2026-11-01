using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using FPG.Demo.Skills;
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
    public sealed class FpgUnityEncounterEntityPort :
        IFpgEncounterEntityPort,
        IFpgFormalEnemyPresentationPort,
        IFpgFormalEnemyMotionAuthority,
        IFpgEnemyHitReactionPort
    {
        private const int DefaultPresentationLeaseTicks = 12;

        private readonly FpgEnemyEntityPool entityPool;
        private readonly FpgCombatantAnchorMap anchorMap;
        private readonly FpgEnemyDefinitionCatalog catalog;
        private readonly FpgRoomSpawnPointResolver resolver;
        private readonly FpgFormalHitboxRegistry hitboxRegistry;
        private readonly FpgOverheadHealthBarPool healthBarPool;
        private readonly RuntimeBinding[] bindings;
        private readonly Vector3[] agentPositions;
        private readonly bool[] agentActive;
        private readonly IFpgEnemyMovementExecutor[] movementExecutors;
        private readonly bool[] externalMovementActive;
        private readonly string[] movementAnimationNames;
        private readonly TickIndex[] movementAnimationStartTicks;
        private readonly int presentationLeaseTicks;
        private readonly FpgRoomNavigationVolume navigationVolume;
        private readonly Transform playerAnchor;
        private readonly Camera worldCamera;

        public FpgUnityEncounterEntityPort(
            FpgEnemyEntityPool entityPool,
            FpgCombatantAnchorMap anchorMap,
            FpgEnemyDefinitionCatalog catalog,
            FpgRoomSpawnPointResolver resolver,
            int capacity,
            FpgFormalHitboxRegistry hitboxRegistry = null,
            FpgOverheadHealthBarPool healthBarPool = null,
            int presentationLeaseTicks = DefaultPresentationLeaseTicks,
            FpgRoomNavigationVolume navigationVolume = null,
            Transform playerAnchor = null,
            Camera worldCamera = null)
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
            this.navigationVolume = navigationVolume;
            this.playerAnchor = playerAnchor;
            this.worldCamera = worldCamera;
            bindings = new RuntimeBinding[capacity];
            agentPositions = new Vector3[capacity];
            agentActive = new bool[capacity];
            movementExecutors = new IFpgEnemyMovementExecutor[capacity];
            externalMovementActive = new bool[capacity];
            movementAnimationNames = new string[capacity];
            movementAnimationStartTicks = new TickIndex[capacity];
            for (int index = 0; index < capacity; index++)
            {
                movementAnimationStartTicks[index] = TickIndex.Invalid;
            }
        }

        public int Capacity => bindings.Length;
        public IFpgFormalHitboxLookup HitboxLookup => hitboxRegistry;
        public int HealthBarBindFailureCount { get; private set; }
        public int HealthBarUpdateFailureCount { get; private set; }
        public int HealthBarReleaseFailureCount { get; private set; }
        public int HealthBarCallbackFaultCount { get; private set; }
        public int EnemyPresentationFailureCount { get; private set; }
        public string LastMotionFaultDiagnostic { get; private set; } = string.Empty;

        /// <summary>
        /// Raised after a committed enemy hit has been delivered to the
        /// presentation view. The formal AI coordinator subscribes to this
        /// event so HitReact is driven by the authoritative combat hit rather
        /// than by a view-local callback.
        /// </summary>
        public event Action<RuntimeId, TickIndex> CommittedEnemyHit;

        public bool TryResolvePresentationSource(
            RuntimeId runtimeId,
            int spawnSequence,
            string socketId,
            out Transform source)
        {
            source = null;
            int index = FindBinding(runtimeId);
            if (index < 0)
            {
                return false;
            }

            RuntimeBinding binding = bindings[index];
            if (!binding.Activated
                || binding.SpawnSequence != spawnSequence
                || binding.Binder == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(socketId))
            {
                source = binding.Binder.GameplayAnchor;
                return source != null;
            }

            D0ActorSocketRegistry sockets = binding.Binder.SocketRegistry;
            return sockets != null
                && sockets.TryResolve(socketId, out source)
                && source != null;
        }

        public DomainResult Prepare(
            FpgSpawnEntry entry,
            RuntimeId runtimeId,
            FpgSpawnPlacement placement)
        {
            if (!runtimeId.IsValid
                || entry.SpawnSequence < 0
                || !placement.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            DomainResult poseResult = TryResolvePlacementPose(
                placement,
                out Pose pose);
            if (!poseResult.IsSuccess)
            {
                return poseResult;
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
                    out string acquireError))
            {
                Debug.LogError(
                    $"[FpgUnityEncounterEntityPort] Prepare acquire failed for "
                    + $"'{definition.EnemyDefinitionId}' runtime={runtimeId} "
                    + $"sequence={entry.SpawnSequence}: {acquireError}",
                    this.entityPool);
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
                binder.SocketRegistry,
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
                        RollbackPrepare(runtimeId, anchorRegistered);
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
                        RollbackPrepare(runtimeId, anchorRegistered);
                        return registered;
                    }

                    registeredHitPartCount++;
                }
            }

            if (placement.RequiresRoomReservation
                && !resolver.TryCommitReservation(
                    placement.RoomPointId,
                    runtimeId))
            {
                RollbackPrepare(runtimeId, anchorRegistered);
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            bindings[bindingIndex] = new RuntimeBinding(
                runtimeId,
                placement,
                entry.SpawnSequence,
                definition,
                binder,
                registeredHitPartCount);
            movementExecutors[bindingIndex] = null;
            externalMovementActive[bindingIndex] = false;
            movementAnimationNames[bindingIndex] = string.Empty;
            movementAnimationStartTicks[bindingIndex] = TickIndex.Invalid;
            return DomainResult.Success;
        }

        public DomainResult Activate(
            FpgSpawnEntry entry,
            RuntimeId runtimeId,
            FpgSpawnPlacement placement)
        {
            int index = FindBinding(runtimeId);
            if (index < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            RuntimeBinding binding = bindings[index];
            if (binding.SpawnSequence != entry.SpawnSequence
                || binding.Placement != placement)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            if (binding.Activated)
            {
                return DomainResult.Success;
            }

            bool requiresExternalMovement = binding.Definition != null
                && binding.Definition.Behavior != null
                && binding.Definition.Behavior.UsesExternalMovement;
            if (requiresExternalMovement
                && (!(binding.Binder is IFpgEnemyMovementSnapshotReceiver)
                    || !(binding.Binder
                        is IFpgFormalEnemyExternalMotionView)))
            {
                Debug.LogError(
                    $"[FpgUnityEncounterEntityPort] Enemy "
                    + $"{runtimeId} has no external movement presentation contract.",
                    binding.Binder as UnityEngine.Object);
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (healthBarPool != null)
            {
                if (TryBindHealthBar(
                        runtimeId,
                        binding.Binder.OverheadHealthBarAnchor,
                        binding.Definition.Life,
                        binding.Definition.Life))
                {
                    binding.HealthBarBound = true;
                }
                else
                {
                    HealthBarBindFailureCount++;
                }
            }

            if (!entityPool.TrySetGameplayEnabled(runtimeId, true))
            {
                if (binding.HealthBarBound)
                {
                    if (!TryReleaseHealthBar(runtimeId))
                    {
                        HealthBarReleaseFailureCount++;
                    }
                    binding.HealthBarBound = false;
                }

                Debug.LogError(
                    $"[FpgUnityEncounterEntityPort] Activate failed to enable "
                    + $"runtime={runtimeId} sequence={entry.SpawnSequence}.",
                    this.entityPool);
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            if (requiresExternalMovement
                && !TryEnableExternalMovement(runtimeId))
            {
                entityPool.TrySetGameplayEnabled(runtimeId, false);
                if (binding.HealthBarBound)
                {
                    if (!TryReleaseHealthBar(runtimeId))
                    {
                        HealthBarReleaseFailureCount++;
                    }
                    binding.HealthBarBound = false;
                }

                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            binding.Activated = true;
            bindings[index] = binding;
            return DomainResult.Success;
        }

        public bool TryApplySkillSequenceFrame(
            in FpgFormalEnemySkillSequenceFrame frame)
        {
            int index = FindBinding(frame.OwnerRuntimeId);
            if (index < 0
                || frame.SpawnSequence < 0
                || frame.Definition == null)
            {
                IncrementEnemyPresentationFailureCount();
                return false;
            }

            RuntimeBinding binding = bindings[index];
            if (!binding.Activated
                || binding.SpawnSequence != frame.SpawnSequence
                || !(binding.Binder
                    is IFpgFormalEnemyPresentationView view))
            {
                IncrementEnemyPresentationFailureCount();
                return false;
            }

            try
            {
                if (view.TrySetSkillSequenceFrame(frame))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // Presentation callbacks cannot affect encounter simulation.
            }

            IncrementEnemyPresentationFailureCount();
            return false;
        }

        /// <summary>
        /// Enables the shared external movement contract. Both Sprite and
        /// Spine views apply the resulting snapshot; neither chooses goals.
        /// </summary>
        public bool TryEnableExternalMovement(RuntimeId runtimeId)
        {
            int index = FindBinding(runtimeId);
            if (index < 0)
            {
                return false;
            }

            RuntimeBinding binding = bindings[index];
            if (binding.Definition == null
                || binding.Definition.Behavior == null
                || !binding.Definition.Behavior.UsesExternalMovement
                || !(binding.Binder is IFpgEnemyMovementSnapshotReceiver)
                || !(binding.Binder is IFpgFormalEnemyExternalMotionView))
            {
                return false;
            }

            if (movementExecutors[index] == null)
            {
                FpgEnemyBehaviorDefinition behavior =
                    binding.Definition.Behavior;
                if (behavior.UsesPositionQueryMovement)
                {
                    movementExecutors[index] =
                        new FpgEnemyPositionQueryMovementExecutor(
                            behavior.PositionQuery);
                }
                else if (behavior.UsesLateralChaseMovement)
                {
                    movementExecutors[index] =
                        new FpgEnemyLateralChaseMovementExecutor(behavior);
                }
                else
                {
                    movementExecutors[index] =
                        new FpgEnemyStationaryMovementExecutor();
                }
            }

            externalMovementActive[index] = true;
            return true;
        }

        public void ClearExternalMovementGoal(RuntimeId runtimeId)
        {
            int index = FindBinding(runtimeId);
            if (index < 0 || !externalMovementActive[index])
            {
                return;
            }

            movementExecutors[index]?.ClearGoal();
        }

        public bool TryGetExternalMovementGoal(
            RuntimeId runtimeId,
            out FpgEnemyAiMovementGoal goal)
        {
            int index = FindBinding(runtimeId);
            if (index < 0
                || !externalMovementActive[index]
                || movementExecutors[index] == null
                || !movementExecutors[index].HasGoal)
            {
                goal = FpgEnemyAiMovementGoal.Invalid;
                return false;
            }

            Vector3 value = movementExecutors[index].Goal;
            goal = new FpgEnemyAiMovementGoal(
                value.x,
                value.y,
                value.z);
            return true;
        }

        public void ApplyAiMovementPresentation(
            RuntimeId runtimeId,
            TickIndex tick,
            bool moving)
        {
            int index = FindBinding(runtimeId);
            if (index < 0
                || !externalMovementActive[index]
                || !tick.IsValid
                || !bindings[index].Activated
                || !(bindings[index].Binder
                    is IFpgEnemyMovementSnapshotReceiver receiver))
            {
                return;
            }

            FpgEnemyBehaviorDefinition behavior =
                bindings[index].Definition.Behavior;
            string animationName = moving
                && !string.IsNullOrWhiteSpace(behavior.MovementAnimation)
                    ? behavior.MovementAnimation
                    : behavior.IdleAnimation;
            if (string.IsNullOrWhiteSpace(animationName))
            {
                return;
            }

            if (!string.Equals(
                    movementAnimationNames[index],
                    animationName,
                    StringComparison.Ordinal)
                || !movementAnimationStartTicks[index].IsValid)
            {
                movementAnimationNames[index] = animationName;
                movementAnimationStartTicks[index] = tick;
            }

            float elapsedSeconds = (float)(tick.Value
                - movementAnimationStartTicks[index].Value)
                / FpgSkillRuntimeConstants.TickRate;
            DomainResult presented = receiver.ApplyMovementAnimation(
                animationName,
                Mathf.Max(0f, elapsedSeconds));
            if (!presented.IsSuccess)
            {
                IncrementEnemyPresentationFailureCount();
            }
        }

        public bool TryAdvanceExternalMovement(
            RuntimeId runtimeId,
            TickIndex tick,
            in FpgEnemyAiMoveRequest request,
            out FpgEnemyAiMovementState state)
        {
            state = FpgEnemyAiMovementState.Idle;
            int index = FindBinding(runtimeId);
            if (index < 0 || !externalMovementActive[index]
                || movementExecutors[index] == null)
            {
                return false;
            }

            RuntimeBinding binding = bindings[index];
            if (!binding.Activated
                || binding.Binder == null
                || binding.Binder.GameplayAnchor == null
                || !(binding.Binder
                    is IFpgEnemyMovementSnapshotReceiver receiver))
            {
                state = FpgEnemyAiMovementState.Faulted;
                return true;
            }

            movementExecutors[index].ConfigureContext(
                new FpgEnemyMovementContext(
                    navigationVolume,
                    playerAnchor,
                    worldCamera,
                    agentPositions,
                    agentActive,
                    bindings.Length,
                    index));
            FpgEnemyMovementRequest movementRequest =
                new FpgEnemyMovementRequest(
                    allowMovement: true,
                    requestReposition: true,
                    moveSpeed: request.MoveSpeed,
                    resetGoal: request.ForceReposition);
            FpgEnemyMovementSnapshot snapshot =
                movementExecutors[index].Advance(
                    tick,
                    binding.Binder.GameplayAnchor.position,
                    in movementRequest);
            DomainResult applied = receiver.ApplyMovementSnapshot(in snapshot);
            if (!applied.IsSuccess
                || !anchorMap.TryUpdatePose(runtimeId))
            {
                state = FpgEnemyAiMovementState.Faulted;
                return true;
            }

            agentPositions[index] = binding.Binder.GameplayAnchor.position;
            state = snapshot.State == FpgEnemyMovementExecutionState.Moving
                ? FpgEnemyAiMovementState.Moving
                : snapshot.State == FpgEnemyMovementExecutionState.NoValidPosition
                    ? FpgEnemyAiMovementState.NoValidPosition
                    : snapshot.State == FpgEnemyMovementExecutionState.Blocked
                        ? FpgEnemyAiMovementState.NoValidPosition
                        : FpgEnemyAiMovementState.Idle;
            return true;
        }

        public void DisableExternalMovement(RuntimeId runtimeId)
        {
            int index = FindBinding(runtimeId);
            if (index < 0)
            {
                return;
            }

            externalMovementActive[index] = false;
            movementExecutors[index]?.Reset();
            movementAnimationNames[index] = string.Empty;
            movementAnimationStartTicks[index] = TickIndex.Invalid;
        }

        public DomainResult AdvanceMotion(TickIndex tick)
        {
            LastMotionFaultDiagnostic = string.Empty;
            if (!tick.IsValid)
            {
                LastMotionFaultDiagnostic = "AdvanceMotion rejected an invalid tick.";
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            for (int index = 0; index < bindings.Length; index++)
            {
                RuntimeBinding binding = bindings[index];
                agentActive[index] = binding.Activated
                    && binding.Binder != null
                    && binding.Binder.GameplayAnchor != null;
                if (agentActive[index])
                {
                    agentPositions[index] =
                        binding.Binder.GameplayAnchor.position;
                }
            }

            for (int index = 0; index < bindings.Length; index++)
            {
                RuntimeBinding binding = bindings[index];
                // Prepared pool slots are already bound so their presentation
                // can be sampled during the warning window, but they are not
                // part of the encounter until activation. Sprite views keep
                // their renderer animated here while gameplay remains
                // disabled; only active entities may move or mutate anchors.
                if (!binding.RuntimeId.IsValid)
                {
                    continue;
                }

                if (!binding.Activated)
                {
                    if (binding.Binder
                            is IFpgFormalEnemyExternalMotionView preparedView)
                    {
                        try
                        {
                            preparedView.AdvanceFormalPresentation(tick);
                        }
                        catch (Exception)
                        {
                            // Warning presentation is best-effort and must
                            // not affect activation or encounter state.
                        }
                    }

                    continue;
                }

                if (!(binding.Binder
                    is IFpgFormalEnemyExternalMotionView externalView))
                {
                    LastMotionFaultDiagnostic =
                        $"AdvanceMotion binding {index} has no external presentation view.";
                    return DomainResult.Rejected(
                        RejectReason.InvalidDefinition);
                }

                DomainResult advanced;
                try
                {
                    advanced = externalView.AdvanceFormalPresentation(tick);
                }
                catch (Exception exception)
                {
                    LastMotionFaultDiagnostic =
                        $"AdvanceMotion binding {index} threw {exception.GetType().Name}: {exception.Message}";
                    return DomainResult.Rejected(
                        RejectReason.InvariantFault);
                }

                if (!advanced.IsSuccess)
                {
                    LastMotionFaultDiagnostic =
                        $"AdvanceMotion binding {index} rejected {advanced.RejectReason}.";
                    return advanced;
                }

                if (!anchorMap.TryUpdatePose(binding.RuntimeId))
                {
                    LastMotionFaultDiagnostic =
                        $"AdvanceMotion binding {index} could not update anchor pose for {binding.RuntimeId}.";
                    return DomainResult.Rejected(
                        RejectReason.InvariantFault);
                }

                if (binding.Binder.GameplayAnchor != null)
                {
                    agentPositions[index] =
                        binding.Binder.GameplayAnchor.position;
                }
            }

            return DomainResult.Success;
        }

        public DomainResult NotifyCommittedEnemyHit(
            RuntimeId runtimeId,
            TickIndex tick)
        {
            if (!runtimeId.IsValid || !tick.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            int index = FindBinding(runtimeId);
            if (index < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            RuntimeBinding binding = bindings[index];
            if (!binding.Activated || binding.Binder == null
                || !binding.Binder.GameplayEnabled)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            try
            {
                // Views without a hit reaction retain their existing combat
                // behavior. Feel remains an independent, optional visual
                // effect. The AI notification below is still emitted for
                // those views because the committed hit is authoritative.
                if (binding.Binder is IFpgFormalEnemyHitView hitView)
                {
                    hitView.NotifyFormalHit();
                }

                // Event subscribers are intentionally isolated from encounter
                // simulation. A faulty observer must not turn a committed
                // damage result into an encounter failure.
                try
                {
                    CommittedEnemyHit?.Invoke(runtimeId, tick);
                }
                catch (Exception exception)
                {
                    LastMotionFaultDiagnostic =
                        $"NotifyCommittedEnemyHit observer threw {exception.GetType().Name}: {exception.Message}";
                }

                return DomainResult.Success;
            }
            catch (Exception exception)
            {
                LastMotionFaultDiagnostic =
                    $"NotifyCommittedEnemyHit binding {index} threw {exception.GetType().Name}: {exception.Message}";
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }
        }

        public bool CanStartAttack(RuntimeId runtimeId)
        {
            return CanEnemyPositionStartAttack(runtimeId)
                && HasEnemyLineOfSight(runtimeId);
        }

        public bool CanEnemyPositionStartAttack(RuntimeId runtimeId)
        {
            int index = FindBinding(runtimeId);
            if (index < 0)
            {
                return false;
            }

            RuntimeBinding binding = bindings[index];
            if (!binding.Activated)
            {
                return false;
            }

            if (externalMovementActive[index]
                && movementExecutors[index] != null
                && binding.Binder != null
                && binding.Binder.GameplayAnchor != null)
            {
                try
                {
                    movementExecutors[index].ConfigureContext(
                        new FpgEnemyMovementContext(
                            navigationVolume,
                            playerAnchor,
                            worldCamera,
                            agentPositions,
                            agentActive,
                            bindings.Length,
                            index));
                    if (!movementExecutors[index].CanStartAttack(
                            binding.Binder.GameplayAnchor.position))
                    {
                        return false;
                    }

                    return true;
                }
                catch (Exception exception)
                {
                    LastMotionFaultDiagnostic =
                        $"CanStartAttack binding {index} threw {exception.GetType().Name}: {exception.Message}";
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Supplies the ground-plane target distance to the AI decision layer.
        /// The port already owns both the player anchor and active enemy binding,
        /// so callers do not need to reach into a view for perception facts.
        /// </summary>
        public bool TryGetEnemyTargetDistance(
            RuntimeId runtimeId,
            out float distance)
        {
            int index = FindBinding(runtimeId);
            if (index < 0
                || playerAnchor == null
                || bindings[index].Binder == null
                || bindings[index].Binder.GameplayAnchor == null)
            {
                distance = 0f;
                return false;
            }

            Vector3 delta = playerAnchor.position
                - bindings[index].Binder.GameplayAnchor.position;
            delta.y = 0f;
            distance = delta.magnitude;
            return !float.IsNaN(distance) && !float.IsInfinity(distance);
        }

        public bool HasEnemyLineOfSight(RuntimeId runtimeId)
        {
            int index = FindBinding(runtimeId);
            if (index < 0
                || playerAnchor == null
                || bindings[index].Binder == null
                || bindings[index].Binder.GameplayAnchor == null)
            {
                return false;
            }

            return navigationVolume == null
                || navigationVolume.HasLineOfSight(
                    bindings[index].Binder.GameplayAnchor.position,
                    playerAnchor.position);
        }


        public DomainResult StartSkillMotion(
            in FpgFormalEnemySkillSequenceFrame frame)
        {
            if (!frame.OwnerRuntimeId.IsValid
                || frame.SpawnSequence < 0
                || frame.Definition == null
                || !frame.StartTick.IsValid
                || frame.Tick != frame.StartTick
                || frame.RelativeTick != 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            int index = FindBinding(frame.OwnerRuntimeId);
            if (index < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            RuntimeBinding binding = bindings[index];
            if (!binding.Activated
                || binding.SpawnSequence != frame.SpawnSequence
                || !(binding.Binder is IFpgFormalEnemyMotionView view))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            try
            {
                DomainResult started = view.StartFormalSkillMotion(frame);
                if (!started.IsSuccess)
                {
                    LastMotionFaultDiagnostic =
                        $"StartSkillMotion binding {index} rejected {started.RejectReason} for {frame.OwnerRuntimeId}.";
                }

                return started;
            }
            catch (Exception exception)
            {
                LastMotionFaultDiagnostic =
                    $"StartSkillMotion binding {index} threw {exception.GetType().Name}: {exception.Message}";
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }
        }

        public DomainResult ApplySkillMotionFrame(
            in FpgFormalEnemySkillSequenceFrame frame)
        {
            if (!frame.OwnerRuntimeId.IsValid
                || frame.SpawnSequence < 0
                || frame.Definition == null
                || !frame.StartTick.IsValid
                || !frame.Tick.IsValid
                || frame.RelativeTick < 0
                || !frame.IsTerminal)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            int index = FindBinding(frame.OwnerRuntimeId);
            if (index < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            RuntimeBinding binding = bindings[index];
            if (!binding.Activated
                || binding.SpawnSequence != frame.SpawnSequence
                || !(binding.Binder is IFpgFormalEnemyMotionView view))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            try
            {
                DomainResult applied = view.ApplyFormalSkillMotionFrame(frame);
                if (!applied.IsSuccess)
                {
                    LastMotionFaultDiagnostic =
                        $"ApplySkillMotionFrame binding {index} rejected {applied.RejectReason} for {frame.OwnerRuntimeId}.";
                }

                return applied;
            }
            catch (Exception exception)
            {
                LastMotionFaultDiagnostic =
                    $"ApplySkillMotionFrame binding {index} threw {exception.GetType().Name}: {exception.Message}";
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }
        }

        public bool TrySetEnemySkillWarning(
            in FpgFormalEnemySkillWarningPresentationEvent warningEvent)
        {
            FpgFormalEnemySkillTimelineEvent timelineEvent =
                warningEvent.TimelineEvent;
            if (!TryGetActivePresentationView(
                    timelineEvent.OwnerRuntimeId,
                    timelineEvent.SpawnSequence,
                    out IFpgFormalEnemyPresentationView view))
            {
                IncrementEnemyPresentationFailureCount();
                return false;
            }

            try
            {
                if (view.TrySetSkillWarning(warningEvent))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // Presentation callbacks cannot affect encounter simulation.
            }

            IncrementEnemyPresentationFailureCount();
            return false;
        }

        public void ClearEnemySkillWarnings()
        {
            for (int index = 0; index < bindings.Length; index++)
            {
                RuntimeBinding binding = bindings[index];
                if (!binding.RuntimeId.IsValid
                    || !(binding.Binder
                        is IFpgFormalEnemyPresentationView view))
                {
                    continue;
                }

                try
                {
                    view.ClearSkillWarnings();
                }
                catch (Exception)
                {
                    IncrementEnemyPresentationFailureCount();
                }
            }
        }

        private bool TryGetActivePresentationView(
            RuntimeId runtimeId,
            int spawnSequence,
            out IFpgFormalEnemyPresentationView view)
        {
            int index = FindBinding(runtimeId);
            if (index >= 0)
            {
                RuntimeBinding binding = bindings[index];
                if (binding.Activated
                    && binding.SpawnSequence == spawnSequence
                    && binding.Binder
                        is IFpgFormalEnemyPresentationView resolved)
                {
                    view = resolved;
                    return true;
                }
            }

            view = null;
            return false;
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
            DisableExternalMovement(runtimeId);

            int removedHitParts = hitboxRegistry == null
                ? 0
                : hitboxRegistry.Unregister(runtimeId);
            bool hitboxesRemoved = hitboxRegistry == null
                || removedHitParts == binding.RegisteredHitPartCount;

            if (binding.HealthBarBound
                && !TryReleaseHealthBar(runtimeId))
            {
                HealthBarReleaseFailureCount++;
            }

            // All external references are gone before the entity becomes
            // available to another RuntimeId.
            bool entityReleased = entityPool.TryRelease(runtimeId);
            movementExecutors[index]?.Reset();
            movementExecutors[index] = null;
            externalMovementActive[index] = false;
            agentActive[index] = false;
            agentPositions[index] = Vector3.zero;
            movementAnimationNames[index] = string.Empty;
            movementAnimationStartTicks[index] = TickIndex.Invalid;
            bindings[index] = default(RuntimeBinding);

            return anchorRemoved
                && gameplayDisabled
                && hitboxesRemoved
                && entityReleased
                    ? DomainResult.Success
                    : DomainResult.Rejected(RejectReason.InvariantFault);
        }

        public bool TryUpdateHealth(RuntimeId runtimeId, int life, int maxLife)
        {
            int index = FindBinding(runtimeId);
            if (index < 0 || !bindings[index].Activated
                || healthBarPool == null || maxLife <= 0)
            {
                HealthBarUpdateFailureCount++;
                return false;
            }

            RuntimeBinding binding = bindings[index];
            if (TryUpdateHealthBar(runtimeId, life, maxLife))
            {
                binding.HealthBarBound = true;
                bindings[index] = binding;
                return true;
            }

            if (TryBindHealthBar(
                    runtimeId,
                    binding.Binder.OverheadHealthBarAnchor,
                    life,
                    maxLife))
            {
                binding.HealthBarBound = true;
                bindings[index] = binding;
                return true;
            }

            binding.HealthBarBound = false;
            bindings[index] = binding;
            HealthBarUpdateFailureCount++;
            return false;
        }

        public void ResynchronizeHealthBars(IFpgVitalsView vitals)
        {
            if (vitals == null)
            {
                HealthBarUpdateFailureCount++;
                return;
            }

            for (int index = 0; index < bindings.Length; index++)
            {
                RuntimeBinding binding = bindings[index];
                if (!binding.RuntimeId.IsValid || !binding.Activated)
                {
                    continue;
                }

                if (!vitals.TryGetLatest(
                        binding.RuntimeId,
                        out FpgVitalsSnapshot snapshot))
                {
                    HealthBarUpdateFailureCount++;
                    continue;
                }

                TryUpdateHealth(
                    binding.RuntimeId,
                    snapshot.Life,
                    snapshot.MaxLife);
            }
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

        private bool TryBindHealthBar(
            RuntimeId runtimeId,
            Transform anchor,
            int life,
            int maxLife)
        {
            if (healthBarPool == null)
            {
                return false;
            }

            try
            {
                return healthBarPool.TryBind(runtimeId, anchor, life, maxLife);
            }
            catch (Exception)
            {
                HealthBarCallbackFaultCount++;
                return false;
            }
        }

        private bool TryUpdateHealthBar(
            RuntimeId runtimeId,
            int life,
            int maxLife)
        {
            if (healthBarPool == null)
            {
                return false;
            }

            try
            {
                return healthBarPool.TryUpdate(runtimeId, life, maxLife);
            }
            catch (Exception)
            {
                HealthBarCallbackFaultCount++;
                return false;
            }
        }

        private bool TryReleaseHealthBar(RuntimeId runtimeId)
        {
            if (healthBarPool == null)
            {
                return false;
            }

            try
            {
                return healthBarPool.TryRelease(runtimeId);
            }
            catch (Exception)
            {
                HealthBarCallbackFaultCount++;
                return false;
            }
        }

        private void IncrementEnemyPresentationFailureCount()
        {
            if (EnemyPresentationFailureCount < int.MaxValue)
            {
                EnemyPresentationFailureCount++;
            }
        }

        private void RollbackPrepare(
            RuntimeId runtimeId,
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
        }

        private DomainResult TryResolvePlacementPose(
            FpgSpawnPlacement placement,
            out Pose pose)
        {
            pose = default(Pose);
            switch (placement.Kind)
            {
                case FpgSpawnPlacementKind.EncounterSpawnPoint:
                case FpgSpawnPlacementKind.SandboxRoomPoint:
                    if (!resolver.TryGetWorldPose(
                            placement.RoomPointId,
                            out pose))
                    {
                        return DomainResult.Rejected(RejectReason.InvalidTarget);
                    }

                    break;

                case FpgSpawnPlacementKind.OwnerPosition:
                    if (!anchorMap.TryGet(
                            placement.SourceRuntimeId,
                            out FpgCombatantAnchorSnapshot owner)
                        || owner.RuntimeId != placement.SourceRuntimeId
                        || owner.Actor == null
                        || owner.GameplayAnchor == null
                        || owner.IsPresentationLeaseActive
                        || !owner.Actor.activeInHierarchy)
                    {
                        return DomainResult.Rejected(RejectReason.InvalidTarget);
                    }

                    Transform ownerRoot = owner.Actor.transform;
                    pose = new Pose(ownerRoot.position, ownerRoot.rotation);
                    break;

                default:
                    return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            return IsFinite(pose)
                ? DomainResult.Success
                : DomainResult.Rejected(RejectReason.InvalidDefinition);
        }

        private static bool IsFinite(Pose pose)
        {
            return IsFinite(pose.position.x)
                && IsFinite(pose.position.y)
                && IsFinite(pose.position.z)
                && IsFinite(pose.rotation.x)
                && IsFinite(pose.rotation.y)
                && IsFinite(pose.rotation.z)
                && IsFinite(pose.rotation.w)
                && pose.rotation.x * pose.rotation.x
                    + pose.rotation.y * pose.rotation.y
                    + pose.rotation.z * pose.rotation.z
                    + pose.rotation.w * pose.rotation.w > 0.000001f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
                FpgSpawnPlacement placement,
                int spawnSequence,
                FpgEnemyDefinition definition,
                IFpgFormalEnemyEntityBinder binder,
                int registeredHitPartCount)
            {
                RuntimeId = runtimeId;
                Placement = placement;
                SpawnSequence = spawnSequence;
                Definition = definition;
                Binder = binder;
                RegisteredHitPartCount = registeredHitPartCount;
                Activated = false;
                HealthBarBound = false;
            }

            public RuntimeId RuntimeId;
            public FpgSpawnPlacement Placement;
            public int SpawnSequence;
            public FpgEnemyDefinition Definition;
            public IFpgFormalEnemyEntityBinder Binder;
            public int RegisteredHitPartCount;
            public bool Activated;
            public bool HealthBarBound;
        }
    }
}
