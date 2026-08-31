/*
 * Synchronizes Space Graphics Toolkit floating-origin snaps with Gravity Engine and publishes each scene shift.
 */

using System;
using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class SgtGravityOriginBridge : MonoBehaviour
    {
        [SerializeField]
        private SgtFloatingCamera floatingCamera;

        [SerializeField]
        private bool logOriginShifts;

        [SerializeField]
        private UniversePosition frameOrigin;

        private GravityEngine gravityEngine;
        private SgtPosition previousSnappedPoint;

        public UniversePosition FrameOrigin => frameOrigin;

        public event Action<UniversePosition, Vector3> OriginShifted;

        private void OnEnable()
        {
            SgtFloatingCamera.OnSnap += HandleFloatingCameraSnap;
        }

        private void Start()
        {
            gravityEngine = GravityEngine.Instance();

            if (floatingCamera != null && floatingCamera.SnappedPointSet)
            {
                previousSnappedPoint = floatingCamera.SnappedPoint;
                frameOrigin = ToUniversePosition(previousSnappedPoint);
            }
        }

        private void OnDisable()
        {
            SgtFloatingCamera.OnSnap -= HandleFloatingCameraSnap;
        }

        private void HandleFloatingCameraSnap(SgtFloatingCamera snappedCamera, Vector3 sceneDelta)
        {
            if (!Application.isPlaying || snappedCamera != floatingCamera)
            {
                return;
            }

            var currentSnappedPoint = snappedCamera.SnappedPoint;
            var originAdvanceMeters = CalculateDeltaMeters(previousSnappedPoint, currentSnappedPoint);

            gravityEngine ??= GravityEngine.Instance();

            if (gravityEngine == null)
            {
                Debug.LogError("Cannot synchronize the origin because no Gravity Engine exists in the scene.", this);
                previousSnappedPoint = currentSnappedPoint;
                frameOrigin = ToUniversePosition(currentSnappedPoint);
                return;
            }

            var physicalScale = gravityEngine.GetPhysicalScale();

            if (Mathf.Approximately(physicalScale, 0.0f))
            {
                Debug.LogError("Cannot synchronize the origin because Gravity Engine's physical scale is zero.", this);
                return;
            }

            var physicsDelta = new Vector3d(
                -originAdvanceMeters.x / physicalScale,
                -originAdvanceMeters.y / physicalScale,
                -originAdvanceMeters.z / physicalScale);

            gravityEngine.MoveAll(physicsDelta);

            previousSnappedPoint = currentSnappedPoint;
            frameOrigin = ToUniversePosition(currentSnappedPoint);

            OriginShifted?.Invoke(frameOrigin, sceneDelta);

            if (logOriginShifts)
            {
                Debug.Log($"Origin shifted to {frameOrigin}. Scene delta: {sceneDelta}.", this);
            }
        }

        private static Vector3d CalculateDeltaMeters(SgtPosition from, SgtPosition to)
        {
            return new Vector3d(
                (to.GlobalX - from.GlobalX) * SgtPosition.CELL_SIZE + to.LocalX - from.LocalX,
                (to.GlobalY - from.GlobalY) * SgtPosition.CELL_SIZE + to.LocalY - from.LocalY,
                (to.GlobalZ - from.GlobalZ) * SgtPosition.CELL_SIZE + to.LocalZ - from.LocalZ);
        }

        private static UniversePosition ToUniversePosition(SgtPosition position)
        {
            return new UniversePosition(
                position.GlobalX,
                position.GlobalY,
                position.GlobalZ,
                position.LocalX,
                position.LocalY,
                position.LocalZ);
        }
    }
}
