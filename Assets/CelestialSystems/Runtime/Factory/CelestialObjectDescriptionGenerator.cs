/*
 * Converts facts already produced by celestial generation into compact, deterministic descriptions.
 */

using System;
using System.Globalization;

namespace jcan.CelestialSystems
{
    public static class CelestialObjectDescriptionGenerator
    {
        public const int ModelVersion = 1;

        public static string DescribeStar(
            CelestialStellarPopulationSample population,
            CelestialStellarEvolutionResult evolution,
            CelestialGalacticEnvironmentDefinition environment)
        {
            if (environment == null)
            {
                return string.Empty;
            }

            var age =
                Format(
                    environment.SystemAgeGigayears);
            var birthMass =
                Format(
                    population.InitialMassSolar);
            var currentMass =
                Format(
                    evolution.CurrentMassSolar);
            var radius =
                Format(
                    evolution.RadiusSolar);
            var luminosity =
                Format(
                    evolution.LuminositySolar);
            var temperature =
                Math.Round(
                    evolution.EffectiveTemperatureKelvin)
                    .ToString(
                        "0",
                        CultureInfo.InvariantCulture);
            var region =
                DescribeRegion(
                    environment.Region);
            var composition =
                DescribeComposition(
                    environment.IronMetallicityDex,
                    environment.AlphaEnhancementDex);

            switch (evolution.EvolutionState)
            {
                case CelestialStellarEvolutionState.MainSequence:
                    return
                        $"This is a {DescribeMainSequenceMass(evolution.CurrentMassSolar)} main-sequence star that formed approximately {age} billion years ago from {composition} gas in {region}. " +
                        $"It formed with about {birthMass} solar masses and currently has {currentMass} solar masses, a radius of {radius} solar radii, and a luminosity of {luminosity} times that of the Sun. " +
                        $"Its estimated effective surface temperature is {temperature} kelvin.";

                case CelestialStellarEvolutionState.WhiteDwarf:
                    return
                        $"This white dwarf is the compact remnant of a star that formed approximately {age} billion years ago from {composition} gas in {region}. " +
                        $"Its progenitor began with about {birthMass} solar masses before shedding its outer layers, leaving a remnant of {currentMass} solar masses and {radius} solar radii. " +
                        $"It now radiates about {luminosity} times the Sun's luminosity with an estimated effective temperature of {temperature} kelvin.";

                case CelestialStellarEvolutionState.NeutronStar:
                    return
                        $"This neutron star is the collapsed remnant of a massive star born approximately {age} billion years ago from {composition} gas in {region}. " +
                        $"Its progenitor formed with about {birthMass} solar masses and ended its life in a core-collapse supernova. " +
                        $"The remaining object has approximately {currentMass} solar masses compressed into a radius of {radius} solar radii.";

                case CelestialStellarEvolutionState.BlackHole:
                    return
                        $"This stellar-mass black hole formed after the collapse of a massive star born approximately {age} billion years ago from {composition} gas in {region}. " +
                        $"Its progenitor began with about {birthMass} solar masses, while the present compact object is estimated to contain {currentMass} solar masses. " +
                        $"The listed radius of {radius} solar radii represents its approximate Schwarzschild radius rather than a material surface.";

                default:
                    return string.Empty;
            }
        }

        public static string DescribePlanet(
            CelestialPlanetFormationResult formation)
        {
            var mass =
                Format(
                    formation.TotalMassEarth);
            var radius =
                Format(
                    formation.RadiusEarth);
            var initialOrbit =
                Format(
                    formation.InitialOrbitAstronomicalUnits);
            var finalOrbit =
                Format(
                    formation.FinalOrbitAstronomicalUnits);
            var volatilePercent =
                Format(
                    formation.VolatileMassFraction *
                    100.0);
            var envelopePercent =
                Format(
                    formation.HydrogenHeliumEnvelopeFraction *
                    100.0);
            var classification =
                DescribePlanetFormationClass(
                    formation.FormationClass);

            var formationHistory =
                formation.InwardMigrationFraction > 0.001
                    ? $"It formed near {initialOrbit} astronomical units from its star and migrated inward to its present orbit near {finalOrbit} astronomical units."
                    : $"It formed near its present orbit, approximately {finalOrbit} astronomical units from its star.";

            return
                $"This is a {classification} planet with an estimated mass of {mass} Earth masses and a radius of {radius} Earth radii. " +
                $"The formation model assigns it a bulk volatile fraction of about {volatilePercent} percent and a hydrogen-helium envelope containing about {envelopePercent} percent of its mass. " +
                formationHistory;
        }

        private static string DescribePlanetFormationClass(
            CelestialPlanetFormationClass formationClass)
        {
            switch (formationClass)
            {
                case CelestialPlanetFormationClass.Rocky:
                    return "rocky";
                case CelestialPlanetFormationClass.VolatileRich:
                    return "volatile-rich";
                case CelestialPlanetFormationClass.GasRich:
                    return "gas-rich";
                case CelestialPlanetFormationClass.Giant:
                    return "giant";
                default:
                    return "unclassified";
            }
        }

        private static string DescribeMainSequenceMass(
            double massSolar)
        {
            if (massSolar < 0.5)
            {
                return "low-mass";
            }

            if (massSolar < 1.5)
            {
                return "Sun-like";
            }

            if (massSolar < 8.0)
            {
                return "intermediate-mass";
            }

            return "high-mass";
        }

        private static string DescribeRegion(
            CelestialGalacticRegion region)
        {
            switch (region)
            {
                case CelestialGalacticRegion.ThinDisk:
                    return "the galaxy's thin disk";
                case CelestialGalacticRegion.ThickDisk:
                    return "the galaxy's thick disk";
                case CelestialGalacticRegion.GalacticBulge:
                    return "the galactic bulge";
                case CelestialGalacticRegion.StellarHalo:
                    return "the stellar halo";
                case CelestialGalacticRegion.StarFormingRegion:
                    return "an active star-forming region";
                case CelestialGalacticRegion.DenseCluster:
                    return "a dense stellar cluster";
                default:
                    return "its local galactic environment";
            }
        }

        private static string DescribeComposition(
            double ironMetallicityDex,
            double alphaEnhancementDex)
        {
            string metallicity;

            if (ironMetallicityDex < -1.0)
            {
                metallicity = "strongly metal-poor";
            }
            else if (ironMetallicityDex < -0.25)
            {
                metallicity = "metal-poor";
            }
            else if (ironMetallicityDex > 0.25)
            {
                metallicity = "metal-rich";
            }
            else
            {
                metallicity = "roughly solar-metallicity";
            }

            return
                alphaEnhancementDex >= 0.2
                    ? metallicity + ", alpha-enhanced"
                    : metallicity;
        }

        private static string Format(
            double value)
        {
            return
                value.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture);
        }
    }
}
