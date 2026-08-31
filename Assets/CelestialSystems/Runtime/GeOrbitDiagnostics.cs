/*
 * Samples Gravity Engine bodies in double precision and reports their Earth-centered orbital-radius stability.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class GeOrbitDiagnostics : MonoBehaviour
    {
        [Header("Bodies")]
        [SerializeField]
        private NBody centerBody;

        [SerializeField]
        private NBody onRailsBody;

        [SerializeField]
        private NBody freeSimulationBody;

        [Header("Sampling")]
        [SerializeField]
        private double expectedRadiusMeters = 6771000.0;

        [SerializeField]
        [Min(0.1f)]
        private float sampleIntervalSeconds = 5.0f;

        [SerializeField]
        private bool logEachSample = true;

        private GravityEngine gravityEngine;
        private double nextSampleTime;
        private double maximumOnRailsErrorMeters;
        private double maximumFreeSimulationErrorMeters;
        private double maximumRadiusDifferenceMeters;
        private bool hasSample;
        private bool missingReferenceReported;

        private void LateUpdate()
        {
            var currentTime = Time.unscaledTimeAsDouble;

            if (currentTime < nextSampleTime)
            {
                return;
            }

            gravityEngine ??= GravityEngine.Instance();

            if (gravityEngine == null || !gravityEngine.IsSetup())
            {
                return;
            }

            if (centerBody == null || onRailsBody == null || freeSimulationBody == null)
            {
                if (!missingReferenceReported)
                {
                    Debug.LogError("Orbit diagnostics requires a center body, an on-rails body, and a free-simulation body.", this);
                    missingReferenceReported = true;
                }

                return;
            }

            nextSampleTime = currentTime + sampleIntervalSeconds;
            Sample();
        }

        private void OnDisable()
        {
            if (!hasSample)
            {
                return;
            }

            Debug.Log(
                $"Orbit diagnostic maximums | " +
                $"On rails error: {maximumOnRailsErrorMeters:N3} m | " +
                $"Free simulation error: {maximumFreeSimulationErrorMeters:N3} m | " +
                $"Radius difference: {maximumRadiusDifferenceMeters:N3} m.",
                this);
        }

        private void Sample()
        {
            var physicalScale = gravityEngine.GetPhysicalScale();
            var centerPosition = gravityEngine.GetPositionDoubleV3(centerBody);
            var onRailsPosition = gravityEngine.GetPositionDoubleV3(onRailsBody);
            var freeSimulationPosition = gravityEngine.GetPositionDoubleV3(freeSimulationBody);

            var onRailsRadiusMeters = Distance(centerPosition, onRailsPosition) * physicalScale;
            var freeSimulationRadiusMeters = Distance(centerPosition, freeSimulationPosition) * physicalScale;
            var onRailsErrorMeters = onRailsRadiusMeters - expectedRadiusMeters;
            var freeSimulationErrorMeters = freeSimulationRadiusMeters - expectedRadiusMeters;
            var radiusDifferenceMeters = freeSimulationRadiusMeters - onRailsRadiusMeters;

            maximumOnRailsErrorMeters = Math.Max(maximumOnRailsErrorMeters, Math.Abs(onRailsErrorMeters));
            maximumFreeSimulationErrorMeters = Math.Max(maximumFreeSimulationErrorMeters, Math.Abs(freeSimulationErrorMeters));
            maximumRadiusDifferenceMeters = Math.Max(maximumRadiusDifferenceMeters, Math.Abs(radiusDifferenceMeters));
            hasSample = true;

            if (logEachSample)
            {
                Debug.Log(
                    $"Orbit radii | " +
                    $"On rails: {onRailsRadiusMeters:N3} m ({onRailsErrorMeters:+0.000;-0.000;0.000}) | " +
                    $"Free simulation: {freeSimulationRadiusMeters:N3} m ({freeSimulationErrorMeters:+0.000;-0.000;0.000}) | " +
                    $"Difference: {radiusDifferenceMeters:+0.000;-0.000;0.000} m.",
                    this);
            }
        }

        private static double Distance(Vector3d a, Vector3d b)
        {
            var x = a.x - b.x;
            var y = a.y - b.y;
            var z = a.z - b.z;

            return Math.Sqrt(x * x + y * y + z * z);
        }
    }
}
