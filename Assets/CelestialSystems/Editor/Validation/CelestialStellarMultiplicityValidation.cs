/*
 * Validates deterministic, mass-dependent stellar multiplicity and hierarchical stability.
 */

using System.Text;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialStellarMultiplicityValidation
    {
        private const int SampleCount = 10000;

        [MenuItem("Tools/Celestial Systems/Validate Stellar Multiplicity Model")]
        public static void Validate()
        {
            if (!TryMeasure(
                    0.25,
                    out var lowMass,
                    out var error) ||
                !TryMeasure(
                    1.0,
                    out var solar,
                    out error) ||
                !TryMeasure(
                    10.0,
                    out var massive,
                    out error))
            {
                Debug.LogError(error);
                return;
            }

            if (lowMass.MultipleCount >= solar.MultipleCount ||
                solar.MultipleCount >= massive.MultipleCount ||
                lowMass.MultipleCount < 1800 ||
                lowMass.MultipleCount > 3000 ||
                solar.MultipleCount < 3700 ||
                solar.MultipleCount > 5100 ||
                massive.MultipleCount < 7400 ||
                massive.MultipleCount > 9000 ||
                solar.TripleCount <= 0 ||
                massive.TripleCount <= solar.TripleCount)
            {
                Debug.LogError(
                    "Stellar multiplicity validation failed: mass-dependent multiplicity or triple occurrence fell outside the v1 calibration.");
                return;
            }

            Debug.Log(
                $"Stellar multiplicity PASS. Model v{CelestialStellarMultiplicityModel.ModelVersion}; {SampleCount} deterministic cases per mass bin; companion properties and hierarchical stability passed. " +
                $"Multiple systems: {lowMass.MultipleCount} at 0.25 solar masses, {solar.MultipleCount} at 1 solar mass, and {massive.MultipleCount} at 10 solar masses; solar-mass triples: {solar.TripleCount}.");
        }

        private static bool TryMeasure(
            double primaryMassSolar,
            out Measurement measurement,
            out string error)
        {
            var multipleCount = 0;
            var tripleCount = 0;

            for (var seed = 1;
                seed <= SampleCount;
                seed++)
            {
                if (!CelestialStellarMultiplicityModel.TryGenerate(
                        seed,
                        primaryMassSolar,
                        0.0,
                        10.0,
                        out var first,
                        out error) ||
                    !CelestialStellarMultiplicityModel.TryGenerate(
                        seed,
                        primaryMassSolar,
                        0.0,
                        10.0,
                        out var repeated,
                        out error))
                {
                    measurement = default;
                    return false;
                }

                if (BuildSignature(first) !=
                    BuildSignature(repeated))
                {
                    measurement = default;
                    error =
                        $"Stellar multiplicity validation failed: seed {seed} was not deterministic.";
                    return false;
                }

                if (first.StarCount > 1)
                {
                    multipleCount++;
                }

                if (first.Multiplicity ==
                    CelestialStellarMultiplicity.HierarchicalTriple)
                {
                    tripleCount++;

                    if (first.Companions[1].PeriapsisAstronomicalUnits <=
                        first.Companions[0].ApoapsisAstronomicalUnits *
                            5.0)
                    {
                        measurement = default;
                        error =
                            $"Stellar multiplicity validation failed: seed {seed} produced an unstable triple.";
                        return false;
                    }
                }
            }

            measurement =
                new Measurement(
                    multipleCount,
                    tripleCount);
            error = string.Empty;
            return true;
        }

        private static string BuildSignature(
            CelestialStellarMultiplicityResult result)
        {
            var signature =
                new StringBuilder();
            signature.Append(result.Multiplicity);

            foreach (var companion in result.Companions)
            {
                signature.Append(':').Append(companion.MassRatio.ToString("R"))
                    .Append(':').Append(companion.BirthMassSolar.ToString("R"))
                    .Append(':').Append(companion.SemiMajorAxisAstronomicalUnits.ToString("R"))
                    .Append(':').Append(companion.Eccentricity.ToString("R"))
                    .Append(':').Append(companion.InclinationDegrees.ToString("R"))
                    .Append(':').Append(companion.OrbitsInnerPairBarycenter);
            }

            return signature.ToString();
        }

        private readonly struct Measurement
        {
            public Measurement(
                int multipleCount,
                int tripleCount)
            {
                MultipleCount = multipleCount;
                TripleCount = tripleCount;
            }

            public int MultipleCount { get; }
            public int TripleCount { get; }
        }
    }
}
