using System;

namespace FPG.Demo.Core
{
    public readonly struct RuntimeId : IEquatable<RuntimeId>, IComparable<RuntimeId>
    {
        public static readonly RuntimeId Invalid = new RuntimeId(0L);

        public RuntimeId(long value) { Value = value; }
        public long Value { get; }
        public bool IsValid => Value > 0L;
        public int CompareTo(RuntimeId other) => Value.CompareTo(other.Value);
        public bool Equals(RuntimeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RuntimeId other && Equals(other);
        public override int GetHashCode() => unchecked((int)(Value ^ (Value >> 32)));
        public override string ToString() => Value.ToString();
        public static bool operator ==(RuntimeId left, RuntimeId right) => left.Equals(right);
        public static bool operator !=(RuntimeId left, RuntimeId right) => !left.Equals(right);
    }

    public readonly struct AttackId : IEquatable<AttackId>, IComparable<AttackId>
    {
        public static readonly AttackId Invalid = new AttackId(0L);

        public AttackId(long value) { Value = value; }
        public long Value { get; }
        public bool IsValid => Value > 0L;
        public int CompareTo(AttackId other) => Value.CompareTo(other.Value);
        public bool Equals(AttackId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is AttackId other && Equals(other);
        public override int GetHashCode() => unchecked((int)(Value ^ (Value >> 32)));
        public override string ToString() => Value.ToString();
        public static bool operator ==(AttackId left, AttackId right) => left.Equals(right);
        public static bool operator !=(AttackId left, AttackId right) => !left.Equals(right);
    }

    public readonly struct ShotId : IEquatable<ShotId>, IComparable<ShotId>
    {
        public static readonly ShotId Invalid = new ShotId(0L);

        public ShotId(long value) { Value = value; }
        public long Value { get; }
        public bool IsValid => Value > 0L;
        public int CompareTo(ShotId other) => Value.CompareTo(other.Value);
        public bool Equals(ShotId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ShotId other && Equals(other);
        public override int GetHashCode() => unchecked((int)(Value ^ (Value >> 32)));
        public override string ToString() => Value.ToString();
        public static bool operator ==(ShotId left, ShotId right) => left.Equals(right);
        public static bool operator !=(ShotId left, ShotId right) => !left.Equals(right);
    }

    public readonly struct ProjectileId : IEquatable<ProjectileId>, IComparable<ProjectileId>
    {
        public static readonly ProjectileId Invalid = new ProjectileId(0L);

        public ProjectileId(long value) { Value = value; }
        public long Value { get; }
        public bool IsValid => Value > 0L;
        public int CompareTo(ProjectileId other) => Value.CompareTo(other.Value);
        public bool Equals(ProjectileId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ProjectileId other && Equals(other);
        public override int GetHashCode() => unchecked((int)(Value ^ (Value >> 32)));
        public override string ToString() => Value.ToString();
        public static bool operator ==(ProjectileId left, ProjectileId right) => left.Equals(right);
        public static bool operator !=(ProjectileId left, ProjectileId right) => !left.Equals(right);
    }

    public readonly struct ImpactId : IEquatable<ImpactId>, IComparable<ImpactId>
    {
        public static readonly ImpactId Invalid = new ImpactId(0L);

        public ImpactId(long value) { Value = value; }
        public long Value { get; }
        public bool IsValid => Value > 0L;
        public int CompareTo(ImpactId other) => Value.CompareTo(other.Value);
        public bool Equals(ImpactId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ImpactId other && Equals(other);
        public override int GetHashCode() => unchecked((int)(Value ^ (Value >> 32)));
        public override string ToString() => Value.ToString();
        public static bool operator ==(ImpactId left, ImpactId right) => left.Equals(right);
        public static bool operator !=(ImpactId left, ImpactId right) => !left.Equals(right);
    }

    public readonly struct InputSequence : IEquatable<InputSequence>, IComparable<InputSequence>
    {
        public static readonly InputSequence Invalid = new InputSequence(0L);

        public InputSequence(long value) { Value = value; }
        public long Value { get; }
        public bool IsValid => Value > 0L;
        public int CompareTo(InputSequence other) => Value.CompareTo(other.Value);
        public bool Equals(InputSequence other) => Value == other.Value;
        public override bool Equals(object obj) => obj is InputSequence other && Equals(other);
        public override int GetHashCode() => unchecked((int)(Value ^ (Value >> 32)));
        public override string ToString() => Value.ToString();
        public static bool operator ==(InputSequence left, InputSequence right) => left.Equals(right);
        public static bool operator !=(InputSequence left, InputSequence right) => !left.Equals(right);
    }

    public readonly struct ControlSequence : IEquatable<ControlSequence>, IComparable<ControlSequence>
    {
        public static readonly ControlSequence Invalid = new ControlSequence(0L);

        public ControlSequence(long value) { Value = value; }
        public long Value { get; }
        public bool IsValid => Value > 0L;
        public int CompareTo(ControlSequence other) => Value.CompareTo(other.Value);
        public bool Equals(ControlSequence other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ControlSequence other && Equals(other);
        public override int GetHashCode() => unchecked((int)(Value ^ (Value >> 32)));
        public override string ToString() => Value.ToString();
        public static bool operator ==(ControlSequence left, ControlSequence right) => left.Equals(right);
        public static bool operator !=(ControlSequence left, ControlSequence right) => !left.Equals(right);
    }

    public readonly struct AttackShotReservation
    {
        internal AttackShotReservation(AttackId attackId, ShotId shotId)
        {
            AttackId = attackId;
            ShotId = shotId;
        }

        public AttackId AttackId { get; }
        public ShotId ShotId { get; }
    }

    public sealed class SessionIdAllocator
    {
        private long nextRuntimeId = 1L;
        private long nextAttackId = 1L;
        private long nextShotId = 1L;
        private long nextProjectileId = 1L;
        private long nextImpactId = 1L;

        public RuntimeId NextRuntimeId()
        {
            return new RuntimeId(nextRuntimeId++);
        }

        public AttackShotReservation ReserveAttackAndShot()
        {
            return new AttackShotReservation(new AttackId(nextAttackId), new ShotId(nextShotId));
        }

        public bool Commit(AttackShotReservation reservation)
        {
            if (reservation.AttackId.Value != nextAttackId || reservation.ShotId.Value != nextShotId)
            {
                return false;
            }

            nextAttackId++;
            nextShotId++;
            return true;
        }

        public AttackId NextAttackId()
        {
            return new AttackId(nextAttackId++);
        }

        public ProjectileId NextProjectileId()
        {
            return new ProjectileId(nextProjectileId++);
        }

        public ImpactId NextImpactId()
        {
            return new ImpactId(nextImpactId++);
        }

        public void Reset()
        {
            nextRuntimeId = 1L;
            nextAttackId = 1L;
            nextShotId = 1L;
            nextProjectileId = 1L;
            nextImpactId = 1L;
        }
    }
}
