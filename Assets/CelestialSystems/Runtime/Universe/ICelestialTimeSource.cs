/*
 * Defines the clock boundary used by prescribed trajectories without depending on a physics engine.
 */

namespace jcan.CelestialSystems
{
    public interface ICelestialTimeSource
    {
        double UniversalTimeSeconds { get; }

        double TimeScale { get; }

        bool IsRunning { get; }
    }
}
