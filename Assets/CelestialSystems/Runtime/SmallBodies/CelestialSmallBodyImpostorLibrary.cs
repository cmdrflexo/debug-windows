/*
 * Pool-owned runtime atlas library for one small-body tool. A procedural
 * appearance can occupy several yaw-view tiles in the same shared atlases.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialSmallBodyImpostorLibrary : MonoBehaviour
    {
        [SerializeField] private string toolId;
        [SerializeField] private int variantCount;
        [SerializeField] private int variantResolution;
        [SerializeField] private int viewCount;
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

        private bool[] readyCaptures;
        private int[] readyCaptureCounts;
        private float[] variantBillboardSizes;

        public string ToolId => toolId;
        public int VariantCount => variantCount;
        public int VariantResolution => variantResolution;
        public int ViewCount => viewCount;
        public int CaptureCount => variantCount * viewCount;
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
            int newViewCount,
            CelestialSmallBodyImpostorMaps newRequestedMaps)
        {
            toolId = newToolId ?? string.Empty;
            variantCount = Mathf.Max(1, newVariantCount);
            variantResolution = Mathf.Clamp(newVariantResolution, 32, 2048);
            viewCount = Mathf.Clamp(newViewCount, 1, 16);
            requestedMaps = newRequestedMaps;
            var captureCount = CaptureCount;
            atlasColumnCount = Mathf.CeilToInt(Mathf.Sqrt(captureCount));
            atlasRowCount = Mathf.CeilToInt(captureCount / (float)atlasColumnCount);
            readyCaptures = new bool[captureCount];
            readyCaptureCounts = new int[variantCount];
            variantBillboardSizes = new float[variantCount];
            readyVariantCount = 0;
            AllocateRequestedAtlases();
            CreateImpostorMaterial();
            isConfigured = true;
        }

        public bool IsVariantReady(int variantIndex)
        {
            return readyCaptureCounts != null &&
                variantIndex >= 0 &&
                variantIndex < readyCaptureCounts.Length &&
                readyCaptureCounts[variantIndex] >= viewCount;
        }

        public bool IsCaptureReady(int variantIndex, int viewIndex)
        {
            var captureIndex = GetCaptureIndex(variantIndex, viewIndex);
            return readyCaptures != null &&
                captureIndex >= 0 &&
                captureIndex < readyCaptures.Length &&
                readyCaptures[captureIndex];
        }

        public void SetVariantBillboardSize(
            int variantIndex,
            float captureFrameSize)
        {
            if (variantBillboardSizes == null ||
                variantIndex < 0 ||
                variantIndex >= variantBillboardSizes.Length)
            {
                return;
            }

            // All yaw views are framed square. Retaining the largest frame
            // avoids a size pop if one asymmetric view has slightly wider
            // bounds than the others.
            variantBillboardSizes[variantIndex] = Mathf.Max(
                variantBillboardSizes[variantIndex],
                Mathf.Max(0.0001f, captureFrameSize));
        }

        public float GetVariantBillboardSize(
            int variantIndex)
        {
            return variantBillboardSizes != null &&
                variantIndex >= 0 &&
                variantIndex < variantBillboardSizes.Length
                ? variantBillboardSizes[variantIndex]
                : 0.0f;
        }

        public void MarkCaptureReady(int variantIndex, int viewIndex)
        {
            var captureIndex = GetCaptureIndex(variantIndex, viewIndex);

            if (readyCaptures == null || captureIndex < 0 ||
                captureIndex >= readyCaptures.Length ||
                readyCaptures[captureIndex])
            {
                return;
            }

            readyCaptures[captureIndex] = true;
            readyCaptureCounts[variantIndex]++;

            if (readyCaptureCounts[variantIndex] == viewCount)
            {
                readyVariantCount++;
            }
        }

        public int GetVariantIndex(uint seed)
        {
            return variantCount <= 0 ? 0 :
                (int)(Hash(seed) % (uint)variantCount);
        }

        public Vector4 GetVariantScaleOffset(int variantIndex, int viewIndex)
        {
            if (atlasColumnCount <= 0 || atlasRowCount <= 0)
            {
                return new Vector4(1f, 1f, 0f, 0f);
            }

            var captureIndex = GetCaptureIndex(variantIndex, viewIndex);
            if (captureIndex < 0)
            {
                return new Vector4(1f, 1f, 0f, 0f);
            }

            var column = captureIndex % atlasColumnCount;
            var row = captureIndex / atlasColumnCount;
            var scale = new Vector2(1f / atlasColumnCount, 1f / atlasRowCount);
            return new Vector4(scale.x, scale.y, column * scale.x, row * scale.y);
        }

        public bool TryCopyCapture(
            CelestialSmallBodyImpostorMaps map,
            RenderTexture source,
            int variantIndex,
            int viewIndex)
        {
            var target = GetAtlas(map);
            var captureIndex = GetCaptureIndex(variantIndex, viewIndex);

            if (target == null || source == null || captureIndex < 0)
            {
                return false;
            }

            var column = captureIndex % atlasColumnCount;
            var row = captureIndex / atlasColumnCount;
            Graphics.CopyTexture(source, 0, 0, 0, 0, variantResolution,
                variantResolution, target, 0, 0,
                column * variantResolution, row * variantResolution);
            return true;
        }

        private int GetCaptureIndex(int variantIndex, int viewIndex)
        {
            if (variantIndex < 0 || variantIndex >= variantCount ||
                viewIndex < 0 || viewIndex >= viewCount)
            {
                return -1;
            }

            return variantIndex * viewCount + viewIndex;
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

        private void AllocateAtlas(ref RenderTexture atlas,
            CelestialSmallBodyImpostorMaps map, string mapName,
            RenderTextureFormat format, int width, int height)
        {
            if ((requestedMaps & map) == 0)
            {
                ReleaseAtlas(ref atlas);
                return;
            }

            ReleaseAtlas(ref atlas);
            atlas = new RenderTexture(width, height, 0, format,
                RenderTextureReadWrite.Linear)
            {
                name = $"{toolId} Impostor {mapName} Atlas",
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
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
            if (atlas == null) return;
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
