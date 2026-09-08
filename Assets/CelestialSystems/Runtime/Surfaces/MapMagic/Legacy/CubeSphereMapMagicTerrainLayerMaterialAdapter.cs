/*
 * Adapts up to four MapMagic TerrainLayers and their first control map across the active curved-tile pool.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(325)]
    [DisallowMultipleComponent]
    public sealed class CubeSphereMapMagicTerrainLayerMaterialAdapter :
        MonoBehaviour
    {
        private sealed class TileMaterialRuntime
        {
            public Material Material;
            public Material TemplateMaterial;
            public TerrainData TerrainData;
            public MeshRenderer Renderer;
            public bool UsesDefinitionMaterial;
        }

        private struct TileKey :
            IEquatable<TileKey>
        {
            public int X;
            public int Z;

            public TileKey(
                int x,
                int z)
            {
                X =
                    x;
                Z =
                    z;
            }

            public bool Equals(
                TileKey other)
            {
                return
                    X == other.X &&
                    Z == other.Z;
            }

            public override bool Equals(
                object value)
            {
                return
                    value is TileKey other &&
                    Equals(
                        other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return
                        (X * 397) ^
                        Z;
                }
            }
        }

        private const int MaximumAdaptedLayerCount =
            4;

        private const string DefaultLayerShaderName =
            "jcan/Celestial Systems/Curved MapMagic Terrain Layers";

        [Header("Configuration")]
        [SerializeField]
        private CubeSphereMapMagicCurvedTilePrototype curvedTile;

        [Header("Pool Runtime")]
        [SerializeField]
        private int sourceCurvedTileCount;

        [SerializeField]
        private int adaptedCurvedTileCount;

        [Header("Primary Tile Runtime")]
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

        private readonly List<
            CubeSphereMapMagicCurvedTilePrototype.CurvedTileData>
                sourceTiles =
                    new List<
                        CubeSphereMapMagicCurvedTilePrototype.CurvedTileData>();

        private readonly Dictionary<TileKey, TileMaterialRuntime>
            tileMaterials =
                new Dictionary<TileKey, TileMaterialRuntime>();

        private readonly HashSet<TileKey>
            desiredMaterialTiles =
                new HashSet<TileKey>();

        private readonly List<TileKey>
            removalBuffer =
                new List<TileKey>();

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
            if (curvedTile == null)
            {
                ReleaseAllGeneratedMaterials();
                ClearRuntimeState();
                return;
            }

            curvedTile.CopyCurvedTilesTo(
                sourceTiles);
            sourceCurvedTileCount =
                sourceTiles.Count;
            adaptedCurvedTileCount = 0;
            desiredMaterialTiles.Clear();
            ClearPrimaryRuntimeState();

            var primaryTileX =
                curvedTile.SourceTileX;
            var primaryTileZ =
                curvedTile.SourceTileZ;
            var templateMaterial =
                curvedTile.ResolvedMeshMaterial;

            for (var index = 0;
                index < sourceTiles.Count;
                index++)
            {
                var sourceTile =
                    sourceTiles[index];
                var key =
                    new TileKey(
                        sourceTile.TileX,
                        sourceTile.TileZ);
                desiredMaterialTiles.Add(
                    key);

                var terrainData =
                    sourceTile.TerrainData;
                var terrainLayers =
                    terrainData != null
                        ? terrainData.terrainLayers
                        : null;
                var controlTextures =
                    terrainData != null
                        ? terrainData.alphamapTextures
                        : null;

                if (!CanAdapt(
                        terrainLayers,
                        controlTextures))
                {
                    RemoveGeneratedMaterial(
                        key);
                    continue;
                }

                var runtime =
                    ResolveMaterialRuntime(
                        key,
                        sourceTile.Renderer,
                        terrainData,
                        templateMaterial);

                if (runtime == null ||
                    runtime.Material == null)
                {
                    continue;
                }

                ConfigureGeneratedMaterial(
                    runtime.Material,
                    terrainData,
                    terrainLayers,
                    controlTextures,
                    sourceTile.TileX,
                    sourceTile.TileZ);
                sourceTile.Renderer.sharedMaterial =
                    runtime.Material;
                adaptedCurvedTileCount++;

                if (sourceTile.TileX ==
                        primaryTileX &&
                    sourceTile.TileZ ==
                        primaryTileZ)
                {
                    ReportPrimaryRuntime(
                        runtime,
                        terrainData,
                        terrainLayers,
                        controlTextures,
                        sourceTile.TileX,
                        sourceTile.TileZ);
                }
            }

            RemoveUndesiredMaterials();
        }

        private TileMaterialRuntime ResolveMaterialRuntime(
            TileKey key,
            MeshRenderer renderer,
            TerrainData terrainData,
            Material templateMaterial)
        {
            if (tileMaterials.TryGetValue(
                    key,
                    out var runtime) &&
                runtime.Material != null &&
                runtime.TerrainData ==
                    terrainData &&
                runtime.TemplateMaterial ==
                    templateMaterial)
            {
                runtime.Renderer =
                    renderer;
                return
                    runtime;
            }

            RemoveGeneratedMaterial(
                key);
            runtime =
                new TileMaterialRuntime
                {
                    Renderer =
                        renderer,
                    TerrainData =
                        terrainData,
                    TemplateMaterial =
                        templateMaterial,
                    UsesDefinitionMaterial =
                        IsCompatibleTemplate(
                            templateMaterial)
                };

            if (runtime.UsesDefinitionMaterial)
            {
                runtime.Material =
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
                        missingShaderLogged =
                            true;
                    }

                    return null;
                }

                runtime.Material =
                    new Material(
                        shader);
            }

            runtime.Material.name =
                $"Curved MapMagic Terrain Layers {key.X},{key.Z}";
            runtime.Material.hideFlags =
                HideFlags.HideAndDontSave;
            tileMaterials.Add(
                key,
                runtime);
            missingShaderLogged =
                false;
            return
                runtime;
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
                terrainLayers.Length ==
                    1 ||
                (controlTextures != null &&
                controlTextures.Length > 0 &&
                controlTextures[0] != null);
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

        private static void ConfigureGeneratedMaterial(
            Material material,
            TerrainData terrainData,
            TerrainLayer[] terrainLayers,
            Texture2D[] controlTextures,
            int tileX,
            int tileZ)
        {
            var firstControlTexture =
                controlTextures != null &&
                controlTextures.Length > 0 &&
                controlTextures[0] != null
                    ? controlTextures[0]
                    : Texture2D.whiteTexture;

            ApplyTexture(
                material,
                "_Control",
                firstControlTexture,
                Vector2.one,
                Vector2.zero);
            SetFloatIfPresent(
                material,
                "_LayerCount",
                terrainLayers.Length);

            for (var index = 0;
                index < MaximumAdaptedLayerCount;
                index++)
            {
                ConfigureLayer(
                    material,
                    terrainData,
                    index < terrainLayers.Length
                        ? terrainLayers[index]
                        : null,
                    index,
                    tileX,
                    tileZ);
            }
        }

        private static void ConfigureLayer(
            Material material,
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
                    material,
                    "_Splat" + suffix,
                    Texture2D.whiteTexture,
                    Vector2.one,
                    Vector2.zero);
                ApplyTexture(
                    material,
                    "_Normal" + suffix,
                    null,
                    Vector2.one,
                    Vector2.zero);
                ApplyTexture(
                    material,
                    "_Mask" + suffix,
                    null,
                    Vector2.one,
                    Vector2.zero);
                SetFloatIfPresent(
                    material,
                    "_HasNormal" + suffix,
                    0.0f);
                SetFloatIfPresent(
                    material,
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
                material,
                "_Splat" + suffix,
                layerDiffuse,
                textureScale,
                textureOffset);
            ApplyTexture(
                material,
                "_Normal" + suffix,
                layerNormal,
                textureScale,
                textureOffset);
            ApplyTexture(
                material,
                "_Mask" + suffix,
                layerMask,
                textureScale,
                textureOffset);
            SetFloatIfPresent(
                material,
                "_HasNormal" + suffix,
                layerNormal != null
                    ? 1.0f
                    : 0.0f);
            SetFloatIfPresent(
                material,
                "_HasMask" + suffix,
                layerMask != null
                    ? 1.0f
                    : 0.0f);
            SetFloatIfPresent(
                material,
                "_NormalScale" + suffix,
                terrainLayer.normalScale);
            SetFloatIfPresent(
                material,
                "_Metallic" + suffix,
                terrainLayer.metallic);
            SetFloatIfPresent(
                material,
                "_Smoothness" + suffix,
                terrainLayer.smoothness);
        }

        private void ReportPrimaryRuntime(
            TileMaterialRuntime runtime,
            TerrainData terrainData,
            TerrainLayer[] terrainLayers,
            Texture2D[] controlTextures,
            int tileX,
            int tileZ)
        {
            var firstLayer =
                terrainLayers[0];
            hasAdaptedMaterial =
                true;
            terrainLayerCount =
                terrainLayers.Length;
            adaptedLayerCount =
                terrainLayers.Length;
            controlTextureCount =
                controlTextures != null
                    ? controlTextures.Length
                    : 0;
            controlTexture =
                controlTextures != null &&
                controlTextures.Length > 0 &&
                controlTextures[0] != null
                    ? controlTextures[0]
                    : Texture2D.whiteTexture;
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
            usesDefinitionMaterial =
                runtime.UsesDefinitionMaterial;
            generatedMaterial =
                runtime.Material;
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

        private void RemoveUndesiredMaterials()
        {
            removalBuffer.Clear();

            foreach (var entry in
                tileMaterials)
            {
                if (!desiredMaterialTiles.Contains(
                        entry.Key))
                {
                    removalBuffer.Add(
                        entry.Key);
                }
            }

            for (var index = 0;
                index < removalBuffer.Count;
                index++)
            {
                RemoveGeneratedMaterial(
                    removalBuffer[index]);
            }
        }

        private void RemoveGeneratedMaterial(
            TileKey key)
        {
            if (!tileMaterials.TryGetValue(
                    key,
                    out var runtime))
            {
                return;
            }

            if (runtime.Material != null)
            {
                DestroyUnityObject(
                    runtime.Material);
            }

            tileMaterials.Remove(
                key);
        }

        private void ReleaseAllGeneratedMaterials()
        {
            removalBuffer.Clear();

            foreach (var key in
                tileMaterials.Keys)
            {
                removalBuffer.Add(
                    key);
            }

            for (var index = 0;
                index < removalBuffer.Count;
                index++)
            {
                RemoveGeneratedMaterial(
                    removalBuffer[index]);
            }

            desiredMaterialTiles.Clear();
            sourceTiles.Clear();
        }

        private void ClearRuntimeState()
        {
            sourceCurvedTileCount =
                default;
            adaptedCurvedTileCount =
                default;
            ClearPrimaryRuntimeState();
        }

        private void ClearPrimaryRuntimeState()
        {
            hasAdaptedMaterial =
                false;
            terrainLayerCount =
                default;
            adaptedLayerCount =
                default;
            controlTextureCount =
                default;
            controlTexture =
                null;
            activeTerrainLayer =
                null;
            diffuseTexture =
                null;
            normalTexture =
                null;
            maskTexture =
                null;
            layerTileSizeMeters =
                default;
            materialTextureScale =
                default;
            materialTextureOffset =
                default;
            usesDefinitionMaterial =
                false;
            generatedMaterial =
                null;
        }

        private void OnDisable()
        {
            ReleaseAllGeneratedMaterials();
            ClearRuntimeState();
        }

        private void OnDestroy()
        {
            ReleaseAllGeneratedMaterials();
        }

        private static float SafeTileSize(
            float value)
        {
            return
                IsFinite(
                    value) &&
                Mathf.Abs(
                    value) >
                    Mathf.Epsilon
                    ? Mathf.Abs(
                        value)
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
                !float.IsNaN(
                    value) &&
                !float.IsInfinity(
                    value);
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
                Destroy(
                    value);
            }
            else
            {
                DestroyImmediate(
                    value);
            }
        }
    }
}
