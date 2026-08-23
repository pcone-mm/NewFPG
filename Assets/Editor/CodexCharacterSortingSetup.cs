using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class CodexCharacterSortingSetup
{
    private const string LayerName = "Character";
    private const int LayerId = 2026073101;
    private const string PrefabPath =
        "Assets/FPGDemo/Presentation/Characters/Players/Fei/Prefabs/PF_FPG_FeiEntity.prefab";
    private const string SpineObjectName = "D0_Fei_30048_StraightAlpha";
    private const string SessionKey = "Codex.CharacterSortingSetup.Completed.v1";

    static CodexCharacterSortingSetup()
    {
        EditorApplication.delayCall += RunOnce;
    }

    private static void RunOnce()
    {
        if (SessionState.GetBool(SessionKey, false)
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        try
        {
            EnsureCharacterSortingLayer();

            int sortingLayerId = SortingLayer.NameToID(LayerName);
            if (sortingLayerId == 0)
            {
                throw new InvalidOperationException(
                    "Unity did not register the Character sorting layer.");
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                MeshRenderer targetRenderer = null;
                foreach (MeshRenderer renderer in
                         prefabRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.name == SpineObjectName)
                    {
                        targetRenderer = renderer;
                        break;
                    }
                }

                if (targetRenderer == null)
                {
                    throw new InvalidOperationException(
                        "Could not find the Fei Spine MeshRenderer in " + PrefabPath);
                }

                targetRenderer.sortingLayerID = sortingLayerId;
                targetRenderer.sortingOrder = 0;
                EditorUtility.SetDirty(targetRenderer);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            SessionState.SetBool(SessionKey, true);
            Debug.Log(
                "[CodexCharacterSorting] SUCCESS layer=" + LayerName
                + " layerId=" + sortingLayerId
                + " prefab=" + PrefabPath
                + " order=0");
        }
        catch (Exception exception)
        {
            Debug.LogError("[CodexCharacterSorting] FAILED " + exception);
        }
    }

    private static void EnsureCharacterSortingLayer()
    {
        UnityEngine.Object tagManager =
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        var serialized = new SerializedObject(tagManager);
        SerializedProperty layers = serialized.FindProperty("m_SortingLayers");
        if (layers == null)
        {
            throw new InvalidOperationException("TagManager has no sorting layers array.");
        }

        for (int index = 0; index < layers.arraySize; index++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (layer.FindPropertyRelative("name").stringValue == LayerName)
            {
                return;
            }
        }

        int insertIndex = 0;
        for (int index = 0; index < layers.arraySize; index++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (layer.FindPropertyRelative("name").stringValue == "Default")
            {
                insertIndex = index + 1;
                break;
            }
        }

        layers.InsertArrayElementAtIndex(insertIndex);
        SerializedProperty inserted = layers.GetArrayElementAtIndex(insertIndex);
        inserted.FindPropertyRelative("name").stringValue = LayerName;
        inserted.FindPropertyRelative("uniqueID").longValue = LayerId;
        inserted.FindPropertyRelative("locked").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tagManager);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            "ProjectSettings/TagManager.asset",
            ImportAssetOptions.ForceUpdate);
    }
}
