/*
 * Projects a lightweight streamed ring object through SGT using its own
 * fixed universe position. This is a visual prototype: ring objects do not
 * inherit velocity or participate in Gravity Engine simulation.
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
        private bool initialized;

        public bool Initialize(
            UniverseMotionState sourceMotion,
            Vector3 sourceLocalOffsetMeters)
        {
            floatingObject ??=
                GetComponent<SgtFloatingObject>();

            if (floatingObject == null)
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
            initialized = true;

            if (!SgtUniversePositionConverter.TryToSgtPosition(
                    initialUniversePosition,
                    0.0,
                    0.0,
                    0.0,
                    out var floatingPosition))
            {
                initialized = false;
                return false;
            }

            floatingObject.SetPosition(
                floatingPosition);
            floatingObject.ApplyPosition();
            return true;
        }
    }
}
