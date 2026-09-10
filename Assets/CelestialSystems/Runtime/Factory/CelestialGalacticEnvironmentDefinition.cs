/*
 * Describes the galactic conditions that influence stellar and planetary-system formation.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    public enum CelestialGalacticRegion
    {
        ThinDisk = 0,
        ThickDisk = 1,
        GalacticBulge = 2,
        StellarHalo = 3,
        StarFormingRegion = 4,
        DenseCluster = 5
    }

    [CreateAssetMenu(
        fileName = "Galactic Environment",
        menuName = "Celestial Systems/Galactic Environment")]
    public sealed class CelestialGalacticEnvironmentDefinition :
        ScriptableObject
    {
        [Header("Population")]
        [SerializeField]
        private CelestialGalacticRegion region =
            CelestialGalacticRegion.ThinDisk;

        [SerializeField]
        [Range(0.0f, 13.8f)]
        [Tooltip("Elapsed time since this stellar system formed, in billions of years.")]
        private double systemAgeGigayears =
            4.6;

        [SerializeField]
        [Range(-4.0f, 1.0f)]
        [Tooltip("Iron abundance relative to the Sun, expressed logarithmically in dex.")]
        private double ironMetallicityDex;

        [SerializeField]
        [Range(-0.2f, 0.8f)]
        [Tooltip("Alpha-element enhancement relative to iron, expressed in dex.")]
        private double alphaEnhancementDex;

        [Header("Birth Environment")]
        [SerializeField]
        [Min(0.0f)]
        [Tooltip("Approximate stellar number density where the system formed, in stars per cubic parsec.")]
        private double birthStellarDensityPerCubicParsec =
            10.0;

        [SerializeField]
        [Min(0.0f)]
        [Tooltip("Far-ultraviolet radiation during disk formation in Habing-field units (G0).")]
        private double birthFarUltravioletFieldG0 =
            1.0;

        [Header("Current Environment")]
        [SerializeField]
        [Min(0.0f)]
        private double currentStellarDensityPerCubicParsec =
            0.1;

        [SerializeField]
        [Min(0.0f)]
        [Tooltip("Local interstellar particle density in particles per cubic centimeter.")]
        private double interstellarParticleDensityPerCubicCentimeter =
            1.0;

        [SerializeField]
        [Min(0.0f)]
        [Tooltip("Cosmic-ray ionization rate in inverse seconds.")]
        private double cosmicRayIonizationRatePerSecond =
            1.0e-16;

        public CelestialGalacticRegion Region =>
            region;

        public double SystemAgeGigayears =>
            systemAgeGigayears;

        public double IronMetallicityDex =>
            ironMetallicityDex;

        public double AlphaEnhancementDex =>
            alphaEnhancementDex;

        public double BirthStellarDensityPerCubicParsec =>
            birthStellarDensityPerCubicParsec;

        public double BirthFarUltravioletFieldG0 =>
            birthFarUltravioletFieldG0;

        public double CurrentStellarDensityPerCubicParsec =>
            currentStellarDensityPerCubicParsec;

        public double InterstellarParticleDensityPerCubicCentimeter =>
            interstellarParticleDensityPerCubicCentimeter;

        public double CosmicRayIonizationRatePerSecond =>
            cosmicRayIonizationRatePerSecond;

        public bool HasValidSettings =>
            TryValidate(
                out _);

        public bool TryValidate(
            out string error)
        {
            if (!IsFiniteInRange(
                    systemAgeGigayears,
                    0.0,
                    13.8))
            {
                error =
                    "A galactic environment requires a system age from 0 to 13.8 billion years.";
                return false;
            }

            if (!IsFiniteInRange(
                    ironMetallicityDex,
                    -4.0,
                    1.0) ||
                !IsFiniteInRange(
                    alphaEnhancementDex,
                    -0.2,
                    0.8))
            {
                error =
                    "A galactic environment contains an invalid elemental-abundance value.";
                return false;
            }

            if (!IsFiniteNonNegative(
                    birthStellarDensityPerCubicParsec) ||
                !IsFiniteNonNegative(
                    birthFarUltravioletFieldG0) ||
                !IsFiniteNonNegative(
                    currentStellarDensityPerCubicParsec) ||
                !IsFiniteNonNegative(
                    interstellarParticleDensityPerCubicCentimeter) ||
                !IsFiniteNonNegative(
                    cosmicRayIonizationRatePerSecond))
            {
                error =
                    "A galactic environment requires finite, non-negative density and radiation values.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFiniteInRange(
            double value,
            double minimum,
            double maximum)
        {
            return
                IsFinite(value) &&
                value >= minimum &&
                value <= maximum;
        }

        private static bool IsFiniteNonNegative(
            double value)
        {
            return
                IsFinite(value) &&
                value >= 0.0;
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
