/*
 * Converts between double-precision planet-relative vectors and canonical equiangular cube-sphere addresses.
 */

using System;

namespace jcan.CelestialSystems
{
    public static class CubeSphereMapping
    {
        public const double HalfFaceAngleRadians =
            Math.PI * 0.25;

        public static bool TryPlanetRelativePositionToAddress(
            DoubleVector3 planetRelativePositionMeters,
            double planetRadiusMeters,
            out CubeSphereAddress address)
        {
            if (!IsFinite(planetRadiusMeters) ||
                planetRadiusMeters <= 0.0)
            {
                address = default;
                return false;
            }

            var distanceMeters =
                Magnitude(planetRelativePositionMeters);

            if (!IsFinite(distanceMeters) ||
                distanceMeters <= 0.0)
            {
                address = default;
                return false;
            }

            return TryDirectionToAddress(
                planetRelativePositionMeters,
                distanceMeters - planetRadiusMeters,
                out address);
        }

        public static bool TryDirectionToAddress(
            DoubleVector3 direction,
            double altitudeMeters,
            out CubeSphereAddress address)
        {
            var absoluteX = Math.Abs(direction.x);
            var absoluteY = Math.Abs(direction.y);
            var absoluteZ = Math.Abs(direction.z);
            var largestComponent =
                Math.Max(
                    absoluteX,
                    Math.Max(absoluteY, absoluteZ));

            if (!IsFinite(largestComponent) ||
                largestComponent <= 0.0 ||
                !IsFinite(altitudeMeters))
            {
                address = default;
                return false;
            }

            CubeSphereFace face;
            double tangentU;
            double tangentV;

            if (absoluteX >= absoluteY &&
                absoluteX >= absoluteZ)
            {
                if (direction.x >= 0.0)
                {
                    face = CubeSphereFace.PositiveX;
                    tangentU = -direction.z / absoluteX;
                    tangentV = direction.y / absoluteX;
                }
                else
                {
                    face = CubeSphereFace.NegativeX;
                    tangentU = direction.z / absoluteX;
                    tangentV = direction.y / absoluteX;
                }
            }
            else if (absoluteY >= absoluteZ)
            {
                if (direction.y >= 0.0)
                {
                    face = CubeSphereFace.PositiveY;
                    tangentU = direction.x / absoluteY;
                    tangentV = -direction.z / absoluteY;
                }
                else
                {
                    face = CubeSphereFace.NegativeY;
                    tangentU = direction.x / absoluteY;
                    tangentV = direction.z / absoluteY;
                }
            }
            else
            {
                if (direction.z >= 0.0)
                {
                    face = CubeSphereFace.PositiveZ;
                    tangentU = direction.x / absoluteZ;
                    tangentV = direction.y / absoluteZ;
                }
                else
                {
                    face = CubeSphereFace.NegativeZ;
                    tangentU = -direction.x / absoluteZ;
                    tangentV = direction.y / absoluteZ;
                }
            }

            var faceU =
                Math.Atan(ClampUnit(tangentU)) /
                HalfFaceAngleRadians;
            var faceV =
                Math.Atan(ClampUnit(tangentV)) /
                HalfFaceAngleRadians;

            address = new CubeSphereAddress(
                face,
                ClampUnit(faceU),
                ClampUnit(faceV),
                altitudeMeters);
            return true;
        }

        public static DoubleVector3 AddressToDirection(
            CubeSphereAddress address)
        {
            var tangentU = Math.Tan(
                address.FaceU *
                HalfFaceAngleRadians);
            var tangentV = Math.Tan(
                address.FaceV *
                HalfFaceAngleRadians);
            DoubleVector3 cubeVector;

            switch (address.Face)
            {
                case CubeSphereFace.PositiveX:
                    cubeVector = new DoubleVector3(
                        1.0,
                        tangentV,
                        -tangentU);
                    break;

                case CubeSphereFace.NegativeX:
                    cubeVector = new DoubleVector3(
                        -1.0,
                        tangentV,
                        tangentU);
                    break;

                case CubeSphereFace.PositiveY:
                    cubeVector = new DoubleVector3(
                        tangentU,
                        1.0,
                        -tangentV);
                    break;

                case CubeSphereFace.NegativeY:
                    cubeVector = new DoubleVector3(
                        tangentU,
                        -1.0,
                        tangentV);
                    break;

                case CubeSphereFace.PositiveZ:
                    cubeVector = new DoubleVector3(
                        tangentU,
                        tangentV,
                        1.0);
                    break;

                case CubeSphereFace.NegativeZ:
                    cubeVector = new DoubleVector3(
                        -tangentU,
                        tangentV,
                        -1.0);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(address),
                        address.Face,
                        "Unknown cube-sphere face.");
            }

            var magnitude = Magnitude(cubeVector);

            return new DoubleVector3(
                cubeVector.x / magnitude,
                cubeVector.y / magnitude,
                cubeVector.z / magnitude);
        }

        public static DoubleVector3 AddressToPlanetRelativePosition(
            CubeSphereAddress address,
            double planetRadiusMeters)
        {
            var direction = AddressToDirection(address);
            var distanceMeters =
                planetRadiusMeters +
                address.AltitudeMeters;

            return new DoubleVector3(
                direction.x * distanceMeters,
                direction.y * distanceMeters,
                direction.z * distanceMeters);
        }

        public static double FaceCoordinateToMeters(
            double faceCoordinate,
            double planetRadiusMeters)
        {
            return
                faceCoordinate *
                HalfFaceAngleRadians *
                planetRadiusMeters;
        }

        public static double MetersToFaceCoordinate(
            double faceMeters,
            double planetRadiusMeters)
        {
            if (!IsFinite(planetRadiusMeters) ||
                planetRadiusMeters <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(planetRadiusMeters),
                    "Planet radius must be positive.");
            }

            return
                faceMeters /
                (HalfFaceAngleRadians *
                    planetRadiusMeters);
        }

        private static double Magnitude(DoubleVector3 vector)
        {
            return Math.Sqrt(
                vector.x * vector.x +
                vector.y * vector.y +
                vector.z * vector.z);
        }

        private static double ClampUnit(double value)
        {
            return Math.Max(-1.0, Math.Min(1.0, value));
        }

        private static bool IsFinite(double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
