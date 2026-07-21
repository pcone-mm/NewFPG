using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;

namespace FPG.Demo.Run
{
    public enum BattleSessionState
    {
        NotStarted = 0,
        Running,
        Paused,
        Completed,
        Disposed,
        Faulted
    }

    public enum BattleCompletionReason
    {
        None = 0,
        Victory,
        Defeat,
        External,
        Restarted,
        Disposed,
        Faulted
    }

    public enum SessionControlCommandType
    {
        Start = 0,
        Pause,
        Resume,
        Complete,
        Restart,
        Dispose
    }

    public enum ThreatCommandType
    {
        Add = 0,
        Start
    }

    public readonly struct SessionControlCommand
    {
        public SessionControlCommand(ControlSequence sequence, SessionControlCommandType type)
        {
            Sequence = sequence;
            Type = type;
        }

        public ControlSequence Sequence { get; }
        public SessionControlCommandType Type { get; }
    }

    public readonly struct ThreatCommand
    {
        public ThreatCommand(
            ControlSequence sequence,
            TickIndex tick,
            ThreatCommandType type,
            ThreatDefinition definition,
            int threatIndex = -1,
            RuntimeId expectedThreatRuntimeId = default(RuntimeId))
        {
            Sequence = sequence;
            Tick = tick;
            Type = type;
            Definition = definition;
            ThreatIndex = threatIndex;
            ExpectedThreatRuntimeId = expectedThreatRuntimeId;
        }

        public ControlSequence Sequence { get; }
        public TickIndex Tick { get; }
        public ThreatCommandType Type { get; }
        public ThreatDefinition Definition { get; }
        public int ThreatIndex { get; }
        public RuntimeId ExpectedThreatRuntimeId { get; }
    }

    /// <summary>
    /// Describes an authored enemy lifetime boundary. The boundary is applied
    /// before the corresponding simulation tick, so Unity spatial bindings can
    /// switch from the old entity to the new entity before any attack query for
    /// that tick runs.
    /// </summary>
    public readonly struct EnemySpawnDefinition
    {
        public EnemySpawnDefinition(
            int definitionId,
            TickIndex spawnTick,
            int life,
            int breakValue,
            TickDuration groggyDuration,
            int threatCapacity = 8)
        {
            if (definitionId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(definitionId));
            }

            if (!spawnTick.IsValid || spawnTick.Value <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(spawnTick));
            }

            if (life <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(life));
            }

            if (breakValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(breakValue));
            }

            if (groggyDuration.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(groggyDuration));
            }

            if (threatCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(threatCapacity));
            }

            DefinitionId = definitionId;
            SpawnTick = spawnTick;
            Life = life;
            Break = breakValue;
            GroggyDuration = groggyDuration;
            ThreatCapacity = threatCapacity;
        }

        public int DefinitionId { get; }
        public TickIndex SpawnTick { get; }
        public int Life { get; }
        public int Break { get; }
        public TickDuration GroggyDuration { get; }
        public int ThreatCapacity { get; }
    }

    public readonly struct EnemyLifecycleChange
    {
        public EnemyLifecycleChange(
            TickIndex tick,
            RuntimeId previousRuntimeId,
            RuntimeId currentRuntimeId,
            int definitionId)
        {
            Tick = tick;
            PreviousRuntimeId = previousRuntimeId;
            CurrentRuntimeId = currentRuntimeId;
            DefinitionId = definitionId;
        }

        public TickIndex Tick { get; }
        public RuntimeId PreviousRuntimeId { get; }
        public RuntimeId CurrentRuntimeId { get; }
        public int DefinitionId { get; }
    }

    public readonly struct ResolvedAttackHit
    {
        public ResolvedAttackHit(
            RuntimeId targetId,
            HitPart hitPart,
            int pelletIndex,
            int impactOrdinal)
        {
            TargetId = targetId;
            HitPart = hitPart;
            PelletIndex = pelletIndex;
            ImpactOrdinal = impactOrdinal;
        }

        public RuntimeId TargetId { get; }
        public HitPart HitPart { get; }
        public int PelletIndex { get; }
        public int ImpactOrdinal { get; }
    }

    public interface IAttackResolutionPort
    {
        int Resolve(
            AttackSnapshot attack,
            PelletSample[] pellets,
            int pelletCount,
            ResolvedAttackHit[] output);
    }

    public interface IPlayerInputSource
    {
        PlayerInputFrame GetFrame(TickIndex tick);
    }

    public sealed class NullAttackResolutionPort : IAttackResolutionPort
    {
        public int Resolve(
            AttackSnapshot attack,
            PelletSample[] pellets,
            int pelletCount,
            ResolvedAttackHit[] output)
        {
            return 0;
        }
    }

    public readonly struct FinalSnapshot
    {
        public FinalSnapshot(
            BattleSessionState state,
            BattleCompletionReason completionReason,
            long executedTickCount,
            int playerLife,
            int playerBarrier,
            int playerAmmo,
            int enemyLife,
            int enemyBreak,
            EnemyControlState enemyControlState,
            int reservedProjectileUnits,
            int activeProjectileUnits,
            int enemyMaxLife = 0,
            int enemyMaxBreak = 0,
            int enemyDefinitionId = 1)
        {
            State = state;
            CompletionReason = completionReason;
            ExecutedTickCount = executedTickCount;
            PlayerLife = playerLife;
            PlayerBarrier = playerBarrier;
            PlayerAmmo = playerAmmo;
            EnemyLife = enemyLife;
            EnemyBreak = enemyBreak;
            EnemyControlState = enemyControlState;
            ReservedProjectileUnits = reservedProjectileUnits;
            ActiveProjectileUnits = activeProjectileUnits;
            // Zero keeps the optional fields backwards-compatible for callers
            // that construct an intermediate snapshot. Presentation falls back
            // to the authored initial enemy definition in that case; live
            // BattleSession snapshots always provide the active runtime caps.
            EnemyMaxLife = enemyMaxLife;
            EnemyMaxBreak = enemyMaxBreak;
            EnemyDefinitionId = enemyDefinitionId;
        }

        public BattleSessionState State { get; }
        public BattleCompletionReason CompletionReason { get; }
        public long ExecutedTickCount { get; }
        public int PlayerLife { get; }
        public int PlayerBarrier { get; }
        public int PlayerAmmo { get; }
        public int EnemyLife { get; }
        public int EnemyBreak { get; }
        public EnemyControlState EnemyControlState { get; }
        public int ReservedProjectileUnits { get; }
        public int ActiveProjectileUnits { get; }
        public int EnemyMaxLife { get; }
        public int EnemyMaxBreak { get; }
        public int EnemyDefinitionId { get; }
    }

    public readonly struct ReplaySummary
    {
        public ReplaySummary(
            ulong definitionHash,
            ulong scenarioSeed,
            long controlCommandCount,
            ulong controlCommandDigest,
            long threatCommandCount,
            ulong threatCommandDigest,
            long threatScheduleDecisionCount,
            ulong threatScheduleDecisionDigest,
            long executedTickCount,
            FinalSnapshot finalSnapshot,
            long traceEventCount,
            long droppedTraceEventCount,
            int spatialContractVersion,
            long spatialDecisionCount,
            ulong spatialDecisionDigest,
            ulong canonicalDigest)
        {
            DefinitionHash = definitionHash;
            RngVersion = DeterministicRandomV1.Version;
            ScenarioSeed = scenarioSeed;
            ControlCommandCount = controlCommandCount;
            ControlCommandDigest = controlCommandDigest;
            ThreatCommandCount = threatCommandCount;
            ThreatCommandDigest = threatCommandDigest;
            ThreatScheduleDecisionCount = threatScheduleDecisionCount;
            ThreatScheduleDecisionDigest = threatScheduleDecisionDigest;
            ExecutedTickCount = executedTickCount;
            FinalSnapshot = finalSnapshot;
            TraceEventCount = traceEventCount;
            DroppedTraceEventCount = droppedTraceEventCount;
            SpatialContractVersion = spatialContractVersion;
            SpatialDecisionCount = spatialDecisionCount;
            SpatialDecisionDigest = spatialDecisionDigest;
            CanonicalDigest = canonicalDigest;
        }

        public ulong DefinitionHash { get; }
        public int RngVersion { get; }
        public ulong ScenarioSeed { get; }
        public long ControlCommandCount { get; }
        public ulong ControlCommandDigest { get; }
        public long ThreatCommandCount { get; }
        public ulong ThreatCommandDigest { get; }
        public long ThreatScheduleDecisionCount { get; }
        public ulong ThreatScheduleDecisionDigest { get; }
        public long ExecutedTickCount { get; }
        public FinalSnapshot FinalSnapshot { get; }
        public long TraceEventCount { get; }
        public long DroppedTraceEventCount { get; }
        public int SpatialContractVersion { get; }
        public long SpatialDecisionCount { get; }
        public ulong SpatialDecisionDigest { get; }
        public ulong CanonicalDigest { get; }
    }
}
