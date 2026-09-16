/*
 * Connects an LOD4 billboard representation to the pool-owned impostor
 * library. Texture-channel assignment is intentionally centralized here so
 * tools never need to know which spawner or pool owns them.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialSmallBodyImpostorBinding :
        MonoBehaviour
    {
        [SerializeField]
        private CelestialSmallBodyImpostorLibrary library;

        [SerializeField]
        private int variantIndex;

        [SerializeField]
        private CelestialSmallBodyImpostorMaps requestedMaps;

        [SerializeField]
        private Vector4 atlasScaleOffset;

        private MaterialPropertyBlock propertyBlock;

        public CelestialSmallBodyImpostorLibrary Library =>
            library;

        public int VariantIndex =>
            variantIndex;

        public void Configure(
            CelestialSmallBodyImpostorLibrary newLibrary,
            uint seed)
        {
            library =
                newLibrary;

            if (library == null)
            {
                variantIndex = 0;
                requestedMaps =
                    CelestialSmallBodyImpostorMaps.None;
                atlasScaleOffset =
                    new Vector4(
                        1.0f,
                        1.0f,
                        0.0f,
                        0.0f);
                return;
            }

            variantIndex =
                library.GetVariantIndex(
                    seed);
            requestedMaps =
                library.RequestedMaps;
            atlasScaleOffset =
                library.GetVariantScaleOffset(
                    variantIndex);
            ApplyProperties();
        }

        private void ApplyProperties()
        {
            var renderer =
                GetComponent<MeshRenderer>();

            if (renderer == null)
            {
                return;
            }

            propertyBlock ??=
                new MaterialPropertyBlock();
            renderer.GetPropertyBlock(
                propertyBlock);
            propertyBlock.SetFloat(
                "_CelestialImpostorVariant",
                variantIndex);
            propertyBlock.SetVector(
                "_CelestialImpostorScaleOffset",
                atlasScaleOffset);
            propertyBlock.SetFloat(
                "_CelestialImpostorMaps",
                (float)requestedMaps);
            renderer.SetPropertyBlock(
                propertyBlock);
        }
    }
}
