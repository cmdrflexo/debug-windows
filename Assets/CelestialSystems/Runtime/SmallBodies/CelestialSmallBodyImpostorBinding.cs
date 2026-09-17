/*
 * Connects one LOD4 representation to a selected pool-owned impostor variant.
 * A per-representation material instance is used for atlas UVs; this keeps the
 * initial runtime implementation deterministic and easy to inspect.
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

        private Material materialInstance;
        private bool materialApplied;

        public CelestialSmallBodyImpostorLibrary Library =>
            library;

        public int VariantIndex =>
            variantIndex;

        public void Configure(
            CelestialSmallBodyImpostorLibrary newLibrary,
            uint seed)
        {
            library = newLibrary;

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
            materialApplied = false;
            TryApplyMaterial();
        }

        private void LateUpdate()
        {
            TryApplyMaterial();
        }

        private void OnDestroy()
        {
            if (materialInstance != null)
            {
                Destroy(
                    materialInstance);
            }
        }

        private void TryApplyMaterial()
        {
            if (materialApplied ||
                library == null ||
                !library.IsVariantReady(
                    variantIndex) ||
                library.ImpostorMaterial == null)
            {
                return;
            }

            var renderer =
                GetComponent<MeshRenderer>();

            if (renderer == null)
            {
                return;
            }

            if (materialInstance != null)
            {
                Destroy(
                    materialInstance);
            }

            materialInstance =
                new Material(
                    library.ImpostorMaterial)
                {
                    name =
                        $"{library.ToolId} Impostor Variant {variantIndex}"
                };
            materialInstance.SetVector(
                "_CelestialImpostorScaleOffset",
                atlasScaleOffset);
            materialInstance.SetTexture(
                "_CelestialImpostorAlbedoTransparencyAtlas",
                library.AlbedoTransparencyAtlas);
            materialInstance.SetTexture(
                "_CelestialImpostorNormalAtlas",
                library.NormalAtlas);
            materialInstance.SetTexture(
                "_CelestialImpostorEmissionAtlas",
                library.EmissionAtlas);
            materialInstance.SetTexture(
                "_CelestialImpostorMetallicSmoothnessAtlas",
                library.MetallicSmoothnessAtlas);
            renderer.sharedMaterial =
                materialInstance;
            materialApplied = true;
        }
    }
}
