using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum D0EnemyBehaviorMode
    {
        Patrol = 0,
        FixedPosition = 1
    }

    /// <summary>
    /// Planner-authored presentation behavior for D0's one enemy. Positions are
    /// offsets from the authored visual and gameplay baseline so the same
    /// profile moves the CZN actor and its query hitboxes together.
    /// </summary>
    [CreateAssetMenu(
        fileName = "D0EnemyBehaviorProfile",
        menuName = "FPG Demo/Config/D0 Enemy Behavior Profile")]
    public sealed class D0EnemyBehaviorProfile : ScriptableObject
    {
        [D0PlannerSection("行为模式")]
        [D0PlannerField("行为模式", "“巡逻”使用已配置的入场、巡逻和退场偏移。“固定站位”保持安装基线位置，仅供陆鸾／蝴蝶遭遇合同使用；它不会改变命中盒或投射物规则。")]
        [SerializeField]
        private D0EnemyBehaviorMode behaviorMode = D0EnemyBehaviorMode.Patrol;

        [D0PlannerSection("行为配置标识")]
        [D0PlannerField("行为配置 ID", "用于场景关联、校验和日志定位的稳定标识。创建后保持非空且稳定，不是移动数值。")]
        [SerializeField]
        private string profileId = "burstbug-combatlab-patrol";

        [D0PlannerField("显示名称", "供策划和验证日志识别的敌人行为名称，不直接参与战斗计算。")]
        [SerializeField]
        private string displayName = "Burstbug CombatLab Patrol";

        [TextArea]
        [D0PlannerField("策划说明", "记录入场、巡逻、攻击停顿与退场的调参意图；运行时不会读取此文本。")]
        [SerializeField]
        private string designerNotes;

        [D0PlannerSection("入场动画契约")]
        [D0PlannerField("入场动画标识", "必须与敌人 D0 角色表现资产的入场动画一致。实际播放仍由角色表现路由器执行；此字段防止行为配置与表现资源脱节。")]
        [SerializeField]
        private string entryAnimationSlot = "enter";

        [D0PlannerSection("入场与巡逻路径")]
        [D0PlannerField("入场起点偏移（世界单位）", "敌人从安装基线加此偏移处开始移动，并先前往“巡逻端点 A”。运行时会同步移动敌人视觉对象和攻击查询锚点；修改后请重启战斗并验证主射、副射命中。")]
        [SerializeField]
        private Vector3 entryOffset = new Vector3(7.5f, 0f, 0f);

        [D0PlannerField("巡逻端点 A 偏移（世界单位）", "敌人入场后首先前往的巡逻端点，之后会与端点 B 往返。运行时会同步移动敌人视觉对象和攻击查询锚点；不要把它当作碰撞体尺寸。")]
        [SerializeField]
        private Vector3 patrolLeftOffset = new Vector3(-1.55f, 0f, 0f);

        [D0PlannerField("巡逻端点 B 偏移（世界单位）", "敌人在端点 A 与此端点之间往返的另一端。运行时会同步移动敌人视觉对象和攻击查询锚点；不要把它当作碰撞体尺寸。")]
        [SerializeField]
        private Vector3 patrolRightOffset = new Vector3(1.55f, 0f, 0f);

        [D0PlannerField("入场速度（世界单位／秒）", "敌人从入场起点移动到巡逻端点 A 的速度。运行时按每秒世界单位换算为 Tick 移动。")]
        [SerializeField, Min(0.01f)]
        private float entrySpeed = 5f;

        [D0PlannerField("巡逻速度（世界单位／秒）", "敌人在巡逻端点 A 与 B 之间往返的速度。运行时按每秒世界单位换算为 Tick 移动。")]
        [SerializeField, Min(0.01f)]
        private float patrolSpeed = 1.4f;

        [D0PlannerSection("攻击期间停顿与恢复")]
        [D0PlannerField("威胁期间停止巡逻", "启用后，敌人在预警、前摇、释放和后摇期间停止巡逻；待命和完成状态不属于此停顿范围。")]
        [SerializeField]
        private bool stopDuringThreat = true;

        [D0PlannerField("威胁结束后恢复巡逻", "仅在“威胁期间停止巡逻”启用时生效。启用后，威胁结束会继续在端点间巡逻；关闭后会停在攻击位置直到本局重启。")]
        [SerializeField]
        private bool resumePatrolAfterRecovery = true;

        [D0PlannerSection("战斗结束后退场")]
        [D0PlannerField("退场目标偏移（世界单位）", "战斗结束后敌人前往的目标偏移。运行时会同步移动敌人视觉对象和攻击查询锚点；修改后请重启战斗并验证。")]
        [SerializeField]
        private Vector3 deathExitOffset = new Vector3(7.5f, 0f, 0f);

        [D0PlannerField("退场延迟（秒）", "战斗结束后开始退场前的等待时间。运行时会按每秒 60 Tick 向上换算为 Tick。")]
        [SerializeField, Min(0f)]
        private float deathExitDelaySeconds = 1.25f;

        [D0PlannerField("退场速度（世界单位／秒）", "敌人从当前位置移动到退场目标的速度。运行时按每秒世界单位换算为 Tick 移动。")]
        [SerializeField, Min(0.01f)]
        private float deathExitSpeed = 3.5f;

        public string ProfileId => profileId;
        public string DisplayName => displayName;
        public string DesignerNotes => designerNotes;
        public string EntryAnimationSlot => entryAnimationSlot;
        public D0EnemyBehaviorMode BehaviorMode => behaviorMode;
        public bool UsesFixedPosition => behaviorMode == D0EnemyBehaviorMode.FixedPosition;
        public Vector3 EntryOffset => UsesFixedPosition ? Vector3.zero : entryOffset;
        public Vector3 PatrolLeftOffset => UsesFixedPosition ? Vector3.zero : patrolLeftOffset;
        public Vector3 PatrolRightOffset => UsesFixedPosition ? Vector3.zero : patrolRightOffset;
        public float EntrySpeed => entrySpeed;
        public float PatrolSpeed => patrolSpeed;
        public bool StopDuringThreat => UsesFixedPosition || stopDuringThreat;
        public bool ResumePatrolAfterRecovery => !UsesFixedPosition && resumePatrolAfterRecovery;
        public Vector3 DeathExitOffset => UsesFixedPosition ? Vector3.zero : deathExitOffset;
        public float DeathExitDelaySeconds => UsesFixedPosition ? 0f : deathExitDelaySeconds;
        public float DeathExitSpeed => deathExitSpeed;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(profileId)
                || string.IsNullOrWhiteSpace(displayName)
                || string.IsNullOrWhiteSpace(entryAnimationSlot))
            {
                error = "D0 enemy behavior profile requires stable ID, display name and entry animation slot values.";
                return false;
            }

            if (behaviorMode != D0EnemyBehaviorMode.Patrol
                && behaviorMode != D0EnemyBehaviorMode.FixedPosition)
            {
                error = "D0 enemy behavior profile has an unsupported behavior mode.";
                return false;
            }

            if (!IsFinite(entryOffset)
                || !IsFinite(patrolLeftOffset)
                || !IsFinite(patrolRightOffset)
                || !IsFinite(deathExitOffset)
                || !IsFinitePositive(entrySpeed)
                || !IsFinitePositive(patrolSpeed)
                || !IsFiniteNonNegative(deathExitDelaySeconds)
                || !IsFinitePositive(deathExitSpeed))
            {
                error = "D0 enemy behavior profile contains invalid anchors or movement values.";
                return false;
            }

            if (behaviorMode == D0EnemyBehaviorMode.Patrol
                && (Mathf.Abs(patrolLeftOffset.x - patrolRightOffset.x) < 0.01f
                    || Mathf.Abs(patrolLeftOffset.y - patrolRightOffset.y) > 0.0001f
                    || Mathf.Abs(patrolLeftOffset.z - patrolRightOffset.z) > 0.0001f))
            {
                error = "D0 enemy behavior profile requires distinct left/right anchors on one horizontal X axis.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
