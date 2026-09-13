/*
 * Stores reusable physical, generation, and surface-system settings for a celestial body.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [CreateAssetMenu(
        fileName = "Celestial Body",
        menuName = "Celestial Systems/Celestial Body")]
    public sealed class CelestialBodyDefinition :
        ScriptableObject
    {
        public const double DefaultRoundBodyMinimumDiameterMeters =
            1000000.0;

        [SerializeField]
        private string definitionId =
            "body";

        [SerializeField]
        private double massKilograms =
            1.0;

        [SerializeField]
        private double referenceRadiusMeters =
            500000.0;

        [SerializeField]
        private int generationSeed;

        [SerializeField]
        private Vector3 northAxis =
            Vector3.up;

        [SerializeField]
        private Vector3 poleReferenceAxis =
            Vector3.forward;

        [SerializeField]
        private CelestialSurfaceSystem surfaceSystem =
            CelestialSurfaceSystem.AutomaticByDiameter;

        [SerializeField]
        private RoundMapMagicSurfaceDefinition roundMapMagicSurface;

        [SerializeField]
        private OceanDefinition oceanDefinition;

        [Header("Gravitational Lensing")]
        [SerializeField]
        private CelestialGravitationalLensingSettings gravitationalLensing =
            CelestialGravitationalLensingSettings.Default;

        [Header("Generated Stellar Properties")]
        [SerializeField]
        private bool hasStellarProperties;

        [SerializeField]
        private CelestialStellarEvolutionState stellarEvolutionState;

        [SerializeField]
        private double stellarInitialMassSolar;

        [SerializeField]
        private double stellarLuminositySolar;

        [SerializeField]
        private double stellarEffectiveTemperatureKelvin;

        [Header("Generated Planet Formation Properties")]
        [SerializeField]
        private bool hasPlanetFormationProperties;

        [SerializeField]
        private CelestialPlanetFormationClass planetFormationClass;

        [SerializeField]
        private double planetInitialOrbitAstronomicalUnits;

        [SerializeField]
        private double planetFinalOrbitAstronomicalUnits;

        [SerializeField]
        private double planetSolidCoreMassEarth;

        [SerializeField]
        private double planetVolatileMassFraction;

        [SerializeField]
        private double planetHydrogenHeliumEnvelopeFraction;

        [SerializeField]
        private double planetInwardMigrationFraction;

        [Header("Generated Post-Main-Sequence Properties")]
        [SerializeField]
        private bool hasPostMainSequenceProperties;

        [SerializeField]
        private CelestialPostMainSequencePlanetOutcome postMainSequenceOutcome;

        [SerializeField]
        private double planetPresentOrbitAstronomicalUnits;

        [SerializeField]
        private double planetOrbitalExpansionFactor;

        [SerializeField]
        private double estimatedMaximumGiantRadiusAstronomicalUnits;

        [SerializeField]
        private double minimumSurvivalPeriapsisAstronomicalUnits;

        [Header("Generated Planetary Evolution Properties")]
        [SerializeField]
        private bool hasPlanetaryEvolutionProperties;

        [SerializeField]
        private double planetAssumedBondAlbedo;

        [SerializeField]
        private double planetEquilibriumTemperatureKelvin;

        [SerializeField]
        private double planetEscapeVelocityMetersPerSecond;

        [SerializeField]
        private CelestialAtmosphereRetention planetAtmosphereRetention;

        [SerializeField]
        private CelestialPlanetDifferentiation planetDifferentiation;

        [SerializeField]
        private CelestialPlanetVolatileState planetVolatileState;

        [Header("Generated Moon Formation Properties")]
        [SerializeField]
        private bool hasMoonFormationProperties;

        [SerializeField]
        private CelestialMoonFormationOrigin moonFormationOrigin;

        [SerializeField]
        private double moonVolatileMassFraction;

        [SerializeField]
        private double moonOrbitalRadiusMeters;

        [SerializeField]
        private double moonOrbitalEccentricity;

        [SerializeField]
        private double moonOrbitalInclinationDegrees;

        [SerializeField]
        private CelestialOrbitDirection moonOrbitDirection =
            CelestialOrbitDirection.Prograde;

        [SerializeField]
        private double moonRocheLimitMeters;

        [SerializeField]
        private double moonStableOuterLimitMeters;

        [SerializeField]
        private double moonHillRadiusMeters;

        [SerializeField]
        private double moonSystemMassBudgetEarth;

        [Header("Generated Moon Evolution Properties")]
        [SerializeField]
        private bool hasMoonEvolutionProperties;

        [SerializeField]
        private double moonAssumedBondAlbedo;

        [SerializeField]
        private double moonEquilibriumTemperatureKelvin;

        [SerializeField]
        private double moonEscapeVelocityMetersPerSecond;

        [SerializeField]
        private CelestialAtmosphereRetention moonAtmosphereRetention;

        [SerializeField]
        private CelestialPlanetDifferentiation moonDifferentiation;

        [SerializeField]
        private CelestialPlanetVolatileState moonVolatileState;

        [SerializeField]
        private bool moonLikelyTidallyLocked;

        [SerializeField]
        private double moonRelativeTidalHeatingIndex;

        [SerializeField]
        private CelestialMoonTidalHeating moonTidalHeating;

        [SerializeField]
        private CelestialMoonTidalMigrationSensitivity moonTidalMigrationSensitivity;

        [Header("Generated Rotation Properties")]
        [SerializeField]
        private bool hasRotationProperties;

        [SerializeField]
        private double rotationPeriodHours;

        [SerializeField]
        private double axialTiltDegrees;

        [SerializeField]
        private CelestialSpinDirection spinDirection =
            CelestialSpinDirection.Prograde;

        [SerializeField]
        private CelestialSpinState spinState =
            CelestialSpinState.FreeRotating;

        [SerializeField]
        [TextArea(3, 8)]
        private string description;

        public string DefinitionId =>
            definitionId;

        public double MassKilograms =>
            massKilograms;

        public double ReferenceRadiusMeters =>
            referenceRadiusMeters;

        public double DiameterMeters =>
            referenceRadiusMeters *
            2.0;

        public int GenerationSeed =>
            generationSeed;

        public Vector3 NorthAxis =>
            northAxis;

        public Vector3 PoleReferenceAxis =>
            poleReferenceAxis;

        public CelestialSurfaceSystem SurfaceSystem =>
            surfaceSystem;

        public CelestialSurfaceSystem ResolvedSurfaceSystem
        {
            get
            {
                if (surfaceSystem !=
                    CelestialSurfaceSystem.AutomaticByDiameter)
                {
                    return surfaceSystem;
                }

                return DiameterMeters >=
                    DefaultRoundBodyMinimumDiameterMeters
                        ? CelestialSurfaceSystem.RoundMapMagic
                        : CelestialSurfaceSystem.Irregular;
            }
        }

        public RoundMapMagicSurfaceDefinition RoundMapMagicSurface =>
            roundMapMagicSurface;

        public OceanDefinition OceanDefinition =>
            oceanDefinition;

        public CelestialGravitationalLensingSettings GravitationalLensing =>
            gravitationalLensing;

        public bool HasStellarProperties =>
            hasStellarProperties;

        public CelestialStellarEvolutionState StellarEvolutionState =>
            stellarEvolutionState;

        public double StellarInitialMassSolar =>
            stellarInitialMassSolar;

        public double StellarLuminositySolar =>
            stellarLuminositySolar;

        public double StellarEffectiveTemperatureKelvin =>
            stellarEffectiveTemperatureKelvin;

        public bool HasPlanetFormationProperties =>
            hasPlanetFormationProperties;

        public CelestialPlanetFormationClass PlanetFormationClass =>
            planetFormationClass;

        public double PlanetInitialOrbitAstronomicalUnits =>
            planetInitialOrbitAstronomicalUnits;

        public double PlanetFinalOrbitAstronomicalUnits =>
            planetFinalOrbitAstronomicalUnits;

        public double PlanetSolidCoreMassEarth =>
            planetSolidCoreMassEarth;

        public double PlanetVolatileMassFraction =>
            planetVolatileMassFraction;

        public double PlanetHydrogenHeliumEnvelopeFraction =>
            planetHydrogenHeliumEnvelopeFraction;

        public double PlanetInwardMigrationFraction =>
            planetInwardMigrationFraction;

        public bool HasPostMainSequenceProperties =>
            hasPostMainSequenceProperties;

        public CelestialPostMainSequencePlanetOutcome PostMainSequenceOutcome =>
            postMainSequenceOutcome;

        public double PlanetPresentOrbitAstronomicalUnits =>
            planetPresentOrbitAstronomicalUnits;

        public double PlanetOrbitalExpansionFactor =>
            planetOrbitalExpansionFactor;

        public double EstimatedMaximumGiantRadiusAstronomicalUnits =>
            estimatedMaximumGiantRadiusAstronomicalUnits;

        public double MinimumSurvivalPeriapsisAstronomicalUnits =>
            minimumSurvivalPeriapsisAstronomicalUnits;

        public bool HasPlanetaryEvolutionProperties =>
            hasPlanetaryEvolutionProperties;

        public double PlanetAssumedBondAlbedo =>
            planetAssumedBondAlbedo;

        public double PlanetEquilibriumTemperatureKelvin =>
            planetEquilibriumTemperatureKelvin;

        public double PlanetEscapeVelocityMetersPerSecond =>
            planetEscapeVelocityMetersPerSecond;

        public CelestialAtmosphereRetention PlanetAtmosphereRetention =>
            planetAtmosphereRetention;

        public CelestialPlanetDifferentiation PlanetDifferentiation =>
            planetDifferentiation;

        public CelestialPlanetVolatileState PlanetVolatileState =>
            planetVolatileState;

        public bool HasMoonFormationProperties =>
            hasMoonFormationProperties;

        public CelestialMoonFormationOrigin MoonFormationOrigin =>
            moonFormationOrigin;

        public double MoonVolatileMassFraction =>
            moonVolatileMassFraction;

        public double MoonOrbitalRadiusMeters =>
            moonOrbitalRadiusMeters;

        public double MoonOrbitalEccentricity =>
            moonOrbitalEccentricity;

        public double MoonOrbitalInclinationDegrees =>
            moonOrbitalInclinationDegrees;

        public CelestialOrbitDirection MoonOrbitDirection =>
            moonOrbitDirection;

        public double MoonRocheLimitMeters =>
            moonRocheLimitMeters;

        public double MoonStableOuterLimitMeters =>
            moonStableOuterLimitMeters;

        public double MoonHillRadiusMeters =>
            moonHillRadiusMeters;

        public double MoonSystemMassBudgetEarth =>
            moonSystemMassBudgetEarth;

        public bool HasMoonEvolutionProperties =>
            hasMoonEvolutionProperties;

        public double MoonAssumedBondAlbedo =>
            moonAssumedBondAlbedo;

        public double MoonEquilibriumTemperatureKelvin =>
            moonEquilibriumTemperatureKelvin;

        public double MoonEscapeVelocityMetersPerSecond =>
            moonEscapeVelocityMetersPerSecond;

        public CelestialAtmosphereRetention MoonAtmosphereRetention =>
            moonAtmosphereRetention;

        public CelestialPlanetDifferentiation MoonDifferentiation =>
            moonDifferentiation;

        public CelestialPlanetVolatileState MoonVolatileState =>
            moonVolatileState;

        public bool MoonLikelyTidallyLocked =>
            moonLikelyTidallyLocked;

        public double MoonRelativeTidalHeatingIndex =>
            moonRelativeTidalHeatingIndex;

        public CelestialMoonTidalHeating MoonTidalHeating =>
            moonTidalHeating;

        public CelestialMoonTidalMigrationSensitivity MoonTidalMigrationSensitivity =>
            moonTidalMigrationSensitivity;

        public bool HasRotationProperties =>
            hasRotationProperties;

        public double RotationPeriodHours =>
            rotationPeriodHours;

        public double AxialTiltDegrees =>
            axialTiltDegrees;

        public CelestialSpinDirection SpinDirection =>
            spinDirection;

        public CelestialSpinState SpinState =>
            spinState;

        public bool IsSpinOrbitSynchronous =>
            spinState ==
                CelestialSpinState.SpinOrbitSynchronous;

        public string Description =>
            description ?? string.Empty;

        public bool HasValidPhysicalSettings =>
            !string.IsNullOrWhiteSpace(
                definitionId) &&
            IsFinite(massKilograms) &&
            massKilograms > 0.0 &&
            IsFinite(referenceRadiusMeters) &&
            referenceRadiusMeters > 0.0 &&
            northAxis.sqrMagnitude >
                0.000001f &&
            poleReferenceAxis.sqrMagnitude >
                0.000001f;

        public bool HasValidResolvedSurfaceSettings
        {
            get
            {
                switch (ResolvedSurfaceSystem)
                {
                    case CelestialSurfaceSystem.RoundMapMagic:
                        return
                            roundMapMagicSurface != null &&
                            roundMapMagicSurface.HasValidSettings;

                    case CelestialSurfaceSystem.Irregular:
                        return false;

                    default:
                        return false;
                }
            }
        }

        internal void ConfigureRuntime(
            string newDefinitionId,
            double newMassKilograms,
            double newReferenceRadiusMeters,
            int newGenerationSeed,
            Vector3 newNorthAxis,
            Vector3 newPoleReferenceAxis,
            CelestialSurfaceSystem newSurfaceSystem,
            RoundMapMagicSurfaceDefinition newRoundMapMagicSurface,
            OceanDefinition newOceanDefinition)
        {
            definitionId = newDefinitionId?.Trim();
            massKilograms = newMassKilograms;
            referenceRadiusMeters = newReferenceRadiusMeters;
            generationSeed = newGenerationSeed;
            northAxis = newNorthAxis;
            poleReferenceAxis = newPoleReferenceAxis;
            surfaceSystem = newSurfaceSystem;
            roundMapMagicSurface = newRoundMapMagicSurface;
            oceanDefinition = newOceanDefinition;
            gravitationalLensing =
                CelestialGravitationalLensingSettings.Default;
            hasStellarProperties = false;
            stellarEvolutionState =
                CelestialStellarEvolutionState.MainSequence;
            stellarInitialMassSolar = 0.0;
            stellarLuminositySolar = 0.0;
            stellarEffectiveTemperatureKelvin = 0.0;
            hasPlanetFormationProperties = false;
            planetFormationClass =
                CelestialPlanetFormationClass.Rocky;
            planetInitialOrbitAstronomicalUnits = 0.0;
            planetFinalOrbitAstronomicalUnits = 0.0;
            planetSolidCoreMassEarth = 0.0;
            planetVolatileMassFraction = 0.0;
            planetHydrogenHeliumEnvelopeFraction = 0.0;
            planetInwardMigrationFraction = 0.0;
            hasPostMainSequenceProperties = false;
            postMainSequenceOutcome =
                CelestialPostMainSequencePlanetOutcome.Unchanged;
            planetPresentOrbitAstronomicalUnits = 0.0;
            planetOrbitalExpansionFactor = 0.0;
            estimatedMaximumGiantRadiusAstronomicalUnits = 0.0;
            minimumSurvivalPeriapsisAstronomicalUnits = 0.0;
            hasPlanetaryEvolutionProperties = false;
            planetAssumedBondAlbedo = 0.0;
            planetEquilibriumTemperatureKelvin = 0.0;
            planetEscapeVelocityMetersPerSecond = 0.0;
            planetAtmosphereRetention =
                CelestialAtmosphereRetention.None;
            planetDifferentiation =
                CelestialPlanetDifferentiation.Undifferentiated;
            planetVolatileState =
                CelestialPlanetVolatileState.Depleted;
            hasMoonFormationProperties = false;
            moonFormationOrigin =
                CelestialMoonFormationOrigin.RegularDisk;
            moonVolatileMassFraction = 0.0;
            moonOrbitalRadiusMeters = 0.0;
            moonOrbitalEccentricity = 0.0;
            moonOrbitalInclinationDegrees = 0.0;
            moonOrbitDirection =
                CelestialOrbitDirection.Prograde;
            moonRocheLimitMeters = 0.0;
            moonStableOuterLimitMeters = 0.0;
            moonHillRadiusMeters = 0.0;
            moonSystemMassBudgetEarth = 0.0;
            hasMoonEvolutionProperties = false;
            moonAssumedBondAlbedo = 0.0;
            moonEquilibriumTemperatureKelvin = 0.0;
            moonEscapeVelocityMetersPerSecond = 0.0;
            moonAtmosphereRetention =
                CelestialAtmosphereRetention.None;
            moonDifferentiation =
                CelestialPlanetDifferentiation.Undifferentiated;
            moonVolatileState =
                CelestialPlanetVolatileState.Depleted;
            moonLikelyTidallyLocked = false;
            moonRelativeTidalHeatingIndex = 0.0;
            moonTidalHeating =
                CelestialMoonTidalHeating.Negligible;
            moonTidalMigrationSensitivity =
                CelestialMoonTidalMigrationSensitivity.Low;
            hasRotationProperties = false;
            rotationPeriodHours = 0.0;
            axialTiltDegrees = 0.0;
            spinDirection =
                CelestialSpinDirection.Prograde;
            spinState =
                CelestialSpinState.FreeRotating;
            description = string.Empty;
        }

        internal void ConfigureRuntimeDescription(
            string newDescription)
        {
            description =
                newDescription?.Trim() ??
                string.Empty;
        }

        internal void ConfigureRuntimeStellarProperties(
            CelestialStellarEvolutionResult properties)
        {
            hasStellarProperties = true;
            stellarEvolutionState =
                properties.EvolutionState;
            stellarInitialMassSolar =
                properties.InitialMassSolar;
            stellarLuminositySolar =
                properties.LuminositySolar;
            stellarEffectiveTemperatureKelvin =
                properties.EffectiveTemperatureKelvin;
        }

        internal void ConfigureRuntimePlanetFormationProperties(
            CelestialPlanetFormationResult properties)
        {
            hasPlanetFormationProperties = true;
            planetFormationClass =
                properties.FormationClass;
            planetInitialOrbitAstronomicalUnits =
                properties.InitialOrbitAstronomicalUnits;
            planetFinalOrbitAstronomicalUnits =
                properties.FinalOrbitAstronomicalUnits;
            planetSolidCoreMassEarth =
                properties.SolidCoreMassEarth;
            planetVolatileMassFraction =
                properties.VolatileMassFraction;
            planetHydrogenHeliumEnvelopeFraction =
                properties.HydrogenHeliumEnvelopeFraction;
            planetInwardMigrationFraction =
                properties.InwardMigrationFraction;
        }

        internal void ConfigureRuntimePostMainSequenceProperties(
            CelestialPostMainSequencePlanetResult properties)
        {
            hasPostMainSequenceProperties = true;
            postMainSequenceOutcome =
                properties.Outcome;
            planetPresentOrbitAstronomicalUnits =
                properties.PresentOrbitAstronomicalUnits;
            planetOrbitalExpansionFactor =
                properties.OrbitalExpansionFactor;
            estimatedMaximumGiantRadiusAstronomicalUnits =
                properties.EstimatedMaximumGiantRadiusAstronomicalUnits;
            minimumSurvivalPeriapsisAstronomicalUnits =
                properties.MinimumSurvivalPeriapsisAstronomicalUnits;
        }

        internal void ConfigureRuntimePlanetaryEvolutionProperties(
            CelestialPlanetaryEvolutionResult properties)
        {
            hasPlanetaryEvolutionProperties = true;
            planetAssumedBondAlbedo =
                properties.AssumedBondAlbedo;
            planetEquilibriumTemperatureKelvin =
                properties.EquilibriumTemperatureKelvin;
            planetEscapeVelocityMetersPerSecond =
                properties.EscapeVelocityMetersPerSecond;
            planetAtmosphereRetention =
                properties.AtmosphereRetention;
            planetDifferentiation =
                properties.Differentiation;
            planetVolatileState =
                properties.VolatileState;
        }

        internal void ConfigureRuntimeMoonFormationProperties(
            CelestialMoonFormationResult moon,
            CelestialMoonSystemFormationResult system)
        {
            hasMoonFormationProperties = true;
            moonFormationOrigin =
                moon.Origin;
            moonVolatileMassFraction =
                moon.VolatileMassFraction;
            moonOrbitalRadiusMeters =
                moon.OrbitalRadiusMeters;
            moonOrbitalEccentricity =
                moon.Eccentricity;
            moonOrbitalInclinationDegrees =
                moon.InclinationDegrees;
            moonOrbitDirection =
                moon.Direction;
            moonRocheLimitMeters =
                moon.RocheLimitMeters;
            moonStableOuterLimitMeters =
                moon.StableOuterLimitMeters;
            moonHillRadiusMeters =
                system.HillRadiusMeters;
            moonSystemMassBudgetEarth =
                system.SatelliteMassBudgetEarth;
        }

        internal void ConfigureRuntimeMoonEvolutionProperties(
            CelestialMoonEvolutionResult properties)
        {
            hasMoonEvolutionProperties = true;
            moonAssumedBondAlbedo =
                properties.AssumedBondAlbedo;
            moonEquilibriumTemperatureKelvin =
                properties.EquilibriumTemperatureKelvin;
            moonEscapeVelocityMetersPerSecond =
                properties.EscapeVelocityMetersPerSecond;
            moonAtmosphereRetention =
                properties.AtmosphereRetention;
            moonDifferentiation =
                properties.Differentiation;
            moonVolatileState =
                properties.VolatileState;
            moonLikelyTidallyLocked =
                properties.LikelyTidallyLocked;
            moonRelativeTidalHeatingIndex =
                properties.RelativeTidalHeatingIndex;
            moonTidalHeating =
                properties.TidalHeating;
            moonTidalMigrationSensitivity =
                properties.MigrationSensitivity;
        }

        internal void ConfigureRuntimeRotationProperties(
            CelestialBodyRotationResult properties)
        {
            hasRotationProperties = true;
            rotationPeriodHours =
                properties.RotationPeriodHours;
            axialTiltDegrees =
                properties.AxialTiltDegrees;
            spinDirection =
                properties.SpinDirection;
            spinState =
                properties.SpinState;
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
