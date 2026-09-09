/*
 * Carries deterministic input supplied to a pluggable celestial star-system generation guide.
 */

namespace jcan.CelestialSystems
{
    public sealed class CelestialStarSystemGenerationRequest
    {
        public CelestialStarSystemGenerationRequest(
            int seed)
        {
            Seed = seed;
        }

        public int Seed { get; }
    }
}
