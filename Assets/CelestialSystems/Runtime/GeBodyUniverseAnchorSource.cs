/*
 * Uses a Gravity Engine body as an active universe anchor while driving the SGT camera from project coordinates.
 */

using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class GeBodyUniverseAnchorSource : UniverseAnchorSource
    {
        [SerializeField]
        private NBody sourceBody;

        [SerializeField]
        private SgtFloatingCamera floatingCamera;

        [SerializeField]
        private double recenterDistanceMeters = 100000.0;

        private GravityEngine gravityEngine;
        private bool coordinateRangeErrorLogged;

        private void Start()
        {
            gravityEngine = GravityEngine.Instance();

            if (sourceBody == null)
            {
                Debug.LogError(
                    "The GE body anchor source requires an NBody.",
                    this);
            }

            if (floatingCamera == null)
            {
                Debug.LogError(
                    "The GE body anchor source requires an SGT floating camera.",
                    this);
            }

            if (UniverseFrame == null)
            {
                Debug.LogError(
                    "The GE body anchor source requires a universe frame controller.",
                    this);
            }

            if (recenterDistanceMeters <= 0.0)
            {
                Debug.LogError(
                    "The GE body anchor source requires a positive recenter distance.",
                    this);
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying ||
                !IsActiveSource ||
                sourceBody == null ||
                floatingCamera == null ||
                UniverseFrame == null ||
                recenterDistanceMeters <= 0.0)
            {
                return;
            }

            if (!UniverseFrame.FrameOriginInitialized &&
                !TryInitializeFrameOrigin(default))
            {
                return;
            }

            if (!TryGetFrameOffsetMeters(out var sourceOffsetMeters))
            {
                return;
            }

            var offsetXMeters = sourceOffsetMeters.x;
            var offsetYMeters = sourceOffsetMeters.y;
            var offsetZMeters = sourceOffsetMeters.z;

            if (!SgtUniversePositionConverter.TryToSgtPosition(
                    UniverseFrame.FrameOrigin,
                    offsetXMeters,
                    offsetYMeters,
                    offsetZMeters,
                    out var cameraPosition))
            {
                if (!coordinateRangeErrorLogged)
                {
                    Debug.LogError(
                        "Cannot position the GE body anchor because it is outside SGT's coordinate range.",
                        this);
                    coordinateRangeErrorLogged = true;
                }

                return;
            }

            coordinateRangeErrorLogged = false;
            floatingCamera.Position = cameraPosition;

            var distanceSquared =
                offsetXMeters * offsetXMeters +
                offsetYMeters * offsetYMeters +
                offsetZMeters * offsetZMeters;
            var recenterDistanceSquared =
                recenterDistanceMeters * recenterDistanceMeters;

            if (distanceSquared <= recenterDistanceSquared)
            {
                return;
            }

            var previousScenePosition =
                floatingCamera.transform.position;

            floatingCamera.Snap();

            var sceneDelta =
                floatingCamera.transform.position -
                previousScenePosition;
            var originAdvanceMeters = new Vector3d(
                offsetXMeters,
                offsetYMeters,
                offsetZMeters);

            TryShiftOrigin(originAdvanceMeters, sceneDelta);
        }

        public override bool TryGetFrameOffsetMeters(
            out Vector3d offsetMeters)
        {
            gravityEngine ??= GravityEngine.Instance();

            if (gravityEngine == null ||
                !gravityEngine.IsSetup() ||
                sourceBody == null)
            {
                offsetMeters = default;
                return false;
            }

            var physicalScale = gravityEngine.GetPhysicalScale();

            if (Mathf.Approximately(physicalScale, 0.0f))
            {
                offsetMeters = default;
                return false;
            }

            var physicsPosition =
                gravityEngine.GetPositionDoubleV3(sourceBody);

            offsetMeters = new Vector3d(
                physicsPosition.x * physicalScale,
                physicsPosition.y * physicalScale,
                physicsPosition.z * physicalScale);
            return true;
        }
    }
}
