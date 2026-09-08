/*
 * Numerically verifies that cube-face edge coordinates reconstruct the same direction and spherical noise value.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class RoundMapMagicSphericalSeamProbe :
        MonoBehaviour
    {
        [Header("Noise Test")]
        [SerializeField]
        private double planetRadiusMeters =
            6371000.0;

        [SerializeField]
        private int surfaceSeed = 12345;

        [SerializeField]
        private double featureSizeMeters =
            200000.0;

        [SerializeField]
        [Range(1, 12)]
        private int octaves = 5;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float persistence = 0.5f;

        [Header("Validation")]
        [SerializeField]
        [Range(2, 257)]
        private int samplesPerEdge = 65;

        [SerializeField]
        private bool validateOnStart = true;

        [SerializeField]
        private double maximumAllowedDirectionGapMeters =
            0.001;

        [SerializeField]
        private double maximumAllowedNoiseDifference =
            0.000001;

        [Header("Runtime Result")]
        [SerializeField]
        private bool hasValidationResult;

        [SerializeField]
        private bool passed;

        [SerializeField]
        private int testedSampleCount;

        [SerializeField]
        private double maximumDirectionGapMeters;

        [SerializeField]
        private double maximumNoiseDifference;

        [SerializeField]
        private string lastError;

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

        private void Start()
        {
            if (validateOnStart)
            {
                ValidateSeams();
            }
        }

        [ContextMenu("Validate Cube-Sphere Seams")]
        public void ValidateSeams()
        {
            hasValidationResult = true;
            passed = false;
            testedSampleCount = 0;
            maximumDirectionGapMeters = 0.0;
            maximumNoiseDifference = 0.0;
            lastError = string.Empty;

            if (!IsFinite(planetRadiusMeters) ||
                planetRadiusMeters <= 0.0 ||
                !IsFinite(featureSizeMeters) ||
                featureSizeMeters <= 0.0)
            {
                lastError =
                    "Planet radius and feature size must be positive finite values.";
                return;
            }

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

                    for (var sampleIndex = 0;
                        sampleIndex < samplesPerEdge;
                        sampleIndex++)
                    {
                        var edgePosition =
                            samplesPerEdge > 1
                                ? sampleIndex /
                                    (double)(
                                        samplesPerEdge -
                                        1) *
                                    2.0 -
                                    1.0
                                : 0.0;
                        var sourceAddress =
                            CreateEdgeAddress(
                                face,
                                edge,
                                edgePosition);
                        var sourceDirection =
                            CubeSphereMapping.AddressToDirection(
                                sourceAddress);

                        if (!CubeSphereMapping.TryDirectionToFaceAddress(
                                sourceDirection,
                                adjacentFace,
                                0.0,
                                out var adjacentAddress))
                        {
                            lastError =
                                $"Could not map {face} {edge} sample {sampleIndex} to {adjacentFace}.";
                            return;
                        }

                        var adjacentDirection =
                            CubeSphereMapping.AddressToDirection(
                                adjacentAddress);
                        var directionGapMeters =
                            DirectionGapMeters(
                                sourceDirection,
                                adjacentDirection,
                                planetRadiusMeters);
                        var sourceNoise =
                            RoundMapMagicSphericalNoise.EvaluateNormalized(
                                sourceDirection,
                                planetRadiusMeters,
                                surfaceSeed,
                                featureSizeMeters,
                                octaves,
                                persistence);
                        var adjacentNoise =
                            RoundMapMagicSphericalNoise.EvaluateNormalized(
                                adjacentDirection,
                                planetRadiusMeters,
                                surfaceSeed,
                                featureSizeMeters,
                                octaves,
                                persistence);

                        maximumDirectionGapMeters =
                            Math.Max(
                                maximumDirectionGapMeters,
                                directionGapMeters);
                        maximumNoiseDifference =
                            Math.Max(
                                maximumNoiseDifference,
                                Math.Abs(
                                    sourceNoise -
                                    adjacentNoise));
                        testedSampleCount++;
                    }
                }
            }

            passed =
                maximumDirectionGapMeters <=
                    maximumAllowedDirectionGapMeters &&
                maximumNoiseDifference <=
                    maximumAllowedNoiseDifference;

            if (!passed)
            {
                lastError =
                    "At least one shared face edge exceeded its validation tolerance.";
            }
        }

        private static CubeSphereAddress CreateEdgeAddress(
            CubeSphereFace face,
            CubeSphereEdge edge,
            double edgePosition)
        {
            switch (edge)
            {
                case CubeSphereEdge.NegativeU:
                    return
                        new CubeSphereAddress(
                            face,
                            -1.0,
                            edgePosition,
                            0.0);

                case CubeSphereEdge.PositiveU:
                    return
                        new CubeSphereAddress(
                            face,
                            1.0,
                            edgePosition,
                            0.0);

                case CubeSphereEdge.NegativeV:
                    return
                        new CubeSphereAddress(
                            face,
                            edgePosition,
                            -1.0,
                            0.0);

                case CubeSphereEdge.PositiveV:
                    return
                        new CubeSphereAddress(
                            face,
                            edgePosition,
                            1.0,
                            0.0);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(edge),
                        edge,
                        "Unknown cube-sphere edge.");
            }
        }

        private static double DirectionGapMeters(
            DoubleVector3 first,
            DoubleVector3 second,
            double radiusMeters)
        {
            var deltaX =
                first.x -
                second.x;
            var deltaY =
                first.y -
                second.y;
            var deltaZ =
                first.z -
                second.z;

            return
                Math.Sqrt(
                    deltaX * deltaX +
                    deltaY * deltaY +
                    deltaZ * deltaZ) *
                radiusMeters;
        }

        private static bool IsFinite(double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
