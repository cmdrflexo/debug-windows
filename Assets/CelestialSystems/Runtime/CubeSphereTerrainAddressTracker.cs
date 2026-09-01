/*
 * Tracks a round body's primary cube-sphere terrain tile and the adjacent face tiles needed near edges and corners.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class CubeSphereTerrainAddressTracker :
        MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private GePlanetSurfaceFrame surfaceFrame;

        [Header("Resolved Definition Settings")]
        [SerializeField]
        private double planetRadiusMeters;

        [SerializeField]
        private double tileSizeMeters;

        [SerializeField]
        private double adjacentPreloadDistanceMeters;

        [Header("Runtime")]
        [SerializeField]
        private bool hasPrimaryTileAddress;

        [SerializeField]
        private CubeSphereTileAddress primaryTileAddress;

        [SerializeField]
        private bool hasUAdjacentTileAddress;

        [SerializeField]
        private CubeSphereEdge uAdjacentEdge;

        [SerializeField]
        private CubeSphereTileAddress uAdjacentTileAddress;

        [SerializeField]
        private bool hasVAdjacentTileAddress;

        [SerializeField]
        private CubeSphereEdge vAdjacentEdge;

        [SerializeField]
        private CubeSphereTileAddress vAdjacentTileAddress;

        public double TileSizeMeters =>
            tileSizeMeters;

        public double AdjacentPreloadDistanceMeters =>
            adjacentPreloadDistanceMeters;

        public bool HasPrimaryTileAddress =>
            hasPrimaryTileAddress;

        public CubeSphereTileAddress PrimaryTileAddress =>
            primaryTileAddress;

        public bool HasUAdjacentTileAddress =>
            hasUAdjacentTileAddress;

        public CubeSphereEdge UAdjacentEdge =>
            uAdjacentEdge;

        public CubeSphereTileAddress UAdjacentTileAddress =>
            uAdjacentTileAddress;

        public bool HasVAdjacentTileAddress =>
            hasVAdjacentTileAddress;

        public CubeSphereEdge VAdjacentEdge =>
            vAdjacentEdge;

        public CubeSphereTileAddress VAdjacentTileAddress =>
            vAdjacentTileAddress;

        private void Reset()
        {
            surfaceFrame =
                GetComponent<GePlanetSurfaceFrame>();
        }

        private void Start()
        {
            if (surfaceFrame == null)
            {
                Debug.LogError(
                    "The terrain address tracker requires a round-body surface frame.",
                    this);
                return;
            }

            if (!RefreshDefinitionSettings())
            {
                Debug.LogError(
                    "The terrain address tracker requires valid Round MapMagic settings from the surface frame's body definition.",
                    this);
            }
        }

        private void LateUpdate()
        {
            ClearRuntimeAddresses();

            if (!RefreshDefinitionSettings() ||
                !surfaceFrame.HasAnchorAddress)
            {
                return;
            }

            var sourceAddress =
                surfaceFrame.AnchorAddress;

            if (!CubeSphereMapping.TryAddressToTileAddress(
                    sourceAddress,
                    planetRadiusMeters,
                    tileSizeMeters,
                    out primaryTileAddress))
            {
                return;
            }

            hasPrimaryTileAddress = true;

            var proximity =
                surfaceFrame.AnchorFaceProximity;

            if (proximity.ClosestUEdgeMeters <=
                adjacentPreloadDistanceMeters)
            {
                uAdjacentEdge =
                    proximity.ClosestUEdge;
                hasUAdjacentTileAddress =
                    TryCreateAdjacentTileAddress(
                        sourceAddress,
                        uAdjacentEdge,
                        out uAdjacentTileAddress);
            }

            if (proximity.ClosestVEdgeMeters <=
                adjacentPreloadDistanceMeters)
            {
                vAdjacentEdge =
                    proximity.ClosestVEdge;
                hasVAdjacentTileAddress =
                    TryCreateAdjacentTileAddress(
                        sourceAddress,
                        vAdjacentEdge,
                        out vAdjacentTileAddress);
            }
        }

        private bool RefreshDefinitionSettings()
        {
            if (surfaceFrame == null ||
                surfaceFrame.BodyContext == null ||
                surfaceFrame.BodyContext.Definition == null ||
                surfaceFrame.BodyContext.ResolvedSurfaceSystem !=
                    CelestialSurfaceSystem.RoundMapMagic)
            {
                return false;
            }

            var definition =
                surfaceFrame.BodyContext.Definition;
            var roundSurface =
                definition.RoundMapMagicSurface;

            if (roundSurface == null)
            {
                return false;
            }

            planetRadiusMeters =
                definition.ReferenceRadiusMeters;
            tileSizeMeters =
                roundSurface.TileSizeMeters;
            adjacentPreloadDistanceMeters =
                roundSurface.AdjacentPreloadDistanceMeters;

            return ConfigurationIsValid();
        }

        private bool TryCreateAdjacentTileAddress(
            CubeSphereAddress sourceAddress,
            CubeSphereEdge edge,
            out CubeSphereTileAddress tileAddress)
        {
            var adjacentFace =
                CubeSphereTopology.GetAdjacentFace(
                    sourceAddress.Face,
                    edge);

            if (!CubeSphereMapping.TryAddressToFaceAddress(
                    sourceAddress,
                    adjacentFace,
                    out var adjacentAddress))
            {
                tileAddress = default;
                return false;
            }

            return CubeSphereMapping.TryAddressToTileAddress(
                adjacentAddress,
                planetRadiusMeters,
                tileSizeMeters,
                out tileAddress);
        }

        private bool ConfigurationIsValid()
        {
            return
                surfaceFrame != null &&
                IsFinite(
                    planetRadiusMeters) &&
                planetRadiusMeters > 0.0 &&
                IsFinite(
                    tileSizeMeters) &&
                tileSizeMeters > 0.0 &&
                IsFinite(
                    adjacentPreloadDistanceMeters) &&
                adjacentPreloadDistanceMeters >= 0.0;
        }

        private void ClearRuntimeAddresses()
        {
            hasPrimaryTileAddress = false;
            primaryTileAddress = default;
            hasUAdjacentTileAddress = false;
            uAdjacentEdge = default;
            uAdjacentTileAddress = default;
            hasVAdjacentTileAddress = false;
            vAdjacentEdge = default;
            vAdjacentTileAddress = default;
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
