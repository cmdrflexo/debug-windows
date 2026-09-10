/*
 * Validates post-main-sequence planet survival, engulfment, core-collapse disruption, and adiabatic orbital expansion.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialPostMainSequenceSystemEvolutionValidation
    {
        [MenuItem(
            "Tools/Celestial Systems/Validate Post-Main-Sequence System Evolution")]
        public static void Validate()
        {
            var mainSequenceStar =
                CreateStar(
                    CelestialStellarEvolutionState.MainSequence,
                    1.0,
                    1.0);
            RequireEvaluation(
                mainSequenceStar,
                1.0,
                0.1,
                out var unchanged);
            Assert(
                unchanged.Survived &&
                unchanged.Outcome ==
                    CelestialPostMainSequencePlanetOutcome.Unchanged,
                "The main-sequence planet did not remain unchanged.");
            AssertEqual(
                1.0,
                unchanged.PresentOrbitAstronomicalUnits,
                "The main-sequence orbit changed.");

            var whiteDwarf =
                CreateStar(
                    CelestialStellarEvolutionState.WhiteDwarf,
                    1.765,
                    0.586);
            RequireEvaluation(
                whiteDwarf,
                0.235,
                0.0,
                out var engulfed);
            Assert(
                !engulfed.Survived &&
                engulfed.Outcome ==
                    CelestialPostMainSequencePlanetOutcome.EngulfedDuringGiantPhase,
                "The close seed-512-like planet survived the progenitor's giant envelope.");

            RequireEvaluation(
                whiteDwarf,
                3.0,
                0.05,
                out var expanded);
            var expectedExpansion =
                1.765 /
                0.586;
            Assert(
                expanded.Survived &&
                expanded.Outcome ==
                    CelestialPostMainSequencePlanetOutcome.ExpandedAfterMassLoss,
                "The distant white-dwarf planet did not survive and expand.");
            AssertEqual(
                expectedExpansion,
                expanded.OrbitalExpansionFactor,
                "The white-dwarf orbital expansion factor is incorrect.");
            AssertEqual(
                3.0 *
                    expectedExpansion,
                expanded.PresentOrbitAstronomicalUnits,
                "The white-dwarf planet's present orbit is incorrect.");

            RequireEvaluation(
                whiteDwarf,
                2.2,
                0.2,
                out var eccentricEngulfment);
            Assert(
                !eccentricEngulfment.Survived &&
                eccentricEngulfment.Outcome ==
                    CelestialPostMainSequencePlanetOutcome.EngulfedDuringGiantPhase,
                "The survival check did not use orbital periapsis.");

            var neutronStar =
                CreateStar(
                    CelestialStellarEvolutionState.NeutronStar,
                    12.0,
                    1.4);
            RequireEvaluation(
                neutronStar,
                10.0,
                0.0,
                out var disrupted);
            Assert(
                !disrupted.Survived &&
                disrupted.Outcome ==
                    CelestialPostMainSequencePlanetOutcome.DisruptedByCoreCollapse,
                "The v1 core-collapse case was not removed.");

            Debug.Log(
                $"Post-main-sequence system evolution PASS. Model v{CelestialPostMainSequenceSystemEvolutionModel.ModelVersion}; unchanged, giant-envelope engulfment, periapsis, adiabatic expansion, and core-collapse disruption checks passed. White-dwarf survivor expanded from 3 AU to {expanded.PresentOrbitAstronomicalUnits:0.###} AU.");
        }

        private static CelestialStellarEvolutionResult CreateStar(
            CelestialStellarEvolutionState state,
            double initialMassSolar,
            double currentMassSolar)
        {
            return
                new CelestialStellarEvolutionResult(
                    state,
                    initialMassSolar,
                    currentMassSolar,
                    0.01,
                    0.001,
                    10000.0);
        }

        private static void RequireEvaluation(
            CelestialStellarEvolutionResult star,
            double orbitAstronomicalUnits,
            double eccentricity,
            out CelestialPostMainSequencePlanetResult result)
        {
            if (!CelestialPostMainSequenceSystemEvolutionModel.TryEvaluatePlanet(
                    star,
                    orbitAstronomicalUnits,
                    eccentricity,
                    out result,
                    out var error))
            {
                throw new InvalidOperationException(
                    error);
            }
        }

        private static void AssertEqual(
            double expected,
            double actual,
            string message)
        {
            Assert(
                BitConverter.DoubleToInt64Bits(
                    expected) ==
                BitConverter.DoubleToInt64Bits(
                    actual),
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
