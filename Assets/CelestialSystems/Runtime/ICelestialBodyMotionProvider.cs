/*
 * Defines the motion boundary used by a celestial-body runtime without coupling it to a particular orbital simulation.
 */

namespace jcan.CelestialSystems
{
    public interface ICelestialBodyMotionProvider
    {
        string ProviderName { get; }

        bool IsReady { get; }

        bool TryGetMotionState(
            out CelestialBodyMotionState motionState);
    }
}
