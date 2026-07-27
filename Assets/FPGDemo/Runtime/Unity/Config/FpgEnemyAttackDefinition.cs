using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public sealed class FpgCompiledEnemySkillDefinition
    {
        private readonly FpgCompiledEnemySkillAction[] attackActions;
        private readonly FpgCompiledEnemySkillAction[] projectileActions;
        private readonly FpgCompiledEnemySkillAction[] summonActions;

        internal FpgCompiledEnemySkillDefinition(
            FpgCompiledSkillDefinition timeline,
            int priority,
            int firstReadyOffsetTicks,
            int sequenceCooldownTicks,
            FpgCompiledEnemySkillAction[] attackActions,
            FpgCompiledEnemySkillAction[] projectileActions,
            FpgCompiledEnemySkillAction[] summonActions,
            int totalProjectileCapacity,
            int totalImpactCapacity,
            int totalSummonCapacity,
            int maxHitCount,
            int lastAttackTick)
        {
            Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
            if (firstReadyOffsetTicks < 0
                || sequenceCooldownTicks < 0
                || attackActions == null
                || projectileActions == null
                || summonActions == null
                || checked(
                    attackActions.Length
                    + projectileActions.Length
                    + summonActions.Length) == 0
                || totalProjectileCapacity < 0
                || totalImpactCapacity < 0
                || totalSummonCapacity < 0
                || maxHitCount < 0
                || lastAttackTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequenceCooldownTicks));
            }

            this.attackActions = CopyActions(
                attackActions,
                FpgEnemySkillActionKind.TimedImpact);
            this.projectileActions = CopyActions(
                projectileActions,
                FpgEnemySkillActionKind.Projectile);
            this.summonActions = CopyActions(
                summonActions,
                FpgEnemySkillActionKind.Summon);
            Priority = priority;
            FirstReadyOffsetTicks = firstReadyOffsetTicks;
            SequenceCooldownTicks = sequenceCooldownTicks;
            TotalProjectileCapacity = totalProjectileCapacity;
            TotalImpactCapacity = totalImpactCapacity;
            TotalSummonCapacity = totalSummonCapacity;
            MaxHitCount = maxHitCount;
            LastAttackTick = lastAttackTick;
            GameplayHash = ComputeGameplayHash(
                Timeline,
                Priority,
                FirstReadyOffsetTicks,
                SequenceCooldownTicks,
                this.attackActions,
                this.projectileActions,
                this.summonActions);
            PresentationHash = timeline.PresentationHash;
        }

        public FpgCompiledSkillDefinition Timeline { get; }
        public int Priority { get; }
        public int FirstReadyOffsetTicks { get; }
        public ulong GameplayHash { get; }
        public ulong PresentationHash { get; }

        /// <summary>
        /// Cooldown starts when the selected sequence reaches its end tick.
        /// </summary>
        public int SequenceCooldownTicks { get; }

        public IReadOnlyList<FpgCompiledEnemySkillAction> AttackActions =>
            attackActions;
        public IReadOnlyList<FpgCompiledEnemySkillAction> ProjectileActions =>
            projectileActions;
        public IReadOnlyList<FpgCompiledEnemySkillAction> SummonActions =>
            summonActions;
        public int GameplayActionCount => checked(
            attackActions.Length
            + projectileActions.Length
            + summonActions.Length);
        public int TotalProjectileCapacity { get; }
        public int TotalImpactCapacity { get; }
        public int TotalSummonCapacity { get; }
        public int MaxHitCount { get; }
        public int LastAttackTick { get; }

        public FpgCompiledEnemySkillAction GetGameplayAction(int index)
        {
            if (index < 0 || index >= GameplayActionCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (index < attackActions.Length)
            {
                return attackActions[index];
            }

            index -= attackActions.Length;
            if (index < projectileActions.Length)
            {
                return projectileActions[index];
            }

            return summonActions[index - projectileActions.Length];
        }

        public bool TryResolveAction(
            in FpgCompiledSkillEvent skillEvent,
            out FpgCompiledEnemySkillAction action)
        {
            if (skillEvent.Kind != FpgSkillEventKind.GameplayAction)
            {
                action = default(FpgCompiledEnemySkillAction);
                return false;
            }

            switch (skillEvent.ActionKind)
            {
                case FpgSkillActionKind.Attack:
                    return TryGetAction(
                        attackActions,
                        skillEvent.ActionIndex,
                        out action);

                case FpgSkillActionKind.LaunchProjectile:
                    return TryGetAction(
                        projectileActions,
                        skillEvent.ActionIndex,
                        out action);

                case FpgSkillActionKind.CommitReload:
                    action = default(FpgCompiledEnemySkillAction);
                    return false;

                case FpgSkillActionKind.SummonActors:
                    return TryGetAction(
                        summonActions,
                        skillEvent.ActionIndex,
                        out action);

                default:
                    action = default(FpgCompiledEnemySkillAction);
                    return false;
            }
        }

        private static ulong AppendString(ulong hash, string value)
        {
            string textValue = value ?? string.Empty;
            hash = StableHash.Append(
                hash,
                unchecked((ulong)textValue.Length));
            for (int index = 0; index < textValue.Length; index++)
            {
                hash = StableHash.Append(hash, textValue[index]);
            }

            return hash;
        }


        private static ulong ComputeGameplayHash(
            FpgCompiledSkillDefinition timeline,
            int priority,
            int firstReadyOffsetTicks,
            int sequenceCooldownTicks,
            FpgCompiledEnemySkillAction[] attacks,
            FpgCompiledEnemySkillAction[] projectiles,
            FpgCompiledEnemySkillAction[] summons)
        {
            ulong hash = StableHash.Mix(0x4650475F45534B31UL);
            hash = StableHash.Append(hash, timeline.GameplayHash);
            hash = StableHash.Append(hash, unchecked((ulong)priority));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)firstReadyOffsetTicks));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)sequenceCooldownTicks));
            hash = AppendActionBufferHash(
                hash,
                FpgSkillActionKind.Attack,
                attacks);
            hash = AppendActionBufferHash(
                hash,
                FpgSkillActionKind.LaunchProjectile,
                projectiles);
            hash = AppendActionBufferHash(
                hash,
                FpgSkillActionKind.SummonActors,
                summons);

            return hash;
        }

        private static ulong AppendActionBufferHash(
            ulong hash,
            FpgSkillActionKind actionKind,
            FpgCompiledEnemySkillAction[] values)
        {
            hash = StableHash.Append(hash, unchecked((ulong)(int)actionKind));
            hash = StableHash.Append(hash, unchecked((ulong)values.Length));
            for (int index = 0; index < values.Length; index++)
            {
                hash = StableHash.Append(hash, unchecked((ulong)index));
                hash = AppendActionHash(hash, values[index]);
            }

            return hash;
        }

        private static ulong AppendActionHash(
            ulong hash,
            FpgCompiledEnemySkillAction action)
        {
            hash = StableHash.Append(hash, unchecked((ulong)action.ActionId));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)(int)action.Kind));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)action.ThreatDefinitionId));

            if (action.Kind != FpgEnemySkillActionKind.Summon)
            {
                return AppendThreatGameplayHash(
                    hash,
                    action.ThreatPayload);
            }

            FpgCompiledEnemySummonPayload summon = action.SummonPayload;
            hash = StableHash.Append(
                hash,
                unchecked((ulong)summon.ActionStableId));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)summon.CandidateCount));
            for (int candidateIndex = 0;
                candidateIndex < summon.CandidateCount;
                candidateIndex++)
            {
                FpgCompiledEnemySummonCandidate candidate =
                    summon.GetCandidate(candidateIndex);
                hash = AppendString(hash, candidate.EnemyDefinitionId);
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)candidate.Weight));
            }

            hash = StableHash.Append(
                hash,
                unchecked((ulong)(int)summon.OccupancyMode));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)(int)summon.PlacementMode));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)(int)summon.OwnerOutcome));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)summon.MaxSummonsPerOwner));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)summon.MaxTotalSummonsPerEncounter));
            return StableHash.Append(
                hash,
                unchecked((ulong)summon.MaxRecursionDepth));
        }

        private static ulong AppendThreatGameplayHash(
            ulong hash,
            in ThreatPayloadDefinition payload)
        {
            hash = StableHash.Append(hash, unchecked((ulong)(int)payload.Kind));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)(int)payload.PresentationKind));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)payload.PayloadCount));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)payload.ImpactDelay.Value));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)(int)payload.TargetPolicy));
            hash = AppendDamageHash(hash, payload.TimedImpactDamage);
            if (!payload.IsSweptProjectile)
            {
                return hash;
            }

            ProjectileDefinition projectile = payload.ProjectileDefinition;
            hash = StableHash.Append(
                hash,
                unchecked((ulong)projectile.DefinitionId));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)projectile.FlightDuration.Value));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)projectile.ExpireDuration.Value));
            hash = AppendDamageHash(hash, projectile.DamageSpec);
            hash = StableHash.Append(
                hash,
                unchecked((ulong)projectile.MaxHitPoints));
            hash = StableHash.Append(
                hash,
                projectile.Interceptable ? 1UL : 0UL);
            hash = StableHash.Append(
                hash,
                unchecked((ulong)projectile.BudgetUnits));
            return StableHash.Append(
                hash,
                unchecked((ulong)projectile.SweepRadiusKey));
        }

        private static ulong AppendDamageHash(
            ulong hash,
            in DamageSpec damage)
        {
            hash = StableHash.Append(
                hash,
                unchecked((ulong)damage.BaseDamage));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)damage.BreakDamage));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)damage
                    .WeakpointDamageMultiplierBasisPoints));
            return StableHash.Append(
                hash,
                unchecked((ulong)damage
                    .WeakpointBreakMultiplierBasisPoints));
        }

        private static bool TryGetAction(
            FpgCompiledEnemySkillAction[] values,
            int actionIndex,
            out FpgCompiledEnemySkillAction action)
        {
            if (actionIndex >= 0 && actionIndex < values.Length)
            {
                action = values[actionIndex];
                return true;
            }

            action = default(FpgCompiledEnemySkillAction);
            return false;
        }

        private static FpgCompiledEnemySkillAction[] CopyActions(
            FpgCompiledEnemySkillAction[] source,
            FpgEnemySkillActionKind expectedKind)
        {
            FpgCompiledEnemySkillAction[] copy =
                new FpgCompiledEnemySkillAction[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                if (source[index].Kind != expectedKind)
                {
                    throw new ArgumentException(
                        $"Compiled enemy action {index} is not '{expectedKind}'.",
                        nameof(source));
                }

                copy[index] = source[index];
            }

            return copy;
        }


    }

    /// <summary>
    /// One reusable enemy skill: an authored timeline plus typed gameplay
    /// actions. This is the only enemy attack authoring and runtime contract.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FpgEnemyAttackDefinition",
        menuName = "FPG Demo/Skills/Enemy Skill")]
    public sealed class FpgEnemyAttackDefinition : FpgSkillTimelineDefinition
    {
        private const int DefaultTimedImpactPresentationResourceKey = 1;
        private const int DefaultProjectilePresentationResourceKey = 1;

        [Header("Enemy Skill")]
        [D0PlannerField("Priority", "Tie-break priority after ReadyTick and SpawnSequence.")]
        [SerializeField]
        private int priority;

        [D0PlannerField(
            "First Ready Offset (Ticks)",
            "Offset from owner activation at which this skill first becomes eligible.")]
        [SerializeField, Min(0)]
        private int firstReadyOffsetTicks = 60;

        [D0PlannerField(
            "Sequence Cooldown (Ticks)",
            "Per-owner cooldown anchored to the end of the completed sequence.")]
        [SerializeField, Min(0)]
        private int sequenceCooldownTicks = 90;

        public int Priority => priority;
        public int FirstReadyOffsetTicks => firstReadyOffsetTicks;
        public int SequenceCooldownTicks => sequenceCooldownTicks;

        public bool TryCompile(
            out FpgCompiledEnemySkillDefinition definition,
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
                FpgCompiledEnemySkillAction[] attackActions;
                FpgCompiledEnemySkillAction[] projectileActions;
                FpgCompiledEnemySkillAction[] summonActions;
                CompileTypedActions(
                    out attackActions,
                    out projectileActions,
                    out summonActions);

                if (!timeline.TryGetSequence(
                        FpgSkillSequenceKind.Execute,
                        out FpgCompiledSkillSequence execute))
                {
                    throw new InvalidOperationException(
                        $"Enemy skill '{SkillId}' has no compiled Execute sequence.");
                }

                int totalProjectileCapacity = 0;
                int totalImpactCapacity = 0;
                int totalSummonCapacity = 0;
                int maxHitCount = 0;
                int lastAttackTick = -1;
                for (int eventIndex = 0;
                    eventIndex < execute.EventCount;
                    eventIndex++)
                {
                    FpgCompiledSkillEvent skillEvent =
                        execute.GetEvent(eventIndex);
                    if (skillEvent.Kind != FpgSkillEventKind.GameplayAction)
                    {
                        continue;
                    }

                    if (!TryResolveAction(
                            skillEvent,
                            attackActions,
                            projectileActions,
                            summonActions,
                            out FpgCompiledEnemySkillAction action))
                    {
                        throw new InvalidOperationException(
                            $"Enemy skill '{SkillId}' compiled an unresolved gameplay action.");
                    }

                    totalProjectileCapacity = checked(
                        totalProjectileCapacity + action.ProjectileCapacity);
                    totalImpactCapacity = checked(
                        totalImpactCapacity + action.ImpactCapacity);
                    totalSummonCapacity = checked(
                        totalSummonCapacity + action.SummonCapacity);
                    maxHitCount = Math.Max(maxHitCount, action.MaxHitCount);
                    lastAttackTick = Math.Max(lastAttackTick, skillEvent.Tick);
                }

                definition = new FpgCompiledEnemySkillDefinition(
                    timeline,
                    priority,
                    firstReadyOffsetTicks,
                    sequenceCooldownTicks,
                    attackActions,
                    projectileActions,
                    summonActions,
                    totalProjectileCapacity,
                    totalImpactCapacity,
                    totalSummonCapacity,
                    maxHitCount,
                    lastAttackTick);
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

        protected override bool TryValidateDefinition(out string error)
        {
            if (firstReadyOffsetTicks < 0
                || sequenceCooldownTicks < 0)
            {
                error = $"Enemy skill '{SkillId}' has invalid scheduling values.";
                return false;
            }

            return TryValidateTypedDefinition(out error);
        }

        private bool TryValidateTypedDefinition(out string error)
        {
            bool hasGameplayAction = false;
            for (int sequenceIndex = 0;
                sequenceIndex < Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence = Sequences[sequenceIndex];
                if (sequence == null
                    || sequence.Kind != FpgSkillSequenceKind.Execute)
                {
                    continue;
                }

                if (sequence.ReloadEvents.Count != 0)
                {
                    error =
                        $"Enemy skill '{SkillId}' cannot contain reload actions.";
                    return false;
                }

                for (int index = 0;
                    index < sequence.AttackEvents.Count;
                    index++)
                {
                    FpgSkillAttackEventDefinition action =
                        sequence.AttackEvents[index];
                    error = string.Empty;
                    if (action.AmmoCost != 0)
                    {
                        error =
                            $"Enemy attack action '{action.EventId}' cannot consume player ammo.";
                        return false;
                    }

                    if (action.Mode != FpgSkillAttackMode.BoundTarget
                        || !TryValidateTypedSpatial(
                            FpgEnemySkillActionKind.TimedImpact,
                            action,
                            out error))
                    {
                        if (string.IsNullOrEmpty(error))
                        {
                            error =
                                $"Enemy attack action '{action.EventId}' must use BoundTarget mode.";
                        }

                        return false;
                    }

                    hasGameplayAction = true;
                }

                for (int index = 0;
                    index < sequence.ProjectileEvents.Count;
                    index++)
                {
                    FpgSkillProjectileEventDefinition action =
                        sequence.ProjectileEvents[index];
                    error = string.Empty;
                    if (action.AmmoCost != 0)
                    {
                        error =
                            $"Enemy projectile action '{action.EventId}' cannot consume player ammo.";
                        return false;
                    }

                    if (action.ImpactMode
                            != FpgSkillProjectileImpactMode.BoundTarget
                        || !TryValidateTypedSpatial(
                            FpgEnemySkillActionKind.Projectile,
                            action,
                            out error))
                    {
                        if (string.IsNullOrEmpty(error))
                        {
                            error =
                                $"Enemy projectile action '{action.EventId}' must use BoundTarget impact mode.";
                        }

                        return false;
                    }

                    hasGameplayAction = true;
                }

                for (int index = 0;
                    index < sequence.SummonEvents.Count;
                    index++)
                {
                    FpgSkillSummonEventDefinition action =
                        sequence.SummonEvents[index];
                    if (!TryValidateTypedSpatial(
                            FpgEnemySkillActionKind.Summon,
                            action,
                            out error))
                    {
                        return false;
                    }

                    hasGameplayAction = true;
                }
            }

            if (hasGameplayAction)
            {
                error = string.Empty;
                return true;
            }

            error =
                $"Enemy skill '{SkillId}' requires at least one gameplay action in Execute.";
            return false;
        }

        private void CompileTypedActions(
            out FpgCompiledEnemySkillAction[] attackActions,
            out FpgCompiledEnemySkillAction[] projectileActions,
            out FpgCompiledEnemySkillAction[] summonActions)
        {
            int attackCount = 0;
            int projectileCount = 0;
            int summonCount = 0;
            for (int sequenceIndex = 0;
                sequenceIndex < Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence = Sequences[sequenceIndex];
                attackCount = checked(
                    attackCount + sequence.AttackEvents.Count);
                projectileCount = checked(
                    projectileCount + sequence.ProjectileEvents.Count);
                summonCount = checked(
                    summonCount + sequence.SummonEvents.Count);
            }

            attackActions =
                new FpgCompiledEnemySkillAction[attackCount];
            projectileActions =
                new FpgCompiledEnemySkillAction[projectileCount];
            summonActions =
                new FpgCompiledEnemySkillAction[summonCount];
            int attackIndex = 0;
            int projectileIndex = 0;
            int summonIndex = 0;
            for (int sequenceIndex = 0;
                sequenceIndex < Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence = Sequences[sequenceIndex];
                for (int index = 0;
                    index < sequence.AttackEvents.Count;
                    index++)
                {
                    attackActions[attackIndex++] = CompileAttackAction(
                        sequence.AttackEvents[index]);
                }

                for (int index = 0;
                    index < sequence.ProjectileEvents.Count;
                    index++)
                {
                    projectileActions[projectileIndex++] =
                        CompileProjectileAction(
                            sequence.ProjectileEvents[index]);
                }

                for (int index = 0;
                    index < sequence.SummonEvents.Count;
                    index++)
                {
                    summonActions[summonIndex++] = CompileSummonAction(
                        sequence.SummonEvents[index]);
                }
            }
        }

        private static FpgCompiledEnemySkillAction CompileAttackAction(
            FpgSkillAttackEventDefinition action)
        {
            int actionId = FpgSkillStableId.CompileEvent(action.EventId);
            return FpgCompiledEnemySkillAction.ForThreat(
                actionId,
                FpgEnemySkillActionKind.TimedImpact,
                action.ThreatDefinitionId,
                ThreatPayloadDefinition.TimedImpact(
                    action.CompileDamage(),
                    action.BoundTargetPolicy,
                    new TickDuration(action.DelayTicks),
                    DefaultTimedImpactPresentationResourceKey,
                    action.ThreatPresentationKind));
        }

        private static FpgCompiledEnemySkillAction
            CompileProjectileAction(
                FpgSkillProjectileEventDefinition action)
        {
            int actionId = FpgSkillStableId.CompileEvent(action.EventId);
            ProjectileDefinition projectile = new ProjectileDefinition(
                action.ProjectileDefinitionId,
                new TickDuration(action.ProjectileFlightTicks),
                new TickDuration(action.ProjectileLifetimeTicks),
                action.CompileDamage(),
                action.ProjectileMaxHitPoints,
                action.ProjectileInterceptable,
                action.ProjectileBudgetUnits,
                action.ProjectileSweepRadiusKey);
            return FpgCompiledEnemySkillAction.ForThreat(
                actionId,
                FpgEnemySkillActionKind.Projectile,
                action.ThreatDefinitionId,
                ThreatPayloadDefinition.SweptProjectile(
                    projectile,
                    action.ProjectileCount,
                    action.ThreatPresentationKind,
                    DefaultProjectilePresentationResourceKey));
        }

        private static FpgCompiledEnemySkillAction CompileSummonAction(
            FpgSkillSummonEventDefinition action)
        {
            int actionId = FpgSkillStableId.CompileEvent(action.EventId);
            IReadOnlyList<FpgEnemyDefinition> candidates =
                action.SummonCandidates;
            FpgCompiledEnemySummonCandidate[] compiledCandidates =
                new FpgCompiledEnemySummonCandidate[candidates.Count];
            for (int index = 0; index < candidates.Count; index++)
            {
                compiledCandidates[index] =
                    new FpgCompiledEnemySummonCandidate(
                        candidates[index],
                        action.GetSummonCandidateWeight(index));
            }

            return FpgCompiledEnemySkillAction.ForSummon(
                actionId,
                new FpgCompiledEnemySummonPayload(
                    actionId,
                    action.EventId,
                    compiledCandidates,
                    action.SummonOccupancyMode,
                    action.SummonPlacementMode,
                    action.SummonOwnerOutcome,
                    action.MaxSummonsPerOwner,
                    action.MaxTotalSummonsPerEncounter,
                    action.MaxSummonRecursionDepth));
        }

        private static bool TryValidateTypedSpatial(
            FpgEnemySkillActionKind payloadKind,
            FpgSkillGameplayActionDefinition action,
            out string error)
        {
            if (action == null)
            {
                error =
                    "Enemy skill spatial validation requires a gameplay action.";
                return false;
            }

            bool hasOffset = action.TargetOffset != Vector3.zero;
            bool hasSocket = !string.IsNullOrEmpty(action.SocketId);
            switch (payloadKind)
            {
                case FpgEnemySkillActionKind.Projectile:
                    if (action.TargetSource
                            == FpgSkillTargetSource.CurrentAim
                        || action.TargetSource
                            == FpgSkillTargetSource.CurrentTarget
                        || (action.TargetSource
                                == FpgSkillTargetSource.SocketForward
                            && hasSocket))
                    {
                        error = string.Empty;
                        return true;
                    }

                    error =
                        "Enemy projectile actions support CurrentAim, CurrentTarget, or SocketForward with an owner socket.";
                    return false;

                case FpgEnemySkillActionKind.TimedImpact:
                case FpgEnemySkillActionKind.Summon:
                    if ((action.TargetSource
                                == FpgSkillTargetSource.CurrentAim
                            || action.TargetSource
                                == FpgSkillTargetSource.CurrentTarget)
                        && !hasSocket
                        && !hasOffset)
                    {
                        error = string.Empty;
                        return true;
                    }

                    error = payloadKind
                            == FpgEnemySkillActionKind.TimedImpact
                        ? "Enemy bound-target attacks require CurrentAim/CurrentTarget with no socket or offset."
                        : "Enemy summon placement owns its position and requires CurrentAim/CurrentTarget with no socket or offset.";
                    return false;

                default:
                    error =
                        "Enemy gameplay action has an unsupported action kind.";
                    return false;
            }
        }

        private static bool TryResolveAction(
            in FpgCompiledSkillEvent skillEvent,
            FpgCompiledEnemySkillAction[] attacks,
            FpgCompiledEnemySkillAction[] projectiles,
            FpgCompiledEnemySkillAction[] summons,
            out FpgCompiledEnemySkillAction action)
        {
            switch (skillEvent.ActionKind)
            {
                case FpgSkillActionKind.Attack:
                    return TryGetAction(
                        attacks,
                        skillEvent.ActionIndex,
                        out action);
                case FpgSkillActionKind.LaunchProjectile:
                    return TryGetAction(
                        projectiles,
                        skillEvent.ActionIndex,
                        out action);
                case FpgSkillActionKind.CommitReload:
                    action = default(FpgCompiledEnemySkillAction);
                    return false;
                case FpgSkillActionKind.SummonActors:
                    return TryGetAction(
                        summons,
                        skillEvent.ActionIndex,
                        out action);
                default:
                    action = default(FpgCompiledEnemySkillAction);
                    return false;
            }
        }

        private static bool TryGetAction(
            FpgCompiledEnemySkillAction[] values,
            int actionIndex,
            out FpgCompiledEnemySkillAction action)
        {
            if (actionIndex >= 0 && actionIndex < values.Length)
            {
                action = values[actionIndex];
                return true;
            }

            action = default(FpgCompiledEnemySkillAction);
            return false;
        }

    }
}
