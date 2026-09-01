/*
 * Verifies cube-sphere direction and position round trips across face interiors, edges, and corners.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CubeSphereMappingDiagnostics : MonoBehaviour
    {
        private static readonly double[] TestCoordinates =
        {
            -1.0,
            -0.75,
            -0.5,
            0.0,
            0.5,
            0.75,
            1.0
        };

        [SerializeField]
        private bool runOnStart;

        [SerializeField]
        private double planetRadiusMeters = 6371000.0;

        [SerializeField]
        private double maximumDirectionError = 0.000000000001;

        [SerializeField]
        private double maximumPositionErrorMeters = 0.000001;

        private void Start()
        {
            if (runOnStart)
            {
                RunDiagnostics();
            }
        }

        [ContextMenu("Run Cube-Sphere Diagnostics")]
        public void RunDiagnostics()
        {
            if (planetRadiusMeters <= 0.0)
            {
                Debug.LogError(
                    "Cube-sphere diagnostics require a positive planet radius.",
                    this);
                return;
            }

            var sampleCount = 0;
            var largestDirectionError = 0.0;
            var largestPositionErrorMeters = 0.0;

            foreach (CubeSphereFace face in
                Enum.GetValues(typeof(CubeSphereFace)))
            {
                foreach (var faceU in TestCoordinates)
                {
                    foreach (var faceV in TestCoordinates)
                    {
                        var address = new CubeSphereAddress(
                            face,
                            faceU,
                            faceV,
                            123.456);
                        var direction =
                            CubeSphereMapping.AddressToDirection(
                                address);

                        if (!CubeSphereMapping.TryDirectionToAddress(
                                direction,
                                address.AltitudeMeters,
                                out var restoredAddress))
                        {
                            LogFailure(
                                $"Direction conversion failed for {address}.");
                            return;
                        }

                        var restoredDirection =
                            CubeSphereMapping.AddressToDirection(
                                restoredAddress);
                        var directionError =
                            Distance(
                                direction,
                                restoredDirection);

                        var position =
                            CubeSphereMapping.AddressToPlanetRelativePosition(
                                address,
                                planetRadiusMeters);

                        if (!CubeSphereMapping.TryPlanetRelativePositionToAddress(
                                position,
                                planetRadiusMeters,
                                out var restoredPositionAddress))
                        {
                            LogFailure(
                                $"Position conversion failed for {address}.");
                            return;
                        }

                        var restoredPosition =
                            CubeSphereMapping.AddressToPlanetRelativePosition(
                                restoredPositionAddress,
                                planetRadiusMeters);
                        var positionErrorMeters =
                            Distance(
                                position,
                                restoredPosition);

                        largestDirectionError = Math.Max(
                            largestDirectionError,
                            directionError);
                        largestPositionErrorMeters = Math.Max(
                            largestPositionErrorMeters,
                            positionErrorMeters);
                        sampleCount++;
                    }
                }
            }

            var expectedCenterEdgeDistanceMeters =
                CubeSphereMapping.FaceCoordinateToMeters(
                    1.0,
                    planetRadiusMeters);

            foreach (CubeSphereFace face in
                Enum.GetValues(typeof(CubeSphereFace)))
            {
                var centerProximity =
                    CubeSphereMapping.GetFaceProximity(
                        new CubeSphereAddress(
                            face,
                            0.0,
                            0.0,
                            0.0),
                        planetRadiusMeters);

                foreach (CubeSphereEdge edge in
                    Enum.GetValues(typeof(CubeSphereEdge)))
                {
                    if (Math.Abs(
                            centerProximity.GetEdgeDistanceMeters(
                                edge) -
                            expectedCenterEdgeDistanceMeters) >
                        maximumPositionErrorMeters)
                    {
                        LogFailure(
                            $"Face-center edge distance was incorrect for {face}, {edge}.");
                        return;
                    }

                    var edgeAddress =
                        CreateEdgeAddress(
                            face,
                            edge);
                    var edgeProximity =
                        CubeSphereMapping.GetFaceProximity(
                            edgeAddress,
                            planetRadiusMeters);

                    if (edgeProximity.GetEdgeDistanceMeters(
                            edge) >
                            maximumPositionErrorMeters ||
                        edgeProximity.ClosestEdge != edge)
                    {
                        LogFailure(
                            $"Edge proximity was incorrect for {face}, {edge}.");
                        return;
                    }

                    var adjacentFace =
                        CubeSphereTopology.GetAdjacentFace(
                            face,
                            edge);

                    if (adjacentFace == face)
                    {
                        LogFailure(
                            $"Edge topology returned the same face for {face}, {edge}.");
                        return;
                    }

                    if (!CubeSphereMapping.TryAddressToFaceAddress(
                            edgeAddress,
                            adjacentFace,
                            out var adjacentEdgeAddress) ||
                        !adjacentEdgeAddress.IsInsideFace)
                    {
                        LogFailure(
                            $"Shared edge could not be represented by {adjacentFace} for {face}, {edge}.");
                        return;
                    }

                    var adjacentEdgeDirection =
                        CubeSphereMapping.AddressToDirection(
                            adjacentEdgeAddress);
                    largestDirectionError = Math.Max(
                        largestDirectionError,
                        Distance(
                            CubeSphereMapping.AddressToDirection(
                                edgeAddress),
                            adjacentEdgeDirection));

                    var nearEdgeAddress =
                        CreateEdgeAddress(
                            face,
                            edge,
                            0.99);

                    if (!CubeSphereMapping.TryAddressToFaceAddress(
                            nearEdgeAddress,
                            adjacentFace,
                            out var adjacentPreviewAddress) ||
                        adjacentPreviewAddress.IsInsideFace)
                    {
                        LogFailure(
                            $"Adjacent preview coordinates were incorrect for {face}, {edge}.");
                        return;
                    }

                    largestDirectionError = Math.Max(
                        largestDirectionError,
                        Distance(
                            CubeSphereMapping.AddressToDirection(
                                nearEdgeAddress),
                            CubeSphereMapping.AddressToDirection(
                                adjacentPreviewAddress)));

                    sampleCount += 3;
                }

                var cornerProximity =
                    CubeSphereMapping.GetFaceProximity(
                        new CubeSphereAddress(
                            face,
                            -1.0,
                            -1.0,
                            0.0),
                        planetRadiusMeters);

                if (!cornerProximity.IsNearCorner(
                        maximumPositionErrorMeters))
                {
                    LogFailure(
                        $"Corner proximity was incorrect for {face}.");
                    return;
                }

                sampleCount++;
            }

            if (CubeSphereMapping.TryDirectionToAddress(
                    default,
                    0.0,
                    out _))
            {
                LogFailure(
                    "A zero direction was accepted as a valid address.");
                return;
            }

            if (largestDirectionError >
                    maximumDirectionError ||
                largestPositionErrorMeters >
                    maximumPositionErrorMeters)
            {
                LogFailure(
                    $"Maximum errors exceeded tolerance. Direction: {largestDirectionError:E6}, Position: {largestPositionErrorMeters:E6} m.");
                return;
            }

            Debug.Log(
                $"Cube-sphere diagnostics passed | Samples: {sampleCount} | Direction error: {largestDirectionError:E6} | Position error: {largestPositionErrorMeters:E6} m.",
                this);
        }

        private static CubeSphereAddress CreateEdgeAddress(
            CubeSphereFace face,
            CubeSphereEdge edge)
        {
            return CreateEdgeAddress(
                face,
                edge,
                1.0);
        }

        private static CubeSphereAddress CreateEdgeAddress(
            CubeSphereFace face,
            CubeSphereEdge edge,
            double coordinateMagnitude)
        {
            switch (edge)
            {
                case CubeSphereEdge.NegativeU:
                    return new CubeSphereAddress(
                        face,
                        -coordinateMagnitude,
                        0.0,
                        0.0);

                case CubeSphereEdge.PositiveU:
                    return new CubeSphereAddress(
                        face,
                        coordinateMagnitude,
                        0.0,
                        0.0);

                case CubeSphereEdge.NegativeV:
                    return new CubeSphereAddress(
                        face,
                        0.0,
                        -coordinateMagnitude,
                        0.0);

                case CubeSphereEdge.PositiveV:
                    return new CubeSphereAddress(
                        face,
                        0.0,
                        coordinateMagnitude,
                        0.0);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(edge),
                        edge,
                        "Unknown cube-sphere edge.");
            }
        }

        private void LogFailure(string message)
        {
            Debug.LogError(
                $"Cube-sphere diagnostics failed | {message}",
                this);
        }

        private static double Distance(
            DoubleVector3 first,
            DoubleVector3 second)
        {
            var deltaX = first.x - second.x;
            var deltaY = first.y - second.y;
            var deltaZ = first.z - second.z;

            return Math.Sqrt(
                deltaX * deltaX +
                deltaY * deltaY +
                deltaZ * deltaZ);
        }
    }
}
