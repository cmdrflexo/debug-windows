using UnityEngine;

namespace jcan.CelestialSystems
{
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BlenderAsteroidPreview : MonoBehaviour
    {
        public BlenderAsteroidSettings Settings = new BlenderAsteroidSettings();
        public Material Material;
        [Tooltip("Regenerate on settings changes. Leave off while adjusting several values.")]
        public bool AutoUpdate;
        [Tooltip("Choose a new seed on explicit Regenerate. Previous/Next Seed always use the specified seed.")]
        public bool RandomSeed;
        [Range(1, 20)] public int NumberOfRocks = 1;
        [Min(0.01f)] public float BatchSpacing = 6;
        private Mesh ownedMesh;
        private Mesh previousMesh;
        private bool hasPrevious;
        private bool pending;
        public uint Seed => Settings == null ? 0 : Settings.Seed;

        private void OnEnable() { pending = true; }
        // OnValidate may run off the main thread. Do not allocate meshes or destroy objects here.
        private void OnValidate() { if (AutoUpdate) pending = true; }
        private void Update() { if (pending) { pending = false; Rebuild(false); } }
        private void OnDisable() { pending = false; Release(); }
        [ContextMenu("Regenerate")]
        public void Rebuild() { Rebuild(true); }
        public void Rebuild(bool allowRandomSeed)
        {
            if (Settings == null) Settings = new BlenderAsteroidSettings();
            if (allowRandomSeed && RandomSeed) Settings.Seed = unchecked((uint)System.Guid.NewGuid().GetHashCode());
            // Build first so a failure leaves the last valid preview intact.
            Mesh replacement = BlenderAsteroidGenerator.Create(Settings);
            replacement.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            var filter = GetComponent<MeshFilter>();
            if (!hasPrevious) { previousMesh = filter.sharedMesh; hasPrevious = true; }
            Mesh old = ownedMesh; ownedMesh = replacement; filter.sharedMesh = replacement;
            if (Material != null) GetComponent<MeshRenderer>().sharedMaterial = Material;
            Dispose(old);
        }
        public void AdvanceSeed(int delta)
        { if (Settings == null) Settings = new BlenderAsteroidSettings(); unchecked { Settings.Seed += (uint)delta; } Rebuild(false); }
        private void Release()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == ownedMesh) filter.sharedMesh = previousMesh;
            Dispose(ownedMesh); ownedMesh = null; previousMesh = null; hasPrevious = false;
        }
        private static void Dispose(Mesh mesh)
        { if (mesh == null) return; if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
    }
}
