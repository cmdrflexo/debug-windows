/*
 * Validates deterministic moon formation, origin-dependent mass budgets, and stable satellite orbits.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialMoonFormationValidation
    {
        [MenuItem(
            "Tools/Celestial Systems/Validate Moon Formation Model")]
        public static void Validate()
        {
            var earthLike =
                CreatePlanet(
                    CelestialPlanetFormationClass.Rocky,
                    1.0,
                    1.0,
                    0.02,
                    0.0);
            var giant =
                CreatePlanet(
                    CelestialPlanetFormationClass.Giant,
                    317.8,
                    11.2,
                    0.08,
                    0.9);
            var volatileRich =
                CreatePlanet(
                    CelestialPlanetFormationClass.VolatileRich,
                    8.0,
                    2.5,
                    0.55,
                    0.0);

            var regularSystems = 0;
            var impactSystems = 0;
            var capturedSystems = 0;
            var moonlessSystems = 0;
            var totalMoons = 0;
            var deterministicSeed = 0;
            CelestialMoonSystemFormationResult deterministicResult = default;

            for (var seed = 1;
                seed <= 256;
                seed++)
            {
                if (!TryGenerateAndValidate(
                        seed,
                        giant,
                        5.2,
                        out var giantSystem,
                        out var error) ||
                    !TryGenerateAndValidate(
                        seed,
                        earthLike,
                        1.0,
                        out var rockySystem,
                        out error) ||
                    !TryGenerateAndValidate(
                        seed,
                        volatileRich,
                        12.0,
                        out var volatileSystem,
                        out error))
                {
                    Debug.LogError(
                        error);
                    return;
                }

                Accumulate(
                    giantSystem,
                    ref regularSystems,
                    ref impactSystems,
                    ref capturedSystems,
                    ref moonlessSystems,
                    ref totalMoons);
                Accumulate(
                    rockySystem,
                    ref regularSystems,
                    ref impactSystems,
                    ref capturedSystems,
                    ref moonlessSystems,
                    ref totalMoons);
                Accumulate(
                    volatileSystem,
                    ref regularSystems,
                    ref impactSystems,
                    ref capturedSystems,
                    ref moonlessSystems,
                    ref totalMoons);

                if (deterministicSeed == 0 &&
                    giantSystem.Moons.Length > 0)
                {
                    deterministicSeed = seed;
                    deterministicResult = giantSystem;
                }
            }

            if (regularSystems == 0 ||
                impactSystems == 0 ||
                capturedSystems == 0 ||
                moonlessSystems == 0 ||
                deterministicSeed == 0)
            {
                Debug.LogError(
                    "Moon formation validation failed: the sample did not produce every modeled origin and a moonless outcome.");
                return;
            }

            if (!CelestialMoonFormationModel.TryGenerate(
                    deterministicSeed,
                    giant,
                    5.2,
                    1.0,
                    out var repeated,
                    out var deterministicError) ||
                !AreEqual(
                    deterministicResult,
                    repeated))
            {
                Debug.LogError(
                    string.IsNullOrWhiteSpace(
                        deterministicError)
                            ? "Moon formation validation failed: identical inputs did not reproduce the same satellite system."
                            : deterministicError);
                return;
            }

            if (!CelestialMoonFormationModel.TryGenerate(
                    91,
                    earthLike,
                    0.003,
                    1.0,
                    out var closeIn,
                    out var closeError) ||
                closeIn.Moons.Length != 0)
            {
                Debug.LogError(
                    string.IsNullOrWhiteSpace(
                        closeError)
                            ? "Moon formation validation failed: a close-in Earth-mass planet retained a moon despite having no stable modeled orbital region."
                            : closeError);
                return;
            }

            Debug.Log(
                $"Moon formation PASS. Model v{CelestialMoonFormationModel.ModelVersion}; deterministic, regular-disk, giant-impact, capture, mass-budget, Roche-limit, Hill-stability, and non-crossing checks passed. " +
                $"Generated {totalMoons} moons across 768 planet cases: {regularSystems} regular systems, {impactSystems} impact systems, {capturedSystems} captured systems, and {moonlessSystems} moonless outcomes.");
        }

        private static bool TryGenerateAndValidate(
            int seed,
            CelestialPlanetFormationResult planet,
            double orbitAstronomicalUnits,
            out CelestialMoonSystemFormationResult result,
            out string error)
        {
            if (!CelestialMoonFormationModel.TryGenerate(
                    seed,
                    planet,
                    orbitAstronomicalUnits,
                    1.0,
                    out result,
                    out error))
            {
                return false;
            }

            var totalMass = 0.0;
            var previousApoapsis = 0.0;

            for (var index = 0;
                index < result.Moons.Length;
                index++)
            {
                var moon =
                    result.Moons[index];
                var periapsis =
                    moon.OrbitalRadiusMeters *
                    (1.0 -
                        moon.Eccentricity);
                var apoapsis =
                    moon.OrbitalRadiusMeters *
                    (1.0 +
                        moon.Eccentricity);

                if (!PositiveFinite(
                        moon.MassEarth) ||
                    !PositiveFinite(
                        moon.RadiusEarth) ||
                    !Fraction(
                        moon.VolatileMassFraction) ||
                    periapsis <=
                        moon.RocheLimitMeters ||
                    apoapsis >=
                        moon.StableOuterLimitMeters ||
                    previousApoapsis >=
                        periapsis ||
                    moon.Origin !=
                        CelestialMoonFormationOrigin.Captured &&
                    moon.Direction !=
                        CelestialOrbitDirection.Prograde)
                {
                    error =
                        $"Moon formation validation failed for seed {seed}: moon {index + 1} has inconsistent mass, composition, direction, or orbital bounds.";
                    return false;
                }

                totalMass +=
                    moon.MassEarth;
                previousApoapsis =
                    apoapsis;
            }

            if (totalMass >
                    result.SatelliteMassBudgetEarth +
                        1.0e-12 ||
                result.Moons.Length > 0 &&
                Math.Abs(
                    totalMass -
                        result.SatelliteMassBudgetEarth) >
                    Math.Max(
                        1.0e-12,
                        result.SatelliteMassBudgetEarth *
                            1.0e-12))
            {
                error =
                    $"Moon formation validation failed for seed {seed}: generated moon mass does not match the satellite-system budget.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static void Accumulate(
            CelestialMoonSystemFormationResult system,
            ref int regularSystems,
            ref int impactSystems,
            ref int capturedSystems,
            ref int moonlessSystems,
            ref int totalMoons)
        {
            totalMoons +=
                system.Moons.Length;

            if (system.Moons.Length == 0)
            {
                moonlessSystems++;
                return;
            }

            switch (system.Moons[0].Origin)
            {
                case CelestialMoonFormationOrigin.RegularDisk:
                    regularSystems++;
                    break;
                case CelestialMoonFormationOrigin.GiantImpact:
                    impactSystems++;
                    break;
                case CelestialMoonFormationOrigin.Captured:
                    capturedSystems++;
                    break;
            }
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

        private static bool AreEqual(
            CelestialMoonSystemFormationResult left,
            CelestialMoonSystemFormationResult right)
        {
            if (left.HillRadiusMeters !=
                    right.HillRadiusMeters ||
                left.SatelliteMassBudgetEarth !=
                    right.SatelliteMassBudgetEarth ||
                left.Moons.Length !=
                    right.Moons.Length)
            {
                return false;
            }

            for (var index = 0;
                index < left.Moons.Length;
                index++)
            {
                var a = left.Moons[index];
                var b = right.Moons[index];

                if (a.Origin != b.Origin ||
                    a.MassEarth != b.MassEarth ||
                    a.RadiusEarth != b.RadiusEarth ||
                    a.VolatileMassFraction != b.VolatileMassFraction ||
                    a.OrbitalRadiusMeters != b.OrbitalRadiusMeters ||
                    a.Eccentricity != b.Eccentricity ||
                    a.InclinationDegrees != b.InclinationDegrees ||
                    a.Direction != b.Direction ||
                    a.RocheLimitMeters != b.RocheLimitMeters ||
                    a.StableOuterLimitMeters != b.StableOuterLimitMeters)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Fraction(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value >= 0.0 &&
                value <= 1.0;
        }

        private static bool PositiveFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
        }
    }
}
