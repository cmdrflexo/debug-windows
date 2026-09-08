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
            double fiberLengthMeters,
            int flowSamples,
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
            var tangentFlow = EvaluateTangentFlow(
                normal,
                bodyRadiusMeters,
                seed,
                flowSizeMeters,
                flowOctaves,
                persistence);
            var tangentMagnitude = tangentFlow.Magnitude;

            if (outputMode == SphericalFlowNoiseOutputMode.FlowMagnitude)
            {
                return Clamp01(tangentMagnitude / 1.4142135623730951);
            }

            var resolvedStrength = IsFinite(flowStrength) ? flowStrength : 0.0;
            var angularWarp = resolvedStrength * fiberSizeMeters / bodyRadiusMeters;
            var warpedDirection = normal + tangentFlow * angularWarp;
            var warpedMagnitude = warpedDirection.Magnitude;
            var warpedNormal = warpedMagnitude > 0.0
                ? warpedDirection / warpedMagnitude
                : normal;
            var resolvedSamples = Math.Max(1, Math.Min(9, flowSamples));
            if (resolvedSamples % 2 == 0)
            {
                resolvedSamples--;
            }
            var resolvedLength = Math.Max(
                0.0,
                IsFinite(fiberLengthMeters) ? fiberLengthMeters : 0.0);
            var pairCount = (resolvedSamples - 1) / 2;
            var stepAngle = pairCount > 0
                ? resolvedLength * 0.5 / pairCount / bodyRadiusMeters
                : 0.0;
            var noiseSum = 0.0;
            var fiberSum = 0.0;
            var weightSum = 0.0;
            var sharpness = Math.Max(0.01, IsFinite(ridgeSharpness) ? ridgeSharpness : 2.0);
            AccumulateFiberSample(
                warpedNormal,
                bodyRadiusMeters,
                seed,
                fiberSizeMeters,
                fiberOctaves,
                persistence,
                sharpness,
                1.0,
                ref noiseSum,
                ref fiberSum,
                ref weightSum);

            var forward = warpedNormal;
            var backward = warpedNormal;
            for (var step = 1; step <= pairCount; step++)
            {
                forward = TraceFlowStep(
                    forward, 1.0, stepAngle, bodyRadiusMeters, seed,
                    flowSizeMeters, flowOctaves, persistence);
                backward = TraceFlowStep(
                    backward, -1.0, stepAngle, bodyRadiusMeters, seed,
                    flowSizeMeters, flowOctaves, persistence);
                var weight = 1.0 - (double)step / pairCount * 0.35;
                AccumulateFiberSample(
                    forward, bodyRadiusMeters, seed, fiberSizeMeters,
                    fiberOctaves, persistence, sharpness, weight,
                    ref noiseSum, ref fiberSum, ref weightSum);
                AccumulateFiberSample(
                    backward, bodyRadiusMeters, seed, fiberSizeMeters,
                    fiberOctaves, persistence, sharpness, weight,
                    ref noiseSum, ref fiberSum, ref weightSum);
            }

            var warpedNoise = weightSum > 0.0 ? noiseSum / weightSum : 0.5;

            if (outputMode == SphericalFlowNoiseOutputMode.WarpedNoise)
            {
                return warpedNoise;
            }

            return Clamp01(weightSum > 0.0 ? fiberSum / weightSum : 0.0);
        }

        private static DoubleVector3 TraceFlowStep(
            DoubleVector3 direction,
            double sign,
            double stepAngle,
            double bodyRadiusMeters,
            int seed,
            double flowSizeMeters,
            int flowOctaves,
            double persistence)
        {
            var tangent = EvaluateTangentFlow(
                direction, bodyRadiusMeters, seed, flowSizeMeters,
                flowOctaves, persistence);
            var tangentMagnitude = tangent.Magnitude;
            if (tangentMagnitude <= 0.000001 || stepAngle <= 0.0)
            {
                return direction;
            }

            var stepped = direction + tangent / tangentMagnitude * (sign * stepAngle);
            var steppedMagnitude = stepped.Magnitude;
            return steppedMagnitude > 0.0 ? stepped / steppedMagnitude : direction;
        }

        private static DoubleVector3 EvaluateTangentFlow(
            DoubleVector3 normal,
            double bodyRadiusMeters,
            int seed,
            double flowSizeMeters,
            int flowOctaves,
            double persistence)
        {
            var flow = new DoubleVector3(
                SampleSigned(normal, bodyRadiusMeters, seed + 1597, flowSizeMeters, flowOctaves, persistence),
                SampleSigned(normal, bodyRadiusMeters, seed + 3571, flowSizeMeters, flowOctaves, persistence),
                SampleSigned(normal, bodyRadiusMeters, seed + 7919, flowSizeMeters, flowOctaves, persistence));
            var normalComponent =
                flow.x * normal.x +
                flow.y * normal.y +
                flow.z * normal.z;
            return flow - normal * normalComponent;
        }

        private static void AccumulateFiberSample(
            DoubleVector3 direction,
            double bodyRadiusMeters,
            int seed,
            double fiberSizeMeters,
            int fiberOctaves,
            double persistence,
            double sharpness,
            double weight,
            ref double noiseSum,
            ref double fiberSum,
            ref double weightSum)
        {
            var sampleNoise = RoundMapMagicSphericalNoise.EvaluateNormalized(
                direction,
                bodyRadiusMeters,
                unchecked(seed + 104729),
                fiberSizeMeters,
                fiberOctaves,
                persistence);
            var sampleRidge = 1.0 - Math.Abs(sampleNoise * 2.0 - 1.0);
            noiseSum += sampleNoise * weight;
            fiberSum += Math.Pow(sampleRidge, sharpness) * weight;
            weightSum += weight;
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
