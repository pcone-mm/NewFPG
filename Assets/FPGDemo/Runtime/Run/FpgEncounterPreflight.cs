using System;
using System.Collections.Generic;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    public readonly struct FpgEnemyPoolCapacityRequirement
    {
        public FpgEnemyPoolCapacityRequirement(
            string enemyDefinitionId,
            int count)
        {
            if (string.IsNullOrWhiteSpace(enemyDefinitionId) || count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            EnemyDefinitionId = enemyDefinitionId;
            Count = count;
        }

        public string EnemyDefinitionId { get; }
        public int Count { get; }
    }

    public readonly struct FpgEncounterCapacityRequirements
    {
        public FpgEncounterCapacityRequirements(
            int plannedEnemies,
            int summonUpperBound,
            int gameplayQuotaSummonUpperBound,
            int entitySlots,
            int entityPoolSlots,
            int simultaneousCombatants,
            int requiredSummonRecursionDepth,
            int requiredRoomSpawnPoints,
            int spawnPointCapacity,
            IReadOnlyList<FpgEnemyPoolCapacityRequirement> enemyPoolRequirements)
        {
            PlannedEnemies = plannedEnemies;
            SummonUpperBound = summonUpperBound;
            GameplayQuotaSummonUpperBound = gameplayQuotaSummonUpperBound;
            EntitySlots = entitySlots;
            EntityPoolSlots = entityPoolSlots;
            SimultaneousCombatants = simultaneousCombatants;
            RequiredSummonRecursionDepth = requiredSummonRecursionDepth;
            RequiredRoomSpawnPoints = requiredRoomSpawnPoints;
            SpawnPointCapacity = spawnPointCapacity;
            EnemyPoolRequirements = enemyPoolRequirements
                ?? Array.Empty<FpgEnemyPoolCapacityRequirement>();
        }

        public int PlannedEnemies { get; }
        public int SummonUpperBound { get; }
        public int GameplayQuotaSummonUpperBound { get; }
        public int EntitySlots { get; }
        public int EntityPoolSlots { get; }
        public int SimultaneousCombatants { get; }
        public int RequiredSummonRecursionDepth { get; }
        public int RequiredRoomSpawnPoints { get; }
        public int SpawnPointCapacity { get; }
        public IReadOnlyList<FpgEnemyPoolCapacityRequirement> EnemyPoolRequirements { get; }
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

                if (!reachableDepths.TryGetValue(
                        entry.EnemyDefinitionId,
                        out int existingRootDepth)
                    || entry.RecursionDepth < existingRootDepth)
                {
                    reachableDepths[entry.EnemyDefinitionId] = entry.RecursionDepth;
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
                    plan.AllEntries,
                    Math.Min(
                        profile.EnemyRosterCapacity,
                        profile.EntityPoolCapacity),
                    out int summonUpperBound,
                    out int gameplayQuotaSummonUpperBound,
                    out int roomPointSummonUpperBound,
                    out int requiredSummonRecursionDepth,
                    out IReadOnlyList<FpgEnemyPoolCapacityRequirement> rawEnemyPoolRequirements,
                    out bool hasOwnerReplacement,
                    out bool hasRoomPointReplacement,
                    out string summonError))
            {
                return Failure(
                    FpgEncounterFailureReason.InvalidSummonGraph,
                    RejectReason.InvalidDefinition,
                    default(FpgEncounterCapacityRequirements),
                    summonError);
            }

            int entitySlots = SaturatingAdd(plan.EntryCount, summonUpperBound);
            int technicalConcurrentLimit = SaturatingAdd(
                profile.MaxConcurrentEntities,
                hasOwnerReplacement ? 1 : 0);
            int simultaneous = Math.Min(entitySlots, technicalConcurrentLimit);
            LimitEnemyPoolRequirements(
                rawEnemyPoolRequirements,
                simultaneous,
                out IReadOnlyList<FpgEnemyPoolCapacityRequirement> enemyPoolRequirements,
                out int entityPoolSlots);
            int requiredRoomSpawnPoints = Math.Min(
                SaturatingAdd(plan.EntryCount, roomPointSummonUpperBound),
                profile.MaxConcurrentEntities);
            if (hasRoomPointReplacement)
            {
                requiredRoomSpawnPoints = simultaneous;
            }

            FpgEncounterCapacityRequirements requirements = new FpgEncounterCapacityRequirements(
                plan.EntryCount,
                summonUpperBound,
                gameplayQuotaSummonUpperBound,
                entitySlots,
                entityPoolSlots,
                simultaneous,
                requiredSummonRecursionDepth,
                requiredRoomSpawnPoints,
                spawnPointCapacity,
                enemyPoolRequirements);

            if (entitySlots > profile.EnemyRosterCapacity
                || entityPoolSlots > profile.EntityPoolCapacity)
            {
                return Failure(
                    FpgEncounterFailureReason.EntityCapacity,
                    RejectReason.BufferCapacity,
                    requirements,
                    "Plan plus summon upper bound exceeds roster capacity, or per-definition warmup exceeds entity-pool capacity.");
            }

            if (requiredRoomSpawnPoints > spawnPointCapacity)
            {
                return Failure(
                    FpgEncounterFailureReason.SpawnPointCapacity,
                    RejectReason.BufferCapacity,
                    requirements,
                    "Room spawn-point capacity is below the authored placement demand.");
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
            IReadOnlyList<FpgSpawnEntry> plannedEntries,
            int entityCapacity,
            out int summonUpperBound,
            out int gameplayQuotaSummonUpperBound,
            out int roomPointSummonUpperBound,
            out int requiredSummonRecursionDepth,
            out IReadOnlyList<FpgEnemyPoolCapacityRequirement> enemyPoolRequirements,
            out bool hasOwnerReplacement,
            out bool hasRoomPointReplacement,
            out string error)
        {
            summonUpperBound = 0;
            gameplayQuotaSummonUpperBound = 0;
            roomPointSummonUpperBound = 0;
            BuildEnemyPoolRequirements(
                plannedEntries,
                Array.Empty<FpgSummonActionData>(),
                new Dictionary<string, int>(StringComparer.Ordinal),
                out enemyPoolRequirements,
                out _);
            requiredSummonRecursionDepth = 0;
            hasOwnerReplacement = false;
            hasRoomPointReplacement = false;
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

                graphRequired |= definition.HasSummonAction;
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

            if (!TryValidateAcyclicSummonGraph(actions, out error))
            {
                return false;
            }

            if (graphRequired)
            {
                foreach (KeyValuePair<string, int> root in reachableDepths)
                {
                    if (!catalog.TryGet(root.Key, out FpgEnemyDefinitionData definition)
                        || definition == null || !definition.HasSummonAction)
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

            bool converged = false;
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

                        if (action.PlacementMode
                                == FpgSummonPlacementMode.EncounterSpawnPoint
                            && !HasCompatiblePoint(room, candidate.Role))
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
                    converged = true;
                    break;
                }
            }

            if (!converged)
            {
                error = "Summon graph did not converge within its fixed action bound.";
                return false;
            }

            foreach (KeyValuePair<string, int> reachable in reachableDepths)
            {
                if (!catalog.TryGet(
                        reachable.Key,
                        out FpgEnemyDefinitionData definition)
                    || definition == null)
                {
                    error = "Reachable summon definition is missing from the enemy catalog: "
                        + reachable.Key;
                    return false;
                }

                if (!definition.HasSummonAction)
                {
                    continue;
                }

                bool represented = false;
                for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    if (ContainsId(
                            actions[actionIndex].OwnerEnemyDefinitionIds,
                            reachable.Key))
                    {
                        represented = true;
                        break;
                    }
                }

                if (!represented)
                {
                    error = "Reachable summoner is absent from the projected summon graph: "
                        + reachable.Key;
                    return false;
                }
            }

            Dictionary<string, int> reachableActionDepths =
                new Dictionary<string, int>(StringComparer.Ordinal);
            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                FpgSummonActionData action = actions[actionIndex];
                int ownerDepth = FindMinimumOwnerDepth(action, reachableDepths);
                if (ownerDepth == int.MaxValue || ownerDepth >= action.MaxRecursionDepth)
                {
                    continue;
                }

                reachableActionDepths.Add(action.ActionId, ownerDepth);
                if (action.OccupancyMode == FpgSummonOccupancyMode.AdditionalEntity)
                {
                    gameplayQuotaSummonUpperBound = SaturatingAdd(
                        gameplayQuotaSummonUpperBound,
                        action.MaxTotalSummonsPerEncounter);
                    if (action.PlacementMode == FpgSummonPlacementMode.EncounterSpawnPoint)
                    {
                        roomPointSummonUpperBound = SaturatingAdd(
                            roomPointSummonUpperBound,
                            action.MaxTotalSummonsPerEncounter);
                    }
                }
                else
                {
                    hasOwnerReplacement = true;
                    hasRoomPointReplacement |= action.PlacementMode
                        == FpgSummonPlacementMode.EncounterSpawnPoint;
                }
            }

            int summonFailureThreshold = FindSummonFailureThreshold(
                entityCapacity,
                plannedEntries.Count);
            int replacementUpperBound = 0;
            for (int entryIndex = 0; entryIndex < plannedEntries.Count; entryIndex++)
            {
                FpgSpawnEntry planned = plannedEntries[entryIndex];
                int chainLength = FindReplacementChainLength(
                    planned.EnemyDefinitionId,
                    planned.RecursionDepth,
                    actions,
                    summonFailureThreshold);
                replacementUpperBound = AddPopulationReplacementUpperBound(
                    replacementUpperBound,
                    1,
                    chainLength,
                    summonFailureThreshold);
            }

            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                FpgSummonActionData action = actions[actionIndex];
                if (action.OccupancyMode != FpgSummonOccupancyMode.AdditionalEntity
                    || !reachableActionDepths.TryGetValue(action.ActionId, out int ownerDepth))
                {
                    continue;
                }

                int childDepth = ownerDepth + 1;
                int longestCandidateChain = 0;
                for (int candidateIndex = 0;
                    candidateIndex < action.CandidateEnemyDefinitionIds.Count;
                    candidateIndex++)
                {
                    longestCandidateChain = Math.Max(
                        longestCandidateChain,
                        FindReplacementChainLength(
                            action.CandidateEnemyDefinitionIds[candidateIndex],
                            childDepth,
                            actions,
                            summonFailureThreshold));
                }

                replacementUpperBound = AddPopulationReplacementUpperBound(
                    replacementUpperBound,
                    action.MaxTotalSummonsPerEncounter,
                    longestCandidateChain,
                    summonFailureThreshold);
            }

            summonUpperBound = Math.Min(
                summonFailureThreshold,
                SaturatingAdd(
                    gameplayQuotaSummonUpperBound,
                    replacementUpperBound));
            requiredSummonRecursionDepth = FindRequiredSummonRecursionDepth(
                plannedEntries,
                actions);
            BuildEnemyPoolRequirements(
                plannedEntries,
                actions,
                reachableActionDepths,
                out enemyPoolRequirements,
                out _);
            return true;
        }

        private static void LimitEnemyPoolRequirements(
            IReadOnlyList<FpgEnemyPoolCapacityRequirement> source,
            int simultaneousCombatants,
            out IReadOnlyList<FpgEnemyPoolCapacityRequirement> requirements,
            out int totalSlots)
        {
            if (source == null || source.Count == 0 || simultaneousCombatants <= 0)
            {
                requirements = Array.Empty<FpgEnemyPoolCapacityRequirement>();
                totalSlots = 0;
                return;
            }

            FpgEnemyPoolCapacityRequirement[] limited =
                new FpgEnemyPoolCapacityRequirement[source.Count];
            totalSlots = 0;
            for (int index = 0; index < source.Count; index++)
            {
                FpgEnemyPoolCapacityRequirement candidate = source[index];
                int count = Math.Min(candidate.Count, simultaneousCombatants);
                limited[index] = new FpgEnemyPoolCapacityRequirement(
                    candidate.EnemyDefinitionId,
                    count);
                totalSlots = SaturatingAdd(totalSlots, count);
            }

            requirements = limited;
        }

        private static void BuildEnemyPoolRequirements(
            IReadOnlyList<FpgSpawnEntry> plannedEntries,
            IReadOnlyList<FpgSummonActionData> actions,
            Dictionary<string, int> reachableActionDepths,
            out IReadOnlyList<FpgEnemyPoolCapacityRequirement> requirements,
            out int totalSlots)
        {
            Dictionary<DefinitionDepth, int> plannedPopulations =
                new Dictionary<DefinitionDepth, int>();
            for (int entryIndex = 0; entryIndex < plannedEntries.Count; entryIndex++)
            {
                FpgSpawnEntry entry = plannedEntries[entryIndex];
                DefinitionDepth source = new DefinitionDepth(
                    entry.EnemyDefinitionId,
                    entry.RecursionDepth);
                plannedPopulations.TryGetValue(source, out int population);
                plannedPopulations[source] = SaturatingAdd(population, 1);
            }

            Dictionary<string, int> counts =
                new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<DefinitionDepth, int> source in plannedPopulations)
            {
                AddPopulationPoolRequirements(
                    new[] { source.Key },
                    source.Value,
                    actions,
                    counts);
            }

            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                FpgSummonActionData action = actions[actionIndex];
                if (action.OccupancyMode != FpgSummonOccupancyMode.AdditionalEntity
                    || !reachableActionDepths.TryGetValue(
                        action.ActionId,
                        out int ownerDepth))
                {
                    continue;
                }

                DefinitionDepth[] roots =
                    new DefinitionDepth[action.CandidateEnemyDefinitionIds.Count];
                for (int candidateIndex = 0; candidateIndex < roots.Length; candidateIndex++)
                {
                    roots[candidateIndex] = new DefinitionDepth(
                        action.CandidateEnemyDefinitionIds[candidateIndex],
                        ownerDepth + 1);
                }

                AddPopulationPoolRequirements(
                    roots,
                    action.MaxTotalSummonsPerEncounter,
                    actions,
                    counts);
            }

            List<string> definitionIds = new List<string>(counts.Keys);
            definitionIds.Sort(StringComparer.Ordinal);
            FpgEnemyPoolCapacityRequirement[] values =
                new FpgEnemyPoolCapacityRequirement[definitionIds.Count];
            totalSlots = 0;
            for (int index = 0; index < definitionIds.Count; index++)
            {
                string definitionId = definitionIds[index];
                int count = counts[definitionId];
                values[index] = new FpgEnemyPoolCapacityRequirement(
                    definitionId,
                    count);
                totalSlots = SaturatingAdd(totalSlots, count);
            }

            requirements = values;
        }

        private static void AddPopulationPoolRequirements(
            IReadOnlyList<DefinitionDepth> roots,
            int population,
            IReadOnlyList<FpgSummonActionData> actions,
            Dictionary<string, int> counts)
        {
            if (population <= 0 || roots == null || roots.Count == 0)
            {
                return;
            }

            Queue<DefinitionDepth> pending = new Queue<DefinitionDepth>();
            HashSet<DefinitionDepth> visitedStates = new HashSet<DefinitionDepth>();
            HashSet<string> reachableDefinitions =
                new HashSet<string>(StringComparer.Ordinal);
            for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++)
            {
                if (visitedStates.Add(roots[rootIndex]))
                {
                    pending.Enqueue(roots[rootIndex]);
                }
            }

            while (pending.Count > 0)
            {
                DefinitionDepth current = pending.Dequeue();
                reachableDefinitions.Add(current.DefinitionId);
                for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    FpgSummonActionData action = actions[actionIndex];
                    if (action.OccupancyMode != FpgSummonOccupancyMode.ReplaceOwner
                        || current.Depth >= action.MaxRecursionDepth
                        || !ContainsId(
                            action.OwnerEnemyDefinitionIds,
                            current.DefinitionId))
                    {
                        continue;
                    }

                    for (int candidateIndex = 0;
                        candidateIndex < action.CandidateEnemyDefinitionIds.Count;
                        candidateIndex++)
                    {
                        DefinitionDepth child = new DefinitionDepth(
                            action.CandidateEnemyDefinitionIds[candidateIndex],
                            current.Depth + 1);
                        if (visitedStates.Add(child))
                        {
                            pending.Enqueue(child);
                        }
                    }
                }
            }

            foreach (string definitionId in reachableDefinitions)
            {
                counts.TryGetValue(definitionId, out int current);
                counts[definitionId] = SaturatingAdd(current, population);
            }
        }

        private static int FindRequiredSummonRecursionDepth(
            IReadOnlyList<FpgSpawnEntry> plannedEntries,
            IReadOnlyList<FpgSummonActionData> actions)
        {
            Queue<DefinitionDepth> pending = new Queue<DefinitionDepth>();
            HashSet<DefinitionDepth> visited = new HashSet<DefinitionDepth>();
            for (int entryIndex = 0; entryIndex < plannedEntries.Count; entryIndex++)
            {
                FpgSpawnEntry entry = plannedEntries[entryIndex];
                DefinitionDepth root = new DefinitionDepth(
                    entry.EnemyDefinitionId,
                    entry.RecursionDepth);
                if (visited.Add(root))
                {
                    pending.Enqueue(root);
                }
            }

            int requiredDepth = 0;
            while (pending.Count > 0)
            {
                DefinitionDepth current = pending.Dequeue();
                for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    FpgSummonActionData action = actions[actionIndex];
                    if (current.Depth >= action.MaxRecursionDepth
                        || !ContainsId(
                            action.OwnerEnemyDefinitionIds,
                            current.DefinitionId))
                    {
                        continue;
                    }

                    int childDepth = current.Depth + 1;
                    requiredDepth = Math.Max(requiredDepth, childDepth);
                    for (int candidateIndex = 0;
                        candidateIndex < action.CandidateEnemyDefinitionIds.Count;
                        candidateIndex++)
                    {
                        DefinitionDepth child = new DefinitionDepth(
                            action.CandidateEnemyDefinitionIds[candidateIndex],
                            childDepth);
                        if (visited.Add(child))
                        {
                            pending.Enqueue(child);
                        }
                    }
                }
            }

            return requiredDepth;
        }

        private readonly struct DefinitionDepth : IEquatable<DefinitionDepth>
        {
            public DefinitionDepth(string definitionId, int depth)
            {
                DefinitionId = definitionId ?? string.Empty;
                Depth = depth;
            }

            public string DefinitionId { get; }
            public int Depth { get; }

            public bool Equals(DefinitionDepth other)
            {
                return Depth == other.Depth
                    && string.Equals(
                        DefinitionId,
                        other.DefinitionId,
                        StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is DefinitionDepth other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (StringComparer.Ordinal.GetHashCode(DefinitionId) * 397)
                        ^ Depth;
                }
            }
        }

        private static int FindMinimumOwnerDepth(
            FpgSummonActionData action,
            Dictionary<string, int> reachableDepths)
        {
            int ownerDepth = int.MaxValue;
            for (int ownerIndex = 0; ownerIndex < action.OwnerEnemyDefinitionIds.Count; ownerIndex++)
            {
                if (reachableDepths.TryGetValue(
                        action.OwnerEnemyDefinitionIds[ownerIndex],
                        out int candidateDepth))
                {
                    ownerDepth = Math.Min(ownerDepth, candidateDepth);
                }
            }

            return ownerDepth;
        }

        private static bool TryValidateAcyclicSummonGraph(
            IReadOnlyList<FpgSummonActionData> actions,
            out string error)
        {
            Dictionary<string, HashSet<string>> edges =
                new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            Dictionary<string, int> incoming =
                new Dictionary<string, int>(StringComparer.Ordinal);
            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                FpgSummonActionData action = actions[actionIndex];
                for (int ownerIndex = 0;
                    ownerIndex < action.OwnerEnemyDefinitionIds.Count;
                    ownerIndex++)
                {
                    string ownerId = action.OwnerEnemyDefinitionIds[ownerIndex];
                    if (!edges.TryGetValue(ownerId, out HashSet<string> candidates))
                    {
                        candidates = new HashSet<string>(StringComparer.Ordinal);
                        edges.Add(ownerId, candidates);
                    }

                    if (!incoming.ContainsKey(ownerId))
                    {
                        incoming.Add(ownerId, 0);
                    }

                    for (int candidateIndex = 0;
                        candidateIndex < action.CandidateEnemyDefinitionIds.Count;
                        candidateIndex++)
                    {
                        string candidateId =
                            action.CandidateEnemyDefinitionIds[candidateIndex];
                        if (!incoming.ContainsKey(candidateId))
                        {
                            incoming.Add(candidateId, 0);
                        }

                        if (candidates.Add(candidateId))
                        {
                            incoming[candidateId]++;
                        }
                    }
                }
            }

            Queue<string> roots = new Queue<string>();
            foreach (KeyValuePair<string, int> pair in incoming)
            {
                if (pair.Value == 0)
                {
                    roots.Enqueue(pair.Key);
                }
            }

            int visited = 0;
            while (roots.Count > 0)
            {
                string ownerId = roots.Dequeue();
                visited++;
                if (!edges.TryGetValue(ownerId, out HashSet<string> candidates))
                {
                    continue;
                }

                foreach (string candidateId in candidates)
                {
                    int remaining = incoming[candidateId] - 1;
                    incoming[candidateId] = remaining;
                    if (remaining == 0)
                    {
                        roots.Enqueue(candidateId);
                    }
                }
            }

            if (visited != incoming.Count)
            {
                error = "Summon graph contains a cycle.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static int FindReplacementChainLength(
            string rootDefinitionId,
            int recursionDepth,
            IReadOnlyList<FpgSummonActionData> actions,
            int resultCap)
        {
            if (string.IsNullOrWhiteSpace(rootDefinitionId)
                || recursionDepth < 0 || resultCap <= 0)
            {
                return 0;
            }

            HashSet<string> current = new HashSet<string>(StringComparer.Ordinal)
            {
                rootDefinitionId
            };
            HashSet<string> next = new HashSet<string>(StringComparer.Ordinal);
            int depth = recursionDepth;
            int chainLength = 0;
            while (chainLength < resultCap)
            {
                next.Clear();
                for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    FpgSummonActionData action = actions[actionIndex];
                    if (action.OccupancyMode != FpgSummonOccupancyMode.ReplaceOwner
                        || depth >= action.MaxRecursionDepth
                        || !ContainsAnyId(current, action.OwnerEnemyDefinitionIds))
                    {
                        continue;
                    }

                    for (int candidateIndex = 0;
                        candidateIndex < action.CandidateEnemyDefinitionIds.Count;
                        candidateIndex++)
                    {
                        next.Add(action.CandidateEnemyDefinitionIds[candidateIndex]);
                    }
                }

                if (next.Count == 0)
                {
                    break;
                }

                HashSet<string> swap = current;
                current = next;
                next = swap;
                chainLength++;
                depth++;
            }

            return chainLength;
        }

        private static bool ContainsAnyId(
            HashSet<string> currentDefinitions,
            IReadOnlyList<string> expectedDefinitions)
        {
            for (int index = 0; index < expectedDefinitions.Count; index++)
            {
                if (currentDefinitions.Contains(expectedDefinitions[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static int AddPopulationReplacementUpperBound(
            int current,
            int population,
            int chainLength,
            int resultCap)
        {
            return Math.Min(
                resultCap,
                SaturatingAdd(
                    current,
                    SaturatingMultiply(population, chainLength)));
        }

        private static int FindSummonFailureThreshold(
            int entityCapacity,
            int plannedEnemyCount)
        {
            if (entityCapacity <= plannedEnemyCount)
            {
                return 1;
            }

            int available = entityCapacity - plannedEnemyCount;
            return available == int.MaxValue ? int.MaxValue : available + 1;
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

        private static int SaturatingMultiply(int left, int right)
        {
            if (left <= 0 || right <= 0)
            {
                return 0;
            }

            return left > int.MaxValue / right
                ? int.MaxValue
                : left * right;
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
