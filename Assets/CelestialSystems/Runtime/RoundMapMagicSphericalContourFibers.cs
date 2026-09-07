/*
 * Generates periodic contour fibers from a smooth body-space scalar field.
 * Inspired by noise-perturbed procedural marble; no latitude/longitude or streamline copies.
 */

using System;

namespace jcan.CelestialSystems
{
    public enum SphericalContourFiberOutput { Fibers, Field }

    public static class RoundMapMagicSphericalContourFibers
    {
        public static double EvaluateNormalized(
            DoubleVector3 direction, double radius, int seed,
            double regionSize, double bands, double distortionSize,
            double distortion, double sharpness, SphericalContourFiberOutput output)
        {
            var magnitude = direction.Magnitude;
            if (!Finite(magnitude) || magnitude <= 0 ||
                !Finite(radius) || radius <= 0 ||
                !Finite(regionSize) || regionSize <= 0 ||
                !Finite(distortionSize) || distortionSize <= 0)
                return 0;
            var n = direction / magnitude;
            // Fixed orthonormal rotations reduce alignment with the value-noise lattice.
            var a = Rotate(n);
            var b = Rotate(new DoubleVector3(n.z, n.x, n.y));
            var field =
                0.65 * Noise(a, radius, seed, regionSize) +
                0.35 * Noise(b, radius, unchecked(seed + 1013), regionSize * 0.73);
            if (output == SphericalContourFiberOutput.Field) return field;
            var count = Clamp(Finite(bands) ? bands : 12, 1, 64);
            var strength = Clamp(Finite(distortion) ? distortion : 0, 0, 2);
            // Perturb phase in cycles, independently of the broad field's contour count.
            var detail = Noise(b, radius, unchecked(seed + 7919), distortionSize) * 2 - 1;
            var phase = field * count + detail * strength;
            var wave = 0.5 + 0.5 * Math.Cos(2 * Math.PI * phase);
            return Math.Pow(Clamp(wave, 0, 1),
                Clamp(Finite(sharpness) ? sharpness : 2, 0.25, 16));
        }

        private static double Noise(DoubleVector3 n, double r, int seed, double size)
        {
            return RoundMapMagicSphericalNoise.EvaluateNormalized(n, r, seed, size, 1, 0.5);
        }

        private static DoubleVector3 Rotate(DoubleVector3 n)
        {
            return new DoubleVector3(
                (n.x + 2 * n.y + 2 * n.z) / 3,
                (2 * n.x + n.y - 2 * n.z) / 3,
                (-2 * n.x + 2 * n.y - n.z) / 3);
        }

        private static double Clamp(double v, double lo, double hi)
        {
            return Math.Max(lo, Math.Min(hi, v));
        }

        private static bool Finite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }
    }
}
