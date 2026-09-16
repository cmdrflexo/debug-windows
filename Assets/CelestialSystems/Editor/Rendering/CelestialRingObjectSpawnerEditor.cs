using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    [CustomEditor(typeof(CelestialRingObjectSpawner))]
    public sealed class CelestialRingObjectSpawnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Spawn Ring Object"))
                {
                    ((CelestialRingObjectSpawner)target)
                        .SpawnDebugRingObject();
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to spawn a debug ring object.",
                    MessageType.Info);
            }
        }
    }
}
