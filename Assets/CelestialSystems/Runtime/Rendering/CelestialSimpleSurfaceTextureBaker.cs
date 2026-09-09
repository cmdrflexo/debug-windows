/*
 * Bakes six coarse MapMagic root patches into fused simple-presentation control and ocean textures.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(390)]
    [DisallowMultipleComponent]
    public sealed class CelestialSimpleSurfaceTextureBaker :
        MonoBehaviour
    {
        private const int MaximumLayerCount = 4;
        private const string ShaderName =
            "jcan/Celestial Systems/Celestial Body Simple Fused";

        private static readonly string[] ControlTextureNames =
        {
            "_ControlPositiveX",
            "_ControlNegativeX",
            "_ControlPositiveY",
            "_ControlNegativeY",
            "_ControlPositiveZ",
            "_ControlNegativeZ"
        };

        private static readonly string[] OceanTextureNames =
        {
            "_OceanPositiveX",
            "_OceanNegativeX",
            "_OceanPositiveY",
            "_OceanNegativeY",
            "_OceanPositiveZ",
            "_OceanNegativeZ"
        };

        [Header("Runtime")]
        [SerializeField]
        private bool initialized;

        [SerializeField]
        private bool isReady;

        [SerializeField]
        private int bakedResolution;

        [SerializeField]
        private int bakedFaceCount;

        [SerializeField]
        private string lastError;

        private CelestialBodyRuntimeContext body;
        private CelestialSurfaceRuntime surfaceRuntime;
        private CelestialSurfacePatchGenerator patchGenerator;
        private Renderer targetRenderer;
        private Material runtimeMaterial;
        private Texture2D[] controlTextures;
        private Texture2D[] oceanTextures;

        public bool Initialized => initialized;
        public bool IsReady => isReady;
        public int BakedResolution => bakedResolution;
        public int BakedFaceCount => bakedFaceCount;
        public string LastError => lastError;

        public bool Initialize(
            CelestialBodyRuntimeContext newBody,
            CelestialSurfaceRuntime newSurfaceRuntime,
            CelestialSurfacePatchGenerator newPatchGenerator,
            Renderer newTargetRenderer)
        {
            ReleaseRuntimeResources();
            body = newBody;
            surfaceRuntime = newSurfaceRuntime;
            patchGenerator = newPatchGenerator;
            targetRenderer = newTargetRenderer;
            initialized = false;
            isReady = false;
            bakedResolution = 0;
            bakedFaceCount = 0;
            lastError = string.Empty;

            if (body == null ||
                body.Definition == null ||
                surfaceRuntime == null ||
                !surfaceRuntime.FoundationReady ||
                patchGenerator == null ||
                !patchGenerator.Initialized ||
                targetRenderer == null)
            {
                return Fail(
                    "The fused simple-surface baker requires an initialized body, surface runtime, patch generator, and renderer.");
            }

            initialized = true;
            return true;
        }

        private void Update()
        {
            if (!initialized ||
                isReady ||
                patchGenerator == null ||
                !patchGenerator.AreRootsReady())
            {
                return;
            }

            TryBake();
        }

        private void TryBake()
        {
            var patches =
                new CelestialSurfacePatchData[
                    surfaceRuntime.RootPatchCount];
            var minimumElevation =
                float.PositiveInfinity;

            for (var index = 0;
                index < patches.Length;
                index++)
            {
                if (!surfaceRuntime.TryGetRootPatch(
                        index,
                        out var address) ||
                    !patchGenerator.TryGetPatchData(
                        address,
                        out patches[index]))
                {
                    return;
                }

                if (index == 0)
                {
                    bakedResolution =
                        patches[index].Resolution;
                }
                else if (patches[index].Resolution !=
                    bakedResolution)
                {
                    Fail(
                        "Fused simple-surface root patches have inconsistent resolutions.");
                    return;
                }

                minimumElevation =
                    Mathf.Min(
                        minimumElevation,
                        patches[index]
                            .MinimumElevationMeters);
            }

            var shader =
                Shader.Find(
                    ShaderName);

            if (shader == null)
            {
                Fail(
                    $"The fused simple-surface shader '{ShaderName}' could not be found.");
                return;
            }

            var definition =
                body.Definition;
            var surfaceDefinition =
                definition.RoundMapMagicSurface;
            var appearance =
                surfaceDefinition != null
                    ? surfaceDefinition.SurfaceAppearance
                    : null;

            if (appearance == null ||
                !appearance.HasValidSettings)
            {
                Fail(
                    "The fused simple presentation requires a valid surface appearance.");
                return;
            }

            if (!TryCreateLayerMap(
                    appearance,
                    patches[0],
                    out var layerMap))
            {
                return;
            }

            runtimeMaterial =
                new Material(
                    shader)
                {
                    name =
                        $"{definition.DefinitionId} Simple Fused (Runtime)"
                };
            controlTextures =
                new Texture2D[
                    patches.Length];
            oceanTextures =
                new Texture2D[
                    patches.Length];

            for (var faceIndex = 0;
                faceIndex < patches.Length;
                faceIndex++)
            {
                BakeFace(
                    faceIndex,
                    patches[faceIndex],
                    appearance,
                    layerMap,
                    minimumElevation,
                    definition.OceanDefinition);
                bakedFaceCount++;
            }

            BindAppearance(
                appearance);
            BindOcean(
                definition.OceanDefinition);
            runtimeMaterial.SetFloat(
                "_SurfaceLightingMode",
                (float)appearance.LightingMode);
            runtimeMaterial.SetFloat(
                "_PlanetRadiusMeters",
                (float)definition.ReferenceRadiusMeters);
            targetRenderer.sharedMaterial =
                runtimeMaterial;
            isReady = true;
            lastError = string.Empty;
        }

        private bool TryCreateLayerMap(
            CelestialSurfaceAppearance appearance,
            CelestialSurfacePatchData patch,
            out int[] layerMap)
        {
            layerMap =
                new int[
                    patch.SurfaceLayerCount];

            if (!patch.HasSurfaceControlData)
            {
                return true;
            }

            for (var sourceIndex = 0;
                sourceIndex < layerMap.Length;
                sourceIndex++)
            {
                var terrainLayer =
                    patchGenerator.GetTerrainLayer(
                        sourceIndex);
                var appearanceIndex =
                    -1;

                for (var candidate = 0;
                    candidate < appearance.LayerCount;
                    candidate++)
                {
                    if (appearance.GetLayer(
                            candidate)
                            .MapMagicTerrainLayer ==
                        terrainLayer)
                    {
                        appearanceIndex =
                            candidate;
                        break;
                    }
                }

                if (appearanceIndex < 0 ||
                    appearanceIndex >=
                        MaximumLayerCount)
                {
                    Fail(
                        $"The generated TerrainLayer '{terrainLayer?.name}' has no matching fused surface-appearance layer.");
                    return false;
                }

                layerMap[sourceIndex] =
                    appearanceIndex;
            }

            return true;
        }

        private void BakeFace(
            int faceIndex,
            CelestialSurfacePatchData patch,
            CelestialSurfaceAppearance appearance,
            int[] layerMap,
            float minimumElevation,
            OceanDefinition ocean)
        {
            var sampleCount =
                patch.SampleCount;
            var controls =
                new Color[
                    sampleCount];
            var oceans =
                new Color[
                    sampleCount];
            var hasOcean =
                ocean != null &&
                ocean.HasValidSettings;
            var seaLevel =
                hasOcean
                    ? (float)ocean
                        .GlobalSurfaceElevationMeters
                    : float.NegativeInfinity;
            var oceanMaterial =
                hasOcean
                    ? ocean.Material
                    : null;
            var shallowColor =
                GetMaterialColor(
                    oceanMaterial,
                    "_ShallowColor",
                    new Color(
                        0.02f,
                        0.35f,
                        0.48f,
                        1.0f));
            var deepColor =
                GetMaterialColor(
                    oceanMaterial,
                    "_DeepColor",
                    new Color(
                        0.005f,
                        0.035f,
                        0.12f,
                        1.0f));
            var maximumDepth =
                Mathf.Max(
                    1.0f,
                    seaLevel -
                        minimumElevation);

            for (var y = 0;
                y < patch.Resolution;
                y++)
            {
                for (var x = 0;
                    x < patch.Resolution;
                    x++)
                {
                    var sampleIndex =
                        y *
                        patch.Resolution +
                        x;
                    var weights =
                        Vector4.zero;

                    if (patch.HasSurfaceControlData)
                    {
                        for (var sourceLayer = 0;
                            sourceLayer <
                                patch.SurfaceLayerCount;
                            sourceLayer++)
                        {
                            weights[
                                layerMap[sourceLayer]] +=
                                patch.GetSurfaceControlWeight(
                                    x,
                                    y,
                                    sourceLayer);
                        }
                    }
                    else
                    {
                        weights.x = 1.0f;
                    }

                    var totalWeight =
                        weights.x +
                        weights.y +
                        weights.z +
                        weights.w;

                    if (totalWeight > 0.000001f)
                    {
                        weights /=
                            totalWeight;
                    }
                    else
                    {
                        weights.x = 1.0f;
                    }

                    controls[sampleIndex] =
                        new Color(
                            weights.x,
                            weights.y,
                            weights.z,
                            weights.w);

                    var elevation =
                        patch.GetElevationMeters(
                            x,
                            y);
                    var oceanMask =
                        hasOcean &&
                        elevation <=
                            seaLevel
                            ? 1.0f
                            : 0.0f;
                    var depth =
                        oceanMask > 0.5f
                            ? Mathf.Clamp01(
                                (seaLevel -
                                    elevation) /
                                maximumDepth)
                            : 0.0f;
                    var oceanColor =
                        Color.Lerp(
                            shallowColor,
                            deepColor,
                            Mathf.Sqrt(
                                depth));
                    oceans[sampleIndex] =
                        new Color(
                            oceanColor.r,
                            oceanColor.g,
                            oceanColor.b,
                            oceanMask);
                }
            }

            controlTextures[faceIndex] =
                CreateTexture(
                    $"{body.Definition.DefinitionId} Simple Control {faceIndex}",
                    patch.Resolution,
                    controls,
                    true);
            oceanTextures[faceIndex] =
                CreateTexture(
                    $"{body.Definition.DefinitionId} Simple Ocean {faceIndex}",
                    patch.Resolution,
                    oceans,
                    false);
            runtimeMaterial.SetTexture(
                ControlTextureNames[faceIndex],
                controlTextures[faceIndex]);
            runtimeMaterial.SetTexture(
                OceanTextureNames[faceIndex],
                oceanTextures[faceIndex]);
        }

        private void BindAppearance(
            CelestialSurfaceAppearance appearance)
        {
            for (var index = 0;
                index < MaximumLayerCount;
                index++)
            {
                var layer =
                    index <
                        appearance.LayerCount
                        ? appearance.GetLayer(
                            index)
                        : null;
                runtimeMaterial.SetTexture(
                    $"_LayerMap{index}",
                    layer != null &&
                        layer.AlbedoTexture != null
                            ? layer.AlbedoTexture
                            : Texture2D.whiteTexture);
                runtimeMaterial.SetColor(
                    $"_LayerTint{index}",
                    layer != null
                        ? layer.Tint
                        : Color.white);
                runtimeMaterial.SetFloat(
                    $"_LayerScale{index}",
                    layer != null
                        ? layer.TextureScaleMeters
                        : 1.0f);
                runtimeMaterial.SetFloat(
                    $"_LayerMetallic{index}",
                    layer != null
                        ? layer.Metallic
                        : 0.0f);
                runtimeMaterial.SetFloat(
                    $"_LayerSmoothness{index}",
                    layer != null
                        ? layer.Smoothness
                        : 0.25f);
                runtimeMaterial.SetFloat(
                    $"_LayerOcclusion{index}",
                    layer != null
                        ? layer.OcclusionStrength
                        : 1.0f);
                runtimeMaterial.SetColor(
                    $"_LayerEmission{index}",
                    layer != null
                        ? layer.EmissionColor *
                            layer.EmissionIntensity
                        : Color.black);
            }
        }

        private void BindOcean(
            OceanDefinition ocean)
        {
            var material =
                ocean != null &&
                ocean.HasValidSettings
                    ? ocean.Material
                    : null;
            runtimeMaterial.SetFloat(
                "_OceanMetallic",
                GetMaterialFloat(
                    material,
                    "_Metallic",
                    0.0f));
            runtimeMaterial.SetFloat(
                "_OceanSmoothness",
                GetMaterialFloat(
                    material,
                    "_Smoothness",
                    0.92f));
            runtimeMaterial.SetColor(
                "_OceanFresnelColor",
                GetMaterialColor(
                    material,
                    "_FresnelColor",
                    new Color(
                        0.35f,
                        0.65f,
                        0.8f,
                        1.0f)));
            runtimeMaterial.SetFloat(
                "_OceanFresnelPower",
                GetMaterialFloat(
                    material,
                    "_FresnelPower",
                    5.0f));
            runtimeMaterial.SetFloat(
                "_OceanFresnelStrength",
                GetMaterialFloat(
                    material,
                    "_FresnelStrength",
                    0.5f));
        }

        private static Texture2D CreateTexture(
            string textureName,
            int resolution,
            Color[] pixels,
            bool linear)
        {
            var texture =
                new Texture2D(
                    resolution,
                    resolution,
                    TextureFormat.RGBA32,
                    true,
                    linear)
                {
                    name = textureName,
                    wrapMode =
                        TextureWrapMode.Clamp,
                    filterMode =
                        FilterMode.Trilinear,
                    anisoLevel = 1
                };
            texture.SetPixels(
                pixels);
            texture.Apply(
                updateMipmaps: true,
                makeNoLongerReadable: true);
            return texture;
        }

        private static Color GetMaterialColor(
            Material material,
            string propertyName,
            Color fallback)
        {
            return
                material != null &&
                material.HasProperty(
                    propertyName)
                    ? material.GetColor(
                        propertyName)
                    : fallback;
        }

        private static float GetMaterialFloat(
            Material material,
            string propertyName,
            float fallback)
        {
            return
                material != null &&
                material.HasProperty(
                    propertyName)
                    ? material.GetFloat(
                        propertyName)
                    : fallback;
        }

        private bool Fail(
            string error)
        {
            initialized = false;
            isReady = false;
            lastError = error;
            Debug.LogError(
                error,
                this);
            return false;
        }

        private void ReleaseRuntimeResources()
        {
            if (runtimeMaterial != null)
            {
                Destroy(
                    runtimeMaterial);
                runtimeMaterial = null;
            }

            DestroyTextures(
                controlTextures);
            DestroyTextures(
                oceanTextures);
            controlTextures = null;
            oceanTextures = null;
        }

        private static void DestroyTextures(
            Texture2D[] textures)
        {
            if (textures == null)
            {
                return;
            }

            for (var index = 0;
                index < textures.Length;
                index++)
            {
                if (textures[index] != null)
                {
                    Destroy(
                        textures[index]);
                }
            }
        }

        private void OnDestroy()
        {
            ReleaseRuntimeResources();
        }
    }
}
