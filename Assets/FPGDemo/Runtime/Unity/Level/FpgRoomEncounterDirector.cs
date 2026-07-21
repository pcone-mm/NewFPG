
using System;
using System.Collections.Generic;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Unity room boundary for the authoritative pure encounter Session. This
    /// component owns room composition, prewarming, exits and event bridging;
    /// it does not implement a second spawn state machine.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class FpgRoomEncounterDirector : MonoBehaviour
    {
        [Header("Room Runtime")]
        [SerializeField] private FpgRoomInstance roomInstance;
        [SerializeField] private FpgEnemyEntityPool enemyEntityPool;
        [SerializeField] private FpgCombatantAnchorMap combatantAnchorMap;
        [SerializeField] private FpgFormalHitboxRegistry formalHitboxRegistry;
        [SerializeField] private FpgOverheadHealthBarPool overheadHealthBarPool;
        [SerializeField] private Camera overheadHealthBarCamera;

        [Header("Exits")]
        [SerializeField] private FpgRoomExitRuntime[] exitRuntimes =
            Array.Empty<FpgRoomExitRuntime>();
        [SerializeField] private GameObject exitRuntimePrefab;
        [SerializeField] private Transform exitRuntimeRoot;

        [Header("Spatial Anchors")]
        [SerializeField] private Transform playerAnchor;
        [SerializeField] private Transform entrySafetyAnchor;

        [Header("Session Ports")]
        [SerializeField] private MonoBehaviour formalCombatPortFactoryComponent;
        [SerializeField] private MonoBehaviour formalPlayerTickDriverComponent;
        [SerializeField] private FpgFormalAttackRuntimeCatalog formalAttackRuntimeCatalog;
        [SerializeField, Min(0)] private int presentationLeaseTicks = 12;

        private SessionIdAllocator idAllocator;
        private IFpgFormalCombatPortFactory configuredFactory;
        private IFpgFormalPlayerTickDriver configuredPlayerDriver;
        private FpgEncounterSession session;
        private FpgFormalCombatRuntimeBundle combatRuntime;
        private FpgUnityEncounterEntityPort entityPort;
        private FpgRoomSpawnPointResolver spawnResolver;
        private FpgRoomExitRuntime[] activeExits = Array.Empty<FpgRoomExitRuntime>();
        private readonly List<FpgRoomExitRuntime> ownedExitRuntimes =
            new List<FpgRoomExitRuntime>();
        private FpgRoomRunRequest request;
        private FpgEncounterPlan encounterPlan;
        private FpgEnemyDefinitionCatalog enemyCatalog;
        private FpgRoomDefinition roomDefinition;
        private FpgEncounterProfileData profile;
        private TickIndex currentTick = TickIndex.Invalid;
        private bool prepared;
        private bool combatStarted;
        private bool roomClearedRaised;
        private bool disposed;
        [NonSerialized] private D0PlayerEntityView configuredPlayerEntity;
        [NonSerialized] private bool playerBindingConfigured;
        [NonSerialized] private bool playerBindingLocked;

        public FpgEncounterPhase Phase { get; private set; } = FpgEncounterPhase.None;
        public FpgEncounterPlan Plan => encounterPlan;
        public FpgEncounterRunContext RunContext => request.RunContext;
        public FpgEncounterSession Session => session;
        public FpgFormalCombatRuntimeBundle CombatRuntime => combatRuntime;
        public D0PlayerEntityView ConfiguredPlayerEntity => configuredPlayerEntity;
        public Transform PlayerAnchor => playerAnchor;
        public bool HasPlayerBinding => playerBindingConfigured
            && configuredPlayerEntity != null
            && playerAnchor == configuredPlayerEntity.transform;
        public bool IsPlayerBindingLocked => playerBindingLocked;
        public FpgMultiEnemyCombatPort CombatPort =>
            combatRuntime == null ? null : combatRuntime.CombatPort;
        public PlayerRuntime Player => combatRuntime == null ? null : combatRuntime.Player;
        public SessionIdAllocator IdAllocator => idAllocator;
        public bool UsesFormalSession => session != null;
        public bool IsPaused => session != null
            && session.State == FpgEncounterSessionState.Paused;
        public int CurrentWaveIndex => session == null
            ? -1
            : session.Runtime.CurrentWaveIndex;
        public int ActiveEnemyCount => session == null ? 0 : session.Roster.LivingCount;
        public int ActiveCapWeight => session == null ? 0 : session.Roster.ActiveCapWeight;
        public int PendingEntryCount => session == null ? 0 : session.Runtime.PendingSpawnCount;
        public bool IsTerminal => Phase == FpgEncounterPhase.Cleared
            || Phase == FpgEncounterPhase.Failed
            || Phase == FpgEncounterPhase.Faulted
            || Phase == FpgEncounterPhase.Disposed;

        public event Action<FpgEncounterLifecycleEvent> LifecycleEvent;
        public event Action<FpgRoomClearedEvent> RoomCleared;
        public event Action<string> ExitSelected;
        public event Action<FpgEncounterFailureReason, string> Failed;

        public bool TryConfigurePlayer(
            D0PlayerEntityView entity,
            out string error)
        {
            if (disposed || playerBindingLocked || session != null
                || prepared || combatStarted)
            {
                error = "Formal room player binding cannot change after session preparation has begun.";
                return false;
            }

            if (entity == null)
            {
                error = "Formal room encounter director requires an explicit player entity.";
                return false;
            }

            if (!entity.TryValidate(out error))
            {
                return false;
            }

            if (!entity.gameObject.scene.IsValid()
                || entity.gameObject.scene != gameObject.scene)
            {
                error = "Formal room player entity must belong to the director scene.";
                return false;
            }

            if (playerBindingConfigured)
            {
                if (configuredPlayerEntity == entity)
                {
                    error = string.Empty;
                    return true;
                }

                error = "Formal room encounter director player binding is already configured.";
                return false;
            }

            configuredPlayerEntity = entity;
            playerAnchor = entity.transform;
            playerBindingConfigured = true;
            error = string.Empty;
            return true;
        }

        public void ClearPlayerBinding()
        {
            if (session != null || combatRuntime != null
                || prepared || combatStarted)
            {
                ClearPreparedRuntime(disposePools: true);
            }

            configuredPlayerEntity = null;
            playerAnchor = null;
            playerBindingConfigured = false;
            playerBindingLocked = false;
        }

        private void Awake()
        {
            idAllocator = new SessionIdAllocator();
        }

        public bool TryConfigureFormalSessionPorts(
            IFpgFormalCombatPortFactory combatPortFactory,
            IFpgFormalPlayerTickDriver playerTickDriver,
            out string error)

        {
            if (session != null)
            {
                error = "Formal session ports cannot change while a session exists.";
                return false;
            }

            if (combatPortFactory == null || playerTickDriver == null)
            {
                error = "Formal session requires explicit combat factory and player tick driver ports.";
                return false;
            }

            configuredFactory = combatPortFactory;
            configuredPlayerDriver = playerTickDriver;
            error = string.Empty;
            return true;
        }

        public bool TryPrepareSession(
            FpgRoomRunRequest nextRequest,
            FpgEncounterPlan nextPlan,
            FpgEnemyDefinitionCatalog nextEnemyCatalog,
            FpgFormalAttackRuntimeCatalog nextAttackRuntimeCatalog,
            out string error)
        {
            if (disposed)
            {
                error = "Formal room encounter director has been disposed.";
                return false;
            }

            if (!HasPlayerBinding)
            {
                return FailPreparation(
                    FpgEncounterFailureReason.InvalidRequest,
                    "Formal Session requires a composed player binding before preparation.",
                    out error);
            }

            IFpgFormalCombatPortFactory factory = configuredFactory
                ?? formalCombatPortFactoryComponent as IFpgFormalCombatPortFactory;
            IFpgFormalPlayerTickDriver playerDriver = configuredPlayerDriver
                ?? formalPlayerTickDriverComponent as IFpgFormalPlayerTickDriver;
            FpgFormalAttackRuntimeCatalog attackCatalog = nextAttackRuntimeCatalog
                == null ? formalAttackRuntimeCatalog : nextAttackRuntimeCatalog;
            if (factory == null || playerDriver == null || attackCatalog == null)
            {
                return FailPreparation(
                    FpgEncounterFailureReason.InvalidRequest,
                    "Formal Session requires explicit combat factory, player driver and attack runtime catalog.",
                    out error);
            }

            FpgEncounterPreflightResult preflight = FpgEncounterPreflight.Validate(
                nextRequest,
                nextPlan,
                nextEnemyCatalog);
            if (!preflight.IsSuccess)
            {
                return FailPreparation(preflight.FailureReason, preflight.Error, out error);
            }

            profile = nextRequest.EncounterProfile.Data;
            if (!factory.TryValidateCapacity(profile, preflight.Requirements, out error))
            {
                return FailPreparation(FpgEncounterFailureReason.EntityCapacity, error, out error);
            }

            if (!(nextRequest.RoomDefinition is FpgRoomDefinitionSourceAdapter roomSource)
                || roomSource.Room == null || roomInstance == null
                || enemyEntityPool == null || combatantAnchorMap == null
                || formalHitboxRegistry == null || overheadHealthBarPool == null)
            {
                return FailPreparation(
                    FpgEncounterFailureReason.InvalidRequest,
                    "Formal Session is missing an authored room or explicit Unity runtime pools.",
                    out error);
            }

            ClearPreparedRuntime(disposePools: true);
            request = nextRequest;
            encounterPlan = nextPlan;
            enemyCatalog = nextEnemyCatalog;
            roomDefinition = roomSource.Room;
            formalAttackRuntimeCatalog = attackCatalog;
            idAllocator.Reset();
            Phase = FpgEncounterPhase.Preparing;
            currentTick = TickIndex.Invalid;
            EmitLocal(FpgEncounterLifecycleEventType.Preparing);

            if (!roomInstance.TryInitialize(roomDefinition, out error)
                || !TryPrepareExits(out error))
            {
                return FailPreparation(FpgEncounterFailureReason.InvalidRequest, error, out error);
            }

            if (!TryBuildWarmupRequests(
                    preflight.Requirements,
                    out List<FpgEnemyPoolWarmupRequest> warmup,
                    out int requiredAttackPatterns,
                    out int requiredConcurrentHitboxes,
                    out int maxHitPartsPerEntity,
                    out error))
            {
                return FailPreparation(FpgEncounterFailureReason.InvalidPool, error, out error);
            }

            if (requiredAttackPatterns > factory.AttackPatternCapacity)

            {
                return FailPreparation(
                    FpgEncounterFailureReason.EntityCapacity,
                    "Formal attack scheduler pattern or summon-action capacity is below preflight.",
                    out error);
            }

            FpgEncounterCapacityRequirements requirements = preflight.Requirements;
            if (enemyEntityPool.Capacity < requirements.EntitySlots
                || combatantAnchorMap.Capacity < requirements.EntitySlots + 1
                || profile.HitboxCapacity < requiredConcurrentHitboxes
                || formalHitboxRegistry.Capacity < requiredConcurrentHitboxes
                || overheadHealthBarPool.Capacity < requirements.SimultaneousCombatants)
            {
                return FailPreparation(
                    FpgEncounterFailureReason.EntityCapacity,
                    "Formal entity, anchor, hitbox or overhead-bar capacity is below preflight.",
                    out error);
            }

            if (!enemyEntityPool.TryPrewarm(warmup, out error)
                || !combatantAnchorMap.TryInitialize(out error)
                || !formalHitboxRegistry.TryInitialize(out error)
                || !overheadHealthBarPool.TryPrewarm(
                    requirements.SimultaneousCombatants,
                    overheadHealthBarCamera,
                    out error))
            {
                return FailPreparation(FpgEncounterFailureReason.EntityCapacity, error, out error);
            }

            spawnResolver = new FpgRoomSpawnPointResolver(
                profile,
                Math.Max(1, nextRequest.RoomDefinition.SpawnPointCount));
            if (!spawnResolver.TryConfigure(
                    roomDefinition,
                    roomInstance,
                    nextRequest.RunContext,
                    playerAnchor,
                    entrySafetyAnchor,
                    out error))
            {
                return FailPreparation(FpgEncounterFailureReason.MissingSpawnPoint, error, out error);
            }

            entityPort = new FpgUnityEncounterEntityPort(
                enemyEntityPool,
                combatantAnchorMap,
                nextEnemyCatalog,
                spawnResolver,
                requirements.EntitySlots,
                formalHitboxRegistry,
                overheadHealthBarPool,
                presentationLeaseTicks);
            FpgEnemyRoster roster = new FpgEnemyRoster(profile.EnemyRosterCapacity);
            FpgMultiEnemyCombatCapacity combatCapacity = factory.Capacity;
            FpgSummonLedger summonLedger = new FpgSummonLedger(
                combatCapacity.SummonCapacity,
                requirements.SummonUpperBound,
                combatCapacity.MaxSummonRecursionDepth);
            FpgEncounterRuntime runtime = new FpgEncounterRuntime(
                nextPlan,
                profile,
                roster,
                idAllocator,
                nextEnemyCatalog,
                spawnResolver,
                entityPort,
                summonLedger: summonLedger,
                spawnQueueCapacity: Math.Max(1, requirements.EntitySlots));
            if (!factory.TryCreate(
                    idAllocator,
                    runtime,
                    nextRequest.RunContext,
                    combatantAnchorMap,
                    formalHitboxRegistry,
                    attackCatalog,
                    out combatRuntime,
                    out error))
            {
                runtime.Dispose();
                return FailPreparation(FpgEncounterFailureReason.External, error, out error);
            }

            if (!TryValidateGeometryIds(
                    requirements,
                    maxHitPartsPerEntity,
                    out error))
            {
                runtime.Dispose();
                return FailPreparation(
                    FpgEncounterFailureReason.EntityCapacity,
                    error,
                    out error);
            }

            if (!combatRuntime.TryBindPlayerTickDriver(playerDriver, out error))
            {
                runtime.Dispose();
                combatRuntime.Dispose();
                combatRuntime = null;
                return FailPreparation(FpgEncounterFailureReason.External, error, out error);
            }

            session = new FpgEncounterSession(
                nextRequest,
                runtime,
                combatRuntime.Synchronizer,
                combatRuntime.CombatPort,
                HandleLifecycle,
                combatRuntime.PlayerSnapshotPort);
            combatRuntime.CombatPort.EnemyDied += HandleEnemyDied;

            combatRuntime.CombatPort.HealthChanged += HandleHealthChanged;
            playerBindingLocked = true;
            prepared = true;
            combatStarted = false;
            roomClearedRaised = false;
            LockExits(true);
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Compatibility signature only. Formal hosts must supply the concrete
        /// plan/catalog through TryPrepareSession so no duplicate state machine
        /// can be selected accidentally.
        /// </summary>
        public bool TryPrepare(
            FpgRoomRunRequest nextRequest,
            IFpgEncounterPlanView nextPlan,
            IReadOnlyList<FpgEnemyDefinition> definitions,
            out string error)
        {
            error = "Compatibility TryPrepare cannot start formal combat; use TryPrepareSession.";
            return false;
        }

        public bool TryPrepare(
            FpgRoomDefinition room,
            FpgEncounterPlan nextPlan,
            IFpgEncounterProfileSource encounterProfile,
            IFpgEncounterOverrideSource encounterOverride,
            FpgEncounterRunContext context,
            IReadOnlyList<FpgEnemyDefinition> definitions,
            out string error)
        {
            error = "Compatibility TryPrepare cannot start formal combat; use TryPrepareSession.";
            return false;
        }

        public bool TryStart(out string error)
        {
            if (!prepared || combatStarted || session == null || Phase != FpgEncounterPhase.Preparing)
            {
                error = "Formal Session must be prepared exactly once before Start.";
                return false;
            }

            try
            {
                enemyEntityPool.BeginCombat();
                overheadHealthBarPool.BeginCombat();
            }
            catch (Exception exception)
            {
                return FailRuntime(FpgEncounterFailureReason.EntityCapacity, exception.Message, out error);
            }

            DomainResult started = session.Start(new TickIndex(0L));
            if (!started.IsSuccess)
            {
                return FailRuntime(
                    FpgEncounterFailureReason.External,
                    "Formal Session start failed: " + started.RejectReason,
                    out error);
            }

            combatStarted = true;
            currentTick = new TickIndex(0L);
            Phase = session.Runtime.Phase;
            LockExits(true);
            EmitLocal(FpgEncounterLifecycleEventType.ExitLocked);
            error = string.Empty;
            return true;
        }

        public bool Tick(TickIndex tick, out string error)
        {
            error = string.Empty;
            if (!combatStarted || session == null)
            {
                error = "Formal Session is not running.";
                return false;
            }

            if (session.State == FpgEncounterSessionState.Paused)
            {
                return true;
            }

            if (session.State == FpgEncounterSessionState.Cleared)
            {
                return true;
            }

            if (!tick.IsValid || currentTick.IsValid && tick <= currentTick)
            {
                return FailRuntime(
                    FpgEncounterFailureReason.SynchronizerFault,
                    "Formal Session ticks must be strictly increasing.",
                    out error);
            }


            DomainResult advanced = session.Advance(tick);
            currentTick = tick;
            combatantAnchorMap.TickPresentationLeases();
            Phase = session.Runtime.Phase;
            if (!advanced.IsSuccess)
            {
                FpgEncounterFailureReason reason =
                    session.Runtime.FailureReason == FpgEncounterFailureReason.None
                        ? FpgEncounterFailureReason.SynchronizerFault
                        : session.Runtime.FailureReason;
                return FailRuntime(
                    reason,
                    "Formal Session faulted: " + advanced.RejectReason,
                    out error);
            }

            return true;
        }

        public bool TryNotifyEnemyDied(RuntimeId runtimeId, out string error)
        {
            DomainResult result = session == null
                ? DomainResult.Rejected(RejectReason.InvalidState)
                : session.MarkEnemyDead(runtimeId, currentTick);
            error = result.IsSuccess ? string.Empty : result.RejectReason.ToString();
            return result.IsSuccess;
        }

        public bool TryPause(out string error)
        {
            DomainResult result = session == null
                ? DomainResult.Rejected(RejectReason.InvalidState)
                : session.Pause(currentTick);
            if (result.IsSuccess)
            {
                Phase = FpgEncounterPhase.Paused;
            }
            error = result.IsSuccess ? string.Empty : result.RejectReason.ToString();
            return result.IsSuccess;
        }

        public bool TryResume(out string error)
        {
            DomainResult result = session == null
                ? DomainResult.Rejected(RejectReason.InvalidState)
                : session.Resume(currentTick);
            if (result.IsSuccess)
            {
                Phase = session.Runtime.Phase;
            }
            error = result.IsSuccess ? string.Empty : result.RejectReason.ToString();
            return result.IsSuccess;
        }

        public bool TryRestart(out string error)
        {
            if (session == null || combatRuntime == null)
            {
                error = "Formal Session has no prepared runtime to restart.";
                return false;
            }

            DomainResult restarted = session.Restart();
            combatRuntime.ClearForRestart();
            if (!restarted.IsSuccess)
            {
                return FailRuntime(
                    FpgEncounterFailureReason.External,
                    "Formal Session restart failed: " + restarted.RejectReason,
                    out error);
            }

            LockExits(true);
            roomClearedRaised = false;
            currentTick = TickIndex.Invalid;
            Phase = FpgEncounterPhase.Preparing;
            DomainResult started = session.Start(new TickIndex(0L));
            if (!started.IsSuccess)
            {
                return FailRuntime(
                    FpgEncounterFailureReason.External,
                    "Formal Session restart Start failed: " + started.RejectReason,
                    out error);
            }

            currentTick = new TickIndex(0L);
            Phase = session.Runtime.Phase;
            error = string.Empty;
            return true;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            ClearPreparedRuntime(disposePools: true);
            DestroyOwnedExits();
            roomInstance?.Clear();
            LockExits(true);
            Phase = FpgEncounterPhase.Disposed;
            EmitLocal(FpgEncounterLifecycleEventType.Disposed);

        }

        private void HandleLifecycle(FpgEncounterLifecycleEvent lifecycle)
        {
            Phase = lifecycle.Phase;
            if (lifecycle.Tick.IsValid)
            {
                currentTick = lifecycle.Tick;
            }

            if (lifecycle.Type == FpgEncounterLifecycleEventType.EnemyActivated)
            {
                RegisterAttackOwner(lifecycle.RuntimeId);
            }
            else if (lifecycle.Type == FpgEncounterLifecycleEventType.RoomCleared)
            {
                LockExits(false);
                EmitLocal(FpgEncounterLifecycleEventType.ExitUnlocked);
                RaiseRoomCleared(lifecycle.Tick);
            }
            else if (lifecycle.Type == FpgEncounterLifecycleEventType.Failed
                || lifecycle.Type == FpgEncounterLifecycleEventType.Faulted)
            {
                LockExits(true);
            }

            LifecycleEvent?.Invoke(lifecycle);
        }

        private void RegisterAttackOwner(RuntimeId runtimeId)
        {
            if (session == null || combatRuntime == null
                || !session.Roster.TryGet(runtimeId, out FpgEnemySlot slot))
            {
                ReportCallbackFailure(RejectReason.InvalidTarget);
                return;
            }

            FpgEnemyDefinition definition = FindDefinition(slot.EnemyDefinitionId);
            if (definition == null)
            {
                ReportCallbackFailure(RejectReason.InvalidDefinition);
                return;
            }

            DomainResult registered = combatRuntime.AttackScheduler.TryRegisterEnemy(
                runtimeId,
                slot.SpawnSequence,
                slot.ActivationTick,
                slot.RecursionDepth,
                definition);
            if (!registered.IsSuccess)
            {
                ReportCallbackFailure(registered.RejectReason);
            }
        }

        private void HandleEnemyDied(FpgEnemyDiedEvent died)
        {
            if (combatRuntime == null || session == null)
            {
                return;
            }

            DomainResult unregistered = combatRuntime.AttackScheduler.TryUnregisterEnemy(
                died.RuntimeId);
            DomainResult marked = session.MarkEnemyDead(died.RuntimeId, died.Tick);
            if (!unregistered.IsSuccess && unregistered.RejectReason != RejectReason.InvalidTarget)
            {
                ReportCallbackFailure(unregistered.RejectReason);
            }
            if (!marked.IsSuccess)
            {
                ReportCallbackFailure(marked.RejectReason);
            }
        }

        private void HandleHealthChanged(FpgCombatHealthChangedEvent changed)
        {
            if (changed.Kind == FPG.Demo.Combat.CombatantKind.Enemy
                && (entityPort == null
                    || !entityPort.TryUpdateHealth(
                        changed.RuntimeId,
                        changed.Life,
                        changed.MaxLife)))
            {
                ReportCallbackFailure(RejectReason.InvalidTarget);
            }
        }

        private void ReportCallbackFailure(RejectReason reason)
        {
            if (combatRuntime != null
                && combatRuntime.Synchronizer is FpgFormalUnityTickSynchronizer synchronizer)
            {
                synchronizer.ReportExternalFailure(reason);
            }
        }

        private bool TryBuildWarmupRequests(

            FpgEncounterCapacityRequirements requirements,
            out List<FpgEnemyPoolWarmupRequest> warmup,
            out int requiredAttackPatterns,
            out int requiredConcurrentHitboxes,
            out int maxHitPartsPerEntity,
            out string error)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < encounterPlan.AllEntries.Count; index++)
            {
                string id = encounterPlan.AllEntries[index].EnemyDefinitionId;
                counts.TryGetValue(id, out int count);
                counts[id] = checked(count + 1);
            }

            if (requirements.SummonUpperBound > 0)
            {
                HashSet<string> reachableOwners =
                    new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> summonCandidates =
                    new HashSet<string>(StringComparer.Ordinal);
                Queue<FpgEnemyDefinition> pendingOwners =
                    new Queue<FpgEnemyDefinition>();

                for (int index = 0; index < encounterPlan.AllEntries.Count; index++)
                {
                    string ownerId = encounterPlan.AllEntries[index].EnemyDefinitionId;
                    FpgEnemyDefinition owner = FindDefinition(ownerId);
                    if (owner != null && reachableOwners.Add(ownerId))
                    {
                        pendingOwners.Enqueue(owner);
                    }
                }

                while (pendingOwners.Count > 0)
                {
                    FpgEnemyDefinition owner = pendingOwners.Dequeue();
                    for (int attackIndex = 0;
                        attackIndex < owner.AttackPatternCount;
                        attackIndex++)
                    {
                        FpgEnemyAttackDefinition attack =
                            owner.GetAttackPattern(attackIndex);
                        if (attack == null
                            || attack.Kind != FpgEnemyAttackKind.Summon
                            || attack.Summon == null)
                        {
                            continue;
                        }

                        FpgEnemyDefinition[] candidates =
                            attack.Summon.CandidateEnemies;
                        for (int candidateIndex = 0;
                            candidateIndex < candidates.Length;
                            candidateIndex++)
                        {
                            FpgEnemyDefinition candidate = candidates[candidateIndex];
                            if (candidate == null)
                            {
                                continue;
                            }

                            string candidateId = candidate.EnemyDefinitionId;
                            if (summonCandidates.Add(candidateId))
                            {
                                counts.TryGetValue(candidateId, out int planned);
                                counts[candidateId] = checked(
                                    planned + requirements.SummonUpperBound);
                            }

                            if (reachableOwners.Add(candidateId))
                            {
                                pendingOwners.Enqueue(candidate);
                            }
                        }
                    }
                }
            }

            warmup = new List<FpgEnemyPoolWarmupRequest>(counts.Count);
            requiredAttackPatterns = 0;
            requiredConcurrentHitboxes = 0;
            maxHitPartsPerEntity = 0;
            int maxAttackPatternsPerEntity = 0;
            int totalEntities = 0;
            HashSet<FpgSummonActionDefinition> uniqueSummonActions =
                new HashSet<FpgSummonActionDefinition>();
            foreach (KeyValuePair<string, int> pair in counts)
            {
                FpgEnemyDefinition definition = FindDefinition(pair.Key);
                if (definition == null)
                {
                    error = "Formal warmup cannot resolve enemy '" + pair.Key + "'.";
                    return false;
                }

                IFpgFormalEnemyEntityBinder binder = definition.EntityPrefab == null
                    ? null
                    : definition.EntityPrefab.GetComponent<IFpgFormalEnemyEntityBinder>();
                if (binder == null || binder.HitPartCount <= 0)
                {
                    error = "Formal enemy '" + pair.Key
                        + "' prefab has no preflight-readable formal hitbox binder.";
                    return false;
                }

                maxHitPartsPerEntity = Math.Max(
                    maxHitPartsPerEntity,
                    binder.HitPartCount);
                totalEntities = checked(totalEntities + pair.Value);
                maxAttackPatternsPerEntity = Math.Max(
                    maxAttackPatternsPerEntity,
                    definition.AttackPatternCount);
                for (int attackIndex = 0;
                    attackIndex < definition.AttackPatternCount;
                    attackIndex++)
                {
                    FpgEnemyAttackDefinition attack =
                        definition.GetAttackPattern(attackIndex);
                    if (attack != null
                        && attack.Kind == FpgEnemyAttackKind.Summon
                        && attack.Summon != null)
                    {
                        uniqueSummonActions.Add(attack.Summon);
                    }
                }

                warmup.Add(new FpgEnemyPoolWarmupRequest(definition, pair.Value));
            }

            if (totalEntities > profile.EntityPoolCapacity)
            {
                error = "Per-definition summon candidate upper bounds exceed entity pool capacity.";
                return false;
            }

            if (maxHitPartsPerEntity > FpgFormalGeometryId.MaxHitPartOrdinal + 1)
            {
                error = "Formal enemy hit-part count exceeds the injective GeometryId bounds.";
                return false;
            }

            int maxPlannedSequence = -1;
            for (int index = 0; index < encounterPlan.AllEntries.Count; index++)
            {
                int sequence = encounterPlan.AllEntries[index].SpawnSequence;
                if (sequence < 0 || sequence > FpgFormalGeometryId.MaxSpawnSequence)
                {
                    error = "Encounter plan SpawnSequence exceeds the injective GeometryId bounds.";
                    return false;
                }

                maxPlannedSequence = Math.Max(maxPlannedSequence, sequence);
            }

            long lastPossibleSequence =
                (long)maxPlannedSequence + requirements.SummonUpperBound;
            if (lastPossibleSequence > FpgFormalGeometryId.MaxSpawnSequence)
            {
                error = "Plan plus summon upper bound exceeds the injective GeometryId sequence range.";
                return false;
            }

            requiredAttackPatterns = Math.Max(
                checked(requirements.SimultaneousCombatants
                    * maxAttackPatternsPerEntity),
                uniqueSummonActions.Count);
            requiredConcurrentHitboxes = checked(
                requirements.SimultaneousCombatants * maxHitPartsPerEntity);
            error = string.Empty;
            return true;
        }

        private bool TryValidateGeometryIds(
            FpgEncounterCapacityRequirements requirements,
            int hitPartCount,
            out string error)
        {
            int maxPlannedSequence = -1;
            for (int index = 0; index < encounterPlan.AllEntries.Count; index++)
            {
                int sequence = encounterPlan.AllEntries[index].SpawnSequence;
                if (!formalHitboxRegistry.TryValidateGeometryIds(
                        sequence,
                        hitPartCount,
                        out error))
                {
                    error = "Formal geometry preflight failed for planned SpawnSequence "
                        + sequence + ": " + error;
                    return false;
                }

                maxPlannedSequence = Math.Max(maxPlannedSequence, sequence);
            }

            int firstDynamicSequence = checked(maxPlannedSequence + 1);
            int lastDynamicSequence = checked(
                maxPlannedSequence + requirements.SummonUpperBound);
            for (int sequence = firstDynamicSequence;
                sequence <= lastDynamicSequence;
                sequence++)
            {
                if (!formalHitboxRegistry.TryValidateGeometryIds(
                        sequence,
                        hitPartCount,
                        out error))
                {
                    error = "Formal geometry preflight failed for summon SpawnSequence "
                        + sequence + ": " + error;
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private bool TryPrepareExits(out string error)
        {
            IReadOnlyList<FpgRoomExitSlot> slots = roomDefinition.ExitSlots;
            if (slots == null || slots.Count == 0)
            {
                error = "Formal room requires at least one exit.";
                return false;
            }

            FpgRoomExitRuntime[] nextExits = new FpgRoomExitRuntime[slots.Count];
            List<FpgRoomExitRuntime> created = new List<FpgRoomExitRuntime>();
            for (int index = 0; index < slots.Count; index++)
            {

                FpgRoomExitSlot slot = slots[index];
                FpgRoomExitRuntime runtime = FindExitRuntime(slot.MarkerId);
                if (runtime == null && exitRuntimePrefab != null)
                {
                    GameObject instance = Instantiate(
                        exitRuntimePrefab,
                        exitRuntimeRoot == null ? transform : exitRuntimeRoot,
                        false);
                    runtime = instance.GetComponent<FpgRoomExitRuntime>();
                    if (runtime != null)
                    {
                        created.Add(runtime);
                    }
                    else if (Application.isPlaying)
                    {
                        Destroy(instance);
                    }
                    else
                    {
                        DestroyImmediate(instance);
                    }
                }

                if (runtime == null
                    || !roomInstance.TryResolveExitPose(slot.MarkerId, out Pose pose)
                    || !runtime.TryConfigure(slot.MarkerId, pose, runtime.transform.parent, out error))
                {
                    error = "Formal room exit '" + slot.MarkerId + "' has no runtime binding.";
                    DestroyExitList(created);
                    return false;
                }

                runtime.Selected -= HandleExitSelected;
                runtime.Selected += HandleExitSelected;
                nextExits[index] = runtime;
            }

            for (int index = 0; index < created.Count; index++)
            {
                ownedExitRuntimes.Add(created[index]);
            }

            for (int index = 0; index < activeExits.Length; index++)
            {
                FpgRoomExitRuntime previous = activeExits[index];
                if (previous == null || Array.IndexOf(nextExits, previous) >= 0)
                {
                    continue;
                }

                previous.Selected -= HandleExitSelected;
                previous.SetLocked(true);
                if (ownedExitRuntimes.Contains(previous))
                {
                    previous.gameObject.SetActive(false);
                }
            }

            for (int index = 0; index < nextExits.Length; index++)
            {
                if (nextExits[index] != null)
                {
                    nextExits[index].gameObject.SetActive(true);
                }
            }

            activeExits = nextExits;
            error = string.Empty;
            return true;
        }

        private FpgRoomExitRuntime FindExitRuntime(string id)
        {
            for (int index = 0; index < activeExits.Length; index++)
            {
                FpgRoomExitRuntime candidate = activeExits[index];
                if (candidate != null
                    && string.Equals(candidate.ExitId, id, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            for (int index = 0; index < exitRuntimes.Length; index++)
            {
                FpgRoomExitRuntime candidate = exitRuntimes[index];
                if (candidate != null
                    && string.Equals(candidate.ExitId, id, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            for (int index = 0; index < ownedExitRuntimes.Count; index++)
            {
                FpgRoomExitRuntime candidate = ownedExitRuntimes[index];
                if (candidate != null
                    && string.Equals(candidate.ExitId, id, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private FpgEnemyDefinition FindDefinition(string id)
        {
            if (enemyCatalog == null) return null;
            IReadOnlyList<FpgEnemyDefinition> definitions = enemyCatalog.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                FpgEnemyDefinition definition = definitions[index];
                if (definition != null
                    && string.Equals(definition.EnemyDefinitionId, id, StringComparison.Ordinal))
                {
                    return definition;
                }
            }
            return null;
        }

        private void HandleExitSelected(FpgRoomExitRuntime exitRuntime)
        {
            if (exitRuntime != null && Phase == FpgEncounterPhase.Cleared)
            {
                ExitSelected?.Invoke(exitRuntime.ExitId);
            }
        }

        private void LockExits(bool locked)
        {
            FpgRoomExitRuntime[] exits = activeExits.Length == 0
                ? exitRuntimes
                : activeExits;
            for (int index = 0; index < exits.Length; index++)
            {
                exits[index]?.SetLocked(locked);
            }
        }

        private void RaiseRoomCleared(TickIndex tick)
        {
            if (roomClearedRaised) return;
            roomClearedRaised = true;
            RoomCleared?.Invoke(new FpgRoomClearedEvent(
                roomDefinition == null ? string.Empty : roomDefinition.RoomId,
                tick,
                request.RunContext));
        }

        private void EmitLocal(FpgEncounterLifecycleEventType type)
        {
            LifecycleEvent?.Invoke(new FpgEncounterLifecycleEvent(

                type,
                currentTick,
                Phase,
                RuntimeId.Invalid,
                CurrentWaveIndex));
        }

        private bool FailPreparation(
            FpgEncounterFailureReason reason,
            string message,
            out string error)
        {
            Phase = FpgEncounterPhase.Failed;
            LockExits(true);
            ClearPreparedRuntime(disposePools: true);
            Failed?.Invoke(reason, message ?? string.Empty);
            error = string.IsNullOrWhiteSpace(message)
                ? "Formal Session preparation failed."
                : message;
            return false;
        }

        private bool FailRuntime(
            FpgEncounterFailureReason reason,
            string message,
            out string error)
        {
            Phase = FpgEncounterPhase.Faulted;
            LockExits(true);

            // Session.Fault clears pure encounter/combat state. These Unity
            // ports are independent fixed stores and must also terminate now.
            entityPort?.ClearAll();
            combatRuntime?.ClearForFault();
            overheadHealthBarPool?.ClearActive();
            formalHitboxRegistry?.Clear();
            combatantAnchorMap?.Clear();
            enemyEntityPool?.ClearActive();
            combatStarted = false;

            Failed?.Invoke(reason, message ?? string.Empty);
            error = string.IsNullOrWhiteSpace(message)
                ? "Formal Session faulted."
                : message;
            return false;
        }

        private void ClearPreparedRuntime(bool disposePools)
        {
            if (combatRuntime != null)
            {
                combatRuntime.CombatPort.EnemyDied -= HandleEnemyDied;
                combatRuntime.CombatPort.HealthChanged -= HandleHealthChanged;
            }

            session?.Dispose();
            combatRuntime?.Dispose();
            session = null;
            combatRuntime = null;
            entityPort = null;
            spawnResolver = null;
            prepared = false;
            combatStarted = false;

            if (disposePools)
            {
                overheadHealthBarPool?.Dispose();
                formalHitboxRegistry?.Clear();
                combatantAnchorMap?.Clear();
                enemyEntityPool?.Dispose();
                roomInstance?.Clear();
            }
        }

        private void DestroyExitList(List<FpgRoomExitRuntime> exits)
        {
            for (int index = exits.Count - 1; index >= 0; index--)
            {
                FpgRoomExitRuntime runtime = exits[index];
                if (runtime == null) continue;
                runtime.Selected -= HandleExitSelected;
                if (Application.isPlaying) Destroy(runtime.gameObject);
                else DestroyImmediate(runtime.gameObject);
            }
            exits.Clear();
        }

        private void DestroyOwnedExits()
        {
            DestroyExitList(ownedExitRuntimes);
            activeExits = Array.Empty<FpgRoomExitRuntime>();
        }

        private void OnDestroy()
        {
            Dispose();
        }
    

        public bool TryPlacePlayerAtEntry(string markerId, out string error)
        {
            if (!prepared || combatStarted || roomInstance == null || playerAnchor == null)
            {
                error = "Formal player entry placement requires a prepared room before combat starts.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(markerId)
                || !roomInstance.TryResolvePlayerEntryPose(markerId, out Pose pose))
            {
                error = $"Formal room player entry '{markerId}' is missing.";
                return false;
            }

            CharacterController characterController =
                playerAnchor.GetComponent<CharacterController>();
            bool controllerWasEnabled = characterController != null
                && characterController.enabled;
            if (controllerWasEnabled)
            {
                characterController.enabled = false;
            }

            playerAnchor.SetPositionAndRotation(pose.position, pose.rotation);

            if (controllerWasEnabled)
            {
                characterController.enabled = true;
            }

            if (entrySafetyAnchor != null && entrySafetyAnchor != playerAnchor)
            {
                entrySafetyAnchor.SetPositionAndRotation(pose.position, pose.rotation);
            }

            CombatLabPlayerBounds playerBounds =
                playerAnchor.GetComponent<CombatLabPlayerBounds>();
            playerBounds?.CaptureInitialSafePosition(out _);
            spawnResolver?.RefreshDistances();
            error = string.Empty;
            return true;
        }
}

    public readonly struct FpgRoomClearedEvent
    {
        public FpgRoomClearedEvent(
            string roomId,
            TickIndex tick,
            FpgEncounterRunContext runContext)
        {
            RoomId = roomId ?? string.Empty;
            Tick = tick;
            RunContext = runContext;

        }

        public string RoomId { get; }
        public TickIndex Tick { get; }
        public FpgEncounterRunContext RunContext { get; }
    }
}



