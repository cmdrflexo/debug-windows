/*
 * Evaluates deterministic coherent 3D surface noise from a planet direction, independent of cube face or tile.
 */

using System;

namespace jcan.CelestialSystems
{
    public static class RoundMapMagicSphericalNoise
    {
        private const int MaximumOctaves = 12;
        private const double UnitHashScale =
            1.0 / 9007199254740992.0;

        public static double EvaluateNormalized(
            DoubleVector3 direction,
            double planetRadiusMeters,
            int seed,
            double featureSizeMeters,
            int octaves,
            double persistence)
        {
            var magnitude =
                Math.Sqrt(
                    direction.x * direction.x +
                    direction.y * direction.y +
                    direction.z * direction.z);

            if (!IsFinite(magnitude) ||
                magnitude <= 0.0 ||
                !IsFinite(planetRadiusMeters) ||
                planetRadiusMeters <= 0.0 ||
                !IsFinite(featureSizeMeters) ||
                featureSizeMeters <= 0.0)
            {
                return 0.5;
            }

            var resolvedOctaves =
                Math.Max(
                    1,
                    Math.Min(
                        MaximumOctaves,
                        octaves));
            var resolvedPersistence =
                Clamp01(
                    IsFinite(persistence)
                        ? persistence
                        : 0.5);
            var baseScale =
                planetRadiusMeters /
                (featureSizeMeters *
                    magnitude);
            var sampleX =
                direction.x *
                baseScale;
            var sampleY =
                direction.y *
                baseScale;
            var sampleZ =
                direction.z *
                baseScale;
            var amplitude = 1.0;
            var amplitudeSum = 0.0;
            var value = 0.0;

            for (var octave = 0;
                octave < resolvedOctaves;
                octave++)
            {
                value +=
                    EvaluateValueNoise(
                        sampleX,
                        sampleY,
                        sampleZ,
                        unchecked(
                            seed +
                            octave * 1013)) *
                    amplitude;
                amplitudeSum +=
                    amplitude;
                amplitude *=
                    resolvedPersistence;
                sampleX *= 2.0;
                sampleY *= 2.0;
                sampleZ *= 2.0;
            }

            if (amplitudeSum <= 0.0)
            {
                return 0.5;
            }

            return
                Clamp01(
                    value /
                        amplitudeSum *
                        0.5 +
                    0.5);
        }

        private static double EvaluateValueNoise(
            double x,
            double y,
            double z,
            int seed)
        {
            var lowerX =
                FloorToLong(x);
            var lowerY =
                FloorToLong(y);
            var lowerZ =
                FloorToLong(z);
            var blendX =
                Fade(
                    x -
                    lowerX);
            var blendY =
                Fade(
                    y -
                    lowerY);
            var blendZ =
                Fade(
                    z -
                    lowerZ);

            var lowerLower =
                Lerp(
                    LatticeValue(
                        lowerX,
                        lowerY,
                        lowerZ,
                        seed),
                    LatticeValue(
                        lowerX + 1,
                        lowerY,
                        lowerZ,
                        seed),
                    blendX);
            var lowerUpper =
                Lerp(
                    LatticeValue(
                        lowerX,
                        lowerY + 1,
                        lowerZ,
                        seed),
                    LatticeValue(
                        lowerX + 1,
                        lowerY + 1,
                        lowerZ,
                        seed),
                    blendX);
            var upperLower =
                Lerp(
                    LatticeValue(
                        lowerX,
                        lowerY,
                        lowerZ + 1,
                        seed),
                    LatticeValue(
                        lowerX + 1,
                        lowerY,
                        lowerZ + 1,
                        seed),
                    blendX);
            var upperUpper =
                Lerp(
                    LatticeValue(
                        lowerX,
                        lowerY + 1,
                        lowerZ + 1,
                        seed),
                    LatticeValue(
                        lowerX + 1,
                        lowerY + 1,
                        lowerZ + 1,
                        seed),
                    blendX);

            return
                Lerp(
                    Lerp(
                        lowerLower,
                        lowerUpper,
                        blendY),
                    Lerp(
                        upperLower,
                        upperUpper,
                        blendY),
                    blendZ);
        }

        private static double LatticeValue(
            long x,
            long y,
            long z,
            int seed)
        {
            unchecked
            {
                var hash =
                    1469598103934665603UL;
                hash =
                    (hash ^ (ulong)x) *
                    1099511628211UL;
                hash =
                    (hash ^ (ulong)y) *
                    1099511628211UL;
                hash =
                    (hash ^ (ulong)z) *
                    1099511628211UL;
                hash =
                    (hash ^ (uint)seed) *
                    1099511628211UL;
                hash ^= hash >> 30;
                hash *= 0xbf58476d1ce4e5b9UL;
                hash ^= hash >> 27;
                hash *= 0x94d049bb133111ebUL;
                hash ^= hash >> 31;

                return
                    (hash >> 11) *
                        UnitHashScale *
                        2.0 -
                    1.0;
            }
        }

        private static long FloorToLong(double value)
        {
            var floored =
                Math.Floor(value);

            if (floored <= long.MinValue)
            {
                return long.MinValue;
            }

            if (floored >= long.MaxValue)
            {
                return long.MaxValue;
            }

            return (long)floored;
        }

        private static double Fade(double value)
        {
            return
                value *
                value *
                value *
                (value *
                    (value * 6.0 - 15.0) +
                    10.0);
        }

        private static double Lerp(
            double first,
            double second,
            double blend)
        {
            return
                first +
                (second - first) *
                blend;
        }

        private static double Clamp01(double value)
        {
            return
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        value));
        }

        private static bool IsFinite(double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
