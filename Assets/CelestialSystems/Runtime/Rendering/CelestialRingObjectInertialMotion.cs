/*
 * Projects a lightweight streamed ring object through SGT using its own
 * universe position. It receives only the source body's initial linear
 * velocity, keeping hundreds of close-range objects out of Gravity Engine.
 */

using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialRingObjectInertialMotion :
        MonoBehaviour
    {
        [SerializeField]
        private SgtFloatingObject floatingObject;

        [SerializeField]
        private UniversePosition initialUniversePosition;

        [SerializeField]
        private DoubleVector3 inheritedVelocityMetersPerSecond;

        [SerializeField]
        private double initialUniversalTimeSeconds;

        [SerializeField]
        private bool initialized;

        public bool Initialize(
            UniverseMotionState sourceMotion,
            Vector3 sourceLocalOffsetMeters)
        {
            floatingObject ??=
                GetComponent<SgtFloatingObject>();

            if (floatingObject == null ||
                CelestialTimeController.Instance == null)
            {
                initialized = false;
                return false;
            }

            var rotatedOffset =
                sourceMotion.Rotation *
                sourceLocalOffsetMeters;
            initialUniversePosition =
                sourceMotion.Position;
            initialUniversePosition.AddLocalMeters(
                rotatedOffset.x,
                rotatedOffset.y,
                rotatedOffset.z);
            inheritedVelocityMetersPerSecond =
                sourceMotion.LinearVelocityMetersPerSecond;
            initialUniversalTimeSeconds =
                CelestialTimeController.Instance
                    .UniversalTimeSeconds;
            initialized = true;
            ApplyCurrentPosition();
            return true;
        }

        private void LateUpdate()
        {
            if (initialized)
            {
                ApplyCurrentPosition();
            }
        }

        private void ApplyCurrentPosition()
        {
            if (floatingObject == null ||
                CelestialTimeController.Instance == null)
            {
                return;
            }

            var elapsedSeconds =
                CelestialTimeController.Instance
                    .UniversalTimeSeconds -
                initialUniversalTimeSeconds;
            var position =
                initialUniversePosition;
            position.AddLocalMeters(
                inheritedVelocityMetersPerSecond.x *
                    elapsedSeconds,
                inheritedVelocityMetersPerSecond.y *
                    elapsedSeconds,
                inheritedVelocityMetersPerSecond.z *
                    elapsedSeconds);

            if (!SgtUniversePositionConverter.TryToSgtPosition(
                    position,
                    0.0,
                    0.0,
                    0.0,
                    out var floatingPosition))
            {
                return;
            }

            floatingObject.SetPosition(
                floatingPosition);
            floatingObject.ApplyPosition();
        }
    }
}
