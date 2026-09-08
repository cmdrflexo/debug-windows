/*
 * Provides an explicit runtime catalog of reusable celestial body, surface, ocean, and quality definitions.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [CreateAssetMenu(
        fileName = "Celestial Definition Catalog",
        menuName = "Celestial Systems/Definition Catalog")]
    public sealed class CelestialDefinitionCatalog : ScriptableObject
    {
        [SerializeField] private List<CelestialBodyDefinition> bodyDefinitions = new List<CelestialBodyDefinition>();
        [SerializeField] private List<RoundMapMagicSurfaceDefinition> surfaceDefinitions = new List<RoundMapMagicSurfaceDefinition>();
        [SerializeField] private List<OceanDefinition> oceanDefinitions = new List<OceanDefinition>();
        [SerializeField] private List<RoundMapMagicSurfaceQualityProfile> qualityProfiles = new List<RoundMapMagicSurfaceQualityProfile>();

        public IReadOnlyList<CelestialBodyDefinition> BodyDefinitions => bodyDefinitions;
        public IReadOnlyList<RoundMapMagicSurfaceDefinition> SurfaceDefinitions => surfaceDefinitions;
        public IReadOnlyList<OceanDefinition> OceanDefinitions => oceanDefinitions;
        public IReadOnlyList<RoundMapMagicSurfaceQualityProfile> QualityProfiles => qualityProfiles;

        public bool TryGetBody(string definitionId, out CelestialBodyDefinition definition)
        {
            definition = bodyDefinitions.Find(candidate =>
                candidate != null && string.Equals(candidate.DefinitionId, definitionId, StringComparison.Ordinal));
            return definition != null;
        }

        public bool TryGetSurface(string assetName, out RoundMapMagicSurfaceDefinition definition)
        {
            definition = surfaceDefinitions.Find(candidate =>
                candidate != null && string.Equals(candidate.name, assetName, StringComparison.Ordinal));
            return definition != null;
        }

        public bool TryGetOcean(string assetName, out OceanDefinition definition)
        {
            definition = oceanDefinitions.Find(candidate =>
                candidate != null && string.Equals(candidate.name, assetName, StringComparison.Ordinal));
            return definition != null;
        }

        public bool TryGetQuality(string assetName, out RoundMapMagicSurfaceQualityProfile definition)
        {
            definition = qualityProfiles.Find(candidate =>
                candidate != null && string.Equals(candidate.name, assetName, StringComparison.Ordinal));
            return definition != null;
        }
    }
}
