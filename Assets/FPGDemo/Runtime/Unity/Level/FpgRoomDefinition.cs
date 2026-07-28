using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [CreateAssetMenu(
        fileName = "FpgRoomDefinition",
        menuName = "FPG Demo/Level/Room Definition")]
    public sealed class FpgRoomDefinition : ScriptableObject
    {
        [D0PlannerSection("基础信息")]
        [D0PlannerField("房间 ID", "项目内全局唯一且通常只读的稳定标识。复制房间资产时必须生成新 ID。")]
        [SerializeField]
        private string roomId;

        [D0PlannerField("显示名称", "供策划、筛选器和校验结果识别的名称，不参与运行时逻辑。")]
        [SerializeField]
        private string displayName;

        [TextArea]
        [D0PlannerField("策划备注", "记录房间构图、玩法意图和验收注意事项；运行时不读取此文本。")]
        [SerializeField]
        private string designerNotes;

        [D0PlannerField("关卡美术场景", "每个房间唯一对应的 Additive Art Scene。环境、灯光、天空盒、Volume、Probe 与烘焙数据均由该场景拥有。")]
        [SerializeField]
        private FpgRoomArtSceneReference artScene =
            new FpgRoomArtSceneReference();

        [D0PlannerField("主分组", "每个房间必须直接引用一个主分组。分组资产不反向保存房间列表。")]
        [SerializeField]
        private FpgRoomGroupDefinition mainGroup;

        [D0PlannerField("标签", "可选的组合筛选标签。标签不替代主分组，同一房间内不得重复。")]
        [SerializeField]
        private FpgRoomTagDefinition[] tags = Array.Empty<FpgRoomTagDefinition>();

        [D0PlannerSection("出口插槽")]
        [D0PlannerField("出口列表", "只声明出口姿态，不保存目标房间或跨房跳转规则。")]
        [SerializeField]
        private FpgRoomExitSlot[] exitSlots = Array.Empty<FpgRoomExitSlot>();

        [D0PlannerSection("玩家入口")]
        [D0PlannerField("玩家入口列表", "声明玩家可使用的进场位置与朝向。普通战斗房至少需要一个。")]
        [SerializeField]
        private FpgRoomPlayerEntryPoint[] playerEntryPoints =
            Array.Empty<FpgRoomPlayerEntryPoint>();

        [D0PlannerSection("敌人出生点")]
        [D0PlannerField("敌人出生点列表", "声明位置、朝向和角色分类；具体敌人及出生时机由 Encounter 所有。")]
        [SerializeField]
        private FpgRoomEnemySpawnPoint[] enemySpawnPoints =
            Array.Empty<FpgRoomEnemySpawnPoint>();

        [D0PlannerSection("可破坏物插槽")]
        [D0PlannerField("可破坏物列表", "房间只选择 Prefab 并设置位置与朝向，不覆盖生命、掉落、行为或缩放。")]
        [SerializeField]
        private FpgRoomDestructibleSlot[] destructibleSlots =
            Array.Empty<FpgRoomDestructibleSlot>();

        [D0PlannerSection("声明式可到达点")]
        [D0PlannerField("可到达点列表", "v1 仅保存策划声明和适用掩码，不保存连线，也不接入 A* 或 NavMesh 校验。")]
        [SerializeField]
        private FpgRoomReachablePoint[] reachablePoints =
            Array.Empty<FpgRoomReachablePoint>();

        public string RoomId => roomId;
        public string DisplayName => displayName;
        public string DesignerNotes => designerNotes;
        public FpgRoomArtSceneReference ArtScene => artScene;
        public string ArtScenePath => artScene == null
            ? string.Empty
            : artScene.ScenePath;
        public FpgRoomGroupDefinition MainGroup => mainGroup;
        public IReadOnlyList<FpgRoomTagDefinition> Tags =>
            tags ?? Array.Empty<FpgRoomTagDefinition>();
        public IReadOnlyList<FpgRoomExitSlot> ExitSlots =>
            exitSlots ?? Array.Empty<FpgRoomExitSlot>();
        public IReadOnlyList<FpgRoomPlayerEntryPoint> PlayerEntryPoints =>
            playerEntryPoints ?? Array.Empty<FpgRoomPlayerEntryPoint>();
        public IReadOnlyList<FpgRoomEnemySpawnPoint> EnemySpawnPoints =>
            enemySpawnPoints ?? Array.Empty<FpgRoomEnemySpawnPoint>();
        public IReadOnlyList<FpgRoomDestructibleSlot> DestructibleSlots =>
            destructibleSlots ?? Array.Empty<FpgRoomDestructibleSlot>();
        public IReadOnlyList<FpgRoomReachablePoint> ReachablePoints =>
            reachablePoints ?? Array.Empty<FpgRoomReachablePoint>();

        public bool TryGetExitSlot(string markerId, out FpgRoomExitSlot slot)
        {
            return TryFind(ExitSlots, markerId, out slot);
        }

        public bool TryGetPlayerEntryPoint(
            string markerId,
            out FpgRoomPlayerEntryPoint point)
        {
            return TryFind(PlayerEntryPoints, markerId, out point);
        }

        public bool TryGetEnemySpawnPoint(
            string markerId,
            out FpgRoomEnemySpawnPoint point)
        {
            return TryFind(EnemySpawnPoints, markerId, out point);
        }

        public bool TryGetDestructibleSlot(
            string markerId,
            out FpgRoomDestructibleSlot slot)
        {
            return TryFind(DestructibleSlots, markerId, out slot);
        }

        public bool TryGetReachablePoint(
            string markerId,
            out FpgRoomReachablePoint point)
        {
            return TryFind(ReachablePoints, markerId, out point);
        }

        public bool TryGetMarker(string markerId, out FpgRoomMarker marker)
        {
            if (TryGetExitSlot(markerId, out FpgRoomExitSlot exit))
            {
                marker = exit;
                return true;
            }

            if (TryGetPlayerEntryPoint(markerId, out FpgRoomPlayerEntryPoint player))
            {
                marker = player;
                return true;
            }

            if (TryGetEnemySpawnPoint(markerId, out FpgRoomEnemySpawnPoint enemy))
            {
                marker = enemy;
                return true;
            }

            if (TryGetDestructibleSlot(markerId, out FpgRoomDestructibleSlot destructible))
            {
                marker = destructible;
                return true;
            }

            if (TryGetReachablePoint(markerId, out FpgRoomReachablePoint reachable))
            {
                marker = reachable;
                return true;
            }

            marker = null;
            return false;
        }

        public FpgRoomValidationResult Validate()
        {
            List<FpgRoomValidationIssue> issues = new List<FpgRoomValidationIssue>();
            if (string.IsNullOrWhiteSpace(roomId))
            {
                AddError(issues, FpgRoomValidationCode.MissingRoomId,
                    "Room definition requires a globally unique room ID.");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                AddError(issues, FpgRoomValidationCode.MissingDisplayName,
                    "Room definition requires a display name.");
            }

            string artSceneError = "Art Scene reference is missing.";
            if (artScene == null || !artScene.TryValidate(out artSceneError))
            {
                FpgRoomValidationCode code = artScene == null
                    || !artScene.IsAssigned
                        ? FpgRoomValidationCode.MissingArtScene
                        : FpgRoomValidationCode.InvalidArtSceneReference;
                AddError(
                    issues,
                    code,
                    $"Room '{roomId}' has an invalid Art Scene reference: {artSceneError}");
            }

            ValidateGrouping(issues);

            HashSet<string> markerIds = new HashSet<string>(StringComparer.Ordinal);
            ValidateMarkers(ExitSlots, markerIds, issues);
            ValidateMarkers(PlayerEntryPoints, markerIds, issues);
            ValidateMarkers(EnemySpawnPoints, markerIds, issues);
            ValidateMarkers(DestructibleSlots, markerIds, issues);
            ValidateMarkers(ReachablePoints, markerIds, issues);

            if (PlayerEntryPoints.Count == 0)
            {
                AddError(issues, FpgRoomValidationCode.MissingPlayerEntryPoint,
                    $"Room '{roomId}' requires at least one player entry point.");
            }

            if (EnemySpawnPoints.Count == 0)
            {
                AddError(issues, FpgRoomValidationCode.MissingEnemySpawnPoint,
                    $"Room '{roomId}' requires at least one enemy spawn point.");
            }

            if (ExitSlots.Count == 0)
            {
                AddWarning(issues, FpgRoomValidationCode.MissingExitSlot,
                    $"Room '{roomId}' has no exit slot. Single-room play remains available.");
            }

            if (ReachablePoints.Count == 0)
            {
                AddWarning(issues, FpgRoomValidationCode.MissingReachablePoint,
                    $"Room '{roomId}' has no declared reachable point.");
            }

            return new FpgRoomValidationResult(issues);
        }

        public bool TryValidate(out FpgRoomValidationResult result)
        {
            result = Validate();
            return result.IsValid;
        }

        private void ValidateGrouping(List<FpgRoomValidationIssue> issues)
        {
            if (mainGroup == null)
            {
                AddError(issues, FpgRoomValidationCode.MissingMainGroup,
                    $"Room '{roomId}' requires one main group.");
            }
            else if (!mainGroup.TryValidate(out string groupError))
            {
                AddError(issues, FpgRoomValidationCode.InvalidMainGroup, groupError);
            }

            HashSet<string> tagIds = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<FpgRoomTagDefinition> roomTags = Tags;
            for (int index = 0; index < roomTags.Count; index++)
            {
                FpgRoomTagDefinition tag = roomTags[index];
                if (tag == null)
                {
                    AddError(issues, FpgRoomValidationCode.MissingTagReference,
                        $"Room '{roomId}' tag entry {index} is missing.");
                    continue;
                }

                if (!tag.TryValidate(out string tagError))
                {
                    AddError(issues, FpgRoomValidationCode.InvalidTag, tagError);
                    continue;
                }

                if (!tagIds.Add(tag.TagId))
                {
                    AddError(issues, FpgRoomValidationCode.DuplicateTag,
                        $"Room '{roomId}' contains duplicate tag ID '{tag.TagId}'.");
                }
            }
        }

        private static void ValidateMarkers<T>(
            IReadOnlyList<T> markers,
            HashSet<string> markerIds,
            List<FpgRoomValidationIssue> issues)
            where T : FpgRoomMarker
        {
            for (int index = 0; index < markers.Count; index++)
            {
                T marker = markers[index];
                if (marker == null)
                {
                    AddError(issues, FpgRoomValidationCode.MissingMarker,
                        $"Marker entry {index} in the {typeof(T).Name} list is missing.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(marker.MarkerId))
                {
                    AddMarkerError(issues, FpgRoomValidationCode.MissingMarkerId,
                        marker, $"{marker.Kind} marker {index} requires a semantic ID.");
                }
                else if (!markerIds.Add(marker.MarkerId))
                {
                    AddMarkerError(issues, FpgRoomValidationCode.DuplicateMarkerId,
                        marker, $"Marker ID '{marker.MarkerId}' must be unique within the room.");
                }

                if (string.IsNullOrWhiteSpace(marker.DisplayName))
                {
                    AddMarkerError(issues, FpgRoomValidationCode.MissingMarkerDisplayName,
                        marker, $"Marker '{marker.MarkerId}' requires a display name.");
                }

                if (!marker.HasFinitePose)
                {
                    AddMarkerError(issues, FpgRoomValidationCode.InvalidMarkerPose,
                        marker, $"Marker '{marker.MarkerId}' requires finite local position and rotation values.");
                }

                if (marker is FpgRoomDestructibleSlot destructible
                    && destructible.Prefab == null)
                {
                    AddMarkerError(issues, FpgRoomValidationCode.MissingDestructiblePrefab,
                        marker, $"Destructible slot '{marker.MarkerId}' requires a Prefab.");
                }

                if (marker is FpgRoomReachablePoint reachable
                    && (reachable.Audience == FpgRoomReachableAudience.None
                        || (reachable.Audience & ~FpgRoomReachableAudience.PlayerAndEnemy) != 0))
                {
                    AddMarkerError(issues, FpgRoomValidationCode.InvalidReachableAudience,
                        marker, $"Reachable point '{marker.MarkerId}' requires a valid player/enemy audience mask.");
                }

                if (marker is FpgRoomEnemySpawnPoint enemy
                    && !Enum.IsDefined(typeof(FpgRoomEnemySpawnRole), enemy.Role))
                {
                    AddMarkerError(issues, FpgRoomValidationCode.InvalidEnemySpawnRole,
                        marker, $"Enemy spawn point '{marker.MarkerId}' has an invalid role.");
                }
            }
        }

        private static bool TryFind<T>(
            IReadOnlyList<T> markers,
            string markerId,
            out T marker)
            where T : FpgRoomMarker
        {
            if (!string.IsNullOrEmpty(markerId))
            {
                for (int index = 0; index < markers.Count; index++)
                {
                    T candidate = markers[index];
                    if (candidate != null
                        && string.Equals(candidate.MarkerId, markerId, StringComparison.Ordinal))
                    {
                        marker = candidate;
                        return true;
                    }
                }
            }

            marker = null;
            return false;
        }

        private static void AddError(
            List<FpgRoomValidationIssue> issues,
            FpgRoomValidationCode code,
            string message)
        {
            issues.Add(new FpgRoomValidationIssue(
                FpgRoomValidationSeverity.Error, code, message));
        }

        private static void AddWarning(
            List<FpgRoomValidationIssue> issues,
            FpgRoomValidationCode code,
            string message)
        {
            issues.Add(new FpgRoomValidationIssue(
                FpgRoomValidationSeverity.Warning, code, message));
        }

        private static void AddMarkerError(
            List<FpgRoomValidationIssue> issues,
            FpgRoomValidationCode code,
            FpgRoomMarker marker,
            string message)
        {
            issues.Add(new FpgRoomValidationIssue(
                FpgRoomValidationSeverity.Error,
                code,
                message,
                marker.Kind,
                marker.MarkerId));
        }
    }
}
