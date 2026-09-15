/*
 * Edit-mode preview for CelestialRockMeshGenerator. Attach to an empty object,
 * adjust the settings, and inspect the generated mesh without entering Play
 * Mode. The mesh exists only as preview data unless explicitly saved later.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ProceduralRockPreview : MonoBehaviour
    {
        [SerializeField]
        private uint seed = 1u;

        [SerializeField]
        [Range(0, 4)]
        private int subdivisions = 2;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Diameter of the generated preview object, in metres.")]
        private float physicalSizeMeters = 10.0f;

        [SerializeField]
        private Vector3 axisScale = new Vector3(1.0f, 0.9f, 1.1f);

        [SerializeField]
        [Range(0.0f, 0.8f)]
        private float broadDeformation = 0.22f;

        [SerializeField]
        [Range(0.0f, 0.5f)]
        private float cavityStrength = 0.17f;

        [SerializeField]
        [Range(0.0f, 0.35f)]
        private float mediumBreakup = 0.06f;

        [SerializeField]
        private bool smoothNormals = true;

        [SerializeField]
        private Material material;

        [SerializeField, HideInInspector]
        private Mesh previewMesh;

        public uint Seed => seed;

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            subdivisions = Mathf.Clamp(subdivisions, 0, 4);
            physicalSizeMeters = Mathf.Max(0.01f, physicalSizeMeters);
            axisScale = new Vector3(
                Mathf.Max(0.05f, axisScale.x),
                Mathf.Max(0.05f, axisScale.y),
                Mathf.Max(0.05f, axisScale.z));
            Rebuild();
        }

        private void OnDisable()
        {
            ReleasePreviewMesh();
        }

        [ContextMenu("Regenerate")]
        public void Rebuild()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                return;
            }

            ReleasePreviewMesh();

            var settings = CelestialRockMeshSettings.Default;
            settings.Seed = seed;
            settings.Subdivisions = subdivisions;
            settings.AxisScale = axisScale * (physicalSizeMeters * 0.5f);
            settings.BroadDeformation = broadDeformation;
            settings.CavityStrength = cavityStrength;
            settings.MediumBreakup = mediumBreakup;
            settings.SmoothNormals = smoothNormals;

            previewMesh = CelestialRockMeshGenerator.Create(settings);
            previewMesh.name = $"Preview Rock {seed:X8}";
            previewMesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            meshFilter.sharedMesh = previewMesh;

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && material != null)
            {
                meshRenderer.sharedMaterial = material;
            }
        }

        public void AdvanceSeed(int amount)
        {
            unchecked
            {
                seed = (uint)((int)seed + amount);
            }

            Rebuild();
        }

        private void ReleasePreviewMesh()
        {
            if (previewMesh == null)
            {
                return;
            }

            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh == previewMesh)
            {
                meshFilter.sharedMesh = null;
            }

            // This method is called by OnValidate as well as normal editor
            // actions. Unity forbids DestroyImmediate from that callback.
            Destroy(previewMesh);
            previewMesh = null;
        }
    }
}
