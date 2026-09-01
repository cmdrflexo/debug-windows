/*
 * Adapts one active MapMagic TerrainLayer into a runtime material for the curved cube-sphere tile.
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(325)]
    [DisallowMultipleComponent]
    public sealed class CubeSphereMapMagicTerrainLayerMaterialAdapter :
        MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private CubeSphereMapMagicCurvedTilePrototype curvedTile;

        [Header("Runtime")]
        [SerializeField]
        private bool hasAdaptedMaterial;

        [SerializeField]
        private int terrainLayerCount;

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
        private Material generatedMaterial;

        private TerrainData builtTerrainData;
        private TerrainLayer builtTerrainLayer;
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
            terrainLayerCount =
                terrainLayers != null
                    ? terrainLayers.Length
                    : 0;

            if (terrainLayerCount != 1 ||
                terrainLayers[0] == null)
            {
                ReleaseGeneratedMaterial();
                ClearLayerRuntimeState();
                return;
            }

            var terrainLayer =
                terrainLayers[0];
            var templateMaterial =
                curvedTile.ResolvedMeshMaterial;
            var tileX =
                curvedTile.SourceTileX;
            var tileZ =
                curvedTile.SourceTileZ;

            if (generatedMaterial == null ||
                builtTerrainData != terrainData ||
                builtTerrainLayer != terrainLayer ||
                builtTemplateMaterial != templateMaterial ||
                builtTileX != tileX ||
                builtTileZ != tileZ)
            {
                BuildGeneratedMaterial(
                    terrainData,
                    terrainLayer,
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
                terrainLayer,
                tileX,
                tileZ);
            curvedTile.CurvedTileRenderer.sharedMaterial =
                generatedMaterial;
            hasAdaptedMaterial = true;
        }

        private void BuildGeneratedMaterial(
            TerrainData terrainData,
            TerrainLayer terrainLayer,
            Material templateMaterial,
            int tileX,
            int tileZ)
        {
            ReleaseGeneratedMaterial();

            if (templateMaterial != null)
            {
                generatedMaterial =
                    new Material(
                        templateMaterial);
            }
            else
            {
                var shader =
                    ResolveLitShader();

                if (shader == null)
                {
                    if (!missingShaderLogged)
                    {
                        Debug.LogError(
                            "No compatible lit shader was found for the MapMagic terrain-layer material adapter.",
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
                "Curved MapMagic Terrain Layer Material";
            generatedMaterial.hideFlags =
                HideFlags.HideAndDontSave;
            builtTerrainData =
                terrainData;
            builtTerrainLayer =
                terrainLayer;
            builtTemplateMaterial =
                templateMaterial;
            builtTileX =
                tileX;
            builtTileZ =
                tileZ;
            missingShaderLogged = false;
        }

        private void ConfigureGeneratedMaterial(
            TerrainData terrainData,
            TerrainLayer terrainLayer,
            int tileX,
            int tileZ)
        {
            activeTerrainLayer =
                terrainLayer;
            diffuseTexture =
                terrainLayer.diffuseTexture;
            normalTexture =
                terrainLayer.normalMapTexture;
            maskTexture =
                terrainLayer.maskMapTexture;
            layerTileSizeMeters =
                terrainLayer.tileSize;

            var tileSizeX =
                SafeTileSize(
                    terrainLayer.tileSize.x);
            var tileSizeZ =
                SafeTileSize(
                    terrainLayer.tileSize.y);
            materialTextureScale =
                new Vector2(
                    terrainData.size.x /
                        tileSizeX,
                    terrainData.size.z /
                        tileSizeZ);
            materialTextureOffset =
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

            ApplyTexture(
                generatedMaterial,
                "_BaseMap",
                diffuseTexture,
                materialTextureScale,
                materialTextureOffset);
            ApplyTexture(
                generatedMaterial,
                "_MainTex",
                diffuseTexture,
                materialTextureScale,
                materialTextureOffset);
            ApplyTexture(
                generatedMaterial,
                "_BumpMap",
                normalTexture,
                materialTextureScale,
                materialTextureOffset);
            ApplyTexture(
                generatedMaterial,
                "_NormalMap",
                normalTexture,
                materialTextureScale,
                materialTextureOffset);
            ApplyTexture(
                generatedMaterial,
                "_MaskMap",
                maskTexture,
                materialTextureScale,
                materialTextureOffset);

            SetColorIfPresent(
                generatedMaterial,
                "_BaseColor",
                Color.white);
            SetColorIfPresent(
                generatedMaterial,
                "_Color",
                Color.white);
            SetFloatIfPresent(
                generatedMaterial,
                "_BumpScale",
                terrainLayer.normalScale);
            SetFloatIfPresent(
                generatedMaterial,
                "_Metallic",
                terrainLayer.metallic);
            SetFloatIfPresent(
                generatedMaterial,
                "_Smoothness",
                terrainLayer.smoothness);
            SetFloatIfPresent(
                generatedMaterial,
                "_Glossiness",
                terrainLayer.smoothness);
            SetColorIfPresent(
                generatedMaterial,
                "_SpecColor",
                terrainLayer.specular);

            SetKeyword(
                generatedMaterial,
                "_NORMALMAP",
                normalTexture != null);
            SetKeyword(
                generatedMaterial,
                "_MASKMAP",
                maskTexture != null);
        }

        private static Shader ResolveLitShader()
        {
            var renderPipeline =
                GraphicsSettings.currentRenderPipeline;
            var pipelineName =
                renderPipeline != null
                    ? renderPipeline.GetType().Name
                    : string.Empty;

            if (pipelineName.IndexOf(
                    "Universal",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return
                    Shader.Find(
                        "Universal Render Pipeline/Lit");
            }

            if (pipelineName.IndexOf(
                    "HDRender",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                pipelineName.IndexOf(
                    "HighDefinition",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return
                    Shader.Find(
                        "HDRP/Lit");
            }

            return
                Shader.Find(
                    "Standard") ??
                Shader.Find(
                    "Unlit/Texture");
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

        private static void SetColorIfPresent(
            Material material,
            string propertyName,
            Color value)
        {
            if (material.HasProperty(
                    propertyName))
            {
                material.SetColor(
                    propertyName,
                    value);
            }
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

        private static void SetKeyword(
            Material material,
            string keyword,
            bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(
                    keyword);
            }
            else
            {
                material.DisableKeyword(
                    keyword);
            }
        }

        private void ReleaseGeneratedMaterial()
        {
            if (generatedMaterial == null)
            {
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
            builtTerrainLayer = null;
            builtTemplateMaterial = null;
            builtTileX = default;
            builtTileZ = default;
            hasAdaptedMaterial = false;
        }

        private void ClearRuntimeState()
        {
            terrainLayerCount = default;
            ClearLayerRuntimeState();
        }

        private void ClearLayerRuntimeState()
        {
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
