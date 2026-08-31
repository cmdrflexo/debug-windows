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
