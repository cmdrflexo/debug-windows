/*
 * Validates nested parent-child samples, cube-face seams, MapMagic patch bounds, metadata, and universe positions.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialSurfaceFoundationDiagnostics :
        MonoBehaviour
    {
        private static readonly CubeSphereFace[] Faces =
        {
            CubeSphereFace.PositiveX,
            CubeSphereFace.NegativeX,
            CubeSphereFace.PositiveY,
            CubeSphereFace.NegativeY,
            CubeSphereFace.PositiveZ,
            CubeSphereFace.NegativeZ
        };

        private static readonly CubeSphereEdge[] Edges =
        {
            CubeSphereEdge.NegativeU,
            CubeSphereEdge.PositiveU,
            CubeSphereEdge.NegativeV,
            CubeSphereEdge.PositiveV
        };

        [Header("Runtime Source")]
        [SerializeField]
        private CelestialSurfaceRuntime surfaceRuntime;

        [SerializeField]
        private bool validateOnInitialize = true;

        [Header("Validation")]
        [SerializeField]
        private double maximumDirectionGapMeters =
            0.000001;

        [SerializeField]
        private double maximumNoiseDifference =
            0.000000000001;

        [Header("Runtime Result")]
        [SerializeField]
        private bool hasResult;

        [SerializeField]
        private bool passed;

        [SerializeField]
        private int testedSampleCount;

        [SerializeField]
        private double largestDirectionGapMeters;

        [SerializeField]
        private double largestNoiseDifference;

        [SerializeField]
        private bool mapMagicCoordinatesMatch;

        [SerializeField]
        private bool patchMetadataValid;

        [SerializeField]
        private string lastError;

        public CelestialSurfaceRuntime SurfaceRuntime =>
            surfaceRuntime;

        public bool HasResult =>
            hasResult;

        public bool Passed =>
            passed;

        public int TestedSampleCount =>
            testedSampleCount;

        public double LargestDirectionGapMeters =>
            largestDirectionGapMeters;

        public double LargestNoiseDifference =>
            largestNoiseDifference;

        public bool MapMagicCoordinatesMatch =>
            mapMagicCoordinatesMatch;

        public bool PatchMetadataValid =>
            patchMetadataValid;

        public string LastError =>
            lastError;

        public void Initialize(
            CelestialSurfaceRuntime newSurfaceRuntime)
        {
            surfaceRuntime =
                newSurfaceRuntime;

            if (validateOnInitialize)
            {
                ValidateFoundation();
            }
        }

        [ContextMenu("Validate Surface Foundation")]
        public void ValidateFoundation()
        {
            hasResult = true;
            passed = false;
            testedSampleCount = 0;
            largestDirectionGapMeters = 0.0;
            largestNoiseDifference = 0.0;
            mapMagicCoordinatesMatch = false;
            patchMetadataValid = false;
            lastError = string.Empty;

            if (surfaceRuntime == null ||
                !surfaceRuntime.FoundationReady)
            {
                Fail(
                    "The packaged surface foundation is not ready.");
                return;
            }

            var resolution =
                surfaceRuntime.PatchResolution;
            var radiusMeters =
                surfaceRuntime.BodyDefinition.ReferenceRadiusMeters;

            if (!ValidateAddressAndPolicy(
                    radiusMeters))
            {
                return;
            }

            for (var faceIndex = 0;
                faceIndex < Faces.Length;
                faceIndex++)
            {
                var root =
                    CubeSpherePatchAddress.Root(
                        Faces[faceIndex]);

                if (!ValidateNestedSamples(
                        root,
                        resolution,
                        radiusMeters))
                {
                    return;
                }

                var representative =
                    new CubeSpherePatchAddress(
                        Faces[faceIndex],
                        3,
                        (faceIndex * 3) % 8,
                        (faceIndex * 5 + 1) % 8);

                if (!ValidateNestedSamples(
                        representative,
                        resolution,
                        radiusMeters) ||
                    !ValidateMapMagicBridge(
                        root,
                        resolution,
                        radiusMeters) ||
                    !ValidateMapMagicBridge(
                        representative,
                        resolution,
                        radiusMeters))
                {
                    return;
                }
            }

            if (!ValidateFaceSeams(
                    resolution,
                    radiusMeters) ||
                !ValidateUniversePosition(
                    resolution,
                    radiusMeters) ||
                !ValidatePatchMetadata(
                    resolution))
            {
                return;
            }

            mapMagicCoordinatesMatch = true;
            patchMetadataValid = true;
            passed =
                largestDirectionGapMeters <=
                    maximumDirectionGapMeters &&
                largestNoiseDifference <=
                    maximumNoiseDifference;

            if (!passed)
            {
                Fail(
                    "The surface foundation exceeded its numerical tolerance.");
            }
        }

        private bool ValidateNestedSamples(
            CubeSpherePatchAddress parent,
            int resolution,
            double radiusMeters)
        {
            for (var y = 0;
                y < resolution;
                y++)
            {
                for (var x = 0;
                    x < resolution;
                    x++)
                {
                    if (!CubeSpherePatchGrid.TryGetMatchingChildSample(
                            parent,
                            resolution,
                            x,
                            y,
                            out var child,
                            out var childX,
                            out var childY) ||
                        !CubeSpherePatchGrid.TryGetSampleDirection(
                            parent,
                            resolution,
                            x,
                            y,
                            out var parentDirection) ||
                        !CubeSpherePatchGrid.TryGetSampleDirection(
                            child,
                            resolution,
                            childX,
                            childY,
                            out var childDirection))
                    {
                        Fail(
                            $"Could not resolve nested sample {parent} ({x}, {y}).");
                        return false;
                    }

                    RecordDirectionGap(
                        parentDirection,
                        childDirection,
                        radiusMeters);
                    testedSampleCount++;
                }
            }

            return true;
        }

        private bool ValidateAddressAndPolicy(
            double radiusMeters)
        {
            if (default(CubeSpherePatchAddress).IsValid ||
                !surfaceRuntime.CacheKey.IsValid ||
                !surfaceRuntime.LodPolicy.IsValid)
            {
                Fail(
                    "The patch address, cache key, or LOD policy contract is invalid.");
                return false;
            }

            var surfaceAddress =
                new CubeSphereAddress(
                    CubeSphereFace.NegativeZ,
                    0.375,
                    -0.625,
                    0.0);

            if (!CubeSpherePatchAddress.TryFromAddress(
                    surfaceAddress,
                    4,
                    out var patchAddress) ||
                !patchAddress.Contains(
                    surfaceAddress) ||
                !patchAddress.TryGetParent(
                    out var parentAddress) ||
                !parentAddress.IsValid)
            {
                Fail(
                    "Canonical surface coordinates did not resolve to a valid patch hierarchy.");
                return false;
            }

            var renderObserver =
                new CelestialSurfaceObserverState(
                    "foundation-camera",
                    new DoubleVector3(
                        0.0,
                        0.0,
                        radiusMeters * 2.0),
                    60.0,
                    1080,
                    true,
                    false);
            var collisionObserver =
                new CelestialSurfaceObserverState(
                    "foundation-collider",
                    new DoubleVector3(
                        radiusMeters + 10.0,
                        0.0,
                        0.0),
                    0.0,
                    0,
                    false,
                    true);
            var projectedError =
                surfaceRuntime.LodPolicy.EstimateProjectedErrorPixels(
                    100.0,
                    radiusMeters,
                    renderObserver);

            if (!renderObserver.IsValid ||
                !collisionObserver.IsValid ||
                double.IsNaN(projectedError) ||
                double.IsInfinity(projectedError) ||
                projectedError <= 0.0)
            {
                Fail(
                    "The shared observer or screen-error policy is invalid.");
                return false;
            }

            testedSampleCount += 3;
            return true;
        }

        private bool ValidateMapMagicBridge(
            CubeSpherePatchAddress patch,
            int resolution,
            double radiusMeters)
        {
            if (!surfaceRuntime.TryCreateMapMagicRequest(
                    patch,
                    out var request,
                    out var error))
            {
                Fail(error);
                return false;
            }

            var currentMapMagicContext =
                new RoundMapMagicSphericalTileData
                {
                    Face =
                        request.Address.Face,
                    PlanetRadiusMeters =
                        request.PlanetRadiusMeters,
                    SurfaceSeed =
                        request.SurfaceSeed,
                    MapWorldOriginXMeters =
                        request.MapWorldOriginXMeters,
                    MapWorldOriginZMeters =
                        request.MapWorldOriginZMeters,
                    MapWorldSizeXMeters =
                        request.MapWorldSizeXMeters,
                    MapWorldSizeZMeters =
                        request.MapWorldSizeZMeters
                };

            for (var pixelZ = 0;
                pixelZ < resolution;
                pixelZ++)
            {
                var sampleY =
                    request.MapPixelZToPatchSampleY(
                        pixelZ);

                for (var pixelX = 0;
                    pixelX < resolution;
                    pixelX++)
                {
                    if (!request.TryMapPixelToDirection(
                            pixelX,
                            pixelZ,
                            out var mapMagicDirection) ||
                        !currentMapMagicContext.TryMapPixelToDirection(
                            pixelX,
                            pixelZ,
                            0,
                            0,
                            resolution,
                            resolution,
                            out var currentMapMagicDirection) ||
                        !CubeSpherePatchGrid.TryGetSampleDirection(
                            patch,
                            resolution,
                            pixelX,
                            sampleY,
                            out var canonicalDirection))
                    {
                        Fail(
                            $"Could not compare MapMagic coordinates for {patch} ({pixelX}, {pixelZ}).");
                        return false;
                    }

                    RecordDirectionGap(
                        canonicalDirection,
                        mapMagicDirection,
                        radiusMeters);
                    RecordDirectionGap(
                        canonicalDirection,
                        currentMapMagicDirection,
                        radiusMeters);

                    var canonicalNoise =
                        EvaluateReferenceNoise(
                            canonicalDirection,
                            radiusMeters);
                    var mapMagicNoise =
                        EvaluateReferenceNoise(
                            currentMapMagicDirection,
                            radiusMeters);
                    largestNoiseDifference =
                        Math.Max(
                            largestNoiseDifference,
                            Math.Abs(
                                canonicalNoise -
                                mapMagicNoise));
                    testedSampleCount++;
                }
            }

            return true;
        }

        private bool ValidateFaceSeams(
            int resolution,
            double radiusMeters)
        {
            for (var faceIndex = 0;
                faceIndex < Faces.Length;
                faceIndex++)
            {
                var face =
                    Faces[faceIndex];

                for (var edgeIndex = 0;
                    edgeIndex < Edges.Length;
                    edgeIndex++)
                {
                    var edge =
                        Edges[edgeIndex];
                    var adjacentFace =
                        CubeSphereTopology.GetAdjacentFace(
                            face,
                            edge);

                    for (var index = 0;
                        index < resolution;
                        index++)
                    {
                        var edgeCoordinate =
                            -1.0 +
                            2.0 * index /
                            (resolution - 1);
                        var sourceAddress =
                            CreateEdgeAddress(
                                face,
                                edge,
                                edgeCoordinate);
                        var sourceDirection =
                            CubeSphereMapping.AddressToDirection(
                                sourceAddress);

                        if (!CubeSphereMapping.TryDirectionToFaceAddress(
                                sourceDirection,
                                adjacentFace,
                                0.0,
                                out var adjacentAddress))
                        {
                            Fail(
                                $"Could not map {face} {edge} to {adjacentFace}.");
                            return false;
                        }

                        RecordDirectionGap(
                            sourceDirection,
                            CubeSphereMapping.AddressToDirection(
                                adjacentAddress),
                            radiusMeters);
                        testedSampleCount++;
                    }
                }
            }

            return true;
        }

        private bool ValidateUniversePosition(
            int resolution,
            double radiusMeters)
        {
            var root =
                CubeSpherePatchAddress.Root(
                    CubeSphereFace.PositiveX);
            var bodyCenter =
                new UniversePosition(
                    7,
                    -2,
                    4,
                    1234.5,
                    -6789.0,
                    42.0);

            if (!CubeSpherePatchGrid.TryGetSampleUniversePosition(
                    root,
                    resolution,
                    resolution / 2,
                    resolution / 2,
                    radiusMeters,
                    250.0,
                    bodyCenter,
                    Quaternion.identity,
                    out var universePosition))
            {
                Fail(
                    "Could not create a canonical sample universe position.");
                return false;
            }

            var expected =
                bodyCenter;
            expected.AddLocalMeters(
                radiusMeters + 250.0,
                0.0,
                0.0);

            if (universePosition.CellX != expected.CellX ||
                universePosition.CellY != expected.CellY ||
                universePosition.CellZ != expected.CellZ ||
                Math.Abs(
                    universePosition.LocalXMeters -
                    expected.LocalXMeters) > 0.000001 ||
                Math.Abs(
                    universePosition.LocalYMeters -
                    expected.LocalYMeters) > 0.000001 ||
                Math.Abs(
                    universePosition.LocalZMeters -
                    expected.LocalZMeters) > 0.000001)
            {
                Fail(
                    "Canonical sample universe position did not preserve the body datum.");
                return false;
            }

            testedSampleCount++;
            return true;
        }

        private bool ValidatePatchMetadata(
            int resolution)
        {
            var elevations =
                new float[
                    resolution *
                    resolution];

            for (var y = 0;
                y < resolution;
                y++)
            {
                for (var x = 0;
                    x < resolution;
                    x++)
                {
                    elevations[
                        y * resolution +
                        x] =
                        x +
                        y * 2.0f -
                        100.0f;
                }
            }

            var data =
                new CelestialSurfacePatchData(
                    surfaceRuntime.CacheKey,
                    CubeSpherePatchAddress.Root(
                        CubeSphereFace.PositiveZ),
                    resolution,
                    elevations);
            var expectedMaximum =
                (resolution - 1) *
                    3.0f -
                100.0f;

            if (data.SampleCount !=
                    resolution * resolution ||
                data.MinimumElevationMeters !=
                    -100.0f ||
                data.MaximumElevationMeters !=
                    expectedMaximum ||
                data.GeometricErrorMeters >
                    0.000001f)
            {
                Fail(
                    "Surface patch metadata did not match its source samples.");
                return false;
            }

            testedSampleCount +=
                data.SampleCount;
            return true;
        }

        private double EvaluateReferenceNoise(
            DoubleVector3 direction,
            double radiusMeters)
        {
            return
                RoundMapMagicSphericalNoise.EvaluateNormalized(
                    direction,
                    radiusMeters,
                    surfaceRuntime.SurfaceDefinition.SurfaceSeed,
                    200000.0,
                    4,
                    0.5);
        }

        private void RecordDirectionGap(
            DoubleVector3 first,
            DoubleVector3 second,
            double radiusMeters)
        {
            var deltaX = first.x - second.x;
            var deltaY = first.y - second.y;
            var deltaZ = first.z - second.z;
            var gapMeters =
                Math.Sqrt(
                    deltaX * deltaX +
                    deltaY * deltaY +
                    deltaZ * deltaZ) *
                radiusMeters;

            largestDirectionGapMeters =
                Math.Max(
                    largestDirectionGapMeters,
                    gapMeters);
        }

        private static CubeSphereAddress CreateEdgeAddress(
            CubeSphereFace face,
            CubeSphereEdge edge,
            double edgeCoordinate)
        {
            switch (edge)
            {
                case CubeSphereEdge.NegativeU:
                    return new CubeSphereAddress(
                        face,
                        -1.0,
                        edgeCoordinate,
                        0.0);

                case CubeSphereEdge.PositiveU:
                    return new CubeSphereAddress(
                        face,
                        1.0,
                        edgeCoordinate,
                        0.0);

                case CubeSphereEdge.NegativeV:
                    return new CubeSphereAddress(
                        face,
                        edgeCoordinate,
                        -1.0,
                        0.0);

                case CubeSphereEdge.PositiveV:
                    return new CubeSphereAddress(
                        face,
                        edgeCoordinate,
                        1.0,
                        0.0);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(edge));
            }
        }

        private void Fail(
            string error)
        {
            passed = false;
            lastError = error;
        }
    }
}
