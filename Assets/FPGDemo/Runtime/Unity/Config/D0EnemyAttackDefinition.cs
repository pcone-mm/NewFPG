using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// The three readable D0 enemy attack languages. This is presentation and
    /// encounter authoring data; the combat domain still receives the resulting
    /// immutable threat payload.
    /// </summary>
    public enum D0EnemyAttackLanguage
    {
        [InspectorName("快速攻击")]
        FastAttack = 0,
        [InspectorName("可拦截弹幕")]
        InterceptableVolley = 1,
        [InspectorName("重型弱点攻击")]
        HeavyWeakpointBreak = 2
    }

    public enum D0AttackRecoveryRule
    {
        [InspectorName("恢复后继续巡逻")]
        ResumePatrolAfterRecovery = 0,
        [InspectorName("停在攻击位置")]
        HoldAtAttackPosition = 1
    }

    public enum D0AnimationMotionStartPhase
    {
        [InspectorName("前摇开始")]
        Windup = 0,
        [InspectorName("释放时")]
        Release = 1
    }

    /// <summary>
    /// Reusable enemy move definition. A single attack asset owns its animation,
    /// warning, timing, payload, hit/break outcome and presentation slots; an
    /// encounter only decides when that asset is scheduled.
    /// </summary>
    [CreateAssetMenu(
        fileName = "D0EnemyAttackDefinition",
        menuName = "FPG Demo/Config/D0 Enemy Attack Definition")]
    public sealed class D0EnemyAttackDefinition : ScriptableObject
    {
        [D0PlannerSection("攻击标识与设计记录")]
        [D0PlannerField("攻击配置 ID", "供遭遇时间表引用、校验和日志定位的稳定标识。创建后保持非空且稳定，不是伤害数值。")]
        [SerializeField]
        private string attackId = "burstbug-fast";

        [D0PlannerField("显示名称", "供策划和验证日志识别的攻击名称，不直接参与伤害或命中计算。")]
        [SerializeField]
        private string displayName = "Burstbug Fast Attack";

        [TextArea]
        [D0PlannerField("策划说明", "记录攻击目的、调参意图与验证备注；运行时不会读取此文本。")]
        [SerializeField]
        private string designerNotes;

        [D0PlannerField("攻击定义 ID", "进入战斗运行时的正整数攻击身份标识。必须与同一遭遇中的其他攻击不同，用于重放、追踪和表现路由。")]
        [SerializeField, Min(1)]
        private int definitionId = 201;

        [D0PlannerField("攻击语义标签", "标准战斗验证器据此锁定快攻、可拦截弹幕或重型弱点／Break 三种攻击语言，并校验其载荷、动画、预警、特效和音频路由。")]
        [SerializeField]
        private D0EnemyAttackLanguage attackLanguage = D0EnemyAttackLanguage.FastAttack;

        [D0PlannerSection("攻击预警")]
        [D0PlannerField("预警标识", "攻击语言的预警合同名。配置验证器将其与攻击表现键和预警路由一并校验，实际预警由表现键路由创建。")]
        [SerializeField]
        private string warningSlot = "enemy-source";

        [D0PlannerSection("攻击时序")]
        [D0PlannerField("预警时长（Tick）", "攻击启动后进入预警状态的时长。单位为 Tick；D0 默认每秒 60 Tick，预警结束后进入前摇。")]
        [SerializeField, Min(0)]
        private int telegraphTicks = 24;

        [D0PlannerField("前摇时长（Tick）", "预警结束到攻击释放之间的时长。单位为 Tick；0 表示预警结束即可释放。")]
        [SerializeField, Min(0)]
        private int windupTicks = 12;

        [D0PlannerField("后摇时长（Tick）", "攻击释放完成后保持恢复状态的时长。单位为 Tick。")]
        [SerializeField, Min(0)]
        private int recoveryTicks = 30;

        [D0PlannerField("攻击结束后行为规则", "威胁完成后的行为合同。D0 Burstbug 控制器在后摇结束后读取该规则；标准战斗要求所有招式恢复原方向巡逻。")]
        [SerializeField]
        private D0AttackRecoveryRule recoveryRule = D0AttackRecoveryRule.ResumePatrolAfterRecovery;

        [D0PlannerSection("攻击载荷与命中结果")]
        [D0PlannerField("攻击载荷类型", "选择 SweptProjectile（飞行投射物）或 TimedImpact（延时命中）。只填写所选类型对应的参数；另一个分组不会参与本次攻击。")]
        [SerializeField]
        private ThreatPayloadKind payloadKind = ThreatPayloadKind.SweptProjectile;

        [D0PlannerField("单次载荷数量", "一次攻击创建的同类型载荷数量。仅飞行投射物可大于 1；延时命中必须为 1。")]
        [SerializeField, Min(1)]
        private int payloadCount = 1;

        [D0PlannerSection("飞行投射物（仅 SweptProjectile 生效）")]
        [D0PlannerField("投射物定义 ID", "进入战斗运行时的投射物身份标识。仅飞行投射物生效；用于重放、追踪和表现路由。")]
        [SerializeField, Min(1)]
        private int projectileDefinitionId = 301;

        [D0PlannerField("投射物飞行时长（Tick）", "投射物从生成到抵达预定命中时刻的时长。单位为 Tick，必须大于 0。")]
        [SerializeField, Min(1)]
        private int projectileFlightTicks = 36;

        [D0PlannerField("投射物最大存活时长（Tick）", "投射物生成后的最长存活时间。单位为 Tick，且必须不小于飞行时长。")]
        [SerializeField, Min(1)]
        private int projectileExpireTicks = 51;

        [D0PlannerField("投射物基础伤害", "投射物命中玩家时的基础伤害。玩家收回护盾时扣护盾，暴露时扣生命；仅飞行投射物生效。")]
        [SerializeField, Min(0)]
        private int projectileDamage = 28;

        [D0PlannerField("投射物韧性伤害（当前不生效）", "该值会写入伤害合同，但当前 D0 玩家受击不结算韧性伤害。保留它仅为兼容数据，不应把它当作可感知调参。")]
        [SerializeField, Min(0)]
        private int projectileBreakDamage;

        [D0PlannerField("可拦截投射物耐久", "玩家拦截该投射物前需要造成的耐久伤害。仅飞行投射物生效；启用“可被玩家拦截”时必须大于 0。")]
        [SerializeField, Min(0)]
        private int projectileHitPoints;

        [D0PlannerField("可被玩家拦截", "启用后，玩家攻击可对该飞行投射物结算拦截伤害；关闭后该投射物不能被拦截。")]
        [SerializeField]
        private bool projectileInterceptable;

        [D0PlannerTechnicalField("投射物预算占用与战斗会话的固定技术容量绑定；改动必须由程序配合容量和峰值测试评估，不作为常规策划调参。")]
        [SerializeField, Min(1)]
        private int projectileBudgetUnits = 1;

        [D0PlannerSection("表现与启动策略")]
        [D0PlannerField("动画位移", "可选的美术动画位移；启用后会与程序位移叠加。")]
        [SerializeField]
        private D0AnimationMotionSettings animationMotion =
            new(false, string.Empty, "gameplay_motion", true);

        [D0PlannerField("动画位移开始阶段", "选择从攻击前摇开始或攻击释放时开始采样动画位移。")]
        [SerializeField]
        private D0AnimationMotionStartPhase animationMotionStartPhase =
            D0AnimationMotionStartPhase.Windup;

        [D0PlannerField("攻击表现键", "当前 D0 威胁路由只识别 1（快速不可拦截）、2（可拦截弹幕）、3（重型弱点攻击）。必须与载荷类型和可拦截性一起校验；其他值会被配置验证拒绝。")]
        [SerializeField, Min(1)]
        private int presentationKey = 1;

        [D0PlannerTechnicalField("投射物碰撞半径键会参与物理扫掠半径计算，属于命中实现边界，不向普通策划开放。")]
        [SerializeField, Min(1)]
        private int sweepRadiusKey = 250;

        [D0PlannerSection("延时命中（仅 TimedImpact 生效）")]
        [D0PlannerField("延时命中基础伤害", "攻击释放后延时结算到玩家的基础伤害。玩家收回护盾时扣护盾，暴露时扣生命；仅延时命中生效。")]
        [SerializeField, Min(0)]
        private int timedImpactDamage;

        [D0PlannerField("延时命中韧性伤害（当前不生效）", "该值会写入伤害合同，但当前 D0 玩家受击不结算韧性伤害。保留它仅为兼容数据，不应把它当作可感知调参。")]
        [SerializeField, Min(0)]
        private int timedImpactBreakDamage;

        [D0PlannerField("延时命中目标", "延时命中锁定的目标策略。当前实现仅支持 PlayerCombatant（玩家角色），不要把它当作多目标配置。")]
        [SerializeField]
        private ThreatTargetPolicy timedImpactTargetPolicy = ThreatTargetPolicy.PlayerCombatant;

        [D0PlannerField("延时命中延迟（Tick）", "从攻击释放到延时命中结算的延迟。单位为 Tick；仅延时命中生效。")]
        [SerializeField, Min(0)]
        private int timedImpactDelayTicks;

        [D0PlannerField("启动失败重试策略", "攻击因敌人硬直、预算不足或容量不足而无法启动时的处理方式。当前仅支持 HoldPendingNextTick：下个 Tick 保持待命并重试。")]
        [SerializeField]
        private ThreatRetryPolicy retryPolicy = ThreatRetryPolicy.HoldPendingNextTick;

        [D0PlannerSection("Attack-owned presentation")]
        [D0PlannerField("Attack presentation contract", "Animation, visual socket, VFX pool and release timing belong to this attack asset.")]
        [SerializeField]
        private D0EnemyAttackPresentationDefinition presentation =
            D0EnemyAttackPresentationDefinition.CreateDefaults();

        public string AttackId => attackId;
        public string DisplayName => displayName;
        public string DesignerNotes => designerNotes;
        public int DefinitionId => definitionId;
        public D0EnemyAttackLanguage AttackLanguage => attackLanguage;
        public string WarningSlot => warningSlot;
        public int TelegraphTicks => telegraphTicks;
        public int WindupTicks => windupTicks;
        public int RecoveryTicks => recoveryTicks;
        public D0AttackRecoveryRule RecoveryRule => recoveryRule;
        public D0AnimationMotionSettings AnimationMotion => animationMotion;
        public D0AnimationMotionStartPhase AnimationMotionStartPhase => animationMotionStartPhase;
        public ThreatPayloadKind PayloadKind => payloadKind;
        public int PayloadCount => payloadCount;
        public int PresentationKey => presentationKey;
        public bool ProjectileInterceptable => projectileInterceptable;
        public D0EnemyAttackPresentationDefinition Presentation => presentation;
        public string SocketId => presentation == null ? string.Empty : presentation.SocketId;
        public string AttackSocketId => SocketId;
        public string AttackAnimation => presentation == null ? string.Empty : presentation.AnimationName;
        public string AnimationName => AttackAnimation;
        public string ReleaseAnimationName => presentation == null
            ? string.Empty
            : presentation.ReleaseAnimationName;
        public string EffectiveVisualEffectKey => presentation == null
            ? string.Empty
            : presentation.VisualEffectKey;
        public GameObject VisualEffectPrefab => presentation == null
            ? null
            : presentation.VisualEffectPrefab;
        public int VfxPrewarmCapacity => presentation == null
            ? 0
            : presentation.PrewarmCapacity;
        public float VfxDuration => presentation == null
            ? 0f
            : presentation.EffectDuration;
        public int VfxSortingOrderOffset => presentation == null
            ? 0
            : presentation.SortingOrderOffset;
        public CombatAudioCue AudioCue => presentation == null
            ? CombatAudioCue.None
            : presentation.AudioCue;
        public string VisualEffectKey => EffectiveVisualEffectKey;
        public int ReleaseMarkerTicks => presentation == null
            ? 0
            : presentation.ReleaseMarkerTicks;

        public bool TryValidatePresentation(out string error)
        {
            if (presentation == null)
            {
                error = "Enemy attack requires an explicit presentation definition.";
                return false;
            }

            if (!presentation.TryValidate(out error))
            {
                error = "Enemy attack presentation is invalid: " + error;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryCreateScheduleEntry(
            long scheduleSequence,
            int dueTick,
            out ThreatScheduleEntry entry,
            out string error)
        {
            entry = default(ThreatScheduleEntry);
            if (!TryValidate(out error))
            {
                return false;
            }

            if (scheduleSequence <= 0L || dueTick < 0)
            {
                error = "Encounter attack schedule sequence must be positive and due tick must be non-negative.";
                return false;
            }

            try
            {
                ThreatPayloadDefinition payload;
                switch (payloadKind)
                {
                    case ThreatPayloadKind.SweptProjectile:
                    {
                        ProjectileDefinition projectile = new ProjectileDefinition(
                            projectileDefinitionId,
                            new TickDuration(projectileFlightTicks),
                            new TickDuration(projectileExpireTicks),
                            new DamageSpec(projectileDamage, projectileBreakDamage),
                            projectileHitPoints,
                            projectileInterceptable,
                            projectileBudgetUnits,
                            presentationKey,
                            sweepRadiusKey);
                        payload = ThreatPayloadDefinition.SweptProjectile(projectile, payloadCount);
                        break;
                    }

                    case ThreatPayloadKind.TimedImpact:
                        if (payloadCount != 1)
                        {
                            error = "Timed-impact D0 attacks must use exactly one payload.";
                            return false;
                        }

                        payload = ThreatPayloadDefinition.TimedImpact(
                            new DamageSpec(timedImpactDamage, timedImpactBreakDamage),
                            timedImpactTargetPolicy,
                            new TickDuration(timedImpactDelayTicks),
                            presentationKey);
                        break;

                    default:
                        error = "D0 attack payload kind is invalid.";
                        return false;
                }

                entry = new ThreatScheduleEntry(
                    scheduleSequence,
                    new TickIndex(dueTick),
                    definitionId,
                    new TickDuration(telegraphTicks),
                    new TickDuration(windupTicks),
                    new TickDuration(recoveryTicks),
                    payload,
                    retryPolicy);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(attackId)
                || string.IsNullOrWhiteSpace(displayName)
                || string.IsNullOrWhiteSpace(warningSlot)
                || definitionId <= 0
                || payloadCount <= 0
                || telegraphTicks < 0
                || windupTicks < 0
                || recoveryTicks < 0)
            {
                error = "D0 enemy attack requires identity, presentation slots and non-negative timing values.";
                return false;
            }

            if (!TryValidatePresentation(out error))
            {
                return false;
            }

            if (!Enum.IsDefined(typeof(D0AnimationMotionStartPhase), animationMotionStartPhase))
            {
                error = "D0 enemy attack animation motion start phase is invalid.";
                return false;
            }

            if (!animationMotion.TryValidate(out error))
            {
                error = "D0 enemy attack animation motion is invalid: " + error;
                return false;
            }

            if (!D0ThreatPresentationRouting.TryGetKind(presentationKey, out _))
            {
                error = "D0 attack presentation key must be 1 (fast), 2 (interceptable volley), or 3 (heavy weakpoint).";
                return false;
            }

            if (attackLanguage != D0EnemyAttackLanguage.FastAttack
                && attackLanguage != D0EnemyAttackLanguage.InterceptableVolley
                && attackLanguage != D0EnemyAttackLanguage.HeavyWeakpointBreak)
            {
                error = "D0 enemy attack language is invalid.";
                return false;
            }

            if ((recoveryRule != D0AttackRecoveryRule.ResumePatrolAfterRecovery
                    && recoveryRule != D0AttackRecoveryRule.HoldAtAttackPosition)
                || retryPolicy != ThreatRetryPolicy.HoldPendingNextTick
                || timedImpactTargetPolicy != ThreatTargetPolicy.PlayerCombatant)
            {
                error = "D0 enemy attack contains an unsupported recovery, retry or target policy.";
                return false;
            }

            switch (payloadKind)
            {
                case ThreatPayloadKind.SweptProjectile:
                    if (projectileDefinitionId <= 0
                        || projectileFlightTicks <= 0
                        || projectileExpireTicks < projectileFlightTicks
                        || projectileDamage < 0
                        || projectileBreakDamage < 0
                        || projectileHitPoints < 0
                        || projectileBudgetUnits <= 0
                        || presentationKey <= 0
                        || sweepRadiusKey <= 0
                        || (projectileInterceptable && projectileHitPoints <= 0))
                    {
                        error = "D0 projectile attack payload contains invalid flight, hit, break or presentation values.";
                        return false;
                    }

                    break;

                case ThreatPayloadKind.TimedImpact:
                    if (payloadCount != 1
                        || timedImpactDamage < 0
                        || timedImpactBreakDamage < 0
                        || timedImpactDelayTicks < 0
                        || presentationKey <= 0)
                    {
                        error = "D0 timed-impact attack payload contains invalid hit, break or presentation values.";
                        return false;
                    }

                    break;

                default:
                    error = "D0 attack payload kind is invalid.";
                    return false;
            }

            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// The encounter-owned timing record for a reusable D0 attack definition.
    /// It contains no duplicated payload or animation data.
    /// </summary>
    [Serializable]
    public struct D0EncounterAttackScheduleEntry
    {
        [D0PlannerField("时序编号", "同一遭遇时间表中的稳定正整数编号。同一触发 Tick 的攻击按它确定先后；保持唯一，便于重放和日志定位。")]
        [SerializeField, Min(1)]
        private long scheduleSequence;

        [D0PlannerField("触发时刻（Tick）", "敌人从该 Tick 起尝试启动此攻击。单位为 Tick；当前 D0 默认每秒 60 Tick。")]
        [SerializeField, Min(0)]
        private int dueTick;

        [D0PlannerField("攻击配置", "本条时序引用的可复用敌人攻击资产。伤害、载荷、预警、后摇和表现键均在该攻击资产内配置。")]
        [SerializeField]
        private D0EnemyAttackDefinition attack;

        public long ScheduleSequence => scheduleSequence;
        public int DueTick => dueTick;
        public D0EnemyAttackDefinition Attack => attack;

        public bool TryCreate(out ThreatScheduleEntry entry, out string error)
        {
            entry = default(ThreatScheduleEntry);
            if (attack == null)
            {
                error = "Encounter attack schedule requires a reusable D0 enemy attack definition.";
                return false;
            }

            return attack.TryCreateScheduleEntry(scheduleSequence, dueTick, out entry, out error);
        }

        public bool IsActiveAt(long tick)
        {
            if (attack == null || tick < dueTick)
            {
                return false;
            }

            long duration = (long)attack.TelegraphTicks
                + attack.WindupTicks
                + attack.RecoveryTicks;
            return tick < dueTick + duration;
        }
    }
}
