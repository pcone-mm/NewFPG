using System;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class BattleSessionHost : MonoBehaviour
    {
        private const int PlayerBodyGeometryId = 1001;

        private BattleSessionFactory factory;
        private UnityBattleInputSource inputSource;
        private UnityBattleInputSource inputOverride;
        private UnityAttackQueryPort unityAttackQueryPort;
        private RecordingAttackQueryPort recordingAttackQueryPort;
        private UnityProjectileWorldPort unityProjectileWorldPort;
        private RecordingProjectileWorldPort recordingProjectileWorldPort;
        private ObservingProjectileWorldPort observingProjectileWorldPort;
        private FixedPlayerShotPresentationFeed playerShotPresentationFeed;
        private PlayerShotPresentationBridge playerShotPresentationBridge;
        private D0PlayerEntityView d0PlayerEntity;
        private D0EnemyEntityWorld d0EnemyEntityWorld;
        private ProjectileCollisionProxyPool projectileCollisionProxyPool;
        private D0EnemyBehaviorController d0EnemyBehaviorController;
        private D0ShotCameraFeedbackController d0ShotCameraFeedbackController;
        private readonly RaycastHit[] combatAimRaycastBuffer = new RaycastHit[8];
        private readonly ProjectWideBattleInputAdapter projectWideBattleInputAdapter =
            new ProjectWideBattleInputAdapter();
        private long nextControlSequence = 1L;
        private int earlyPauseControlFrame = -1;
        private BattleSessionState earlyPauseControlStateBefore = BattleSessionState.Disposed;
        private bool shutdown;

        public BattleSession Session { get; private set; }

        public BattleSceneContext Context { get; private set; }

        public BattleScenarioConfig ScenarioConfig { get; private set; }

        public HitboxRegistry HitboxRegistry { get; private set; }

        public UnityAttackQuerySettings AttackQuerySettings { get; private set; }

        public SpatialPortTranscript SpatialTranscript { get; private set; }

        public ProjectileCollisionProxyPool ProjectileCollisionProxyPool => projectileCollisionProxyPool;

        public D0EnemyEntityWorld EnemyEntityWorld => d0EnemyEntityWorld;

        public ObservingProjectileWorldPort ObservingProjectileWorldPort => observingProjectileWorldPort;

        public IProjectilePresentationFeed ProjectilePresentationFeed => observingProjectileWorldPort?.Feed;

        /// <summary>
        /// Read-only, locally bounded feed of shots whose spatial transaction
        /// has committed. Presentation consumers must not retain its writer.
        /// </summary>
        public IPlayerShotPresentationFeed PlayerShotPresentationFeed => playerShotPresentationFeed;

        public bool IsInitialized => Session != null
            && Session.State != BattleSessionState.Disposed
            && !shutdown;

        /// <summary>
        /// True only while the bound battle session can accept gameplay input.
        /// Scene-facing controllers use this instead of duplicating session-state
        /// checks, so locomotion and look pause with the combat simulation.
        /// </summary>
        public bool IsSessionRunning => IsInitialized
            && Session.State == BattleSessionState.Running;

        public bool IsSpatialQueryReady => IsInitialized
            && unityAttackQueryPort != null
            && recordingAttackQueryPort != null
            && SpatialTranscript != null
            && HitboxRegistry != null
            && HitboxRegistry.IsReadyForQueries
            && HitboxRegistry.Count > 0;

        public bool IsProjectileWorldReady => IsSpatialQueryReady
            && unityProjectileWorldPort != null
            && recordingProjectileWorldPort != null
            && observingProjectileWorldPort != null
            && unityProjectileWorldPort.IsSessionBound
            && unityProjectileWorldPort.PlayerRuntimeId == Session.PlayerRuntimeId
            && unityProjectileWorldPort.EnemyRuntimeId == Session.EnemyRuntimeId;

        public string LastError { get; private set; } = string.Empty;

        public int LastExecutedSteps { get; private set; }

        /// <summary>
        /// Installs a Unity-facing input source for the opt-in D0 performance
        /// harness. Normal gameplay never sets this reference and therefore
        /// continues to capture directly from keyboard and mouse devices.
        /// </summary>
        public void SetInputOverrideForD0Performance(UnityBattleInputSource nextInputSource)
        {
            inputOverride = nextInputSource;
        }

        /// <summary>
        /// Returns the host to normal device capture after an opt-in D0
        /// performance run. This only affects FPG.Unity input plumbing and
        /// does not enter the deterministic battle input contract.
        /// </summary>
        public void ClearInputOverrideForD0Performance()
        {
            inputOverride = null;
        }

        /// <summary>
        /// Raised only after a replacement session and all of its scene-facing
        /// dependencies have been committed successfully.
        /// </summary>
        public event Action<BattleSessionHost> SessionRestarted;

        public bool TryInitialize(
            BattleSceneContext context,
            BattleScenarioConfig scenarioConfig,
            out string error)
        {
            if (IsInitialized)
            {
                error = "BattleSessionHost is already initialized.";
                return false;
            }

            if (context == null || scenarioConfig == null)
            {
                error = "BattleSceneContext and BattleScenarioConfig are required.";
                LastError = error;
                return false;
            }

            if (context.SessionHost != this)
            {
                error = "BattleSceneContext must reference this BattleSessionHost.";
                LastError = error;
                return false;
            }

            if (context.ScenarioConfig != scenarioConfig)
            {
                error = "BattleSceneContext must reference the supplied BattleScenarioConfig.";
                LastError = error;
                return false;
            }

            bool roomInitialized = false;
            bool initializationCommitted = false;
            bool playerSceneServicesBound = false;
            D0PlayerEntityView nextD0PlayerEntity = null;
            D0EnemyEntityWorld nextD0EnemyEntityWorld = null;
            try
            {
                if (!context.TryInitializeRoom(out error))
            {
                LastError = $"Room scene binding is invalid: {error}";
                return false;
            }
            roomInitialized = context.RoomBinding != null
                && context.RoomBinding.IsInitialized;

            D0EnemyBehaviorController nextD0EnemyBehaviorController = null;
            D0ShotCameraFeedbackController nextD0ShotCameraFeedbackController = null;

            if (scenarioConfig.UsesAuthoredScenario)
            {
                Context = context;
                ScenarioConfig = scenarioConfig;
                nextD0PlayerEntity = context.PlayerEntity;
                if (!TryBindPlayerSceneServices(
                        context,
                        scenarioConfig.AuthoredScenario,
                        nextD0PlayerEntity,
                        out error))
                {
                    LastError = $"Unable to bind player Entity scene services: {error}";
                    return false;
                }

                playerSceneServicesBound = true;
                if (!D0ThreeCRuntimeProfileApplier.TryApplyAuthoredPresentation(
                        context,
                        out error))
                {
                    LastError = $"D0 3C runtime presentation is invalid: {error}";
                    return false;
                }

                nextD0EnemyBehaviorController = context.D0EnemyBehaviorController;
                nextD0ShotCameraFeedbackController = context.D0ShotCameraFeedbackController;
                nextD0EnemyEntityWorld = context.EnemyEntityWorld;
                if (nextD0EnemyEntityWorld != null
                    && !nextD0EnemyEntityWorld.TryPrepareScenario(
                        scenarioConfig.AuthoredScenario,
                        context,
                        out error))
                {
                    LastError = $"Unable to prepare enemy EntityWorld: {error}";
                    return false;
                }

                if (!TryBindEnemySceneServices(
                        context,
                        scenarioConfig.AuthoredScenario,
                        nextD0EnemyEntityWorld,
                        out error))
                {
                    LastError = $"Unable to bind enemy Entity scene services: {error}";
                    return false;
                }

                nextD0EnemyEntityWorld?.RefreshRuntimeBindings();
                if (!context.TryValidateD0RuntimeBindings(out error))
                {
                    LastError = $"D0 scene runtime bindings are invalid: {error}";
                    return false;
                }
            }

            HitboxRegistry registry = context.HitboxRegistry;
            if (registry == null)
            {
                error = "BattleSceneContext must reference a HitboxRegistry.";
                LastError = error;
                return false;
            }

            if (context.PlayerEntity == null
                || context.PlayerAnchor == null
                || nextD0EnemyEntityWorld == null
                || nextD0EnemyEntityWorld.ActiveGameplayAnchor == null
                || nextD0EnemyEntityWorld.ActiveProjectileSpawnAnchor == null
                || context.PlayerAnchor == nextD0EnemyEntityWorld.ActiveGameplayAnchor)
            {
                error = "BattleSceneContext must provide a complete player Entity and EnemyEntityWorld with distinct active anchors.";
                LastError = error;
                return false;
            }

            if (!scenarioConfig.TryValidateSpatialConfiguration(out error)
                || !TryCreateScenarioDefinitionForContext(context, scenarioConfig, out ScenarioDefinition definition, out error))
            {
                LastError = error;
                return false;
            }

            if (scenarioConfig.UsesAuthoredScenario
                && !TryPlaceAuthoredPlayer(
                    context,
                    scenarioConfig.AuthoredScenario,
                    out error))
            {
                LastError = $"Unable to place authored player spawn: {error}";
                return false;
            }

            if (D0RuntimePerformanceStressDriver.IsRequested())
            {
                definition = D0RuntimePerformanceStressDriver.CreateScenarioDefinitionForStress(definition);
            }

            if (!registry.TryValidateStaticBindings(
                    scenarioConfig.AttackQuerySettings,
                    out error))
            {
                LastError = error;
                return false;
            }

            if (!registry.TryInitialize(out error))
            {
                LastError = error;
                return false;
            }

            Context = context;
            ScenarioConfig = scenarioConfig;
            HitboxRegistry = registry;
            d0EnemyBehaviorController = nextD0EnemyBehaviorController;
            d0ShotCameraFeedbackController = nextD0ShotCameraFeedbackController;
            d0PlayerEntity = nextD0PlayerEntity;
            d0EnemyEntityWorld = nextD0EnemyEntityWorld;

            if (!TryCreateCollisionProxyPool(
                    definition,
                    scenarioConfig.AttackQuerySettings,
                    registry,
                    context.ProjectilesRoot,
                    out ProjectileCollisionProxyPool nextCollisionProxyPool,
                    out error))
            {
                LastError = error;
                return false;
            }

            if (!TryPreparePresentation(context, definition, out error))
            {
                nextCollisionProxyPool.Dispose();
                LastError = error;
                return false;
            }

            if (scenarioConfig.UsesAuthoredScenario
                && !context.TryPrepareCombatVfx(
                    scenarioConfig.AuthoredScenario,
                    out string vfxPrepareError))
            {
                nextCollisionProxyPool.Dispose();
                context.PresentationCoordinator.DisposePresentation();
                LastError = $"Unable to prewarm Combat VFX World: {vfxPrepareError}";
                return false;
            }

            BattleSessionFactory nextFactory = new BattleSessionFactory();
            if (!TryComposeInitialSession(
                    nextFactory,
                    definition,
                    registry,
                    scenarioConfig,
                    context.PlayerAnchor,
                    nextD0PlayerEntity,
                    nextD0EnemyEntityWorld.ActiveGameplayAnchor,
                    nextD0EnemyEntityWorld.ActiveProjectileSpawnAnchor,
                    nextCollisionProxyPool,
                    out SessionComposition composition,
                    out RejectReason rejectReason,
                    out error))
            {
                nextCollisionProxyPool.Dispose();
                context.PresentationCoordinator.DisposePresentation();
                LastError = error;
                return false;
            }

            DomainResult started = StartFreshSession(composition.Session);
            if (!started.IsSuccess)
            {
                error = $"Unable to start BattleSession: {started.RejectReason}.";
                LastError = error;
                composition.Session.Dispose();
                nextCollisionProxyPool.Dispose();
                registry.ClearDynamicAndStaticBindings();
                context.PresentationCoordinator.DisposePresentation();
                return false;
            }

            if (d0EnemyEntityWorld != null)
            {
                if (!d0EnemyEntityWorld.TryBindInitialRuntime(
                        composition.Session.PlayerRuntimeId,
                        composition.Session.EnemyRuntimeId,
                        out error))
                {
                    composition.Session.Dispose();
                    nextCollisionProxyPool.Dispose();
                    registry.ClearDynamicAndStaticBindings();
                    context.PresentationCoordinator.DisposePresentation();
                    LastError = error;
                    return false;
                }

                if (!composition.UnityProjectileWorldPort.TryRebindEnemyAnchors(
                        d0EnemyEntityWorld.ActiveGameplayAnchor,
                        d0EnemyEntityWorld.ActiveProjectileSpawnAnchor,
                        out error))
                {
                    composition.Session.Dispose();
                    nextCollisionProxyPool.Dispose();
                    registry.ClearDynamicAndStaticBindings();
                    context.PresentationCoordinator.DisposePresentation();
                    LastError = error;
                    return false;
                }

                d0EnemyEntityWorld.RefreshRuntimeBindings();
            }

            if (!context.PresentationCoordinator.TryBind(
                    composition.Session,
                    composition.ObservingProjectileWorldPort.Feed,
                    out error))
            {
                LastError = $"Unable to bind projectile presentation: {error}";
                composition.Session.Dispose();
                nextCollisionProxyPool.Dispose();
                registry.ClearDynamicAndStaticBindings();
                context.PresentationCoordinator.DisposePresentation();
                return false;
            }

            factory = nextFactory;
            projectileCollisionProxyPool = nextCollisionProxyPool;
            CommitComposition(composition);
            if (scenarioConfig.UsesAuthoredScenario)
            {
                context.BeginCombatVfx();
            }
            inputSource = new UnityBattleInputSource();
            ApplyD0InputBufferProfile(inputSource);
            inputOverride = null;
            nextControlSequence = 2L;
            earlyPauseControlFrame = -1;
            earlyPauseControlStateBefore = BattleSessionState.Disposed;
            shutdown = false;
            LastError = string.Empty;
            LastExecutedSteps = 0;
            if (Application.isPlaying)
            {
                projectWideBattleInputAdapter.SetEarlyPausePressedHandler(
                    TryHandleProjectWidePausePerformed);
            }

            initializationCommitted = true;
            error = string.Empty;
            return true;
            }
            finally
            {
                if (!initializationCommitted)
                {
                    if (playerSceneServicesBound)
                    {
                        UnbindPlayerSceneServices(context, nextD0PlayerEntity);
                    }

                    if (roomInitialized && context.RoomBinding != null)
                    {
                        context.RoomBinding.ClearRuntimeRoom();
                    }

                    if (nextD0EnemyEntityWorld != null)
                    {
                        nextD0EnemyEntityWorld.UnbindAndDeactivateAll();
                    }

                    if (HitboxRegistry != null)
                    {
                        HitboxRegistry.ClearDynamicAndStaticBindings();
                    }

                    if (context.PresentationCoordinator != null)
                    {
                        context.PresentationCoordinator.DisposePresentation();
                    }

                    if (context.RoomBinding != null)
                    {
                        FpgRoomPlaytestOverrides.Clear();
                    }

                    Context = null;
                    ScenarioConfig = null;
                    HitboxRegistry = null;
                    d0EnemyBehaviorController = null;
                    d0ShotCameraFeedbackController = null;
                    d0PlayerEntity = null;
                    d0EnemyEntityWorld = null;
                }
            }
        }

        private bool TryBindPlayerSceneServices(
            BattleSceneContext context,
            D0CombatScenarioDefinition scenario,
            D0PlayerEntityView playerEntity,
            out string error)
        {
            error = string.Empty;
            if (context == null || scenario == null || scenario.Player == null
                || playerEntity == null)
            {
                error = "Player scene services require context, scenario and a complete player Entity.";
                return false;
            }

            Actor2DPresenter actorPresenter = playerEntity.ActorPresenter;
            CombatPresentationProfile presentationProfile =
                context.D0PresentationProfile;
            D0ActorPresentationDefinition actorPresentation =
                scenario.Player.ActorPresentation;
            if (actorPresenter == null || presentationProfile == null
                || actorPresentation == null
                || !actorPresenter.TryConfigureRuntime(
                    playerEntity.SkeletonAnimation,
                    presentationProfile,
                    true,
                    playerEntity.VisualRoot,
                    actorPresentation,
                    out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Player Entity requires an authored presenter, global profile and state presentation.";
                }

                return false;
            }

            CombatLabPlayerController controller = playerEntity.Controller;
            D0PlayerBarrierPresentationController barrier = playerEntity.Barrier;
            PlayerWeaponPresentationController weaponController =
                context.PlayerWeaponPresentationController;
            if (controller == null || barrier == null || weaponController == null)
            {
                error = "Player Entity scene services require movement, barrier and weapon presentation controllers.";
                return false;
            }

            if (!controller.TryBindSceneServices(this, out error))
            {
                return false;
            }

            if (!barrier.TryBindSceneServices(this, out error))
            {
                controller.UnbindSceneServices();
                return false;
            }

            if (!weaponController.TryBindSceneServices(
                    this,
                    context.MainCamera,
                    out error)
                || !weaponController.TryBindPlayerEntity(
                    playerEntity,
                    scenario.Player.Weapon,
                    out error))
            {
                weaponController.UnbindSceneServices();
                barrier.UnbindSceneServices();
                controller.UnbindSceneServices();
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryBindEnemySceneServices(
            BattleSceneContext context,
            D0CombatScenarioDefinition scenario,
            D0EnemyEntityWorld entityWorld,
            out string error)
        {
            error = string.Empty;
            D0EnemyBehaviorController behavior = context == null
                ? null
                : context.D0EnemyBehaviorController;
            D0EnemyEntityView activeEntity = entityWorld == null
                ? null
                : entityWorld.ActiveEntity;
            D0EncounterDefinition encounter = scenario == null
                ? null
                : scenario.Encounter;
            D0EnemyBehaviorProfile profile = encounter == null
                || encounter.Enemy == null
                ? null
                : encounter.Enemy.BehaviorProfile;
            if (behavior == null || activeEntity == null
                || encounter == null || profile == null)
            {
                error = "Enemy scene services require behavior, encounter and an active complete Entity.";
                return false;
            }

            D0LuanSummonHudieDefinition summon = scenario.LuanSummonHudie;
            behavior.Configure(
                this,
                profile,
                encounter,
                activeEntity.VisualRoot,
                activeEntity.GameplayAnchor,
                activeEntity.SkeletonAnimation,
                summon);
            if (!behavior.TryValidate(out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static void UnbindPlayerSceneServices(
            BattleSceneContext context,
            D0PlayerEntityView playerEntity)
        {
            PlayerWeaponPresentationController weaponController =
                context == null ? null : context.PlayerWeaponPresentationController;
            if (weaponController != null)
            {
                weaponController.ClearPlayerEntityBinding();
                weaponController.UnbindSceneServices();
            }

            playerEntity?.Barrier?.UnbindSceneServices();
            playerEntity?.Controller?.UnbindSceneServices();
        }

        private static bool TryCreateScenarioDefinitionForContext(
            BattleSceneContext context,
            BattleScenarioConfig config,
            out ScenarioDefinition definition,
            out string error)
        {
            if (config == null)
            {
                definition = null;
                error = "BattleScenarioConfig is required.";
                return false;
            }

            return context != null && context.RoomBinding != null
                ? config.TryCreateDefinitionForRoom(out definition, out error)
                : config.TryCreateDefinition(out definition, out error);
        }
        public DomainResult TryPause()
        {
            DomainResult result = ApplyControl(SessionControlCommandType.Pause);
            if (result.IsSuccess)
            {
                ClearGameplayInput();
                SynchronizePresentationPauseState();
            }

            return result;
        }

        public DomainResult TryResume()
        {
            DomainResult result = ApplyControl(SessionControlCommandType.Resume);
            if (result.IsSuccess)
            {
                ClearGameplayInput();
                SynchronizePresentationPauseState();
            }

            return result;
        }

        public DomainResult TryRestart()
        {
            if (!IsInitialized || factory == null || ScenarioConfig == null || HitboxRegistry == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!ScenarioConfig.TryValidateSpatialConfiguration(out string validationError)
                || !TryCreateScenarioDefinitionForContext(
                    Context,
                    ScenarioConfig,
                    out ScenarioDefinition restartedDefinition,
                    out validationError))
            {
                LastError = validationError;
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (Context == null
                || Context.HitboxRegistry != HitboxRegistry
                || Context.PlayerEntity == null
                || Context.PlayerAnchor == null
                || d0EnemyEntityWorld == null
                || d0EnemyEntityWorld.ActiveGameplayAnchor == null
                || d0EnemyEntityWorld.ActiveProjectileSpawnAnchor == null
                || Context.PlayerAnchor == d0EnemyEntityWorld.ActiveGameplayAnchor)
            {
                LastError = "BattleSceneContext spatial references are no longer valid for restart.";
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!Context.TryValidateD0RuntimeBindings(out string runtimeBindingError))
            {
                LastError = $"BattleSceneContext D0 bindings are no longer valid for restart: {runtimeBindingError}";
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!HitboxRegistry.IsReadyForQueries || HitboxRegistry.Count <= 0)
            {
                LastError = "HitboxRegistry is not ready to recompose the spatial query session.";
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!HitboxRegistry.TryValidateStaticBindings(
                    ScenarioConfig.AttackQuerySettings,
                    out string bindingValidationError))
            {
                LastError = bindingValidationError;
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            DomainResult restartCommand = ApplyControl(SessionControlCommandType.Restart);
            if (!restartCommand.IsSuccess)
            {
                LastError = $"Unable to commit BattleSession restart: {restartCommand.RejectReason}.";
                return restartCommand;
            }
            ClearGameplayInput();

            if (ScenarioConfig.UsesAuthoredScenario
                && !TryPlaceAuthoredPlayer(
                    Context,
                    ScenarioConfig.AuthoredScenario,
                    out string playerSpawnError))
            {
                LastError = $"Unable to reset authored player spawn: {playerSpawnError}";
                FailClosedAfterRestart();
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (ScenarioConfig.UsesAuthoredScenario
                && !D0ThreeCRuntimeProfileApplier.TryApplyAuthoredPresentation(
                    Context,
                    out string presentationApplyError))
            {
                LastError = $"D0 3C runtime presentation is invalid: {presentationApplyError}";
                FailClosedAfterRestart();
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            playerShotPresentationBridge?.ClearPending();

            Context.PresentationCoordinator.UnbindAndClear();

            if (projectileCollisionProxyPool == null)
            {
                LastError = "ProjectileCollisionProxyPool is missing for the active spatial session.";
                FailClosedAfterRestart();
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            projectileCollisionProxyPool.ForceReleaseAll();
            if (d0EnemyEntityWorld != null
                && !d0EnemyEntityWorld.TryResetForSession(
                    out string entityResetError))
            {
                LastError = $"Unable to reset enemy entity world: {entityResetError}";
                FailClosedAfterRestart();
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!DoesCollisionProxyPoolMatch(
                    projectileCollisionProxyPool,
                    restartedDefinition,
                    ScenarioConfig.AttackQuerySettings))
            {
                projectileCollisionProxyPool.Dispose();
                projectileCollisionProxyPool = null;
                if (!TryCreateCollisionProxyPool(
                        restartedDefinition,
                        ScenarioConfig.AttackQuerySettings,
                        HitboxRegistry,
                        Context.ProjectilesRoot,
                        out projectileCollisionProxyPool,
                        out string poolError))
                {
                    LastError = poolError;
                    FailClosedAfterRestart();
                    return DomainResult.Rejected(RejectReason.InvalidDefinition);
                }
            }

            if (!TryComposeRestartedSession(
                    factory,
                    restartedDefinition,
                    HitboxRegistry,
                    ScenarioConfig,
                    Context.PlayerAnchor,
                    Context.PlayerEntity,
                    d0EnemyEntityWorld.ActiveGameplayAnchor,
                    d0EnemyEntityWorld.ActiveProjectileSpawnAnchor,
                    projectileCollisionProxyPool,
                    out SessionComposition composition,
                    out RejectReason rejectReason,
                    out string error))
            {
                LastError = error;
                FailClosedAfterRestart();

                return DomainResult.Rejected(rejectReason);
            }

            DomainResult started = StartFreshSession(composition.Session);
            if (!started.IsSuccess)
            {
                composition.Session.Dispose();
                projectileCollisionProxyPool.ForceReleaseAll();
                HitboxRegistry.ClearDynamicAndStaticBindings();
                LastError = $"Unable to start restarted BattleSession: {started.RejectReason}.";
                FailClosedAfterRestart();
                return started;
            }

            if (d0EnemyEntityWorld != null)
            {
                if (!d0EnemyEntityWorld.TryBindInitialRuntime(
                        composition.Session.PlayerRuntimeId,
                        composition.Session.EnemyRuntimeId,
                        out string entityError)
                    || !composition.UnityProjectileWorldPort.TryRebindEnemyAnchors(
                        d0EnemyEntityWorld.ActiveGameplayAnchor,
                        d0EnemyEntityWorld.ActiveProjectileSpawnAnchor,
                        out entityError))
                {
                    composition.Session.Dispose();
                    projectileCollisionProxyPool.ForceReleaseAll();
                    HitboxRegistry.ClearDynamicAndStaticBindings();
                    LastError = $"Unable to bind restarted enemy entity: {entityError}";
                    FailClosedAfterRestart();
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                d0EnemyEntityWorld.RefreshRuntimeBindings();
            }

            if (!TryPreparePresentation(
                    Context,
                    restartedDefinition,
                    out string presentationPrepareError))
            {
                composition.Session.Dispose();
                projectileCollisionProxyPool.ForceReleaseAll();
                HitboxRegistry.ClearDynamicAndStaticBindings();
                LastError = $"Unable to prepare battle presentation after restart: {presentationPrepareError}";
                FailClosedAfterRestart();
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!Context.PresentationCoordinator.TryBind(
                    composition.Session,
                    composition.ObservingProjectileWorldPort.Feed,
                    out string presentationBindError))
            {
                composition.Session.Dispose();
                projectileCollisionProxyPool.ForceReleaseAll();
                HitboxRegistry.ClearDynamicAndStaticBindings();
                LastError = $"Unable to bind projectile presentation after restart: {presentationBindError}";
                FailClosedAfterRestart();
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            CommitComposition(composition);
            if (ScenarioConfig.UsesAuthoredScenario)
            {
                Context.BeginCombatVfx();
            }
            inputSource = new UnityBattleInputSource();
            ApplyD0InputBufferProfile(inputSource);
            nextControlSequence = 2L;
            earlyPauseControlFrame = -1;
            earlyPauseControlStateBefore = BattleSessionState.Disposed;
            shutdown = false;
            LastExecutedSteps = 0;
            LastError = string.Empty;
            Context.CombatAimReticle?.ResetToCenter();
            SessionRestarted?.Invoke(this);
            return DomainResult.Success;
        }

        private static bool TryCreateCollisionProxyPool(
            ScenarioDefinition definition,
            UnityAttackQuerySettings attackQuerySettings,
            HitboxRegistry registry,
            Transform proxyRoot,
            out ProjectileCollisionProxyPool pool,
            out string error)
        {
            pool = null;
            try
            {
                pool = new ProjectileCollisionProxyPool(
                    definition.ProjectileCapacity,
                    attackQuerySettings.HitboxLayerMask,
                    proxyRoot == null
                        ? registry.transform
                        : proxyRoot);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Unable to create ProjectileCollisionProxyPool: {exception.Message}";
                return false;
            }
        }

        private static bool DoesCollisionProxyPoolMatch(
            ProjectileCollisionProxyPool pool,
            ScenarioDefinition definition,
            UnityAttackQuerySettings attackQuerySettings)
        {
            return pool != null
                && pool.Capacity == definition.ProjectileCapacity
                && pool.HitboxLayerMask == attackQuerySettings.HitboxLayerMask;
        }

        public void Shutdown()
        {
            Context?.EndCombatVfx();
            projectWideBattleInputAdapter.ClearEarlyPausePressedHandler();
            earlyPauseControlFrame = -1;
            earlyPauseControlStateBefore = BattleSessionState.Disposed;
            if (shutdown)
            {
                return;
            }

            ClearGameplayInput();
            shutdown = true;
            if (Session != null)
            {
                Session.EnemyRuntimeChanged -= OnEnemyRuntimeChanged;
            }

            if (Session != null && Session.State != BattleSessionState.Disposed)
            {
                DomainResult disposed = ApplyControl(SessionControlCommandType.Dispose);
                if (!disposed.IsSuccess)
                {
                    Session.Dispose();
                }
            }

            d0EnemyEntityWorld?.ResetForSession();
            if (HitboxRegistry != null)
            {
                HitboxRegistry.TryUnbindPlayerEntity(d0PlayerEntity);
                projectileCollisionProxyPool?.ForceReleaseAll();
                projectileCollisionProxyPool?.Dispose();
                projectileCollisionProxyPool = null;
                HitboxRegistry.ClearDynamicAndStaticBindings();
            }

            Context?.PresentationCoordinator?.UnbindAndClear();
            UnbindPlayerSceneServices(Context, d0PlayerEntity);
            if (Context != null && Context.RoomBinding != null)
            {
                Context.RoomBinding.ClearRuntimeRoom();
                if (FpgRoomPlaytestOverrides.IsActive)
                {
                    FpgRoomPlaytestOverrides.Clear();
                }
            }
            d0EnemyBehaviorController = null;
            d0ShotCameraFeedbackController = null;
            d0PlayerEntity = null;
            d0EnemyEntityWorld = null;
            observingProjectileWorldPort = null;
            playerShotPresentationBridge?.ClearPending();
            playerShotPresentationBridge = null;
            playerShotPresentationFeed = null;
        }

        private void Update()
        {
            UnityBattleInputSource activeInputSource = inputOverride ?? inputSource;
            if (!IsInitialized || activeInputSource == null)
            {
                return;
            }

            if (TryConsumeEarlyPauseControlFrame(out BattleSessionState stateBeforeEarlyPause))
            {
                if (inputOverride == null
                    && projectWideBattleInputAdapter.IsRestartPressedThisFrame())
                {
                    DomainResult restart = TryRestart();
                    if (!restart.IsSuccess)
                    {
                        // The normal Update path evaluates F5 before Pause.
                        // If the early toggle exposed a pre-validation restart
                        // rejection, restore the state F5 would have left.
                        RestoreStateAfterEarlyPauseControl(stateBeforeEarlyPause);
                    }
                }

                // The Input System callback has already committed this frame's
                // Pause or Resume and cleared the source. Preserve the legacy
                // control-frame behavior: no input recapture or simulation pump
                // runs until the next rendered frame.
                LastExecutedSteps = 0;
                return;
            }

            if (inputOverride == null)
            {
                if (!projectWideBattleInputAdapter.TryCapture(activeInputSource))
                {
                    activeInputSource.CaptureFromDevices();
                }
            }

            Physics.SyncTransforms();
            CaptureCombatAimPose(activeInputSource);
            if (activeInputSource.ConsumeRestartPressed())
            {
                TryRestart();
                return;
            }

            if (activeInputSource.ConsumePausePressed())
            {
                if (Session.State == BattleSessionState.Running)
                {
                    TryPause();
                }
                else if (Session.State == BattleSessionState.Paused)
                {
                    TryResume();
                }
            }

            if (Session.State != BattleSessionState.Running)
            {
                LastExecutedSteps = 0;
                return;
            }

            long elapsedTimeSpanTicks = (long)Math.Round(
                Time.unscaledDeltaTime * TimeSpan.TicksPerSecond,
                MidpointRounding.AwayFromZero);
            if (!TryResolveD0EnemyTickObserver(out IBattleTickObserver d0EnemyTickObserver))
            {
                LastExecutedSteps = 0;
                return;
            }

            DomainResult pumped = Session.PumpWithBattleInput(
                Math.Max(elapsedTimeSpanTicks, 0L),
                activeInputSource,
                d0EnemyTickObserver,
                out int executedSteps);
            LastExecutedSteps = executedSteps;
            if (!pumped.IsSuccess)
            {
                LastError = $"BattleSession Pump rejected: {pumped.RejectReason}.";
                enabled = false;
                Debug.LogError($"[{nameof(BattleSessionHost)}] {LastError}", this);
            }
        }

        private static bool TryComposeInitialSession(
            BattleSessionFactory sessionFactory,
            ScenarioDefinition definition,
            HitboxRegistry registry,
            BattleScenarioConfig scenarioConfig,
            Transform playerAnchor,
            D0PlayerEntityView playerEntity,
            Transform enemyAnchor,
            Transform enemyProjectileSpawnAnchor,
            ProjectileCollisionProxyPool collisionProxyPool,
            out SessionComposition composition,
            out RejectReason rejectReason,
            out string error)
        {
            return TryComposeSession(
                sessionFactory,
                definition,
                registry,
                scenarioConfig,
                playerAnchor,
                playerEntity,
                enemyAnchor,
                enemyProjectileSpawnAnchor,
                collisionProxyPool,
                out composition,
                out rejectReason,
                out error);
        }

        private static bool TryPlaceAuthoredPlayer(
            BattleSceneContext context,
            D0CombatScenarioDefinition scenario,
            out string error)
        {
            error = string.Empty;
            D0PlayerEntityView playerEntity = context == null
                ? null
                : context.PlayerEntity;
            if (playerEntity == null || scenario == null || scenario.Player == null)
            {
                error = "Authored player spawn requires context, scenario and a complete player Entity.";
                return false;
            }

            if (!playerEntity.TryValidate(out error))
            {
                error = "Authored player Entity is invalid: " + error;
                return false;
            }

            if (!context.TryGetEncounterSpawnPoint(
                    scenario.PlayerSpawnPointId,
                    out D0SpawnPoint spawnPoint))
            {
                error = $"Player spawn point '{scenario.PlayerSpawnPointId}' is not bound.";
                return false;
            }

            CharacterController characterController = playerEntity.CharacterController;
            bool controllerWasEnabled = characterController != null
                && characterController.enabled;
            if (controllerWasEnabled)
            {
                characterController.enabled = false;
            }

            if (!playerEntity.HasCapturedAuthoredLocalPose)
            {
                playerEntity.CaptureAuthoredLocalPose();
            }
            else if (!playerEntity.RestoreAuthoredLocalPose())
            {
                if (controllerWasEnabled)
                {
                    characterController.enabled = true;
                }

                error = "Player Entity authored local pose could not be restored.";
                return false;
            }

            playerEntity.transform.SetPositionAndRotation(
                spawnPoint.transform.position,
                spawnPoint.transform.rotation);
            if (controllerWasEnabled)
            {
                characterController.enabled = true;
            }

            Actor2DPresenter presenter = playerEntity.ActorPresenter;
            D0ActorPresentationDefinition presentationDefinition =
                scenario.Player.ActorPresentation;
            if (presenter == null || presentationDefinition == null)
            {
                error = "Authored player spawn requires the Entity presenter and state presentation.";
                return false;
            }

            if (!presenter.TrySetRuntimePresentationOverride(
                    presentationDefinition,
                    out error))
            {
                return false;
            }

            playerEntity.SetGameplayCollidersEnabled(true);
            CombatLabPlayerController controller = playerEntity.Controller;
            controller?.CaptureInitialSpawn();
            CombatLabPlayerBounds bounds = playerEntity.Bounds;
            if (bounds != null
                && !bounds.CaptureInitialSafePosition(out error))
            {
                return false;
            }

            Physics.SyncTransforms();
            error = string.Empty;
            return true;
        }

        private static bool TryPreparePresentation(
            BattleSceneContext context,
            ScenarioDefinition definition,
            out string error)
        {
            if (context == null
                || context.PresentationCoordinator == null
                || context.PresentationCatalog == null
                || context.ProjectileViewRoot == null)
            {
                error = "BattleSceneContext must reference a presentation coordinator, catalog and projectile view root.";
                return false;
            }

            bool requiresFeedback = context.PresentationCatalog.WarningEntryCount > 0
                || context.WarningViewRoot != null
                || context.ImpactViewRoot != null
                || context.PresentationCanvas != null
                || context.BattleHudPresenter != null;
            if (requiresFeedback)
            {
                if (context.WarningViewRoot == null
                    || context.ImpactViewRoot == null
                    || context.PresentationCanvas == null
                    || context.BattleHudPresenter == null)
                {
                    error = "C feedback presentation requires warning root, impact root, presentation canvas and BattleHudPresenter references.";
                    return false;
                }

                return context.PresentationCoordinator.TryPrepare(
                    definition,
                    context.PresentationCatalog,
                    context.ProjectileViewRoot,
                    context.WarningViewRoot,
                    context.ImpactViewRoot,
                    context.BattleHudPresenter,
                    out error);
            }

            return context.PresentationCoordinator.TryPrepare(
                definition,
                context.PresentationCatalog,
                context.ProjectileViewRoot,
                out error);
        }

        private static bool TryComposeRestartedSession(
            BattleSessionFactory sessionFactory,
            ScenarioDefinition definition,
            HitboxRegistry registry,
            BattleScenarioConfig scenarioConfig,
            Transform playerAnchor,
            D0PlayerEntityView playerEntity,
            Transform enemyAnchor,
            Transform enemyProjectileSpawnAnchor,
            ProjectileCollisionProxyPool collisionProxyPool,
            out SessionComposition composition,
            out RejectReason rejectReason,
            out string error)
        {
            return TryComposeSession(
                sessionFactory,
                definition,
                registry,
                scenarioConfig,
                playerAnchor,
                playerEntity,
                enemyAnchor,
                enemyProjectileSpawnAnchor,
                collisionProxyPool,
                out composition,
                out rejectReason,
                out error);
        }

        private static bool TryComposeSession(
            BattleSessionFactory sessionFactory,
            ScenarioDefinition definition,
            HitboxRegistry registry,
            BattleScenarioConfig scenarioConfig,
            Transform playerAnchor,
            D0PlayerEntityView playerEntity,
            Transform enemyAnchor,
            Transform enemyProjectileSpawnAnchor,
            ProjectileCollisionProxyPool collisionProxyPool,
            out SessionComposition composition,
            out RejectReason rejectReason,
            out string error)
        {
            composition = null;
            rejectReason = RejectReason.InvalidDefinition;
            BattleSession nextSession = null;
            try
            {
                if (collisionProxyPool == null)
                {
                    error = "ProjectileCollisionProxyPool is required for spatial session composition.";
                    return false;
                }

                int transcriptOperationCapacity = scenarioConfig.SpatialTranscriptOperationCapacity;
                if (D0RuntimePerformanceStressDriver.IsRequested())
                {
                    // The opt-in G5 pressure executable records 32 real
                    // projectile sweeps every simulation tick for one
                    // continuous capture. Keep that evidence run deterministic
                    // and fixed-capacity without changing the authored replay
                    // budget used by ordinary play.
                    transcriptOperationCapacity = Math.Max(
                        transcriptOperationCapacity,
                        D0RuntimePerformanceStressDriver.StressTranscriptOperationCapacity);
                }

                SpatialPortTranscript transcript = new SpatialPortTranscript(
                    transcriptOperationCapacity,
                    scenarioConfig.SpatialTranscriptQueryCandidateCapacity);
                FixedPlayerShotPresentationFeed playerShotFeed =
                    new FixedPlayerShotPresentationFeed();
                PlayerShotPresentationBridge playerShotBridge =
                    new PlayerShotPresentationBridge(playerShotFeed);
                UnityAttackQueryPort unityPort = new UnityAttackQueryPort(
                    registry,
                    scenarioConfig.AttackQuerySettings,
                    null,
                    playerShotBridge);
                RecordingAttackQueryPort recordingPort = new RecordingAttackQueryPort(
                    unityPort,
                    transcript);
                UnityAttackQuerySettings attackSettings = scenarioConfig.AttackQuerySettings;
                UnityProjectileWorldPort projectileWorldPort = new UnityProjectileWorldPort(
                    registry,
                    playerAnchor,
                    enemyAnchor,
                    enemyProjectileSpawnAnchor,
                    new UnityProjectileWorldSettings(
                        attackSettings.HitboxLayerMask,
                        attackSettings.BlockerLayerMask),
                    definition.ProjectileCapacity,
                    null,
                    collisionProxyPool);
                RecordingProjectileWorldPort recordingProjectilePort =
                    new RecordingProjectileWorldPort(projectileWorldPort, transcript);
                FixedProjectilePresentationFeed projectilePresentationFeed =
                    new FixedProjectilePresentationFeed(definition.ProjectileCapacity);
                ObservingProjectileWorldPort observingProjectilePort =
                    new ObservingProjectileWorldPort(recordingProjectilePort, projectilePresentationFeed);

                nextSession = sessionFactory.Create(
                    definition,
                    null,
                    recordingPort,
                    observingProjectilePort,
                    transcript,
                    playerShotBridge);

                if (!registry.ResetForSession(
                        nextSession.PlayerRuntimeId,
                        nextSession.EnemyRuntimeId,
                        out string registryError))
                {
                    error = $"Unable to bind HitboxRegistry to BattleSession: {registryError}";
                    nextSession.Dispose();
                    collisionProxyPool.ForceReleaseAll();
                    registry.ClearDynamicAndStaticBindings();
                    return false;
                }

                if (playerEntity != null
                    && !registry.TryBindPlayerEntity(
                        nextSession.PlayerRuntimeId,
                        playerEntity,
                        new GeometryId(PlayerBodyGeometryId),
                        out registryError))
                {
                    error = $"Unable to bind the player Entity hitbox: {registryError}";
                    nextSession.Dispose();
                    collisionProxyPool.ForceReleaseAll();
                    registry.ClearDynamicAndStaticBindings();
                    return false;
                }

                if (!registry.IsReadyForQueries || registry.Count <= 0)
                {
                    error = "HitboxRegistry must contain an environment or combatant binding and be ready for queries.";
                    nextSession.Dispose();
                    collisionProxyPool.ForceReleaseAll();
                    registry.ClearDynamicAndStaticBindings();
                    rejectReason = RejectReason.InvalidState;
                    return false;
                }

                if (!projectileWorldPort.ResetForSession(
                        nextSession.PlayerRuntimeId,
                        nextSession.EnemyRuntimeId,
                        out string projectileWorldError))
                {
                    error = $"Unable to bind ProjectileWorld to BattleSession: {projectileWorldError}";
                    nextSession.Dispose();
                    collisionProxyPool.ForceReleaseAll();
                    registry.ClearDynamicAndStaticBindings();
                    rejectReason = RejectReason.InvalidState;
                    return false;
                }

                Physics.SyncTransforms();
                composition = new SessionComposition(
                    nextSession,
                    unityPort,
                    recordingPort,
                    projectileWorldPort,
                    recordingProjectilePort,
                    observingProjectilePort,
                    transcript,
                    playerShotFeed,
                    playerShotBridge);
                rejectReason = RejectReason.None;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                if (nextSession != null && nextSession.State != BattleSessionState.Disposed)
                {
                    nextSession.Dispose();
                }

                collisionProxyPool?.ForceReleaseAll();
                registry.ClearDynamicAndStaticBindings();

                error = $"Unable to compose spatial BattleSession: {exception.Message}";
                return false;
            }
        }

        private static DomainResult StartFreshSession(BattleSession session)
        {
            return session.ApplyControl(new SessionControlCommand(
                new ControlSequence(1L),
                SessionControlCommandType.Start));
        }

        private void CommitComposition(SessionComposition composition)
        {
            if (Session != null)
            {
                Session.EnemyRuntimeChanged -= OnEnemyRuntimeChanged;
            }

            Session = composition.Session;
            Session.EnemyRuntimeChanged += OnEnemyRuntimeChanged;
            unityAttackQueryPort = composition.UnityAttackQueryPort;
            recordingAttackQueryPort = composition.RecordingAttackQueryPort;
            unityProjectileWorldPort = composition.UnityProjectileWorldPort;
            recordingProjectileWorldPort = composition.RecordingProjectileWorldPort;
            observingProjectileWorldPort = composition.ObservingProjectileWorldPort;
            playerShotPresentationFeed = composition.PlayerShotPresentationFeed;
            playerShotPresentationBridge = composition.PlayerShotPresentationBridge;
            SpatialTranscript = composition.SpatialTranscript;
            AttackQuerySettings = ScenarioConfig.AttackQuerySettings;
        }

        private void OnEnemyRuntimeChanged(EnemyLifecycleChange change)
        {
            if (Session == null || Session.State == BattleSessionState.Disposed)
            {
                return;
            }

            if (change.CurrentRuntimeId != Session.EnemyRuntimeId)
            {
                LastError = "Enemy lifecycle event does not match the active BattleSession enemy.";
                throw new InvalidOperationException(LastError);
            }

            if (d0EnemyEntityWorld != null)
            {
                string entityError;
                if (!d0EnemyEntityWorld.TryApplyLifecycleChange(
                        change,
                        Session.PlayerRuntimeId,
                        out entityError))
                {
                    LastError = "Unable to replace the active enemy entity: " + entityError;
                    throw new InvalidOperationException(LastError);
                }

                if (unityProjectileWorldPort == null
                    || !unityProjectileWorldPort.TryRebindEnemyAnchors(
                        d0EnemyEntityWorld.ActiveGameplayAnchor,
                        d0EnemyEntityWorld.ActiveProjectileSpawnAnchor,
                        out entityError))
                {
                    LastError = "Unable to rebind projectile anchors after enemy replacement: "
                        + entityError;
                    throw new InvalidOperationException(LastError);
                }
            }

            if (d0EnemyBehaviorController != null)
            {
                d0EnemyBehaviorController.NotifyEnemyRuntimeChanged(change);
            }

            string hitboxError = string.Empty;
            if (HitboxRegistry == null
                || !HitboxRegistry.TryRebindEnemyRuntimeId(
                    change.CurrentRuntimeId,
                    out hitboxError))
            {
                LastError = "Unable to rebind enemy hitboxes after lifecycle transition: "
                    + hitboxError;
                throw new InvalidOperationException(LastError);
            }

            string projectileError = string.Empty;
            if (unityProjectileWorldPort == null
                || !unityProjectileWorldPort.TryRebindEnemyRuntimeId(
                    change.CurrentRuntimeId,
                    out projectileError))
            {
                LastError = "Unable to rebind projectile world after lifecycle transition: "
                    + projectileError;
                throw new InvalidOperationException(LastError);
            }

            string presentationError = string.Empty;
            if (Context != null
                && Context.PresentationCoordinator != null
                && !Context.PresentationCoordinator.TryRebindEnemyRuntimeId(
                    change.CurrentRuntimeId,
                    out presentationError))
            {
                LastError = "Unable to rebind battle presentation after lifecycle transition: "
                    + presentationError;
                throw new InvalidOperationException(LastError);
            }
        }

        private void FailClosedAfterRestart()
        {
            Context?.EndCombatVfx();
            projectWideBattleInputAdapter.ClearEarlyPausePressedHandler();
            earlyPauseControlFrame = -1;
            earlyPauseControlStateBefore = BattleSessionState.Disposed;
            Context?.PresentationCoordinator?.DisposePresentation();
            d0EnemyEntityWorld?.ResetForSession();
            HitboxRegistry?.TryUnbindPlayerEntity(d0PlayerEntity);
            UnbindPlayerSceneServices(Context, d0PlayerEntity);
            projectileCollisionProxyPool?.ForceReleaseAll();
            projectileCollisionProxyPool?.Dispose();
            projectileCollisionProxyPool = null;
            if (HitboxRegistry != null)
            {
                HitboxRegistry.ClearDynamicAndStaticBindings();
            }

            if (Session != null)
            {
                Session.EnemyRuntimeChanged -= OnEnemyRuntimeChanged;
            }

            Session = null;
            ClearGameplayInput();
            inputSource = null;
            inputOverride = null;
            unityAttackQueryPort = null;
            recordingAttackQueryPort = null;
            unityProjectileWorldPort = null;
            recordingProjectileWorldPort = null;
            observingProjectileWorldPort = null;
            playerShotPresentationBridge?.ClearPending();
            playerShotPresentationBridge = null;
            playerShotPresentationFeed = null;
            SpatialTranscript = null;
            d0EnemyBehaviorController = null;
            d0ShotCameraFeedbackController = null;
            d0EnemyEntityWorld = null;
            LastExecutedSteps = 0;
            shutdown = true;
        }

        private DomainResult ApplyControl(SessionControlCommandType type)
        {
            if (Session == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            SessionControlCommand command = new SessionControlCommand(
                new ControlSequence(nextControlSequence++),
                type);
            return Session.ApplyControl(command);
        }

        /// <summary>
        /// Input System actions perform during dynamic input processing, before
        /// normal MonoBehaviour Update. When the project-wide Battle map owns
        /// Pause, commit and present the transition here so Spine sees the new
        /// time scale before its regular Update. Legacy raw-device fallback
        /// remains in Update and preserves its existing behavior.
        /// </summary>
        private bool TryHandleProjectWidePausePerformed()
        {
            if (!IsInitialized || inputOverride != null)
            {
                return false;
            }

            if (Session.State == BattleSessionState.Running)
            {
                DomainResult result = TryPause();
                if (result.IsSuccess)
                {
                    earlyPauseControlFrame = Time.frameCount;
                    earlyPauseControlStateBefore = BattleSessionState.Running;
                    return true;
                }

                return false;
            }

            if (Session.State == BattleSessionState.Paused)
            {
                DomainResult result = TryResume();
                if (result.IsSuccess)
                {
                    earlyPauseControlFrame = Time.frameCount;
                    earlyPauseControlStateBefore = BattleSessionState.Paused;
                    return true;
                }

                return false;
            }

            return false;
        }

        private void SynchronizePresentationPauseState()
        {
            Context?.PresentationCoordinator?.SynchronizePauseState();
        }

        private bool TryConsumeEarlyPauseControlFrame(out BattleSessionState stateBefore)
        {
            stateBefore = earlyPauseControlStateBefore;
            if (earlyPauseControlFrame != Time.frameCount)
            {
                return false;
            }

            earlyPauseControlFrame = -1;
            earlyPauseControlStateBefore = BattleSessionState.Disposed;
            return true;
        }

        private void RestoreStateAfterEarlyPauseControl(BattleSessionState stateBefore)
        {
            if (!IsInitialized || Session.State == stateBefore)
            {
                return;
            }

            if (stateBefore == BattleSessionState.Running
                && Session.State == BattleSessionState.Paused)
            {
                TryResume();
            }
            else if (stateBefore == BattleSessionState.Paused
                     && Session.State == BattleSessionState.Running)
            {
                TryPause();
            }
        }

        private void ClearGameplayInput()
        {
            inputSource?.ClearGameplayInput();
            if (inputOverride != null && inputOverride != inputSource)
            {
                inputOverride.ClearGameplayInput();
            }
        }

        private void ApplyD0InputBufferProfile(UnityBattleInputSource target)
        {
            D0CombatScenarioDefinition scenario = ScenarioConfig == null
                ? null
                : ScenarioConfig.AuthoredScenario;
            D0ThreeCProfile profile = scenario == null ? null : scenario.ThreeCProfile;
            if (target == null || profile == null || !profile.TryValidate(out _))
            {
                return;
            }

            target.ConfigureInputBufferTicks(profile.InputBufferTicks);
        }

        private void CaptureCombatAimPose(UnityBattleInputSource activeInputSource)
        {
            Transform aimAnchor = Context == null ? null : Context.AimAnchor;
            Camera mainCamera = Context == null ? null : Context.MainCamera;
            if (aimAnchor == null || mainCamera == null)
            {
                return;
            }

            UnityAttackQuerySettings settings = AttackQuerySettings;
            if (!settings.IsValid)
            {
                activeInputSource.CaptureAimPose(aimAnchor);
                return;
            }

            Vector2 viewport = CombatAimViewportMath.Center;
            ICombatAimViewportSource viewportSource = Context.AimViewportSource;
            if (viewportSource != null
                && viewportSource.TryGetViewport(out Vector2 suppliedViewport))
            {
                viewport = CombatAimViewportMath.ClampToSafeArea(
                    suppliedViewport,
                    ResolveAimSafeViewport(viewportSource));
            }

            Ray cameraRay = mainCamera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            if (d0ShotCameraFeedbackController != null)
            {
                // Camera recoil is deliberately visual-only. Its camera-space
                // translation must not alter the stable origin consumed by the
                // deterministic aim query on the following frame.
                cameraRay = new Ray(
                    cameraRay.origin - d0ShotCameraFeedbackController.CurrentWorldPresentationOffset,
                    cameraRay.direction);
            }

            Vector3 targetPoint = cameraRay.GetPoint(settings.MaxDistance);
            int hitCount = Physics.RaycastNonAlloc(
                cameraRay,
                combatAimRaycastBuffer,
                settings.MaxDistance,
                settings.PhysicsLayerMask,
                QueryTriggerInteraction.Collide);
            float nearestDistance = float.PositiveInfinity;
            Transform playerRoot = Context.PlayerAnchor;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = combatAimRaycastBuffer[index];
                Collider collider = hit.collider;
                if (collider == null
                    || (playerRoot != null
                        && (collider.transform == playerRoot
                            || collider.transform.IsChildOf(playerRoot)))
                    || hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                targetPoint = hit.point;
            }

            Vector3 direction = targetPoint - aimAnchor.position;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = aimAnchor.forward;
            }

            activeInputSource.CaptureAimPose(aimAnchor.position, direction, mainCamera.transform.up);
        }

        private static Rect ResolveAimSafeViewport(ICombatAimViewportSource viewportSource)
        {
            CombatAimReticle reticle = viewportSource as CombatAimReticle;
            if (reticle != null && CombatAimViewportMath.IsValidSafeArea(reticle.SafeViewport))
            {
                return reticle.SafeViewport;
            }

            return CombatAimViewportMath.DefaultSafeArea;
        }

        private bool TryResolveD0EnemyTickObserver(out IBattleTickObserver observer)
        {
            observer = null;
            if (ScenarioConfig == null || !ScenarioConfig.UsesAuthoredScenario)
            {
                return true;
            }

            if (d0EnemyBehaviorController != null
                && d0EnemyBehaviorController.isActiveAndEnabled)
            {
                observer = d0EnemyBehaviorController;
                return true;
            }

            LastError = "Authored D0 session lost its required active D0EnemyBehaviorController.";
            Debug.LogError($"[{nameof(BattleSessionHost)}] {LastError}", this);
            enabled = false;
            return false;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ClearGameplayInput();
                return;
            }

            Context?.CombatAimReticle?.ResetToCenter();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                ClearGameplayInput();
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                Shutdown();
            }
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private sealed class SessionComposition
        {
            public SessionComposition(
                BattleSession session,
                UnityAttackQueryPort unityAttackQueryPort,
                RecordingAttackQueryPort recordingAttackQueryPort,
                UnityProjectileWorldPort unityProjectileWorldPort,
                RecordingProjectileWorldPort recordingProjectileWorldPort,
                ObservingProjectileWorldPort observingProjectileWorldPort,
                SpatialPortTranscript spatialTranscript,
                FixedPlayerShotPresentationFeed playerShotPresentationFeed,
                PlayerShotPresentationBridge playerShotPresentationBridge)
            {
                Session = session;
                UnityAttackQueryPort = unityAttackQueryPort;
                RecordingAttackQueryPort = recordingAttackQueryPort;
                UnityProjectileWorldPort = unityProjectileWorldPort;
                RecordingProjectileWorldPort = recordingProjectileWorldPort;
                ObservingProjectileWorldPort = observingProjectileWorldPort;
                SpatialTranscript = spatialTranscript;
                PlayerShotPresentationFeed = playerShotPresentationFeed;
                PlayerShotPresentationBridge = playerShotPresentationBridge;
            }

            public BattleSession Session { get; }
            public UnityAttackQueryPort UnityAttackQueryPort { get; }
            public RecordingAttackQueryPort RecordingAttackQueryPort { get; }
            public UnityProjectileWorldPort UnityProjectileWorldPort { get; }
            public RecordingProjectileWorldPort RecordingProjectileWorldPort { get; }
            public ObservingProjectileWorldPort ObservingProjectileWorldPort { get; }
            public SpatialPortTranscript SpatialTranscript { get; }
            public FixedPlayerShotPresentationFeed PlayerShotPresentationFeed { get; }
            public PlayerShotPresentationBridge PlayerShotPresentationBridge { get; }
        }
    }
}
