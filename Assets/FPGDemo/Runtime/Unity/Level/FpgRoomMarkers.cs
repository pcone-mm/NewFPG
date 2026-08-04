using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum FpgRoomMarkerKind
    {
        Exit = 0,
        PlayerEntry = 1,
        EnemySpawn = 2,
        Destructible = 3,
        Cover = 5
    }

    public enum FpgRoomEnemySpawnRole
    {
        Any = 0,
        Melee = 1,
        Ranged = 2,
        Support = 3
    }

    [Serializable]
    public abstract class FpgRoomMarker
    {
        [D0PlannerField("标记 ID", "当前房间内唯一的语义标识，例如 player-main 或 enemy-melee-01。复制房间时保留此 ID。")]
        [SerializeField]
        private string markerId;

        [D0PlannerField("中文显示名", "供编辑器列表和 SceneView 标签显示，不参与运行时解析。")]
        [SerializeField]
        private string displayName;

        [D0PlannerField("局部位置", "相对于房间实例根节点的位置。")]
        [SerializeField]
        private Vector3 localPosition;

        [D0PlannerField("局部旋转（度）", "相对于房间实例根节点的欧拉角；房间不保存标记缩放。")]
        [SerializeField]
        private Vector3 localEulerAngles;

        public string MarkerId => markerId;
        public string DisplayName => displayName;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEulerAngles => localEulerAngles;
        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
        public Pose LocalPose => new Pose(localPosition, LocalRotation);
        public abstract FpgRoomMarkerKind Kind { get; }

        internal bool HasFinitePose =>
            FpgRoomValidationUtility.IsFinite(localPosition)
            && FpgRoomValidationUtility.IsFinite(localEulerAngles);
    }

    [Serializable]
    public sealed class FpgRoomExitSlot : FpgRoomMarker
    {
        public override FpgRoomMarkerKind Kind => FpgRoomMarkerKind.Exit;
    }

    [Serializable]
    public sealed class FpgRoomPlayerEntryPoint : FpgRoomMarker
    {
        public override FpgRoomMarkerKind Kind => FpgRoomMarkerKind.PlayerEntry;
    }

    [Serializable]
    public sealed class FpgRoomEnemySpawnPoint : FpgRoomMarker
    {
        [D0PlannerField("出生角色分类", "约束该位置适合 Any、Melee、Ranged 或 Support 敌人；不指定具体敌人资产。")]
        [SerializeField]
        private FpgRoomEnemySpawnRole role = FpgRoomEnemySpawnRole.Any;

        public FpgRoomEnemySpawnRole Role => role;
        public override FpgRoomMarkerKind Kind => FpgRoomMarkerKind.EnemySpawn;
    }

    [Serializable]
    public sealed class FpgRoomDestructibleSlot : FpgRoomMarker
    {
        [D0PlannerField("可破坏物 Prefab", "可破坏物的生命、掉落、动画、行为和缩放完全由 Prefab 所有，房间只决定位置与朝向。")]
        [SerializeField]
        private GameObject prefab;

        public GameObject Prefab => prefab;
        public override FpgRoomMarkerKind Kind => FpgRoomMarkerKind.Destructible;
    }

    [Serializable]
    public sealed class FpgRoomCoverSlot : FpgRoomMarker
    {
        [D0PlannerField("掩体 Prefab", "掩体样式、完好与损毁外观以及阻挡碰撞均由 Prefab 所有。")]
        [SerializeField]
        private GameObject prefab;

        [D0PlannerField("镜头配置", "玩家抵达该掩体后使用的独立镜头构图配置；正式房间中必填。")]
        [SerializeField]
        private FpgCoverCameraProfile cameraProfile;

        [D0PlannerField("最大耐久", "进入或重新开始房间时恢复到该值，不跨房间保存。")]
        [SerializeField]
        private int maxDurability = 100;

        [D0PlannerField("初始掩体", "正式房间必须且只能配置一个初始掩体。")]
        [SerializeField]
        private bool isStartingCover;

        [D0PlannerField("玩家到达点位置", "玩家在该掩体节点停留时使用的房间局部位置。")]
        [SerializeField]
        private Vector3 playerReachableLocalPosition;

        [D0PlannerField("玩家到达点朝向", "玩家抵达该掩体节点后的房间局部朝向。")]
        [SerializeField]
        private Vector3 playerReachableLocalEulerAngles;

        [D0PlannerField("玩家左侧探身点位置", "玩家从当前掩体向左探身时使用的房间局部表现位置。")]
        [SerializeField]
        private Vector3 playerLeftPeekLocalPosition;

        [D0PlannerField("玩家右侧探身点位置", "玩家从当前掩体向右探身时使用的房间局部表现位置。")]
        [SerializeField]
        private Vector3 playerRightPeekLocalPosition;

        public GameObject Prefab => prefab;
        public FpgCoverCameraProfile CameraProfile => cameraProfile;
        public int MaxDurability => maxDurability;
        public bool IsStartingCover => isStartingCover;
        public Vector3 PlayerReachableLocalPosition =>
            playerReachableLocalPosition;
        public Vector3 PlayerReachableLocalEulerAngles =>
            playerReachableLocalEulerAngles;
        public Quaternion PlayerReachableLocalRotation =>
            Quaternion.Euler(playerReachableLocalEulerAngles);
        public Vector3 PlayerLeftPeekLocalPosition =>
            playerLeftPeekLocalPosition;
        public Vector3 PlayerRightPeekLocalPosition =>
            playerRightPeekLocalPosition;
        public Pose PlayerReachableLocalPose => new Pose(
            playerReachableLocalPosition,
            PlayerReachableLocalRotation);
        public override FpgRoomMarkerKind Kind => FpgRoomMarkerKind.Cover;

        internal bool HasFiniteReachablePose =>
            FpgRoomValidationUtility.IsFinite(playerReachableLocalPosition)
            && FpgRoomValidationUtility.IsFinite(
                playerReachableLocalEulerAngles);
        internal bool HasValidPeekPositions
        {
            get
            {
                const float MinimumDistanceSquared = 0.000001f;
                return FpgRoomValidationUtility.IsFinite(
                        playerLeftPeekLocalPosition)
                    && FpgRoomValidationUtility.IsFinite(
                        playerRightPeekLocalPosition)
                    && (playerLeftPeekLocalPosition
                        - playerRightPeekLocalPosition).sqrMagnitude
                        > MinimumDistanceSquared
                    && (playerLeftPeekLocalPosition
                        - playerReachableLocalPosition).sqrMagnitude
                        > MinimumDistanceSquared
                    && (playerRightPeekLocalPosition
                        - playerReachableLocalPosition).sqrMagnitude
                        > MinimumDistanceSquared;
            }
        }
    }
}
