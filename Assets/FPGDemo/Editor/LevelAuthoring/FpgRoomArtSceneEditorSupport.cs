using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Editor.LevelAuthoring
{
    [CustomPropertyDrawer(typeof(FpgRoomArtSceneReference))]
    public sealed class FpgRoomArtSceneReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            SerializedProperty guidProperty =
                property.FindPropertyRelative("sceneGuid");
            SerializedProperty pathProperty =
                property.FindPropertyRelative("scenePath");
            SceneAsset current = string.IsNullOrEmpty(pathProperty.stringValue)
                ? null
                : AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    pathProperty.stringValue);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            SceneAsset selected = EditorGUI.ObjectField(
                position,
                label,
                current,
                typeof(SceneAsset),
                false) as SceneAsset;
            if (EditorGUI.EndChangeCheck())
            {
                string path = selected == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(selected);
                pathProperty.stringValue = path;
                guidProperty.stringValue = string.IsNullOrEmpty(path)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(path);
            }

            EditorGUI.EndProperty();
        }
    }

    public static class FpgRoomArtSceneEditorUtility
    {
        public const string RoomCatalogPath =
            "Assets/FPGDemo/Config/Level/FPG_RoomCatalog.asset";

        public static bool TrySynchronizeReference(
            FpgRoomDefinition room,
            out string error)
        {
            error = string.Empty;
            if (room == null)
            {
                error = "RoomDefinition is missing.";
                return false;
            }

            SerializedObject serializedRoom = new SerializedObject(room);
            SerializedProperty artScene =
                serializedRoom.FindProperty("artScene");
            SerializedProperty guidProperty =
                artScene?.FindPropertyRelative("sceneGuid");
            SerializedProperty pathProperty =
                artScene?.FindPropertyRelative("scenePath");
            if (guidProperty == null || pathProperty == null)
            {
                error =
                    $"Room '{room.name}' has no serialized Art Scene reference.";
                return false;
            }

            string guid = guidProperty.stringValue;
            string storedPath = pathProperty.stringValue;
            if (string.IsNullOrWhiteSpace(guid))
            {
                error = $"Room '{room.name}' has no Art Scene GUID.";
                return false;
            }

            string resolvedPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(resolvedPath)
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(resolvedPath) == null)
            {
                error =
                    $"Room '{room.name}' Art Scene GUID '{guid}' does not resolve to a Scene asset.";
                return false;
            }

            if (!string.Equals(
                    storedPath,
                    resolvedPath,
                    StringComparison.Ordinal))
            {
                pathProperty.stringValue = resolvedPath;
                serializedRoom.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(room);
            }

            return TryValidateStoredReference(room, out error);
        }

        public static bool TryValidateStoredReference(
            FpgRoomDefinition room,
            out string error)
        {
            if (room == null || room.ArtScene == null)
            {
                error = "Room Art Scene reference is missing.";
                return false;
            }

            if (!room.ArtScene.TryValidate(out error))
            {
                return false;
            }

            string resolvedPath =
                AssetDatabase.GUIDToAssetPath(room.ArtScene.SceneGuid);
            if (!string.Equals(
                    resolvedPath,
                    room.ArtScenePath,
                    StringComparison.Ordinal))
            {
                error =
                    $"Room '{room.RoomId}' Art Scene GUID resolves to '{resolvedPath}', not stored path '{room.ArtScenePath}'.";
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    room.ArtScenePath) == null)
            {
                error =
                    $"Room '{room.RoomId}' Art Scene is missing at '{room.ArtScenePath}'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool TrySynchronizeAll(out string error)
        {
            FpgRoomDefinition[] rooms = LoadCatalogRooms();
            Array.Sort(
                rooms,
                (left, right) => StringComparer.Ordinal.Compare(
                    left.RoomId,
                    right.RoomId));
            for (int index = 0; index < rooms.Length; index++)
            {
                if (!TrySynchronizeReference(
                        rooms[index],
                        out error))
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public static FpgRoomDefinition[] LoadCatalogRooms()
        {
            FpgRoomCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FpgRoomCatalog>(RoomCatalogPath);
            return catalog == null
                ? Array.Empty<FpgRoomDefinition>()
                : catalog.Rooms.Where(room => room != null).ToArray();
        }
    }

    public static class FpgRoomArtSceneContractValidator
    {
        private static readonly MethodInfo GetRenderSettingsMethod =
            typeof(RenderSettings).GetMethod(
                "GetRenderSettings",
                BindingFlags.Static | BindingFlags.NonPublic);

        public static bool TryValidateCatalog(out string error)
        {
            FpgRoomCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FpgRoomCatalog>(
                    FpgRoomArtSceneEditorUtility.RoomCatalogPath);
            if (catalog == null)
            {
                error = "Room catalog is missing.";
                return false;
            }

            if (!catalog.TryValidate(out error))
            {
                return false;
            }

            if (!TryValidateUniqueReferences(catalog.Rooms, out error))
            {
                return false;
            }

            for (int index = 0; index < catalog.Rooms.Count; index++)
            {
                if (!TryValidateScene(catalog.Rooms[index], out error))
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public static bool TryValidateUniqueReferences(
            IReadOnlyList<FpgRoomDefinition> rooms,
            out string error)
        {
            if (rooms == null || rooms.Count == 0)
            {
                error = "Room catalog requires at least one room.";
                return false;
            }

            HashSet<string> guids =
                new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> paths =
                new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> sceneNames =
                new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < rooms.Count; index++)
            {
                FpgRoomDefinition room = rooms[index];
                if (room == null)
                {
                    error = $"Room catalog entry {index} is missing.";
                    return false;
                }

                if (!FpgRoomArtSceneEditorUtility.TryValidateStoredReference(
                        room,
                        out error))
                {
                    return false;
                }

                if (!guids.Add(room.ArtScene.SceneGuid)
                    || !paths.Add(room.ArtScenePath)
                    || !sceneNames.Add(room.ArtScene.SceneName))
                {
                    error =
                        $"Room catalog Art Scene GUIDs, paths and scene names must each be one-to-one. Duplicate found for room '{room.RoomId}'.";
                    return false;
                }

            }

            error = string.Empty;
            return true;
        }

        public static bool TryValidateScene(
            FpgRoomDefinition room,
            out string error)
        {
            error = string.Empty;
            if (!FpgRoomArtSceneEditorUtility.TryValidateStoredReference(
                    room,
                    out error))
            {
                return false;
            }

            Scene scene = SceneManager.GetSceneByPath(room.ArtScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            Scene previousActive = SceneManager.GetActiveScene();
            try
            {
                if (openedForValidation)
                {
                    scene = EditorSceneManager.OpenScene(
                        room.ArtScenePath,
                        OpenSceneMode.Additive);
                }

                if (!FpgRoomArtRoot.TryResolve(
                        scene,
                        room,
                        out FpgRoomArtRoot root,
                        out error))
                {
                    return false;
                }

                if (SceneManager.GetActiveScene() != scene
                    && !SceneManager.SetActiveScene(scene))
                {
                    error =
                        $"Could not make Art Scene '{scene.path}' active for validation.";
                    return false;
                }

                if (!TryGetAuthoredSun(out Light authoredSun, out error))
                {
                    return false;
                }

                if (authoredSun != root.MainDirectionalLight)
                {
                    error =
                        $"Art Scene '{scene.path}' RenderSettings.sun must equal the root's main directional light.";
                    return false;
                }

                if (!TryValidateForbiddenComponents(root, out error))
                {
                    return false;
                }

                return true;
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded
                    && SceneManager.GetActiveScene() != previousActive)
                {
                    SceneManager.SetActiveScene(previousActive);
                }

                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static bool TryValidateForbiddenComponents(
            FpgRoomArtRoot root,
            out string error)
        {
            Type[] forbidden =
            {
                typeof(Camera),
                typeof(AudioListener),
                typeof(GameBootstrap),
                typeof(FpgFormalEncounterHost),
                typeof(FpgEncounterHost),
                typeof(FpgRoomEncounterDirector),
                typeof(FpgRoomInstance),
                typeof(FpgRoomArtSceneLoader)
            };
            for (int index = 0; index < forbidden.Length; index++)
            {
                Component component = root.GetComponentInChildren(
                    forbidden[index],
                    true);
                if (component != null)
                {
                    error =
                        $"Art Scene '{root.gameObject.scene.path}' cannot contain {forbidden[index].Name} ('{component.name}').";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryGetAuthoredSun(
            out Light authoredSun,
            out string error)
        {
            authoredSun = null;
            if (GetRenderSettingsMethod == null)
            {
                error =
                    "Unity RenderSettings serialization API is unavailable; cannot validate RenderSettings.sun.";
                return false;
            }

            UnityEngine.Object settings =
                GetRenderSettingsMethod.Invoke(null, null) as UnityEngine.Object;
            if (settings == null)
            {
                error =
                    "Active Art Scene has no serialized RenderSettings object.";
                return false;
            }

            SerializedProperty sunProperty =
                new SerializedObject(settings).FindProperty("m_Sun");
            if (sunProperty == null)
            {
                error =
                    "Active Art Scene RenderSettings has no serialized sun field.";
                return false;
            }

            authoredSun = sunProperty.objectReferenceValue as Light;
            error = string.Empty;
            return true;
        }
    }

    internal sealed class FpgRoomArtSceneAssetPostprocessor
        : AssetPostprocessor
    {
        private static bool synchronizationQueued;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (synchronizationQueued
                || !ContainsScene(importedAssets)
                && !ContainsScene(deletedAssets)
                && !ContainsScene(movedAssets)
                && !ContainsScene(movedFromAssetPaths))
            {
                return;
            }

            synchronizationQueued = true;
            EditorApplication.delayCall += Synchronize;
        }

        private static void Synchronize()
        {
            synchronizationQueued = false;
            if (!FpgRoomArtSceneEditorUtility.TrySynchronizeAll(
                    out string error))
            {
                Debug.LogError("[FPG Room Art] " + error);
            }
        }

        private static bool ContainsScene(string[] paths)
        {
            return paths != null && Array.Exists(
                paths,
                path => path.EndsWith(
                    ".unity",
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    internal sealed class FpgRoomArtSceneSaveGuard
        : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            List<string> allowed = new List<string>(paths.Length);
            for (int index = 0; index < paths.Length; index++)
            {
                FpgRoomDefinition room =
                    AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(
                        paths[index]);
                if (room != null
                    && !FpgRoomArtSceneEditorUtility.TryValidateStoredReference(
                        room,
                        out string error))
                {
                    Debug.LogError(
                        $"[FPG Room Art] Save blocked for '{paths[index]}': {error}",
                        room);
                    continue;
                }

                allowed.Add(paths[index]);
            }

            return allowed.ToArray();
        }
    }
}
