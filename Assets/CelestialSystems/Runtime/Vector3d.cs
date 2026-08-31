/*
 * Stores a dependency-free three-dimensional vector using double-precision components.
 */

using System;

namespace jcan.CelestialSystems
{
    [Serializable]
    public struct Vector3d : IEquatable<Vector3d>
    {
        public double x;
        public double y;
        public double z;

        public static Vector3d zero =>
            new Vector3d(0.0, 0.0, 0.0);

        public double SqrMagnitude =>
            x * x +
            y * y +
            z * z;

        public double Magnitude =>
            Math.Sqrt(SqrMagnitude);

        public Vector3d(
            double x,
            double y,
            double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public bool Equals(Vector3d other)
        {
            return
                x.Equals(other.x) &&
                y.Equals(other.y) &&
                z.Equals(other.z);
        }

        public override bool Equals(object obj)
        {
            return
                obj is Vector3d other &&
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

        public static Vector3d operator +(
            Vector3d first,
            Vector3d second)
        {
            return new Vector3d(
                first.x + second.x,
                first.y + second.y,
                first.z + second.z);
        }

        public static Vector3d operator -(
            Vector3d first,
            Vector3d second)
        {
            return new Vector3d(
                first.x - second.x,
                first.y - second.y,
                first.z - second.z);
        }

        public static Vector3d operator *(
            Vector3d vector,
            double scalar)
        {
            return new Vector3d(
                vector.x * scalar,
                vector.y * scalar,
                vector.z * scalar);
        }

        public static Vector3d operator /(
            Vector3d vector,
            double scalar)
        {
            return new Vector3d(
                vector.x / scalar,
                vector.y / scalar,
                vector.z / scalar);
        }

        public static bool operator ==(
            Vector3d first,
            Vector3d second)
        {
            return first.Equals(second);
        }

        public static bool operator !=(
            Vector3d first,
            Vector3d second)
        {
            return !first.Equals(second);
        }
    }
}
