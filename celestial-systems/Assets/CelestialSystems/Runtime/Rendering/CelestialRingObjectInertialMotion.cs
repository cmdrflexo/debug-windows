/*
 * Resolves a streamed ring cell into one fixed universe position, then hands
 * that state to the reusable SGT velocity-motion component.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialRingObjectInertialMotion :
        MonoBehaviour
    {
        [SerializeField]
        private UniverseVelocityMotion velocityMotion;

        [SerializeField]
        private UniversePosition initialUniversePosition;

        [SerializeField]
        private bool initialized;

        public bool Initialize(
            UniverseMotionState sourceMotion,
            Vector3 sourceLocalOffsetMeters)
        {
            velocityMotion ??=
                GetComponent<UniverseVelocityMotion>();

            if (velocityMotion == null)
            {
                velocityMotion =
                    gameObject.AddComponent<UniverseVelocityMotion>();
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

            initialized =
                velocityMotion.Initialize(
                    new UniverseMotionState(
                        initialUniversePosition,
                        Quaternion.identity));
            return initialized;
        }
    }
}
