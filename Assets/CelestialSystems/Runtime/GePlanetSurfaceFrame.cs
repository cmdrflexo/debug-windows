/*
 * Positions and orients a local tangent frame on the surface of a spherical Gravity Engine body.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class GePlanetSurfaceFrame : MonoBehaviour
    {
        private const float MinimumAxisSquared = 0.000001f;

        [SerializeField]
        private UniverseFrameController universeFrame;

        [SerializeField]
        private NBody planetBody;

        [SerializeField]
        private double planetRadiusMeters = 6371000.0;

        [SerializeField]
        private Vector3 planetNorthAxis = Vector3.up;

        [SerializeField]
        private Vector3 poleReferenceAxis = Vector3.forward;

        [SerializeField]
        private double anchorAltitudeMeters;

        [Header("Gizmos")]
        [SerializeField]
        private GizmoDrawMode gizmoDrawMode;

        [SerializeField]
        private float gizmoSize = 100000.0f;

        private GravityEngine gravityEngine;
        private Vector3 previousTangentForward;
        private bool previousTangentForwardSet;

        public double AnchorAltitudeMeters =>
            anchorAltitudeMeters;

        private void Start()
        {
            gravityEngine = GravityEngine.Instance();

            if (universeFrame == null)
            {
                Debug.LogError(
                    "The planet surface frame requires a universe frame controller.",
                    this);
            }

            if (planetBody == null)
            {
                Debug.LogError(
                    "The planet surface frame requires a planet NBody.",
                    this);
            }

            if (planetRadiusMeters <= 0.0)
            {
                Debug.LogError(
                    "The planet surface frame requires a positive radius.",
                    this);
            }
        }

        private void LateUpdate()
        {
            if (universeFrame == null ||
                planetBody == null ||
                planetRadiusMeters <= 0.0 ||
                !universeFrame.TryGetActiveAnchorOffsetMeters(
                    out var anchorOffsetMeters))
            {
                return;
            }

            gravityEngine ??= GravityEngine.Instance();

            if (gravityEngine == null || !gravityEngine.IsSetup())
            {
                return;
            }

            var physicalScale = gravityEngine.GetPhysicalScale();

            if (Mathf.Approximately(physicalScale, 0.0f))
            {
                return;
            }

            var physicsPosition =
                gravityEngine.GetPositionDoubleV3(planetBody);
            var planetCenterX =
                physicsPosition.x * physicalScale;
            var planetCenterY =
                physicsPosition.y * physicalScale;
            var planetCenterZ =
                physicsPosition.z * physicalScale;
            var radialX =
                anchorOffsetMeters.x - planetCenterX;
            var radialY =
                anchorOffsetMeters.y - planetCenterY;
            var radialZ =
                anchorOffsetMeters.z - planetCenterZ;
            var radialDistanceSquared =
                radialX * radialX +
                radialY * radialY +
                radialZ * radialZ;

            if (radialDistanceSquared <= double.Epsilon)
            {
                return;
            }

            var radialDistance =
                Math.Sqrt(radialDistanceSquared);
            var upX = radialX / radialDistance;
            var upY = radialY / radialDistance;
            var upZ = radialZ / radialDistance;
            var surfacePosition = new Vector3(
                (float)(planetCenterX + upX * planetRadiusMeters),
                (float)(planetCenterY + upY * planetRadiusMeters),
                (float)(planetCenterZ + upZ * planetRadiusMeters));
            var surfaceUp = new Vector3(
                (float)upX,
                (float)upY,
                (float)upZ).normalized;
            var tangentForward =
                ResolveTangentForward(surfaceUp);

            anchorAltitudeMeters =
                radialDistance - planetRadiusMeters;
            transform.SetPositionAndRotation(
                surfacePosition,
                Quaternion.LookRotation(
                    tangentForward,
                    surfaceUp));

            previousTangentForward = tangentForward;
            previousTangentForwardSet = true;
        }

        private Vector3 ResolveTangentForward(Vector3 surfaceUp)
        {
            var tangentForward =
                ProjectOntoTangent(
                    planetNorthAxis,
                    surfaceUp);

            if (tangentForward.sqrMagnitude <
                MinimumAxisSquared &&
                previousTangentForwardSet)
            {
                tangentForward =
                    ProjectOntoTangent(
                        previousTangentForward,
                        surfaceUp);
            }

            if (tangentForward.sqrMagnitude <
                MinimumAxisSquared)
            {
                tangentForward =
                    ProjectOntoTangent(
                        poleReferenceAxis,
                        surfaceUp);
            }

            if (tangentForward.sqrMagnitude <
                MinimumAxisSquared)
            {
                var fallbackAxis =
                    Mathf.Abs(Vector3.Dot(
                        surfaceUp,
                        Vector3.forward)) < 0.99f
                        ? Vector3.forward
                        : Vector3.right;

                tangentForward =
                    ProjectOntoTangent(
                        fallbackAxis,
                        surfaceUp);
            }

            return tangentForward.normalized;
        }

        private static Vector3 ProjectOntoTangent(
            Vector3 direction,
            Vector3 surfaceUp)
        {
            return Vector3.ProjectOnPlane(
                direction,
                surfaceUp);
        }

        private void OnDrawGizmos()
        {
            if (gizmoDrawMode == GizmoDrawMode.Always)
            {
                DrawGizmos();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (gizmoDrawMode == GizmoDrawMode.Selected)
            {
                DrawGizmos();
            }
        }

        private void DrawGizmos()
        {
            if (gizmoSize <= 0.0f)
            {
                return;
            }

            Gizmos.color = Color.red;
            Gizmos.DrawRay(
                transform.position,
                transform.right * gizmoSize);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(
                transform.position,
                transform.up * gizmoSize);

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(
                transform.position,
                transform.forward * gizmoSize);
        }
    }
}
