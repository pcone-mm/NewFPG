using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NewFPG.Forging
{
    public static class ForgingCatalogLoader
    {
        public const string LegacyCatalogPath = "Assets/Settings/Forging/forging_catalog.json";
        public const string DefaultWeaponBlueprintsPath = "Assets/Settings/Forging/weapon_blueprints.json";
        public const string DefaultMaterialsPath = "Assets/Settings/Forging/materials.json";
        public const string DefaultCatalogPath = LegacyCatalogPath;

        public static ForgingCatalog LoadDefault()
        {
            return LoadFromProjectPaths(DefaultWeaponBlueprintsPath, DefaultMaterialsPath);
        }

        public static ForgingCatalog LoadFromProjectPaths(string weaponBlueprintsPath, string materialsPath)
        {
            ForgingCatalog catalog = new ForgingCatalog();
            MergeCatalog(catalog, LoadCatalogFile(weaponBlueprintsPath));
            MergeCatalog(catalog, LoadCatalogFile(materialsPath));
            catalog.Normalize();

            if (catalog.IsEmpty)
            {
                Debug.LogWarning("Forging catalog is empty or missing. WeaponBlueprints="
                    + weaponBlueprintsPath + ", Materials=" + materialsPath);
            }

            return catalog;
        }

        public static ForgingCatalog LoadFromProjectPath(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                return new ForgingCatalog();
            }

            ForgingCatalog catalog = LoadCatalogFile(projectPath);
            if (catalog.IsEmpty)
            {
                Debug.LogWarning("Forging catalog is empty or missing: " + projectPath);
            }

            return catalog;
        }

        private static ForgingCatalog LoadCatalogFile(string projectPath)
        {
            string json = LoadJson(projectPath);
            return ForgingCatalog.FromJson(json);
        }

        private static void MergeCatalog(ForgingCatalog target, ForgingCatalog source)
        {
            if (target == null || source == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(target.version))
            {
                target.version = source.version;
            }

            if (string.IsNullOrWhiteSpace(target.source))
            {
                target.source = source.source;
            }

            if (source.weaponBlueprints != null && source.weaponBlueprints.Count > 0)
            {
                target.weaponBlueprints.AddRange(source.weaponBlueprints);
            }

            if (source.materials != null && source.materials.Count > 0)
            {
                target.materials.AddRange(source.materials);
            }
        }

        private static string LoadJson(string projectPath)
        {
#if UNITY_EDITOR
            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(projectPath);
            if (textAsset != null)
            {
                return textAsset.text;
            }
#endif

            string absolutePath = Path.Combine(Application.dataPath, projectPath.StartsWith("Assets/")
                ? projectPath.Substring("Assets/".Length)
                : projectPath);

            return File.Exists(absolutePath) ? File.ReadAllText(absolutePath) : string.Empty;
        }
    }
}
