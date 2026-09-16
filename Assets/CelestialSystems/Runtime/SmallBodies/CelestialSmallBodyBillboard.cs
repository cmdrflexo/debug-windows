/*
 * Faces a generated low-detail small body toward its observing camera.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialSmallBodyBillboard :
        MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Optional camera. Camera.main is used when empty.")]
        private Camera observerCamera;

        private void LateUpdate()
        {
            var camera =
                observerCamera != null
                    ? observerCamera
                    : Camera.main;

            if (camera == null)
            {
                return;
            }

            var towardCamera =
                camera.transform.position -
                transform.position;

            if (towardCamera.sqrMagnitude >
                0.000001f)
            {
                transform.rotation =
                    Quaternion.LookRotation(
                        towardCamera,
                        camera.transform.up);
            }
        }
    }
}
