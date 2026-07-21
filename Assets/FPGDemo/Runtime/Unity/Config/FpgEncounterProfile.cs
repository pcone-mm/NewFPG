using System;
using System.Collections.Generic;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum FpgWaveBudgetTemplate
    {
        Full = 0,
        HalfHalf = 1,
        ThirtyFifteenFiftyFive = 2,
        Custom = 3
    }

    [Serializable]
    public struct FpgWaveBudgetShareDefinition
    {
        [D0PlannerField("Basis Points", "Integer wave share; 10000 basis points equals 100 percent.")]
        [SerializeField, Min(1)]
        private int basisPoints;

        public FpgWaveBudgetShareDefinition(int basisPoints)
        {
            this.basisPoints = basisPoints;
        }

        public int BasisPoints => basisPoints;
    }

    /// <summary>
    /// Formal encounter tuning. It owns budget, wave, cap, timing and fixed
    /// capacity rules; the room asset only owns environment and marker poses.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FpgEncounterProfile",
        menuName = "FPG Demo/Formal Encounter/Encounter Profile")]
    public sealed class FpgEncounterProfile : ScriptableObject, IFpgEncounterProfileSource
    {
        public const int BasisPointsOne = FpgEncounterRunContext.BasisPointsOne;

        [D0PlannerSection("Identity")]
        [D0PlannerField("Profile ID", "Stable profile identity used by room requests and preview logs.")]
        [SerializeField]
        private string profileId = "normal-room";

        [D0PlannerField("Display Name", "Authoring-only profile name.")]
        [SerializeField]
        private string displayName = "Normal Room";

        [TextArea]
        [D0PlannerField("Designer Notes", "Authoring notes for this encounter family.")]
        [SerializeField]
        private string designerNotes = string.Empty;

        [D0PlannerSection("Budget Formula")]
        [D0PlannerField("Base Budget", "Base integer budget before depth and difficulty scaling.")]
        [SerializeField, Min(0)]
        private int baseBudget = 6;

        [D0PlannerField("Depth Ramp", "Additional budget per run depth.")]
        [SerializeField, Min(0)]
        private int depthRamp = 2;

        [D0PlannerField("Minimum Budget", "Lower bound after difficulty scaling.")]
        [SerializeField, Min(0)]
        private int minBudget = 6;

        [D0PlannerField("Default Difficulty (Basis Points)", "Fallback difficulty multiplier; 10000 means 1.0x.")]
        [SerializeField, Min(1)]
        private int defaultDifficultyMultiplierBasisPoints = BasisPointsOne;

        [D0PlannerSection("Wave Budget")]
        [D0PlannerField("Wave Template", "Built-in Hades-style shares or Custom basis-point shares.")]
        [SerializeField]
        private FpgWaveBudgetTemplate waveBudgetTemplate = FpgWaveBudgetTemplate.HalfHalf;

        [D0PlannerField("Custom Wave Shares", "Used only when Wave Template is Custom; shares must total 10000.")]
        [SerializeField]
        private FpgWaveBudgetShareDefinition[] customWaveShares = Array.Empty<FpgWaveBudgetShareDefinition>();

        [D0PlannerField("Weighted Wave Layouts", "Weighted Hades-style layouts; each layout shares must total 10000.")]
        [SerializeField]
        private FpgWaveLayoutDefinition[] weightedWaveLayouts =
            FpgWaveLayoutDefinition.CreateHadesDefaults();

        [D0PlannerSection("Spawn Timing and Caps")]
        [D0PlannerField("Max Concurrent Cap Weight", "Sum of active enemy CapWeight values may not exceed this value.")]
        [SerializeField, Min(1)]
        private int maxConcurrentCapWeight = 4;

        [D0PlannerField("Max Concurrent Entities", "Fixed maximum active enemy instances.")]
        [SerializeField, Min(1)]
        private int maxConcurrentEntities = 4;

        [D0PlannerField("Spawn Interval (Ticks)", "Delay between activating entries after the warning phase.")]
        [SerializeField, Min(0)]
        private int spawnIntervalTicks = 15;

        [D0PlannerField("Warning Duration (Ticks)", "Per-entry telegraph duration; no hitbox, threat, or damage is active.")]
        [SerializeField, Min(0)]
        private int warningDurationTicks = 30;

        [D0PlannerField("Wave Interval (Ticks)", "Delay after a wave is empty before the next wave warning starts.")]
        [SerializeField, Min(0)]
        private int waveIntervalTicks = 60;

        [D0PlannerSection("Spawn Point Safety")]
        [D0PlannerField("Player Safety Distance", "Initial minimum distance from the player in authored world units.")]
        [SerializeField, Min(0)]
        private int spawnSafetyDistanceUnits = 4;

        [D0PlannerField("Entry Safety Distance", "Initial minimum distance from room entry in authored world units.")]
        [SerializeField, Min(0)]
        private int entrySafetyDistanceUnits = 2;

        [D0PlannerField("Soft Relaxation Step", "Distance removed per fallback attempt when a compatible point is occupied.")]
        [SerializeField, Min(0)]
        private int softDistanceRelaxationStepUnits = 1;

        [D0PlannerField("Soft Relaxation Attempts", "Maximum deterministic fallback attempts before fail-closed.")]
        [SerializeField, Min(0)]
        private int softDistanceRelaxationAttempts = 4;

        [D0PlannerField("Max Spawn Wait (Ticks)", "Maximum time a queued entry can wait for a compatible free point.")]
        [SerializeField, Min(0)]
        private int maxSpawnWaitTicks = 120;

        [D0PlannerSection("Fixed Capacities")]
        [D0PlannerField("Enemy Roster Capacity", "Fixed runtime slot count; no battle-time growth.")]
        [SerializeField, Min(1)]
        private int enemyRosterCapacity = 16;

        [D0PlannerField("Entity Pool Capacity", "Prewarmed enemy entity count; no Instantiate/Destroy in battle.")]
        [SerializeField, Min(1)]
        private int entityPoolCapacity = 16;

        [D0PlannerField("Hitbox Capacity", "Prewarmed hitbox binding count.")]
        [SerializeField, Min(1)]
        private int hitboxCapacity = 64;

        [D0PlannerField("Threat Capacity", "Prewarmed threat count.")]
        [SerializeField, Min(1)]
        private int threatCapacity = 32;

        [D0PlannerField("Projectile Capacity", "Prewarmed hostile projectile count.")]
        [SerializeField, Min(1)]
        private int projectileCapacity = 32;

        [D0PlannerField("Warning Capacity", "Prewarmed telegraph view count.")]
        [SerializeField, Min(1)]
        private int warningCapacity = 16;

        [D0PlannerField("Health Bar Capacity", "Independent overhead health-bar view count.")]
        [SerializeField, Min(1)]
        private int overheadHealthBarCapacity = 16;

        [D0PlannerSection("References")]
        [D0PlannerField("Enemy Pool", "Depth-filtered enemy source. It is selected by the room request, not stored on the room.")]
        [SerializeField]
        private FpgEnemyPoolDefinition enemyPool = null;

        public string ProfileId => profileId;
        public string DisplayName => displayName;
        public string DesignerNotes => designerNotes;
        public int BaseBudget => baseBudget;
        public int DepthRamp => depthRamp;
        public int MinBudget => minBudget;
        public int DefaultDifficultyMultiplierBasisPoints => defaultDifficultyMultiplierBasisPoints;
        public FpgWaveBudgetTemplate WaveBudgetTemplate => waveBudgetTemplate;
        public IReadOnlyList<FpgWaveLayoutDefinition> WeightedWaveLayouts =>
            weightedWaveLayouts ?? Array.Empty<FpgWaveLayoutDefinition>();
        public FpgEnemyPoolDefinition EnemyPool => enemyPool;
        public int MaxConcurrentCapWeight => maxConcurrentCapWeight;
        public int MaxConcurrentEntities => maxConcurrentEntities;
        public int SpawnIntervalTicks => spawnIntervalTicks;
        public int WarningDurationTicks => warningDurationTicks;
        public int WaveIntervalTicks => waveIntervalTicks;
        public int SpawnSafetyDistanceUnits => spawnSafetyDistanceUnits;
        public int EntrySafetyDistanceUnits => entrySafetyDistanceUnits;
        public int SoftDistanceRelaxationStepUnits => softDistanceRelaxationStepUnits;
        public int SoftDistanceRelaxationAttempts => softDistanceRelaxationAttempts;
        public int MaxSpawnWaitTicks => maxSpawnWaitTicks;
        public int EnemyRosterCapacity => enemyRosterCapacity;
        public int EntityPoolCapacity => entityPoolCapacity;
        public int HitboxCapacity => hitboxCapacity;
        public int ThreatCapacity => threatCapacity;
        public int ProjectileCapacity => projectileCapacity;
        public int WarningCapacity => warningCapacity;
        public int OverheadHealthBarCapacity => overheadHealthBarCapacity;

        // The pure planner consumes this immutable projection.
        public FpgEncounterProfileData Data
        {
            get
            {
                return TryBuildData(out FpgEncounterProfileData data, out _)
                    ? data
                    : null;
            }
        }

        public bool TryBuildData(out FpgEncounterProfileData data, out string error)
        {
            data = null;
            if (!TryValidate(out error))
            {
                return false;
            }

            List<FpgEnemyPoolEntryData> poolData = new List<FpgEnemyPoolEntryData>(enemyPool.EntryCount);
            for (int index = 0; index < enemyPool.EntryCount; index++)
            {
                FpgEnemyPoolEntryDefinition entry = enemyPool.GetEntry(index);
                if (!FpgFormalConfigAdapters.TryBuildPoolEntryData(entry, out FpgEnemyPoolEntryData value, out error))
                {
                    return false;
                }

                poolData.Add(value);
            }

            FpgWaveLayoutDefinition[] layoutDefinitions = GetWaveLayoutDefinitions();
            List<FpgWaveLayoutData> layouts = new List<FpgWaveLayoutData>(layoutDefinitions.Length);
            for (int index = 0; index < layoutDefinitions.Length; index++)
            {
                if (!layoutDefinitions[index].TryBuildData(
                        out FpgWaveLayoutData layout,
                        out error))
                {
                    return false;
                }

                layouts.Add(layout);
            }

            List<FpgWaveBudgetShare> shares;
            if (weightedWaveLayouts != null && weightedWaveLayouts.Length > 0)
            {
                shares = new List<FpgWaveBudgetShare>(layouts[0].BudgetShares);
            }
            else
            {
                FpgWaveBudgetShareDefinition[] definitions = GetWaveBudgetDefinitions();
                shares = new List<FpgWaveBudgetShare>(definitions.Length);
                for (int index = 0; index < definitions.Length; index++)
                {
                    shares.Add(new FpgWaveBudgetShare(definitions[index].BasisPoints));
                }
            }

            try
            {
                data = new FpgEncounterProfileData(
                    baseBudget,
                    depthRamp,
                    minBudget,
                    maxConcurrentCapWeight,
                    maxConcurrentEntities,
                    spawnIntervalTicks,
                    warningDurationTicks,
                    waveIntervalTicks,
                    spawnSafetyDistanceUnits,
                    entrySafetyDistanceUnits,
                    maxSpawnWaitTicks,
                    enemyRosterCapacity,
                    threatCapacity,
                    projectileCapacity,
                    entityPoolCapacity,
                    shares,
                    poolData,
                    softDistanceRelaxationStepUnits,
                    softDistanceRelaxationAttempts,
                    hitboxCapacity,
                    warningCapacity,
                    overheadHealthBarCapacity,
                    layouts);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                data = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public int CalculateBudget(int depth, int difficultyMultiplierBasisPoints)
        {
            long baseValue = (long)baseBudget + (long)Math.Max(0, depth) * depthRamp;
            long scaled = (baseValue * Math.Max(1, difficultyMultiplierBasisPoints)
                + BasisPointsOne - 1L) / BasisPointsOne;
            long budget = Math.Max(minBudget, scaled);
            return budget > int.MaxValue ? int.MaxValue : (int)budget;
        }

        public FpgWaveBudgetShareDefinition[] GetWaveBudgetDefinitions()
        {
            switch (waveBudgetTemplate)
            {
                case FpgWaveBudgetTemplate.Full:
                    return new[] { new FpgWaveBudgetShareDefinition(BasisPointsOne) };

                case FpgWaveBudgetTemplate.HalfHalf:
                    return new[]
                    {
                        new FpgWaveBudgetShareDefinition(5000),
                        new FpgWaveBudgetShareDefinition(5000)
                    };

                case FpgWaveBudgetTemplate.ThirtyFifteenFiftyFive:
                    return new[]
                    {
                        new FpgWaveBudgetShareDefinition(3000),
                        new FpgWaveBudgetShareDefinition(1500),
                        new FpgWaveBudgetShareDefinition(5500)
                    };

                case FpgWaveBudgetTemplate.Custom:
                    return customWaveShares ?? Array.Empty<FpgWaveBudgetShareDefinition>();

                default:
                    return Array.Empty<FpgWaveBudgetShareDefinition>();
            }
        }

        public FpgWaveLayoutDefinition[] GetWaveLayoutDefinitions()
        {
            if (weightedWaveLayouts != null && weightedWaveLayouts.Length > 0)
            {
                return weightedWaveLayouts;
            }

            return new[]
            {
                new FpgWaveLayoutDefinition(
                    "legacy-" + waveBudgetTemplate.ToString(),
                    1,
                    GetWaveBudgetDefinitions())
            };
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(displayName))
            {
                error = "Formal encounter profile requires a stable ID and display name.";
                return false;
            }

            if (baseBudget < 0 || depthRamp < 0 || minBudget < 0
                || defaultDifficultyMultiplierBasisPoints <= 0
                || maxConcurrentCapWeight <= 0
                || maxConcurrentEntities <= 0
                || spawnIntervalTicks < 0
                || warningDurationTicks < 0
                || waveIntervalTicks < 0
                || spawnSafetyDistanceUnits < 0
                || entrySafetyDistanceUnits < 0
                || softDistanceRelaxationStepUnits < 0
                || softDistanceRelaxationAttempts < 0
                || maxSpawnWaitTicks < 0
                || enemyRosterCapacity <= 0
                || entityPoolCapacity <= 0
                || hitboxCapacity <= 0
                || threatCapacity <= 0
                || projectileCapacity <= 0
                || warningCapacity <= 0
                || overheadHealthBarCapacity <= 0)
            {
                error = $"Formal encounter profile '{profileId}' has invalid numeric values.";
                return false;
            }

            bool hasWeightedLayouts = weightedWaveLayouts != null
                && weightedWaveLayouts.Length > 0;
            if (!hasWeightedLayouts)
            {
                FpgWaveBudgetShareDefinition[] shares = GetWaveBudgetDefinitions();
                long total = 0L;
                for (int index = 0; index < shares.Length; index++)
                {
                    if (shares[index].BasisPoints <= 0)
                    {
                        error = $"Formal encounter profile '{profileId}' has a non-positive wave share.";
                        return false;
                    }

                    total += shares[index].BasisPoints;
                }

                if (shares.Length == 0 || total != BasisPointsOne)
                {
                    error = $"Formal encounter profile '{profileId}' wave shares must total {BasisPointsOne}.";
                    return false;
                }
            }

            FpgWaveLayoutDefinition[] layouts = GetWaveLayoutDefinitions();
            HashSet<string> layoutIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < layouts.Length; index++)
            {
                FpgWaveLayoutDefinition layout = layouts[index];
                if (layout == null)
                {
                    error = $"Formal encounter profile '{profileId}' has a missing wave layout.";
                    return false;
                }

                if (!layout.TryValidate(out string layoutError))
                {
                    error = layoutError;
                    return false;
                }

                if (!layoutIds.Add(layout.LayoutId))
                {
                    error = $"Formal encounter profile '{profileId}' repeats wave layout ID '{layout.LayoutId}'.";
                    return false;
                }
            }

            if (enemyPool == null)
            {
                error = $"Formal encounter profile '{profileId}' requires an enemy pool.";
                return false;
            }

            return enemyPool.TryValidate(out error);
        }
    }
}

