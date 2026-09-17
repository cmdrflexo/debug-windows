/*
 * Shared contracts for asynchronously producing generic celestial small-body
 * instances. Implementations may prepare CPU data on worker threads, but must
 * invoke completion on Unity's main thread when returning a GameObject.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    // Matches Unity LODGroup convention: LOD 0 is the highest detail and
    // LOD 4 is the billboard/impostor fallback.
    public enum CelestialSmallBodyLod
    {
        Lod0 = 0,
        Lod1 = 1,
        Lod2 = 2,
        Lod3 = 3,
        Lod4 = 4
    }

    [Flags]
    public enum CelestialSmallBodyImpostorMaps
    {
        None = 0,
        AlbedoTransparency = 1 << 0,
        Normal = 1 << 1,
        Emission = 1 << 2,
        MetallicSmoothness = 1 << 3
    }

    public readonly struct CelestialSmallBodyRequest
    {
        public string ToolId { get; }

        public uint Seed { get; }

        public bool HasExplicitSeed { get; }

        public CelestialSmallBodyLod DesiredLod { get; }

        public CelestialSmallBodyRequest(
            string toolId,
            uint seed,
            bool hasExplicitSeed,
            CelestialSmallBodyLod desiredLod)
        {
            ToolId = toolId ?? string.Empty;
            Seed = seed;
            HasExplicitSeed = hasExplicitSeed;
            DesiredLod = desiredLod;
        }
    }

    public readonly struct CelestialSmallBodyGenerationResult
    {
        public CelestialSmallBodyRequest Request { get; }

        public GameObject Instance { get; }

        public string Error { get; }

        public bool Succeeded =>
            Instance != null &&
            string.IsNullOrEmpty(Error);

        public CelestialSmallBodyGenerationResult(
            CelestialSmallBodyRequest request,
            GameObject instance,
            string error = null)
        {
            Request = request;
            Instance = instance;
            Error = error ?? string.Empty;
        }
    }

    public interface ICelestialSmallBodyGenerationTool
    {
        string ToolId { get; }

        int LodCount { get; }

        bool CanGenerate(
            CelestialSmallBodyRequest request);

        bool TryBeginGeneration(
            CelestialSmallBodyRequest request,
            Action<CelestialSmallBodyGenerationResult> completed);
    }

    // Optional capability. The pool manager owns the shared library while
    // the tool specifies how many visual variants and which PBR channels it
    // needs baked into that library.
    public interface ICelestialSmallBodyImpostorProvider
    {
        int ImpostorVariantCount { get; }

        int ImpostorResolution { get; }

        CelestialSmallBodyImpostorMaps RequestedImpostorMaps { get; }

        uint GetImpostorVariantSeed(
            int variantIndex);
    }
}
