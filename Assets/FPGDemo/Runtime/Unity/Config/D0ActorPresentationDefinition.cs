using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum D0ActorKind
    {
        [InspectorName("玩家")]
        Player = 0,

        [InspectorName("敌人")]
        Enemy = 1
    }

    public enum D0EnemyEffectSlot
    {
        [InspectorName("死亡特效层 F4")]
        DeathLayerF4 = 2,

        [InspectorName("死亡特效层 F3")]
        DeathLayerF3 = 3,

        [InspectorName("死亡特效层 F2")]
        DeathLayerF2 = 4,

        [InspectorName("死亡特效层 F1")]
        DeathLayerF1 = 5
    }

    [Serializable]
    public sealed class PlayerActorPresentationDefinition
    {
        [D0PlannerField("待机动画", "玩家未播放动作或状态反应时循环播放的动画。")]
        [SerializeField]
        private string idleAnimation = "b_idle";

        [D0PlannerField("受击动画", "玩家收到已提交受击事件时播放的状态反应动画。")]
        [SerializeField]
        private string hitAnimation = "hit";

        [D0PlannerField("护盾破裂动画", "玩家护盾破裂时播放的短暂硬直状态动画。")]
        [SerializeField]
        private string groggyAnimation = "groggy";

        [D0PlannerField("失败准备动画", "玩家进入失败结果前播放的准备动画。")]
        [SerializeField]
        private string defeatReadyAnimation = "death_ready";

        [D0PlannerField("失败动画", "玩家生命归零后播放的失败动画。")]
        [SerializeField]
        private string defeatAnimation = "death";

        [D0PlannerField("胜利准备动画", "敌人死亡后玩家进入胜利结果前播放的准备动画。")]
        [SerializeField]
        private string victoryReadyAnimation = "victory_ready";

        [D0PlannerField("胜利动画", "玩家完成胜利流程时循环播放的动画。")]
        [SerializeField]
        private string victoryAnimation = "victory";

        public string IdleAnimation => idleAnimation;
        public string HitAnimation => hitAnimation;
        public string GroggyAnimation => groggyAnimation;
        public string DefeatReadyAnimation => defeatReadyAnimation;
        public string DefeatAnimation => defeatAnimation;
        public string VictoryReadyAnimation => victoryReadyAnimation;
        public string VictoryAnimation => victoryAnimation;

        public bool TryValidate(out string error)
        {
            return TryValidateAnimationNames(
                out error,
                idleAnimation,
                hitAnimation,
                groggyAnimation,
                defeatReadyAnimation,
                defeatAnimation,
                victoryReadyAnimation,
                victoryAnimation);
        }

        private static bool TryValidateAnimationNames(
            out string error,
            params string[] animationNames)
        {
            for (int index = 0; index < animationNames.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(animationNames[index]))
                {
                    error =
                        $"Player state presentation animation {index} is missing.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class EnemyActorPresentationDefinition
    {
        [D0PlannerField("登场动画", "敌人实体进入战斗时播放的状态动画。")]
        [SerializeField]
        private string enterAnimation = "normal_enter";

        [D0PlannerField("待机动画", "敌人未播放攻击或状态反应时循环播放的动画。")]
        [SerializeField]
        private string idleAnimation = "normal_idle";

        [D0PlannerField("受击动画", "敌人收到已提交受击事件时播放的状态反应动画。")]
        [SerializeField]
        private string hitAnimation = "normal_hit";

        [D0PlannerField("破韧动画", "敌人进入 Break 或硬直状态时循环播放的动画。")]
        [SerializeField]
        private string groggyAnimation = "normal_groggy";

        [D0PlannerField("死亡动画", "敌人生命归零后播放的死亡状态动画。")]
        [SerializeField]
        private string deathAnimation = "normal_death";

        [D0PlannerField("破韧反馈时长", "弱点破韧碎片反馈持续时间，仅影响该敌人的状态表现。")]
        [SerializeField, Min(0.01f)]
        private float breakFeedbackDuration = 0.85f;

        public string EnterAnimation => enterAnimation;
        public string IdleAnimation => idleAnimation;
        public string HitAnimation => hitAnimation;
        public string GroggyAnimation => groggyAnimation;
        public string DeathAnimation => deathAnimation;
        public float BreakFeedbackDuration => breakFeedbackDuration;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(enterAnimation)
                || string.IsNullOrWhiteSpace(idleAnimation)
                || string.IsNullOrWhiteSpace(hitAnimation)
                || string.IsNullOrWhiteSpace(groggyAnimation)
                || string.IsNullOrWhiteSpace(deathAnimation))
            {
                error = "Enemy state presentation requires every state animation.";
                return false;
            }

            if (float.IsNaN(breakFeedbackDuration)
                || float.IsInfinity(breakFeedbackDuration)
                || breakFeedbackDuration < 0.01f)
            {
                error =
                    "Enemy Break feedback duration must be finite and at least 0.01 seconds.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class D0EnemyEffectPoolDefinition
    {
        [D0PlannerField("状态特效槽位", "敌人死亡状态使用的稳定分层特效槽位。")]
        [SerializeField]
        private D0EnemyEffectSlot slot;

        [D0PlannerField("状态特效预制体", "该状态槽位使用的 D0 表现预制体，不参与命中或伤害。")]
        [SerializeField]
        private GameObject visualPrefab;

        [D0PlannerField("预热实例数", "战斗启动前为该状态特效固定预热的实例数量。")]
        [SerializeField, Min(1)]
        private int prewarmCapacity = 1;

        [D0PlannerField("特效动画", "状态特效预制体播放的动画名称。")]
        [SerializeField]
        private string animationName = "animation";

        [D0PlannerField("播放时长", "状态特效归还固定池之前的持续秒数。")]
        [SerializeField, Min(0.01f)]
        private float duration = 1f;

        [D0PlannerField("排序偏移", "相对全局世界特效排序的局部层级偏移。")]
        [SerializeField]
        private int sortingOrderOffset;

        public D0EnemyEffectSlot Slot => slot;
        public GameObject VisualPrefab => visualPrefab;
        public int PrewarmCapacity => prewarmCapacity;
        public string AnimationName => animationName;
        public float Duration => duration;
        public int SortingOrderOffset => sortingOrderOffset;

        public bool TryValidate(out string error)
        {
            if (!Enum.IsDefined(typeof(D0EnemyEffectSlot), slot))
            {
                error = $"Enemy state effect slot '{slot}' is unsupported.";
                return false;
            }

            if (visualPrefab == null)
            {
                error = "Enemy state effect pool requires a visual prefab.";
                return false;
            }

            if (prewarmCapacity <= 0)
            {
                error =
                    "Enemy state effect pool prewarm capacity must be positive.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(animationName))
            {
                error = "Enemy state effect pool requires an animation name.";
                return false;
            }

            if (float.IsNaN(duration)
                || float.IsInfinity(duration)
                || duration < 0.01f)
            {
                error =
                    "Enemy state effect duration must be finite and at least 0.01 seconds.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class D0EnemyEffectPresentationDefinition
    {
        private static readonly D0EnemyEffectSlot[] RequiredSlots =
        {
            D0EnemyEffectSlot.DeathLayerF4,
            D0EnemyEffectSlot.DeathLayerF3,
            D0EnemyEffectSlot.DeathLayerF2,
            D0EnemyEffectSlot.DeathLayerF1
        };

        [D0PlannerField("死亡状态特效池", "敌人死亡状态使用的四层固定容量特效池。")]
        [SerializeField]
        private D0EnemyEffectPoolDefinition[] pools =
            Array.Empty<D0EnemyEffectPoolDefinition>();

        public int PoolCount => pools == null ? 0 : pools.Length;

        public bool TryGet(
            D0EnemyEffectSlot slot,
            out D0EnemyEffectPoolDefinition definition)
        {
            definition = null;
            if (pools == null)
            {
                return false;
            }

            for (int index = 0; index < pools.Length; index++)
            {
                D0EnemyEffectPoolDefinition candidate = pools[index];
                if (candidate != null && candidate.Slot == slot)
                {
                    definition = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (pools == null || pools.Length != RequiredSlots.Length)
            {
                error =
                    $"Enemy state presentation requires exactly {RequiredSlots.Length} death effect pools.";
                return false;
            }

            HashSet<D0EnemyEffectSlot> seen =
                new HashSet<D0EnemyEffectSlot>();
            for (int index = 0; index < pools.Length; index++)
            {
                D0EnemyEffectPoolDefinition pool = pools[index];
                if (pool == null || !pool.TryValidate(out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = $"Enemy state effect pool {index} is missing.";
                    }

                    return false;
                }

                if (!seen.Add(pool.Slot))
                {
                    error =
                        $"Enemy state effect slot '{pool.Slot}' must be unique.";
                    return false;
                }
            }

            for (int index = 0; index < RequiredSlots.Length; index++)
            {
                if (!seen.Contains(RequiredSlots[index]))
                {
                    error =
                        $"Enemy state presentation is missing '{RequiredSlots[index]}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Actor state presentation only. Entity structure and pose live on the
    /// Entity Prefab; weapon and enemy attack actions live on their definitions.
    /// </summary>
    [CreateAssetMenu(
        fileName = "D0ActorPresentationDefinition",
        menuName = "FPG Demo/Config/D0 Actor Presentation Definition")]
    public sealed class D0ActorPresentationDefinition : ScriptableObject
    {
        [D0PlannerField("表现角色 ID", "角色状态表现资产的稳定配置标识。")]
        [SerializeField]
        private string actorId = "fei";

        [D0PlannerField("显示名称", "供策划识别的角色状态表现名称。")]
        [SerializeField]
        private string displayName = "Fei";

        [D0PlannerField("表现角色类型", "决定该资产保存玩家还是敌人的状态表现。")]
        [SerializeField]
        private D0ActorKind actorKind;

        [TextArea]
        [D0PlannerField("策划说明", "记录状态动画或死亡特效调整意图，不参与战斗计算。")]
        [SerializeField]
        private string designerNotes;

        [D0PlannerField("玩家状态表现", "仅保存待机、受击、失败和胜利等玩家状态动画。")]
        [SerializeField]
        private PlayerActorPresentationDefinition player =
            new PlayerActorPresentationDefinition();

        [D0PlannerField("敌人状态表现", "仅保存登场、待机、受击、Break 和死亡等敌人状态动画。")]
        [SerializeField]
        private EnemyActorPresentationDefinition enemy =
            new EnemyActorPresentationDefinition();

        [D0PlannerField("敌人状态特效", "仅保存敌人死亡等状态特效，不保存具体攻击特效。")]
        [SerializeField]
        private D0EnemyEffectPresentationDefinition enemyEffects;

        public string ActorId => actorId;
        public string DisplayName => displayName;
        public D0ActorKind ActorKind => actorKind;
        public string DesignerNotes => designerNotes;

        public bool TryGetPlayer(
            out PlayerActorPresentationDefinition definition)
        {
            definition = actorKind == D0ActorKind.Player ? player : null;
            return definition != null;
        }

        public bool TryGetEnemy(
            out EnemyActorPresentationDefinition definition)
        {
            definition = actorKind == D0ActorKind.Enemy ? enemy : null;
            return definition != null;
        }

        public bool TryGetEnemyEffects(
            out D0EnemyEffectPresentationDefinition definition)
        {
            definition = actorKind == D0ActorKind.Enemy ? enemyEffects : null;
            return definition != null;
        }

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(actorId)
                || string.IsNullOrWhiteSpace(displayName))
            {
                error =
                    "Actor state presentation requires a stable ID and display name.";
                return false;
            }

            switch (actorKind)
            {
                case D0ActorKind.Player:
                    if (player == null)
                    {
                        error =
                            "Player actor presentation requires state animation data.";
                        return false;
                    }

                    return player.TryValidate(out error);

                case D0ActorKind.Enemy:
                    if (enemy == null || !enemy.TryValidate(out error))
                    {
                        if (string.IsNullOrEmpty(error))
                        {
                            error =
                                "Enemy actor presentation requires state animation data.";
                        }

                        return false;
                    }

                    if (enemyEffects != null
                        && !enemyEffects.TryValidate(out error))
                    {
                        error =
                            "Enemy actor state effects are invalid: " + error;
                        return false;
                    }

                    error = string.Empty;
                    return true;

                default:
                    error = "Actor presentation kind is invalid.";
                    return false;
            }
        }
    }
}
