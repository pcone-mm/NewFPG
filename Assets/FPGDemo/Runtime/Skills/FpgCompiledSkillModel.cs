using System;
using System.Collections.Generic;
using FPG.Demo.Core;

namespace FPG.Demo.Skills
{
    public readonly struct FpgCompiledSkillEvent : IEquatable<FpgCompiledSkillEvent>
    {
        public FpgCompiledSkillEvent(
            int eventId,
            int tick,
            FpgSkillEventKind kind,
            int payloadSlotId,
            int cueId,
            int warningId,
            int sortOrder = 0,
            int socketId = 0,
            FpgSkillTargetSource targetSource = FpgSkillTargetSource.CurrentAim,
            int offsetXMillimeters = 0,
            int offsetYMillimeters = 0,
            int offsetZMillimeters = 0,
            int boundGameplayEventId = 0)
        {
            EventId = eventId;
            Tick = tick;
            Kind = kind;
            PayloadSlotId = payloadSlotId;
            CueId = cueId;
            WarningId = warningId;
            SortOrder = sortOrder;
            SocketId = socketId;
            TargetSource = targetSource;
            Offset = new FpgSkillOffset(
                offsetXMillimeters,
                offsetYMillimeters,
                offsetZMillimeters);
            BoundGameplayEventId = boundGameplayEventId;
        }

        public int EventId { get; }

        public int Tick { get; }

        public FpgSkillEventKind Kind { get; }

        public int PayloadSlotId { get; }

        public int CueId { get; }

        public int WarningId { get; }

        public int SortOrder { get; }

        public int SocketId { get; }

        public FpgSkillTargetSource TargetSource { get; }

        public FpgSkillOffset Offset { get; }

        public int BoundGameplayEventId { get; }

        public bool IsValid => FpgSkillCompiler.ValidateEvent(this, int.MaxValue, -1).IsValid;

        public bool Equals(FpgCompiledSkillEvent other)
        {
            return EventId == other.EventId
                && Tick == other.Tick
                && Kind == other.Kind
                && PayloadSlotId == other.PayloadSlotId
                && CueId == other.CueId
                && WarningId == other.WarningId
                && SortOrder == other.SortOrder
                && SocketId == other.SocketId
                && TargetSource == other.TargetSource
                && Offset.Equals(other.Offset)
                && BoundGameplayEventId == other.BoundGameplayEventId;
        }

        public override bool Equals(object obj)
        {
            return obj is FpgCompiledSkillEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = EventId;
                hash = (hash * 397) ^ Tick;
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ PayloadSlotId;
                hash = (hash * 397) ^ CueId;
                hash = (hash * 397) ^ WarningId;
                hash = (hash * 397) ^ SortOrder;
                hash = (hash * 397) ^ SocketId;
                hash = (hash * 397) ^ (int)TargetSource;
                hash = (hash * 397) ^ Offset.GetHashCode();
                return (hash * 397) ^ BoundGameplayEventId;
            }
        }

        public static bool operator ==(FpgCompiledSkillEvent left, FpgCompiledSkillEvent right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FpgCompiledSkillEvent left, FpgCompiledSkillEvent right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct FpgCompiledSkillPhase : IEquatable<FpgCompiledSkillPhase>
    {
        public FpgCompiledSkillPhase(
            int phaseId,
            FpgSkillPhaseKind kind,
            int startTick,
            int endTick)
        {
            PhaseId = phaseId;
            Kind = kind;
            StartTick = startTick;
            EndTick = endTick;
        }

        public int PhaseId { get; }

        public FpgSkillPhaseKind Kind { get; }

        public int StartTick { get; }

        public int EndTick { get; }

        public bool IsValid =>
            FpgSkillCompiler.ValidatePhase(this, int.MaxValue, -1).IsValid;

        public bool Equals(FpgCompiledSkillPhase other)
        {
            return PhaseId == other.PhaseId
                && Kind == other.Kind
                && StartTick == other.StartTick
                && EndTick == other.EndTick;
        }

        public override bool Equals(object obj)
        {
            return obj is FpgCompiledSkillPhase other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = PhaseId;
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ StartTick;
                return (hash * 397) ^ EndTick;
            }
        }

        public static bool operator ==(
            FpgCompiledSkillPhase left,
            FpgCompiledSkillPhase right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            FpgCompiledSkillPhase left,
            FpgCompiledSkillPhase right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct FpgCompiledSkillSequence
    {
        private static readonly FpgCompiledSkillEvent[] EmptyEvents =
            Array.Empty<FpgCompiledSkillEvent>();
        private static readonly FpgCompiledSkillPhase[] EmptyPhases =
            Array.Empty<FpgCompiledSkillPhase>();
        private static readonly int[] EmptyAnimations = Array.Empty<int>();
        private readonly FpgCompiledSkillEvent[] events;
        private readonly FpgCompiledSkillPhase[] phases;
        private readonly int[] animationVariants;

        public FpgCompiledSkillSequence(
            FpgSkillSequenceKind kind,
            int durationTicks,
            int mainAnimation,
            bool loop,
            FpgCompiledSkillEvent[] events)
            : this(
                kind,
                durationTicks,
                mainAnimation,
                loop,
                FpgSkillAnimationPlaybackMode.NaturalSpeed,
                0,
                durationTicks,
                null,
                events)
        {
        }

        public FpgCompiledSkillSequence(
            FpgSkillSequenceKind kind,
            int durationTicks,
            int mainAnimation,
            bool loop,
            FpgSkillAnimationPlaybackMode animationPlaybackMode,
            int animationStartTick,
            int animationEndTick,
            FpgCompiledSkillEvent[] events)
            : this(
                kind,
                durationTicks,
                mainAnimation,
                loop,
                animationPlaybackMode,
                animationStartTick,
                animationEndTick,
                null,
                events)
        {
        }

        public FpgCompiledSkillSequence(
            FpgSkillSequenceKind kind,
            int durationTicks,
            int mainAnimation,
            bool loop,
            FpgCompiledSkillPhase[] phases,
            FpgCompiledSkillEvent[] events)
            : this(
                kind,
                durationTicks,
                mainAnimation,
                loop,
                FpgSkillAnimationPlaybackMode.NaturalSpeed,
                0,
                durationTicks,
                null,
                phases,
                events)
        {
        }

        public FpgCompiledSkillSequence(
            FpgSkillSequenceKind kind,
            int durationTicks,
            int mainAnimation,
            bool loop,
            FpgSkillAnimationPlaybackMode animationPlaybackMode,
            int animationStartTick,
            int animationEndTick,
            int[] alternateAnimations,
            FpgCompiledSkillEvent[] events)
            : this(
                kind,
                durationTicks,
                mainAnimation,
                loop,
                animationPlaybackMode,
                animationStartTick,
                animationEndTick,
                alternateAnimations,
                null,
                events)
        {
        }

        public FpgCompiledSkillSequence(
            FpgSkillSequenceKind kind,
            int durationTicks,
            int mainAnimation,
            bool loop,
            FpgSkillAnimationPlaybackMode animationPlaybackMode,
            int animationStartTick,
            int animationEndTick,
            int[] alternateAnimations,
            FpgCompiledSkillPhase[] phases,
            FpgCompiledSkillEvent[] events)
        {
            FpgCompiledSkillPhase[] phaseValues = phases ?? EmptyPhases;
            FpgSkillValidationResult validation = FpgSkillCompiler.ValidateSequence(
                kind,
                durationTicks,
                mainAnimation,
                phaseValues,
                events,
                -1);
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ToString(), nameof(events));
            }

            if (!Enum.IsDefined(
                    typeof(FpgSkillAnimationPlaybackMode),
                    animationPlaybackMode)
                || animationStartTick < 0
                || animationEndTick < animationStartTick
                || animationEndTick > durationTicks)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(animationPlaybackMode));
            }

            Kind = kind;
            DurationTicks = durationTicks;
            MainAnimation = mainAnimation;
            Loop = loop;
            AnimationPlaybackMode = animationPlaybackMode;
            AnimationStartTick = animationStartTick;
            AnimationEndTick = animationEndTick;
            animationVariants = CopyAnimationVariants(
                mainAnimation,
                alternateAnimations);
            this.phases = FpgSkillCompiler.CopyPhases(phaseValues);
            this.events = FpgSkillCompiler.CopyAndSortEvents(events);
            GameplayHash = FpgSkillCompiler.ComputeSequenceHash(
                kind,
                durationTicks,
                mainAnimation,
                loop,
                animationPlaybackMode,
                animationStartTick,
                animationEndTick,
                animationVariants,
                this.phases,
                this.events);
        }

        public FpgSkillSequenceKind Kind { get; }

        public int DurationTicks { get; }

        public int MainAnimation { get; }

        public bool Loop { get; }

        public FpgSkillAnimationPlaybackMode AnimationPlaybackMode { get; }

        public int AnimationStartTick { get; }

        public int AnimationEndTick { get; }

        public IReadOnlyList<int> AnimationVariants =>
            animationVariants ?? EmptyAnimations;

        public int AnimationVariantCount =>
            animationVariants == null ? 0 : animationVariants.Length;

        public IReadOnlyList<FpgCompiledSkillPhase> Phases =>
            phases ?? EmptyPhases;

        public int PhaseCount => phases == null ? 0 : phases.Length;

        public IReadOnlyList<FpgCompiledSkillEvent> Events => events ?? EmptyEvents;

        public int EventCount => events == null ? 0 : events.Length;

        public ulong GameplayHash { get; }

        public bool IsValid => Kind != FpgSkillSequenceKind.None
            && DurationTicks >= 0
            && MainAnimation > 0
            && Enum.IsDefined(
                typeof(FpgSkillAnimationPlaybackMode),
                AnimationPlaybackMode)
            && AnimationStartTick >= 0
            && AnimationEndTick >= AnimationStartTick
            && AnimationEndTick <= DurationTicks
            && animationVariants != null
            && animationVariants.Length > 0
            && phases != null
            && events != null;

        public int GetAnimationVariant(int index)
        {
            if (animationVariants == null
                || index < 0
                || index >= animationVariants.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return animationVariants[index];
        }

        public int ResolveAnimation(SkillExecutionId executionId)
        {
            if (!executionId.IsValid
                || animationVariants == null
                || animationVariants.Length == 0)
            {
                return MainAnimation;
            }

            int index = (int)((executionId.Value - 1L)
                % animationVariants.Length);
            return animationVariants[index];
        }

        public FpgCompiledSkillPhase GetPhase(int index)
        {
            if (phases == null || index < 0 || index >= phases.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return phases[index];
        }

        public FpgCompiledSkillEvent GetEvent(int index)
        {
            if (events == null || index < 0 || index >= events.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return events[index];
        }

        public static bool TryCreate(
            FpgSkillSequenceKind kind,
            int durationTicks,
            int mainAnimation,
            bool loop,
            FpgCompiledSkillEvent[] events,
            out FpgCompiledSkillSequence sequence,
            out FpgSkillValidationResult validation)
        {
            return TryCreate(
                kind,
                durationTicks,
                mainAnimation,
                loop,
                null,
                events,
                out sequence,
                out validation);
        }

        public static bool TryCreate(
            FpgSkillSequenceKind kind,
            int durationTicks,
            int mainAnimation,
            bool loop,
            FpgCompiledSkillPhase[] phases,
            FpgCompiledSkillEvent[] events,
            out FpgCompiledSkillSequence sequence,
            out FpgSkillValidationResult validation)
        {
            FpgCompiledSkillPhase[] phaseValues = phases ?? EmptyPhases;
            validation = FpgSkillCompiler.ValidateSequence(
                kind,
                durationTicks,
                mainAnimation,
                phaseValues,
                events,
                -1);
            if (!validation.IsValid)
            {
                sequence = default(FpgCompiledSkillSequence);
                return false;
            }

            sequence = new FpgCompiledSkillSequence(
                kind,
                durationTicks,
                mainAnimation,
                loop,
                phaseValues,
                events);
            return true;
        }

        private static int[] CopyAnimationVariants(
            int mainAnimation,
            int[] alternateAnimations)
        {
            int alternateCount = alternateAnimations == null
                ? 0
                : alternateAnimations.Length;
            int[] values = new int[alternateCount + 1];
            values[0] = mainAnimation;
            for (int index = 0; index < alternateCount; index++)
            {
                int animation = alternateAnimations[index];
                if (animation <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(alternateAnimations));
                }

                for (int existingIndex = 0;
                    existingIndex <= index;
                    existingIndex++)
                {
                    if (values[existingIndex] == animation)
                    {
                        throw new ArgumentException(
                            "Skill animation variants must be unique.",
                            nameof(alternateAnimations));
                    }
                }

                values[index + 1] = animation;
            }

            return values;
        }

    }

    public sealed class FpgCompiledSkillDefinition
    {
        private static readonly FpgCompiledSkillSequence[] EmptySequences = Array.Empty<FpgCompiledSkillSequence>();
        private readonly FpgCompiledSkillSequence[] sequences;

        public FpgCompiledSkillDefinition(int skillId, FpgCompiledSkillSequence[] sequences)
        {
            FpgSkillValidationResult validation = FpgSkillCompiler.ValidateDefinition(skillId, sequences);
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ToString(), nameof(sequences));
            }

            SkillId = skillId;
            this.sequences = FpgSkillCompiler.CopyAndSortSequences(sequences);
            GameplayHash = FpgSkillCompiler.ComputeDefinitionHash(skillId, this.sequences);
        }

        public int SkillId { get; }

        public IReadOnlyList<FpgCompiledSkillSequence> Sequences => sequences ?? EmptySequences;

        public int SequenceCount => sequences == null ? 0 : sequences.Length;

        public ulong GameplayHash { get; }

        public bool IsValid => SkillId > 0 && sequences != null && sequences.Length > 0;

        public FpgCompiledSkillSequence GetSequence(int index)
        {
            if (sequences == null || index < 0 || index >= sequences.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return sequences[index];
        }

        public bool TryGetSequence(FpgSkillSequenceKind kind, out FpgCompiledSkillSequence sequence)
        {
            if (sequences != null)
            {
                for (int index = 0; index < sequences.Length; index++)
                {
                    if (sequences[index].Kind == kind)
                    {
                        sequence = sequences[index];
                        return true;
                    }
                }
            }

            sequence = default(FpgCompiledSkillSequence);
            return false;
        }

        public static bool TryCreate(
            int skillId,
            FpgCompiledSkillSequence[] sequences,
            out FpgCompiledSkillDefinition definition,
            out FpgSkillValidationResult validation)
        {
            validation = FpgSkillCompiler.ValidateDefinition(skillId, sequences);
            if (!validation.IsValid)
            {
                definition = null;
                return false;
            }

            definition = new FpgCompiledSkillDefinition(skillId, sequences);
            return true;
        }
    }

    public static class FpgSkillCompiler
    {
        private const ulong SequenceHashSeed = 0x4650475F53455131UL;
        private const ulong DefinitionHashSeed = 0x4650475F534B4C31UL;

        public static bool TryCompileSequence(
            FpgSkillSequenceKind kind,
            int durationTicks,
            int mainAnimation,
            bool loop,
            FpgCompiledSkillEvent[] events,
            out FpgCompiledSkillSequence sequence,
            out FpgSkillValidationResult validation)
        {
            return FpgCompiledSkillSequence.TryCreate(
                kind,
                durationTicks,
                mainAnimation,
                loop,
                events,
                out sequence,
                out validation);
        }

        public static bool TryCompileSequence(
            FpgSkillSequenceKind kind,
            int durationTicks,
            int mainAnimation,
            bool loop,
            FpgCompiledSkillPhase[] phases,
            FpgCompiledSkillEvent[] events,
            out FpgCompiledSkillSequence sequence,
            out FpgSkillValidationResult validation)
        {
            return FpgCompiledSkillSequence.TryCreate(
                kind,
                durationTicks,
                mainAnimation,
                loop,
                phases,
                events,
                out sequence,
                out validation);
        }

        public static bool TryCompileDefinition(
            int skillId,
            FpgCompiledSkillSequence[] sequences,
            out FpgCompiledSkillDefinition definition,
            out FpgSkillValidationResult validation)
        {
            return FpgCompiledSkillDefinition.TryCreate(skillId, sequences, out definition, out validation);
        }

        internal static FpgSkillValidationResult ValidateDefinition(
            int skillId,
            FpgCompiledSkillSequence[] sequences)
        {
            if (skillId <= 0)
            {
                return Invalid(FpgSkillValidationError.InvalidSkillId, -1, -1, skillId);
            }

            if (sequences == null)
            {
                return Invalid(FpgSkillValidationError.NullSequences, -1, -1, 0);
            }

            if (sequences.Length == 0)
            {
                return Invalid(FpgSkillValidationError.EmptySequences, -1, -1, 0);
            }

            bool hasExecute = false;
            for (int index = 0; index < sequences.Length; index++)
            {
                FpgCompiledSkillSequence sequence = sequences[index];
                if (!sequence.IsValid)
                {
                    return Invalid(FpgSkillValidationError.InvalidSequence, index, -1, (int)sequence.Kind);
                }

                if (!Enum.IsDefined(typeof(FpgSkillSequenceKind), sequence.Kind)
                    || sequence.Kind == FpgSkillSequenceKind.None)
                {
                    return Invalid(FpgSkillValidationError.InvalidSequenceKind, index, -1, (int)sequence.Kind);
                }

                if (sequence.Kind == FpgSkillSequenceKind.Execute)
                {
                    hasExecute = true;
                }

                for (int otherIndex = 0; otherIndex < index; otherIndex++)
                {
                    if (sequences[otherIndex].Kind == sequence.Kind)
                    {
                        return Invalid(
                            FpgSkillValidationError.DuplicateSequenceKind,
                            index,
                            -1,
                            (int)sequence.Kind);
                    }
                }
            }

            if (!hasExecute)
            {
                return Invalid(FpgSkillValidationError.MissingExecuteSequence, -1, -1, 0);
            }

            return FpgSkillValidationResult.Valid;
        }

        internal static FpgSkillValidationResult ValidateSequence(
            FpgSkillSequenceKind kind,
            int durationTicks,
            int mainAnimation,
            FpgCompiledSkillEvent[] events,
            int sequenceIndex)
        {
            return ValidateSequence(
                kind,
                durationTicks,
                mainAnimation,
                Array.Empty<FpgCompiledSkillPhase>(),
                events,
                sequenceIndex);
        }

        internal static FpgSkillValidationResult ValidateSequence(
            FpgSkillSequenceKind kind,
            int durationTicks,
            int mainAnimation,
            FpgCompiledSkillPhase[] phases,
            FpgCompiledSkillEvent[] events,
            int sequenceIndex)
        {
            if (!Enum.IsDefined(typeof(FpgSkillSequenceKind), kind)
                || kind == FpgSkillSequenceKind.None)
            {
                return Invalid(
                    FpgSkillValidationError.InvalidSequenceKind,
                    sequenceIndex,
                    -1,
                    (int)kind);
            }

            if (durationTicks < 0)
            {
                return Invalid(
                    FpgSkillValidationError.InvalidDurationTicks,
                    sequenceIndex,
                    -1,
                    durationTicks);
            }

            if (mainAnimation <= 0)
            {
                return Invalid(
                    FpgSkillValidationError.InvalidMainAnimation,
                    sequenceIndex,
                    -1,
                    mainAnimation);
            }

            FpgCompiledSkillPhase[] phaseValues =
                phases ?? Array.Empty<FpgCompiledSkillPhase>();
            for (int phaseIndex = 0;
                phaseIndex < phaseValues.Length;
                phaseIndex++)
            {
                FpgCompiledSkillPhase phase = phaseValues[phaseIndex];
                FpgSkillValidationResult phaseValidation = ValidatePhase(
                    phase,
                    durationTicks,
                    phaseIndex,
                    sequenceIndex);
                if (!phaseValidation.IsValid)
                {
                    return phaseValidation;
                }

                for (int otherIndex = 0;
                    otherIndex < phaseIndex;
                    otherIndex++)
                {
                    FpgCompiledSkillPhase other = phaseValues[otherIndex];
                    if (other.PhaseId == phase.PhaseId)
                    {
                        return Invalid(
                            FpgSkillValidationError.DuplicatePhaseId,
                            sequenceIndex,
                            phaseIndex,
                            phase.PhaseId);
                    }

                    if (other.Kind == phase.Kind)
                    {
                        return Invalid(
                            FpgSkillValidationError.DuplicatePhaseKind,
                            sequenceIndex,
                            phaseIndex,
                            (int)phase.Kind);
                    }
                }

                if (phaseIndex > 0)
                {
                    FpgCompiledSkillPhase previous =
                        phaseValues[phaseIndex - 1];
                    if (phase.StartTick < previous.EndTick
                        || phase.Kind <= previous.Kind)
                    {
                        return Invalid(
                            FpgSkillValidationError.InvalidPhaseOrder,
                            sequenceIndex,
                            phaseIndex,
                            phase.PhaseId);
                    }
                }
            }

            if (events == null)
            {
                return Invalid(
                    FpgSkillValidationError.NullEvents,
                    sequenceIndex,
                    -1,
                    0);
            }

            for (int eventIndex = 0; eventIndex < events.Length; eventIndex++)
            {
                FpgSkillValidationResult eventValidation = ValidateEvent(
                    events[eventIndex],
                    durationTicks,
                    eventIndex,
                    sequenceIndex);
                if (!eventValidation.IsValid)
                {
                    return eventValidation;
                }

                for (int otherIndex = 0;
                    otherIndex < eventIndex;
                    otherIndex++)
                {
                    if (events[otherIndex].EventId
                        == events[eventIndex].EventId)
                    {
                        return Invalid(
                            FpgSkillValidationError.DuplicateEventId,
                            sequenceIndex,
                            eventIndex,
                            events[eventIndex].EventId);
                    }

                    if (events[otherIndex].Tick == events[eventIndex].Tick
                        && events[otherIndex].SortOrder
                            == events[eventIndex].SortOrder)
                    {
                        return Invalid(
                            FpgSkillValidationError.DuplicateEventSortOrder,
                            sequenceIndex,
                            eventIndex,
                            events[eventIndex].SortOrder);
                    }
                }
            }

            for (int eventIndex = 0; eventIndex < events.Length; eventIndex++)
            {
                FpgCompiledSkillEvent skillEvent = events[eventIndex];
                if (skillEvent.BoundGameplayEventId == 0)
                {
                    continue;
                }

                bool foundSameTickGameplayEvent = false;
                for (int otherIndex = 0;
                    otherIndex < events.Length;
                    otherIndex++)
                {
                    FpgCompiledSkillEvent candidate = events[otherIndex];
                    if (candidate.Kind == FpgSkillEventKind.GameplayPayload
                        && candidate.EventId
                            == skillEvent.BoundGameplayEventId
                        && candidate.Tick == skillEvent.Tick)
                    {
                        foundSameTickGameplayEvent = true;
                        break;
                    }
                }

                if (!foundSameTickGameplayEvent)
                {
                    return Invalid(
                        FpgSkillValidationError.InvalidBoundGameplayEventId,
                        sequenceIndex,
                        eventIndex,
                        skillEvent.BoundGameplayEventId);
                }
            }

            return FpgSkillValidationResult.Valid;
        }

        internal static FpgSkillValidationResult ValidatePhase(
            FpgCompiledSkillPhase phase,
            int durationTicks,
            int phaseIndex,
            int sequenceIndex = -1)
        {
            if (phase.PhaseId <= 0)
            {
                return Invalid(
                    FpgSkillValidationError.InvalidPhaseId,
                    sequenceIndex,
                    phaseIndex,
                    phase.PhaseId);
            }

            if (!Enum.IsDefined(typeof(FpgSkillPhaseKind), phase.Kind)
                || phase.Kind == FpgSkillPhaseKind.None)
            {
                return Invalid(
                    FpgSkillValidationError.InvalidPhaseKind,
                    sequenceIndex,
                    phaseIndex,
                    (int)phase.Kind);
            }

            if (phase.StartTick < 0
                || phase.EndTick < phase.StartTick
                || phase.EndTick > durationTicks)
            {
                int invalidTick = phase.StartTick < 0
                    ? phase.StartTick
                    : phase.EndTick;
                return Invalid(
                    FpgSkillValidationError.PhaseTickOutOfRange,
                    sequenceIndex,
                    phaseIndex,
                    invalidTick);
            }

            return FpgSkillValidationResult.Valid;
        }

        internal static FpgSkillValidationResult ValidateEvent(
            FpgCompiledSkillEvent skillEvent,
            int durationTicks,
            int eventIndex,
            int sequenceIndex = -1)
        {
            if (skillEvent.EventId <= 0)
            {
                return Invalid(FpgSkillValidationError.InvalidEventId, sequenceIndex, eventIndex, skillEvent.EventId);
            }

            if (skillEvent.Tick < 0 || skillEvent.Tick > durationTicks)
            {
                return Invalid(
                    FpgSkillValidationError.EventTickOutOfRange,
                    sequenceIndex,
                    eventIndex,
                    skillEvent.Tick);
            }

            if (!Enum.IsDefined(typeof(FpgSkillEventKind), skillEvent.Kind)
                || skillEvent.Kind == FpgSkillEventKind.None)
            {
                return Invalid(
                    FpgSkillValidationError.InvalidEventKind,
                    sequenceIndex,
                    eventIndex,
                    (int)skillEvent.Kind);
            }

            if (skillEvent.Kind == FpgSkillEventKind.GameplayPayload && skillEvent.PayloadSlotId <= 0)
            {
                return Invalid(
                    FpgSkillValidationError.InvalidPayloadSlotId,
                    sequenceIndex,
                    eventIndex,
                    skillEvent.PayloadSlotId);
            }

            if (skillEvent.Kind == FpgSkillEventKind.PresentationCue && skillEvent.CueId <= 0)
            {
                return Invalid(FpgSkillValidationError.InvalidCueId, sequenceIndex, eventIndex, skillEvent.CueId);
            }

            if ((skillEvent.Kind == FpgSkillEventKind.WarningStarted
                    || skillEvent.Kind == FpgSkillEventKind.WarningEnded)
                && skillEvent.WarningId <= 0)
            {
                return Invalid(
                    FpgSkillValidationError.InvalidWarningId,
                    sequenceIndex,
                    eventIndex,
                    skillEvent.WarningId);
            }

            if (skillEvent.SortOrder < 0)
            {
                return Invalid(
                    FpgSkillValidationError.InvalidSortOrder,
                    sequenceIndex,
                    eventIndex,
                    skillEvent.SortOrder);
            }

            if (skillEvent.SocketId < 0)
            {
                return Invalid(
                    FpgSkillValidationError.InvalidSocketId,
                    sequenceIndex,
                    eventIndex,
                    skillEvent.SocketId);
            }

            if (skillEvent.BoundGameplayEventId < 0
                || (skillEvent.Kind != FpgSkillEventKind.PresentationCue
                    && skillEvent.BoundGameplayEventId != 0))
            {
                return Invalid(
                    FpgSkillValidationError.InvalidBoundGameplayEventId,
                    sequenceIndex,
                    eventIndex,
                    skillEvent.BoundGameplayEventId);
            }

            if (!Enum.IsDefined(
                    typeof(FpgSkillTargetSource),
                    skillEvent.TargetSource)
                || (skillEvent.Kind == FpgSkillEventKind.GameplayPayload
                    && skillEvent.TargetSource == FpgSkillTargetSource.None))
            {
                return Invalid(
                    FpgSkillValidationError.InvalidTargetSource,
                    sequenceIndex,
                    eventIndex,
                    (int)skillEvent.TargetSource);
            }

            return FpgSkillValidationResult.Valid;
        }

        internal static FpgCompiledSkillPhase[] CopyPhases(
            FpgCompiledSkillPhase[] source)
        {
            FpgCompiledSkillPhase[] copy =
                new FpgCompiledSkillPhase[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        internal static FpgCompiledSkillEvent[] CopyAndSortEvents(FpgCompiledSkillEvent[] source)
        {
            FpgCompiledSkillEvent[] copy = new FpgCompiledSkillEvent[source.Length];
            Array.Copy(source, copy, source.Length);

            for (int index = 1; index < copy.Length; index++)
            {
                FpgCompiledSkillEvent value = copy[index];
                int insertionIndex = index - 1;
                while (insertionIndex >= 0 && CompareEvents(copy[insertionIndex], value) > 0)
                {
                    copy[insertionIndex + 1] = copy[insertionIndex];
                    insertionIndex--;
                }

                copy[insertionIndex + 1] = value;
            }

            return copy;
        }

        internal static FpgCompiledSkillSequence[] CopyAndSortSequences(FpgCompiledSkillSequence[] source)
        {
            FpgCompiledSkillSequence[] copy = new FpgCompiledSkillSequence[source.Length];
            Array.Copy(source, copy, source.Length);

            for (int index = 1; index < copy.Length; index++)
            {
                FpgCompiledSkillSequence value = copy[index];
                int insertionIndex = index - 1;
                while (insertionIndex >= 0 && (int)copy[insertionIndex].Kind > (int)value.Kind)
                {
                    copy[insertionIndex + 1] = copy[insertionIndex];
                    insertionIndex--;
                }

                copy[insertionIndex + 1] = value;
            }

            return copy;
        }

        internal static ulong ComputeSequenceHash(
            FpgSkillSequenceKind kind,
            int durationTicks,
            int mainAnimation,
            bool loop,
            FpgSkillAnimationPlaybackMode animationPlaybackMode,
            int animationStartTick,
            int animationEndTick,
            int[] animationVariants,
            FpgCompiledSkillPhase[] phases,
            FpgCompiledSkillEvent[] events)
        {
            ulong hash = StableHash.Append(
                StableHash.Mix(SequenceHashSeed),
                unchecked((ulong)FpgSkillRuntimeConstants.GameplayHashVersion));
            hash = StableHash.Append(hash, unchecked((ulong)(int)kind));
            hash = StableHash.Append(hash, unchecked((ulong)durationTicks));
            hash = StableHash.Append(hash, unchecked((ulong)mainAnimation));
            hash = StableHash.Append(hash, loop ? 1UL : 0UL);
            hash = StableHash.Append(
                hash,
                unchecked((ulong)(int)animationPlaybackMode));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)animationStartTick));

            hash = StableHash.Append(
                hash,
                unchecked((ulong)(animationVariants == null
                    ? 0
                    : animationVariants.Length)));
            if (animationVariants != null)
            {
                for (int index = 0; index < animationVariants.Length; index++)
                {
                    hash = StableHash.Append(
                        hash,
                        unchecked((ulong)animationVariants[index]));
                }
            }

            hash = StableHash.Append(
                hash,
                unchecked((ulong)animationEndTick));
            hash = StableHash.Append(hash, unchecked((ulong)phases.Length));
            for (int index = 0; index < phases.Length; index++)
            {
                FpgCompiledSkillPhase phase = phases[index];
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)phase.PhaseId));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)(int)phase.Kind));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)phase.StartTick));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)phase.EndTick));
            }

            hash = StableHash.Append(hash, unchecked((ulong)events.Length));

            for (int index = 0; index < events.Length; index++)
            {
                FpgCompiledSkillEvent skillEvent = events[index];
                hash = StableHash.Append(hash, unchecked((ulong)skillEvent.EventId));
                hash = StableHash.Append(hash, unchecked((ulong)skillEvent.Tick));
                hash = StableHash.Append(hash, unchecked((ulong)(int)skillEvent.Kind));
                hash = StableHash.Append(hash, unchecked((ulong)skillEvent.PayloadSlotId));
                hash = StableHash.Append(hash, unchecked((ulong)skillEvent.CueId));
                hash = StableHash.Append(hash, unchecked((ulong)skillEvent.WarningId));
                hash = StableHash.Append(hash, unchecked((ulong)skillEvent.SortOrder));
                hash = StableHash.Append(hash, unchecked((ulong)skillEvent.SocketId));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)(int)skillEvent.TargetSource));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)skillEvent.Offset.XMillimeters));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)skillEvent.Offset.YMillimeters));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)skillEvent.Offset.ZMillimeters));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)skillEvent.BoundGameplayEventId));
            }

            return hash;
        }

        internal static ulong ComputeDefinitionHash(int skillId, FpgCompiledSkillSequence[] sequences)
        {
            ulong hash = StableHash.Append(
                StableHash.Mix(DefinitionHashSeed),
                unchecked((ulong)FpgSkillRuntimeConstants.GameplayHashVersion));
            hash = StableHash.Append(hash, unchecked((ulong)skillId));
            hash = StableHash.Append(hash, unchecked((ulong)sequences.Length));
            for (int index = 0; index < sequences.Length; index++)
            {
                hash = StableHash.Append(hash, sequences[index].GameplayHash);
            }

            return hash;
        }

        private static int CompareEvents(FpgCompiledSkillEvent left, FpgCompiledSkillEvent right)
        {
            int tick = left.Tick.CompareTo(right.Tick);
            if (tick != 0)
            {
                return tick;
            }

            return left.SortOrder.CompareTo(right.SortOrder);
        }

        private static FpgSkillValidationResult Invalid(
            FpgSkillValidationError error,
            int sequenceIndex,
            int eventIndex,
            int value)
        {
            return new FpgSkillValidationResult(error, sequenceIndex, eventIndex, value);
        }
    }
}
