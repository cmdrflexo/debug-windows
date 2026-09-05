/*
 * Shares exact patch triangles, radial surface queries, and double-precision pose math between rendering and collision.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    public static class CelestialSurfaceGeometry
    {
        public static DoubleVector3 ReferencePosition(CubeSpherePatchAddress address, double radius)
        {
            return CubeSphereMapping.AddressToDirection(new CubeSphereAddress(
                address.Face, (address.MinimumU + address.MaximumU) * 0.5,
                (address.MinimumV + address.MaximumV) * 0.5, 0.0)) * radius;
        }

        public static DoubleVector3 Vertex(CelestialSurfacePatchData data, double radius, int x, int y)
        {
            if (!CubeSpherePatchGrid.TryGetSamplePlanetRelativePosition(
                    data.Address, data.Resolution, x, y, radius,
                    data.GetElevationMeters(x, y), out var position))
                throw new InvalidOperationException("Invalid radial terrain sample.");
            return position;
        }

        public static Vector3[] BuildVertices(CelestialSurfacePatchData data, double radius)
        {
            var origin = ReferencePosition(data.Address, radius);
            var vertices = new Vector3[data.SampleCount];
            for (var y = 0; y < data.Resolution; y++)
                for (var x = 0; x < data.Resolution; x++)
                    vertices[y * data.Resolution + x] = ToVector3(Vertex(data, radius, x, y) - origin);
            return vertices;
        }

        // Same diagonal and winding as the adaptive renderer. No skirts in physics.
        public static int[] BuildTriangles(int resolution)
        {
            var indices = new int[(resolution - 1) * (resolution - 1) * 6];
            var i = 0;
            for (var y = 0; y < resolution - 1; y++)
                for (var x = 0; x < resolution - 1; x++)
                {
                    var a = y * resolution + x;
                    indices[i++] = a; indices[i++] = a + 1; indices[i++] = a + resolution;
                    indices[i++] = a + 1; indices[i++] = a + resolution + 1; indices[i++] = a + resolution;
                }
            return indices;
        }

        public static bool TrySample(CelestialSurfacePatchData data, double radius,
            DoubleVector3 bodyPosition, out DoubleVector3 point, out DoubleVector3 normal)
        {
            point = normal = default;
            if (data == null || !IsFinite(bodyPosition.Magnitude) || bodyPosition.Magnitude <= 0.0 ||
                !CubeSphereMapping.TryDirectionToFaceAddress(bodyPosition, data.Address.Face, 0.0, out var address))
                return false;
            var patch = data.Address;
            var u = (address.FaceU - patch.MinimumU) / (patch.MaximumU - patch.MinimumU);
            var v = (address.FaceV - patch.MinimumV) / (patch.MaximumV - patch.MinimumV);
            const double epsilon = 1e-9;
            if (u < -epsilon || u > 1.0 + epsilon || v < -epsilon || v > 1.0 + epsilon)
                return false;
            var n = data.Resolution - 1;
            var x = Math.Min(n - 1, Math.Max(0, (int)Math.Floor(u * n)));
            var y = Math.Min(n - 1, Math.Max(0, (int)Math.Floor(v * n)));
            var direction = bodyPosition / bodyPosition.Magnitude;
            var a = Vertex(data, radius, x, y);
            var b = Vertex(data, radius, x + 1, y);
            var c = Vertex(data, radius, x, y + 1);
            var d = Vertex(data, radius, x + 1, y + 1);
            return TryRadialTriangle(direction, a, b, c, out point, out normal) ||
                TryRadialTriangle(direction, b, d, c, out point, out normal);
        }

        private static bool TryRadialTriangle(DoubleVector3 direction, DoubleVector3 a,
            DoubleVector3 b, DoubleVector3 c, out DoubleVector3 point, out DoubleVector3 normal)
        {
            point = normal = default;
            var ab = b - a;
            var ac = c - a;
            var cross = Cross(direction, ac);
            var determinant = Dot(ab, cross);
            if (Math.Abs(determinant) < 1e-18)
                return false;
            var origin = a * -1.0;
            var u = Dot(origin, cross) / determinant;
            var q = Cross(origin, ab);
            var v = Dot(direction, q) / determinant;
            var distance = Dot(ac, q) / determinant;
            if (u < -1e-7 || v < -1e-7 || u + v > 1.0000001 || distance <= 0.0)
                return false;
            point = direction * distance;
            normal = Cross(ab, ac);
            normal /= normal.Magnitude;
            if (Dot(normal, direction) < 0.0) normal *= -1.0;
            return true;
        }

        public static DoubleVector3 Rotate(DoubleVector3 value, Quaternion rotation)
        {
            var norm = Math.Sqrt((double)rotation.x * rotation.x + (double)rotation.y * rotation.y +
                (double)rotation.z * rotation.z + (double)rotation.w * rotation.w);
            if (!IsFinite(norm) || norm < 1e-12) throw new ArgumentException("Invalid body rotation.");
            var axis = new DoubleVector3(rotation.x / norm, rotation.y / norm, rotation.z / norm);
            var cross = Cross(axis, value);
            return value + cross * (2.0 * rotation.w / norm) + Cross(axis, cross) * 2.0;
        }

        public static DoubleVector3 Difference(UniversePosition a, UniversePosition b)
        {
            // Decimal subtraction preserves nearby cells even above double's exact integer range.
            return new DoubleVector3(
                (double)((decimal)a.CellX - b.CellX) * UniversePosition.CellSizeMeters + a.LocalXMeters - b.LocalXMeters,
                (double)((decimal)a.CellY - b.CellY) * UniversePosition.CellSizeMeters + a.LocalYMeters - b.LocalYMeters,
                (double)((decimal)a.CellZ - b.CellZ) * UniversePosition.CellSizeMeters + a.LocalZMeters - b.LocalZMeters);
        }

        public static UniversePosition Add(UniversePosition position, DoubleVector3 delta)
        {
            position.AddLocalMeters(delta.x, delta.y, delta.z);
            return position;
        }

        public static double Dot(DoubleVector3 a, DoubleVector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static DoubleVector3 Cross(DoubleVector3 a, DoubleVector3 b) =>
            new DoubleVector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static Vector3 ToVector3(DoubleVector3 value) => new Vector3((float)value.x, (float)value.y, (float)value.z);
        public static DoubleVector3 ToDouble(Vector3 value) => new DoubleVector3(value.x, value.y, value.z);
    }
}
