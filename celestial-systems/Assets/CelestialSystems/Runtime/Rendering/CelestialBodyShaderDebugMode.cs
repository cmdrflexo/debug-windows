/*
 * Identifies the diagnostic view requested from a celestial-body surface shader.
 */

namespace jcan.CelestialSystems
{
    public enum CelestialBodyShaderDebugMode
    {
        Lit,
        MeshNormal,
        RadialNormal,
        Elevation,
        Slope,
        Latitude,
        PatchUv,
        BodyRelativeCoordinates
    }
}
