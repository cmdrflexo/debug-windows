/*
 * Exposes GE translation and sampled body rotation as a universe motion state in SI units.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(-95)]
    [DisallowMultipleComponent]
    public sealed class GravityEngineCelestialBodyMotionProvider :
        MonoBehaviour,
        ICelestialBodyMotionProvider
    {
        [SerializeField]
        private UniverseFrameController universeFrame;

        [SerializeField]
        private NBody sourceBody;

        [Header("Runtime")]
        [SerializeField]
        private bool isReady;

        [SerializeField]
        private bool hasMotionState;

        [SerializeField]
        private UniverseMotionState motionState;

        [SerializeField]
        private double simulationTimeSeconds;

        [SerializeField]
        private bool hasAngularVelocityEstimate;

        [SerializeField]
        private string lastError;

        private GravityEngine gravityEngine;
        private NBody sampledBody;
        private GravityState sampledWorldState;
        private readonly UniverseAngularVelocitySampler angularVelocitySampler =
            new UniverseAngularVelocitySampler();

        public string ProviderName =>
            "Gravity Engine";

        public bool IsReady =>
            isReady;

        public UniverseFrameController UniverseFrame =>
            universeFrame;

        public NBody SourceBody =>
            sourceBody;

        public bool HasMotionState =>
            hasMotionState;

        public UniverseMotionState MotionState =>
            motionState;

        public double SimulationTimeSeconds =>
            simulationTimeSeconds;

        public bool HasAngularVelocityEstimate =>
            hasAngularVelocityEstimate;

        public string LastError =>
            lastError;

        private void Start()
        {
            gravityEngine =
                GravityEngine.Instance();
            RefreshMotionState();
        }

        private void LateUpdate()
        {
            RefreshMotionState();
        }

        private void OnDisable()
        {
            ResetAngularVelocitySampling();
        }

        [ContextMenu("Reset Angular Velocity Sampling")]
        public void ResetAngularVelocitySampling()
        {
            angularVelocitySampler.Reset();
            hasAngularVelocityEstimate = false;
            sampledBody = null;
            sampledWorldState = null;
        }

        public void Initialize(
            UniverseFrameController newUniverseFrame,
            NBody newSourceBody)
        {
            universeFrame =
                newUniverseFrame;
            sourceBody =
                newSourceBody;
            gravityEngine =
                GravityEngine.Instance();

            ResetAngularVelocitySampling();
            RefreshMotionState();
        }

        public bool TryGetMotionState(
            out UniverseMotionState currentMotionState)
        {
            RefreshMotionState();

            currentMotionState =
                motionState;
            return hasMotionState;
        }

        private void RefreshMotionState()
        {
            isReady = false;
            hasMotionState = false;

            if (universeFrame == null)
            {
                RecordMotionFailure("Universe frame is not assigned.");
                return;
            }

            if (sourceBody == null)
            {
                RecordMotionFailure("Gravity Engine body is not assigned.");
                return;
            }

            if (gravityEngine == null)
                gravityEngine = GravityEngine.Instance();

            if (gravityEngine == null ||
                !gravityEngine.IsSetup())
            {
                RecordMotionFailure("Gravity Engine is not ready.");
                return;
            }

            if (!universeFrame.FrameOriginInitialized)
            {
                RecordMotionFailure("Universe frame origin is not initialized.");
                return;
            }

            // Match the factory's SI-only boundary; physicalScale alone does not convert AU/km.
            if (gravityEngine.units != GravityScaler.Units.SI)
            {
                RecordMotionFailure("Universe motion currently requires Gravity Engine to use SI units.");
                return;
            }

            var physicalScale =
                gravityEngine.GetPhysicalScale();
            var velocityScale =
                GravityScaler.VelocityScaletoSIUnits();

            if (!IsFinite(physicalScale) ||
                physicalScale <= 0.0 ||
                !IsFinite(velocityScale) ||
                velocityScale <= 0.0)
            {
                RecordMotionFailure("Gravity Engine returned invalid SI position or velocity scaling.");
                return;
            }

            // Read the current state without cloning it or differentiating shifted positions.
            var worldState = gravityEngine.GetWorldState();
            if (worldState == null)
            {
                RecordMotionFailure("Gravity Engine has no current world state.");
                return;
            }

            var physicsPosition =
                gravityEngine.GetPositionDoubleV3(
                    sourceBody);

            var positionMeters = new DoubleVector3(
                physicsPosition.x * physicalScale,
                physicsPosition.y * physicalScale,
                physicsPosition.z * physicalScale);
            var physicsVelocity = worldState.GetVelocity3d(sourceBody);
            var linearVelocity = new DoubleVector3(
                physicsVelocity.x * velocityScale,
                physicsVelocity.y * velocityScale,
                physicsVelocity.z * velocityScale);

            if (!IsFinite(positionMeters.x) ||
                !IsFinite(positionMeters.y) ||
                !IsFinite(positionMeters.z) ||
                !IsFinite(linearVelocity.x) ||
                !IsFinite(linearVelocity.y) ||
                !IsFinite(linearVelocity.z))
            {
                RecordMotionFailure("Gravity Engine returned a non-finite SI body position or velocity.");
                return;
            }

            if (sampledBody != sourceBody || !ReferenceEquals(sampledWorldState, worldState))
            {
                ResetAngularVelocitySampling();
                sampledBody = sourceBody;
                sampledWorldState = worldState;
            }

            simulationTimeSeconds = GravityScaler.GetWorldTimeSeconds(worldState.GetPhysicsTime());
            var rotation = sourceBody.transform.rotation;
            if (!angularVelocitySampler.TrySample(rotation, simulationTimeSeconds))
            {
                RecordMotionFailure("Body rotation or simulation time is invalid for angular velocity sampling.");
                return;
            }

            hasAngularVelocityEstimate = angularVelocitySampler.HasEstimate;
            var universePosition =
                universeFrame.FrameOrigin;

            universePosition.AddLocalMeters(
                positionMeters.x,
                positionMeters.y,
                positionMeters.z);

            motionState =
                new UniverseMotionState(
                    universePosition,
                    rotation,
                    linearVelocity,
                    angularVelocitySampler.AngularVelocityRadiansPerSecond);
            hasMotionState = true;
            isReady = true;
            lastError = string.Empty;
        }

        private void RecordMotionFailure(string error)
        {
            ResetAngularVelocitySampling();
            lastError = error;
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
