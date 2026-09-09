/*
 * Provides a deterministic prototype guide that builds a star and simple free-simulation planets from reusable body definitions.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [CreateAssetMenu(
        fileName = "Basic System Generation Guide",
        menuName = "Celestial Systems/Generation Guides/Basic Star System")]
    public sealed class BasicSystemGenerationGuide :
        CelestialSystemGenerationGuide
    {
        private const double GravitationalConstant =
            6.67430e-11;

        [Header("Identity")]
        [SerializeField]
        private string planDefinitionId =
            "basic-star-system";

        [Header("Prototype Bodies")]
        [SerializeField]
        private CelestialBodyDefinition starDefinition;

        [SerializeField]
        private RoundMapMagicSurfaceQualityProfile starQualityProfile;

        [SerializeField]
        private CelestialBodyDefinition planetDefinition;

        [SerializeField]
        private RoundMapMagicSurfaceQualityProfile planetQualityProfile;

        [Header("Planet Count")]
        [SerializeField]
        [Min(0)]
        private int minimumPlanetCount = 1;

        [SerializeField]
        [Min(0)]
        private int maximumPlanetCount = 3;

        [Header("Prototype Orbits")]
        [SerializeField]
        [Min(1.0f)]
        private double innerOrbitRadiusMeters =
            10000000000.0;

        [SerializeField]
        [Min(1.0f)]
        private double outerOrbitRadiusMeters =
            200000000000.0;

        [SerializeField]
        [Range(0.0f, 45.0f)]
        private float maximumInclinationDegrees =
            5.0f;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float retrogradeChance;

        public override bool TryGenerate(
            CelestialStarSystemGenerationRequest request,
            out CelestialStarSystemPlan plan,
            out string error)
        {
            plan = null;

            if (!TryValidate(
                    request,
                    out error))
            {
                return false;
            }

            var random =
                new DeterministicRandom(
                    request.Seed);
            var planetCount =
                random.NextInclusive(
                    minimumPlanetCount,
                    maximumPlanetCount);
            var bodySystems =
                new List<CelestialStarSystemPlan.BodySystemPlan>(
                    planetCount + 1);

            bodySystems.Add(
                CreateSingleBodySystem(
                    "stellar",
                    "star",
                    starDefinition,
                    starQualityProfile,
                    new DoubleVector3(),
                    new DoubleVector3()));

            for (var index = 0;
                index < planetCount;
                index++)
            {
                var nominalFraction =
                    ((double)index + 0.5) /
                    planetCount;
                var jitterRange =
                    0.3 /
                    planetCount;
                var orbitFraction =
                    Clamp01(
                        nominalFraction +
                        (random.Next01() * 2.0 - 1.0) *
                        jitterRange);
                var orbitRadius =
                    LogarithmicLerp(
                        innerOrbitRadiusMeters,
                        outerOrbitRadiusMeters,
                        orbitFraction);
                var phaseRadians =
                    random.Next01() *
                    Math.PI *
                    2.0;
                var inclinationRadians =
                    (random.Next01() * 2.0 - 1.0) *
                    maximumInclinationDegrees *
                    Math.PI /
                    180.0;
                var direction =
                    random.Next01() <
                        retrogradeChance
                            ? -1.0
                            : 1.0;
                var orbitalSpeed =
                    Math.Sqrt(
                        GravitationalConstant *
                        starDefinition.MassKilograms /
                        orbitRadius);
                var cosinePhase =
                    Math.Cos(
                        phaseRadians);
                var sinePhase =
                    Math.Sin(
                        phaseRadians);
                var cosineInclination =
                    Math.Cos(
                        inclinationRadians);
                var sineInclination =
                    Math.Sin(
                        inclinationRadians);
                var position =
                    new DoubleVector3(
                        cosinePhase *
                            orbitRadius,
                        sinePhase *
                            sineInclination *
                            orbitRadius,
                        sinePhase *
                            cosineInclination *
                            orbitRadius);
                var velocity =
                    new DoubleVector3(
                        -sinePhase *
                            orbitalSpeed *
                            direction,
                        cosinePhase *
                            sineInclination *
                            orbitalSpeed *
                            direction,
                        cosinePhase *
                            cosineInclination *
                            orbitalSpeed *
                            direction);
                var instanceId =
                    $"planet-{index + 1}";

                bodySystems.Add(
                    CreateSingleBodySystem(
                        instanceId,
                        "planet",
                        planetDefinition,
                        planetQualityProfile,
                        position,
                        velocity));
            }

            plan =
                new CelestialStarSystemPlan(
                    planDefinitionId,
                    bodySystems);
            error = string.Empty;
            return true;
        }

        private bool TryValidate(
            CelestialStarSystemGenerationRequest request,
            out string error)
        {
            if (request == null)
            {
                error =
                    "The basic system generation guide requires a generation request.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    planDefinitionId))
            {
                error =
                    "The basic system generation guide requires a plan definition ID.";
                return false;
            }

            if (!IsUsablePrototype(
                    starDefinition))
            {
                error =
                    "The basic system generation guide requires a valid, spawnable star body definition.";
                return false;
            }

            if (maximumPlanetCount > 0 &&
                !IsUsablePrototype(
                    planetDefinition))
            {
                error =
                    "The basic system generation guide requires a valid, spawnable planet body definition.";
                return false;
            }

            if (minimumPlanetCount < 0 ||
                maximumPlanetCount <
                    minimumPlanetCount)
            {
                error =
                    "The basic system generation guide has an invalid planet-count range.";
                return false;
            }

            if (!IsFinite(
                    innerOrbitRadiusMeters) ||
                !IsFinite(
                    outerOrbitRadiusMeters) ||
                innerOrbitRadiusMeters <= 0.0 ||
                outerOrbitRadiusMeters <
                    innerOrbitRadiusMeters)
            {
                error =
                    "The basic system generation guide has an invalid orbital-radius range.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static CelestialStarSystemPlan.BodySystemPlan CreateSingleBodySystem(
            string systemInstanceId,
            string bodyInstanceId,
            CelestialBodyDefinition definition,
            RoundMapMagicSurfaceQualityProfile qualityProfile,
            DoubleVector3 position,
            DoubleVector3 velocity)
        {
            var entry =
                new CelestialBodySystemDefinition.BodyEntry(
                    bodyInstanceId,
                    definition,
                    qualityProfile,
                    string.Empty,
                    CelestialBodySpawnMode.FreeSimulation,
                    new DoubleVector3(),
                    new DoubleVector3(),
                    Vector3.zero,
                    new DoubleVector3());
            var definitionId =
                $"{systemInstanceId}-body-system";
            var systemDefinition =
                CelestialBodySystemDefinition.CreateRuntime(
                    definitionId,
                    new[]
                    {
                        entry
                    });

            return
                new CelestialStarSystemPlan.BodySystemPlan(
                    systemInstanceId,
                    systemDefinition,
                    position,
                    velocity,
                    Quaternion.identity);
        }

        private static bool IsUsablePrototype(
            CelestialBodyDefinition definition)
        {
            return
                definition != null &&
                definition.HasValidPhysicalSettings &&
                definition.HasValidResolvedSurfaceSettings;
        }

        private static double LogarithmicLerp(
            double minimum,
            double maximum,
            double fraction)
        {
            if (minimum == maximum)
            {
                return minimum;
            }

            return Math.Exp(
                Math.Log(minimum) +
                (Math.Log(maximum) -
                    Math.Log(minimum)) *
                fraction);
        }

        private static double Clamp01(
            double value)
        {
            return Math.Max(
                0.0,
                Math.Min(
                    1.0,
                    value));
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }

        private struct DeterministicRandom
        {
            private uint state;

            public DeterministicRandom(
                int seed)
            {
                state =
                    unchecked((uint)seed);

                if (state == 0)
                {
                    state =
                        0x6D2B79F5u;
                }
            }

            public int NextInclusive(
                int minimum,
                int maximum)
            {
                var range =
                    (uint)(maximum -
                        minimum +
                        1);

                return
                    minimum +
                    (int)(NextUInt() %
                        range);
            }

            public double Next01()
            {
                return
                    (NextUInt() >> 8) *
                    (1.0 / 16777216.0);
            }

            private uint NextUInt()
            {
                var value =
                    state;
                value ^=
                    value << 13;
                value ^=
                    value >> 17;
                value ^=
                    value << 5;
                state =
                    value;
                return value;
            }
        }
    }
}
