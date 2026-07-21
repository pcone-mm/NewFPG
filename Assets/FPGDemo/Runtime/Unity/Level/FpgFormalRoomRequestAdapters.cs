using System;
using FPG.Demo.Run;

namespace FPG.Demo.Unity
{
    public sealed class FpgEncounterProfileSourceAdapter : IFpgEncounterProfileSource
    {
        private readonly FpgEncounterProfile profile;

        public FpgEncounterProfileSourceAdapter(FpgEncounterProfile profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public FpgEncounterProfileData Data => profile.Data;
        public FpgEncounterProfile Profile => profile;
    }

    public sealed class FpgEncounterOverrideSourceAdapter : IFpgEncounterOverrideSource
    {
        private readonly FpgEncounterOverrideData data;

        public FpgEncounterOverrideSourceAdapter(FpgEncounterOverrideData data)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public FpgEncounterOverrideData Data => data;
    }

    public static class FpgFormalRoomRequestFactory
    {
        public static FpgRoomRunRequest Create(
            FpgRoomDefinition room,
            FpgEncounterProfile profile,
            FpgEncounterOverrideData encounterOverride,
            FpgEncounterRunContext runContext)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return new FpgRoomRunRequest(
                new FpgRoomDefinitionSourceAdapter(room),
                new FpgEncounterProfileSourceAdapter(profile),
                encounterOverride == null
                    ? null
                    : new FpgEncounterOverrideSourceAdapter(encounterOverride),
                runContext);
        }

        public static FpgRoomRunRequest Create(
            FpgRoomDefinition room,
            FpgEncounterProfile profile,
            FpgEncounterOverrideDefinition encounterOverride,
            FpgEncounterRunContext runContext)
        {
            return Create(
                room,
                profile,
                encounterOverride == null ? null : encounterOverride.Data,
                runContext);
        }
    }
}
