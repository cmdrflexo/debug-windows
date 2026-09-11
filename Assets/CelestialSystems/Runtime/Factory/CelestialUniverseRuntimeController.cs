/*
 * Provides the scene-facing entry point for the universe-to-body runtime factory chain.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialUniverseRuntimeController :
        MonoBehaviour
    {
        private const double SolarMassKilograms =
            1.98847e30;

        private const double SolarRadiusMeters =
            6.957e8;

        private const double EarthMassKilograms =
            5.9722e24;

        private const double EarthRadiusMeters =
            6371000.0;

        [Header("Factory")]
        [SerializeField]
        private CelestialBodyFactory bodyFactory;

        [Header("Prototype Generation")]
        [SerializeField]
        private CelestialSystemGenerationGuide starSystemGuide;

        [SerializeField]
        private CelestialGalacticEnvironmentDefinition galacticEnvironment;

        [SerializeField]
        private int seed = 1;

        [SerializeField]
        private string universeInstanceId =
            "universe-runtime";

        [SerializeField]
        private string galaxyInstanceId =
            "prototype-galaxy";

        [SerializeField]
        private string starSystemInstanceId =
            "prototype-star-system";

        [Header("Initial Motion")]
        [SerializeField]
        private DoubleVector3 positionMetersFromFrameOrigin;

        [SerializeField]
        private DoubleVector3 velocityMetersPerSecond;

        [SerializeField]
        private Vector3 rotationEulerDegrees;

        [Header("Observation")]
        [SerializeField]
        [Tooltip("Optional grid pivot marker that will point toward the generated star system's barycenter.")]
        private UniverseObservationPivotMarker observationPivotMarker;

        [Header("Lifecycle")]
        [SerializeField]
        private Transform generatedParentOverride;

        [SerializeField]
        private bool generateOnStart = true;

        [Header("Development")]
        [SerializeField]
        [Tooltip("Print a compact star-and-planet summary after generation.")]
        private bool logGeneratedStarSystemSummary = true;

        [Header("Runtime")]
        [SerializeField]
        private bool waitingForFactory;

        [SerializeField]
        private bool generationAttempted;

        [SerializeField]
        private bool generationSucceeded;

        [SerializeField]
        private Transform generatedUniverseRoot;

        [SerializeField]
        private int generatedGalaxyCount;

        [SerializeField]
        private int generatedStarSystemCount;

        [SerializeField]
        private int generatedBodySystemCount;

        [SerializeField]
        private int generatedBodyCount;

        [SerializeField]
        private bool hasGeneratedStarSystemBarycenter;

        [SerializeField]
        private UniversePosition generatedStarSystemBarycenter;

        [SerializeField]
        private string lastError;

        private CelestialUniverseFactory universeFactory;

        private CelestialUniverseFactory.GeneratedUniverse generatedUniverse;

        public CelestialBodyFactory BodyFactory =>
            bodyFactory;

        public CelestialSystemGenerationGuide StarSystemGuide =>
            starSystemGuide;

        public CelestialGalacticEnvironmentDefinition GalacticEnvironment =>
            galacticEnvironment;

        public int Seed =>
            seed;

        public bool WaitingForFactory =>
            waitingForFactory;

        public bool GenerationAttempted =>
            generationAttempted;

        public bool GenerationSucceeded =>
            generationSucceeded;

        public Transform GeneratedUniverseRoot =>
            generatedUniverseRoot;

        public int GeneratedGalaxyCount =>
            generatedGalaxyCount;

        public int GeneratedStarSystemCount =>
            generatedStarSystemCount;

        public int GeneratedBodySystemCount =>
            generatedBodySystemCount;

        public int GeneratedBodyCount =>
            generatedBodyCount;

        public bool HasGeneratedStarSystemBarycenter =>
            hasGeneratedStarSystemBarycenter;

        public UniversePosition GeneratedStarSystemBarycenter =>
            generatedStarSystemBarycenter;

        public string LastError =>
            lastError;

        private IEnumerator Start()
        {
            if (!generateOnStart)
            {
                yield break;
            }

            if (bodyFactory == null)
            {
                SetError(
                    "The universe runtime controller requires a celestial body factory.");
                yield break;
            }

            waitingForFactory = true;

            while (bodyFactory != null &&
                !bodyFactory.CanSpawnBodies)
            {
                yield return null;
            }

            waitingForFactory = false;

            if (bodyFactory == null)
            {
                SetError(
                    "The assigned celestial body factory was destroyed before it became ready.");
                yield break;
            }

            TryGenerate();
        }

        public bool TryGenerate()
        {
            if (generatedUniverse != null)
            {
                return SetError(
                    "This universe runtime controller already owns a generated universe.");
            }

            ResetRuntimeSummary();
            generationAttempted = true;

            if (!Application.isPlaying)
            {
                return SetError(
                    "A universe can only be generated while the application is playing.");
            }

            if (bodyFactory == null)
            {
                return SetError(
                    "The universe runtime controller requires a celestial body factory.");
            }

            if (starSystemGuide == null)
            {
                return SetError(
                    "The universe runtime controller requires a star-system generation guide.");
            }

            if (galacticEnvironment == null)
            {
                return SetError(
                    "The universe runtime controller requires a galactic environment.");
            }

            universeFactory ??=
                new CelestialUniverseFactory(                    bodyFactory);

            var parent =
                generatedParentOverride != null
                    ? generatedParentOverride
                    : transform;

            generationSucceeded =
                universeFactory.TryGenerate(
                    universeInstanceId,
                    galaxyInstanceId,
                    starSystemInstanceId,
                    seed,
                    starSystemGuide,
                    galacticEnvironment,
                    positionMetersFromFrameOrigin,
                    velocityMetersPerSecond,
                    Quaternion.Euler(
                        rotationEulerDegrees),
                    parent,
                    out generatedUniverse);

            if (!generationSucceeded)
            {
                lastError =
                    universeFactory.LastError;
                return false;
            }

            generatedUniverseRoot =
                generatedUniverse.Root;
            generatedGalaxyCount = 1;
            generatedStarSystemCount = 1;

            var starSystem =
                generatedUniverse.Galaxy?.StarSystem;

            if (starSystem != null)
            {
                generatedBodySystemCount =
                    starSystem.BodySystems.Count;

                foreach (var bodySystem in
                    starSystem.BodySystems.Values)
                {
                    if (bodySystem != null)
                    {
                        generatedBodyCount +=
                            bodySystem.Bodies.Count;
                    }
                }

                SetObservationMarkerToBarycenter(
                    starSystem);

                if (logGeneratedStarSystemSummary)
                {
                    LogStarSystemSummary(
                        starSystem);
                }
            }

            return true;
        }

        public bool TryDespawn()
        {
            if (universeFactory == null ||
                generatedUniverse == null)
            {
                return false;
            }

            if (!universeFactory.TryDespawn(
                    generatedUniverse))
            {
                lastError =
                    universeFactory.LastError;
                return false;
            }

            generatedUniverse = null;
            ResetRuntimeSummary();
            return true;
        }

        private void LogStarSystemSummary(
            CelestialStarSystemFactory.GeneratedSystem starSystem)
        {
            CelestialBodyDefinition star = null;
            var planets =
                new List<CelestialBodyDefinition>();

            foreach (var bodySystem in
                starSystem.BodySystems.Values)
            {
                if (bodySystem == null)
                {
                    continue;
                }

                foreach (var body in
                    bodySystem.Bodies.Values)
                {
                    var definition =
                        body != null
                            ? body.Definition
                            : null;

                    if (definition == null)
                    {
                        continue;
                    }

                    if (definition.HasStellarProperties)
                    {
                        star ??= definition;
                    }
                    else if (definition.HasPlanetFormationProperties)
                    {
                        planets.Add(
                            definition);
                    }
                }
            }

            planets.Sort(
                (left, right) =>
                    ResolvePresentOrbit(left).CompareTo(
                        ResolvePresentOrbit(right)));

            var builder =
                new StringBuilder();
            builder.Append(
                "Generated star system '");
            builder.Append(
                starSystem.InstanceId);
            builder.Append(
                "' (seed ");
            builder.Append(
                starSystem.Seed);
            builder.AppendLine(
                "):");

            if (star != null)
            {
                AppendSummaryLine(
                    builder,
                    "star",
                    DescribeStarClassification(
                        star),
                    star.MassKilograms /
                        SolarMassKilograms,
                    "Solar masses",
                    star.ReferenceRadiusMeters /
                        SolarRadiusMeters,
                    "Solar radii");
            }

            for (var index = 0;
                index < planets.Count;
                index++)
            {
                var planet =
                    planets[index];

                AppendSummaryLine(
                    builder,
                    $"planet {index + 1}",
                    DescribePlanetClassification(
                        planet.PlanetFormationClass),
                    planet.MassKilograms /
                        EarthMassKilograms,
                    "Earth masses",
                    planet.ReferenceRadiusMeters /
                        EarthRadiusMeters,
                    "Earth radii");
            }

            Debug.Log(
                builder.ToString().TrimEnd(),
                this);
        }

        private static void AppendSummaryLine(
            StringBuilder builder,
            string label,
            string classification,
            double mass,
            string massUnit,
            double radius,
            string radiusUnit)
        {
            builder.Append(
                label.PadRight(
                    9));
            builder.Append(
                "- ");
            builder.Append(
                classification.PadRight(
                    31));
            builder.Append(
                " [");
            builder.Append(
                FormatSummaryValue(
                    mass).PadLeft(
                        10));
            builder.Append(
                " ");
            builder.Append(
                massUnit.PadRight(
                    12));
            builder.Append(
                "] [");
            builder.Append(
                FormatSummaryValue(
                    radius).PadLeft(
                        7));
            builder.Append(
                " ");
            builder.Append(
                radiusUnit);
            builder.AppendLine(
                "]");
        }

        private static string DescribeStarClassification(
            CelestialBodyDefinition star)
        {
            switch (star.StellarEvolutionState)
            {
                case CelestialStellarEvolutionState.WhiteDwarf:
                    return "white dwarf";
                case CelestialStellarEvolutionState.NeutronStar:
                    return "neutron star";
                case CelestialStellarEvolutionState.BlackHole:
                    return "stellar-mass black hole";
                case CelestialStellarEvolutionState.MainSequence:
                    var massSolar =
                        star.MassKilograms /
                        SolarMassKilograms;

                    if (massSolar < 0.5)
                    {
                        return "low-mass main-sequence star";
                    }

                    if (massSolar < 1.5)
                    {
                        return "solar-type main-sequence star";
                    }

                    return "intermediate-mass main-sequence star";
                default:
                    return "unclassified star";
            }
        }

        private static string DescribePlanetClassification(
            CelestialPlanetFormationClass formationClass)
        {
            switch (formationClass)
            {
                case CelestialPlanetFormationClass.Rocky:
                    return "rocky planet";
                case CelestialPlanetFormationClass.VolatileRich:
                    return "volatile-rich planet";
                case CelestialPlanetFormationClass.GasRich:
                    return "gas-rich planet";
                case CelestialPlanetFormationClass.Giant:
                    return "giant planet";
                default:
                    return "unclassified planet";
            }
        }

        private static double ResolvePresentOrbit(
            CelestialBodyDefinition planet)
        {
            return
                planet.HasPostMainSequenceProperties
                    ? planet.PlanetPresentOrbitAstronomicalUnits
                    : planet.PlanetFinalOrbitAstronomicalUnits;
        }

        private static string FormatSummaryValue(
            double value)
        {
            return
                value.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture);
        }

        private void SetObservationMarkerToBarycenter(
            CelestialStarSystemFactory.GeneratedSystem starSystem)
        {
            observationPivotMarker ??=
                FindFirstObjectByType<UniverseObservationPivotMarker>();

            if (observationPivotMarker == null ||
                !TryCalculateBarycenter(
                    starSystem,
                    out generatedStarSystemBarycenter))
            {
                hasGeneratedStarSystemBarycenter = false;
                return;
            }

            hasGeneratedStarSystemBarycenter = true;
            observationPivotMarker.SetDirectionReference(
                generatedStarSystemBarycenter);
        }

        private static bool TryCalculateBarycenter(
            CelestialStarSystemFactory.GeneratedSystem starSystem,
            out UniversePosition barycenter)
        {
            barycenter = default;

            if (starSystem == null)
            {
                return false;
            }

            var hasOrigin = false;
            var origin = default(UniversePosition);
            var weightedX = 0.0;
            var weightedY = 0.0;
            var weightedZ = 0.0;
            var totalMass = 0.0;

            foreach (var bodySystem in
                starSystem.BodySystems.Values)
            {
                if (bodySystem == null)
                {
                    continue;
                }

                foreach (var body in
                    bodySystem.Bodies.Values)
                {
                    if (body == null ||
                        body.Definition == null ||
                        !IsFinitePositive(
                            body.Definition.MassKilograms) ||
                        !body.TryGetMotionState(
                            out var motionState))
                    {
                        continue;
                    }

                    if (!hasOrigin)
                    {
                        origin = motionState.Position;
                        hasOrigin = true;
                    }

                    if (!motionState.Position.TryGetOffsetMetersFrom(
                            origin,
                            out var offset))
                    {
                        continue;
                    }

                    var mass =
                        body.Definition.MassKilograms;
                    weightedX +=
                        offset.x *
                        mass;
                    weightedY +=
                        offset.y *
                        mass;
                    weightedZ +=
                        offset.z *
                        mass;
                    totalMass +=
                        mass;
                }
            }

            if (!hasOrigin ||
                !IsFinitePositive(
                    totalMass))
            {
                return false;
            }

            var offsetX =
                weightedX /
                totalMass;
            var offsetY =
                weightedY /
                totalMass;
            var offsetZ =
                weightedZ /
                totalMass;

            if (!IsFinite(
                    offsetX) ||
                !IsFinite(
                    offsetY) ||
                !IsFinite(
                    offsetZ))
            {
                return false;
            }

            barycenter = origin;
            barycenter.AddLocalMeters(
                offsetX,
                offsetY,
                offsetZ);
            return true;
        }

        private static bool IsFinitePositive(
            double value)
        {
            return
                IsFinite(value) &&
                value > 0.0;
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }

        private void ResetRuntimeSummary()
        {
            generationSucceeded = false;
            generatedUniverseRoot = null;
            generatedGalaxyCount = 0;
            generatedStarSystemCount = 0;
            generatedBodySystemCount = 0;
            generatedBodyCount = 0;

            if (hasGeneratedStarSystemBarycenter &&
                observationPivotMarker != null)
            {
                observationPivotMarker.ClearDirectionReference();
            }

            hasGeneratedStarSystemBarycenter = false;
            generatedStarSystemBarycenter = default;
            lastError = string.Empty;
        }

        private bool SetError(
            string error)
        {
            generationSucceeded = false;
            lastError = error;
            Debug.LogError(
                error,
                this);
            return false;
        }
    }
}
