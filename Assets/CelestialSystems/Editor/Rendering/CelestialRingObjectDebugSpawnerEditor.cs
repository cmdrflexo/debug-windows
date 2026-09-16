using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    [CustomEditor(typeof(CelestialRingObjectDebugSpawner))]
    public sealed class CelestialRingObjectDebugSpawnerEditor :
        UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Spawn Ring Object"))
                {
                    ((CelestialRingObjectDebugSpawner)target)
                        .SpawnRingObject();
                }

                if (GUILayout.Button("Clear Spawned Objects"))
                {
                    ((CelestialRingObjectDebugSpawner)target)
                        .ClearSpawnedObjects();
                }
            }
        }
    }
}
