/*
 * Stores reusable rendering-quality limits for the custom meshes built from a Round MapMagic surface.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [CreateAssetMenu(
        fileName = "Round MapMagic Surface Quality",
        menuName = "Celestial Systems/Quality Profiles/Round MapMagic Surface")]
    public sealed class RoundMapMagicSurfaceQualityProfile :
        ScriptableObject
    {
        [Header("Local Detail")]
        [SerializeField]
        [Range(3, 257)]
        private int localMeshResolution = 65;

        [SerializeField]
        private double localCoverageRadiusMeters =
            3000.0;

        [SerializeField]
        [Tooltip("Radius around the active anchor that receives close-tile colliders, independent of the Local visual coverage radius. Zero disables collider coverage.")]
        private double localColliderCoverageRadiusMeters =
            2000.0;

        [SerializeField]
        private double localActivationAltitudeMeters =
            25000.0;

        [SerializeField]
        private double localReleaseAltitudeMeters =
            30000.0;

        [Header("Mid Detail")]
        [SerializeField]
        [Range(3, 257)]
        private int midMeshResolution = 17;

        [SerializeField]
        [Range(1, 64)]
        private int midTileSizeMultiplier = 8;

        [SerializeField]
        private double midCoverageRadiusMeters =
            60000.0;

        [SerializeField]
        private double midActivationAltitudeMeters =
            60000.0;

        [SerializeField]
        private double midReleaseAltitudeMeters =
            75000.0;

        [Header("Transitions")]
        [SerializeField]
        private double transitionOverlapMeters =
            2000.0;

        [SerializeField]
        private double customSurfaceHandoffAltitudeMeters =
            750.0;

        [SerializeField]
        private double customSurfaceReleaseAltitudeMeters =
            1000.0;

        public int LocalMeshResolution =>
            localMeshResolution;

        public double LocalCoverageRadiusMeters =>
            localCoverageRadiusMeters;

        public double LocalColliderCoverageRadiusMeters =>
            localColliderCoverageRadiusMeters;

        public double LocalActivationAltitudeMeters =>
            localActivationAltitudeMeters;

        public double LocalReleaseAltitudeMeters =>
            localReleaseAltitudeMeters;

        public int MidMeshResolution =>
            midMeshResolution;

        public int MidTileSizeMultiplier =>
            midTileSizeMultiplier;

        public double MidCoverageRadiusMeters =>
            midCoverageRadiusMeters;

        public double MidActivationAltitudeMeters =>
            midActivationAltitudeMeters;

        public double MidReleaseAltitudeMeters =>
            midReleaseAltitudeMeters;

        public double TransitionOverlapMeters =>
            transitionOverlapMeters;

        public double CustomSurfaceHandoffAltitudeMeters =>
            customSurfaceHandoffAltitudeMeters;

        public double CustomSurfaceReleaseAltitudeMeters =>
            customSurfaceReleaseAltitudeMeters;

        public bool HasValidSettings =>
            HasPowerOfTwoIntervals(
                localMeshResolution) &&
            IsFinite(
                localCoverageRadiusMeters) &&
            localCoverageRadiusMeters >
                0.0 &&
            IsFinite(
                localColliderCoverageRadiusMeters) &&
            localColliderCoverageRadiusMeters >=
                0.0 &&
            IsFinite(
                localActivationAltitudeMeters) &&
            localActivationAltitudeMeters >=
                0.0 &&
            IsFinite(
                localReleaseAltitudeMeters) &&
            localReleaseAltitudeMeters >=
                localActivationAltitudeMeters &&
            HasPowerOfTwoIntervals(
                midMeshResolution) &&
            midTileSizeMultiplier >=
                1 &&
            IsFinite(
                midCoverageRadiusMeters) &&
            midCoverageRadiusMeters >=
                localCoverageRadiusMeters &&
            IsFinite(
                midActivationAltitudeMeters) &&
            midActivationAltitudeMeters >=
                0.0 &&
            IsFinite(
                midReleaseAltitudeMeters) &&
            midReleaseAltitudeMeters >=
                midActivationAltitudeMeters &&
            IsFinite(
                transitionOverlapMeters) &&
            transitionOverlapMeters >=
                0.0 &&
            IsFinite(
                customSurfaceHandoffAltitudeMeters) &&
            customSurfaceHandoffAltitudeMeters >=
                0.0 &&
            IsFinite(
                customSurfaceReleaseAltitudeMeters) &&
            customSurfaceReleaseAltitudeMeters >=
                customSurfaceHandoffAltitudeMeters;

        private static bool HasPowerOfTwoIntervals(
            int resolution)
        {
            var intervalCount =
                resolution -
                1;

            return
                intervalCount >=
                    2 &&
                (intervalCount &
                    (intervalCount - 1)) ==
                    0;
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
