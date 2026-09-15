/*
 * Stable visual-source contract for any ring body consumer. Spawners receive a
 * resolved mesh/material/prefab description and do not need to know whether it
 * originated from authored variants, procedural generation, or a future cache.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [Serializable]
    public struct CelestialRingBodyRequest
    {
        public uint Seed;
        public CelestialRingObjectFamilyKind FamilyKind;
        public float NominalDiameterMeters;
    }

    [Serializable]
    public struct CelestialRingResolvedBody
    {
        public GameObject Prefab;
        public Mesh Mesh;
        public Material Material;
        [Min(0.001f)] public float DiameterMultiplier;

        public bool HasVisual => Prefab != null || Mesh != null;
        public float SafeDiameterMultiplier => Mathf.Max(0.001f, DiameterMultiplier);
    }

    public abstract class CelestialRingBodySource : ScriptableObject
    {
        public abstract bool TryResolve(
            CelestialRingBodyRequest request,
            out CelestialRingResolvedBody body);
    }

    [Serializable]
    public sealed class CelestialRingMeshBodyVariant
    {
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material material;
        [SerializeField, Min(0f)] private float weight = 1f;
        [SerializeField, Min(0.001f)] private float diameterMultiplier = 1f;

        public Mesh Mesh => mesh;
        public Material Material => material;
        public float Weight => Mathf.Max(0f, weight);
        public float DiameterMultiplier => Mathf.Max(0.001f, diameterMultiplier);
    }

    [CreateAssetMenu(
        fileName = "Ring Mesh Body Source",
        menuName = "Celestial Systems/Rings/Body Sources/Mesh Variants")]
    public sealed class CelestialRingMeshBodySource : CelestialRingBodySource
    {
        [SerializeField] private CelestialRingMeshBodyVariant[] variants =
            Array.Empty<CelestialRingMeshBodyVariant>();

        public override bool TryResolve(
            CelestialRingBodyRequest request,
            out CelestialRingResolvedBody body)
        {
            body = default;
            if (variants == null || variants.Length == 0) return false;

            var totalWeight = 0f;
            for (var index = 0; index < variants.Length; index++)
            {
                var variant = variants[index];
                if (variant != null && variant.Mesh != null)
                    totalWeight += variant.Weight;
            }

            if (totalWeight <= 0.000001f) return false;

            var threshold = HashToUnit(request.Seed) * totalWeight;
            var accumulated = 0f;
            for (var index = 0; index < variants.Length; index++)
            {
                var variant = variants[index];
                if (variant == null || variant.Mesh == null) continue;

                accumulated += variant.Weight;
                if (threshold > accumulated) continue;

                body = new CelestialRingResolvedBody
                {
                    Mesh = variant.Mesh,
                    Material = variant.Material,
                    DiameterMultiplier = variant.DiameterMultiplier
                };
                return true;
            }

            return false;
        }

        private static float HashToUnit(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777216f;
        }
    }
}
