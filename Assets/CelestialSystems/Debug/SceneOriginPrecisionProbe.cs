using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jcan.CelestialSystems
{
    /// <summary>
    /// Measures the float transform round trip from scene zero through this object.
    /// This is a precision probe, not an independent universe-position reference.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Celestial Systems/Debug/Scene Origin Precision Probe")]
    public sealed class SceneOriginPrecisionProbe : MonoBehaviour
    {
        [SerializeField] private Color markerColor = Color.cyan;
        [SerializeField, Min(0.01f)] private float crosshairSizeMeters = 2f;
        [SerializeField] private bool showLabel = true;
        [SerializeField] private bool showTrueOrigin = true;
        [SerializeField, Min(1f)] private float errorMagnification = 1f;

        [Header("Measured Each Frame")]
        [SerializeField] private Vector3 originInObjectSpace;
        [SerializeField] private Vector3 reconstructedSceneOrigin;
        [SerializeField] private float errorMeters;
        [SerializeField] private float peakErrorMeters;
        [SerializeField] private Vector3 objectWorldPosition;

        private void OnEnable()
        {
            peakErrorMeters = 0f;
            Measure();
        }

        private void LateUpdate()
        {
            Measure();
        }

        private void Measure()
        {
            originInObjectSpace = transform.InverseTransformPoint(Vector3.zero);
            reconstructedSceneOrigin = transform.TransformPoint(originInObjectSpace);
            objectWorldPosition = transform.position;
            errorMeters = reconstructedSceneOrigin.magnitude;
            if (IsFinite(reconstructedSceneOrigin))
                peakErrorMeters = Mathf.Max(peakErrorMeters, errorMeters);
        }

        [ContextMenu("Reset Peak Error")]
        private void ResetPeakError()
        {
            peakErrorMeters = 0f;
            Measure();
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private void OnDrawGizmos()
        {
            if (!enabled) return;
            // Sample at draw time as well, after scene transforms have updated.
            Measure();
            var oldMatrix = Gizmos.matrix;
            var oldColor = Gizmos.color;
            try
            {
                // Do not accidentally apply this object's transform a second time.
                Gizmos.matrix = Matrix4x4.identity;
                float size = Mathf.Max(0.01f, crosshairSizeMeters);
                if (showTrueOrigin)
                {
                    Gizmos.color = Color.white;
                    DrawCross(Vector3.zero, size * 0.5f);
                }

                if (!IsFinite(reconstructedSceneOrigin)) return;
                var displayedOrigin = reconstructedSceneOrigin *
                    Mathf.Max(1f, errorMagnification);
                Gizmos.color = markerColor;
                DrawCross(displayedOrigin, size);
                Gizmos.DrawLine(Vector3.zero, displayedOrigin);
#if UNITY_EDITOR
                if (showLabel)
                {
                    var style = new GUIStyle(EditorStyles.miniLabel);
                    style.normal.textColor = markerColor;
                    Handles.Label(displayedOrigin + Vector3.up * size,
                        name + " [" + GetInstanceID() + "]"
                        + "\nError: " + errorMeters.ToString("G6") + " m"
                        + " | Peak: " + peakErrorMeters.ToString("G6") + " m"
                        + "\nXYZ: " + reconstructedSceneOrigin.ToString("G9")
                        + (errorMagnification > 1f
                            ? "\nDisplay magnification: " + errorMagnification + "x"
                            : ""), style);
                }
#endif
            }
            finally
            {
                Gizmos.matrix = oldMatrix;
                Gizmos.color = oldColor;
            }
        }

        private static void DrawCross(Vector3 center, float size)
        {
            Gizmos.DrawLine(center - Vector3.right * size, center + Vector3.right * size);
            Gizmos.DrawLine(center - Vector3.up * size, center + Vector3.up * size);
            Gizmos.DrawLine(center - Vector3.forward * size, center + Vector3.forward * size);
        }
    }
}
