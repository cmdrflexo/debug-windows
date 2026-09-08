/*
 * Describes the local Milestone 3 lifecycle of one MapMagic-backed adaptive patch request.
 */

namespace jcan.CelestialSystems
{
    public enum CelestialSurfacePatchGenerationState
    {
        None,
        Queued,
        Generating,
        Ready,
        Failed
    }
}
