/*
 * Places and orients a visual marker at the universe observation pivot.
 */

using System;
using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(380)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SgtFloatingObject))]
    public sealed class UniverseObservationPivotMarker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private UniverseObservationAnchorController observationController;

        [SerializeField]
        [Tooltip("Optional moving point that the marker's local Z axis faces.")]
        private CelestialBodyRuntimeContext directionReferenceContext;

        [Header("Direction Reference")]
        [SerializeField]
        [Tooltip("Use the observation target when no explicit direction reference is assigned.")]
        private bool useObservationTargetWhenUnset = true;

        [SerializeField]
        private bool hasDirectionReferencePosition;

        [SerializeField]
        private UniversePosition directionReferencePosition;

        [Header("Rendering")]
        [SerializeField]
        private Renderer markerRenderer;

        [SerializeField]
        private bool visible = true;

        [Header("Runtime")]
        [SerializeField]
        private bool hasPivotPosition;

        [SerializeField]
        private UniversePosition pivotPosition;

        [SerializeField]
        private bool hasResolvedDirectionReference;

        [SerializeField]
        private UniversePosition resolvedDirectionReferencePosition;

        private SgtFloatingObject floatingObject;

        public UniverseObservationAnchorController ObservationController =>
            observationController;

        public CelestialBodyRuntimeContext DirectionReferenceContext =>
            directionReferenceContext;

        public bool HasPivotPosition => hasPivotPosition;

        public UniversePosition PivotPosition => pivotPosition;

        public bool HasDirectionReference => hasResolvedDirectionReference;

        public UniversePosition DirectionReferencePosition =>
            resolvedDirectionReferencePosition;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void LateUpdate()
        {
            ResolveReferences();

            if (!visible ||
                observationController == null ||
                floatingObject == null ||
                !observationController.TryGetObservationPlane(
                    out var currentPivotPosition,
                    out var planeRight,
                    out var planeForward,
                    out var planeUp) ||
                !SgtUniversePositionConverter.TryToSgtPosition(
                    currentPivotPosition,
                    0.0,
                    0.0,
                    0.0,
                    out var markerPosition))
            {
                hasPivotPosition = false;
                hasResolvedDirectionReference = false;
                SetRendererVisible(false);
                return;
            }

            pivotPosition = currentPivotPosition;
            hasPivotPosition = true;
            floatingObject.SetPosition(markerPosition);
            floatingObject.ApplyPosition();

            var markerForward = planeForward;

            if (TryResolveDirectionReference(out var referencePosition))
            {
                resolvedDirectionReferencePosition = referencePosition;
                hasResolvedDirectionReference = true;

                if (TryGetPlanarDirection(
                        currentPivotPosition,
                        referencePosition,
                        planeRight,
                        planeForward,
                        out var planarDirection))
                {
                    markerForward = planarDirection;
                }
            }
            else
            {
                hasResolvedDirectionReference = false;
            }

            transform.rotation = Quaternion.LookRotation(
                markerForward,
                planeUp);
            SetRendererVisible(true);
        }

        public void SetDirectionReference(
            CelestialBodyRuntimeContext context)
        {
            directionReferenceContext = context;

            if (context != null &&
                context.TryGetMotionState(out var motionState))
            {
                directionReferencePosition = motionState.Position;
                hasDirectionReferencePosition = true;
            }
        }

        public void SetDirectionReference(UniversePosition position)
        {
            directionReferenceContext = null;
            directionReferencePosition = position;
            hasDirectionReferencePosition = true;
        }

        public void ClearDirectionReference()
        {
            directionReferenceContext = null;
            hasDirectionReferencePosition = false;
            hasResolvedDirectionReference = false;
        }

        public void SetVisible(bool value)
        {
            visible = value;

            if (!visible)
            {
                SetRendererVisible(false);
            }
        }

        private bool TryResolveDirectionReference(
            out UniversePosition position)
        {
            if (directionReferenceContext != null &&
                directionReferenceContext.TryGetMotionState(
                    out var directionMotion))
            {
                position = directionMotion.Position;
                directionReferencePosition = position;
                hasDirectionReferencePosition = true;
                return true;
            }

            if (hasDirectionReferencePosition)
            {
                position = directionReferencePosition;
                return true;
            }

            var observationTarget = observationController.Target;

            if (useObservationTargetWhenUnset &&
                observationTarget != null &&
                observationTarget.TryGetMotionState(
                    out var targetMotion))
            {
                position = targetMotion.Position;
                return true;
            }

            position = default;
            return false;
        }

        private void ResolveReferences()
        {
            if (observationController == null)
            {
                observationController =
                    FindFirstObjectByType<UniverseObservationAnchorController>();
            }

            floatingObject ??= GetComponent<SgtFloatingObject>();

            if (markerRenderer == null)
            {
                markerRenderer = GetComponentInChildren<Renderer>();
            }
        }

        private void SetRendererVisible(bool value)
        {
            if (markerRenderer != null)
            {
                markerRenderer.enabled = value;
            }
        }

        private static bool TryGetPlanarDirection(
            UniversePosition from,
            UniversePosition to,
            Vector3 planeRight,
            Vector3 planeForward,
            out Vector3 direction)
        {
            var deltaX =
                ((double)to.CellX - from.CellX) *
                    UniversePosition.CellSizeMeters +
                to.LocalXMeters -
                from.LocalXMeters;
            var deltaY =
                ((double)to.CellY - from.CellY) *
                    UniversePosition.CellSizeMeters +
                to.LocalYMeters -
                from.LocalYMeters;
            var deltaZ =
                ((double)to.CellZ - from.CellZ) *
                    UniversePosition.CellSizeMeters +
                to.LocalZMeters -
                from.LocalZMeters;
            var right =
                deltaX * planeRight.x +
                deltaY * planeRight.y +
                deltaZ * planeRight.z;
            var forward =
                deltaX * planeForward.x +
                deltaY * planeForward.y +
                deltaZ * planeForward.z;
            var magnitude = Math.Sqrt(
                right * right +
                forward * forward);

            if (magnitude <= 1.0e-9 ||
                double.IsNaN(magnitude) ||
                double.IsInfinity(magnitude))
            {
                direction = default;
                return false;
            }

            direction =
                planeRight * (float)(right / magnitude) +
                planeForward * (float)(forward / magnitude);
            direction.Normalize();
            return true;
        }
    }
}
