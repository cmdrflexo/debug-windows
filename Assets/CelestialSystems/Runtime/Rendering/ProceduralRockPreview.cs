/*
 * Edit-mode preview for CelestialRockMeshGenerator. Attach to an empty object,
 * adjust the settings, and inspect the generated mesh without entering Play
 * Mode. The mesh exists only as preview data unless explicitly saved later.
 */

using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        [FormerlySerializedAs("cavityStrength")]
        [Range(0.0f, 0.5f)]
        [Tooltip("Strength of the broad Voronoi-like plates and their lower boundaries.")]
        private float cellularFacetStrength = 0.18f;

        [SerializeField]
        [Range(0.0f, 0.25f)]
        private float mediumBreakup = 0.035f;

        [SerializeField]
        private bool smoothNormals = true;

        [SerializeField]
        private Material material;

        [SerializeField, HideInInspector]
        private Mesh previewMesh;

        private bool isValidating;

        public uint Seed => seed;

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            isValidating = true;
            subdivisions = Mathf.Clamp(subdivisions, 0, 4);
            physicalSizeMeters = Mathf.Max(0.01f, physicalSizeMeters);
            axisScale = new Vector3(
                Mathf.Max(0.05f, axisScale.x),
                Mathf.Max(0.05f, axisScale.y),
                Mathf.Max(0.05f, axisScale.z));
            Rebuild();
            isValidating = false;
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
            settings.CellularFacetStrength = cellularFacetStrength;
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

            var meshToRelease = previewMesh;
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh == meshToRelease)
            {
                meshFilter.sharedMesh = null;
            }

            previewMesh = null;

            if (Application.isPlaying)
            {
                Destroy(meshToRelease);
                return;
            }

#if UNITY_EDITOR
            if (isValidating)
            {
                // DestroyImmediate is correct for editor-only temporary assets,
                // but Unity forbids it inside OnValidate itself.
                EditorApplication.delayCall += () =>
                {
                    if (meshToRelease != null)
                    {
                        DestroyImmediate(meshToRelease);
                    }
                };
                return;
            }
#endif

            DestroyImmediate(meshToRelease);
        }
    }
}
