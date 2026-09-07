/*
 * Generates spherical fibers by line-integral convolution through a seeded tangent vector field.
 */

using System;

namespace jcan.CelestialSystems
{
    public enum SphericalContourFiberOutput { Fibers, Field }

    public static class RoundMapMagicSphericalContourFibers
    {
        public static double EvaluateNormalized(
            DoubleVector3 direction, double radius, int seed,
            double flowScale, double samples, double fiberWidth,
            double flowComplexity, double sharpness, SphericalContourFiberOutput output)
        {
            var magnitude = direction.Magnitude;
            if (!Finite(magnitude) || magnitude <= 0 || !Finite(radius) || radius <= 0 ||
                !Finite(flowScale) || flowScale <= 0 || !Finite(fiberWidth) || fiberWidth <= 0)
                return 0;
            var n = direction / magnitude;
            var complexity = Clamp(Finite(flowComplexity) ? flowComplexity : 0.35, 0, 1);
            if (output == SphericalContourFiberOutput.Field)
            {
                var field = Flow(n, radius, seed, flowScale, complexity);
                return Clamp(0.5 + 0.5 * field.z, 0, 1);
            }
            var count = (int)Math.Round(Clamp(Finite(samples) ? samples : 24, 4, 48));
            var half = Math.Max(2, count / 2);
            var step = Clamp(fiberWidth / radius * 0.65, 0.0000001, 0.08);
            var carrierSeed = unchecked(seed + 15401);
            var total = Noise(n, radius, carrierSeed, fiberWidth);
            var totalWeight = 1.0;
            var forward = n;
            var backward = n;
            for (var i = 1; i <= half; i++)
            {
                forward = Normalize(forward + Flow(forward, radius, seed, flowScale, complexity) * step);
                backward = Normalize(backward - Flow(backward, radius, seed, flowScale, complexity) * step);
                var weight = 0.5 + 0.5 * Math.Cos(Math.PI * i / (half + 1.0));
                total += weight * (Noise(forward, radius, carrierSeed, fiberWidth) +
                                   Noise(backward, radius, carrierSeed, fiberWidth));
                totalWeight += 2 * weight;
            }
            // LIC compresses contrast around 0.5, so restore a useful normalized range.
            var value = Clamp((total / totalWeight - 0.5) * 3.0 + 0.5, 0, 1);
            return Math.Pow(value, Clamp(Finite(sharpness) ? sharpness : 1.2, 0.25, 8));
        }

        private static DoubleVector3 Flow(DoubleVector3 n, double radius, int seed, double scale, double complexity)
        {
            var v = new DoubleVector3(
                Noise(n, radius, unchecked(seed + 1013), scale) * 2 - 1,
                Noise(new DoubleVector3(n.y, n.z, n.x), radius, unchecked(seed + 3251), scale * 0.83) * 2 - 1,
                Noise(new DoubleVector3(n.z, n.x, n.y), radius, unchecked(seed + 7919), scale * 1.17) * 2 - 1);
            var swirl = Cross(n, v);
            var tangent = v - n * Dot(n, v);
            return Normalize(swirl * (1 - complexity) + tangent * complexity);
        }

        private static double Noise(DoubleVector3 n, double radius, int seed, double size)
        {
            return RoundMapMagicSphericalNoise.EvaluateNormalized(n, radius, seed, size, 1, 0.5);
        }

        private static DoubleVector3 Normalize(DoubleVector3 v)
        {
            var magnitude = v.Magnitude;
            return !Finite(magnitude) || magnitude <= 0 ? DoubleVector3.zero : v / magnitude;
        }

        private static double Dot(DoubleVector3 a, DoubleVector3 b) { return a.x*b.x + a.y*b.y + a.z*b.z; }
        private static DoubleVector3 Cross(DoubleVector3 a, DoubleVector3 b)
        {
            return new DoubleVector3(a.y*b.z-a.z*b.y, a.z*b.x-a.x*b.z, a.x*b.y-a.y*b.x);
        }
        private static double Clamp(double v, double lo, double hi) { return Math.Max(lo, Math.Min(hi, v)); }
        private static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
    }
}
