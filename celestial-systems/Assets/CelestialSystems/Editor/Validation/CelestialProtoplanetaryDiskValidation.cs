/*
 * Validates deterministic disk generation and its response to metallicity and birth environment.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialProtoplanetaryDiskValidation
    {
        private const string SolarEnvironmentPath =
            "Assets/CelestialSystems/Definitions/Galactic Environments/Solar-Neighborhood Thin Disk.asset";

        [MenuItem(
            "Tools/Celestial Systems/Validate Protoplanetary Disk Model")]
        public static void Validate()
        {
            var solarEnvironment =
                AssetDatabase.LoadAssetAtPath<
                    CelestialGalacticEnvironmentDefinition>(
                        SolarEnvironmentPath);

            if (solarEnvironment == null)
            {
                Debug.LogError(
                    $"Protoplanetary disk validation could not load '{SolarEnvironmentPath}'.");
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

            if (!TryGenerate(
                    137,
                    population,
                    evolution,
                    solarEnvironment,
                    out var first,
                    out var error) ||
                !TryGenerate(
                    137,
                    population,
                    evolution,
                    solarEnvironment,
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
                    "Protoplanetary disk validation failed: identical inputs were not deterministic.");
                return;
            }

            if (first.InitialSolidMassEarth < 10.0 ||
                first.InitialSolidMassEarth > 100.0 ||
                (int)first.FormationCapacity <
                    (int)CelestialPlanetFormationCapacity.Typical ||
                first.FrostLineAstronomicalUnits < 2.6 ||
                first.FrostLineAstronomicalUnits > 2.8 ||
                first.InnerBoundaryAstronomicalUnits <= 0.0 ||
                first.OuterBoundaryAstronomicalUnits <=
                    first.InnerBoundaryAstronomicalUnits)
            {
                Debug.LogError(
                    "Protoplanetary disk validation failed: Solar-analog material budget or boundaries were outside the expected v1 range.");
                return;
            }

            var metalPoor =
                UnityEngine.Object.Instantiate(
                    solarEnvironment);
            var irradiated =
                UnityEngine.Object.Instantiate(
                    solarEnvironment);

            try
            {
                SetDouble(
                    metalPoor,
                    "ironMetallicityDex",
                    -1.0);
                SetDouble(
                    irradiated,
                    "birthFarUltravioletFieldG0",
                    10000.0);

                if (!TryGenerate(
                        137,
                        population,
                        evolution,
                        metalPoor,
                        out var poorDisk,
                        out error) ||
                    !TryGenerate(
                        137,
                        population,
                        evolution,
                        irradiated,
                        out var irradiatedDisk,
                        out error))
                {
                    Debug.LogError(
                        error);
                    return;
                }

                if (poorDisk.InitialSolidMassEarth >=
                    first.InitialSolidMassEarth)
                {
                    Debug.LogError(
                        "Protoplanetary disk validation failed: lower metallicity did not reduce the solid-material budget.");
                    return;
                }

                if (irradiatedDisk.EnvironmentalRetentionFraction >=
                        first.EnvironmentalRetentionFraction ||
                    irradiatedDisk.OuterBoundaryAstronomicalUnits >=
                        first.OuterBoundaryAstronomicalUnits ||
                    irradiatedDisk.PlanetFormationWindowMegayears >=
                        first.PlanetFormationWindowMegayears)
                {
                    Debug.LogError(
                        "Protoplanetary disk validation failed: strong birth radiation did not reduce disk retention, size, and formation time.");
                    return;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    metalPoor);
                UnityEngine.Object.DestroyImmediate(
                    irradiated);
            }

            Debug.Log(
                $"Protoplanetary disk PASS. Model v{CelestialProtoplanetaryDiskModel.ModelVersion}; deterministic, metallicity, radiation, and boundary checks passed. Solar analog: {first.InitialSolidMassEarth:0.##} Earth masses of solids, frost line {first.FrostLineAstronomicalUnits:0.##} AU, outer boundary {first.OuterBoundaryAstronomicalUnits:0.##} AU, formation window {first.PlanetFormationWindowMegayears:0.##} Myr, capacity {first.FormationCapacity}.");
        }

        private static bool TryGenerate(
            int seed,
            CelestialStellarPopulationSample population,
            CelestialStellarEvolutionResult evolution,
            CelestialGalacticEnvironmentDefinition environment,
            out CelestialProtoplanetaryDiskResult result,
            out string error)
        {
            return
                CelestialProtoplanetaryDiskModel.TryGenerate(
                    seed,
                    population,
                    evolution,
                    environment,
                    out result,
                    out error);
        }

        private static void SetDouble(
            UnityEngine.Object target,
            string propertyName,
            double value)
        {
            var serialized =
                new SerializedObject(
                    target);
            var property =
                serialized.FindProperty(
                    propertyName);
            property.doubleValue =
                value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool AreEqual(
            CelestialProtoplanetaryDiskResult left,
            CelestialProtoplanetaryDiskResult right)
        {
            return
                left.InitialGasMassSolar ==
                    right.InitialGasMassSolar &&
                left.InitialSolidMassEarth ==
                    right.InitialSolidMassEarth &&
                left.InnerBoundaryAstronomicalUnits ==
                    right.InnerBoundaryAstronomicalUnits &&
                left.OuterBoundaryAstronomicalUnits ==
                    right.OuterBoundaryAstronomicalUnits &&
                left.FrostLineAstronomicalUnits ==
                    right.FrostLineAstronomicalUnits &&
                left.PlanetFormationWindowMegayears ==
                    right.PlanetFormationWindowMegayears &&
                left.EnvironmentalRetentionFraction ==
                    right.EnvironmentalRetentionFraction &&
                left.FormationCapacity ==
                    right.FormationCapacity;
        }
    }
}
