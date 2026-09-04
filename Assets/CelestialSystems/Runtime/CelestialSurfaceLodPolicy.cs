/*
 * Stores the shared adaptive-surface resolution and error limits used by all observers and patch types.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [Serializable]
    public struct CelestialSurfaceLodPolicy
    {
        public const int DefaultPatchResolution = 33;
        public const int DefaultMaximumLevel = 20;
        public const double DefaultMaximumScreenErrorPixels = 2.0;
        public const double DefaultHysteresisFraction = 0.15;

        [SerializeField]
        private int patchResolution;

        [SerializeField]
        private int minimumLevel;

        [SerializeField]
        private int maximumLevel;

        [SerializeField]
        private int collisionMaximumLevel;

        [SerializeField]
        private double maximumScreenErrorPixels;

        [SerializeField]
        private double hysteresisFraction;

        [SerializeField]
        private int maximumNeighborLevelDifference;

        public int PatchResolution =>
            patchResolution;

        public int MinimumLevel =>
            minimumLevel;

        public int MaximumLevel =>
            maximumLevel;

        public int CollisionMaximumLevel =>
            collisionMaximumLevel;

        public double MaximumScreenErrorPixels =>
            maximumScreenErrorPixels;

        public double HysteresisFraction =>
            hysteresisFraction;

        public int MaximumNeighborLevelDifference =>
            maximumNeighborLevelDifference;

        public bool IsValid =>
            CubeSpherePatchGrid.HasNestedResolution(
                patchResolution) &&
            minimumLevel >= 0 &&
            minimumLevel <= maximumLevel &&
            maximumLevel <=
                CubeSpherePatchAddress.MaximumLevel &&
            collisionMaximumLevel >=
                minimumLevel &&
            collisionMaximumLevel <=
                maximumLevel &&
            IsFinite(maximumScreenErrorPixels) &&
            maximumScreenErrorPixels > 0.0 &&
            IsFinite(hysteresisFraction) &&
            hysteresisFraction >= 0.0 &&
            hysteresisFraction < 1.0 &&
            maximumNeighborLevelDifference >= 0 &&
            maximumNeighborLevelDifference <= 1;

        public CelestialSurfaceLodPolicy(
            int patchResolution,
            int minimumLevel,
            int maximumLevel,
            int collisionMaximumLevel,
            double maximumScreenErrorPixels,
            double hysteresisFraction,
            int maximumNeighborLevelDifference)
        {
            this.patchResolution =
                patchResolution;
            this.minimumLevel =
                minimumLevel;
            this.maximumLevel =
                maximumLevel;
            this.collisionMaximumLevel =
                collisionMaximumLevel;
            this.maximumScreenErrorPixels =
                maximumScreenErrorPixels;
            this.hysteresisFraction =
                hysteresisFraction;
            this.maximumNeighborLevelDifference =
                maximumNeighborLevelDifference;
        }

        public static CelestialSurfaceLodPolicy CreateDefault()
        {
            return new CelestialSurfaceLodPolicy(
                DefaultPatchResolution,
                0,
                DefaultMaximumLevel,
                DefaultMaximumLevel,
                DefaultMaximumScreenErrorPixels,
                DefaultHysteresisFraction,
                1);
        }

        public double EstimateProjectedErrorPixels(
            double geometricErrorMeters,
            double distanceMeters,
            CelestialSurfaceObserverState observer)
        {
            if (!IsValid ||
                !observer.IsValid ||
                !observer.RequestsRendering ||
                !observer.HasValidProjection ||
                !IsFinite(geometricErrorMeters) ||
                geometricErrorMeters < 0.0 ||
                !IsFinite(distanceMeters) ||
                distanceMeters <= 0.0)
            {
                return double.PositiveInfinity;
            }

            var halfFieldOfViewRadians =
                observer.VerticalFieldOfViewDegrees *
                Math.PI /
                360.0;
            var focalLengthPixels =
                observer.ViewportHeightPixels /
                (2.0 *
                    Math.Tan(
                        halfFieldOfViewRadians));

            return
                geometricErrorMeters /
                distanceMeters *
                focalLengthPixels;
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
