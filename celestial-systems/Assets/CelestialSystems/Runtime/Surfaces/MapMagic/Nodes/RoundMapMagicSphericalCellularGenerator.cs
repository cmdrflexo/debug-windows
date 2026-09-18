/*
 * Provides a MapMagic cellular matrix source that remains continuous across cube-sphere faces and tiles.
 */

using System;
using Den.Tools.GUI;
using Den.Tools.Matrices;
using MapMagic.Nodes;
using MapMagic.Products;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [Serializable]
    [GeneratorMenu(
        menu = "Map/Initial",
        name = "Spherical Cellular",
        section = 1,
        colorType = typeof(MatrixWorld),
        iconName = "GeneratorIcons/Noise")]
    public sealed class RoundMapMagicSphericalCellularGenerator : Generator, IOutlet<MatrixWorld>
    {
        [Val("Seed Offset")]
        public int seedOffset;

        [Val("Cell Size (m)")]
        public float cellSizeMeters = 1000000.0f;

        [Val("Jitter")]
        public float jitter = 0.75f;

        [Val("Output Mode")]
        public SphericalCellularOutputMode outputMode = SphericalCellularOutputMode.CellInterior;

        [Val("Intensity")]
        public float intensity = 1.0f;

        [Val("Offset")]
        public float offset;

        [Val("Preview Face")]
        public CubeSphereFace previewFace = CubeSphereFace.PositiveX;

        [Val("Preview Radius (m)")]
        public double previewRadiusMeters = 6371000.0;

        [Val("Preview Surface Seed")]
        public int previewSurfaceSeed = 12345;

        public override (string, int) GetCodeFileLine()
        {
            return GetCodeFileLineBase();
        }

        public override void Generate(TileData data, StopToken stop)
        {
            if (stop != null && stop.stop) return;

            if (!enabled)
            {
                data.StoreProduct(this, null);
                return;
            }

            var sphericalData = data.Root as RoundMapMagicSphericalTileData;
            var face = sphericalData != null && sphericalData.HasValidSphericalContext
                ? sphericalData.Face
                : previewFace;
            var bodyRadiusMeters = sphericalData != null && sphericalData.HasValidSphericalContext
                ? sphericalData.PlanetRadiusMeters
                : previewRadiusMeters;
            var surfaceSeed = sphericalData != null && sphericalData.HasValidSphericalContext
                ? sphericalData.SurfaceSeed
                : previewSurfaceSeed;
            var matrix = new MatrixWorld(
                data.area.full.rect,
                data.area.full.worldPos,
                data.area.full.worldSize,
                data.globals.height);
            var width = matrix.rect.size.x;
            var height = matrix.rect.size.z;

            for (var localZ = 0; localZ < height; localZ++)
            {
                if (stop != null && stop.stop) return;
                var pixelZ = matrix.rect.offset.z + localZ;

                for (var localX = 0; localX < width; localX++)
                {
                    var pixelX = matrix.rect.offset.x + localX;
                    var mapWorldPosition = matrix.PixelToWorld(pixelX, pixelZ);
                    DoubleVector3 direction;

                    if (sphericalData == null ||
                        !sphericalData.TryMapPixelToDirection(
                            pixelX,
                            pixelZ,
                            data.area.active.rect.offset.x,
                            data.area.active.rect.offset.z,
                            data.area.active.rect.size.x,
                            data.area.active.rect.size.z,
                            out direction))
                    {
                        var address = new CubeSphereAddress(
                            face,
                            CubeSphereMapping.MetersToFaceCoordinate(mapWorldPosition.x, bodyRadiusMeters),
                            CubeSphereMapping.MetersToFaceCoordinate(-mapWorldPosition.z, bodyRadiusMeters),
                            0.0);
                        direction = CubeSphereMapping.AddressToDirection(address);
                    }

                    var value = RoundMapMagicSphericalCellular.EvaluateNormalized(
                        direction,
                        bodyRadiusMeters,
                        unchecked(surfaceSeed + seedOffset),
                        Math.Max(0.001, cellSizeMeters),
                        jitter,
                        outputMode);

                    matrix.arr[localZ * width + localX] =
                        Mathf.Clamp01((float)value * intensity + offset);
                }
            }

            if (stop != null && stop.stop) return;
            data.StoreProduct(this, matrix);
        }
    }
}
