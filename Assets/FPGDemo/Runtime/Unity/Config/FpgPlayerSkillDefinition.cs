using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum FpgPlayerSkillPayloadKind
    {
        None = 0,
        PelletRay,
        AreaAtFirstSurface,
        ReloadCommit
    }

    [Serializable]
    public sealed class FpgPlayerSkillPayloadSlot
    {
        [SerializeField]
        private string slotId = "payload.primary";

        [SerializeField]
        private string displayName = "Primary Payload";

        [SerializeField]
        private FpgPlayerSkillPayloadKind kind =
            FpgPlayerSkillPayloadKind.PelletRay;

        [SerializeField, Min(0)]
        private int ammoCost = 1;

        [SerializeField, Min(0)]
        private int baseDamage = 4;

        [SerializeField, Min(0)]
        private int breakDamage = 4;

        [SerializeField, Min(0)]
        private int weakpointDamageMultiplierBasisPoints = 12000;

        [SerializeField, Min(0)]
        private int weakpointBreakMultiplierBasisPoints = 25000;

        [SerializeField]
        private AttackQueryMode queryMode =
            AttackQueryMode.FirstSurfacePenetration;

        [SerializeField, Min(1)]
        private int pelletCount = WeaponDefinition.PrimaryPelletCount;

        [SerializeField, Min(0)]
        private int additionalPenetrationCount;

        [SerializeField, Min(1)]
        private int areaCombatantLimit = 4;

        [SerializeField, Min(0)]
        private int areaProjectileLimit =
            WeaponDefinition.DefaultSecondaryAreaProjectileLimit;

        [SerializeField]
        private AttackTargetKinds allowedTargetKinds =
            WeaponDefinition.PlayerAttackTargetKinds;

        public string SlotId => slotId;
        public string DisplayName => displayName;
        public FpgPlayerSkillPayloadKind Kind => kind;
        public int AmmoCost => ammoCost;
        public int BaseDamage => baseDamage;
        public int BreakDamage => breakDamage;
        public int WeakpointDamageMultiplierBasisPoints =>
            weakpointDamageMultiplierBasisPoints;
        public int WeakpointBreakMultiplierBasisPoints =>
            weakpointBreakMultiplierBasisPoints;
        public AttackQueryMode QueryMode => queryMode;
        public int PelletCount => pelletCount;
        public int AdditionalPenetrationCount => additionalPenetrationCount;
        public int AreaCombatantLimit => areaCombatantLimit;
        public int AreaProjectileLimit => areaProjectileLimit;
        public AttackTargetKinds AllowedTargetKinds => allowedTargetKinds;

        public bool TryValidate(out string error)
        {
            if (!FpgSkillStableId.IsValid(slotId)
                || string.IsNullOrWhiteSpace(displayName)
                || !Enum.IsDefined(typeof(FpgPlayerSkillPayloadKind), kind)
                || kind == FpgPlayerSkillPayloadKind.None)
            {
                error = "Player skill payload requires a stable slot ID, display name and valid kind.";
                return false;
            }

            if (ammoCost < 0
                || baseDamage < 0
                || breakDamage < 0
                || weakpointDamageMultiplierBasisPoints < 0
                || weakpointBreakMultiplierBasisPoints < 0)
            {
                error = $"Player skill payload '{slotId}' has invalid cost or damage values.";
                return false;
            }

            switch (kind)
            {
                case FpgPlayerSkillPayloadKind.PelletRay:
                    return TryValidatePelletRay(out error);

                case FpgPlayerSkillPayloadKind.AreaAtFirstSurface:
                    return TryValidateArea(out error);

                case FpgPlayerSkillPayloadKind.ReloadCommit:
                    return TryValidateReload(out error);

                default:
                    error = $"Player skill payload '{slotId}' has an unsupported kind.";
                    return false;
            }
        }

        internal FpgCompiledPlayerSkillPayloadSlot Compile()
        {
            QueryPolicy queryPolicy;
            int payloadCount;
            int maxImpactCount;
            int compiledAdditionalPenetration;
            int compiledAreaCombatantLimit;
            int compiledAreaProjectileLimit;
            AttackTargetKinds compiledTargetKinds;

            switch (kind)
            {
                case FpgPlayerSkillPayloadKind.PelletRay:
                    queryPolicy = QueryPolicy.PelletRays;
                    payloadCount = pelletCount;
                    maxImpactCount = checked(
                        pelletCount * (additionalPenetrationCount + 1));
                    compiledAdditionalPenetration = additionalPenetrationCount;
                    compiledAreaCombatantLimit = 0;
                    compiledAreaProjectileLimit = 0;
                    compiledTargetKinds = allowedTargetKinds;
                    break;

                case FpgPlayerSkillPayloadKind.AreaAtFirstSurface:
                    queryPolicy = QueryPolicy.DirectThenArea;
                    payloadCount = 1;
                    maxImpactCount = checked(
                        areaCombatantLimit + areaProjectileLimit);
                    compiledAdditionalPenetration = 0;
                    compiledAreaCombatantLimit = areaCombatantLimit;
                    compiledAreaProjectileLimit = areaProjectileLimit;
                    compiledTargetKinds = allowedTargetKinds;
                    break;

                case FpgPlayerSkillPayloadKind.ReloadCommit:
                    queryPolicy = QueryPolicy.None;
                    payloadCount = 0;
                    maxImpactCount = 0;
                    compiledAdditionalPenetration = 0;
                    compiledAreaCombatantLimit = 0;
                    compiledAreaProjectileLimit = 0;
                    compiledTargetKinds = AttackTargetKinds.None;
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported player skill payload kind '{kind}'.");
            }

            return new FpgCompiledPlayerSkillPayloadSlot(
                FpgSkillStableId.CompilePayloadSlot(slotId),
                kind,
                ammoCost,
                new DamageSpec(
                    baseDamage,
                    breakDamage,
                    weakpointDamageMultiplierBasisPoints,
                    weakpointBreakMultiplierBasisPoints),
                queryPolicy,
                queryMode,
                payloadCount,
                maxImpactCount,
                compiledAdditionalPenetration,
                compiledAreaCombatantLimit,
                compiledAreaProjectileLimit,
                compiledTargetKinds);
        }

        private bool TryValidatePelletRay(out string error)
        {
            if (ammoCost <= 0
                || queryMode != AttackQueryMode.FirstSurfacePenetration
                || pelletCount <= 0
                || additionalPenetrationCount < 0
                || additionalPenetrationCount
                    > (int.MaxValue / pelletCount) - 1
                || !IsValidPlayerTargetKinds(allowedTargetKinds))
            {
                error = $"Pellet payload '{slotId}' has invalid ammo, query or capacity values.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateArea(out string error)
        {
            if (ammoCost <= 0
                || queryMode != AttackQueryMode.AreaAtFirstSurface
                || areaCombatantLimit <= 0
                || areaProjectileLimit < 0
                || areaCombatantLimit > int.MaxValue - areaProjectileLimit
                || !IsValidPlayerTargetKinds(allowedTargetKinds))
            {
                error = $"Area payload '{slotId}' has invalid ammo, query or capacity values.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateReload(out string error)
        {
            if (ammoCost != 0
                || baseDamage != 0
                || breakDamage != 0
                || queryMode != AttackQueryMode.Legacy
                || allowedTargetKinds != AttackTargetKinds.None)
            {
                error = $"Reload payload '{slotId}' must not consume ammo, deal damage or query targets.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsValidPlayerTargetKinds(AttackTargetKinds value)
        {
            return value != AttackTargetKinds.None
                && (value & ~WeaponDefinition.PlayerAttackTargetKinds)
                    == AttackTargetKinds.None;
        }
    }

    public readonly struct FpgCompiledPlayerSkillPayloadSlot
    {
        public FpgCompiledPlayerSkillPayloadSlot(
            int slotId,
            FpgPlayerSkillPayloadKind kind,
            int ammoCost,
            DamageSpec damage,
            QueryPolicy queryPolicy,
            AttackQueryMode queryMode,
            int payloadCount,
            int maxImpactCount,
            int additionalPenetrationCount,
            int areaCombatantLimit,
            int areaProjectileLimit,
            AttackTargetKinds allowedTargetKinds)
        {
            if (slotId <= 0
                || !Enum.IsDefined(typeof(FpgPlayerSkillPayloadKind), kind)
                || kind == FpgPlayerSkillPayloadKind.None
                || ammoCost < 0
                || payloadCount < 0
                || maxImpactCount < 0
                || additionalPenetrationCount < 0
                || areaCombatantLimit < 0
                || areaProjectileLimit < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotId));
            }

            SlotId = slotId;
            Kind = kind;
            AmmoCost = ammoCost;
            Damage = damage;
            QueryPolicy = queryPolicy;
            QueryMode = queryMode;
            PayloadCount = payloadCount;
            MaxImpactCount = maxImpactCount;
            AdditionalPenetrationCount = additionalPenetrationCount;
            AreaCombatantLimit = areaCombatantLimit;
            AreaProjectileLimit = areaProjectileLimit;
            AllowedTargetKinds = allowedTargetKinds;
        }

        public int SlotId { get; }
        public FpgPlayerSkillPayloadKind Kind { get; }
        public int AmmoCost { get; }
        public DamageSpec Damage { get; }
        public QueryPolicy QueryPolicy { get; }
        public AttackQueryMode QueryMode { get; }
        public int PayloadCount { get; }
        public int MaxImpactCount { get; }
        public int AdditionalPenetrationCount { get; }
        public int AreaCombatantLimit { get; }
        public int AreaProjectileLimit { get; }
        public AttackTargetKinds AllowedTargetKinds { get; }
    }

    public readonly struct FpgCompiledPlayerSkillSequenceSummary
    {
        public FpgCompiledPlayerSkillSequenceSummary(
            FpgSkillSequenceKind kind,
            int totalAmmoCost,
            int lastAttackTick,
            int maximumImpactCount,
            int maximumPelletCount,
            int attackEventCount,
            int reloadCommitEventCount)
        {
            Kind = kind;
            TotalAmmoCost = totalAmmoCost;
            LastAttackTick = lastAttackTick;
            MaximumImpactCount = maximumImpactCount;
            MaximumPelletCount = maximumPelletCount;
            AttackEventCount = attackEventCount;
            ReloadCommitEventCount = reloadCommitEventCount;
        }

        public FpgSkillSequenceKind Kind { get; }
        public int TotalAmmoCost { get; }
        public int LastAttackTick { get; }
        public int MaximumImpactCount { get; }
        public int MaximumPelletCount { get; }
        public int AttackEventCount { get; }
        public int ReloadCommitEventCount { get; }
        public bool HasAttack => AttackEventCount > 0;
        public bool HasReloadCommit => ReloadCommitEventCount > 0;
    }

    public sealed class FpgCompiledPlayerSkillDefinition
    {
        private readonly FpgCompiledPlayerSkillPayloadSlot[] payloadSlots;
        private readonly FpgCompiledPlayerSkillSequenceSummary[] sequenceSummaries;

        public FpgCompiledPlayerSkillDefinition(
            FpgCompiledSkillDefinition timeline,
            int sequenceCooldownTicks,
            FpgCompiledPlayerSkillPayloadSlot[] payloadSlots)
        {
            Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
            if (sequenceCooldownTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequenceCooldownTicks));
            }

            if (payloadSlots == null || payloadSlots.Length == 0)
            {
                throw new ArgumentException(
                    "Compiled player skill requires payload slots.",
                    nameof(payloadSlots));
            }

            this.payloadSlots = CopyAndSortPayloadSlots(payloadSlots);
            SequenceCooldownTicks = sequenceCooldownTicks;
            sequenceSummaries = BuildSequenceSummaries(timeline);
            MaximumImpactCount = ComputeMaximumImpactCount(this.payloadSlots);
            MaximumPelletCount = ComputeMaximumPelletCount(this.payloadSlots);
            GameplayHash = ComputeGameplayHash(
                timeline,
                sequenceCooldownTicks,
                this.payloadSlots);
        }

        public FpgCompiledSkillDefinition Timeline { get; }
        public int SequenceCooldownTicks { get; }
        public IReadOnlyList<FpgCompiledPlayerSkillPayloadSlot> PayloadSlots =>
            payloadSlots;
        public int PayloadSlotCount => payloadSlots.Length;
        public IReadOnlyList<FpgCompiledPlayerSkillSequenceSummary> SequenceSummaries =>
            sequenceSummaries;
        public int MaximumImpactCount { get; }
        public int MaximumPelletCount { get; }
        public ulong GameplayHash { get; }

        public int ExecuteAmmoCost => TryGetSequenceSummary(
            FpgSkillSequenceKind.Execute,
            out FpgCompiledPlayerSkillSequenceSummary summary)
                ? summary.TotalAmmoCost
                : 0;

        public int ExecuteLastAttackTick => TryGetSequenceSummary(
            FpgSkillSequenceKind.Execute,
            out FpgCompiledPlayerSkillSequenceSummary summary)
                ? summary.LastAttackTick
                : -1;

        public int ReleaseAmmoCost => TryGetSequenceSummary(
            FpgSkillSequenceKind.Release,
            out FpgCompiledPlayerSkillSequenceSummary summary)
                ? summary.TotalAmmoCost
                : 0;

        public int ReleaseLastAttackTick => TryGetSequenceSummary(
            FpgSkillSequenceKind.Release,
            out FpgCompiledPlayerSkillSequenceSummary summary)
                ? summary.LastAttackTick
                : -1;

        public bool TryGetPayloadSlot(
            int slotId,
            out FpgCompiledPlayerSkillPayloadSlot payloadSlot)
        {
            for (int index = 0; index < payloadSlots.Length; index++)
            {
                if (payloadSlots[index].SlotId == slotId)
                {
                    payloadSlot = payloadSlots[index];
                    return true;
                }
            }

            payloadSlot = default(FpgCompiledPlayerSkillPayloadSlot);
            return false;
        }

        public bool TryGetSequenceSummary(
            FpgSkillSequenceKind kind,
            out FpgCompiledPlayerSkillSequenceSummary summary)
        {
            for (int index = 0; index < sequenceSummaries.Length; index++)
            {
                if (sequenceSummaries[index].Kind == kind)
                {
                    summary = sequenceSummaries[index];
                    return true;
                }
            }

            summary = default(FpgCompiledPlayerSkillSequenceSummary);
            return false;
        }

        private FpgCompiledPlayerSkillSequenceSummary[] BuildSequenceSummaries(
            FpgCompiledSkillDefinition timeline)
        {
            FpgCompiledPlayerSkillSequenceSummary[] summaries =
                new FpgCompiledPlayerSkillSequenceSummary[timeline.SequenceCount];
            for (int sequenceIndex = 0;
                sequenceIndex < timeline.SequenceCount;
                sequenceIndex++)
            {
                FpgCompiledSkillSequence sequence =
                    timeline.GetSequence(sequenceIndex);
                int totalAmmoCost = 0;
                int lastAttackTick = -1;
                int maximumImpactCount = 0;
                int maximumPelletCount = 0;
                int attackEventCount = 0;
                int reloadCommitEventCount = 0;

                for (int eventIndex = 0;
                    eventIndex < sequence.EventCount;
                    eventIndex++)
                {
                    FpgCompiledSkillEvent skillEvent =
                        sequence.GetEvent(eventIndex);
                    if (skillEvent.Kind != FpgSkillEventKind.GameplayPayload)
                    {
                        continue;
                    }

                    if (!TryGetPayloadSlot(
                            skillEvent.PayloadSlotId,
                            out FpgCompiledPlayerSkillPayloadSlot payload))
                    {
                        throw new ArgumentException(
                            "Compiled timeline references a missing player payload slot.",
                            nameof(timeline));
                    }

                    totalAmmoCost = checked(totalAmmoCost + payload.AmmoCost);
                    if (payload.Kind == FpgPlayerSkillPayloadKind.ReloadCommit)
                    {
                        reloadCommitEventCount++;
                        continue;
                    }

                    attackEventCount++;
                    lastAttackTick = Math.Max(lastAttackTick, skillEvent.Tick);
                    maximumImpactCount = Math.Max(
                        maximumImpactCount,
                        payload.MaxImpactCount);
                    maximumPelletCount = Math.Max(
                        maximumPelletCount,
                        payload.QueryPolicy == QueryPolicy.PelletRays
                            ? payload.PayloadCount
                            : 0);
                }

                summaries[sequenceIndex] =
                    new FpgCompiledPlayerSkillSequenceSummary(
                        sequence.Kind,
                        totalAmmoCost,
                        lastAttackTick,
                        maximumImpactCount,
                        maximumPelletCount,
                        attackEventCount,
                        reloadCommitEventCount);
            }

            return summaries;
        }

        private static FpgCompiledPlayerSkillPayloadSlot[] CopyAndSortPayloadSlots(
            FpgCompiledPlayerSkillPayloadSlot[] source)
        {
            FpgCompiledPlayerSkillPayloadSlot[] copy =
                new FpgCompiledPlayerSkillPayloadSlot[source.Length];
            Array.Copy(source, copy, source.Length);
            for (int index = 1; index < copy.Length; index++)
            {
                FpgCompiledPlayerSkillPayloadSlot value = copy[index];
                int insertionIndex = index - 1;
                while (insertionIndex >= 0
                    && copy[insertionIndex].SlotId > value.SlotId)
                {
                    copy[insertionIndex + 1] = copy[insertionIndex];
                    insertionIndex--;
                }

                copy[insertionIndex + 1] = value;
            }

            for (int index = 1; index < copy.Length; index++)
            {
                if (copy[index - 1].SlotId == copy[index].SlotId)
                {
                    throw new ArgumentException(
                        "Compiled player skill repeats a payload slot ID.",
                        nameof(source));
                }
            }

            return copy;
        }

        private static int ComputeMaximumImpactCount(
            FpgCompiledPlayerSkillPayloadSlot[] values)
        {
            int maximum = 0;
            for (int index = 0; index < values.Length; index++)
            {
                maximum = Math.Max(maximum, values[index].MaxImpactCount);
            }

            return maximum;
        }

        private static int ComputeMaximumPelletCount(
            FpgCompiledPlayerSkillPayloadSlot[] values)
        {
            int maximum = 0;
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index].QueryPolicy == QueryPolicy.PelletRays)
                {
                    maximum = Math.Max(maximum, values[index].PayloadCount);
                }
            }

            return maximum;
        }

        private static ulong ComputeGameplayHash(
            FpgCompiledSkillDefinition timeline,
            int sequenceCooldownTicks,
            FpgCompiledPlayerSkillPayloadSlot[] values)
        {
            ulong hash = StableHash.Mix(0x4650475F50534B31UL);
            hash = StableHash.Append(hash, timeline.GameplayHash);
            hash = StableHash.Append(
                hash,
                unchecked((ulong)sequenceCooldownTicks));
            hash = StableHash.Append(hash, unchecked((ulong)values.Length));

            for (int index = 0; index < values.Length; index++)
            {
                FpgCompiledPlayerSkillPayloadSlot payload = values[index];
                hash = StableHash.Append(hash, unchecked((ulong)payload.SlotId));
                hash = StableHash.Append(hash, unchecked((ulong)(int)payload.Kind));
                hash = StableHash.Append(hash, unchecked((ulong)payload.AmmoCost));
                hash = StableHash.Append(hash, unchecked((ulong)payload.Damage.BaseDamage));
                hash = StableHash.Append(hash, unchecked((ulong)payload.Damage.BreakDamage));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)payload.Damage.WeakpointDamageMultiplierBasisPoints));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)payload.Damage.WeakpointBreakMultiplierBasisPoints));
                hash = StableHash.Append(hash, unchecked((ulong)(int)payload.QueryPolicy));
                hash = StableHash.Append(hash, unchecked((ulong)(int)payload.QueryMode));
                hash = StableHash.Append(hash, unchecked((ulong)payload.PayloadCount));
                hash = StableHash.Append(hash, unchecked((ulong)payload.MaxImpactCount));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)payload.AdditionalPenetrationCount));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)payload.AreaCombatantLimit));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)payload.AreaProjectileLimit));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)(int)payload.AllowedTargetKinds));
            }

            return hash;
        }
    }

    [CreateAssetMenu(
        fileName = "FpgPlayerSkillDefinition",
        menuName = "FPG Demo/Skills/Player Skill")]
    public sealed class FpgPlayerSkillDefinition : FpgSkillTimelineDefinition
    {
        [Header("Player Skill Activation")]
        [SerializeField]
        private SecondaryTriggerMode secondaryTriggerMode =
            SecondaryTriggerMode.ChargeRelease;

        [SerializeField, Min(0)]
        private int minimumChargeTicks;

        [Header("Player Skill Presentation")]
        [SerializeField]
        private D0WeaponShotPresentationDefinition shotPresentation =
            D0WeaponShotPresentationDefinition.CreatePrimaryDefaults();

        [SerializeField]
        private D0WeaponSecondaryPresentationDefinition secondaryPresentation =
            D0WeaponSecondaryPresentationDefinition.CreateDefaults();

        [SerializeField]
        private D0WeaponReloadPresentationDefinition reloadPresentation =
            new D0WeaponReloadPresentationDefinition();

        [SerializeField, Min(0)]
        private int sequenceCooldownTicks;

        [SerializeField]
        private FpgPlayerSkillPayloadSlot[] payloadSlots =
        {
            new FpgPlayerSkillPayloadSlot()
        };

        public SecondaryTriggerMode SecondaryTriggerMode =>
            secondaryTriggerMode;
        public int MinimumChargeTicks => minimumChargeTicks;
        public D0WeaponShotPresentationDefinition ShotPresentation =>
            shotPresentation;
        public D0WeaponSecondaryPresentationDefinition SecondaryPresentation =>
            secondaryPresentation;
        public D0WeaponReloadPresentationDefinition ReloadPresentation =>
            reloadPresentation;
        public int SequenceCooldownTicks => sequenceCooldownTicks;
        public IReadOnlyList<FpgPlayerSkillPayloadSlot> PayloadSlots =>
            payloadSlots ?? Array.Empty<FpgPlayerSkillPayloadSlot>();

        public bool TryCompile(
            out FpgCompiledPlayerSkillDefinition definition,
            out string error)
        {
            definition = null;
            if (!base.TryCompile(
                    out FpgCompiledSkillDefinition timeline,
                    out error))
            {
                return false;
            }

            try
            {
                FpgPlayerSkillPayloadSlot[] values =
                    payloadSlots ?? Array.Empty<FpgPlayerSkillPayloadSlot>();
                FpgCompiledPlayerSkillPayloadSlot[] compiled =
                    new FpgCompiledPlayerSkillPayloadSlot[values.Length];
                for (int index = 0; index < values.Length; index++)
                {
                    compiled[index] = values[index].Compile();
                }

                definition = new FpgCompiledPlayerSkillDefinition(
                    timeline,
                    sequenceCooldownTicks,
                    compiled);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidOperationException
                || exception is OverflowException)
            {
                definition = null;
                error = exception.Message;
                return false;
            }
        }

        protected override bool TryValidatePayloadSlots(out string error)
        {
            FpgPlayerSkillPayloadSlot[] values =
                payloadSlots ?? Array.Empty<FpgPlayerSkillPayloadSlot>();
            if (values.Length == 0)
            {
                error = $"Player skill '{SkillId}' requires at least one payload slot.";
                return false;
            }

            HashSet<string> slotIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> compiledSlotIds = new HashSet<int>();
            for (int index = 0; index < values.Length; index++)
            {
                FpgPlayerSkillPayloadSlot value = values[index];
                if (value == null)
                {
                    error = $"Player skill '{SkillId}' has a missing payload slot at index {index}.";
                    return false;
                }

                if (!value.TryValidate(out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = $"Player skill '{SkillId}' has an invalid payload slot at index {index}.";
                    }

                    return false;
                }

                int compiledId = FpgSkillStableId.CompilePayloadSlot(value.SlotId);
                if (!slotIds.Add(value.SlotId) || !compiledSlotIds.Add(compiledId))
                {
                    error = $"Player skill '{SkillId}' repeats payload slot '{value.SlotId}' or has a stable-ID collision.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        protected override bool ContainsPayloadSlot(string payloadSlotId)
        {
            FpgPlayerSkillPayloadSlot[] values =
                payloadSlots ?? Array.Empty<FpgPlayerSkillPayloadSlot>();
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] != null
                    && string.Equals(
                        values[index].SlotId,
                        payloadSlotId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        protected override bool TryValidateDefinition(out string error)
        {
            if (sequenceCooldownTicks < 0)
            {
                error = $"Player skill '{SkillId}' has a negative sequence cooldown.";
                return false;
            }

            if (!Enum.IsDefined(
                    typeof(SecondaryTriggerMode),
                    secondaryTriggerMode)
                || minimumChargeTicks < 0)
            {
                error = $"Player skill '{SkillId}' has invalid activation metadata.";
                return false;
            }

            bool hasGameplayEvent = false;
            bool hasPelletPayload = false;
            bool hasAreaPayload = false;
            bool hasReloadPayload = false;
            for (int sequenceIndex = 0;
                sequenceIndex < Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence = Sequences[sequenceIndex];
                for (int eventIndex = 0;
                    eventIndex < sequence.LogicEvents.Count;
                    eventIndex++)
                {
                    FpgSkillLogicEventDefinition logicEvent =
                        sequence.LogicEvents[eventIndex];
                    if (!TryGetPayloadSlot(
                            logicEvent.PayloadSlotId,
                            out FpgPlayerSkillPayloadSlot payload))
                    {
                        error = $"Player skill '{SkillId}' references a missing payload slot '{logicEvent.PayloadSlotId}'.";
                        return false;
                    }

                    FpgSkillTargetSource requiredSource =
                        payload.Kind == FpgPlayerSkillPayloadKind.ReloadCommit
                            ? FpgSkillTargetSource.Self
                            : FpgSkillTargetSource.CurrentAim;
                    if (logicEvent.TargetSource != requiredSource)
                    {
                        error = payload.Kind == FpgPlayerSkillPayloadKind.ReloadCommit
                            ? $"Player reload payload '{payload.SlotId}' must target Self."
                            : $"Player attack payload '{payload.SlotId}' must target CurrentAim.";
                        return false;
                    }

                    hasGameplayEvent = true;
                    hasPelletPayload |= payload.Kind
                        == FpgPlayerSkillPayloadKind.PelletRay;
                    hasAreaPayload |= payload.Kind
                        == FpgPlayerSkillPayloadKind.AreaAtFirstSurface;
                    hasReloadPayload |= payload.Kind
                        == FpgPlayerSkillPayloadKind.ReloadCommit;
                }
            }

            if (!hasGameplayEvent)
            {
                error = $"Player skill '{SkillId}' requires at least one gameplay payload event.";
                return false;
            }

            if (hasPelletPayload)
            {
                if (shotPresentation == null)
                {
                    error = $"Player skill '{SkillId}' is missing shot presentation.";
                    return false;
                }

                if (!shotPresentation.TryValidate(out error))
                {
                    error = $"Player skill '{SkillId}' has invalid shot presentation: {error}";
                    return false;
                }
            }

            if (hasAreaPayload)
            {
                if (secondaryPresentation == null)
                {
                    error = $"Player skill '{SkillId}' is missing secondary presentation.";
                    return false;
                }

                if (!secondaryPresentation.TryValidate(out error))
                {
                    error = $"Player skill '{SkillId}' has invalid secondary presentation: {error}";
                    return false;
                }
            }

            if (hasReloadPayload)
            {
                if (reloadPresentation == null)
                {
                    error = $"Player skill '{SkillId}' is missing reload presentation.";
                    return false;
                }

                if (!reloadPresentation.TryValidate(out error))
                {
                    error = $"Player skill '{SkillId}' has invalid reload presentation: {error}";
                    return false;
                }
            }

            if (hasAreaPayload
                && secondaryTriggerMode == SecondaryTriggerMode.ChargeRelease
                && (!HasSequence(FpgSkillSequenceKind.ChargeEnter)
                    || !HasSequence(FpgSkillSequenceKind.ChargeLoop)
                    || !HasSequence(FpgSkillSequenceKind.Release)))
            {
                error = $"Charged player skill '{SkillId}' requires ChargeEnter, ChargeLoop and Release sequences.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryGetPayloadSlot(
            string slotId,
            out FpgPlayerSkillPayloadSlot payload)
        {
            FpgPlayerSkillPayloadSlot[] values =
                payloadSlots ?? Array.Empty<FpgPlayerSkillPayloadSlot>();
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] != null
                    && string.Equals(
                        values[index].SlotId,
                        slotId,
                        StringComparison.Ordinal))
                {
                    payload = values[index];
                    return true;
                }
            }

            payload = null;
            return false;
        }

        private bool HasSequence(FpgSkillSequenceKind kind)
        {
            for (int index = 0; index < Sequences.Count; index++)
            {
                if (Sequences[index].Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
