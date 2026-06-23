using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Combat.SkillIndicators
{
    [CreateAssetMenu(fileName = "SO_IND_TemporaryArtIndex", menuName = "NewFPG/Combat/Skill Indicators/Temporary Art Index")]
    public sealed class SkillIndicatorTemporaryArtIndex : ScriptableObject
    {
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
        public string resourceId;
        public string category;
        [TextArea(2, 4)] public string usage;
        public GameObject prefab;
        public Material material;
        public Texture2D texture;
        public Mesh mesh;
        public AudioClip audioClip;
    }
}
