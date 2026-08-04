using System;
using FPG.Demo.Core;

namespace FPG.Demo.Skills
{
    public enum FpgAttackTimingMode
    {
        FixedCooldown = 0,
        CharacterAttackSpeed
    }

    public enum FpgAttackPhase
    {
        None = 0,
        Windup,
        Recovery
    }

    public readonly struct FpgAttackSpeedProfile
    {
        public FpgAttackSpeedProfile(
            double baseAttackSpeed,
            double attackSpeedRatio,
            double attackSpeedCap)
        {
            BaseAttackSpeed = baseAttackSpeed;
            AttackSpeedRatio = attackSpeedRatio;
            AttackSpeedCap = attackSpeedCap;
        }

        public double BaseAttackSpeed { get; }
        public double AttackSpeedRatio { get; }
        public double AttackSpeedCap { get; }

        public bool IsValid => IsFinitePositive(BaseAttackSpeed)
            && IsFiniteNonNegative(AttackSpeedRatio)
            && IsFinitePositive(AttackSpeedCap)
            && AttackSpeedCap >= BaseAttackSpeed;

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value > 0d;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value >= 0d;
        }
    }

    public interface IAttackSpeedBonusProvider
    {
        double GetBonusAttackSpeed(RuntimeId ownerId, TickIndex tick);
    }

    public sealed class StaticAttackSpeedBonusProvider
        : IAttackSpeedBonusProvider
    {
        public static readonly StaticAttackSpeedBonusProvider Zero =
            new StaticAttackSpeedBonusProvider(0d);

        public StaticAttackSpeedBonusProvider(double bonusAttackSpeed)
        {
            if (double.IsNaN(bonusAttackSpeed)
                || double.IsInfinity(bonusAttackSpeed))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bonusAttackSpeed));
            }

            BonusAttackSpeed = bonusAttackSpeed;
        }

        public double BonusAttackSpeed { get; }

        public double GetBonusAttackSpeed(RuntimeId ownerId, TickIndex tick)
        {
            return BonusAttackSpeed;
        }
    }

    public readonly struct FpgCompiledSkillTimingDefinition
    {
        public static readonly FpgCompiledSkillTimingDefinition Fixed =
            new FpgCompiledSkillTimingDefinition(
                FpgAttackTimingMode.FixedCooldown,
                1d,
                -1,
                -1);

        public FpgCompiledSkillTimingDefinition(
            FpgAttackTimingMode mode,
            double windupAttackSpeedCoefficient,
            int differentAttackInterruptTick,
            int authoredAttackFrameTick)
        {
            if (!Enum.IsDefined(typeof(FpgAttackTimingMode), mode)
                || double.IsNaN(windupAttackSpeedCoefficient)
                || double.IsInfinity(windupAttackSpeedCoefficient)
                || windupAttackSpeedCoefficient < 0d
                || windupAttackSpeedCoefficient > 1d
                || differentAttackInterruptTick < -1
                || authoredAttackFrameTick < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            Mode = mode;
            WindupAttackSpeedCoefficient = windupAttackSpeedCoefficient;
            DifferentAttackInterruptTick = differentAttackInterruptTick;
            AuthoredAttackFrameTick = authoredAttackFrameTick;
            TimingContractHash = ComputeHash(
                mode,
                windupAttackSpeedCoefficient,
                differentAttackInterruptTick,
                authoredAttackFrameTick);
        }

        public FpgAttackTimingMode Mode { get; }
        public double WindupAttackSpeedCoefficient { get; }
        public int DifferentAttackInterruptTick { get; }
        public int AuthoredAttackFrameTick { get; }
        public ulong TimingContractHash { get; }

        public bool IsFixed => Mode == FpgAttackTimingMode.FixedCooldown;

        private static ulong ComputeHash(
            FpgAttackTimingMode mode,
            double windupAttackSpeedCoefficient,
            int differentAttackInterruptTick,
            int authoredAttackFrameTick)
        {
            ulong hash = StableHash.Mix(0x4650475F54494D31UL);
            hash = StableHash.Append(hash, unchecked((ulong)(int)mode));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)BitConverter.DoubleToInt64Bits(
                    windupAttackSpeedCoefficient)));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)differentAttackInterruptTick));
            return StableHash.Append(
                hash,
                unchecked((ulong)authoredAttackFrameTick));
        }
    }

    public readonly struct FpgResolvedSkillTimingSnapshot
    {
        internal FpgResolvedSkillTimingSnapshot(
            FpgAttackTimingMode mode,
            TickIndex startTick,
            int authoredDurationTicks,
            int resolvedDurationTicks,
            int authoredAttackFrameTick,
            int windupTicks,
            int intervalTicks,
            int differentAttackInterruptTick,
            double baseAttackSpeed,
            double attackSpeedRatio,
            double attackSpeedCap,
            double bonusAttackSpeed,
            double effectiveAttackSpeed,
            ulong sourceGameplayHash,
            ulong sourceTimingHash)
        {
            Mode = mode;
            StartTick = startTick;
            AuthoredDurationTicks = authoredDurationTicks;
            ResolvedDurationTicks = resolvedDurationTicks;
            AuthoredAttackFrameTick = authoredAttackFrameTick;
            WindupTicks = windupTicks;
            IntervalTicks = intervalTicks;
            RecoveryTicks = Math.Max(0, intervalTicks - windupTicks);
            DifferentAttackInterruptRelativeTick =
                differentAttackInterruptTick;
            BaseAttackSpeed = baseAttackSpeed;
            AttackSpeedRatio = attackSpeedRatio;
            AttackSpeedCap = attackSpeedCap;
            BonusAttackSpeed = bonusAttackSpeed;
            EffectiveAttackSpeed = effectiveAttackSpeed;
            SourceGameplayHash = sourceGameplayHash;
            SourceTimingHash = sourceTimingHash;

            AttackFrameTick = startTick.IsValid
                ? new TickIndex(checked(startTick.Value + windupTicks))
                : TickIndex.Invalid;
            SameAttackReadyTick = startTick.IsValid && intervalTicks > 0
                ? new TickIndex(checked(startTick.Value + intervalTicks))
                : TickIndex.Invalid;
            DifferentAttackInterruptTick = startTick.IsValid
                && differentAttackInterruptTick >= 0
                    ? new TickIndex(checked(
                        startTick.Value + differentAttackInterruptTick))
                    : TickIndex.Invalid;
            TimingSnapshotHash = ComputeHash(
                mode,
                startTick,
                authoredDurationTicks,
                resolvedDurationTicks,
                authoredAttackFrameTick,
                windupTicks,
                intervalTicks,
                differentAttackInterruptTick,
                baseAttackSpeed,
                attackSpeedRatio,
                attackSpeedCap,
                bonusAttackSpeed,
                effectiveAttackSpeed,
                sourceGameplayHash,
                sourceTimingHash);
        }

        public FpgAttackTimingMode Mode { get; }
        public TickIndex StartTick { get; }
        public int AuthoredDurationTicks { get; }
        public int ResolvedDurationTicks { get; }
        public int DurationTicks => ResolvedDurationTicks;
        public int AuthoredAttackFrameTick { get; }
        public int WindupTicks { get; }
        public int IntervalTicks { get; }
        public int RecoveryTicks { get; }
        public int DifferentAttackInterruptRelativeTick { get; }
        public TickIndex AttackFrameTick { get; }
        public TickIndex SameAttackReadyTick { get; }
        public TickIndex DifferentAttackInterruptTick { get; }
        public double BaseAttackSpeed { get; }
        public double AttackSpeedRatio { get; }
        public double AttackSpeedCap { get; }
        public double BonusAttackSpeed { get; }
        public double EffectiveAttackSpeed { get; }
        public ulong SourceGameplayHash { get; }
        public ulong SourceTimingHash { get; }
        public ulong TimingSnapshotHash { get; }

        public bool IsValid => Enum.IsDefined(
                typeof(FpgAttackTimingMode),
                Mode)
            && StartTick.IsValid
            && AuthoredDurationTicks >= 0
            && ResolvedDurationTicks >= AuthoredDurationTicks
            && WindupTicks >= 0
            && IntervalTicks > 0
            && WindupTicks < IntervalTicks
            && AttackFrameTick.IsValid
            && SameAttackReadyTick.IsValid
            && DifferentAttackInterruptRelativeTick >= 0
            && DifferentAttackInterruptRelativeTick <= AuthoredDurationTicks
            && DifferentAttackInterruptTick.IsValid;

        public bool UsesCharacterAttackSpeed => Mode
            == FpgAttackTimingMode.CharacterAttackSpeed;

        public FpgAttackPhase GetPhase(TickIndex tick)
        {
            if (!IsValid || !tick.IsValid || tick < StartTick)
            {
                return FpgAttackPhase.None;
            }

            if (tick < AttackFrameTick)
            {
                return FpgAttackPhase.Windup;
            }

            return FpgAttackPhase.Recovery;
        }

        public bool IsInRecovery(TickIndex tick)
        {
            return IsValid && tick.IsValid && tick >= AttackFrameTick;
        }

        public bool IsSameAttackReadyAt(TickIndex tick)
        {
            return IsInRecovery(tick) && tick >= SameAttackReadyTick;
        }

        public bool CanInterruptWithDifferentAttackAt(TickIndex tick)
        {
            return IsValid
                && tick.IsValid
                && tick >= DifferentAttackInterruptTick;
        }

        private static ulong ComputeHash(
            FpgAttackTimingMode mode,
            TickIndex startTick,
            int authoredDurationTicks,
            int resolvedDurationTicks,
            int authoredAttackFrameTick,
            int windupTicks,
            int intervalTicks,
            int differentAttackInterruptTick,
            double baseAttackSpeed,
            double attackSpeedRatio,
            double attackSpeedCap,
            double bonusAttackSpeed,
            double effectiveAttackSpeed,
            ulong sourceGameplayHash,
            ulong sourceTimingHash)
        {
            ulong hash = StableHash.Mix(0x4650475F54534E31UL);
            hash = StableHash.Append(hash, sourceGameplayHash);
            hash = StableHash.Append(hash, sourceTimingHash);
            hash = StableHash.Append(hash, unchecked((ulong)(int)mode));
            hash = StableHash.Append(hash, unchecked((ulong)startTick.Value));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)authoredDurationTicks));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)resolvedDurationTicks));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)authoredAttackFrameTick));
            hash = StableHash.Append(hash, unchecked((ulong)windupTicks));
            hash = StableHash.Append(hash, unchecked((ulong)intervalTicks));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)differentAttackInterruptTick));
            hash = AppendDouble(hash, baseAttackSpeed);
            hash = AppendDouble(hash, attackSpeedRatio);
            hash = AppendDouble(hash, attackSpeedCap);
            hash = AppendDouble(hash, bonusAttackSpeed);
            return AppendDouble(hash, effectiveAttackSpeed);
        }

        private static ulong AppendDouble(ulong hash, double value)
        {
            return StableHash.Append(
                hash,
                unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
        }
    }

    public sealed class FpgResolvedSkillSchedule
    {
        private readonly int[] eventOrder;
        private readonly int[] resolvedTicks;
        private readonly int durationTicks;

        private FpgResolvedSkillSchedule(
            FpgCompiledSkillSequence sequence,
            FpgResolvedSkillTimingSnapshot timing,
            int[] eventOrder,
            int[] resolvedTicks,
            int durationTicks)
        {
            Sequence = sequence;
            Timing = timing;
            this.eventOrder = eventOrder;
            this.resolvedTicks = resolvedTicks;
            this.durationTicks = durationTicks;
        }

        public FpgCompiledSkillSequence Sequence { get; }
        public FpgResolvedSkillTimingSnapshot Timing { get; }
        public int DurationTicks => durationTicks;
        public int EventCount => eventOrder.Length;
        public bool IsValid => Sequence.IsValid
            && Timing.IsValid
            && eventOrder != null
            && resolvedTicks != null
            && eventOrder.Length == Sequence.EventCount
            && resolvedTicks.Length == Sequence.EventCount
            && durationTicks >= Sequence.DurationTicks
            && Timing.ResolvedDurationTicks == durationTicks;

        public FpgCompiledSkillEvent GetEvent(int scheduleIndex)
        {
            ValidateIndex(scheduleIndex);
            return Sequence.GetEvent(eventOrder[scheduleIndex]);
        }

        public int GetResolvedTick(int scheduleIndex)
        {
            ValidateIndex(scheduleIndex);
            return resolvedTicks[scheduleIndex];
        }

        public static FpgResolvedSkillSchedule CreateIdentity(
            FpgCompiledSkillSequence sequence,
            TickIndex startTick,
            int sequenceCooldownTicks = 0)
        {
            return CreateIdentity(
                sequence,
                startTick,
                sequenceCooldownTicks,
                FpgCompiledSkillTimingDefinition.Fixed);
        }

        internal static FpgResolvedSkillSchedule CreateIdentity(
            FpgCompiledSkillSequence sequence,
            TickIndex startTick,
            int sequenceCooldownTicks,
            FpgCompiledSkillTimingDefinition definition)
        {
            int attackFrame = FindFirstAttackFrame(sequence);
            if (attackFrame < 0)
            {
                attackFrame = 0;
            }

            int marker = definition.DifferentAttackInterruptTick < 0
                ? sequence.DurationTicks
                : definition.DifferentAttackInterruptTick;
            int interval = Math.Max(
                1,
                Math.Max(sequence.DurationTicks + 1, sequenceCooldownTicks));
            FpgResolvedSkillTimingSnapshot timing =
                new FpgResolvedSkillTimingSnapshot(
                    FpgAttackTimingMode.FixedCooldown,
                    startTick,
                    sequence.DurationTicks,
                    sequence.DurationTicks,
                    attackFrame,
                    attackFrame,
                    Math.Max(interval, attackFrame + 1),
                    marker,
                    FpgSkillRuntimeConstants.TickRate / (double)interval,
                    0d,
                    FpgSkillRuntimeConstants.TickRate / (double)interval,
                    0d,
                    FpgSkillRuntimeConstants.TickRate / (double)interval,
                    sequence.GameplayHash,
                    definition.TimingContractHash);
            return Create(sequence, timing);
        }

        internal static FpgResolvedSkillSchedule Create(
            FpgCompiledSkillSequence sequence,
            FpgResolvedSkillTimingSnapshot timing)
        {
            int count = sequence.EventCount;
            int[] indices = new int[count];
            int[] ticksByEventIndex = new int[count];
            int attackEventId = 0;

            for (int index = 0; index < count; index++)
            {
                indices[index] = index;
                FpgCompiledSkillEvent skillEvent = sequence.GetEvent(index);
                int resolvedTick = timing.UsesCharacterAttackSpeed
                    ? MapAuthoredTick(sequence, timing, skillEvent.Tick)
                    : skillEvent.Tick;
                if (timing.UsesCharacterAttackSpeed
                    && skillEvent.Kind == FpgSkillEventKind.GameplayAction
                    && skillEvent.ActionKind == FpgSkillActionKind.Attack)
                {
                    resolvedTick = timing.WindupTicks;
                    if (attackEventId == 0)
                    {
                        attackEventId = skillEvent.EventId;
                    }
                }

                ticksByEventIndex[index] = resolvedTick;
            }

            if (timing.UsesCharacterAttackSpeed && attackEventId != 0)
            {
                for (int index = 0; index < count; index++)
                {
                    FpgCompiledSkillEvent skillEvent = sequence.GetEvent(index);
                    if (skillEvent.BoundGameplayEventId == attackEventId)
                    {
                        ticksByEventIndex[index] = timing.WindupTicks;
                    }
                }
            }

            Array.Sort(indices, (left, right) =>
            {
                int tickOrder = ticksByEventIndex[left].CompareTo(
                    ticksByEventIndex[right]);
                if (tickOrder != 0)
                {
                    return tickOrder;
                }

                int authoredOrder = sequence.GetEvent(left).SortOrder
                    .CompareTo(sequence.GetEvent(right).SortOrder);
                return authoredOrder != 0
                    ? authoredOrder
                    : left.CompareTo(right);
            });

            int[] orderedTicks = new int[count];
            for (int index = 0; index < count; index++)
            {
                orderedTicks[index] = ticksByEventIndex[indices[index]];
            }

            return new FpgResolvedSkillSchedule(
                sequence,
                timing,
                indices,
                orderedTicks,
                timing.ResolvedDurationTicks);
        }

        public int GetResolvedTickForAuthoredEventIndex(int eventIndex)
        {
            if (eventIndex < 0 || eventIndex >= eventOrder.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(eventIndex));
            }

            for (int index = 0; index < eventOrder.Length; index++)
            {
                if (eventOrder[index] == eventIndex)
                {
                    return resolvedTicks[index];
                }
            }

            throw new InvalidOperationException(
                "Resolved schedule has no authored event mapping.");
        }

        internal static int MapAuthoredTick(
            FpgCompiledSkillSequence sequence,
            FpgResolvedSkillTimingSnapshot timing,
            int authoredTick)
        {
            if (!timing.UsesCharacterAttackSpeed)
            {
                return authoredTick;
            }

            int attackFrame = timing.AuthoredAttackFrameTick;
            if (authoredTick <= attackFrame)
            {
                return attackFrame <= 0
                    ? timing.WindupTicks
                    : DivideCeiling(
                        checked(authoredTick * timing.WindupTicks),
                        attackFrame);
            }

            int authoredRecovery = sequence.DurationTicks - attackFrame;
            if (authoredRecovery <= 0)
            {
                return timing.WindupTicks;
            }

            int resolvedRecovery = Math.Max(
                0,
                timing.IntervalTicks - 1 - timing.WindupTicks);
            int resolvedOffset = DivideCeiling(
                checked((authoredTick - attackFrame) * resolvedRecovery),
                authoredRecovery);
            return checked(timing.WindupTicks + resolvedOffset);
        }

        private static int DivideCeiling(int numerator, int denominator)
        {
            return numerator <= 0
                ? 0
                : checked((numerator + denominator - 1) / denominator);
        }

        internal static int FindFirstAttackFrame(
            FpgCompiledSkillSequence sequence)
        {
            for (int index = 0; index < sequence.EventCount; index++)
            {
                FpgCompiledSkillEvent skillEvent = sequence.GetEvent(index);
                if (skillEvent.Kind == FpgSkillEventKind.GameplayAction
                    && skillEvent.ActionKind == FpgSkillActionKind.Attack)
                {
                    return skillEvent.Tick;
                }
            }

            return -1;
        }

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= eventOrder.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }

    public static class FpgAttackTimingResolver
    {
        private const double TickCeilingEpsilon = 1e-9d;

        public static bool TryResolve(
            FpgCompiledSkillSequence sequence,
            FpgCompiledSkillTimingDefinition definition,
            int sequenceCooldownTicks,
            FpgAttackSpeedProfile profile,
            double bonusAttackSpeed,
            TickIndex startTick,
            out FpgResolvedSkillSchedule schedule,
            out string error)
        {
            schedule = null;
            if (!sequence.IsValid || sequenceCooldownTicks < 0
                || !startTick.IsValid)
            {
                error = "Attack timing requires a valid sequence, cooldown and start tick.";
                return false;
            }

            if (definition.IsFixed)
            {
                try
                {
                    schedule = FpgResolvedSkillSchedule.CreateIdentity(
                        sequence,
                        startTick,
                        sequenceCooldownTicks,
                        definition);
                }
                catch (OverflowException)
                {
                    schedule = null;
                    error = "Fixed attack timing overflows the simulation tick range.";
                    return false;
                }

                if (!schedule.IsValid)
                {
                    schedule = null;
                    error = "Fixed attack timing definition is invalid for the sequence.";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            if (!profile.IsValid
                || double.IsNaN(bonusAttackSpeed)
                || double.IsInfinity(bonusAttackSpeed))
            {
                error = "Character attack speed parameters are invalid.";
                return false;
            }

            int gameplayAttackCount = 0;
            for (int index = 0; index < sequence.EventCount; index++)
            {
                FpgCompiledSkillEvent skillEvent = sequence.GetEvent(index);
                if (skillEvent.Kind != FpgSkillEventKind.GameplayAction)
                {
                    continue;
                }

                if (skillEvent.ActionKind != FpgSkillActionKind.Attack
                    || ++gameplayAttackCount > 1)
                {
                    error = "Character attack-speed sequences require exactly one attack gameplay event and no other gameplay actions.";
                    return false;
                }
            }

            if (gameplayAttackCount != 1)
            {
                error = "Character attack-speed sequences require exactly one attack gameplay event and no other gameplay actions.";
                return false;
            }

            int authoredAttackFrame =
                definition.AuthoredAttackFrameTick >= 0
                    ? definition.AuthoredAttackFrameTick
                    : FpgResolvedSkillSchedule.FindFirstAttackFrame(sequence);
            if (authoredAttackFrame < 0
                || authoredAttackFrame > sequence.DurationTicks)
            {
                error = "Character attack-speed sequences require one valid attack frame.";
                return false;
            }

            int marker = definition.DifferentAttackInterruptTick < 0
                ? sequence.DurationTicks
                : definition.DifferentAttackInterruptTick;
            if (marker < 0 || marker > sequence.DurationTicks)
            {
                error = "Different-attack interrupt tick must be within the sequence duration.";
                return false;
            }

            double effectiveAttackSpeed = Math.Min(
                profile.AttackSpeedCap,
                profile.BaseAttackSpeed
                    + profile.AttackSpeedRatio * bonusAttackSpeed);
            if (effectiveAttackSpeed <= 0d
                || double.IsNaN(effectiveAttackSpeed)
                || double.IsInfinity(effectiveAttackSpeed))
            {
                error = "Resolved attack speed must be finite and positive.";
                return false;
            }

            int authoredCycleTicks = Math.Max(
                1,
                Math.Max(
                    checked(sequence.DurationTicks + 1),
                    sequenceCooldownTicks));
            double periodSeconds = 1d / effectiveAttackSpeed;
            double windupPercent = authoredAttackFrame
                / (double)authoredCycleTicks;
            double baseWindupSeconds = authoredAttackFrame
                / (double)FpgSkillRuntimeConstants.TickRate;
            double fullyScaledWindupSeconds = periodSeconds
                * windupPercent;
            double actualWindupSeconds = baseWindupSeconds
                + definition.WindupAttackSpeedCoefficient
                    * (fullyScaledWindupSeconds - baseWindupSeconds);

            int intervalTicks;
            int windupTicks;
            try
            {
                intervalTicks = CeilTicks(periodSeconds);
                windupTicks = CeilTicks(actualWindupSeconds, allowZero: true);
            }
            catch (OverflowException)
            {
                error = "Resolved attack timing exceeds tick capacity.";
                return false;
            }

            if (windupTicks >= intervalTicks)
            {
                double coefficient =
                    definition.WindupAttackSpeedCoefficient;
                double denominator = 1d - coefficient * windupPercent;
                if (denominator <= 0d)
                {
                    error = "Attack timing cannot preserve a recovery tick.";
                    return false;
                }

                double minimumIntervalTicks =
                    ((1d - coefficient) * authoredAttackFrame + 1d)
                    / denominator;
                try
                {
                    intervalTicks = Math.Max(
                        intervalTicks,
                        CeilPositive(minimumIntervalTicks));
                }
                catch (OverflowException)
                {
                    error = "Attack speed recovery protection exceeds tick capacity.";
                    return false;
                }
                effectiveAttackSpeed = FpgSkillRuntimeConstants.TickRate
                    / (double)intervalTicks;
                periodSeconds = 1d / effectiveAttackSpeed;
                fullyScaledWindupSeconds = periodSeconds * windupPercent;
                actualWindupSeconds = baseWindupSeconds
                    + coefficient
                        * (fullyScaledWindupSeconds - baseWindupSeconds);
                try
                {
                    windupTicks = CeilTicks(
                        actualWindupSeconds,
                        allowZero: true);
                }
                catch (OverflowException)
                {
                    error = "Resolved attack timing exceeds tick capacity.";
                    return false;
                }

                if (windupTicks >= intervalTicks)
                {
                    error = "Attack timing cannot preserve a recovery tick after limiting attack speed.";
                    return false;
                }
            }

            try
            {
                int resolvedDurationTicks = Math.Max(
                    sequence.DurationTicks,
                    checked(intervalTicks - 1));
                FpgResolvedSkillTimingSnapshot timing =
                    new FpgResolvedSkillTimingSnapshot(
                        definition.Mode,
                        startTick,
                        sequence.DurationTicks,
                        resolvedDurationTicks,
                        authoredAttackFrame,
                        windupTicks,
                        intervalTicks,
                        marker,
                        profile.BaseAttackSpeed,
                        profile.AttackSpeedRatio,
                        profile.AttackSpeedCap,
                        bonusAttackSpeed,
                        effectiveAttackSpeed,
                        sequence.GameplayHash,
                        definition.TimingContractHash);
                schedule = FpgResolvedSkillSchedule.Create(sequence, timing);
                if (!schedule.IsValid)
                {
                    schedule = null;
                    error = "Resolved attack schedule is invalid.";
                    return false;
                }
            }
            catch (OverflowException)
            {
                error = "Resolved attack timing overflows the simulation tick range.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static int CeilTicks(double seconds, bool allowZero = false)
        {
            if (double.IsNaN(seconds)
                || double.IsInfinity(seconds)
                || seconds < 0d)
            {
                throw new OverflowException();
            }

            double value = Math.Ceiling(
                seconds * FpgSkillRuntimeConstants.TickRate
                    - TickCeilingEpsilon);
            if (value > int.MaxValue)
            {
                throw new OverflowException();
            }

            int ticks = (int)value;
            return allowZero ? Math.Max(0, ticks) : Math.Max(1, ticks);
        }

        private static int CeilPositive(double value)
        {
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value <= 0d
                || value > int.MaxValue)
            {
                throw new OverflowException();
            }

            return Math.Max(
                1,
                (int)Math.Ceiling(value - TickCeilingEpsilon));
        }
    }
}
