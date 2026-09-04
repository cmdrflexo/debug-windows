/*
 * Describes one camera or gameplay observer requesting adaptive visual or collision surface detail.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [Serializable]
    public struct CelestialSurfaceObserverState
    {
        [SerializeField]
        private string observerId;

        [SerializeField]
        private DoubleVector3 bodyRelativePositionMeters;

        [SerializeField]
        private double verticalFieldOfViewDegrees;

        [SerializeField]
        private int viewportHeightPixels;

        [SerializeField]
        private bool requestsRendering;

        [SerializeField]
        private bool requestsCollision;

        [SerializeField]
        private float priority;

        public string ObserverId =>
            observerId;

        public DoubleVector3 BodyRelativePositionMeters =>
            bodyRelativePositionMeters;

        public double VerticalFieldOfViewDegrees =>
            verticalFieldOfViewDegrees;

        public int ViewportHeightPixels =>
            viewportHeightPixels;

        public bool RequestsRendering =>
            requestsRendering;

        public bool RequestsCollision =>
            requestsCollision;

        public float Priority =>
            priority;

        public double DistanceFromBodyCenterMeters =>
            Math.Sqrt(
                bodyRelativePositionMeters.x *
                    bodyRelativePositionMeters.x +
                bodyRelativePositionMeters.y *
                    bodyRelativePositionMeters.y +
                bodyRelativePositionMeters.z *
                    bodyRelativePositionMeters.z);

        public bool HasValidProjection =>
            IsFinite(verticalFieldOfViewDegrees) &&
            verticalFieldOfViewDegrees > 0.0 &&
            verticalFieldOfViewDegrees < 180.0 &&
            viewportHeightPixels > 0;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(observerId) &&
            IsFinite(bodyRelativePositionMeters.x) &&
            IsFinite(bodyRelativePositionMeters.y) &&
            IsFinite(bodyRelativePositionMeters.z) &&
            IsFinite(DistanceFromBodyCenterMeters) &&
            DistanceFromBodyCenterMeters > 0.0 &&
            IsFinite(priority) &&
            priority >= 0.0f &&
            (requestsRendering ||
                requestsCollision) &&
            (!requestsRendering ||
                HasValidProjection);

        public CelestialSurfaceObserverState(
            string observerId,
            DoubleVector3 bodyRelativePositionMeters,
            double verticalFieldOfViewDegrees,
            int viewportHeightPixels,
            bool requestsRendering,
            bool requestsCollision,
            float priority = 1.0f)
        {
            this.observerId = observerId;
            this.bodyRelativePositionMeters =
                bodyRelativePositionMeters;
            this.verticalFieldOfViewDegrees =
                verticalFieldOfViewDegrees;
            this.viewportHeightPixels =
                viewportHeightPixels;
            this.requestsRendering =
                requestsRendering;
            this.requestsCollision =
                requestsCollision;
            this.priority = priority;
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}
