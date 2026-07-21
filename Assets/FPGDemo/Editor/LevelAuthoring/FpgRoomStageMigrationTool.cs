using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor.LevelAuthoring
{
    public static class FpgRoomStageMigrationTool
    {
        private const string StageTypeName = "FPG.Demo.Unity.D0StageDefinition, FPG.Unity";
        private const string GroupTypeName = "FPG.Demo.Unity.FpgRoomGroupDefinition, FPG.Unity";
        private const string TagTypeName = "FPG.Demo.Unity.FpgRoomTagDefinition, FPG.Unity";
        private const string DefaultConfigFolder = "Assets/FPGDemo/Config/Level";
        private const string DefaultPrefabFolder = "Assets/FPGDemo/Presentation/Level/Environment";

        [MenuItem("FPG Demo/Room Migration/Migrate Selected D0 Stage", priority = 140)]
        public static void MigrateSelectedStageMenu()
        {
            if (TryMigrate(Selection.activeObject, DefaultConfigFolder, DefaultPrefabFolder,
                    out ScriptableObject room, out string error))
            {
                Selection.activeObject = room;
                EditorGUIUtility.PingObject(room);
                EditorUtility.DisplayDialog("D0 Stage 迁移完成",
                    "已创建房间、环境 Prefab、主分组和迁移标签。旧 Stage 资产保持不变；已有迁移目标会被拒绝以避免覆盖修改。", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("D0 Stage 迁移失败", error, "确定");
            }
        }

        [MenuItem("FPG Demo/Room Migration/Migrate Selected D0 Stage", true)]
        private static bool ValidateMigrateSelectedStageMenu()
        {
            Type stageType = Type.GetType(StageTypeName, false);
            return stageType != null && Selection.activeObject != null
                && stageType.IsInstanceOfType(Selection.activeObject);
        }

        public static bool TryMigrate(UnityEngine.Object stage, string configFolder, string prefabFolder,
            out ScriptableObject room, out string error)
        {
            room = null;
            error = string.Empty;
            Type stageType = Type.GetType(StageTypeName, false);
            Type roomType = FpgRoomAuthoringSchema.RoomType;
            Type groupType = Type.GetType(GroupTypeName, false);
            Type tagType = Type.GetType(TagTypeName, false);
            if (stageType == null || roomType == null || groupType == null || tagType == null)
            {
                error = "Stage 或房间运行时类型尚未编译。";
                return false;
            }

            if (stage == null || !stageType.IsInstanceOfType(stage))
            {
                error = "请选择一个 D0StageDefinition 资产。";
                return false;
            }

            FPG.Demo.Unity.D0StageDefinition sourceStage =
                stage as FPG.Demo.Unity.D0StageDefinition;
            string stageError = string.Empty;
            if (sourceStage == null || !sourceStage.TryValidate(out stageError))
            {
                error = "D0 Stage 迁移前校验失败：" +
                    (string.IsNullOrWhiteSpace(stageError) ? "未知结构错误。" : stageError);
                return false;
            }

            SerializedObject serializedStage = new SerializedObject(stage);
            string stageId = serializedStage.FindProperty("stageId")?.stringValue;
            if (string.IsNullOrWhiteSpace(stageId))
            {
                error = "D0 Stage 缺少 stageId。";
                return false;
            }

            string displayName = serializedStage.FindProperty("displayName")?.stringValue;
            string sourceNotes = serializedStage.FindProperty("designerNotes")?.stringValue;
            string safeName = SanitizeFileName(stageId);
            string roomsFolder = configFolder + "/Rooms";
            string groupsFolder = configFolder + "/Groups";
            string tagsFolder = configFolder + "/Tags";
            EnsureFolder(roomsFolder);
            EnsureFolder(groupsFolder);
            EnsureFolder(tagsFolder);
            EnsureFolder(prefabFolder);
            string groupPath = groupsFolder + "/RoomGroup_NormalCombat.asset";
            string tagPath = tagsFolder + "/RoomTag_MigratedD0.asset";
            string roomPath = roomsFolder + "/Room_" + safeName + ".asset";
            string prefabPath = prefabFolder + "/ENV_" + safeName + ".prefab";

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(roomPath) != null
                || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(prefabPath) != null)
            {
                error = "迁移目标已存在。为避免覆盖策划或美术修改，请改名/移走旧结果后再执行。";
                return false;
            }

            ScriptableObject existingGroup = AssetDatabase.LoadAssetAtPath(groupPath, groupType) as ScriptableObject;
            ScriptableObject group = existingGroup ?? GetOrCreateDefinition(groupType, groupPath);
            if (existingGroup == null)
            {
                SetTaxonomy(group, "groupId", "normal-combat", "普通战斗房", "普通战斗房的主分组。" );
            }

            ScriptableObject existingTag = AssetDatabase.LoadAssetAtPath(tagPath, tagType) as ScriptableObject;
            ScriptableObject tag = existingTag ?? GetOrCreateDefinition(tagType, tagPath);
            if (existingTag == null)
            {
                SetTaxonomy(tag, "tagId", "migrated-d0-stage", "D0 Stage 迁移", "标记一次性迁移来源。" );
            }

            if (!TryCreateEnvironmentPrefab(serializedStage, prefabPath, out GameObject environment, out error))
            {
                return false;
            }

            room = ScriptableObject.CreateInstance(roomType);
            AssetDatabase.CreateAsset(room, roomPath);
            SerializedObject serializedRoom = new SerializedObject(room);
            string preferredRoomId = "room-" + stageId;
            string[] existingRoomIds = FpgRoomAuthoringSchema.FindAllRooms()
                .Select(candidate => FpgRoomAuthoringSchema.GetString(candidate, "roomId"))
                .ToArray();
            serializedRoom.FindProperty("roomId").stringValue =
                FPG.Demo.Unity.FpgRoomIdUtility.GenerateRoomId(preferredRoomId, existingRoomIds);
            serializedRoom.FindProperty("displayName").stringValue =
                string.IsNullOrWhiteSpace(displayName) ? stageId : displayName;
            serializedRoom.FindProperty("designerNotes").stringValue =
                "由旧 D0StageDefinition 一次性迁移；旧 Stage 仅作为兼容回退保留。"
                + (string.IsNullOrWhiteSpace(sourceNotes) ? string.Empty : "\n\n旧 Stage 备注：\n" + sourceNotes);
            serializedRoom.FindProperty("environmentPrefab").objectReferenceValue = environment;
            serializedRoom.FindProperty("mainGroup").objectReferenceValue = group;
            SerializedProperty tags = serializedRoom.FindProperty("tags");
            tags.arraySize = 1;
            tags.GetArrayElementAtIndex(0).objectReferenceValue = tag;
            ConvertSpawnPoints(serializedStage, serializedRoom);
            serializedRoom.ApplyModifiedPropertiesWithoutUndo();
            FPG.Demo.Unity.FpgRoomDefinition generatedRoom =
                room as FPG.Demo.Unity.FpgRoomDefinition;
            FPG.Demo.Unity.FpgRoomValidationResult generatedValidation = null;
            if (generatedRoom == null
                || !generatedRoom.TryValidate(out generatedValidation))
            {
                error = "生成的房间校验失败："
                    + (generatedValidation == null || generatedValidation.FirstError == null
                        ? "未知结构错误。"
                        : generatedValidation.FirstError.Message);
                AssetDatabase.DeleteAsset(roomPath);
                AssetDatabase.DeleteAsset(prefabPath);
                room = null;
                AssetDatabase.Refresh();
                return false;
            }
            EditorUtility.SetDirty(room);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static void ConvertSpawnPoints(SerializedObject stage, SerializedObject room)
        {
            SerializedProperty playerEntries = room.FindProperty("playerEntryPoints");
            SerializedProperty enemySpawns = room.FindProperty("enemySpawnPoints");
            playerEntries.arraySize = 0;
            enemySpawns.arraySize = 0;
            SerializedProperty spawnPoints = stage.FindProperty("spawnPoints");
            if (spawnPoints == null || !spawnPoints.isArray)
            {
                return;
            }

            for (int index = 0; index < spawnPoints.arraySize; index++)
            {
                SerializedProperty source = spawnPoints.GetArrayElementAtIndex(index);
                string id = source.FindPropertyRelative("spawnPointId")?.stringValue ?? string.Empty;
                bool isPlayer = id.StartsWith("player-", StringComparison.OrdinalIgnoreCase);
                bool isEnemy = id.StartsWith("enemy-", StringComparison.OrdinalIgnoreCase);
                if (!isPlayer && !isEnemy)
                {
                    Debug.LogWarning($"D0 Stage 出生点 '{id}' 不是 player-* 或 enemy-*，迁移工具不会猜测其类型。" );
                    continue;
                }

                SerializedProperty targetArray = isPlayer ? playerEntries : enemySpawns;
                int targetIndex = targetArray.arraySize;
                targetArray.InsertArrayElementAtIndex(targetIndex);
                SerializedProperty target = targetArray.GetArrayElementAtIndex(targetIndex);
                target.FindPropertyRelative("markerId").stringValue = id;
                target.FindPropertyRelative("displayName").stringValue =
                    isPlayer ? "玩家入口 " + id : "敌人出生点 " + id;
                target.FindPropertyRelative("localPosition").vector3Value =
                    source.FindPropertyRelative("localPosition")?.vector3Value ?? Vector3.zero;
                target.FindPropertyRelative("localEulerAngles").vector3Value =
                    source.FindPropertyRelative("localEulerAngles")?.vector3Value ?? Vector3.zero;
                if (isEnemy)
                {
                    target.FindPropertyRelative("role").intValue = 0;
                }
            }
        }

        private static bool TryCreateEnvironmentPrefab(SerializedObject stage, string prefabPath,
            out GameObject prefab, out string error)
        {
            prefab = null;
            error = string.Empty;
            GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                SerializedProperty layers = stage.FindProperty("forestLayers");
                if (layers == null || !layers.isArray || layers.arraySize == 0)
                {
                    error = "D0 Stage 没有可迁移的森林图层。";
                    return false;
                }

                FPG.Demo.Unity.D0ForestParallaxLayer[] parallaxLayers =
                    new FPG.Demo.Unity.D0ForestParallaxLayer[layers.arraySize];
                for (int index = 0; index < layers.arraySize; index++)
                {
                    SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                    Sprite sprite = layer.FindPropertyRelative("sprite")?.objectReferenceValue as Sprite;
                    if (sprite == null)
                    {
                        error = $"森林图层 #{index + 1} 缺少 Sprite。";
                        return false;
                    }

                    string layerId = layer.FindPropertyRelative("layerId")?.stringValue;
                    GameObject child = new GameObject(string.IsNullOrWhiteSpace(layerId) ? "Layer_" + index : layerId);
                    child.transform.SetParent(root.transform, false);
                    Vector3 basePosition = layer.FindPropertyRelative("baseLocalPosition")?.vector3Value ?? Vector3.zero;
                    Vector2 viewportOffset = layer.FindPropertyRelative("viewportOffsetMultiplier")?.vector2Value ?? Vector2.zero;
                    float width = layer.FindPropertyRelative("desiredWorldWidth")?.floatValue ?? sprite.bounds.size.x;
                    float scale = sprite.bounds.size.x > 0f ? width / sprite.bounds.size.x : 1f;
                    child.transform.localPosition = basePosition;
                    child.transform.localScale = new Vector3(scale, scale, 1f);

                    SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.sortingOrder = layer.FindPropertyRelative("sortingOrder")?.intValue ?? 0;
                    renderer.color = layer.FindPropertyRelative("color")?.colorValue ?? Color.white;
                    renderer.flipX = layer.FindPropertyRelative("flipX")?.boolValue ?? false;

                    FPG.Demo.Unity.D0ForestParallaxLayer parallaxLayer =
                        child.AddComponent<FPG.Demo.Unity.D0ForestParallaxLayer>();
                    parallaxLayer.Configure(basePosition, viewportOffset);
                    parallaxLayers[index] = parallaxLayer;
                }

                FPG.Demo.Unity.D0ForestParallax parallax =
                    root.AddComponent<FPG.Demo.Unity.D0ForestParallax>();
                parallax.Configure(null, parallaxLayers);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null)
                {
                    error = "环境 Prefab 保存失败。";
                    return false;
                }

                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static ScriptableObject GetOrCreateDefinition(Type type, string path)
        {
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath(path, type) as ScriptableObject;
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetTaxonomy(ScriptableObject asset, string idProperty, string id,
            string displayName, string notes)
        {
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty(idProperty).stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("designerNotes").stringValue = notes;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value;
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
