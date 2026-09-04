/*
 * Translates one canonical quadtree patch into the same MapMagic world bounds and face orientation used by the legacy samplers.
 */

using System;

namespace jcan.CelestialSystems
{
    public sealed class RoundMapMagicSurfacePatchRequest
    {
        public CubeSpherePatchAddress Address { get; }

        public int Resolution { get; }

        public double PlanetRadiusMeters { get; }

        public int SurfaceSeed { get; }

        public double ElevationOffsetMeters { get; }

        public double MapWorldOriginXMeters { get; }

        public double MapWorldOriginZMeters { get; }

        public double MapWorldSizeXMeters { get; }

        public double MapWorldSizeZMeters { get; }

        public bool IsValid =>
            Address.IsValid &&
            CubeSpherePatchGrid.HasNestedResolution(
                Resolution) &&
            IsFinite(PlanetRadiusMeters) &&
            PlanetRadiusMeters > 0.0 &&
            IsFinite(ElevationOffsetMeters) &&
            IsFinite(MapWorldOriginXMeters) &&
            IsFinite(MapWorldOriginZMeters) &&
            IsFinite(MapWorldSizeXMeters) &&
            IsFinite(MapWorldSizeZMeters) &&
            MapWorldSizeXMeters > 0.0 &&
            MapWorldSizeZMeters > 0.0;

        private RoundMapMagicSurfacePatchRequest(
            CubeSpherePatchAddress address,
            int resolution,
            double planetRadiusMeters,
            int surfaceSeed,
            double elevationOffsetMeters,
            double mapWorldOriginXMeters,
            double mapWorldOriginZMeters,
            double mapWorldSizeXMeters,
            double mapWorldSizeZMeters)
        {
            Address = address;
            Resolution = resolution;
            PlanetRadiusMeters =
                planetRadiusMeters;
            SurfaceSeed = surfaceSeed;
            ElevationOffsetMeters =
                elevationOffsetMeters;
            MapWorldOriginXMeters =
                mapWorldOriginXMeters;
            MapWorldOriginZMeters =
                mapWorldOriginZMeters;
            MapWorldSizeXMeters =
                mapWorldSizeXMeters;
            MapWorldSizeZMeters =
                mapWorldSizeZMeters;
        }

        public static bool TryCreate(
            CelestialBodyDefinition bodyDefinition,
            CubeSpherePatchAddress address,
            int resolution,
            out RoundMapMagicSurfacePatchRequest request,
            out string error)
        {
            request = null;

            if (bodyDefinition == null ||
                bodyDefinition.ResolvedSurfaceSystem !=
                    CelestialSurfaceSystem.RoundMapMagic ||
                bodyDefinition.RoundMapMagicSurface == null ||
                !bodyDefinition.RoundMapMagicSurface.HasValidSettings)
            {
                error =
                    "A valid Round MapMagic body definition is required to create a patch request.";
                return false;
            }

            if (!address.IsValid)
            {
                error =
                    "A valid cube-sphere patch address is required.";
                return false;
            }

            if (!CubeSpherePatchGrid.HasNestedResolution(
                    resolution))
            {
                error =
                    "Patch resolution must have a power-of-two interval count.";
                return false;
            }

            var radiusMeters =
                bodyDefinition.ReferenceRadiusMeters;
            var surfaceDefinition =
                bodyDefinition.RoundMapMagicSurface;
            var minimumUMeters =
                CubeSphereMapping.FaceCoordinateToMeters(
                    address.MinimumU,
                    radiusMeters);
            var maximumUMeters =
                CubeSphereMapping.FaceCoordinateToMeters(
                    address.MaximumU,
                    radiusMeters);
            var minimumVMeters =
                CubeSphereMapping.FaceCoordinateToMeters(
                    address.MinimumV,
                    radiusMeters);
            var maximumVMeters =
                CubeSphereMapping.FaceCoordinateToMeters(
                    address.MaximumV,
                    radiusMeters);

            request =
                new RoundMapMagicSurfacePatchRequest(
                    address,
                    resolution,
                    radiusMeters,
                    surfaceDefinition.SurfaceSeed,
                    surfaceDefinition.ElevationOffsetMeters,
                    minimumUMeters,
                    -maximumVMeters,
                    maximumUMeters -
                        minimumUMeters,
                    maximumVMeters -
                        minimumVMeters);
            error = string.Empty;
            return request.IsValid;
        }

        public int PatchSampleYToMapPixelZ(
            int sampleY)
        {
            ValidatePixelCoordinate(
                sampleY,
                nameof(sampleY));
            return
                Resolution - 1 -
                sampleY;
        }

        public int MapPixelZToPatchSampleY(
            int pixelZ)
        {
            ValidatePixelCoordinate(
                pixelZ,
                nameof(pixelZ));
            return
                Resolution - 1 -
                pixelZ;
        }

        public bool TryMapPixelToDirection(
            int pixelX,
            int pixelZ,
            out DoubleVector3 direction)
        {
            if (!IsValid ||
                pixelX < 0 ||
                pixelX >= Resolution ||
                pixelZ < 0 ||
                pixelZ >= Resolution)
            {
                direction = default;
                return false;
            }

            var normalizedX =
                pixelX /
                (double)(Resolution - 1);
            var normalizedZ =
                pixelZ /
                (double)(Resolution - 1);
            var mapWorldX =
                MapWorldOriginXMeters +
                MapWorldSizeXMeters *
                normalizedX;
            var mapWorldZ =
                MapWorldOriginZMeters +
                MapWorldSizeZMeters *
                normalizedZ;
            var faceU =
                CubeSphereMapping.MetersToFaceCoordinate(
                    mapWorldX,
                    PlanetRadiusMeters);
            var faceV =
                CubeSphereMapping.MetersToFaceCoordinate(
                    -mapWorldZ,
                    PlanetRadiusMeters);

            direction =
                CubeSphereMapping.AddressToDirection(
                    new CubeSphereAddress(
                        Address.Face,
                        faceU,
                        faceV,
                        0.0));
            return
                IsFinite(direction.x) &&
                IsFinite(direction.y) &&
                IsFinite(direction.z);
        }

        private void ValidatePixelCoordinate(
            int coordinate,
            string parameterName)
        {
            if (coordinate < 0 ||
                coordinate >= Resolution)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName);
            }
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
