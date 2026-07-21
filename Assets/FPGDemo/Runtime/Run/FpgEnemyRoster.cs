using System;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    /// <summary>
    /// A fixed-capacity enemy slot. The slot identity is stable for the life of
    /// a room, while RuntimeId changes for each reservation.
    /// </summary>
    public sealed class FpgEnemySlot
    {
        internal FpgEnemySlot(int slotIndex)
        {
            SlotIndex = slotIndex;
            Reset();
        }

        public int SlotIndex { get; }
        public RuntimeId RuntimeId { get; private set; }
        public string EnemyDefinitionId { get; private set; }
        public string SpawnEntryId { get; private set; }
        public int SpawnSequence { get; private set; }
        public int RecursionDepth { get; private set; }
        public int WaveIndex { get; private set; }
        public string SpawnPointId { get; private set; }
        public FpgEnemyRole Role { get; private set; }
        public FpgSpawnEntryState State { get; private set; }
        public TickIndex WarningUntilTick { get; private set; }
        public TickIndex ActivationTick { get; private set; }
        public int Life { get; private set; }
        public int MaxLife { get; private set; }
        public int Break { get; private set; }
        public int MaxBreak { get; private set; }
        public int CapWeight { get; private set; }
        public bool IsReserved => State != FpgSpawnEntryState.Planned
            && State != FpgSpawnEntryState.Canceled
            && State != FpgSpawnEntryState.Failed;
        public bool IsLive => State == FpgSpawnEntryState.Warning
            || State == FpgSpawnEntryState.Queued
            || State == FpgSpawnEntryState.Active;
        public bool IsActive => State == FpgSpawnEntryState.Active;

        internal void Reserve(
            FpgSpawnEntry entry,
            RuntimeId runtimeId,
            string spawnPointId,
            TickIndex warningUntilTick)
        {
            RuntimeId = runtimeId;
            EnemyDefinitionId = entry.EnemyDefinitionId;
            SpawnEntryId = entry.SpawnEntryId;
            SpawnSequence = entry.SpawnSequence;
            RecursionDepth = entry.RecursionDepth;
            WaveIndex = entry.WaveIndex;
            SpawnPointId = spawnPointId ?? string.Empty;
            Role = entry.Role;
            State = FpgSpawnEntryState.Warning;
            WarningUntilTick = warningUntilTick;
            ActivationTick = TickIndex.Invalid;
            Life = 0;
            MaxLife = 0;
            Break = 0;
            MaxBreak = 0;
            CapWeight = entry.CapWeight;
        }

        internal void SetStats(int life, int breakValue)
        {
            MaxLife = Math.Max(0, life);
            Life = MaxLife;
            MaxBreak = Math.Max(0, breakValue);
            Break = MaxBreak;
        }

        internal void Activate(TickIndex tick)
        {
            if (State == FpgSpawnEntryState.Warning || State == FpgSpawnEntryState.Queued)
            {
                State = FpgSpawnEntryState.Active;
                ActivationTick = tick;
            }
        }

        internal void ShiftWarningTicks(long delta)
        {
            if (delta <= 0L || !WarningUntilTick.IsValid
                || (State != FpgSpawnEntryState.Warning && State != FpgSpawnEntryState.Queued))
            {
                return;
            }

            WarningUntilTick = WarningUntilTick.Value > long.MaxValue - delta
                ? new TickIndex(long.MaxValue)
                : new TickIndex(WarningUntilTick.Value + delta);
        }

        internal void MarkDead()
        {
            if (State == FpgSpawnEntryState.Active || State == FpgSpawnEntryState.Warning)
            {
                State = FpgSpawnEntryState.Dead;
                Life = 0;
            }
        }

        internal void Cancel()
        {
            if (State != FpgSpawnEntryState.Dead)
            {
                State = FpgSpawnEntryState.Canceled;
            }
        }

        internal void Fail()
        {
            State = FpgSpawnEntryState.Failed;
        }

        internal void Reset()
        {
            RuntimeId = RuntimeId.Invalid;
            EnemyDefinitionId = string.Empty;
            SpawnEntryId = string.Empty;
            SpawnSequence = -1;
            RecursionDepth = 0;
            WaveIndex = -1;
            SpawnPointId = string.Empty;
            Role = FpgEnemyRole.Any;
            State = FpgSpawnEntryState.Planned;
            WarningUntilTick = TickIndex.Invalid;
            ActivationTick = TickIndex.Invalid;
            Life = 0;
            MaxLife = 0;
            Break = 0;
            MaxBreak = 0;
            CapWeight = 0;
        }
    }

    /// <summary>
    /// Fixed-capacity roster keyed by RuntimeId. It deliberately uses linear
    /// scans to make capacity and lookup cost explicit and bounded.
    /// </summary>
    public sealed class FpgEnemyRoster
    {
        private readonly FpgEnemySlot[] slots;

        public FpgEnemyRoster(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            slots = new FpgEnemySlot[capacity];
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index] = new FpgEnemySlot(index);
            }
        }

        public int Capacity => slots.Length;
        public int ReservedCount { get; private set; }
        public int LivingCount { get; private set; }
        public int ActiveCapWeight { get; private set; }
        public int ReservedCapWeight { get; private set; }

        public FpgEnemySlot GetSlot(int index)
        {
            if (index < 0 || index >= slots.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return slots[index];
        }

        public bool TryGet(RuntimeId runtimeId, out FpgEnemySlot slot)
        {
            int index = Find(runtimeId);
            slot = index < 0 ? null : slots[index];
            return slot != null;
        }

        public bool TryGetBySpawnEntry(string spawnEntryId, out FpgEnemySlot slot)
        {
            if (string.IsNullOrEmpty(spawnEntryId))
            {
                slot = null;
                return false;
            }

            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index].IsReserved
                    && string.Equals(slots[index].SpawnEntryId, spawnEntryId, StringComparison.Ordinal))
                {
                    slot = slots[index];
                    return true;
                }
            }

            slot = null;
            return false;
        }

        public DomainResult TryReserve(
            FpgSpawnEntry entry,
            RuntimeId runtimeId,
            string spawnPointId,
            TickIndex warningUntilTick,
            int life,
            int breakValue,
            out FpgEnemySlot slot)
        {
            slot = null;
            if (!runtimeId.IsValid || string.IsNullOrWhiteSpace(spawnPointId)
                || !warningUntilTick.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (Find(runtimeId) >= 0)
            {
                return DomainResult.Rejected(RejectReason.DuplicateSequence);
            }

            if (TryGetBySpawnEntry(entry.SpawnEntryId, out FpgEnemySlot duplicateEntry))
            {
                return DomainResult.Rejected(RejectReason.DuplicateSequence);
            }

            for (int index = 0; index < slots.Length; index++)
            {
                FpgEnemySlot candidate = slots[index];
                if (candidate.IsReserved)
                {
                    continue;
                }

                candidate.Reserve(entry, runtimeId, spawnPointId, warningUntilTick);
                candidate.SetStats(life, breakValue);
                ReservedCount++;
                LivingCount++;
                ReservedCapWeight = SaturatingAdd(ReservedCapWeight, entry.CapWeight);
                slot = candidate;
                return DomainResult.Success;
            }

            return DomainResult.Rejected(RejectReason.BufferCapacity);
        }

        public DomainResult TryActivate(RuntimeId runtimeId, TickIndex tick)
        {
            if (!TryGet(runtimeId, out FpgEnemySlot slot))
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            if (slot.State == FpgSpawnEntryState.Active)
            {
                return DomainResult.Success;
            }

            if (slot.State != FpgSpawnEntryState.Warning && slot.State != FpgSpawnEntryState.Queued)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            slot.Activate(tick);
            ActiveCapWeight = SaturatingAdd(ActiveCapWeight, slot.CapWeight);
            return DomainResult.Success;
        }

        public DomainResult TryMarkDead(RuntimeId runtimeId)
        {
            if (!TryGet(runtimeId, out FpgEnemySlot slot))
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            if (slot.State == FpgSpawnEntryState.Dead)
            {
                return DomainResult.Success;
            }

            if (slot.State != FpgSpawnEntryState.Active && slot.State != FpgSpawnEntryState.Warning)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            bool active = slot.IsActive;
            slot.MarkDead();
            LivingCount = Math.Max(0, LivingCount - 1);
            ReservedCapWeight = Math.Max(0, ReservedCapWeight - slot.CapWeight);
            if (active)
            {
                ActiveCapWeight = Math.Max(0, ActiveCapWeight - slot.CapWeight);
            }

            return DomainResult.Success;
        }

        public DomainResult TryRelease(RuntimeId runtimeId)
        {
            if (!TryGet(runtimeId, out FpgEnemySlot slot))
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            if (slot.IsLive)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (slot.IsReserved)
            {
                ReservedCount = Math.Max(0, ReservedCount - 1);
            }

            slot.Reset();
            return DomainResult.Success;
        }

        public DomainResult ShiftWarningTicks(long delta)
        {
            if (delta < 0L)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            for (int index = 0; index < slots.Length; index++)
            {
                slots[index].ShiftWarningTicks(delta);
            }

            return DomainResult.Success;
        }

        public void Clear()
        {
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index].Reset();
            }

            ReservedCount = 0;
            LivingCount = 0;
            ActiveCapWeight = 0;
            ReservedCapWeight = 0;
        }

        public int CopyLiveSlots(FpgEnemySlot[] destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            int copied = 0;
            for (int index = 0; index < slots.Length && copied < destination.Length; index++)
            {
                if (!slots[index].IsLive)
                {
                    continue;
                }

                destination[copied++] = slots[index];
            }

            return copied;
        }

        private int Find(RuntimeId runtimeId)
        {
            if (!runtimeId.IsValid)
            {
                return -1;
            }

            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index].IsReserved && slots[index].RuntimeId == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int SaturatingAdd(int left, int right)
        {
            return right > int.MaxValue - left ? int.MaxValue : left + right;
        }
    }
}







