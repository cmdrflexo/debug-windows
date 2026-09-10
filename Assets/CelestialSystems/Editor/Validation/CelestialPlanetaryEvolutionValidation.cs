/*
 * Validates deterministic planetary evolution, irradiation, atmosphere retention, differentiation, and volatile-state responses.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialPlanetaryEvolutionValidation
    {
        [MenuItem(
            "Tools/Celestial Systems/Validate Planetary Evolution Model")]
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
            var temperateFormation =
                CreateFormation(
                    CelestialPlanetFormationClass.Rocky,
                    1.0,
                    1.0,
                    1.0,
                    1.0,
                    0.03,
                    0.0);

            RequireEvaluation(
                101,
                temperateFormation,
                star,
                4.6,
                out var first);
            RequireEvaluation(
                101,
                temperateFormation,
                star,
                4.6,
                out var repeated);

            AssertEqual(
                first.AssumedBondAlbedo,
                repeated.AssumedBondAlbedo,
                "Repeated evaluation changed albedo.");
            AssertEqual(
                first.EquilibriumTemperatureKelvin,
                repeated.EquilibriumTemperatureKelvin,
                "Repeated evaluation changed equilibrium temperature.");
            AssertEqual(
                first.EscapeVelocityMetersPerSecond,
                repeated.EscapeVelocityMetersPerSecond,
                "Repeated evaluation changed escape velocity.");
            Assert(
                first.AtmosphereRetention ==
                    repeated.AtmosphereRetention &&
                first.Differentiation ==
                    repeated.Differentiation &&
                first.VolatileState ==
                    repeated.VolatileState,
                "Repeated evaluation changed a classification.");
            Assert(
                first.EquilibriumTemperatureKelvin >= 240.0 &&
                first.EquilibriumTemperatureKelvin <= 275.0,
                "The Solar-distance equilibrium temperature fell outside its expected calibration range.");

            var hotFormation =
                CreateFormation(
                    CelestialPlanetFormationClass.Rocky,
                    0.1,
                    1.0,
                    1.0,
                    0.05,
                    0.0,
                    0.0);
            var coldFormation =
                CreateFormation(
                    CelestialPlanetFormationClass.VolatileRich,
                    5.0,
                    1.0,
                    1.0,
                    0.5,
                    0.0,
                    0.0);
            RequireEvaluation(
                101,
                hotFormation,
                star,
                4.6,
                out var hot);
            RequireEvaluation(
                101,
                coldFormation,
                star,
                4.6,
                out var cold);
            Assert(
                hot.EquilibriumTemperatureKelvin >
                    first.EquilibriumTemperatureKelvin &&
                first.EquilibriumTemperatureKelvin >
                    cold.EquilibriumTemperatureKelvin,
                "Equilibrium temperature did not decrease with orbital distance.");
            Assert(
                hot.VolatileState ==
                    CelestialPlanetVolatileState.VaporDominated,
                "The hot volatile-bearing case was not vapor dominated.");
            Assert(
                cold.VolatileState ==
                    CelestialPlanetVolatileState.Frozen,
                "The cold volatile-rich case was not frozen.");

            var smallFormation =
                CreateFormation(
                    CelestialPlanetFormationClass.Rocky,
                    1.0,
                    0.08,
                    0.45,
                    0.01,
                    0.0,
                    0.0);
            var massiveFormation =
                CreateFormation(
                    CelestialPlanetFormationClass.Rocky,
                    1.0,
                    5.0,
                    1.6,
                    0.03,
                    0.0,
                    0.0);
            RequireEvaluation(
                202,
                smallFormation,
                star,
                4.6,
                out var small);
            RequireEvaluation(
                202,
                massiveFormation,
                star,
                4.6,
                out var massive);
            Assert(
                massive.EscapeVelocityMetersPerSecond >
                    small.EscapeVelocityMetersPerSecond,
                "The more massive case did not have greater escape velocity.");
            Assert(
                massive.AtmosphereRetention >
                    small.AtmosphereRetention,
                "Atmosphere retention did not improve with escape velocity.");
            Assert(
                massive.Differentiation ==
                    CelestialPlanetDifferentiation.Differentiated,
                "The massive rocky case was not differentiated.");

            var giantFormation =
                CreateFormation(
                    CelestialPlanetFormationClass.Giant,
                    5.0,
                    100.0,
                    11.0,
                    10.0,
                    0.8,
                    0.7);
            RequireEvaluation(
                303,
                giantFormation,
                star,
                4.6,
                out var giant);
            Assert(
                giant.AtmosphereRetention ==
                    CelestialAtmosphereRetention.Massive &&
                giant.VolatileState ==
                    CelestialPlanetVolatileState.DeepEnvelope,
                "The giant case did not retain its deep envelope.");

            Debug.Log(
                $"Planetary evolution PASS. Model v{CelestialPlanetaryEvolutionModel.ModelVersion}; deterministic, irradiation, escape, differentiation, and volatile-state checks passed. Solar-distance case: {first.EquilibriumTemperatureKelvin:0.#} K equilibrium temperature, {first.EscapeVelocityMetersPerSecond / 1000.0:0.##} km/s escape velocity, {first.AtmosphereRetention} atmosphere retention.");
        }

        private static CelestialPlanetFormationResult CreateFormation(
            CelestialPlanetFormationClass formationClass,
            double orbitAu,
            double massEarth,
            double radiusEarth,
            double volatileFraction,
            double envelopeFraction,
            double migrationFraction)
        {
            return
                new CelestialPlanetFormationResult(
                    formationClass,
                    orbitAu /
                        (1.0 -
                            migrationFraction),
                    orbitAu,
                    massEarth *
                        (1.0 -
                            envelopeFraction),
                    massEarth,
                    radiusEarth,
                    volatileFraction,
                    envelopeFraction,
                    migrationFraction);
        }

        private static void RequireEvaluation(
            int seed,
            CelestialPlanetFormationResult formation,
            CelestialStellarEvolutionResult star,
            double ageGigayears,
            out CelestialPlanetaryEvolutionResult result)
        {
            if (!CelestialPlanetaryEvolutionModel.TryEvaluate(
                    seed,
                    formation,
                    star,
                    ageGigayears,
                    out result,
                    out var error))
            {
                throw new InvalidOperationException(
                    error);
            }
        }

        private static void AssertEqual(
            double left,
            double right,
            string message)
        {
            Assert(
                BitConverter.DoubleToInt64Bits(left) ==
                    BitConverter.DoubleToInt64Bits(right),
                message);
        }

        private static void Assert(
            bool condition,
            string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    message);
            }
        }
    }
}
