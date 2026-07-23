using FPG.Demo.Run;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Explicit room-entry request used by run flow. Encounter profile and
    /// catalogs remain owned by the scene host; room, run context and optional
    /// carried player resources are supplied per visit.
    /// </summary>
    public readonly struct FpgEncounterStartRequest
    {
        public FpgEncounterStartRequest(
            FpgRoomDefinition roomDefinition,
            FpgEncounterRunContext runContext)
            : this(
                roomDefinition,
                runContext,
                false,
                default(FpgPlayerRunResourceState))
        {
        }

        public FpgEncounterStartRequest(
            FpgRoomDefinition roomDefinition,
            FpgEncounterRunContext runContext,
            FpgPlayerRunResourceState playerRunResources)
            : this(roomDefinition, runContext, true, playerRunResources)
        {
        }

        private FpgEncounterStartRequest(
            FpgRoomDefinition roomDefinition,
            FpgEncounterRunContext runContext,
            bool hasPlayerRunResources,
            FpgPlayerRunResourceState playerRunResources)
        {
            RoomDefinition = roomDefinition;
            RunContext = runContext;
            HasPlayerRunResources = hasPlayerRunResources;
            PlayerRunResources = playerRunResources;
        }

        public FpgRoomDefinition RoomDefinition { get; }
        public FpgEncounterRunContext RunContext { get; }
        public bool HasPlayerRunResources { get; }
        public FpgPlayerRunResourceState PlayerRunResources { get; }

        public bool TryValidate(out string error)
        {
            if (RoomDefinition == null)
            {
                error = "Encounter start request requires a room definition.";
                return false;
            }

            FpgRoomValidationResult roomValidation = RoomDefinition.Validate();
            if (!roomValidation.IsValid)
            {
                error = roomValidation.FirstError == null
                    ? $"Room '{RoomDefinition.RoomId}' is invalid."
                    : roomValidation.FirstError.Message;
                return false;
            }

            if (!RunContext.IsValid)
            {
                error = "Encounter start request run context is invalid.";
                return false;
            }

            if (HasPlayerRunResources && !PlayerRunResources.IsValid)
            {
                error = "Encounter start request player resources are invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
