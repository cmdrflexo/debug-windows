/*
 * Validates deterministic stellar-population sampling and reports a compact model summary.
 */

using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialStellarPopulationValidation
    {
        private const string SolarEnvironmentPath =
            "Assets/CelestialSystems/Definitions/Galactic Environments/Solar-Neighborhood Thin Disk.asset";

        [MenuItem(
            "Tools/Celestial Systems/Validate Stellar Population Sampler")]
        public static void Validate()
        {
            var environment =
                AssetDatabase.LoadAssetAtPath<
                    CelestialGalacticEnvironmentDefinition>(
                        SolarEnvironmentPath);

            if (environment == null)
            {
                Debug.LogError(
                    $"Stellar population validation could not load '{SolarEnvironmentPath}'.");
                return;
            }

            const int sampleCount = 10000;
            var belowHalfSolar = 0;
            var aboveEightSolar = 0;
            var evolved = 0;

            for (var seed = 1;
                seed <= sampleCount;
                seed++)
            {
                if (!CelestialStellarPopulationSampler.TrySamplePrimary(
                        seed,
                        environment,
                        out var first,
                        out var error) ||
                    !CelestialStellarPopulationSampler.TrySamplePrimary(
                        seed,
                        environment,
                        out var second,
                        out _))
                {
                    Debug.LogError(
                        $"Stellar population validation failed at seed {seed}: {error}");
                    return;
                }

                if (first.InitialMassSolar !=
                        second.InitialMassSolar ||
                    first.EvolutionState !=
                        second.EvolutionState)
                {
                    Debug.LogError(
                        $"Stellar population validation was not deterministic at seed {seed}.");
                    return;
                }

                if (first.InitialMassSolar <
                    0.5)
                {
                    belowHalfSolar++;
                }

                if (first.InitialMassSolar >=
                    8.0)
                {
                    aboveEightSolar++;
                }

                if (first.EvolutionState !=
                    CelestialStellarEvolutionState.MainSequence)
                {
                    evolved++;
                }
            }

            if (belowHalfSolar <=
                    sampleCount / 2 ||
                aboveEightSolar >=
                    sampleCount / 100)
            {
                Debug.LogError(
                    "Stellar population validation produced an implausible mass distribution.");
                return;
            }

            Debug.Log(
                $"Stellar population PASS. Model v{CelestialStellarPopulationSampler.ModelVersion}; " +
                $"{sampleCount} deterministic samples; " +
                $"{belowHalfSolar} below 0.5 solar masses; " +
                $"{aboveEightSolar} born at or above 8 solar masses; " +
                $"{evolved} evolved by {environment.SystemAgeGigayears:F2} Gyr.");
        }
    }
}
