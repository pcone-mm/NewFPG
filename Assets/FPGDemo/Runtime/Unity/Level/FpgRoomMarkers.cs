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
        Reachable = 4
    }

    public enum FpgRoomEnemySpawnRole
    {
        Any = 0,
        Melee = 1,
        Ranged = 2,
        Support = 3
    }

    [Flags]
    public enum FpgRoomReachableAudience
    {
        None = 0,
        Player = 1 << 0,
        Enemy = 1 << 1,
        PlayerAndEnemy = Player | Enemy
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
    public sealed class FpgRoomReachablePoint : FpgRoomMarker
    {
        [D0PlannerField("适用对象", "声明该点适用于玩家、敌人或两者。v1 不创建连线，也不执行 A* 或 NavMesh 可达校验。")]
        [SerializeField]
        private FpgRoomReachableAudience audience = FpgRoomReachableAudience.PlayerAndEnemy;

        public FpgRoomReachableAudience Audience => audience;
        public override FpgRoomMarkerKind Kind => FpgRoomMarkerKind.Reachable;
    }
}
