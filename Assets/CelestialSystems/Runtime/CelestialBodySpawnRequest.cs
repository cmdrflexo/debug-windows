/*
 * Carries instance-specific data into the celestial-body factory while reusable body data remains in definitions.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    public sealed class CelestialBodySpawnRequest
    {
        public string InstanceId { get; }

        public CelestialBodyDefinition Definition { get; }

        public DoubleVector3 InitialPositionMetersFromFrameOrigin { get; }

        public DoubleVector3 InitialVelocityMetersPerSecond { get; }

        public Quaternion InitialRotation { get; }

        public RoundMapMagicSurfaceQualityProfile QualityProfile { get; }

        public Transform ParentOverride { get; }

        public CelestialBodySpawnMode MotionMode { get; }

        public CelestialBodyRuntimeContext OrbitCenter { get; }

        public DoubleVector3 InitialAngularVelocityRadiansPerSecond { get; }

        public CelestialBodySpawnRequest(
            string instanceId,
            CelestialBodyDefinition definition,
            DoubleVector3 initialPositionMetersFromFrameOrigin,
            DoubleVector3 initialVelocityMetersPerSecond,
            Quaternion initialRotation,
            RoundMapMagicSurfaceQualityProfile qualityProfile = null,
            Transform parentOverride = null,
            CelestialBodySpawnMode motionMode = CelestialBodySpawnMode.FreeSimulation,
            CelestialBodyRuntimeContext orbitCenter = null,
            DoubleVector3 initialAngularVelocityRadiansPerSecond = default)
        {
            InstanceId = instanceId;
            Definition = definition;
            InitialPositionMetersFromFrameOrigin =
                initialPositionMetersFromFrameOrigin;
            InitialVelocityMetersPerSecond =
                initialVelocityMetersPerSecond;
            InitialRotation = initialRotation;
            QualityProfile = qualityProfile;
            ParentOverride = parentOverride;
            MotionMode = motionMode;
            OrbitCenter = orbitCenter;
            InitialAngularVelocityRadiansPerSecond =
                initialAngularVelocityRadiansPerSecond;
        }
    }
}
