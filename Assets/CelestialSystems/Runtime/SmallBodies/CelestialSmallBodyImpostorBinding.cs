/*
 * Binds a billboard representation to one procedural impostor appearance and
 * updates its atlas tile as the observer moves around the object.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialSmallBodyImpostorBinding : MonoBehaviour
    {
        [SerializeField] private CelestialSmallBodyImpostorLibrary library;
        [SerializeField] private int variantIndex;
        [SerializeField] private int activeViewIndex;
        [SerializeField] private CelestialSmallBodyImpostorMaps requestedMaps;
        [SerializeField] private Vector4 atlasScaleOffset;

        private Material materialInstance;
        private bool materialApplied;

        public CelestialSmallBodyImpostorLibrary Library => library;
        public int VariantIndex => variantIndex;
        public int ActiveViewIndex => activeViewIndex;

        public void Configure(CelestialSmallBodyImpostorLibrary newLibrary, uint seed)
        {
            library = newLibrary;

            if (library == null)
            {
                variantIndex = 0;
                activeViewIndex = 0;
                requestedMaps = CelestialSmallBodyImpostorMaps.None;
                atlasScaleOffset = new Vector4(1f, 1f, 0f, 0f);
                return;
            }

            variantIndex = library.GetVariantIndex(seed);
            activeViewIndex = 0;
            requestedMaps = library.RequestedMaps;
            atlasScaleOffset = library.GetVariantScaleOffset(variantIndex, activeViewIndex);
            materialApplied = false;
            TryApplyMaterial();
        }

        private void LateUpdate()
        {
            TryApplyMaterial();

            if (materialApplied)
            {
                UpdateViewTile();
            }
        }

        private void OnDestroy()
        {
            if (materialInstance != null)
            {
                Destroy(materialInstance);
            }
        }

        private void TryApplyMaterial()
        {
            if (materialApplied || library == null ||
                !library.IsVariantReady(variantIndex) ||
                library.ImpostorMaterial == null)
            {
                return;
            }

            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null) return;

            if (materialInstance != null)
            {
                Destroy(materialInstance);
            }

            materialInstance = new Material(library.ImpostorMaterial)
            {
                name = $"{library.ToolId} Impostor Variant {variantIndex}"
            };
            materialInstance.SetTexture("_CelestialImpostorAlbedoTransparencyAtlas", library.AlbedoTransparencyAtlas);
            materialInstance.SetTexture("_CelestialImpostorNormalAtlas", library.NormalAtlas);
            materialInstance.SetTexture("_CelestialImpostorEmissionAtlas", library.EmissionAtlas);
            materialInstance.SetTexture("_CelestialImpostorMetallicSmoothnessAtlas", library.MetallicSmoothnessAtlas);
            renderer.sharedMaterial = materialInstance;
            materialApplied = true;
            ApplyActiveTile();
        }

        private void UpdateViewTile()
        {
            if (library.ViewCount <= 1) return;

            var camera = Camera.main;
            if (camera == null) return;

            var towardCamera = camera.transform.position - transform.position;
            towardCamera.y = 0.0f;
            if (towardCamera.sqrMagnitude < 0.000001f) return;

            // Capture view zero looks at the body's -Z face. Quantizing the
            // horizontal camera direction makes the atlas select its nearest
            // yaw capture while CelestialSmallBodyBillboard faces the quad.
            var angle = Mathf.Atan2(towardCamera.x, -towardCamera.z);
            var normalizedAngle = Mathf.Repeat(angle, Mathf.PI * 2.0f);
            var selectedView = Mathf.RoundToInt(
                normalizedAngle / (Mathf.PI * 2.0f) * library.ViewCount) %
                library.ViewCount;

            if (selectedView == activeViewIndex) return;

            activeViewIndex = selectedView;
            ApplyActiveTile();
        }

        private void ApplyActiveTile()
        {
            if (materialInstance == null || library == null) return;

            atlasScaleOffset = library.GetVariantScaleOffset(variantIndex, activeViewIndex);
            materialInstance.SetVector("_CelestialImpostorScaleOffset", atlasScaleOffset);

            var captureFrameSize = library.GetVariantBillboardSize(variantIndex);
            if (captureFrameSize > 0.0f)
            {
                transform.localScale = Vector3.one * captureFrameSize;
            }
        }
    }
}
