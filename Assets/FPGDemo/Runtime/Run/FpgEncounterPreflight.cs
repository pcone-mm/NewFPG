using System;
using System.Collections.Generic;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    public readonly struct FpgEncounterCapacityRequirements
    {
        public FpgEncounterCapacityRequirements(
            int plannedEnemies,
            int summonUpperBound,
            int entitySlots,
            int simultaneousCombatants,
            int spawnPointCapacity)
        {
            PlannedEnemies = plannedEnemies;
            SummonUpperBound = summonUpperBound;
            EntitySlots = entitySlots;
            SimultaneousCombatants = simultaneousCombatants;
            SpawnPointCapacity = spawnPointCapacity;
        }

        public int PlannedEnemies { get; }
        public int SummonUpperBound { get; }
        public int EntitySlots { get; }
        public int SimultaneousCombatants { get; }
        public int SpawnPointCapacity { get; }
    }

    public readonly struct FpgEncounterPreflightResult
    {
        public FpgEncounterPreflightResult(
            DomainResult result,
            FpgEncounterFailureReason failureReason,
            FpgEncounterCapacityRequirements requirements,
            string error)
        {
            Result = result;
            FailureReason = failureReason;
            Requirements = requirements;
            Error = error ?? string.Empty;
        }

        public DomainResult Result { get; }
        public FpgEncounterFailureReason FailureReason { get; }
        public FpgEncounterCapacityRequirements Requirements { get; }
        public string Error { get; }
        public bool IsSuccess => Result.IsSuccess;
    }

    /// <summary>
    /// Fail-closed validation performed before any formal battle tick. It
    /// covers ownership, role compatibility, duplicate stable identities and
    /// fixed capacities that can be derived from pure encounter data.
    /// </summary>
    public static class FpgEncounterPreflight
    {
        public static FpgEncounterPreflightResult Validate(
            FpgRoomRunRequest request,
            FpgEncounterPlan plan,
            IFpgEnemyDefinitionCatalog catalog)
        {
            if (!request.IsValid || plan == null || catalog == null)
            {
                return Failure(
                    FpgEncounterFailureReason.InvalidRequest,
                    RejectReason.InvalidDefinition,
                    default(FpgEncounterCapacityRequirements),
                    "Encounter preflight requires a valid request, plan and enemy catalog.");
            }

            FpgEncounterProfileData profile = request.EncounterProfile.Data;
            string profileError = null;
            if (profile == null || !profile.TryValidate(out profileError))
            {
                return Failure(
                    FpgEncounterFailureReason.InvalidProfile,
                    RejectReason.InvalidDefinition,
                    default(FpgEncounterCapacityRequirements),
                    profileError ?? "Encounter profile is invalid.");
            }

            if (!request.RunContext.Equals(plan.RunContext)
                || !string.Equals(
                    request.RoomDefinition.RoomDefinitionId,
                    plan.RoomDefinitionId,
                    StringComparison.Ordinal))
            {
                return Failure(
                    FpgEncounterFailureReason.InvalidRunContext,
                    RejectReason.InvalidDefinition,
                    default(FpgEncounterCapacityRequirements),
                    "Encounter plan does not belong to this room run request.");
            }

            if (request.RoomDefinition.ExitCount <= 0)
            {
                return Failure(
                    FpgEncounterFailureReason.InvalidRequest,
                    RejectReason.InvalidDefinition,
                    default(FpgEncounterCapacityRequirements),
                    "Formal combat room requires at least one exit.");
            }

            int spawnPointCapacity = 0;
            for (int pointIndex = 0; pointIndex < request.RoomDefinition.SpawnPointCount; pointIndex++)
            {
                FpgSpawnPointCandidate point = request.RoomDefinition.GetSpawnPoint(pointIndex);
                spawnPointCapacity = SaturatingAdd(spawnPointCapacity, point.Capacity);
            }

            Dictionary<string, int> reachableDepths = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int entryIndex = 0; entryIndex < plan.AllEntries.Count; entryIndex++)
            {
                FpgSpawnEntry entry = plan.AllEntries[entryIndex];
                if (!catalog.TryGet(entry.EnemyDefinitionId, out FpgEnemyDefinitionData definition)
                    || definition == null)
                {
                    return Failure(
                        FpgEncounterFailureReason.InvalidPool,
                        RejectReason.InvalidDefinition,
                        default(FpgEncounterCapacityRequirements),
                        "Plan references an enemy missing from the runtime catalog: "
                            + entry.EnemyDefinitionId);
                }

                if (!HasCompatiblePoint(request.RoomDefinition, entry.Role))
                {
                    return Failure(
                        FpgEncounterFailureReason.MissingSpawnPoint,
                        RejectReason.InvalidTarget,
                        default(FpgEncounterCapacityRequirements),
                        "No compatible spawn point exists for enemy role " + entry.Role + ".");
                }

                if (!reachableDepths.ContainsKey(entry.EnemyDefinitionId))
                {
                    reachableDepths.Add(entry.EnemyDefinitionId, 0);
                }
                for (int priorIndex = 0; priorIndex < entryIndex; priorIndex++)
                {
                    FpgSpawnEntry prior = plan.AllEntries[priorIndex];
                    if (string.Equals(prior.SpawnEntryId, entry.SpawnEntryId, StringComparison.Ordinal)
                        || prior.SpawnSequence == entry.SpawnSequence)
                    {
                        return Failure(
                            FpgEncounterFailureReason.InvalidRequest,
                            RejectReason.DuplicateSequence,
                            default(FpgEncounterCapacityRequirements),
                            "Encounter plan repeats a spawn identity or sequence.");
                    }
                }
            }

            if (!TryCalculateSummonUpperBound(
                    request.RoomDefinition,
                    catalog,
                    reachableDepths,
                    out int summonUpperBound,
                    out string summonError))
            {
                return Failure(
                    FpgEncounterFailureReason.InvalidSummonGraph,
                    RejectReason.InvalidDefinition,
                    default(FpgEncounterCapacityRequirements),
                    summonError);
            }

            int entitySlots = SaturatingAdd(plan.EntryCount, summonUpperBound);
            int simultaneous = Math.Min(entitySlots, profile.MaxConcurrentEntities);
            FpgEncounterCapacityRequirements requirements = new FpgEncounterCapacityRequirements(
                plan.EntryCount,
                summonUpperBound,
                entitySlots,
                simultaneous,
                spawnPointCapacity);

            if (entitySlots > profile.EnemyRosterCapacity
                || entitySlots > profile.EntityPoolCapacity)
            {
                return Failure(
                    FpgEncounterFailureReason.EntityCapacity,
                    RejectReason.BufferCapacity,
                    requirements,
                    "Plan plus summon upper bound exceeds roster or entity-pool capacity.");
            }

            if (simultaneous > spawnPointCapacity)
            {
                return Failure(
                    FpgEncounterFailureReason.SpawnPointCapacity,
                    RejectReason.BufferCapacity,
                    requirements,
                    "Room spawn-point capacity is below the simultaneous enemy limit.");
            }

            if (simultaneous > profile.WarningCapacity
                || simultaneous > profile.OverheadHealthBarCapacity
                || simultaneous > profile.HitboxCapacity)
            {
                return Failure(
                    FpgEncounterFailureReason.EntityCapacity,
                    RejectReason.BufferCapacity,
                    requirements,
                    "Warning, health-bar or hitbox capacity is below the simultaneous enemy requirement.");
            }

            return new FpgEncounterPreflightResult(
                DomainResult.Success,
                FpgEncounterFailureReason.None,
                requirements,
                string.Empty);
        }

        private static bool TryCalculateSummonUpperBound(
            IFpgRoomDefinitionSource room,
            IFpgEnemyDefinitionCatalog catalog,
            Dictionary<string, int> reachableDepths,
            out int summonUpperBound,
            out string error)
        {
            summonUpperBound = 0;
            error = string.Empty;
            bool graphRequired = false;
            foreach (KeyValuePair<string, int> root in reachableDepths)
            {
                if (!catalog.TryGet(root.Key, out FpgEnemyDefinitionData definition)
                    || definition == null)
                {
                    error = "Summon closure root is missing from the enemy catalog: " + root.Key;
                    return false;
                }

                graphRequired |= definition.MaxSummons > 0;
            }

            if (!(catalog is IFpgSummonGraphCatalog graphCatalog))
            {
                if (graphRequired)
                {
                    error = "Enemy catalog contains summoners but exposes no summon graph projection.";
                    return false;
                }

                return true;
            }

            if (!graphCatalog.TryBuildSummonGraph(
                    out IReadOnlyList<FpgSummonActionData> actions,
                    out error)
                || actions == null)
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Enemy catalog failed to build its summon graph."
                    : error;
                return false;
            }

            HashSet<string> actionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < actions.Count; index++)
            {
                FpgSummonActionData action = actions[index];
                if (action == null || !actionIds.Add(action.ActionId))
                {
                    error = "Summon graph contains a missing action or duplicate action ID.";
                    return false;
                }
            }

            if (graphRequired)
            {
                foreach (KeyValuePair<string, int> root in reachableDepths)
                {
                    if (!catalog.TryGet(root.Key, out FpgEnemyDefinitionData definition)
                        || definition == null || definition.MaxSummons <= 0)
                    {
                        continue;
                    }

                    bool represented = false;
                    for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                    {
                        if (ContainsId(actions[actionIndex].OwnerEnemyDefinitionIds, root.Key))
                        {
                            represented = true;
                            break;
                        }
                    }

                    if (!represented)
                    {
                        error = "Summoner is absent from the projected summon graph: " + root.Key;
                        return false;
                    }
                }
            }

            HashSet<string> countedActions = new HashSet<string>(StringComparer.Ordinal);
            for (int iteration = 0; iteration <= actions.Count; iteration++)
            {
                bool depthChanged = false;
                for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    FpgSummonActionData action = actions[actionIndex];
                    int ownerDepth = int.MaxValue;
                    for (int ownerIndex = 0; ownerIndex < action.OwnerEnemyDefinitionIds.Count; ownerIndex++)
                    {
                        if (reachableDepths.TryGetValue(
                                action.OwnerEnemyDefinitionIds[ownerIndex],
                                out int candidateDepth)
                            && candidateDepth < ownerDepth)
                        {
                            ownerDepth = candidateDepth;
                        }
                    }

                    if (ownerDepth == int.MaxValue || ownerDepth >= action.MaxRecursionDepth)
                    {
                        continue;
                    }

                    if (countedActions.Add(action.ActionId))
                    {
                        summonUpperBound = SaturatingAdd(
                            summonUpperBound,
                            action.MaxTotalSummonsPerEncounter);
                    }

                    int childDepth = ownerDepth == int.MaxValue
                        ? int.MaxValue
                        : ownerDepth + 1;
                    for (int candidateIndex = 0;
                        candidateIndex < action.CandidateEnemyDefinitionIds.Count;
                        candidateIndex++)
                    {
                        string candidateId = action.CandidateEnemyDefinitionIds[candidateIndex];
                        if (!catalog.TryGet(candidateId, out FpgEnemyDefinitionData candidate)
                            || candidate == null)
                        {
                            error = "Summon graph candidate is missing from the enemy catalog: "
                                + candidateId;
                            return false;
                        }

                        if (!HasCompatiblePoint(room, candidate.Role))
                        {
                            error = "No compatible spawn point exists for summoned enemy role "
                                + candidate.Role + ".";
                            return false;
                        }

                        if (!reachableDepths.TryGetValue(candidateId, out int existingDepth)
                            || childDepth < existingDepth)
                        {
                            reachableDepths[candidateId] = childDepth;
                            depthChanged = true;
                        }
                    }
                }

                if (!depthChanged)
                {
                    return true;
                }
            }

            error = "Summon graph did not converge within its fixed action bound.";
            return false;
        }

        private static bool ContainsId(IReadOnlyList<string> values, string expected)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index], expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCompatiblePoint(
            IFpgRoomDefinitionSource room,
            FpgEnemyRole enemyRole)
        {
            for (int index = 0; index < room.SpawnPointCount; index++)
            {
                FpgEnemyRole pointRole = room.GetSpawnPoint(index).Role;
                if (pointRole == FpgEnemyRole.Any
                    || enemyRole == FpgEnemyRole.Any
                    || pointRole == enemyRole)
                {
                    return true;
                }
            }

            return false;
        }

        private static int SaturatingAdd(int left, int right)
        {
            if (right <= 0)
            {
                return left;
            }

            return right > int.MaxValue - left ? int.MaxValue : left + right;
        }

        private static FpgEncounterPreflightResult Failure(
            FpgEncounterFailureReason failureReason,
            RejectReason rejectReason,
            FpgEncounterCapacityRequirements requirements,
            string error)
        {
            return new FpgEncounterPreflightResult(
                DomainResult.Rejected(rejectReason),
                failureReason,
                requirements,
                error);
        }
    }
}

