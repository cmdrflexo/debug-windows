/*
 * Draws the curved boundaries of the active or manually selected cube-sphere face for scene-view navigation and debugging.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CubeSphereEdgeGizmo :
        MonoBehaviour
    {
        private static readonly CubeSphereFace[] Faces =
        {
            CubeSphereFace.PositiveX,
            CubeSphereFace.NegativeX,
            CubeSphereFace.PositiveY,
            CubeSphereFace.NegativeY,
            CubeSphereFace.PositiveZ,
            CubeSphereFace.NegativeZ
        };

        private static readonly CubeSphereEdge[] Edges =
        {
            CubeSphereEdge.NegativeU,
            CubeSphereEdge.PositiveU,
            CubeSphereEdge.NegativeV,
            CubeSphereEdge.PositiveV
        };

        [Header("Source")]
        [SerializeField]
        private CubeSphereGizmoSourceMode sourceMode =
            CubeSphereGizmoSourceMode.SurfaceFrame;

        [SerializeField]
        private GePlanetSurfaceFrame surfaceFrame;

        [Header("Manual Source")]
        [SerializeField]
        private Transform manualPlanetCenter;

        [SerializeField]
        private CubeSphereFace manualFace =
            CubeSphereFace.PositiveZ;

        [SerializeField]
        private double manualPlanetRadiusMeters =
            6371000.0;

        [Header("Drawing")]
        [SerializeField]
        private GizmoDrawMode gizmoDrawMode =
            GizmoDrawMode.Always;

        [SerializeField]
        private bool drawAllFaceEdges;

        [SerializeField]
        private double edgeOffsetMeters =
            10000.0;

        [SerializeField]
        [Range(1, 256)]
        private int segmentsPerEdge = 64;

        [Header("Handoff Boundaries")]
        [SerializeField]
        private bool drawHandoffBoundaries = true;

        [SerializeField]
        private CubeSphereTerrainAddressTracker addressTracker;

        [SerializeField]
        private bool useTrackerPreloadDistance = true;

        [SerializeField]
        private double handoffBoundaryDistanceMeters =
            3000.0;

        [SerializeField]
        private Color handoffBoundaryColor =
            new Color(0.25f, 1.0f, 0.2f, 1.0f);

        [Header("Colors")]
        [SerializeField]
        private Color negativeUEdgeColor =
            new Color(1.0f, 0.1f, 0.1f, 1.0f);

        [SerializeField]
        private Color positiveUEdgeColor =
            new Color(1.0f, 0.55f, 0.0f, 1.0f);

        [SerializeField]
        private Color negativeVEdgeColor =
            new Color(0.15f, 0.35f, 1.0f, 1.0f);

        [SerializeField]
        private Color positiveVEdgeColor =
            new Color(0.0f, 1.0f, 1.0f, 1.0f);

        private void Reset()
        {
            surfaceFrame =
                GetComponent<GePlanetSurfaceFrame>();
            addressTracker =
                GetComponent<CubeSphereTerrainAddressTracker>();
        }

        private void OnDrawGizmos()
        {
            if (gizmoDrawMode ==
                GizmoDrawMode.Always)
            {
                DrawEdgeGizmos();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (gizmoDrawMode ==
                GizmoDrawMode.Selected)
            {
                DrawEdgeGizmos();
            }
        }

        private void DrawEdgeGizmos()
        {
            if (!TryResolveGeometry(
                    out var planetCenter,
                    out var planetRadiusMeters,
                    out var activeFace) ||
                segmentsPerEdge < 1)
            {
                return;
            }

            var drawingRadiusMeters =
                planetRadiusMeters +
                edgeOffsetMeters;

            if (!IsFinite(drawingRadiusMeters) ||
                drawingRadiusMeters <= 0.0 ||
                drawingRadiusMeters >
                    float.MaxValue)
            {
                return;
            }

            if (drawAllFaceEdges)
            {
                DrawAllEdges(
                    planetCenter,
                    drawingRadiusMeters);
            }
            else
            {
                DrawFaceEdges(
                    activeFace,
                    planetCenter,
                    drawingRadiusMeters);
            }

            if (drawHandoffBoundaries &&
                TryResolveHandoffBoundaryDistance(
                    out var boundaryDistanceMeters))
            {
                DrawFaceHandoffBoundaries(
                    activeFace,
                    planetCenter,
                    planetRadiusMeters,
                    drawingRadiusMeters,
                    boundaryDistanceMeters);
            }
        }

        private bool TryResolveGeometry(
            out Vector3 planetCenter,
            out double planetRadiusMeters,
            out CubeSphereFace activeFace)
        {
            switch (sourceMode)
            {
                case CubeSphereGizmoSourceMode.SurfaceFrame:
                    if (surfaceFrame == null ||
                        !surfaceFrame.HasAnchorAddress ||
                        !surfaceFrame.TryGetPlanetCenterScenePosition(
                            out planetCenter))
                    {
                        planetCenter = default;
                        planetRadiusMeters = default;
                        activeFace = default;
                        return false;
                    }

                    planetRadiusMeters =
                        surfaceFrame.PlanetRadiusMeters;
                    activeFace =
                        surfaceFrame.AnchorAddress.Face;
                    break;

                case CubeSphereGizmoSourceMode.Manual:
                    planetCenter =
                        manualPlanetCenter != null
                            ? manualPlanetCenter.position
                            : transform.position;
                    planetRadiusMeters =
                        manualPlanetRadiusMeters;
                    activeFace =
                        manualFace;
                    break;

                default:
                    planetCenter = default;
                    planetRadiusMeters = default;
                    activeFace = default;
                    return false;
            }

            return
                IsFinite(planetRadiusMeters) &&
                planetRadiusMeters > 0.0;
        }

        private void DrawAllEdges(
            Vector3 planetCenter,
            double drawingRadiusMeters)
        {
            for (var faceIndex = 0;
                faceIndex < Faces.Length;
                faceIndex++)
            {
                var face = Faces[faceIndex];

                for (var edgeIndex = 0;
                    edgeIndex < Edges.Length;
                    edgeIndex++)
                {
                    var edge = Edges[edgeIndex];
                    var adjacentFace =
                        CubeSphereTopology.GetAdjacentFace(
                            face,
                            edge);

                    if ((int)adjacentFace <=
                        (int)face)
                    {
                        continue;
                    }

                    DrawEdge(
                        face,
                        edge,
                        planetCenter,
                        drawingRadiusMeters);
                }
            }
        }

        private void DrawFaceEdges(
            CubeSphereFace face,
            Vector3 planetCenter,
            double drawingRadiusMeters)
        {
            for (var edgeIndex = 0;
                edgeIndex < Edges.Length;
                edgeIndex++)
            {
                DrawEdge(
                    face,
                    Edges[edgeIndex],
                    planetCenter,
                    drawingRadiusMeters);
            }
        }

        private bool TryResolveHandoffBoundaryDistance(
            out double boundaryDistanceMeters)
        {
            boundaryDistanceMeters =
                useTrackerPreloadDistance &&
                addressTracker != null
                    ? addressTracker.AdjacentPreloadDistanceMeters
                    : handoffBoundaryDistanceMeters;

            return
                IsFinite(boundaryDistanceMeters) &&
                boundaryDistanceMeters >= 0.0;
        }

        private void DrawFaceHandoffBoundaries(
            CubeSphereFace face,
            Vector3 planetCenter,
            double planetRadiusMeters,
            double drawingRadiusMeters,
            double boundaryDistanceMeters)
        {
            Gizmos.color =
                handoffBoundaryColor;

            for (var edgeIndex = 0;
                edgeIndex < Edges.Length;
                edgeIndex++)
            {
                DrawHandoffBoundary(
                    face,
                    Edges[edgeIndex],
                    planetCenter,
                    planetRadiusMeters,
                    drawingRadiusMeters,
                    boundaryDistanceMeters);
            }
        }

        private void DrawHandoffBoundary(
            CubeSphereFace face,
            CubeSphereEdge edge,
            Vector3 planetCenter,
            double planetRadiusMeters,
            double drawingRadiusMeters,
            double boundaryDistanceMeters)
        {
            var previousPoint =
                default(Vector3);
            var hasPreviousPoint =
                false;

            for (var segment = 0;
                segment <= segmentsPerEdge;
                segment++)
            {
                var varyingCoordinate =
                    -1.0 +
                    2.0 * segment /
                    segmentsPerEdge;

                if (!TryResolveHandoffFaceCoordinates(
                        face,
                        edge,
                        varyingCoordinate,
                        planetRadiusMeters,
                        boundaryDistanceMeters,
                        out var faceU,
                        out var faceV))
                {
                    hasPreviousPoint =
                        false;
                    continue;
                }

                var direction =
                    CubeSphereMapping.AddressToDirection(
                        new CubeSphereAddress(
                            face,
                            faceU,
                            faceV,
                            0.0));
                var point =
                    planetCenter +
                    new Vector3(
                        (float)(
                            direction.x *
                            drawingRadiusMeters),
                        (float)(
                            direction.y *
                            drawingRadiusMeters),
                        (float)(
                            direction.z *
                            drawingRadiusMeters));

                if (hasPreviousPoint)
                {
                    Gizmos.DrawLine(
                        previousPoint,
                        point);
                }

                previousPoint = point;
                hasPreviousPoint = true;
            }
        }

        private static bool TryResolveHandoffFaceCoordinates(
            CubeSphereFace face,
            CubeSphereEdge edge,
            double varyingCoordinate,
            double planetRadiusMeters,
            double boundaryDistanceMeters,
            out double faceU,
            out double faceV)
        {
            ResolveFaceCoordinates(
                edge,
                varyingCoordinate,
                out var boundaryU,
                out var boundaryV);

            if (boundaryDistanceMeters <= 0.0)
            {
                faceU = boundaryU;
                faceV = boundaryV;
                return true;
            }

            var oppositeU =
                boundaryU;
            var oppositeV =
                boundaryV;

            switch (edge)
            {
                case CubeSphereEdge.NegativeU:
                case CubeSphereEdge.PositiveU:
                    oppositeU =
                        -boundaryU;
                    break;

                case CubeSphereEdge.NegativeV:
                case CubeSphereEdge.PositiveV:
                    oppositeV =
                        -boundaryV;
                    break;

                default:
                    faceU = default;
                    faceV = default;
                    return false;
            }

            var oppositeDistanceMeters =
                GetEdgeDistanceMeters(
                    face,
                    edge,
                    oppositeU,
                    oppositeV,
                    planetRadiusMeters);

            if (!IsFinite(oppositeDistanceMeters) ||
                boundaryDistanceMeters >
                    oppositeDistanceMeters)
            {
                faceU = default;
                faceV = default;
                return false;
            }

            var lowerAmount = 0.0;
            var upperAmount = 1.0;

            for (var iteration = 0;
                iteration < 32;
                iteration++)
            {
                var middleAmount =
                    (lowerAmount +
                    upperAmount) *
                    0.5;
                var middleU =
                    Lerp(
                        boundaryU,
                        oppositeU,
                        middleAmount);
                var middleV =
                    Lerp(
                        boundaryV,
                        oppositeV,
                        middleAmount);
                var middleDistanceMeters =
                    GetEdgeDistanceMeters(
                        face,
                        edge,
                        middleU,
                        middleV,
                        planetRadiusMeters);

                if (middleDistanceMeters <
                    boundaryDistanceMeters)
                {
                    lowerAmount =
                        middleAmount;
                }
                else
                {
                    upperAmount =
                        middleAmount;
                }
            }

            var resolvedAmount =
                (lowerAmount +
                upperAmount) *
                0.5;
            faceU =
                Lerp(
                    boundaryU,
                    oppositeU,
                    resolvedAmount);
            faceV =
                Lerp(
                    boundaryV,
                    oppositeV,
                    resolvedAmount);
            return true;
        }

        private static double GetEdgeDistanceMeters(
            CubeSphereFace face,
            CubeSphereEdge edge,
            double faceU,
            double faceV,
            double planetRadiusMeters)
        {
            return CubeSphereMapping.GetFaceProximity(
                    new CubeSphereAddress(
                        face,
                        faceU,
                        faceV,
                        0.0),
                    planetRadiusMeters)
                .GetEdgeDistanceMeters(edge);
        }

        private static double Lerp(
            double first,
            double second,
            double amount)
        {
            return
                first +
                (second - first) *
                amount;
        }

        private void DrawEdge(
            CubeSphereFace face,
            CubeSphereEdge edge,
            Vector3 planetCenter,
            double drawingRadiusMeters)
        {
            Gizmos.color =
                GetEdgeColor(edge);

            var previousPoint = default(Vector3);
            var hasPreviousPoint = false;

            for (var segment = 0;
                segment <= segmentsPerEdge;
                segment++)
            {
                var varyingCoordinate =
                    -1.0 +
                    2.0 * segment /
                    segmentsPerEdge;
                ResolveFaceCoordinates(
                    edge,
                    varyingCoordinate,
                    out var faceU,
                    out var faceV);

                var direction =
                    CubeSphereMapping.AddressToDirection(
                        new CubeSphereAddress(
                            face,
                            faceU,
                            faceV,
                            0.0));
                var point =
                    planetCenter +
                    new Vector3(
                        (float)(
                            direction.x *
                            drawingRadiusMeters),
                        (float)(
                            direction.y *
                            drawingRadiusMeters),
                        (float)(
                            direction.z *
                            drawingRadiusMeters));

                if (hasPreviousPoint)
                {
                    Gizmos.DrawLine(
                        previousPoint,
                        point);
                }

                previousPoint = point;
                hasPreviousPoint = true;
            }
        }

        private static void ResolveFaceCoordinates(
            CubeSphereEdge edge,
            double varyingCoordinate,
            out double faceU,
            out double faceV)
        {
            switch (edge)
            {
                case CubeSphereEdge.NegativeU:
                    faceU = -1.0;
                    faceV = varyingCoordinate;
                    break;

                case CubeSphereEdge.PositiveU:
                    faceU = 1.0;
                    faceV = varyingCoordinate;
                    break;

                case CubeSphereEdge.NegativeV:
                    faceU = varyingCoordinate;
                    faceV = -1.0;
                    break;

                case CubeSphereEdge.PositiveV:
                    faceU = varyingCoordinate;
                    faceV = 1.0;
                    break;

                default:
                    faceU = default;
                    faceV = default;
                    break;
            }
        }

        private Color GetEdgeColor(
            CubeSphereEdge edge)
        {
            switch (edge)
            {
                case CubeSphereEdge.NegativeU:
                    return negativeUEdgeColor;

                case CubeSphereEdge.PositiveU:
                    return positiveUEdgeColor;

                case CubeSphereEdge.NegativeV:
                    return negativeVEdgeColor;

                case CubeSphereEdge.PositiveV:
                    return positiveVEdgeColor;

                default:
                    return Color.white;
            }
        }

        private static bool IsFinite(double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
