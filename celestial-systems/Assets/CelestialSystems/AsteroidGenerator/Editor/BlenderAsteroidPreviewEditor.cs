using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    [CustomEditor(typeof(BlenderAsteroidPreview))]
    public sealed class BlenderAsteroidPreviewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var preview = (BlenderAsteroidPreview)target;
            EditorGUILayout.HelpBox("Asteroid defaults match the screenshot. Scale X/Y/Z are Blender axes (Z up). Detail applies twice: use Viewport 2–3 while tuning. Render 4 can produce over a million triangles.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Previous Seed")) { Undo.RecordObject(preview, "Previous asteroid seed"); preview.AdvanceSeed(-1); EditorUtility.SetDirty(preview); }
                if (GUILayout.Button("Regenerate")) { Undo.RecordObject(preview, "Regenerate asteroid"); preview.Rebuild(); EditorUtility.SetDirty(preview); }
                if (GUILayout.Button("Next Seed")) { Undo.RecordObject(preview, "Next asteroid seed"); preview.AdvanceSeed(1); EditorUtility.SetDirty(preview); }
            }
            if (GUILayout.Button("Reset to Asteroid Screenshot Settings"))
            { Undo.RecordObject(preview, "Reset asteroid settings"); preview.Settings = new BlenderAsteroidSettings(); preview.Rebuild(false); EditorUtility.SetDirty(preview); }
            if (GUILayout.Button("Create Batch (Viewport Detail)")) CreateBatch(preview);
            if (GUILayout.Button("Save Mesh Asset (Render Detail)")) SaveMesh(preview);
        }
        private static void SaveMesh(BlenderAsteroidPreview preview)
        {
            string path = EditorUtility.SaveFilePanelInProject("Save asteroid mesh", $"Asteroid_{preview.Seed}", "asset", "Choose a mesh asset location.");
            if (string.IsNullOrEmpty(path)) return;
            // Never overwrite an existing user asset without an explicit separate edit.
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var mesh = BlenderAsteroidGenerator.Create(preview.Settings, true);
            AssetDatabase.CreateAsset(mesh, path); AssetDatabase.SaveAssets(); EditorGUIUtility.PingObject(mesh);
        }
        private static void CreateBatch(BlenderAsteroidPreview source)
        {
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Create asteroid batch");
            var parent = new GameObject("Asteroid Batch"); Undo.RegisterCreatedObjectUndo(parent, "Create asteroid batch");
            parent.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            int count = Mathf.Clamp(source.NumberOfRocks, 1, 20), columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var go = new GameObject($"Asteroid {unchecked(source.Seed + (uint)i)}");
                    Undo.RegisterCreatedObjectUndo(go, "Create asteroid"); go.transform.SetParent(parent.transform, false);
                    go.transform.localPosition = new Vector3((i % columns) * Mathf.Max(0.01f, source.BatchSpacing), 0, (i / columns) * Mathf.Max(0.01f, source.BatchSpacing));
                    var p = go.AddComponent<BlenderAsteroidPreview>();
                    p.Settings = JsonUtility.FromJson<BlenderAsteroidSettings>(JsonUtility.ToJson(source.Settings));
                    p.Settings.Seed = unchecked(source.Seed + (uint)i); p.Material = source.Material;
                    // OnEnable schedules generation for the next editor update after settings are assigned.
                }
                Selection.activeGameObject = parent;
            }
            finally { Undo.CollapseUndoOperations(group); }
        }
    }
}
