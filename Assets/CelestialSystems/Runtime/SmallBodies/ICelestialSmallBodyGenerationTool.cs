/*
 * Shared contracts for asynchronously producing generic celestial small-body
 * instances. Implementations may prepare CPU data on worker threads, but must
 * invoke completion on Unity's main thread when returning a GameObject.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    public enum CelestialSmallBodyLod
    {
        Billboard = 0,
        Low = 1,
        Medium = 2,
        High = 3
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

        bool CanGenerate(
            CelestialSmallBodyRequest request);

        bool TryBeginGeneration(
            CelestialSmallBodyRequest request,
            Action<CelestialSmallBodyGenerationResult> completed);
    }
}
