using System;

namespace FPG.Demo.Core
{
    public static class SpatialContract
    {
        public const int Version = 2;
        public const int PositionUnitsPerMeter = 1000;
        public const int DirectionUnits = 1000000;
        public const int DistanceUnitsPerMeter = 1000;
        public const int AttackQueryCandidateCapacity = 64;
    }

    public readonly struct GeometryId : IEquatable<GeometryId>, IComparable<GeometryId>
    {
        public static readonly GeometryId Invalid = new GeometryId(0);

        public GeometryId(int value)
        {
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;
        public int CompareTo(GeometryId other) => Value.CompareTo(other.Value);
        public bool Equals(GeometryId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GeometryId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
        public static bool operator ==(GeometryId left, GeometryId right) => left.Equals(right);
        public static bool operator !=(GeometryId left, GeometryId right) => !left.Equals(right);
    }

    // Quantized, engine-neutral vector. The scale is owned by the Unity adapter and
    // must be part of the adapter/version contract, never inferred by the domain.
    public readonly struct SpatialVectorKey : IEquatable<SpatialVectorKey>
    {
        public static readonly SpatialVectorKey Zero = new SpatialVectorKey(0, 0, 0);

        public SpatialVectorKey(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public bool IsZero => X == 0 && Y == 0 && Z == 0;

        public bool Equals(SpatialVectorKey other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is SpatialVectorKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X;
                hash = (hash * 397) ^ Y;
                return (hash * 397) ^ Z;
            }
        }

        public override string ToString() => $"({X},{Y},{Z})";
        public static bool operator ==(SpatialVectorKey left, SpatialVectorKey right) => left.Equals(right);
        public static bool operator !=(SpatialVectorKey left, SpatialVectorKey right) => !left.Equals(right);
    }
}
