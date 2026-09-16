/*
 * Projects a universe-space kinematic motion state through an SGT floating
 * object. It can optionally inherit the current linear velocity of a body
 * while retaining its own independent universe position.
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
        [Tooltip("Continuously matches this object's linear velocity to the reference body.")]
        private bool matchVelocityWithReferenceBody;

        [Header("Runtime State")]
        [SerializeField]
        private UniverseMotionState currentMotionState;

        [SerializeField]
        private double lastEvaluatedUniversalTimeSeconds;

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
            var elapsedSeconds =
                universalTimeSeconds -
                lastEvaluatedUniversalTimeSeconds;
            var velocity =
                currentMotionState
                    .LinearVelocityMetersPerSecond;

            if (matchVelocityWithReferenceBody &&
                velocityReferenceBody != null &&
                velocityReferenceBody.TryGetMotionState(
                    out var referenceMotion))
            {
                velocity =
                    referenceMotion
                        .LinearVelocityMetersPerSecond;
            }

            var position =
                currentMotionState.Position;
            position.AddLocalMeters(
                velocity.x * elapsedSeconds,
                velocity.y * elapsedSeconds,
                velocity.z * elapsedSeconds);
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
