/*
 * Stores reusable MapMagic generation and streaming settings for round celestial-body surfaces.
 */

using MapMagic.Nodes;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [CreateAssetMenu(
        fileName = "Round MapMagic Surface",
        menuName = "Celestial Systems/Surface Profiles/Round MapMagic")]
    public sealed class RoundMapMagicSurfaceDefinition :
        ScriptableObject
    {
        [SerializeField]
        private Graph graph;

        [SerializeField]
        private int surfaceSeed = 12345;

        [SerializeField]
        [Tooltip("Offsets generated MapMagic heights relative to the body's reference-radius datum. Negative values place terrain below the datum.")]
        private double elevationOffsetMeters;

        [SerializeField]
        private double tileSizeMeters =
            1000.0;

        [SerializeField]
        private double adjacentPreloadDistanceMeters =
            3000.0;

        [SerializeField]
        [Range(3, 257)]
        private int meshResolution = 65;

        [SerializeField]
        private Material material;

        [SerializeField]
        [Tooltip("Optional reusable appearance definition. Its ordered layers correspond to MapMagic surface-control outputs.")]
        private CelestialSurfaceDefinition surfaceAppearance;

        public Graph Graph =>
            graph;

        public int SurfaceSeed =>
            surfaceSeed;

        public double ElevationOffsetMeters =>
            elevationOffsetMeters;

        public double TileSizeMeters =>
            tileSizeMeters;

        public double AdjacentPreloadDistanceMeters =>
            adjacentPreloadDistanceMeters;

        public int MeshResolution =>
            meshResolution;

        public Material Material =>
            material;

        public CelestialSurfaceDefinition SurfaceAppearance =>
            surfaceAppearance;

        public bool HasValidSettings =>
            graph != null &&
            IsFinite(elevationOffsetMeters) &&
            IsFinite(tileSizeMeters) &&
            tileSizeMeters > 0.0 &&
            IsFinite(
                adjacentPreloadDistanceMeters) &&
            adjacentPreloadDistanceMeters >= 0.0 &&
            meshResolution >= 3 &&
            (surfaceAppearance == null ||
                surfaceAppearance.HasValidSettings);

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
