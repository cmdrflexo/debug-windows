/*
 * Adapts Space Graphics Toolkit floating-camera snaps into project-owned universe-frame shifts.
 */

using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class SgtGravityOriginBridge : MonoBehaviour
    {
        private const long SgtCellsPerUniverseCell = 20;

        [SerializeField]
        private UniverseFrameController universeFrame;

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

            if (universeFrame == null)
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
                universeFrame.InitializeFrameOrigin(
                    ToUniversePosition(previousSnappedPoint));
            }
        }

        private void OnDisable()
        {
            SgtFloatingCamera.OnSnap -= HandleFloatingCameraSnap;
        }

        private void HandleFloatingCameraSnap(
            SgtFloatingCamera snappedCamera,
            Vector3 sceneDelta)
        {
            if (!Application.isPlaying || snappedCamera != floatingCamera)
            {
                return;
            }

            var currentSnappedPoint = snappedCamera.SnappedPoint;

            if (!previousSnappedPointSet)
            {
                previousSnappedPoint = currentSnappedPoint;
                previousSnappedPointSet = true;
                universeFrame?.InitializeFrameOrigin(
                    ToUniversePosition(currentSnappedPoint));
                return;
            }

            if (universeFrame == null)
            {
                Debug.LogError(
                    "Cannot forward the SGT origin shift because no universe frame controller is assigned.",
                    this);
                return;
            }

            var originAdvanceMeters =
                CalculateDeltaMeters(previousSnappedPoint, currentSnappedPoint);

            if (universeFrame.ShiftOrigin(originAdvanceMeters, sceneDelta))
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

        private static UniversePosition ToUniversePosition(
            SgtPosition position)
        {
            ConvertSgtAxis(
                position.GlobalX,
                position.LocalX,
                out var cellX,
                out var localXMeters);
            ConvertSgtAxis(
                position.GlobalY,
                position.LocalY,
                out var cellY,
                out var localYMeters);
            ConvertSgtAxis(
                position.GlobalZ,
                position.LocalZ,
                out var cellZ,
                out var localZMeters);

            return new UniversePosition(
                cellX,
                cellY,
                cellZ,
                localXMeters,
                localYMeters,
                localZMeters);
        }

        private static void ConvertSgtAxis(
            long sgtCell,
            double sgtLocalMeters,
            out long universeCell,
            out double universeLocalMeters)
        {
            universeCell = sgtCell / SgtCellsPerUniverseCell;
            var remainingSgtCells =
                sgtCell % SgtCellsPerUniverseCell;
            universeLocalMeters =
                remainingSgtCells * SgtPosition.CELL_SIZE +
                sgtLocalMeters;
        }
    }
}
