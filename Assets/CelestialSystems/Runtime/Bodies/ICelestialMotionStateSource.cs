/*
 * Supplies mass and timestamped universe motion to prescribed trajectories without requiring a visible celestial body.
 */

namespace jcan.CelestialSystems
{
    public interface ICelestialMotionStateSource
    {
        double ConfiguredMassKilograms { get; }

        bool TryGetMotionState(
            out UniverseMotionState motionState);
    }
}
