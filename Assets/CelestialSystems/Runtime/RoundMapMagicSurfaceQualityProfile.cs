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
        [Header("Unified Adaptive Surface")]
        [SerializeField]
        [Range(3, 257)]
        private int adaptivePatchResolution = 33;

        [SerializeField]
        [Range(0, CubeSpherePatchAddress.MaximumLevel)]
        private int adaptiveMinimumLevel;

        [SerializeField]
        [Range(0, CubeSpherePatchAddress.MaximumLevel)]
        private int adaptiveMaximumLevel = 20;

        [SerializeField]
        [Range(0, CubeSpherePatchAddress.MaximumLevel)]
        private int adaptiveCollisionMaximumLevel = 20;

        [SerializeField]
        [Min(0.01f)]
        private double adaptiveMaximumScreenErrorPixels = 2.0;

        [SerializeField]
        [Range(0.0f, 0.95f)]
        private double adaptiveLodHysteresisFraction = 0.15;

        [SerializeField]
        [Range(16, 4096)]
        private int adaptiveMaximumPatchCount = 768;

        [SerializeField]
        [Range(1, 32)]
        private int adaptiveMaximumMeshBuildsPerFrame = 4;

        [SerializeField]
        [Range(0, 16)]
        private int adaptiveGenerationMargins = 2;

        [SerializeField]
        [Tooltip("Requests all six root patches at background priority as soon as a body registers with the shared cache.")]
        private bool adaptivePrewarmCoarseSurface = true;

        [SerializeField]
        [Range(0.0f, 5.0f)]
        [Tooltip("Seconds used to morph child geometry between parent and child elevations during adaptive LOD changes.")]
        private float adaptiveLodMorphDurationSeconds =
            0.35f;

        [SerializeField]
        [Range(0.001f, 0.25f)]
        private float adaptiveSkirtDepthCellFraction = 0.02f;

        [SerializeField]
        [Min(0.01f)]
        private float adaptiveMinimumSkirtDepthMeters = 1.0f;

        [Header("Unified Collision")]
        [SerializeField] private bool adaptiveCollisionEnabled = true;
        [SerializeField] private bool adaptiveCollisionFollowsCamera = true;
        [SerializeField, Min(1.0f)] private float adaptiveCollisionCoverageRadiusMeters = 256.0f;
        [SerializeField, Min(0.0f)] private float adaptiveCollisionPrefetchMarginMeters = 128.0f;
        [SerializeField, Min(0.1f)] private float adaptiveCollisionSampleSpacingMeters = 8.0f;
        [SerializeField, Min(0.0f)] private float adaptiveCollisionActivationAltitudeMeters = 2500.0f;
        [SerializeField, Range(4, 512)] private int adaptiveCollisionMaximumPatchCount = 128;
        [SerializeField, Range(1, 8)] private int adaptiveCollisionMeshBuildsPerFrame = 2;
        [SerializeField, Min(0.0f)] private float adaptiveCollisionRetirementDelaySeconds = 1.0f;
        [SerializeField, Range(0, 31)] private int adaptiveCollisionLayer;

        public bool AdaptiveCollisionEnabled => adaptiveCollisionEnabled;
        public bool AdaptiveCollisionFollowsCamera => adaptiveCollisionFollowsCamera;
        public float AdaptiveCollisionCoverageRadiusMeters => adaptiveCollisionCoverageRadiusMeters;
        public float AdaptiveCollisionPrefetchMarginMeters => adaptiveCollisionPrefetchMarginMeters;
        public float AdaptiveCollisionSampleSpacingMeters => adaptiveCollisionSampleSpacingMeters;
        public float AdaptiveCollisionActivationAltitudeMeters => adaptiveCollisionActivationAltitudeMeters;
        public int AdaptiveCollisionMaximumPatchCount => adaptiveCollisionMaximumPatchCount;
        public int AdaptiveCollisionMeshBuildsPerFrame => adaptiveCollisionMeshBuildsPerFrame;
        public float AdaptiveCollisionRetirementDelaySeconds => adaptiveCollisionRetirementDelaySeconds;
        public int AdaptiveCollisionLayer => adaptiveCollisionLayer;

        public bool HasValidAdaptiveCollisionSettings =>
            IsFinite(adaptiveCollisionCoverageRadiusMeters) && adaptiveCollisionCoverageRadiusMeters >= 1.0f &&
            IsFinite(adaptiveCollisionPrefetchMarginMeters) && adaptiveCollisionPrefetchMarginMeters >= 0.0f &&
            IsFinite(adaptiveCollisionSampleSpacingMeters) && adaptiveCollisionSampleSpacingMeters >= 0.1f &&
            IsFinite(adaptiveCollisionActivationAltitudeMeters) && adaptiveCollisionActivationAltitudeMeters >= 0.0f &&
            adaptiveCollisionMaximumPatchCount >= 4 && adaptiveCollisionMaximumPatchCount <= 512 &&
            adaptiveCollisionMeshBuildsPerFrame >= 1 && adaptiveCollisionMeshBuildsPerFrame <= 8 &&
            IsFinite(adaptiveCollisionRetirementDelaySeconds) && adaptiveCollisionRetirementDelaySeconds >= 0.0f &&
            adaptiveCollisionLayer >= 0 && adaptiveCollisionLayer <= 31;

        [Header("Local Detail")]
        [SerializeField]
        [Range(3, 257)]
        private int localMeshResolution = 65;

        [SerializeField]
        private double localCoverageRadiusMeters =
            3000.0;

        [SerializeField]
        [Tooltip("Additional Local sample-cache distance beyond the visible coverage radius.")]
        private double localPrefetchMarginMeters =
            1000.0;

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
        [Tooltip("Additional Mid sample-cache distance beyond the visible coverage radius.")]
        private double midPrefetchMarginMeters =
            8000.0;

        [SerializeField]
        private double midActivationAltitudeMeters =
            60000.0;

        [SerializeField]
        private double midReleaseAltitudeMeters =
            75000.0;

        [Header("Transitions")]
        [SerializeField]
        [Tooltip("Width of the complementary dithered blend between Local and Mid terrain.")]
        private double localMidBlendWidthMeters =
            1000.0;

        [SerializeField]
        [Tooltip("Seconds taken for a newly visible custom terrain tile to dither into view. Zero disables tile fading.")]
        private float tileFadeDurationSeconds =
            0.3f;

        [SerializeField]
        private double transitionOverlapMeters =
            2000.0;

        [SerializeField]
        private double customSurfaceHandoffAltitudeMeters =
            750.0;

        [SerializeField]
        private double customSurfaceReleaseAltitudeMeters =
            1000.0;

        public CelestialSurfaceLodPolicy AdaptiveLodPolicy =>
            new CelestialSurfaceLodPolicy(
                adaptivePatchResolution,
                adaptiveMinimumLevel,
                adaptiveMaximumLevel,
                adaptiveCollisionMaximumLevel,
                adaptiveMaximumScreenErrorPixels,
                adaptiveLodHysteresisFraction,
                1);

        public int AdaptiveMaximumPatchCount =>
            adaptiveMaximumPatchCount;

        public int AdaptiveMaximumMeshBuildsPerFrame =>
            adaptiveMaximumMeshBuildsPerFrame;

        public int AdaptiveGenerationMargins =>
            adaptiveGenerationMargins;

        public bool AdaptivePrewarmCoarseSurface =>
            adaptivePrewarmCoarseSurface;

        public float AdaptiveLodMorphDurationSeconds =>
            adaptiveLodMorphDurationSeconds;

        public float AdaptiveSkirtDepthCellFraction =>
            adaptiveSkirtDepthCellFraction;

        public float AdaptiveMinimumSkirtDepthMeters =>
            adaptiveMinimumSkirtDepthMeters;

        public int LocalMeshResolution =>
            localMeshResolution;

        public double LocalCoverageRadiusMeters =>
            localCoverageRadiusMeters;

        public double LocalPrefetchMarginMeters =>
            localPrefetchMarginMeters;

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

        public double MidPrefetchMarginMeters =>
            midPrefetchMarginMeters;

        public double MidActivationAltitudeMeters =>
            midActivationAltitudeMeters;

        public double MidReleaseAltitudeMeters =>
            midReleaseAltitudeMeters;

        public double TransitionOverlapMeters =>
            transitionOverlapMeters;

        public double LocalMidBlendWidthMeters =>
            localMidBlendWidthMeters;

        public float TileFadeDurationSeconds =>
            tileFadeDurationSeconds;

        public double CustomSurfaceHandoffAltitudeMeters =>
            customSurfaceHandoffAltitudeMeters;

        public double CustomSurfaceReleaseAltitudeMeters =>
            customSurfaceReleaseAltitudeMeters;

        public bool HasValidSettings =>
            AdaptiveLodPolicy.IsValid &&
            HasValidAdaptiveCollisionSettings &&
            adaptiveMaximumPatchCount >= 16 &&
            adaptiveMaximumMeshBuildsPerFrame >= 1 &&
            adaptiveGenerationMargins >= 0 &&
            adaptiveGenerationMargins <= 16 &&
            IsFinite(
                adaptiveLodMorphDurationSeconds) &&
            adaptiveLodMorphDurationSeconds >= 0.0f &&
            adaptiveLodMorphDurationSeconds <= 5.0f &&
            IsFinite(
                adaptiveSkirtDepthCellFraction) &&
            adaptiveSkirtDepthCellFraction >= 0.001f &&
            adaptiveSkirtDepthCellFraction <= 0.25f &&
            IsFinite(
                adaptiveMinimumSkirtDepthMeters) &&
            adaptiveMinimumSkirtDepthMeters >= 0.01f &&
            HasPowerOfTwoIntervals(
                localMeshResolution) &&
            IsFinite(
                localCoverageRadiusMeters) &&
            localCoverageRadiusMeters >
                0.0 &&
            IsFinite(
                localPrefetchMarginMeters) &&
            localPrefetchMarginMeters >=
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
                midPrefetchMarginMeters) &&
            midPrefetchMarginMeters >=
                0.0 &&
            IsFinite(
                midActivationAltitudeMeters) &&
            midActivationAltitudeMeters >=
                0.0 &&
            IsFinite(
                midReleaseAltitudeMeters) &&
            midReleaseAltitudeMeters >=
                midActivationAltitudeMeters &&
            IsFinite(
                localMidBlendWidthMeters) &&
            localMidBlendWidthMeters >=
                0.0 &&
            localMidBlendWidthMeters <=
                localCoverageRadiusMeters &&
            IsFinite(
                tileFadeDurationSeconds) &&
            tileFadeDurationSeconds >=
                0.0f &&
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

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}
