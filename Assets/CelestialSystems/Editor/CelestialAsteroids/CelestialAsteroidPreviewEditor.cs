using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    [CustomEditor(typeof(CelestialAsteroidPreview))]
    public sealed class CelestialAsteroidPreviewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var preview = (CelestialAsteroidPreview)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Detail 3 is the intended close-range setting. Low Cost Topology, Fast Cellular Noise, disabled UVs/tangents, and disabled medium/fine layers are independent runtime-cost controls.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Previous Seed"))
                {
                    Undo.RecordObject(preview, "Previous celestial asteroid seed");
                    preview.AdvanceSeed(-1);
                    EditorUtility.SetDirty(preview);
                }

                if (GUILayout.Button("Regenerate"))
                {
                    preview.Rebuild();
                }

                if (GUILayout.Button("Next Seed"))
                {
                    Undo.RecordObject(preview, "Next celestial asteroid seed");
                    preview.AdvanceSeed(1);
                    EditorUtility.SetDirty(preview);
                }
            }

            if (GUILayout.Button("Create Batch (Viewport Detail)"))
            {
                CreateBatch(preview);
            }

            if (GUILayout.Button("Save Mesh Asset (Render Detail)"))
            {
                SaveMesh(preview);
            }
        }

        private static void CreateBatch(CelestialAsteroidPreview source)
        {
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create celestial asteroid batch");

            try
            {
                var parent = new GameObject("Celestial Asteroid Batch");
                Undo.RegisterCreatedObjectUndo(parent, "Create celestial asteroid batch");
                parent.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);

                var count = source.NumberOfAsteroids;
                var columns = Mathf.CeilToInt(Mathf.Sqrt(count));
                for (var index = 0; index < count; index++)
                {
                    var item = new GameObject($"Celestial Asteroid {source.Seed + (uint)index}");
                    Undo.RegisterCreatedObjectUndo(item, "Create celestial asteroid");
                    item.transform.SetParent(parent.transform, false);
                    item.transform.localPosition = new Vector3(
                        index % columns * source.BatchSpacing,
                        0f,
                        index / columns * source.BatchSpacing);

                    var preview = item.AddComponent<CelestialAsteroidPreview>();
                    preview.Settings = JsonUtility.FromJson<CelestialAsteroidSettings>(
                        JsonUtility.ToJson(source.Settings));
                    preview.Settings.Seed = source.Seed + (uint)index;
                    preview.Material = source.Material;
                }

                Selection.activeGameObject = parent;
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }
        }

        private static void SaveMesh(CelestialAsteroidPreview preview)
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Save celestial asteroid mesh",
                $"CelestialAsteroid_{preview.Seed}",
                "asset",
                "Choose a mesh asset location.");
            if (string.IsNullOrEmpty(path)) return;

            var mesh = CelestialAsteroidGenerator.Create(preview.Settings, true);
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(mesh);
        }
    }
}
