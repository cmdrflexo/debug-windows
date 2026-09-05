/*
 * Selects a bounded, uniform-level collision footprint on every cube face, including edges and corners.
 */

using System;
using System.Collections.Generic;

namespace jcan.CelestialSystems
{
    public static class CelestialSurfaceCollisionCoverage
    {
        public static int ResolveLevel(double radius, int resolution, double spacing, int minimum, int maximum)
        {
            var level = minimum;
            while (level < maximum && Math.PI * radius * 0.5 / ((1L << level) * (resolution - 1)) > spacing)
                level++;
            return level;
        }

        public static bool Collect(DoubleVector3 direction, double radius, double coverageMeters,
            int level, HashSet<CubeSpherePatchAddress> destination, int limit)
        {
            if (direction.Magnitude <= 0.0 || !CelestialSurfaceGeometry.IsFinite(direction.Magnitude) ||
                !CelestialSurfaceGeometry.IsFinite(radius) || !CelestialSurfaceGeometry.IsFinite(coverageMeters) ||
                radius <= 0.0 || coverageMeters < 0.0 || destination == null || limit <= 0 || level < 0 ||
                level > CubeSpherePatchAddress.MaximumLevel) return false;
            direction /= direction.Magnitude;
            foreach (CubeSphereFace face in Enum.GetValues(typeof(CubeSphereFace)))
                if (!CollectPatch(CubeSpherePatchAddress.Root(face), direction, radius,
                        coverageMeters, level, destination, limit))
                    return false;
            return true;
        }

        private static bool CollectPatch(CubeSpherePatchAddress patch, DoubleVector3 direction,
            double radius, double coverage, int level, HashSet<CubeSpherePatchAddress> result, int limit)
        {
            var center = CelestialSurfaceGeometry.ReferencePosition(patch, 1.0);
            // The sum of both half angular widths bounds the distance to every point in this patch.
            var bound = Math.PI * 0.5 / patch.PatchCountPerAxis;
            if ((direction - center).Magnitude * radius > coverage + bound * radius) return true;
            if (patch.Level == level)
            {
                if (result.Contains(patch)) return true;
                if (result.Count >= limit) return false;
                result.Add(patch);
                return true;
            }
            for (var i = 0; i < 4; i++)
            {
                patch.TryGetChild((CubeSpherePatchQuadrant)i, out var child);
                if (!CollectPatch(child, direction, radius, coverage, level, result, limit)) return false;
            }
            return true;
        }
    }
}
