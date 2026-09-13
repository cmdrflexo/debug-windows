/*
 * Draws generated ring-band boundaries around a ringed body's current orientation.
 * Debug-only; no render geometry is created.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialRingSystemDebugGizmos :
        MonoBehaviour
    {
        [SerializeField]
        [Range(24, 256)]
        private int circleSegments = 96;

        [SerializeField]
        private bool drawWhenSelectedOnly;

        [SerializeField]
        private CelestialBodyRuntimeContext body;

        public void Initialize(
            CelestialBodyRuntimeContext target)
        {
            body = target;
        }

        private void OnDrawGizmos()
        {
            if (!drawWhenSelectedOnly)
            {
                DrawRingBoundaries();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (drawWhenSelectedOnly)
            {
                DrawRingBoundaries();
            }
        }

        private void DrawRingBoundaries()
        {
            body ??= GetComponent<CelestialBodyRuntimeContext>();

            var definition =
                body != null
                    ? body.Definition
                    : null;

            if (definition == null ||
                !definition.HasRingSystemProperties ||
                definition.RingBandInnerRadiiMeters == null ||
                definition.RingBandOuterRadiiMeters == null)
            {
                return;
            }

            var bandCount =
                Mathf.Min(
                    definition.RingBandInnerRadiiMeters.Count,
                    definition.RingBandOuterRadiiMeters.Count);

            if (bandCount <= 0)
            {
                return;
            }

            var axis =
                transform.TransformDirection(
                    definition.NorthAxis).normalized;

            if (axis.sqrMagnitude < 0.000001f)
            {
                return;
            }

            var radial =
                Vector3.ProjectOnPlane(
                    transform.TransformDirection(
                        definition.PoleReferenceAxis),
                    axis).normalized;

            if (radial.sqrMagnitude < 0.000001f)
            {
                radial =
                    Vector3.Cross(
                        axis,
                        Vector3.right);

                if (radial.sqrMagnitude < 0.000001f)
                {
                    radial =
                        Vector3.Cross(
                            axis,
                            Vector3.forward);
                }

                radial.Normalize();
            }

            var tangent =
                Vector3.Cross(
                    axis,
                    radial).normalized;

            for (var index = 0;
                index < bandCount;
                index++)
            {
                var color =
                    ResolveBandColor(
                        index,
                        bandCount);
                Gizmos.color = color;
                DrawCircle(
                    transform.position,
                    radial,
                    tangent,
                    definition.RingBandInnerRadiiMeters[index]);
                DrawCircle(
                    transform.position,
                    radial,
                    tangent,
                    definition.RingBandOuterRadiiMeters[index]);
            }
        }

        private void DrawCircle(
            Vector3 center,
            Vector3 radial,
            Vector3 tangent,
            double radiusMeters)
        {
            if (double.IsNaN(radiusMeters) ||
                double.IsInfinity(radiusMeters) ||
                radiusMeters <= 0.0 ||
                radiusMeters > float.MaxValue)
            {
                return;
            }

            var segments =
                Mathf.Max(
                    3,
                    circleSegments);
            var radius =
                (float)radiusMeters;
            var previous =
                center + radial * radius;

            for (var index = 1;
                index <= segments;
                index++)
            {
                var angle =
                    index * Mathf.PI * 2.0f /
                    segments;
                var next =
                    center +
                    (radial * Mathf.Cos(angle) +
                        tangent * Mathf.Sin(angle)) *
                    radius;
                Gizmos.DrawLine(
                    previous,
                    next);
                previous = next;
            }
        }

        private static Color ResolveBandColor(
            int index,
            int count)
        {
            if (count <= 1)
            {
                return new Color(
                    0.1f,
                    0.9f,
                    1.0f,
                    0.9f);
            }

            var hue =
                Mathf.Repeat(
                    index * 0.61803398875f,
                    1.0f);
            return Color.HSVToRGB(
                hue,
                0.8f,
                1.0f);
        }
    }
}
