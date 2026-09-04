/*
 * Identifies sampled surface data by body, graph contents, generation settings, datum, and cache format.
 */

using System;
using System.Globalization;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [Serializable]
    public struct CelestialSurfaceCacheKey :
        IEquatable<CelestialSurfaceCacheKey>
    {
        public const int CurrentFormatVersion = 1;

        [SerializeField]
        private int formatVersion;

        [SerializeField]
        private string definitionId;

        [SerializeField]
        private int generationSeed;

        [SerializeField]
        private double referenceRadiusMeters;

        [SerializeField]
        private int surfaceSeed;

        [SerializeField]
        private double elevationOffsetMeters;

        [SerializeField]
        private string graphSettingsHash;

        public int FormatVersion =>
            formatVersion;

        public string DefinitionId =>
            definitionId;

        public int GenerationSeed =>
            generationSeed;

        public double ReferenceRadiusMeters =>
            referenceRadiusMeters;

        public int SurfaceSeed =>
            surfaceSeed;

        public double ElevationOffsetMeters =>
            elevationOffsetMeters;

        public string GraphSettingsHash =>
            graphSettingsHash;

        public bool IsValid =>
            formatVersion == CurrentFormatVersion &&
            !string.IsNullOrWhiteSpace(definitionId) &&
            IsFinite(referenceRadiusMeters) &&
            referenceRadiusMeters > 0.0 &&
            IsFinite(elevationOffsetMeters) &&
            !string.IsNullOrWhiteSpace(graphSettingsHash);

        private CelestialSurfaceCacheKey(
            int formatVersion,
            string definitionId,
            int generationSeed,
            double referenceRadiusMeters,
            int surfaceSeed,
            double elevationOffsetMeters,
            string graphSettingsHash)
        {
            this.formatVersion =
                formatVersion;
            this.definitionId =
                definitionId;
            this.generationSeed =
                generationSeed;
            this.referenceRadiusMeters =
                referenceRadiusMeters;
            this.surfaceSeed =
                surfaceSeed;
            this.elevationOffsetMeters =
                elevationOffsetMeters;
            this.graphSettingsHash =
                graphSettingsHash;
        }

        public static bool TryCreate(
            CelestialBodyDefinition definition,
            out CelestialSurfaceCacheKey cacheKey,
            out string error)
        {
            cacheKey = default;

            if (definition == null ||
                !definition.HasValidPhysicalSettings)
            {
                error =
                    "A valid celestial body definition is required to create a surface cache key.";
                return false;
            }

            if (definition.ResolvedSurfaceSystem !=
                    CelestialSurfaceSystem.RoundMapMagic ||
                definition.RoundMapMagicSurface == null ||
                !definition.RoundMapMagicSurface.HasValidSettings)
            {
                error =
                    "A valid Round MapMagic surface definition is required to create a surface cache key.";
                return false;
            }

            var surface =
                definition.RoundMapMagicSurface;
            string graphHash;

            try
            {
                var graphJson =
                    JsonUtility.ToJson(
                        surface.Graph);
                var hashInput =
                    string.Join(
                        "|",
                        CurrentFormatVersion.ToString(
                            CultureInfo.InvariantCulture),
                        definition.DefinitionId,
                        definition.GenerationSeed.ToString(
                            CultureInfo.InvariantCulture),
                        definition.ReferenceRadiusMeters.ToString(
                            "R",
                            CultureInfo.InvariantCulture),
                        surface.SurfaceSeed.ToString(
                            CultureInfo.InvariantCulture),
                        surface.ElevationOffsetMeters.ToString(
                            "R",
                            CultureInfo.InvariantCulture),
                        surface.Graph.name,
                        graphJson);
                graphHash =
                    Hash128.Compute(
                        hashInput).ToString();
            }
            catch (Exception exception)
            {
                error =
                    $"Could not fingerprint the MapMagic surface graph: {exception.GetBaseException().Message}";
                return false;
            }

            cacheKey =
                new CelestialSurfaceCacheKey(
                    CurrentFormatVersion,
                    definition.DefinitionId,
                    definition.GenerationSeed,
                    definition.ReferenceRadiusMeters,
                    surface.SurfaceSeed,
                    surface.ElevationOffsetMeters,
                    graphHash);
            error = string.Empty;
            return cacheKey.IsValid;
        }

        public bool Equals(
            CelestialSurfaceCacheKey other)
        {
            return
                formatVersion == other.formatVersion &&
                string.Equals(
                    definitionId,
                    other.definitionId,
                    StringComparison.Ordinal) &&
                generationSeed == other.generationSeed &&
                referenceRadiusMeters.Equals(
                    other.referenceRadiusMeters) &&
                surfaceSeed == other.surfaceSeed &&
                elevationOffsetMeters.Equals(
                    other.elevationOffsetMeters) &&
                string.Equals(
                    graphSettingsHash,
                    other.graphSettingsHash,
                    StringComparison.Ordinal);
        }

        public override bool Equals(
            object value)
        {
            return
                value is CelestialSurfaceCacheKey other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = formatVersion;
                hash =
                    (hash * 397) ^
                    (definitionId != null
                        ? definitionId.GetHashCode()
                        : 0);
                hash =
                    (hash * 397) ^
                    generationSeed;
                hash =
                    (hash * 397) ^
                    referenceRadiusMeters.GetHashCode();
                hash =
                    (hash * 397) ^
                    surfaceSeed;
                hash =
                    (hash * 397) ^
                    elevationOffsetMeters.GetHashCode();
                hash =
                    (hash * 397) ^
                    (graphSettingsHash != null
                        ? graphSettingsHash.GetHashCode()
                        : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            return
                $"{definitionId} v{formatVersion} {graphSettingsHash}";
        }

        public static bool operator ==(
            CelestialSurfaceCacheKey first,
            CelestialSurfaceCacheKey second)
        {
            return first.Equals(second);
        }

        public static bool operator !=(
            CelestialSurfaceCacheKey first,
            CelestialSurfaceCacheKey second)
        {
            return !first.Equals(second);
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
