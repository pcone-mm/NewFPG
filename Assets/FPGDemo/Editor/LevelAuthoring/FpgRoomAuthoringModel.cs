using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor.LevelAuthoring
{
    internal enum FpgRoomMarkerKind
    {
        Exit = 0,
        PlayerEntry = 1,
        EnemySpawn = 2,
        Destructible = 3,
        Cover = 5
    }

    internal enum FpgRoomValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    internal enum FpgRoomValidationStatus
    {
        Valid,
        Warning,
        Error
    }

    internal sealed class FpgRoomMarkerHandle
    {
        public FpgRoomMarkerHandle(FpgRoomMarkerKind kind, int index, string markerId, string displayName)
        {
            Kind = kind;
            Index = index;
            MarkerId = markerId;
            DisplayName = displayName;
        }

        public FpgRoomMarkerKind Kind { get; }
        public int Index { get; }
        public string MarkerId { get; }
        public string DisplayName { get; }
    }

    internal sealed class FpgRoomValidationItem
    {
        public FpgRoomValidationItem(
            FpgRoomValidationSeverity severity,
            string message,
            string propertyPath = null,
            FpgRoomMarkerKind? markerKind = null,
            int markerIndex = -1)
        {
            Severity = severity;
            Message = message;
            PropertyPath = propertyPath;
            MarkerKind = markerKind;
            MarkerIndex = markerIndex;
        }

        public FpgRoomValidationSeverity Severity { get; }
        public string Message { get; }
        public string PropertyPath { get; }
        public FpgRoomMarkerKind? MarkerKind { get; }
        public int MarkerIndex { get; }
    }

    internal sealed class FpgRoomRecord
    {
        public FpgRoomRecord(ScriptableObject asset, List<FpgRoomValidationItem> validation)
        {
            Asset = asset;
            Validation = validation;
        }

        public ScriptableObject Asset { get; }
        public List<FpgRoomValidationItem> Validation { get; }
        public string RoomId => FpgRoomAuthoringSchema.GetString(Asset, "roomId");
        public string DisplayName
        {
            get
            {
                string value = FpgRoomAuthoringSchema.GetString(Asset, "displayName");
                return string.IsNullOrWhiteSpace(value) ? Asset.name : value;
            }
        }

        public string MainGroupName => FpgRoomAuthoringSchema.GetObjectName(Asset, "mainGroup");
        public IReadOnlyList<string> TagNames => FpgRoomAuthoringSchema.GetObjectNames(Asset, "tags");
        public FpgRoomValidationStatus Status => Validation.Any(item => item.Severity == FpgRoomValidationSeverity.Error)
            ? FpgRoomValidationStatus.Error
            : Validation.Any(item => item.Severity == FpgRoomValidationSeverity.Warning)
                ? FpgRoomValidationStatus.Warning
                : FpgRoomValidationStatus.Valid;
    }

    internal static class FpgRoomAuthoringSchema
    {
        internal const string RoomTypeName = "FPG.Demo.Unity.FpgRoomDefinition, FPG.Unity";
        internal const string ScenarioTypeName = "FPG.Demo.Unity.D0CombatScenarioDefinition, FPG.Unity";
        internal const string DefaultRoomAssetFolder = "Assets/FPGDemo/Config/Level/Rooms";

        private static readonly Dictionary<FpgRoomMarkerKind, string> MarkerArrayNames =
            new Dictionary<FpgRoomMarkerKind, string>
            {
                { FpgRoomMarkerKind.Exit, "exitSlots" },
                { FpgRoomMarkerKind.PlayerEntry, "playerEntryPoints" },
                { FpgRoomMarkerKind.EnemySpawn, "enemySpawnPoints" },
                { FpgRoomMarkerKind.Destructible, "destructibleSlots" },
                { FpgRoomMarkerKind.Cover, "coverSlots" }
            };

        private static readonly Dictionary<FpgRoomMarkerKind, Color> MarkerColors =
            new Dictionary<FpgRoomMarkerKind, Color>
            {
                { FpgRoomMarkerKind.Exit, new Color(0.95f, 0.66f, 0.22f) },
                { FpgRoomMarkerKind.PlayerEntry, new Color(0.22f, 0.86f, 0.62f) },
                { FpgRoomMarkerKind.EnemySpawn, new Color(0.94f, 0.32f, 0.30f) },
                { FpgRoomMarkerKind.Destructible, new Color(0.76f, 0.39f, 0.88f) },
                { FpgRoomMarkerKind.Cover, new Color(0.96f, 0.78f, 0.28f) }
            };

        internal static Type RoomType => Type.GetType(RoomTypeName, false);
        internal static Type ScenarioType => Type.GetType(ScenarioTypeName, false);

        internal static string MarkerArrayName(FpgRoomMarkerKind kind) => MarkerArrayNames[kind];
        internal static Color MarkerColor(FpgRoomMarkerKind kind) => MarkerColors[kind];

        internal static string MarkerKindName(FpgRoomMarkerKind kind)
        {
            switch (kind)
            {
                case FpgRoomMarkerKind.Exit: return "出口";
                case FpgRoomMarkerKind.PlayerEntry: return "玩家入口";
                case FpgRoomMarkerKind.EnemySpawn: return "敌人出生";
                case FpgRoomMarkerKind.Destructible: return "可破坏物";
                case FpgRoomMarkerKind.Cover: return "掩体";
                default: return kind.ToString();
            }
        }

        internal static string ChinesePropertyName(string propertyName)
        {
            switch (propertyName)
            {
                case "roomId": return "房间 ID";
                case "displayName": return "显示名";
                case "designerNotes": return "策划备注";
                case "artScene": return "关卡美术场景";
                case "mainGroup": return "主分组";
                case "tags": return "标签";
                case "markerId": return "标记 ID";
                case "localPosition": return "局部位置";
                case "localRotation": return "局部朝向";
                case "localEulerAngles": return "局部朝向";
                case "enemyRole": return "出生角色分类";
                case "role": return "出生角色分类";
                case "destructiblePrefab": return "可破坏物 Prefab";
                case "prefab": return "Prefab";
                case "maxDurability": return "最大耐久";
                case "isStartingCover": return "初始掩体";
                case "playerReachableLocalPosition": return "玩家到达点位置";
                case "playerReachableLocalEulerAngles": return "玩家到达点朝向";
                default: return ObjectNames.NicifyVariableName(propertyName);
            }
        }

        internal static List<ScriptableObject> FindAllRooms()
        {
            Type roomType = RoomType;
            if (roomType == null)
            {
                return new List<ScriptableObject>();
            }

            return AssetDatabase.FindAssets("t:" + roomType.Name)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath(path, roomType) as ScriptableObject)
                .Where(room => room != null)
                .OrderBy(room => GetString(room, "displayName"), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static string GetString(ScriptableObject asset, string propertyName)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            SerializedProperty property = new SerializedObject(asset).FindProperty(propertyName);
            return property != null && property.propertyType == SerializedPropertyType.String
                ? property.stringValue
                : string.Empty;
        }

        internal static string GetObjectName(ScriptableObject asset, string propertyName)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            SerializedProperty property = new SerializedObject(asset).FindProperty(propertyName);
            return property != null && property.propertyType == SerializedPropertyType.ObjectReference
                && property.objectReferenceValue != null
                    ? GetTaxonomyDisplayName(property.objectReferenceValue)
                    : string.Empty;
        }

        internal static IReadOnlyList<string> GetObjectNames(ScriptableObject asset, string propertyName)
        {
            List<string> values = new List<string>();
            if (asset == null)
            {
                return values;
            }

            SerializedProperty property = new SerializedObject(asset).FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return values;
            }

            for (int index = 0; index < property.arraySize; index++)
            {
                UnityEngine.Object value = property.GetArrayElementAtIndex(index).objectReferenceValue;
                if (value != null)
                {
                    values.Add(GetTaxonomyDisplayName(value));
                }
            }

            return values;
        }

        private static string GetTaxonomyDisplayName(UnityEngine.Object value)
        {
            if (value is FPG.Demo.Unity.FpgRoomGroupDefinition group)
            {
                return string.IsNullOrWhiteSpace(group.DisplayName) ? group.name : group.DisplayName;
            }

            if (value is FPG.Demo.Unity.FpgRoomTagDefinition tag)
            {
                return string.IsNullOrWhiteSpace(tag.DisplayName) ? tag.name : tag.DisplayName;
            }

            return value == null ? string.Empty : value.name;
        }


        internal static List<FpgRoomMarkerHandle> GetMarkers(ScriptableObject room)
        {
            List<FpgRoomMarkerHandle> markers = new List<FpgRoomMarkerHandle>();
            if (room == null)
            {
                return markers;
            }

            SerializedObject serializedRoom = new SerializedObject(room);
            foreach (KeyValuePair<FpgRoomMarkerKind, string> pair in MarkerArrayNames)
            {
                SerializedProperty array = serializedRoom.FindProperty(pair.Value);
                if (array == null || !array.isArray)
                {
                    continue;
                }

                for (int index = 0; index < array.arraySize; index++)
                {
                    SerializedProperty marker = array.GetArrayElementAtIndex(index);
                    string id = FindRelative(marker, "markerId", "id")?.stringValue ?? string.Empty;
                    string name = FindRelative(marker, "displayName", "name")?.stringValue ?? string.Empty;
                    markers.Add(new FpgRoomMarkerHandle(pair.Key, index, id, name));
                }
            }

            return markers;
        }

        internal static SerializedProperty FindMarkerProperty(
            SerializedObject serializedRoom,
            FpgRoomMarkerKind kind,
            int index)
        {
            SerializedProperty array = serializedRoom?.FindProperty(MarkerArrayName(kind));
            return array != null && array.isArray && index >= 0 && index < array.arraySize
                ? array.GetArrayElementAtIndex(index)
                : null;
        }

        internal static SerializedProperty FindRelative(SerializedProperty parent, params string[] names)
        {
            if (parent == null)
            {
                return null;
            }

            foreach (string name in names)
            {
                SerializedProperty child = parent.FindPropertyRelative(name);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        internal static Vector3 GetMarkerPosition(SerializedProperty marker)
        {
            SerializedProperty property = FindRelative(marker, "localPosition", "position");
            return property != null && property.propertyType == SerializedPropertyType.Vector3
                ? property.vector3Value
                : Vector3.zero;
        }

        internal static Quaternion GetMarkerRotation(SerializedProperty marker)
        {
            SerializedProperty property = FindRelative(marker, "localRotation", "rotation", "localEulerAngles");
            if (property == null)
            {
                return Quaternion.identity;
            }

            if (property.propertyType == SerializedPropertyType.Quaternion)
            {
                return property.quaternionValue;
            }

            return property.propertyType == SerializedPropertyType.Vector3
                ? Quaternion.Euler(property.vector3Value)
                : Quaternion.identity;
        }

        internal static void SetMarkerPosition(SerializedProperty marker, Vector3 position)
        {
            SerializedProperty property = FindRelative(marker, "localPosition", "position");
            if (property != null && property.propertyType == SerializedPropertyType.Vector3)
            {
                property.vector3Value = position;
            }
        }

        internal static void SetMarkerRotation(SerializedProperty marker, Quaternion rotation)
        {
            SerializedProperty property = FindRelative(marker, "localRotation", "rotation", "localEulerAngles");
            if (property == null)
            {
                return;
            }

            if (property.propertyType == SerializedPropertyType.Quaternion)
            {
                property.quaternionValue = rotation;
            }
            else if (property.propertyType == SerializedPropertyType.Vector3)
            {
                property.vector3Value = rotation.eulerAngles;
            }
        }

        internal static List<FpgRoomValidationItem> Validate(
            ScriptableObject room,
            IReadOnlyDictionary<string, int> globalIdCounts)
        {
            List<FpgRoomValidationItem> result = new List<FpgRoomValidationItem>();
            FPG.Demo.Unity.FpgRoomDefinition definition =
                room as FPG.Demo.Unity.FpgRoomDefinition;
            if (definition == null)
            {
                result.Add(Error("房间资产为空或类型无效。"));
                return result;
            }

            FPG.Demo.Unity.FpgRoomValidationResult runtimeResult = definition.Validate();
            for (int index = 0; index < runtimeResult.Issues.Count; index++)
            {
                result.Add(ToEditorValidationItem(room, runtimeResult.Issues[index]));
            }

            if (!string.IsNullOrWhiteSpace(definition.RoomId)
                && globalIdCounts != null
                && globalIdCounts.TryGetValue(definition.RoomId, out int count)
                && count > 1)
            {
                result.Add(Error($"房间 ID '{definition.RoomId}' 在项目中重复。", "roomId"));
            }

            return result;
        }

        internal static List<FpgRoomValidationItem> ValidateScenarioCompatibility(
            ScriptableObject room,
            ScriptableObject scenario)
        {
            List<FpgRoomValidationItem> result = new List<FpgRoomValidationItem>();
            if (room == null || scenario == null)
            {
                return result;
            }

            FPG.Demo.Unity.FpgRoomDefinition roomDefinition =
                room as FPG.Demo.Unity.FpgRoomDefinition;
            FPG.Demo.Unity.D0CombatScenarioDefinition scenarioDefinition =
                scenario as FPG.Demo.Unity.D0CombatScenarioDefinition;
            if (roomDefinition == null || scenarioDefinition == null)
            {
                result.Add(Error("房间或 D0 遭遇资产类型无效。"));
                return result;
            }

            FPG.Demo.Unity.FpgRoomEncounterValidationResult runtimeResult =
                FPG.Demo.Unity.FpgRoomEncounterValidator.Validate(
                    roomDefinition,
                    scenarioDefinition);
            for (int index = 0; index < runtimeResult.Issues.Count; index++)
            {
                FPG.Demo.Unity.FpgRoomEncounterValidationIssue issue = runtimeResult.Issues[index];
                if (issue.Code == FPG.Demo.Unity.FpgRoomEncounterValidationCode.InvalidRoom)
                {
                    continue;
                }

                result.Add(ToEditorEncounterValidationItem(room, issue));
            }

            return result;
        }

        private static FpgRoomValidationItem ToEditorValidationItem(
            ScriptableObject room,
            FPG.Demo.Unity.FpgRoomValidationIssue issue)
        {
            FpgRoomMarkerKind? markerKind = issue.MarkerKind.HasValue
                ? ToEditorMarkerKind(issue.MarkerKind.Value)
                : (FpgRoomMarkerKind?)null;
            int markerIndex = FindMarkerIndex(room, markerKind, issue.MarkerId);
            string propertyPath = markerKind.HasValue
                ? MarkerArrayName(markerKind.Value)
                : RoomIssuePropertyPath(issue.Code);
            FpgRoomValidationSeverity severity =
                issue.Severity == FPG.Demo.Unity.FpgRoomValidationSeverity.Error
                    ? FpgRoomValidationSeverity.Error
                    : FpgRoomValidationSeverity.Warning;
            return new FpgRoomValidationItem(
                severity,
                RoomIssueMessage(issue),
                propertyPath,
                markerKind,
                markerIndex);
        }

        private static FpgRoomValidationItem ToEditorEncounterValidationItem(
            ScriptableObject room,
            FPG.Demo.Unity.FpgRoomEncounterValidationIssue issue)
        {
            FpgRoomMarkerKind? markerKind = null;
            string propertyPath = null;
            if (issue.Code == FPG.Demo.Unity.FpgRoomEncounterValidationCode.MissingPlayerEntryPoint)
            {
                markerKind = FpgRoomMarkerKind.PlayerEntry;
                propertyPath = MarkerArrayName(markerKind.Value);
            }
            else if (issue.Code == FPG.Demo.Unity.FpgRoomEncounterValidationCode.MissingEnemySpawnPoint)
            {
                markerKind = FpgRoomMarkerKind.EnemySpawn;
                propertyPath = MarkerArrayName(markerKind.Value);
            }

            int markerIndex = FindMarkerIndex(room, markerKind, issue.MarkerId);
            string message;
            switch (issue.Code)
            {
                case FPG.Demo.Unity.FpgRoomEncounterValidationCode.MissingRoom:
                    message = "缺少房间定义。";
                    break;
                case FPG.Demo.Unity.FpgRoomEncounterValidationCode.MissingScenario:
                    message = "缺少 D0 遭遇配置。";
                    break;
                case FPG.Demo.Unity.FpgRoomEncounterValidationCode.InvalidScenario:
                    message = "D0 遭遇配置无效：" + issue.Message;
                    break;
                case FPG.Demo.Unity.FpgRoomEncounterValidationCode.MissingPlayerEntryPoint:
                    message = $"遭遇的玩家入口 '{issue.MarkerId}' 无法在当前房间解析。";
                    break;
                case FPG.Demo.Unity.FpgRoomEncounterValidationCode.MissingEnemySpawnPoint:
                    message = $"遭遇需要的敌人出生点 '{issue.MarkerId}' 无法在当前房间解析。";
                    break;
                default:
                    message = issue.Message;
                    break;
            }

            return new FpgRoomValidationItem(
                FpgRoomValidationSeverity.Error,
                message,
                propertyPath,
                markerKind,
                markerIndex);
        }

        private static string RoomIssuePropertyPath(
            FPG.Demo.Unity.FpgRoomValidationCode code)
        {
            switch (code)
            {
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingRoomId:
                case FPG.Demo.Unity.FpgRoomValidationCode.DuplicateRoomId:
                    return "roomId";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingDisplayName:
                    return "displayName";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingArtScene:
                case FPG.Demo.Unity.FpgRoomValidationCode.InvalidArtSceneReference:
                    return "artScene";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingMainGroup:
                case FPG.Demo.Unity.FpgRoomValidationCode.InvalidMainGroup:
                    return "mainGroup";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingTagReference:
                case FPG.Demo.Unity.FpgRoomValidationCode.InvalidTag:
                case FPG.Demo.Unity.FpgRoomValidationCode.DuplicateTag:
                    return "tags";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingPlayerEntryPoint:
                    return "playerEntryPoints";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingEnemySpawnPoint:
                    return "enemySpawnPoints";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingExitSlot:
                    return "exitSlots";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingCoverSlot:
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingStartingCover:
                case FPG.Demo.Unity.FpgRoomValidationCode.MultipleStartingCovers:
                    return "coverSlots";
                default:
                    return null;
            }
        }

        private static string RoomIssueMessage(
            FPG.Demo.Unity.FpgRoomValidationIssue issue)
        {
            switch (issue.Code)
            {
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingRoomId:
                    return "缺少全局唯一房间 ID。";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingDisplayName:
                    return "缺少房间显示名。";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingArtScene:
                    return "缺少关卡美术场景。";
                case FPG.Demo.Unity.FpgRoomValidationCode.InvalidArtSceneReference:
                    return "关卡美术场景的 GUID 与路径不一致或场景资产缺失。";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingMainGroup:
                    return "缺少主分组。";
                case FPG.Demo.Unity.FpgRoomValidationCode.InvalidMainGroup:
                    return "主分组资产无效：" + issue.Message;
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingTagReference:
                    return "标签列表包含空引用。";
                case FPG.Demo.Unity.FpgRoomValidationCode.InvalidTag:
                    return "标签资产无效：" + issue.Message;
                case FPG.Demo.Unity.FpgRoomValidationCode.DuplicateTag:
                    return "房间包含重复标签。";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingMarker:
                    return "标记列表包含空数据。";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingMarkerId:
                    return "标记缺少语义 ID。";
                case FPG.Demo.Unity.FpgRoomValidationCode.DuplicateMarkerId:
                    return $"标记 ID '{issue.MarkerId}' 在当前房间内重复。";
                case FPG.Demo.Unity.FpgRoomValidationCode.InvalidMarkerPose:
                    return $"标记 '{issue.MarkerId}' 包含非法 Transform。";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingDestructiblePrefab:
                    return $"可破坏物槽位 '{issue.MarkerId}' 缺少 Prefab。";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingPlayerEntryPoint:
                    return "至少需要一个玩家入口。";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingEnemySpawnPoint:
                    return "至少需要一个敌人出生点。";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingExitSlot:
                    return "尚未配置出口插槽；单房试玩仍可继续。";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingMarkerDisplayName:
                    return $"标记 '{issue.MarkerId}' 缺少中文显示名。";
                case FPG.Demo.Unity.FpgRoomValidationCode.InvalidEnemySpawnRole:
                    return $"敌人出生点 '{issue.MarkerId}' 的角色分类无效。";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingCoverSlot:
                    return "至少需要配置一个掩体。";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingCoverPrefab:
                    return $"掩体 '{issue.MarkerId}' 缺少 Prefab。";
                case FPG.Demo.Unity.FpgRoomValidationCode.InvalidCoverPrefab:
                    return $"掩体 '{issue.MarkerId}' 的 Prefab 契约无效：{issue.Message}";
                case FPG.Demo.Unity.FpgRoomValidationCode.InvalidCoverDurability:
                    return $"掩体 '{issue.MarkerId}' 的最大耐久必须大于 0。";
                case FPG.Demo.Unity.FpgRoomValidationCode.InvalidCoverReachablePose:
                    return $"掩体 '{issue.MarkerId}' 的玩家到达点无效。";
                case FPG.Demo.Unity.FpgRoomValidationCode.MissingStartingCover:
                    return "必须指定一个初始掩体。";
                case FPG.Demo.Unity.FpgRoomValidationCode.MultipleStartingCovers:
                    return "只能指定一个初始掩体。";
                case FPG.Demo.Unity.FpgRoomValidationCode.OverlappingCoverReachablePosition:
                    return $"掩体 '{issue.MarkerId}' 的玩家到达点横向位置与其他掩体重叠。";
                default:
                    return issue.Message;
            }
        }

        private static FpgRoomMarkerKind ToEditorMarkerKind(
            FPG.Demo.Unity.FpgRoomMarkerKind kind)
        {
            return (FpgRoomMarkerKind)(int)kind;
        }

        private static int FindMarkerIndex(
            ScriptableObject room,
            FpgRoomMarkerKind? kind,
            string markerId)
        {
            if (!kind.HasValue || string.IsNullOrWhiteSpace(markerId))
            {
                return -1;
            }

            FpgRoomMarkerHandle marker = GetMarkers(room).FirstOrDefault(candidate =>
                candidate.Kind == kind.Value
                && string.Equals(candidate.MarkerId, markerId, StringComparison.Ordinal));
            return marker == null ? -1 : marker.Index;
        }


        internal static string CreateSemanticMarkerId(
            ScriptableObject room,
            FpgRoomMarkerKind kind)
        {
            string prefix;
            switch (kind)
            {
                case FpgRoomMarkerKind.Exit: prefix = "exit"; break;
                case FpgRoomMarkerKind.PlayerEntry: prefix = "player"; break;
                case FpgRoomMarkerKind.EnemySpawn: prefix = "enemy-any"; break;
                case FpgRoomMarkerKind.Destructible: prefix = "destructible"; break;
                case FpgRoomMarkerKind.Cover: prefix = "cover"; break;
                default: prefix = "marker"; break;
            }

            HashSet<string> existing = new HashSet<string>(GetMarkers(room).Select(item => item.MarkerId), StringComparer.Ordinal);
            if (kind == FpgRoomMarkerKind.PlayerEntry && !existing.Contains("player-main"))
            {
                return "player-main";
            }

            if (kind == FpgRoomMarkerKind.EnemySpawn && !existing.Contains("enemy-main"))
            {
                return "enemy-main";
            }

            for (int suffix = 1; suffix < 1000; suffix++)
            {
                string candidate = $"{prefix}-{suffix:00}";
                if (!existing.Contains(candidate))
                {
                    return candidate;
                }
            }

            return prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        internal static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        internal static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ValidateRequiredObject(
            SerializedObject serializedRoom,
            string propertyName,
            string message,
            ICollection<FpgRoomValidationItem> result)
        {
            SerializedProperty property = serializedRoom.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
            {
                result.Add(Error(message, propertyName));
            }
        }

        private static void ValidateMarkerArray(
            SerializedObject serializedRoom,
            FpgRoomMarkerKind kind,
            bool required,
            bool warnWhenEmpty,
            ICollection<FpgRoomValidationItem> result)
        {
            string arrayName = MarkerArrayName(kind);
            SerializedProperty array = serializedRoom.FindProperty(arrayName);
            int count = array != null && array.isArray ? array.arraySize : 0;
            if (count == 0 && required)
            {
                result.Add(Error($"至少需要一个{MarkerKindName(kind)}。", arrayName));
            }
            else if (count == 0 && warnWhenEmpty)
            {
                result.Add(new FpgRoomValidationItem(
                    FpgRoomValidationSeverity.Warning,
                    $"尚未配置{MarkerKindName(kind)}；单房试玩仍可继续。",
                    arrayName));
            }

            if (array == null || !array.isArray)
            {
                return;
            }

            for (int index = 0; index < array.arraySize; index++)
            {
                SerializedProperty marker = array.GetArrayElementAtIndex(index);
                Vector3 position = GetMarkerPosition(marker);
                Quaternion rotation = GetMarkerRotation(marker);
                if (!IsFinite(position) || !IsFinite(rotation))
                {
                    result.Add(new FpgRoomValidationItem(
                        FpgRoomValidationSeverity.Error,
                        $"{MarkerKindName(kind)} #{index + 1} 包含非法 Transform。",
                        arrayName, kind, index));
                }

                if (kind == FpgRoomMarkerKind.Destructible)
                {
                    SerializedProperty prefab = FindRelative(marker, "prefab", "destructiblePrefab");
                    if (prefab == null || prefab.objectReferenceValue == null)
                    {
                        result.Add(new FpgRoomValidationItem(
                            FpgRoomValidationSeverity.Error,
                            $"可破坏物 #{index + 1} 缺少 Prefab。",
                            arrayName, kind, index));
                    }
                }
            }
        }

        private static FpgRoomValidationItem Error(string message, string path = null)
        {
            return new FpgRoomValidationItem(FpgRoomValidationSeverity.Error, message, path);
        }
    }
}
