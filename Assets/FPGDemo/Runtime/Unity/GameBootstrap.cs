using System;
using System.Collections;
using System.Collections.Generic;
using FPG.Demo.Player;
using FPG.Demo.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Unity
{
    public enum BootstrapState
    {
        NotStarted,
        Loading,
        Running,
        Failed,
        WaitingForRoomSelection,
        WaitingForCharacterSelection,
        WaitingForSecondaryModeSelection
    }

    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField]
        private GameBootstrapConfig config;

        [SerializeField]
        private Camera bootCamera;

        [SerializeField]
        private Light bootLight;

        [SerializeField]
        private FpgRoomTransitionCurtain transitionCurtain;

        [Header("Character Selection")]
        [SerializeField]
        private FpgPlayableCharacterCatalog playableCharacterCatalog;

        [SerializeField]
        private FpgBootCharacterChoice[] characterChoices =
            Array.Empty<FpgBootCharacterChoice>();

        [SerializeField]
        private FpgBootSecondaryModeSelector secondaryModeSelector;

        [Header("Room Selection")]
        [SerializeField]
        private FpgBootRoomEntrance[] roomEntrances =
            Array.Empty<FpgBootRoomEntrance>();

        [SerializeField, Min(0.1f)]
        private float entranceShotDistance = 100f;

        [SerializeField]
        private LayerMask entranceLayerMask = ~0;

        private readonly FpgRunFlowController runFlowController =
            new FpgRunFlowController();
        private Coroutine roomTransitionCoroutine;

        public GameBootstrapConfig Config => config;
        public FpgPlayableCharacterCatalog PlayableCharacterCatalog =>
            playableCharacterCatalog;
        public FpgBootSecondaryModeSelector SecondaryModeSelector =>
            secondaryModeSelector;
        public BootstrapState State { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public FpgPlayableCharacterSelection SelectedPlayerSelection { get; private set; }

        [Obsolete("Use SelectedPlayerSelection instead.")]
        public FpgPlayableCharacterSelection SelectedCharacter =>
            SelectedPlayerSelection;
        public FpgRoomDefinition SelectedRoom { get; private set; }
        public FpgEncounterHost ActiveFormalHost { get; private set; }
        public FpgFormalEncounterHost ActiveFormalSceneHost { get; private set; }
        public FpgRoomEncounterDirector ActiveEncounterDirector { get; private set; }
        public FpgRunFlowController RunFlowController => runFlowController;

        public IReadOnlyList<FpgBootCharacterChoice> CharacterChoices =>
            characterChoices ?? Array.Empty<FpgBootCharacterChoice>();

        public IReadOnlyList<FpgBootRoomEntrance> RoomEntrances =>
            roomEntrances ?? Array.Empty<FpgBootRoomEntrance>();

        public bool TryValidateConfiguration(out string error)
        {
            const float expectedFixedDeltaTime =
                1f / FPG.Demo.Skills.FpgSkillRuntimeConstants.TickRate;
            if (FPG.Demo.Core.GameplayClock.DefaultTickRate
                    != FPG.Demo.Skills.FpgSkillRuntimeConstants.TickRate
                || Mathf.Abs(Time.fixedDeltaTime - expectedFixedDeltaTime)
                    > 0.000001f)
            {
                error =
                    "Formal runtime requires GameplayClock, skill timelines, "
                    + "and Project Fixed Timestep to use 60Hz.";
                return false;
            }
            if (config == null)
            {
                error = "GameBootstrapConfig is not assigned.";
                return false;
            }

            if (bootCamera == null)
            {
                error = "Boot Camera is not assigned.";
                return false;
            }

            if (bootLight == null)
            {
                error = "Boot Directional Light is not assigned.";
                return false;
            }

            if (transitionCurtain == null)
            {
                error = "Boot room transition curtain is not assigned.";
                return false;
            }

            if (!transitionCurtain.TryValidateAuthoring(out error))
            {
                error = "Boot room transition curtain is invalid: " + error;
                return false;
            }

            if (!config.TryValidate(out error))
            {
                return false;
            }

            if (config.ExitRoomRefreshRule == null
                || !config.ExitRoomRefreshRule.TryValidate(out error))
            {
                error = "GameBootstrap requires a valid exit room refresh rule: "
                    + error;
                return false;
            }

            if (playableCharacterCatalog == null)
            {
                error = "Playable character catalog is not assigned.";
                return false;
            }

            if (!playableCharacterCatalog.TryValidate(out error))
            {
                error = $"Playable character catalog is invalid: {error}";
                return false;
            }

            if (config.RequireCharacterSelection
                && !TryValidateCharacterChoices(out error))
            {
                return false;
            }

            if (config.RequireCharacterSelection
                && RequiresSecondaryModeSelector()
                && !TryValidateSecondaryModeSelector(out error))
            {
                return false;
            }

            if (config.RequireEntranceSelection
                && !TryValidateRoomEntrances(out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TrySelectCharacter(
            FpgBootCharacterChoice choice,
            out string error)
        {
            error = string.Empty;
            if (State != BootstrapState.WaitingForCharacterSelection)
            {
                error = "Boot is not waiting for a character choice shot.";
                return false;
            }

            if (choice == null || !ContainsCharacterChoice(choice))
            {
                error = "The hit Boot character choice is not configured.";
                return false;
            }

            if (!choice.IsSelectable)
            {
                error = "The hit Boot character choice is not selectable.";
                return false;
            }

            if (!choice.TryResolveSelection(
                    playableCharacterCatalog,
                    out FpgPlayableCharacterSelection selection,
                    out error))
            {
                return false;
            }

            SelectedPlayerSelection = selection;
            choice.MarkSelected();
            SetCharacterChoicesSelectable(false);
            if (selection.SupportedSecondaryModes.Count > 1)
            {
                return TryBeginSecondaryModeSelection(out error);
            }

            return TryContinueAfterCharacterSelection(out error);
        }

        public bool TrySelectSecondaryMode(
            SecondaryTriggerMode mode,
            out string error)
        {
            error = string.Empty;
            if (State != BootstrapState.WaitingForSecondaryModeSelection)
            {
                error = "Boot is not waiting for a secondary-mode choice.";
                return false;
            }

            if (!SelectedPlayerSelection.SupportsSecondaryMode(mode))
            {
                error =
                    $"Secondary mode '{mode}' is not supported by selected character "
                    + $"'{SelectedPlayerSelection.CharacterId}'.";
                return false;
            }

            FpgPlayableCharacterSelection updatedSelection =
                SelectedPlayerSelection.WithSecondaryMode(mode);
            if (!updatedSelection.TryValidate(out error))
            {
                error = $"Selected secondary mode is invalid: {error}";
                return false;
            }

            SelectedPlayerSelection = updatedSelection;
            secondaryModeSelector?.Hide();
            return TryContinueAfterCharacterSelection(out error);
        }

        public bool TryEnterRoom(
            FpgBootRoomEntrance entrance,
            out string error)
        {
            error = string.Empty;
            if (State != BootstrapState.WaitingForRoomSelection)
            {
                error = "Boot is not waiting for a room entrance shot.";
                return false;
            }

            if (entrance == null || !ContainsEntrance(entrance))
            {
                error = "The hit Boot entrance is not configured.";
                return false;
            }

            if (!entrance.IsSelectable)
            {
                error = "The hit Boot entrance is not selectable.";
                return false;
            }

            if (!SelectedPlayerSelection.TryValidate(out error))
            {
                error = $"Selected playable character is invalid: {error}";
                return false;
            }

            if (!entrance.TryValidate(out error))
            {
                return false;
            }

            SelectedRoom = entrance.RoomDefinition;
            entrance.MarkSelected();
            SetRoomEntrancesSelectable(false);
            SetRoomEntrancesVisible(false);
            return TryBeginLoadingRoom(SelectedRoom, out error);
        }

        private IEnumerator Start()
        {
            secondaryModeSelector?.Hide();
            if (!TryValidateConfiguration(out string validationError))
            {
                Fail(validationError);
                yield break;
            }

            ApplyFrameRateStrategy();
            SetCharacterChoicesSelectable(false);
            SetRoomEntrancesSelectable(false);
            SetRoomEntrancesVisible(false);

            if (config.RequireCharacterSelection)
            {
                SetCharacterChoicesSelectable(true);
                State = BootstrapState.WaitingForCharacterSelection;
                yield break;
            }

            if (!playableCharacterCatalog.TryResolveDefault(
                    out FpgPlayableCharacterSelection defaultSelection,
                    out string selectionError))
            {
                Fail($"Default playable character is invalid: {selectionError}");
                yield break;
            }

            SelectedPlayerSelection = defaultSelection;
            if (!TryContinueAfterCharacterSelection(out string flowError))
            {
                Fail(flowError);
            }
        }

        private void Update()
        {
            if (State != BootstrapState.WaitingForCharacterSelection
                && State != BootstrapState.WaitingForRoomSelection)
            {
                return;
            }

            if (Mouse.current == null
                || !Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            Vector2 pointerPosition = Mouse.current.position.ReadValue();
            Ray ray = bootCamera.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    entranceShotDistance,
                    entranceLayerMask,
                    QueryTriggerInteraction.Collide))
            {
                return;
            }

            if (State == BootstrapState.WaitingForCharacterSelection)
            {
                FpgBootCharacterChoice choice =
                    hit.collider.GetComponentInParent<FpgBootCharacterChoice>();
                if (choice == null || !choice.IsSelectable
                    || !choice.OwnsCollider(hit.collider))
                {
                    return;
                }

                if (!TrySelectCharacter(choice, out string characterError))
                {
                    Fail(characterError);
                }

                return;
            }

            FpgBootRoomEntrance entrance =
                hit.collider.GetComponentInParent<FpgBootRoomEntrance>();
            if (entrance == null || !entrance.IsSelectable
                || !entrance.OwnsCollider(hit.collider))
            {
                return;
            }

            if (!TryEnterRoom(entrance, out string roomError))
            {
                Fail(roomError);
            }
        }

        private bool TryContinueAfterCharacterSelection(out string error)
        {
            if (!SelectedPlayerSelection.TryValidate(out error))
            {
                error = $"Selected playable character is invalid: {error}";
                return false;
            }

            secondaryModeSelector?.Hide();
            SetCharacterChoicesSelectable(false);
            if (config.RequireEntranceSelection)
            {
                SetRoomEntrancesVisible(true);
                SetRoomEntrancesSelectable(true);
                State = BootstrapState.WaitingForRoomSelection;
                error = string.Empty;
                return true;
            }

            if (!config.LoadRoomOnStart)
            {
                State = BootstrapState.Running;
                error = string.Empty;
                return true;
            }

            return TryBeginLoadingRoom(GetDefaultRoomDefinition(), out error);
        }

        private bool TryBeginSecondaryModeSelection(out string error)
        {
            IReadOnlyList<SecondaryTriggerMode> supportedModes =
                SelectedPlayerSelection.SupportedSecondaryModes;
            if (supportedModes == null || supportedModes.Count <= 1)
            {
                return TryContinueAfterCharacterSelection(out error);
            }

            if (secondaryModeSelector == null)
            {
                error =
                    "Selected character supports multiple secondary modes, but "
                    + "the Boot secondary-mode selector is not assigned.";
                return false;
            }

            SetRoomEntrancesSelectable(false);
            SetRoomEntrancesVisible(false);
            if (!secondaryModeSelector.TryShow(
                    SelectedPlayerSelection,
                    HandleSecondaryModeSelected,
                    out error))
            {
                error = "Boot secondary-mode selector is invalid: " + error;
                return false;
            }

            State = BootstrapState.WaitingForSecondaryModeSelection;
            error = string.Empty;
            return true;
        }

        private void HandleSecondaryModeSelected(SecondaryTriggerMode mode)
        {
            if (!TrySelectSecondaryMode(mode, out string error))
            {
                Fail(error);
            }
        }

        private bool TryBeginLoadingRoom(
            FpgRoomDefinition roomDefinition,
            out string error)
        {
            if (!SelectedPlayerSelection.TryValidate(out error))
            {
                error = $"Selected playable character is invalid: {error}";
                return false;
            }

            BootstrapSelectionSnapshot snapshot =
                new BootstrapSelectionSnapshot(
                    SelectedPlayerSelection,
                    roomDefinition);
            secondaryModeSelector?.Hide();
            State = BootstrapState.Loading;
            StartCoroutine(LoadRoomScene(snapshot));
            error = string.Empty;
            return true;
        }

        private IEnumerator LoadRoomScene(BootstrapSelectionSnapshot snapshot)
        {
            yield return transitionCurtain.FadeToOpaque();

            string sceneName = config.RoomSceneName;
            Scene roomScene = SceneManager.GetSceneByName(sceneName);
            bool loadedByBootstrap = false;
            if (!roomScene.IsValid() || !roomScene.isLoaded)
            {
                AsyncOperation loadOperation;
                try
                {
                    loadOperation = SceneManager.LoadSceneAsync(
                        sceneName,
                        LoadSceneMode.Additive);
                }
                catch (Exception exception)
                {
                    FailRoomBootstrap(
                        $"Unable to start loading scene '{sceneName}': {exception.Message}",
                        roomScene,
                        loadedByBootstrap: false);
                    yield break;
                }

                if (loadOperation == null)
                {
                    FailRoomBootstrap(
                        $"Unable to start loading scene '{sceneName}'.",
                        roomScene,
                        loadedByBootstrap: false);
                    yield break;
                }

                loadedByBootstrap = true;
                yield return loadOperation;
                roomScene = SceneManager.GetSceneByName(sceneName);
            }

            yield return CompleteBootstrap(
                roomScene,
                snapshot,
                loadedByBootstrap);
        }

        private IEnumerator CompleteBootstrap(
            Scene roomScene,
            BootstrapSelectionSnapshot snapshot,
            bool loadedByBootstrap)
        {
            if (!roomScene.IsValid() || !roomScene.isLoaded)
            {
                FailRoomBootstrap(
                    $"Scene '{config.RoomSceneName}' was not loaded successfully.",
                    roomScene,
                    loadedByBootstrap);
                yield break;
            }

            if (!TryGetSingleComponent(
                    roomScene,
                    out FpgFormalEncounterHost formalSceneHost,
                    out string formalHostError))
            {
                FailRoomBootstrap(
                    formalHostError,
                    roomScene,
                    loadedByBootstrap);
                yield break;
            }

            if (!SceneManager.SetActiveScene(roomScene))
            {
                FailRoomBootstrap(
                    $"Unable to make scene '{config.RoomSceneName}' active.",
                    roomScene,
                    loadedByBootstrap);
                yield break;
            }

            formalSceneHost.SetPresentationEnabled(false);
            if (snapshot.RoomDefinition != null
                && !formalSceneHost.TrySetRoomDefinition(
                    snapshot.RoomDefinition,
                    out string roomError))
            {
                FailRoomBootstrap(
                    $"Formal room selection is invalid: {roomError}",
                    roomScene,
                    loadedByBootstrap);
                yield break;
            }

            if (!formalSceneHost.TryComposePlayer(
                    snapshot.CharacterSelection,
                    out string playerError))
            {
                FailRoomBootstrap(
                    $"Formal room player composition is invalid: {playerError}",
                    roomScene,
                    loadedByBootstrap);
                yield break;
            }

            if (!formalSceneHost.TryValidate(out string sceneHostError))
            {
                FailRoomBootstrap(
                    $"Formal room scene host is invalid: {sceneHostError}",
                    roomScene,
                    loadedByBootstrap);
                yield break;
            }

            bool artLoadSucceeded = false;
            string artLoadError = string.Empty;
            yield return formalSceneHost.RoomArtSceneLoader.LoadAsync(
                formalSceneHost.RoomDefinition,
                formalSceneHost.WorldCamera,
                formalSceneHost.AimViewportSource,
                (succeeded, error) =>
                {
                    artLoadSucceeded = succeeded;
                    artLoadError = error;
                });
            if (!artLoadSucceeded)
            {
                FailRoomBootstrap(
                    "Formal room Art Scene failed to load: " + artLoadError,
                    roomScene,
                    loadedByBootstrap);
                yield break;
            }

            if (!formalSceneHost.TryPrepareAndStart(out string encounterError))
            {
                FailRoomBootstrap(
                    $"Formal room encounter failed to start: {encounterError}",
                    roomScene,
                    loadedByBootstrap);
                yield break;
            }

            if (!formalSceneHost.TryActivatePlayerPresentation(
                    out string presentationError))
            {
                FailRoomBootstrap(
                    $"Formal player presentation failed to activate: {presentationError}",
                    roomScene,
                    loadedByBootstrap);
                yield break;
            }

            SelectedPlayerSelection = snapshot.CharacterSelection;
            SelectedRoom = formalSceneHost.RoomDefinition;
            ActiveFormalHost = formalSceneHost.EncounterHost;
            ActiveFormalSceneHost = formalSceneHost;
            ActiveEncounterDirector = formalSceneHost.EncounterDirector;
            if (!runFlowController.TryBind(
                    this,
                    formalSceneHost,
                    out string runFlowError))
            {
                FailRoomBootstrap(
                    "Formal room run flow failed to bind: " + runFlowError,
                    roomScene,
                    loadedByBootstrap);
                yield break;
            }

            formalSceneHost.SetPresentationEnabled(true);
            DisableBootPresentation();
            State = BootstrapState.Running;
            LastError = string.Empty;
            yield return transitionCurtain.FadeToTransparent();

            if (config.DevelopmentDiagnosticsEnabled)
            {
                string roomId = SelectedRoom == null
                    ? "<scene-default>"
                    : SelectedRoom.RoomId;
                Debug.Log(
                    $"[{nameof(GameBootstrap)}] Character '{SelectedPlayerSelection.CharacterId}' entered formal room '{roomId}'.",
                    this);
            }
        }

        internal void HandleRunFlowRoomCleared(
            FpgRunFlowController sender,
            FpgRoomClearedEvent clearedEvent)
        {
            if (!ReferenceEquals(sender, runFlowController)
                || State != BootstrapState.Running
                || SelectedRoom == null
                || ActiveEncounterDirector == null
                || !clearedEvent.RunContext.IsValid
                || !string.Equals(
                    clearedEvent.RoomId,
                    SelectedRoom.RoomId,
                    StringComparison.Ordinal))
            {
                BeginRunFlowFault("Room clear arrived without an active run flow.");
                return;
            }

            IReadOnlyList<FpgRoomExitSlot> slots = SelectedRoom.ExitSlots;
            string[] exitIds = new string[slots.Count];
            for (int index = 0; index < slots.Count; index++)
            {
                exitIds[index] = slots[index].MarkerId;
            }

            FpgExitRefreshContext context = new FpgExitRefreshContext(
                clearedEvent.RunContext,
                clearedEvent.RoomId);
            if (!config.ExitRoomRefreshRule.TryCreateOffers(
                    context,
                    exitIds,
                    out FpgExitOffer[] offers,
                    out string error)
                || !ActiveEncounterDirector.TryRevealExits(offers, out error)
                || !runFlowController.TryMarkAwaitingExit(out error))
            {
                BeginRunFlowFault("Exit refresh failed: " + error);
            }
        }

        internal void HandleRunFlowExitSelected(
            FpgRunFlowController sender,
            FpgExitSelectionEvent selectionEvent)
        {
            if (!ReferenceEquals(sender, runFlowController)
                || selectionEvent.Offer == null
                || !selectionEvent.Offer.IsValid
                || ActiveFormalSceneHost == null
                || ActiveFormalSceneHost.CombatRuntime == null
                || ActiveFormalSceneHost.ActivePlayerDefinition == null
                || !runFlowController.TryBeginTransition(out string error))
            {
                BeginRunFlowFault("Exit selection is invalid.");
                return;
            }

            ActiveEncounterDirector?.DeactivateAndClearExits();
            if (!ActiveFormalSceneHost.TryCapturePlayerRunResources(
                out FpgPlayerRunResourceState resources,
                out string captureError))
            {
                BeginRunFlowFault(
                    "Player resources could not be captured: "
                    + captureError);
                return;
            }

            FpgEncounterRunContext current =
                ActiveFormalSceneHost.EncounterHost.RunContext;
            if (current.Depth == int.MaxValue
                || current.RoomVisitOrdinal == int.MaxValue)
            {
                BeginRunFlowFault("Run depth or room visit ordinal is exhausted.");
                return;
            }

            FpgEncounterRunContext next = new FpgEncounterRunContext(
                current.RunSeed,
                current.RegionId,
                current.Depth + 1,
                current.DifficultyMultiplierBasisPoints,
                current.RoomVisitOrdinal + 1);
            roomTransitionCoroutine = StartCoroutine(
                TransitionToRoom(
                    selectionEvent.Offer,
                    next,
                    resources));
        }

        internal void HandleRunFlowFailed(
            FpgRunFlowController sender,
            FpgEncounterFailureReason reason,
            string message)
        {
            if (!ReferenceEquals(sender, runFlowController))
            {
                return;
            }

            string error = string.IsNullOrWhiteSpace(message)
                ? reason.ToString()
                : message;
            bool canRetainForRestart = State == BootstrapState.Running
                && ActiveFormalSceneHost != null
                && ActiveEncounterDirector != null
                && roomTransitionCoroutine == null
                && (runFlowController.State == FpgRunFlowState.Running
                    || runFlowController.State == FpgRunFlowState.AwaitingExit);
            if (canRetainForRestart)
            {
                LastError = error;
                runFlowController.SetRecoverableFault(LastError);
                ActiveEncounterDirector.DeactivateAndClearExits();
                Debug.LogError(
                    "[" + nameof(GameBootstrap) + "] " + LastError
                    + " Formal room retained for restart.",
                    this);
                return;
            }

            BeginRunFlowFault(error);
        }

        internal void HandleRunFlowRestarted(FpgRunFlowController sender)
        {
            if (!ReferenceEquals(sender, runFlowController)
                || State != BootstrapState.Running
                || ActiveFormalSceneHost == null
                || ActiveEncounterDirector == null)
            {
                return;
            }

            LastError = string.Empty;
        }

        private IEnumerator TransitionToRoom(
            FpgExitOffer offer,
            FpgEncounterRunContext nextContext,
            FpgPlayerRunResourceState resources)
        {
            FpgFormalEncounterHost formalHost = ActiveFormalSceneHost;
            FpgPlayableCharacterSelection playerSelection =
                SelectedPlayerSelection;
            yield return transitionCurtain.FadeToOpaque();

            if (formalHost == null || offer == null || !offer.IsValid)
            {
                yield return FailRetainedRoomTransitionAsync(
                    "Room transition lost its retained formal host.",
                    formalHost);
                yield break;
            }

            formalHost.StopAndClear();
            yield return Application.isBatchMode
                ? null
                : new WaitForEndOfFrame();

            Scene formalScene = formalHost.gameObject.scene;
            bool artUnloadSucceeded = false;
            string artUnloadError = string.Empty;
            yield return formalHost.RoomArtSceneLoader.UnloadActiveAsync(
                formalScene,
                (succeeded, unloadError) =>
                {
                    artUnloadSucceeded = succeeded;
                    artUnloadError = unloadError;
                });
            if (!artUnloadSucceeded)
            {
                yield return FailRetainedRoomTransitionAsync(
                    "Previous room Art Scene failed to unload: "
                    + artUnloadError,
                    formalHost);
                yield break;
            }

            if (!formalHost.TrySetRoomDefinition(
                    offer.DestinationRoom,
                    out string error)
                || !formalHost.TryComposePlayer(playerSelection, out error)
                || !formalHost.TryValidate(out error))
            {
                yield return FailRetainedRoomTransitionAsync(
                    "Next room composition failed: " + error,
                    formalHost);
                yield break;
            }

            bool artLoadSucceeded = false;
            string artLoadError = string.Empty;
            yield return formalHost.RoomArtSceneLoader.LoadAsync(
                offer.DestinationRoom,
                formalHost.WorldCamera,
                formalHost.AimViewportSource,
                (succeeded, loadError) =>
                {
                    artLoadSucceeded = succeeded;
                    artLoadError = loadError;
                });
            if (!artLoadSucceeded)
            {
                yield return FailRetainedRoomTransitionAsync(
                    "Next room Art Scene failed to load: " + artLoadError,
                    formalHost);
                yield break;
            }

            FpgEncounterStartRequest request =
                new FpgEncounterStartRequest(
                    offer.DestinationRoom,
                    nextContext,
                    resources);
            if (!formalHost.TryPrepareAndStart(request, out error)
                || !formalHost.TryActivatePlayerPresentation(out error)
                || !runFlowController.TryBind(this, formalHost, out error))
            {
                yield return FailRetainedRoomTransitionAsync(
                    "Next room startup failed: " + error,
                    formalHost);
                yield break;
            }

            formalHost.SetPresentationEnabled(true);
            SelectedPlayerSelection = playerSelection;
            SelectedRoom = offer.DestinationRoom;
            ActiveFormalHost = formalHost.EncounterHost;
            ActiveFormalSceneHost = formalHost;
            ActiveEncounterDirector = formalHost.EncounterDirector;
            LastError = string.Empty;
            State = BootstrapState.Running;
            yield return transitionCurtain.FadeToTransparent();
            roomTransitionCoroutine = null;
        }

        private void BeginRunFlowFault(string error)
        {
            LastError = string.IsNullOrWhiteSpace(error)
                ? "Formal room run flow failed."
                : error;
            runFlowController.SetFault(LastError);
            ActiveEncounterDirector?.DeactivateAndClearExits();
            if (roomTransitionCoroutine == null)
            {
                roomTransitionCoroutine =
                    StartCoroutine(FailRunFlowNextFrame());
            }
        }

        private IEnumerator FailRunFlowNextFrame()
        {
            yield return null;
            yield return FailRetainedRoomTransitionAsync(
                LastError,
                ActiveFormalSceneHost);
        }

        private IEnumerator FailRetainedRoomTransitionAsync(
            string error,
            FpgFormalEncounterHost formalHost)
        {
            LastError = string.IsNullOrWhiteSpace(error)
                ? "Formal room transition failed."
                : error;
            Debug.LogError("[" + nameof(GameBootstrap) + "] " + LastError, this);
            runFlowController.SetFault(LastError);
            yield return transitionCurtain.FadeToOpaque();
            formalHost?.StopAndClear();

            if (formalHost != null
                && formalHost.RoomArtSceneLoader != null
                && formalHost.RoomArtSceneLoader.HasActiveArtScene)
            {
                bool unloadSucceeded = false;
                string unloadError = string.Empty;
                yield return formalHost.RoomArtSceneLoader.UnloadActiveAsync(
                    formalHost.gameObject.scene,
                    (succeeded, failure) =>
                    {
                        unloadSucceeded = succeeded;
                        unloadError = failure;
                    });
                if (!unloadSucceeded && !string.IsNullOrWhiteSpace(unloadError))
                {
                    LastError += " Art Scene cleanup failed: " + unloadError;
                }
            }

            Scene bootScene = gameObject.scene;
            if (bootScene.IsValid() && bootScene.isLoaded)
            {
                SceneManager.SetActiveScene(bootScene);
            }

            ActiveFormalHost = null;
            ActiveFormalSceneHost = null;
            ActiveEncounterDirector = null;
            SelectedRoom = null;
            State = BootstrapState.Loading;
            RestoreBootPresentationAfterFailure();
            yield return transitionCurtain.FadeToTransparent();
            RestoreBootInteractionAfterFailure();
            roomTransitionCoroutine = null;
        }

        private void ApplyFrameRateStrategy()
        {
            QualitySettings.vSyncCount = config.VSyncCount;
            Application.targetFrameRate = config.FrameRateMode == FrameRateMode.Locked
                ? config.LockedFramesPerSecond
                : -1;
        }

        private void DisableBootPresentation()
        {
            secondaryModeSelector?.Hide();
            SetCharacterChoicesSelectable(false);
            SetRoomEntrancesSelectable(false);
            SetRoomEntrancesVisible(false);
            bootCamera.enabled = false;
            AudioListener bootListener = bootCamera.GetComponent<AudioListener>();
            if (bootListener != null)
            {
                bootListener.enabled = false;
            }

            bootLight.enabled = false;
        }

        private void FailRoomBootstrap(
            string error,
            Scene roomScene,
            bool loadedByBootstrap)
        {
            LastError = string.IsNullOrWhiteSpace(error)
                ? "Formal room bootstrap failed."
                : error;
            State = BootstrapState.Loading;
            Debug.LogError($"[{nameof(GameBootstrap)}] {LastError}", this);
            runFlowController.SetFault(LastError);
            roomTransitionCoroutine = null;

            RollbackRoomRuntime(roomScene);

            Scene bootScene = gameObject.scene;
            if (bootScene.IsValid() && bootScene.isLoaded
                && SceneManager.GetActiveScene() != bootScene
                && !SceneManager.SetActiveScene(bootScene))
            {
                Debug.LogError(
                    $"[{nameof(GameBootstrap)}] Failed to restore Boot as the active scene.",
                    this);
            }

            ActiveFormalHost = null;
            ActiveFormalSceneHost = null;
            ActiveEncounterDirector = null;
            SelectedRoom = null;

            StartCoroutine(
                RecoverFailedRoomBootstrap(
                    roomScene,
                    loadedByBootstrap));
        }

        private static void RollbackRoomRuntime(Scene roomScene)
        {
            if (!roomScene.IsValid() || !roomScene.isLoaded)
            {
                return;
            }

            GameObject[] roots = roomScene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                FpgFormalEncounterHost[] formalHosts =
                    roots[rootIndex].GetComponentsInChildren<FpgFormalEncounterHost>(true);
                for (int hostIndex = 0;
                    hostIndex < formalHosts.Length;
                    hostIndex++)
                {
                    formalHosts[hostIndex].StopAndClear();
                }
            }
        }

        private IEnumerator RecoverFailedRoomBootstrap(
            Scene roomScene,
            bool loadedByBootstrap)
        {
            FpgRoomArtSceneLoader artLoader = null;
            if (roomScene.IsValid() && roomScene.isLoaded)
            {
                TryGetSingleComponent(
                    roomScene,
                    out artLoader,
                    out _);
            }

            if (artLoader != null && artLoader.HasActiveArtScene)
            {
                bool cleanupSucceeded = false;
                string cleanupError = string.Empty;
                yield return artLoader.UnloadActiveAsync(
                    roomScene,
                    (succeeded, failure) =>
                    {
                        cleanupSucceeded = succeeded;
                        cleanupError = failure;
                    });
                if (!cleanupSucceeded)
                {
                    Debug.LogError(
                        $"[{nameof(GameBootstrap)}] Failed to clean up the Art Scene: {cleanupError}",
                        this);
                }
            }

            Scene bootScene = gameObject.scene;
            if (bootScene.IsValid() && bootScene.isLoaded
                && SceneManager.GetActiveScene() != bootScene
                && !SceneManager.SetActiveScene(bootScene))
            {
                Debug.LogError(
                    $"[{nameof(GameBootstrap)}] Failed to restore Boot as the active scene.",
                    this);
            }

            if (loadedByBootstrap && roomScene.IsValid() && roomScene.isLoaded
                && (!bootScene.IsValid()
                    || roomScene.handle != bootScene.handle))
            {
                AsyncOperation unloadOperation =
                    SceneManager.UnloadSceneAsync(roomScene);
                if (unloadOperation == null)
                {
                    Debug.LogError(
                        $"[{nameof(GameBootstrap)}] Failed to start unloading room scene '{roomScene.name}'.",
                        this);
                }
                else
                {
                    yield return unloadOperation;
                }
            }

            RestoreBootPresentationAfterFailure();
            yield return transitionCurtain.FadeToTransparent();
            RestoreBootInteractionAfterFailure();
        }

        private void RestoreBootPresentationAfterFailure()
        {
            secondaryModeSelector?.Hide();
            bootCamera.enabled = true;
            AudioListener bootListener = bootCamera.GetComponent<AudioListener>();
            if (bootListener != null)
            {
                bootListener.enabled = true;
            }

            bootLight.enabled = true;
            SetCharacterChoicesSelectable(false);
            SetRoomEntrancesSelectable(false);
            SetRoomEntrancesVisible(false);
            State = BootstrapState.Loading;
        }

        private void RestoreBootInteractionAfterFailure()
        {
            RestoreBootPresentationAfterFailure();

            if (SelectedPlayerSelection.TryValidate(out _)
                && config != null
                && config.RequireEntranceSelection)
            {
                SetRoomEntrancesVisible(true);
                SetRoomEntrancesSelectable(true);
                State = BootstrapState.WaitingForRoomSelection;
                return;
            }

            SelectedPlayerSelection = default(FpgPlayableCharacterSelection);
            SetCharacterChoicesSelectable(true);
            State = BootstrapState.WaitingForCharacterSelection;
        }


        private void Fail(string error)
        {
            secondaryModeSelector?.Hide();
            LastError = string.IsNullOrWhiteSpace(error)
                ? "Game bootstrap failed."
                : error;
            State = BootstrapState.Failed;
            Debug.LogError($"[{nameof(GameBootstrap)}] {LastError}", this);
        }

        private bool TryValidateCharacterChoices(out string error)
        {
            FpgBootCharacterChoice[] choices =
                characterChoices ?? Array.Empty<FpgBootCharacterChoice>();
            if (choices.Length == 0)
            {
                error = "Boot requires at least one character choice.";
                return false;
            }

            HashSet<string> characterIds =
                new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < choices.Length; index++)
            {
                FpgBootCharacterChoice choice = choices[index];
                if (choice == null || choice.gameObject.scene != gameObject.scene)
                {
                    error =
                        $"Boot character choice {index} is missing or belongs to another scene.";
                    return false;
                }

                if (choice.PreviewRoot != null
                    && (choice.PreviewRoot.transform == transform
                        || transform.IsChildOf(choice.PreviewRoot.transform)))
                {
                    error =
                        $"Boot character choice {index} preview root cannot contain GameBootstrap.";
                    return false;
                }

                if (!choice.TryResolveSelection(
                        playableCharacterCatalog,
                        out FpgPlayableCharacterSelection selection,
                        out error))
                {
                    error = $"Boot character choice {index} is invalid: {error}";
                    return false;
                }

                if (!characterIds.Add(selection.CharacterId))
                {
                    error =
                        $"Boot character choices contain duplicate character ID '{selection.CharacterId}'.";
                    return false;
                }
            }

            if (characterIds.Count != playableCharacterCatalog.Count)
            {
                error =
                    "Boot character choices must represent every playable character catalog entry exactly once.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool RequiresSecondaryModeSelector()
        {
            FpgBootCharacterChoice[] choices =
                characterChoices ?? Array.Empty<FpgBootCharacterChoice>();
            for (int index = 0; index < choices.Length; index++)
            {
                FpgBootCharacterChoice choice = choices[index];
                if (choice != null
                    && choice.TryResolveSelection(
                        playableCharacterCatalog,
                        out FpgPlayableCharacterSelection selection,
                        out _)
                    && selection.SupportedSecondaryModes.Count > 1)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryValidateSecondaryModeSelector(out string error)
        {
            if (secondaryModeSelector == null)
            {
                error =
                    "Boot requires a secondary-mode selector when a selectable "
                    + "character supports multiple modes.";
                return false;
            }

            if (secondaryModeSelector.gameObject.scene != gameObject.scene)
            {
                error =
                    "Boot secondary-mode selector must belong to the Boot scene.";
                return false;
            }

            if (!secondaryModeSelector.TryValidateAuthoring(out error))
            {
                error = "Boot secondary-mode selector is invalid: " + error;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateRoomEntrances(out string error)
        {
            FpgBootRoomEntrance[] entrances =
                roomEntrances ?? Array.Empty<FpgBootRoomEntrance>();
            if (entrances.Length == 0)
            {
                error = "Boot requires at least one room entrance.";
                return false;
            }

            for (int index = 0; index < entrances.Length; index++)
            {
                FpgBootRoomEntrance entrance = entrances[index];
                if (entrance == null
                    || entrance.gameObject.scene != gameObject.scene)
                {
                    error =
                        $"Boot room entrance {index} is missing or belongs to another scene.";
                    return false;
                }

                if (entrance.transform == transform
                    || transform.IsChildOf(entrance.transform))
                {
                    error =
                        $"Boot room entrance {index} cannot contain GameBootstrap.";
                    return false;
                }

                if (!entrance.TryValidate(out error))
                {
                    error = $"Boot room entrance {index} is invalid: {error}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private FpgRoomDefinition GetDefaultRoomDefinition()
        {
            FpgBootRoomEntrance[] entrances =
                roomEntrances ?? Array.Empty<FpgBootRoomEntrance>();
            return entrances.Length == 0 || entrances[0] == null
                ? null
                : entrances[0].RoomDefinition;
        }

        private bool ContainsCharacterChoice(FpgBootCharacterChoice candidate)
        {
            FpgBootCharacterChoice[] choices =
                characterChoices ?? Array.Empty<FpgBootCharacterChoice>();
            for (int index = 0; index < choices.Length; index++)
            {
                if (choices[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsEntrance(FpgBootRoomEntrance candidate)
        {
            FpgBootRoomEntrance[] entrances =
                roomEntrances ?? Array.Empty<FpgBootRoomEntrance>();
            for (int index = 0; index < entrances.Length; index++)
            {
                if (entrances[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetCharacterChoicesSelectable(bool selectable)
        {
            FpgBootCharacterChoice[] choices =
                characterChoices ?? Array.Empty<FpgBootCharacterChoice>();
            for (int index = 0; index < choices.Length; index++)
            {
                choices[index]?.SetSelectable(selectable);
            }
        }

        private void SetRoomEntrancesSelectable(bool selectable)
        {
            FpgBootRoomEntrance[] entrances =
                roomEntrances ?? Array.Empty<FpgBootRoomEntrance>();
            for (int index = 0; index < entrances.Length; index++)
            {
                entrances[index]?.SetSelectable(selectable);
            }
        }

        private void SetRoomEntrancesVisible(bool visible)
        {
            FpgBootRoomEntrance[] entrances =
                roomEntrances ?? Array.Empty<FpgBootRoomEntrance>();
            for (int index = 0; index < entrances.Length; index++)
            {
                FpgBootRoomEntrance entrance = entrances[index];
                if (entrance != null && entrance.gameObject.activeSelf != visible)
                {
                    entrance.gameObject.SetActive(visible);
                }
            }
        }

        private void OnDestroy()
        {
            secondaryModeSelector?.Hide();
            runFlowController.Dispose();
        }

        private static bool TryGetSingleComponent<T>(
            Scene scene,
            out T component,
            out string error)
            where T : Component
        {
            component = null;
            int componentCount = 0;
            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                T[] components =
                    rootObjects[rootIndex].GetComponentsInChildren<T>(true);
                for (int componentIndex = 0;
                    componentIndex < components.Length;
                    componentIndex++)
                {
                    component = components[componentIndex];
                    componentCount++;
                }
            }

            if (componentCount != 1)
            {
                error =
                    $"Scene '{scene.name}' must contain exactly one {typeof(T).Name}, found {componentCount}.";
                component = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private readonly struct BootstrapSelectionSnapshot
        {
            public BootstrapSelectionSnapshot(
                FpgPlayableCharacterSelection characterSelection,
                FpgRoomDefinition roomDefinition)
            {
                CharacterSelection = characterSelection;
                RoomDefinition = roomDefinition;
            }

            public FpgPlayableCharacterSelection CharacterSelection { get; }
            public FpgRoomDefinition RoomDefinition { get; }
        }
    }
}
