using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Skills;
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

        [D0PlannerSection("正式技能时间轴")]
        [D0PlannerField("主射技能", "主射正式技能资产。运行时只从该资产编译攻击事件、弹药消耗、动作时长与冷却。")]
        [SerializeField]
        private FpgPlayerSkillDefinition primarySkill;

        [D0PlannerField("副射技能", "副射正式技能资产。蓄力释放优先执行 Release 序列，否则执行 Execute 序列。")]
        [SerializeField]
        private FpgPlayerSkillDefinition secondarySkill;

        [D0PlannerField("换弹技能", "换弹正式技能资产。Execute 序列必须包含 ReloadCommit 逻辑事件。")]
        [SerializeField]
        private FpgPlayerSkillDefinition reloadSkill;

        [D0PlannerSection("瞄准配置")]


        [D0PlannerField("瞄准指示器", "该武器的常态、射击与命中 UI 表现，不参与输入、射线或伤害。")]
        [SerializeField]
        private PlayerAimIndicatorPresentationDefinition aimIndicator =
            new PlayerAimIndicatorPresentationDefinition();

        public int DefinitionId => definitionId;
        public string WeaponId => weaponId;
        public string DisplayName => displayName;
        public int MagazineCapacity => magazineCapacity;

        public int PrimaryIntervalTicks =>
            primarySkill == null ? 0 : primarySkill.SequenceCooldownTicks;

        public AttackQueryMode PrimaryQueryMode =>
            TryFindCompiledPayload(
                primarySkill,
                FpgPlayerSkillActionKind.PelletRay,
                FpgPlayerSkillActionKind.None,
                out FpgCompiledPlayerSkillAction payload)
                ? payload.QueryMode
                : AttackQueryMode.FirstSurfacePenetration;

        public int PrimaryAdditionalPenetrationCount =>
            TryFindCompiledPayload(
                primarySkill,
                FpgPlayerSkillActionKind.PelletRay,
                FpgPlayerSkillActionKind.None,
                out FpgCompiledPlayerSkillAction payload)
                ? payload.AdditionalPenetrationCount
                : 0;

        public SecondaryTriggerMode SecondaryTriggerMode =>
            secondarySkill == null
                ? SecondaryTriggerMode.ChargeRelease
                : secondarySkill.SecondaryTriggerMode;

        public AttackQueryMode SecondaryQueryMode =>
            TryFindCompiledPayload(
                secondarySkill,
                FpgPlayerSkillActionKind.AreaAtFirstSurface,
                FpgPlayerSkillActionKind.ProjectileAreaAtFirstSurface,
                out FpgCompiledPlayerSkillAction payload)
                ? payload.QueryMode
                : AttackQueryMode.AreaAtFirstSurface;

        public int SecondaryMinimumChargeTicks =>
            secondarySkill == null ? 0 : secondarySkill.MinimumChargeTicks;

        public int SecondaryAmmoCost => GetSequenceAmmoCost(
            secondarySkill,
            ResolveSecondarySequenceKind());

        public int SecondaryEnemyMaxImpactCount =>
            TryFindCompiledPayload(
                secondarySkill,
                FpgPlayerSkillActionKind.AreaAtFirstSurface,
                FpgPlayerSkillActionKind.ProjectileAreaAtFirstSurface,
                out FpgCompiledPlayerSkillAction payload)
                ? payload.AreaCombatantLimit
                : 0;

        public int SecondaryProjectileMaxImpactCount =>
            TryFindCompiledPayload(
                secondarySkill,
                FpgPlayerSkillActionKind.AreaAtFirstSurface,
                FpgPlayerSkillActionKind.ProjectileAreaAtFirstSurface,
                out FpgCompiledPlayerSkillAction payload)
                ? payload.AreaProjectileLimit
                : 0;

        public int ReloadDurationTicks => GetSequenceDuration(
            reloadSkill,
            FpgSkillSequenceKind.Execute);

        public FpgPlayerSkillDefinition PrimarySkill => primarySkill;
        public FpgPlayerSkillDefinition SecondarySkill => secondarySkill;
        public FpgPlayerSkillDefinition ReloadSkill => reloadSkill;

        public PlayerAimIndicatorPresentationDefinition AimIndicator =>
            aimIndicator;

        public bool TryValidatePresentation(out string error)
        {
            error = string.Empty;
            if (aimIndicator == null || !aimIndicator.TryValidate(out error))
            {
                error = string.IsNullOrEmpty(error)
                    ? "Weapon aim-indicator presentation is missing."
                    : "Weapon aim-indicator presentation is invalid: " + error;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryCreate(out WeaponDefinition definition, out string error)
        {
            return TryCreate(
                SecondaryTriggerMode,
                out definition,
                out error);
        }

        public bool TryCreate(
            SecondaryTriggerMode secondaryTriggerModeOverride,
            out WeaponDefinition definition,
            out string error)
        {
            definition = default(WeaponDefinition);
            if (string.IsNullOrWhiteSpace(weaponId) || string.IsNullOrWhiteSpace(displayName))
            {
                error = "Weapon definition requires stable ID and display name values.";
                return false;
            }

            if (!Enum.IsDefined(
                    typeof(SecondaryTriggerMode),
                    secondaryTriggerModeOverride))
            {
                error =
                    $"Weapon definition has invalid secondary trigger mode '{secondaryTriggerModeOverride}'.";
                return false;
            }

            if (!TryValidatePresentation(out error))
            {
                return false;
            }

            if (!TryCompileSkills(
                    out FpgCompiledPlayerSkillDefinition compiledPrimary,
                    out FpgCompiledPlayerSkillDefinition compiledSecondary,
                    out FpgCompiledPlayerSkillDefinition compiledReload,
                    out error))
            {
                return false;
            }

            if (!TryBuildWeaponProjection(
                    compiledPrimary,
                    compiledSecondary,
                    compiledReload,
                    secondaryTriggerModeOverride,
                    out WeaponProjection projection,
                    out error))
            {
                return false;
            }

            try
            {
                definition = new WeaponDefinition(
                    definitionId,
                    magazineCapacity,
                    projection.PrimaryAmmoCost,
                    new TickDuration(projection.PrimaryLockTicks),
                    projection.PrimaryPayload.Damage,
                    projection.SecondaryAmmoCost,
                    new TickDuration(SecondaryMinimumChargeTicks),
                    new TickDuration(projection.SecondaryLockTicks),
                    projection.SecondaryPayload.Damage,
                    new TickDuration(projection.ReloadLockTicks),
                    projection.SecondaryPayload.AreaCombatantLimit,
                    secondaryTriggerModeOverride,
                    projection.PrimaryPayload.QueryMode,
                    projection.PrimaryPayload.AdditionalPenetrationCount,
                    projection.SecondaryPayload.QueryMode,
                    projection.SecondaryPayload.AreaProjectileLimit,
                    projection.PrimaryPayload.AllowedTargetKinds,
                    projection.SecondaryPayload.AllowedTargetKinds,
                    projection.PrimaryPayload.PayloadCount,
                    projection.MaximumImpactCount);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TryCompileSkills(
            out FpgCompiledPlayerSkillDefinition compiledPrimary,
            out FpgCompiledPlayerSkillDefinition compiledSecondary,
            out FpgCompiledPlayerSkillDefinition compiledReload,
            out string error)
        {
            compiledPrimary = null;
            compiledSecondary = null;
            compiledReload = null;

            if (primarySkill == null || secondarySkill == null || reloadSkill == null)
            {
                error = "Weapon definition requires primary, secondary and reload skill assets.";
                return false;
            }

            if (!primarySkill.TryCompile(out compiledPrimary, out error))
            {
                error = "Primary skill is invalid: " + error;
                return false;
            }

            if (!secondarySkill.TryCompile(out compiledSecondary, out error))
            {
                error = "Secondary skill is invalid: " + error;
                return false;
            }

            if (!reloadSkill.TryCompile(out compiledReload, out error))
            {
                error = "Reload skill is invalid: " + error;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryBuildWeaponProjection(
            FpgCompiledPlayerSkillDefinition compiledPrimary,
            FpgCompiledPlayerSkillDefinition compiledSecondary,
            FpgCompiledPlayerSkillDefinition compiledReload,
            SecondaryTriggerMode secondaryTriggerModeOverride,
            out WeaponProjection projection,
            out string error)
        {
            projection = default(WeaponProjection);
            if (!compiledPrimary.Timeline.TryGetSequence(
                    FpgSkillSequenceKind.Execute,
                    out FpgCompiledSkillSequence primarySequence)
                || !compiledPrimary.TryGetSequenceSummary(
                    FpgSkillSequenceKind.Execute,
                    out FpgCompiledPlayerSkillSequenceSummary primarySummary))
            {
                error = "Primary skill requires an Execute sequence.";
                return false;
            }

            FpgSkillSequenceKind secondarySequenceKind =
                secondaryTriggerModeOverride == SecondaryTriggerMode.ChargeRelease
                    && compiledSecondary.Timeline.TryGetSequence(
                        FpgSkillSequenceKind.Release,
                        out _)
                        ? FpgSkillSequenceKind.Release
                        : FpgSkillSequenceKind.Execute;
            if (!compiledSecondary.Timeline.TryGetSequence(
                    secondarySequenceKind,
                    out FpgCompiledSkillSequence secondarySequence)
                || !compiledSecondary.TryGetSequenceSummary(
                    secondarySequenceKind,
                    out FpgCompiledPlayerSkillSequenceSummary secondarySummary))
            {
                error = "Secondary skill has no executable sequence for the selected trigger mode.";
                return false;
            }

            if (!compiledReload.Timeline.TryGetSequence(
                    FpgSkillSequenceKind.Execute,
                    out FpgCompiledSkillSequence reloadSequence)
                || !compiledReload.TryGetSequenceSummary(
                    FpgSkillSequenceKind.Execute,
                    out FpgCompiledPlayerSkillSequenceSummary reloadSummary))
            {
                error = "Reload skill requires an Execute sequence.";
                return false;
            }

            if (!TryValidateAttackSequence(
                    compiledPrimary,
                    primarySequence,
                    primarySummary,
                    FpgPlayerSkillActionKind.PelletRay,
                    FpgPlayerSkillActionKind.None,
                    "Primary",
                    out FpgCompiledPlayerSkillAction primaryPayload,
                    out error)
                || !TryValidateAttackSequence(
                    compiledSecondary,
                    secondarySequence,
                    secondarySummary,
                    FpgPlayerSkillActionKind.AreaAtFirstSurface,
                    FpgPlayerSkillActionKind.ProjectileAreaAtFirstSurface,
                    "Secondary",
                    out FpgCompiledPlayerSkillAction secondaryPayload,
                    out error))
            {
                return false;
            }

            if (reloadSummary.AttackEventCount != 0
                || reloadSummary.ReloadCommitEventCount <= 0
                || reloadSummary.TotalAmmoCost != 0)
            {
                error = "Reload Execute sequence must contain reload commits and no attack payloads.";
                return false;
            }

            if (primarySummary.TotalAmmoCost > magazineCapacity
                || secondarySummary.TotalAmmoCost > magazineCapacity)
            {
                error = "A complete attack sequence consumes more ammo than the shared magazine can hold.";
                return false;
            }

            if (compiledPrimary.MaximumPelletCount > WeaponDefinition.PrimaryPelletCount
                || compiledSecondary.MaximumPelletCount > WeaponDefinition.PrimaryPelletCount
                || compiledReload.MaximumPelletCount > WeaponDefinition.PrimaryPelletCount)
            {
                error = "Formal player pellet payloads exceed the fixed eight-pellet query capacity.";
                return false;
            }

            try
            {
                int primaryLockTicks = Math.Max(
                    1,
                    compiledPrimary.SequenceCooldownTicks);
                int secondaryLockTicks = Math.Max(
                    1,
                    compiledSecondary.SequenceCooldownTicks);
                int reloadLockTicks = checked(reloadSequence.DurationTicks + 1);
                int maximumImpactCount = Math.Max(
                    compiledPrimary.MaximumImpactCount,
                    Math.Max(
                        compiledSecondary.MaximumImpactCount,
                        compiledReload.MaximumImpactCount));

                projection = new WeaponProjection(
                    primaryPayload,
                    secondaryPayload,
                    primarySummary.TotalAmmoCost,
                    secondarySummary.TotalAmmoCost,
                    primaryLockTicks,
                    secondaryLockTicks,
                    reloadLockTicks,
                    maximumImpactCount);
                error = string.Empty;
                return true;
            }
            catch (OverflowException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool TryValidateAttackSequence(
            FpgCompiledPlayerSkillDefinition definition,
            FpgCompiledSkillSequence sequence,
            FpgCompiledPlayerSkillSequenceSummary summary,
            FpgPlayerSkillActionKind requiredKind,
            FpgPlayerSkillActionKind alternateKind,
            string label,
            out FpgCompiledPlayerSkillAction representativePayload,
            out string error)
        {
            representativePayload = default(FpgCompiledPlayerSkillAction);
            if (summary.AttackEventCount <= 0
                || summary.ReloadCommitEventCount != 0
                || summary.TotalAmmoCost <= 0)
            {
                error = label + " sequence requires attack payloads and cannot contain reload commits.";
                return false;
            }

            bool found = false;
            for (int eventIndex = 0; eventIndex < sequence.EventCount; eventIndex++)
            {
                FpgCompiledSkillEvent skillEvent = sequence.GetEvent(eventIndex);
                if (skillEvent.Kind != FpgSkillEventKind.GameplayAction)
                {
                    continue;
                }

                if (!definition.TryResolveAction(
                        skillEvent,
                        out FpgCompiledPlayerSkillAction payload)
                    || (payload.Kind != requiredKind
                        && payload.Kind != alternateKind))
                {
                    error = label + " sequence contains an incompatible gameplay payload.";
                    return false;
                }

                if (!found)
                {
                    representativePayload = payload;
                    found = true;
                }
            }

            error = found
                ? string.Empty
                : label + " sequence has no gameplay payload.";
            return found;
        }

        private readonly struct WeaponProjection
        {
            public WeaponProjection(
                FpgCompiledPlayerSkillAction primaryPayload,
                FpgCompiledPlayerSkillAction secondaryPayload,
                int primaryAmmoCost,
                int secondaryAmmoCost,
                int primaryLockTicks,
                int secondaryLockTicks,
                int reloadLockTicks,
                int maximumImpactCount)
            {
                PrimaryPayload = primaryPayload;
                SecondaryPayload = secondaryPayload;
                PrimaryAmmoCost = primaryAmmoCost;
                SecondaryAmmoCost = secondaryAmmoCost;
                PrimaryLockTicks = primaryLockTicks;
                SecondaryLockTicks = secondaryLockTicks;
                ReloadLockTicks = reloadLockTicks;
                MaximumImpactCount = maximumImpactCount;
            }

            public FpgCompiledPlayerSkillAction PrimaryPayload { get; }
            public FpgCompiledPlayerSkillAction SecondaryPayload { get; }
            public int PrimaryAmmoCost { get; }
            public int SecondaryAmmoCost { get; }
            public int PrimaryLockTicks { get; }
            public int SecondaryLockTicks { get; }
            public int ReloadLockTicks { get; }
            public int MaximumImpactCount { get; }
        }


        private FpgSkillSequenceKind ResolveSecondarySequenceKind()
        {
            return SecondaryTriggerMode == SecondaryTriggerMode.ChargeRelease
                ? FpgSkillSequenceKind.Release
                : FpgSkillSequenceKind.Execute;
        }

        private static bool TryFindCompiledPayload(
            FpgPlayerSkillDefinition definition,
            FpgPlayerSkillActionKind requiredKind,
            FpgPlayerSkillActionKind alternateKind,
            out FpgCompiledPlayerSkillAction payload)
        {
            payload = default(FpgCompiledPlayerSkillAction);
            if (definition == null
                || !definition.TryCompile(
                    out FpgCompiledPlayerSkillDefinition compiled,
                    out _))
            {
                return false;
            }

            for (int sequenceIndex = 0;
                sequenceIndex < compiled.Timeline.SequenceCount;
                sequenceIndex++)
            {
                FpgCompiledSkillSequence sequence =
                    compiled.Timeline.GetSequence(sequenceIndex);
                for (int eventIndex = 0;
                    eventIndex < sequence.EventCount;
                    eventIndex++)
                {
                    FpgCompiledSkillEvent skillEvent =
                        sequence.GetEvent(eventIndex);
                    if (skillEvent.Kind == FpgSkillEventKind.GameplayAction
                        && compiled.TryResolveAction(skillEvent, out payload)
                        && (payload.Kind == requiredKind
                            || payload.Kind == alternateKind))
                    {
                        return true;
                    }
                }
            }

            payload = default(FpgCompiledPlayerSkillAction);
            return false;
        }

        private static int GetSequenceAmmoCost(
            FpgPlayerSkillDefinition definition,
            FpgSkillSequenceKind kind)
        {
            if (definition != null
                && definition.TryCompile(
                    out FpgCompiledPlayerSkillDefinition compiled,
                    out _)
                && compiled.TryGetSequenceSummary(
                    kind,
                    out FpgCompiledPlayerSkillSequenceSummary summary))
            {
                return summary.TotalAmmoCost;
            }

            return 0;
        }

        private static int GetSequenceDuration(
            FpgPlayerSkillDefinition definition,
            FpgSkillSequenceKind kind)
        {
            if (definition == null)
            {
                return 0;
            }

            IReadOnlyList<FpgSkillSequenceDefinition> sequences =
                definition.Sequences;
            for (int index = 0; index < sequences.Count; index++)
            {
                FpgSkillSequenceDefinition sequence = sequences[index];
                if (sequence != null && sequence.Kind == kind)
                {
                    return sequence.DurationTicks;
                }
            }

            return 0;
        }
}
}
