using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum FpgPlayerSkillActionKind
    {
        None = 0,
        PelletRay,
        AreaAtFirstSurface,
        ReloadCommit,
        ProjectileAreaAtFirstSurface
    }

    public readonly struct FpgCompiledPlayerSkillAction
    {
        public FpgCompiledPlayerSkillAction(
            FpgPlayerSkillActionKind kind,
            int ammoCost,
            DamageSpec damage,
            QueryPolicy queryPolicy,
            AttackQueryMode queryMode,
            int payloadCount,
            int maxImpactCount,
            int additionalPenetrationCount,
            int areaCombatantLimit,
            int areaProjectileLimit,
            AttackTargetKinds allowedTargetKinds,
            int projectileFlightTicks = 0,
            int projectileSweepRadiusKey = 0,
            int projectileDefinitionId = 0,
            int projectileCount = 0,
            int projectileLifetimeTicks = 0,
            int projectileMaxHitPoints = 0,
            bool projectileInterceptable = false,
            int projectileBudgetUnits = 0)
        {
            if (!Enum.IsDefined(typeof(FpgPlayerSkillActionKind), kind)
                || kind == FpgPlayerSkillActionKind.None
                || ammoCost < 0
                || payloadCount < 0
                || maxImpactCount < 0
                || additionalPenetrationCount < 0
                || areaCombatantLimit < 0
                || areaProjectileLimit < 0
                || projectileFlightTicks < 0
                || projectileSweepRadiusKey < 0
                || projectileDefinitionId < 0
                || projectileCount < 0
                || projectileLifetimeTicks < 0
                || projectileMaxHitPoints < 0
                || projectileBudgetUnits < 0
                || (kind
                        == FpgPlayerSkillActionKind
                            .ProjectileAreaAtFirstSurface
                    && (projectileFlightTicks <= 0
                        || projectileSweepRadiusKey <= 0
                        || projectileDefinitionId <= 0
                        || projectileCount <= 0
                        || projectileLifetimeTicks < projectileFlightTicks
                        || projectileBudgetUnits <= 0)))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

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
            ProjectileFlightTicks = projectileFlightTicks;
            ProjectileSweepRadiusKey = projectileSweepRadiusKey;
            ProjectileDefinitionId = projectileDefinitionId;
            ProjectileCount = projectileCount;
            ProjectileLifetimeTicks = projectileLifetimeTicks;
            ProjectileMaxHitPoints = projectileMaxHitPoints;
            ProjectileInterceptable = projectileInterceptable;
            ProjectileBudgetUnits = projectileBudgetUnits;
        }

        public FpgPlayerSkillActionKind Kind { get; }
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
        public int ProjectileFlightTicks { get; }
        public int ProjectileSweepRadiusKey { get; }
        public int ProjectileDefinitionId { get; }
        public int ProjectileCount { get; }
        public int ProjectileLifetimeTicks { get; }
        public int ProjectileMaxHitPoints { get; }
        public bool ProjectileInterceptable { get; }
        public int ProjectileBudgetUnits { get; }
    }

    public readonly struct FpgCompiledPlayerAttackAction
    {
        public FpgCompiledPlayerAttackAction(
            FpgSkillAttackMode mode,
            FpgCompiledPlayerSkillAction payload)
        {
            if (!IsMatchingPayload(mode, payload.Kind))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            Mode = mode;
            Payload = payload;
        }

        public FpgSkillAttackMode Mode { get; }
        public FpgCompiledPlayerSkillAction Payload { get; }
        public bool IsValid => IsMatchingPayload(Mode, Payload.Kind);

        private static bool IsMatchingPayload(
            FpgSkillAttackMode mode,
            FpgPlayerSkillActionKind payloadKind)
        {
            return (mode == FpgSkillAttackMode.PelletRays
                    && payloadKind == FpgPlayerSkillActionKind.PelletRay)
                || (mode == FpgSkillAttackMode.AreaAtFirstSurface
                    && payloadKind
                        == FpgPlayerSkillActionKind.AreaAtFirstSurface);
        }
    }

    public readonly struct FpgCompiledPlayerProjectileAction
    {
        public FpgCompiledPlayerProjectileAction(
            FpgSkillProjectileImpactMode impactMode,
            FpgCompiledPlayerSkillAction payload,
            int threatDefinitionId = 1)
        {
            if (impactMode
                    != FpgSkillProjectileImpactMode.AreaAtFirstSurface
                || payload.Kind != FpgPlayerSkillActionKind
                    .ProjectileAreaAtFirstSurface
                || threatDefinitionId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(impactMode));
            }

            ImpactMode = impactMode;
            Payload = payload;
            ThreatDefinitionId = threatDefinitionId;
        }

        public FpgSkillProjectileImpactMode ImpactMode { get; }
        public FpgCompiledPlayerSkillAction Payload { get; }
        public int ThreatDefinitionId { get; }
        public bool IsValid => ImpactMode
                == FpgSkillProjectileImpactMode.AreaAtFirstSurface
            && Payload.Kind == FpgPlayerSkillActionKind
                .ProjectileAreaAtFirstSurface
            && ThreatDefinitionId > 0;
    }

    public readonly struct FpgCompiledPlayerReloadAction
    {
        public FpgCompiledPlayerReloadAction(
            FpgCompiledPlayerSkillAction payload)
        {
            if (payload.Kind != FpgPlayerSkillActionKind.ReloadCommit)
            {
                throw new ArgumentOutOfRangeException(nameof(payload));
            }

            Payload = payload;
        }

        public FpgCompiledPlayerSkillAction Payload { get; }
        public bool IsValid => Payload.Kind
            == FpgPlayerSkillActionKind.ReloadCommit;
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
        private readonly FpgCompiledPlayerAttackAction[] attackActions;
        private readonly FpgCompiledPlayerProjectileAction[] projectileActions;
        private readonly FpgCompiledPlayerReloadAction[] reloadActions;
        private readonly FpgCompiledPlayerSkillSequenceSummary[] sequenceSummaries;
        private readonly FpgCompiledSkillTimingDefinition[] sequenceTimings;

        public FpgCompiledPlayerSkillDefinition(
            FpgCompiledSkillDefinition timeline,
            int sequenceCooldownTicks,
            FpgCompiledPlayerAttackAction[] attackActions,
            FpgCompiledPlayerProjectileAction[] projectileActions,
            FpgCompiledPlayerReloadAction[] reloadActions,
            int chargeProgressTicks = 30,
            FpgCompiledSkillTimingDefinition[] sequenceTimings = null)
        {
            Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
            if (sequenceCooldownTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequenceCooldownTicks));
            }

            if (chargeProgressTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chargeProgressTicks));
            }

            if (attackActions == null
                || projectileActions == null
                || reloadActions == null
                || checked(
                    attackActions.Length
                    + projectileActions.Length
                    + reloadActions.Length) == 0)
            {
                throw new ArgumentException(
                    "Compiled player skill requires at least one gameplay action.",
                    nameof(attackActions));
            }

            this.attackActions = CopyActions(attackActions);
            this.projectileActions = CopyActions(projectileActions);
            this.reloadActions = CopyActions(reloadActions);
            ValidateTypedActions(
                this.attackActions,
                this.projectileActions,
                this.reloadActions);
            SequenceCooldownTicks = sequenceCooldownTicks;
            ChargeProgressTicks = chargeProgressTicks;
            sequenceSummaries = BuildSequenceSummaries(timeline);
            this.sequenceTimings = CopySequenceTimings(
                timeline,
                sequenceTimings);
            MaximumImpactCount = ComputeMaximumImpactCount(
                this.attackActions,
                this.projectileActions);
            MaximumPelletCount = ComputeMaximumPelletCount(
                this.attackActions);
            GameplayHash = ComputeGameplayHash(
                timeline,
                sequenceCooldownTicks,
                chargeProgressTicks,
                this.attackActions,
                this.projectileActions,
                this.reloadActions);
            PresentationHash = timeline.PresentationHash;
            TimingContractHash = ComputeTimingContractHash(
                this.sequenceTimings);
        }

        public FpgCompiledSkillDefinition Timeline { get; }
        public int SequenceCooldownTicks { get; }
        public int ChargeProgressTicks { get; }
        public IReadOnlyList<FpgCompiledPlayerAttackAction> AttackActions =>
            attackActions;
        public int AttackActionCount => attackActions.Length;
        public IReadOnlyList<FpgCompiledPlayerProjectileAction>
            ProjectileActions => projectileActions;
        public int ProjectileActionCount => projectileActions.Length;
        public IReadOnlyList<FpgCompiledPlayerReloadAction> ReloadActions =>
            reloadActions;
        public int ReloadActionCount => reloadActions.Length;
        public IReadOnlyList<FpgCompiledPlayerSkillSequenceSummary> SequenceSummaries =>
            sequenceSummaries;
        public int MaximumImpactCount { get; }
        public int MaximumPelletCount { get; }
        public ulong GameplayHash { get; }
        public ulong PresentationHash { get; }
        public ulong TimingContractHash { get; }

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

        public bool TryResolveAction(
            FpgCompiledSkillEvent skillEvent,
            out FpgCompiledPlayerSkillAction payload)
        {
            payload = default(FpgCompiledPlayerSkillAction);
            if (skillEvent.Kind != FpgSkillEventKind.GameplayAction)
            {
                return false;
            }

            int actionIndex = skillEvent.ActionIndex;
            switch (skillEvent.ActionKind)
            {
                case FpgSkillActionKind.Attack:
                    if (actionIndex < 0 || actionIndex >= attackActions.Length)
                    {
                        return false;
                    }

                    payload = attackActions[actionIndex].Payload;
                    return true;

                case FpgSkillActionKind.LaunchProjectile:
                    if (actionIndex < 0
                        || actionIndex >= projectileActions.Length)
                    {
                        return false;
                    }

                    payload = projectileActions[actionIndex].Payload;
                    return true;

                case FpgSkillActionKind.CommitReload:
                    if (actionIndex < 0 || actionIndex >= reloadActions.Length)
                    {
                        return false;
                    }

                    payload = reloadActions[actionIndex].Payload;
                    return true;

                default:
                    return false;
            }
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

        public bool TryGetTimingDefinition(
            FpgSkillSequenceKind kind,
            out FpgCompiledSkillTimingDefinition timing)
        {
            for (int index = 0; index < sequenceTimings.Length; index++)
            {
                if (Timeline.GetSequence(index).Kind == kind)
                {
                    timing = sequenceTimings[index];
                    return true;
                }
            }

            timing = FpgCompiledSkillTimingDefinition.Fixed;
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
                    if (skillEvent.Kind != FpgSkillEventKind.GameplayAction)
                    {
                        continue;
                    }

                    if (!TryResolveAction(
                            skillEvent,
                            out FpgCompiledPlayerSkillAction payload))
                    {
                        throw new ArgumentException(
                            "Compiled timeline references a missing player payload slot.",
                            nameof(timeline));
                    }

                    totalAmmoCost = checked(totalAmmoCost + payload.AmmoCost);
                    if (payload.Kind == FpgPlayerSkillActionKind.ReloadCommit)
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

        private static TAction[] CopyActions<TAction>(TAction[] source)
        {
            TAction[] copy = new TAction[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static void ValidateTypedActions(
            FpgCompiledPlayerAttackAction[] attacks,
            FpgCompiledPlayerProjectileAction[] projectiles,
            FpgCompiledPlayerReloadAction[] reloads)
        {
            for (int index = 0; index < attacks.Length; index++)
            {
                if (!attacks[index].IsValid)
                {
                    throw new ArgumentException(
                        "Compiled player skill contains an invalid attack action.",
                        nameof(attacks));
                }
            }

            for (int index = 0; index < projectiles.Length; index++)
            {
                if (!projectiles[index].IsValid)
                {
                    throw new ArgumentException(
                        "Compiled player skill contains an invalid projectile action.",
                        nameof(projectiles));
                }
            }

            for (int index = 0; index < reloads.Length; index++)
            {
                if (!reloads[index].IsValid)
                {
                    throw new ArgumentException(
                        "Compiled player skill contains an invalid reload action.",
                        nameof(reloads));
                }
            }
        }

        private static int ComputeMaximumImpactCount(
            FpgCompiledPlayerAttackAction[] attacks,
            FpgCompiledPlayerProjectileAction[] projectiles)
        {
            int maximum = 0;
            for (int index = 0; index < attacks.Length; index++)
            {
                maximum = Math.Max(
                    maximum,
                    attacks[index].Payload.MaxImpactCount);
            }

            for (int index = 0; index < projectiles.Length; index++)
            {
                maximum = Math.Max(
                    maximum,
                    projectiles[index].Payload.MaxImpactCount);
            }

            return maximum;
        }

        private static int ComputeMaximumPelletCount(
            FpgCompiledPlayerAttackAction[] attacks)
        {
            int maximum = 0;
            for (int index = 0; index < attacks.Length; index++)
            {
                FpgCompiledPlayerSkillAction action = attacks[index].Payload;
                if (action.QueryPolicy == QueryPolicy.PelletRays)
                {
                    maximum = Math.Max(maximum, action.PayloadCount);
                }
            }

            return maximum;
        }

        private static ulong ComputeGameplayHash(
            FpgCompiledSkillDefinition timeline,
            int sequenceCooldownTicks,
            int chargeProgressTicks,
            FpgCompiledPlayerAttackAction[] attacks,
            FpgCompiledPlayerProjectileAction[] projectiles,
            FpgCompiledPlayerReloadAction[] reloads)
        {
            ulong hash = StableHash.Mix(0x4650475F50534B31UL);
            hash = StableHash.Append(hash, timeline.GameplayHash);
            hash = StableHash.Append(
                hash,
                unchecked((ulong)sequenceCooldownTicks));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)chargeProgressTicks));

            hash = StableHash.Append(hash, unchecked((ulong)attacks.Length));
            for (int index = 0; index < attacks.Length; index++)
            {
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)(int)attacks[index].Mode));
                hash = AppendActionHash(hash, attacks[index].Payload);
            }

            hash = StableHash.Append(
                hash,
                unchecked((ulong)projectiles.Length));
            for (int index = 0; index < projectiles.Length; index++)
            {
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)(int)projectiles[index].ImpactMode));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)projectiles[index].ThreatDefinitionId));
                hash = AppendActionHash(hash, projectiles[index].Payload);
            }

            hash = StableHash.Append(hash, unchecked((ulong)reloads.Length));
            for (int index = 0; index < reloads.Length; index++)
            {
                hash = AppendActionHash(hash, reloads[index].Payload);
            }

            return hash;
        }

        private static ulong AppendActionHash(
            ulong hash,
            in FpgCompiledPlayerSkillAction action)
        {
            hash = StableHash.Append(hash, unchecked((ulong)(int)action.Kind));
            hash = StableHash.Append(hash, unchecked((ulong)action.AmmoCost));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.Damage.BaseDamage));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.Damage.BreakDamage));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.Damage
                    .WeakpointDamageMultiplierBasisPoints));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.Damage
                    .WeakpointBreakMultiplierBasisPoints));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)(int)action.QueryPolicy));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)(int)action.QueryMode));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.PayloadCount));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.MaxImpactCount));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.AdditionalPenetrationCount));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.AreaCombatantLimit));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.AreaProjectileLimit));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.ProjectileFlightTicks));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.ProjectileSweepRadiusKey));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.ProjectileDefinitionId));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.ProjectileCount));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.ProjectileLifetimeTicks));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.ProjectileMaxHitPoints));
            hash = StableHash.Append(
                hash,
                action.ProjectileInterceptable ? 1UL : 0UL);
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.ProjectileBudgetUnits));
            return StableHash.Append(
                hash,
                unchecked((ulong)(int)action.AllowedTargetKinds));
        }

        private static FpgCompiledSkillTimingDefinition[] CopySequenceTimings(
            FpgCompiledSkillDefinition timeline,
            FpgCompiledSkillTimingDefinition[] values)
        {
            int count = timeline.SequenceCount;
            FpgCompiledSkillTimingDefinition[] copy =
                new FpgCompiledSkillTimingDefinition[count];
            if (values == null)
            {
                for (int index = 0; index < count; index++)
                {
                    copy[index] = FpgCompiledSkillTimingDefinition.Fixed;
                }

                return copy;
            }

            if (values.Length != count)
            {
                throw new ArgumentException(
                    "Compiled skill timing count must match the timeline sequence count.",
                    nameof(values));
            }

            Array.Copy(values, copy, count);
            return copy;
        }

        private static ulong ComputeTimingContractHash(
            FpgCompiledSkillTimingDefinition[] values)
        {
            ulong hash = StableHash.Mix(0x4650475F50544D31UL);
            hash = StableHash.Append(hash, unchecked((ulong)values.Length));
            for (int index = 0; index < values.Length; index++)
            {
                hash = StableHash.Append(
                    hash,
                    values[index].TimingContractHash);
            }

            return hash;
        }

    }

    [CreateAssetMenu(
        fileName = "FpgPlayerSkillDefinition",
        menuName = "FPG Demo/Skills/Player Skill")]
    public sealed class FpgPlayerSkillDefinition : FpgSkillTimelineDefinition
    {
        [Header("玩家技能激活")]
        [InspectorName("副射触发模式")]
        [SerializeField]
        private SecondaryTriggerMode secondaryTriggerMode =
            SecondaryTriggerMode.ChargeRelease;

        [InspectorName("最小蓄力 Tick")]
        [SerializeField, Min(0)]
        private int minimumChargeTicks;

        [InspectorName("序列冷却 Tick")]
        [SerializeField, Min(0)]
        private int sequenceCooldownTicks;

        [InspectorName("蓄力进度 Tick")]
        [SerializeField, Min(0)]
        private int chargeProgressTicks = 30;

        public SecondaryTriggerMode SecondaryTriggerMode =>
            secondaryTriggerMode;
        public bool UsesSecondaryTriggerMode => HasSecondaryPayload();
        public int MinimumChargeTicks => minimumChargeTicks;
        public int SequenceCooldownTicks => sequenceCooldownTicks;
        public int ChargeProgressTicks => chargeProgressTicks;

        protected override bool RequiresExecuteSequence =>
            !IsChargeReleaseAreaOrProjectileSkill();

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
                CompileTypedActions(
                    out FpgCompiledPlayerAttackAction[] attacks,
                    out FpgCompiledPlayerProjectileAction[] projectiles,
                    out FpgCompiledPlayerReloadAction[] reloads);
                FpgCompiledSkillTimingDefinition[] timings =
                    new FpgCompiledSkillTimingDefinition[Sequences.Count];
                for (int index = 0; index < Sequences.Count; index++)
                {
                    timings[index] = Sequences[index].CompileTiming();
                }

                definition = new FpgCompiledPlayerSkillDefinition(
                    timeline,
                    sequenceCooldownTicks,
                    attacks,
                    projectiles,
                    reloads,
                    chargeProgressTicks,
                    timings);

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

        private void CompileTypedActions(
            out FpgCompiledPlayerAttackAction[] attacks,
            out FpgCompiledPlayerProjectileAction[] projectiles,
            out FpgCompiledPlayerReloadAction[] reloads)
        {
            int attackCount = 0;
            int projectileCount = 0;
            int reloadCount = 0;
            for (int sequenceIndex = 0;
                sequenceIndex < Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence = Sequences[sequenceIndex];
                attackCount = checked(
                    attackCount + sequence.AttackEvents.Count);
                projectileCount = checked(
                    projectileCount + sequence.ProjectileEvents.Count);
                reloadCount = checked(
                    reloadCount + sequence.ReloadEvents.Count);
            }

            attacks = new FpgCompiledPlayerAttackAction[attackCount];
            projectiles =
                new FpgCompiledPlayerProjectileAction[projectileCount];
            reloads = new FpgCompiledPlayerReloadAction[reloadCount];
            int attackWriteIndex = 0;
            int projectileWriteIndex = 0;
            int reloadWriteIndex = 0;

            for (int sequenceIndex = 0;
                sequenceIndex < Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence = Sequences[sequenceIndex];
                for (int index = 0;
                    index < sequence.AttackEvents.Count;
                    index++)
                {
                    FpgSkillAttackEventDefinition action =
                        sequence.AttackEvents[index];
                    attacks[attackWriteIndex++] = CompileAttackAction(action);
                }

                for (int index = 0;
                    index < sequence.ProjectileEvents.Count;
                    index++)
                {
                    FpgSkillProjectileEventDefinition action =
                        sequence.ProjectileEvents[index];
                    projectiles[projectileWriteIndex++] =
                        CompileProjectileAction(action);
                }

                for (int index = 0;
                    index < sequence.ReloadEvents.Count;
                    index++)
                {
                    reloads[reloadWriteIndex++] = CompileReloadAction();
                }
            }
        }

        private static FpgCompiledPlayerAttackAction CompileAttackAction(
            FpgSkillAttackEventDefinition action)
        {
            bool pellet = action.Mode == FpgSkillAttackMode.PelletRays;
            FpgCompiledPlayerSkillAction payload =
                new FpgCompiledPlayerSkillAction(
                    pellet
                        ? FpgPlayerSkillActionKind.PelletRay
                        : FpgPlayerSkillActionKind.AreaAtFirstSurface,
                    action.AmmoCost,
                    action.CompileDamage(),
                    action.QueryPolicy,
                    action.QueryMode,
                    action.PayloadCount,
                    action.MaxImpactCount,
                    pellet ? action.AdditionalPenetrationCount : 0,
                    pellet ? 0 : action.AreaCombatantLimit,
                    pellet ? 0 : action.AreaProjectileLimit,
                    action.AllowedTargetKinds);
            return new FpgCompiledPlayerAttackAction(action.Mode, payload);
        }

        private static FpgCompiledPlayerProjectileAction
            CompileProjectileAction(
                FpgSkillProjectileEventDefinition action)
        {
            FpgCompiledPlayerSkillAction payload =
                new FpgCompiledPlayerSkillAction(
                    FpgPlayerSkillActionKind.ProjectileAreaAtFirstSurface,
                    action.AmmoCost,
                    action.CompileDamage(),
                    QueryPolicy.DirectThenArea,
                    AttackQueryMode.AreaAtFirstSurface,
                    action.ProjectileCount,
                    action.MaxImpactCount,
                    0,
                    action.AreaCombatantLimit,
                    action.AreaProjectileLimit,
                    action.AllowedTargetKinds,
                    action.ProjectileFlightTicks,
                    action.ProjectileSweepRadiusKey,
                    action.ProjectileDefinitionId,
                    action.ProjectileCount,
                    action.ProjectileLifetimeTicks,
                    action.ProjectileMaxHitPoints,
                    action.ProjectileInterceptable,
                    action.ProjectileBudgetUnits);
            return new FpgCompiledPlayerProjectileAction(
                action.ImpactMode,
                payload,
                action.ThreatDefinitionId);
        }

        private static FpgCompiledPlayerReloadAction CompileReloadAction()
        {
            return new FpgCompiledPlayerReloadAction(
                new FpgCompiledPlayerSkillAction(
                    FpgPlayerSkillActionKind.ReloadCommit,
                    0,
                    new DamageSpec(0, 0),
                    QueryPolicy.None,
                    AttackQueryMode.Legacy,
                    0,
                    0,
                    0,
                    0,
                    0,
                    AttackTargetKinds.None));
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
                || minimumChargeTicks < 0
                || chargeProgressTicks < 0
                || (secondaryTriggerMode == SecondaryTriggerMode.ChargeRelease
                    && chargeProgressTicks <= 0))
            {
                error = $"Player skill '{SkillId}' has invalid activation metadata.";
                return false;
            }

            bool hasGameplayEvent = false;
            bool hasPelletPayload = false;
            bool hasAreaPayload = false;
            bool hasReloadPayload = false;
            if (!TryInspectTypedGameplay(
                    ref hasGameplayEvent,
                    ref hasPelletPayload,
                    ref hasAreaPayload,
                    ref hasReloadPayload,
                    out error))
            {
                return false;
            }

            if (!hasGameplayEvent)
            {
                error = $"Player skill '{SkillId}' requires at least one gameplay action.";
                return false;
            }

            bool isChargedAreaSkill = hasAreaPayload
                && secondaryTriggerMode == SecondaryTriggerMode.ChargeRelease;
            if (isChargedAreaSkill
                && HasSequence(FpgSkillSequenceKind.Execute))
            {
                error = $"Charged player skill '{SkillId}' cannot contain an Execute sequence.";
                return false;
            }

            for (int sequenceIndex = 0;
                sequenceIndex < Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence = Sequences[sequenceIndex];
                int lastAttackTick = -1;
                for (int eventIndex = 0;
                    eventIndex < sequence.AttackEvents.Count;
                    eventIndex++)
                {
                    lastAttackTick = Math.Max(
                        lastAttackTick,
                        sequence.AttackEvents[eventIndex].Tick);
                }

                for (int eventIndex = 0;
                    eventIndex < sequence.ProjectileEvents.Count;
                    eventIndex++)
                {
                    lastAttackTick = Math.Max(
                        lastAttackTick,
                        sequence.ProjectileEvents[eventIndex].Tick);
                }

                if (lastAttackTick >= 0
                    && (sequence.AllowWithdrawTick < 0
                        || sequence.AllowWithdrawTick <= lastAttackTick))
                {
                    error = $"Player skill '{SkillId}' sequence '{sequence.Kind}' requires AllowWithdrawTick after its final attack event.";
                    return false;
                }
            }

            if (isChargedAreaSkill
                && (!HasSequence(FpgSkillSequenceKind.ChargeEnter)
                    || !HasSequence(FpgSkillSequenceKind.ChargeLoop)
                    || !HasSequence(FpgSkillSequenceKind.Release)
                    || !HasSequence(FpgSkillSequenceKind.Cancel)))
            {
                error = $"Charged player skill '{SkillId}' requires ChargeEnter, ChargeLoop, Release and Cancel sequences.";
                return false;
            }

            if (!isChargedAreaSkill
                && !HasSequence(FpgSkillSequenceKind.Execute))
            {
                error = $"Skill '{SkillId}' requires an Execute sequence.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool IsChargeReleaseAreaOrProjectileSkill()
        {
            if (secondaryTriggerMode != SecondaryTriggerMode.ChargeRelease
                || !UsesSecondaryTriggerMode)
            {
                return false;
            }

            return true;
        }

        private bool HasSecondaryPayload()
        {
            for (int sequenceIndex = 0;
                sequenceIndex < Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence = Sequences[sequenceIndex];
                if (sequence == null)
                {
                    continue;
                }

                if (sequence.ProjectileEvents.Count > 0)
                {
                    return true;
                }

                for (int actionIndex = 0;
                    actionIndex < sequence.AttackEvents.Count;
                    actionIndex++)
                {
                    FpgSkillAttackEventDefinition action =
                        sequence.AttackEvents[actionIndex];
                    if (action != null
                        && action.Mode == FpgSkillAttackMode.AreaAtFirstSurface)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryInspectTypedGameplay(
            ref bool hasGameplayEvent,
            ref bool hasPelletPayload,
            ref bool hasAreaPayload,
            ref bool hasReloadPayload,
            out string error)
        {
            for (int sequenceIndex = 0;
                sequenceIndex < Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence = Sequences[sequenceIndex];
                if (sequence.SummonEvents.Count > 0)
                {
                    error = $"Player skill '{SkillId}' cannot contain summon actions.";
                    return false;
                }

                if (sequence.SelfDestructOwnerEvents.Count > 0)
                {
                    error = $"Player skill '{SkillId}' cannot contain self-destruct actions.";
                    return false;
                }

                for (int index = 0;
                    index < sequence.AttackEvents.Count;
                    index++)
                {
                    FpgSkillAttackEventDefinition action =
                        sequence.AttackEvents[index];
                    if ((action.Mode != FpgSkillAttackMode.PelletRays
                            && action.Mode
                                != FpgSkillAttackMode.AreaAtFirstSurface)
                        || action.TargetSource
                            != FpgSkillTargetSource.CurrentAim
                        || action.AmmoCost <= 0
                        || !IsValidPlayerTargetKinds(
                            action.AllowedTargetKinds))
                    {
                        error = $"Player attack action '{action.EventId}' has an unsupported mode, target source, ammo cost or target kind.";
                        return false;
                    }

                    hasGameplayEvent = true;
                    hasPelletPayload |= action.Mode
                        == FpgSkillAttackMode.PelletRays;
                    hasAreaPayload |= action.Mode
                        == FpgSkillAttackMode.AreaAtFirstSurface;
                }

                for (int index = 0;
                    index < sequence.ProjectileEvents.Count;
                    index++)
                {
                    FpgSkillProjectileEventDefinition action =
                        sequence.ProjectileEvents[index];
                    if (action.ImpactMode != FpgSkillProjectileImpactMode
                            .AreaAtFirstSurface
                        || action.TargetSource
                            != FpgSkillTargetSource.CurrentAim
                        || action.AmmoCost <= 0
                        || action.ProjectileCount != 1
                        || action.ProjectileInterceptable
                        || !IsValidPlayerTargetKinds(
                            action.AllowedTargetKinds))
                    {
                        error = $"Player projectile action '{action.EventId}' must be one non-interceptable area projectile targeting CurrentAim.";
                        return false;
                    }

                    hasGameplayEvent = true;
                    hasAreaPayload = true;
                }

                for (int index = 0;
                    index < sequence.ReloadEvents.Count;
                    index++)
                {
                    FpgSkillReloadEventDefinition action =
                        sequence.ReloadEvents[index];
                    if (action.TargetSource != FpgSkillTargetSource.Self)
                    {
                        error = $"Player reload action '{action.EventId}' must target Self.";
                        return false;
                    }

                    hasGameplayEvent = true;
                    hasReloadPayload = true;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool IsValidPlayerTargetKinds(
            AttackTargetKinds value)
        {
            return value != AttackTargetKinds.None
                && (value & ~WeaponDefinition.PlayerAttackTargetKinds)
                    == AttackTargetKinds.None;
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
