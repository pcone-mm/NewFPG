
using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public interface IFpgFormalPlayerTickDriver
    {
        DomainResult ProcessPlayerTick(
            TickIndex tick,
            FpgFormalCombatRuntimeBundle runtime);

        DomainResult ProcessRoomInteractionTick(
            TickIndex tick,
            FpgFormalCombatRuntimeBundle runtime);

        void BeginRoomInteraction();

        void Clear();
    }

    public interface IFpgFormalCombatPortFactory
    {
        FpgMultiEnemyCombatCapacity Capacity { get; }
        int AttackPatternCapacity { get; }

        bool TryValidateCapacity(
            FpgEncounterProfileData profile,
            FpgEncounterCapacityRequirements requirements,
            out string error);

        bool TryCreate(
            SessionIdAllocator idAllocator,
            FpgEncounterRuntime encounterRuntime,
            FpgEncounterRunContext runContext,
            FpgCombatantAnchorMap anchorMap,
            FpgFormalHitboxRegistry formalHitboxRegistry,
            FpgFormalAttackRuntimeCatalog attackRuntimeCatalog,
            out FpgFormalCombatRuntimeBundle bundle,
            out string error);
    }

    /// <summary>
    /// One-shot room-entry resource import. The encounter host clears this
    /// port after every preparation attempt so failed starts cannot leak state
    /// into a later room.
    /// </summary>
    public interface IFpgFormalPlayerRunResourceImportPort
    {
        bool TrySetNextPlayerRunResources(
            in FpgPlayerRunResourceState state,
            out string error);

        void ClearNextPlayerRunResources();
    }

    public sealed class FpgFormalCombatRuntimeBundle : IDisposable
    {
        private readonly HitboxRegistry staticHitboxRegistry;
        private readonly FpgCombatantAnchorMap anchorMap;
        private readonly D0PlayerEntityView playerEntity;
        private bool disposed;

        internal FpgFormalCombatRuntimeBundle(
            SessionIdAllocator idAllocator,
            FpgEncounterRunContext runContext,
            CombatKernel combatKernel,
            PlayerRuntime player,
            FpgMultiEnemyCombatPort combatPort,
            FpgFormalProjectileWorldPort projectileWorldPort,
            UnityAttackQueryPort attackQueryPort,
            FpgFormalEnemyAttackScheduler attackScheduler,
            IFpgBattleTickSynchronizer synchronizer,
            IFpgPlayerRoomSnapshotPort playerSnapshotPort,
            HitboxRegistry staticHitboxRegistry,
            FpgCombatantAnchorMap anchorMap,
            D0PlayerEntityView playerEntity)
        {
            IdAllocator = idAllocator;
            RunContext = runContext;
            CombatKernel = combatKernel;
            Player = player;
            CombatPort = combatPort;
            ProjectileWorldPort = projectileWorldPort;
            AttackQueryPort = attackQueryPort;
            AttackScheduler = attackScheduler;
            Synchronizer = synchronizer;
            PlayerSnapshotPort = playerSnapshotPort;
            this.staticHitboxRegistry = staticHitboxRegistry;
            this.anchorMap = anchorMap;
            this.playerEntity = playerEntity;
        }

        public SessionIdAllocator IdAllocator { get; }
        public FpgEncounterRunContext RunContext { get; }
        public CombatKernel CombatKernel { get; }
        public PlayerRuntime Player { get; }
        public FpgMultiEnemyCombatPort CombatPort { get; }
        public FpgFormalProjectileWorldPort ProjectileWorldPort { get; }
        public UnityAttackQueryPort AttackQueryPort { get; }
        public FpgFormalEnemyAttackScheduler AttackScheduler { get; }
        public IFpgBattleTickSynchronizer Synchronizer { get; }
        public IFpgPlayerRoomSnapshotPort PlayerSnapshotPort { get; }
        public bool IsDisposed => disposed;

        public bool TryBindPlayerTickDriver(
            IFpgFormalPlayerTickDriver driver,
            out string error)
        {
            if (!(Synchronizer is FpgFormalUnityTickSynchronizer unitySynchronizer))
            {
                error = "Formal runtime bundle has no Unity phase synchronizer.";
                return false;
            }

            return unitySynchronizer.TryBind(this, driver, out error);
        }


        public void ClearForRestart()
        {
            playerEntity.SetGameplayCollidersEnabled(true);
            CombatPort.ResetPresentationState(new TickIndex(0L));
            AttackScheduler.Clear();
            ProjectileWorldPort.ClearAll();
            anchorMap.Clear();
            FpgFormalUnityTickSynchronizer synchronizer =
                Synchronizer as FpgFormalUnityTickSynchronizer;
            synchronizer?.Reset();
            if (!anchorMap.TryRegister(
                    Player.RuntimeId,
                    playerEntity.GameplayAnchor,
                    playerEntity.AimAnchor,
                    playerEntity.GameplayAnchor,
                    playerEntity.gameObject,
                    out _))
            {
                synchronizer?.ReportExternalFailure(RejectReason.BufferCapacity);
            }
        }

        public void ClearForFault()
        {
            playerEntity.SetGameplayCollidersEnabled(false);
            AttackScheduler.Clear();
            ProjectileWorldPort.ClearAll();
            CombatPort.ClearAll();
            anchorMap.Clear();
            FpgFormalUnityTickSynchronizer synchronizer =
                Synchronizer as FpgFormalUnityTickSynchronizer;
            synchronizer?.Reset();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            playerEntity.SetGameplayCollidersEnabled(false);
            CombatPort.ClearAll();
            ProjectileWorldPort.ClearAll();
            AttackScheduler.Clear();
            CombatKernel.Dispose();
            anchorMap.TryUnregister(Player.RuntimeId, false, 0);
            staticHitboxRegistry.TryUnbindPlayerEntity(playerEntity);
            staticHitboxRegistry.ClearDynamicAndStaticBindings();
            disposed = true;
        }


    }

    [DisallowMultipleComponent]
    public sealed class FpgFormalCombatPortFactory : MonoBehaviour,
        IFpgFormalCombatPortFactory,
        IFpgFormalPlayerRunResourceImportPort
    {
        [Header("Player")]
        [SerializeField] private D0CharacterDefinition playerDefinition;
        [SerializeField] private D0PlayerEntityView playerEntity;
        [SerializeField, Min(1)] private int playerBodyGeometryId = 90001;

        [NonSerialized] private bool playerBindingConfigured;
        [NonSerialized] private bool playerBindingLocked;
        [NonSerialized] private D0ThreeCProfile playerThreeCProfile;
        [NonSerialized] private D0CombatFeelProfile playerCombatFeelProfile;
        [NonSerialized] private UnityAttackQuerySettings runtimeAttackQuerySettings;
        [NonSerialized] private int playerMaximumAttackImpactCount;
        [NonSerialized] private bool hasNextPlayerRunResources;
        [NonSerialized] private FpgPlayerRunResourceState nextPlayerRunResources;

        [Header("Spatial")]
        [SerializeField] private HitboxRegistry staticHitboxRegistry;
        [SerializeField] private Transform projectileProxyRoot;
        [SerializeField]
        private UnityAttackQueryTechnicalSettings attackQueryTechnicalSettings =
            default(UnityAttackQueryTechnicalSettings);
        [SerializeField] private UnityProjectileWorldSettings projectileWorldSettings = default(UnityProjectileWorldSettings);

        [NonSerialized] private ProjectileCollisionProxyPool projectileProxyPool;
        [NonSerialized] private FpgFormalCombatRuntimeBundle activeBundle;

        [Header("Fixed combat capacities")]
        [SerializeField, Min(1)] private int enemyCapacity = 16;
        [SerializeField, Min(1)] private int playerHitCommandCapacity = 64;
        [SerializeField, Min(1)] private int attackScheduleCapacity = 128;
        [SerializeField, Min(1)] private int projectileCapacity = 32;
        [SerializeField, Min(1)] private int threatAdvanceCapacity = 64;
        [SerializeField, Min(1)] private int perEnemyThreatCapacity = 8;
        [SerializeField, Min(1)] private int summonCapacity = 16;
        [SerializeField, Min(0)] private int maxTotalSummons = 16;
        [SerializeField, Min(0)] private int maxSummonRecursionDepth = 2;
        [SerializeField, Min(1)] private int attackPatternCapacity = 128;
        [SerializeField, Min(1)] private int groggyDurationTicks = 120;
        [SerializeField, Min(1)] private int vitalsEventCapacity = 128;
        [SerializeField, Min(1)] private int damageFeedbackCapacity = 128;

        [Header("Kernel capacities")]
        [SerializeField, Min(1)] private int projectileBudgetCapacity = 64;
        [SerializeField, Min(1)] private int impactHistoryCapacity = 256;
        [SerializeField, Min(1)] private int shotTargetHistoryCapacity = 256;
        [SerializeField, Min(1)] private int impactQueueCapacity = 128;
        [SerializeField, Min(1)] private int projectileReservationCapacity = 64;

        public FpgMultiEnemyCombatCapacity Capacity => new FpgMultiEnemyCombatCapacity(
            enemyCapacity,
            playerHitCommandCapacity,
            attackScheduleCapacity,
            projectileCapacity,
            threatAdvanceCapacity,
            perEnemyThreatCapacity,
            summonCapacity,
            maxTotalSummons,
            maxSummonRecursionDepth,
            vitalsEventCapacity,
            damageFeedbackCapacity);

        public int AttackPatternCapacity => attackPatternCapacity;
        public D0CharacterDefinition PlayerDefinition => playerDefinition;
        public D0PlayerEntityView PlayerEntity => playerEntity;
        public HitboxRegistry StaticHitboxRegistry => staticHitboxRegistry;
        public D0ThreeCProfile PlayerThreeCProfile => playerThreeCProfile;
        public D0CombatFeelProfile PlayerCombatFeelProfile => playerCombatFeelProfile;
        public UnityAttackQueryTechnicalSettings AttackQueryTechnicalSettings =>
            attackQueryTechnicalSettings;
        public UnityAttackQuerySettings EffectiveAttackQuerySettings =>
            runtimeAttackQuerySettings;
        public bool HasPlayerBinding => playerBindingConfigured
            && playerDefinition != null
            && playerEntity != null;
        public bool IsPlayerBindingLocked => playerBindingLocked;
        public bool HasActiveRuntime => activeBundle != null
            && !activeBundle.IsDisposed;
        public bool HasNextPlayerRunResources => hasNextPlayerRunResources;

        public bool TryConfigurePlayer(
            D0CharacterDefinition definition,
            D0PlayerEntityView entity,
            D0ThreeCProfile threeCProfile,
            D0CombatFeelProfile combatFeelProfile,
            out string error)
        {
            if (playerBindingLocked)
            {
                error = "Formal combat factory player binding is locked for the active session.";
                return false;
            }

            if (definition == null || entity == null
                || threeCProfile == null || combatFeelProfile == null)
            {
                error = "Formal combat factory requires an explicit player definition and entity.";
                return false;
            }

            if (!definition.TryValidate(out error)
                || !entity.TryValidate(out error)
                || !definition.Weapon.TryCreate(
                    out WeaponDefinition configuredWeapon,
                    out error))
            {
                return false;
            }

            if (!combatFeelProfile.TryCreateAttackQuerySettings(
                    threeCProfile,
                    attackQueryTechnicalSettings,
                    out UnityAttackQuerySettings composedQuerySettings,
                    out error))
            {
                return false;
            }

            if (!entity.gameObject.scene.IsValid()
                || entity.gameObject.scene != gameObject.scene)
            {
                error = "Formal combat factory player entity must belong to the factory scene.";
                return false;
            }

            if (playerBindingConfigured)
            {
                if (playerDefinition == definition && playerEntity == entity
                    && playerThreeCProfile == threeCProfile
                    && playerCombatFeelProfile == combatFeelProfile
                    && playerMaximumAttackImpactCount
                        == configuredWeapon.MaximumAttackImpactCount)
                {
                    error = string.Empty;
                    return true;
                }

                error = "Formal combat factory player binding is already configured.";
                return false;
            }

            playerDefinition = definition;
            playerEntity = entity;
            playerThreeCProfile = threeCProfile;
            playerCombatFeelProfile = combatFeelProfile;
            runtimeAttackQuerySettings = composedQuerySettings;
            playerMaximumAttackImpactCount =
                configuredWeapon.MaximumAttackImpactCount;
            playerBindingConfigured = true;
            error = string.Empty;
            return true;
        }

        public void ClearPlayerBinding()
        {
            if (playerBindingLocked && HasActiveRuntime)
            {
                return;
            }

            activeBundle = null;
            playerDefinition = null;
            playerEntity = null;
            playerThreeCProfile = null;
            playerCombatFeelProfile = null;
            runtimeAttackQuerySettings = default(UnityAttackQuerySettings);
            playerMaximumAttackImpactCount = 0;
            ClearNextPlayerRunResources();
            playerBindingConfigured = false;
            playerBindingLocked = false;
        }

        public bool TrySetNextPlayerRunResources(
            in FpgPlayerRunResourceState state,
            out string error)
        {
            if (playerBindingLocked || HasActiveRuntime)
            {
                error = "Player run resources cannot change after runtime creation.";
                return false;
            }

            if (!playerBindingConfigured || playerDefinition == null
                || playerDefinition.Weapon == null)
            {
                error = "Player run resources require a composed player binding.";
                return false;
            }

            if (!state.IsValid)
            {
                error = "Player run resources are invalid.";
                return false;
            }

            if (!string.Equals(
                    state.CharacterId,
                    playerDefinition.CharacterId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    state.WeaponId,
                    playerDefinition.Weapon.WeaponId,
                    StringComparison.Ordinal))
            {
                error =
                    "Player run resources do not match the composed character and weapon.";
                return false;
            }

            if (state.Life > playerDefinition.Life
                || state.Barrier > playerDefinition.Barrier
                || state.Ammo > playerDefinition.Weapon.MagazineCapacity)
            {
                error =
                    "Player run resources exceed the composed character or weapon capacity.";
                return false;
            }

            nextPlayerRunResources = state;
            hasNextPlayerRunResources = true;
            error = string.Empty;
            return true;
        }

        public void ClearNextPlayerRunResources()
        {
            hasNextPlayerRunResources = false;
            nextPlayerRunResources = default(FpgPlayerRunResourceState);
        }

        public bool TryValidateCapacity(
            FpgEncounterProfileData profile,
            FpgEncounterCapacityRequirements requirements,
            out string error)
        {
            if (profile == null)
            {
                error = "Formal combat factory requires an encounter profile.";
                return false;
            }

            if (!playerBindingConfigured || playerMaximumAttackImpactCount <= 0)
            {
                error = "Formal combat factory requires a configured player weapon.";
                return false;
            }

            if (!TryValidatePlayerAttackCapacity(
                    playerMaximumAttackImpactCount,
                    playerHitCommandCapacity,
                    impactHistoryCapacity,
                    impactQueueCapacity,
                    TargetSelector.DefaultCandidateCapacity,
                    out error))
            {
                return false;
            }

            if (enemyCapacity < requirements.SimultaneousCombatants
                || projectileCapacity < profile.ProjectileCapacity
                || threatAdvanceCapacity < profile.ThreatCapacity
                || summonCapacity < Math.Max(1, requirements.SummonUpperBound)
                || maxTotalSummons < requirements.SummonUpperBound
                || attackScheduleCapacity < profile.ThreatCapacity
                || perEnemyThreatCapacity <= 0
                || projectileBudgetCapacity < profile.ProjectileCapacity
                || projectileReservationCapacity < profile.ProjectileCapacity
                || impactQueueCapacity < profile.ThreatCapacity + profile.ProjectileCapacity
                || vitalsEventCapacity < requirements.SimultaneousCombatants + 1
                || damageFeedbackCapacity < playerHitCommandCapacity)
            {
                error = "Formal combat factory fixed capacities are below encounter preflight requirements.";
                return false;
            }

            UnityAttackQuerySettings querySettings = EffectiveAttackQuerySettings;
            if (!attackQueryTechnicalSettings.IsValid
                || !querySettings.IsValid || !projectileWorldSettings.IsValid
                || querySettings.HitboxLayerMask != projectileWorldSettings.HitboxLayerMask
                || querySettings.BlockerLayerMask != projectileWorldSettings.BlockerLayerMask)
            {
                error = "Formal combat factory spatial settings are invalid.";
                return false;
            }

            if (!TryEnsureProjectileProxyPool(out error)
                || projectileProxyPool.Capacity != projectileCapacity)
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Formal projectile proxy capacity is invalid."
                    : error;
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal static bool TryValidatePlayerAttackCapacity(
            int maximumAttackImpactCount,
            int commandCapacity,
            int ledgerCapacity,
            int queueCapacity,
            int queryCandidateCapacity,
            out string error)
        {
            long requiredLedgerCapacity =
                (long)queueCapacity + maximumAttackImpactCount;
            if (maximumAttackImpactCount <= 0
                || commandCapacity < maximumAttackImpactCount
                || queueCapacity < maximumAttackImpactCount
                || queryCandidateCapacity < maximumAttackImpactCount
                || ledgerCapacity < requiredLedgerCapacity)
            {
                error =
                    "Formal player attack capacities cannot hold one maximum-impact release and the active impact queue.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryEnsureProjectileProxyPool(out string error)
        {
            int hitboxLayerMask = playerBindingConfigured
                && runtimeAttackQuerySettings.IsValid
                    ? runtimeAttackQuerySettings.HitboxLayerMask
                    : attackQueryTechnicalSettings.HitboxLayerMask;
            if (projectileCapacity <= 0 || hitboxLayerMask == 0)
            {
                error = "Formal projectile proxy configuration is invalid.";
                return false;
            }

            if (projectileProxyPool != null
                && (projectileProxyPool.Capacity != projectileCapacity
                    || projectileProxyPool.HitboxLayerMask != hitboxLayerMask))
            {
                projectileProxyPool.Dispose();
                projectileProxyPool = null;
            }

            projectileProxyPool ??= new ProjectileCollisionProxyPool(
                projectileCapacity,
                hitboxLayerMask,
                projectileProxyRoot == null ? transform : projectileProxyRoot);
            error = string.Empty;
            return true;
        }

        private void OnDestroy()
        {
            projectileProxyPool?.Dispose();
            projectileProxyPool = null;
        }

        private DomainResult ImportPlayerRunResources(
            PlayerRuntime player,
            in FpgPlayerRunResourceState state)
        {
            return FpgPlayerRunResourceTransfer.TryRestoreRoomEntry(
                player,
                playerDefinition.CharacterId,
                playerDefinition.Weapon.WeaponId,
                state);
        }


        public bool TryCreate(
            SessionIdAllocator idAllocator,
            FpgEncounterRuntime encounterRuntime,
            FpgEncounterRunContext runContext,
            FpgCombatantAnchorMap anchorMap,
            FpgFormalHitboxRegistry formalHitboxRegistry,
            FpgFormalAttackRuntimeCatalog attackRuntimeCatalog,
            out FpgFormalCombatRuntimeBundle bundle,
            out string error)
        {
            bundle = null;
            bool importPlayerRunResources = hasNextPlayerRunResources;
            FpgPlayerRunResourceState playerRunResources =
                nextPlayerRunResources;
            ClearNextPlayerRunResources();
            if (playerBindingLocked)
            {
                error = "Formal combat factory already created a runtime for its player binding.";
                return false;
            }

            if (idAllocator == null || encounterRuntime == null || !runContext.IsValid
                || anchorMap == null || formalHitboxRegistry == null
                || attackRuntimeCatalog == null || !playerBindingConfigured
                || playerDefinition == null
                || playerEntity == null || staticHitboxRegistry == null)
            {
                error = "Formal combat factory is missing explicit runtime, player, catalog, anchor or hitbox references.";
                return false;
            }

            if (!playerDefinition.TryValidate(out error)
                || !playerEntity.TryValidate(out error)
                || !attackRuntimeCatalog.TryValidate(out error)
                || !playerDefinition.Weapon.TryCreate(out WeaponDefinition weaponDefinition, out error))
            {
                return false;
            }

            if (weaponDefinition.MaximumAttackImpactCount
                != playerMaximumAttackImpactCount)
            {
                error = "Formal player weapon changed after its runtime binding was configured.";
                return false;
            }

            RuntimeId playerRuntimeId = idAllocator.NextRuntimeId();
            RuntimeId staticEnemyPlaceholder = idAllocator.NextRuntimeId();
            UnityAttackQuerySettings querySettings = EffectiveAttackQuerySettings;
            if (!staticHitboxRegistry.TryValidateStaticBindings(querySettings, out error)
                || !staticHitboxRegistry.ResetForSession(
                    playerRuntimeId,
                    staticEnemyPlaceholder,
                    out error)
                || !staticHitboxRegistry.TryBindPlayerEntity(
                    playerRuntimeId,
                    playerEntity,
                    new GeometryId(playerBodyGeometryId),
                    out error))
            {
                staticHitboxRegistry.ClearDynamicAndStaticBindings();
                return false;
            }

            if (!formalHitboxRegistry.TrySetExternalGeometryRegistry(
                    staticHitboxRegistry,
                    out error))
            {
                staticHitboxRegistry.ClearDynamicAndStaticBindings();
                return false;
            }

            if (!anchorMap.TryRegister(
                    playerRuntimeId,
                    playerEntity.GameplayAnchor,
                    playerEntity.AimAnchor,
                    playerEntity.GameplayAnchor,
                    playerEntity.gameObject,
                    out error))
            {
                staticHitboxRegistry.ClearDynamicAndStaticBindings();
                return false;
            }

            if (!projectileProxyPool.TryPrepare(staticHitboxRegistry, out error))
            {
                anchorMap.TryUnregister(playerRuntimeId, false, 0);
                staticHitboxRegistry.ClearDynamicAndStaticBindings();
                return false;
            }


            try
            {
                IUnityPhysicsQueryBackend physics = new UnityPhysicsQueryBackend(
                    SpatialContract.AttackQueryCandidateCapacity);
                FpgCombinedHitboxLookup combinedLookup = new FpgCombinedHitboxLookup(
                    staticHitboxRegistry,
                    formalHitboxRegistry);
                UnityAttackQueryPort attackQueryPort = new UnityAttackQueryPort(
                    combinedLookup,
                    querySettings,
                    physics);
                FpgFormalProjectileWorldPort projectileWorldPort =
                    new FpgFormalProjectileWorldPort(
                        anchorMap,
                        combinedLookup,
                        projectileWorldSettings,
                        projectileCapacity,
                        physics,
                        projectileProxyPool);
                CombatKernel combatKernel = new CombatKernel(
                    projectileBudgetCapacity,
                    impactHistoryCapacity,
                    shotTargetHistoryCapacity,
                    impactQueueCapacity,
                    projectileReservationCapacity: projectileReservationCapacity);
                PlayerRuntime player = new PlayerRuntime(
                    new CombatantState(
                        playerRuntimeId,
                        CombatantKind.Player,
                        playerDefinition.Life,
                        playerDefinition.Barrier,
                        0),
                    new ExposureRuntime(PlayerExposureState.Exposed),
                    new WeaponRuntime(weaponDefinition));
                if (importPlayerRunResources)
                {
                    DomainResult imported = ImportPlayerRunResources(
                        player,
                        playerRunResources);
                    if (!imported.IsSuccess)
                    {
                        throw new InvalidOperationException(imported.ToString());
                    }
                }

                FpgEncounterRuntimeSummonSink summonSink =
                    new FpgEncounterRuntimeSummonSink(encounterRuntime);
                FpgMultiEnemyCombatPort combatPort = new FpgMultiEnemyCombatPort(
                    combatKernel,
                    player,
                    idAllocator,
                    Capacity,
                    new TickDuration(groggyDurationTicks),
                    projectileWorldPort,
                    summonSink);
                FpgFormalEnemyAttackScheduler scheduler = new FpgFormalEnemyAttackScheduler(
                    combatPort,
                    runContext,
                    attackRuntimeCatalog,
                    enemyCapacity,
                    attackPatternCapacity);
                FpgFormalUnityTickSynchronizer synchronizer =
                    new FpgFormalUnityTickSynchronizer(
                        scheduler,
                        anchorMap,
                        formalHitboxRegistry,
                        staticHitboxRegistry,
                        physics);
                FpgFormalPlayerRoomSnapshotPort snapshotPort =
                    new FpgFormalPlayerRoomSnapshotPort(player);
                bundle = new FpgFormalCombatRuntimeBundle(
                    idAllocator,
                    runContext,
                    combatKernel,
                    player,
                    combatPort,
                    projectileWorldPort,
                    attackQueryPort,
                    scheduler,
                    synchronizer,
                    snapshotPort,
                    staticHitboxRegistry,
                    anchorMap,
                    playerEntity);
                playerEntity.SetGameplayCollidersEnabled(true);
                activeBundle = bundle;
                playerBindingLocked = true;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                playerEntity.SetGameplayCollidersEnabled(false);
                anchorMap.TryUnregister(playerRuntimeId, false, 0);
                staticHitboxRegistry.ClearDynamicAndStaticBindings();
                error = "Formal combat port construction failed: " + exception.Message;
                return false;
            }
        }
    }

    public sealed class FpgFormalUnityTickSynchronizer : IFpgBattleTickSynchronizer
    {
        private static readonly FpgBattleTickPhase[] TickOrder =
        {
            FpgBattleTickPhase.LifecycleBoundary,
            FpgBattleTickPhase.EnemyRecovery,
            FpgBattleTickPhase.PlayerAttackAndHit,
            FpgBattleTickPhase.DeathAndThreatCleanup,
            FpgBattleTickPhase.EnemyAttackDirector,
            FpgBattleTickPhase.ThreatAndProjectileAdvance,
            FpgBattleTickPhase.ImpactResolution,
            FpgBattleTickPhase.EncounterCompletion
        };


        private readonly FpgFormalEnemyAttackScheduler scheduler;
        private readonly FpgCombatantAnchorMap anchorMap;
        private readonly FpgFormalHitboxRegistry formalHitboxes;
        private readonly HitboxRegistry staticHitboxes;
        private readonly IUnityPhysicsQueryBackend physics;

        private FpgFormalCombatRuntimeBundle runtime;
        private IFpgFormalPlayerTickDriver playerDriver;
        private TickIndex phaseTick = TickIndex.Invalid;
        private int nextPhaseIndex;
        private RejectReason externalFailure = RejectReason.None;

        public FpgFormalUnityTickSynchronizer(
            FpgFormalEnemyAttackScheduler scheduler,
            FpgCombatantAnchorMap anchorMap,
            FpgFormalHitboxRegistry formalHitboxes,
            HitboxRegistry staticHitboxes,
            IUnityPhysicsQueryBackend physics)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.anchorMap = anchorMap ?? throw new ArgumentNullException(nameof(anchorMap));
            this.formalHitboxes = formalHitboxes ?? throw new ArgumentNullException(nameof(formalHitboxes));
            this.staticHitboxes = staticHitboxes ?? throw new ArgumentNullException(nameof(staticHitboxes));
            this.physics = physics ?? throw new ArgumentNullException(nameof(physics));
        }

        public bool TryBind(
            FpgFormalCombatRuntimeBundle runtime,
            IFpgFormalPlayerTickDriver playerDriver,
            out string error)
        {
            if (this.runtime != null || runtime == null || playerDriver == null)
            {
                error = "Formal phase synchronizer requires one runtime and player driver binding.";
                return false;
            }

            this.runtime = runtime;
            this.playerDriver = playerDriver;
            error = string.Empty;
            return true;
        }

        public void ReportExternalFailure(RejectReason reason)
        {
            if (reason != RejectReason.None)
            {
                externalFailure = reason;
            }
        }

        public void Reset()
        {
            if (playerDriver is FpgFormalPlayerTickDriver concretePlayerDriver)
            {
                concretePlayerDriver.ResetRuntimeState();
            }
            else
            {
                playerDriver?.Clear();
            }

            phaseTick = TickIndex.Invalid;
            nextPhaseIndex = 0;
            externalFailure = RejectReason.None;
        }

        public DomainResult Synchronize(FpgBattleTickPhase phase, TickIndex tick)
        {
            if (!tick.IsValid || runtime == null || playerDriver == null
                || !anchorMap.IsInitialized || !formalHitboxes.IsInitialized
                || !staticHitboxes.IsReadyForQueries)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (externalFailure != RejectReason.None)
            {
                return DomainResult.Rejected(externalFailure);
            }

            if (nextPhaseIndex < 0 || nextPhaseIndex >= TickOrder.Length
                || phase != TickOrder[nextPhaseIndex])
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            if (nextPhaseIndex == 0)
            {
                if (phaseTick.IsValid && tick <= phaseTick)
                {
                    return DomainResult.Rejected(RejectReason.WrongTick);
                }

                phaseTick = tick;
                physics.SyncTransforms();
            }
            else if (tick != phaseTick)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            DomainResult result = DomainResult.Success;
            if (phase == FpgBattleTickPhase.PlayerAttackAndHit)
            {
                result = playerDriver.ProcessPlayerTick(tick, runtime);
            }

            else if (phase == FpgBattleTickPhase.EnemyAttackDirector)
            {
                result = scheduler.Tick(tick);
            }

            if (!result.IsSuccess)
            {
                return result;
            }

            nextPhaseIndex++;
            if (nextPhaseIndex == TickOrder.Length)
            {
                nextPhaseIndex = 0;
            }

            return DomainResult.Success;
        }
    }

    public sealed class FpgFormalPlayerRoomSnapshotPort : IFpgPlayerRoomSnapshotPort
    {
        private readonly PlayerRuntime player;
        private CombatantResourceSnapshot combatantSnapshot;
        private WeaponRuntimeSnapshot weaponSnapshot;
        private ExposureRuntimeSnapshot exposureSnapshot;
        private bool captured;

        public FpgFormalPlayerRoomSnapshotPort(PlayerRuntime player)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public DomainResult CaptureEntrySnapshot()
        {
            if (captured)
            {
                return DomainResult.Rejected(RejectReason.DuplicateSequence);
            }

            combatantSnapshot = player.Combatant.CaptureResources();
            weaponSnapshot = player.Weapon.CaptureRoomSnapshot();
            exposureSnapshot = player.Exposure.CaptureRoomSnapshot();
            captured = true;
            return DomainResult.Success;
        }

        public DomainResult RestoreEntrySnapshot()
        {
            if (!captured)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            DomainResult combatant = player.Combatant.RestoreResources(combatantSnapshot);
            if (!combatant.IsSuccess)
            {
                return combatant;
            }

            DomainResult weapon = player.Weapon.RestoreRoomSnapshot(weaponSnapshot);
            if (!weapon.IsSuccess)
            {
                return weapon;
            }

            return player.Exposure.RestoreRoomSnapshot(exposureSnapshot);
        }

        public void KeepAcrossWave()
        {
        }
    }

}
