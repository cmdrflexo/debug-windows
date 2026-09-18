using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    [CustomEditor(typeof(ProceduralRockPreview))]
    public sealed class ProceduralRockPreviewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var preview = (ProceduralRockPreview)target;
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Previous Seed"))
                {
                    preview.AdvanceSeed(-1);
                }

                if (GUILayout.Button("Regenerate"))
                {
                    preview.Rebuild();
                }

                if (GUILayout.Button("Next Seed"))
                {
                    preview.AdvanceSeed(1);
                }
            }
        }
    }
}
