using UnityEngine;

namespace FPG.Demo.Unity
{
    [CreateAssetMenu(
        fileName = "D0EnemyDefinition",
        menuName = "FPG Demo/Config/D0 Enemy Definition")]
    public sealed class D0EnemyDefinition : ScriptableObject
    {
        [D0PlannerSection("基础信息")]
        [D0PlannerField("敌人 ID", "敌人的稳定配置标识，用于配置关联与校验。保持非空且稳定，不要用显示名称替代。")]
        [SerializeField]
        private string enemyId = "burstbug";

        [D0PlannerField("显示名称", "供策划、验证日志和编辑器识别的敌人名称，不参与战斗计算。")]
        [SerializeField]
        private string displayName = "Burstbug";

        [TextArea]
        [D0PlannerField("策划说明", "记录敌人定位、数值意图和验证备注；运行时不会读取此文本。")]
        [SerializeField]
        private string designerNotes;

        [D0PlannerSection("战斗数值")]
        [D0PlannerField("生命上限", "敌人战斗开始时的生命上限与初始值。必须大于 0。")]
        [SerializeField, Min(1)]
        private int life = 800;

        [D0PlannerField("韧性上限", "敌人战斗开始时的韧性上限与初始值。降为 0 会触发破韧硬直，并在硬直结束后恢复。")]
        [SerializeField, Min(1)]
        private int breakValue = 160;

        [D0PlannerSection("表现关联")]
        [D0PlannerField("敌人表现配置", "必须引用“敌人”类型的角色表现资产，其中包含模型、动画名和 Burstbug 专属特效池。")]
        [SerializeField]
        private D0ActorPresentationDefinition actorPresentation;

        [D0PlannerField("敌人行为模式", "配置场外入场、左右循环横移、攻击停驻、后摇恢复巡航和死亡退场。不包含跟踪、转向或导航。")]
        [SerializeField]
        private D0EnemyBehaviorProfile behaviorProfile;

        [D0PlannerSection("实体预制体")]
        [D0PlannerField("敌人 Entity Prefab", "敌人的完整运行时实体。预制体拥有视觉根、gameplay 根、投射物锚点、弱点和命中体；关卡与遭遇都不得复制这些结构。")]
        [SerializeField]
        private D0EnemyEntityView entityPrefab;

        public string EnemyId => enemyId;
        public string DisplayName => displayName;
        public int Life => life;
        public int BreakValue => breakValue;
        public D0ActorPresentationDefinition ActorPresentation => actorPresentation;
        public D0EnemyBehaviorProfile BehaviorProfile => behaviorProfile;
        public D0EnemyEntityView EntityPrefab => entityPrefab;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(enemyId) || string.IsNullOrWhiteSpace(displayName))
            {
                error = "Enemy definition requires stable ID and display name values.";
                return false;
            }

            if (life <= 0 || breakValue <= 0)
            {
                error = "Enemy life and break must be positive.";
                return false;
            }

            if (actorPresentation == null || actorPresentation.ActorKind != D0ActorKind.Enemy)
            {
                error = "Enemy definition requires an enemy actor presentation.";
                return false;
            }

            if (!actorPresentation.TryValidate(out error))
            {
                return false;
            }

            if (behaviorProfile == null)
            {
                error = "Enemy definition requires a D0 enemy behavior profile.";
                return false;
            }

            if (!behaviorProfile.TryValidate(out error))
            {
                return false;
            }

            if (entityPrefab == null)
            {
                error = "Enemy definition requires an entity prefab.";
                return false;
            }
            return entityPrefab.TryValidate(out error);
        }
    }
}
