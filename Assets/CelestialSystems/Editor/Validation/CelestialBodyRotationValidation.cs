/*
 * Validates deterministic planet and moon spin generation and synchronous-period calculations.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialBodyRotationValidation
    {
        [MenuItem("Tools/Celestial Systems/Validate Body Rotation Model")]
        public static void Validate()
        {
            var distantPlanet = CreatePlanet(1.0, 1.0, 1.0);
            var closePlanet = CreatePlanet(1.0, 1.0, 0.02);

            if (!CelestialBodyRotationModel.TryEvaluatePlanet(
                    41,
                    distantPlanet,
                    1.0,
                    4.6,
                    out var distant,
                    out var error) ||
                !CelestialBodyRotationModel.TryEvaluatePlanet(
                    41,
                    distantPlanet,
                    1.0,
                    4.6,
                    out var repeated,
                    out error) ||
                !CelestialBodyRotationModel.TryEvaluatePlanet(
                    42,
                    closePlanet,
                    1.0,
                    4.6,
                    out var close,
                    out error))
            {
                Debug.LogError(error);
                return;
            }

            if (!AreEqual(distant, repeated) ||
                distant.IsSynchronous ||
                !close.IsSynchronous ||
                close.RotationPeriodHours < 24.0)
            {
                Debug.LogError("Body rotation validation failed: deterministic or planetary tidal-locking checks failed.");
                return;
            }

            var lockedMoonEvolution = CreateMoonEvolution(true);
            var unlockedMoonEvolution = CreateMoonEvolution(false);
            var progradeMoon = CreateMoon(CelestialOrbitDirection.Prograde);
            var retrogradeMoon = CreateMoon(CelestialOrbitDirection.Retrograde);

            if (!CelestialBodyRotationModel.TryEvaluateMoon(
                    73,
                    progradeMoon,
                    lockedMoonEvolution,
                    317.8,
                    out var lockedMoon,
                    out error) ||
                !CelestialBodyRotationModel.TryEvaluateMoon(
                    74,
                    retrogradeMoon,
                    unlockedMoonEvolution,
                    317.8,
                    out var unlockedMoon,
                    out error))
            {
                Debug.LogError(error);
                return;
            }

            var expectedIoPeriodHours = 42.46;
            if (!lockedMoon.IsSynchronous ||
                Math.Abs(lockedMoon.RotationPeriodHours - expectedIoPeriodHours) > 0.25 ||
                lockedMoon.SpinDirection != CelestialSpinDirection.Prograde ||
                unlockedMoon.IsSynchronous ||
                unlockedMoon.SpinDirection != CelestialSpinDirection.Retrograde)
            {
                Debug.LogError("Body rotation validation failed: lunar synchronization or spin-direction checks failed.");
                return;
            }

            Debug.Log(
                $"Body rotation PASS. Model v{CelestialBodyRotationModel.ModelVersion}; deterministic free rotation, planetary tidal locking, lunar synchronization, axial tilt, and spin-direction checks passed. " +
                $"Io-distance synchronous period: {lockedMoon.RotationPeriodHours:0.##} hours.");
        }

        private static CelestialPlanetFormationResult CreatePlanet(
            double massEarth,
            double radiusEarth,
            double orbitAstronomicalUnits)
        {
            return new CelestialPlanetFormationResult(
                CelestialPlanetFormationClass.Rocky,
                orbitAstronomicalUnits,
                orbitAstronomicalUnits,
                massEarth,
                massEarth,
                radiusEarth,
                0.01,
                0.0,
                0.0);
        }

        private static CelestialMoonFormationResult CreateMoon(
            CelestialOrbitDirection direction)
        {
            return new CelestialMoonFormationResult(
                CelestialMoonFormationOrigin.RegularDisk,
                0.01495,
                0.286,
                0.01,
                421700000.0,
                0.0041,
                0.5,
                direction,
                150000000.0,
                20000000000.0);
        }

        private static CelestialMoonEvolutionResult CreateMoonEvolution(bool locked)
        {
            return new CelestialMoonEvolutionResult(
                0.3,
                115.0,
                2560.0,
                CelestialAtmosphereRetention.None,
                CelestialPlanetDifferentiation.PartiallyDifferentiated,
                CelestialPlanetVolatileState.Frozen,
                locked,
                1.0,
                CelestialMoonTidalHeating.Significant,
                CelestialMoonTidalMigrationSensitivity.Low);
        }

        private static bool AreEqual(
            CelestialBodyRotationResult left,
            CelestialBodyRotationResult right)
        {
            return left.RotationPeriodHours == right.RotationPeriodHours &&
                left.AxialTiltDegrees == right.AxialTiltDegrees &&
                left.SpinDirection == right.SpinDirection &&
                left.SpinState == right.SpinState;
        }
    }
}
