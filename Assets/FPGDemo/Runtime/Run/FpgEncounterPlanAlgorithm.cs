using System;
using System.Collections.Generic;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    /// <summary>
    /// Deterministic Hades-style planner used by FpgEncounterPlanGenerator.
    /// Layout, theme, and type-budget choices intentionally use separate
    /// random domains.
    /// </summary>
    internal static class FpgEncounterPlanAlgorithm
    {
        private const ulong PlanDomain = 0x4650475F504C414EUL;
        private const ulong WaveLayoutDomain = 0x4650475F57415645UL;
        private const ulong ThemeDomain = 0x4650475F5448454DUL;
        private const ulong EnemyBudgetDomain = 0x4650475F454E4255UL;
        private const int MaxBudgetAllocationDraws = 65536;
        private const int MaxPlanEntries = 65536;

        public static FpgEncounterPlanGenerationResult Generate(FpgRoomRunRequest request)
        {
            if (!request.IsValid)
            {
                return Failure(RejectReason.InvalidDefinition, "Room request is incomplete or has an invalid run context.");
            }

            FpgEncounterProfileData profile = request.EncounterProfile.Data;
            if (profile == null)
            {
                return Failure(RejectReason.InvalidDefinition, "Encounter profile is missing.");
            }

            if (!profile.TryValidate(out string profileError))
            {
                return Failure(RejectReason.InvalidDefinition, profileError ?? "Encounter profile is invalid.");
            }

            FpgEncounterOverrideData overrideData = request.EncounterOverride == null
                ? new FpgEncounterOverrideData()
                : request.EncounterOverride.Data;
            if (overrideData == null)
            {
                return Failure(RejectReason.InvalidDefinition, "Encounter override is invalid.");
            }

            if (request.RoomDefinition.SpawnPointCount <= 0)
            {
                return Failure(RejectReason.BufferCapacity, "Room has no enemy spawn points.");
            }

            int requiredWaveCount = GetRequiredWaveCount(overrideData);
            FpgWaveLayoutData layout = SelectWaveLayout(
                profile.WaveLayouts,
                requiredWaveCount,
                request.RunContext,
                out long layoutRoll,
                out long layoutWeight);
            if (layout == null)
            {
                return Failure(
                    RejectReason.InvalidDefinition,
                    "No configured wave layout can contain every fixed spawn wave.");
            }

            List<FpgEnemyPoolEntryData> candidates = BuildCandidates(
                profile.EnemyPool,
                overrideData,
                request.RunContext.Depth);
            if (candidates.Count == 0)
            {
                return Failure(RejectReason.InvalidDefinition, "No enemy pool entry is eligible at this depth.");
            }

            int totalBudget;
            try
            {
                totalBudget = overrideData.LockedBudget
                    ?? profile.CalculateBudget(
                        request.RunContext.Depth,
                        request.RunContext.DifficultyMultiplierBasisPoints);
            }
            catch (Exception exception)
            {
                return Failure(RejectReason.InvalidDefinition, exception.Message);
            }

            if (totalBudget < 0)
            {
                return Failure(RejectReason.InvalidDefinition, "Encounter budget cannot be negative.");
            }

            int themeIndex = SelectThemeEnemy(
                candidates,
                request.RunContext,
                out long themeRoll,
                out long themeWeight);
            string themeEnemyId = themeIndex < 0
                ? string.Empty
                : candidates[themeIndex].Definition.EnemyDefinitionId;

            List<string> diagnostics = new List<string>();
            diagnostics.Add(
                "Wave layout: " + layout.LayoutId
                + " roll=" + layoutRoll + "/" + layoutWeight
                + " shares=" + FormatShares(layout.BudgetShares));
            diagnostics.Add(themeIndex < 0
                ? "Theme enemy: none"
                : "Theme enemy: " + themeEnemyId + " roll=" + themeRoll + "/" + themeWeight);

            int waveCount = layout.WaveCount;
            List<FpgEncounterWavePlan> waves = new List<FpgEncounterWavePlan>(waveCount);
            int[] roomCounts = new int[candidates.Count];
            int[,] waveCounts = new int[waveCount, candidates.Count];
            int globalSpawnSequence = 0;
            int allocatedBudget = 0;
            int shareRemainder = 0;

            for (int waveIndex = 0; waveIndex < waveCount; waveIndex++)
            {
                FpgWaveBudgetShare share = layout.BudgetShares[waveIndex];
                int requestedBudget = CalculateWaveBudget(
                    totalBudget,
                    share.BasisPoints,
                    waveIndex,
                    waveCount,
                    allocatedBudget,
                    ref shareRemainder);
                allocatedBudget = SaturatingAdd(allocatedBudget, requestedBudget);

                List<FpgSpawnEntry> entries = new List<FpgSpawnEntry>();
                List<string> waveDiagnostics = new List<string>();
                int spent = 0;
                bool clipped = false;

                if (overrideData.Mode == FpgEncounterOverrideMode.FixedWaves)
                {
                    AppendFixedWaveEntries(
                        overrideData,
                        waveIndex,
                        candidates,
                        roomCounts,
                        waveCounts,
                        themeEnemyId,
                        ref globalSpawnSequence,
                        entries,
                        ref spent,
                        requestedBudget,
                        ref clipped,
                        waveDiagnostics);
                }
                else
                {
                    AppendForcedEntries(
                        overrideData,
                        waveIndex,
                        waveCount,
                        candidates,
                        roomCounts,
                        waveCounts,
                        themeEnemyId,
                        ref globalSpawnSequence,
                        entries,
                        ref spent,
                        requestedBudget,
                        ref clipped,
                        waveDiagnostics);

                    AllocateGeneratedWave(
                        request.RunContext,
                        waveIndex,
                        requestedBudget,
                        themeIndex,
                        candidates,
                        roomCounts,
                        waveCounts,
                        ref globalSpawnSequence,
                        entries,
                        ref spent,
                        ref clipped,
                        waveDiagnostics);
                }

                if (spent < requestedBudget)
                {
                    clipped = true;
                    waveDiagnostics.Add("budget clipped " + spent + "/" + requestedBudget);
                }
                else if (spent > requestedBudget)
                {
                    waveDiagnostics.Add("budget overrun " + spent + "/" + requestedBudget);
                }

                string waveDiagnostic = string.Join("; ", waveDiagnostics);
                if (!string.IsNullOrEmpty(waveDiagnostic))
                {
                    diagnostics.Add("Wave " + waveIndex + " " + waveDiagnostic);
                }

                waves.Add(new FpgEncounterWavePlan(
                    waveIndex,
                    spent,
                    requestedBudget,
                    clipped,
                    entries.ToArray(),
                    waveDiagnostic,
                    share.BasisPoints));
            }

            ulong digest = BuildDigest(
                request,
                totalBudget,
                layout,
                waves,
                themeEnemyId,
                diagnostics);
            FpgEncounterPlan plan = new FpgEncounterPlan(
                request.RoomDefinition.RoomDefinitionId,
                request.RunContext,
                totalBudget,
                waves,
                diagnostics,
                digest,
                themeEnemyId,
                layout.LayoutId,
                layout.BudgetShares);
            return new FpgEncounterPlanGenerationResult(DomainResult.Success, plan, string.Empty);
        }

        private static List<FpgEnemyPoolEntryData> BuildCandidates(
            IReadOnlyList<FpgEnemyPoolEntryData> pool,
            FpgEncounterOverrideData overrideData,
            int depth)
        {
            List<FpgEnemyPoolEntryData> candidates = new List<FpgEnemyPoolEntryData>(pool.Count);
            for (int index = 0; index < pool.Count; index++)
            {
                FpgEnemyPoolEntryData candidate = pool[index];
                if (candidate.Definition == null
                    || !candidate.IsAvailableAtDepth(depth)
                    || overrideData.IsExcluded(candidate.Definition.EnemyDefinitionId))
                {
                    continue;
                }

                candidates.Add(candidate);
            }

            candidates.Sort((left, right) => string.CompareOrdinal(
                left.Definition.EnemyDefinitionId,
                right.Definition.EnemyDefinitionId));
            return candidates;
        }

        private static int GetRequiredWaveCount(FpgEncounterOverrideData overrideData)
        {
            int count = 1;
            for (int index = 0; index < overrideData.FixedSpawns.Count; index++)
            {
                count = Math.Max(count, overrideData.FixedSpawns[index].WaveIndex + 1);
            }

            return count;
        }

        private static FpgWaveLayoutData SelectWaveLayout(
            IReadOnlyList<FpgWaveLayoutData> configuredLayouts,
            int requiredWaveCount,
            FpgEncounterRunContext context,
            out long roll,
            out long totalWeight)
        {
            List<FpgWaveLayoutData> eligible = new List<FpgWaveLayoutData>();
            totalWeight = 0L;
            for (int index = 0; index < configuredLayouts.Count; index++)
            {
                FpgWaveLayoutData layout = configuredLayouts[index];
                if (layout == null || layout.WaveCount < requiredWaveCount)
                {
                    continue;
                }

                eligible.Add(layout);
                totalWeight += layout.SelectionWeight;
            }

            eligible.Sort((left, right) => string.CompareOrdinal(left.LayoutId, right.LayoutId));
            if (totalWeight <= 0L)
            {
                roll = 0L;
                return null;
            }

            ulong random = context.DeriveSeed(
                WaveLayoutDomain,
                unchecked((ulong)requiredWaveCount),
                0UL);
            roll = (long)(random % (ulong)totalWeight);
            long cursor = roll;
            for (int index = 0; index < eligible.Count; index++)
            {
                cursor -= eligible[index].SelectionWeight;
                if (cursor < 0L)
                {
                    return eligible[index];
                }
            }

            return eligible[eligible.Count - 1];
        }

        private static int SelectThemeEnemy(
            List<FpgEnemyPoolEntryData> candidates,
            FpgEncounterRunContext context,
            out long roll,
            out long totalWeight)
        {
            totalWeight = 0L;
            for (int index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].ThemeEligible)
                {
                    totalWeight += candidates[index].SelectionWeight;
                }
            }

            if (totalWeight <= 0L)
            {
                roll = 0L;
                return -1;
            }

            ulong random = context.DeriveSeed(ThemeDomain, 0UL, 0UL);
            roll = (long)(random % (ulong)totalWeight);
            long cursor = roll;
            for (int index = 0; index < candidates.Count; index++)
            {
                FpgEnemyPoolEntryData candidate = candidates[index];
                if (!candidate.ThemeEligible)
                {
                    continue;
                }

                cursor -= candidate.SelectionWeight;
                if (cursor < 0L)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int CalculateWaveBudget(
            int totalBudget,
            int shareBasisPoints,
            int waveIndex,
            int waveCount,
            int allocatedBudget,
            ref int shareRemainder)
        {
            if (waveIndex == waveCount - 1)
            {
                return Math.Max(0, totalBudget - allocatedBudget);
            }

            long weighted = (long)totalBudget * shareBasisPoints + shareRemainder;
            int budget = (int)(weighted / FpgEncounterRunContext.BasisPointsOne);
            shareRemainder = (int)(weighted % FpgEncounterRunContext.BasisPointsOne);
            return Math.Max(0, budget);
        }

        private static void AppendForcedEntries(
            FpgEncounterOverrideData overrideData,
            int waveIndex,
            int waveCount,
            List<FpgEnemyPoolEntryData> candidates,
            int[] roomCounts,
            int[,] waveCounts,
            string themeEnemyId,
            ref int globalSpawnSequence,
            List<FpgSpawnEntry> entries,
            ref int spent,
            int requestedBudget,
            ref bool clipped,
            List<string> diagnostics)
        {
            for (int forcedIndex = 0; forcedIndex < overrideData.ForcedEnemies.Count; forcedIndex++)
            {
                FpgForcedEnemyCount forced = overrideData.ForcedEnemies[forcedIndex];
                int candidateIndex = FindCandidate(candidates, forced.EnemyDefinitionId);
                if (candidateIndex < 0)
                {
                    diagnostics.Add("forced enemy unavailable " + forced.EnemyDefinitionId);
                    clipped = true;
                    continue;
                }

                int requestedCount = forced.Count / Math.Max(1, waveCount);
                if (waveIndex < forced.Count % Math.Max(1, waveCount))
                {
                    requestedCount++;
                }

                FpgEnemyPoolEntryData candidate = candidates[candidateIndex];
                int allowedCount = GetAllowedCount(
                    candidate,
                    candidateIndex,
                    waveIndex,
                    roomCounts,
                    waveCounts,
                    requestedCount,
                    globalSpawnSequence);
                if (allowedCount < requestedCount)
                {
                    diagnostics.Add(
                        "forced " + candidate.Definition.EnemyDefinitionId
                        + " clipped " + allowedCount + "/" + requestedCount);
                    clipped = true;
                }

                AddRepeatedEntries(
                    candidate,
                    candidateIndex,
                    waveIndex,
                    allowedCount,
                    themeEnemyId,
                    true,
                    requestedBudget,
                    roomCounts,
                    waveCounts,
                    ref globalSpawnSequence,
                    entries,
                    ref spent);
            }
        }

        private static void AppendFixedWaveEntries(
            FpgEncounterOverrideData overrideData,
            int waveIndex,
            List<FpgEnemyPoolEntryData> candidates,
            int[] roomCounts,
            int[,] waveCounts,
            string themeEnemyId,
            ref int globalSpawnSequence,
            List<FpgSpawnEntry> entries,
            ref int spent,
            int requestedBudget,
            ref bool clipped,
            List<string> diagnostics)
        {
            for (int fixedIndex = 0; fixedIndex < overrideData.FixedSpawns.Count; fixedIndex++)
            {
                FpgFixedSpawnSpec fixedSpawn = overrideData.FixedSpawns[fixedIndex];
                if (fixedSpawn.WaveIndex != waveIndex)
                {
                    continue;
                }

                int candidateIndex = FindCandidate(candidates, fixedSpawn.EnemyDefinitionId);
                if (candidateIndex < 0)
                {
                    diagnostics.Add("fixed enemy unavailable " + fixedSpawn.EnemyDefinitionId);
                    clipped = true;
                    continue;
                }

                FpgEnemyPoolEntryData candidate = candidates[candidateIndex];
                int allowedCount = GetAllowedCount(
                    candidate,
                    candidateIndex,
                    waveIndex,
                    roomCounts,
                    waveCounts,
                    fixedSpawn.Count,
                    globalSpawnSequence);
                if (allowedCount < fixedSpawn.Count)
                {
                    diagnostics.Add(
                        "fixed " + candidate.Definition.EnemyDefinitionId
                        + " clipped " + allowedCount + "/" + fixedSpawn.Count);
                    clipped = true;
                }

                AddRepeatedEntries(
                    candidate,
                    candidateIndex,
                    waveIndex,
                    allowedCount,
                    themeEnemyId,
                    true,
                    requestedBudget,
                    roomCounts,
                    waveCounts,
                    ref globalSpawnSequence,
                    entries,
                    ref spent);
            }
        }

        private static void AllocateGeneratedWave(
            FpgEncounterRunContext context,
            int waveIndex,
            int requestedBudget,
            int themeIndex,
            List<FpgEnemyPoolEntryData> candidates,
            int[] roomCounts,
            int[,] waveCounts,
            ref int globalSpawnSequence,
            List<FpgSpawnEntry> entries,
            ref int spent,
            ref bool clipped,
            List<string> diagnostics)
        {
            int candidateCount = candidates.Count;
            int[] typeBudgets = new int[candidateCount];
            int[] maximumTypeBudgets = new int[candidateCount];
            int[] firstAllocationOrdinals = new int[candidateCount];
            for (int index = 0; index < candidateCount; index++)
            {
                FpgEnemyPoolEntryData candidate = candidates[index];
                int remainingCount = GetAllowedCount(
                    candidate,
                    index,
                    waveIndex,
                    roomCounts,
                    waveCounts,
                    int.MaxValue,
                    globalSpawnSequence);
                maximumTypeBudgets[index] = SaturatingMultiply(
                    remainingCount,
                    candidate.Definition.SpawnCost);
                firstAllocationOrdinals[index] = int.MaxValue;
            }

            int remainingBudget = Math.Max(0, requestedBudget - spent);
            bool themeAlreadyPresent = themeIndex >= 0 && waveCounts[waveIndex, themeIndex] > 0;
            if (themeIndex >= 0 && requestedBudget > 0 && !themeAlreadyPresent)
            {
                int themeCapacityBudget = maximumTypeBudgets[themeIndex];
                if (themeCapacityBudget > 0)
                {
                    int themeCost = candidates[themeIndex].Definition.SpawnCost;
                    int reservedBudget = remainingBudget > 0
                        ? Math.Min(themeCost, remainingBudget)
                        : 1;
                    reservedBudget = Math.Min(reservedBudget, themeCapacityBudget);
                    typeBudgets[themeIndex] = reservedBudget;
                    firstAllocationOrdinals[themeIndex] = -1;
                    remainingBudget = Math.Max(0, remainingBudget - reservedBudget);
                    diagnostics.Add(
                        "theme reserve " + candidates[themeIndex].Definition.EnemyDefinitionId
                        + " budget=" + reservedBudget);
                }
                else
                {
                    diagnostics.Add(
                        "theme unavailable by limits "
                        + candidates[themeIndex].Definition.EnemyDefinitionId);
                }
            }

            ulong decisionHash = StableHash.Mix(
                EnemyBudgetDomain ^ unchecked((ulong)waveIndex));
            int allocationOrdinal = 0;
            while (remainingBudget > 0 && allocationOrdinal < MaxBudgetAllocationDraws)
            {
                int selectedIndex = SelectWeightedBudgetCandidate(
                    candidates,
                    typeBudgets,
                    maximumTypeBudgets,
                    waveIndex,
                    allocationOrdinal,
                    context,
                    out ulong random,
                    out long roll,
                    out long totalWeight);
                if (selectedIndex < 0)
                {
                    clipped = true;
                    break;
                }

                int drawsLeft = MaxBudgetAllocationDraws - allocationOrdinal;
                int quantum = CeilingDivide(remainingBudget, drawsLeft);
                int capacity = maximumTypeBudgets[selectedIndex] - typeBudgets[selectedIndex];
                int allocation = Math.Min(remainingBudget, Math.Min(quantum, capacity));
                if (allocation <= 0)
                {
                    clipped = true;
                    break;
                }

                if (firstAllocationOrdinals[selectedIndex] == int.MaxValue)
                {
                    firstAllocationOrdinals[selectedIndex] = allocationOrdinal;
                }

                typeBudgets[selectedIndex] = SaturatingAdd(
                    typeBudgets[selectedIndex],
                    allocation);
                remainingBudget -= allocation;
                decisionHash = StableHash.Append(decisionHash, random);
                decisionHash = StableHash.Append(decisionHash, unchecked((ulong)roll));
                decisionHash = StableHash.Append(decisionHash, unchecked((ulong)totalWeight));
                decisionHash = StableHash.Append(
                    decisionHash,
                    StableTextHash(candidates[selectedIndex].Definition.EnemyDefinitionId));
                decisionHash = StableHash.Append(decisionHash, unchecked((ulong)allocation));
                allocationOrdinal++;
            }

            if (remainingBudget > 0)
            {
                clipped = true;
                diagnostics.Add("unallocated type budget=" + remainingBudget);
            }

            diagnostics.Add(
                "allocation draws=" + allocationOrdinal
                + " decision=" + decisionHash.ToString("X16"));

            List<int> allocatedTypes = new List<int>();
            for (int index = 0; index < candidateCount; index++)
            {
                if (typeBudgets[index] > 0)
                {
                    allocatedTypes.Add(index);
                }
            }

            allocatedTypes.Sort((left, right) =>
            {
                if (left == themeIndex && right != themeIndex)
                {
                    return -1;
                }

                if (right == themeIndex && left != themeIndex)
                {
                    return 1;
                }

                int ordinalComparison = firstAllocationOrdinals[left].CompareTo(
                    firstAllocationOrdinals[right]);
                return ordinalComparison != 0
                    ? ordinalComparison
                    : string.CompareOrdinal(
                        candidates[left].Definition.EnemyDefinitionId,
                        candidates[right].Definition.EnemyDefinitionId);
            });

            for (int allocationIndex = 0; allocationIndex < allocatedTypes.Count; allocationIndex++)
            {
                int candidateIndex = allocatedTypes[allocationIndex];
                FpgEnemyPoolEntryData candidate = candidates[candidateIndex];
                int typeBudget = typeBudgets[candidateIndex];
                int requestedCount = CeilingDivide(
                    typeBudget,
                    candidate.Definition.SpawnCost);
                int allowedCount = GetAllowedCount(
                    candidate,
                    candidateIndex,
                    waveIndex,
                    roomCounts,
                    waveCounts,
                    requestedCount,
                    globalSpawnSequence);
                if (allowedCount < requestedCount)
                {
                    clipped = true;
                    diagnostics.Add(
                        "type " + candidate.Definition.EnemyDefinitionId
                        + " clipped " + allowedCount + "/" + requestedCount);
                }

                diagnostics.Add(
                    "type " + candidate.Definition.EnemyDefinitionId
                    + " budget=" + typeBudget
                    + " cost=" + candidate.Definition.SpawnCost
                    + " count=ceil(" + typeBudget + "/" + candidate.Definition.SpawnCost
                    + ")=" + requestedCount);
                AddRepeatedEntries(
                    candidate,
                    candidateIndex,
                    waveIndex,
                    allowedCount,
                    themeIndex == candidateIndex
                        ? candidate.Definition.EnemyDefinitionId
                        : string.Empty,
                    false,
                    requestedBudget,
                    roomCounts,
                    waveCounts,
                    ref globalSpawnSequence,
                    entries,
                    ref spent);
            }
        }

        private static int SelectWeightedBudgetCandidate(
            List<FpgEnemyPoolEntryData> candidates,
            int[] typeBudgets,
            int[] maximumTypeBudgets,
            int waveIndex,
            int ordinal,
            FpgEncounterRunContext context,
            out ulong random,
            out long roll,
            out long totalWeight)
        {
            totalWeight = 0L;
            for (int index = 0; index < candidates.Count; index++)
            {
                if (typeBudgets[index] < maximumTypeBudgets[index])
                {
                    totalWeight += candidates[index].SelectionWeight;
                }
            }

            if (totalWeight <= 0L)
            {
                random = 0UL;
                roll = 0L;
                return -1;
            }

            random = context.DeriveSeed(
                EnemyBudgetDomain,
                unchecked((ulong)waveIndex),
                unchecked((ulong)ordinal));
            roll = (long)(random % (ulong)totalWeight);
            long cursor = roll;
            for (int index = 0; index < candidates.Count; index++)
            {
                if (typeBudgets[index] >= maximumTypeBudgets[index])
                {
                    continue;
                }

                cursor -= candidates[index].SelectionWeight;
                if (cursor < 0L)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int GetAllowedCount(
            FpgEnemyPoolEntryData candidate,
            int candidateIndex,
            int waveIndex,
            int[] roomCounts,
            int[,] waveCounts,
            int requestedCount,
            int globalSpawnSequence)
        {
            int roomRemaining = Math.Max(0, candidate.MaxPerRoom - roomCounts[candidateIndex]);
            int waveRemaining = Math.Max(
                0,
                candidate.MaxPerWave - waveCounts[waveIndex, candidateIndex]);
            int planRemaining = Math.Max(0, MaxPlanEntries - globalSpawnSequence);
            return Math.Min(
                Math.Max(0, requestedCount),
                Math.Min(roomRemaining, Math.Min(waveRemaining, planRemaining)));
        }

        private static void AddRepeatedEntries(
            FpgEnemyPoolEntryData candidate,
            int candidateIndex,
            int waveIndex,
            int count,
            string themeEnemyId,
            bool forced,
            int requestedBudget,
            int[] roomCounts,
            int[,] waveCounts,
            ref int globalSpawnSequence,
            List<FpgSpawnEntry> entries,
            ref int spent)
        {
            bool themeEnemy = string.Equals(
                candidate.Definition.EnemyDefinitionId,
                themeEnemyId,
                StringComparison.Ordinal);
            for (int entryIndex = 0; entryIndex < count; entryIndex++)
            {
                bool overBudget = candidate.Definition.SpawnCost
                    > Math.Max(0, requestedBudget - spent);
                AddEntry(
                    candidate,
                    candidateIndex,
                    waveIndex,
                    forced,
                    themeEnemy,
                    overBudget,
                    roomCounts,
                    waveCounts,
                    ref globalSpawnSequence,
                    entries);
                spent = SaturatingAdd(spent, candidate.Definition.SpawnCost);
            }
        }

        private static void AddEntry(
            FpgEnemyPoolEntryData candidate,
            int candidateIndex,
            int waveIndex,
            bool forced,
            bool themeEnemy,
            bool overBudget,
            int[] roomCounts,
            int[,] waveCounts,
            ref int globalSpawnSequence,
            List<FpgSpawnEntry> entries)
        {
            int sequence = globalSpawnSequence++;
            string entryId = candidate.Definition.EnemyDefinitionId
                + "-w" + waveIndex + "-s" + sequence;
            entries.Add(new FpgSpawnEntry(
                entryId,
                candidate.Definition.EnemyDefinitionId,
                waveIndex,
                sequence,
                candidate.Definition.SpawnCost,
                candidate.Definition.CapWeight,
                candidate.Definition.Role,
                forced,
                themeEnemy,
                overBudget));
            roomCounts[candidateIndex] = SaturatingAdd(roomCounts[candidateIndex], 1);
            waveCounts[waveIndex, candidateIndex] = SaturatingAdd(
                waveCounts[waveIndex, candidateIndex],
                1);
        }

        private static int FindCandidate(
            List<FpgEnemyPoolEntryData> candidates,
            string enemyDefinitionId)
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                if (string.Equals(
                    candidates[index].Definition.EnemyDefinitionId,
                    enemyDefinitionId,
                    StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static string FormatShares(IReadOnlyList<FpgWaveBudgetShare> shares)
        {
            string[] values = new string[shares.Count];
            for (int index = 0; index < shares.Count; index++)
            {
                values[index] = shares[index].BasisPoints.ToString();
            }

            return string.Join("/", values);
        }

        private static ulong BuildDigest(
            FpgRoomRunRequest request,
            int totalBudget,
            FpgWaveLayoutData layout,
            List<FpgEncounterWavePlan> waves,
            string themeEnemyId,
            List<string> diagnostics)
        {
            ulong digest = StableHash.Mix(PlanDomain);
            digest = StableHash.Append(
                digest,
                StableTextHash(request.RoomDefinition.RoomDefinitionId));
            digest = request.RunContext.AppendStableHash(digest);
            digest = StableHash.Append(digest, unchecked((ulong)totalBudget));
            digest = StableHash.Append(digest, StableTextHash(layout.LayoutId));
            digest = StableHash.Append(digest, unchecked((ulong)layout.SelectionWeight));
            for (int shareIndex = 0; shareIndex < layout.BudgetShares.Count; shareIndex++)
            {
                digest = StableHash.Append(
                    digest,
                    unchecked((ulong)layout.BudgetShares[shareIndex].BasisPoints));
            }

            digest = StableHash.Append(digest, StableTextHash(themeEnemyId));
            for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                FpgEncounterWavePlan wave = waves[waveIndex];
                digest = StableHash.Append(digest, unchecked((ulong)wave.WaveIndex));
                digest = StableHash.Append(digest, unchecked((ulong)wave.BudgetShareBasisPoints));
                digest = StableHash.Append(digest, unchecked((ulong)wave.Budget));
                digest = StableHash.Append(digest, unchecked((ulong)wave.RequestedBudget));
                digest = StableHash.Append(digest, wave.Clipped ? 1UL : 0UL);
                for (int entryIndex = 0; entryIndex < wave.Entries.Count; entryIndex++)
                {
                    FpgSpawnEntry entry = wave.Entries[entryIndex];
                    digest = StableHash.Append(digest, StableTextHash(entry.SpawnEntryId));
                    digest = StableHash.Append(digest, StableTextHash(entry.EnemyDefinitionId));
                    digest = StableHash.Append(digest, unchecked((ulong)entry.SpawnSequence));
                    digest = StableHash.Append(digest, unchecked((ulong)entry.SpawnCost));
                    digest = StableHash.Append(digest, unchecked((ulong)entry.CapWeight));
                    digest = StableHash.Append(digest, unchecked((ulong)entry.Role));
                    digest = StableHash.Append(digest, entry.Forced ? 1UL : 0UL);
                    digest = StableHash.Append(digest, entry.ThemeEnemy ? 1UL : 0UL);
                    digest = StableHash.Append(digest, entry.OverBudget ? 1UL : 0UL);
                    digest = StableHash.Append(digest, unchecked((ulong)entry.RecursionDepth));
                }
            }

            for (int index = 0; index < diagnostics.Count; index++)
            {
                digest = StableHash.Append(digest, StableTextHash(diagnostics[index]));
            }

            return digest;
        }

        private static int CeilingDivide(int numerator, int denominator)
        {
            if (numerator <= 0 || denominator <= 0)
            {
                return 0;
            }

            return (int)(((long)numerator + denominator - 1L) / denominator);
        }

        private static int SaturatingMultiply(int left, int right)
        {
            if (left <= 0 || right <= 0)
            {
                return 0;
            }

            return left > int.MaxValue / right ? int.MaxValue : left * right;
        }

        private static int SaturatingAdd(int left, int right)
        {
            return right > int.MaxValue - left ? int.MaxValue : left + right;
        }

        private static ulong StableTextHash(string value)
        {
            ulong hash = StableHash.Mix(PlanDomain);
            if (string.IsNullOrEmpty(value))
            {
                return hash;
            }

            for (int index = 0; index < value.Length; index++)
            {
                hash = StableHash.Append(hash, value[index]);
            }

            return hash;
        }

        private static FpgEncounterPlanGenerationResult Failure(
            RejectReason reason,
            string error)
        {
            return new FpgEncounterPlanGenerationResult(
                DomainResult.Rejected(reason),
                null,
                error);
        }
    }
}
