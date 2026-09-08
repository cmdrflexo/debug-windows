/*
 * Defines the orientation and neighboring-face relationships of the canonical cube-sphere charts.
 */

using System;

namespace jcan.CelestialSystems
{
    public static class CubeSphereTopology
    {
        public static DoubleVector3 GetFaceNormal(
            CubeSphereFace face)
        {
            switch (face)
            {
                case CubeSphereFace.PositiveX:
                    return new DoubleVector3(1.0, 0.0, 0.0);

                case CubeSphereFace.NegativeX:
                    return new DoubleVector3(-1.0, 0.0, 0.0);

                case CubeSphereFace.PositiveY:
                    return new DoubleVector3(0.0, 1.0, 0.0);

                case CubeSphereFace.NegativeY:
                    return new DoubleVector3(0.0, -1.0, 0.0);

                case CubeSphereFace.PositiveZ:
                    return new DoubleVector3(0.0, 0.0, 1.0);

                case CubeSphereFace.NegativeZ:
                    return new DoubleVector3(0.0, 0.0, -1.0);

                default:
                    throw UnknownFace(face);
            }
        }

        public static DoubleVector3 GetFaceUAxis(
            CubeSphereFace face)
        {
            switch (face)
            {
                case CubeSphereFace.PositiveX:
                    return new DoubleVector3(0.0, 0.0, -1.0);

                case CubeSphereFace.NegativeX:
                    return new DoubleVector3(0.0, 0.0, 1.0);

                case CubeSphereFace.PositiveY:
                case CubeSphereFace.NegativeY:
                case CubeSphereFace.PositiveZ:
                    return new DoubleVector3(1.0, 0.0, 0.0);

                case CubeSphereFace.NegativeZ:
                    return new DoubleVector3(-1.0, 0.0, 0.0);

                default:
                    throw UnknownFace(face);
            }
        }

        public static DoubleVector3 GetFaceVAxis(
            CubeSphereFace face)
        {
            switch (face)
            {
                case CubeSphereFace.PositiveX:
                case CubeSphereFace.NegativeX:
                case CubeSphereFace.PositiveZ:
                case CubeSphereFace.NegativeZ:
                    return new DoubleVector3(0.0, 1.0, 0.0);

                case CubeSphereFace.PositiveY:
                    return new DoubleVector3(0.0, 0.0, -1.0);

                case CubeSphereFace.NegativeY:
                    return new DoubleVector3(0.0, 0.0, 1.0);

                default:
                    throw UnknownFace(face);
            }
        }

        public static CubeSphereFace GetAdjacentFace(
            CubeSphereFace face,
            CubeSphereEdge edge)
        {
            DoubleVector3 adjacentNormal;

            switch (edge)
            {
                case CubeSphereEdge.NegativeU:
                    adjacentNormal =
                        GetFaceUAxis(face) * -1.0;
                    break;

                case CubeSphereEdge.PositiveU:
                    adjacentNormal =
                        GetFaceUAxis(face);
                    break;

                case CubeSphereEdge.NegativeV:
                    adjacentNormal =
                        GetFaceVAxis(face) * -1.0;
                    break;

                case CubeSphereEdge.PositiveV:
                    adjacentNormal =
                        GetFaceVAxis(face);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(edge),
                        edge,
                        "Unknown cube-sphere edge.");
            }

            return NormalToFace(adjacentNormal);
        }

        private static CubeSphereFace NormalToFace(
            DoubleVector3 normal)
        {
            if (normal.x > 0.5)
            {
                return CubeSphereFace.PositiveX;
            }

            if (normal.x < -0.5)
            {
                return CubeSphereFace.NegativeX;
            }

            if (normal.y > 0.5)
            {
                return CubeSphereFace.PositiveY;
            }

            if (normal.y < -0.5)
            {
                return CubeSphereFace.NegativeY;
            }

            if (normal.z > 0.5)
            {
                return CubeSphereFace.PositiveZ;
            }

            if (normal.z < -0.5)
            {
                return CubeSphereFace.NegativeZ;
            }

            throw new ArgumentException(
                "A cube-sphere face normal must be axis aligned.",
                nameof(normal));
        }

        private static ArgumentOutOfRangeException UnknownFace(
            CubeSphereFace face)
        {
            return new ArgumentOutOfRangeException(
                nameof(face),
                face,
                "Unknown cube-sphere face.");
        }
    }
}
