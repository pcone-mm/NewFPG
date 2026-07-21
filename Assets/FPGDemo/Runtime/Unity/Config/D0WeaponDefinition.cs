using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Planner-owned Fei weapon data. This maps into the immutable domain
    /// WeaponDefinition at the BattleScenarioConfig boundary.
    /// </summary>
    [CreateAssetMenu(
        fileName = "D0WeaponDefinition",
        menuName = "FPG Demo/Config/D0 Weapon Definition")]
    public sealed class D0WeaponDefinition : ScriptableObject
    {
        [D0PlannerSection("基础信息")]
        [D0PlannerField("武器定义编号", "战斗记录与确定性定义使用的正整数编号。更换编号会改变定义身份，不应用于普通数值调参。")]
        [SerializeField, Min(1)]
        private int definitionId = 1;

        [D0PlannerField("武器 ID", "供配置和资源关联使用的稳定字符串标识。保持非空且稳定。")]
        [SerializeField]
        private string weaponId = "fei-primary-secondary";

        [D0PlannerField("显示名称", "供策划和编辑器识别的武器名称，不参与伤害或弹药计算。")]
        [SerializeField]
        private string displayName = "Fei";

        [TextArea]
        [D0PlannerField("策划说明", "记录武器定位、调参目的和验证备注；运行时不会读取此文本。")]
        [SerializeField]
        private string designerNotes;

        [D0PlannerSection("共享弹匣")]
        [D0PlannerField("弹匣容量", "主射与副射共用的弹药上限。副射不是瞄准模式，而是一种独立蓄力攻击；两者都会消耗这一个弹匣。")]
        [SerializeField, Min(1)]
        private int magazineCapacity = 8;

        [D0PlannerSection("主射：独立攻击")]
        [D0PlannerField("主射弹药消耗", "每次主射释放从共享弹匣扣除的弹药数。必须大于 0 且不超过弹匣容量。")]
        [SerializeField, Min(1)]
        private int primaryAmmoCost = 1;

        [D0PlannerField("主射射速（发/秒）", "按住左键时每秒最多释放的主射次数。Inspector 会按 60Hz 战斗时钟自动换算并取整，提交后显示的实际射速可能轻微变化。")]
        [SerializeField, Min(1)]
        private int primaryIntervalTicks = 39;

        [D0PlannerField("主射单颗弹丸生命伤害", "主射固定发射 8 颗弹丸；每颗命中普通部位时造成该基础生命伤害。弱点命中会再应用下方的弱点生命伤害倍率。")]
        [SerializeField, Min(0)]
        private int primaryDamage = 4;

        [D0PlannerField("主射单颗弹丸削韧伤害", "主射固定发射 8 颗弹丸；每颗命中时对敌人韧性条造成该基础削减值。弱点命中会再应用下方的弱点削韧倍率。")]
        [SerializeField, Min(0)]
        private int primaryBreakDamage = 4;

        [D0PlannerField("主射弱点生命倍率（万分比）", "主射命中弱点时的生命伤害倍率。10000 表示 1 倍，12000 表示 1.2 倍。")]
        [SerializeField, Min(0)]
        private int primaryWeakpointDamageMultiplierBasisPoints = 12000;

        [D0PlannerField("主射弱点削韧倍率（万分比）", "主射命中弱点时的削韧伤害倍率。10000 表示 1 倍，25000 表示 2.5 倍。")]
        [SerializeField, Min(0)]
        private int primaryWeakpointBreakMultiplierBasisPoints = 25000;

        [D0PlannerSection("副射：独立蓄力攻击")]
        [D0PlannerField("副射弹药消耗", "副射达到最低蓄力后松开并成功释放时，从共享弹匣扣除的弹药数。必须大于 0 且不超过弹匣容量；取消蓄力或未达到最低蓄力时不扣弹。")]
        [SerializeField, Min(1)]
        private int secondaryAmmoCost = 2;

        [D0PlannerField("副射最低蓄力（Tick）", "按住副射到可释放所需的最短时间。单位为 Tick；达到该时长后松开才会释放独立副射攻击。可设为 0，此时按下后在同一 Tick 松开即可成功释放。")]
        [SerializeField, Min(0)]
        private int secondaryMinimumChargeTicks = 0;

        [D0PlannerField("副射射速（发/秒）", "成功释放副射后每秒最多可再次释放的次数。Inspector 会按 60Hz 战斗时钟自动换算为恢复时间；恢复结束前主射、副射和换弹都不能开始。")]
        [SerializeField, Min(1)]
        private int secondaryRecoveryTicks = 30;

        [D0PlannerField("副射生命伤害", "副射命中普通部位时造成的基础生命伤害。弱点命中会再应用下方的弱点生命伤害倍率。")]
        [SerializeField, Min(0)]
        private int secondaryDamage = 28;

        [D0PlannerField("副射削韧伤害", "副射命中时对敌人韧性条造成的基础削减值。弱点命中会再应用下方的弱点削韧倍率。")]
        [SerializeField, Min(0)]
        private int secondaryBreakDamage = 20;

        [D0PlannerField("副射弱点生命倍率（万分比）", "副射命中弱点时的生命伤害倍率。10000 表示 1 倍，20000 表示 2 倍。")]
        [SerializeField, Min(0)]
        private int secondaryWeakpointDamageMultiplierBasisPoints = 12000;

        [D0PlannerField("副射弱点削韧倍率（万分比）", "副射命中弱点时的削韧伤害倍率。10000 表示 1 倍，25000 表示 2.5 倍。")]
        [SerializeField, Min(0)]
        private int secondaryWeakpointBreakMultiplierBasisPoints = 25000;

        [D0PlannerField("副射最大命中数", "一次副射最多结算的命中目标数。这是攻击结果上限，不代表当前 D0 场景已经支持多敌人或多波次。")]
        [SerializeField, Min(1)]
        private int secondaryMaxImpactCount = 4;

        [D0PlannerSection("换弹")]
        [D0PlannerField("换弹时长（Tick）", "从开始换弹到共享弹匣恢复的时长。单位为 Tick；当前 D0 默认时钟为 60 Tick/秒。")]
        [SerializeField, Min(1)]
        private int reloadDurationTicks = 84;


        [D0PlannerSection("技能表现与 Socket")]
        [D0PlannerField("主射表现", "主射技能独有的动画、枪口和弹道 VFX key；不参与射线、命中或伤害结算。")]
        [SerializeField]
        private D0WeaponShotPresentationDefinition primaryPresentation =
            D0WeaponShotPresentationDefinition.CreatePrimaryDefaults();

        [D0PlannerField("副射表现", "副射释放、蓄力、目标爆发和对应 Socket 的表现数据；战斗规则仍由本资产的数值字段决定。")]
        [SerializeField]
        private D0WeaponSecondaryPresentationDefinition secondaryPresentation =
            D0WeaponSecondaryPresentationDefinition.CreateDefaults();

        [D0PlannerField("换弹表现", "换弹 Spine 动画名称；只影响表现，不改变换弹 Tick。")]
        [SerializeField]
        private D0WeaponReloadPresentationDefinition reloadPresentation =
            new D0WeaponReloadPresentationDefinition();

        [D0PlannerField("瞄准指示器", "该武器的常态、射击与命中 UI 表现，不参与输入、射线或伤害。")]
        [SerializeField]
        private PlayerAimIndicatorPresentationDefinition aimIndicator =
            new PlayerAimIndicatorPresentationDefinition();

        public int DefinitionId => definitionId;
        public string WeaponId => weaponId;
        public string DisplayName => displayName;
        public int MagazineCapacity => magazineCapacity;
        public int PrimaryIntervalTicks => primaryIntervalTicks;
        public int SecondaryMinimumChargeTicks => secondaryMinimumChargeTicks;
        public int SecondaryAmmoCost => secondaryAmmoCost;
        public int ReloadDurationTicks => reloadDurationTicks;


        public D0WeaponShotPresentationDefinition PrimaryPresentation =>
            primaryPresentation;

        public D0WeaponSecondaryPresentationDefinition SecondaryPresentation =>
            secondaryPresentation;

        public D0WeaponReloadPresentationDefinition ReloadPresentation =>
            reloadPresentation;

        public PlayerAimIndicatorPresentationDefinition AimIndicator =>
            aimIndicator;

        public bool TryValidatePresentation(out string error)
        {
            error = string.Empty;
            if (primaryPresentation == null
                || !primaryPresentation.TryValidate(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Weapon primary presentation is missing.";
                }
                else
                {
                    error = "Weapon primary presentation is invalid: " + error;
                }

                return false;
            }

            if (secondaryPresentation == null
                || !secondaryPresentation.TryValidate(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Weapon secondary presentation is missing.";
                }
                else
                {
                    error = "Weapon secondary presentation is invalid: " + error;
                }

                return false;
            }

            if (reloadPresentation == null
                || !reloadPresentation.TryValidate(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Weapon reload presentation is missing.";
                }
                else
                {
                    error = "Weapon reload presentation is invalid: " + error;
                }

                return false;
            }

            if (aimIndicator == null || !aimIndicator.TryValidate(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Weapon aim-indicator presentation is missing.";
                }
                else
                {
                    error =
                        "Weapon aim-indicator presentation is invalid: " + error;
                }

                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryCreate(out WeaponDefinition definition, out string error)
        {
            definition = default(WeaponDefinition);
            if (string.IsNullOrWhiteSpace(weaponId) || string.IsNullOrWhiteSpace(displayName))
            {
                error = "Weapon definition requires stable ID and display name values.";
                return false;
            }

            if (!TryValidatePresentation(out error))
            {
                return false;
            }

            try
            {
                definition = new WeaponDefinition(
                    definitionId,
                    magazineCapacity,
                    primaryAmmoCost,
                    new TickDuration(primaryIntervalTicks),
                    new DamageSpec(
                        primaryDamage,
                        primaryBreakDamage,
                        primaryWeakpointDamageMultiplierBasisPoints,
                        primaryWeakpointBreakMultiplierBasisPoints),
                    secondaryAmmoCost,
                    new TickDuration(secondaryMinimumChargeTicks),
                    new TickDuration(secondaryRecoveryTicks),
                    new DamageSpec(
                        secondaryDamage,
                        secondaryBreakDamage,
                        secondaryWeakpointDamageMultiplierBasisPoints,
                        secondaryWeakpointBreakMultiplierBasisPoints),
                    new TickDuration(reloadDurationTicks),
                    secondaryMaxImpactCount);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
