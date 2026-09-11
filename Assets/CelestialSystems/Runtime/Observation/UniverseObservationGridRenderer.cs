/*
 * Renders a scale-aware grid on the active universe observation plane.
 */

using System;
using System.Collections.Generic;
using SpaceGraphicsToolkit;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(375)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SgtFloatingObject))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class UniverseObservationGridRenderer : MonoBehaviour
    {
        public enum GridFadeMode
        {
            DistanceFromCenter,
            LinesFromEdge
        }

        [Header("References")]
        [SerializeField]
        private UniverseObservationAnchorController observationController;

        [Header("Grid Size")]
        [SerializeField]
        [FormerlySerializedAs("lineCount")]
        [Min(2)]
        private int lineCount = 100;

        [SerializeField]
        [Tooltip("Approximate current-scale cells per camera distance.")]
        [Min(1.0f)]
        private float cellsPerCameraDistance = 10.0f;

        [SerializeField]
        [Tooltip("Smallest automatic grid-cell size in meters.")]
        [Min(0.000001f)]
        private double minimumCellSizeMeters = 10.0;

        [Header("Edge Fade")]
        [SerializeField]
        private GridFadeMode fadeMode = GridFadeMode.LinesFromEdge;

        [SerializeField]
        [Tooltip("Radial distance where fading starts when using Distance From Center.")]
        [Min(0.0f)]
        private double fadeStartDistanceMeters;

        [SerializeField]
        [Tooltip("Number of lines at each edge used for fading.")]
        [Min(1)]
        private int fadeLineCount = 30;

        [SerializeField]
        [Range(1, 32)]
        [Tooltip("Segments per line used to make the edge fade smooth.")]
        private int fadeSegmentsPerLine = 12;

        [Header("Scale Colors")]
        [SerializeField]
        private Color scaleDownColor = new Color(0.15f, 0.45f, 0.75f, 0.22f);

        [SerializeField]
        private Color currentScaleColor = new Color(0.25f, 0.85f, 1.0f, 0.55f);

        [SerializeField]
        private Color scaleUpColor = new Color(1.0f, 0.55f, 0.15f, 0.75f);

        [Header("Rendering")]
        [SerializeField]
        private Material gridMaterial;

        [SerializeField]
        private bool visible = true;

        [Header("Runtime")]
        [SerializeField]
        private double currentCellSizeMeters;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float scaleTransition;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color32> colors = new List<Color32>();
        private readonly List<int> indices = new List<int>();

        private SgtFloatingObject floatingObject;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Material runtimeMaterial;
        private bool geometryDirty = true;
        private double lastOffsetRight = double.NaN;
        private double lastOffsetForward = double.NaN;
        private double lastBaseSpacing = double.NaN;
        private float lastTransition = float.NaN;

        public double CurrentCellSizeMeters => currentCellSizeMeters;

        public float ScaleTransition => scaleTransition;

        private void Awake()
        {
            ResolveReferences();
            EnsureRenderResources();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureRenderResources();
            geometryDirty = true;
        }

        private void OnDisable()
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }

            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }

        private void OnValidate()
        {
            lineCount = Mathf.Max(2, lineCount);
            cellsPerCameraDistance = Mathf.Max(1.0f, cellsPerCameraDistance);
            minimumCellSizeMeters = Math.Max(0.000001, minimumCellSizeMeters);
            fadeStartDistanceMeters = Math.Max(0.0, fadeStartDistanceMeters);
            fadeLineCount = Mathf.Max(1, fadeLineCount);
            fadeSegmentsPerLine = Mathf.Clamp(fadeSegmentsPerLine, 1, 32);
            geometryDirty = true;
        }

        private void LateUpdate()
        {
            ResolveReferences();
            EnsureRenderResources();

            if (!visible ||
                observationController == null ||
                floatingObject == null ||
                meshRenderer == null ||
                !observationController.TryGetObservationPlane(
                    out var pivotPosition,
                    out var planeRight,
                    out var planeForward,
                    out var planeUp))
            {
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = false;
                }

                return;
            }

            if (!SgtUniversePositionConverter.TryToSgtPosition(
                    pivotPosition,
                    0.0,
                    0.0,
                    0.0,
                    out var gridPosition))
            {
                meshRenderer.enabled = false;
                return;
            }

            floatingObject.SetPosition(gridPosition);
            floatingObject.ApplyPosition();
            transform.rotation = Quaternion.LookRotation(
                planeForward,
                planeUp);

            var distanceMeters = Math.Max(
                observationController.DistanceMeters,
                minimumCellSizeMeters);
            var desiredSpacing = Math.Max(
                minimumCellSizeMeters,
                distanceMeters / cellsPerCameraDistance);
            var exponent = Math.Floor(Math.Log10(desiredSpacing));
            var baseSpacing = Math.Max(
                minimumCellSizeMeters,
                Math.Pow(10.0, exponent));
            var transition = Mathf.Clamp01(
                (float)Math.Log10(desiredSpacing / baseSpacing));

            currentCellSizeMeters = baseSpacing;
            scaleTransition = transition;

            var offsetRight = observationController.PlateOffsetRightMeters;
            var offsetForward = observationController.PlateOffsetForwardMeters;
            var offsetTolerance = Math.Max(baseSpacing * 0.000001, 0.000001);

            if (geometryDirty ||
                Math.Abs(offsetRight - lastOffsetRight) > offsetTolerance ||
                Math.Abs(offsetForward - lastOffsetForward) > offsetTolerance ||
                !Approximately(baseSpacing, lastBaseSpacing) ||
                Mathf.Abs(transition - lastTransition) > 0.001f)
            {
                RebuildMesh(
                    offsetRight,
                    offsetForward,
                    baseSpacing,
                    transition);
                lastOffsetRight = offsetRight;
                lastOffsetForward = offsetForward;
                lastBaseSpacing = baseSpacing;
                lastTransition = transition;
                geometryDirty = false;
            }

            meshRenderer.enabled = mesh != null && mesh.vertexCount > 0;
        }

        private void RebuildMesh(
            double pivotOffsetRight,
            double pivotOffsetForward,
            double baseSpacing,
            float transition)
        {
            vertices.Clear();
            colors.Clear();
            indices.Clear();

            AddScaleLayer(
                baseSpacing * 0.1,
                pivotOffsetRight,
                pivotOffsetForward,
                scaleDownColor,
                1.0f - transition,
                true);
            AddScaleLayer(
                baseSpacing,
                pivotOffsetRight,
                pivotOffsetForward,
                Color.Lerp(currentScaleColor, scaleDownColor, transition),
                1.0f,
                true);
            AddScaleLayer(
                baseSpacing * 10.0,
                pivotOffsetRight,
                pivotOffsetForward,
                Color.Lerp(scaleUpColor, currentScaleColor, transition),
                1.0f,
                true);
            AddScaleLayer(
                baseSpacing * 100.0,
                pivotOffsetRight,
                pivotOffsetForward,
                scaleUpColor,
                transition,
                false);

            mesh.Clear();
            mesh.indexFormat =
                vertices.Count > ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(
                indices,
                MeshTopology.Lines,
                0,
                false);
            mesh.RecalculateBounds();
        }

        private void AddScaleLayer(
            double spacing,
            double pivotOffsetRight,
            double pivotOffsetForward,
            Color color,
            float layerOpacity,
            bool omitNextScaleLines)
        {
            if (spacing <= 0.0 || layerOpacity <= 0.0001f)
            {
                return;
            }

            var nearestX = Math.Round(pivotOffsetRight / spacing);
            var nearestY = Math.Round(pivotOffsetForward / spacing);
            var phaseX = pivotOffsetRight - nearestX * spacing;
            var phaseY = pivotOffsetForward - nearestY * spacing;
            var halfX = spacing * (lineCount - 1) * 0.5;
            var halfY = spacing * (lineCount - 1) * 0.5;
            var startX = -(lineCount / 2);
            var startY = -(lineCount / 2);

            for (var i = 0; i < lineCount; i++)
            {
                var indexOffset = startX + i;
                var gridIndex = nearestX + indexOffset;

                if (omitNextScaleLines && IsMultipleOfTen(gridIndex))
                {
                    continue;
                }

                var x = indexOffset * spacing - phaseX;
                AddSegmentedLine(
                    x,
                    -halfY - phaseY,
                    x,
                    halfY - phaseY,
                    halfX,
                    halfY,
                    spacing,
                    color,
                    layerOpacity);
            }

            for (var i = 0; i < lineCount; i++)
            {
                var indexOffset = startY + i;
                var gridIndex = nearestY + indexOffset;

                if (omitNextScaleLines && IsMultipleOfTen(gridIndex))
                {
                    continue;
                }

                var y = indexOffset * spacing - phaseY;
                AddSegmentedLine(
                    -halfX - phaseX,
                    y,
                    halfX - phaseX,
                    y,
                    halfX,
                    halfY,
                    spacing,
                    color,
                    layerOpacity);
            }
        }

        private void AddSegmentedLine(
            double startX,
            double startY,
            double endX,
            double endY,
            double halfX,
            double halfY,
            double spacing,
            Color color,
            float layerOpacity)
        {
            for (var segment = 0; segment < fadeSegmentsPerLine; segment++)
            {
                var t0 = segment / (double)fadeSegmentsPerLine;
                var t1 = (segment + 1) / (double)fadeSegmentsPerLine;
                var x0 = Lerp(startX, endX, t0);
                var y0 = Lerp(startY, endY, t0);
                var x1 = Lerp(startX, endX, t1);
                var y1 = Lerp(startY, endY, t1);

                AddVertex(
                    x0,
                    y0,
                    ResolveEdgeOpacity(x0, y0, halfX, halfY, spacing),
                    color,
                    layerOpacity);
                AddVertex(
                    x1,
                    y1,
                    ResolveEdgeOpacity(x1, y1, halfX, halfY, spacing),
                    color,
                    layerOpacity);
            }
        }

        private void AddVertex(
            double x,
            double y,
            float edgeOpacity,
            Color color,
            float layerOpacity)
        {
            vertices.Add(new Vector3((float)x, 0.0f, (float)y));
            var vertexColor = color;
            vertexColor.a *= edgeOpacity * layerOpacity;
            colors.Add(vertexColor);
            indices.Add(indices.Count);
        }

        private float ResolveEdgeOpacity(
            double x,
            double y,
            double halfX,
            double halfY,
            double spacing)
        {
            if (fadeMode == GridFadeMode.DistanceFromCenter)
            {
                var fadeEnd = Math.Max(
                    spacing,
                    Math.Min(halfX, halfY));
                var fadeStart = fadeStartDistanceMeters > 0.0
                    ? Math.Min(fadeStartDistanceMeters, fadeEnd - spacing)
                    : fadeEnd * 0.7;
                var distance = Math.Sqrt(x * x + y * y);
                return 1.0f - SmoothStep(fadeStart, fadeEnd, distance);
            }

            var startX = Math.Max(0.0, halfX - fadeLineCount * spacing);
            var startY = Math.Max(0.0, halfY - fadeLineCount * spacing);
            var fadeX = SmoothStep(startX, Math.Max(startX + spacing, halfX), Math.Abs(x));
            var fadeY = SmoothStep(startY, Math.Max(startY + spacing, halfY), Math.Abs(y));
            return 1.0f - Mathf.Max(fadeX, fadeY);
        }

        private void ResolveReferences()
        {
            if (observationController == null)
            {
                observationController =
                    FindFirstObjectByType<UniverseObservationAnchorController>();
            }

            floatingObject ??= GetComponent<SgtFloatingObject>();
            meshFilter ??= GetComponent<MeshFilter>();
            meshRenderer ??= GetComponent<MeshRenderer>();
        }

        private void EnsureRenderResources()
        {
            if (meshFilter == null || meshRenderer == null)
            {
                return;
            }

            if (mesh == null)
            {
                mesh = new Mesh
                {
                    name = "Universe Observation Grid"
                };
                mesh.MarkDynamic();
                meshFilter.sharedMesh = mesh;
            }

            if (gridMaterial != null)
            {
                meshRenderer.sharedMaterial = gridMaterial;
                return;
            }

            if (runtimeMaterial == null)
            {
                var shader = Shader.Find(
                    "jcan/Celestial Systems/Universe Observation Grid");

                if (shader == null)
                {
                    return;
                }

                runtimeMaterial = new Material(shader)
                {
                    name = "Universe Observation Grid (Runtime)",
                    hideFlags = HideFlags.DontSave
                };
            }

            meshRenderer.sharedMaterial = runtimeMaterial;
        }

        private static bool IsMultipleOfTen(double value)
        {
            var nearestMultiple = Math.Round(value / 10.0) * 10.0;
            return Math.Abs(value - nearestMultiple) < 0.001;
        }

        private static bool Approximately(double a, double b)
        {
            return Math.Abs(a - b) <=
                Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b))) * 1.0e-12;
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        private static float SmoothStep(double start, double end, double value)
        {
            if (end <= start)
            {
                return value >= end ? 1.0f : 0.0f;
            }

            var t = Mathf.Clamp01((float)((value - start) / (end - start)));
            return t * t * (3.0f - 2.0f * t);
        }
    }
}
