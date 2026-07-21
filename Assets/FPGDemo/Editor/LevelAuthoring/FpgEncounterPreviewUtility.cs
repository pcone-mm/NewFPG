using System;
using System.Collections.Generic;
using FPG.Demo.Run;
using FPG.Demo.Unity;

namespace FPG.Demo.Editor.LevelAuthoring
{
    internal readonly struct FpgEncounterConcurrencyEstimate
    {
        public FpgEncounterConcurrencyEstimate(int capWeight, int entityCount)
        {
            CapWeight = capWeight;
            EntityCount = entityCount;
        }

        public int CapWeight { get; }
        public int EntityCount { get; }
    }

    internal static class FpgEncounterPreviewUtility
    {
        public static bool TryGenerate(
            FpgRoomDefinition room,
            FpgEncounterProfile profile,
            FpgEncounterOverrideDefinition encounterOverride,
            long runSeed,
            string regionId,
            int depth,
            int difficultyBasisPoints,
            int roomVisitOrdinal,
            out FpgEncounterPlan plan,
            out string error)
        {
            plan = null;
            error = string.Empty;
            if (room == null || profile == null)
            {
                error = "Room and Encounter Profile are required.";
                return false;
            }

            try
            {
                FpgEncounterRunContext context = new FpgEncounterRunContext(
                    unchecked((ulong)runSeed),
                    string.IsNullOrWhiteSpace(regionId) ? "default" : regionId,
                    Math.Max(0, depth),
                    Math.Max(1, difficultyBasisPoints),
                    Math.Max(0, roomVisitOrdinal));
                FpgEncounterOverrideData overrideData = encounterOverride == null
                    ? null
                    : encounterOverride.Data;
                if (encounterOverride != null && overrideData == null)
                {
                    error = "Encounter Override validation failed; inspect the asset before previewing.";
                    return false;
                }

                FpgRoomRunRequest request = FpgFormalRoomRequestFactory.Create(
                    room,
                    profile,
                    overrideData,
                    context);
                FpgEncounterPlanGenerationResult result =
                    FpgEncounterPlanGenerator.Generate(request);
                if (!result.IsSuccess)
                {
                    error = result.Error;
                    return false;
                }

                plan = result.Plan;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static FpgEncounterConcurrencyEstimate EstimateConcurrency(
            FpgEncounterPlan plan,
            FpgEncounterProfile profile)
        {
            FpgEncounterProfileData data = profile == null ? null : profile.Data;
            if (plan == null || data == null)
            {
                return new FpgEncounterConcurrencyEstimate(0, 0);
            }

            int peakCapWeight = 0;
            int peakEntityCount = 0;
            for (int waveIndex = 0; waveIndex < plan.Waves.Count; waveIndex++)
            {
                FpgEncounterWavePlan wave = plan.Waves[waveIndex];
                int capWeightSum = 0;
                List<int> weights = new List<int>(wave.Entries.Count);
                for (int index = 0; index < wave.Entries.Count; index++)
                {
                    int weight = Math.Max(1, wave.Entries[index].CapWeight);
                    capWeightSum = SaturatingAdd(capWeightSum, weight);
                    weights.Add(weight);
                }

                peakCapWeight = Math.Max(
                    peakCapWeight,
                    Math.Min(data.MaxConcurrentCapWeight, capWeightSum));

                weights.Sort();
                int admittedCapWeight = 0;
                int admittedEntities = 0;
                for (int index = 0; index < weights.Count; index++)
                {
                    if (admittedEntities >= data.MaxConcurrentEntities
                        || weights[index] > data.MaxConcurrentCapWeight - admittedCapWeight)
                    {
                        break;
                    }

                    admittedCapWeight += weights[index];
                    admittedEntities++;
                }

                peakEntityCount = Math.Max(peakEntityCount, admittedEntities);
            }

            return new FpgEncounterConcurrencyEstimate(peakCapWeight, peakEntityCount);
        }

        public static bool IsSpawnPointCompatible(
            FpgRoomEnemySpawnRole pointRole,
            FpgEnemyRole enemyRole)
        {
            if (pointRole == FpgRoomEnemySpawnRole.Any
                || enemyRole == FpgEnemyRole.Any)
            {
                return true;
            }

            switch (pointRole)
            {
                case FpgRoomEnemySpawnRole.Melee:
                    return enemyRole == FpgEnemyRole.Melee;
                case FpgRoomEnemySpawnRole.Ranged:
                    return enemyRole == FpgEnemyRole.Ranged;
                case FpgRoomEnemySpawnRole.Support:
                    return enemyRole == FpgEnemyRole.Support;
                default:
                    return false;
            }
        }

        private static int SaturatingAdd(int left, int right)
        {
            return right > int.MaxValue - left ? int.MaxValue : left + right;
        }
    }
}

