using System;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    public readonly struct FpgQueuedSpawn
    {
        public FpgQueuedSpawn(
            FpgSpawnEntry entry,
            TickIndex queuedTick,
            TickIndex earliestActivationTick,
            int attempt)
        {
            Entry = entry;
            QueuedTick = queuedTick;
            EarliestActivationTick = earliestActivationTick;
            Attempt = attempt;
        }

        public FpgSpawnEntry Entry { get; }
        public TickIndex QueuedTick { get; }
        public TickIndex EarliestActivationTick { get; }
        public int Attempt { get; }

        public FpgQueuedSpawn ShiftTicks(long delta)
        {
            if (delta < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(delta));
            }

            return new FpgQueuedSpawn(
                Entry,
                ShiftTick(QueuedTick, delta),
                ShiftTick(EarliestActivationTick, delta),
                Attempt);
        }

        private static TickIndex ShiftTick(TickIndex tick, long delta)
        {
            if (!tick.IsValid || delta == 0L)
            {
                return tick;
            }

            return tick.Value > long.MaxValue - delta
                ? new TickIndex(long.MaxValue)
                : new TickIndex(tick.Value + delta);
        }
    }

    /// <summary>
    /// FIFO queue with a fixed backing array. A failed point selection is
    /// retried in place, so one blocked enemy cannot reorder later entries.
    /// </summary>
    public sealed class FpgSpawnQueue
    {
        private readonly FpgQueuedSpawn[] entries;
        private int head;
        private int count;

        public FpgSpawnQueue(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            entries = new FpgQueuedSpawn[capacity];
        }

        public int Capacity => entries.Length;
        public int Count => count;
        public bool IsEmpty => count == 0;

        public DomainResult TryEnqueue(FpgQueuedSpawn entry)
        {
            if (count >= entries.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            int tail = (head + count) % entries.Length;
            entries[tail] = entry;
            count++;
            return DomainResult.Success;
        }

        public bool TryPeek(out FpgQueuedSpawn entry)
        {
            if (count == 0)
            {
                entry = default(FpgQueuedSpawn);
                return false;
            }

            entry = entries[head];
            return true;
        }

        public bool TryDequeue(out FpgQueuedSpawn entry)
        {
            if (!TryPeek(out entry))
            {
                return false;
            }

            entries[head] = default(FpgQueuedSpawn);
            head = (head + 1) % entries.Length;
            count--;
            return true;
        }

        public DomainResult TryRetryHead(TickIndex nextActivationTick)
        {
            if (count == 0 || !nextActivationTick.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            FpgQueuedSpawn current = entries[head];
            entries[head] = new FpgQueuedSpawn(
                current.Entry,
                current.QueuedTick,
                nextActivationTick,
                current.Attempt + 1);
            return DomainResult.Success;
        }

        public DomainResult ShiftScheduledTicks(long delta)
        {
            if (delta < 0L)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            for (int offset = 0; offset < count; offset++)
            {
                int index = (head + offset) % entries.Length;
                entries[index] = entries[index].ShiftTicks(delta);
            }

            return DomainResult.Success;
        }

        public void Clear()
        {
            Array.Clear(entries, 0, entries.Length);
            head = 0;
            count = 0;
        }
    }

    public readonly struct FpgSpawnPointSelectionOptions
    {
        public FpgSpawnPointSelectionOptions(
            int playerDistanceMinimum,
            int entryDistanceMinimum,
            int softDistanceStep,
            int maxSoftRelaxations,
            int maxAttempts)
        {
            if (playerDistanceMinimum < 0 || entryDistanceMinimum < 0 || softDistanceStep < 0
                || maxSoftRelaxations < 0 || maxAttempts <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerDistanceMinimum));
            }

            PlayerDistanceMinimum = playerDistanceMinimum;
            EntryDistanceMinimum = entryDistanceMinimum;
            SoftDistanceStep = softDistanceStep;
            MaxSoftRelaxations = maxSoftRelaxations;
            MaxAttempts = maxAttempts;
        }

        public int PlayerDistanceMinimum { get; }
        public int EntryDistanceMinimum { get; }
        public int SoftDistanceStep { get; }
        public int MaxSoftRelaxations { get; }
        public int MaxAttempts { get; }
    }

    /// <summary>
    /// Runtime candidate extends the authored point with transient occupancy
    /// and distance measurements supplied by Unity. This keeps role matching
    /// deterministic while leaving physics queries outside FPG.Run.
    /// </summary>
    public readonly struct FpgSpawnPointRuntimeCandidate
    {
        public FpgSpawnPointRuntimeCandidate(
            FpgSpawnPointCandidate point,
            int playerDistanceUnits,
            int entryDistanceUnits,
            int occupiedCount = 0)
        {
            if (playerDistanceUnits < 0 || entryDistanceUnits < 0 || occupiedCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerDistanceUnits));
            }

            Point = point;
            PlayerDistanceUnits = playerDistanceUnits;
            EntryDistanceUnits = entryDistanceUnits;
            OccupiedCount = occupiedCount;
        }

        public FpgSpawnPointCandidate Point { get; }
        public int PlayerDistanceUnits { get; }
        public int EntryDistanceUnits { get; }
        public int OccupiedCount { get; }
        public bool IsFull => OccupiedCount >= Point.Capacity;
    }

    public readonly struct FpgSpawnPointSelectionResult
    {
        public FpgSpawnPointSelectionResult(
            DomainResult result,
            string pointId,
            int relaxationLevel,
            int candidateIndex)
        {
            Result = result;
            PointId = pointId ?? string.Empty;
            RelaxationLevel = relaxationLevel;
            CandidateIndex = candidateIndex;
        }

        public DomainResult Result { get; }
        public string PointId { get; }
        public int RelaxationLevel { get; }
        public int CandidateIndex { get; }
        public bool IsSuccess => Result.IsSuccess && !string.IsNullOrEmpty(PointId);
    }

    public static class FpgSpawnPointSelector
    {
        private const ulong SpawnPointDomain = 0x4650475F53504F54UL;

        public static FpgSpawnPointSelectionResult Select(
            FpgEnemyRole enemyRole,
            FpgSpawnPointRuntimeCandidate[] candidates,
            FpgSpawnPointSelectionOptions options,
            FpgEncounterRunContext runContext,
            int spawnSequence,
            int attempt)
        {
            if (candidates == null || candidates.Length == 0 || !runContext.IsValid)
            {
                return Failure(RejectReason.BufferCapacity);
            }

            if (spawnSequence < 0 || attempt < 0)
            {
                return Failure(RejectReason.InvalidDefinition);
            }

            int maxRelaxation = options.MaxSoftRelaxations;
            for (int relaxation = 0; relaxation <= maxRelaxation; relaxation++)
            {
                int playerMinimum = Math.Max(0,
                    options.PlayerDistanceMinimum - relaxation * options.SoftDistanceStep);
                int entryMinimum = Math.Max(0,
                    options.EntryDistanceMinimum - relaxation * options.SoftDistanceStep);

                int validCount = 0;
                for (int index = 0; index < candidates.Length; index++)
                {
                    FpgSpawnPointRuntimeCandidate candidate = candidates[index];
                    if (candidate.IsFull
                        || !RoleMatches(enemyRole, candidate.Point.Role)
                        || candidate.PlayerDistanceUnits < playerMinimum
                        || candidate.EntryDistanceUnits < entryMinimum)
                    {
                        continue;
                    }

                    validCount++;
                }

                if (validCount == 0)
                {
                    continue;
                }

                ulong random = runContext.DeriveSeed(
                    SpawnPointDomain,
                    unchecked((ulong)spawnSequence),
                    unchecked((ulong)(attempt + relaxation)));
                int selectedOrdinal = (int)(random % (ulong)validCount);
                for (int index = 0; index < candidates.Length; index++)
                {
                    FpgSpawnPointRuntimeCandidate candidate = candidates[index];
                    if (candidate.IsFull
                        || !RoleMatches(enemyRole, candidate.Point.Role)
                        || candidate.PlayerDistanceUnits < playerMinimum
                        || candidate.EntryDistanceUnits < entryMinimum)
                    {
                        continue;
                    }

                    if (selectedOrdinal-- == 0)
                    {
                        return new FpgSpawnPointSelectionResult(
                            DomainResult.Success,
                            candidate.Point.PointId,
                            relaxation,
                            index);
                    }
                }
            }

            return Failure(RejectReason.InvalidTarget);
        }

        private static bool RoleMatches(FpgEnemyRole enemyRole, FpgEnemyRole pointRole)
        {
            return pointRole == FpgEnemyRole.Any || enemyRole == FpgEnemyRole.Any || enemyRole == pointRole;
        }

        private static FpgSpawnPointSelectionResult Failure(RejectReason reason)
        {
            return new FpgSpawnPointSelectionResult(
                DomainResult.Rejected(reason),
                string.Empty,
                -1,
                -1);
        }
    }
}

