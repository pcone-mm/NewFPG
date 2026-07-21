using System.Collections.Generic;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class BattleSceneContext : MonoBehaviour
    {
        [SerializeField]
        private Transform worldRoot;

        [SerializeField]
        private Transform actorsRoot;

        [SerializeField]
        private Transform projectilesRoot;

        [SerializeField]
        private Transform presentationRoot;

        [SerializeField]
        private Transform debugRoot;

        [SerializeField]
        private D0PlayerEntityView playerEntity;

        [SerializeField]
        private Camera mainCamera;

        [SerializeField]
        private Light directionalLight;

        [SerializeField]
        private BattleScenarioConfig scenarioConfig;

        [SerializeField]
        private HitboxRegistry hitboxRegistry;

        [SerializeField]
        private BattleSessionHost sessionHost;

        [SerializeField]
        private BattleSessionDiagnosticsPresenter diagnosticsPresenter;

        [SerializeField]
        private BattlePresentationCatalog presentationCatalog;

        [SerializeField]
        private BattlePresentationCoordinator presentationCoordinator;

        [SerializeField]
        private Transform projectileViewRoot;

        [SerializeField]
        private Transform warningViewRoot;

        [SerializeField]
        private Transform impactViewRoot;

        [SerializeField]
        private Transform playerShotViewRoot;

        [SerializeField]
        private PlayerWeaponPresentationController playerWeaponPresentationController;

        [SerializeField]
        private Canvas presentationCanvas;

        [SerializeField]
        private BattleHudPresenter battleHudPresenter;

        [SerializeField]
        private CombatAimReticle combatAimReticle;

        [Header("D0 slice runtime bindings")]
        [SerializeField]
        private D0EnemyBehaviorController d0EnemyBehaviorController;

        [SerializeField]
        private D0EnemyEntityWorld enemyEntityWorld;

        [SerializeField]
        private D0CombatVfxWorld combatVfxWorld;

        [SerializeField, Tooltip("房间与遭遇的显式 CombatLab 组合桥；为空时继续使用旧 Stage 出生点。")]
        private FpgRoomCombatLabBinding roomBinding;
        [SerializeField]
        private D0SpawnPoint[] encounterSpawnPoints =
            System.Array.Empty<D0SpawnPoint>();

        [SerializeField]
        private D0ShotCameraFeedbackController d0ShotCameraFeedbackController;

        [Header("D0 slice presentation bindings")]
        [SerializeField]
        private D0HitTipPresenter d0HitTipPresenter;

        [SerializeField]
        private ThreatTelegraph2DPresenter d0ThreatTelegraphPresenter;

        [SerializeField]
        private D0WeakpointPresentationController d0WeakpointPresentationController;

        [SerializeField]
        private CombatAudioPresenter d0CombatAudioPresenter;

        [SerializeField]
        private CombatHud2DPresenter d0CombatHud2DPresenter;

        public Transform WorldRoot => worldRoot;

        public Transform ActorsRoot => actorsRoot;

        public Transform ProjectilesRoot => projectilesRoot;

        public Transform PresentationRoot => presentationRoot;

        public Transform DebugRoot => debugRoot;

        public D0PlayerEntityView PlayerEntity => playerEntity;

        public Transform PlayerAnchor => playerEntity == null ? null : playerEntity.transform;

        /// <summary>
        /// Optional visual-only anchor for effects that must remain at the
        /// player's feet, such as ground warning telegraphs. Combat queries
        /// and projectile origins continue to use <see cref="PlayerAnchor"/>.
        /// </summary>
        public Transform PlayerGroundAnchor => playerEntity == null
            ? null
            : playerEntity.GroundAnchor;

        public Transform EnemyAnchor => enemyEntityWorld == null
            ? null
            : enemyEntityWorld.ActiveGameplayAnchor;

        public Transform ActiveEnemyGameplayAnchor => EnemyAnchor;

        public Transform ActiveEnemyProjectileSpawnAnchor => enemyEntityWorld == null
            ? null
            : enemyEntityWorld.ActiveProjectileSpawnAnchor;

        public Transform ActiveEnemyWeakpointAnchor => enemyEntityWorld == null
            ? null
            : enemyEntityWorld.ActiveWeakpointAnchor;

        /// <summary>
        /// Gameplay-owned origin sampled only when an enemy projectile is
        /// registered. Active projectiles retain their frozen world paths.
        /// </summary>
        public Transform EnemyProjectileSpawnAnchor => ActiveEnemyProjectileSpawnAnchor;

        /// <summary>
        /// Scene-authored visual anchor for the D0 heavy warning. This is not
        /// a combat-query input; its sole owner is warning presentation.
        /// </summary>
        public Transform EnemyWeakpointAnchor => ActiveEnemyWeakpointAnchor;

        public Transform AimAnchor => playerEntity == null ? null : playerEntity.AimAnchor;

        public Camera MainCamera => mainCamera;

        public Light DirectionalLight => directionalLight;

        public BattleScenarioConfig ScenarioConfig => scenarioConfig;

        public HitboxRegistry HitboxRegistry => hitboxRegistry;

        public BattleSessionHost SessionHost => sessionHost;

        public BattleSessionDiagnosticsPresenter DiagnosticsPresenter => diagnosticsPresenter;

        public BattlePresentationCatalog PresentationCatalog => presentationCatalog;

        public BattlePresentationCoordinator PresentationCoordinator => presentationCoordinator;

        public Transform ProjectileViewRoot => projectileViewRoot;

        public Transform WarningViewRoot => warningViewRoot;

        public Transform ImpactViewRoot => impactViewRoot;

        public Transform PlayerShotViewRoot => playerShotViewRoot;

        public PlayerWeaponPresentationController PlayerWeaponPresentationController =>
            playerWeaponPresentationController;

        public Canvas PresentationCanvas => presentationCanvas;

        public BattleHudPresenter BattleHudPresenter => battleHudPresenter;

        /// <summary>
        /// Scene-owned free cursor used to source the camera viewport ray. It is
        /// presentation-only and deliberately does not enter BattleTickInput.
        /// </summary>
        public CombatAimReticle CombatAimReticle => combatAimReticle;

        public ICombatAimViewportSource AimViewportSource => combatAimReticle;

        public D0EnemyEntityWorld EnemyEntityWorld => enemyEntityWorld;
        public D0CombatVfxWorld CombatVfxWorld => combatVfxWorld;
        public CombatPresentationProfile D0PresentationProfile =>
            FindD0SliceInstallationMarker()?.PresentationProfile;
        public FpgRoomCombatLabBinding RoomBinding => roomBinding;
        public IReadOnlyList<D0SpawnPoint> EncounterSpawnPoints =>
            roomBinding != null && roomBinding.IsInitialized
                ? roomBinding.SpawnPoints
                : encounterSpawnPoints ?? System.Array.Empty<D0SpawnPoint>();
        public Actor2DPresenter ActiveD0EnemyActorPresenter =>
            enemyEntityWorld == null ? null : enemyEntityWorld.ActiveActorPresenter;
        public D0EnemyBehaviorController D0EnemyBehaviorController =>
            d0EnemyBehaviorController;

        public bool TryPrepareCombatVfx(
            D0CombatScenarioDefinition scenario,
            out string error)
        {
            if (combatVfxWorld == null)
            {
                error = string.Empty;
                return true;
            }

            if (scenario == null || scenario.Player == null || scenario.Encounter == null)
            {
                error = "Combat VFX World requires an authored scenario with player and encounter definitions.";
                return false;
            }

            List<D0WeaponDefinition> weapons = new List<D0WeaponDefinition>();
            if (scenario.Player.Weapon != null)
            {
                weapons.Add(scenario.Player.Weapon);
            }

            List<D0EnemyAttackDefinition> attacks =
                new List<D0EnemyAttackDefinition>();
            for (int index = 0; index < scenario.Encounter.AttackScheduleCount; index++)
            {
                D0EncounterAttackScheduleEntry entry =
                    scenario.Encounter.GetAttackScheduleEntry(index);
                if (entry.Attack != null)
                {
                    attacks.Add(entry.Attack);
                }
            }

            List<D0LuanSummonHudieDefinition> summons =
                new List<D0LuanSummonHudieDefinition>();
            if (scenario.LuanSummonHudie != null)
            {
                summons.Add(scenario.LuanSummonHudie);
            }

            List<D0ActorPresentationDefinition> actorStates =
                new List<D0ActorPresentationDefinition>();
            for (int index = 0; index < scenario.Encounter.SpawnSlotCount; index++)
            {
                D0EncounterSpawnSlot slot =
                    scenario.Encounter.GetSpawnSlot(index);
                if (slot != null
                    && slot.Enemy != null
                    && slot.Enemy.ActorPresentation != null)
                {
                    actorStates.Add(slot.Enemy.ActorPresentation);
                }
            }

            return combatVfxWorld.TryPrepareForScenario(
                weapons,
                attacks,
                summons,
                actorStates,
                out error);
        }

        public void BeginCombatVfx()
        {
            combatVfxWorld?.BeginCombat();
        }

        public void EndCombatVfx()
        {
            combatVfxWorld?.EndCombat();
        }

        public bool TryGetEncounterSpawnPoint(
            string spawnPointId,
            out D0SpawnPoint spawnPoint)
        {
            if (roomBinding != null
                && roomBinding.TryGetSpawnPoint(spawnPointId, out spawnPoint))
            {
                return true;
            }

            D0SpawnPoint[] points = encounterSpawnPoints
                ?? System.Array.Empty<D0SpawnPoint>();
            for (int index = 0; index < points.Length; index++)
            {
                D0SpawnPoint candidate = points[index];
                if (candidate != null
                    && string.Equals(
                        candidate.SpawnPointId,
                        spawnPointId,
                        System.StringComparison.Ordinal))
                {
                    spawnPoint = candidate;
                    return true;
                }
            }

            spawnPoint = null;
            return false;
        }

        public bool TryInitializeRoom(out string error)
        {
            if (roomBinding == null)
            {
                error = string.Empty;
                return true;
            }

            FpgRoomDefinition configuredRoom = roomBinding.ConfiguredRoomDefinition;
            D0CombatScenarioDefinition configuredScenario =
                roomBinding.ConfiguredScenarioDefinition;
            if (configuredRoom == null || configuredScenario == null)
            {
                error = "Room binding requires serialized room and scenario references.";
                return false;
            }

            D0CombatScenarioDefinition activeScenario =
                scenarioConfig == null ? null : scenarioConfig.AuthoredScenario;
            if (FpgRoomPlaytestOverrides.IsActive)
            {
                if (FpgRoomPlaytestOverrides.RoomDefinition == null
                    || FpgRoomPlaytestOverrides.ScenarioDefinition == null)
                {
                    error = "Room playtest override must provide both room and scenario.";
                    return false;
                }

                if (activeScenario != FpgRoomPlaytestOverrides.ScenarioDefinition)
                {
                    error = "BattleScenarioConfig did not resolve the requested playtest scenario.";
                    return false;
                }
            }
            else if (activeScenario != configuredScenario)
            {
                error = "Room binding scenario must match BattleScenarioConfig.AuthoredScenario.";
                return false;
            }

            return roomBinding.TryInitializeRoom(
                actorsRoot,
                combatAimReticle,
                out error);
        }

        /// <summary>
        /// Explicit presentation-only camera recoil bridge for the installed
        /// D0 slice. BattleSessionHost validates and caches this binding before
        /// it creates the authored session, so aim capture never scans the
        /// camera GameObject at runtime.
        /// </summary>
        public D0ShotCameraFeedbackController D0ShotCameraFeedbackController =>
            d0ShotCameraFeedbackController;

        /// <summary>
        /// Explicit presentation-only bridge for Fei in the installed D0
        /// slice. This is null in pre-D0 and non-D0 scenes by design.
        /// </summary>
        public Actor2DPresenter D0PlayerActorPresenter =>
            playerEntity == null ? null : playerEntity.ActorPresenter;

        /// <summary>
        /// Explicit presentation-only bridge for Burstbug in the installed D0
        /// slice. This is null in pre-D0 and non-D0 scenes by design.
        /// </summary>
        public Actor2DPresenter D0EnemyActorPresenter => ActiveD0EnemyActorPresenter;

        /// <summary>
        /// Explicit owner of the D0 floating hit-tip pool. It remains outside
        /// simulation and is only required after the D0 marker is installed.
        /// </summary>
        public D0HitTipPresenter D0HitTipPresenter => d0HitTipPresenter;

        /// <summary>
        /// Explicit D0-only read model for the three enemy threat families.
        /// It consumes copies of snapshots and trace entries but never writes
        /// the BattleSession.
        /// </summary>
        public ThreatTelegraph2DPresenter D0ThreatTelegraphPresenter =>
            d0ThreatTelegraphPresenter;

        /// <summary>
        /// Visual-only weakpoint pulse, lock and Break bridge for Burstbug.
        /// </summary>
        public D0WeakpointPresentationController D0WeakpointPresentationController =>
            d0WeakpointPresentationController;

        /// <summary>
        /// Fixed 16-voice presentation-only audio bridge for the D0 slice.
        /// </summary>
        public CombatAudioPresenter D0CombatAudioPresenter => d0CombatAudioPresenter;

        /// <summary>
        /// D0's player-facing HUD and terminal result bridge. It reads only
        /// snapshot/trace data supplied by BattlePresentationCoordinator.
        /// </summary>
        public CombatHud2DPresenter D0CombatHud2DPresenter => d0CombatHud2DPresenter;

        public bool TryValidate(out string error)
        {
            List<string> missingReferences = new List<string>();
            bool requiresFeedbackPresentation = RequiresFeedbackPresentation();
            bool requiresD0RuntimeBindings = scenarioConfig != null
                && scenarioConfig.UsesAuthoredScenario;
            bool requiresEnemyEntityWorld = RequiresEnemyEntityWorld();

            AddMissingReference(missingReferences, worldRoot, nameof(worldRoot));
            AddMissingReference(missingReferences, actorsRoot, nameof(actorsRoot));
            AddMissingReference(missingReferences, projectilesRoot, nameof(projectilesRoot));
            AddMissingReference(missingReferences, presentationRoot, nameof(presentationRoot));
            AddMissingReference(missingReferences, debugRoot, nameof(debugRoot));
            if (requiresD0RuntimeBindings)
            {
                AddMissingReference(missingReferences, playerEntity, nameof(playerEntity));
                ValidatePlayerEntityBinding(missingReferences);
            }
            else
            {
                AddMissingReference(missingReferences, PlayerAnchor, nameof(PlayerAnchor));
            }

            AddMissingReference(missingReferences, AimAnchor, nameof(AimAnchor));
            AddMissingReference(missingReferences, mainCamera, nameof(mainCamera));
            AddMissingReference(missingReferences, directionalLight, nameof(directionalLight));
            AddMissingReference(missingReferences, scenarioConfig, nameof(scenarioConfig));
            AddMissingReference(missingReferences, hitboxRegistry, nameof(hitboxRegistry));
            AddMissingReference(missingReferences, sessionHost, nameof(sessionHost));
            AddMissingReference(missingReferences, diagnosticsPresenter, nameof(diagnosticsPresenter));
            AddMissingReference(missingReferences, presentationCatalog, nameof(presentationCatalog));
            AddMissingReference(missingReferences, presentationCoordinator, nameof(presentationCoordinator));
            AddMissingReference(missingReferences, projectileViewRoot, nameof(projectileViewRoot));
            AddMissingReference(missingReferences, playerShotViewRoot, nameof(playerShotViewRoot));
            AddMissingReference(
                missingReferences,
                playerWeaponPresentationController,
                nameof(playerWeaponPresentationController));
            D0SliceInstallationMarker d0SliceMarker = FindD0SliceInstallationMarker();
            if (requiresD0RuntimeBindings || d0SliceMarker != null)
            {
                AddMissingReference(
                    missingReferences,
                    combatAimReticle,
                    nameof(combatAimReticle));
            }

            if (requiresD0RuntimeBindings)
            {
                ValidateD0RuntimeBindings(missingReferences, d0SliceMarker, false);
            }
            else if (d0SliceMarker != null)
            {
                if (!d0SliceMarker.TryValidate(out string markerError))
                {
                    missingReferences.Add(
                        $"d0SliceInstallationMarker is invalid: {markerError}");
                }

                ValidateD0SlicePresentationBindings(missingReferences, d0SliceMarker, false);
            }

            if (RequiresEnemyWeakpointWarningAnchor())
            {
                AddMissingReference(
                    missingReferences,
                    ActiveEnemyWeakpointAnchor,
                    nameof(ActiveEnemyWeakpointAnchor));
            }

            if (requiresFeedbackPresentation)
            {
                AddMissingReference(missingReferences, warningViewRoot, nameof(warningViewRoot));
                AddMissingReference(missingReferences, impactViewRoot, nameof(impactViewRoot));
                AddMissingReference(missingReferences, presentationCanvas, nameof(presentationCanvas));
                AddMissingReference(missingReferences, battleHudPresenter, nameof(battleHudPresenter));
            }

            if (scenarioConfig != null
                && !scenarioConfig.TryValidateSpatialConfiguration(out string spatialConfigurationError))
            {
                missingReferences.Add($"scenarioConfig spatial configuration is invalid: {spatialConfigurationError}");
            }

            if (scenarioConfig != null
                && !TryCreateScenarioDefinitionForCurrentMode(out var definition, out string scenarioDefinitionError))
            {
                missingReferences.Add($"scenarioConfig definition is invalid: {scenarioDefinitionError}");
            }
            else if (scenarioConfig != null && presentationCatalog != null
                && TryCreateScenarioDefinitionForCurrentMode(out var presentationDefinition, out _)
                && !TryValidatePresentationCatalog(
                    presentationDefinition,
                    requiresFeedbackPresentation,
                    out string presentationCatalogError))
            {
                missingReferences.Add($"presentationCatalog is invalid: {presentationCatalogError}");
            }

            if (scenarioConfig != null && hitboxRegistry != null
                && !hitboxRegistry.TryValidateStaticBindings(
                    scenarioConfig.AttackQuerySettings,
                    out string hitboxBindingError))
            {
                missingReferences.Add($"hitboxRegistry static bindings are invalid: {hitboxBindingError}");
            }

            if (diagnosticsPresenter != null
                && diagnosticsPresenter.SessionHost != sessionHost)
            {
                missingReferences.Add("diagnosticsPresenter must reference sessionHost");
            }

            ValidatePresentationCoordinatorBinding(missingReferences);

            if (projectileViewRoot != null && projectilesRoot != null
                && !projectileViewRoot.IsChildOf(projectilesRoot))
            {
                missingReferences.Add("projectileViewRoot must be parented below projectilesRoot");
            }

            ValidateShotAndAimBindings(missingReferences, false);

            if (requiresFeedbackPresentation)
            {
                ValidateFeedbackPresentation(
                    missingReferences,
                    warningViewRoot,
                    nameof(warningViewRoot));
                ValidateFeedbackPresentation(
                    missingReferences,
                    impactViewRoot,
                    nameof(impactViewRoot));

                if (warningViewRoot != null && impactViewRoot != null
                    && warningViewRoot == impactViewRoot)
                {
                    missingReferences.Add("warningViewRoot and impactViewRoot must be distinct");
                }

                if (presentationRoot != null && warningViewRoot != null
                    && !warningViewRoot.IsChildOf(presentationRoot))
                {
                    missingReferences.Add("warningViewRoot must be parented below presentationRoot");
                }

                if (presentationRoot != null && impactViewRoot != null
                    && !impactViewRoot.IsChildOf(presentationRoot))
                {
                    missingReferences.Add("impactViewRoot must be parented below presentationRoot");
                }

                if (presentationCanvas != null)
                {
                    if (presentationCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    {
                        missingReferences.Add("presentationCanvas must use RenderMode.ScreenSpaceOverlay");
                    }

                    if (presentationRoot != null
                        && !presentationCanvas.transform.IsChildOf(presentationRoot))
                    {
                        missingReferences.Add("presentationCanvas must be parented below presentationRoot");
                    }

                    ValidateFeedbackPresentation(
                        missingReferences,
                        presentationCanvas.transform,
                        nameof(presentationCanvas));
                }

                if (battleHudPresenter != null)
                {
                    if (presentationCanvas != null
                        && !battleHudPresenter.transform.IsChildOf(presentationCanvas.transform))
                    {
                        missingReferences.Add("battleHudPresenter must be parented below presentationCanvas");
                    }

                    if (!battleHudPresenter.TryValidate(out string hudError))
                    {
                        missingReferences.Add($"battleHudPresenter is invalid: {hudError}");
                    }
                }
            }

            if (directionalLight != null && directionalLight.type != LightType.Directional)
            {
                missingReferences.Add("directionalLight must use LightType.Directional");
            }

            Transform activePlayerAnchor = PlayerAnchor;
            Transform activeEnemyAnchor = ActiveEnemyGameplayAnchor;
            if (activePlayerAnchor != null && activeEnemyAnchor != null
                && activePlayerAnchor == activeEnemyAnchor)
            {
                missingReferences.Add("Player and active enemy Entity roots must be distinct");
            }

            Transform activePlayerGroundAnchor = PlayerGroundAnchor;
            if (activePlayerGroundAnchor != null && activePlayerAnchor != null
                && !activePlayerGroundAnchor.IsChildOf(activePlayerAnchor))
            {
                missingReferences.Add("PlayerGroundAnchor must be parented below PlayerAnchor");
            }

            error = missingReferences.Count == 0
                ? string.Empty
                : string.Join(", ", missingReferences);
            return missingReferences.Count == 0;
        }

        /// <summary>
        /// Checks the D0-only composition contract without requiring callers
        /// to validate every legacy presentation binding. BattleSessionHost
        /// invokes this before it creates a session so direct host callers
        /// cannot bypass the authored D0 fail-closed boundary.
        /// </summary>
        public bool TryValidateD0RuntimeBindings(out string error)
        {
            if (scenarioConfig == null || !scenarioConfig.UsesAuthoredScenario)
            {
                error = string.Empty;
                return true;
            }

            List<string> errors = new List<string>();
            AddRequiredD0CoreSceneReferences(errors);
            ValidateEnemyGameplayAnchorHierarchy(errors);
            ValidateProjectileIndependenceHierarchy(errors);
            ValidateShotAndAimBindings(errors, true);
            ValidatePresentationCoordinatorBinding(errors);
            ValidateD0RuntimeBindings(errors, FindD0SliceInstallationMarker(), true);
            error = errors.Count == 0 ? string.Empty : string.Join(", ", errors);
            return errors.Count == 0;
        }

        private void AddRequiredD0CoreSceneReferences(List<string> errors)
        {
            AddMissingReference(errors, playerEntity, nameof(playerEntity));
            ValidatePlayerEntityBinding(errors);
            AddMissingReference(errors, enemyEntityWorld, nameof(enemyEntityWorld));
            AddMissingReference(errors, presentationRoot, nameof(presentationRoot));
            AddMissingReference(errors, playerShotViewRoot, nameof(playerShotViewRoot));
            AddMissingReference(errors, AimAnchor, nameof(AimAnchor));
            AddMissingReference(errors, mainCamera, nameof(mainCamera));
            AddMissingReference(
                errors,
                presentationCoordinator,
                nameof(presentationCoordinator));
            AddMissingReference(
                errors,
                playerWeaponPresentationController,
                nameof(playerWeaponPresentationController));
            AddMissingReference(errors, combatAimReticle, nameof(combatAimReticle));
            AddMissingReference(errors, combatVfxWorld, nameof(combatVfxWorld));
            if (combatVfxWorld != null
                && !combatVfxWorld.TryValidate(out string vfxWorldError))
            {
                errors.Add($"combatVfxWorld is invalid: {vfxWorldError}");
            }
        }

        private void ValidatePlayerEntityBinding(List<string> errors)
        {
            if (playerEntity == null)
            {
                return;
            }

            if (!playerEntity.TryValidate(out string entityError))
            {
                errors.Add($"playerEntity is invalid: {entityError}");
            }

            if (actorsRoot != null
                && (playerEntity.transform == actorsRoot
                    || !playerEntity.transform.IsChildOf(actorsRoot)))
            {
                errors.Add("playerEntity must be parented below actorsRoot");
            }

            D0CombatScenarioDefinition scenario = scenarioConfig == null
                ? null
                : scenarioConfig.AuthoredScenario;
            if (scenario != null && scenario.Player != null
                && scenario.Player.EntityPrefab == null)
            {
                errors.Add("The authored player definition requires an EntityPrefab.");
            }
        }

        private void ValidateEnemyGameplayAnchorHierarchy(List<string> errors)
        {
            AddMissingReference(errors, enemyEntityWorld, nameof(enemyEntityWorld));
            if (enemyEntityWorld == null || actorsRoot == null)
            {
                return;
            }

            if (enemyEntityWorld.transform == actorsRoot
                || !enemyEntityWorld.transform.IsChildOf(actorsRoot))
            {
                errors.Add("enemyEntityWorld must be parented below actorsRoot");
            }
        }

        private void ValidateProjectileIndependenceHierarchy(List<string> errors)
        {
            Transform activePlayerAnchor = PlayerAnchor;
            Transform enemyEntityRoot = enemyEntityWorld == null
                ? null
                : enemyEntityWorld.EntityRoot;
            if (hitboxRegistry != null
                && (activePlayerAnchor != null
                        && hitboxRegistry.transform.IsChildOf(activePlayerAnchor)
                    || enemyEntityRoot != null
                        && hitboxRegistry.transform.IsChildOf(enemyEntityRoot)))
            {
                errors.Add(
                    "hitboxRegistry must remain outside player and enemy entities so projectile collision proxies never inherit actor motion");
            }

            if (projectilesRoot != null
                && (activePlayerAnchor != null
                        && projectilesRoot.IsChildOf(activePlayerAnchor)
                    || enemyEntityRoot != null
                        && projectilesRoot.IsChildOf(enemyEntityRoot)))
            {
                errors.Add(
                    "projectilesRoot must remain outside player and enemy entities so active projectiles never inherit actor motion");
            }

            if (projectileViewRoot != null
                && (activePlayerAnchor != null
                        && projectileViewRoot.IsChildOf(activePlayerAnchor)
                    || enemyEntityRoot != null
                        && projectileViewRoot.IsChildOf(enemyEntityRoot)))
            {
                errors.Add(
                    "projectileViewRoot must remain outside player and enemy entities so projectile views never inherit actor motion");
            }
        }

        private void ValidateShotAndAimBindings(
            List<string> errors,
            bool requireRuntimeBindings)
        {
            if (presentationRoot != null && playerShotViewRoot != null
                && !playerShotViewRoot.IsChildOf(presentationRoot))
            {
                errors.Add("playerShotViewRoot must be parented below presentationRoot");
            }

            if (playerShotViewRoot != null)
            {
                ValidateFeedbackPresentation(
                    errors,
                    playerShotViewRoot,
                    nameof(playerShotViewRoot));
            }

            Transform activeAimAnchor = AimAnchor;
            Transform activePlayerAnchor = PlayerAnchor;
            if (activeAimAnchor != null && activePlayerAnchor != null
                && !activeAimAnchor.IsChildOf(activePlayerAnchor))
            {
                errors.Add("AimAnchor must be parented below PlayerAnchor");
            }

            if (playerWeaponPresentationController != null)
            {
                if (playerShotViewRoot != null
                    && !playerWeaponPresentationController.transform.IsChildOf(playerShotViewRoot))
                {
                    errors.Add(
                        "playerWeaponPresentationController must be parented below playerShotViewRoot");
                }

                if (requireRuntimeBindings
                    && playerWeaponPresentationController.SessionHost != sessionHost)
                {
                    errors.Add(
                        "playerWeaponPresentationController must reference sessionHost");
                }

                if (playerWeaponPresentationController.ShotViewRoot != playerShotViewRoot)
                {
                    errors.Add(
                        "playerWeaponPresentationController must reference playerShotViewRoot");
                }

                bool playerWeaponValid = requireRuntimeBindings
                    ? playerWeaponPresentationController.TryValidate(
                        out string playerWeaponError)
                    : playerWeaponPresentationController.TryValidateAuthoring(
                        out playerWeaponError);
                if (!playerWeaponValid)
                {
                    errors.Add(
                        $"playerWeaponPresentationController is invalid: {playerWeaponError}");
                }
            }

            ValidateCombatAimReticleBinding(errors, requireRuntimeBindings);
        }

        private void ValidatePresentationCoordinatorBinding(List<string> errors)
        {
            if (presentationCoordinator != null
                && presentationCoordinator.SessionHost != sessionHost)
            {
                errors.Add("presentationCoordinator must reference sessionHost");
            }
        }

        private bool TryCreateScenarioDefinitionForCurrentMode(
            out ScenarioDefinition definition,
            out string error)
        {
            if (scenarioConfig == null)
            {
                definition = null;
                error = "BattleSceneContext requires a scenario config.";
                return false;
            }

            return roomBinding != null
                ? scenarioConfig.TryCreateDefinitionForRoom(out definition, out error)
                : scenarioConfig.TryCreateDefinition(out definition, out error);
        }
        private static void AddMissingReference(List<string> missingReferences, Object reference, string referenceName)
        {
            if (reference == null)
            {
                missingReferences.Add(referenceName);
            }
        }

        private bool RequiresFeedbackPresentation()
        {
            return (presentationCatalog != null && presentationCatalog.WarningEntryCount > 0)
                || warningViewRoot != null
                || impactViewRoot != null
                || presentationCanvas != null
                || battleHudPresenter != null;
        }

        private bool RequiresEnemyWeakpointWarningAnchor()
        {
            return presentationCatalog != null
                && presentationCatalog.UsesWarningAnchorKind(WarningAnchorKind.EnemyWeakpoint);
        }

        private D0SliceInstallationMarker FindD0SliceInstallationMarker()
        {
            return presentationRoot == null
                ? null
                : presentationRoot.GetComponentInChildren<D0SliceInstallationMarker>(true);
        }

        private void ValidateD0RuntimeBindings(
            List<string> errors,
            D0SliceInstallationMarker marker,
            bool requireRuntimeBindings)
        {
            AddMissingReference(errors, marker, "d0SliceInstallationMarker");
            if (marker != null)
            {
                if (!marker.TryValidate(out string markerError))
                {
                    errors.Add($"d0SliceInstallationMarker is invalid: {markerError}");
                }

                ValidateD0SlicePresentationBindings(
                    errors,
                    marker,
                    requireRuntimeBindings);
            }

            ValidateEncounterSpawnPoints(errors);
            ValidateD0EnemyBehaviorBinding(errors, requireRuntimeBindings);
            ValidateD0ShotCameraFeedbackBinding(errors, requireRuntimeBindings);
            if (RequiresEnemyEntityWorld())
            {
                AddMissingReference(errors, enemyEntityWorld, nameof(enemyEntityWorld));
                if (enemyEntityWorld != null)
                {
                    if (!enemyEntityWorld.TryValidate(out string entityWorldError))
                    {
                        errors.Add($"enemyEntityWorld is invalid: {entityWorldError}");
                    }

                    if (actorsRoot != null
                        && (enemyEntityWorld.transform == actorsRoot
                            || !enemyEntityWorld.transform.IsChildOf(actorsRoot)))
                    {
                        errors.Add("enemyEntityWorld must be parented below actorsRoot");
                    }

                    if (enemyEntityWorld.SessionHost != sessionHost
                        || enemyEntityWorld.HitboxRegistry != hitboxRegistry)
                    {
                        errors.Add(
                            "enemyEntityWorld must use this context's sessionHost and hitboxRegistry");
                    }
                }
            }
        }

        private bool RequiresEnemyEntityWorld()
        {
            D0CombatScenarioDefinition scenario = scenarioConfig == null
                ? null
                : scenarioConfig.AuthoredScenario;
            return scenario != null
                && scenario.Encounter != null
                && scenario.Encounter.SpawnSlotCount > 0;
        }

        private void ValidateEncounterSpawnPoints(List<string> errors)
        {
            D0CombatScenarioDefinition scenario = scenarioConfig == null
                ? null
                : scenarioConfig.AuthoredScenario;
            if (scenario == null)
            {
                return;
            }

            if (roomBinding != null)
            {
                if (!roomBinding.TryValidate(out string roomError))
                {
                    errors.Add($"roomBinding is invalid: {roomError}");
                }

                if (roomBinding.ScenarioDefinition != scenario)
                {
                    errors.Add("roomBinding scenario must match the active authored scenario");
                }

                if (roomBinding.IsInitialized
                    && !roomBinding.TryValidateRuntimeSpawnPoints(
                        actorsRoot,
                        out string spawnError))
                {
                    errors.Add($"roomBinding runtime spawn points are invalid: {spawnError}");
                }

                return;
            }
            D0SpawnPoint[] points = encounterSpawnPoints
                ?? System.Array.Empty<D0SpawnPoint>();
            if (points.Length == 0)
            {
                errors.Add("encounterSpawnPoints must bind the authored stage spawn points");
                return;
            }

            HashSet<string> ids = new HashSet<string>(
                System.StringComparer.Ordinal);
            for (int index = 0; index < points.Length; index++)
            {
                D0SpawnPoint point = points[index];
                if (point == null)
                {
                    errors.Add($"encounterSpawnPoints[{index}] is missing");
                    continue;
                }

                if (!point.TryValidate(out string pointError))
                {
                    errors.Add($"encounterSpawnPoints[{index}] is invalid: {pointError}");
                }

                if (actorsRoot != null
                    && (point.transform == actorsRoot
                        || !point.transform.IsChildOf(actorsRoot)))
                {
                    errors.Add(
                        $"encounterSpawnPoints[{index}] must be parented below actorsRoot");
                }

                if (!ids.Add(point.SpawnPointId))
                {
                    errors.Add(
                        $"encounter spawn point id '{point.SpawnPointId}' must be unique");
                }

                D0StageDefinition stage = scenario.StageDefinition;
                if (stage == null
                    || !stage.TryGetSpawnPoint(
                        point.SpawnPointId,
                        out D0StageSpawnPointDefinition definition))
                {
                    errors.Add(
                        $"encounter spawn point '{point.SpawnPointId}' is not defined by the authored stage");
                    continue;
                }

                if (actorsRoot != null)
                {
                    Vector3 expectedPosition =
                        actorsRoot.TransformPoint(definition.LocalPosition);
                    Quaternion expectedRotation = actorsRoot.rotation
                        * Quaternion.Euler(definition.LocalEulerAngles);
                    if (Vector3.Distance(
                            point.transform.position,
                            expectedPosition) > 0.001f
                        || Quaternion.Angle(
                            point.transform.rotation,
                            expectedRotation) > 0.01f)
                    {
                        errors.Add(
                            $"encounter spawn point '{point.SpawnPointId}' does not match the authored stage pose");
                    }
                }
            }

            if (!TryGetEncounterSpawnPoint(
                    scenario.PlayerSpawnPointId,
                    out _))
            {
                errors.Add(
                    $"player spawn point '{scenario.PlayerSpawnPointId}' is not bound");
            }

            D0EncounterDefinition encounter = scenario.Encounter;
            if (encounter == null)
            {
                return;
            }

            for (int index = 0; index < encounter.SpawnSlotCount; index++)
            {
                string spawnPointId =
                    encounter.GetSpawnSlot(index).SpawnPointId;
                if (!TryGetEncounterSpawnPoint(spawnPointId, out _))
                {
                    errors.Add(
                        $"enemy spawn point '{spawnPointId}' is not bound");
                }
            }
        }

        private void ValidateCombatAimReticleBinding(
            List<string> errors,
            bool requireRuntimeBindings)
        {
            if (combatAimReticle == null)
            {
                return;
            }

            if (presentationRoot != null
                && !combatAimReticle.transform.IsChildOf(presentationRoot))
            {
                errors.Add("combatAimReticle must be parented below presentationRoot");
            }

            if (combatAimReticle.SessionHost != sessionHost)
            {
                errors.Add("combatAimReticle must reference sessionHost");
            }

            if (!combatAimReticle.TryValidate(out string reticleError))
            {
                errors.Add($"combatAimReticle is invalid: {reticleError}");
            }

            bool hasAuthoredD0Scenario = scenarioConfig != null
                && scenarioConfig.UsesAuthoredScenario;
            bool requiresD0AimIndicator = hasAuthoredD0Scenario
                || FindD0SliceInstallationMarker() != null;
            if (!requiresD0AimIndicator)
            {
                return;
            }

            LayeredAimIndicatorGraphic indicatorGraphic =
                combatAimReticle.GetComponent<LayeredAimIndicatorGraphic>();
            PlayerAimIndicatorPresenter indicatorPresenter =
                combatAimReticle.GetComponent<PlayerAimIndicatorPresenter>();
            if (indicatorGraphic == null)
            {
                errors.Add(
                    "combatAimReticle requires LayeredAimIndicatorGraphic for the D0 slice");
            }

            if (indicatorPresenter == null)
            {
                errors.Add(
                    "combatAimReticle requires PlayerAimIndicatorPresenter for the D0 slice");
            }

            if (indicatorGraphic == null || indicatorPresenter == null)
            {
                return;
            }

            if (!indicatorGraphic.isActiveAndEnabled)
            {
                errors.Add("layeredAimIndicatorGraphic must be active and enabled");
            }

            if (indicatorGraphic.raycastTarget)
            {
                errors.Add("layeredAimIndicatorGraphic must not receive UI raycasts");
            }

            if (!indicatorPresenter.isActiveAndEnabled)
            {
                errors.Add("playerAimIndicatorPresenter must be active and enabled");
            }

            if (indicatorPresenter.SessionHost != sessionHost)
            {
                errors.Add("playerAimIndicatorPresenter must reference sessionHost");
            }

            if (indicatorPresenter.IndicatorGraphic != indicatorGraphic)
            {
                errors.Add(
                    "playerAimIndicatorPresenter must reference the reticle's layered graphic");
            }

            if (hasAuthoredD0Scenario && requireRuntimeBindings)
            {
                D0CombatScenarioDefinition scenario = scenarioConfig.AuthoredScenario;
                D0WeaponDefinition expectedWeapon = scenario == null
                    || scenario.Player == null
                    ? null
                    : scenario.Player.Weapon;
                if (expectedWeapon == null
                    || indicatorPresenter.WeaponDefinition != expectedWeapon)
                {
                    errors.Add(
                        "playerAimIndicatorPresenter must use the active player's WeaponDefinition");
                }
            }

            if (!indicatorGraphic.TryValidate(out string graphicError))
            {
                errors.Add($"layeredAimIndicatorGraphic is invalid: {graphicError}");
            }

            if (requireRuntimeBindings
                && !indicatorPresenter.TryValidate(out string indicatorError))
            {
                errors.Add($"playerAimIndicatorPresenter is invalid: {indicatorError}");
            }
        }

        private void ValidateD0EnemyBehaviorBinding(
            List<string> errors,
            bool requireRuntimeBindings)
        {
            AddMissingReference(
                errors,
                d0EnemyBehaviorController,
                nameof(d0EnemyBehaviorController));
            if (d0EnemyBehaviorController == null)
            {
                return;
            }

            if (!d0EnemyBehaviorController.isActiveAndEnabled)
            {
                errors.Add("d0EnemyBehaviorController must be active and enabled");
            }

            if (actorsRoot != null
                && (d0EnemyBehaviorController.transform == actorsRoot
                    || !d0EnemyBehaviorController.transform.IsChildOf(actorsRoot)))
            {
                errors.Add("d0EnemyBehaviorController must be parented below actorsRoot");
            }

            if (!requireRuntimeBindings)
            {
                return;
            }

            if (d0EnemyBehaviorController.SessionHost != sessionHost)
            {
                errors.Add("d0EnemyBehaviorController must reference sessionHost");
            }

            D0CombatScenarioDefinition authoredScenario = scenarioConfig == null
                ? null
                : scenarioConfig.AuthoredScenario;
            D0EncounterDefinition authoredEncounter = authoredScenario == null
                ? null
                : authoredScenario.Encounter;
            D0EnemyDefinition authoredEnemy = authoredEncounter == null
                ? null
                : authoredEncounter.Enemy;
            D0EnemyEntityView authoredEntityPrefab = authoredEnemy == null
                ? null
                : authoredEnemy.EntityPrefab;
            D0EnemyEntityView activeEntity = enemyEntityWorld == null
                ? null
                : enemyEntityWorld.ActiveEntity;
            Transform expectedGameplayAnchor = activeEntity == null
                ? authoredEntityPrefab == null ? null : authoredEntityPrefab.GameplayAnchor
                : activeEntity.GameplayAnchor;
            Transform expectedVisualRoot = activeEntity == null
                ? authoredEntityPrefab == null ? null : authoredEntityPrefab.VisualRoot
                : activeEntity.VisualRoot;

            if (d0EnemyBehaviorController.GameplayAnchor != expectedGameplayAnchor)
            {
                errors.Add(
                    "d0EnemyBehaviorController must reference the selected enemy Entity gameplay anchor");
            }

            if (d0EnemyBehaviorController.VisualRoot != expectedVisualRoot)
            {
                errors.Add(
                    "d0EnemyBehaviorController must reference the selected enemy Entity visual root");
            }

            D0EnemyBehaviorProfile authoredProfile = authoredEnemy == null
                ? null
                : authoredEnemy.BehaviorProfile;
            if (authoredEncounter == null || authoredProfile == null)
            {
                errors.Add(
                    "D0 authored scenario must provide an encounter and enemy behavior profile");
            }
            else
            {
                if (d0EnemyBehaviorController.Encounter != authoredEncounter)
                {
                    errors.Add(
                        "d0EnemyBehaviorController must reference the authored D0 encounter");
                }

                if (d0EnemyBehaviorController.BehaviorProfile != authoredProfile)
                {
                    errors.Add(
                        "d0EnemyBehaviorController must reference the authored enemy behavior profile");
                }
            }

            if (!d0EnemyBehaviorController.TryValidate(out string behaviorError))
            {
                errors.Add($"d0EnemyBehaviorController is invalid: {behaviorError}");
            }
        }

        private void ValidateD0ShotCameraFeedbackBinding(
            List<string> errors,
            bool requireRuntimeBindings)
        {
            AddMissingReference(
                errors,
                d0ShotCameraFeedbackController,
                nameof(d0ShotCameraFeedbackController));
            if (d0ShotCameraFeedbackController == null)
            {
                return;
            }

            if (!d0ShotCameraFeedbackController.isActiveAndEnabled)
            {
                errors.Add("d0ShotCameraFeedbackController must be active and enabled");
            }

            if (mainCamera != null
                && d0ShotCameraFeedbackController.gameObject != mainCamera.gameObject)
            {
                errors.Add("d0ShotCameraFeedbackController must be attached to mainCamera");
            }

            if (!requireRuntimeBindings)
            {
                return;
            }

            if (d0ShotCameraFeedbackController.SessionHost != sessionHost)
            {
                errors.Add("d0ShotCameraFeedbackController must reference sessionHost");
            }

            D0CombatScenarioDefinition authoredScenario = scenarioConfig == null
                ? null
                : scenarioConfig.AuthoredScenario;
            D0ThreeCProfile authoredProfile = authoredScenario == null
                ? null
                : authoredScenario.ThreeCProfile;
            if (authoredProfile == null)
            {
                errors.Add("D0 authored scenario must provide an authoritative D0 3C profile");
            }
            else if (d0ShotCameraFeedbackController.ThreeCProfile != authoredProfile)
            {
                errors.Add(
                    "d0ShotCameraFeedbackController must reference the authoritative D0 3C profile");
            }

            if (d0ShotCameraFeedbackController.TargetCamera != mainCamera)
            {
                errors.Add("d0ShotCameraFeedbackController must reference mainCamera");
            }

            if (!d0ShotCameraFeedbackController.TryValidate(out string feedbackError))
            {
                errors.Add($"d0ShotCameraFeedbackController is invalid: {feedbackError}");
            }
        }

        private void ValidateD0SlicePresentationBindings(
            List<string> errors,
            D0SliceInstallationMarker marker,
            bool requireRuntimeBindings)
        {
            D0CombatScenarioDefinition authoredScenario = scenarioConfig == null
                ? null
                : scenarioConfig.AuthoredScenario;
            D0ActorPresentationDefinition authoredPlayerPresentation =
                authoredScenario == null || authoredScenario.Player == null
                    ? null
                    : authoredScenario.Player.ActorPresentation;
            D0ActorPresentationDefinition authoredEnemyPresentation =
                authoredScenario == null || authoredScenario.Encounter == null
                    || authoredScenario.Encounter.Enemy == null
                    ? null
                    : authoredScenario.Encounter.Enemy.ActorPresentation;

            Actor2DPresenter activePlayerPresenter = D0PlayerActorPresenter;
            Actor2DPresenter activeEnemyPresenter = ActiveD0EnemyActorPresenter;
            if (activeEnemyPresenter == null
                && authoredScenario != null
                && authoredScenario.Encounter != null
                && authoredScenario.Encounter.Enemy != null
                && authoredScenario.Encounter.Enemy.EntityPrefab != null)
            {
                activeEnemyPresenter =
                    authoredScenario.Encounter.Enemy.EntityPrefab.ActorPresenter;
            }

            AddMissingReference(errors, activePlayerPresenter, nameof(D0PlayerActorPresenter));
            AddMissingReference(errors, activeEnemyPresenter, nameof(ActiveD0EnemyActorPresenter));
            AddMissingReference(errors, d0HitTipPresenter, nameof(d0HitTipPresenter));
            AddMissingReference(
                errors,
                d0ThreatTelegraphPresenter,
                nameof(d0ThreatTelegraphPresenter));
            AddMissingReference(
                errors,
                d0WeakpointPresentationController,
                nameof(d0WeakpointPresentationController));
            AddMissingReference(
                errors,
                d0CombatAudioPresenter,
                nameof(d0CombatAudioPresenter));
            AddMissingReference(
                errors,
                d0CombatHud2DPresenter,
                nameof(d0CombatHud2DPresenter));

            if (activePlayerPresenter != null)
            {
                if (!activePlayerPresenter.IsPlayerActor)
                {
                    errors.Add("D0PlayerActorPresenter must be configured as the player actor");
                }

                if (activePlayerPresenter != playerEntity.ActorPresenter)
                {
                    errors.Add("D0PlayerActorPresenter must come from playerEntity");
                }

                string playerActorError;
                bool playerPresenterValid = authoredPlayerPresentation == null
                    ? activePlayerPresenter.TryValidate(out playerActorError)
                    : activePlayerPresenter.TryValidateWithPresentation(
                        authoredPlayerPresentation,
                        out playerActorError);
                if (!playerPresenterValid)
                {
                    errors.Add($"D0PlayerActorPresenter is invalid: {playerActorError}");
                }
            }

            if (activeEnemyPresenter != null)
            {
                if (activeEnemyPresenter.IsPlayerActor)
                {
                    errors.Add("ActiveD0EnemyActorPresenter must be configured as an enemy actor");
                }

                Actor2DPresenter worldPresenter = enemyEntityWorld == null
                    ? null
                    : enemyEntityWorld.ActiveActorPresenter;
                Actor2DPresenter prefabPresenter = authoredScenario == null
                    || authoredScenario.Encounter == null
                    || authoredScenario.Encounter.Enemy == null
                    || authoredScenario.Encounter.Enemy.EntityPrefab == null
                    ? null
                    : authoredScenario.Encounter.Enemy.EntityPrefab.ActorPresenter;
                if (activeEnemyPresenter != worldPresenter
                    && activeEnemyPresenter != prefabPresenter)
                {
                    errors.Add(
                        "ActiveD0EnemyActorPresenter must come from EnemyEntityWorld or the selected EntityPrefab");
                }

                string enemyActorError;
                bool enemyPresenterValid = authoredEnemyPresentation == null
                    ? activeEnemyPresenter.TryValidate(out enemyActorError)
                    : activeEnemyPresenter.TryValidateWithPresentation(
                        authoredEnemyPresentation,
                        out enemyActorError);
                if (!enemyPresenterValid)
                {
                    errors.Add($"ActiveD0EnemyActorPresenter is invalid: {enemyActorError}");
                }
            }

            if (activePlayerPresenter != null
                && activePlayerPresenter == activeEnemyPresenter)
            {
                errors.Add("Player and enemy Entity presenters must be distinct");
            }

            if (d0HitTipPresenter != null)
            {
                if (!(d0HitTipPresenter.transform is RectTransform))
                {
                    errors.Add("d0HitTipPresenter must be attached to a RectTransform");
                }

                ValidateD0PresentationChild(
                    errors,
                    d0HitTipPresenter.transform,
                    marker.transform,
                    nameof(d0HitTipPresenter));

                if (!d0HitTipPresenter.TryValidate(out string hitTipError))
                {
                    errors.Add($"d0HitTipPresenter is invalid: {hitTipError}");
                }
            }

            ValidateD0ThreatPresentationBindings(
                errors,
                marker,
                requireRuntimeBindings);
            ValidateD0AudioPresentationBinding(errors, marker);
            ValidateD0HudPresentationBinding(errors, marker);

            // D0 must never silently fall back to an unprofiled camera ray or
            // omit Fei's charge/release route. The player-shot controller is
            // intentionally optional in pre-D0 scenes, but once the D0 marker
            // is installed all of these presentation-only bindings become part
            // of the scene contract.
            if (playerWeaponPresentationController == null)
            {
                return;
            }

            if (playerWeaponPresentationController.PresentationProfile
                != marker.PresentationProfile)
            {
                errors.Add(
                    "playerWeaponPresentationController must reference the D0 marker presentation profile");
            }

            if (!requireRuntimeBindings)
            {
                if (!playerWeaponPresentationController.TryValidateAuthoring(
                        out string playerWeaponAuthoringError))
                {
                    errors.Add(
                        $"playerWeaponPresentationController is invalid: {playerWeaponAuthoringError}");
                }

                return;
            }

            if (playerWeaponPresentationController.ActorPresenter
                != activePlayerPresenter)
            {
                errors.Add(
                    "playerWeaponPresentationController must reference the active player Entity presenter");
            }

            if (playerWeaponPresentationController.PresentationCamera != mainCamera)
            {
                errors.Add(
                    "playerWeaponPresentationController must reference mainCamera for D0 free-aim presentation");
            }

            if (playerEntity == null)
            {
                return;
            }

            if (playerWeaponPresentationController.PlayerEntity != playerEntity)
            {
                errors.Add(
                    "playerWeaponPresentationController must bind the active playerEntity");
            }

            D0WeaponDefinition activeWeapon = authoredScenario == null
                || authoredScenario.Player == null
                ? null
                : authoredScenario.Player.Weapon;
            if (playerWeaponPresentationController.WeaponDefinition != activeWeapon)
            {
                errors.Add(
                    "playerWeaponPresentationController must bind the active player's WeaponDefinition");
            }

            D0ActorSocketRegistry registry = playerEntity.SocketRegistry;
            if (activeWeapon != null
                && (registry == null
                    || !registry.TryResolve(
                        activeWeapon.PrimaryPresentation.SocketId,
                        out _)
                    || activeWeapon.SecondaryPresentation == null
                    || activeWeapon.SecondaryPresentation.Shot == null
                    || !registry.TryResolve(
                        activeWeapon.SecondaryPresentation.Shot.SocketId,
                        out _)))
            {
                errors.Add(
                    "playerEntity SocketRegistry must resolve every active weapon source socket");
            }
        }

        private void ValidateD0ThreatPresentationBindings(
            List<string> errors,
            D0SliceInstallationMarker marker,
            bool requireRuntimeBindings)
        {
            if (d0ThreatTelegraphPresenter != null)
            {
                ValidateD0PresentationChild(
                    errors,
                    d0ThreatTelegraphPresenter.transform,
                    marker.transform,
                    nameof(d0ThreatTelegraphPresenter));
                ValidateFeedbackPresentation(
                    errors,
                    d0ThreatTelegraphPresenter.transform,
                    nameof(d0ThreatTelegraphPresenter));
                bool threatValid = requireRuntimeBindings
                    ? d0ThreatTelegraphPresenter.TryValidate(
                        out string threatError)
                    : d0ThreatTelegraphPresenter.TryValidateAuthoring(
                        out threatError);
                if (!threatValid)
                {
                    errors.Add($"d0ThreatTelegraphPresenter is invalid: {threatError}");
                }
            }

            if (d0WeakpointPresentationController != null)
            {
                ValidateD0PresentationChild(
                    errors,
                    d0WeakpointPresentationController.transform,
                    marker.transform,
                    nameof(d0WeakpointPresentationController));
                ValidateFeedbackPresentation(
                    errors,
                    d0WeakpointPresentationController.transform,
                    nameof(d0WeakpointPresentationController));
                bool weakpointValid = requireRuntimeBindings
                    ? d0WeakpointPresentationController.TryValidate(
                        out string weakpointError)
                    : d0WeakpointPresentationController.TryValidateAuthoring(
                        out weakpointError);
                if (!weakpointValid)
                {
                    errors.Add(
                        $"d0WeakpointPresentationController is invalid: {weakpointError}");
                }
            }

        }

        private void ValidateD0AudioPresentationBinding(
            List<string> errors,
            D0SliceInstallationMarker marker)
        {
            if (d0CombatAudioPresenter == null)
            {
                return;
            }

            ValidateD0PresentationChild(
                errors,
                d0CombatAudioPresenter.transform,
                marker.transform,
                nameof(d0CombatAudioPresenter));
            ValidateFeedbackPresentation(
                errors,
                d0CombatAudioPresenter.transform,
                nameof(d0CombatAudioPresenter));
            if (d0CombatAudioPresenter.SessionHost != sessionHost)
            {
                errors.Add("d0CombatAudioPresenter must reference sessionHost");
            }

            if (d0CombatAudioPresenter.AudioBank != marker.AudioBank)
            {
                errors.Add("d0CombatAudioPresenter must reference the D0 marker audio bank");
            }

            if (d0CombatAudioPresenter.PresentationProfile != marker.PresentationProfile)
            {
                errors.Add("d0CombatAudioPresenter must reference the D0 marker presentation profile");
            }

            if (d0ThreatTelegraphPresenter != null
                && d0ThreatTelegraphPresenter.AudioPresenter != d0CombatAudioPresenter)
            {
                errors.Add(
                    "d0ThreatTelegraphPresenter must route local presentation cues through d0CombatAudioPresenter");
            }

            if (d0WeakpointPresentationController != null
                && d0WeakpointPresentationController.AudioPresenter != d0CombatAudioPresenter)
            {
                errors.Add(
                    "d0WeakpointPresentationController must route local presentation cues through d0CombatAudioPresenter");
            }

            if (!d0CombatAudioPresenter.TryValidate(out string audioError))
            {
                errors.Add($"d0CombatAudioPresenter is invalid: {audioError}");
            }
        }

        private void ValidateD0HudPresentationBinding(
            List<string> errors,
            D0SliceInstallationMarker marker)
        {
            if (d0CombatHud2DPresenter == null)
            {
                return;
            }

            ValidateD0PresentationChild(
                errors,
                d0CombatHud2DPresenter.transform,
                marker.transform,
                nameof(d0CombatHud2DPresenter));
            ValidateFeedbackPresentation(
                errors,
                d0CombatHud2DPresenter.transform,
                nameof(d0CombatHud2DPresenter));
            if (d0CombatHud2DPresenter.PresentationProfile != marker.PresentationProfile)
            {
                errors.Add(
                    "d0CombatHud2DPresenter must reference the D0 marker presentation profile");
            }

            if (!d0CombatHud2DPresenter.TryValidate(out string hudError))
            {
                errors.Add($"d0CombatHud2DPresenter is invalid: {hudError}");
            }
        }

        private static void ValidateD0PresentationChild(
            List<string> errors,
            Transform candidate,
            Transform markerRoot,
            string referenceName)
        {
            if (candidate == null || markerRoot == null)
            {
                return;
            }

            if (candidate == markerRoot || !candidate.IsChildOf(markerRoot))
            {
                errors.Add($"{referenceName} must be parented below the D0 slice installation marker");
            }
        }

        private bool TryValidatePresentationCatalog(
            FPG.Demo.Run.ScenarioDefinition definition,
            bool requiresFeedbackPresentation,
            out string error)
        {
            return requiresFeedbackPresentation
                ? presentationCatalog.TryValidatePresentationCoverage(definition, out error)
                : presentationCatalog.TryValidateProjectileCoverage(definition, out error);
        }

        private static void ValidateFeedbackPresentation(
            List<string> errors,
            Transform root,
            string rootName)
        {
            if (root == null)
            {
                return;
            }

            if (root.GetComponentsInChildren<Collider>(true).Length > 0
                || root.GetComponentsInChildren<Collider2D>(true).Length > 0)
            {
                errors.Add($"{rootName} must not contain colliders");
            }

            if (root.GetComponentsInChildren<Rigidbody>(true).Length > 0
                || root.GetComponentsInChildren<Rigidbody2D>(true).Length > 0)
            {
                errors.Add($"{rootName} must not contain rigidbodies");
            }
        }
    }
}
