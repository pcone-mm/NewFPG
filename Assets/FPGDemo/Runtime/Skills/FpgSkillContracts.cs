using System;

namespace FPG.Demo.Skills
{
    public static class FpgSkillRuntimeConstants
    {
        public const int TickRate = 60;
        public const int GameplayHashVersion = 4;
        public const int PresentationHashVersion = 1;
    }

    public readonly struct FpgPresentationHandle :
        IEquatable<FpgPresentationHandle>,
        IComparable<FpgPresentationHandle>
    {
        public FpgPresentationHandle(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;

        public int CompareTo(FpgPresentationHandle other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(FpgPresentationHandle other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is FpgPresentationHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(
            FpgPresentationHandle left,
            FpgPresentationHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            FpgPresentationHandle left,
            FpgPresentationHandle right)
        {
            return !left.Equals(right);
        }
    }

    public enum FpgActivePresentationKind
    {
        None = 0,
        Vfx = 1,
        Audio = 2,
        CameraShake = 3
    }

    public enum FpgSkillAnimationPlaybackMode
    {
        NaturalSpeed = 0,
        FitInterval = 1
    }

    public enum FpgSkillTargetSource
    {
        None = 0,
        CurrentAim,
        CurrentTarget,
        Self,
        SocketForward
    }

    public readonly struct FpgSkillOffset : IEquatable<FpgSkillOffset>
    {
        public FpgSkillOffset(
            int xMillimeters,
            int yMillimeters,
            int zMillimeters)
        {
            XMillimeters = xMillimeters;
            YMillimeters = yMillimeters;
            ZMillimeters = zMillimeters;
        }

        public int XMillimeters { get; }
        public int YMillimeters { get; }
        public int ZMillimeters { get; }

        public bool Equals(FpgSkillOffset other)
        {
            return XMillimeters == other.XMillimeters
                && YMillimeters == other.YMillimeters
                && ZMillimeters == other.ZMillimeters;
        }

        public override bool Equals(object obj)
        {
            return obj is FpgSkillOffset other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = XMillimeters;
                hash = (hash * 397) ^ YMillimeters;
                return (hash * 397) ^ ZMillimeters;
            }
        }
    }

    public readonly struct SkillExecutionId : IEquatable<SkillExecutionId>, IComparable<SkillExecutionId>
    {
        public static readonly SkillExecutionId Invalid = new SkillExecutionId(0L);

        public SkillExecutionId(long value)
        {
            Value = value;
        }

        public long Value { get; }

        public bool IsValid => Value > 0L;

        public int CompareTo(SkillExecutionId other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(SkillExecutionId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is SkillExecutionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((int)(Value ^ (Value >> 32)));
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(SkillExecutionId left, SkillExecutionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SkillExecutionId left, SkillExecutionId right)
        {
            return !left.Equals(right);
        }
    }

    public enum FpgSkillEventKind
    {
        None = 0,
        GameplayAction = 1,
        WarningStarted = 3,
        WarningEnded = 4,
        ActivePresentation = 5
    }

    public enum FpgSkillActionKind
    {
        None = 0,
        Attack = 1,
        LaunchProjectile = 2,
        CommitReload = 3,
        SummonActors = 4
    }

    public enum FpgSkillSequenceKind
    {
        None = 0,
        Execute = 1,
        ChargeEnter = 2,
        ChargeLoop = 3,
        Release = 4,
        Cancel = 5
    }

    public enum FpgSkillValidationError
    {
        None = 0,
        InvalidSkillId,
        NullSequences,
        EmptySequences,
        InvalidSequence,
        InvalidSequenceKind,
        DuplicateSequenceKind,
        MissingExecuteSequence,
        InvalidDurationTicks,
        InvalidMainAnimation,
        NullEvents = 16,
        InvalidEventId,
        DuplicateEventId,
        EventTickOutOfRange,
        InvalidEventKind,
        DuplicateEventSortOrder,
        InvalidWarningId,
        InvalidSortOrder,
        InvalidSocketId,
        InvalidBoundGameplayEventId,
        InvalidTargetSource,
        InvalidAnimationPlayback,
        InvalidActionKind,
        InvalidActionIndex,
        InvalidActivePresentationKind,
        InvalidPresentationHandle,
        InvalidPresentationTrackId
    }

    public readonly struct FpgSkillValidationResult
    {
        internal FpgSkillValidationResult(
            FpgSkillValidationError error,
            int sequenceIndex,
            int eventIndex,
            int value)
        {
            Error = error;
            SequenceIndex = sequenceIndex;
            EventIndex = eventIndex;
            Value = value;
        }

        public bool IsValid => Error == FpgSkillValidationError.None;

        public FpgSkillValidationError Error { get; }

        public int SequenceIndex { get; }

        public int EventIndex { get; }

        public int Value { get; }

        public static FpgSkillValidationResult Valid => new FpgSkillValidationResult(
            FpgSkillValidationError.None,
            -1,
            -1,
            0);

        public override string ToString()
        {
            return IsValid
                ? "Valid"
                : Error + " (sequence=" + SequenceIndex + ", event=" + EventIndex + ", value=" + Value + ")";
        }
    }

    public enum FpgSkillExecutionState
    {
        Idle = 0,
        Running,
        Completed,
        Canceled
    }

    public enum FpgSkillEventOutcome
    {
        Triggered = 0,
        Canceled
    }

    public enum FpgSkillRuntimeError
    {
        None = 0,
        InvalidExecutionId,
        InvalidSequence,
        InvalidTick,
        TickRangeOverflow,
        AlreadyRunning,
        NotRunning,
        WrongTick,
        ResultCapacityExceeded,
        AlreadyTerminal
    }

    public readonly struct FpgSkillRuntimeResult
    {
        private FpgSkillRuntimeResult(
            bool isSuccess,
            FpgSkillRuntimeError error,
            FpgSkillExecutionState state,
            int eventResultCount)
        {
            IsSuccess = isSuccess;
            Error = error;
            State = state;
            EventResultCount = eventResultCount;
        }

        public bool IsSuccess { get; }

        public FpgSkillRuntimeError Error { get; }

        public FpgSkillExecutionState State { get; }

        public int EventResultCount { get; }

        internal static FpgSkillRuntimeResult Success(FpgSkillExecutionState state, int eventResultCount)
        {
            return new FpgSkillRuntimeResult(true, FpgSkillRuntimeError.None, state, eventResultCount);
        }

        internal static FpgSkillRuntimeResult Rejected(
            FpgSkillRuntimeError error,
            FpgSkillExecutionState state)
        {
            return new FpgSkillRuntimeResult(false, error, state, 0);
        }

        public override string ToString()
        {
            return IsSuccess ? State + " (results=" + EventResultCount + ")" : Error.ToString();
        }
    }
}
