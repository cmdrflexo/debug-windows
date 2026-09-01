/*
 * Adapts up to four active MapMagic TerrainLayers and their first control map into a curved-tile runtime material.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(325)]
    [DisallowMultipleComponent]
    public sealed class CubeSphereMapMagicTerrainLayerMaterialAdapter :
        MonoBehaviour
    {
        private const int MaximumAdaptedLayerCount =
            4;

        private const string DefaultLayerShaderName =
            "jcan/Celestial Systems/Curved MapMagic Terrain Layers";

        [Header("Configuration")]
        [SerializeField]
        private CubeSphereMapMagicCurvedTilePrototype curvedTile;

        [Header("Runtime")]
        [SerializeField]
        private bool hasAdaptedMaterial;

        [SerializeField]
        private int terrainLayerCount;

        [SerializeField]
        private int adaptedLayerCount;

        [SerializeField]
        private int controlTextureCount;

        [SerializeField]
        private Texture controlTexture;

        [SerializeField]
        private TerrainLayer activeTerrainLayer;

        [SerializeField]
        private Texture diffuseTexture;

        [SerializeField]
        private Texture normalTexture;

        [SerializeField]
        private Texture maskTexture;

        [SerializeField]
        private Vector2 layerTileSizeMeters;

        [SerializeField]
        private Vector2 materialTextureScale;

        [SerializeField]
        private Vector2 materialTextureOffset;

        [SerializeField]
        private bool usesDefinitionMaterial;

        [SerializeField]
        private Material generatedMaterial;

        private TerrainData builtTerrainData;
        private Material builtTemplateMaterial;
        private int builtTileX;
        private int builtTileZ;
        private bool missingShaderLogged;

        private void Reset()
        {
            curvedTile =
                GetComponent<CubeSphereMapMagicCurvedTilePrototype>();
        }

        private void Start()
        {
            if (curvedTile == null)
            {
                Debug.LogError(
                    "The MapMagic terrain-layer material adapter requires a curved tile prototype.",
                    this);
            }
        }

        private void LateUpdate()
        {
            if (curvedTile == null ||
                !curvedTile.HasCurvedTile ||
                curvedTile.CurvedTileRenderer == null ||
                curvedTile.SourceTerrainData == null)
            {
                ReleaseGeneratedMaterial();
                ClearRuntimeState();
                return;
            }

            var terrainData =
                curvedTile.SourceTerrainData;
            var terrainLayers =
                terrainData.terrainLayers;
            var controlTextures =
                terrainData.alphamapTextures;
            terrainLayerCount =
                terrainLayers != null
                    ? terrainLayers.Length
                    : 0;
            controlTextureCount =
                controlTextures != null
                    ? controlTextures.Length
                    : 0;

            if (!CanAdapt(
                    terrainLayers,
                    controlTextures))
            {
                ReleaseGeneratedMaterial();
                ClearLayerRuntimeState();
                return;
            }

            var templateMaterial =
                curvedTile.ResolvedMeshMaterial;
            var tileX =
                curvedTile.SourceTileX;
            var tileZ =
                curvedTile.SourceTileZ;

            if (generatedMaterial == null ||
                builtTerrainData != terrainData ||
                builtTemplateMaterial != templateMaterial ||
                builtTileX != tileX ||
                builtTileZ != tileZ)
            {
                BuildGeneratedMaterial(
                    terrainData,
                    templateMaterial,
                    tileX,
                    tileZ);
            }

            if (generatedMaterial == null)
            {
                return;
            }

            ConfigureGeneratedMaterial(
                terrainData,
                terrainLayers,
                controlTextures,
                tileX,
                tileZ);
            curvedTile.CurvedTileRenderer.sharedMaterial =
                generatedMaterial;
            hasAdaptedMaterial = true;
        }

        private static bool CanAdapt(
            TerrainLayer[] terrainLayers,
            Texture2D[] controlTextures)
        {
            if (terrainLayers == null ||
                terrainLayers.Length < 1 ||
                terrainLayers.Length >
                    MaximumAdaptedLayerCount)
            {
                return false;
            }

            for (var index = 0;
                index < terrainLayers.Length;
                index++)
            {
                if (terrainLayers[index] == null)
                {
                    return false;
                }
            }

            return
                terrainLayers.Length == 1 ||
                (controlTextures != null &&
                controlTextures.Length > 0 &&
                controlTextures[0] != null);
        }

        private void BuildGeneratedMaterial(
            TerrainData terrainData,
            Material templateMaterial,
            int tileX,
            int tileZ)
        {
            ReleaseGeneratedMaterial();
            usesDefinitionMaterial =
                IsCompatibleTemplate(
                    templateMaterial);

            if (usesDefinitionMaterial)
            {
                generatedMaterial =
                    new Material(
                        templateMaterial);
            }
            else
            {
                var shader =
                    Shader.Find(
                        DefaultLayerShaderName);

                if (shader == null)
                {
                    if (!missingShaderLogged)
                    {
                        Debug.LogError(
                            $"Shader '{DefaultLayerShaderName}' was not found for the MapMagic terrain-layer material adapter.",
                            this);
                        missingShaderLogged = true;
                    }

                    return;
                }

                generatedMaterial =
                    new Material(
                        shader);
            }

            generatedMaterial.name =
                "Curved MapMagic Terrain Layers Material";
            generatedMaterial.hideFlags =
                HideFlags.HideAndDontSave;
            builtTerrainData =
                terrainData;
            builtTemplateMaterial =
                templateMaterial;
            builtTileX =
                tileX;
            builtTileZ =
                tileZ;
            missingShaderLogged = false;
        }

        private static bool IsCompatibleTemplate(
            Material material)
        {
            return
                material != null &&
                material.HasProperty(
                    "_Control") &&
                material.HasProperty(
                    "_LayerCount") &&
                material.HasProperty(
                    "_Splat0");
        }

        private void ConfigureGeneratedMaterial(
            TerrainData terrainData,
            TerrainLayer[] terrainLayers,
            Texture2D[] controlTextures,
            int tileX,
            int tileZ)
        {
            adaptedLayerCount =
                terrainLayers.Length;
            controlTexture =
                controlTextures != null &&
                controlTextures.Length > 0 &&
                controlTextures[0] != null
                    ? controlTextures[0]
                    : Texture2D.whiteTexture;

            ApplyTexture(
                generatedMaterial,
                "_Control",
                controlTexture,
                Vector2.one,
                Vector2.zero);
            SetFloatIfPresent(
                generatedMaterial,
                "_LayerCount",
                adaptedLayerCount);

            for (var index = 0;
                index < MaximumAdaptedLayerCount;
                index++)
            {
                ConfigureLayer(
                    terrainData,
                    index < terrainLayers.Length
                        ? terrainLayers[index]
                        : null,
                    index,
                    tileX,
                    tileZ);
            }

            var firstLayer =
                terrainLayers[0];
            activeTerrainLayer =
                firstLayer;
            diffuseTexture =
                firstLayer.diffuseTexture;
            normalTexture =
                firstLayer.normalMapTexture;
            maskTexture =
                firstLayer.maskMapTexture;
            layerTileSizeMeters =
                firstLayer.tileSize;
            ResolveTextureTransform(
                terrainData,
                firstLayer,
                tileX,
                tileZ,
                out materialTextureScale,
                out materialTextureOffset);
        }

        private void ConfigureLayer(
            TerrainData terrainData,
            TerrainLayer terrainLayer,
            int layerIndex,
            int tileX,
            int tileZ)
        {
            var suffix =
                layerIndex.ToString();

            if (terrainLayer == null)
            {
                ApplyTexture(
                    generatedMaterial,
                    "_Splat" + suffix,
                    Texture2D.whiteTexture,
                    Vector2.one,
                    Vector2.zero);
                ApplyTexture(
                    generatedMaterial,
                    "_Normal" + suffix,
                    null,
                    Vector2.one,
                    Vector2.zero);
                ApplyTexture(
                    generatedMaterial,
                    "_Mask" + suffix,
                    null,
                    Vector2.one,
                    Vector2.zero);
                SetFloatIfPresent(
                    generatedMaterial,
                    "_HasNormal" + suffix,
                    0.0f);
                SetFloatIfPresent(
                    generatedMaterial,
                    "_HasMask" + suffix,
                    0.0f);
                return;
            }

            ResolveTextureTransform(
                terrainData,
                terrainLayer,
                tileX,
                tileZ,
                out var textureScale,
                out var textureOffset);
            var layerDiffuse =
                terrainLayer.diffuseTexture != null
                    ? terrainLayer.diffuseTexture
                    : Texture2D.whiteTexture;
            var layerNormal =
                terrainLayer.normalMapTexture;
            var layerMask =
                terrainLayer.maskMapTexture;

            ApplyTexture(
                generatedMaterial,
                "_Splat" + suffix,
                layerDiffuse,
                textureScale,
                textureOffset);
            ApplyTexture(
                generatedMaterial,
                "_Normal" + suffix,
                layerNormal,
                textureScale,
                textureOffset);
            ApplyTexture(
                generatedMaterial,
                "_Mask" + suffix,
                layerMask,
                textureScale,
                textureOffset);
            SetFloatIfPresent(
                generatedMaterial,
                "_HasNormal" + suffix,
                layerNormal != null
                    ? 1.0f
                    : 0.0f);
            SetFloatIfPresent(
                generatedMaterial,
                "_HasMask" + suffix,
                layerMask != null
                    ? 1.0f
                    : 0.0f);
            SetFloatIfPresent(
                generatedMaterial,
                "_NormalScale" + suffix,
                terrainLayer.normalScale);
            SetFloatIfPresent(
                generatedMaterial,
                "_Metallic" + suffix,
                terrainLayer.metallic);
            SetFloatIfPresent(
                generatedMaterial,
                "_Smoothness" + suffix,
                terrainLayer.smoothness);
        }

        private static void ResolveTextureTransform(
            TerrainData terrainData,
            TerrainLayer terrainLayer,
            int tileX,
            int tileZ,
            out Vector2 scale,
            out Vector2 offset)
        {
            var tileSizeX =
                SafeTileSize(
                    terrainLayer.tileSize.x);
            var tileSizeZ =
                SafeTileSize(
                    terrainLayer.tileSize.y);
            scale =
                new Vector2(
                    terrainData.size.x /
                        tileSizeX,
                    terrainData.size.z /
                        tileSizeZ);
            offset =
                new Vector2(
                    Repeat01(
                        ((double)tileX *
                            terrainData.size.x +
                        terrainLayer.tileOffset.x) /
                        tileSizeX),
                    Repeat01(
                        ((double)tileZ *
                            terrainData.size.z +
                        terrainLayer.tileOffset.y) /
                        tileSizeZ));
        }

        private static void ApplyTexture(
            Material material,
            string propertyName,
            Texture texture,
            Vector2 scale,
            Vector2 offset)
        {
            if (!material.HasProperty(
                    propertyName))
            {
                return;
            }

            material.SetTexture(
                propertyName,
                texture);
            material.SetTextureScale(
                propertyName,
                scale);
            material.SetTextureOffset(
                propertyName,
                offset);
        }

        private static void SetFloatIfPresent(
            Material material,
            string propertyName,
            float value)
        {
            if (material.HasProperty(
                    propertyName))
            {
                material.SetFloat(
                    propertyName,
                    value);
            }
        }

        private void ReleaseGeneratedMaterial()
        {
            if (generatedMaterial == null)
            {
                usesDefinitionMaterial = false;
                return;
            }

            var renderer =
                curvedTile != null
                    ? curvedTile.CurvedTileRenderer
                    : null;

            if (renderer != null &&
                renderer.sharedMaterial ==
                    generatedMaterial)
            {
                renderer.sharedMaterial =
                    curvedTile.ResolvedMeshMaterial;
            }

            DestroyUnityObject(
                generatedMaterial);
            generatedMaterial = null;
            builtTerrainData = null;
            builtTemplateMaterial = null;
            builtTileX = default;
            builtTileZ = default;
            hasAdaptedMaterial = false;
            usesDefinitionMaterial = false;
        }

        private void ClearRuntimeState()
        {
            terrainLayerCount = default;
            controlTextureCount = default;
            ClearLayerRuntimeState();
        }

        private void ClearLayerRuntimeState()
        {
            adaptedLayerCount = default;
            controlTexture = null;
            activeTerrainLayer = null;
            diffuseTexture = null;
            normalTexture = null;
            maskTexture = null;
            layerTileSizeMeters = default;
            materialTextureScale = default;
            materialTextureOffset = default;
            hasAdaptedMaterial = false;
        }

        private void OnDisable()
        {
            ReleaseGeneratedMaterial();
            ClearRuntimeState();
        }

        private void OnDestroy()
        {
            ReleaseGeneratedMaterial();
        }

        private static float SafeTileSize(
            float value)
        {
            return
                IsFinite(value) &&
                Mathf.Abs(value) >
                    Mathf.Epsilon
                    ? Mathf.Abs(value)
                    : 1.0f;
        }

        private static float Repeat01(
            double value)
        {
            return
                (float)(
                    value -
                    Math.Floor(
                        value));
        }

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }

        private static void DestroyUnityObject(
            UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }
    }
}
