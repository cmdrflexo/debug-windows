using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jcan.CelestialSystems
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CelestialAsteroidPreview : MonoBehaviour
    {
        [SerializeField] private CelestialAsteroidSettings settings = new CelestialAsteroidSettings();
        [SerializeField] private Material material;
        [SerializeField, Range(1, 20)] private int numberOfAsteroids = 1;
        [SerializeField, Min(0.01f)] private float batchSpacing = 10f;
        [SerializeField] private bool autoUpdate;
        [SerializeField] private bool randomizeOnRegenerate;
        [SerializeField, HideInInspector] private Mesh previewMesh;

        private bool rebuildQueued;

        public CelestialAsteroidSettings Settings
        {
            get => settings;
            set => settings = value ?? new CelestialAsteroidSettings();
        }

        public Material Material
        {
            get => material;
            set => material = value;
        }

        public uint Seed => settings == null ? 0u : settings.Seed;
        public int NumberOfAsteroids => numberOfAsteroids;
        public float BatchSpacing => batchSpacing;

        private void OnEnable() => QueueRebuild();
        private void OnValidate()
        {
            if (autoUpdate) QueueRebuild();
        }
        private void OnDisable() => ReleasePreview();

        public void AdvanceSeed(int direction)
        {
            if (settings == null) settings = new CelestialAsteroidSettings();
            unchecked { settings.Seed = (uint)((int)settings.Seed + direction); }
            Rebuild();
        }

        [ContextMenu("Regenerate")]
        public void Regenerate()
        {
            if (settings == null) settings = new CelestialAsteroidSettings();
            if (randomizeOnRegenerate)
                settings.Seed = unchecked((uint)System.DateTime.UtcNow.Ticks);
            Rebuild(false);
        }

        public void Rebuild() => Rebuild(false);

        public void Rebuild(bool renderDetail)
        {
            rebuildQueued = false;
            var filter = GetComponent<MeshFilter>();
            if (filter == null) return;
            if (settings == null) settings = new CelestialAsteroidSettings();

            ReleasePreview();

            // A preview owns and destroys its mesh. Clone settings so its
            // regenerate cycle never writes into or destroys the runtime cache.
            var previewSettings = JsonUtility.FromJson<CelestialAsteroidSettings>(
                JsonUtility.ToJson(settings));
            previewSettings.UseRuntimeMeshCache = false;

            previewMesh = CelestialAsteroidGenerator.Create(previewSettings, renderDetail);
            previewMesh.name = $"Preview Celestial Asteroid {settings.Seed:X8}";
            previewMesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            filter.sharedMesh = previewMesh;

            var renderer = GetComponent<MeshRenderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
        }

        private void QueueRebuild()
        {
            if (rebuildQueued) return;
            rebuildQueued = true;

#if UNITY_EDITOR
            EditorApplication.delayCall += DelayedRebuild;
#else
            Rebuild();
#endif
        }

#if UNITY_EDITOR
        private void DelayedRebuild()
        {
            if (this == null || !isActiveAndEnabled) return;
            Rebuild();
        }
#endif

        private void ReleasePreview()
        {
            if (previewMesh == null) return;
            var mesh = previewMesh;
            previewMesh = null;

            var filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == mesh) filter.sharedMesh = null;

            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }
}
