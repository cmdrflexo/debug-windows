/*
 * Produces nested sample coordinates, directions, and universe positions for adaptive cube-sphere patches.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    public static class CubeSpherePatchGrid
    {
        public static bool HasNestedResolution(
            int resolution)
        {
            var intervalCount =
                resolution - 1;

            return
                intervalCount >= 2 &&
                (intervalCount &
                    (intervalCount - 1)) == 0;
        }

        public static bool TryGetSampleAddress(
            CubeSpherePatchAddress patch,
            int resolution,
            int sampleX,
            int sampleY,
            double elevationMeters,
            out CubeSphereAddress address)
        {
            if (!TryGetSampleFaceCoordinates(
                    patch,
                    resolution,
                    sampleX,
                    sampleY,
                    out var faceU,
                    out var faceV) ||
                !IsFinite(elevationMeters))
            {
                address = default;
                return false;
            }

            address =
                new CubeSphereAddress(
                    patch.Face,
                    faceU,
                    faceV,
                    elevationMeters);
            return true;
        }

        public static bool TryGetSampleFaceCoordinates(
            CubeSpherePatchAddress patch,
            int resolution,
            int sampleX,
            int sampleY,
            out double faceU,
            out double faceV)
        {
            if (!patch.IsValid ||
                !HasNestedResolution(resolution) ||
                sampleX < 0 ||
                sampleX >= resolution ||
                sampleY < 0 ||
                sampleY >= resolution)
            {
                faceU = default;
                faceV = default;
                return false;
            }

            var intervals =
                resolution - 1;
            var patchCount =
                1L << patch.Level;
            var globalIntervals =
                patchCount * intervals;
            var globalX =
                (long)patch.X * intervals +
                sampleX;
            var globalY =
                (long)patch.Y * intervals +
                sampleY;

            faceU =
                -1.0 +
                2.0 * globalX /
                globalIntervals;
            faceV =
                -1.0 +
                2.0 * globalY /
                globalIntervals;
            return true;
        }

        public static bool TryGetSampleDirection(
            CubeSpherePatchAddress patch,
            int resolution,
            int sampleX,
            int sampleY,
            out DoubleVector3 direction)
        {
            if (!TryGetSampleAddress(
                    patch,
                    resolution,
                    sampleX,
                    sampleY,
                    0.0,
                    out var address))
            {
                direction = default;
                return false;
            }

            direction =
                CubeSphereMapping.AddressToDirection(
                    address);
            return
                IsFinite(direction.x) &&
                IsFinite(direction.y) &&
                IsFinite(direction.z);
        }

        public static bool TryGetSamplePlanetRelativePosition(
            CubeSpherePatchAddress patch,
            int resolution,
            int sampleX,
            int sampleY,
            double referenceRadiusMeters,
            double elevationMeters,
            out DoubleVector3 positionMeters)
        {
            if (!IsFinite(referenceRadiusMeters) ||
                referenceRadiusMeters <= 0.0 ||
                !IsFinite(elevationMeters) ||
                referenceRadiusMeters +
                    elevationMeters <= 0.0 ||
                !TryGetSampleAddress(
                    patch,
                    resolution,
                    sampleX,
                    sampleY,
                    elevationMeters,
                    out var address))
            {
                positionMeters = default;
                return false;
            }

            positionMeters =
                CubeSphereMapping.AddressToPlanetRelativePosition(
                    address,
                    referenceRadiusMeters);
            return
                IsFinite(positionMeters.x) &&
                IsFinite(positionMeters.y) &&
                IsFinite(positionMeters.z);
        }

        public static bool TryGetSampleUniversePosition(
            CubeSpherePatchAddress patch,
            int resolution,
            int sampleX,
            int sampleY,
            double referenceRadiusMeters,
            double elevationMeters,
            UniversePosition bodyCenter,
            Quaternion bodyRotation,
            out UniversePosition universePosition)
        {
            if (!TryGetSamplePlanetRelativePosition(
                    patch,
                    resolution,
                    sampleX,
                    sampleY,
                    referenceRadiusMeters,
                    elevationMeters,
                    out var bodyRelativePosition) ||
                !TryRotate(
                    bodyRelativePosition,
                    bodyRotation,
                    out var rotatedPosition))
            {
                universePosition = default;
                return false;
            }

            universePosition =
                bodyCenter;
            universePosition.AddLocalMeters(
                rotatedPosition.x,
                rotatedPosition.y,
                rotatedPosition.z);
            return true;
        }

        public static bool TryGetMatchingChildSample(
            CubeSpherePatchAddress parent,
            int resolution,
            int parentSampleX,
            int parentSampleY,
            out CubeSpherePatchAddress child,
            out int childSampleX,
            out int childSampleY)
        {
            if (!parent.IsValid ||
                parent.Level >=
                    CubeSpherePatchAddress.MaximumLevel ||
                !HasNestedResolution(resolution) ||
                parentSampleX < 0 ||
                parentSampleX >= resolution ||
                parentSampleY < 0 ||
                parentSampleY >= resolution)
            {
                child = default;
                childSampleX = default;
                childSampleY = default;
                return false;
            }

            var intervals =
                resolution - 1;
            var halfIntervals =
                intervals / 2;
            var positiveU =
                parentSampleX > halfIntervals;
            var positiveV =
                parentSampleY > halfIntervals;
            var quadrant =
                ResolveQuadrant(
                    positiveU,
                    positiveV);

            if (!parent.TryGetChild(
                    quadrant,
                    out child))
            {
                childSampleX = default;
                childSampleY = default;
                return false;
            }

            childSampleX =
                positiveU
                    ? (parentSampleX -
                        halfIntervals) * 2
                    : parentSampleX * 2;
            childSampleY =
                positiveV
                    ? (parentSampleY -
                        halfIntervals) * 2
                    : parentSampleY * 2;
            return true;
        }

        public static double GetApproximateArcSizeMeters(
            CubeSpherePatchAddress patch,
            double referenceRadiusMeters)
        {
            if (!patch.IsValid ||
                !IsFinite(referenceRadiusMeters) ||
                referenceRadiusMeters <= 0.0)
            {
                return double.NaN;
            }

            return
                Math.PI * 0.5 *
                referenceRadiusMeters /
                patch.PatchCountPerAxis;
        }

        private static CubeSpherePatchQuadrant ResolveQuadrant(
            bool positiveU,
            bool positiveV)
        {
            if (positiveV)
            {
                return positiveU
                    ? CubeSpherePatchQuadrant.PositiveUPositiveV
                    : CubeSpherePatchQuadrant.NegativeUPositiveV;
            }

            return positiveU
                ? CubeSpherePatchQuadrant.PositiveUNegativeV
                : CubeSpherePatchQuadrant.NegativeUNegativeV;
        }

        private static bool TryRotate(
            DoubleVector3 value,
            Quaternion rotation,
            out DoubleVector3 rotated)
        {
            var magnitudeSquared =
                (double)rotation.x * rotation.x +
                (double)rotation.y * rotation.y +
                (double)rotation.z * rotation.z +
                (double)rotation.w * rotation.w;

            if (!IsFinite(magnitudeSquared) ||
                magnitudeSquared <= 0.0)
            {
                rotated = default;
                return false;
            }

            var inverseMagnitude =
                1.0 /
                Math.Sqrt(magnitudeSquared);
            var qx = rotation.x * inverseMagnitude;
            var qy = rotation.y * inverseMagnitude;
            var qz = rotation.z * inverseMagnitude;
            var qw = rotation.w * inverseMagnitude;
            var crossX =
                qy * value.z -
                qz * value.y;
            var crossY =
                qz * value.x -
                qx * value.z;
            var crossZ =
                qx * value.y -
                qy * value.x;
            var secondCrossX =
                qy * crossZ -
                qz * crossY;
            var secondCrossY =
                qz * crossX -
                qx * crossZ;
            var secondCrossZ =
                qx * crossY -
                qy * crossX;

            rotated =
                new DoubleVector3(
                    value.x +
                        2.0 *
                        (qw * crossX +
                            secondCrossX),
                    value.y +
                        2.0 *
                        (qw * crossY +
                            secondCrossY),
                    value.z +
                        2.0 *
                        (qw * crossZ +
                            secondCrossZ));
            return
                IsFinite(rotated.x) &&
                IsFinite(rotated.y) &&
                IsFinite(rotated.z);
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
