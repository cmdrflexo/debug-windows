/*
 * Carries an object's global universe pose and motion in meters, seconds, and universe axes.
 */

using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace jcan.CelestialSystems
{
    [Serializable]
    [MovedFrom(false, sourceNamespace: "jcan.CelestialSystems",
        sourceAssembly: "Assembly-CSharp", sourceClassName: "CelestialBodyMotionState")]
    public struct UniverseMotionState
    {
        [SerializeField]
        private UniversePosition position;

        [SerializeField]
        private Quaternion rotation;

        [SerializeField]
        private DoubleVector3 linearVelocityMetersPerSecond;

        [SerializeField]
        private DoubleVector3 angularVelocityRadiansPerSecond;

        /// <summary>Global position, independent of the current scene origin.</summary>
        public UniversePosition Position =>
            position;

        /// <summary>Orientation from object-local axes into universe axes.</summary>
        public Quaternion Rotation =>
            rotation;

        /// <summary>Velocity of Position in universe axes, per simulated SI second.</summary>
        public DoubleVector3 LinearVelocityMetersPerSecond =>
            linearVelocityMetersPerSecond;

        /// <summary>
        /// Universe-space rotation axis multiplied by radians per simulated SI second.
        /// Uses the same axis/angle convention as Quaternion.AngleAxis; not Euler angle rates.
        /// </summary>
        public DoubleVector3 AngularVelocityRadiansPerSecond =>
            angularVelocityRadiansPerSecond;

        /// <summary>Creates a stationary pose with both motion vectors set to zero.</summary>
        public UniverseMotionState(
            UniversePosition position,
            Quaternion rotation)
            : this(position, rotation, DoubleVector3.zero, DoubleVector3.zero)
        {
        }

        public UniverseMotionState(
            UniversePosition position,
            Quaternion rotation,
            DoubleVector3 linearVelocityMetersPerSecond,
            DoubleVector3 angularVelocityRadiansPerSecond)
        {
            this.position = position;
            this.rotation = rotation;
            this.linearVelocityMetersPerSecond = linearVelocityMetersPerSecond;
            this.angularVelocityRadiansPerSecond = angularVelocityRadiansPerSecond;
        }
    }
}
