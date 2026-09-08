/*
 * Provides a MapMagic source for seamless LIC fibers following a sphere-tangent flow field.
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
        name = "Spherical Contour Fibers",
        section = 1,
        colorType = typeof(MatrixWorld),
        iconName = "GeneratorIcons/Noise")]
    public sealed class RoundMapMagicSphericalContourFibersGenerator : Generator, IOutlet<MatrixWorld>
    {
        [Val("Seed Offset")]
        public int seedOffset;

        [Val("Flow Scale (m)")]
        public float regionSizeMeters = 180000000f;

        [Val("Streamline Samples")]
        public float bands = 24f;

        [Val("Fiber Width (m)")]
        public float distortionSizeMeters = 8000000f;

        [Val("Flow Complexity")]
        public float distortion = 0.35f;

        [Val("Sharpness")]
        public float sharpness = 1.2f;

        [Val("Output")]
        public SphericalContourFiberOutput outputMode = SphericalContourFiberOutput.Fibers;

        [Val("Intensity")]
        public float intensity = 1.0f;

        [Val("Offset")]
        public float offset;

        [Val("Preview Face")]
        public CubeSphereFace previewFace = CubeSphereFace.PositiveX;

        [Val("Preview Radius (m)")]
        public double previewRadiusMeters = 696340000.0;

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

                    if ((localX & 15) == 0 && stop != null && stop.stop) return;
                    var value = RoundMapMagicSphericalContourFibers.EvaluateNormalized(
                        direction, bodyRadiusMeters, unchecked(surfaceSeed + seedOffset),
                        regionSizeMeters, bands, distortionSizeMeters,
                        distortion, sharpness, outputMode);

                    matrix.arr[localZ * width + localX] =
                        Mathf.Clamp01((float)value * intensity + offset);
                }
            }

            if (stop != null && stop.stop) return;
            data.StoreProduct(this, matrix);
        }
    }
}
