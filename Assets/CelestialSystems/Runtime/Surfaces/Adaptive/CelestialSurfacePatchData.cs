/*
 * Stores immutable sampled elevation and surface-control data separately from any Unity render or collider mesh.
 */

using System;
using System.Collections.Generic;

namespace jcan.CelestialSystems
{
    public sealed class CelestialSurfacePatchData
    {
        private readonly float[] elevationsMeters;
        private readonly float[] surfaceControlWeights;
        private readonly IReadOnlyList<float> elevationsView;
        private readonly IReadOnlyList<float> surfaceControlWeightsView;

        public CelestialSurfaceCacheKey CacheKey { get; }

        public CubeSpherePatchAddress Address { get; }

        public int Resolution { get; }

        public int SampleCount =>
            elevationsMeters.Length;

        public long EstimatedMemoryBytes =>
            256L +
            elevationsMeters.LongLength *
                sizeof(float) +
            (surfaceControlWeights != null
                ? surfaceControlWeights.LongLength *
                    sizeof(float)
                : 0L);

        public int SurfaceLayerCount { get; }

        public bool HasSurfaceControlData =>
            SurfaceLayerCount > 0 &&
            surfaceControlWeights != null &&
            surfaceControlWeights.Length ==
                SampleCount *
                SurfaceLayerCount;

        public float MinimumElevationMeters { get; }

        public float MaximumElevationMeters { get; }

        public float GeometricErrorMeters { get; }

        public IReadOnlyList<float> ElevationsMeters =>
            elevationsView;

        public IReadOnlyList<float> SurfaceControlWeights =>
            surfaceControlWeightsView;

        public CelestialSurfacePatchData(
            CelestialSurfaceCacheKey cacheKey,
            CubeSpherePatchAddress address,
            int resolution,
            float[] elevationsMeters,
            int surfaceLayerCount = 0,
            float[] surfaceControlWeights = null)
        {
            if (!cacheKey.IsValid)
            {
                throw new ArgumentException(
                    "Surface patch data requires a valid cache key.",
                    nameof(cacheKey));
            }

            if (!address.IsValid)
            {
                throw new ArgumentException(
                    "Surface patch data requires a valid patch address.",
                    nameof(address));
            }

            if (!CubeSpherePatchGrid.HasNestedResolution(
                    resolution))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(resolution),
                    "Surface patch resolution must have a power-of-two interval count.");
            }

            var expectedSampleCount =
                checked(
                    resolution *
                    resolution);

            if (elevationsMeters == null ||
                elevationsMeters.Length !=
                    expectedSampleCount)
            {
                throw new ArgumentException(
                    $"Surface patch elevations must contain exactly {expectedSampleCount} samples.",
                    nameof(elevationsMeters));
            }

            if (surfaceLayerCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(surfaceLayerCount));
            }

            if (surfaceLayerCount == 0 &&
                surfaceControlWeights != null &&
                surfaceControlWeights.Length > 0)
            {
                throw new ArgumentException(
                    "Surface control weights require at least one surface layer.",
                    nameof(surfaceControlWeights));
            }

            if (surfaceLayerCount > 0 &&
                (surfaceControlWeights == null ||
                    surfaceControlWeights.Length !=
                        checked(
                            expectedSampleCount *
                            surfaceLayerCount)))
            {
                throw new ArgumentException(
                    "Surface control weight dimensions do not match the patch samples and layer count.",
                    nameof(surfaceControlWeights));
            }

            CacheKey =
                cacheKey;
            Address =
                address;
            Resolution =
                resolution;
            SurfaceLayerCount =
                surfaceLayerCount;
            this.elevationsMeters =
                (float[])elevationsMeters.Clone();
            this.surfaceControlWeights =
                surfaceControlWeights != null
                    ? (float[])surfaceControlWeights.Clone()
                    : null;
            elevationsView =
                Array.AsReadOnly(
                    this.elevationsMeters);
            surfaceControlWeightsView =
                this.surfaceControlWeights != null
                    ? Array.AsReadOnly(
                        this.surfaceControlWeights)
                    : null;

            var minimumElevation =
                float.PositiveInfinity;
            var maximumElevation =
                float.NegativeInfinity;

            for (var index = 0;
                index < this.elevationsMeters.Length;
                index++)
            {
                var elevation =
                    this.elevationsMeters[index];

                if (!IsFinite(elevation))
                {
                    throw new ArgumentException(
                        "Surface patch elevations must contain only finite values.",
                        nameof(elevationsMeters));
                }

                minimumElevation =
                    Math.Min(
                        minimumElevation,
                        elevation);
                maximumElevation =
                    Math.Max(
                        maximumElevation,
                        elevation);
            }

            if (this.surfaceControlWeights != null)
            {
                for (var index = 0;
                    index < this.surfaceControlWeights.Length;
                    index++)
                {
                    if (!IsFinite(
                            this.surfaceControlWeights[index]))
                    {
                        throw new ArgumentException(
                            "Surface control weights must contain only finite values.",
                            nameof(surfaceControlWeights));
                    }
                }
            }

            MinimumElevationMeters =
                minimumElevation;
            MaximumElevationMeters =
                maximumElevation;
            GeometricErrorMeters =
                CalculateGeometricErrorMeters(
                    this.elevationsMeters,
                    resolution);
        }

        public float GetElevationMeters(
            int x,
            int y)
        {
            ValidateSampleCoordinate(
                x,
                y);
            return
                elevationsMeters[
                    y * Resolution +
                    x];
        }

        public float GetSurfaceControlWeight(
            int x,
            int y,
            int layer)
        {
            ValidateSampleCoordinate(
                x,
                y);

            if (!HasSurfaceControlData ||
                layer < 0 ||
                layer >= SurfaceLayerCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(layer));
            }

            return
                surfaceControlWeights[
                    (y * Resolution +
                        x) *
                        SurfaceLayerCount +
                    layer];
        }

        public bool TryGetInterpolatedElevationMeters(
            double normalizedX,
            double normalizedY,
            out float elevationMeters)
        {
            if (!IsFinite(normalizedX) ||
                !IsFinite(normalizedY) ||
                normalizedX < 0.0 ||
                normalizedX > 1.0 ||
                normalizedY < 0.0 ||
                normalizedY > 1.0)
            {
                elevationMeters = default;
                return false;
            }

            var sampleX =
                normalizedX *
                (Resolution - 1);
            var sampleY =
                normalizedY *
                (Resolution - 1);
            var lowerX =
                (int)Math.Floor(sampleX);
            var lowerY =
                (int)Math.Floor(sampleY);
            var upperX =
                Math.Min(
                    lowerX + 1,
                    Resolution - 1);
            var upperY =
                Math.Min(
                    lowerY + 1,
                    Resolution - 1);
            var blendX =
                sampleX - lowerX;
            var blendY =
                sampleY - lowerY;
            var lower =
                Lerp(
                    GetElevationMeters(
                        lowerX,
                        lowerY),
                    GetElevationMeters(
                        upperX,
                        lowerY),
                    blendX);
            var upper =
                Lerp(
                    GetElevationMeters(
                        lowerX,
                        upperY),
                    GetElevationMeters(
                        upperX,
                        upperY),
                    blendX);

            elevationMeters =
                (float)Lerp(
                    lower,
                    upper,
                    blendY);
            return true;
        }

        public void CopyElevationsTo(
            float[] destination)
        {
            if (destination == null ||
                destination.Length <
                    elevationsMeters.Length)
            {
                throw new ArgumentException(
                    "The destination array is too small for this surface patch.",
                    nameof(destination));
            }

            Array.Copy(
                elevationsMeters,
                destination,
                elevationsMeters.Length);
        }

        private static float CalculateGeometricErrorMeters(
            float[] elevations,
            int resolution)
        {
            var maximumError = 0.0f;

            for (var coarseY = 0;
                coarseY < resolution - 1;
                coarseY += 2)
            {
                for (var coarseX = 0;
                    coarseX < resolution - 1;
                    coarseX += 2)
                {
                    var bottomLeft =
                        elevations[
                            coarseY * resolution +
                            coarseX];
                    var bottomRight =
                        elevations[
                            coarseY * resolution +
                            coarseX + 2];
                    var topLeft =
                        elevations[
                            (coarseY + 2) *
                                resolution +
                            coarseX];
                    var topRight =
                        elevations[
                            (coarseY + 2) *
                                resolution +
                            coarseX + 2];

                    for (var offsetY = 0;
                        offsetY <= 2;
                        offsetY++)
                    {
                        var blendY =
                            offsetY * 0.5;

                        for (var offsetX = 0;
                            offsetX <= 2;
                            offsetX++)
                        {
                            var blendX =
                                offsetX * 0.5;
                            var expected =
                                Lerp(
                                    Lerp(
                                        bottomLeft,
                                        bottomRight,
                                        blendX),
                                    Lerp(
                                        topLeft,
                                        topRight,
                                        blendX),
                                    blendY);
                            var actual =
                                elevations[
                                    (coarseY + offsetY) *
                                        resolution +
                                    coarseX + offsetX];
                            maximumError =
                                Math.Max(
                                    maximumError,
                                    (float)Math.Abs(
                                        actual - expected));
                        }
                    }
                }
            }

            return maximumError;
        }

        private void ValidateSampleCoordinate(
            int x,
            int y)
        {
            if (x < 0 ||
                x >= Resolution ||
                y < 0 ||
                y >= Resolution)
            {
                throw new ArgumentOutOfRangeException(
                    $"Sample coordinate ({x}, {y}) is outside resolution {Resolution}.");
            }
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

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
