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

                if (!director.TryPlacePlayerAtEntry(playerEntryMarkerId, out error))
                {
                    return Fail(error, out error);
                }

                if (!director.TryStart(out error))
                {
                    return Fail(error, out error);
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
                nextTick = 1L;
            }
            else
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
}
}
