using UnityEngine;

namespace NewFPG.Monsters
{
    [CreateAssetMenu(menuName = "NewFPG/Monsters/Monster Catalog Authoring", fileName = "MonsterCatalogAuthoring")]
    public sealed class MonsterCatalogAuthoring : ScriptableObject
    {
        [SerializeField] private MonsterCatalog catalog = new MonsterCatalog
        {
            version = "draft-2026-06-30",
            source = "Unity ScriptableObject 怪物配置",
            designerNote = "运行时默认从 monster_catalog.json 读取；策划可在 MonsterCatalogAuthoring.asset 里编辑后导出 JSON。",
        };

        public MonsterCatalog Catalog => catalog;

        public void ImportFromJson(string json)
        {
            catalog = MonsterCatalog.FromJson(json);
        }

        public string ExportToJson()
        {
            if (catalog == null)
            {
                catalog = new MonsterCatalog();
            }

            return catalog.ToJson();
        }

        private void OnValidate()
        {
            if (catalog == null)
            {
                catalog = new MonsterCatalog();
            }

            catalog.Normalize();
        }
    }
}
