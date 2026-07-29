using UnityEngine;

namespace FPG.Demo.Unity
{
    [CreateAssetMenu(
        fileName = "D0CharacterDefinition",
        menuName = "FPG Demo/Config/D0 Character Definition")]
    public sealed class D0CharacterDefinition : ScriptableObject
    {
        [D0PlannerSection("基础信息")]
        [D0PlannerField("角色 ID", "角色的稳定标识，用于配置关联与校验。保持非空且稳定，不要用显示名称替代。")]
        [SerializeField]
        private string characterId = "fei";

        [D0PlannerField("显示名称", "供策划和编辑器识别的角色名称，不参与战斗计算。")]
        [SerializeField]
        private string displayName = "Fei";

        [TextArea]
        [D0PlannerField("策划说明", "记录角色定位、改动原因和验证备注；运行时不会读取此文本。")]
        [SerializeField]
        private string designerNotes;

        [D0PlannerSection("生存数值")]
        [D0PlannerField("生命上限", "战斗开始时的生命值上限与初始值。必须大于 0；敌方伤害在角色探身时结算到生命。")]
        [SerializeField, Min(1)]
        private int life = 100;

        [D0PlannerField("护盾上限", "战斗开始时的掩体护盾上限与初始值。必须大于 0；护盾被打空后保持耗尽。")]
        [SerializeField, Min(1)]
        private int barrier = 100;

        [D0PlannerSection("关联资产")]
        [D0PlannerField("武器配置", "该角色使用的武器资产。Fei 的主射与副射在同一资产中作为两种独立攻击配置，并共享弹匣。")]
        [SerializeField]
        private D0WeaponDefinition weapon;

        [D0PlannerField("玩家 Entity Prefab", "玩家完整实体预制体。它是 GameplayRoot、VisualRoot、命中体、弱点、Socket 和本地表现组件的唯一人工编辑入口；场景和角色表现资产不得另存一份玩家实体结构。")]
        [SerializeField]
        private FpgPlayerEntityView entityPrefab;

        [D0PlannerField("角色表现", "该角色的模型、动画名和表现时序。这里只引用表现资产，不修改战斗判定。")]
        [SerializeField]
        private D0ActorPresentationDefinition actorPresentation;

        public string CharacterId => characterId;
        public string DisplayName => displayName;
        public int Life => life;
        public int Barrier => barrier;
        public D0WeaponDefinition Weapon => weapon;
        public FpgPlayerEntityView EntityPrefab => entityPrefab;
        public D0ActorPresentationDefinition ActorPresentation => actorPresentation;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(displayName))
            {
                error = "Character definition requires stable ID and display name values.";
                return false;
            }

            if (life <= 0 || barrier <= 0)
            {
                error = "Character life and barrier must be positive.";
                return false;
            }

            if (weapon == null)
            {
                error = "Character definition requires a weapon definition.";
                return false;
            }

            if (!weapon.TryCreate(out _, out error))
            {
                return false;
            }

            if (entityPrefab == null)
            {
                error = "Character definition requires an entity prefab.";
                return false;
            }

            if (!entityPrefab.TryValidate(out error))
            {
                return false;
            }

            if (actorPresentation == null || actorPresentation.ActorKind != D0ActorKind.Player)
            {
                error = "Character definition requires a player actor presentation.";
                return false;
            }

            return actorPresentation.TryValidate(out error);
        }
    }
}
