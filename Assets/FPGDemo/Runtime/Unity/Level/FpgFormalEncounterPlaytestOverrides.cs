using System;
using FPG.Demo.Run;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Editor playtest hand-off for one formal host start. The override is
    /// memory-only and must be cleared immediately after TryPrepareAndStart.
    /// </summary>
    public static class FpgFormalEncounterPlaytestOverrides
    {
        public static FpgRoomDefinition RoomDefinition { get; private set; }
        public static FpgEncounterProfile EncounterProfile { get; private set; }
        public static FpgEncounterOverrideDefinition EncounterOverride { get; private set; }
        public static FpgEncounterRunContext RunContext { get; private set; }

        public static bool IsActive => RoomDefinition != null && EncounterProfile != null;

        public static void Set(
            FpgRoomDefinition roomDefinition,
            FpgEncounterProfile encounterProfile,
            FpgEncounterOverrideDefinition encounterOverride,
            FpgEncounterRunContext runContext)
        {
            if (roomDefinition == null)
            {
                throw new ArgumentNullException(nameof(roomDefinition));
            }

            if (encounterProfile == null)
            {
                throw new ArgumentNullException(nameof(encounterProfile));
            }

            if (!runContext.IsValid)
            {
                throw new ArgumentException(
                    "Formal encounter playtest requires a valid run context.",
                    nameof(runContext));
            }

            if (!roomDefinition.TryValidate(out FpgRoomValidationResult roomValidation))
            {
                throw new ArgumentException(
                    "Formal encounter playtest room is invalid: " +
                    (roomValidation.FirstError == null
                        ? "unknown validation error"
                        : roomValidation.FirstError.Message),
                    nameof(roomDefinition));
            }

            if (!encounterProfile.TryValidate(out string profileError))
            {
                throw new ArgumentException(profileError, nameof(encounterProfile));
            }

            if (encounterOverride != null
                && !encounterOverride.TryValidate(out string overrideError))
            {
                throw new ArgumentException(overrideError, nameof(encounterOverride));
            }

            RoomDefinition = roomDefinition;
            EncounterProfile = encounterProfile;
            EncounterOverride = encounterOverride;
            RunContext = runContext;
        }

        public static void Clear()
        {
            RoomDefinition = null;
            EncounterProfile = null;
            EncounterOverride = null;
            RunContext = default;
        }
    }
}
