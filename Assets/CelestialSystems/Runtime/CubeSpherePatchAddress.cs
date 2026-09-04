/*
 * Stores a stable face, quadtree level, and two-dimensional coordinate for one adaptive cube-sphere patch.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [Serializable]
    public struct CubeSpherePatchAddress :
        IEquatable<CubeSpherePatchAddress>
    {
        public const int MaximumLevel = 30;

        [SerializeField]
        private bool initialized;

        [SerializeField]
        private CubeSphereFace face;

        [SerializeField]
        private int level;

        [SerializeField]
        private int x;

        [SerializeField]
        private int y;

        public CubeSphereFace Face =>
            face;

        public int Level =>
            level;

        public int X =>
            x;

        public int Y =>
            y;

        public int PatchCountPerAxis =>
            IsValid
                ? 1 << level
                : 0;

        public bool IsRoot =>
            IsValid &&
            level == 0;

        public bool IsValid
        {
            get
            {
                if (!initialized ||
                    level < 0 ||
                    level > MaximumLevel ||
                    !Enum.IsDefined(
                        typeof(CubeSphereFace),
                        face))
                {
                    return false;
                }

                var patchCount =
                    1 << level;

                return
                    x >= 0 &&
                    x < patchCount &&
                    y >= 0 &&
                    y < patchCount;
            }
        }

        public double MinimumU =>
            GetAxisMinimum(x);

        public double MaximumU =>
            GetAxisMaximum(x);

        public double MinimumV =>
            GetAxisMinimum(y);

        public double MaximumV =>
            GetAxisMaximum(y);

        public CubeSpherePatchAddress(
            CubeSphereFace face,
            int level,
            int x,
            int y)
        {
            initialized = true;
            this.face = face;
            this.level = level;
            this.x = x;
            this.y = y;
        }

        public static CubeSpherePatchAddress Root(
            CubeSphereFace face)
        {
            return new CubeSpherePatchAddress(
                face,
                0,
                0,
                0);
        }

        public static bool TryFromAddress(
            CubeSphereAddress surfaceAddress,
            int level,
            out CubeSpherePatchAddress patchAddress)
        {
            if (!surfaceAddress.IsInsideFace ||
                double.IsNaN(surfaceAddress.FaceU) ||
                double.IsInfinity(surfaceAddress.FaceU) ||
                double.IsNaN(surfaceAddress.FaceV) ||
                double.IsInfinity(surfaceAddress.FaceV) ||
                level < 0 ||
                level > MaximumLevel)
            {
                patchAddress = default;
                return false;
            }

            var patchCount =
                1 << level;
            var resolvedX =
                Math.Min(
                    patchCount - 1,
                    (int)Math.Floor(
                        (surfaceAddress.FaceU + 1.0) *
                        0.5 *
                        patchCount));
            var resolvedY =
                Math.Min(
                    patchCount - 1,
                    (int)Math.Floor(
                        (surfaceAddress.FaceV + 1.0) *
                        0.5 *
                        patchCount));

            patchAddress =
                new CubeSpherePatchAddress(
                    surfaceAddress.Face,
                    level,
                    resolvedX,
                    resolvedY);
            return patchAddress.IsValid;
        }

        public bool TryGetParent(
            out CubeSpherePatchAddress parent)
        {
            if (!IsValid ||
                level == 0)
            {
                parent = default;
                return false;
            }

            parent =
                new CubeSpherePatchAddress(
                    face,
                    level - 1,
                    x >> 1,
                    y >> 1);
            return true;
        }

        public bool TryGetChild(
            CubeSpherePatchQuadrant quadrant,
            out CubeSpherePatchAddress child)
        {
            if (!IsValid ||
                level >= MaximumLevel)
            {
                child = default;
                return false;
            }

            int childX;
            int childY;

            switch (quadrant)
            {
                case CubeSpherePatchQuadrant.NegativeUNegativeV:
                    childX = 0;
                    childY = 0;
                    break;

                case CubeSpherePatchQuadrant.PositiveUNegativeV:
                    childX = 1;
                    childY = 0;
                    break;

                case CubeSpherePatchQuadrant.NegativeUPositiveV:
                    childX = 0;
                    childY = 1;
                    break;

                case CubeSpherePatchQuadrant.PositiveUPositiveV:
                    childX = 1;
                    childY = 1;
                    break;

                default:
                    child = default;
                    return false;
            }

            child =
                new CubeSpherePatchAddress(
                    face,
                    level + 1,
                    x * 2 + childX,
                    y * 2 + childY);
            return true;
        }

        public bool Contains(
            CubeSphereAddress address)
        {
            if (!IsValid ||
                address.Face != face ||
                !address.IsInsideFace)
            {
                return false;
            }

            return
                ContainsAxis(
                    address.FaceU,
                    MinimumU,
                    MaximumU,
                    x) &&
                ContainsAxis(
                    address.FaceV,
                    MinimumV,
                    MaximumV,
                    y);
        }

        public bool Equals(
            CubeSpherePatchAddress other)
        {
            return
                initialized == other.initialized &&
                face == other.face &&
                level == other.level &&
                x == other.x &&
                y == other.y;
        }

        public override bool Equals(
            object value)
        {
            return
                value is CubeSpherePatchAddress other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = initialized
                    ? 1
                    : 0;
                hash = (hash * 397) ^ (int)face;
                hash = (hash * 397) ^ level;
                hash = (hash * 397) ^ x;
                hash = (hash * 397) ^ y;
                return hash;
            }
        }

        public override string ToString()
        {
            return
                $"{face} L{level} ({x}, {y})";
        }

        public static bool operator ==(
            CubeSpherePatchAddress first,
            CubeSpherePatchAddress second)
        {
            return first.Equals(second);
        }

        public static bool operator !=(
            CubeSpherePatchAddress first,
            CubeSpherePatchAddress second)
        {
            return !first.Equals(second);
        }

        private double GetAxisMinimum(
            int coordinate)
        {
            if (!IsValid)
            {
                return double.NaN;
            }

            return
                -1.0 +
                2.0 * coordinate /
                PatchCountPerAxis;
        }

        private double GetAxisMaximum(
            int coordinate)
        {
            if (!IsValid)
            {
                return double.NaN;
            }

            return
                -1.0 +
                2.0 * (coordinate + 1L) /
                PatchCountPerAxis;
        }

        private bool ContainsAxis(
            double value,
            double minimum,
            double maximum,
            int coordinate)
        {
            return
                value >= minimum &&
                (coordinate ==
                    PatchCountPerAxis - 1
                        ? value <= maximum
                        : value < maximum);
        }
    }
}
