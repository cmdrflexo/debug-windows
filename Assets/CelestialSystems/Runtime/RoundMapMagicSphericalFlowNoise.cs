/*
 * Evaluates seam-safe domain-warped ridge noise for flowing structures on spherical bodies.
 */

using System;

namespace jcan.CelestialSystems
{
    public enum SphericalFlowNoiseOutputMode
    {
        Fibers,
        WarpedNoise,
        FlowMagnitude
    }

    public static class RoundMapMagicSphericalFlowNoise
    {
        public static double EvaluateNormalized(
            DoubleVector3 direction,
            double bodyRadiusMeters,
            int seed,
            double fiberSizeMeters,
            double flowSizeMeters,
            double flowStrength,
            int fiberOctaves,
            int flowOctaves,
            double persistence,
            double ridgeSharpness,
            SphericalFlowNoiseOutputMode outputMode)
        {
            var magnitude = direction.Magnitude;
            if (!IsFinite(magnitude) || magnitude <= 0.0 ||
                !IsFinite(bodyRadiusMeters) || bodyRadiusMeters <= 0.0 ||
                !IsFinite(fiberSizeMeters) || fiberSizeMeters <= 0.0 ||
                !IsFinite(flowSizeMeters) || flowSizeMeters <= 0.0)
            {
                return 0.0;
            }

            var normal = direction / magnitude;
            var flow = new DoubleVector3(
                SampleSigned(normal, bodyRadiusMeters, seed + 1597, flowSizeMeters, flowOctaves, persistence),
                SampleSigned(normal, bodyRadiusMeters, seed + 3571, flowSizeMeters, flowOctaves, persistence),
                SampleSigned(normal, bodyRadiusMeters, seed + 7919, flowSizeMeters, flowOctaves, persistence));
            var normalComponent =
                flow.x * normal.x +
                flow.y * normal.y +
                flow.z * normal.z;
            var tangentFlow = flow - normal * normalComponent;
            var tangentMagnitude = tangentFlow.Magnitude;

            if (outputMode == SphericalFlowNoiseOutputMode.FlowMagnitude)
            {
                return Clamp01(tangentMagnitude / 1.4142135623730951);
            }

            var resolvedStrength = IsFinite(flowStrength) ? flowStrength : 0.0;
            var angularWarp = resolvedStrength * fiberSizeMeters / bodyRadiusMeters;
            var warpedDirection = normal + tangentFlow * angularWarp;
            var warpedNoise = RoundMapMagicSphericalNoise.EvaluateNormalized(
                warpedDirection,
                bodyRadiusMeters,
                unchecked(seed + 104729),
                fiberSizeMeters,
                fiberOctaves,
                persistence);

            if (outputMode == SphericalFlowNoiseOutputMode.WarpedNoise)
            {
                return warpedNoise;
            }

            var ridge = 1.0 - Math.Abs(warpedNoise * 2.0 - 1.0);
            var sharpness = Math.Max(0.01, IsFinite(ridgeSharpness) ? ridgeSharpness : 2.0);
            return Clamp01(Math.Pow(ridge, sharpness));
        }

        private static double SampleSigned(
            DoubleVector3 direction,
            double bodyRadiusMeters,
            int seed,
            double featureSizeMeters,
            int octaves,
            double persistence)
        {
            return RoundMapMagicSphericalNoise.EvaluateNormalized(
                direction,
                bodyRadiusMeters,
                seed,
                featureSizeMeters,
                octaves,
                persistence) * 2.0 - 1.0;
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
