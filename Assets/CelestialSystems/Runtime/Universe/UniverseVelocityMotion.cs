/*
 * Projects a constant-velocity universe motion state through an SGT floating
 * object. It is a lightweight kinematic source, not a Gravity Engine body.
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

        [SerializeField]
        private UniverseMotionState initialMotionState;

        [SerializeField]
        private double initialUniversalTimeSeconds;

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
            initialized = true;
            lastError = string.Empty;
            ApplyCurrentState();
            return true;
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

            var elapsedSeconds =
                timeController.UniversalTimeSeconds -
                initialUniversalTimeSeconds;
            var position =
                initialMotionState.Position;
            var velocity =
                initialMotionState
                    .LinearVelocityMetersPerSecond;

            position.AddLocalMeters(
                velocity.x * elapsedSeconds,
                velocity.y * elapsedSeconds,
                velocity.z * elapsedSeconds);
            motionState =
                new UniverseMotionState(
                    position,
                    initialMotionState.Rotation,
                    velocity,
                    initialMotionState
                        .AngularVelocityRadiansPerSecond);
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
