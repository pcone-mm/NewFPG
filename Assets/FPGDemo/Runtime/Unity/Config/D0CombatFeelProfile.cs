using System;
using FPG.Demo.Core;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Planner-tunable combat feel values. Physics layer masks and query buffer
    /// capacities intentionally remain in BattleScenarioConfig as technical data.
    /// </summary>
    [CreateAssetMenu(
        fileName = "D0CombatFeelProfile",
        menuName = "FPG Demo/Config/D0 Combat Feel Profile")]
    public sealed class D0CombatFeelProfile : ScriptableObject
    {
        [D0PlannerSection("基础信息")]
        [D0PlannerField("手感配置 ID", "战斗手感资产的稳定标识，用于配置校验。保持非空且稳定。")]
        [SerializeField]
        private string feelProfileId = "fei-combatlab";

        [TextArea]
        [D0PlannerField("策划说明", "记录手感目标、调参理由和验证备注；运行时不会读取此文本。")]
        [SerializeField]
        private string designerNotes;

        [D0PlannerSection("瞄准与射击手感")]
        [D0PlannerField("攻击查询最远距离（世界单位）", "主射与副射共同使用的权威弹道查询距离。修改后需要通过射击手感工作台重建战斗；它不等同于相机裁剪距离。")]
        [SerializeField, Min(0.01f)]
        private float maximumAimDistance = 50f;

        [D0PlannerField("主射基础散布（正切值）", "主射弹道散布角的正切值。0 表示无散布；数值越大，弹道离开准星的范围越大。")]
        [SerializeField, Range(0f, 0.5f)]
        private float primaryBaseSpreadTangent = 0.04f;

        [D0PlannerField("副射范围半径", "副射射线只确定首表面爆心，不产生直击伤害；随后以该半径做范围查询。单位为 Unity 世界单位，不等同于特效缩放。")]
        [SerializeField, Min(0.01f)]
        private float secondaryAreaRadius = 3f;

        [D0PlannerSection("掩体护盾与节奏")]
        [D0PlannerField("完美回撤窗口（Tick）", "玩家收回掩体护盾后，可按完美回撤规则承伤的时间窗口。单位为 Tick；当前 D0 默认时钟为 60 Tick/秒。")]
        [SerializeField, Min(1)]
        private int perfectRetractWindowTicks = 6;

        [D0PlannerField("完美回撤承伤倍率（万分比）", "完美回撤窗口内，护盾实际承受的伤害倍率。10000 表示原伤害，2500 表示承受 25% 伤害。")]
        [SerializeField, Min(0)]
        private int perfectRetractMultiplierBasisPoints = 2500;

        [D0PlannerField("敌人瘫痪时长（Tick）", "敌人韧性归零后保持瘫痪状态的时长。单位为 Tick；当前 D0 默认时钟为 60 Tick/秒。")]
        [SerializeField, Min(1)]
        private int enemyGroggyDurationTicks = 60;

        public string FeelProfileId => feelProfileId;
        public float MaximumAimDistance => maximumAimDistance;
        public float PrimaryBaseSpreadTangent => primaryBaseSpreadTangent;
        public float SecondaryAreaRadius => secondaryAreaRadius;
        public TickDuration PerfectRetractWindow => new TickDuration(perfectRetractWindowTicks);
        public int PerfectRetractMultiplierBasisPoints => perfectRetractMultiplierBasisPoints;
        public TickDuration EnemyGroggyDuration => new TickDuration(enemyGroggyDurationTicks);

        public bool TryCreateAttackQuerySettings(
            UnityAttackQuerySettings technicalSettings,
            out UnityAttackQuerySettings settings,
            out string error)
        {
            settings = default(UnityAttackQuerySettings);
            if (!TryValidate(out error))
            {
                return false;
            }

            if (!technicalSettings.IsValid)
            {
                error = "Technical attack-query layer configuration is invalid.";
                return false;
            }

            try
            {
                settings = new UnityAttackQuerySettings(
                    maximumAimDistance,
                    primaryBaseSpreadTangent,
                    secondaryAreaRadius,
                    technicalSettings.HitboxLayerMask,
                    technicalSettings.BlockerLayerMask);
                error = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TryCreateAttackQuerySettings(
            D0ThreeCProfile threeCProfile,
            UnityAttackQuerySettings technicalSettings,
            out UnityAttackQuerySettings settings,
            out string error)
        {
            settings = default(UnityAttackQuerySettings);
            if (threeCProfile == null)
            {
                error = "Formal attack-query composition requires a 3C profile.";
                return false;
            }

            if (!threeCProfile.TryValidate(out error) || !TryValidate(out error))
            {
                return false;
            }

            if (!technicalSettings.IsValid)
            {
                error = "Technical attack-query layer configuration is invalid.";
                return false;
            }

            try
            {
                settings = new UnityAttackQuerySettings(
                    maximumAimDistance,
                    primaryBaseSpreadTangent,
                    secondaryAreaRadius,
                    technicalSettings.HitboxLayerMask,
                    technicalSettings.BlockerLayerMask);
                error = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TryCreateAttackQuerySettings(
            D0ThreeCProfile threeCProfile,
            UnityAttackQueryTechnicalSettings technicalSettings,
            out UnityAttackQuerySettings settings,
            out string error)
        {
            settings = default(UnityAttackQuerySettings);
            if (threeCProfile == null)
            {
                error = "Formal attack-query composition requires a 3C profile.";
                return false;
            }

            if (!threeCProfile.TryValidate(out error) || !TryValidate(out error))
            {
                return false;
            }

            if (!technicalSettings.IsValid)
            {
                error = "Technical attack-query layer configuration is invalid.";
                return false;
            }

            try
            {
                settings = new UnityAttackQuerySettings(
                    maximumAimDistance,
                    primaryBaseSpreadTangent,
                    secondaryAreaRadius,
                    technicalSettings.HitboxLayerMask,
                    technicalSettings.BlockerLayerMask);
                error = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(feelProfileId))
            {
                error = "Combat-feel profile requires a stable ID.";
                return false;
            }

            if (!IsFinitePositive(maximumAimDistance)
                || !IsFiniteNonNegative(primaryBaseSpreadTangent)
                || !IsFinitePositive(secondaryAreaRadius)
                || perfectRetractWindowTicks <= 0
                || perfectRetractMultiplierBasisPoints < 0
                || enemyGroggyDurationTicks <= 0)
            {
                error = "Combat-feel profile contains invalid tuning values.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
