/*
 * Describes a nonblocking query against cached terrain triangles, including the resolved LOD and geometric normal.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    public readonly struct CelestialSurfaceSample
    {
        public bool IsValid { get; }
        public CubeSpherePatchAddress Patch { get; }
        public int RequestedLevel { get; }
        public int ResolvedLevel => Patch.Level;
        public int Resolution { get; }
        public int CacheVersion { get; }
        public bool IsRequestedDetail => IsValid && ResolvedLevel == RequestedLevel;
        public DoubleVector3 BodyPositionMeters { get; }
        public DoubleVector3 BodyNormal { get; }
        public UniversePosition UniversePosition { get; }
        public double ElevationMeters { get; }
        public double AltitudeMeters { get; }
        public double SlopeDegrees { get; }
        public double ApproximateSampleSpacingMeters { get; }

        internal CelestialSurfaceSample(CelestialSurfacePatchData data, int requestedLevel, int version,
            double radius, DoubleVector3 observer, DoubleVector3 point, DoubleVector3 normal,
            UniverseMotionState motion)
        {
            IsValid = true;
            Patch = data.Address;
            RequestedLevel = requestedLevel;
            Resolution = data.Resolution;
            CacheVersion = version;
            BodyPositionMeters = point;
            BodyNormal = normal;
            UniversePosition = CelestialSurfaceGeometry.Add(motion.Position,
                CelestialSurfaceGeometry.Rotate(point, motion.Rotation));
            ElevationMeters = point.Magnitude - radius;
            AltitudeMeters = observer.Magnitude - point.Magnitude;
            SlopeDegrees = Math.Acos(Math.Max(-1.0, Math.Min(1.0,
                CelestialSurfaceGeometry.Dot(normal, point / point.Magnitude)))) * 180.0 / Math.PI;
            ApproximateSampleSpacingMeters = CubeSpherePatchGrid.GetApproximateArcSizeMeters(Patch, radius) /
                (Resolution - 1);
        }
    }
}
