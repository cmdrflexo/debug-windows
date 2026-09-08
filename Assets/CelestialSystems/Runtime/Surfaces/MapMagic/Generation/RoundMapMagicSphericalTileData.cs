/*
 * Carries cube-sphere body context through one direct MapMagic graph generation request.
 */

using System;
using MapMagic.Products;

namespace jcan.CelestialSystems
{
    public sealed class RoundMapMagicSphericalTileData :
        TileData
    {
        public CubeSphereFace Face { get; set; }

        public double PlanetRadiusMeters { get; set; }

        public int SurfaceSeed { get; set; }

        public double MapWorldOriginXMeters { get; set; }

        public double MapWorldOriginZMeters { get; set; }

        public double MapWorldSizeXMeters { get; set; }

        public double MapWorldSizeZMeters { get; set; }

        public bool HasValidSphericalContext =>
            IsFinite(PlanetRadiusMeters) &&
            PlanetRadiusMeters > 0.0;

        public bool HasExactMapWorldBounds =>
            IsFinite(MapWorldOriginXMeters) &&
            IsFinite(MapWorldOriginZMeters) &&
            IsFinite(MapWorldSizeXMeters) &&
            IsFinite(MapWorldSizeZMeters) &&
            MapWorldSizeXMeters > 0.0 &&
            MapWorldSizeZMeters > 0.0;

        public bool TryMapPixelToDirection(
            int pixelX,
            int pixelZ,
            int activePixelOffsetX,
            int activePixelOffsetZ,
            int activeResolutionX,
            int activeResolutionZ,
            out DoubleVector3 direction)
        {
            if (!HasExactMapWorldBounds ||
                activeResolutionX < 2 ||
                activeResolutionZ < 2)
            {
                direction = default;
                return false;
            }

            var normalizedX =
                (pixelX -
                    activePixelOffsetX) /
                (double)(
                    activeResolutionX -
                    1);
            var normalizedZ =
                (pixelZ -
                    activePixelOffsetZ) /
                (double)(
                    activeResolutionZ -
                    1);

            return TryMapWorldPositionToDirection(
                MapWorldOriginXMeters +
                    MapWorldSizeXMeters *
                    normalizedX,
                MapWorldOriginZMeters +
                    MapWorldSizeZMeters *
                    normalizedZ,
                out direction);
        }

        public bool TryMapWorldPositionToDirection(
            double mapWorldXMeters,
            double mapWorldZMeters,
            out DoubleVector3 direction)
        {
            if (!HasValidSphericalContext ||
                !IsFinite(mapWorldXMeters) ||
                !IsFinite(mapWorldZMeters))
            {
                direction = default;
                return false;
            }

            var address =
                new CubeSphereAddress(
                    Face,
                    CubeSphereMapping.MetersToFaceCoordinate(
                        mapWorldXMeters,
                        PlanetRadiusMeters),
                    CubeSphereMapping.MetersToFaceCoordinate(
                        -mapWorldZMeters,
                        PlanetRadiusMeters),
                    0.0);

            direction =
                CubeSphereMapping.AddressToDirection(
                    address);
            return
                IsFinite(direction.x) &&
                IsFinite(direction.y) &&
                IsFinite(direction.z);
        }

        private static bool IsFinite(double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
