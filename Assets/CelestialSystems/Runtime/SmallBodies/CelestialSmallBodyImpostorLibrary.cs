/*
 * Pool-owned runtime atlas library for one small-body tool. The manager fills
 * its variant slots; LOD4 representations only reference this shared data.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialSmallBodyImpostorLibrary :
        MonoBehaviour
    {
        [SerializeField] private string toolId;
        [SerializeField] private int variantCount;
        [SerializeField] private int variantResolution;
        [SerializeField] private CelestialSmallBodyImpostorMaps requestedMaps;
        [SerializeField] private int atlasColumnCount;
        [SerializeField] private int atlasRowCount;
        [SerializeField] private bool isConfigured;
        [SerializeField] private int readyVariantCount;
        [SerializeField] private RenderTexture albedoTransparencyAtlas;
        [SerializeField] private RenderTexture normalAtlas;
        [SerializeField] private RenderTexture emissionAtlas;
        [SerializeField] private RenderTexture metallicSmoothnessAtlas;
        [SerializeField] private Material impostorMaterial;

        private bool[] readyVariants;

        public string ToolId => toolId;
        public int VariantCount => variantCount;
        public int VariantResolution => variantResolution;
        public CelestialSmallBodyImpostorMaps RequestedMaps => requestedMaps;
        public bool IsConfigured => isConfigured;
        public int ReadyVariantCount => readyVariantCount;
        public RenderTexture AlbedoTransparencyAtlas => albedoTransparencyAtlas;
        public RenderTexture NormalAtlas => normalAtlas;
        public RenderTexture EmissionAtlas => emissionAtlas;
        public RenderTexture MetallicSmoothnessAtlas => metallicSmoothnessAtlas;
        public Material ImpostorMaterial => impostorMaterial;

        public void Configure(
            string newToolId,
            int newVariantCount,
            int newVariantResolution,
            CelestialSmallBodyImpostorMaps newRequestedMaps)
        {
            toolId = newToolId ?? string.Empty;
            variantCount = Mathf.Max(1, newVariantCount);
            variantResolution = Mathf.Clamp(newVariantResolution, 32, 2048);
            requestedMaps = newRequestedMaps;
            atlasColumnCount = Mathf.CeilToInt(Mathf.Sqrt(variantCount));
            atlasRowCount = Mathf.CeilToInt(variantCount / (float)atlasColumnCount);
            readyVariants = new bool[variantCount];
            readyVariantCount = 0;
            AllocateRequestedAtlases();
            CreateImpostorMaterial();
            isConfigured = true;
        }

        public bool IsVariantReady(int variantIndex)
        {
            return readyVariants != null &&
                variantIndex >= 0 &&
                variantIndex < readyVariants.Length &&
                readyVariants[variantIndex];
        }

        public void MarkVariantReady(int variantIndex)
        {
            if (readyVariants == null ||
                variantIndex < 0 ||
                variantIndex >= readyVariants.Length ||
                readyVariants[variantIndex])
            {
                return;
            }

            readyVariants[variantIndex] = true;
            readyVariantCount++;
        }

        public int GetVariantIndex(uint seed)
        {
            return variantCount <= 0 ? 0 : (int)(Hash(seed) % (uint)variantCount);
        }

        public Vector4 GetVariantScaleOffset(int variantIndex)
        {
            if (atlasColumnCount <= 0 || atlasRowCount <= 0)
            {
                return new Vector4(1.0f, 1.0f, 0.0f, 0.0f);
            }

            variantIndex = Mathf.Clamp(variantIndex, 0, Mathf.Max(0, variantCount - 1));
            var column = variantIndex % atlasColumnCount;
            var row = variantIndex / atlasColumnCount;
            var scale = new Vector2(1.0f / atlasColumnCount, 1.0f / atlasRowCount);
            return new Vector4(scale.x, scale.y, column * scale.x, row * scale.y);
        }

        public bool TryCopyCapture(
            CelestialSmallBodyImpostorMaps map,
            RenderTexture source,
            int variantIndex)
        {
            var target = GetAtlas(map);

            if (target == null || source == null ||
                variantIndex < 0 || variantIndex >= variantCount)
            {
                return false;
            }

            var column = variantIndex % atlasColumnCount;
            var row = variantIndex / atlasColumnCount;
            Graphics.CopyTexture(
                source,
                0,
                0,
                0,
                0,
                variantResolution,
                variantResolution,
                target,
                0,
                0,
                column * variantResolution,
                row * variantResolution);
            return true;
        }

        private void OnDestroy()
        {
            ReleaseAtlas(ref albedoTransparencyAtlas);
            ReleaseAtlas(ref normalAtlas);
            ReleaseAtlas(ref emissionAtlas);
            ReleaseAtlas(ref metallicSmoothnessAtlas);

            if (impostorMaterial != null)
            {
                Object.Destroy(impostorMaterial);
            }
        }

        private void AllocateRequestedAtlases()
        {
            var width = atlasColumnCount * variantResolution;
            var height = atlasRowCount * variantResolution;
            AllocateAtlas(ref albedoTransparencyAtlas, CelestialSmallBodyImpostorMaps.AlbedoTransparency, "Albedo Transparency", RenderTextureFormat.ARGBHalf, width, height);
            AllocateAtlas(ref normalAtlas, CelestialSmallBodyImpostorMaps.Normal, "Normal", RenderTextureFormat.ARGBHalf, width, height);
            AllocateAtlas(ref emissionAtlas, CelestialSmallBodyImpostorMaps.Emission, "Emission", RenderTextureFormat.ARGBHalf, width, height);
            AllocateAtlas(ref metallicSmoothnessAtlas, CelestialSmallBodyImpostorMaps.MetallicSmoothness, "Metallic Smoothness", RenderTextureFormat.ARGBHalf, width, height);
        }

        private void AllocateAtlas(
            ref RenderTexture atlas,
            CelestialSmallBodyImpostorMaps map,
            string mapName,
            RenderTextureFormat format,
            int width,
            int height)
        {
            if ((requestedMaps & map) == 0)
            {
                ReleaseAtlas(ref atlas);
                return;
            }

            // Atlas tiles are independently copied at mip 0. Generating atlas
            // mipmaps would leave stale levels after CopyTexture and would also
            // bleed neighbouring variants into each other at distance.
            if (atlas != null && atlas.width == width && atlas.height == height &&
                atlas.format == format && !atlas.useMipMap)
            {
                return;
            }

            ReleaseAtlas(ref atlas);
            atlas = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear)
            {
                name = $"{toolId} Impostor {mapName} Atlas",
                useMipMap = true,
                autoGenerateMips = true
            };
            atlas.Create();
        }

        private RenderTexture GetAtlas(CelestialSmallBodyImpostorMaps map)
        {
            return map switch
            {
                CelestialSmallBodyImpostorMaps.AlbedoTransparency => albedoTransparencyAtlas,
                CelestialSmallBodyImpostorMaps.Normal => normalAtlas,
                CelestialSmallBodyImpostorMaps.Emission => emissionAtlas,
                CelestialSmallBodyImpostorMaps.MetallicSmoothness => metallicSmoothnessAtlas,
                _ => null
            };
        }

        private void CreateImpostorMaterial()
        {
            if (impostorMaterial != null)
            {
                Object.Destroy(impostorMaterial);
            }

            var shader = Shader.Find("jcan/Celestial Small Body Impostor");

            if (shader == null)
            {
                return;
            }

            impostorMaterial = new Material(shader)
            {
                name = $"{toolId} Impostor Material"
            };
            impostorMaterial.SetTexture("_CelestialImpostorAlbedoTransparencyAtlas", albedoTransparencyAtlas);
            impostorMaterial.SetTexture("_CelestialImpostorNormalAtlas", normalAtlas);
            impostorMaterial.SetTexture("_CelestialImpostorEmissionAtlas", emissionAtlas);
            impostorMaterial.SetTexture("_CelestialImpostorMetallicSmoothnessAtlas", metallicSmoothnessAtlas);
        }

        private static void ReleaseAtlas(ref RenderTexture atlas)
        {
            if (atlas == null)
            {
                return;
            }

            atlas.Release();
            Object.Destroy(atlas);
            atlas = null;
        }

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }
}
