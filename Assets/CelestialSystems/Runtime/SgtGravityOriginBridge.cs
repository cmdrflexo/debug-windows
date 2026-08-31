/*
 * Uses an SGT floating camera as the active free-flight source for project-owned universe-frame shifts.
 */

using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class SgtGravityOriginBridge : UniverseAnchorSource
    {
        [SerializeField]
        private SgtFloatingCamera floatingCamera;

        private SgtPosition previousSnappedPoint;
        private bool previousSnappedPointSet;

        private void OnEnable()
        {
            SgtFloatingCamera.OnSnap += HandleFloatingCameraSnap;
        }

        private void Start()
        {
            if (floatingCamera == null)
            {
                Debug.LogError(
                    "The SGT origin bridge requires a floating-camera anchor.",
                    this);
                return;
            }

            if (UniverseFrame == null)
            {
                Debug.LogError(
                    "The SGT origin bridge requires a universe frame controller.",
                    this);
                return;
            }

            if (floatingCamera.SnappedPointSet)
            {
                previousSnappedPoint = floatingCamera.SnappedPoint;
                previousSnappedPointSet = true;

                if (IsActiveSource)
                {
                    TryInitializeFrameOrigin(
                        SgtUniversePositionConverter.ToUniversePosition(
                            previousSnappedPoint));
                }
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying ||
                !IsActiveSource ||
                floatingCamera == null)
            {
                return;
            }

            if (floatingCamera.transform.position.magnitude >
                floatingCamera.SnapDistance)
            {
                floatingCamera.Snap();
            }
        }

        private void OnDisable()
        {
            SgtFloatingCamera.OnSnap -= HandleFloatingCameraSnap;
        }

        protected override void OnSourceActivationChanged(bool active)
        {
            if (!active)
            {
                return;
            }

            previousSnappedPointSet = false;

            if (floatingCamera != null &&
                floatingCamera.SnappedPointSet)
            {
                previousSnappedPoint = floatingCamera.SnappedPoint;
                previousSnappedPointSet = true;
            }
        }

        private void HandleFloatingCameraSnap(
            SgtFloatingCamera snappedCamera,
            Vector3 sceneDelta)
        {
            if (!Application.isPlaying ||
                !IsActiveSource ||
                snappedCamera != floatingCamera)
            {
                return;
            }

            var currentSnappedPoint = snappedCamera.SnappedPoint;

            if (!previousSnappedPointSet)
            {
                previousSnappedPoint = currentSnappedPoint;
                previousSnappedPointSet = true;
                TryInitializeFrameOrigin(
                    SgtUniversePositionConverter.ToUniversePosition(
                        currentSnappedPoint));
                return;
            }

            var originAdvanceMeters =
                CalculateDeltaMeters(previousSnappedPoint, currentSnappedPoint);

            if (TryShiftOrigin(originAdvanceMeters, sceneDelta))
            {
                previousSnappedPoint = currentSnappedPoint;
            }
        }

        private static Vector3d CalculateDeltaMeters(
            SgtPosition from,
            SgtPosition to)
        {
            return new Vector3d(
                (to.GlobalX - from.GlobalX) * SgtPosition.CELL_SIZE +
                to.LocalX -
                from.LocalX,
                (to.GlobalY - from.GlobalY) * SgtPosition.CELL_SIZE +
                to.LocalY -
                from.LocalY,
                (to.GlobalZ - from.GlobalZ) * SgtPosition.CELL_SIZE +
                to.LocalZ -
                from.LocalZ);
        }
    }
}
