using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Battle
{
    [Serializable]
    public sealed class PrepLoadoutEntry
    {
        public string artifactId;
        public int startSlotIndex;
    }

    [Serializable]
    public sealed class PrepLoadoutState
    {
        [SerializeField] private int capacity = 10;
        [SerializeField] private List<PrepLoadoutEntry> equippedArtifacts = new List<PrepLoadoutEntry>();
        [SerializeField] private string selectedArtifactId;

        public int Capacity
        {
            get { return Mathf.Max(0, capacity); }
            set { capacity = Mathf.Max(0, value); }
        }

        public IReadOnlyList<PrepLoadoutEntry> EquippedArtifacts
        {
            get { return equippedArtifacts; }
        }

        public string SelectedArtifactId
        {
            get { return selectedArtifactId; }
            set { selectedArtifactId = value; }
        }

        public int EquippedCount
        {
            get { return equippedArtifacts != null ? equippedArtifacts.Count : 0; }
        }

        public int UsedCapacity(ArtifactCatalog catalog)
        {
            if (catalog == null || equippedArtifacts == null)
            {
                return 0;
            }

            int used = 0;
            for (int i = 0; i < equippedArtifacts.Count; i++)
            {
                ArtifactCombatProfile profile = catalog.FindById(equippedArtifacts[i].artifactId);
                if (profile != null)
                {
                    used += Mathf.Max(1, profile.size);
                }
            }

            return used;
        }

        public int RemainingCapacity(ArtifactCatalog catalog)
        {
            return Mathf.Max(0, Capacity - UsedCapacity(catalog));
        }

        public bool IsValid(ArtifactCatalog catalog, out string reason)
        {
            reason = string.Empty;
            if (equippedArtifacts == null)
            {
                reason = "器匣数据缺失。";
                return false;
            }

            if (catalog == null)
            {
                reason = "法宝数据缺失。";
                return false;
            }

            bool[] occupiedSlots = new bool[Capacity];
            for (int i = 0; i < equippedArtifacts.Count; i++)
            {
                PrepLoadoutEntry entry = equippedArtifacts[i];
                ArtifactCombatProfile profile = catalog.FindById(entry.artifactId);
                if (profile == null)
                {
                    reason = "法宝数据缺失。";
                    return false;
                }

                int size = Mathf.Max(1, profile.size);
                if (entry.startSlotIndex < 0 || entry.startSlotIndex + size > Capacity)
                {
                    reason = "有法宝超出器匣范围。";
                    return false;
                }

                for (int slotIndex = entry.startSlotIndex; slotIndex < entry.startSlotIndex + size; slotIndex++)
                {
                    if (occupiedSlots[slotIndex])
                    {
                        reason = "有法宝占格重叠。";
                        return false;
                    }

                    occupiedSlots[slotIndex] = true;
                }
            }

            return true;
        }

        public bool IsEquipped(string artifactId)
        {
            return IndexOf(artifactId) >= 0;
        }

        public int IndexOf(string artifactId)
        {
            if (string.IsNullOrWhiteSpace(artifactId) || equippedArtifacts == null)
            {
                return -1;
            }

            for (int i = 0; i < equippedArtifacts.Count; i++)
            {
                if (string.Equals(equippedArtifacts[i].artifactId, artifactId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        public PrepLoadoutEntry FindEntry(string artifactId)
        {
            int index = IndexOf(artifactId);
            return index >= 0 ? equippedArtifacts[index] : null;
        }

        public PrepLoadoutEntry FindEntryAtSlot(ArtifactCatalog catalog, int slotIndex)
        {
            if (catalog == null || equippedArtifacts == null)
            {
                return null;
            }

            for (int i = 0; i < equippedArtifacts.Count; i++)
            {
                PrepLoadoutEntry entry = equippedArtifacts[i];
                ArtifactCombatProfile profile = catalog.FindById(entry.artifactId);
                if (profile == null)
                {
                    continue;
                }

                int start = Mathf.Clamp(entry.startSlotIndex, 0, Mathf.Max(0, Capacity - 1));
                int end = start + Mathf.Max(1, profile.size);
                if (slotIndex >= start && slotIndex < end)
                {
                    return entry;
                }
            }

            return null;
        }

        public bool CanPlace(ArtifactCombatProfile profile, ArtifactCatalog catalog, int startSlotIndex, string movingArtifactId, out string reason)
        {
            reason = string.Empty;
            if (profile == null)
            {
                reason = "法宝数据缺失。";
                return false;
            }

            if (catalog == null || equippedArtifacts == null)
            {
                reason = "器匣数据缺失。";
                return false;
            }

            int size = Mathf.Max(1, profile.size);
            if (startSlotIndex < 0 || startSlotIndex + size > Capacity)
            {
                reason = "该位置放不下。";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(movingArtifactId) &&
                !string.Equals(movingArtifactId, profile.artifactId, StringComparison.Ordinal))
            {
                reason = "移动目标不匹配。";
                return false;
            }

            for (int i = 0; i < equippedArtifacts.Count; i++)
            {
                PrepLoadoutEntry otherEntry = equippedArtifacts[i];
                if (string.Equals(otherEntry.artifactId, profile.artifactId, StringComparison.Ordinal))
                {
                    continue;
                }

                ArtifactCombatProfile otherProfile = catalog.FindById(otherEntry.artifactId);
                if (otherProfile == null)
                {
                    continue;
                }

                int otherStart = otherEntry.startSlotIndex;
                int otherSize = Mathf.Max(1, otherProfile.size);
                bool overlaps = startSlotIndex < otherStart + otherSize && startSlotIndex + size > otherStart;
                if (overlaps)
                {
                    reason = "该位置已被占用。";
                    return false;
                }
            }

            return true;
        }

        public bool TryPlace(ArtifactCombatProfile profile, ArtifactCatalog catalog, int startSlotIndex, out string reason)
        {
            reason = string.Empty;
            if (profile == null)
            {
                reason = "法宝数据缺失。";
                return false;
            }

            if (IsEquipped(profile.artifactId))
            {
                reason = "该法宝已在器匣中。";
                return false;
            }

            if (!CanPlace(profile, catalog, startSlotIndex, string.Empty, out reason))
            {
                return false;
            }

            equippedArtifacts.Add(new PrepLoadoutEntry
            {
                artifactId = profile.artifactId,
                startSlotIndex = startSlotIndex,
            });
            SortEntries();
            selectedArtifactId = profile.artifactId;
            return true;
        }

        public bool TryMove(string artifactId, ArtifactCatalog catalog, int startSlotIndex, out string reason)
        {
            reason = string.Empty;
            PrepLoadoutEntry entry = FindEntry(artifactId);
            if (entry == null)
            {
                reason = "先选中一个器匣法宝。";
                return false;
            }

            ArtifactCombatProfile profile = catalog != null ? catalog.FindById(artifactId) : null;
            if (!CanPlace(profile, catalog, startSlotIndex, artifactId, out reason))
            {
                return false;
            }

            entry.startSlotIndex = startSlotIndex;
            SortEntries();
            selectedArtifactId = artifactId;
            return true;
        }

        public bool CanInsertMove(string artifactId, ArtifactCatalog catalog, int targetSlotIndex, bool insertAfterTarget, out string reason)
        {
            List<PrepLoadoutEntry> nextEntries;
            return TryBuildInsertedLayout(artifactId, catalog, targetSlotIndex, insertAfterTarget, out nextEntries, out reason);
        }

        public bool TryGetInsertMoveStartSlot(
            string artifactId,
            ArtifactCatalog catalog,
            int targetSlotIndex,
            bool insertAfterTarget,
            out int startSlotIndex,
            out string reason)
        {
            startSlotIndex = 0;
            List<PrepLoadoutEntry> nextEntries;
            if (!TryBuildInsertedLayout(artifactId, catalog, targetSlotIndex, insertAfterTarget, out nextEntries, out reason))
            {
                return false;
            }

            for (int i = 0; i < nextEntries.Count; i++)
            {
                if (string.Equals(nextEntries[i].artifactId, artifactId, StringComparison.Ordinal))
                {
                    startSlotIndex = nextEntries[i].startSlotIndex;
                    return true;
                }
            }

            reason = "插入目标不存在。";
            return false;
        }

        public bool TryInsertMove(string artifactId, ArtifactCatalog catalog, int targetSlotIndex, bool insertAfterTarget, out string reason)
        {
            List<PrepLoadoutEntry> nextEntries;
            if (!TryBuildInsertedLayout(artifactId, catalog, targetSlotIndex, insertAfterTarget, out nextEntries, out reason))
            {
                return false;
            }

            equippedArtifacts.Clear();
            equippedArtifacts.AddRange(nextEntries);
            selectedArtifactId = artifactId;
            return true;
        }

        public bool TryRemove(string artifactId)
        {
            int index = IndexOf(artifactId);
            if (index < 0)
            {
                return false;
            }

            equippedArtifacts.RemoveAt(index);
            if (string.Equals(selectedArtifactId, artifactId, StringComparison.Ordinal))
            {
                selectedArtifactId = equippedArtifacts.Count > 0
                    ? equippedArtifacts[Mathf.Clamp(index, 0, equippedArtifacts.Count - 1)].artifactId
                    : string.Empty;
            }

            return true;
        }

        public ArtifactQueueState ToQueueState(ArtifactCatalog catalog)
        {
            ArtifactQueueState queueState = new ArtifactQueueState();
            queueState.capacity = Capacity;
            queueState.equippedArtifacts = new List<ArtifactRuntimeState>();
            queueState.slots = new List<ArtifactQueueSlot>();

            if (catalog == null || equippedArtifacts == null)
            {
                queueState.isLoadoutValid = true;
                FillEmptySlots(queueState);
                return queueState;
            }

            SortEntries();
            int usedCapacity = 0;
            for (int i = 0; i < equippedArtifacts.Count; i++)
            {
                PrepLoadoutEntry entry = equippedArtifacts[i];
                ArtifactCombatProfile profile = catalog.FindById(entry.artifactId);
                if (profile == null)
                {
                    continue;
                }

                ArtifactRuntimeState runtime = new ArtifactRuntimeState();
                runtime.runtimeId = profile.artifactId;
                runtime.profile = profile;
                runtime.autoEnabled = profile.autoEnabled;
                runtime.isReady = true;
                runtime.cooldownRemaining = 0f;
                runtime.canProcSupply = profile.canProcSupply;
                queueState.equippedArtifacts.Add(runtime);

                int size = Mathf.Max(1, profile.size);
                usedCapacity += size;
                for (int j = 0; j < size; j++)
                {
                    int slotIndex = entry.startSlotIndex + j;
                    if (slotIndex < 0 || slotIndex >= Capacity)
                    {
                        continue;
                    }

                    EnsureSlotCount(queueState, slotIndex + 1);
                    queueState.slots[slotIndex] = new ArtifactQueueSlot
                    {
                        slotIndex = slotIndex,
                        occupant = runtime,
                        isLeftBoundary = j == 0,
                        isRightBoundary = j == size - 1,
                    };
                }
            }

            queueState.usedCapacity = usedCapacity;
            string invalidReason;
            queueState.isLoadoutValid = IsValid(catalog, out invalidReason);
            queueState.invalidReason = queueState.isLoadoutValid ? string.Empty : invalidReason;
            FillEmptySlots(queueState);
            queueState.EnsureCycleInitialized();
            return queueState;
        }

        public void Clear()
        {
            equippedArtifacts.Clear();
            selectedArtifactId = string.Empty;
        }

        public void SetArtifacts(IEnumerable<string> artifactIds)
        {
            Clear();
            if (artifactIds == null)
            {
                return;
            }

            int nextSlot = 0;
            foreach (string artifactId in artifactIds)
            {
                if (string.IsNullOrWhiteSpace(artifactId))
                {
                    continue;
                }

                equippedArtifacts.Add(new PrepLoadoutEntry
                {
                    artifactId = artifactId,
                    startSlotIndex = nextSlot,
                });
                nextSlot++;
            }

            selectedArtifactId = equippedArtifacts.Count > 0 ? equippedArtifacts[0].artifactId : string.Empty;
        }

        private void SortEntries()
        {
            equippedArtifacts.Sort((left, right) => left.startSlotIndex.CompareTo(right.startSlotIndex));
        }

        private bool TryBuildInsertedLayout(
            string artifactId,
            ArtifactCatalog catalog,
            int targetSlotIndex,
            bool insertAfterTarget,
            out List<PrepLoadoutEntry> nextEntries,
            out string reason)
        {
            nextEntries = null;
            reason = string.Empty;

            if (catalog == null || equippedArtifacts == null)
            {
                reason = "器匣数据缺失。";
                return false;
            }

            PrepLoadoutEntry movingEntry = FindEntry(artifactId);
            if (movingEntry == null)
            {
                reason = "先选中一个器匣法宝。";
                return false;
            }

            ArtifactCombatProfile movingProfile = catalog.FindById(artifactId);
            if (movingProfile == null)
            {
                reason = "法宝数据缺失。";
                return false;
            }

            PrepLoadoutEntry targetEntry = FindEntryAtSlot(catalog, Mathf.Clamp(targetSlotIndex, 0, Mathf.Max(0, Capacity - 1)));
            if (targetEntry == null || string.Equals(targetEntry.artifactId, artifactId, StringComparison.Ordinal))
            {
                reason = "没有可插入的目标。";
                return false;
            }

            List<PrepLoadoutEntry> orderedEntries = new List<PrepLoadoutEntry>();
            for (int i = 0; i < equippedArtifacts.Count; i++)
            {
                PrepLoadoutEntry entry = equippedArtifacts[i];
                if (string.Equals(entry.artifactId, artifactId, StringComparison.Ordinal))
                {
                    continue;
                }

                orderedEntries.Add(new PrepLoadoutEntry
                {
                    artifactId = entry.artifactId,
                    startSlotIndex = entry.startSlotIndex,
                });
            }

            orderedEntries.Sort((left, right) => left.startSlotIndex.CompareTo(right.startSlotIndex));

            int insertIndex = -1;
            for (int i = 0; i < orderedEntries.Count; i++)
            {
                if (string.Equals(orderedEntries[i].artifactId, targetEntry.artifactId, StringComparison.Ordinal))
                {
                    insertIndex = insertAfterTarget ? i + 1 : i;
                    break;
                }
            }

            if (insertIndex < 0)
            {
                reason = "插入目标不存在。";
                return false;
            }

            orderedEntries.Insert(Mathf.Clamp(insertIndex, 0, orderedEntries.Count), new PrepLoadoutEntry
            {
                artifactId = artifactId,
                startSlotIndex = 0,
            });

            int nextSlot = 0;
            for (int i = 0; i < orderedEntries.Count; i++)
            {
                ArtifactCombatProfile profile = catalog.FindById(orderedEntries[i].artifactId);
                if (profile == null)
                {
                    reason = "法宝数据缺失。";
                    return false;
                }

                int size = Mathf.Max(1, profile.size);
                if (nextSlot + size > Capacity)
                {
                    reason = "器匣容量不足。";
                    return false;
                }

                orderedEntries[i].startSlotIndex = nextSlot;
                nextSlot += size;
            }

            nextEntries = orderedEntries;
            return true;
        }

        private static void EnsureSlotCount(ArtifactQueueState queueState, int count)
        {
            while (queueState.slots.Count < count)
            {
                int slotIndex = queueState.slots.Count;
                queueState.slots.Add(new ArtifactQueueSlot
                {
                    slotIndex = slotIndex,
                    occupant = null,
                    isLeftBoundary = false,
                    isRightBoundary = false,
                });
            }
        }

        private static void FillEmptySlots(ArtifactQueueState queueState)
        {
            EnsureSlotCount(queueState, queueState.capacity);
            for (int i = 0; i < queueState.slots.Count; i++)
            {
                if (queueState.slots[i] == null)
                {
                    queueState.slots[i] = new ArtifactQueueSlot
                    {
                        slotIndex = i,
                        occupant = null,
                        isLeftBoundary = false,
                        isRightBoundary = false,
                    };
                }
            }
        }
    }
}
