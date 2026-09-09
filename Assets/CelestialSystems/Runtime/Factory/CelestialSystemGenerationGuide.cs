/*
 * Defines the plug-in boundary for converting a seed into a celestial star-system generation plan.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    public abstract class CelestialSystemGenerationGuide :
        ScriptableObject
    {
        public abstract bool TryGenerate(
            CelestialStarSystemGenerationRequest request,
            out CelestialStarSystemPlan plan,
            out string error);
    }
}
