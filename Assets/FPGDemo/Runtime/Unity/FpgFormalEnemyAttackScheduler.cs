using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Fixed-capacity adapter from formal Unity attack assets to the pure
    /// multi-enemy combat port. The host calls Tick once in the enemy attack
    /// director phase, after player hits and death cleanup.
    /// </summary>
    public sealed class FpgFormalEnemyAttackScheduler
    {
        private const ulong SummonSelectionDomain = 0x4650475F53554D4DUL;

        private readonly FpgMultiEnemyCombatPort combatPort;
        private readonly FpgEncounterRunContext runContext;
        private readonly FpgFormalAttackRuntimeCatalog runtimeCatalog;
        private readonly OwnerState[] owners;
        private readonly PatternState[] patterns;
        private readonly SummonActionState[] summonActions;

        private TickIndex lastTick = TickIndex.Invalid;
        private long nextScheduleSequence;
        private int ownerCount;
        private int patternCount;
        private int summonActionCount;

        public FpgFormalEnemyAttackScheduler(
            FpgMultiEnemyCombatPort combatPort,
            FpgEncounterRunContext runContext,
            FpgFormalAttackRuntimeCatalog runtimeCatalog,
            int ownerCapacity,
            int patternCapacity)
        {
            if (combatPort == null)
            {
                throw new ArgumentNullException(nameof(combatPort));
            }

            if (!runContext.IsValid)
            {
                throw new ArgumentException("Formal attack scheduler requires a valid run context.", nameof(runContext));
            }

            if (runtimeCatalog == null)
            {
                throw new ArgumentNullException(nameof(runtimeCatalog));
            }

            if (!runtimeCatalog.TryValidate(out string catalogError))
            {
                throw new ArgumentException(catalogError, nameof(runtimeCatalog));
            }

            if (ownerCapacity <= 0 || patternCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerCapacity));
            }

            this.combatPort = combatPort;
            this.runContext = runContext;
            this.runtimeCatalog = runtimeCatalog;
            owners = new OwnerState[ownerCapacity];
            patterns = new PatternState[patternCapacity];
            summonActions = new SummonActionState[patternCapacity];
        }

        public int OwnerCapacity => owners.Length;
        public int PatternCapacity => patterns.Length;
        public int RegisteredOwnerCount => ownerCount;
        public int RegisteredPatternCount => patternCount;
        public int RegisteredSummonActionCount => summonActionCount;
        public TickIndex LastTick => lastTick;

        /// <summary>
        /// Called for every EnemyActivated lifecycle event. Recursion depth is
        /// mandatory so summoned enemies cannot silently regain root depth.
        /// </summary>
        public DomainResult TryRegisterEnemy(
            RuntimeId runtimeId,
            int spawnSequence,
            TickIndex activationTick,
            int recursionDepth,
            FpgEnemyDefinition definition)
        {
            if (!runtimeId.IsValid
                || spawnSequence < 0
                || !activationTick.IsValid
                || recursionDepth < 0
                || definition == null
                || definition.AttackPatternCount <= 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (FindOwner(runtimeId) >= 0 || FindOwnerBySpawnSequence(spawnSequence) >= 0)
            {
                return DomainResult.Rejected(RejectReason.DuplicateSequence);
            }

            int ownerIndex = FindFreeOwner();
            int requiredSummonActions = CountNewSummonActions(definition);
            if (ownerIndex < 0
                || CountFreePatterns() < definition.AttackPatternCount
                || summonActions.Length - summonActionCount < requiredSummonActions)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            for (int ordinal = 0; ordinal < definition.AttackPatternCount; ordinal++)
            {
                FpgEnemyAttackDefinition attack = definition.GetAttackPattern(ordinal);
                if (attack == null
                    || !runtimeCatalog.TryResolve(attack, out FpgFormalAttackRuntimeEntry runtimeEntry)
                    || runtimeEntry == null
                    || !TryAddTicks(activationTick, attack.FirstReadyOffsetTicks, out TickIndex ignoredTick))
                {
                    return DomainResult.Rejected(RejectReason.InvalidDefinition);
                }
            }

            owners[ownerIndex] = new OwnerState(
                runtimeId,
                spawnSequence,
                activationTick,
                recursionDepth,
                definition);
            ownerCount++;

            for (int ordinal = 0; ordinal < definition.AttackPatternCount; ordinal++)
            {
                FpgEnemyAttackDefinition attack = definition.GetAttackPattern(ordinal);
                runtimeCatalog.TryResolve(attack, out FpgFormalAttackRuntimeEntry runtimeEntry);
                TryAddTicks(activationTick, attack.FirstReadyOffsetTicks, out TickIndex firstReadyTick);

                int patternIndex = FindFreePattern();
                if (attack.Kind == FpgEnemyAttackKind.Summon
                    && FindSummonAction(attack.Summon) < 0)
                {
                    int actionIndex = FindFreeSummonAction();
                    summonActions[actionIndex] = new SummonActionState(attack.Summon);
                    summonActionCount++;
                }

                bool recursionBlocked = attack.Kind == FpgEnemyAttackKind.Summon
                    && recursionDepth >= attack.Summon.MaxRecursionDepth;
                patterns[patternIndex] = new PatternState(
                    ownerIndex,
                    ordinal,
                    attack,
                    runtimeEntry,
                    firstReadyTick,
                    recursionBlocked);
                patternCount++;
            }

            return DomainResult.Success;
        }

        /// <summary>
        /// Called during death cleanup before Tick. The combat port separately
        /// cancels its queued attacks and threats for the same RuntimeId.
        /// </summary>
        public DomainResult TryUnregisterEnemy(RuntimeId runtimeId)
        {
            int ownerIndex = FindOwner(runtimeId);
            if (ownerIndex < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            for (int index = 0; index < patterns.Length; index++)
            {
                if (!patterns[index].IsUsed || patterns[index].OwnerIndex != ownerIndex)
                {
                    continue;
                }

                patterns[index] = default(PatternState);
                patternCount--;
            }

            owners[ownerIndex] = default(OwnerState);
            ownerCount--;
            return DomainResult.Success;
        }

        /// <summary>
        /// Submits every due, currently eligible pattern. The combat port owns
        /// groggy/death eligibility arbitration and retries accepted commands
        /// when threat slots, projectile budget, or the spawn queue are busy.
        /// </summary>
        public DomainResult Tick(TickIndex tick)
        {
            if (!tick.IsValid || (lastTick.IsValid && tick <= lastTick))
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            lastTick = tick;
            int processed = 0;
            while (processed < patterns.Length)
            {
                int patternIndex = FindBestDuePattern(tick);
                if (patternIndex < 0)
                {
                    return DomainResult.Success;
                }

                processed++;

                PatternState pattern = patterns[patternIndex];
                pattern.LastProcessedTick = tick;
                patterns[patternIndex] = pattern;

                OwnerState owner = owners[pattern.OwnerIndex];
                long scheduleSequence = nextScheduleSequence;
                if (scheduleSequence < 0
                    || scheduleSequence == long.MaxValue
                    || pattern.Occurrence == long.MaxValue)
                {
                    return DomainResult.Rejected(RejectReason.BufferCapacity);
                }

                DomainResult payloadResult = TryBuildPayload(
                    owner,
                    pattern,
                    scheduleSequence,
                    out FpgEnemyAttackPayload payload);
                if (!payloadResult.IsSuccess)
                {
                    return payloadResult;
                }

                int summonActionIndex = -1;
                bool disableAfterSubmit = false;
                int cooldownTicks = pattern.Attack.CooldownTicks;
                TickIndex cooldownAnchor = tick;
                if (pattern.Attack.Kind == FpgEnemyAttackKind.Summon)
                {
                    summonActionIndex =
                        FindSummonAction(pattern.Attack.Summon);
                    if (summonActionIndex < 0)
                    {
                        return DomainResult.Rejected(
                            RejectReason.InvariantFault);
                    }

                    cooldownTicks = Math.Max(
                        cooldownTicks,
                        pattern.Attack.Summon.CooldownTicks);
                    disableAfterSubmit =
                        pattern.Occurrence + 1L
                        >= pattern.Attack.Summon.MaxSummonsPerOwner;
                    if (!TryAddTicks(
                            tick,
                            payload.Summon.ReleaseDelayTicks,
                            out cooldownAnchor))
                    {
                        return DomainResult.Rejected(
                            RejectReason.BufferCapacity);
                    }
                }

                TickIndex nextReadyTick = TickIndex.Invalid;
                if (!disableAfterSubmit
                    && !TryAddTicks(
                        cooldownAnchor,
                        cooldownTicks,
                        out nextReadyTick))
                {
                    return DomainResult.Rejected(
                        RejectReason.BufferCapacity);
                }

                FpgAttackScheduleRequest schedule = new FpgAttackScheduleRequest(
                    owner.RuntimeId,
                    pattern.NextReadyTick,
                    pattern.Attack.Priority,
                    scheduleSequence,
                    pattern.Attack.AttackId);
                DomainResult submitted = combatPort.TrySubmitEnemyAttack(
                    new FpgEnemyAttackCommand(schedule, owner.SpawnSequence, payload));
                if (!submitted.IsSuccess)
                {
                    return submitted;
                }

                nextScheduleSequence++;
                pattern = patterns[patternIndex];
                pattern.Occurrence++;
                if (summonActionIndex >= 0)
                {
                    SummonActionState actionState =
                        summonActions[summonActionIndex];
                    actionState.Occurrence++;
                    summonActions[summonActionIndex] = actionState;
                }

                pattern.IsDisabled = disableAfterSubmit;
                if (!pattern.IsDisabled)
                {
                    pattern.NextReadyTick = nextReadyTick;
                }

                patterns[patternIndex] = pattern;
            }

            return DomainResult.Success;
        }

        public void Clear()
        {
            Array.Clear(owners, 0, owners.Length);
            Array.Clear(patterns, 0, patterns.Length);
            Array.Clear(summonActions, 0, summonActions.Length);
            lastTick = TickIndex.Invalid;
            nextScheduleSequence = 0L;
            ownerCount = 0;
            patternCount = 0;
            summonActionCount = 0;
        }

        private DomainResult TryBuildPayload(
            OwnerState owner,
            PatternState pattern,
            long scheduleSequence,
            out FpgEnemyAttackPayload payload)
        {
            payload = default(FpgEnemyAttackPayload);
            FpgEnemyAttackDefinition attack = pattern.Attack;
            FpgFormalAttackRuntimeEntry runtimeEntry = pattern.RuntimeEntry;

            if (attack.Kind == FpgEnemyAttackKind.Summon)
            {
                return TryBuildSummonPayload(owner, pattern, scheduleSequence, out payload);
            }

            DamageSpec damage = new DamageSpec(
                attack.Damage,
                attack.BreakDamage,
                runtimeEntry.WeakpointDamageMultiplierBasisPoints,
                runtimeEntry.WeakpointBreakMultiplierBasisPoints);

            ThreatPayloadDefinition threatPayload;
            if (attack.Kind == FpgEnemyAttackKind.Projectile)
            {
                ProjectileDefinition projectile = new ProjectileDefinition(
                    attack.ProjectileDefinitionId,
                    new TickDuration(attack.ProjectileFlightTicks),
                    new TickDuration(attack.ProjectileLifetimeTicks),
                    damage,
                    runtimeEntry.ProjectileMaxHitPoints,
                    attack.Interceptable,
                    runtimeEntry.ProjectileBudgetUnits,
                    runtimeEntry.ProjectilePresentationKey,
                    runtimeEntry.ProjectileSweepRadiusKey);
                threatPayload = ThreatPayloadDefinition.SweptProjectile(
                    projectile,
                    attack.ProjectileCount);
            }
            else if (attack.Kind == FpgEnemyAttackKind.TimedImpact)
            {
                threatPayload = ThreatPayloadDefinition.TimedImpact(
                    damage,
                    ThreatTargetPolicy.PlayerCombatant,
                    new TickDuration(runtimeEntry.TimedImpactDelayTicks),
                    runtimeEntry.TimedImpactPresentationKey);
            }
            else
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            ThreatDefinition threat = new ThreatDefinition(
                runtimeEntry.ThreatDefinitionId,
                new TickDuration(attack.TelegraphTicks),
                new TickDuration(attack.WindupTicks),
                new TickDuration(attack.RecoveryTicks),
                threatPayload);
            payload = FpgEnemyAttackPayload.ForThreat(threat);
            return DomainResult.Success;
        }

        private DomainResult TryBuildSummonPayload(
            OwnerState owner,
            PatternState pattern,
            long scheduleSequence,
            out FpgEnemyAttackPayload payload)
        {
            payload = default(FpgEnemyAttackPayload);
            FpgSummonActionDefinition summon = pattern.Attack.Summon;
            if (!TryGetSummonReleaseDelay(
                    pattern.Attack,
                    out int releaseDelayTicks))
            {
                return DomainResult.Rejected(
                    RejectReason.InvalidDefinition);
            }

            if (summon == null
                || owner.RecursionDepth >= summon.MaxRecursionDepth
                || owner.RecursionDepth == int.MaxValue)
            {
                return DomainResult.Rejected(RejectReason.OwnerInterrupted);
            }

            int candidateCount = summon.CandidateEnemies.Length;
            ulong totalWeight = 0UL;
            for (int index = 0; index < candidateCount; index++)
            {
                int weight = summon.GetCandidateWeight(index);
                if (summon.CandidateEnemies[index] == null || weight <= 0
                    || totalWeight > ulong.MaxValue - unchecked((ulong)weight))
                {
                    return DomainResult.Rejected(RejectReason.InvalidDefinition);
                }

                totalWeight += unchecked((ulong)weight);
            }

            if (candidateCount == 0 || totalWeight == 0UL)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            ulong ownerKey = StableHash.Combine(
                unchecked((ulong)owner.SpawnSequence),
                SummonSelectionDomain,
                StableStringHash(pattern.Attack.AttackId),
                unchecked((ulong)pattern.PatternOrdinal));
            ulong random = runContext.DeriveSeed(
                SummonSelectionDomain,
                ownerKey,
                unchecked((ulong)pattern.Occurrence));
            ulong selectedWeight = random % totalWeight;
            FpgEnemyDefinition selected = null;
            for (int index = 0; index < candidateCount; index++)
            {
                ulong weight = unchecked((ulong)summon.GetCandidateWeight(index));
                if (selectedWeight < weight)
                {
                    selected = summon.CandidateEnemies[index];
                    break;
                }

                selectedWeight -= weight;
            }

            if (selected == null || string.IsNullOrWhiteSpace(selected.EnemyDefinitionId))
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            FpgSummonRequest request = new FpgSummonRequest(
                owner.RuntimeId,
                selected.EnemyDefinitionId,
                owner.RecursionDepth + 1,
                scheduleSequence,
                summon.MaxSummonsPerOwner);
            payload = FpgEnemyAttackPayload.ForSummon(
                new FpgFormalSummonPayload(
                    request,
                    summon.MaxSummonsPerOwner,
                    releaseDelayTicks,
                    pattern.Attack.SummonOwnerOutcome));
            return DomainResult.Success;
        }

        private int FindBestDuePattern(TickIndex tick)
        {
            int best = -1;
            for (int index = 0; index < patterns.Length; index++)
            {
                PatternState candidate = patterns[index];
                if (!candidate.IsUsed
                    || candidate.IsDisabled
                    || candidate.NextReadyTick > tick
                    || candidate.LastProcessedTick == tick)
                {
                    continue;
                }

                OwnerState owner = owners[candidate.OwnerIndex];
                if (candidate.Attack.Kind == FpgEnemyAttackKind.Summon
                    && CountSummonOccurrences(candidate.Attack.Summon)
                        >= candidate.Attack.Summon.MaxTotalSummonsPerEncounter)
                {
                    continue;
                }

                if (!owner.IsUsed || !combatPort.CanAttack(owner.RuntimeId))
                {
                    continue;
                }

                if (best < 0 || Compare(candidate, patterns[best]) < 0)
                {
                    best = index;
                }
            }

            return best;
        }

        private int Compare(PatternState left, PatternState right)
        {
            int comparison = left.NextReadyTick.Value.CompareTo(right.NextReadyTick.Value);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = owners[left.OwnerIndex].SpawnSequence.CompareTo(
                owners[right.OwnerIndex].SpawnSequence);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Attack.Priority.CompareTo(right.Attack.Priority);
            return comparison != 0
                ? comparison
                : left.PatternOrdinal.CompareTo(right.PatternOrdinal);
        }

        private long CountSummonOccurrences(FpgSummonActionDefinition action)
        {
            int index = FindSummonAction(action);
            return index < 0 ? 0L : summonActions[index].Occurrence;
        }

        private int CountNewSummonActions(FpgEnemyDefinition definition)
        {
            int count = 0;
            for (int attackIndex = 0; attackIndex < definition.AttackPatternCount; attackIndex++)
            {
                FpgEnemyAttackDefinition attack = definition.GetAttackPattern(attackIndex);
                if (attack == null || attack.Kind != FpgEnemyAttackKind.Summon
                    || attack.Summon == null
                    || FindSummonAction(attack.Summon) >= 0)
                {
                    continue;
                }

                bool repeatedEarlier = false;
                for (int previous = 0; previous < attackIndex; previous++)
                {
                    FpgEnemyAttackDefinition prior = definition.GetAttackPattern(previous);
                    if (prior != null && prior.Kind == FpgEnemyAttackKind.Summon
                        && prior.Summon == attack.Summon)
                    {
                        repeatedEarlier = true;
                        break;
                    }
                }

                if (!repeatedEarlier)
                {
                    count++;
                }
            }

            return count;
        }

        private int FindSummonAction(FpgSummonActionDefinition action)
        {
            if (action == null)
            {
                return -1;
            }

            for (int index = 0; index < summonActions.Length; index++)
            {
                if (summonActions[index].IsUsed && summonActions[index].Action == action)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeSummonAction()
        {
            for (int index = 0; index < summonActions.Length; index++)
            {
                if (!summonActions[index].IsUsed)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindOwner(RuntimeId runtimeId)
        {
            if (!runtimeId.IsValid)
            {
                return -1;
            }

            for (int index = 0; index < owners.Length; index++)
            {
                if (owners[index].IsUsed && owners[index].RuntimeId == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindOwnerBySpawnSequence(int spawnSequence)
        {
            for (int index = 0; index < owners.Length; index++)
            {
                if (owners[index].IsUsed && owners[index].SpawnSequence == spawnSequence)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeOwner()
        {
            for (int index = 0; index < owners.Length; index++)
            {
                if (!owners[index].IsUsed)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreePattern()
        {
            for (int index = 0; index < patterns.Length; index++)
            {
                if (!patterns[index].IsUsed)
                {
                    return index;
                }
            }

            return -1;
        }

        private int CountFreePatterns()
        {
            return patterns.Length - patternCount;
        }

        private static bool TryGetSummonReleaseDelay(
            FpgEnemyAttackDefinition attack,
            out int releaseDelayTicks)
        {
            releaseDelayTicks = 0;
            if (attack == null
                || attack.TelegraphTicks < 0
                || attack.WindupTicks < 0
                || attack.TelegraphTicks
                    > int.MaxValue - attack.WindupTicks)
            {
                return false;
            }

            releaseDelayTicks =
                attack.TelegraphTicks + attack.WindupTicks;
            return true;
        }

        private static bool TryAddTicks(TickIndex start, int duration, out TickIndex result)
        {
            if (!start.IsValid || duration < 0 || start.Value > long.MaxValue - duration)
            {
                result = TickIndex.Invalid;
                return false;
            }

            result = new TickIndex(start.Value + duration);
            return true;
        }

        private static ulong StableStringHash(string value)
        {
            ulong hash = StableHash.Mix(0x4650475F4154544BUL);
            if (value == null)
            {
                return hash;
            }

            for (int index = 0; index < value.Length; index++)
            {
                hash = StableHash.Append(hash, value[index]);
            }

            return hash;
        }

        private readonly struct OwnerState
        {
            public OwnerState(
                RuntimeId runtimeId,
                int spawnSequence,
                TickIndex activationTick,
                int recursionDepth,
                FpgEnemyDefinition definition)
            {
                RuntimeId = runtimeId;
                SpawnSequence = spawnSequence;
                ActivationTick = activationTick;
                RecursionDepth = recursionDepth;
                Definition = definition;
                IsUsed = true;
            }

            public RuntimeId RuntimeId { get; }
            public int SpawnSequence { get; }
            public TickIndex ActivationTick { get; }
            public int RecursionDepth { get; }
            public FpgEnemyDefinition Definition { get; }
            public bool IsUsed { get; }
        }

        private struct SummonActionState
        {
            public SummonActionState(FpgSummonActionDefinition action)
            {
                Action = action;
                Occurrence = 0L;
                IsUsed = true;
            }

            public FpgSummonActionDefinition Action;
            public long Occurrence;
            public bool IsUsed;
        }

        private struct PatternState
        {
            public PatternState(
                int ownerIndex,
                int patternOrdinal,
                FpgEnemyAttackDefinition attack,
                FpgFormalAttackRuntimeEntry runtimeEntry,
                TickIndex nextReadyTick,
                bool isDisabled)
            {
                OwnerIndex = ownerIndex;
                PatternOrdinal = patternOrdinal;
                Attack = attack;
                RuntimeEntry = runtimeEntry;
                NextReadyTick = nextReadyTick;
                LastProcessedTick = TickIndex.Invalid;
                Occurrence = 0L;
                IsDisabled = isDisabled;
                IsUsed = true;
            }

            public int OwnerIndex;
            public int PatternOrdinal;
            public FpgEnemyAttackDefinition Attack;
            public FpgFormalAttackRuntimeEntry RuntimeEntry;
            public TickIndex NextReadyTick;
            public TickIndex LastProcessedTick;
            public long Occurrence;
            public bool IsDisabled;
            public bool IsUsed;
        }
    }
}
