using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    [CustomEditor(
        typeof(
            CelestialSmallBodyPoolManager))]
    public sealed class CelestialSmallBodyPoolManagerEditor :
        UnityEditor.Editor
    {
        private static readonly string[] HiddenProperties =
        {
            "m_Script",
            "lod0Readout",
            "lod1Readout",
            "lod2Readout",
            "lod3Readout",
            "lod4Readout"
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(
                serializedObject,
                HiddenProperties);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "LOD Prewarm Diagnostics",
                EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(
                true);
            DrawReadout(
                "LOD0",
                "lod0Readout");
            DrawReadout(
                "LOD1",
                "lod1Readout");
            DrawReadout(
                "LOD2",
                "lod2Readout");
            DrawReadout(
                "LOD3",
                "lod3Readout");
            DrawReadout(
                "LOD4",
                "lod4Readout");
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawReadout(
            string label,
            string propertyName)
        {
            var property =
                serializedObject.FindProperty(
                    propertyName);
            EditorGUILayout.TextField(
                label,
                property != null
                    ? property.stringValue
                    : "N/A");
        }
    }
}
