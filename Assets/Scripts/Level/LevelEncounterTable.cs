using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Level
{
    public enum LevelSpawnSelectionMode
    {
        PresetGroupRandom,
        RandomPool,
    }

    [CreateAssetMenu(fileName = "LevelEncounterTable", menuName = "NewFPG/Level/Encounter Table")]
    public sealed class LevelEncounterTable : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Settings/Level/LevelEncounterTable.asset";

        [SerializeField, TextArea, Tooltip("策划备注，只用于说明这张刷怪表的用途，不参与运行时逻辑。")]
        private string tableNote;

        [SerializeField, Tooltip("可被路线房间引用的 encounter 配置。房间通过 encounterId 或选项的 encounterIdOverride 查找这里的条目。")]
        private List<LevelEncounterDefinition> encounters = new List<LevelEncounterDefinition>();

        public string TableNote => tableNote;
        public IReadOnlyList<LevelEncounterDefinition> Encounters => encounters;

        public LevelEncounterDefinition FindEncounter(string encounterId)
        {
            if (string.IsNullOrWhiteSpace(encounterId) || encounters == null)
            {
                return null;
            }

            for (int i = 0; i < encounters.Count; i++)
            {
                LevelEncounterDefinition encounter = encounters[i];
                if (encounter != null && encounter.encounterId == encounterId)
                {
                    return encounter;
                }
            }

            return null;
        }

        public void SetTableNote(string nextTableNote)
        {
            tableNote = nextTableNote ?? string.Empty;
        }

        public void SetEncounters(IEnumerable<LevelEncounterDefinition> nextEncounters)
        {
            encounters.Clear();
            if (nextEncounters != null)
            {
                encounters.AddRange(nextEncounters);
            }

            Normalize();
        }

        private void OnValidate()
        {
            Normalize();
        }

        private void Normalize()
        {
            if (encounters == null)
            {
                encounters = new List<LevelEncounterDefinition>();
            }

            for (int i = 0; i < encounters.Count; i++)
            {
                encounters[i]?.Normalize();
            }
        }
    }

    [Serializable]
    public sealed class LevelEncounterDefinition
    {
        [Tooltip("Encounter 内部 id。路线表中的 encounterId 必须和这里完全一致。")]
        public string encounterId;

        [TextArea, Tooltip("策划备注，只用于说明这个 encounter 的用途，不参与运行时逻辑。")]
        public string encounterNote;

        [Tooltip("按顺序执行的刷怪波次。当前波次全部怪物死亡后，才会进入下一波。")]
        public List<LevelEncounterWave> waves = new List<LevelEncounterWave>();

        public void Normalize()
        {
            if (waves == null)
            {
                waves = new List<LevelEncounterWave>();
            }

            for (int i = 0; i < waves.Count; i++)
            {
                waves[i]?.Normalize();
            }
        }
    }

    [Serializable]
    public sealed class LevelEncounterWave
    {
        [Tooltip("波次 id，仅用于配置辨识和错误日志，不参与查找。")]
        public string waveId = "wave_1";

        [TextArea, Tooltip("策划备注，只用于说明这个波次的用途，不参与运行时逻辑。")]
        public string waveNote;

        [Tooltip("上一波清空后到本波开始前的延迟秒数。第一波通常填 0。")]
        [Min(0f)] public float delayAfterPreviousWave;

        [Tooltip("本波选怪方式：预设组随机或随机池抽取。")]
        public LevelSpawnSelectionMode selectionMode = LevelSpawnSelectionMode.PresetGroupRandom;

        [Tooltip("预设组随机使用：从这些固定怪物组合中按权重选中一组，并刷出该组全部条目。")]
        public List<LevelSpawnGroup> presetGroups = new List<LevelSpawnGroup>();

        [Tooltip("随机池使用：在数量区间内决定总数量，再从候选池按权重逐个抽取。")]
        public LevelRandomPool randomPool = new LevelRandomPool();

        public void Normalize()
        {
            delayAfterPreviousWave = Mathf.Max(0f, delayAfterPreviousWave);
            if (presetGroups == null)
            {
                presetGroups = new List<LevelSpawnGroup>();
            }

            for (int i = 0; i < presetGroups.Count; i++)
            {
                presetGroups[i]?.Normalize();
            }

            if (randomPool == null)
            {
                randomPool = new LevelRandomPool();
            }

            randomPool.Normalize();
        }
    }

    [Serializable]
    public sealed class LevelSpawnGroup
    {
        [Tooltip("预设组 id，仅用于配置辨识，不参与运行时查找。")]
        public string groupId;

        [Tooltip("该预设组被选中的权重。0 表示不会被抽中。")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("该预设组被选中后要生成的怪物条目。")]
        public List<LevelSpawnEntry> entries = new List<LevelSpawnEntry>();

        public void Normalize()
        {
            weight = Mathf.Max(0f, weight);
            if (entries == null)
            {
                entries = new List<LevelSpawnEntry>();
            }

            for (int i = 0; i < entries.Count; i++)
            {
                entries[i]?.Normalize();
            }
        }
    }

    [Serializable]
    public sealed class LevelRandomPool
    {
        [Tooltip("随机池本波最少生成怪物数量。")]
        [Min(0)] public int minCount = 1;

        [Tooltip("随机池本波最多生成怪物数量。")]
        [Min(0)] public int maxCount = 1;

        [Tooltip("随机池候选怪物。每生成 1 只怪时，都会从候选里按权重抽取一次。")]
        public List<LevelSpawnEntry> candidates = new List<LevelSpawnEntry>();

        public void Normalize()
        {
            minCount = Mathf.Max(0, minCount);
            maxCount = Mathf.Max(minCount, maxCount);
            if (candidates == null)
            {
                candidates = new List<LevelSpawnEntry>();
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                candidates[i]?.Normalize();
            }
        }
    }

    [Serializable]
    public sealed class LevelSpawnEntry
    {
        [Tooltip("怪物内部 id，仅用于日志和配置辨识。实际生成使用下方 prefab。")]
        public string monsterId = "fish";

        [Tooltip("实际实例化的怪物 prefab。为空时该条目不会生成。")]
        public GameObject monsterPrefab;

        [Tooltip("预设组模式使用：该条目一次生成多少只。随机池模式下不使用该字段。")]
        [Min(0)] public int count = 1;

        [Tooltip("随机池候选权重，或预设组条目的备注权重字段。随机池中 0 表示不会被抽中。")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("是否显式覆盖该怪物生成后的最大生命值。不勾选时使用 prefab/怪物配置自带数值。")]
        public bool overrideMaxHealth;

        [Tooltip("勾选覆盖生命值时使用的最大生命值。")]
        [Min(1f)] public float maxHealthOverride = 80f;

        public int SpawnCount => Mathf.Max(0, count);
        public float SpawnWeight => Mathf.Max(0f, weight);
        public bool HasMaxHealthOverride => overrideMaxHealth && maxHealthOverride > 0f;

        public void Normalize()
        {
            count = Mathf.Max(0, count);
            weight = Mathf.Max(0f, weight);
            maxHealthOverride = Mathf.Max(1f, maxHealthOverride);
        }
    }

    public readonly struct LevelSpawnRequest
    {
        public readonly string MonsterId;
        public readonly GameObject MonsterPrefab;
        public readonly bool HasMaxHealthOverride;
        public readonly float MaxHealthOverride;

        public LevelSpawnRequest(LevelSpawnEntry entry)
        {
            MonsterId = entry != null ? entry.monsterId : string.Empty;
            MonsterPrefab = entry != null ? entry.monsterPrefab : null;
            HasMaxHealthOverride = entry != null && entry.HasMaxHealthOverride;
            MaxHealthOverride = entry != null ? Mathf.Max(1f, entry.maxHealthOverride) : 1f;
        }
    }
}
