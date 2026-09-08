/*
 * Measures angular velocity between universe rotations using simulation seconds, independently of polling frequency.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    public sealed class UniverseAngularVelocitySampler
    {
        private bool hasSample;
        private Quaternion previousRotation;
        private Quaternion currentRotation;
        private double previousTimeSeconds;
        private double currentTimeSeconds;

        public bool HasEstimate { get; private set; }
        public DoubleVector3 AngularVelocityRadiansPerSecond { get; private set; }

        public void Reset()
        {
            hasSample = false;
            HasEstimate = false;
            AngularVelocityRadiansPerSecond = DoubleVector3.zero;
        }

        /// <summary>
        /// Returns false for unusable input. The first sample and time rewinds establish a
        /// baseline with zero velocity and HasEstimate false. Equal timestamps reuse the
        /// last distinct interval, including when rotation is updated later in the same tick.
        /// Call Reset after a rotation teleport or a change of motion authority.
        /// </summary>
        public bool TrySample(Quaternion rotation, double simulationTimeSeconds)
        {
            if (!IsFinite(simulationTimeSeconds) || !IsUsable(rotation))
            {
                Reset();
                return false;
            }

            if (!hasSample || simulationTimeSeconds < currentTimeSeconds)
            {
                Reset();
                hasSample = true;
                currentRotation = rotation;
                currentTimeSeconds = simulationTimeSeconds;
                return true;
            }

            if (simulationTimeSeconds > currentTimeSeconds)
            {
                previousRotation = currentRotation;
                previousTimeSeconds = currentTimeSeconds;
                currentTimeSeconds = simulationTimeSeconds;
                HasEstimate = true;
            }

            currentRotation = rotation;
            if (!HasEstimate)
                return true;

            var elapsedSeconds = currentTimeSeconds - previousTimeSeconds;
            if (!IsFinite(elapsedSeconds) || elapsedSeconds <= 0.0)
            {
                Reset();
                return false;
            }

            // current * conjugate(previous) gives a delta in universe axes. Multiplying
            // in double precision preserves small angles that float acos(w) would lose.
            // The common quaternion scale cancels in atan2 and axis normalization.
            var x = -(double)currentRotation.w * previousRotation.x +
                (double)currentRotation.x * previousRotation.w -
                (double)currentRotation.y * previousRotation.z +
                (double)currentRotation.z * previousRotation.y;
            var y = -(double)currentRotation.w * previousRotation.y +
                (double)currentRotation.x * previousRotation.z +
                (double)currentRotation.y * previousRotation.w -
                (double)currentRotation.z * previousRotation.x;
            var z = -(double)currentRotation.w * previousRotation.z -
                (double)currentRotation.x * previousRotation.y +
                (double)currentRotation.y * previousRotation.x +
                (double)currentRotation.z * previousRotation.w;
            var w = (double)currentRotation.w * previousRotation.w +
                (double)currentRotation.x * previousRotation.x +
                (double)currentRotation.y * previousRotation.y +
                (double)currentRotation.z * previousRotation.z;

            // q and -q encode the same orientation. Select the shortest rotation.
            if (w < 0.0)
            {
                x = -x;
                y = -y;
                z = -z;
                w = -w;
            }

            var sineHalfAngle = Math.Sqrt(x * x + y * y + z * z);
            if (sineHalfAngle == 0.0)
            {
                AngularVelocityRadiansPerSecond = DoubleVector3.zero;
                return true;
            }

            var scale = 2.0 * Math.Atan2(sineHalfAngle, w) / elapsedSeconds / sineHalfAngle;
            var velocity = new DoubleVector3(x * scale, y * scale, z * scale);
            if (!IsFinite(velocity.x) || !IsFinite(velocity.y) || !IsFinite(velocity.z))
            {
                Reset();
                return false;
            }

            AngularVelocityRadiansPerSecond = velocity;
            return true;
        }

        private static bool IsUsable(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) &&
                IsFinite(value.w) &&
                (value.x != 0.0f || value.y != 0.0f || value.z != 0.0f || value.w != 0.0f);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
