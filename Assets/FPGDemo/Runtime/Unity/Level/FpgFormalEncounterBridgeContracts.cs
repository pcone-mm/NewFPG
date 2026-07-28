using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// A small Unity-facing projection of a pure encounter plan. The run
    /// assembly owns the actual plan; this interface keeps the room director
    /// independent from its concrete generator representation.
    /// </summary>
    public readonly struct FpgRoomEncounterSpawnCommand
    {
        public FpgRoomEncounterSpawnCommand(
            string spawnEntryId,
            string enemyDefinitionId,
            FpgEnemyRole role,
            int waveIndex,
            int spawnSequence,
            int capWeight)
        {
            if (string.IsNullOrWhiteSpace(spawnEntryId))
            {
                throw new ArgumentException("Spawn entry id is required.", nameof(spawnEntryId));
            }

            if (string.IsNullOrWhiteSpace(enemyDefinitionId))
            {
                throw new ArgumentException("Enemy definition id is required.", nameof(enemyDefinitionId));
            }

            if (!Enum.IsDefined(typeof(FpgEnemyRole), role)
                || waveIndex < 0
                || spawnSequence < 0
                || capWeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(role));
            }

            SpawnEntryId = spawnEntryId;
            EnemyDefinitionId = enemyDefinitionId;
            Role = role;
            WaveIndex = waveIndex;
            SpawnSequence = spawnSequence;
            CapWeight = capWeight;
        }

        public string SpawnEntryId { get; }
        public string EnemyDefinitionId { get; }
        public FpgEnemyRole Role { get; }
        public int WaveIndex { get; }
        public int SpawnSequence { get; }
        public int CapWeight { get; }
    }

    /// <summary>
    /// Read-only view consumed by <see cref="FpgRoomEncounterDirector"/>.
    /// Entries must be sorted by wave and deterministic spawn sequence.
    /// </summary>
    public interface IFpgEncounterPlanView
    {
        int WaveCount { get; }
        int EntryCount { get; }
        int GetWaveBudget(int waveIndex);
        FpgRoomEncounterSpawnCommand GetEntry(int entryIndex);
    }

    public readonly struct FpgEnemyPoolWarmupRequest
    {
        public FpgEnemyPoolWarmupRequest(FpgEnemyDefinition definition, int count)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Count = count;
        }

        public FpgEnemyDefinition Definition { get; }
        public int Count { get; }
    }

    public readonly struct FpgEnemyEntityHandle
    {
        internal FpgEnemyEntityHandle(
            int poolSlot,
            RuntimeId runtimeId,
            FpgEnemyDefinition definition,
            GameObject instance,
            IFpgFormalEnemyEntityBinder binder)
        {
            PoolSlot = poolSlot;
            RuntimeId = runtimeId;
            Definition = definition;
            Instance = instance;
            Binder = binder;
        }

        public int PoolSlot { get; }
        public RuntimeId RuntimeId { get; }
        public FpgEnemyDefinition Definition { get; }
        public GameObject Instance { get; }
        public IFpgFormalEnemyEntityBinder Binder { get; }
        public bool IsValid => PoolSlot >= 0
            && RuntimeId.IsValid
            && Instance != null
            && Binder != null;
    }

    /// <summary>
    /// Formal views expose every runtime anchor and hit part up front so the
    /// pool never performs component discovery during combat.
    /// </summary>
    public interface IFpgFormalEnemyEntityBinder
    {
        Transform GameplayAnchor { get; }
        Transform ProjectileAnchor { get; }
        Transform WeakpointAnchor { get; }
        D0ActorSocketRegistry SocketRegistry { get; }
        Transform OverheadHealthBarAnchor { get; }
        int HitPartCount { get; }

        bool TryGetHitPart(
            int hitPartOrdinal,
            out Collider collider,
            out HitPart hitPart);

        bool TryBindFormalRuntime(
            RuntimeId runtimeId,
            int spawnSequence,
            FpgEnemyDefinition definition,
            out string error);

        void SetFormalGameplayEnabled(bool enabled);
        void UnbindFormalRuntime();
    }

    /// <summary>
    /// Best-effort presentation endpoint for an already bound formal enemy.
    /// Presentation failures are diagnostic only and never change combat state.
    /// </summary>
    public interface IFpgFormalEnemyPresentationView
    {
        bool TrySetSkillSequenceFrame(
            in FpgFormalEnemySkillSequenceFrame frame);

        bool TrySetSkillWarning(
            in FpgFormalEnemySkillWarningPresentationEvent warningEvent);

        void ClearSkillWarnings();
    }

    /// <summary>
    /// Gameplay-authoritative animation motion exposed by a bound formal
    /// enemy view. Unlike presentation callbacks, failures stop the battle
    /// tick because gameplay anchors and colliders share the moved entity.
    /// </summary>
    public interface IFpgFormalEnemyMotionView
    {
        DomainResult AdvanceFormalMotion(TickIndex tick);

        DomainResult StartFormalSkillMotion(
            in FpgFormalEnemySkillSequenceFrame frame);

        DomainResult ApplyFormalSkillMotionFrame(
            in FpgFormalEnemySkillSequenceFrame frame);
    }

    /// <summary>
    /// Fixed-tick authority used by the formal battle synchronizer and enemy
    /// skill scheduler. Implementations cover both Prepared and Active views.
    /// </summary>
    public interface IFpgFormalEnemyMotionAuthority
    {
        DomainResult AdvanceMotion(TickIndex tick);

        DomainResult StartSkillMotion(
            in FpgFormalEnemySkillSequenceFrame frame);

        DomainResult ApplySkillMotionFrame(
            in FpgFormalEnemySkillSequenceFrame frame);
    }

    public interface IFpgFormalEnemySkillPresentationConsumer
    {
        bool TrySetEnemySkillWarning(
            in FpgFormalEnemySkillWarningPresentationEvent warningEvent);

        void ClearEnemySkillWarnings();
    }

    public interface IFpgFormalEnemyPresentationPort :
        IFpgFormalEnemySkillPresentationConsumer
    {
        bool TryApplySkillSequenceFrame(
            in FpgFormalEnemySkillSequenceFrame frame);
    }

    public static class FpgFormalGeometryId
    {
        public const int HitPartOrdinalBits = 10;
        public const int MaxHitPartOrdinal = (1 << HitPartOrdinalBits) - 1;
        public const int MaxSpawnSequence = (1 << 20) - 1;

        // The upper positive-int half is reserved for formal combatants.
        private const int FormalGeometryDomain = 1 << 30;

        /// <summary>
        /// Backward-compatible numeric projection of the formal geometry ID.
        /// </summary>
        public static long Derive(int spawnSequence, int hitPartOrdinal)
        {
            return DeriveCombatGeometryId(spawnSequence, hitPartOrdinal).Value;
        }

        /// <summary>
        /// Injective positive geometry identity for the supported sequence and
        /// hit-part bounds. No hash or Unity instance identity is involved.
        /// </summary>
        public static GeometryId DeriveCombatGeometryId(
            int spawnSequence,
            int hitPartOrdinal)
        {
            return TryDeriveCombatGeometryId(
                spawnSequence,
                hitPartOrdinal,
                out GeometryId geometryId)
                    ? geometryId
                    : GeometryId.Invalid;
        }

        public static bool TryDeriveCombatGeometryId(
            int spawnSequence,
            int hitPartOrdinal,
            out GeometryId geometryId)
        {
            if (spawnSequence < 0 || spawnSequence > MaxSpawnSequence
                || hitPartOrdinal < 0 || hitPartOrdinal > MaxHitPartOrdinal)
            {
                geometryId = GeometryId.Invalid;
                return false;
            }

            int packed = FormalGeometryDomain
                | spawnSequence << HitPartOrdinalBits
                | hitPartOrdinal;
            geometryId = new GeometryId(packed);
            return true;
        }
    }
}

