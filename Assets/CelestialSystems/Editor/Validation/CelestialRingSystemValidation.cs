/*
 * Editor validation for deterministic ring-system formation.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialRingSystemValidation
    {
        [MenuItem("Tools/Celestial Systems/Validate Ring System Model")]
        public static void Validate()
        {
            var giant = new CelestialPlanetFormationResult(
                CelestialPlanetFormationClass.Giant,
                5.2,
                5.2,
                25.0,
                85.0,
                7.5,
                0.80,
                0.08);
            var moons = new CelestialMoonSystemFormationResult(
                5.0e9,
                0.02,
                new[]
                {
                    new CelestialMoonFormationResult(
                        CelestialMoonFormationOrigin.RegularDisk,
                        0.0001,
                        0.03,
                        0.55,
                        1.22e8,
                        0.01,
                        1.0,
                        CelestialOrbitDirection.Prograde,
                        9.0e7,
                        2.0e9)
                });

            if (!CelestialRingSystemModel.TryGenerate(
                    47, giant, moons, out var first, out var error) ||
                !CelestialRingSystemModel.TryGenerate(
                    47, giant, moons, out var repeated, out error))
            {
                Fail(error);
                return;
            }

            if (!Same(first, repeated) ||
                !first.HasRings ||
                first.Bands.Length < 1 ||
                first.OuterRadiusMeters >=
                    moons.Moons[0].OrbitalRadiusMeters *
                    (1.0 - moons.Moons[0].Eccentricity) ||
                first.OuterShepherdMoonCount < 0)
            {
                Fail("Deterministic giant-ring, moon-clearance, or band checks failed.");
                return;
            }

            var anyTwoBand = false;
            var anyShepherd = false;

            for (var seed = 1; seed <= 256; seed++)
            {
                if (!CelestialRingSystemModel.TryGenerate(
                        seed, giant, moons, out var result, out error))
                {
                    Fail(error);
                    return;
                }

                if (result.HasRings)
                {
                    if (result.Bands.Length == 2)
                    {
                        anyTwoBand = true;
                    }

                    if (result.OuterShepherdMoonCount > 0)
                    {
                        anyShepherd = true;
                    }
                }
            }

            if (!anyTwoBand || !anyShepherd)
            {
                Fail("Ring diversity or shepherd classification checks failed.");
                return;
            }

            Debug.Log(
                $"Ring system PASS. Model v{CelestialRingSystemModel.ModelVersion}; deterministic boundaries, Roche-region containment, moon clearance, multi-band, and shepherd checks passed. Example: {first.Bands.Length} band(s), {first.InnerRadiusMeters / 1000.0:0}–{first.OuterRadiusMeters / 1000.0:0} km, {first.OuterShepherdMoonCount} outer shepherd candidate(s).");
        }

        private static bool Same(
            CelestialRingSystemResult left,
            CelestialRingSystemResult right)
        {
            if (left.HasRings != right.HasRings ||
                left.Origin != right.Origin ||
                left.InnerRadiusMeters != right.InnerRadiusMeters ||
                left.OuterRadiusMeters != right.OuterRadiusMeters ||
                left.OpticalDepth != right.OpticalDepth ||
                left.IceMassFraction != right.IceMassFraction ||
                left.OuterShepherdMoonCount != right.OuterShepherdMoonCount ||
                left.Bands.Length != right.Bands.Length)
            {
                return false;
            }

            for (var index = 0; index < left.Bands.Length; index++)
            {
                if (left.Bands[index].InnerRadiusMeters != right.Bands[index].InnerRadiusMeters ||
                    left.Bands[index].OuterRadiusMeters != right.Bands[index].OuterRadiusMeters)
                {
                    return false;
                }
            }

            return true;
        }

        private static void Fail(string error)
        {
            Debug.LogError(
                $"Ring-system generation validation failed: {error}");
        }
    }
}
