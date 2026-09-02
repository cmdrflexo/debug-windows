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

        public bool HasValidSphericalContext =>
            IsFinite(PlanetRadiusMeters) &&
            PlanetRadiusMeters > 0.0;

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
