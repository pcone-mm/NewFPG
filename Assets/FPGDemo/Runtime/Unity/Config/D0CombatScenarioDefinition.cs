using System;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Unity-only authored composition of a playable D0 encounter. The output
    /// is still a pure ScenarioDefinition, so BattleSessionHost stays unaware of
    /// the underlying ScriptableObject graph.
    /// </summary>
    [CreateAssetMenu(
        fileName = "D0CombatScenarioDefinition",
        menuName = "FPG Demo/Config/D0 Combat Scenario Definition")]
    public sealed class D0CombatScenarioDefinition : ScriptableObject
    {
        [D0PlannerSection("基础信息")]
        [D0PlannerField("场景 ID", "本单遭遇配置的稳定标识，用于配置校验、日志与确定性场景定义关联。保持非空且稳定，不要用显示名称替代。")]
        [SerializeField]
        private string scenarioId = "combatlab-fei-vs-burstbug";

        [D0PlannerField("显示名称", "供策划和编辑器识别的名称，不参与战斗数值、随机或资源寻址。")]
        [SerializeField]
        private string displayName = "CombatLab: Fei vs Burstbug";

        [TextArea]
        [D0PlannerField("策划说明", "记录本场遭遇的设计意图、调参原因和验证备注；运行时不会读取此文本。")]
        [SerializeField]
        private string designerNotes;

        [D0PlannerField("随机种子", "本遭遇的确定性随机种子。相同配置、输入和种子会得到相同战斗过程；必须为非负整数。")]
        [SerializeField, Min(0)]
        private long scenarioSeed = 1L;

        [D0PlannerSection("遭遇组合")]
        [D0PlannerField("玩家角色", "本场遭遇使用的角色定义。生命、护盾、武器和角色表现都从该资产解析。")]
        [SerializeField]
        private D0CharacterDefinition player;

        [D0PlannerField("玩家出生点 ID", "选择关卡舞台中的具名 SpawnPoint 作为玩家 gameplay 根节点。角色视觉偏移、朝向和缩放仍由玩家角色表现定义拥有。")]
        [SerializeField]
        private string playerSpawnPointId = "player-main";

        [D0PlannerField("敌人遭遇", "本场遭遇使用的单活动敌人及其生成槽位和威胁时间表。SpawnSlot 可以编排实体替换，但不表示支持多敌人同时在场。")]
        [SerializeField]
        private D0EncounterDefinition encounter;

        [D0PlannerField("陆鸾孵化蝴蝶技能", "陆鸾／蝴蝶组合遭遇直接引用的召唤技能。动画、运动、Socket、VFX、音频与时序全部由该技能拥有。")]
        [SerializeField]
        private D0LuanSummonHudieDefinition luanSummonHudie;

        [D0PlannerField("战斗手感", "主射散布、副射范围、掩体护盾节奏等可调手感参数。物理层和查询容量不在此资产开放。")]
        [SerializeField]
        private D0CombatFeelProfile feelProfile;

        [D0PlannerField("2.5D 3C 配置", "统一定义相机枢轴与主相机局部变换、视野角、裁剪距离、固定构图验收锚点、自由准星范围与灵敏度、攻击查询距离、输入缓冲、探身／缩回护盾表现和射击镜头后移。它不启用玩家自由移动。")]
        [SerializeField]
        private D0ThreeCProfile threeCProfile;

        [D0PlannerSection("单遭遇舞台")]
        [D0PlannerField("关卡舞台", "本场遭遇使用的环境图层和具名 SpawnPoint。角色视觉、技能特效、敌人实体 Prefab 与命中体均不由关卡舞台定义。")]
        [SerializeField]
        private D0StageDefinition stageDefinition;

        public string ScenarioId => scenarioId;
        public string DisplayName => displayName;
        public long ScenarioSeed => scenarioSeed;
        public D0CharacterDefinition Player => player;
        public string PlayerSpawnPointId => playerSpawnPointId;
        public D0EncounterDefinition Encounter => encounter;
        public D0EncounterContract EncounterContract => encounter == null
            ? D0EncounterContract.BurstbugStandard
            : encounter.EncounterContract;
        public D0LuanSummonHudieDefinition LuanSummonHudie => luanSummonHudie;
        public D0CombatFeelProfile FeelProfile => feelProfile;
        public D0ThreeCProfile ThreeCProfile => threeCProfile;
        public D0StageDefinition StageDefinition => stageDefinition;

        public bool TryCreateDefinition(
            D0CombatScenarioTechnicalSettings technicalSettings,
            out ScenarioDefinition definition,
            out string error)
        {
            return TryCreateDefinition(
                technicalSettings,
                validateStage: true,
                out definition,
                out error);
        }

        public bool TryCreateDefinitionForRoom(
            D0CombatScenarioTechnicalSettings technicalSettings,
            out ScenarioDefinition definition,
            out string error)
        {
            return TryCreateDefinition(
                technicalSettings,
                validateStage: false,
                out definition,
                out error);
        }

        private bool TryCreateDefinition(
            D0CombatScenarioTechnicalSettings technicalSettings,
            bool validateStage,
            out ScenarioDefinition definition,
            out string error)
        {
            definition = null;
            if (!TryValidate(validateStage, out error))
            {
                return false;
            }

            if (!player.Weapon.TryCreate(out WeaponDefinition weapon, out error)
                || !encounter.TryCreateThreatSchedule(out var threatSchedule, out error))
            {
                return false;
            }

            try
            {
                EnemySpawnDefinition[] enemySpawns = CreateEnemySpawns(technicalSettings.ThreatCapacity);
                definition = new ScenarioDefinition(
                    unchecked((ulong)scenarioSeed),
                    weapon,
                    player.Life,
                    player.Barrier,
                    encounter.Enemy.Life,
                    encounter.Enemy.BreakValue,
                    feelProfile.PerfectRetractWindow,
                    feelProfile.PerfectRetractMultiplierBasisPoints,
                    feelProfile.EnemyGroggyDuration,
                    technicalSettings.ProjectileBudgetCapacity,
                    technicalSettings.ProjectileCapacity,
                    technicalSettings.ThreatCapacity,
                    technicalSettings.ImpactHistoryCapacity,
                    technicalSettings.ShotTargetHistoryCapacity,
                    threatSchedule,
                    enemySpawns);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException)
            {
                error = exception.Message;
                return false;
            }
        }

        private EnemySpawnDefinition[] CreateEnemySpawns(int threatCapacity)
        {
            int replacementCount = encounter.SpawnSlotCount - 1;
            if (replacementCount <= 0)
            {
                return Array.Empty<EnemySpawnDefinition>();
            }

            EnemySpawnDefinition[] spawns = new EnemySpawnDefinition[replacementCount];
            for (int index = 0; index < replacementCount; index++)
            {
                D0EncounterSpawnSlot slot = encounter.GetSpawnSlot(index + 1);
                D0EnemyDefinition enemy = slot.Enemy;
                spawns[index] = new EnemySpawnDefinition(
                    slot.DefinitionId,
                    new TickIndex(slot.SpawnTick),
                    enemy.Life,
                    enemy.BreakValue,
                    feelProfile.EnemyGroggyDuration,
                    threatCapacity);
            }

            return spawns;
        }

        public bool TryCreateAttackQuerySettings(
            UnityAttackQuerySettings technicalSettings,
            out UnityAttackQuerySettings settings,
            out string error)
        {
            settings = default(UnityAttackQuerySettings);
            if (feelProfile == null)
            {
                error = "Authored combat scenario requires a combat-feel profile.";
                return false;
            }

            if (threeCProfile == null)
            {
                error = "Authored combat scenario requires a D0 3C profile.";
                return false;
            }

            if (!feelProfile.TryCreateAttackQuerySettings(technicalSettings, out settings, out error)
                || !threeCProfile.TryValidate(out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            return TryValidate(validateStage: true, out error);
        }

        public bool TryValidateForRoom(out string error)
        {
            return TryValidate(validateStage: false, out error);
        }

        private bool TryValidate(bool validateStage, out string error)
        {
            if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(displayName))
            {
                error = "Combat scenario requires stable ID and display name values.";
                return false;
            }

            if (scenarioSeed < 0L)
            {
                error = "Combat scenario seed must be non-negative.";
                return false;
            }

            if (player == null)
            {
                error = "Combat scenario requires a player definition.";
                return false;
            }

            if (!player.TryValidate(out error))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(playerSpawnPointId))
            {
                error = "Combat scenario requires a player spawn point id.";
                return false;
            }

            if (encounter == null)
            {
                error = "Combat scenario requires an encounter definition.";
                return false;
            }

            if (!encounter.TryValidate(out error))
            {
                return false;
            }

            if (!encounter.UsesReusableAttackDefinitions)
            {
                error = "D0 combat scenario requires an encounter composed from reusable enemy attack definitions.";
                return false;
            }

            if (!encounter.TryValidateCombatContract(out error))
            {
                return false;
            }

            if (EncounterContract == D0EncounterContract.LuanHudieSingleProjectile)
            {
                if (luanSummonHudie == null)
                {
                    error = "Luan/Hudie combat scenario requires a direct summon definition.";
                    return false;
                }

                if (!luanSummonHudie.TryValidate(out string summonError))
                {
                    error = $"Luan/Hudie summon definition is invalid: {summonError}";
                    return false;
                }

                D0EnemyDefinition luanEnemy = encounter.InitialSpawnSlot.Enemy;
                D0EnemyDefinition hudieEnemy = luanSummonHudie.HudieEnemy;
                if (encounter.SpawnSlotCount != 2 || luanEnemy == null)
                {
                    error = "Luan/Hudie combat scenario requires exactly two spawn slots with Luan first.";
                    return false;
                }

                if (hudieEnemy == null
                    || hudieEnemy == luanEnemy
                    || string.Equals(hudieEnemy.EnemyId, luanEnemy.EnemyId, StringComparison.Ordinal))
                {
                    error = "Luan/Hudie combat scenario requires a Hudie enemy definition distinct from Luan.";
                    return false;
                }

                D0EncounterSpawnSlot hudieSlot = encounter.GetSpawnSlot(1);
                int hudieVisibleTick = luanSummonHudie.AppearanceTick;
                if (hudieSlot.DefinitionId != 2
                    || hudieSlot.Enemy != hudieEnemy
                    || hudieSlot.SpawnTick != hudieVisibleTick
                    || hudieSlot.PosePolicy != D0EncounterSpawnPosePolicy.InheritPreviousGameplayPose)
                {
                    error = "Luan/Hudie combat scenario requires Hudie spawn slot 2 at the summon-defined appearance tick while inheriting Luan's gameplay pose.";
                    return false;
                }

                if (luanEnemy.EntityPrefab == null
                    || !luanEnemy.EntityPrefab.TryResolveSocket(
                        luanSummonHudie.SummonSocketId,
                        out _))
                {
                    error = $"Luan entity prefab cannot resolve summon socket '{luanSummonHudie.SummonSocketId}'.";
                    return false;
                }

                if (hudieEnemy.EntityPrefab == null
                    || !hudieEnemy.EntityPrefab.TryResolveSocket(
                        luanSummonHudie.AppearanceSocketId,
                        out _))
                {
                    error = $"Hudie entity prefab cannot resolve appearance socket '{luanSummonHudie.AppearanceSocketId}'.";
                    return false;
                }

                if (!TryValidateEntityAnimations(
                        luanEnemy,
                        out error,
                        luanSummonHudie.SummonAnimation)
                    || !TryValidateEntityAnimations(
                        hudieEnemy,
                        out error,
                        luanSummonHudie.AppearanceAnimation))
                {
                    return false;
                }

                for (int index = 0; index < encounter.AttackScheduleCount; index++)
                {
                    D0EncounterAttackScheduleEntry scheduledAttack =
                        encounter.GetAttackScheduleEntry(index);
                    if (scheduledAttack.Attack == null)
                    {
                        error =
                            "Luan/Hudie combat scenario requires concrete Hudie attack definitions.";
                        return false;
                    }
                }

                D0EncounterAttackScheduleEntry firstAttack = encounter.GetAttackScheduleEntry(0);
                if (firstAttack.DueTick < hudieVisibleTick)
                {
                    error =
                        $"Luan/Hudie first projectile due tick ({firstAttack.DueTick}) must not precede "
                        + $"Hudie's visible tick ({hudieVisibleTick}).";
                    return false;
                }
            }
            else
            {
                if (luanSummonHudie != null)
                {
                    error = "Non-composite D0 combat scenarios must not reference a Luan/Hudie summon definition.";
                    return false;
                }

                if (encounter.SpawnSlotCount != 1)
                {
                    error = "Non-composite D0 combat scenarios require exactly one initial enemy spawn slot.";
                    return false;
                }
            }

            if (feelProfile == null)
            {
                error = "Combat scenario requires a combat-feel profile.";
                return false;
            }

            if (!feelProfile.TryValidate(out error))
            {
                return false;
            }

            if (threeCProfile == null)
            {
                error = "Combat scenario requires a D0 3C profile.";
                return false;
            }

            if (!threeCProfile.TryValidate(out error))
            {
                return false;
            }

            if (validateStage)
            {
                if (stageDefinition == null)
                {
                    error = "Combat scenario requires a stage definition.";
                    return false;
                }

                if (!stageDefinition.TryValidate(out error))
                {
                    return false;
                }

                if (!stageDefinition.TryGetSpawnPoint(playerSpawnPointId, out _))
                {
                    error = $"Combat scenario player spawn point '{playerSpawnPointId}' is not defined by stage '{stageDefinition.StageId}'.";
                    return false;
                }

                for (int index = 0; index < encounter.SpawnSlotCount; index++)
                {
                    D0EncounterSpawnSlot slot = encounter.GetSpawnSlot(index);
                    if (!stageDefinition.TryGetSpawnPoint(slot.SpawnPointId, out _))
                    {
                        error = $"Encounter spawn point '{slot.SpawnPointId}' is not defined by stage '{stageDefinition.StageId}'.";
                        return false;
                    }
                }
            }


            error = string.Empty;
            return true;
        }

        private static bool TryValidateEntityAnimations(
            D0EnemyDefinition enemy,
            out string error,
            params string[] animationNames)
        {
            var skeleton = enemy == null || enemy.EntityPrefab == null
                ? null
                : enemy.EntityPrefab.SkeletonAnimation;
            if (skeleton == null || skeleton.SkeletonDataAsset == null)
            {
                error = "Summon presentation requires an Entity Prefab with SkeletonData.";
                return false;
            }

            var data = skeleton.SkeletonDataAsset.GetSkeletonData(true);
            if (data == null)
            {
                error = $"Enemy '{enemy.EnemyId}' could not load its SkeletonData.";
                return false;
            }

            for (int index = 0; index < animationNames.Length; index++)
            {
                string animationName = animationNames[index];
                if (string.IsNullOrWhiteSpace(animationName)
                    || data.FindAnimation(animationName) == null)
                {
                    error =
                        $"Enemy '{enemy.EnemyId}' entity cannot resolve summon animation "
                        + $"'{animationName}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

    }

    /// <summary>
    /// Technical, non-designer capacities supplied by BattleScenarioConfig when
    /// an authored scenario is composed. It deliberately contains no Unity object
    /// references and is not kept as runtime state.
    /// </summary>
    public readonly struct D0CombatScenarioTechnicalSettings
    {
        public D0CombatScenarioTechnicalSettings(
            int projectileBudgetCapacity,
            int projectileCapacity,
            int threatCapacity,
            int impactHistoryCapacity,
            int shotTargetHistoryCapacity)
        {
            ProjectileBudgetCapacity = projectileBudgetCapacity;
            ProjectileCapacity = projectileCapacity;
            ThreatCapacity = threatCapacity;
            ImpactHistoryCapacity = impactHistoryCapacity;
            ShotTargetHistoryCapacity = shotTargetHistoryCapacity;
        }

        public int ProjectileBudgetCapacity { get; }
        public int ProjectileCapacity { get; }
        public int ThreatCapacity { get; }
        public int ImpactHistoryCapacity { get; }
        public int ShotTargetHistoryCapacity { get; }
    }
}
