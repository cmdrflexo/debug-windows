/*
 * Uses an SGT floating camera as the active free-flight source for project-owned universe-frame shifts.
 */

using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class SgtUniverseOriginBridge : UniverseAnchorSource
    {
        [SerializeField]
        private SgtFloatingCamera floatingCamera;

        [SerializeField]
        [Tooltip("Transform whose universe rotation is captured and restored with camera poses.")]
        private Transform poseRotationSource;

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

            SnapIfBeyondDistance();
        }

        private void OnDisable()
        {
            SgtFloatingCamera.OnSnap -= HandleFloatingCameraSnap;
        }

        public bool TryGetUniversePose(
            out UniverseMotionState pose)
        {
            if (floatingCamera == null)
            {
                pose = default;
                return false;
            }

            var rotationSource = ResolvePoseRotationSource();
            pose = new UniverseMotionState(
                SgtUniversePositionConverter.ToUniversePosition(
                    floatingCamera.Position),
                rotationSource.rotation);
            return true;
        }

        public bool TrySetUniversePose(
            UniverseMotionState pose)
        {
            if (!IsActiveSource ||
                floatingCamera == null ||
                !SgtUniversePositionConverter.TryToSgtPosition(
                    pose.Position,
                    0.0,
                    0.0,
                    0.0,
                    out var targetPosition))
            {
                return false;
            }

            floatingCamera.Position = targetPosition;
            ResolvePoseRotationSource().rotation = pose.Rotation;

            // Tracking a time-driven body updates its pose every frame. Snapping
            // unconditionally here causes a no-op origin shift on every update.
            SnapIfBeyondDistance();
            return true;
        }

        public override bool TryGetFrameOffsetMeters(
            out Vector3d offsetMeters)
        {
            if (floatingCamera == null)
            {
                offsetMeters = default;
                return false;
            }

            var scenePosition = floatingCamera.transform.position;

            offsetMeters = new Vector3d(
                scenePosition.x,
                scenePosition.y,
                scenePosition.z);
            return true;
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

        private void SnapIfBeyondDistance()
        {
            if (floatingCamera.transform.position.magnitude >
                floatingCamera.SnapDistance)
            {
                floatingCamera.Snap();
            }
        }

        private Transform ResolvePoseRotationSource()
        {
            if (poseRotationSource != null)
                return poseRotationSource;

            var mainCamera = Camera.main;
            if (mainCamera != null &&
                mainCamera.transform.IsChildOf(floatingCamera.transform))
            {
                poseRotationSource = mainCamera.transform;
            }
            else
            {
                poseRotationSource = floatingCamera.transform;
            }

            return poseRotationSource;
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
