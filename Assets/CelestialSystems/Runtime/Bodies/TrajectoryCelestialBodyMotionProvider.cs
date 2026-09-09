/*
 * Drives a celestial body from an independent clock using an inertial state or a prescribed trajectory.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(-95)]
    [DisallowMultipleComponent]
    public sealed class TrajectoryCelestialBodyMotionProvider :
        MonoBehaviour,
        ICelestialBodyMotionProvider
    {
        [Header("Runtime")]
        [SerializeField]
        private bool isReady;

        [SerializeField]
        private bool hasMotionState;

        [SerializeField]
        private double evaluatedUniversalTimeSeconds;

        [SerializeField]
        private UniverseMotionState motionState;

        [SerializeField]
        private string lastError;

        private UniverseFrameController universeFrame;
        private ICelestialTimeSource timeSource;
        private Transform drivenTransform;
        private CelestialTrajectoryDefinition trajectory;
        private CelestialBodyRuntimeContext referenceBody;
        private double referenceMassKilograms;
        private double orbitingMassKilograms;
        private UniversePosition inertialPositionAtEpoch;
        private DoubleVector3 inertialVelocityMetersPerSecond;
        private double inertialEpochSeconds;
        private Quaternion rotation;
        private DoubleVector3 angularVelocityRadiansPerSecond;
        private bool usesTrajectory;

        public string ProviderName =>
            usesTrajectory
                ? "Prescribed Circular Trajectory"
                : "Prescribed Inertial State";

        public bool IsReady =>
            isReady;

        public string LastError =>
            lastError;

        private void LateUpdate()
        {
            if (TryRefreshMotionState())
            {
                ApplySceneTransform();
            }
        }

        public bool InitializeInertial(
            UniverseFrameController newUniverseFrame,
            ICelestialTimeSource newTimeSource,
            Transform newDrivenTransform,
            DoubleVector3 positionMetersFromFrameOrigin,
            DoubleVector3 velocityMetersPerSecond,
            Quaternion initialRotation,
            DoubleVector3 initialAngularVelocityRadiansPerSecond)
        {
            ResetState();

            if (!TryAssignSharedDependencies(
                    newUniverseFrame,
                    newTimeSource,
                    newDrivenTransform))
            {
                return false;
            }

            inertialPositionAtEpoch =
                universeFrame.FrameOrigin;
            inertialPositionAtEpoch.AddLocalMeters(
                positionMetersFromFrameOrigin.x,
                positionMetersFromFrameOrigin.y,
                positionMetersFromFrameOrigin.z);
            inertialVelocityMetersPerSecond =
                velocityMetersPerSecond;
            inertialEpochSeconds =
                timeSource.UniversalTimeSeconds;
            rotation =
                initialRotation;
            angularVelocityRadiansPerSecond =
                initialAngularVelocityRadiansPerSecond;
            usesTrajectory = false;
            isReady = true;
            return TryRefreshMotionState() &&
                ApplySceneTransform();
        }

        public bool InitializeTrajectory(
            UniverseFrameController newUniverseFrame,
            ICelestialTimeSource newTimeSource,
            Transform newDrivenTransform,
            CelestialTrajectoryDefinition newTrajectory,
            CelestialBodyRuntimeContext newReferenceBody,
            double newOrbitingMassKilograms,
            Quaternion initialRotation,
            DoubleVector3 initialAngularVelocityRadiansPerSecond)
        {
            ResetState();

            if (!TryAssignSharedDependencies(
                    newUniverseFrame,
                    newTimeSource,
                    newDrivenTransform))
            {
                return false;
            }

            if (newTrajectory == null ||
                !newTrajectory.TryValidate(
                    out var trajectoryError))
            {
                return Fail(
                    string.IsNullOrWhiteSpace(
                        trajectoryError)
                        ? "A trajectory motion provider requires a valid trajectory definition."
                        : trajectoryError);
            }

            if (newReferenceBody == null ||
                !IsFinitePositive(
                    newReferenceBody.ConfiguredMassKilograms))
            {
                return Fail(
                    "A trajectory motion provider requires an initialized reference body with positive mass.");
            }

            if (!IsFinitePositive(
                    newOrbitingMassKilograms))
            {
                return Fail(
                    "A trajectory motion provider requires a positive orbiting-body mass.");
            }

            trajectory =
                newTrajectory;
            referenceBody =
                newReferenceBody;
            referenceMassKilograms =
                referenceBody.ConfiguredMassKilograms;
            orbitingMassKilograms =
                newOrbitingMassKilograms;
            rotation =
                initialRotation;
            angularVelocityRadiansPerSecond =
                initialAngularVelocityRadiansPerSecond;
            usesTrajectory = true;
            isReady = true;
            return TryRefreshMotionState() &&
                ApplySceneTransform();
        }

        public bool TryGetMotionState(
            out UniverseMotionState currentMotionState)
        {
            if (!TryRefreshMotionState())
            {
                currentMotionState = default;
                return false;
            }

            currentMotionState =
                motionState;
            return true;
        }

        private bool TryAssignSharedDependencies(
            UniverseFrameController newUniverseFrame,
            ICelestialTimeSource newTimeSource,
            Transform newDrivenTransform)
        {
            if (newUniverseFrame == null)
            {
                return Fail(
                    "A trajectory motion provider requires a universe frame.");
            }

            if (newTimeSource == null)
            {
                return Fail(
                    "A trajectory motion provider requires a celestial time source.");
            }

            if (newDrivenTransform == null)
            {
                return Fail(
                    "A trajectory motion provider requires a transform to drive.");
            }

            universeFrame =
                newUniverseFrame;
            timeSource =
                newTimeSource;
            drivenTransform =
                newDrivenTransform;
            return true;
        }

        private bool TryRefreshMotionState()
        {
            if (!isReady ||
                universeFrame == null ||
                timeSource == null ||
                drivenTransform == null)
            {
                return false;
            }

            var universalTimeSeconds =
                timeSource.UniversalTimeSeconds;
            UniversePosition position;
            DoubleVector3 velocity;

            if (usesTrajectory)
            {
                if (referenceBody == null ||
                    !referenceBody.TryGetMotionState(
                        out var referenceState))
                {
                    return Fail(
                        "The trajectory reference body's motion state is unavailable.");
                }

                if (!CelestialTrajectoryEvaluator.TryEvaluateRelativeState(
                        trajectory,
                        referenceMassKilograms,
                        orbitingMassKilograms,
                        universalTimeSeconds,
                        out var relativePosition,
                        out var relativeVelocity,
                        out var trajectoryError))
                {
                    return Fail(
                        trajectoryError);
                }

                position =
                    referenceState.Position;
                position.AddLocalMeters(
                    relativePosition.x,
                    relativePosition.y,
                    relativePosition.z);
                velocity =
                    referenceState.LinearVelocityMetersPerSecond +
                    relativeVelocity;
            }
            else
            {
                var elapsedSeconds =
                    universalTimeSeconds -
                    inertialEpochSeconds;
                var elapsedPosition =
                    inertialVelocityMetersPerSecond *
                    elapsedSeconds;

                position =
                    inertialPositionAtEpoch;
                position.AddLocalMeters(
                    elapsedPosition.x,
                    elapsedPosition.y,
                    elapsedPosition.z);
                velocity =
                    inertialVelocityMetersPerSecond;
            }

            motionState =
                new UniverseMotionState(
                    position,
                    rotation,
                    velocity,
                    angularVelocityRadiansPerSecond);
            evaluatedUniversalTimeSeconds =
                universalTimeSeconds;
            hasMotionState = true;
            lastError = string.Empty;
            return true;
        }

        private bool ApplySceneTransform()
        {
            if (!motionState.Position.TryGetOffsetMetersFrom(
                    universeFrame.FrameOrigin,
                    out var frameOffsetMeters) ||
                !FitsFloat(
                    frameOffsetMeters))
            {
                return Fail(
                    "The prescribed trajectory position cannot be represented in the active scene frame.");
            }

            drivenTransform.position =
                new Vector3(
                    (float)frameOffsetMeters.x,
                    (float)frameOffsetMeters.y,
                    (float)frameOffsetMeters.z);
            drivenTransform.rotation =
                rotation;
            return true;
        }

        private void ResetState()
        {
            isReady = false;
            hasMotionState = false;
            evaluatedUniversalTimeSeconds = 0.0;
            motionState = default;
            lastError = string.Empty;
            universeFrame = null;
            timeSource = null;
            drivenTransform = null;
            trajectory = null;
            referenceBody = null;
            referenceMassKilograms = 0.0;
            orbitingMassKilograms = 0.0;
            inertialPositionAtEpoch = default;
            inertialVelocityMetersPerSecond = default;
            inertialEpochSeconds = 0.0;
            rotation = Quaternion.identity;
            angularVelocityRadiansPerSecond = default;
            usesTrajectory = false;
        }

        private bool Fail(
            string error)
        {
            isReady = false;
            hasMotionState = false;
            lastError =
                error;
            return false;
        }

        private static bool FitsFloat(
            DoubleVector3 value)
        {
            return
                IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z) &&
                System.Math.Abs(value.x) <=
                    float.MaxValue &&
                System.Math.Abs(value.y) <=
                    float.MaxValue &&
                System.Math.Abs(value.z) <=
                    float.MaxValue;
        }

        private static bool IsFinitePositive(
            double value)
        {
            return
                IsFinite(value) &&
                value > 0.0;
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
