/*
 * Identifies which independently prepared parts of a celestial-body runtime package are ready for use.
 */

using System;

namespace jcan.CelestialSystems
{
    [Flags]
    public enum CelestialBodyReadiness
    {
        None = 0,
        Definition = 1 << 0,
        Registered = 1 << 1,
        RuntimeHierarchy = 1 << 2,
        Motion = 1 << 3,
        CoarseSurface = 1 << 4,
        VisibleSurface = 1 << 5,
        CollisionSurface = 1 << 6,
        Ocean = 1 << 7,
        SurfaceFoundation = 1 << 8
    }
}
