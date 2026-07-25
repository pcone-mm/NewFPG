using System;
using System.Collections.Generic;

namespace FPG.Demo.Run
{
    /// <summary>
    /// Pure projection of one generic summon action. Additional summons expose
    /// gameplay quotas; owner replacements derive their technical capacity
    /// from the reachable owner graph instead of enforcing a runtime quota.
    /// </summary>
    public sealed class FpgSummonActionData
    {
        public FpgSummonActionData(
            string actionId,
            IReadOnlyList<string> ownerEnemyDefinitionIds,
            IReadOnlyList<string> candidateEnemyDefinitionIds,
            int maxSummonsPerOwner,
            int maxTotalSummonsPerEncounter,
            int maxRecursionDepth,
            FpgSummonOccupancyMode occupancyMode = FpgSummonOccupancyMode.AdditionalEntity,
            FpgSummonPlacementMode placementMode = FpgSummonPlacementMode.EncounterSpawnPoint)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                throw new ArgumentException("Summon action ID is required.", nameof(actionId));
            }

            if (ownerEnemyDefinitionIds == null || ownerEnemyDefinitionIds.Count == 0
                || candidateEnemyDefinitionIds == null || candidateEnemyDefinitionIds.Count == 0)
            {
                throw new ArgumentException("Summon action owners and candidates are required.");
            }

            if (maxSummonsPerOwner < 0 || maxTotalSummonsPerEncounter < 0
                || maxRecursionDepth < 0
                || !Enum.IsDefined(typeof(FpgSummonOccupancyMode), occupancyMode)
                || !Enum.IsDefined(typeof(FpgSummonPlacementMode), placementMode)
                || (occupancyMode == FpgSummonOccupancyMode.AdditionalEntity
                    && (maxSummonsPerOwner <= 0 || maxTotalSummonsPerEncounter <= 0))
                || (occupancyMode == FpgSummonOccupancyMode.ReplaceOwner
                    && (maxSummonsPerOwner != 0 || maxTotalSummonsPerEncounter != 0)))
            {
                throw new ArgumentOutOfRangeException(nameof(maxSummonsPerOwner));
            }

            ValidateStableIds(ownerEnemyDefinitionIds, nameof(ownerEnemyDefinitionIds));
            ValidateStableIds(candidateEnemyDefinitionIds, nameof(candidateEnemyDefinitionIds));
            ActionId = actionId;
            OwnerEnemyDefinitionIds = new List<string>(ownerEnemyDefinitionIds).ToArray();
            CandidateEnemyDefinitionIds = new List<string>(candidateEnemyDefinitionIds).ToArray();
            MaxSummonsPerOwner = maxSummonsPerOwner;
            MaxTotalSummonsPerEncounter = maxTotalSummonsPerEncounter;
            MaxRecursionDepth = maxRecursionDepth;
            OccupancyMode = occupancyMode;
            PlacementMode = placementMode;
        }

        public string ActionId { get; }
        public IReadOnlyList<string> OwnerEnemyDefinitionIds { get; }
        public IReadOnlyList<string> CandidateEnemyDefinitionIds { get; }
        public int MaxSummonsPerOwner { get; }
        public int MaxTotalSummonsPerEncounter { get; }
        public int MaxRecursionDepth { get; }
        public FpgSummonOccupancyMode OccupancyMode { get; }
        public FpgSummonPlacementMode PlacementMode { get; }

        private static void ValidateStableIds(IReadOnlyList<string> values, string parameterName)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(values[index]))
                {
                    throw new ArgumentException("Summon graph IDs must be non-empty.", parameterName);
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (string.Equals(values[previous], values[index], StringComparison.Ordinal))
                    {
                        throw new ArgumentException("Summon graph IDs must be unique.", parameterName);
                    }
                }
            }
        }
    }

    public interface IFpgSummonGraphCatalog
    {
        bool TryBuildSummonGraph(
            out IReadOnlyList<FpgSummonActionData> actions,
            out string error);
    }
}
