/*
 * Resolves same-level adaptive-patch neighbors both within a chart and across canonical cube-face edges.
 */

using System;

namespace jcan.CelestialSystems
{
    public static class CubeSpherePatchTopology
    {
        public static bool TryGetNeighbor(
            CubeSpherePatchAddress address,
            CubeSphereEdge edge,
            out CubeSpherePatchAddress neighbor)
        {
            if (!address.IsValid)
            {
                neighbor = default;
                return false;
            }

            var patchCount =
                address.PatchCountPerAxis;
            var neighborX =
                address.X;
            var neighborY =
                address.Y;

            switch (edge)
            {
                case CubeSphereEdge.NegativeU:
                    neighborX--;
                    break;

                case CubeSphereEdge.PositiveU:
                    neighborX++;
                    break;

                case CubeSphereEdge.NegativeV:
                    neighborY--;
                    break;

                case CubeSphereEdge.PositiveV:
                    neighborY++;
                    break;

                default:
                    neighbor = default;
                    return false;
            }

            if (neighborX >= 0 &&
                neighborX < patchCount &&
                neighborY >= 0 &&
                neighborY < patchCount)
            {
                neighbor =
                    new CubeSpherePatchAddress(
                        address.Face,
                        address.Level,
                        neighborX,
                        neighborY);
                return true;
            }

            return TryGetCrossFaceNeighbor(
                address,
                edge,
                out neighbor);
        }

        private static bool TryGetCrossFaceNeighbor(
            CubeSpherePatchAddress address,
            CubeSphereEdge edge,
            out CubeSpherePatchAddress neighbor)
        {
            var centerU =
                (address.MinimumU +
                    address.MaximumU) *
                0.5;
            var centerV =
                (address.MinimumV +
                    address.MaximumV) *
                0.5;
            double edgeU;
            double edgeV;

            switch (edge)
            {
                case CubeSphereEdge.NegativeU:
                    edgeU = -1.0;
                    edgeV = centerV;
                    break;

                case CubeSphereEdge.PositiveU:
                    edgeU = 1.0;
                    edgeV = centerV;
                    break;

                case CubeSphereEdge.NegativeV:
                    edgeU = centerU;
                    edgeV = -1.0;
                    break;

                case CubeSphereEdge.PositiveV:
                    edgeU = centerU;
                    edgeV = 1.0;
                    break;

                default:
                    neighbor = default;
                    return false;
            }

            var adjacentFace =
                CubeSphereTopology.GetAdjacentFace(
                    address.Face,
                    edge);
            var edgeAddress =
                new CubeSphereAddress(
                    address.Face,
                    edgeU,
                    edgeV,
                    0.0);

            if (!CubeSphereMapping.TryAddressToFaceAddress(
                    edgeAddress,
                    adjacentFace,
                    out var adjacentAddress) ||
                !CubeSpherePatchAddress.TryFromAddress(
                    adjacentAddress,
                    address.Level,
                    out neighbor))
            {
                neighbor = default;
                return false;
            }

            return true;
        }
    }
}
