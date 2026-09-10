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
            hasStellarProperties = false;
            stellarEvolutionState =
                CelestialStellarEvolutionState.MainSequence;
            stellarInitialMassSolar = 0.0;
            stellarLuminositySolar = 0.0;
            stellarEffectiveTemperatureKelvin = 0.0;
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

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
