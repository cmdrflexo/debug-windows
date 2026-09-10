/*
 * Validates deterministic planet formation, material conservation, composition gradients, and disk-capacity response.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialPlanetFormationValidation
    {
        private const string SolarEnvironmentPath =
            "Assets/CelestialSystems/Definitions/Galactic Environments/Solar-Neighborhood Thin Disk.asset";

        private static readonly double[] SolarOrbitsAu =
        {
            0.35,
            0.7,
            1.2,
            2.2,
            4.5,
            9.0,
            18.0
        };

        [MenuItem(
            "Tools/Celestial Systems/Validate Planet Formation Model")]
        public static void Validate()
        {
            var environment =
                AssetDatabase.LoadAssetAtPath<
                    CelestialGalacticEnvironmentDefinition>(
                        SolarEnvironmentPath);

            if (environment == null)
            {
                Debug.LogError(
                    $"Planet formation validation could not load '{SolarEnvironmentPath}'.");
                return;
            }

            var population =
                new CelestialStellarPopulationSample(
                    1.0,
                    10.0,
                    CelestialStellarEvolutionState.MainSequence);
            var evolution =
                new CelestialStellarEvolutionResult(
                    CelestialStellarEvolutionState.MainSequence,
                    1.0,
                    1.0,
                    1.0,
                    1.0,
                    5772.0);

            if (!CelestialProtoplanetaryDiskModel.TryGenerate(
                    137,
                    population,
                    evolution,
                    environment,
                    out var disk,
                    out var error) ||
                !TryGenerate(
                    811,
                    disk,
                    population,
                    out var first,
                    out error) ||
                !TryGenerate(
                    811,
                    disk,
                    population,
                    out var repeated,
                    out error))
            {
                Debug.LogError(
                    error);
                return;
            }

            if (!AreEqual(
                    first,
                    repeated))
            {
                Debug.LogError(
                    "Planet formation validation failed: identical inputs were not deterministic.");
                return;
            }

            var formedSolids =
                SumSolidMass(
                    first);

            if (formedSolids >
                    disk.InitialSolidMassEarth +
                    1.0e-9 ||
                formedSolids <= 0.0)
            {
                Debug.LogError(
                    "Planet formation validation failed: generated solid cores violated the disk material budget.");
                return;
            }

            var innerVolatiles =
                AverageVolatileFraction(
                    first,
                    disk.FrostLineAstronomicalUnits,
                    false);
            var outerVolatiles =
                AverageVolatileFraction(
                    first,
                    disk.FrostLineAstronomicalUnits,
                    true);

            if (outerVolatiles <=
                innerVolatiles)
            {
                Debug.LogError(
                    "Planet formation validation failed: bodies beyond the frost line were not more volatile-rich.");
                return;
            }

            var richDisk =
                new CelestialProtoplanetaryDiskResult(
                    disk.InitialGasMassSolar *
                        2.0,
                    disk.InitialSolidMassEarth *
                        3.0,
                    disk.InnerBoundaryAstronomicalUnits,
                    disk.OuterBoundaryAstronomicalUnits,
                    disk.FrostLineAstronomicalUnits,
                    disk.PlanetFormationWindowMegayears,
                    disk.EnvironmentalRetentionFraction,
                    CelestialPlanetFormationCapacity.High);

            if (!TryGenerate(
                    811,
                    richDisk,
                    population,
                    out var richPlanets,
                    out error))
            {
                Debug.LogError(
                    error);
                return;
            }

            if (SumSolidMass(
                    richPlanets) <=
                formedSolids)
            {
                Debug.LogError(
                    "Planet formation validation failed: a higher-capacity disk did not form more solid planetary mass.");
                return;
            }

            for (var index = 0;
                index < first.Length;
                index++)
            {
                if (first[index].FinalOrbitAstronomicalUnits >
                        first[index].InitialOrbitAstronomicalUnits ||
                    first[index].RadiusEarth <= 0.0 ||
                    first[index].TotalMassEarth <
                        first[index].SolidCoreMassEarth)
                {
                    Debug.LogError(
                        $"Planet formation validation failed: planet {index + 1} has inconsistent generated properties.");
                    return;
                }
            }

            Debug.Log(
                $"Planet formation PASS. Model v{CelestialPlanetFormationModel.ModelVersion}; {first.Length} deterministic outcomes; material budget, frost-line composition, migration, mass, radius, and disk-capacity checks passed. Formed solids: {formedSolids:0.##} of {disk.InitialSolidMassEarth:0.##} Earth masses.");
        }

        private static bool TryGenerate(
            int seed,
            CelestialProtoplanetaryDiskResult disk,
            CelestialStellarPopulationSample population,
            out CelestialPlanetFormationResult[] planets,
            out string error)
        {
            return
                CelestialPlanetFormationModel.TryGenerate(
                    seed,
                    disk,
                    population,
                    SolarOrbitsAu,
                    out planets,
                    out error);
        }

        private static double SumSolidMass(
            CelestialPlanetFormationResult[] planets)
        {
            var total =
                0.0;

            for (var index = 0;
                index < planets.Length;
                index++)
            {
                total +=
                    planets[index].SolidCoreMassEarth;
            }

            return total;
        }

        private static double AverageVolatileFraction(
            CelestialPlanetFormationResult[] planets,
            double frostLineAu,
            bool outside)
        {
            var total =
                0.0;
            var count =
                0;

            for (var index = 0;
                index < planets.Length;
                index++)
            {
                var isOutside =
                    planets[index].InitialOrbitAstronomicalUnits >=
                    frostLineAu;

                if (isOutside != outside)
                {
                    continue;
                }

                total +=
                    planets[index].VolatileMassFraction;
                count++;
            }

            return
                count > 0
                    ? total /
                        count
                    : 0.0;
        }

        private static bool AreEqual(
            CelestialPlanetFormationResult[] left,
            CelestialPlanetFormationResult[] right)
        {
            if (left.Length !=
                right.Length)
            {
                return false;
            }

            for (var index = 0;
                index < left.Length;
                index++)
            {
                if (left[index].FormationClass !=
                        right[index].FormationClass ||
                    left[index].InitialOrbitAstronomicalUnits !=
                        right[index].InitialOrbitAstronomicalUnits ||
                    left[index].FinalOrbitAstronomicalUnits !=
                        right[index].FinalOrbitAstronomicalUnits ||
                    left[index].SolidCoreMassEarth !=
                        right[index].SolidCoreMassEarth ||
                    left[index].TotalMassEarth !=
                        right[index].TotalMassEarth ||
                    left[index].RadiusEarth !=
                        right[index].RadiusEarth ||
                    left[index].VolatileMassFraction !=
                        right[index].VolatileMassFraction ||
                    left[index].HydrogenHeliumEnvelopeFraction !=
                        right[index].HydrogenHeliumEnvelopeFraction ||
                    left[index].InwardMigrationFraction !=
                        right[index].InwardMigrationFraction)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
