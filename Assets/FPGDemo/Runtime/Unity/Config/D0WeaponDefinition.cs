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
            FindPayload(primarySkill, FpgPlayerSkillPayloadKind.PelletRay)
                ?.QueryMode
            ?? AttackQueryMode.FirstSurfacePenetration;

        public int PrimaryAdditionalPenetrationCount =>
            FindPayload(primarySkill, FpgPlayerSkillPayloadKind.PelletRay)
                ?.AdditionalPenetrationCount
            ?? 0;

        public SecondaryTriggerMode SecondaryTriggerMode =>
            secondarySkill == null
                ? SecondaryTriggerMode.ChargeRelease
                : secondarySkill.SecondaryTriggerMode;

        public AttackQueryMode SecondaryQueryMode =>
            FindPayload(
                    secondarySkill,
                    FpgPlayerSkillPayloadKind.AreaAtFirstSurface)
                ?.QueryMode
            ?? AttackQueryMode.AreaAtFirstSurface;

        public int SecondaryMinimumChargeTicks =>
            secondarySkill == null ? 0 : secondarySkill.MinimumChargeTicks;

        public int SecondaryAmmoCost => GetSequenceAmmoCost(
            secondarySkill,
            ResolveSecondarySequenceKind());

        public int SecondaryEnemyMaxImpactCount =>
            FindPayload(
                    secondarySkill,
                    FpgPlayerSkillPayloadKind.AreaAtFirstSurface)
                ?.AreaCombatantLimit
            ?? 0;

        public int SecondaryProjectileMaxImpactCount =>
            FindPayload(
                    secondarySkill,
                    FpgPlayerSkillPayloadKind.AreaAtFirstSurface)
                ?.AreaProjectileLimit
            ?? 0;

        public int ReloadDurationTicks => GetSequenceDuration(
            reloadSkill,
            FpgSkillSequenceKind.Execute);

        public FpgPlayerSkillDefinition PrimarySkill => primarySkill;
        public FpgPlayerSkillDefinition SecondarySkill => secondarySkill;
        public FpgPlayerSkillDefinition ReloadSkill => reloadSkill;

        public D0WeaponShotPresentationDefinition PrimaryPresentation =>
            primarySkill == null ? null : primarySkill.ShotPresentation;

        public D0WeaponSecondaryPresentationDefinition SecondaryPresentation =>
            secondarySkill == null ? null : secondarySkill.SecondaryPresentation;

        public D0WeaponReloadPresentationDefinition ReloadPresentation =>
            reloadSkill == null ? null : reloadSkill.ReloadPresentation;

        public PlayerAimIndicatorPresentationDefinition AimIndicator =>
            aimIndicator;

        public bool TryValidatePresentation(out string error)
        {
            error = string.Empty;
            D0WeaponShotPresentationDefinition primary = PrimaryPresentation;
            if (primary == null || !primary.TryValidate(out error))
            {
                error = string.IsNullOrEmpty(error)
                    ? "Primary skill presentation is missing."
                    : "Primary skill presentation is invalid: " + error;
                return false;
            }

            D0WeaponSecondaryPresentationDefinition secondary =
                SecondaryPresentation;
            if (secondary == null || !secondary.TryValidate(out error))
            {
                error = string.IsNullOrEmpty(error)
                    ? "Secondary skill presentation is missing."
                    : "Secondary skill presentation is invalid: " + error;
                return false;
            }

            D0WeaponReloadPresentationDefinition reload = ReloadPresentation;
            if (reload == null || !reload.TryValidate(out error))
            {
                error = string.IsNullOrEmpty(error)
                    ? "Reload skill presentation is missing."
                    : "Reload skill presentation is invalid: " + error;
                return false;
            }

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
                    SecondaryTriggerMode,
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
                SecondaryTriggerMode == SecondaryTriggerMode.ChargeRelease
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
                error = "Secondary skill has no executable release sequence.";
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
                    FpgPlayerSkillPayloadKind.PelletRay,
                    "Primary",
                    out FpgCompiledPlayerSkillPayloadSlot primaryPayload,
                    out error)
                || !TryValidateAttackSequence(
                    compiledSecondary,
                    secondarySequence,
                    secondarySummary,
                    FpgPlayerSkillPayloadKind.AreaAtFirstSurface,
                    "Secondary",
                    out FpgCompiledPlayerSkillPayloadSlot secondaryPayload,
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
                int primaryLockTicks = ComputeProjectedLockTicks(
                    primarySequence,
                    primarySummary,
                    compiledPrimary.SequenceCooldownTicks);
                int secondaryLockTicks = ComputeProjectedLockTicks(
                    secondarySequence,
                    secondarySummary,
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
            FpgPlayerSkillPayloadKind requiredKind,
            string label,
            out FpgCompiledPlayerSkillPayloadSlot representativePayload,
            out string error)
        {
            representativePayload = default(FpgCompiledPlayerSkillPayloadSlot);
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
                if (skillEvent.Kind != FpgSkillEventKind.GameplayPayload)
                {
                    continue;
                }

                if (!definition.TryGetPayloadSlot(
                        skillEvent.PayloadSlotId,
                        out FpgCompiledPlayerSkillPayloadSlot payload)
                    || payload.Kind != requiredKind)
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

        private static int ComputeProjectedLockTicks(
            FpgCompiledSkillSequence sequence,
            FpgCompiledPlayerSkillSequenceSummary summary,
            int cooldownTicks)
        {
            int sequenceUnlock = checked(sequence.DurationTicks + 1);
            int cooldownUnlock = summary.LastAttackTick < 0
                ? 0
                : checked(summary.LastAttackTick + cooldownTicks);
            return Math.Max(1, Math.Max(sequenceUnlock, cooldownUnlock));
        }

        private readonly struct WeaponProjection
        {
            public WeaponProjection(
                FpgCompiledPlayerSkillPayloadSlot primaryPayload,
                FpgCompiledPlayerSkillPayloadSlot secondaryPayload,
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

            public FpgCompiledPlayerSkillPayloadSlot PrimaryPayload { get; }
            public FpgCompiledPlayerSkillPayloadSlot SecondaryPayload { get; }
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

        private static FpgPlayerSkillPayloadSlot FindPayload(
            FpgPlayerSkillDefinition definition,
            FpgPlayerSkillPayloadKind kind)
        {
            if (definition == null)
            {
                return null;
            }

            IReadOnlyList<FpgPlayerSkillPayloadSlot> slots =
                definition.PayloadSlots;
            for (int index = 0; index < slots.Count; index++)
            {
                FpgPlayerSkillPayloadSlot slot = slots[index];
                if (slot != null && slot.Kind == kind)
                {
                    return slot;
                }
            }

            return null;
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
