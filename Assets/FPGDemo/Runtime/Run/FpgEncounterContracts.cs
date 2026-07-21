using System;
using System.Collections.Generic;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    /// <summary>
    /// Semantic role used by both encounter entries and authored room points.
    /// The Unity authoring layer can map its marker enum to this value without
    /// coupling the deterministic planner to UnityEngine.
    /// </summary>
    public enum FpgEnemyRole
    {
        Any = 0,
        Melee = 1,
        Ranged = 2,
        Support = 3
    }

    public enum FpgEncounterPhase
    {
        None = 0,
        Preparing,
        Warning,
        Spawning,
        Combat,
        WaveDelay,
        Cleared,
        Failed,
        Paused,
        Faulted,
        Disposed
    }

    public enum FpgEncounterFailureReason
    {
        None = 0,
        InvalidRequest,
        InvalidRunContext,
        InvalidProfile,
        InvalidPool,
        InvalidOverride,
        MissingSpawnPoint,
        SpawnPointCapacity,
        SpawnPointUnavailable,
        EntityCapacity,
        ThreatCapacity,
        ProjectileCapacity,
        InvalidSummonGraph,
        SynchronizerFault,
        External,
        Disposed
    }

    public enum FpgEncounterLifecycleEventType
    {
        Preparing = 0,
        Started,
        WarningStarted,
        EnemyQueued,
        EnemyActivated,
        EnemyDied,
        WaveStarted,
        WaveCleared,
        RoomCleared,
        ExitLocked,
        ExitUnlocked,
        Paused,
        Resumed,
        Restarted,
        Failed,
        Faulted,
        Disposed
    }

    public enum FpgSpawnEntryState
    {
        Planned = 0,
        Queued,
        Warning,
        Active,
        Dead,
        Canceled,
        Failed
    }

    public enum FpgRoomRunRequestKind
    {
        FormalEncounter = 0
    }

    /// <summary>
    /// All run-scoped values that participate in encounter generation.
    /// Difficulty is represented as integer basis points (10000 = 1.0x).
    /// </summary>
    public readonly struct FpgEncounterRunContext : IEquatable<FpgEncounterRunContext>
    {
        public const int BasisPointsOne = 10000;

        public FpgEncounterRunContext(
            ulong runSeed,
            string regionId,
            int depth,
            int difficultyMultiplierBasisPoints,
            int roomVisitOrdinal)
        {
            if (string.IsNullOrWhiteSpace(regionId))
            {
                throw new ArgumentException("Region id is required.", nameof(regionId));
            }

            if (depth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(depth));
            }

            if (difficultyMultiplierBasisPoints <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(difficultyMultiplierBasisPoints));
            }

            if (roomVisitOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(roomVisitOrdinal));
            }

            RunSeed = runSeed;
            RegionId = regionId;
            Depth = depth;
            DifficultyMultiplierBasisPoints = difficultyMultiplierBasisPoints;
            RoomVisitOrdinal = roomVisitOrdinal;
        }

        public ulong RunSeed { get; }
        public string RegionId { get; }
        public int Depth { get; }
        public int DifficultyMultiplierBasisPoints { get; }
        public int RoomVisitOrdinal { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(RegionId)
            && Depth >= 0
            && DifficultyMultiplierBasisPoints > 0
            && RoomVisitOrdinal >= 0;

        public ulong RegionHash => StableHash.Append(StableHash.Mix(0x4650475F52454749UL),
            StableStringHash(RegionId));

        public ulong DeriveSeed(ulong domain, ulong owner = 0UL, ulong ordinal = 0UL)
        {
            ulong contextOwner = StableHash.Combine(RegionHash, (ulong)RoomVisitOrdinal,
                unchecked((ulong)Depth), unchecked((ulong)DifficultyMultiplierBasisPoints));
            contextOwner = StableHash.Append(contextOwner, unchecked((ulong)RoomVisitOrdinal));
            return StableHash.Combine(RunSeed, domain, contextOwner ^ owner, ordinal);
        }

        public ulong AppendStableHash(ulong hash)
        {
            hash = StableHash.Append(hash, RunSeed);
            hash = StableHash.Append(hash, RegionHash);
            hash = StableHash.Append(hash, unchecked((ulong)Depth));
            hash = StableHash.Append(hash, unchecked((ulong)DifficultyMultiplierBasisPoints));
            return StableHash.Append(hash, unchecked((ulong)RoomVisitOrdinal));
        }

        public bool Equals(FpgEncounterRunContext other)
        {
            return RunSeed == other.RunSeed
                && string.Equals(RegionId, other.RegionId, StringComparison.Ordinal)
                && Depth == other.Depth
                && DifficultyMultiplierBasisPoints == other.DifficultyMultiplierBasisPoints
                && RoomVisitOrdinal == other.RoomVisitOrdinal;
        }

        public override bool Equals(object obj)
        {
            return obj is FpgEncounterRunContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RegionId == null ? 0 : StringComparer.Ordinal.GetHashCode(RegionId);
                hash = (hash * 397) ^ RunSeed.GetHashCode();
                hash = (hash * 397) ^ Depth;
                hash = (hash * 397) ^ DifficultyMultiplierBasisPoints;
                return (hash * 397) ^ RoomVisitOrdinal;
            }
        }

        public static bool operator ==(FpgEncounterRunContext left, FpgEncounterRunContext right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FpgEncounterRunContext left, FpgEncounterRunContext right)
        {
            return !left.Equals(right);
        }

        private static ulong StableStringHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0UL;
            }

            ulong hash = StableHash.Mix(0x4650475F53545231UL);
            for (int index = 0; index < value.Length; index++)
            {
                hash = StableHash.Append(hash, value[index]);
            }

            return hash;
        }
    }

    /// <summary>
    /// A room spawn point projected into the pure run layer. Position keys are
    /// authored deterministic coordinates; the Unity bridge performs the
    /// actual distance checks before passing candidates to the director.
    /// </summary>
    public readonly struct FpgSpawnPointCandidate
    {
        public FpgSpawnPointCandidate(
            string pointId,
            FpgEnemyRole role,
            long positionKey,
            int capacity = 1)
        {
            if (string.IsNullOrWhiteSpace(pointId))
            {
                throw new ArgumentException("Spawn point id is required.", nameof(pointId));
            }

            if (!Enum.IsDefined(typeof(FpgEnemyRole), role))
            {
                throw new ArgumentOutOfRangeException(nameof(role));
            }

            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            PointId = pointId;
            Role = role;
            PositionKey = positionKey;
            Capacity = capacity;
        }

        public string PointId { get; }
        public FpgEnemyRole Role { get; }
        public long PositionKey { get; }
        public int Capacity { get; }
    }

    public interface IFpgRoomDefinitionSource
    {
        string RoomDefinitionId { get; }
        int ExitCount { get; }
        int SpawnPointCount { get; }
        FpgSpawnPointCandidate GetSpawnPoint(int index);
    }

    public interface IFpgEncounterProfileSource
    {
        FpgEncounterProfileData Data { get; }
    }

    public interface IFpgEncounterOverrideSource
    {
        FpgEncounterOverrideData Data { get; }
    }

    /// <summary>
    /// The only request accepted by the formal room flow. Encounter assets do
    /// not live on the room; Unity adapters implement the source interfaces.
    /// </summary>
    public readonly struct FpgRoomRunRequest
    {
        public FpgRoomRunRequest(
            IFpgRoomDefinitionSource roomDefinition,
            IFpgEncounterProfileSource encounterProfile,
            IFpgEncounterOverrideSource encounterOverride,
            FpgEncounterRunContext runContext)
        {
            RoomDefinition = roomDefinition ?? throw new ArgumentNullException(nameof(roomDefinition));
            EncounterProfile = encounterProfile ?? throw new ArgumentNullException(nameof(encounterProfile));
            EncounterOverride = encounterOverride;
            RunContext = runContext;
        }

        public IFpgRoomDefinitionSource RoomDefinition { get; }
        public IFpgEncounterProfileSource EncounterProfile { get; }
        public IFpgEncounterOverrideSource EncounterOverride { get; }
        public FpgEncounterRunContext RunContext { get; }
        public FpgRoomRunRequestKind Kind => FpgRoomRunRequestKind.FormalEncounter;

        public bool IsValid => RoomDefinition != null
            && EncounterProfile != null
            && RunContext.IsValid;
    }

    public sealed class FpgEnemyDefinitionData
    {
        public FpgEnemyDefinitionData(
            string enemyDefinitionId,
            FpgEnemyRole role,
            int life,
            int breakValue,
            int spawnCost,
            int capWeight,
            int maxSummons = 0,
            int maxSummonDepth = 0,
            string entityViewKey = null,
            string behaviorKey = null,
            string attackPatternKey = null)
        {
            if (string.IsNullOrWhiteSpace(enemyDefinitionId))
            {
                throw new ArgumentException("Enemy definition id is required.", nameof(enemyDefinitionId));
            }

            if (!Enum.IsDefined(typeof(FpgEnemyRole), role))
            {
                throw new ArgumentOutOfRangeException(nameof(role));
            }

            if (life <= 0 || breakValue < 0 || spawnCost <= 0 || capWeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(life));
            }

            if (maxSummons < 0 || maxSummonDepth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSummons));
            }

            EnemyDefinitionId = enemyDefinitionId;
            Role = role;
            Life = life;
            Break = breakValue;
            SpawnCost = spawnCost;
            CapWeight = capWeight;
            MaxSummons = maxSummons;
            MaxSummonDepth = maxSummonDepth;
            EntityViewKey = entityViewKey ?? string.Empty;
            BehaviorKey = behaviorKey ?? string.Empty;
            AttackPatternKey = attackPatternKey ?? string.Empty;
        }

        public string EnemyDefinitionId { get; }
        public FpgEnemyRole Role { get; }
        public int Life { get; }
        public int Break { get; }
        public int SpawnCost { get; }
        public int CapWeight { get; }
        public int MaxSummons { get; }
        public int MaxSummonDepth { get; }
        public string EntityViewKey { get; }
        public string BehaviorKey { get; }
        public string AttackPatternKey { get; }

        public ulong AppendStableHash(ulong hash)
        {
            hash = StableHash.Append(hash, StableTextHash(EnemyDefinitionId));
            hash = StableHash.Append(hash, (ulong)Role);
            hash = StableHash.Append(hash, unchecked((ulong)Life));
            hash = StableHash.Append(hash, unchecked((ulong)Break));
            hash = StableHash.Append(hash, unchecked((ulong)SpawnCost));
            hash = StableHash.Append(hash, unchecked((ulong)CapWeight));
            hash = StableHash.Append(hash, unchecked((ulong)MaxSummons));
            return StableHash.Append(hash, unchecked((ulong)MaxSummonDepth));
        }

        private static ulong StableTextHash(string value)
        {
            ulong hash = StableHash.Mix(0x4650475F54455854UL);
            for (int index = 0; index < value.Length; index++)
            {
                hash = StableHash.Append(hash, value[index]);
            }

            return hash;
        }
    }

    public readonly struct FpgEnemyPoolEntryData
    {
        public FpgEnemyPoolEntryData(
            FpgEnemyDefinitionData definition,
            int selectionWeight,
            int minDepth = 0,
            int maxDepth = int.MaxValue,
            int maxPerWave = int.MaxValue,
            int maxPerRoom = int.MaxValue,
            bool themeEligible = true)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (selectionWeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(selectionWeight));
            }

            if (minDepth < 0 || maxDepth < minDepth || maxPerWave <= 0 || maxPerRoom <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minDepth));
            }

            SelectionWeight = selectionWeight;
            MinDepth = minDepth;
            MaxDepth = maxDepth;
            MaxPerWave = maxPerWave;
            MaxPerRoom = maxPerRoom;
            ThemeEligible = themeEligible;
        }

        public FpgEnemyDefinitionData Definition { get; }
        public int SelectionWeight { get; }
        public int MinDepth { get; }
        public int MaxDepth { get; }
        public int MaxPerWave { get; }
        public int MaxPerRoom { get; }
        public bool ThemeEligible { get; }

        public bool IsAvailableAtDepth(int depth)
        {
            return depth >= MinDepth && depth <= MaxDepth;
        }
    }

    public readonly struct FpgWaveBudgetShare
    {
        public FpgWaveBudgetShare(int basisPoints)
        {
            if (basisPoints <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(basisPoints));
            }

            BasisPoints = basisPoints;
        }

        public int BasisPoints { get; }
    }

    public sealed class FpgWaveLayoutData
    {
        public FpgWaveLayoutData(
            string layoutId,
            int selectionWeight,
            IReadOnlyList<FpgWaveBudgetShare> budgetShares)
        {
            if (string.IsNullOrWhiteSpace(layoutId))
            {
                throw new ArgumentException("Wave layout ID is required.", nameof(layoutId));
            }

            if (selectionWeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(selectionWeight));
            }

            if (budgetShares == null || budgetShares.Count == 0)
            {
                throw new ArgumentException("Wave layout requires at least one share.", nameof(budgetShares));
            }

            long total = 0L;
            for (int index = 0; index < budgetShares.Count; index++)
            {
                if (budgetShares[index].BasisPoints <= 0)
                {
                    throw new ArgumentException("Wave layout shares must be positive.", nameof(budgetShares));
                }

                total += budgetShares[index].BasisPoints;
            }

            if (total != FpgEncounterRunContext.BasisPointsOne)
            {
                throw new ArgumentException("Wave layout shares must total 10000 basis points.", nameof(budgetShares));
            }

            LayoutId = layoutId;
            SelectionWeight = selectionWeight;
            BudgetShares = new List<FpgWaveBudgetShare>(budgetShares).ToArray();
        }

        public string LayoutId { get; }
        public int SelectionWeight { get; }
        public IReadOnlyList<FpgWaveBudgetShare> BudgetShares { get; }
        public int WaveCount => BudgetShares.Count;
    }

    public sealed class FpgEncounterProfileData
    {
        public FpgEncounterProfileData(
            int baseBudget,
            int depthRamp,
            int minBudget,
            int maxConcurrentCapWeight,
            int maxConcurrentEntities,
            int spawnIntervalTicks,
            int warningDurationTicks,
            int waveIntervalTicks,
            int spawnSafetyDistanceUnits,
            int entrySafetyDistanceUnits,
            int maxSpawnWaitTicks,
            int enemyRosterCapacity,
            int threatCapacity,
            int projectileCapacity,
            int entityPoolCapacity,
            IReadOnlyList<FpgWaveBudgetShare> waveBudgetShares,
            IReadOnlyList<FpgEnemyPoolEntryData> enemyPool,
            int softDistanceRelaxationStepUnits = 1,
            int softDistanceRelaxationAttempts = 3,
            int hitboxCapacity = 1,
            int warningCapacity = 1,
            int overheadHealthBarCapacity = 1,
            IReadOnlyList<FpgWaveLayoutData> waveLayouts = null)
        {
            if (baseBudget < 0 || depthRamp < 0 || minBudget < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseBudget));
            }

            if (maxConcurrentCapWeight <= 0 || maxConcurrentEntities <= 0
                || spawnIntervalTicks < 0 || warningDurationTicks < 0 || waveIntervalTicks < 0
                || spawnSafetyDistanceUnits < 0 || entrySafetyDistanceUnits < 0
                || maxSpawnWaitTicks < 0 || enemyRosterCapacity <= 0 || threatCapacity <= 0
                || projectileCapacity <= 0 || entityPoolCapacity <= 0
                || softDistanceRelaxationStepUnits < 0 || softDistanceRelaxationAttempts < 0
                || hitboxCapacity <= 0 || warningCapacity <= 0 || overheadHealthBarCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConcurrentCapWeight));
            }

            if (waveBudgetShares == null || waveBudgetShares.Count == 0)
            {
                throw new ArgumentException("At least one wave budget share is required.", nameof(waveBudgetShares));
            }

            if (enemyPool == null || enemyPool.Count == 0)
            {
                throw new ArgumentException("At least one enemy pool entry is required.", nameof(enemyPool));
            }

            BaseBudget = baseBudget;
            DepthRamp = depthRamp;
            MinBudget = minBudget;
            MaxConcurrentCapWeight = maxConcurrentCapWeight;
            MaxConcurrentEntities = maxConcurrentEntities;
            SpawnIntervalTicks = spawnIntervalTicks;
            WarningDurationTicks = warningDurationTicks;
            WaveIntervalTicks = waveIntervalTicks;
            SpawnSafetyDistanceUnits = spawnSafetyDistanceUnits;
            EntrySafetyDistanceUnits = entrySafetyDistanceUnits;
            MaxSpawnWaitTicks = maxSpawnWaitTicks;
            EnemyRosterCapacity = enemyRosterCapacity;
            ThreatCapacity = threatCapacity;
            ProjectileCapacity = projectileCapacity;
            EntityPoolCapacity = entityPoolCapacity;
            SoftDistanceRelaxationStepUnits = softDistanceRelaxationStepUnits;
            SoftDistanceRelaxationAttempts = softDistanceRelaxationAttempts;
            HitboxCapacity = hitboxCapacity;
            WarningCapacity = warningCapacity;
            OverheadHealthBarCapacity = overheadHealthBarCapacity;
            WaveBudgetShares = new List<FpgWaveBudgetShare>(waveBudgetShares).ToArray();
            WaveLayouts = waveLayouts == null || waveLayouts.Count == 0
                ? new[] { new FpgWaveLayoutData("legacy", 1, WaveBudgetShares) }
                : new List<FpgWaveLayoutData>(waveLayouts).ToArray();
            EnemyPool = new List<FpgEnemyPoolEntryData>(enemyPool).ToArray();
        }

        public int BaseBudget { get; }
        public int DepthRamp { get; }
        public int MinBudget { get; }
        public int MaxConcurrentCapWeight { get; }
        public int MaxConcurrentEntities { get; }
        public int SpawnIntervalTicks { get; }
        public int WarningDurationTicks { get; }
        public int WaveIntervalTicks { get; }
        public int SpawnSafetyDistanceUnits { get; }
        public int EntrySafetyDistanceUnits { get; }
        public int MaxSpawnWaitTicks { get; }
        public int EnemyRosterCapacity { get; }
        public int ThreatCapacity { get; }
        public int ProjectileCapacity { get; }
        public int EntityPoolCapacity { get; }
        public int SoftDistanceRelaxationStepUnits { get; }
        public int SoftDistanceRelaxationAttempts { get; }
        public int HitboxCapacity { get; }
        public int WarningCapacity { get; }
        public int OverheadHealthBarCapacity { get; }
        public IReadOnlyList<FpgWaveBudgetShare> WaveBudgetShares { get; }
        public IReadOnlyList<FpgWaveLayoutData> WaveLayouts { get; }
        public IReadOnlyList<FpgEnemyPoolEntryData> EnemyPool { get; }

        public int CalculateBudget(int depth, int difficultyMultiplierBasisPoints)
        {
            if (depth < 0 || difficultyMultiplierBasisPoints <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(depth));
            }

            long baseValue = (long)BaseBudget + (long)depth * DepthRamp;
            long scaled = (baseValue * difficultyMultiplierBasisPoints + FpgEncounterRunContext.BasisPointsOne - 1L)
                / FpgEncounterRunContext.BasisPointsOne;
            long budget = Math.Max(MinBudget, scaled);
            return budget > int.MaxValue ? int.MaxValue : (int)budget;
        }

        public bool TryValidate(out string error)
        {
            error = null;
            long shareTotal = 0L;
            for (int index = 0; index < WaveBudgetShares.Count; index++)
            {
                if (WaveBudgetShares[index].BasisPoints <= 0)
                {
                    error = "Wave budget share must be positive.";
                    return false;
                }

                shareTotal += WaveBudgetShares[index].BasisPoints;
            }

            if (shareTotal != FpgEncounterRunContext.BasisPointsOne)
            {
                error = "Wave budget shares must total 10000 basis points.";
                return false;
            }

            if (EnemyPool.Count == 0)
            {
                error = "Enemy pool cannot be empty.";
                return false;
            }

            HashSet<string> layoutIds = new HashSet<string>(StringComparer.Ordinal);
            for (int layoutIndex = 0; layoutIndex < WaveLayouts.Count; layoutIndex++)
            {
                FpgWaveLayoutData layout = WaveLayouts[layoutIndex];
                if (layout == null
                    || string.IsNullOrWhiteSpace(layout.LayoutId)
                    || layout.SelectionWeight <= 0
                    || !layoutIds.Add(layout.LayoutId))
                {
                    error = "Wave layouts require unique IDs and positive selection weights.";
                    return false;
                }

                long layoutShareTotal = 0L;
                for (int shareIndex = 0; shareIndex < layout.BudgetShares.Count; shareIndex++)
                {
                    if (layout.BudgetShares[shareIndex].BasisPoints <= 0)
                    {
                        error = "Wave layout shares must be positive.";
                        return false;
                    }

                    layoutShareTotal += layout.BudgetShares[shareIndex].BasisPoints;
                }

                if (layout.BudgetShares.Count == 0
                    || layoutShareTotal != FpgEncounterRunContext.BasisPointsOne)
                {
                    error = "Wave layout shares must total 10000 basis points.";
                    return false;
                }
            }

            return true;
        }
    }

    public enum FpgEncounterOverrideMode
    {
        Generated = 0,
        GeneratedWithLocks,
        FixedWaves
    }

    public readonly struct FpgForcedEnemyCount
    {
        public FpgForcedEnemyCount(string enemyDefinitionId, int count)
        {
            if (string.IsNullOrWhiteSpace(enemyDefinitionId) || count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyDefinitionId));
            }

            EnemyDefinitionId = enemyDefinitionId;
            Count = count;
        }

        public string EnemyDefinitionId { get; }
        public int Count { get; }
    }

    public readonly struct FpgFixedSpawnSpec
    {
        public FpgFixedSpawnSpec(string enemyDefinitionId, int waveIndex, int count)
        {
            if (string.IsNullOrWhiteSpace(enemyDefinitionId) || waveIndex < 0 || count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyDefinitionId));
            }

            EnemyDefinitionId = enemyDefinitionId;
            WaveIndex = waveIndex;
            Count = count;
        }

        public string EnemyDefinitionId { get; }
        public int WaveIndex { get; }
        public int Count { get; }
    }

    public sealed class FpgEncounterOverrideData
    {
        public FpgEncounterOverrideData(
            FpgEncounterOverrideMode mode = FpgEncounterOverrideMode.Generated,
            IReadOnlyList<FpgForcedEnemyCount> forcedEnemies = null,
            IReadOnlyList<string> excludedEnemyDefinitionIds = null,
            IReadOnlyList<FpgFixedSpawnSpec> fixedSpawns = null,
            int? lockedBudget = null)
        {
            if (!Enum.IsDefined(typeof(FpgEncounterOverrideMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            if (lockedBudget.HasValue && lockedBudget.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lockedBudget));
            }

            Mode = mode;
            ForcedEnemies = forcedEnemies == null
                ? Array.Empty<FpgForcedEnemyCount>()
                : new List<FpgForcedEnemyCount>(forcedEnemies).ToArray();
            ExcludedEnemyDefinitionIds = excludedEnemyDefinitionIds == null
                ? Array.Empty<string>()
                : new List<string>(excludedEnemyDefinitionIds).ToArray();
            FixedSpawns = fixedSpawns == null
                ? Array.Empty<FpgFixedSpawnSpec>()
                : new List<FpgFixedSpawnSpec>(fixedSpawns).ToArray();
            LockedBudget = lockedBudget;
        }

        public FpgEncounterOverrideMode Mode { get; }
        public IReadOnlyList<FpgForcedEnemyCount> ForcedEnemies { get; }
        public IReadOnlyList<string> ExcludedEnemyDefinitionIds { get; }
        public IReadOnlyList<FpgFixedSpawnSpec> FixedSpawns { get; }
        public int? LockedBudget { get; }

        public bool IsExcluded(string enemyDefinitionId)
        {
            for (int index = 0; index < ExcludedEnemyDefinitionIds.Count; index++)
            {
                if (string.Equals(ExcludedEnemyDefinitionIds[index], enemyDefinitionId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class FpgRoomRunRequestSource : IFpgEncounterOverrideSource
    {
        public FpgRoomRunRequestSource(FpgEncounterOverrideData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public FpgEncounterOverrideData Data { get; }
    }

    public readonly struct FpgEncounterLifecycleEvent
    {
        public FpgEncounterLifecycleEvent(
            FpgEncounterLifecycleEventType type,
            TickIndex tick,
            FpgEncounterPhase phase,
            RuntimeId runtimeId = default(RuntimeId),
            int waveIndex = -1,
            string spawnEntryId = null,
            FpgEncounterFailureReason failureReason = FpgEncounterFailureReason.None)
        {
            Type = type;
            Tick = tick;
            Phase = phase;
            RuntimeId = runtimeId;
            WaveIndex = waveIndex;
            SpawnEntryId = spawnEntryId ?? string.Empty;
            FailureReason = failureReason;
        }

        public FpgEncounterLifecycleEventType Type { get; }
        public TickIndex Tick { get; }
        public FpgEncounterPhase Phase { get; }
        public RuntimeId RuntimeId { get; }
        public int WaveIndex { get; }
        public string SpawnEntryId { get; }
        public FpgEncounterFailureReason FailureReason { get; }
    }
}

