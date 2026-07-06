using System;
using System.Collections.Generic;

namespace NewFPG.Level
{
    public static class LevelEncounterResolver
    {
        public static LevelSpawnRequest[] Resolve(LevelEncounterWave wave, Random random)
        {
            if (wave == null)
            {
                return Array.Empty<LevelSpawnRequest>();
            }

            random = random ?? new Random();
            switch (wave.selectionMode)
            {
                case LevelSpawnSelectionMode.RandomPool:
                    return ResolveRandomPool(wave.randomPool, random);
                default:
                    return ResolvePresetGroup(wave.presetGroups, random);
            }
        }

        private static LevelSpawnRequest[] ResolvePresetGroup(IReadOnlyList<LevelSpawnGroup> groups, Random random)
        {
            int groupIndex = PickWeightedGroup(groups, random);
            if (groupIndex < 0)
            {
                return Array.Empty<LevelSpawnRequest>();
            }

            LevelSpawnGroup group = groups[groupIndex];
            List<LevelSpawnRequest> requests = new List<LevelSpawnRequest>();
            if (group.entries == null)
            {
                return requests.ToArray();
            }

            for (int i = 0; i < group.entries.Count; i++)
            {
                LevelSpawnEntry entry = group.entries[i];
                if (!CanSpawn(entry))
                {
                    continue;
                }

                for (int count = 0; count < entry.SpawnCount; count++)
                {
                    requests.Add(new LevelSpawnRequest(entry));
                }
            }

            return requests.ToArray();
        }

        private static LevelSpawnRequest[] ResolveRandomPool(LevelRandomPool pool, Random random)
        {
            if (pool == null)
            {
                return Array.Empty<LevelSpawnRequest>();
            }

            int minCount = Math.Max(0, pool.minCount);
            int maxCount = Math.Max(minCount, pool.maxCount);
            int spawnCount = random.Next(minCount, maxCount + 1);
            List<LevelSpawnRequest> requests = new List<LevelSpawnRequest>(spawnCount);
            for (int i = 0; i < spawnCount; i++)
            {
                int entryIndex = PickWeightedEntry(pool.candidates, random);
                if (entryIndex < 0)
                {
                    break;
                }

                requests.Add(new LevelSpawnRequest(pool.candidates[entryIndex]));
            }

            return requests.ToArray();
        }

        private static int PickWeightedGroup(IReadOnlyList<LevelSpawnGroup> groups, Random random)
        {
            if (groups == null || groups.Count == 0)
            {
                return -1;
            }

            float totalWeight = 0f;
            for (int i = 0; i < groups.Count; i++)
            {
                LevelSpawnGroup group = groups[i];
                if (group != null && group.weight > 0f && HasSpawnEntries(group.entries))
                {
                    totalWeight += group.weight;
                }
            }

            if (totalWeight <= 0f)
            {
                return -1;
            }

            double roll = random.NextDouble() * totalWeight;
            for (int i = 0; i < groups.Count; i++)
            {
                LevelSpawnGroup group = groups[i];
                if (group == null || group.weight <= 0f || !HasSpawnEntries(group.entries))
                {
                    continue;
                }

                roll -= group.weight;
                if (roll <= 0d)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int PickWeightedEntry(IReadOnlyList<LevelSpawnEntry> entries, Random random)
        {
            if (entries == null || entries.Count == 0)
            {
                return -1;
            }

            float totalWeight = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                LevelSpawnEntry entry = entries[i];
                if (CanSpawnCandidate(entry))
                {
                    totalWeight += entry.SpawnWeight;
                }
            }

            if (totalWeight <= 0f)
            {
                return -1;
            }

            double roll = random.NextDouble() * totalWeight;
            for (int i = 0; i < entries.Count; i++)
            {
                LevelSpawnEntry entry = entries[i];
                if (!CanSpawnCandidate(entry))
                {
                    continue;
                }

                roll -= entry.SpawnWeight;
                if (roll <= 0d)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool HasSpawnEntries(IReadOnlyList<LevelSpawnEntry> entries)
        {
            if (entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (CanSpawn(entries[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanSpawn(LevelSpawnEntry entry)
        {
            return entry != null && entry.monsterPrefab != null && entry.SpawnCount > 0;
        }

        private static bool CanSpawnCandidate(LevelSpawnEntry entry)
        {
            return entry != null && entry.monsterPrefab != null && entry.SpawnWeight > 0f;
        }
    }
}
