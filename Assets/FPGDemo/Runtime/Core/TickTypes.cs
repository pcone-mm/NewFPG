using System;

namespace FPG.Demo.Core
{
    public readonly struct TickIndex : IEquatable<TickIndex>, IComparable<TickIndex>
    {
        public static readonly TickIndex Invalid = new TickIndex(-1L);

        public TickIndex(long value)
        {
            Value = value;
        }

        public long Value { get; }

        public bool IsValid => Value >= 0L;

        public int CompareTo(TickIndex other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(TickIndex other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is TickIndex other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((int)(Value ^ (Value >> 32)));
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static TickIndex operator +(TickIndex tick, TickDuration duration)
        {
            return new TickIndex(tick.Value + duration.Value);
        }

        public static long operator -(TickIndex left, TickIndex right)
        {
            return left.Value - right.Value;
        }

        public static bool operator ==(TickIndex left, TickIndex right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TickIndex left, TickIndex right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(TickIndex left, TickIndex right)
        {
            return left.Value < right.Value;
        }

        public static bool operator >(TickIndex left, TickIndex right)
        {
            return left.Value > right.Value;
        }

        public static bool operator <=(TickIndex left, TickIndex right)
        {
            return left.Value <= right.Value;
        }

        public static bool operator >=(TickIndex left, TickIndex right)
        {
            return left.Value >= right.Value;
        }
    }

    public readonly struct TickDuration : IEquatable<TickDuration>
    {
        public static readonly TickDuration Zero = new TickDuration(0);

        public TickDuration(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Value = value;
        }

        public int Value { get; }

        public static TickDuration FromSeconds(double seconds, int tickRate = GameplayClock.DefaultTickRate)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            if (tickRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate));
            }

            double ticks = Math.Ceiling(seconds * tickRate);
            if (ticks > int.MaxValue)
            {
                throw new OverflowException("Tick duration exceeds Int32 capacity.");
            }

            return new TickDuration((int)ticks);
        }

        public bool Equals(TickDuration other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is TickDuration other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(TickDuration left, TickDuration right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TickDuration left, TickDuration right)
        {
            return !left.Equals(right);
        }
    }
}
