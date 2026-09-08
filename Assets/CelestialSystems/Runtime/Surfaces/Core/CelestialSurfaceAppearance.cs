/*
 * Stores an ordered, reusable set of physical surface layers for a celestial body.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    public enum SurfaceLightingMode
    {
        Lit,
        Emissive,
        LitAndEmissive
    }

    [CreateAssetMenu(
        fileName = "Celestial Surface",
        menuName = "Celestial Systems/Surface Appearance/Surface")]
    public sealed class CelestialSurfaceAppearance :
        ScriptableObject
    {
        public const int MaximumSupportedLayerCount =
            4;

        [SerializeField]
        private string definitionId =
            "surface";

        [SerializeField]
        private SurfaceLightingMode lightingMode =
            SurfaceLightingMode.Lit;

        [SerializeField]
        private List<CelestialSurfaceLayerDefinition> layers =
            new List<CelestialSurfaceLayerDefinition>();

        public string DefinitionId =>
            definitionId;

        public SurfaceLightingMode LightingMode =>
            lightingMode;

        public IReadOnlyList<CelestialSurfaceLayerDefinition> Layers =>
            layers;

        public int LayerCount =>
            layers != null
                ? layers.Count
                : 0;

        public bool HasValidSettings =>
            TryValidate(
                out _);

        public CelestialSurfaceLayerDefinition GetLayer(
            int index)
        {
            return
                layers != null &&
                index >= 0 &&
                index < layers.Count
                    ? layers[index]
                    : null;
        }

        public bool TryValidate(
            out string error)
        {
            if (string.IsNullOrWhiteSpace(
                    definitionId))
            {
                error =
                    "A celestial surface definition requires a definition ID.";
                return false;
            }

            if (layers == null ||
                layers.Count == 0)
            {
                error =
                    "A celestial surface definition requires at least one layer.";
                return false;
            }

            if (layers.Count >
                MaximumSupportedLayerCount)
            {
                error =
                    $"A celestial surface definition currently supports at most {MaximumSupportedLayerCount} layers.";
                return false;
            }

            for (var index = 0;
                index < layers.Count;
                index++)
            {
                var layer =
                    layers[index];

                if (layer == null)
                {
                    error =
                        $"Surface layer {index} is missing.";
                    return false;
                }

                if (!layer.HasValidSettings)
                {
                    error =
                        $"Surface layer {index} ('{layer.name}') has invalid settings.";
                    return false;
                }

                for (var previousIndex = 0;
                    previousIndex < index;
                    previousIndex++)
                {
                    var previous =
                        layers[previousIndex];

                    if (previous == layer)
                    {
                        error =
                            $"Surface layer {index} duplicates layer {previousIndex}.";
                        return false;
                    }

                    if (string.Equals(
                            previous.LayerId,
                            layer.LayerId,
                            StringComparison.Ordinal))
                    {
                        error =
                            $"Surface layers {previousIndex} and {index} share the ID '{layer.LayerId}'.";
                        return false;
                    }

                    if (previous.MapMagicTerrainLayer != null &&
                        previous.MapMagicTerrainLayer ==
                            layer.MapMagicTerrainLayer)
                    {
                        error =
                            $"Surface layers {previousIndex} and {index} reference the same MapMagic TerrainLayer.";
                        return false;
                    }
                }
            }

            error =
                string.Empty;
            return true;
        }
    }
}
