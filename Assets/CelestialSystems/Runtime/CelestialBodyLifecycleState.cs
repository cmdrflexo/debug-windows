/*
 * Describes the lifecycle of one packaged celestial-body runtime instance.
 */

namespace jcan.CelestialSystems
{
    public enum CelestialBodyLifecycleState
    {
        Uninitialized,
        Initializing,
        Active,
        Failed,
        Destroying
    }
}
