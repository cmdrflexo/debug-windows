/*
 * Ring body source backed by the Blender-compatible asteroid generator. The
 * source owns template settings and a bounded deterministic mesh cache; ring
 * spawners only receive a resolved body descriptor.
 */

using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [CreateAssetMenu(
        fileName = "Ring Asteroid Body Source",
        menuName = "Celestial Systems/Rings/Body Sources/Procedural Asteroids")]
    public sealed class CelestialRingAsteroidBodySource : CelestialRingBodySource
    {
        [SerializeField]
        private BlenderAsteroidSettings asteroidSettings =
            new BlenderAsteroidSettings();

        [SerializeField]
        [Tooltip("Use the requesting cell/body seed to select a deterministic variant.")]
        private bool useRequestSeed = true;

        [SerializeField]
        private uint seedSalt = 0xC31E57u;

        [SerializeField]
        [Range(1, 64)]
        [Tooltip("Limits the number of generated meshes retained by folding request seeds into this many variants.")]
        private int variantCount = 16;

        [SerializeField]
        [Tooltip("Use the template Viewport Detail. Enable only for a one-off hero/moonlet source.")]
        private bool useRenderDetail;

        [SerializeField]
        [Range(1, 4)]
        [Tooltip("Zero keeps the template detail. Otherwise overrides both preview and render detail.")]
        private int detailOverride;

        [SerializeField]
        private bool overrideGenerateUVs;

        [SerializeField]
        private bool generateUVs = true;

        [SerializeField]
        private bool overrideSmoothNormals;

        [SerializeField]
        private bool smoothNormals = true;

        [SerializeField]
        private Material defaultMaterial;

        private readonly Dictionary<int, Mesh> cachedVariants =
            new Dictionary<int, Mesh>();

        public override bool TryResolve(
            CelestialRingBodyRequest request,
            out CelestialRingResolvedBody body)
        {
            body = default;
            var variant = ResolveVariant(request.Seed);
            if (!cachedVariants.TryGetValue(variant, out var mesh) ||
                mesh == null)
            {
                var settings = CloneSettings();
                settings.Seed = useRequestSeed
                    ? MixSeed(request.Seed, seedSalt) + (uint)variant
                    : settings.Seed + (uint)variant;

                if (detailOverride > 0)
                {
                    settings.ViewportDetail = detailOverride;
                    settings.RenderDetail = detailOverride;
                }

                if (overrideGenerateUVs)
                    settings.GenerateUVs = generateUVs;
                if (overrideSmoothNormals)
                    settings.SmoothNormals = smoothNormals;

                mesh = BlenderAsteroidGenerator.Create(settings, useRenderDetail);
                mesh.name = $"Ring Asteroid Variant {variant}";
                cachedVariants[variant] = mesh;
            }

            var maximumExtent = Mathf.Max(
                mesh.bounds.size.x,
                Mathf.Max(mesh.bounds.size.y, mesh.bounds.size.z));
            body = new CelestialRingResolvedBody
            {
                Mesh = mesh,
                Material = defaultMaterial,
                DiameterMultiplier = 1.0f / Mathf.Max(0.001f, maximumExtent)
            };
            return true;
        }

        private void OnDisable()
        {
            ClearGeneratedMeshCache();
        }

        [ContextMenu("Clear Generated Mesh Cache")]
        public void ClearGeneratedMeshCache()
        {
            foreach (var mesh in cachedVariants.Values)
            {
                if (mesh == null) continue;

                if (Application.isPlaying)
                {
                    Destroy(mesh);
                }
                else
                {
                    DestroyImmediate(mesh);
                }
            }

            cachedVariants.Clear();
        }

        private int ResolveVariant(uint requestSeed)
        {
            var count = Mathf.Max(1, variantCount);
            var value = useRequestSeed
                ? MixSeed(requestSeed, seedSalt)
                : seedSalt;
            return (int)(value % (uint)count);
        }

        private BlenderAsteroidSettings CloneSettings()
        {
            if (asteroidSettings == null)
                asteroidSettings = new BlenderAsteroidSettings();

            return JsonUtility.FromJson<BlenderAsteroidSettings>(
                JsonUtility.ToJson(asteroidSettings));
        }

        private static uint MixSeed(uint value, uint salt)
        {
            value ^= salt + 0x9E3779B9u + (value << 6) + (value >> 2);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private void OnValidate()
        {
            variantCount = Mathf.Clamp(variantCount, 1, 64);
            detailOverride = Mathf.Clamp(detailOverride, 0, 4);
        }
    }
}
