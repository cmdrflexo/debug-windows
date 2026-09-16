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
            "toolPools",
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

            DrawToolPools();

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

        private void DrawToolPools()
        {
            var pools =
                serializedObject.FindProperty(
                    "toolPools");

            if (pools == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Tool Pools",
                EditorStyles.boldLabel);

            for (var index = 0;
                index < pools.arraySize;
                index++)
            {
                var pool =
                    pools.GetArrayElementAtIndex(
                        index);
                var toolSource =
                    pool.FindPropertyRelative(
                        "toolSource");
                var targetReadyCount =
                    pool.FindPropertyRelative(
                        "targetReadyCount");
                var minimumPrewarmLod =
                    pool.FindPropertyRelative(
                        "minimumPrewarmLod");
                var maximumPrewarmLod =
                    pool.FindPropertyRelative(
                        "maximumPrewarmLod");
                var regenerateAfterCheckout =
                    pool.FindPropertyRelative(
                        "regenerateAfterCheckout");

                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    $"Pool {index + 1}",
                    EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    toolSource,
                    new GUIContent(
                        "Tool Source"));
                EditorGUILayout.PropertyField(
                    targetReadyCount,
                    new GUIContent(
                        "Target Ready Count"));

                DrawPrewarmLodFields(
                    minimumPrewarmLod,
                    maximumPrewarmLod);

                EditorGUILayout.PropertyField(
                    regenerateAfterCheckout,
                    new GUIContent(
                        "Regenerate After Checkout"));

                if (GUILayout.Button(
                        "Remove Pool"))
                {
                    pools.DeleteArrayElementAtIndex(
                        index);
                    break;
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button(
                    "Add Tool Pool"))
            {
                pools.InsertArrayElementAtIndex(
                    pools.arraySize);
            }
        }

        private static void DrawPrewarmLodFields(
            SerializedProperty lowestMeshLod,
            SerializedProperty highestMeshLod)
        {
            var options =
                new[]
                {
                    "LOD0 (highest)",
                    "LOD1",
                    "LOD2",
                    "LOD3 (lowest mesh)"
                };
            var lowest =
                Mathf.Clamp(
                    lowestMeshLod.enumValueIndex,
                    0,
                    3);
            var highest =
                Mathf.Clamp(
                    highestMeshLod.enumValueIndex,
                    0,
                    3);

            lowest = EditorGUILayout.Popup(
                "Lowest Mesh LOD (guaranteed)",
                lowest,
                options);
            highest = EditorGUILayout.Popup(
                "Highest Mesh LOD (idle)",
                highest,
                options);

            // Unity LOD numbering runs from highest detail (0) to lowest
            // detail (3 here). The idle ceiling cannot be lower detail than
            // the guaranteed level.
            highest =
                Mathf.Min(
                    highest,
                    lowest);
            lowestMeshLod.enumValueIndex =
                lowest;
            highestMeshLod.enumValueIndex =
                highest;
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
