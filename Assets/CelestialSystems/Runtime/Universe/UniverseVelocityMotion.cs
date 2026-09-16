/*
 * Projects a universe-space kinematic motion state through an SGT floating
 * object. It can optionally inherit a body's velocity without becoming
 * position-locked to that body.
 */

using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SgtFloatingObject))]
    public sealed class UniverseVelocityMotion :
        MonoBehaviour,
        ICelestialBodyMotionProvider
    {
        [SerializeField]
        private SgtFloatingObject floatingObject;

        [Header("Initial State")]
        [SerializeField]
        private UniverseMotionState initialMotionState;

        [SerializeField]
        private double initialUniversalTimeSeconds;

        [Header("Velocity Reference")]
        [SerializeField]
        [Tooltip("The body whose live linear velocity may be inherited.")]
        private CelestialBodyRuntimeContext velocityReferenceBody;

        [SerializeField]
        [Tooltip("Samples and smoothly follows the reference body's linear velocity.")]
        private bool matchVelocityWithReferenceBody;

        [SerializeField]
        [Min(0.0f)]
        [Tooltip("Real-time seconds between reference velocity samples. Zero samples every evaluation.")]
        private float velocityReferenceUpdateIntervalSeconds =
            1.0f;

        [SerializeField]
        [Min(0.0f)]
        [Tooltip("Real-time seconds used to blend toward each sampled reference velocity. Zero changes immediately.")]
        private float velocityReferenceBlendSeconds =
            0.5f;

        [Header("Runtime State")]
        [SerializeField]
        private UniverseMotionState currentMotionState;

        [SerializeField]
        private double lastEvaluatedUniversalTimeSeconds;

        [SerializeField]
        private DoubleVector3 targetReferenceVelocity;

        [SerializeField]
        private double lastVelocityReferenceSampleRealtimeSeconds;

        [SerializeField]
        private double lastVelocityBlendRealtimeSeconds;

        [SerializeField]
        private bool hasTargetReferenceVelocity;

        [SerializeField]
        private bool initialized;

        [SerializeField]
        private bool applyRotation;

        [SerializeField]
        private string lastError;

        public string ProviderName =>
            nameof(UniverseVelocityMotion);

        public bool IsReady =>
            initialized;

        public string LastError =>
            lastError;

        public CelestialBodyRuntimeContext VelocityReferenceBody =>
            velocityReferenceBody;

        public bool MatchVelocityWithReferenceBody =>
            matchVelocityWithReferenceBody;

        public bool Initialize(
            UniverseMotionState motionState)
        {
            var timeController =
                CelestialTimeController.Instance;

            if (timeController == null)
            {
                return Fail(
                    "Universe velocity motion requires an active Celestial Time Controller.");
            }

            floatingObject ??=
                GetComponent<SgtFloatingObject>();

            if (floatingObject == null)
            {
                return Fail(
                    "Universe velocity motion requires an SGT floating object.");
            }

            initialMotionState =
                motionState;
            initialUniversalTimeSeconds =
                timeController.UniversalTimeSeconds;
            currentMotionState =
                motionState;
            lastEvaluatedUniversalTimeSeconds =
                initialUniversalTimeSeconds;
            ResetVelocityReferenceRuntimeState();
            initialized = true;
            lastError = string.Empty;
            ApplyCurrentState();
            return true;
        }

        public void SetVelocityReference(
            CelestialBodyRuntimeContext referenceBody,
            bool matchVelocity)
        {
            velocityReferenceBody =
                referenceBody;
            matchVelocityWithReferenceBody =
                matchVelocity && referenceBody != null;
            ResetVelocityReferenceRuntimeState();
        }

        public bool TryGetMotionState(
            out UniverseMotionState motionState)
        {
            if (!initialized)
            {
                motionState = default;
                return false;
            }

            return TryEvaluateCurrentState(
                out motionState);
        }

        public bool Stop()
        {
            if (!TryGetMotionState(
                    out var currentState))
            {
                return false;
            }

            matchVelocityWithReferenceBody = false;
            ResetVelocityReferenceRuntimeState();
            return Initialize(
                new UniverseMotionState(
                    currentState.Position,
                    currentState.Rotation,
                    DoubleVector3.zero,
                    currentState.AngularVelocityRadiansPerSecond));
        }

        private void Reset()
        {
            floatingObject =
                GetComponent<SgtFloatingObject>();
        }

        private void OnValidate()
        {
            velocityReferenceUpdateIntervalSeconds =
                Mathf.Max(
                    0.0f,
                    velocityReferenceUpdateIntervalSeconds);
            velocityReferenceBlendSeconds =
                Mathf.Max(
                    0.0f,
                    velocityReferenceBlendSeconds);
        }

        private void LateUpdate()
        {
            if (initialized)
            {
                ApplyCurrentState();
            }
        }

        private bool TryEvaluateCurrentState(
            out UniverseMotionState motionState)
        {
            var timeController =
                CelestialTimeController.Instance;

            if (timeController == null)
            {
                motionState = default;
                return false;
            }

            var universalTimeSeconds =
                timeController.UniversalTimeSeconds;
            var elapsedUniverseSeconds =
                universalTimeSeconds -
                lastEvaluatedUniversalTimeSeconds;
            var velocity =
                currentMotionState
                    .LinearVelocityMetersPerSecond;
            var position =
                currentMotionState.Position;

            // Integrate the time since the previous state with the velocity
            // that was already active. A fresh reference sample changes only
            // the next segment of travel.
            position.AddLocalMeters(
                velocity.x * elapsedUniverseSeconds,
                velocity.y * elapsedUniverseSeconds,
                velocity.z * elapsedUniverseSeconds);

            UpdateReferenceVelocity(
                ref velocity);

            currentMotionState =
                new UniverseMotionState(
                    position,
                    currentMotionState.Rotation,
                    velocity,
                    currentMotionState
                        .AngularVelocityRadiansPerSecond);
            lastEvaluatedUniversalTimeSeconds =
                universalTimeSeconds;
            motionState =
                currentMotionState;
            return true;
        }

        private void UpdateReferenceVelocity(
            ref DoubleVector3 velocity)
        {
            var realtimeSeconds =
                Time.realtimeSinceStartupAsDouble;

            if (matchVelocityWithReferenceBody &&
                velocityReferenceBody != null &&
                ShouldSampleReferenceVelocity(
                    realtimeSeconds) &&
                velocityReferenceBody.TryGetMotionState(
                    out var referenceMotion))
            {
                targetReferenceVelocity =
                    referenceMotion
                        .LinearVelocityMetersPerSecond;
                hasTargetReferenceVelocity = true;
                lastVelocityReferenceSampleRealtimeSeconds =
                    realtimeSeconds;
            }

            if (!hasTargetReferenceVelocity)
            {
                lastVelocityBlendRealtimeSeconds =
                    realtimeSeconds;
                return;
            }

            var blendElapsedSeconds =
                Mathf.Max(
                    0.0f,
                    (float)(
                        realtimeSeconds -
                        lastVelocityBlendRealtimeSeconds));
            var blendFactor =
                velocityReferenceBlendSeconds <= 0.0f
                    ? 1.0
                    : 1.0 -
                        System.Math.Exp(
                            -blendElapsedSeconds /
                            velocityReferenceBlendSeconds);

            velocity +=
                (targetReferenceVelocity - velocity) *
                blendFactor;
            lastVelocityBlendRealtimeSeconds =
                realtimeSeconds;
        }

        private bool ShouldSampleReferenceVelocity(
            double realtimeSeconds)
        {
            return
                !hasTargetReferenceVelocity ||
                velocityReferenceUpdateIntervalSeconds <= 0.0f ||
                realtimeSeconds -
                    lastVelocityReferenceSampleRealtimeSeconds >=
                    velocityReferenceUpdateIntervalSeconds;
        }

        private void ResetVelocityReferenceRuntimeState()
        {
            targetReferenceVelocity =
                DoubleVector3.zero;
            hasTargetReferenceVelocity = false;
            lastVelocityReferenceSampleRealtimeSeconds =
                double.NegativeInfinity;
            lastVelocityBlendRealtimeSeconds =
                Time.realtimeSinceStartupAsDouble;
        }

        private void ApplyCurrentState()
        {
            if (floatingObject == null ||
                !TryEvaluateCurrentState(
                    out var motionState))
            {
                return;
            }

            if (!SgtUniversePositionConverter.TryToSgtPosition(
                    motionState.Position,
                    0.0,
                    0.0,
                    0.0,
                    out var floatingPosition))
            {
                Fail(
                    "Universe velocity motion position is outside SGT's coordinate range.");
                return;
            }

            floatingObject.SetPosition(
                floatingPosition);
            floatingObject.ApplyPosition();

            if (applyRotation)
            {
                transform.rotation =
                    motionState.Rotation;
            }
        }

        private bool Fail(
            string error)
        {
            initialized = false;
            lastError = error;
            Debug.LogError(
                error,
                this);
            return false;
        }
    }
}
