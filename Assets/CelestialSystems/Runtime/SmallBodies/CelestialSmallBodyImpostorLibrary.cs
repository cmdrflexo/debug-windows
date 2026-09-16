/*
 * Pool-owned description and runtime texture allocation for the shared
 * impostor variants of one small-body generation tool.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialSmallBodyImpostorLibrary :
        MonoBehaviour
    {
        [SerializeField]
        private string toolId;

        [SerializeField]
        private int variantCount;

        [SerializeField]
        private int variantResolution;

        [SerializeField]
        private CelestialSmallBodyImpostorMaps requestedMaps;

        [SerializeField]
        private int atlasColumnCount;

        [SerializeField]
        private int atlasRowCount;

        [SerializeField]
        private bool isConfigured;

        [SerializeField]
        private RenderTexture albedoTransparencyAtlas;

        [SerializeField]
        private RenderTexture normalAtlas;

        [SerializeField]
        private RenderTexture emissionAtlas;

        [SerializeField]
        private RenderTexture metallicSmoothnessAtlas;

        public string ToolId =>
            toolId;

        public int VariantCount =>
            variantCount;

        public int VariantResolution =>
            variantResolution;

        public CelestialSmallBodyImpostorMaps RequestedMaps =>
            requestedMaps;

        public bool IsConfigured =>
            isConfigured;

        public RenderTexture AlbedoTransparencyAtlas =>
            albedoTransparencyAtlas;

        public RenderTexture NormalAtlas =>
            normalAtlas;

        public RenderTexture EmissionAtlas =>
            emissionAtlas;

        public RenderTexture MetallicSmoothnessAtlas =>
            metallicSmoothnessAtlas;

        public void Configure(
            string newToolId,
            int newVariantCount,
            int newVariantResolution,
            CelestialSmallBodyImpostorMaps newRequestedMaps)
        {
            toolId =
                newToolId ?? string.Empty;
            variantCount =
                Mathf.Max(
                    1,
                    newVariantCount);
            variantResolution =
                Mathf.Clamp(
                    newVariantResolution,
                    32,
                    2048);
            requestedMaps =
                newRequestedMaps;
            atlasColumnCount =
                Mathf.CeilToInt(
                    Mathf.Sqrt(
                        variantCount));
            atlasRowCount =
                Mathf.CeilToInt(
                    variantCount /
                    (float)atlasColumnCount);
            AllocateRequestedAtlases();
            isConfigured = true;
        }

        private void OnDestroy()
        {
            ReleaseAtlas(
                ref albedoTransparencyAtlas);
            ReleaseAtlas(
                ref normalAtlas);
            ReleaseAtlas(
                ref emissionAtlas);
            ReleaseAtlas(
                ref metallicSmoothnessAtlas);
        }

        private void AllocateRequestedAtlases()
        {
            var atlasWidth =
                atlasColumnCount *
                variantResolution;
            var atlasHeight =
                atlasRowCount *
                variantResolution;

            AllocateAtlas(
                ref albedoTransparencyAtlas,
                CelestialSmallBodyImpostorMaps
                    .AlbedoTransparency,
                "Albedo Transparency",
                RenderTextureFormat.ARGB32,
                atlasWidth,
                atlasHeight);
            AllocateAtlas(
                ref normalAtlas,
                CelestialSmallBodyImpostorMaps.Normal,
                "Normal",
                RenderTextureFormat.ARGB32,
                atlasWidth,
                atlasHeight);
            AllocateAtlas(
                ref emissionAtlas,
                CelestialSmallBodyImpostorMaps.Emission,
                "Emission",
                RenderTextureFormat.ARGBHalf,
                atlasWidth,
                atlasHeight);
            AllocateAtlas(
                ref metallicSmoothnessAtlas,
                CelestialSmallBodyImpostorMaps
                    .MetallicSmoothness,
                "Metallic Smoothness",
                RenderTextureFormat.ARGB32,
                atlasWidth,
                atlasHeight);
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
                ReleaseAtlas(
                    ref atlas);
                return;
            }

            if (atlas != null &&
                atlas.width == width &&
                atlas.height == height &&
                atlas.format == format)
            {
                return;
            }

            ReleaseAtlas(
                ref atlas);
            atlas =
                new RenderTexture(
                    width,
                    height,
                    0,
                    format,
                    RenderTextureReadWrite.Linear)
                {
                    name =
                        $"{toolId} Impostor {mapName} Atlas",
                    useMipMap = true,
                    autoGenerateMips = true
                };
            atlas.Create();
        }

        private static void ReleaseAtlas(
            ref RenderTexture atlas)
        {
            if (atlas == null)
            {
                return;
            }

            atlas.Release();

            if (Application.isPlaying)
            {
                Destroy(
                    atlas);
            }
            else
            {
                DestroyImmediate(
                    atlas);
            }

            atlas = null;
        }

        public int GetVariantIndex(
            uint seed)
        {
            if (variantCount <= 0)
            {
                return 0;
            }

            return
                (int)(Hash(
                    seed) %
                    (uint)variantCount);
        }

        public Vector4 GetVariantScaleOffset(
            int variantIndex)
        {
            if (atlasColumnCount <= 0 ||
                atlasRowCount <= 0)
            {
                return new Vector4(
                    1.0f,
                    1.0f,
                    0.0f,
                    0.0f);
            }

            variantIndex =
                Mathf.Clamp(
                    variantIndex,
                    0,
                    Mathf.Max(
                        0,
                        variantCount - 1));
            var column =
                variantIndex %
                atlasColumnCount;
            var row =
                variantIndex /
                atlasColumnCount;
            var scale = new Vector2(
                1.0f / atlasColumnCount,
                1.0f / atlasRowCount);
            return new Vector4(
                scale.x,
                scale.y,
                column * scale.x,
                row * scale.y);
        }

        private static uint Hash(
            uint value)
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
