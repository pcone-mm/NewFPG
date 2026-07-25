using System;
using System.Collections.Generic;
using FPG.Demo.Core;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public sealed class FpgCompiledEnemySkillDefinition
    {
        private readonly FpgCompiledEnemySkillPayloadSlot[] payloadSlots;

        internal FpgCompiledEnemySkillDefinition(
            FpgCompiledSkillDefinition timeline,
            int priority,
            int firstReadyOffsetTicks,
            int sequenceCooldownTicks,
            FpgCompiledEnemySkillPayloadSlot[] payloadSlots,
            int totalProjectileCapacity,
            int totalImpactCapacity,
            int totalSummonCapacity,
            int maxHitCount,
            int lastAttackTick)
        {
            Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
            if (firstReadyOffsetTicks < 0
                || sequenceCooldownTicks < 0
                || payloadSlots == null
                || payloadSlots.Length == 0
                || totalProjectileCapacity < 0
                || totalImpactCapacity < 0
                || totalSummonCapacity < 0
                || maxHitCount < 0
                || lastAttackTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequenceCooldownTicks));
            }

            this.payloadSlots = CopyAndSortPayloadSlots(payloadSlots);
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
                this.payloadSlots);
        }

        public FpgCompiledSkillDefinition Timeline { get; }
        public int Priority { get; }
        public int FirstReadyOffsetTicks { get; }
        public ulong GameplayHash { get; }

        /// <summary>
        /// Cooldown starts when the selected sequence reaches its end tick.
        /// </summary>
        public int SequenceCooldownTicks { get; }

        public IReadOnlyList<FpgCompiledEnemySkillPayloadSlot> PayloadSlots =>
            payloadSlots;
        public int PayloadSlotCount => payloadSlots.Length;
        public int TotalProjectileCapacity { get; }
        public int TotalImpactCapacity { get; }
        public int TotalSummonCapacity { get; }
        public int MaxHitCount { get; }
        public int LastAttackTick { get; }

        public bool TryGetPayloadSlot(
            int slotId,
            out FpgCompiledEnemySkillPayloadSlot payloadSlot)
        {
            for (int index = 0; index < payloadSlots.Length; index++)
            {
                if (payloadSlots[index].SlotId == slotId)
                {
                    payloadSlot = payloadSlots[index];
                    return true;
                }
            }

            payloadSlot = default(FpgCompiledEnemySkillPayloadSlot);
            return false;
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
            FpgCompiledEnemySkillPayloadSlot[] values)
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
            hash = StableHash.Append(hash, unchecked((ulong)values.Length));

            for (int index = 0; index < values.Length; index++)
            {
                FpgCompiledEnemySkillPayloadSlot payload = values[index];
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)payload.SlotId));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)(int)payload.Kind));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)payload.ThreatDefinitionId));

                if (payload.Kind != FpgEnemySkillPayloadKind.Summon)
                {
                    hash = payload.ThreatPayload.AppendStableHash(hash);
                    continue;
                }

                FpgCompiledEnemySummonPayload summon =
                    payload.SummonPayload;
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
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)summon.MaxRecursionDepth));
            }

            return hash;
        }


        private static FpgCompiledEnemySkillPayloadSlot[] CopyAndSortPayloadSlots(
            FpgCompiledEnemySkillPayloadSlot[] source)
        {
            FpgCompiledEnemySkillPayloadSlot[] copy =
                new FpgCompiledEnemySkillPayloadSlot[source.Length];
            Array.Copy(source, copy, source.Length);
            for (int index = 1; index < copy.Length; index++)
            {
                FpgCompiledEnemySkillPayloadSlot value = copy[index];
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
                        "Compiled enemy skill repeats a payload slot ID.",
                        nameof(source));
                }
            }

            return copy;
        }
    }

    /// <summary>
    /// One reusable enemy skill: an authored timeline plus local typed payload
    /// slots. This is the only enemy attack authoring and runtime contract.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FpgEnemyAttackDefinition",
        menuName = "FPG Demo/Skills/Enemy Skill")]
    public sealed class FpgEnemyAttackDefinition : FpgSkillTimelineDefinition
    {
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

        [D0PlannerField(
            "Payload Slots",
            "Local typed payloads referenced by stable IDs from timeline logic events.")]
        [SerializeField]
        private FpgEnemySkillPayloadSlot[] payloadSlots =
        {
            new FpgEnemySkillPayloadSlot()
        };

        public int Priority => priority;
        public int FirstReadyOffsetTicks => firstReadyOffsetTicks;
        public int SequenceCooldownTicks => sequenceCooldownTicks;
        public IReadOnlyList<FpgEnemySkillPayloadSlot> PayloadSlots =>
            payloadSlots ?? Array.Empty<FpgEnemySkillPayloadSlot>();

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
                FpgEnemySkillPayloadSlot[] values =
                    payloadSlots ?? Array.Empty<FpgEnemySkillPayloadSlot>();
                FpgCompiledEnemySkillPayloadSlot[] compiled =
                    new FpgCompiledEnemySkillPayloadSlot[values.Length];
                for (int index = 0; index < values.Length; index++)
                {
                    compiled[index] = values[index].Compile();
                }

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
                    if (skillEvent.Kind != FpgSkillEventKind.GameplayPayload)
                    {
                        continue;
                    }

                    if (!TryGetPayloadSlot(
                            compiled,
                            skillEvent.PayloadSlotId,
                            out FpgCompiledEnemySkillPayloadSlot payload))
                    {
                        throw new InvalidOperationException(
                            $"Enemy skill '{SkillId}' compiled an unresolved payload slot.");
                    }

                    totalProjectileCapacity = checked(
                        totalProjectileCapacity + payload.ProjectileCapacity);
                    totalImpactCapacity = checked(
                        totalImpactCapacity + payload.ImpactCapacity);
                    totalSummonCapacity = checked(
                        totalSummonCapacity + payload.SummonCapacity);
                    maxHitCount = Math.Max(maxHitCount, payload.MaxHitCount);
                    lastAttackTick = Math.Max(lastAttackTick, skillEvent.Tick);
                }

                definition = new FpgCompiledEnemySkillDefinition(
                    timeline,
                    priority,
                    firstReadyOffsetTicks,
                    sequenceCooldownTicks,
                    compiled,
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

        protected override bool TryValidatePayloadSlots(out string error)
        {
            FpgEnemySkillPayloadSlot[] values =
                payloadSlots ?? Array.Empty<FpgEnemySkillPayloadSlot>();
            if (values.Length == 0)
            {
                error = $"Enemy skill '{SkillId}' requires at least one typed payload slot.";
                return false;
            }

            HashSet<string> slotIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> compiledSlotIds = new HashSet<int>();
            HashSet<int> threatDefinitionIds = new HashSet<int>();
            for (int index = 0; index < values.Length; index++)
            {
                FpgEnemySkillPayloadSlot value = values[index];
                if (value == null)
                {
                    error = $"Enemy skill '{SkillId}' has a missing payload slot at index {index}.";
                    return false;
                }

                if (!value.TryValidate(out error))
                {
                    return false;
                }

                int compiledId = FpgSkillStableId.CompilePayloadSlot(value.SlotId);
                if (!slotIds.Add(value.SlotId)
                    || !compiledSlotIds.Add(compiledId))
                {
                    error = $"Enemy skill '{SkillId}' repeats payload slot '{value.SlotId}' or has a stable-ID collision.";
                    return false;
                }

                if (value.Kind != FpgEnemySkillPayloadKind.Summon
                    && !threatDefinitionIds.Add(value.ThreatDefinitionId))
                {
                    error = $"Enemy skill '{SkillId}' repeats threat definition ID {value.ThreatDefinitionId}.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        protected override bool ContainsPayloadSlot(string payloadSlotId)
        {
            FpgEnemySkillPayloadSlot[] values =
                payloadSlots ?? Array.Empty<FpgEnemySkillPayloadSlot>();
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
            if (firstReadyOffsetTicks < 0
                || sequenceCooldownTicks < 0)
            {
                error = $"Enemy skill '{SkillId}' has invalid scheduling values.";
                return false;
            }

            bool hasGameplayEvent = false;
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

                for (int eventIndex = 0;
                    eventIndex < sequence.LogicEvents.Count;
                    eventIndex++)
                {
                    FpgSkillLogicEventDefinition skillEvent =
                        sequence.LogicEvents[eventIndex];
                    error = string.Empty;
                    if (!TryGetAuthoredPayloadSlot(
                            skillEvent.PayloadSlotId,
                            out FpgEnemySkillPayloadSlot payload)
                        || !FpgEnemySkillSpatialPolicy.TryValidate(
                            payload.Kind,
                            skillEvent,
                            out error))
                    {
                        if (string.IsNullOrEmpty(error))
                        {
                            error = $"Enemy skill '{SkillId}' event '{skillEvent?.EventId ?? "<missing>"}' has invalid spatial metadata.";
                        }

                        return false;
                    }

                    hasGameplayEvent = true;
                }
            }

            if (hasGameplayEvent)
            {
                error = string.Empty;
                return true;
            }

            error = $"Enemy skill '{SkillId}' requires at least one gameplay payload event in Execute.";
            return false;
        }

        private bool TryGetAuthoredPayloadSlot(
            string slotId,
            out FpgEnemySkillPayloadSlot payload)
        {
            FpgEnemySkillPayloadSlot[] values =
                payloadSlots ?? Array.Empty<FpgEnemySkillPayloadSlot>();
            for (int index = 0; index < values.Length; index++)
            {
                FpgEnemySkillPayloadSlot candidate = values[index];
                if (candidate != null
                    && string.Equals(
                        candidate.SlotId,
                        slotId,
                        StringComparison.Ordinal))
                {
                    payload = candidate;
                    return true;
                }
            }

            payload = null;
            return false;
        }
        private static bool TryGetPayloadSlot(
            FpgCompiledEnemySkillPayloadSlot[] values,
            int slotId,
            out FpgCompiledEnemySkillPayloadSlot payloadSlot)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index].SlotId == slotId)
                {
                    payloadSlot = values[index];
                    return true;
                }
            }

            payloadSlot = default(FpgCompiledEnemySkillPayloadSlot);
            return false;
        }
    }
}
