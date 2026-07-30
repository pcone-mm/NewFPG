using System;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Explicit formal runtime host. It owns no room composition; it only
    /// combines serialized spatial/config references into one run request and
    /// hands the complete plan to FpgRoomEncounterDirector before entry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgEncounterHost : MonoBehaviour
    {
        [Header("Formal request")]
        [SerializeField]
        private FpgRoomDefinition roomDefinition;

        [SerializeField]
        private FpgEncounterProfile encounterProfile;

        [SerializeField]
        private FpgEncounterOverrideDefinition encounterOverride;

        [SerializeField]
        private FpgEnemyDefinitionCatalog enemyCatalog;

        [SerializeField]
        private FpgRoomEncounterDirector director;

        [SerializeField]
        [Tooltip("Player-entry marker applied after Preparing and before the formal Session starts.")]
        private string playerEntryMarkerId = "player-main";

        [Header("Run context")]
        [SerializeField]
        private long runSeed = 1L;

        [SerializeField]
        private string regionId = "default";

        [SerializeField, Min(0)]
        private int depth;

        [SerializeField, Min(1)]
        private int difficultyMultiplierBasisPoints = FpgEncounterRunContext.BasisPointsOne;

        [SerializeField, Min(0)]
        private int roomVisitOrdinal;

        [SerializeField]
        private bool driveFromFixedUpdate = true;

        private long nextTick;
        private bool prepared;
        private bool initialCoverTransitionPending;
        private string pendingInitialCoverId = string.Empty;

        public FpgRoomDefinition RoomDefinition => roomDefinition;
        public FpgEncounterProfile EncounterProfile => encounterProfile;
        public FpgEncounterOverrideDefinition EncounterOverride => encounterOverride;
        public FpgEnemyDefinitionCatalog EnemyCatalog => enemyCatalog;
        public FpgRoomEncounterDirector Director => director;
        public string PlayerEntryMarkerId => playerEntryMarkerId;
        public FpgEncounterPlan Plan { get; private set; }
        public FpgEncounterRunContext RunContext { get; private set; }
        public bool IsPrepared => prepared;
        public string LastError { get; private set; } = string.Empty;

        public event Action<FpgRoomClearedEvent> RoomCleared;

        private void Awake()
        {
            if (director != null)
            {
                director.RoomCleared += HandleRoomCleared;
                director.LifecycleEvent += HandleDirectorLifecycle;
                director.RestartSucceeded += HandleDirectorRestartSucceeded;
            }
        }

        public bool TrySetRoomDefinition(FpgRoomDefinition room, out string error)
        {
            if (prepared || director != null && director.UsesFormalSession)
            {
                error = "Formal encounter room cannot change after preparation.";
                return false;
            }

            if (room == null)
            {
                error = "Formal encounter room is missing.";
                return false;
            }

            FpgRoomValidationResult validation = room.Validate();
            if (!validation.IsValid)
            {
                error = validation.FirstError == null
                    ? $"Room '{room.RoomId}' is invalid."
                    : validation.FirstError.Message;
                return false;
            }

            roomDefinition = room;
            error = string.Empty;
            return true;
        }


        public bool TryPrepareAndStart(out string error)
        {
            return TryPrepareAndStartInternal(
                false,
                default(FpgEncounterStartRequest),
                out error);
        }

        public bool TryPrepareAndStart(
            in FpgEncounterStartRequest startRequest,
            out string error)
        {
            if (!startRequest.TryValidate(out error))
            {
                return Fail(error, out error);
            }

            return TryPrepareAndStartInternal(true, startRequest, out error);
        }

        private bool TryPrepareAndStartInternal(
            bool hasExplicitRequest,
            in FpgEncounterStartRequest startRequest,
            out string error)
        {
            prepared = false;
            Plan = null;
            LastError = string.Empty;

            try
            {
                bool playtestOverrideActive =
                    FpgFormalEncounterPlaytestOverrides.IsActive;
                FpgRoomDefinition effectiveRoom = hasExplicitRequest
                    ? startRequest.RoomDefinition
                    : playtestOverrideActive
                        ? FpgFormalEncounterPlaytestOverrides.RoomDefinition
                        : roomDefinition;
                FpgEncounterProfile effectiveProfile = playtestOverrideActive
                    ? FpgFormalEncounterPlaytestOverrides.EncounterProfile
                    : encounterProfile;
                FpgEncounterOverrideDefinition effectiveOverride =
                    playtestOverrideActive
                        ? FpgFormalEncounterPlaytestOverrides.EncounterOverride
                        : encounterOverride;
                FpgEncounterRunContext effectiveRunContext =
                    hasExplicitRequest
                        ? startRequest.RunContext
                        : playtestOverrideActive
                            ? FpgFormalEncounterPlaytestOverrides.RunContext
                            : new FpgEncounterRunContext(
                            unchecked((ulong)runSeed),
                            string.IsNullOrWhiteSpace(regionId)
                                ? "default"
                                : regionId,
                            Mathf.Max(0, depth),
                            Mathf.Max(1, difficultyMultiplierBasisPoints),
                            Mathf.Max(0, roomVisitOrdinal));

                if (effectiveRoom == null || effectiveProfile == null
                    || !effectiveRunContext.IsValid || enemyCatalog == null
                    || director == null)
                {
                    return Fail(
                        "Formal encounter host requires effective room, profile, run context, enemy catalog and director references.",
                        out error);
                }

                if (!enemyCatalog.TryValidate(out error))
                {
                    return Fail(error, out error);
                }

                RunContext = effectiveRunContext;
                FpgEncounterOverrideData overrideData = effectiveOverride == null
                    ? null
                    : effectiveOverride.Data;
                if (effectiveOverride != null && overrideData == null)
                {
                    return Fail(
                        "Formal encounter override failed validation.",
                        out error);
                }

                FpgRoomRunRequest request = FpgFormalRoomRequestFactory.Create(
                    effectiveRoom,
                    effectiveProfile,
                    overrideData,
                    effectiveRunContext);
                FpgEncounterPlanGenerationResult generated =
                    FpgEncounterPlanGenerator.Generate(request);
                if (!generated.IsSuccess)
                {
                    return Fail(generated.Error, out error);
                }

                Plan = generated.Plan;
                FpgEncounterPreflightResult preflight =
                    FpgEncounterPreflight.Validate(
                        request,
                        Plan,
                        enemyCatalog);
                if (!preflight.IsSuccess)
                {
                    return Fail(preflight.Error, out error);
                }

                IFpgFormalPlayerRunResourceImportPort importPort =
                    director.PlayerRunResourceImportPort;
                importPort?.ClearNextPlayerRunResources();
                if (hasExplicitRequest && startRequest.HasPlayerRunResources
                    && importPort == null)
                {
                    return Fail(
                        typeof(IFpgFormalPlayerRunResourceImportPort).Name,
                        out error);
                }
                if (hasExplicitRequest && startRequest.HasPlayerRunResources
                    && !importPort.TrySetNextPlayerRunResources(
                        startRequest.PlayerRunResources,
                        out error))
                {
                    return Fail(error, out error);
                }

                bool sessionPrepared;
                try
                {
                    sessionPrepared = director.TryPrepareSession(
                        request,
                        Plan,
                        enemyCatalog,
                        out error);
                }
                finally
                {
                    importPort?.ClearNextPlayerRunResources();
                }

                if (!sessionPrepared)
                {
                    return Fail(error, out error);
                }

                if (!TryBeginInitialCoverTransition(out error))
                {
                    return Fail(
                        string.IsNullOrWhiteSpace(error)
                            ? "Formal initial cover transition could not start."
                            : error,
                        out error);
                }

                nextTick = 1L;
                roomDefinition = effectiveRoom;
                prepared = true;
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                return Fail(exception.Message, out error);
            }
        }

        public bool Tick(TickIndex tick, out string error)
        {
            if (!prepared || director == null)
            {
                error = "Formal encounter host is not prepared.";
                return false;
            }

            if (initialCoverTransitionPending)
            {
                TryCompleteInitialCoverTransition();
                if (!prepared)
                {
                    error = string.IsNullOrWhiteSpace(LastError)
                        ? "Formal initial cover transition failed."
                        : LastError;
                    return false;
                }

                if (initialCoverTransitionPending)
                {
                    error = string.Empty;
                    return true;
                }
            }

            bool result = director.Tick(tick, out error);
            if (!result)
            {
                LastError = error ?? string.Empty;
            }

            return result;
        }

        public void StopAndClear()
        {
            prepared = false;
            initialCoverTransitionPending = false;
            pendingInitialCoverId = string.Empty;
            director?.ClearPlayerBinding();
            Plan = null;
        }

        public bool TryPause(out string error)
        {
            return prepared && director != null
                ? director.TryPause(out error)
                : Fail("Formal encounter host is not prepared.", out error);
        }

        public bool TryResume(out string error)
        {
            return prepared && director != null
                ? director.TryResume(out error)
                : Fail("Formal encounter host is not prepared.", out error);
        }

        public bool TryRestart(out string error)
        {
            if (!prepared || director == null)
            {
                return Fail("Formal encounter host is not prepared.", out error);
            }

            bool restarted = director.TryRestart(out error);
            if (restarted)
            {
                if (!initialCoverTransitionPending)
                {
                    error = string.IsNullOrWhiteSpace(LastError)
                        ? "Formal restart could not begin the cover entry transition."
                        : LastError;
                    restarted = false;
                }
                else
                {
                    nextTick = 1L;
                }
            }

            if (!restarted)
            {
                LastError = error ?? string.Empty;
            }
            return restarted;
        }

        private void FixedUpdate()
        {
            if (!driveFromFixedUpdate || !prepared || director == null
                || director.IsPaused)
            {
                return;
            }

            if (initialCoverTransitionPending)
            {
                TryCompleteInitialCoverTransition();
                return;
            }

            TickIndex tick = new TickIndex(nextTick);
            bool advanced;
            if (director.Phase == FpgEncounterPhase.Cleared)
            {
                advanced = director.HasAvailableExits
                    && director.ProcessRoomInteractionTick(tick, out _);
            }
            else
            {
                advanced = !director.IsTerminal && Tick(tick, out _);
            }

            if (advanced)
            {
                nextTick++;
            }
        }

        private void TryCompleteInitialCoverTransition()
        {
            FpgCoverTraversalPresenter presenter =
                director.PlayerTickDriver?.CoverTraversalPresenter;
            FpgFormalPlayerCameraFeedback cameraFeedback =
                director.PlayerTickDriver?.CameraFeedback;
            if (presenter == null || cameraFeedback == null
                || !presenter.HasReachedVisualEnd)
            {
                return;
            }

            FpgResolvedCameraShot sourceShot = cameraFeedback.CommittedShot;
            if (!director.TryResolveCoverReachablePoseAndCameraShot(
                    pendingInitialCoverId,
                    out Pose pose,
                    out _,
                    out FpgResolvedCameraShot targetShot,
                    out string error)
                || (!cameraFeedback.TryCommitShotTransition(out error)
                    && !cameraFeedback.TryApplyImmediateShot(
                        targetShot,
                        out error)))
            {
                FailInitialCoverTransition(error);
                return;
            }

            if (!director.TryPlacePlayerAtCover(
                    pendingInitialCoverId,
                    out error))
            {
                cameraFeedback.TryApplyImmediateShot(sourceShot, out _);
                director.TryPlacePlayerAtEntry(playerEntryMarkerId, out _);
                FailInitialCoverTransition(error);
                return;
            }

            presenter.Complete(pose);
            FpgCoverSnapshot cover = director.CombatRuntime.Covers.CurrentSnapshot;
            if (cover.IsDestroyed)
            {
                director.Player.Exposure.ForceExposed(new TickIndex(0L), out _);
            }
            else
            {
                director.Player.Exposure.ApplyCombatPosture(
                    false,
                    new TickIndex(0L),
                    false,
                    out _);
            }

            director.RefreshCoverViews();
            if (!director.TryStart(out error))
            {
                FailInitialCoverTransition(error);
                return;
            }

            initialCoverTransitionPending = false;
            pendingInitialCoverId = string.Empty;
        }

        private bool TryBeginInitialCoverTransition(out string error)
        {
            FpgCoverTraversalPresenter traversalPresenter =
                director.PlayerTickDriver?.CoverTraversalPresenter;
            traversalPresenter?.Cancel();
            FpgFormalPlayerCameraFeedback cameraFeedback =
                director.PlayerTickDriver?.CameraFeedback;
            cameraFeedback?.CancelShotTransition();
            if (!director.TryPlacePlayerAtEntry(playerEntryMarkerId, out error))
            {
                return false;
            }

            FpgCoverRuntime covers = director.CombatRuntime?.Covers;
            FpgCoverSnapshot startingCover = covers == null
                ? default(FpgCoverSnapshot)
                : covers.CurrentSnapshot;
            Pose sourcePose = new Pose(
                director.PlayerAnchor.position,
                director.PlayerAnchor.rotation);
            float traversalSeconds = director.PlayerTickDriver == null
                || director.PlayerTickDriver.ThreeCProfile == null
                    ? 0f
                    : director.PlayerTickDriver.ThreeCProfile
                        .CoverTraversalSeconds;
            if (!startingCover.IsValid
                || traversalPresenter == null
                || cameraFeedback == null
                || !director.TryResolveCoverCameraShot(
                    startingCover.CoverId,
                    sourcePose,
                    out FpgResolvedCameraShot sourceShot,
                    out error)
                || !director.TryResolveCoverReachablePoseAndCameraShot(
                    startingCover.CoverId,
                    out Pose targetPose,
                    out _,
                    out FpgResolvedCameraShot targetShot,
                    out error)
                || !cameraFeedback.TryApplyImmediateShot(
                    sourceShot,
                    out error)
                || !traversalPresenter.TryBegin(
                    sourcePose,
                    targetPose,
                    traversalSeconds,
                    out error)
                || !cameraFeedback.TryBeginShotTransition(
                    sourceShot,
                    targetShot,
                    traversalSeconds,
                    out error))
            {
                traversalPresenter?.Cancel();
                cameraFeedback?.CancelShotTransition();
                error = string.IsNullOrWhiteSpace(error)
                    ? "Formal initial cover transition could not start."
                    : error;
                return false;
            }

            initialCoverTransitionPending = true;
            pendingInitialCoverId = startingCover.CoverId;
            error = string.Empty;
            return true;
        }

        private void FailInitialCoverTransition(string message)
        {
            director.PlayerTickDriver?.CoverTraversalPresenter?.Cancel();
            director.PlayerTickDriver?.CameraFeedback?.CancelShotTransition();
            initialCoverTransitionPending = false;
            pendingInitialCoverId = string.Empty;
            prepared = false;
            Fail(message, out _);
        }

        private bool Fail(string message, out string error)
        {
            error = string.IsNullOrWhiteSpace(message) ? "Formal encounter host failed." : message;
            LastError = error;
            return false;
        }

        private void HandleRoomCleared(FpgRoomClearedEvent cleared)
        {
            RoomCleared?.Invoke(cleared);
        }

        private void OnDestroy()
        {
            if (director != null)
            {
                director.RoomCleared -= HandleRoomCleared;
                director.LifecycleEvent -= HandleDirectorLifecycle;
                director.RestartSucceeded -= HandleDirectorRestartSucceeded;
            }

            if (prepared)
            {
                director?.Dispose();
            }
        }
    

        private void HandleDirectorLifecycle(
            FpgEncounterLifecycleEvent lifecycleEvent)
        {
            if (lifecycleEvent.Type == FpgEncounterLifecycleEventType.Restarted)
            {
                nextTick = 1L;
                LastError = string.Empty;
            }
        }

        private void HandleDirectorRestartSucceeded()
        {
            nextTick = 1L;
            if (!TryBeginInitialCoverTransition(out string error))
            {
                FailInitialCoverTransition(error);
                return;
            }

            LastError = string.Empty;
        }
}
}
