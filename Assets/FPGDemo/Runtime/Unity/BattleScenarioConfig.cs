using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [Serializable]
    public struct ThreatScheduleEntryAuthoring
    {
        [D0PlannerSection("通用出招节奏")]
        [D0PlannerField("时序编号", "本时间表中的稳定正整数编号；同一触发 Tick 的攻击按它确定先后。保持唯一，避免重放和日志难以定位。")]
        [SerializeField]
        private long scheduleSequence;

        [D0PlannerField("触发时刻（Tick）", "敌人从该 Tick 起尝试启动攻击。单位为 Tick；当前 D0 默认时钟为 60 Tick/秒。")]
        [SerializeField, Min(0)]
        private int dueTick;

        [D0PlannerField("攻击定义 ID", "运行时攻击身份标识。用于追踪和确定性定义；新建攻击时保持正数且与当前设计文档一致。")]
        [SerializeField, Min(1)]
        private int definitionId;

        [D0PlannerField("预警时长（Tick）", "攻击启动后的预警状态时长。单位为 Tick；预警结束后才进入前摇。")]
        [SerializeField, Min(0)]
        private int telegraphTicks;

        [D0PlannerField("前摇时长（Tick）", "预警结束到攻击释放之间的前摇时长。单位为 Tick；0 表示预警结束即可释放。")]
        [SerializeField, Min(0)]
        private int windupTicks;

        [D0PlannerField("后摇时长（Tick）", "攻击载荷创建完成后，敌人保持恢复状态的时长。单位为 Tick。")]
        [SerializeField, Min(0)]
        private int recoveryTicks;

        [D0PlannerField("攻击载荷类型", "选择 SweptProjectile（飞行投射物）或 TimedImpact（延时命中）。只填写所选类型对应的参数；另一个分组的参数不会参与本次攻击。")]
        [SerializeField]
        private ThreatPayloadKind payloadKind;

        [D0PlannerSection("飞行投射物（仅 SweptProjectile 生效）")]
        [D0PlannerField("单次投射物数量", "一次攻击释放的同类型投射物数量。仅飞行投射物生效；延时命中固定只能为 1。")]
        [SerializeField, Min(1)]
        private int payloadCount;

        [D0PlannerField("投射物定义 ID", "运行时投射物身份标识。仅飞行投射物生效；用于重放、追踪和表现路由，不是普通伤害数值。")]
        [SerializeField, Min(1)]
        private int projectileDefinitionId;

        [D0PlannerField("投射物飞行时长（Tick）", "投射物从生成到抵达预定命中时刻的时长。仅飞行投射物生效，单位为 Tick。")]
        [SerializeField, Min(1)]
        private int projectileFlightTicks;

        [D0PlannerField("投射物最大存活时长（Tick）", "投射物生成后的最长存活时间。仅飞行投射物生效，单位为 Tick，且必须不小于飞行时长。")]
        [SerializeField, Min(1)]
        private int projectileExpireTicks;

        [D0PlannerField("投射物基础伤害", "飞行投射物命中玩家时使用的基础伤害。玩家收回护盾时扣护盾，暴露时扣生命；仅飞行投射物生效。")]
        [SerializeField, Min(0)]
        private int projectileDamage;

        [D0PlannerField("投射物韧性伤害（当前不生效）", "该值会写入伤害合同，但当前 D0 玩家受击不结算韧性伤害。保留它仅为兼容数据，不应把它当作可感知调参。")]
        [SerializeField, Min(0)]
        private int projectileBreakDamage;

        [D0PlannerField("可拦截投射物耐久", "玩家拦截该投射物前需要造成的耐久伤害。仅飞行投射物生效；当“可被玩家拦截”启用时应大于 0。")]
        [SerializeField, Min(0)]
        private int projectileHitPoints;

        [D0PlannerField("可被玩家拦截", "启用后，玩家攻击可对该飞行投射物结算拦截伤害；关闭后该投射物不能被拦截。")]
        [SerializeField]
        private bool projectileInterceptable;

        [D0PlannerTechnicalField("投射物预算占用与 BattleScenarioConfig 的固定技术容量绑定；改动必须由程序配合容量测试评估，不作为常规策划调参。")]
        [SerializeField, Min(1)]
        private int projectileBudgetUnits;

        [D0PlannerSection("表现与启动策略")]
        [D0PlannerField("攻击表现键", "当前 D0 威胁路由只识别 1（快速不可拦截）、2（可拦截弹幕）、3（重型弱点攻击）。必须与载荷类型和可拦截性一起校验；其他值会被配置验证拒绝。")]
        [SerializeField, Min(1)]
        private int presentationKey;

        [D0PlannerTechnicalField("投射物碰撞半径键会参与物理扫掠半径计算，属于命中实现边界，不向普通策划开放。")]
        [SerializeField, Min(1)]
        private int sweepRadiusKey;

        [D0PlannerSection("延时命中（仅 TimedImpact 生效）")]
        [D0PlannerField("延时命中基础伤害", "攻击释放后延时结算到玩家的基础伤害。玩家收回护盾时扣护盾，暴露时扣生命；仅延时命中生效。")]
        [SerializeField, Min(0)]
        private int timedImpactDamage;

        [D0PlannerField("延时命中韧性伤害（当前不生效）", "该值会写入伤害合同，但当前 D0 玩家受击不结算韧性伤害。保留它仅为兼容数据，不应把它当作可感知调参。")]
        [SerializeField, Min(0)]
        private int timedImpactBreakDamage;

        [D0PlannerField("延时命中目标", "延时命中锁定的目标策略。当前实现仅支持 PlayerCombatant（玩家角色），不要把它当作多目标配置。")]
        [SerializeField]
        private ThreatTargetPolicy timedImpactTargetPolicy;

        [D0PlannerField("延时命中延迟（Tick）", "从攻击释放到延时命中结算的延迟。仅 TimedImpact 生效，单位为 Tick。")]
        [SerializeField, Min(0)]
        private int timedImpactDelayTicks;

        [D0PlannerField("启动失败重试策略", "攻击因敌人硬直、预算不足或容量不足而无法启动时的处理方式。当前仅支持 HoldPendingNextTick：下个 Tick 保持待命并重试。")]
        [SerializeField]
        private ThreatRetryPolicy retryPolicy;

        public bool TryCreate(out ThreatScheduleEntry entry, out string error)
        {
            entry = default(ThreatScheduleEntry);
            if (scheduleSequence <= 0L || dueTick < 0 || definitionId <= 0)
            {
                error = "Threat schedule identity values must be positive and dueTick must be non-negative.";
                return false;
            }

            if (!D0ThreatPresentationRouting.TryGetKind(presentationKey, out _))
            {
                error = "D0 threat presentation key must be 1 (fast), 2 (interceptable volley), or 3 (heavy weakpoint).";
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
                            error = "Timed-impact threat schedules must use exactly one payload.";
                            return false;
                        }

                        payload = ThreatPayloadDefinition.TimedImpact(
                            new DamageSpec(timedImpactDamage, timedImpactBreakDamage),
                            timedImpactTargetPolicy,
                            new TickDuration(timedImpactDelayTicks),
                            presentationKey);
                        break;

                    default:
                        error = "Threat schedule payload kind is invalid.";
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
    }

    [CreateAssetMenu(fileName = "BattleScenarioConfig", menuName = "FPG Demo/Battle Scenario Config")]
    public sealed class BattleScenarioConfig : ScriptableObject
    {
        public const int DefaultSpatialTranscriptOperationCapacity = 1024;
        public const int DefaultSpatialTranscriptQueryCandidateCapacity =
            DefaultSpatialTranscriptOperationCapacity * SpatialContract.AttackQueryCandidateCapacity;

        [SerializeField, Min(0)]
        private long scenarioSeed = 1L;

        [Tooltip("选择 D0 场景配置后，角色、武器、手感、遭遇和舞台均从该资产链读取；下方旧内联字段不再作为 D0 数据来源。")]
        [SerializeField]
        private D0CombatScenarioDefinition authoredScenario;

        private D0CombatScenarioDefinition EffectiveAuthoredScenario =>
            FpgRoomPlaytestOverrides.ScenarioDefinition != null
                ? FpgRoomPlaytestOverrides.ScenarioDefinition
                : authoredScenario;
        [SerializeField, Min(1)]
        private int playerLife = 100;

        [SerializeField, Min(1)]
        private int playerBarrier = 100;

        [SerializeField, Min(1)]
        private int enemyLife = 800;

        [SerializeField, Min(1)]
        private int enemyBreak = 160;

        [SerializeField, Min(1)]
        private int weaponDefinitionId = 1;

        [SerializeField, Min(1)]
        private int magazineCapacity = 8;

        [SerializeField, Min(1)]
        private int primaryAmmoCost = 1;

        [SerializeField, Min(1)]
        private int primaryIntervalTicks = 39;

        [SerializeField, Min(0)]
        private int primaryDamage = 4;

        [SerializeField, Min(0)]
        private int primaryBreakDamage = 4;

        [SerializeField, Min(0)]
        private int primaryWeakpointDamageMultiplierBasisPoints = 12000;

        [SerializeField, Min(0)]
        private int primaryWeakpointBreakMultiplierBasisPoints = 25000;

        [SerializeField, Min(1)]
        private int secondaryAmmoCost = 2;

        [SerializeField, Min(0)]
        private int secondaryMinimumChargeTicks = 0;

        [SerializeField, Min(1)]
        private int secondaryRecoveryTicks = 30;

        [SerializeField, Min(0)]
        private int secondaryDamage = 28;

        [SerializeField, Min(0)]
        private int secondaryBreakDamage = 20;

        [SerializeField, Min(0)]
        private int secondaryWeakpointDamageMultiplierBasisPoints = 12000;

        [SerializeField, Min(0)]
        private int secondaryWeakpointBreakMultiplierBasisPoints = 25000;

        [SerializeField, Min(1)]
        private int secondaryMaxImpactCount = 4;

        [SerializeField, Min(1)]
        private int reloadDurationTicks = 84;

        [SerializeField, Min(1)]
        private int perfectRetractWindowTicks = 6;

        [SerializeField, Min(0)]
        private int perfectRetractMultiplierBasisPoints = 2500;

        [SerializeField, Min(1)]
        private int barrierLockDurationTicks = 30;

        [SerializeField, Min(0)]
        private int barrierRestoreBasisPoints = 10000;

        [SerializeField, Min(1)]
        private int enemyGroggyDurationTicks = 60;

        [SerializeField, Min(1)]
        private int projectileBudgetCapacity = 32;

        [SerializeField, Min(1)]
        private int projectileCapacity = 32;

        [SerializeField, Min(1)]
        private int threatCapacity = 8;

        [SerializeField, Min(1)]
        private int impactHistoryCapacity = 4096;

        [SerializeField, Min(1)]
        private int shotTargetHistoryCapacity = 1024;

        [SerializeField]
        private ThreatScheduleEntryAuthoring[] threatSchedule = Array.Empty<ThreatScheduleEntryAuthoring>();

        [SerializeField]
        private UnityAttackQuerySettings attackQuerySettings = UnityAttackQuerySettings.Default;

        [SerializeField, Min(1)]
        private int spatialTranscriptOperationCapacity = DefaultSpatialTranscriptOperationCapacity;

        [SerializeField, Min(SpatialContract.AttackQueryCandidateCapacity)]
        private int spatialTranscriptQueryCandidateCapacity =
            DefaultSpatialTranscriptQueryCandidateCapacity;

        public long ScenarioSeed => EffectiveAuthoredScenario != null
            ? EffectiveAuthoredScenario.ScenarioSeed
            : scenarioSeed;

        public D0CombatScenarioDefinition AuthoredScenario => EffectiveAuthoredScenario;

        public bool UsesAuthoredScenario => EffectiveAuthoredScenario != null;

        public int PlayerLife => EffectiveAuthoredScenario != null && EffectiveAuthoredScenario.Player != null
            ? EffectiveAuthoredScenario.Player.Life
            : playerLife;

        public int PlayerBarrier => EffectiveAuthoredScenario != null && EffectiveAuthoredScenario.Player != null
            ? EffectiveAuthoredScenario.Player.Barrier
            : playerBarrier;

        public int EnemyLife => EffectiveAuthoredScenario != null
            && EffectiveAuthoredScenario.Encounter != null
            && EffectiveAuthoredScenario.Encounter.Enemy != null
            ? EffectiveAuthoredScenario.Encounter.Enemy.Life
            : enemyLife;

        public int EnemyBreak => EffectiveAuthoredScenario != null
            && EffectiveAuthoredScenario.Encounter != null
            && EffectiveAuthoredScenario.Encounter.Enemy != null
            ? EffectiveAuthoredScenario.Encounter.Enemy.BreakValue
            : enemyBreak;

        public int MagazineCapacity => EffectiveAuthoredScenario != null
            && EffectiveAuthoredScenario.Player != null
            && EffectiveAuthoredScenario.Player.Weapon != null
            ? EffectiveAuthoredScenario.Player.Weapon.MagazineCapacity
            : magazineCapacity;

        public UnityAttackQuerySettings AttackQuerySettings
        {
            get
            {
                return TryResolveAttackQuerySettings(out UnityAttackQuerySettings settings, out _)
                    ? settings
                    : ResolveLegacyAttackQuerySettings();
            }
        }

        public int SpatialTranscriptOperationCapacity => spatialTranscriptOperationCapacity == 0
            ? DefaultSpatialTranscriptOperationCapacity
            : spatialTranscriptOperationCapacity;

        public int SpatialTranscriptQueryCandidateCapacity =>
            spatialTranscriptQueryCandidateCapacity == 0
                ? DefaultSpatialTranscriptQueryCandidateCapacity
                : spatialTranscriptQueryCandidateCapacity;

        public int ThreatScheduleCount => EffectiveAuthoredScenario != null
            && EffectiveAuthoredScenario.Encounter != null
            ? EffectiveAuthoredScenario.Encounter.ThreatScheduleCount
            : threatSchedule == null ? 0 : threatSchedule.Length;

        public bool TryValidateSpatialConfiguration(out string error)
        {
            if (!TryResolveAttackQuerySettings(out UnityAttackQuerySettings settings, out error)
                || !settings.IsValid)
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Unity attack query settings are invalid.";
                }

                return false;
            }

            if (SpatialTranscriptOperationCapacity <= 0)
            {
                error = "Spatial transcript operation capacity must be positive.";
                return false;
            }

            if (SpatialTranscriptQueryCandidateCapacity
                < SpatialContract.AttackQueryCandidateCapacity)
            {
                error = $"Spatial transcript query candidate capacity must be at least {SpatialContract.AttackQueryCandidateCapacity}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryCreateDefinition(out ScenarioDefinition definition, out string error)
        {
            definition = null;
            if (EffectiveAuthoredScenario != null)
            {
                D0CombatScenarioTechnicalSettings technicalSettings =
                    new D0CombatScenarioTechnicalSettings(
                        projectileBudgetCapacity,
                        projectileCapacity,
                        threatCapacity,
                        impactHistoryCapacity,
                        shotTargetHistoryCapacity);
                return FpgRoomPlaytestOverrides.RoomDefinition != null
                    ? EffectiveAuthoredScenario.TryCreateDefinitionForRoom(
                        technicalSettings,
                        out definition,
                        out error)
                    : EffectiveAuthoredScenario.TryCreateDefinition(
                        technicalSettings,
                        out definition,
                        out error);
            }

            return TryCreateLegacyDefinition(out definition, out error);
        }

        public bool TryCreateDefinitionForRoom(
            out ScenarioDefinition definition,
            out string error)
        {
            definition = null;
            if (EffectiveAuthoredScenario != null)
            {
                D0CombatScenarioTechnicalSettings technicalSettings =
                    new D0CombatScenarioTechnicalSettings(
                        projectileBudgetCapacity,
                        projectileCapacity,
                        threatCapacity,
                        impactHistoryCapacity,
                        shotTargetHistoryCapacity);
                return EffectiveAuthoredScenario.TryCreateDefinitionForRoom(
                    technicalSettings,
                    out definition,
                    out error);
            }

            return TryCreateLegacyDefinition(out definition, out error);
        }
        private bool TryCreateLegacyDefinition(out ScenarioDefinition definition, out string error)
        {
            definition = null;
            if (scenarioSeed < 0L)
            {
                error = "Scenario seed must be non-negative.";
                return false;
            }

            try
            {
                WeaponDefinition weapon = new WeaponDefinition(
                    weaponDefinitionId,
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

                if (!TryCreateThreatSchedule(out ThreatScheduleEntry[] schedule, out error))
                {
                    return false;
                }

                definition = new ScenarioDefinition(
                    unchecked((ulong)scenarioSeed),
                    weapon,
                    playerLife,
                    playerBarrier,
                    enemyLife,
                    enemyBreak,
                    new TickDuration(perfectRetractWindowTicks),
                    perfectRetractMultiplierBasisPoints,
                    new TickDuration(barrierLockDurationTicks),
                    barrierRestoreBasisPoints,
                    new TickDuration(enemyGroggyDurationTicks),
                    projectileBudgetCapacity,
                    projectileCapacity,
                    threatCapacity,
                    impactHistoryCapacity,
                    shotTargetHistoryCapacity,
                    schedule);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException)
            {
                error = exception.Message;
                return false;
            }
        }

        private bool TryResolveAttackQuerySettings(
            out UnityAttackQuerySettings settings,
            out string error)
        {
            UnityAttackQuerySettings technicalSettings = ResolveLegacyAttackQuerySettings();
            if (EffectiveAuthoredScenario == null)
            {
                settings = technicalSettings;
                error = string.Empty;
                return true;
            }

            return EffectiveAuthoredScenario.TryCreateAttackQuerySettings(
                technicalSettings,
                out settings,
                out error);
        }

        private UnityAttackQuerySettings ResolveLegacyAttackQuerySettings()
        {
            return IsLegacyZeroAttackQuerySettings(attackQuerySettings)
                ? UnityAttackQuerySettings.Default
                : attackQuerySettings;
        }

        private static bool IsLegacyZeroAttackQuerySettings(UnityAttackQuerySettings settings)
        {
            return settings.MaxDistance == 0f
                && settings.PrimarySpreadTangent == 0f
                && settings.SecondaryAreaRadius == 0f
                && settings.HitboxLayerMask == 0
                && settings.BlockerLayerMask == 0;
        }

        private bool TryCreateThreatSchedule(
            out ThreatScheduleEntry[] schedule,
            out string error)
        {
            if (threatSchedule == null || threatSchedule.Length == 0)
            {
                schedule = Array.Empty<ThreatScheduleEntry>();
                error = string.Empty;
                return true;
            }

            schedule = new ThreatScheduleEntry[threatSchedule.Length];
            for (int index = 0; index < threatSchedule.Length; index++)
            {
                if (!threatSchedule[index].TryCreate(out schedule[index], out string entryError))
                {
                    schedule = null;
                    error = $"Threat schedule entry {index} is invalid: {entryError}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
