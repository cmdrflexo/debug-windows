/* Debug gizmo attached to streamed ring objects. */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialRingObjectDebugGizmo : MonoBehaviour
    {
        private bool drawGizmo;
        private Color gizmoColor = Color.cyan;

        public void Configure(bool shouldDraw, Color color)
        {
            drawGizmo = shouldDraw;
            gizmoColor = color;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmo) return;

            var previousColor = Gizmos.color;
            var previousMatrix = Gizmos.matrix;
            Gizmos.color = gizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;

            // Ring visuals are scaled to their requested diameter, so the
            // primitive's unit sphere represents the object footprint.
            Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
            Gizmos.DrawLine(
                Vector3.zero,
                Vector3.forward * 0.7f);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
#endif
    }
}
