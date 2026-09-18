/*
 * Carries deterministic input supplied to a pluggable celestial star-system generation guide.
 */

namespace jcan.CelestialSystems
{
    public sealed class CelestialStarSystemGenerationRequest
    {
        public CelestialStarSystemGenerationRequest(
            int seed,
            CelestialGalacticEnvironmentDefinition environment = null)
        {
            Seed = seed;
            Environment = environment;
        }

        public int Seed { get; }

        public CelestialGalacticEnvironmentDefinition Environment { get; }
    }
}
