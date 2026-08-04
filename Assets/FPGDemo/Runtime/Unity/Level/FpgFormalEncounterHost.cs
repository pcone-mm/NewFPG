using System;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Explicit-reference scene root for the formal encounter path. It is a
    /// neutral host, separate from CombatLab/BattleSession, and deliberately
    /// does not discover services through Find or static globals.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class FpgFormalEncounterHost : MonoBehaviour,
        IFpgShootingTuningPreviewHost
    {
        [Header("Scene Roots")]
        [SerializeField]
        private Transform actorsRoot;

        [SerializeField]
        private Transform cameraRoot;

        [SerializeField]
        private Transform presentationRoot;

        [Header("Room Art Presentation")]
        [SerializeField]
        private Camera worldCamera;

        [SerializeField]
        private FpgRoomArtSceneLoader roomArtSceneLoader;

        [Header("Formal Runtime")]
        [SerializeField]
        private FpgEncounterHost encounterHost;

        [SerializeField]
        private FpgRoomEncounterDirector encounterDirector;

        [SerializeField]
        private FpgEnemyEntityPool enemyEntityPool;

        [SerializeField]
        private FpgCombatantAnchorMap combatantAnchorMap;

        [Header("Player Composition")]
        [SerializeField]
        private FpgPlayableCharacterCatalog playableCharacterCatalog;

        [SerializeField]
        private FpgFormalPlayerComposer playerComposer;

        [Header("External Ports")]
        [Tooltip("Scene-owned player/input adapter. The formal host never searches for it.")]
        [SerializeField]
        private MonoBehaviour playerInputPort;

        [Tooltip("Scene-owned physics/query adapter. The formal host never searches for it.")]
        [SerializeField]
        private MonoBehaviour physicsQueryPort;

        [Tooltip("Scene-owned IFpgFormalCombatPortFactory implementation.")]
        [SerializeField]
        private MonoBehaviour combatPortFactory;

        [Header("Development Diagnostics")]
        [SerializeField]
        private bool enableShootingDiagnostics = true;

        private bool disposed;
        private string portBindingError = string.Empty;
        private bool hasLastValidShootingSnapshot;
        private FpgShootingTuningSnapshot lastValidShootingSnapshot;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private FpgShootingDevelopmentPanel shootingDevelopmentPanel;
#endif

        public Transform ActorsRoot => actorsRoot;
        public Transform CameraRoot => cameraRoot;
        public Transform PresentationRoot => presentationRoot;
        public Camera WorldCamera => worldCamera;
        public FpgRoomArtSceneLoader RoomArtSceneLoader => roomArtSceneLoader;
        public FpgEncounterHost EncounterHost => encounterHost;
        public FpgRoomDefinition RoomDefinition =>
            encounterHost == null ? null : encounterHost.RoomDefinition;
        public FpgRoomEncounterDirector EncounterDirector => encounterDirector;
        public FpgEnemyEntityPool EnemyEntityPool => enemyEntityPool;
        public FpgCombatantAnchorMap CombatantAnchorMap => combatantAnchorMap;
        public FpgPlayableCharacterCatalog PlayableCharacterCatalog =>
            playableCharacterCatalog;
        public FpgFormalPlayerComposer PlayerComposer => playerComposer;
        public FpgPlayableCharacterSelection ActivePlayerSelection =>
            playerComposer == null
                ? default(FpgPlayableCharacterSelection)
                : playerComposer.ActiveSelection;
        public D0CharacterDefinition ActivePlayerDefinition =>
            playerComposer == null ? null : playerComposer.ActiveDefinition;
        public FpgPlayerEntityView ActivePlayerEntity =>
            playerComposer == null ? null : playerComposer.ActiveEntity;
        public bool IsPlayerComposed => playerComposer != null
            && playerComposer.IsComposed;
        public bool IsPlayerPresentationActive => playerComposer != null
            && playerComposer.IsPresentationActive;
        public MonoBehaviour PlayerInputPort => playerInputPort;
        public MonoBehaviour PhysicsQueryPort => physicsQueryPort;
        public MonoBehaviour CombatPortFactory => combatPortFactory;
        public ICombatAimViewportSource AimViewportSource =>
            (playerInputPort as FpgFormalPlayerTickDriver)?.AimViewportSource;
        public FpgEncounterSession Session =>
            encounterDirector == null ? null : encounterDirector.Session;
        public FpgFormalCombatRuntimeBundle CombatRuntime =>
            encounterDirector == null ? null : encounterDirector.CombatRuntime;
        public bool IsDisposed => disposed;

        public event Action<FpgEncounterLifecycleEvent> LifecycleEvent;
        public event Action<FpgRoomClearedEvent> RoomCleared;

        private void Awake()
        {
            FpgShootingTuningRuntimeRegistry.Register(this);
            if (encounterHost == null)
            {
                TryGetComponent(out encounterHost);
            }

            if (encounterDirector != null)
            {
                encounterDirector.LifecycleEvent += HandleLifecycleEvent;
                encounterDirector.RoomCleared += HandleRoomCleared;
                if (!encounterDirector.TryConfigureFormalSessionPorts(
                        combatPortFactory as IFpgFormalCombatPortFactory,
                        playerInputPort as IFpgFormalPlayerTickDriver,
                        out portBindingError))
                {
                    portBindingError = string.IsNullOrWhiteSpace(portBindingError)
                        ? "Formal host port binding failed."
                        : portBindingError;
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (enableShootingDiagnostics)
            {
                shootingDevelopmentPanel =
                    GetComponent<FpgShootingDevelopmentPanel>();
                if (shootingDevelopmentPanel == null)
                {
                    shootingDevelopmentPanel = gameObject
                        .AddComponent<FpgShootingDevelopmentPanel>();
                }

                shootingDevelopmentPanel.TryConfigure(this, out _);
            }
#endif
        }

        public bool TryComposePlayer(
            FpgPlayableCharacterSelection selection,
            out string error)
        {
            if (disposed || playerComposer == null
                || playableCharacterCatalog == null)
            {
                error = disposed
                    ? "Formal encounter host has been disposed."
                    : playerComposer == null
                        ? "Formal encounter host has no player composer."
                        : "Formal encounter host requires a playable character catalog.";
                return false;
            }

            if (!selection.TryValidate(out error))
            {
                return false;
            }

            if (!playableCharacterCatalog.TryResolve(
                    selection.CharacterId,
                    out FpgPlayableCharacterSelection catalogSelection,
                    out error))
            {
                return false;
            }

            if (!ReferenceEquals(
                    selection.CharacterDefinition,
                    catalogSelection.CharacterDefinition)
                || !ReferenceEquals(
                    selection.ThreeCProfile,
                    catalogSelection.ThreeCProfile)
                || !ReferenceEquals(
                    selection.CombatFeelProfile,
                    catalogSelection.CombatFeelProfile)
                || !ReferenceEquals(
                    selection.SelectionPreviewPrefab,
                    catalogSelection.SelectionPreviewPrefab))
            {
                error =
                    $"Playable character selection '{selection.CharacterId}' does not match the FormalRoom catalog entry.";
                return false;
            }

            FpgPlayableCharacterSelection canonicalSelection =
                catalogSelection.WithSecondaryMode(
                    selection.SelectedSecondaryTriggerMode);
            if (!canonicalSelection.TryValidate(out error))
            {
                error =
                    $"Playable character selection '{selection.CharacterId}' has an unsupported secondary mode: {error}";
                return false;
            }

            return playerComposer.TryCompose(canonicalSelection, out error);
        }

        public bool TryComposeDefaultPlayer(out string error)
        {
            if (playableCharacterCatalog == null)
            {
                error = "Formal encounter host requires a playable character catalog.";
                return false;
            }

            if (!playableCharacterCatalog.TryResolveDefault(
                    out FpgPlayableCharacterSelection selection,
                    out error))
            {
                return false;
            }

            return TryComposePlayer(selection, out error);
        }

        public bool TrySetRoomDefinition(
            FpgRoomDefinition roomDefinition,
            out string error)
        {
            if (disposed || encounterHost == null)
            {
                error = disposed
                    ? "Formal encounter host has been disposed."
                    : "Formal encounter host has no encounter runtime host.";
                return false;
            }

            return encounterHost.TrySetRoomDefinition(roomDefinition, out error);
        }

        public bool TryPrepareAndStart(out string error)
        {
            if (disposed || encounterHost == null)
            {
                error = disposed
                    ? "Formal encounter host has been disposed."
                    : "Formal encounter host has no encounter runtime host.";
                return false;
            }

            return encounterHost.TryPrepareAndStart(out error);
        }

        public bool TryPrepareAndStartSandbox(out string error)
        {
            if (disposed || encounterHost == null)
            {
                error = disposed
                    ? "Formal encounter host has been disposed."
                    : "Formal encounter host has no encounter runtime host.";
                return false;
            }

            return encounterHost.TryPrepareAndStartSandbox(out error);
        }

        public bool TryPrepareAndStart(
            in FpgEncounterStartRequest startRequest,
            out string error)
        {
            if (disposed || encounterHost == null)
            {
                error = nameof(encounterHost);
                return false;
            }

            return encounterHost.TryPrepareAndStart(startRequest, out error);
        }

        public bool TryCapturePlayerRunResources(
            out FpgPlayerRunResourceState state,
            out string error)
        {
            FpgFormalCombatRuntimeBundle runtime = CombatRuntime;
            D0CharacterDefinition definition = ActivePlayerDefinition;
            string characterId = definition == null
                ? null
                : definition.CharacterId;
            string weaponId = definition == null || definition.Weapon == null
                ? null
                : definition.Weapon.WeaponId;
            DomainResult result = FpgPlayerRunResourceTransfer.TryCapture(
                disposed || runtime == null || runtime.IsDisposed
                    ? null
                    : runtime.Player,
                characterId,
                weaponId,
                out state);
            error = result.IsSuccess
                ? string.Empty
                : result.RejectReason.ToString();
            return result.IsSuccess;
        }

        public bool TryGetShootingTuning(
            out FpgShootingTuningSnapshot snapshot,
            out string error)
        {
            snapshot = default(FpgShootingTuningSnapshot);
            if (disposed || playerComposer == null
                || !playerComposer.IsComposed)
            {
                error = disposed
                    ? "Formal encounter host has been disposed."
                    : "Shooting tuning requires a composed player.";
                return false;
            }

            FpgFormalPlayerTickDriver driver =
                playerComposer.PlayerTickDriver;
            if (driver != null && driver.HasShootingPreview)
            {
                snapshot = driver.ShootingPreview;
                error = string.Empty;
                return true;
            }

            return FpgShootingTuningSnapshot.TryCapture(
                playerComposer.ActiveSelection,
                out snapshot,
                out error);
        }

        public bool TryGetShootingDiagnostics(
            out FpgShootingDiagnosticsSnapshot snapshot,
            out string error)
        {
            snapshot = default(FpgShootingDiagnosticsSnapshot);
            if (disposed || playerComposer == null
                || !playerComposer.IsComposed
                || playerComposer.PlayerTickDriver == null)
            {
                error = disposed
                    ? "Formal encounter host has been disposed."
                    : "Shooting diagnostics require a composed player.";
                return false;
            }

            return playerComposer.PlayerTickDriver
                .TryGetShootingDiagnostics(out snapshot, out error);
        }

        public bool TryApplyShootingLivePreview(
            in FpgShootingTuningSnapshot snapshot,
            out string error)
        {
            error = string.Empty;
            if (disposed || playerComposer == null
                || !playerComposer.IsComposed
                || !snapshot.TryValidate(out error)
                || !snapshot.MatchesSelection(
                    playerComposer.ActiveSelection))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Shooting live preview does not match the active player."
                    : error;
                return false;
            }

            if (!TryGetShootingTuning(
                    out FpgShootingTuningSnapshot previousSnapshot,
                    out error))
            {
                return false;
            }

            if (!hasLastValidShootingSnapshot
                && !FpgShootingTuningSnapshot.TryCapture(
                    playerComposer.ActiveSelection,
                    out lastValidShootingSnapshot,
                    out error))
            {
                return false;
            }

            hasLastValidShootingSnapshot = true;
            FpgFormalPlayerTickDriver driver =
                playerComposer.PlayerTickDriver;
            FpgFormalPlayerPresentationBridge presentation =
                playerComposer.PresentationBridge;
            if (!driver.TryApplyShootingPreview(snapshot, out error))
            {
                return false;
            }

            if (!presentation.TryApplyShootingPreview(snapshot, out error))
            {
                string previewError = error;
                bool driverRestored = driver.TryApplyShootingPreview(
                    previousSnapshot,
                    out string driverRestoreError);
                bool presentationRestored = presentation
                    .TryApplyShootingPreview(
                        previousSnapshot,
                        out string presentationRestoreError);
                if (!driverRestored || !presentationRestored)
                {
                    error = previewError
                        + " Live-preview rollback failed: "
                        + (driverRestored
                            ? presentationRestoreError
                            : driverRestoreError);
                }
                else
                {
                    error = previewError;
                }

                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryApplyShootingPreviewAndRebuild(
            in FpgShootingTuningSnapshot snapshot,
            out string error)
        {
            if (disposed || playerComposer == null
                || !playerComposer.IsComposed)
            {
                error = disposed
                    ? "Formal encounter host has been disposed."
                    : "Shooting preview rebuild requires a composed player.";
                return false;
            }

            FpgPlayableCharacterSelection selection =
                playerComposer.ActiveSelection;
            if (!snapshot.TryValidate(out error)
                || !snapshot.MatchesSelection(selection)
                || !snapshot.TryCreateAttackQuerySettings(
                    playerComposer.CombatPortFactory
                        .AttackQueryTechnicalSettings,
                    out _,
                    out error)
                || !snapshot.TryCreateWeaponDefinition(out _, out error))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Shooting preview failed preflight validation."
                    : error;
                return false;
            }

            FpgShootingTuningSnapshot rollbackSnapshot;
            if (hasLastValidShootingSnapshot)
            {
                rollbackSnapshot = lastValidShootingSnapshot;
            }
            else if (!FpgShootingTuningSnapshot.TryCapture(
                         selection,
                         out rollbackSnapshot,
                         out error))
            {
                return false;
            }

            if (TryRebuildShootingPreview(
                    selection,
                    snapshot,
                    out error))
            {
                lastValidShootingSnapshot = snapshot;
                hasLastValidShootingSnapshot = true;
                return true;
            }

            string previewError = error;
            if (!TryRebuildShootingPreview(
                    selection,
                    rollbackSnapshot,
                    out string rollbackError))
            {
                error = previewError
                    + " Rollback also failed: "
                    + rollbackError;
                return false;
            }

            lastValidShootingSnapshot = rollbackSnapshot;
            hasLastValidShootingSnapshot = true;
            error = previewError;
            return false;
        }

        private bool TryRebuildShootingPreview(
            in FpgPlayableCharacterSelection selection,
            in FpgShootingTuningSnapshot snapshot,
            out string error)
        {
            error = string.Empty;
            StopAndClear();
            FpgFormalCombatPortFactory factory =
                playerComposer.CombatPortFactory;
            if (factory == null
                || !factory.TrySetShootingPreview(snapshot, out error)
                || !TryComposePlayer(selection, out error)
                || !TryApplyShootingLivePreview(snapshot, out error)
                || !TryValidate(out error)
                || !TryPrepareAndStart(out error)
                || !TryActivatePlayerPresentation(out error))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Shooting preview combat rebuild failed."
                    : error;
                return false;
            }

            SetPresentationEnabled(true);
            error = string.Empty;
            return true;
        }

        public void StopAndClear()
        {
            encounterHost?.StopAndClear();
            ClearPlayerComposition();
            SetPresentationEnabled(false);
        }


        public bool TryActivatePlayerPresentation(out string error)
        {
            if (disposed || playerComposer == null)
            {
                error = disposed
                    ? "Formal encounter host has been disposed."
                    : "Formal encounter host has no player composer.";
                return false;
            }

            return playerComposer.TryActivatePlayerPresentation(out error);
        }

        public void ClearPlayerComposition()
        {
            playerComposer?.ClearPlayerComposition();
        }

        public void SetPresentationEnabled(bool enabled)
        {
            if (cameraRoot != null)
            {
                cameraRoot.gameObject.SetActive(enabled);
            }

            if (presentationRoot != null)
            {
                presentationRoot.gameObject.SetActive(enabled);
            }
        }


        public bool TryValidateAuthoring(out string error)
        {
            if (disposed)
            {
                error = "Formal encounter host has been disposed.";
                return false;
            }

            if (actorsRoot == null || cameraRoot == null || presentationRoot == null)
            {
                error = "Formal encounter host requires explicit actor, camera and presentation roots.";
                return false;
            }

            if (worldCamera == null || roomArtSceneLoader == null)
            {
                error =
                    "Formal encounter host requires an explicit world Camera and Room Art Scene Loader.";
                return false;
            }

            if (encounterHost == null || encounterDirector == null
                || enemyEntityPool == null || combatantAnchorMap == null
                || playableCharacterCatalog == null || playerComposer == null)
            {
                error = "Formal encounter host requires explicit encounter host, director, pools, catalog and player composer references.";
                return false;
            }

            if (playerInputPort == null || physicsQueryPort == null
                || combatPortFactory == null)
            {
                error = "Formal encounter host requires explicit player driver, physics/query and combat factory ports.";
                return false;
            }

            if (actorsRoot.gameObject.scene != gameObject.scene
                || cameraRoot.gameObject.scene != gameObject.scene
                || presentationRoot.gameObject.scene != gameObject.scene
                || encounterHost.gameObject.scene != gameObject.scene
                || encounterDirector.gameObject.scene != gameObject.scene
                || enemyEntityPool.gameObject.scene != gameObject.scene
                || combatantAnchorMap.gameObject.scene != gameObject.scene
                || playerComposer.gameObject.scene != gameObject.scene
                || worldCamera.gameObject.scene != gameObject.scene
                || roomArtSceneLoader.gameObject.scene != gameObject.scene
                || playerInputPort.gameObject.scene != gameObject.scene
                || physicsQueryPort.gameObject.scene != gameObject.scene
                || combatPortFactory.gameObject.scene != gameObject.scene)
            {
                error = "Formal encounter host references must belong to its scene.";
                return false;
            }

            if (worldCamera.transform != cameraRoot
                && !worldCamera.transform.IsChildOf(cameraRoot))
            {
                error = "Formal world Camera must belong to the camera root.";
                return false;
            }

            if (!roomArtSceneLoader.TryValidateAuthoring(out error))
            {
                return false;
            }

            if (encounterHost.Director != encounterDirector)
            {
                error = "Formal scene host and encounter runtime host must share one director.";
                return false;
            }

            if (!(playerInputPort is IFpgFormalPlayerTickDriver)
                || !(physicsQueryPort is IFpgFormalCombatPortFactory)
                || !(combatPortFactory is IFpgFormalCombatPortFactory))
            {
                error = "Formal encounter host requires explicit player driver, physics/query and combat factory ports.";
                return false;
            }

            if (!playableCharacterCatalog.TryValidate(out error)
                || !playerComposer.TryValidateAuthoring(out error))
            {
                return false;
            }

            FpgFormalPlayerPresentationBridge presentationBridge =
                playerComposer.PresentationBridge;
            if (presentationBridge == null
                || presentationBridge.TargetCamera != worldCamera
                || presentationBridge.CameraRig != cameraRoot
                || presentationBridge.CameraFeedback == null
                || presentationBridge.CameraFeedback.TargetCamera
                    != worldCamera
                || presentationBridge.CameraFeedback.CameraRig
                    != cameraRoot)
            {
                error = "Formal host, player presentation and camera feedback must share one scene-owned Camera Rig and Camera.";
                return false;
            }

            if (!(playerInputPort is FpgFormalPlayerTickDriver concretePlayerDriver)
                || !(combatPortFactory is FpgFormalCombatPortFactory concreteFactory)
                || playerComposer.ActorsRoot != actorsRoot
                || playerComposer.PlayerTickDriver != concretePlayerDriver
                || playerComposer.CombatPortFactory != concreteFactory
                || playerComposer.EncounterDirector != encounterDirector
                || physicsQueryPort != combatPortFactory)
            {
                error = "Formal host ports and player composer must reference the same scene-owned runtime.";
                return false;
            }

            if (concretePlayerDriver.AimViewportSource == null)
            {
                error = "Formal host requires an explicit combat aim viewport source.";
                return false;
            }

            if (!string.IsNullOrEmpty(portBindingError))
            {
                error = portBindingError;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryValidateRuntime(out string error)
        {
            if (!TryValidateAuthoring(out error))
            {
                return false;
            }

            return playerComposer.TryValidateRuntime(out error);
        }

        public bool TryValidate(out string error)
        {
            return TryValidateRuntime(out error);
        }

        /// <summary>
        /// Passes a deterministic tick boundary to the room director. The
        /// caller owns the clock; this host does not create a second Update
        /// loop or advance the legacy BattleSession.
        /// </summary>
        public bool Advance(TickIndex tick, out string error)
        {
            if (!TryValidate(out error))
            {
                return false;
            }

            return encounterHost.Tick(tick, out error);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            FpgShootingTuningRuntimeRegistry.Unregister(this);
            ClearPlayerComposition();
            disposed = true;
            if (encounterDirector != null)
            {
                encounterDirector.LifecycleEvent -= HandleLifecycleEvent;
                encounterDirector.RoomCleared -= HandleRoomCleared;
                encounterDirector.Dispose();
            }
        }

        private void HandleLifecycleEvent(FpgEncounterLifecycleEvent lifecycleEvent)
        {
            LifecycleEvent?.Invoke(lifecycleEvent);
        }

        private void HandleRoomCleared(FpgRoomClearedEvent clearedEvent)
        {
            RoomCleared?.Invoke(clearedEvent);
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
