/*
 * Provides a MapMagic matrix source whose values remain continuous across cube-sphere faces and tile boundaries.
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
        name = "Spherical Noise",
        section = 1,
        colorType = typeof(MatrixWorld),
        iconName = "GeneratorIcons/Noise")]
    public sealed class RoundMapMagicSphericalNoiseGenerator :
        Generator,
        IOutlet<MatrixWorld>
    {
        [Val("Seed Offset")]
        public int seedOffset;

        [Val("Feature Size (m)")]
        public float featureSizeMeters =
            200000.0f;

        [Val("Octaves")]
        public int octaves = 5;

        [Val("Persistence")]
        public float persistence = 0.5f;

        [Val("Intensity")]
        public float intensity = 1.0f;

        [Val("Offset")]
        public float offset;

        [Val("Preview Face")]
        public CubeSphereFace previewFace =
            CubeSphereFace.PositiveX;

        [Val("Preview Radius (m)")]
        public double previewRadiusMeters =
            6371000.0;

        [Val("Preview Surface Seed")]
        public int previewSurfaceSeed = 12345;

        public override (string, int) GetCodeFileLine()
        {
            return GetCodeFileLineBase();
        }

        public override void Generate(
            TileData data,
            StopToken stop)
        {
            if (stop != null &&
                stop.stop)
            {
                return;
            }

            if (!enabled)
            {
                data.StoreProduct(
                    this,
                    null);
                return;
            }

            var sphericalData =
                data.Root as
                    RoundMapMagicSphericalTileData;
            var face =
                sphericalData != null &&
                sphericalData.HasValidSphericalContext
                    ? sphericalData.Face
                    : previewFace;
            var planetRadiusMeters =
                sphericalData != null &&
                sphericalData.HasValidSphericalContext
                    ? sphericalData.PlanetRadiusMeters
                    : previewRadiusMeters;
            var surfaceSeed =
                sphericalData != null &&
                sphericalData.HasValidSphericalContext
                    ? sphericalData.SurfaceSeed
                    : previewSurfaceSeed;
            var matrix =
                new MatrixWorld(
                    data.area.full.rect,
                    data.area.full.worldPos,
                    data.area.full.worldSize,
                    data.globals.height);
            var width =
                matrix.rect.size.x;
            var height =
                matrix.rect.size.z;

            for (var localZ = 0;
                localZ < height;
                localZ++)
            {
                if (stop != null &&
                    stop.stop)
                {
                    return;
                }

                var pixelZ =
                    matrix.rect.offset.z +
                    localZ;

                for (var localX = 0;
                    localX < width;
                    localX++)
                {
                    var pixelX =
                        matrix.rect.offset.x +
                        localX;
                    var mapWorldPosition =
                        matrix.PixelToWorld(
                            pixelX,
                            pixelZ);
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
                        var address =
                            new CubeSphereAddress(
                                face,
                                CubeSphereMapping.MetersToFaceCoordinate(
                                    mapWorldPosition.x,
                                    planetRadiusMeters),
                                CubeSphereMapping.MetersToFaceCoordinate(
                                    -mapWorldPosition.z,
                                    planetRadiusMeters),
                                0.0);

                        direction =
                            CubeSphereMapping.AddressToDirection(
                                address);
                    }
                    var normalizedNoise =
                        RoundMapMagicSphericalNoise.EvaluateNormalized(
                            direction,
                            planetRadiusMeters,
                            unchecked(
                                surfaceSeed +
                                seedOffset),
                            Math.Max(
                                0.001,
                                featureSizeMeters),
                            octaves,
                            persistence);

                    matrix.arr[
                        localZ *
                            width +
                        localX] =
                        Mathf.Clamp01(
                            (float)normalizedNoise *
                                intensity +
                            offset);
                }
            }

            if (stop != null &&
                stop.stop)
            {
                return;
            }

            data.StoreProduct(
                this,
                matrix);
        }
    }
}
