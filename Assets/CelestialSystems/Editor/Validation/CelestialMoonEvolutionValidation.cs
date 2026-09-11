/*
 * Validates deterministic moon evolution, irradiation, escape, tidal locking, and relative tidal-heating response.
 */

using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialMoonEvolutionValidation
    {
        [MenuItem(
            "Tools/Celestial Systems/Validate Moon Evolution Model")]
        public static void Validate()
        {
            var star =
                new CelestialStellarEvolutionResult(
                    CelestialStellarEvolutionState.MainSequence,
                    1.0,
                    1.0,
                    1.0,
                    1.0,
                    5772.0);
            var giantPlanet =
                CreatePlanet(
                    CelestialPlanetFormationClass.Giant,
                    317.8,
                    11.2,
                    0.08,
                    0.9);
            var earthLike =
                CreatePlanet(
                    CelestialPlanetFormationClass.Rocky,
                    1.0,
                    1.0,
                    0.02,
                    0.0);
            var ioLike =
                CreateMoon(
                    CelestialMoonFormationOrigin.RegularDisk,
                    0.01495,
                    0.286,
                    0.01,
                    421700000.0,
                    0.0041,
                    150000000.0,
                    20000000000.0);
            var wideMoon =
                CreateMoon(
                    CelestialMoonFormationOrigin.RegularDisk,
                    0.01495,
                    0.286,
                    0.5,
                    3000000000.0,
                    0.001,
                    150000000.0,
                    20000000000.0);
            var lunarAnalog =
                CreateMoon(
                    CelestialMoonFormationOrigin.GiantImpact,
                    0.0123,
                    0.2727,
                    0.001,
                    384400000.0,
                    0.0549,
                    18000000.0,
                    1000000000.0);

            if (!TryEvaluate(
                    73,
                    ioLike,
                    giantPlanet,
                    star,
                    4.6,
                    5.2,
                    out var io,
                    out var error) ||
                !TryEvaluate(
                    73,
                    ioLike,
                    giantPlanet,
                    star,
                    4.6,
                    5.2,
                    out var repeatedIo,
                    out error) ||
                !TryEvaluate(
                    91,
                    wideMoon,
                    giantPlanet,
                    star,
                    4.6,
                    5.2,
                    out var wide,
                    out error) ||
                !TryEvaluate(
                    117,
                    lunarAnalog,
                    earthLike,
                    star,
                    4.6,
                    1.0,
                    out var lunar,
                    out error))
            {
                Debug.LogError(
                    error);
                return;
            }

            if (!AreEqual(
                    io,
                    repeatedIo))
            {
                Debug.LogError(
                    "Moon evolution validation failed: identical inputs were not deterministic.");
                return;
            }

            if (io.TidalHeating !=
                    CelestialMoonTidalHeating.Significant ||
                io.RelativeTidalHeatingIndex < 0.95 ||
                io.RelativeTidalHeatingIndex > 1.05 ||
                wide.RelativeTidalHeatingIndex >=
                    io.RelativeTidalHeatingIndex ||
                wide.TidalHeating !=
                    CelestialMoonTidalHeating.Negligible)
            {
                Debug.LogError(
                    "Moon evolution validation failed: tidal heating did not respond correctly to eccentricity and orbital distance.");
                return;
            }

            if (!io.LikelyTidallyLocked ||
                !lunar.LikelyTidallyLocked ||
                lunar.AtmosphereRetention !=
                    CelestialAtmosphereRetention.None ||
                wide.VolatileState !=
                    CelestialPlanetVolatileState.Frozen)
            {
                Debug.LogError(
                    "Moon evolution validation failed: tidal locking, atmosphere retention, or volatile state was inconsistent with the reference cases.");
                return;
            }

            if (lunar.MigrationSensitivity !=
                CelestialMoonTidalMigrationSensitivity.High)
            {
                Debug.LogError(
                    "Moon evolution validation failed: a high moon-to-planet mass ratio was not marked as tidally migration-sensitive.");
                return;
            }

            Debug.Log(
                $"Moon evolution PASS. Model v{CelestialMoonEvolutionModel.ModelVersion}; deterministic irradiation, escape, volatile-state, differentiation, tidal-locking, tidal-heating, and migration-sensitivity checks passed. " +
                $"Io analog: {io.EquilibriumTemperatureKelvin:0.#} K, {io.EscapeVelocityMetersPerSecond / 1000.0:0.##} km/s escape velocity, relative tidal-heating index {io.RelativeTidalHeatingIndex:0.###}.");
        }

        private static bool TryEvaluate(
            int seed,
            CelestialMoonFormationResult moon,
            CelestialPlanetFormationResult planet,
            CelestialStellarEvolutionResult star,
            double ageGigayears,
            double planetOrbitAstronomicalUnits,
            out CelestialMoonEvolutionResult result,
            out string error)
        {
            return
                CelestialMoonEvolutionModel.TryEvaluate(
                    seed,
                    moon,
                    planet,
                    star,
                    ageGigayears,
                    planetOrbitAstronomicalUnits,
                    out result,
                    out error);
        }

        private static CelestialPlanetFormationResult CreatePlanet(
            CelestialPlanetFormationClass formationClass,
            double massEarth,
            double radiusEarth,
            double volatileFraction,
            double envelopeFraction)
        {
            return
                new CelestialPlanetFormationResult(
                    formationClass,
                    1.0,
                    1.0,
                    massEarth *
                        (1.0 -
                            envelopeFraction),
                    massEarth,
                    radiusEarth,
                    volatileFraction,
                    envelopeFraction,
                    0.0);
        }

        private static CelestialMoonFormationResult CreateMoon(
            CelestialMoonFormationOrigin origin,
            double massEarth,
            double radiusEarth,
            double volatileFraction,
            double orbitalRadiusMeters,
            double eccentricity,
            double rocheLimitMeters,
            double stableOuterLimitMeters)
        {
            return
                new CelestialMoonFormationResult(
                    origin,
                    massEarth,
                    radiusEarth,
                    volatileFraction,
                    orbitalRadiusMeters,
                    eccentricity,
                    0.5,
                    CelestialOrbitDirection.Prograde,
                    rocheLimitMeters,
                    stableOuterLimitMeters);
        }

        private static bool AreEqual(
            CelestialMoonEvolutionResult left,
            CelestialMoonEvolutionResult right)
        {
            return
                left.AssumedBondAlbedo ==
                    right.AssumedBondAlbedo &&
                left.EquilibriumTemperatureKelvin ==
                    right.EquilibriumTemperatureKelvin &&
                left.EscapeVelocityMetersPerSecond ==
                    right.EscapeVelocityMetersPerSecond &&
                left.AtmosphereRetention ==
                    right.AtmosphereRetention &&
                left.Differentiation ==
                    right.Differentiation &&
                left.VolatileState ==
                    right.VolatileState &&
                left.LikelyTidallyLocked ==
                    right.LikelyTidallyLocked &&
                left.RelativeTidalHeatingIndex ==
                    right.RelativeTidalHeatingIndex &&
                left.TidalHeating ==
                    right.TidalHeating &&
                left.MigrationSensitivity ==
                    right.MigrationSensitivity;
        }
    }
}
