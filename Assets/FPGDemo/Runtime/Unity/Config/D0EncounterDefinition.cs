using System;
using System.Collections.Generic;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum D0EncounterContract
    {
        BurstbugStandard = 0,
        LuanHudieSingleProjectile = 1,
        HudieSingleProjectile = 2
    }

    public enum D0EncounterSpawnPosePolicy
    {
        [InspectorName("使用出生点")]
        AtSpawnPoint = 0,

        [InspectorName("继承上一实体姿态")]
        InheritPreviousGameplayPose = 1
    }

    [Serializable]
    public sealed class D0EncounterSpawnSlot
    {
        [D0PlannerField("运行时定义 ID", "遭遇内敌人生命周期使用的稳定正整数。初始敌人必须为 1；后续替换必须大于 1 且唯一。")]
        [SerializeField, Min(1)]
        private int definitionId = 1;

        [D0PlannerField("敌人配置", "该生命周期槽位使用的可复用敌人定义。敌人数值、表现、行为和 Entity Prefab 均从该资产解析。")]
        [SerializeField]
        private D0EnemyDefinition enemy;

        [D0PlannerField("出生点 ID", "选择舞台中的具名 SpawnPoint。遭遇决定谁使用哪个点，但不复制位置或旋转。")]
        [SerializeField]
        private string spawnPointId = "enemy-main";

        [D0PlannerField("生成 Tick", "初始敌人为 0；后续替换必须严格晚于前一个槽位。单位为确定性战斗 Tick。")]
        [SerializeField, Min(0)]
        private int spawnTick;

        [D0PlannerField("姿态策略", "使用出生点会重置到该点；继承上一实体姿态用于孵化或替换时保持已产生的 gameplay 位移。")]
        [SerializeField]
        private D0EncounterSpawnPosePolicy posePolicy = D0EncounterSpawnPosePolicy.AtSpawnPoint;

        public int DefinitionId => definitionId;
        public D0EnemyDefinition Enemy => enemy;
        public string SpawnPointId => spawnPointId;
        public int SpawnTick => spawnTick;
        public D0EncounterSpawnPosePolicy PosePolicy => posePolicy;

        public bool TryValidate(out string error)
        {
            if (definitionId <= 0 || enemy == null
                || string.IsNullOrWhiteSpace(spawnPointId) || spawnTick < 0)
            {
                error = "Encounter spawn slot requires a positive id, enemy, spawn point and non-negative tick.";
                return false;
            }

            if (!Enum.IsDefined(typeof(D0EncounterSpawnPosePolicy), posePolicy))
            {
                error = $"Encounter spawn slot {definitionId} has an unsupported pose policy.";
                return false;
            }

            if (!enemy.TryValidate(out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// A small authored encounter unit. It is the first level-facing D0 asset;
    /// it owns the active enemy attack schedule. A scenario may replace that
    /// active runtime with authored EnemySpawnDefinition boundaries.
    /// </summary>
    [CreateAssetMenu(
        fileName = "D0EncounterDefinition",
        menuName = "FPG Demo/Config/D0 Encounter Definition")]
    public sealed class D0EncounterDefinition : ScriptableObject
    {
        [D0PlannerSection("基础信息")]
        [D0PlannerField("遭遇 ID", "单敌人遭遇的稳定配置标识，用于配置关联与校验。保持非空且稳定。")]
        [SerializeField]
        private string encounterId = "burstbug-training";

        [D0PlannerField("显示名称", "供策划、验证日志和编辑器识别的遭遇名称，不参与战斗计算。")]
        [SerializeField]
        private string displayName = "Burstbug Training";

        [TextArea]
        [D0PlannerField("策划说明", "记录遭遇目标、调参意图和验证备注；运行时不会读取此文本。")]
        [SerializeField]
        private string designerNotes;

        [D0PlannerSection("遭遇合同")]
        [D0PlannerField("遭遇合同", "“Burstbug 标准战斗”保留既有三种攻击语言。“陆鸾／蝴蝶单发投射物”仅允许固定站位敌人反复使用同一单发投射物攻击；不要在同一资产中混用两种合同。")]
        [SerializeField]
        private D0EncounterContract encounterContract = D0EncounterContract.BurstbugStandard;

        [D0PlannerSection("单活动敌人生成编排")]
        [D0PlannerField("敌人生成槽位", "按运行时定义 ID 与生成 Tick 编排初始敌人及后续替换。当前仍只有一个活动 EnemyRuntime，不代表支持多敌同场。")]
        [SerializeField]
        private D0EncounterSpawnSlot[] spawnSlots = Array.Empty<D0EncounterSpawnSlot>();

        [D0PlannerSection("单敌人攻击编排")]
        [D0PlannerField("可复用攻击编排", "只编排出招 Tick 和可复用的攻击定义。勿在此复制动画、预警、载荷、特效或音频参数。")]
        [SerializeField]
        private D0EncounterAttackScheduleEntry[] attackSchedule = Array.Empty<D0EncounterAttackScheduleEntry>();

        public string EncounterId => encounterId;
        public string DisplayName => displayName;
        public D0EncounterContract EncounterContract => encounterContract;
        public int SpawnSlotCount => spawnSlots == null ? 0 : spawnSlots.Length;
        public D0EncounterSpawnSlot InitialSpawnSlot => SpawnSlotCount == 0
            ? null
            : spawnSlots[0];
        public D0EnemyDefinition Enemy => InitialSpawnSlot == null
            ? null
            : InitialSpawnSlot.Enemy;
        public int AttackScheduleCount => attackSchedule == null ? 0 : attackSchedule.Length;
        public int ThreatScheduleCount => AttackScheduleCount;
        public bool UsesReusableAttackDefinitions => attackSchedule != null && attackSchedule.Length > 0;

        public D0EncounterSpawnSlot GetSpawnSlot(int index)
        {
            if (spawnSlots == null || index < 0 || index >= spawnSlots.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return spawnSlots[index];
        }

        public bool TryGetSpawnSlot(
            int definitionId,
            out D0EncounterSpawnSlot slot)
        {
            D0EncounterSpawnSlot[] slots = spawnSlots
                ?? Array.Empty<D0EncounterSpawnSlot>();
            for (int index = 0; index < slots.Length; index++)
            {
                D0EncounterSpawnSlot candidate = slots[index];
                if (candidate != null && candidate.DefinitionId == definitionId)
                {
                    slot = candidate;
                    return true;
                }
            }

            slot = null;
            return false;
        }

        public D0EncounterAttackScheduleEntry GetAttackScheduleEntry(int index)
        {
            if (!UsesReusableAttackDefinitions || index < 0 || index >= attackSchedule.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return attackSchedule[index];
        }

        /// <summary>
        /// Resolves the reusable authored attack behind a runtime threat
        /// definition id. Repeated schedule entries intentionally return the
        /// same asset, which keeps recovery behavior and presentation language
        /// authored in one place.
        /// </summary>
        public bool TryGetAttackDefinition(
            int definitionId,
            out D0EnemyAttackDefinition attack)
        {
            attack = null;
            if (!UsesReusableAttackDefinitions || definitionId <= 0)
            {
                return false;
            }

            for (int index = 0; index < attackSchedule.Length; index++)
            {
                D0EnemyAttackDefinition candidate = attackSchedule[index].Attack;
                if (candidate != null && candidate.DefinitionId == definitionId)
                {
                    attack = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetAttackByPresentationKey(
            int presentationKey,
            out D0EnemyAttackDefinition attack)
        {
            attack = null;
            if (!UsesReusableAttackDefinitions || presentationKey <= 0)
            {
                return false;
            }

            for (int index = 0; index < attackSchedule.Length; index++)
            {
                D0EnemyAttackDefinition candidate =
                    attackSchedule[index].Attack;
                if (candidate == null
                    || candidate.PresentationKey != presentationKey)
                {
                    continue;
                }

                if (attack != null && attack != candidate)
                {
                    attack = null;
                    return false;
                }

                attack = candidate;
            }

            return attack != null;
        }

public bool TryCreateThreatSchedule(out ThreatScheduleEntry[] schedule, out string error)
        {
            schedule = Array.Empty<ThreatScheduleEntry>();
            if (!UsesReusableAttackDefinitions)
            {
                error = "Encounter definition requires at least one reusable attack schedule entry.";
                return false;
            }

            ThreatScheduleEntry[] created = new ThreatScheduleEntry[attackSchedule.Length];
            for (int index = 0; index < attackSchedule.Length; index++)
            {
                if (!attackSchedule[index].TryCreate(out created[index], out string entryError))
                {
                    error = $"Encounter reusable attack schedule entry {index} is invalid: {entryError}";
                    return false;
                }
            }

            schedule = created;
            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(encounterId) || string.IsNullOrWhiteSpace(displayName))
            {
                error = "Encounter definition requires stable ID and display name values.";
                return false;
            }

            if (encounterContract != D0EncounterContract.BurstbugStandard
                && encounterContract != D0EncounterContract.LuanHudieSingleProjectile
                && encounterContract != D0EncounterContract.HudieSingleProjectile)
            {
                error = "Encounter definition has an unsupported combat contract.";
                return false;
            }


            if (spawnSlots == null || spawnSlots.Length == 0)
            {
                error = "Encounter definition requires at least one enemy spawn slot.";
                return false;
            }

            HashSet<int> definitionIds = new HashSet<int>();
            int previousSpawnTick = -1;
            for (int index = 0; index < spawnSlots.Length; index++)
            {
                D0EncounterSpawnSlot slot = spawnSlots[index];
                if (slot == null || !slot.TryValidate(out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = $"Encounter spawn slot {index} is missing.";
                    }

                    return false;
                }

                if (!definitionIds.Add(slot.DefinitionId))
                {
                    error = $"Encounter spawn slot definition id {slot.DefinitionId} must be unique.";
                    return false;
                }

                if (index == 0)
                {
                    if (slot.DefinitionId != 1
                        || slot.SpawnTick != 0
                        || slot.PosePolicy != D0EncounterSpawnPosePolicy.AtSpawnPoint)
                    {
                        error = "Encounter initial spawn slot must use definition id 1, tick 0 and AtSpawnPoint.";
                        return false;
                    }
                }
                else if (slot.DefinitionId <= 1 || slot.SpawnTick <= previousSpawnTick)
                {
                    error = "Encounter replacement spawn slots require ids greater than 1 and strictly increasing ticks.";
                    return false;
                }

                previousSpawnTick = slot.SpawnTick;
            }

            if (!TryCreateThreatSchedule(out _, out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryValidateCombatContract(out string error)
        {
            switch (encounterContract)
            {
                case D0EncounterContract.BurstbugStandard:
                    return TryValidateReusableAttackContract(out error);

                case D0EncounterContract.LuanHudieSingleProjectile:
                    return TryValidateLuanHudieSingleProjectileContract(out error);

                case D0EncounterContract.HudieSingleProjectile:
                    return TryValidateHudieSingleProjectileContract(out error);

                default:
                    error = "Encounter definition has an unsupported combat contract.";
                    return false;
            }
        }

        /// <summary>
        /// Validates the concrete D0 standard-battle contract after the generic
        /// encounter data has been checked. Keeping this separate lets the
        /// reusable-attack container remain usable by later encounters while
        /// making the Fei × Burstbug sample explicitly prove its three attack
        /// languages, presentation mapping and patrol recovery rule.
        /// </summary>
        public bool TryValidateReusableAttackContract(out string error)
        {

            if (encounterContract != D0EncounterContract.BurstbugStandard)
            {
                error = "The Burstbug reusable-attack contract is valid only for BurstbugStandard encounters.";
                return false;
            }

            if (!TryValidate(out error))
            {
                return false;
            }

            if (!UsesReusableAttackDefinitions)
            {
                error = "D0 standard battle requires reusable enemy attack definitions.";
                return false;
            }

            if (Enemy == null || Enemy.BehaviorProfile == null)
            {
                error = "D0 standard battle requires a Burstbug behavior profile.";
                return false;
            }

            if (!Enemy.BehaviorProfile.TryValidate(out error))
            {
                return false;
            }

            if (Enemy.BehaviorProfile.BehaviorMode != D0EnemyBehaviorMode.Patrol)
            {
                error = "D0 standard battle requires Burstbug's patrol behavior mode.";
                return false;
            }


            if (!Enemy.BehaviorProfile.StopDuringThreat
                || !Enemy.BehaviorProfile.ResumePatrolAfterRecovery)
            {
                error = "D0 standard battle requires Burstbug to stop for attacks and resume patrol after recovery.";
                return false;
            }

            D0ActorPresentationDefinition actorPresentation = Enemy.ActorPresentation;
            if (actorPresentation == null
                || !actorPresentation.TryGetEnemy(out EnemyActorPresentationDefinition enemyPresentation)
                || enemyPresentation == null)
            {
                error = "D0 standard battle requires Burstbug enemy presentation data.";
                return false;
            }

            if (!string.Equals(
                    Enemy.BehaviorProfile.EntryAnimationSlot,
                    enemyPresentation.EnterAnimation,
                    StringComparison.Ordinal))
            {
                error = "Burstbug behavior entry animation must match the authored enemy presentation entry animation.";
                return false;
            }

            D0EnemyEffectPresentationDefinition effects = null;
            if (actorPresentation.TryGetEnemyEffects(
                    out D0EnemyEffectPresentationDefinition authoredEffects)
                && authoredEffects != null)
            {
                effects = authoredEffects;
                if (!effects.TryValidate(out error))
                {
                    return false;
                }
            }

            bool hasFast = false;
            bool hasVolley = false;
            bool hasHeavy = false;
            long previousSequence = 0L;
            int previousDueTick = -1;
            for (int index = 0; index < attackSchedule.Length; index++)
            {
                D0EncounterAttackScheduleEntry schedule = attackSchedule[index];
                if (!schedule.TryCreate(out _, out string entryError))
                {
                    error = $"D0 reusable attack schedule entry {index} is invalid: {entryError}";
                    return false;
                }

                if (schedule.ScheduleSequence <= previousSequence
                    || schedule.DueTick < previousDueTick)
                {
                    error = "D0 reusable attack schedule must be ordered by increasing sequence and non-decreasing due tick.";
                    return false;
                }

                D0EnemyAttackDefinition attack = schedule.Attack;
                if (attack == null
                    || attack.RecoveryRule != D0AttackRecoveryRule.ResumePatrolAfterRecovery)
                {
                    error = "Every D0 standard-battle attack must resume Burstbug patrol after recovery.";
                    return false;
                }

                for (int priorIndex = 0; priorIndex < index; priorIndex++)
                {
                    D0EnemyAttackDefinition priorAttack = attackSchedule[priorIndex].Attack;
                    if (priorAttack != null
                        && priorAttack != attack
                        && priorAttack.DefinitionId == attack.DefinitionId)
                    {
                        error = "D0 reusable attack definitions must not share a runtime definition ID unless they are the same asset.";
                        return false;
                    }
                }

                switch (attack.AttackLanguage)
                {
                    case D0EnemyAttackLanguage.FastAttack:
                        if (attack.PayloadKind != ThreatPayloadKind.SweptProjectile
                            || attack.ProjectileInterceptable
                            || attack.ThreatPresentationKind !=
                                FpgThreatPresentationKind.FastUninterceptable)
                        {
                            error = "D0 fast attack must use the fast threat language and an uninterceptable projectile.";
                            return false;
                        }

                        hasFast = true;
                        break;

                    case D0EnemyAttackLanguage.InterceptableVolley:
                        if (attack.PayloadKind != ThreatPayloadKind.SweptProjectile
                            || !attack.ProjectileInterceptable
                            || attack.ThreatPresentationKind !=
                                FpgThreatPresentationKind.InterceptableVolley)
                        {
                            error = "D0 interceptable volley must use the volley threat language and interceptable projectiles.";
                            return false;
                        }

                        hasVolley = true;
                        break;

                    case D0EnemyAttackLanguage.HeavyWeakpointBreak:
                        if (attack.PayloadKind != ThreatPayloadKind.TimedImpact
                            || attack.ThreatPresentationKind !=
                                FpgThreatPresentationKind.HeavyWeakpoint)
                        {
                            error = "D0 heavy weakpoint attack must use the heavy threat language and a timed-impact payload.";
                            return false;
                        }

                        hasHeavy = true;
                        break;

                    default:
                        error = "D0 standard battle contains an unsupported enemy attack language.";
                        return false;
                }

                previousSequence = schedule.ScheduleSequence;
                previousDueTick = schedule.DueTick;
            }

            if (!hasFast || !hasVolley || !hasHeavy)
            {
                error = "D0 standard battle must schedule fast, interceptable-volley and heavy-weakpoint attack languages.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryValidateLuanHudieSingleProjectileContract(out string error)
        {
            return TryValidateSingleProjectileContract(
                D0EncounterContract.LuanHudieSingleProjectile,
                "Luan/Hudie",
                nameof(D0EncounterContract.LuanHudieSingleProjectile),
                out error);
        }

        public bool TryValidateHudieSingleProjectileContract(out string error)
        {
            return TryValidateSingleProjectileContract(
                D0EncounterContract.HudieSingleProjectile,
                "Hudie",
                nameof(D0EncounterContract.HudieSingleProjectile),
                out error);
        }

        private bool TryValidateSingleProjectileContract(
            D0EncounterContract expectedContract,
            string contractDisplayName,
            string expectedContractName,
            out string error)
        {
            if (encounterContract != expectedContract)
            {
                error = $"The {contractDisplayName} single-projectile contract is valid only for {expectedContractName} encounters.";
                return false;
            }

            if (!TryValidate(out error))
            {
                return false;
            }

            if (!UsesReusableAttackDefinitions)
            {
                error = $"The {contractDisplayName} encounter requires reusable enemy attack definitions.";
                return false;
            }

            if (Enemy == null || Enemy.BehaviorProfile == null)
            {
                error = $"The {contractDisplayName} encounter requires an enemy behavior profile.";
                return false;
            }

            if (!Enemy.BehaviorProfile.UsesFixedPosition)
            {
                error = $"The {contractDisplayName} encounter requires a fixed-position enemy behavior profile.";
                return false;
            }

            D0EnemyAttackDefinition scheduledAttack = null;
            long previousSequence = 0L;
            int previousDueTick = -1;
            for (int index = 0; index < attackSchedule.Length; index++)
            {
                D0EncounterAttackScheduleEntry schedule = attackSchedule[index];
                if (!schedule.TryCreate(out _, out string entryError))
                {
                    error = $"{contractDisplayName} reusable attack schedule entry {index} is invalid: {entryError}";
                    return false;
                }

                if (schedule.ScheduleSequence <= previousSequence
                    || schedule.DueTick < previousDueTick)
                {
                    error = $"{contractDisplayName} reusable attack schedule must be ordered by increasing sequence and non-decreasing due tick.";
                    return false;
                }

                D0EnemyAttackDefinition attack = schedule.Attack;
                if (attack == null)
                {
                    error = $"{contractDisplayName} reusable attack schedule requires a single attack definition.";
                    return false;
                }

                if (scheduledAttack != null && attack != scheduledAttack)
                {
                    error = $"Every {contractDisplayName} schedule entry must reference the same reusable attack definition.";
                    return false;
                }

                if (attack.AttackLanguage != D0EnemyAttackLanguage.FastAttack
                    || attack.PayloadKind != ThreatPayloadKind.SweptProjectile
                    || attack.PayloadCount != 1
                    || attack.ProjectileInterceptable
                    || attack.RecoveryRule != D0AttackRecoveryRule.HoldAtAttackPosition
                    || attack.ThreatPresentationKind !=
                        FpgThreatPresentationKind.FastUninterceptable)
                {
                    error = $"{contractDisplayName} attacks must be a non-interceptable FastAttack with one swept-projectile payload and hold-position recovery.";
                    return false;
                }

                scheduledAttack = attack;
                previousSequence = schedule.ScheduleSequence;
                previousDueTick = schedule.DueTick;
            }

            error = string.Empty;
            return true;
        }

    }
}
