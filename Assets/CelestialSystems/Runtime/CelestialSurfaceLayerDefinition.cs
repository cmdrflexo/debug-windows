/*
 * Stores one reusable physical surface appearance and its optional MapMagic TerrainLayer identity.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [CreateAssetMenu(
        fileName = "Celestial Surface Layer",
        menuName = "Celestial Systems/Surface Appearance/Layer")]
    public sealed class CelestialSurfaceLayerDefinition :
        ScriptableObject
    {
        [SerializeField]
        private string layerId =
            "surface";

        [SerializeField]
        [Tooltip("Optional TerrainLayer used by MapMagic to identify this surface in graph output.")]
        private TerrainLayer mapMagicTerrainLayer;

        [Header("Textures")]
        [SerializeField]
        private Texture2D albedoTexture;

        [SerializeField]
        private Texture2D normalTexture;

        [SerializeField]
        [Tooltip("Optional packed mask: R metallic, G ambient occlusion, B height, A smoothness.")]
        private Texture2D maskTexture;

        [Header("Surface Values")]
        [SerializeField]
        private Color tint =
            Color.white;

        [SerializeField]
        [Min(0.001f)]
        [Tooltip("Physical width and height represented by one texture repeat, in meters.")]
        private float textureScaleMeters =
            10.0f;

        [SerializeField]
        [Range(0.0f, 2.0f)]
        private float normalStrength =
            1.0f;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float metallic;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float smoothness =
            0.25f;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float occlusionStrength =
            1.0f;

        public string LayerId =>
            layerId;

        public TerrainLayer MapMagicTerrainLayer =>
            mapMagicTerrainLayer;

        public Texture2D AlbedoTexture =>
            albedoTexture;

        public Texture2D NormalTexture =>
            normalTexture;

        public Texture2D MaskTexture =>
            maskTexture;

        public Color Tint =>
            tint;

        public float TextureScaleMeters =>
            textureScaleMeters;

        public float NormalStrength =>
            normalStrength;

        public float Metallic =>
            metallic;

        public float Smoothness =>
            smoothness;

        public float OcclusionStrength =>
            occlusionStrength;

        public bool HasValidSettings =>
            !string.IsNullOrWhiteSpace(
                layerId) &&
            IsFinite(
                tint) &&
            IsFinite(
                textureScaleMeters) &&
            textureScaleMeters > 0.0f &&
            IsUnitValue(
                metallic) &&
            IsUnitValue(
                smoothness) &&
            IsUnitValue(
                occlusionStrength) &&
            IsFinite(
                normalStrength) &&
            normalStrength >= 0.0f;

        private static bool IsUnitValue(
            float value)
        {
            return
                IsFinite(
                    value) &&
                value >= 0.0f &&
                value <= 1.0f;
        }

        private static bool IsFinite(
            Color value)
        {
            return
                IsFinite(
                    value.r) &&
                IsFinite(
                    value.g) &&
                IsFinite(
                    value.b) &&
                IsFinite(
                    value.a);
        }

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(
                    value) &&
                !float.IsInfinity(
                    value);
        }
    }
}
