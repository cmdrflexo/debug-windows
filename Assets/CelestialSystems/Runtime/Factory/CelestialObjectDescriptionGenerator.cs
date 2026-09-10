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
            CelestialPlanetFormationResult formation,
            int generationSeed)
        {
            var mass =
                Format(
                    formation.TotalMassEarth);
            var radius =
                Format(
                    formation.RadiusEarth);
            var finalOrbit =
                Format(
                    formation.FinalOrbitAstronomicalUnits);
            var classification =
                DescribePlanetFormationClass(
                    formation.FormationClass);
            var physicalSentence =
                $"Its estimated mass is {mass} Earth masses, with a radius of {radius} Earth radii.";
            var compositionSentence =
                DescribePlanetComposition(
                    formation);
            var orbitSentence =
                DescribePlanetOrbit(
                    formation,
                    finalOrbit);
            var hasMigration =
                orbitSentence.Length > 0;
            var orbitIntroduction =
                hasMigration
                    ? $"This is a {classification} planet."
                    : $"This {classification} planet orbits approximately {finalOrbit} astronomical units from its star.";

            switch (SelectVariant(
                generationSeed,
                3))
            {
                case 0:
                    return
                        JoinSentences(
                            orbitIntroduction,
                            physicalSentence,
                            compositionSentence,
                            orbitSentence);

                case 1:
                    return
                        JoinSentences(
                            $"With an estimated mass of {mass} Earth masses and a radius of {radius} Earth radii, this world is classified as a {classification} planet.",
                            compositionSentence,
                            orbitSentence);

                default:
                    return
                        JoinSentences(
                            hasMigration
                                ? $"This is a {classification} world."
                                : $"This is a {classification} world located approximately {finalOrbit} astronomical units from its star.",
                            compositionSentence,
                            physicalSentence,
                            orbitSentence);
            }
        }

        private static string DescribePlanetComposition(
            CelestialPlanetFormationResult formation)
        {
            var volatilePercent =
                formation.VolatileMassFraction *
                100.0;
            var envelopePercent =
                formation.HydrogenHeliumEnvelopeFraction *
                100.0;
            var hasVolatiles =
                volatilePercent >= 0.05;
            var hasEnvelope =
                envelopePercent >= 0.05;

            if (hasVolatiles &&
                hasEnvelope)
            {
                return
                    $"The formation model assigns about {Format(volatilePercent)} percent volatile material and a hydrogen-helium envelope containing about {Format(envelopePercent)} percent of its mass.";
            }

            if (hasEnvelope)
            {
                return
                    $"A hydrogen-helium envelope contains about {Format(envelopePercent)} percent of its mass.";
            }

            if (hasVolatiles)
            {
                return
                    $"The formation model assigns it a volatile fraction of about {Format(volatilePercent)} percent.";
            }

            return string.Empty;
        }

        private static string DescribePlanetOrbit(
            CelestialPlanetFormationResult formation,
            string finalOrbit)
        {
            if (formation.InwardMigrationFraction <
                0.0005)
            {
                return string.Empty;
            }

            var initialOrbit =
                Format(
                    formation.InitialOrbitAstronomicalUnits);

            return
                $"It formed near {initialOrbit} astronomical units before migrating inward to its present orbit near {finalOrbit} astronomical units.";
        }

        private static int SelectVariant(
            int generationSeed,
            int variantCount)
        {
            var value =
                unchecked((uint)generationSeed);
            value ^=
                value >> 16;
            value *=
                0x7FEB352Du;
            value ^=
                value >> 15;

            return
                (int)(value %
                    (uint)variantCount);
        }

        private static string JoinSentences(
            params string[] sentences)
        {
            var result =
                string.Empty;

            for (var index = 0;
                index < sentences.Length;
                index++)
            {
                if (string.IsNullOrWhiteSpace(
                        sentences[index]))
                {
                    continue;
                }

                if (result.Length > 0)
                {
                    result += " ";
                }

                result +=
                    sentences[index].Trim();
            }

            return result;
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
