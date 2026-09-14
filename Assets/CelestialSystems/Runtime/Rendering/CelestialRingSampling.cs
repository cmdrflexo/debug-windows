/*
 * Shared radial sampling and deterministic identity utilities for all ring
 * presentation tiers: shader lookups, fog, and future near-object streaming.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    public readonly struct CelestialRingSample
    {
        public CelestialRingSample(
            int bandIndex,
            float radiusFraction,
            double radiusMeters,
            Color albedo,
            float density,
            float population,
            float particleScale,
            float verticalThicknessMeters,
            float iceWeight,
            float rockWeight,
            float dustWeight)
        {
            BandIndex = bandIndex;
            RadiusFraction = radiusFraction;
            RadiusMeters = radiusMeters;
            Albedo = albedo;
            Density = density;
            Population = population;
            ParticleScale = particleScale;
            VerticalThicknessMeters = verticalThicknessMeters;
            IceWeight = iceWeight;
            RockWeight = rockWeight;
            DustWeight = dustWeight;
        }

        public int BandIndex { get; }

        public float RadiusFraction { get; }

        public double RadiusMeters { get; }

        public Color Albedo { get; }

        public float Density { get; }

        public float Population { get; }

        public float ParticleScale { get; }

        public float VerticalThicknessMeters { get; }

        public float IceWeight { get; }

        public float RockWeight { get; }

        public float DustWeight { get; }
    }

    public readonly struct CelestialRingPolarCell
    {
        public CelestialRingPolarCell(
            int bandIndex,
            int radialIndex,
            int angularIndex)
        {
            BandIndex = bandIndex;
            RadialIndex = radialIndex;
            AngularIndex = angularIndex;
        }

        public int BandIndex { get; }

        public int RadialIndex { get; }

        public int AngularIndex { get; }
    }

    public static class CelestialRingSampling
    {
        public static bool TrySample(
            CelestialBodyDefinition definition,
            int bandIndex,
            float radiusFraction,
            out CelestialRingSample sample)
        {
            sample = default;

            if (definition == null ||
                !definition.HasRingSystemProperties ||
                bandIndex < 0 ||
                bandIndex >= definition.RingBandInnerRadiiMeters.Count ||
                bandIndex >= definition.RingBandOuterRadiiMeters.Count)
            {
                return false;
            }

            var innerRadius =
                definition.RingBandInnerRadiiMeters[
                    bandIndex];
            var outerRadius =
                definition.RingBandOuterRadiiMeters[
                    bandIndex];

            if (!IsFinitePositive(
                    innerRadius) ||
                !IsFinitePositive(
                    outerRadius) ||
                outerRadius <=
                    innerRadius)
            {
                return false;
            }

            var fraction =
                Mathf.Clamp01(
                    radiusFraction);
            var albedo =
                EvaluateGradient(
                    definition.RingAlbedoGradients,
                    bandIndex,
                    Color.white,
                    fraction);
            var density =
                EvaluateCurve(
                    definition.RingDensityGradients,
                    bandIndex,
                    1.0f,
                    fraction);
            var population =
                EvaluateCurve(
                    definition.RingPopulationGradients,
                    bandIndex,
                    density,
                    fraction);
            var particleScale =
                EvaluateCurve(
                    definition.RingParticleScaleGradients,
                    bandIndex,
                    1.0f,
                    fraction);
            var thickness =
                EvaluateCurve(
                    definition.RingVerticalThicknessGradients,
                    bandIndex,
                    0.0f,
                    fraction);
            var material =
                EvaluateGradient(
                    definition.RingMaterialGradients,
                    bandIndex,
                    new Color(
                        1.0f / 3.0f,
                        1.0f / 3.0f,
                        1.0f / 3.0f),
                    fraction);
            NormalizeMaterialWeights(
                material,
                out var ice,
                out var rock,
                out var dust);

            sample =
                new CelestialRingSample(
                    bandIndex,
                    fraction,
                    innerRadius +
                        (outerRadius -
                            innerRadius) *
                        fraction,
                    albedo,
                    Mathf.Clamp01(
                        density),
                    Mathf.Clamp01(
                        population),
                    Mathf.Max(
                        0.0f,
                        particleScale),
                    Mathf.Max(
                        0.0f,
                        thickness),
                    ice,
                    rock,
                    dust);
            return true;
        }

        public static uint GetStableCellHash(
            int universeSeed,
            string bodyDefinitionId,
            CelestialRingPolarCell cell,
            uint stream = 0u)
        {
            var hash =
                2166136261u;
            hash = Mix(
                hash,
                unchecked(
                    (uint)universeSeed));
            hash = MixString(
                hash,
                bodyDefinitionId);
            hash = Mix(
                hash,
                unchecked(
                    (uint)cell.BandIndex));
            hash = Mix(
                hash,
                unchecked(
                    (uint)cell.RadialIndex));
            hash = Mix(
                hash,
                unchecked(
                    (uint)cell.AngularIndex));
            return Mix(
                hash,
                stream);
        }

        public static float HashToUnitFloat(
            uint hash)
        {
            return (hash >> 8) *
                (1.0f /
                    16777216.0f);
        }

        private static Color EvaluateGradient(
            System.Collections.Generic.IReadOnlyList<Gradient> gradients,
            int index,
            Color fallback,
            float fraction)
        {
            return gradients != null &&
                index < gradients.Count &&
                gradients[index] != null
                    ? gradients[index].Evaluate(
                        fraction)
                    : fallback;
        }

        private static float EvaluateCurve(
            System.Collections.Generic.IReadOnlyList<AnimationCurve> curves,
            int index,
            float fallback,
            float fraction)
        {
            return curves != null &&
                index < curves.Count &&
                curves[index] != null
                    ? curves[index].Evaluate(
                        fraction)
                    : fallback;
        }

        private static void NormalizeMaterialWeights(
            Color material,
            out float ice,
            out float rock,
            out float dust)
        {
            ice =
                Mathf.Max(
                    0.0f,
                    material.r);
            rock =
                Mathf.Max(
                    0.0f,
                    material.g);
            dust =
                Mathf.Max(
                    0.0f,
                    material.b);
            var total =
                ice +
                rock +
                dust;

            if (total <=
                0.000001f)
            {
                ice =
                    1.0f / 3.0f;
                rock =
                    1.0f / 3.0f;
                dust =
                    1.0f / 3.0f;
                return;
            }

            ice /= total;
            rock /= total;
            dust /= total;
        }

        private static uint Mix(
            uint hash,
            uint value)
        {
            return (hash ^ value) *
                16777619u;
        }

        private static uint MixString(
            uint hash,
            string value)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return Mix(
                    hash,
                    0u);
            }

            for (var index = 0;
                index < value.Length;
                index++)
            {
                hash = Mix(
                    hash,
                    value[index]);
            }

            return hash;
        }

        private static bool IsFinitePositive(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
        }
    }
}
