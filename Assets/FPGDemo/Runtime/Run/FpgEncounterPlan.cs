using System;
using System.Collections.Generic;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    public readonly struct FpgSpawnEntry
    {
        public FpgSpawnEntry(
            string spawnEntryId,
            string enemyDefinitionId,
            int waveIndex,
            int spawnSequence,
            int spawnCost,
            int capWeight,
            FpgEnemyRole role,
            bool forced,
            bool themeEnemy,
            bool overBudget,
            int recursionDepth = 0)
        {
            if (string.IsNullOrWhiteSpace(spawnEntryId)
                || string.IsNullOrWhiteSpace(enemyDefinitionId))
            {
                throw new ArgumentException("Spawn and enemy ids are required.");
            }

            if (waveIndex < 0 || spawnSequence < 0 || spawnCost <= 0 || capWeight <= 0
                || recursionDepth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex));
            }

            SpawnEntryId = spawnEntryId;
            EnemyDefinitionId = enemyDefinitionId;
            WaveIndex = waveIndex;
            SpawnSequence = spawnSequence;
            SpawnCost = spawnCost;
            CapWeight = capWeight;
            Role = role;
            Forced = forced;
            ThemeEnemy = themeEnemy;
            OverBudget = overBudget;
            RecursionDepth = recursionDepth;
        }

        public string SpawnEntryId { get; }
        public string EnemyDefinitionId { get; }
        public int WaveIndex { get; }
        public int SpawnSequence { get; }
        public int SpawnCost { get; }
        public int CapWeight { get; }
        public FpgEnemyRole Role { get; }
        public bool Forced { get; }
        public bool ThemeEnemy { get; }
        public bool OverBudget { get; }
        public int RecursionDepth { get; }
    }

    public sealed class FpgEncounterWavePlan
    {
        internal FpgEncounterWavePlan(
            int waveIndex,
            int budget,
            int requestedBudget,
            bool clipped,
            IReadOnlyList<FpgSpawnEntry> entries,
            string diagnostic,
            int budgetShareBasisPoints = 0)
        {
            WaveIndex = waveIndex;
            Budget = budget;
            RequestedBudget = requestedBudget;
            Clipped = clipped;
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
            Diagnostic = diagnostic ?? string.Empty;
            BudgetShareBasisPoints = budgetShareBasisPoints;
        }

        public int WaveIndex { get; }
        public int Budget { get; }
        public int RequestedBudget { get; }
        public bool Clipped { get; }
        public IReadOnlyList<FpgSpawnEntry> Entries { get; }
        public string Diagnostic { get; }
        public int BudgetShareBasisPoints { get; }
        public int EntryCount => Entries.Count;

        public int TotalCost
        {
            get
            {
                int total = 0;
                for (int index = 0; index < Entries.Count; index++)
                {
                    total = SaturatingAdd(total, Entries[index].SpawnCost);
                }

                return total;
            }
        }

        public int TotalCapWeight
        {
            get
            {
                int total = 0;
                for (int index = 0; index < Entries.Count; index++)
                {
                    total = SaturatingAdd(total, Entries[index].CapWeight);
                }

                return total;
            }
        }

        private static int SaturatingAdd(int left, int right)
        {
            return right > int.MaxValue - left ? int.MaxValue : left + right;
        }
    }

    public sealed class FpgEncounterPlan
    {
        private readonly FpgSpawnEntry[] allEntries;

        internal FpgEncounterPlan(
            string roomDefinitionId,
            FpgEncounterRunContext runContext,
            int totalBudget,
            IReadOnlyList<FpgEncounterWavePlan> waves,
            IReadOnlyList<string> diagnostics,
            ulong digest,
            string themeEnemyDefinitionId,
            string waveLayoutId = null,
            IReadOnlyList<FpgWaveBudgetShare> waveBudgetShares = null)
        {
            RoomDefinitionId = roomDefinitionId ?? string.Empty;
            RunContext = runContext;
            TotalBudget = totalBudget;
            Waves = waves == null ? Array.Empty<FpgEncounterWavePlan>() : new List<FpgEncounterWavePlan>(waves).ToArray();
            Diagnostics = diagnostics == null ? Array.Empty<string>() : new List<string>(diagnostics).ToArray();
            Digest = digest;
            ThemeEnemyDefinitionId = themeEnemyDefinitionId ?? string.Empty;
            WaveLayoutId = waveLayoutId ?? string.Empty;
            WaveBudgetShares = waveBudgetShares == null
                ? Array.Empty<FpgWaveBudgetShare>()
                : new List<FpgWaveBudgetShare>(waveBudgetShares).ToArray();

            int count = 0;
            for (int wave = 0; wave < Waves.Count; wave++)
            {
                count += Waves[wave].Entries.Count;
            }

            allEntries = new FpgSpawnEntry[count];
            int cursor = 0;
            for (int wave = 0; wave < Waves.Count; wave++)
            {
                IReadOnlyList<FpgSpawnEntry> entries = Waves[wave].Entries;
                for (int index = 0; index < entries.Count; index++)
                {
                    allEntries[cursor++] = entries[index];
                }
            }
        }

        public string RoomDefinitionId { get; }
        public FpgEncounterRunContext RunContext { get; }
        public int TotalBudget { get; }
        public IReadOnlyList<FpgEncounterWavePlan> Waves { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public ulong Digest { get; }
        public string ThemeEnemyDefinitionId { get; }
        public string WaveLayoutId { get; }
        public IReadOnlyList<FpgWaveBudgetShare> WaveBudgetShares { get; }
        public IReadOnlyList<FpgSpawnEntry> AllEntries => allEntries;
        public int WaveCount => Waves.Count;
        public int EntryCount => allEntries.Length;

        public bool TryGetWave(int waveIndex, out FpgEncounterWavePlan wave)
        {
            if (waveIndex < 0 || waveIndex >= Waves.Count)
            {
                wave = null;
                return false;
            }

            wave = Waves[waveIndex];
            return true;
        }
    }

    public readonly struct FpgEncounterPlanGenerationResult
    {
        public FpgEncounterPlanGenerationResult(
            DomainResult result,
            FpgEncounterPlan plan,
            string error)
        {
            Result = result;
            Plan = plan;
            Error = error ?? string.Empty;
        }

        public DomainResult Result { get; }
        public FpgEncounterPlan Plan { get; }
        public string Error { get; }
        public bool IsSuccess => Result.IsSuccess && Plan != null;
    }

    /// <summary>
    /// Public entry point for the deterministic Hades-style planner.
    /// </summary>
    public static class FpgEncounterPlanGenerator
    {
        public static FpgEncounterPlanGenerationResult Generate(FpgRoomRunRequest request)
        {
            return FpgEncounterPlanAlgorithm.Generate(request);
        }

        public static FpgEncounterPlan CreateBattleTestSandbox(
            string roomDefinitionId,
            FpgEncounterRunContext runContext)
        {
            if (string.IsNullOrWhiteSpace(roomDefinitionId))
            {
                throw new ArgumentException(
                    "Sandbox plans require a room definition id.",
                    nameof(roomDefinitionId));
            }

            if (!runContext.IsValid)
            {
                throw new ArgumentException(
                    "Sandbox plans require a valid run context.",
                    nameof(runContext));
            }

            FpgEncounterWavePlan emptyWave = new FpgEncounterWavePlan(
                waveIndex: 0,
                budget: 0,
                requestedBudget: 0,
                clipped: false,
                entries: Array.Empty<FpgSpawnEntry>(),
                diagnostic: "BattleTest sandbox wave.");
            return new FpgEncounterPlan(
                roomDefinitionId,
                runContext,
                totalBudget: 0,
                waves: new[] { emptyWave },
                diagnostics: new[] { "BattleTest sandbox plan has no authored wave entries." },
                digest: StableHash.Combine(
                    runContext.DeriveSeed(0x424154544C455354UL),
                    0x53414E44424F5855UL,
                    0UL,
                    0UL),
                themeEnemyDefinitionId: string.Empty,
                waveLayoutId: "battle-test-sandbox");
        }
    }
}
