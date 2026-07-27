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
            FpgSkillEventKind warningKind,
            int warningId,
            int sortOrder = 0,
            int socketId = 0)
        {
            if (warningKind != FpgSkillEventKind.WarningStarted
                && warningKind != FpgSkillEventKind.WarningEnded)
            {
                throw new ArgumentOutOfRangeException(nameof(warningKind));
            }

            EventId = eventId;
            Tick = tick;
            Kind = warningKind;
            ActionKind = FpgSkillActionKind.None;
            ActionIndex = -1;
            WarningId = warningId;
            SortOrder = sortOrder;
            SocketId = socketId;
            TargetSource = FpgSkillTargetSource.None;
            Offset = default(FpgSkillOffset);
            BoundGameplayEventId = 0;
            ActivePresentationKind = FpgActivePresentationKind.None;
            PresentationHandle = default(FpgPresentationHandle);
            PresentationTrackId = 0;
            PresentationContentHash = 0UL;
        }

        public FpgCompiledSkillEvent(
            int eventId,
            int tick,
            FpgSkillActionKind actionKind,
            int actionIndex,
            int sortOrder = 0,
            int socketId = 0,
            FpgSkillTargetSource targetSource = FpgSkillTargetSource.CurrentAim,
            int offsetXMillimeters = 0,
            int offsetYMillimeters = 0,
            int offsetZMillimeters = 0)
        {
            EventId = eventId;
            Tick = tick;
            Kind = FpgSkillEventKind.GameplayAction;
            ActionKind = actionKind;
            ActionIndex = actionIndex;
            WarningId = 0;
            SortOrder = sortOrder;
            SocketId = socketId;
            TargetSource = targetSource;
            Offset = new FpgSkillOffset(
                offsetXMillimeters,
                offsetYMillimeters,
                offsetZMillimeters);
            BoundGameplayEventId = 0;
            ActivePresentationKind = FpgActivePresentationKind.None;
            PresentationHandle = default(FpgPresentationHandle);
            PresentationTrackId = 0;
            PresentationContentHash = 0UL;
        }

        public FpgCompiledSkillEvent(
            int eventId,
            int tick,
            FpgActivePresentationKind activePresentationKind,
            FpgPresentationHandle presentationHandle,
            int presentationTrackId,
            ulong presentationContentHash,
            int sortOrder = 0,
            int socketId = 0,
            int boundGameplayEventId = 0)
        {
            EventId = eventId;
            Tick = tick;
            Kind = FpgSkillEventKind.ActivePresentation;
            ActionKind = FpgSkillActionKind.None;
            ActionIndex = -1;
            WarningId = 0;
            SortOrder = sortOrder;
            SocketId = socketId;
            TargetSource = FpgSkillTargetSource.None;
            Offset = default(FpgSkillOffset);
            BoundGameplayEventId = boundGameplayEventId;
            ActivePresentationKind = activePresentationKind;
            PresentationHandle = presentationHandle;
            PresentationTrackId = presentationTrackId;
            PresentationContentHash = presentationContentHash;
        }

        public int EventId { get; }

        public int Tick { get; }

        public FpgSkillEventKind Kind { get; }

        public FpgSkillActionKind ActionKind { get; }

        public int ActionIndex { get; }

        public int WarningId { get; }

        public int SortOrder { get; }

        public int SocketId { get; }

        public FpgSkillTargetSource TargetSource { get; }

        public FpgSkillOffset Offset { get; }

        public int BoundGameplayEventId { get; }

        public FpgActivePresentationKind ActivePresentationKind { get; }

        public FpgPresentationHandle PresentationHandle { get; }

        public int PresentationTrackId { get; }

        public ulong PresentationContentHash { get; }

        public bool IsValid => FpgSkillCompiler.ValidateEvent(this, int.MaxValue, -1).IsValid;

        public bool Equals(FpgCompiledSkillEvent other)
        {
            return EventId == other.EventId
                && Tick == other.Tick
                && Kind == other.Kind
                && ActionKind == other.ActionKind
                && ActionIndex == other.ActionIndex
                && WarningId == other.WarningId
                && SortOrder == other.SortOrder
                && SocketId == other.SocketId
                && TargetSource == other.TargetSource
                && Offset.Equals(other.Offset)
                && BoundGameplayEventId == other.BoundGameplayEventId
                && ActivePresentationKind == other.ActivePresentationKind
                && PresentationHandle == other.PresentationHandle
                && PresentationTrackId == other.PresentationTrackId
                && PresentationContentHash == other.PresentationContentHash;
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
                hash = (hash * 397) ^ (int)ActionKind;
                hash = (hash * 397) ^ ActionIndex;
                hash = (hash * 397) ^ WarningId;
                hash = (hash * 397) ^ SortOrder;
                hash = (hash * 397) ^ SocketId;
                hash = (hash * 397) ^ (int)TargetSource;
                hash = (hash * 397) ^ Offset.GetHashCode();
                hash = (hash * 397) ^ BoundGameplayEventId;
                hash = (hash * 397) ^ (int)ActivePresentationKind;
                hash = (hash * 397) ^ PresentationHandle.GetHashCode();
                hash = (hash * 397) ^ PresentationTrackId;
                return (hash * 397) ^ PresentationContentHash.GetHashCode();
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

    public readonly struct FpgCompiledImpactPresentation
    {
        public FpgCompiledImpactPresentation(
            FpgPresentationHandle baseVfx,
            FpgPresentationHandle baseAudio,
            FpgPresentationHandle baseCameraShake,
            FpgPresentationHandle weakpointVfxOverride,
            FpgPresentationHandle weakpointAudioOverride,
            FpgPresentationHandle weakpointCameraShakeOverride,
            ulong presentationContentHash)
        {
            BaseVfx = baseVfx;
            BaseAudio = baseAudio;
            BaseCameraShake = baseCameraShake;
            WeakpointVfxOverride = weakpointVfxOverride;
            WeakpointAudioOverride = weakpointAudioOverride;
            WeakpointCameraShakeOverride = weakpointCameraShakeOverride;
            PresentationContentHash = presentationContentHash;
        }

        public FpgPresentationHandle BaseVfx { get; }
        public FpgPresentationHandle BaseAudio { get; }
        public FpgPresentationHandle BaseCameraShake { get; }
        public FpgPresentationHandle WeakpointVfxOverride { get; }
        public FpgPresentationHandle WeakpointAudioOverride { get; }
        public FpgPresentationHandle WeakpointCameraShakeOverride { get; }
        public ulong PresentationContentHash { get; }

        public bool HasAny => BaseVfx.IsValid
            || BaseAudio.IsValid
            || BaseCameraShake.IsValid
            || WeakpointVfxOverride.IsValid
            || WeakpointAudioOverride.IsValid
            || WeakpointCameraShakeOverride.IsValid;
    }

    public readonly struct FpgCompiledSkillActionPresentation
    {
        public FpgCompiledSkillActionPresentation(
            FpgSkillActionKind actionKind,
            int actionIndex,
            FpgPresentationHandle trajectoryVfx,
            FpgCompiledImpactPresentation impact,
            FpgPresentationHandle flightVfx,
            FpgCompiledImpactPresentation collision,
            int successAnimation,
            ulong presentationContentHash)
        {
            if (!Enum.IsDefined(typeof(FpgSkillActionKind), actionKind)
                || actionKind == FpgSkillActionKind.None
                || actionIndex < 0
                || successAnimation < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionKind));
            }

            ActionKind = actionKind;
            ActionIndex = actionIndex;
            TrajectoryVfx = trajectoryVfx;
            Impact = impact;
            FlightVfx = flightVfx;
            Collision = collision;
            SuccessAnimation = successAnimation;
            PresentationContentHash = presentationContentHash;

            if (!HasAny || !HasValidShape())
            {
                throw new ArgumentException(
                    "Compiled action presentation does not match its action kind.",
                    nameof(actionKind));
            }
        }

        public FpgSkillActionKind ActionKind { get; }
        public int ActionIndex { get; }
        public FpgPresentationHandle TrajectoryVfx { get; }
        public FpgCompiledImpactPresentation Impact { get; }
        public FpgPresentationHandle FlightVfx { get; }
        public FpgCompiledImpactPresentation Collision { get; }
        public int SuccessAnimation { get; }
        public ulong PresentationContentHash { get; }

        public bool HasAny => TrajectoryVfx.IsValid
            || Impact.HasAny
            || FlightVfx.IsValid
            || Collision.HasAny
            || SuccessAnimation > 0;

        public bool IsValid => ActionIndex >= 0
            && Enum.IsDefined(typeof(FpgSkillActionKind), ActionKind)
            && ActionKind != FpgSkillActionKind.None
            && HasAny
            && HasValidShape();

        private bool HasValidShape()
        {
            switch (ActionKind)
            {
                case FpgSkillActionKind.Attack:
                    return !FlightVfx.IsValid
                        && !Collision.HasAny
                        && SuccessAnimation == 0;

                case FpgSkillActionKind.LaunchProjectile:
                    return !TrajectoryVfx.IsValid
                        && !Impact.HasAny
                        && SuccessAnimation == 0;

                case FpgSkillActionKind.CommitReload:
                    return !TrajectoryVfx.IsValid
                        && !Impact.HasAny
                        && !FlightVfx.IsValid
                        && !Collision.HasAny
                        && SuccessAnimation > 0;

                default:
                    return false;
            }
        }
    }

    public readonly struct FpgCompiledSkillSequence
    {
        private static readonly FpgCompiledSkillEvent[] EmptyEvents =
            Array.Empty<FpgCompiledSkillEvent>();
        private static readonly FpgCompiledSkillActionPresentation[]
            EmptyActionPresentations =
                Array.Empty<FpgCompiledSkillActionPresentation>();
        private static readonly int[] EmptyAnimations = Array.Empty<int>();
        private readonly FpgCompiledSkillEvent[] events;
        private readonly FpgCompiledSkillActionPresentation[]
            actionPresentations;
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
                events,
                null)
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
            FpgCompiledSkillEvent[] events,
            FpgCompiledSkillActionPresentation[] actionPresentations)
        {
            FpgSkillValidationResult validation = FpgSkillCompiler.ValidateSequence(
                kind,
                durationTicks,
                mainAnimation,
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
            this.events = FpgSkillCompiler.CopyAndSortEvents(events);
            this.actionPresentations =
                FpgSkillCompiler.CopyAndSortActionPresentations(
                    actionPresentations
                        ?? EmptyActionPresentations);
            GameplayHash = FpgSkillCompiler.ComputeSequenceHash(
                kind,
                durationTicks,
                loop,
                this.events);
            PresentationHash =
                FpgSkillCompiler.ComputeSequencePresentationHash(
                    kind,
                    mainAnimation,
                    animationPlaybackMode,
                    animationStartTick,
                    animationEndTick,
                    animationVariants,
                    this.events,
                    this.actionPresentations);
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

        public IReadOnlyList<FpgCompiledSkillEvent> Events => events ?? EmptyEvents;

        public int EventCount => events == null ? 0 : events.Length;

        public IReadOnlyList<FpgCompiledSkillActionPresentation>
            ActionPresentations =>
                actionPresentations ?? EmptyActionPresentations;

        public int ActionPresentationCount => actionPresentations == null
            ? 0
            : actionPresentations.Length;

        public ulong GameplayHash { get; }

        public ulong PresentationHash { get; }

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
            && events != null
            && actionPresentations != null;

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

        public FpgCompiledSkillEvent GetEvent(int index)
        {
            if (events == null || index < 0 || index >= events.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return events[index];
        }

        public FpgCompiledSkillActionPresentation GetActionPresentation(
            int index)
        {
            if (actionPresentations == null
                || index < 0
                || index >= actionPresentations.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return actionPresentations[index];
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
            validation = FpgSkillCompiler.ValidateSequence(
                kind,
                durationTicks,
                mainAnimation,
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
            FpgSkillCompiler.ValidatePresentationHandleConflicts(
                this.sequences);
            GameplayHash = FpgSkillCompiler.ComputeDefinitionHash(skillId, this.sequences);
            PresentationHash =
                FpgSkillCompiler.ComputeDefinitionPresentationHash(
                    skillId,
                    this.sequences);
        }

        public int SkillId { get; }

        public IReadOnlyList<FpgCompiledSkillSequence> Sequences => sequences ?? EmptySequences;

        public int SequenceCount => sequences == null ? 0 : sequences.Length;

        public ulong GameplayHash { get; }

        public ulong PresentationHash { get; }

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
        private const ulong SequencePresentationHashSeed =
            0x4650475F50534531UL;
        private const ulong DefinitionPresentationHashSeed =
            0x4650475F50534B31UL;

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

                bool foundBoundGameplayEvent = false;
                for (int otherIndex = 0;
                    otherIndex < events.Length;
                    otherIndex++)
                {
                    FpgCompiledSkillEvent candidate = events[otherIndex];
                    if (candidate.Kind == FpgSkillEventKind.GameplayAction
                        && candidate.EventId
                            == skillEvent.BoundGameplayEventId
                        && candidate.Tick <= skillEvent.Tick)
                    {
                        foundBoundGameplayEvent = true;
                        break;
                    }
                }

                if (!foundBoundGameplayEvent)
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

            if (skillEvent.Kind == FpgSkillEventKind.GameplayAction)
            {
                if (!Enum.IsDefined(
                        typeof(FpgSkillActionKind),
                        skillEvent.ActionKind)
                    || skillEvent.ActionKind == FpgSkillActionKind.None)
                {
                    return Invalid(
                        FpgSkillValidationError.InvalidActionKind,
                        sequenceIndex,
                        eventIndex,
                        (int)skillEvent.ActionKind);
                }

                if (skillEvent.ActionIndex < 0)
                {
                    return Invalid(
                        FpgSkillValidationError.InvalidActionIndex,
                        sequenceIndex,
                        eventIndex,
                        skillEvent.ActionIndex);
                }
            }

            if (skillEvent.Kind == FpgSkillEventKind.ActivePresentation)
            {
                if (!Enum.IsDefined(
                        typeof(FpgActivePresentationKind),
                        skillEvent.ActivePresentationKind)
                    || skillEvent.ActivePresentationKind
                        == FpgActivePresentationKind.None)
                {
                    return Invalid(
                        FpgSkillValidationError
                            .InvalidActivePresentationKind,
                        sequenceIndex,
                        eventIndex,
                        (int)skillEvent.ActivePresentationKind);
                }

                if (!skillEvent.PresentationHandle.IsValid)
                {
                    return Invalid(
                        FpgSkillValidationError.InvalidPresentationHandle,
                        sequenceIndex,
                        eventIndex,
                        skillEvent.PresentationHandle.Value);
                }

                if (skillEvent.PresentationTrackId <= 0)
                {
                    return Invalid(
                        FpgSkillValidationError.InvalidPresentationTrackId,
                        sequenceIndex,
                        eventIndex,
                        skillEvent.PresentationTrackId);
                }
            }
            else if (skillEvent.ActivePresentationKind
                    != FpgActivePresentationKind.None
                || skillEvent.PresentationHandle.IsValid
                || skillEvent.PresentationTrackId != 0
                || skillEvent.PresentationContentHash != 0UL)
            {
                return Invalid(
                    FpgSkillValidationError.InvalidPresentationHandle,
                    sequenceIndex,
                    eventIndex,
                    skillEvent.PresentationHandle.Value);
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
                || (skillEvent.Kind
                        != FpgSkillEventKind.ActivePresentation
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
                || (skillEvent.Kind == FpgSkillEventKind.GameplayAction
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

        internal static FpgCompiledSkillActionPresentation[]
            CopyAndSortActionPresentations(
                FpgCompiledSkillActionPresentation[] source)
        {
            FpgCompiledSkillActionPresentation[] copy =
                new FpgCompiledSkillActionPresentation[source.Length];
            Array.Copy(source, copy, source.Length);

            for (int index = 0; index < copy.Length; index++)
            {
                if (!copy[index].IsValid)
                {
                    throw new ArgumentException(
                        "Compiled skill action presentation is invalid.",
                        nameof(source));
                }

                for (int otherIndex = 0;
                    otherIndex < index;
                    otherIndex++)
                {
                    if (copy[otherIndex].ActionKind == copy[index].ActionKind
                        && copy[otherIndex].ActionIndex
                            == copy[index].ActionIndex)
                    {
                        throw new ArgumentException(
                            "Compiled skill action presentation is duplicated.",
                            nameof(source));
                    }
                }
            }

            for (int index = 1; index < copy.Length; index++)
            {
                FpgCompiledSkillActionPresentation value = copy[index];
                int insertionIndex = index - 1;
                while (insertionIndex >= 0
                    && CompareActionPresentations(
                        copy[insertionIndex],
                        value) > 0)
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

        internal static void ValidatePresentationHandleConflicts(
            FpgCompiledSkillSequence[] sequences)
        {
            HashSet<int> handles = new HashSet<int>();
            for (int sequenceIndex = 0;
                sequenceIndex < sequences.Length;
                sequenceIndex++)
            {
                FpgCompiledSkillSequence sequence = sequences[sequenceIndex];
                for (int eventIndex = 0;
                    eventIndex < sequence.EventCount;
                    eventIndex++)
                {
                    FpgCompiledSkillEvent skillEvent =
                        sequence.GetEvent(eventIndex);
                    if (skillEvent.PresentationHandle.IsValid
                        && !handles.Add(skillEvent.PresentationHandle.Value))
                    {
                        throw new ArgumentException(
                            "Compiled presentation handle collision.",
                            nameof(sequences));
                    }
                }

                for (int actionIndex = 0;
                    actionIndex < sequence.ActionPresentationCount;
                    actionIndex++)
                {
                    FpgCompiledSkillActionPresentation action =
                        sequence.GetActionPresentation(actionIndex);
                    AddPresentationHandle(handles, action.TrajectoryVfx);
                    AddImpactHandles(handles, action.Impact);
                    AddPresentationHandle(handles, action.FlightVfx);
                    AddImpactHandles(handles, action.Collision);
                }
            }
        }

        private static void AddImpactHandles(
            HashSet<int> handles,
            in FpgCompiledImpactPresentation value)
        {
            AddPresentationHandle(handles, value.BaseVfx);
            AddPresentationHandle(handles, value.BaseAudio);
            AddPresentationHandle(handles, value.BaseCameraShake);
            AddPresentationHandle(handles, value.WeakpointVfxOverride);
            AddPresentationHandle(handles, value.WeakpointAudioOverride);
            AddPresentationHandle(
                handles,
                value.WeakpointCameraShakeOverride);
        }

        private static void AddPresentationHandle(
            HashSet<int> handles,
            FpgPresentationHandle handle)
        {
            if (handle.IsValid && !handles.Add(handle.Value))
            {
                throw new ArgumentException(
                    "Compiled presentation handle collision.",
                    nameof(handle));
            }
        }

        internal static ulong ComputeSequenceHash(
            FpgSkillSequenceKind kind,
            int durationTicks,
            bool loop,
            FpgCompiledSkillEvent[] events)
        {
            ulong hash = StableHash.Append(
                StableHash.Mix(SequenceHashSeed),
                unchecked((ulong)FpgSkillRuntimeConstants.GameplayHashVersion));
            hash = StableHash.Append(hash, unchecked((ulong)(int)kind));
            hash = StableHash.Append(hash, unchecked((ulong)durationTicks));
            hash = StableHash.Append(hash, loop ? 1UL : 0UL);

            int gameplayEventCount = 0;
            for (int index = 0; index < events.Length; index++)
            {
                if (!IsPresentationEvent(events[index].Kind))
                {
                    gameplayEventCount++;
                }
            }

            hash = StableHash.Append(
                hash,
                unchecked((ulong)gameplayEventCount));

            for (int index = 0; index < events.Length; index++)
            {
                FpgCompiledSkillEvent skillEvent = events[index];
                if (IsPresentationEvent(skillEvent.Kind))
                {
                    continue;
                }

                hash = StableHash.Append(hash, unchecked((ulong)skillEvent.EventId));
                hash = StableHash.Append(hash, unchecked((ulong)skillEvent.Tick));
                hash = StableHash.Append(hash, unchecked((ulong)(int)skillEvent.Kind));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)(int)skillEvent.ActionKind));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)skillEvent.ActionIndex));
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

        internal static ulong ComputeSequencePresentationHash(
            FpgSkillSequenceKind kind,
            int mainAnimation,
            FpgSkillAnimationPlaybackMode animationPlaybackMode,
            int animationStartTick,
            int animationEndTick,
            int[] animationVariants,
            FpgCompiledSkillEvent[] events,
            FpgCompiledSkillActionPresentation[] actionPresentations)
        {
            ulong hash = StableHash.Append(
                StableHash.Mix(SequencePresentationHashSeed),
                unchecked((ulong)FpgSkillRuntimeConstants
                    .PresentationHashVersion));
            hash = StableHash.Append(hash, unchecked((ulong)(int)kind));
            hash = StableHash.Append(hash, unchecked((ulong)mainAnimation));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)(int)animationPlaybackMode));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)animationStartTick));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)animationEndTick));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)animationVariants.Length));
            for (int index = 0; index < animationVariants.Length; index++)
            {
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)animationVariants[index]));
            }

            int presentationEventCount = 0;
            for (int index = 0; index < events.Length; index++)
            {
                if (IsPresentationEvent(events[index].Kind))
                {
                    presentationEventCount++;
                }
            }

            hash = StableHash.Append(
                hash,
                unchecked((ulong)presentationEventCount));
            for (int index = 0; index < events.Length; index++)
            {
                FpgCompiledSkillEvent skillEvent = events[index];
                if (!IsPresentationEvent(skillEvent.Kind))
                {
                    continue;
                }

                hash = AppendPresentationEventHash(hash, skillEvent);
            }

            hash = StableHash.Append(
                hash,
                unchecked((ulong)actionPresentations.Length));
            for (int index = 0;
                index < actionPresentations.Length;
                index++)
            {
                FpgCompiledSkillActionPresentation value =
                    actionPresentations[index];
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)(int)value.ActionKind));
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)value.ActionIndex));
                hash = AppendPresentationHandleHash(
                    hash,
                    value.TrajectoryVfx);
                hash = AppendImpactPresentationHash(hash, value.Impact);
                hash = AppendPresentationHandleHash(hash, value.FlightVfx);
                hash = AppendImpactPresentationHash(hash, value.Collision);
                hash = StableHash.Append(
                    hash,
                    unchecked((ulong)value.SuccessAnimation));
                hash = StableHash.Append(
                    hash,
                    value.PresentationContentHash);
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

        internal static ulong ComputeDefinitionPresentationHash(
            int skillId,
            FpgCompiledSkillSequence[] sequences)
        {
            ulong hash = StableHash.Append(
                StableHash.Mix(DefinitionPresentationHashSeed),
                unchecked((ulong)FpgSkillRuntimeConstants
                    .PresentationHashVersion));
            hash = StableHash.Append(hash, unchecked((ulong)skillId));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)sequences.Length));
            for (int index = 0; index < sequences.Length; index++)
            {
                hash = StableHash.Append(
                    hash,
                    sequences[index].PresentationHash);
            }

            return hash;
        }

        private static ulong AppendPresentationEventHash(
            ulong hash,
            in FpgCompiledSkillEvent skillEvent)
        {
            hash = StableHash.Append(
                hash,
                unchecked((ulong)skillEvent.EventId));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)skillEvent.Tick));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)(int)skillEvent.Kind));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)skillEvent.SortOrder));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)skillEvent.SocketId));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)skillEvent.BoundGameplayEventId));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)(int)skillEvent.ActivePresentationKind));
            hash = AppendPresentationHandleHash(
                hash,
                skillEvent.PresentationHandle);
            hash = StableHash.Append(
                hash,
                unchecked((ulong)skillEvent.PresentationTrackId));
            return StableHash.Append(
                hash,
                skillEvent.PresentationContentHash);
        }

        private static ulong AppendImpactPresentationHash(
            ulong hash,
            in FpgCompiledImpactPresentation impact)
        {
            hash = AppendPresentationHandleHash(hash, impact.BaseVfx);
            hash = AppendPresentationHandleHash(hash, impact.BaseAudio);
            hash = AppendPresentationHandleHash(
                hash,
                impact.BaseCameraShake);
            hash = AppendPresentationHandleHash(
                hash,
                impact.WeakpointVfxOverride);
            hash = AppendPresentationHandleHash(
                hash,
                impact.WeakpointAudioOverride);
            hash = AppendPresentationHandleHash(
                hash,
                impact.WeakpointCameraShakeOverride);
            return StableHash.Append(
                hash,
                impact.PresentationContentHash);
        }

        private static ulong AppendPresentationHandleHash(
            ulong hash,
            FpgPresentationHandle handle)
        {
            return StableHash.Append(
                hash,
                unchecked((ulong)handle.Value));
        }

        private static bool IsPresentationEvent(FpgSkillEventKind kind)
        {
            return kind == FpgSkillEventKind.ActivePresentation;
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

        private static int CompareActionPresentations(
            FpgCompiledSkillActionPresentation left,
            FpgCompiledSkillActionPresentation right)
        {
            int kind = ((int)left.ActionKind).CompareTo(
                (int)right.ActionKind);
            return kind != 0
                ? kind
                : left.ActionIndex.CompareTo(right.ActionIndex);
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
