/*
 * Provides a MapMagic latitude source evaluated from the shared cube-sphere body direction.
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
    [GeneratorMenu(menu = "Map/Initial", name = "Spherical Latitude", section = 1,
        colorType = typeof(MatrixWorld), iconName = "GeneratorIcons/Noise")]
    public sealed class RoundMapMagicSphericalLatitudeGenerator : Generator, IOutlet<MatrixWorld>
    {
        [Val("Axis")]
        public SphericalLatitudeAxis axis = SphericalLatitudeAxis.Y;

        [Val("Output")]
        public SphericalLatitudeOutput outputMode = SphericalLatitudeOutput.AbsoluteLatitude;

        [Val("Intensity")]
        public float intensity = 1;

        [Val("Offset")]
        public float offset;

        [Val("Preview Face")]
        public CubeSphereFace previewFace = CubeSphereFace.PositiveX;

        [Val("Preview Radius (m)")]
        public double previewRadiusMeters = 6371000;

        public override (string, int) GetCodeFileLine() { return GetCodeFileLineBase(); }

        public override void Generate(TileData data, StopToken stop)
        {
            if (stop != null && stop.stop) return;
            if (!enabled) { data.StoreProduct(this, null); return; }

            var sphericalData = data.Root as RoundMapMagicSphericalTileData;
            var face = sphericalData != null && sphericalData.HasValidSphericalContext
                ? sphericalData.Face : previewFace;
            var radius = sphericalData != null && sphericalData.HasValidSphericalContext
                ? sphericalData.PlanetRadiusMeters : previewRadiusMeters;
            var matrix = new MatrixWorld(data.area.full.rect, data.area.full.worldPos,
                data.area.full.worldSize, data.globals.height);
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
                    if (sphericalData == null || !sphericalData.TryMapPixelToDirection(
                        pixelX, pixelZ, data.area.active.rect.offset.x, data.area.active.rect.offset.z,
                        data.area.active.rect.size.x, data.area.active.rect.size.z, out direction))
                    {
                        var address = new CubeSphereAddress(face,
                            CubeSphereMapping.MetersToFaceCoordinate(mapWorldPosition.x, radius),
                            CubeSphereMapping.MetersToFaceCoordinate(-mapWorldPosition.z, radius), 0);
                        direction = CubeSphereMapping.AddressToDirection(address);
                    }
                    var value = RoundMapMagicSphericalLatitude.EvaluateNormalized(direction, axis, outputMode);
                    matrix.arr[localZ*width + localX] = Mathf.Clamp01((float)value*intensity + offset);
                }
            }
            if (stop != null && stop.stop) return;
            data.StoreProduct(this, matrix);
        }
    }
}
