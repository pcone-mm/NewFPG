using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Combat.SkillIndicators
{
    [CreateAssetMenu(fileName = "SO_IND_TemporaryArtIndex", menuName = "NewFPG/战斗/技能指示器/临时美术索引")]
    public sealed class SkillIndicatorTemporaryArtIndex : ScriptableObject
    {
        [InspectorName("条目")]
        [SerializeField] private List<SkillIndicatorTemporaryArtEntry> entries = new List<SkillIndicatorTemporaryArtEntry>();

        public IReadOnlyList<SkillIndicatorTemporaryArtEntry> Entries => entries;

        public GameObject GetPrefab(string resourceId)
        {
            SkillIndicatorTemporaryArtEntry entry = Find(resourceId);
            return entry != null ? entry.prefab : null;
        }

        public Material GetMaterial(string resourceId)
        {
            SkillIndicatorTemporaryArtEntry entry = Find(resourceId);
            return entry != null ? entry.material : null;
        }

        public Texture2D GetTexture(string resourceId)
        {
            SkillIndicatorTemporaryArtEntry entry = Find(resourceId);
            return entry != null ? entry.texture : null;
        }

        public Mesh GetMesh(string resourceId)
        {
            SkillIndicatorTemporaryArtEntry entry = Find(resourceId);
            return entry != null ? entry.mesh : null;
        }

        public AudioClip GetAudioClip(string resourceId)
        {
            SkillIndicatorTemporaryArtEntry entry = Find(resourceId);
            return entry != null ? entry.audioClip : null;
        }

        public SkillIndicatorTemporaryArtEntry Find(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return null;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                SkillIndicatorTemporaryArtEntry entry = entries[i];
                if (entry != null && string.Equals(entry.resourceId, resourceId, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        public void SetEntries(IEnumerable<SkillIndicatorTemporaryArtEntry> nextEntries)
        {
            entries.Clear();
            if (nextEntries == null)
            {
                return;
            }

            entries.AddRange(nextEntries);
        }
    }

    [Serializable]
    public sealed class SkillIndicatorTemporaryArtEntry
    {
        [InspectorName("资源 ID"), Tooltip("运行时通过这个字符串 ID 查找预制体、材质、贴图、网格或音效。")]
        public string resourceId;
        [InspectorName("分类"), Tooltip("资源类型，例如预制体、材质、贴图、网格或音效。")]
        public string category;
        [InspectorName("用途说明"), Tooltip("这个临时资源适合用于哪些技能指示器场景。")]
        [TextArea(2, 4)] public string usage;
        [InspectorName("预制体")]
        public GameObject prefab;
        [InspectorName("材质")]
        public Material material;
        [InspectorName("贴图")]
        public Texture2D texture;
        [InspectorName("网格")]
        public Mesh mesh;
        [InspectorName("音效")]
        public AudioClip audioClip;
    }
}
