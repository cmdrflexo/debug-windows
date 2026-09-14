/*
 * Reusable visual families for the close-range planetary-ring object system.
 * Ring definitions describe local material; this asset maps that material to
 * available meshes, materials, size ranges, and selection affinities.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    public enum CelestialRingObjectFamilyKind
    {
        FineIce = 0,
        IceChunk = 1,
        DarkRubble = 2,
        DustCluster = 3,
        LargeClump = 4
    }

    [Serializable]
    public sealed class CelestialRingObjectFamily
    {
        [SerializeField]
        private string displayName =
            "Ring Object";

        [SerializeField]
        private CelestialRingObjectFamilyKind kind;

        [Header("Variants")]
        [SerializeField]
        private Mesh[] meshVariants =
            Array.Empty<Mesh>();

        [SerializeField]
        private Material[] materialVariants =
            Array.Empty<Material>();

        [Header("Selection")]
        [SerializeField]
        [Min(0.0f)]
        private float baseSelectionWeight =
            1.0f;

        [SerializeField]
        [Min(0.0f)]
        private float iceAffinity =
            1.0f;

        [SerializeField]
        [Min(0.0f)]
        private float rockAffinity;

        [SerializeField]
        [Min(0.0f)]
        private float dustAffinity;

        [Header("Physical Scale")]
        [SerializeField]
        [Min(0.001f)]
        [Tooltip("Prototype visual scale until the particle-size model is calibrated against object families.")]
        private float minimumDiameterMeters =
            0.1f;

        [SerializeField]
        [Min(0.001f)]
        private float maximumDiameterMeters =
            1.0f;

        public string DisplayName =>
            string.IsNullOrWhiteSpace(
                displayName)
                    ? kind.ToString()
                    : displayName;

        public CelestialRingObjectFamilyKind Kind =>
            kind;

        public IReadOnlyList<Mesh> MeshVariants =>
            meshVariants;

        public IReadOnlyList<Material> MaterialVariants =>
            materialVariants;

        public float BaseSelectionWeight =>
            baseSelectionWeight;

        public float IceAffinity =>
            iceAffinity;

        public float RockAffinity =>
            rockAffinity;

        public float DustAffinity =>
            dustAffinity;

        public float MinimumDiameterMeters =>
            minimumDiameterMeters;

        public float MaximumDiameterMeters =>
            maximumDiameterMeters;

        public bool HasRenderableVariant =>
            meshVariants != null &&
            meshVariants.Length > 0;

        public float EvaluateSelectionWeight(
            float iceWeight,
            float rockWeight,
            float dustWeight)
        {
            var materialWeight =
                Mathf.Max(
                    0.0f,
                    iceWeight) *
                    iceAffinity +
                Mathf.Max(
                    0.0f,
                    rockWeight) *
                    rockAffinity +
                Mathf.Max(
                    0.0f,
                    dustWeight) *
                    dustAffinity;

            return Mathf.Max(
                0.0f,
                baseSelectionWeight) *
                materialWeight;
        }

        internal void Validate()
        {
            baseSelectionWeight =
                Mathf.Max(
                    0.0f,
                    baseSelectionWeight);
            iceAffinity =
                Mathf.Max(
                    0.0f,
                    iceAffinity);
            rockAffinity =
                Mathf.Max(
                    0.0f,
                    rockAffinity);
            dustAffinity =
                Mathf.Max(
                    0.0f,
                    dustAffinity);
            minimumDiameterMeters =
                Mathf.Max(
                    0.001f,
                    minimumDiameterMeters);
            maximumDiameterMeters =
                Mathf.Max(
                    minimumDiameterMeters,
                    maximumDiameterMeters);
        }
    }

    [CreateAssetMenu(
        fileName = "Ring Object Set",
        menuName = "Celestial Systems/Rings/Ring Object Set")]
    public sealed class CelestialRingObjectSet :
        ScriptableObject
    {
        [SerializeField]
        private CelestialRingObjectFamily[] families =
        {
            new CelestialRingObjectFamily(),
            new CelestialRingObjectFamily(),
            new CelestialRingObjectFamily(),
            new CelestialRingObjectFamily()
        };

        public IReadOnlyList<CelestialRingObjectFamily> Families =>
            families;

        public bool HasRenderableFamilies
        {
            get
            {
                if (families == null)
                {
                    return false;
                }

                for (var index = 0;
                    index < families.Length;
                    index++)
                {
                    if (families[index] != null &&
                        families[index].HasRenderableVariant)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void OnValidate()
        {
            if (families == null)
            {
                families =
                    Array.Empty<CelestialRingObjectFamily>();
                return;
            }

            for (var index = 0;
                index < families.Length;
                index++)
            {
                families[index]?.Validate();
            }
        }
    }
}
