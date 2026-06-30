using System.Collections.Generic;
using System.IO;
using NewFPG.Forging;
using UnityEditor;
using UnityEngine;

namespace NewFPG.EditorTools
{
    public static class ForgingConfigEditorUtility
    {
        private const string MaterialConfigFolder = "Assets/Settings/Forging/Materials";
        private const string WeaponBlueprintConfigFolder = "Assets/Settings/Forging/Blueprints";

        [MenuItem("NewFPG/Forging/Sync ScriptableObject Configs From JSON")]
        public static void SyncScriptableObjectsFromJson()
        {
            EnsureFolder(MaterialConfigFolder);
            EnsureFolder(WeaponBlueprintConfigFolder);

            ForgingCatalog catalog = ForgingCatalogLoader.LoadDefault();
            if (catalog.materials != null)
            {
                for (int i = 0; i < catalog.materials.Count; i++)
                {
                    ForgingMaterialDefinition definition = catalog.materials[i];
                    if (definition == null)
                    {
                        continue;
                    }

                    string path = MaterialConfigFolder + "/" + ToAssetFileName(definition.materialId, "Material") + ".asset";
                    ForgingMaterialConfig config = LoadOrCreate<ForgingMaterialConfig>(path);
                    config.ApplyDefinition(definition);
                    EditorUtility.SetDirty(config);
                }
            }

            if (catalog.weaponBlueprints != null)
            {
                for (int i = 0; i < catalog.weaponBlueprints.Count; i++)
                {
                    ForgingWeaponBlueprintDefinition definition = catalog.weaponBlueprints[i];
                    if (definition == null)
                    {
                        continue;
                    }

                    string path = WeaponBlueprintConfigFolder + "/" + ToAssetFileName(definition.blueprintId, "Blueprint") + ".asset";
                    ForgingWeaponBlueprintConfig config = LoadOrCreate<ForgingWeaponBlueprintConfig>(path);
                    config.ApplyDefinition(definition);
                    EditorUtility.SetDirty(config);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ForgingConfigEditorUtility] Synced forging ScriptableObject configs from JSON.");
        }

        [MenuItem("NewFPG/Forging/Export JSON From ScriptableObject Configs")]
        public static void ExportJsonFromScriptableObjects()
        {
            EnsureFolder(MaterialConfigFolder);
            EnsureFolder(WeaponBlueprintConfigFolder);

            ForgingCatalog orderCatalog = ForgingCatalogLoader.LoadFromProjectPath(ForgingCatalogLoader.LegacyCatalogPath);
            List<ForgingMaterialDefinition> materials = LoadDefinitionsFromAssets<ForgingMaterialConfig, ForgingMaterialDefinition>(
                MaterialConfigFolder,
                config => config.ToDefinition());
            List<ForgingWeaponBlueprintDefinition> weaponBlueprints =
                LoadDefinitionsFromAssets<ForgingWeaponBlueprintConfig, ForgingWeaponBlueprintDefinition>(
                    WeaponBlueprintConfigFolder,
                    config => config.ToDefinition());
            SortMaterials(materials, orderCatalog);
            SortWeaponBlueprints(weaponBlueprints, orderCatalog);

            ForgingCatalog catalog = new ForgingCatalog
            {
                version = "draft-2026-06-27",
                source = "Unity ScriptableObject forging configs",
                materials = materials,
                weaponBlueprints = weaponBlueprints,
            };
            catalog.Normalize();

            EnsureFolder("Assets/Settings/Forging");
            WriteProjectText(ForgingCatalogLoader.DefaultWeaponBlueprintsPath, catalog.ToJson(true, false));
            WriteProjectText(ForgingCatalogLoader.DefaultMaterialsPath, catalog.ToJson(false, true));

            AssetDatabase.ImportAsset(ForgingCatalogLoader.DefaultWeaponBlueprintsPath);
            AssetDatabase.ImportAsset(ForgingCatalogLoader.DefaultMaterialsPath);
            AssetDatabase.Refresh();
            Debug.Log("[ForgingConfigEditorUtility] Exported forging JSON from ScriptableObject configs.");
        }

        private static List<TDefinition> LoadDefinitionsFromAssets<TConfig, TDefinition>(
            string folder,
            System.Func<TConfig, TDefinition> convert)
            where TConfig : ScriptableObject
        {
            List<TDefinition> definitions = new List<TDefinition>();
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(TConfig).Name, new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TConfig config = AssetDatabase.LoadAssetAtPath<TConfig>(path);
                if (config != null)
                {
                    definitions.Add(convert(config));
                }
            }

            return definitions;
        }

        private static void SortMaterials(List<ForgingMaterialDefinition> materials, ForgingCatalog orderCatalog)
        {
            Dictionary<string, int> order = new Dictionary<string, int>();
            if (orderCatalog != null && orderCatalog.materials != null)
            {
                for (int i = 0; i < orderCatalog.materials.Count; i++)
                {
                    ForgingMaterialDefinition material = orderCatalog.materials[i];
                    if (material != null && !string.IsNullOrWhiteSpace(material.materialId))
                    {
                        order[material.materialId] = i;
                    }
                }
            }

            materials.Sort((left, right) => CompareByOrder(
                left != null ? left.materialId : string.Empty,
                right != null ? right.materialId : string.Empty,
                order));
        }

        private static void SortWeaponBlueprints(
            List<ForgingWeaponBlueprintDefinition> weaponBlueprints,
            ForgingCatalog orderCatalog)
        {
            Dictionary<string, int> order = new Dictionary<string, int>();
            if (orderCatalog != null && orderCatalog.weaponBlueprints != null)
            {
                for (int i = 0; i < orderCatalog.weaponBlueprints.Count; i++)
                {
                    ForgingWeaponBlueprintDefinition blueprint = orderCatalog.weaponBlueprints[i];
                    if (blueprint != null && !string.IsNullOrWhiteSpace(blueprint.blueprintId))
                    {
                        order[blueprint.blueprintId] = i;
                    }
                }
            }

            weaponBlueprints.Sort((left, right) => CompareByOrder(
                left != null ? left.blueprintId : string.Empty,
                right != null ? right.blueprintId : string.Empty,
                order));
        }

        private static int CompareByOrder(string leftId, string rightId, IReadOnlyDictionary<string, int> order)
        {
            int leftOrder = 0;
            int rightOrder = 0;
            bool leftKnown = order != null && order.TryGetValue(leftId, out leftOrder);
            bool rightKnown = order != null && order.TryGetValue(rightId, out rightOrder);
            if (leftKnown && rightKnown)
            {
                return leftOrder.CompareTo(rightOrder);
            }

            if (leftKnown)
            {
                return -1;
            }

            if (rightKnown)
            {
                return 1;
            }

            return string.Compare(leftId, rightId, System.StringComparison.Ordinal);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            if (parts.Length <= 1)
            {
                return;
            }

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void WriteProjectText(string projectPath, string content)
        {
            string absolutePath = Path.GetFullPath(projectPath);
            File.WriteAllText(absolutePath, content + "\n");
        }

        private static string ToAssetFileName(string id, string fallback)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return fallback;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            string value = id.Trim();
            for (int i = 0; i < invalid.Length; i++)
            {
                value = value.Replace(invalid[i], '_');
            }

            return value;
        }
    }
}
