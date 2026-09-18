/*
 * Validates deterministic stellar magnetic activity and visible-beam eligibility.
 */

using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialStellarMagneticActivityValidation
    {
        [MenuItem("Tools/Celestial Systems/Validate Stellar Magnetic Activity Model")]
        public static void Validate()
        {
            var slowDwarf = Star(CelestialStellarEvolutionState.MainSequence, 0.25, 3200.0);
            var neutronStar = Star(CelestialStellarEvolutionState.NeutronStar, 1.4, 1000000.0);
            var blackHole = Star(CelestialStellarEvolutionState.BlackHole, 8.0, 0.0);
            var slowRotation = new CelestialBodyRotationResult(2400.0, 12.0, CelestialSpinDirection.Prograde, CelestialSpinState.FreeRotating);
            var fastRotation = new CelestialBodyRotationResult(8.0, 12.0, CelestialSpinDirection.Prograde, CelestialSpinState.FreeRotating);

            if (!CelestialStellarMagneticActivityModel.TryEvaluate(31, slowDwarf, slowRotation, out var quiet, out var error) ||
                !CelestialStellarMagneticActivityModel.TryEvaluate(72, blackHole, fastRotation, out var hole, out error) ||
                !CelestialStellarMagneticActivityModel.TryEvaluate(31, slowDwarf, slowRotation, out var repeated, out error))
            {
                Debug.LogError(error);
                return;
            }

            var pulsarFound = false;
            for (var seed = 1; seed <= 100 && !pulsarFound; seed++)
            {
                if (!CelestialStellarMagneticActivityModel.TryEvaluate(seed, neutronStar, fastRotation, out var candidate, out error))
                {
                    Debug.LogError(error);
                    return;
                }

                pulsarFound = candidate.HasVisibleBeam &&
                    candidate.Activity == CelestialStellarMagneticActivity.Pulsar;
            }

            if (quiet.HasVisibleBeam || hole.HasVisibleBeam ||
                hole.Activity != CelestialStellarMagneticActivity.None ||
                quiet.Activity != repeated.Activity ||
                quiet.HasVisibleBeam != repeated.HasVisibleBeam ||
                quiet.MagneticAxisTiltDegrees != repeated.MagneticAxisTiltDegrees ||
                !pulsarFound)
            {
                Debug.LogError("Stellar magnetic activity validation failed: deterministic, quiet-star, black-hole, or neutron-star checks failed.");
                return;
            }

            Debug.Log("Stellar magnetic activity PASS. Model v1; deterministic magnetic classification, quiet low-mass, black-hole exclusion, and neutron-star beam checks passed.");
        }

        private static CelestialStellarEvolutionResult Star(CelestialStellarEvolutionState state, double mass, double temperature)
        {
            return new CelestialStellarEvolutionResult(state, mass, mass, state == CelestialStellarEvolutionState.BlackHole ? 0.00001 : 1.0, 0.0, temperature);
        }
    }
}
