
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
    public sealed class FpgRoomEncounterDirector : MonoBehaviour,
        IFpgFormalEnemySkillPresentationConsumer
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
        [SerializeField] private HitboxRegistry exitHitboxRegistry;

        [Header("Spatial Anchors")]
        [SerializeField] private Transform playerAnchor;
        [SerializeField] private Transform entrySafetyAnchor;

        [Header("Session Ports")]
        [SerializeField] private MonoBehaviour formalCombatPortFactoryComponent;
        [SerializeField] private MonoBehaviour formalPlayerTickDriverComponent;
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
        private readonly FpgExitAttackRegistry exitAttackRegistry =
            new FpgExitAttackRegistry();
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
        private FpgVitalsSnapshot[] enemyVitalsBuffer =
            Array.Empty<FpgVitalsSnapshot>();
        private long enemyVitalsCursor;
        [NonSerialized] private FpgPlayerEntityView configuredPlayerEntity;
        [NonSerialized] private bool playerBindingConfigured;
        [NonSerialized] private bool playerBindingLocked;

        public FpgEncounterPhase Phase { get; private set; } = FpgEncounterPhase.None;
        public FpgEncounterPlan Plan => encounterPlan;
        public FpgEncounterRunContext RunContext => request.RunContext;
        public TickIndex CurrentTick => currentTick;
        public FpgEncounterSession Session => session;
        public FpgFormalCombatRuntimeBundle CombatRuntime => combatRuntime;
        public FpgEnemyDefinitionCatalog EnemyCatalog => enemyCatalog;
        public IFpgFormalPlayerRunResourceImportPort PlayerRunResourceImportPort =>
            (configuredFactory ?? formalCombatPortFactoryComponent
                as IFpgFormalCombatPortFactory)
            as IFpgFormalPlayerRunResourceImportPort;
        public FpgPlayerEntityView ConfiguredPlayerEntity => configuredPlayerEntity;
        public Transform PlayerAnchor => playerAnchor;
        public bool HasPlayerBinding => playerBindingConfigured
            && configuredPlayerEntity != null
            && playerAnchor == configuredPlayerEntity.transform;
        public bool IsPlayerBindingLocked => playerBindingLocked;
        public FpgMultiEnemyCombatPort CombatPort =>
            combatRuntime == null ? null : combatRuntime.CombatPort;
        public PlayerRuntime Player => combatRuntime == null ? null : combatRuntime.Player;
        public FpgFormalPlayerTickDriver PlayerTickDriver =>
            configuredPlayerDriver as FpgFormalPlayerTickDriver;
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
            || Phase == FpgEncounterPhase.Defeated
            || Phase == FpgEncounterPhase.Failed
            || Phase == FpgEncounterPhase.Faulted
            || Phase == FpgEncounterPhase.Disposed;
        public int PresentationFaultCount { get; private set; }
        public int EnemyVitalsGapCount { get; private set; }
        public FpgExitAttackRegistry ExitAttackRegistry => exitAttackRegistry;
        public bool HasAvailableExits => exitAttackRegistry.Count > 0;

        public bool TryResolveEnemyPresentationSource(
            RuntimeId runtimeId,
            int spawnSequence,
            string socketId,
            out Transform source)
        {
            source = null;
            return entityPort != null
                && entityPort.TryResolvePresentationSource(
                    runtimeId,
                    spawnSequence,
                    socketId,
                    out source);
        }

        public event Action<FpgEncounterLifecycleEvent> LifecycleEvent;
        public event Action<FpgFormalEnemySkillTimelineEvent>
            EnemySkillTimelineEvent;
        public event Action<FpgRoomClearedEvent> RoomCleared;
        public event Action<string> ExitSelected;
        public event Action<FpgExitSelectionEvent> ExitOfferSelected;
        public event Action<FpgEncounterFailureReason, string> Failed;
        public event Action RestartSucceeded;

        public bool TryConfigurePlayer(
            FpgPlayerEntityView entity,
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
            if (exitHitboxRegistry == null
                && combatPortFactory is FpgFormalCombatPortFactory concreteFactory)
            {
                exitHitboxRegistry = concreteFactory.StaticHitboxRegistry;
            }

            if (exitHitboxRegistry == null)
            {
                configuredFactory = null;
                configuredPlayerDriver = null;
                error = "Formal session requires an exit hitbox registry.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryPrepareSession(
            FpgRoomRunRequest nextRequest,
            FpgEncounterPlan nextPlan,
            FpgEnemyDefinitionCatalog nextEnemyCatalog,
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
            if (factory == null || playerDriver == null)
            {
                return FailPreparation(
                    FpgEncounterFailureReason.InvalidRequest,
                    "Formal Session requires an explicit combat factory and player driver.",
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
                || formalHitboxRegistry == null)
            {
                return FailPreparation(
                    FpgEncounterFailureReason.InvalidRequest,
                    "Formal Session is missing an authored room or explicit Unity runtime pools.",
                    out error);
            }

            ClearPreparedRuntime(disposePools: true);
            PresentationFaultCount = 0;
            EnemyVitalsGapCount = 0;
            request = nextRequest;
            encounterPlan = nextPlan;
            enemyCatalog = nextEnemyCatalog;
            roomDefinition = roomSource.Room;
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
            if (enemyEntityPool.Capacity < requirements.EntityPoolSlots
                || combatantAnchorMap.Capacity < requirements.EntitySlots + 1
                || profile.HitboxCapacity < requiredConcurrentHitboxes
                || formalHitboxRegistry.Capacity < requiredConcurrentHitboxes)
            {
                return FailPreparation(
                    FpgEncounterFailureReason.EntityCapacity,
                    "Formal entity, anchor or hitbox capacity is below preflight.",
                    out error);
            }

            if (!enemyEntityPool.TryPrewarm(warmup, out error)
                || !combatantAnchorMap.TryInitialize(out error)
                || !formalHitboxRegistry.TryInitialize(out error))
            {
                return FailPreparation(FpgEncounterFailureReason.EntityCapacity, error, out error);
            }

            TryPrewarmOverheadHealthBars(
                requirements.SimultaneousCombatants,
                overheadHealthBarCamera);

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
            enemyVitalsBuffer = new FpgVitalsSnapshot[
                combatCapacity.VitalsEventCapacity];
            enemyVitalsCursor = 0L;
            FpgSummonLedger summonLedger = new FpgSummonLedger(
                combatCapacity.SummonCapacity,
                requirements.GameplayQuotaSummonUpperBound,
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
                    entityPort,
                    out combatRuntime,
                    out error))
            {
                runtime.Dispose();
                return FailPreparation(FpgEncounterFailureReason.External, error, out error);
            }

            try
            {
                IReadOnlyList<FpgRoomCoverSlot> coverSlots =
                    roomDefinition.CoverSlots;
                FpgCoverNodeDefinition[] coverDefinitions =
                    new FpgCoverNodeDefinition[coverSlots.Count];
                for (int coverIndex = 0;
                    coverIndex < coverSlots.Count;
                    coverIndex++)
                {
                    FpgRoomCoverSlot cover = coverSlots[coverIndex];
                    int lateralPositionKey = checked((int)Math.Round(
                        cover.PlayerReachableLocalPosition.x
                            * SpatialContract.PositionUnitsPerMeter,
                        MidpointRounding.AwayFromZero));
                    coverDefinitions[coverIndex] = new FpgCoverNodeDefinition(
                        cover.MarkerId,
                        lateralPositionKey,
                        cover.MaxDurability,
                        cover.IsStartingCover);
                }

                float traversalSeconds = factory is FpgFormalCombatPortFactory
                        concreteFactory
                    && concreteFactory.PlayerThreeCProfile != null
                        ? concreteFactory.PlayerThreeCProfile.CoverTraversalSeconds
                        : 0.25f;
                FpgCoverRuntime covers = new FpgCoverRuntime(
                    combatRuntime.Player.RuntimeId,
                    coverDefinitions,
                    TickDuration.FromSeconds(traversalSeconds));
                if (!combatRuntime.TryBindCovers(covers, out error))
                {
                    throw new InvalidOperationException(error);
                }

                roomInstance.RefreshCoverViews(covers);
            }
            catch (Exception exception)
            {
                runtime.Dispose();
                combatRuntime.Dispose();
                combatRuntime = null;
                return FailPreparation(
                    FpgEncounterFailureReason.InvalidRequest,
                    "Formal cover preparation failed: " + exception.Message,
                    out error);
            }

            for (int warmupIndex = 0;
                warmupIndex < warmup.Count;
                warmupIndex++)
            {
                DomainResult preparedEnemy =
                    combatRuntime.AttackScheduler
                        .TryPrepareEnemyDefinition(
                            warmup[warmupIndex].Definition);
                if (!preparedEnemy.IsSuccess)
                {
                    runtime.Dispose();
                    return FailPreparation(
                        FpgEncounterFailureReason.InvalidRequest,
                        "Formal enemy skill preparation failed: "
                            + preparedEnemy.RejectReason,
                        out error);
                }
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
            combatRuntime.AttackScheduler.TimelineEvent +=
                HandleEnemySkillTimelineEvent;
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
            }
            catch (Exception exception)
            {
                return FailRuntime(FpgEncounterFailureReason.EntityCapacity, exception.Message, out error);
            }

            try
            {
                TryBeginOverheadHealthBars();
            }
            catch (Exception)
            {
                PresentationFaultCount++;
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
            ConsumeEnemyVitalsPresentation();
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

            if (session.State == FpgEncounterSessionState.Defeated)
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

            roomInstance.RefreshCoverViews(combatRuntime?.Covers);
            if (configuredPlayerDriver is FpgFormalPlayerTickDriver playerDriver)
            {
                playerDriver.TryRefreshPresentationSnapshot(out _);
            }

            ConsumeEnemySkillSequenceFrames();
            ConsumeEnemyVitalsPresentation();
            return true;
        }

        public bool BeginRoomInteraction(out string error)
        {
            IFpgFormalPlayerTickDriver driver = configuredPlayerDriver
                ?? formalPlayerTickDriverComponent as IFpgFormalPlayerTickDriver;
            if (Phase != FpgEncounterPhase.Cleared
                || combatRuntime == null
                || driver == null)
            {
                error = "Room interaction requires a cleared formal session.";
                return false;
            }

            driver.BeginRoomInteraction();
            error = string.Empty;
            return true;
        }

        public bool ProcessRoomInteractionTick(
            TickIndex tick,
            out string error)
        {
            IFpgFormalPlayerTickDriver driver = configuredPlayerDriver
                ?? formalPlayerTickDriverComponent as IFpgFormalPlayerTickDriver;
            if (Phase != FpgEncounterPhase.Cleared
                || combatRuntime == null
                || driver == null
                || !HasAvailableExits
                || !tick.IsValid
                || currentTick.IsValid && tick <= currentTick)
            {
                error = "Room interaction tick is not available.";
                return false;
            }

            Physics.SyncTransforms();
            TickIndex previousTick = currentTick;
            currentTick = tick;
            DomainResult result =
                driver.ProcessRoomInteractionTick(tick, combatRuntime);
            if (!result.IsSuccess)
            {
                currentTick = previousTick;
                error = "Room interaction tick failed: " + result.RejectReason;
                FailRuntime(
                    FpgEncounterFailureReason.SynchronizerFault,
                    error,
                    out error);
                return false;
            }

            error = string.Empty;
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
                combatRuntime?.AttackScheduler
                    .ClearPresentationCommitState();
                TrySetOverheadHealthBarsPaused(true);
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
                TrySetOverheadHealthBarsPaused(false);
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
            if (!restarted.IsSuccess)
            {
                return FailRuntime(
                    FpgEncounterFailureReason.External,
                    "Formal Session restart failed: " + restarted.RejectReason,
                    out error);
            }

            combatRuntime.ClearForRestart();
            DeactivateAndClearExits();
            roomClearedRaised = false;
            PresentationFaultCount = 0;
            EnemyVitalsGapCount = 0;
            TrySetOverheadHealthBarsPaused(false);
            enemyVitalsCursor = 0L;
            Array.Clear(enemyVitalsBuffer, 0, enemyVitalsBuffer.Length);
            currentTick = TickIndex.Invalid;
            Phase = FpgEncounterPhase.Preparing;
            combatStarted = false;
            roomInstance.RefreshCoverViews(combatRuntime.Covers);
            RestartSucceeded?.Invoke();
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
                PlayerTickDriver?.CancelCoverTraversalForTerminalState();
                LockExits(true);
                RaiseRoomCleared(lifecycle.Tick);
            }
            else if (lifecycle.Type == FpgEncounterLifecycleEventType.Defeated)
            {
                PlayerTickDriver?.CancelCoverTraversalForTerminalState();
                exitAttackRegistry.Clear();
                LockExits(true);
                combatRuntime?.ClearForDefeat();
                TryClearOverheadHealthBars();
            }
            else if (lifecycle.Type == FpgEncounterLifecycleEventType.Failed
                || lifecycle.Type == FpgEncounterLifecycleEventType.Faulted)
            {
                PlayerTickDriver?.CancelCoverTraversalForTerminalState();
                exitAttackRegistry.Clear();
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

        private void ConsumeEnemySkillSequenceFrames()
        {
            if (combatRuntime == null || entityPort == null)
            {
                return;
            }

            FpgFormalEnemyAttackScheduler scheduler =
                combatRuntime.AttackScheduler;
            for (int index = 0;
                index < scheduler.SequenceFrameCount;
                index++)
            {
                FpgFormalEnemySkillSequenceFrame frame =
                    scheduler.GetSequenceFrame(index);
                try
                {
                    if (!entityPort.TryApplySkillSequenceFrame(frame))
                    {
                        PresentationFaultCount++;
                    }
                }
                catch (Exception)
                {
                    PresentationFaultCount++;
                }
            }
        }

        public bool TrySetEnemySkillWarning(
            in FpgFormalEnemySkillWarningPresentationEvent warningEvent)
        {
            return entityPort != null
                && entityPort.TrySetEnemySkillWarning(warningEvent);
        }

        public void ClearEnemySkillWarnings()
        {
            entityPort?.ClearEnemySkillWarnings();
        }

        private void HandleEnemySkillTimelineEvent(
            FpgFormalEnemySkillTimelineEvent skillEvent)
        {
            try
            {
                EnemySkillTimelineEvent?.Invoke(skillEvent);
            }
            catch (Exception)
            {
                PresentationFaultCount++;
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

        private void ConsumeEnemyVitalsPresentation()
        {
            try
            {
                ConsumeEnemyVitalsPresentationCore();
            }
            catch (Exception)
            {
                PresentationFaultCount++;
                RecoverEnemyVitalsPresentationAfterFault();
            }
        }

        private void RecoverEnemyVitalsPresentationAfterFault()
        {
            try
            {
                IFpgVitalsView vitals = combatRuntime == null
                    ? null
                    : combatRuntime.CombatPort.Vitals;
                enemyVitalsCursor = vitals == null ? 0L : vitals.LastSequence;
            }
            catch (Exception)
            {
                enemyVitalsCursor = 0L;
            }

            try
            {
                if (enemyVitalsBuffer != null && enemyVitalsBuffer.Length > 0)
                {
                    Array.Clear(
                        enemyVitalsBuffer,
                        0,
                        enemyVitalsBuffer.Length);
                }
            }
            catch (Exception)
            {
                enemyVitalsBuffer = Array.Empty<FpgVitalsSnapshot>();
            }
        }

        private void ConsumeEnemyVitalsPresentationCore()
        {
            if (combatRuntime == null || entityPort == null
                || enemyVitalsBuffer == null || enemyVitalsBuffer.Length == 0)
            {
                return;
            }

            IFpgVitalsView vitals = combatRuntime.CombatPort.Vitals;
            if (vitals == null)
            {
                PresentationFaultCount++;
                return;
            }

            int copied;
            bool hasGap;
            try
            {
                copied = vitals.CopyChangesAfter(
                    enemyVitalsCursor,
                    enemyVitalsBuffer,
                    out hasGap);
            }
            catch (ArgumentException)
            {
                PresentationFaultCount++;
                ResynchronizeEnemyHealthBars(vitals);
                enemyVitalsCursor = vitals.LastSequence;
                Array.Clear(enemyVitalsBuffer, 0, enemyVitalsBuffer.Length);
                return;
            }

            if (hasGap)
            {
                EnemyVitalsGapCount++;
                ResynchronizeEnemyHealthBars(vitals);
                enemyVitalsCursor = vitals.LastSequence;
                Array.Clear(enemyVitalsBuffer, 0, enemyVitalsBuffer.Length);
                return;
            }

            for (int index = 0; index < copied; index++)
            {
                FpgVitalsSnapshot snapshot = enemyVitalsBuffer[index];
                enemyVitalsCursor = Math.Max(enemyVitalsCursor, snapshot.Sequence);
                if (snapshot.Kind == FPG.Demo.Combat.CombatantKind.Enemy
                    && !snapshot.Dead
                    && overheadHealthBarPool != null
                    && overheadHealthBarPool.IsPrepared
                    && !entityPort.TryUpdateHealth(
                        snapshot.RuntimeId,
                        snapshot.Life,
                        snapshot.MaxLife))
                {
                    PresentationFaultCount++;
                }

                enemyVitalsBuffer[index] = default(FpgVitalsSnapshot);
            }
        }

        private void ResynchronizeEnemyHealthBars(IFpgVitalsView vitals)
        {
            if (overheadHealthBarPool == null
                || !overheadHealthBarPool.IsPrepared)
            {
                return;
            }

            int failureCountBefore = entityPort.HealthBarUpdateFailureCount;
            entityPort.ResynchronizeHealthBars(vitals);
            PresentationFaultCount += Math.Max(
                0,
                entityPort.HealthBarUpdateFailureCount - failureCountBefore);
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
            IReadOnlyList<FpgEnemyPoolCapacityRequirement> poolRequirements =
                requirements.EnemyPoolRequirements;
            if (poolRequirements == null || poolRequirements.Count == 0)
            {
                warmup = null;
                requiredAttackPatterns = 0;
                requiredConcurrentHitboxes = 0;
                maxHitPartsPerEntity = 0;
                error = "Formal preflight produced no entity-pool requirements.";
                return false;
            }

            warmup = new List<FpgEnemyPoolWarmupRequest>(poolRequirements.Count);
            requiredAttackPatterns = 0;
            requiredConcurrentHitboxes = 0;
            maxHitPartsPerEntity = 0;
            int maxAttackPatternsPerEntity = 0;
            int totalEntities = 0;
            for (int requirementIndex = 0;
                requirementIndex < poolRequirements.Count;
                requirementIndex++)
            {
                FpgEnemyPoolCapacityRequirement poolRequirement =
                    poolRequirements[requirementIndex];
                FpgEnemyDefinition definition = FindDefinition(
                    poolRequirement.EnemyDefinitionId);
                if (definition == null)
                {
                    error = "Formal warmup cannot resolve enemy '"
                        + poolRequirement.EnemyDefinitionId + "'.";
                    return false;
                }

                IFpgFormalEnemyEntityBinder binder = definition.EntityPrefab == null
                    ? null
                    : definition.EntityPrefab.GetComponent<IFpgFormalEnemyEntityBinder>();
                if (binder == null || binder.HitPartCount <= 0)
                {
                    error = "Formal enemy '" + poolRequirement.EnemyDefinitionId
                        + "' prefab has no preflight-readable formal hitbox binder.";
                    return false;
                }

                maxHitPartsPerEntity = Math.Max(
                    maxHitPartsPerEntity,
                    binder.HitPartCount);
                totalEntities = checked(totalEntities + poolRequirement.Count);
                maxAttackPatternsPerEntity = Math.Max(
                    maxAttackPatternsPerEntity,
                    definition.AttackPatternCount);
                warmup.Add(new FpgEnemyPoolWarmupRequest(
                    definition,
                    poolRequirement.Count));
            }

            if (totalEntities != requirements.EntityPoolSlots
                || totalEntities > profile.EntityPoolCapacity)
            {
                error = "Per-definition entity-pool requirements are inconsistent or exceed capacity.";
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

            requiredAttackPatterns = checked(
                requirements.SimultaneousCombatants
                    * maxAttackPatternsPerEntity);
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

        public bool TryRevealExits(
            IReadOnlyList<FpgExitOffer> offers,
            out string error)
        {
            if (Phase != FpgEncounterPhase.Cleared
                || offers == null
                || offers.Count != activeExits.Length
                || exitHitboxRegistry == null)
            {
                error = "Cleared room exits require one offer per runtime and a hitbox registry.";
                return false;
            }

            exitAttackRegistry.Clear();
            LockExits(true);
            int geometryValue = FpgExitAttackRegistry.GeometryIdStart;
            for (int index = 0; index < activeExits.Length; index++)
            {
                FpgRoomExitRuntime runtime = activeExits[index];
                FpgExitOffer offer = FindOffer(offers, runtime.ExitId);
                if (offer == null)
                {
                    error = $"Missing room exit offer for '{runtime.ExitId}'.";
                    exitAttackRegistry.Clear();
                    LockExits(true);
                    return false;
                }

                if (!runtime.TryReveal(offer, out error)
                    || !exitAttackRegistry.TryRegisterRuntime(
                        runtime,
                        exitHitboxRegistry,
                        ref geometryValue,
                        out error))
                {
                    exitAttackRegistry.Clear();
                    LockExits(true);
                    return false;
                }
            }

            if (!BeginRoomInteraction(out error))
            {
                exitAttackRegistry.Clear();
                LockExits(true);
                return false;
            }

            EmitLocal(FpgEncounterLifecycleEventType.ExitUnlocked);
            error = string.Empty;
            return true;
        }

        public bool TrySelectExit(GeometryId geometryId, out string error)
        {
            if (Phase != FpgEncounterPhase.Cleared
                || !exitAttackRegistry.TryGetRuntime(
                    geometryId,
                    out FpgRoomExitRuntime runtime)
                || runtime.Offer == null
                || !runtime.TrySelect())
            {
                error = "Attack did not hit an available room exit.";
                return false;
            }

            FpgExitOffer selectedOffer = runtime.Offer;
            for (int index = 0; index < activeExits.Length; index++)
            {
                if (activeExits[index] != runtime)
                {
                    activeExits[index]?.ConsumeSilently();
                }
            }

            exitAttackRegistry.Clear();
            FpgExitSelectionEvent selection = new FpgExitSelectionEvent(
                geometryId,
                selectedOffer,
                currentTick);
            ExitOfferSelected?.Invoke(selection);
            ExitSelected?.Invoke(selectedOffer.ExitId);
            error = string.Empty;
            return true;
        }

        public void DeactivateAndClearExits()
        {
            exitAttackRegistry.Clear();
            LockExits(true);
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

        private static FpgExitOffer FindOffer(
            IReadOnlyList<FpgExitOffer> offers,
            string exitId)
        {
            for (int index = 0; index < offers.Count; index++)
            {
                FpgExitOffer offer = offers[index];
                if (offer != null
                    && string.Equals(offer.ExitId, exitId, StringComparison.Ordinal))
                {
                    return offer;
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
            bool lifecycleAlreadyPublished = session != null
                && (session.Runtime.Phase == FpgEncounterPhase.Failed
                    || session.Runtime.Phase == FpgEncounterPhase.Faulted);
            Phase = FpgEncounterPhase.Faulted;
            PlayerTickDriver?.CancelCoverTraversalForTerminalState();
            DeactivateAndClearExits();

            // Session.Fault clears pure encounter/combat state. These Unity
            // ports are independent fixed stores and must also terminate now.
            entityPort?.ClearAll();
            combatRuntime?.ClearForFault();
            TryClearOverheadHealthBars();
            formalHitboxRegistry?.Clear();
            combatantAnchorMap?.Clear();
            enemyEntityPool?.ClearActive();
            combatStarted = false;
            if (!lifecycleAlreadyPublished)
            {
                EmitLocal(FpgEncounterLifecycleEventType.Faulted);
            }

            Failed?.Invoke(reason, message ?? string.Empty);
            error = string.IsNullOrWhiteSpace(message)
                ? "Formal Session faulted."
                : message;
            return false;
        }

        private void ClearPreparedRuntime(bool disposePools)
        {
            DeactivateAndClearExits();
            if (combatRuntime != null)
            {
                combatRuntime.CombatPort.EnemyDied -= HandleEnemyDied;
                combatRuntime.AttackScheduler.TimelineEvent -=
                    HandleEnemySkillTimelineEvent;
            }

            session?.Dispose();
            combatRuntime?.Dispose();
            session = null;
            combatRuntime = null;
            entityPort = null;
            spawnResolver = null;
            enemyVitalsBuffer = Array.Empty<FpgVitalsSnapshot>();
            enemyVitalsCursor = 0L;
            prepared = false;
            combatStarted = false;

            if (disposePools)
            {
                DestroyOwnedExits();
                TryDisposeOverheadHealthBars();
                formalHitboxRegistry?.Clear();
                combatantAnchorMap?.Clear();
                enemyEntityPool?.Dispose();
                roomInstance?.Clear();
            }
        }

        private void TryPrewarmOverheadHealthBars(
            int requestedCapacity,
            Camera targetCamera)
        {
            if (overheadHealthBarPool == null)
            {
                return;
            }

            try
            {
                int capacity = Math.Min(
                    requestedCapacity,
                    overheadHealthBarPool.Capacity);
                if (!overheadHealthBarPool.TryPrewarm(
                        capacity,
                        targetCamera,
                        out _))
                {
                    PresentationFaultCount++;
                }
            }
            catch (Exception)
            {
                PresentationFaultCount++;
            }
        }

        private void TryBeginOverheadHealthBars()
        {
            TryRunOverheadHealthBarAction(pool => pool.BeginCombat());
        }

        private void TrySetOverheadHealthBarsPaused(bool paused)
        {
            TryRunOverheadHealthBarAction(pool => pool.SetPaused(paused));
        }

        private void TryClearOverheadHealthBars()
        {
            TryRunOverheadHealthBarAction(pool => pool.ClearActive());
        }

        private void TryDisposeOverheadHealthBars()
        {
            TryRunOverheadHealthBarAction(pool => pool.Dispose());
        }

        private void TryRunOverheadHealthBarAction(
            Action<FpgOverheadHealthBarPool> action)
        {
            if (overheadHealthBarPool == null || action == null)
            {
                return;
            }

            try
            {
                action(overheadHealthBarPool);
            }
            catch (Exception)
            {
                PresentationFaultCount++;
            }
        }

        private void DestroyExitList(List<FpgRoomExitRuntime> exits)
        {
            for (int index = exits.Count - 1; index >= 0; index--)
            {
                FpgRoomExitRuntime runtime = exits[index];
                if (runtime == null) continue;
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

            FpgPlayerBounds playerBounds =
                playerAnchor.GetComponent<FpgPlayerBounds>();
            playerBounds?.CaptureInitialSafePosition(out _);
            spawnResolver?.RefreshDistances();
            error = string.Empty;
            return true;
        }

        public bool TryResolveCoverReachablePose(
            string coverId,
            out Pose pose)
        {
            if (roomInstance != null
                && roomInstance.TryResolveCoverReachablePose(coverId, out pose))
            {
                return true;
            }

            pose = default;
            return false;
        }

        public bool TryPlacePlayerAtCover(string coverId, out string error)
        {
            if (!prepared || roomInstance == null || playerAnchor == null
                || string.IsNullOrWhiteSpace(coverId)
                || !roomInstance.TryResolveCoverReachablePose(
                    coverId,
                    out Pose pose))
            {
                error = $"Formal cover destination '{coverId}' is unavailable.";
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
                entrySafetyAnchor.SetPositionAndRotation(
                    pose.position,
                    pose.rotation);
            }

            playerAnchor.GetComponent<FpgPlayerBounds>()
                ?.CaptureInitialSafePosition(out _);
            spawnResolver?.RefreshDistances();
            error = string.Empty;
            return true;
        }

        public void RefreshCoverViews()
        {
            roomInstance?.RefreshCoverViews(combatRuntime?.Covers);
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
