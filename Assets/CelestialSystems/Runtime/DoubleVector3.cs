/*
 * Stores a dependency-free three-dimensional vector using double-precision components.
 */

using System;

namespace jcan.CelestialSystems
{
    [Serializable]
    public struct DoubleVector3 : IEquatable<DoubleVector3>
    {
        public double x;
        public double y;
        public double z;

        public static DoubleVector3 zero =>
            new DoubleVector3(0.0, 0.0, 0.0);

        public double SqrMagnitude =>
            x * x +
            y * y +
            z * z;

        public double Magnitude =>
            Math.Sqrt(SqrMagnitude);

        public DoubleVector3(
            double x,
            double y,
            double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public bool Equals(DoubleVector3 other)
        {
            return
                x.Equals(other.x) &&
                y.Equals(other.y) &&
                z.Equals(other.z);
        }

        public override bool Equals(object obj)
        {
            return
                obj is DoubleVector3 other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = x.GetHashCode();
                hashCode = (hashCode * 397) ^ y.GetHashCode();
                hashCode = (hashCode * 397) ^ z.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"({x}, {y}, {z})";
        }

        public static DoubleVector3 operator +(
            DoubleVector3 first,
            DoubleVector3 second)
        {
            return new DoubleVector3(
                first.x + second.x,
                first.y + second.y,
                first.z + second.z);
        }

        public static DoubleVector3 operator -(
            DoubleVector3 first,
            DoubleVector3 second)
        {
            return new DoubleVector3(
                first.x - second.x,
                first.y - second.y,
                first.z - second.z);
        }

        public static DoubleVector3 operator *(
            DoubleVector3 vector,
            double scalar)
        {
            return new DoubleVector3(
                vector.x * scalar,
                vector.y * scalar,
                vector.z * scalar);
        }

        public static DoubleVector3 operator /(
            DoubleVector3 vector,
            double scalar)
        {
            return new DoubleVector3(
                vector.x / scalar,
                vector.y / scalar,
                vector.z / scalar);
        }

        public static bool operator ==(
            DoubleVector3 first,
            DoubleVector3 second)
        {
            return first.Equals(second);
        }

        public static bool operator !=(
            DoubleVector3 first,
            DoubleVector3 second)
        {
            return !first.Equals(second);
        }
    }
}
