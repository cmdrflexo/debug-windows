/*
 * Validates representative outputs of the approximate stellar evolution model.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialStellarEvolutionValidation
    {
        private const string SolarEnvironmentPath =
            "Assets/CelestialSystems/Definitions/Galactic Environments/Solar-Neighborhood Thin Disk.asset";

        [MenuItem(
            "Tools/Celestial Systems/Validate Stellar Evolution Model")]
        public static void Validate()
        {
            var environment =
                AssetDatabase.LoadAssetAtPath<
                    CelestialGalacticEnvironmentDefinition>(
                        SolarEnvironmentPath);

            if (environment == null)
            {
                Debug.LogError(
                    $"Stellar evolution validation could not load '{SolarEnvironmentPath}'.");
                return;
            }

            var solarSample =
                new CelestialStellarPopulationSample(
                    1.0,
                    10.0,
                    CelestialStellarEvolutionState.MainSequence);

            if (!TryEvaluate(
                    solarSample,
                    environment,
                    out var solar,
                    out var error) ||
                !Approximately(
                    solar.CurrentMassSolar,
                    1.0,
                    1.0e-12) ||
                !Approximately(
                    solar.RadiusSolar,
                    1.0,
                    1.0e-12) ||
                !Approximately(
                    solar.LuminositySolar,
                    1.0,
                    1.0e-12) ||
                !Approximately(
                    solar.EffectiveTemperatureKelvin,
                    5772.0,
                    1.0e-9))
            {
                Debug.LogError(
                    $"Stellar evolution Solar check failed: {error}");
                return;
            }

            var redDwarfSample =
                new CelestialStellarPopulationSample(
                    0.2,
                    10.0 *
                        Math.Pow(
                            0.2,
                            -2.5),
                    CelestialStellarEvolutionState.MainSequence);

            if (!TryEvaluate(
                    redDwarfSample,
                    environment,
                    out var redDwarf,
                    out error) ||
                redDwarf.LuminositySolar >=
                    solar.LuminositySolar ||
                redDwarf.EffectiveTemperatureKelvin >=
                    solar.EffectiveTemperatureKelvin)
            {
                Debug.LogError(
                    $"Stellar evolution red-dwarf check failed: {error}");
                return;
            }

            var whiteDwarfSample =
                new CelestialStellarPopulationSample(
                    3.0,
                    0.64,
                    CelestialStellarEvolutionState.WhiteDwarf);

            if (!TryEvaluate(
                    whiteDwarfSample,
                    environment,
                    out var whiteDwarf,
                    out error) ||
                whiteDwarf.CurrentMassSolar <= 0.5 ||
                whiteDwarf.CurrentMassSolar >=
                    whiteDwarf.InitialMassSolar ||
                whiteDwarf.RadiusSolar >= 0.1)
            {
                Debug.LogError(
                    $"Stellar evolution white-dwarf check failed: {error}");
                return;
            }

            var neutronStarSample =
                new CelestialStellarPopulationSample(
                    12.0,
                    0.02,
                    CelestialStellarEvolutionState.NeutronStar);
            var blackHoleSample =
                new CelestialStellarPopulationSample(
                    40.0,
                    0.001,
                    CelestialStellarEvolutionState.BlackHole);

            if (!TryEvaluate(
                    neutronStarSample,
                    environment,
                    out var neutronStar,
                    out error) ||
                !TryEvaluate(
                    blackHoleSample,
                    environment,
                    out var blackHole,
                    out error) ||
                neutronStar.RadiusSolar >=
                    whiteDwarf.RadiusSolar ||
                blackHole.LuminositySolar != 0.0 ||
                blackHole.EffectiveTemperatureKelvin != 0.0)
            {
                Debug.LogError(
                    $"Stellar evolution compact-remnant check failed: {error}");
                return;
            }

            Debug.Log(
                $"Stellar evolution PASS. Model v{CelestialStellarEvolutionModel.ModelVersion}; " +
                $"Solar, red dwarf, white dwarf, neutron star, and black hole checks passed. " +
                $"WD: {whiteDwarf.CurrentMassSolar:F3} solar masses, {whiteDwarf.RadiusSolar:F5} solar radii.");
        }

        private static bool TryEvaluate(
            CelestialStellarPopulationSample sample,
            CelestialGalacticEnvironmentDefinition environment,
            out CelestialStellarEvolutionResult result,
            out string error)
        {
            return
                CelestialStellarEvolutionModel.TryEvaluate(
                    sample,
                    environment,
                    out result,
                    out error);
        }

        private static bool Approximately(
            double first,
            double second,
            double tolerance)
        {
            return
                Math.Abs(
                    first - second) <=
                tolerance;
        }
    }
}
