/*
 * Evaluates deterministic 3D cellular fields from a body direction, independent of cube face or tile.
 */

using System;

namespace jcan.CelestialSystems
{
    public enum SphericalCellularOutputMode
    {
        CellInterior,
        F1,
        Edge,
        CellId
    }

    public static class RoundMapMagicSphericalCellular
    {
        private const double UnitHashScale = 1.0 / 9007199254740992.0;
        private const double MaximumSearchDistance = 1.7320508075688772;

        public static double EvaluateNormalized(
            DoubleVector3 direction,
            double bodyRadiusMeters,
            int seed,
            double cellSizeMeters,
            double jitter,
            SphericalCellularOutputMode outputMode)
        {
            var magnitude = Math.Sqrt(
                direction.x * direction.x +
                direction.y * direction.y +
                direction.z * direction.z);

            if (!IsFinite(magnitude) || magnitude <= 0.0 ||
                !IsFinite(bodyRadiusMeters) || bodyRadiusMeters <= 0.0 ||
                !IsFinite(cellSizeMeters) || cellSizeMeters <= 0.0)
            {
                return 0.0;
            }

            var scale = bodyRadiusMeters / (cellSizeMeters * magnitude);
            var sampleX = direction.x * scale;
            var sampleY = direction.y * scale;
            var sampleZ = direction.z * scale;
            var baseX = FloorToLong(sampleX);
            var baseY = FloorToLong(sampleY);
            var baseZ = FloorToLong(sampleZ);
            var resolvedJitter = Clamp01(IsFinite(jitter) ? jitter : 0.75);
            var nearestSquared = double.MaxValue;
            var secondSquared = double.MaxValue;
            var nearestId = 0.0;

            for (var z = -1; z <= 1; z++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    for (var x = -1; x <= 1; x++)
                    {
                        var cellX = baseX + x;
                        var cellY = baseY + y;
                        var cellZ = baseZ + z;
                        var featureX = cellX + 0.5 +
                            (Hash01(cellX, cellY, cellZ, seed, 0) - 0.5) * resolvedJitter;
                        var featureY = cellY + 0.5 +
                            (Hash01(cellX, cellY, cellZ, seed, 1) - 0.5) * resolvedJitter;
                        var featureZ = cellZ + 0.5 +
                            (Hash01(cellX, cellY, cellZ, seed, 2) - 0.5) * resolvedJitter;
                        var deltaX = featureX - sampleX;
                        var deltaY = featureY - sampleY;
                        var deltaZ = featureZ - sampleZ;
                        var distanceSquared =
                            deltaX * deltaX +
                            deltaY * deltaY +
                            deltaZ * deltaZ;

                        if (distanceSquared < nearestSquared)
                        {
                            secondSquared = nearestSquared;
                            nearestSquared = distanceSquared;
                            nearestId = Hash01(cellX, cellY, cellZ, seed, 3);
                        }
                        else if (distanceSquared < secondSquared)
                        {
                            secondSquared = distanceSquared;
                        }
                    }
                }
            }

            var firstDistance = Math.Sqrt(nearestSquared);
            var secondDistance = Math.Sqrt(secondSquared);
            var normalizedF1 = Clamp01(firstDistance / MaximumSearchDistance);

            switch (outputMode)
            {
                case SphericalCellularOutputMode.F1:
                    return normalizedF1;
                case SphericalCellularOutputMode.Edge:
                    return Clamp01(secondDistance - firstDistance);
                case SphericalCellularOutputMode.CellId:
                    return nearestId;
                default:
                    return 1.0 - normalizedF1;
            }
        }

        private static double Hash01(long x, long y, long z, int seed, int channel)
        {
            unchecked
            {
                var hash = 1469598103934665603UL;
                hash = (hash ^ (ulong)x) * 1099511628211UL;
                hash = (hash ^ (ulong)y) * 1099511628211UL;
                hash = (hash ^ (ulong)z) * 1099511628211UL;
                hash = (hash ^ (uint)seed) * 1099511628211UL;
                hash = (hash ^ (uint)channel) * 1099511628211UL;
                hash ^= hash >> 30;
                hash *= 0xbf58476d1ce4e5b9UL;
                hash ^= hash >> 27;
                hash *= 0x94d049bb133111ebUL;
                hash ^= hash >> 31;
                return (hash >> 11) * UnitHashScale;
            }
        }

        private static long FloorToLong(double value)
        {
            var floored = Math.Floor(value);
            if (floored <= long.MinValue) return long.MinValue;
            if (floored >= long.MaxValue) return long.MaxValue;
            return (long)floored;
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
