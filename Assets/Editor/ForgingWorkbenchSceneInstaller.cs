using NewFPG.Forging;
using NewFPG.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ForgingWorkbenchSceneInstaller
{
    private const string ScenePath = "Assets/Scenes/lianqi.unity";
    private const string MaterialTexturePath = "Assets/Art/UI/ForgingPSDImport/Layers/03_yellow_earth.png";
    private const string LayoutPresetPath = "Assets/Settings/Forging/ForgingUILayoutPreset.asset";
    private const string WeaponOutputFolder = "Assets/Settings/Forging/Weapons";

    [MenuItem("NewFPG/Forging/Install Lianqi Workbench")]
    public static void Install()
    {
        CreateOrUpdateBlueprintWeapons();

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        GameObject root = GameObject.Find("ForgingUICanvas/ForgingUIRoot");
        if (root == null)
        {
            root = GameObject.Find("ForgingUIRoot");
        }

        if (root == null)
        {
            Debug.LogError("[ForgingWorkbenchSceneInstaller] Could not find ForgingUIRoot in lianqi scene.");
            return;
        }

        Canvas canvas = root.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                Undo.RecordObject(canvasRect, "Normalize Forging Canvas Transform");
                canvasRect.localPosition = Vector3.zero;
                canvasRect.localRotation = Quaternion.identity;
                canvasRect.localScale = Vector3.one;
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.zero;
                canvasRect.anchoredPosition = Vector2.zero;
            }
        }

        ForgingWorkbenchController controller = root.GetComponent<ForgingWorkbenchController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<ForgingWorkbenchController>(root);
        }

        Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(MaterialTexturePath);
        SerializedObject serializedObject = new SerializedObject(controller);
        SetString(serializedObject, "weaponBlueprintsPath", ForgingCatalogLoader.DefaultWeaponBlueprintsPath);
        SetString(serializedObject, "materialsPath", ForgingCatalogLoader.DefaultMaterialsPath);
        SetString(serializedObject, "catalogPath", ForgingCatalogLoader.LegacyCatalogPath);
        serializedObject.FindProperty("materialTexture").objectReferenceValue = texture;
        serializedObject.FindProperty("showDrawerOnStart").boolValue = true;
        serializedObject.FindProperty("layoutPreset").objectReferenceValue = LoadOrCreateLayoutPreset();
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[ForgingWorkbenchSceneInstaller] Installed forging workbench controller in lianqi scene.");
    }

    [MenuItem("NewFPG/Forging/Create Blueprint Weapon Assets")]
    public static void CreateOrUpdateBlueprintWeapons()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Settings/Forging"))
        {
            AssetDatabase.CreateFolder("Assets/Settings", "Forging");
        }

        if (!AssetDatabase.IsValidFolder(WeaponOutputFolder))
        {
            AssetDatabase.CreateFolder("Assets/Settings/Forging", "Weapons");
        }

        ForgingCatalog catalog = ForgingCatalogLoader.LoadDefault();
        if (catalog.weaponBlueprints == null)
        {
            return;
        }

        for (int i = 0; i < catalog.weaponBlueprints.Count; i++)
        {
            ForgingWeaponBlueprintDefinition blueprint = catalog.weaponBlueprints[i];
            if (blueprint == null || blueprint.runtime == null || string.IsNullOrWhiteSpace(blueprint.runtime.weaponDefinitionAssetPath))
            {
                continue;
            }

            string path = blueprint.runtime.weaponDefinitionAssetPath;
            WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (weapon == null)
            {
                weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(weapon, path);
            }

            ForgingWeaponFactory.ApplyToWeaponDefinition(weapon, blueprint, null);
            EditorUtility.SetDirty(weapon);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ForgingWorkbenchSceneInstaller] Created or updated forging blueprint WeaponDefinition assets.");
    }

    [MenuItem("NewFPG/Forging/Create Or Bind UI Layout Preset")]
    public static void CreateOrBindLayoutPreset()
    {
        ForgingUILayoutPreset preset = LoadOrCreateLayoutPreset();
        ForgingWorkbenchController controller = Object.FindFirstObjectByType<ForgingWorkbenchController>();
        if (controller == null)
        {
            Debug.LogWarning("[ForgingWorkbenchSceneInstaller] Could not find a ForgingWorkbenchController in the active scene.");
            return;
        }

        SerializedObject serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("layoutPreset").objectReferenceValue = preset;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[ForgingWorkbenchSceneInstaller] Bound forging UI layout preset.");
    }

    private static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }

    private static ForgingUILayoutPreset LoadOrCreateLayoutPreset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Settings/Forging"))
        {
            AssetDatabase.CreateFolder("Assets/Settings", "Forging");
        }

        ForgingUILayoutPreset preset = AssetDatabase.LoadAssetAtPath<ForgingUILayoutPreset>(LayoutPresetPath);
        if (preset == null)
        {
            preset = ScriptableObject.CreateInstance<ForgingUILayoutPreset>();
            AssetDatabase.CreateAsset(preset, LayoutPresetPath);
            AssetDatabase.SaveAssets();
        }

        return preset;
    }
}
