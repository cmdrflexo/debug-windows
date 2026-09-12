/*
 * Drives a celestial body from an independent clock using an inertial state or a prescribed trajectory.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    // Project body transforms only after the observation camera (order 50) has
    // applied its pose and synchronously snapped the SGT/universe origin.
    // TryGetMotionState evaluates on demand, so the camera can still query the
    // current simulation state before this component's LateUpdate runs.
    [DefaultExecutionOrder(100)]
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
        private ICelestialMotionStateSource referenceSource;
        private CelestialBodyDefinition bodyDefinition;
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
                ? (trajectory.Kind ==
                        CelestialTrajectoryKind.TwoBodyBarycentricComponent
                    ? "Prescribed Two-Body Barycentric Trajectory"
                    : trajectory.Kind == CelestialTrajectoryKind.KeplerianConic
                        ? "Prescribed Keplerian Conic Trajectory"
                        : "Prescribed Circular Trajectory")
                : "Prescribed Inertial State";

        public bool IsReady =>
            isReady;

        public string LastError =>
            lastError;

        public bool UsesTrajectory =>
            usesTrajectory;

        public CelestialTrajectoryDefinition Trajectory =>
            trajectory;

        public ICelestialMotionStateSource ReferenceSource =>
            referenceSource;

        public CelestialBodyRuntimeContext ReferenceBody =>
            referenceSource as
                CelestialBodyRuntimeContext;

        public bool TryEvaluateRelativeState(
            double universalTimeSeconds,
            out DoubleVector3 relativePositionMeters,
            out DoubleVector3 relativeVelocityMetersPerSecond)
        {
            relativePositionMeters = default;
            relativeVelocityMetersPerSecond = default;

            if (!isReady || !usesTrajectory || trajectory == null)
            {
                return false;
            }

            return CelestialTrajectoryEvaluator.TryEvaluateRelativeState(
                trajectory,
                referenceMassKilograms,
                orbitingMassKilograms,
                universalTimeSeconds,
                out relativePositionMeters,
                out relativeVelocityMetersPerSecond,
                out _);
        }

        public bool TryGetClosedOrbitPeriodSeconds(
            out double periodSeconds)
        {
            periodSeconds = 0.0;

            return isReady &&
                usesTrajectory &&
                trajectory != null &&
                CelestialTrajectoryEvaluator.TryCalculatePeriodSeconds(
                    trajectory,
                    referenceMassKilograms,
                    orbitingMassKilograms,
                    out periodSeconds,
                    out _);
        }

        public bool TryGetConicApsisStates(
            out DoubleVector3 periapsisPositionMeters,
            out DoubleVector3 apoapsisPositionMeters,
            out bool hasApoapsis)
        {
            periapsisPositionMeters = default;
            apoapsisPositionMeters = default;
            hasApoapsis = false;

            if (!isReady ||
                !usesTrajectory ||
                (trajectory?.Kind != CelestialTrajectoryKind.KeplerianConic &&
                    trajectory?.Kind != CelestialTrajectoryKind.TwoBodyBarycentricComponent) ||
                trajectory.Conic == null)
            {
                return false;
            }

            var conic = trajectory.Conic;
            var gravitatingMassKilograms =
                trajectory.Kind ==
                    CelestialTrajectoryKind.TwoBodyBarycentricComponent
                        ? trajectory.TwoBodyOrbit.TotalMassKilograms
                        : referenceMassKilograms +
                            orbitingMassKilograms;
            var meanMotion =
                System.Math.Sqrt(
                    CelestialTrajectoryEvaluator.GravitationalConstant *
                    gravitatingMassKilograms /
                    System.Math.Pow(System.Math.Abs(conic.SemiMajorAxisMeters), 3.0));
            var direction = (double)conic.Direction;
            var meanAtEpochRadians =
                conic.MeanAnomalyAtEpochDegrees *
                System.Math.PI /
                180.0;
            var periapsisTime =
                conic.EpochUniversalTimeSeconds -
                meanAtEpochRadians /
                (direction * meanMotion);

            if (!TryEvaluateRelativeState(
                    periapsisTime,
                    out periapsisPositionMeters,
                    out _))
            {
                return false;
            }

            if (!conic.IsClosed ||
                !TryGetClosedOrbitPeriodSeconds(
                    out var periodSeconds) ||
                !TryEvaluateRelativeState(
                    periapsisTime +
                        periodSeconds * 0.5,
                    out apoapsisPositionMeters,
                    out _))
            {
                return true;
            }

            hasApoapsis = true;
            return true;
        }

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
            return InitializeInertial(
                newUniverseFrame,
                newTimeSource,
                newDrivenTransform,
                positionMetersFromFrameOrigin,
                velocityMetersPerSecond,
                initialRotation,
                initialAngularVelocityRadiansPerSecond,
                null);
        }

        public bool InitializeInertial(
            UniverseFrameController newUniverseFrame,
            ICelestialTimeSource newTimeSource,
            Transform newDrivenTransform,
            DoubleVector3 positionMetersFromFrameOrigin,
            DoubleVector3 velocityMetersPerSecond,
            Quaternion initialRotation,
            DoubleVector3 initialAngularVelocityRadiansPerSecond,
            CelestialBodyDefinition newBodyDefinition)
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
            bodyDefinition =
                newBodyDefinition;
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
            ICelestialMotionStateSource newReferenceSource,
            double newOrbitingMassKilograms,
            Quaternion initialRotation,
            DoubleVector3 initialAngularVelocityRadiansPerSecond)
        {
            return InitializeTrajectory(
                newUniverseFrame,
                newTimeSource,
                newDrivenTransform,
                newTrajectory,
                newReferenceSource,
                newOrbitingMassKilograms,
                initialRotation,
                initialAngularVelocityRadiansPerSecond,
                null);
        }

        public bool InitializeTrajectory(
            UniverseFrameController newUniverseFrame,
            ICelestialTimeSource newTimeSource,
            Transform newDrivenTransform,
            CelestialTrajectoryDefinition newTrajectory,
            ICelestialMotionStateSource newReferenceSource,
            double newOrbitingMassKilograms,
            Quaternion initialRotation,
            DoubleVector3 initialAngularVelocityRadiansPerSecond,
            CelestialBodyDefinition newBodyDefinition)
        {
            ResetState();

            if (!TryAssignSharedDependencies(
                    newUniverseFrame,
                    newTimeSource,
                    newDrivenTransform))
            {
                return false;
            }

            var trajectoryError =
                string.Empty;

            if (newTrajectory == null ||
                !newTrajectory.TryValidate(
                    out trajectoryError))
            {
                return Fail(
                    string.IsNullOrWhiteSpace(
                        trajectoryError)
                        ? "A trajectory motion provider requires a valid trajectory definition."
                        : trajectoryError);
            }

            if (newReferenceSource == null ||
                !IsFinitePositive(
                    newReferenceSource.ConfiguredMassKilograms))
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
            referenceSource =
                newReferenceSource;
            referenceMassKilograms =
                referenceSource.ConfiguredMassKilograms;
            orbitingMassKilograms =
                newOrbitingMassKilograms;
            rotation =
                initialRotation;
            angularVelocityRadiansPerSecond =
                initialAngularVelocityRadiansPerSecond;
            bodyDefinition =
                newBodyDefinition;
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
            var relativePosition =
                new DoubleVector3();
            var relativeVelocity =
                new DoubleVector3();

            if (usesTrajectory)
            {
                if (referenceSource == null ||
                    !referenceSource.TryGetMotionState(
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
                        out relativePosition,
                        out relativeVelocity,
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

            EvaluateGeneratedRotation(
                universalTimeSeconds,
                relativePosition,
                relativeVelocity);

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

        private void EvaluateGeneratedRotation(
            double universalTimeSeconds,
            DoubleVector3 relativePosition,
            DoubleVector3 relativeVelocity)
        {
            if (bodyDefinition == null ||
                !bodyDefinition.HasRotationProperties)
            {
                return;
            }

            if (bodyDefinition.IsSpinOrbitSynchronous &&
                usesTrajectory &&
                TryNormalize(
                    relativePosition *
                        -1.0,
                    out var towardPrimary) &&
                TryNormalize(
                    Cross(
                        relativePosition,
                        relativeVelocity),
                    out var orbitNormal))
            {
                var tangent =
                    Vector3.Cross(
                        orbitNormal,
                        towardPrimary).normalized;
                var synchronousSpinAxis =
                    Quaternion.AngleAxis(
                        (float)bodyDefinition.AxialTiltDegrees,
                        tangent) *
                    orbitNormal;
                var primeDirection =
                    Vector3.ProjectOnPlane(
                        towardPrimary,
                        synchronousSpinAxis).normalized;

                rotation =
                    MapLocalAxes(
                        bodyDefinition.NorthAxis,
                        bodyDefinition.PoleReferenceAxis,
                        synchronousSpinAxis,
                        primeDirection);
                var radiusSquared =
                    relativePosition.x * relativePosition.x +
                    relativePosition.y * relativePosition.y +
                    relativePosition.z * relativePosition.z;
                var orbitalAngularVelocity =
                    Cross(
                        relativePosition,
                        relativeVelocity) /
                    radiusSquared;
                angularVelocityRadiansPerSecond =
                    orbitalAngularVelocity;
                return;
            }

            var baseNorth =
                Vector3.up;

            if (usesTrajectory &&
                TryNormalize(
                    Cross(
                        relativePosition,
                        relativeVelocity),
                    out var evaluatedOrbitNormal))
            {
                baseNorth =
                    evaluatedOrbitNormal;
            }

            var seedAngle =
                PositiveModulo(
                    bodyDefinition.GenerationSeed *
                        0.6180339887498949 *
                        360.0,
                    360.0);
            var tiltAxis =
                Quaternion.AngleAxis(
                    (float)seedAngle,
                    baseNorth) *
                Vector3.forward;

            if (Math.Abs(
                    Vector3.Dot(
                        tiltAxis.normalized,
                        baseNorth)) >
                0.99f)
            {
                tiltAxis =
                    Vector3.right;
            }

            tiltAxis =
                Vector3.ProjectOnPlane(
                    tiltAxis,
                    baseNorth).normalized;
            var spinAxis =
                Quaternion.AngleAxis(
                    (float)bodyDefinition.AxialTiltDegrees,
                    tiltAxis) *
                baseNorth;
            var referenceDirection =
                Vector3.ProjectOnPlane(
                    tiltAxis,
                    spinAxis).normalized;
            var baseRotation =
                MapLocalAxes(
                    bodyDefinition.NorthAxis,
                    bodyDefinition.PoleReferenceAxis,
                    spinAxis,
                    referenceDirection);
            var direction =
                bodyDefinition.SpinDirection ==
                    CelestialSpinDirection.Retrograde
                        ? -1.0
                        : 1.0;
            var periodSeconds =
                bodyDefinition.RotationPeriodHours *
                3600.0;
            var phaseDegrees =
                PositiveModulo(
                    seedAngle +
                    direction *
                    universalTimeSeconds /
                    periodSeconds *
                    360.0,
                    360.0);

            rotation =
                Quaternion.AngleAxis(
                    (float)phaseDegrees,
                    spinAxis) *
                baseRotation;
            var angularSpeed =
                direction *
                2.0 *
                Math.PI /
                periodSeconds;
            angularVelocityRadiansPerSecond =
                new DoubleVector3(
                    spinAxis.x * angularSpeed,
                    spinAxis.y * angularSpeed,
                    spinAxis.z * angularSpeed);
        }

        private static Quaternion MapLocalAxes(
            Vector3 localNorth,
            Vector3 localReference,
            Vector3 worldNorth,
            Vector3 worldReference)
        {
            var normalizedLocalNorth =
                localNorth.normalized;
            var normalizedWorldNorth =
                worldNorth.normalized;
            var localForward =
                Vector3.ProjectOnPlane(
                    localReference,
                    normalizedLocalNorth).normalized;
            var worldForward =
                Vector3.ProjectOnPlane(
                    worldReference,
                    normalizedWorldNorth).normalized;

            if (localForward.sqrMagnitude < 0.000001f)
            {
                localForward =
                    Vector3.forward;
            }

            if (worldForward.sqrMagnitude < 0.000001f)
            {
                worldForward =
                    Vector3.forward;
            }

            return
                Quaternion.LookRotation(
                    worldForward,
                    normalizedWorldNorth) *
                Quaternion.Inverse(
                    Quaternion.LookRotation(
                        localForward,
                        normalizedLocalNorth));
        }

        private static DoubleVector3 Cross(
            DoubleVector3 left,
            DoubleVector3 right)
        {
            return
                new DoubleVector3(
                    left.y * right.z -
                        left.z * right.y,
                    left.z * right.x -
                        left.x * right.z,
                    left.x * right.y -
                        left.y * right.x);
        }

        private static bool TryNormalize(
            DoubleVector3 value,
            out Vector3 normalized)
        {
            var maximum =
                Math.Max(
                    Math.Abs(value.x),
                    Math.Max(
                        Math.Abs(value.y),
                        Math.Abs(value.z)));

            if (!IsFinite(maximum) ||
                maximum <= 0.0)
            {
                normalized = default;
                return false;
            }

            var scaled =
                new Vector3(
                    (float)(value.x / maximum),
                    (float)(value.y / maximum),
                    (float)(value.z / maximum));
            normalized =
                scaled.normalized;
            return normalized.sqrMagnitude >
                0.999f;
        }

        private static double PositiveModulo(
            double value,
            double modulus)
        {
            var remainder =
                value %
                modulus;
            return
                remainder < 0.0
                    ? remainder + modulus
                    : remainder;
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
            referenceSource = null;
            bodyDefinition = null;
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
