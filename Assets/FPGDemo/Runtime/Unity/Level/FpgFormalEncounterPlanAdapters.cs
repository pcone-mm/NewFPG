using System;
using FPG.Demo.Core;
using FPG.Demo.Run;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Adapter for the concrete pure plan. Keeping this adapter in the Unity
    /// assembly lets the deterministic generator remain free of UnityEngine.
    /// </summary>
    public sealed class FpgEncounterPlanView : IFpgEncounterPlanView
    {
        private readonly FpgEncounterPlan plan;

        public FpgEncounterPlanView(FpgEncounterPlan plan)
        {
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
        }

        public FpgEncounterPlan Plan => plan;
        public int WaveCount => plan.WaveCount;
        public int EntryCount => plan.EntryCount;

        public int GetWaveBudget(int waveIndex)
        {
            if (!plan.TryGetWave(waveIndex, out FpgEncounterWavePlan wave))
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex));
            }

            return wave.Budget;
        }

        public FpgRoomEncounterSpawnCommand GetEntry(int entryIndex)
        {
            if (entryIndex < 0 || entryIndex >= plan.AllEntries.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(entryIndex));
            }

            FpgSpawnEntry entry = plan.AllEntries[entryIndex];
            return new FpgRoomEncounterSpawnCommand(
                entry.SpawnEntryId,
                entry.EnemyDefinitionId,
                entry.Role,
                entry.WaveIndex,
                entry.SpawnSequence,
                entry.CapWeight);
        }
    }

    /// <summary>
    /// Unity projection of authored room markers used to construct the pure
    /// FpgRoomRunRequest. It never writes to the Room asset.
    /// </summary>
    public sealed class FpgRoomDefinitionSourceAdapter : IFpgRoomDefinitionSource
    {
        private readonly FpgRoomDefinition room;

        public FpgRoomDefinitionSourceAdapter(FpgRoomDefinition room)
        {
            this.room = room ?? throw new ArgumentNullException(nameof(room));
        }

        public FpgRoomDefinition Room => room;
        public string RoomDefinitionId => room.RoomId;
        public int ExitCount => room.ExitSlots.Count;
        public int SpawnPointCount => room.EnemySpawnPoints.Count;

        public FpgSpawnPointCandidate GetSpawnPoint(int index)
        {
            if (index < 0 || index >= room.EnemySpawnPoints.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            FpgRoomEnemySpawnPoint marker = room.EnemySpawnPoints[index];
            return new FpgSpawnPointCandidate(
                marker.MarkerId,
                ToRunRole(marker.Role),
                StablePositionKey(marker),
                1);
        }

        private static FpgEnemyRole ToRunRole(FpgRoomEnemySpawnRole role)
        {
            switch (role)
            {
                case FpgRoomEnemySpawnRole.Melee:
                    return FpgEnemyRole.Melee;
                case FpgRoomEnemySpawnRole.Ranged:
                    return FpgEnemyRole.Ranged;
                case FpgRoomEnemySpawnRole.Support:
                    return FpgEnemyRole.Support;
                default:
                    return FpgEnemyRole.Any;
            }
        }

        private static long StablePositionKey(FpgRoomMarker marker)
        {
            ulong hash = StableHash.Mix(0x4650475F524F4F4DUL);
            string id = marker == null ? string.Empty : marker.MarkerId;
            for (int index = 0; index < id.Length; index++)
            {
                hash = StableHash.Append(hash, id[index]);
            }

            return unchecked((long)(hash & 0x7FFFFFFFFFFFFFFFUL));
        }
    }
}
