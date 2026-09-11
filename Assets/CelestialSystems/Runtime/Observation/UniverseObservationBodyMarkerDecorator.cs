/*
 * Marks a body's projection on the observation grid and draws its elevation line.
 */

using System;
using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(385)]
    [RequireComponent(typeof(UniverseObservationGridRenderer))]
    public sealed class UniverseObservationBodyMarkerDecorator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UniverseObservationAnchorController observationController;
        [SerializeField] private CelestialBodyRuntimeContext targetContext;

        [Header("Fixed Target")]
        [SerializeField] private bool hasTargetPosition;
        [SerializeField] private UniversePosition targetPosition;

        [Header("Marker")]
        [SerializeField] private Material markerMaterial;
        [SerializeField] private Color color = Color.white;
        [SerializeField]
        [Tooltip("Marker width as a fraction of the camera's observation distance.")]
        [Min(0.000001f)]
        private float relativeSize = 0.035f;
        [SerializeField]
        [Tooltip("Small lift above the grid as a fraction of marker size.")]
        [Min(0.0f)]
        private float planeOffsetFraction = 0.001f;
        [SerializeField] private bool visible = true;

        [Header("Runtime")]
        [SerializeField] private bool hasResolvedTarget;
        [SerializeField] private UniversePosition resolvedTargetPosition;
        [SerializeField] private UniversePosition projectedGridPosition;
        [SerializeField] private double elevationMeters;

        private Transform generatedAnchor;
        private Transform markerVisual;
        private SgtFloatingObject floatingObject;
        private MeshRenderer markerRenderer;
        private MeshRenderer elevationLineRenderer;
        private Mesh markerMesh;
        private Mesh elevationLineMesh;
        private Material elevationLineMaterial;
        private MaterialPropertyBlock markerPropertyBlock;

        public CelestialBodyRuntimeContext TargetContext => targetContext;
        public bool HasResolvedTarget => hasResolvedTarget;
        public UniversePosition TargetPosition => resolvedTargetPosition;
        public UniversePosition ProjectedGridPosition => projectedGridPosition;
        public double ElevationMeters => elevationMeters;

        public Color MarkerColor
        {
            get => color;
            set
            {
                color = value;
                ApplyColor();
            }
        }

        public float RelativeSize
        {
            get => relativeSize;
            set => relativeSize = Mathf.Max(0.000001f, value);
        }

        public Material MarkerMaterial
        {
            get => markerMaterial;
            set
            {
                markerMaterial = value;
                ApplyMarkerMaterial();
            }
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureGeneratedVisuals();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureGeneratedVisuals();
        }

        private void OnDestroy()
        {
            if (generatedAnchor != null)
            {
                Destroy(generatedAnchor.gameObject);
            }

            if (markerMesh != null)
            {
                Destroy(markerMesh);
            }

            if (elevationLineMesh != null)
            {
                Destroy(elevationLineMesh);
            }

            if (elevationLineMaterial != null)
            {
                Destroy(elevationLineMaterial);
            }
        }

        private void OnValidate()
        {
            relativeSize = Mathf.Max(0.000001f, relativeSize);
            planeOffsetFraction = Mathf.Max(0.0f, planeOffsetFraction);
            ApplyMarkerMaterial();
            ApplyColor();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            EnsureGeneratedVisuals();

            if (!visible || observationController == null ||
                floatingObject == null ||
                !TryResolveTarget(out var bodyPosition) ||
                !observationController.TryGetObservationPlane(
                    out var pivotPosition,
                    out var planeRight,
                    out var planeForward,
                    out var planeUp) ||
                !TryProjectOntoPlane(
                    pivotPosition,
                    bodyPosition,
                    planeRight,
                    planeForward,
                    planeUp,
                    out var projection,
                    out var elevation) ||
                !SgtUniversePositionConverter.TryToSgtPosition(
                    projection,
                    0.0,
                    0.0,
                    0.0,
                    out var floatingPosition))
            {
                var hadResolvedTarget = hasResolvedTarget;
                hasResolvedTarget = false;

                // Keep the last valid decoration visible through transient
                // motion/frame conversion failures. Visibility is only
                // explicitly removed by SetVisible(false) or ClearTarget().
                if (!hadResolvedTarget)
                {
                    SetRenderersVisible(false);
                }

                return;
            }

            hasResolvedTarget = true;
            resolvedTargetPosition = bodyPosition;
            projectedGridPosition = projection;
            elevationMeters = elevation;

            floatingObject.SetPosition(floatingPosition);
            floatingObject.ApplyPosition();
            generatedAnchor.rotation = Quaternion.LookRotation(
                planeForward,
                planeUp);

            var worldSize = Math.Max(
                0.000001,
                observationController.DistanceMeters * relativeSize);
            var anchorScale = generatedAnchor.lossyScale;
            var scaleCompensation = Math.Max(
                0.000001,
                Math.Max(Math.Abs(anchorScale.x), Math.Abs(anchorScale.z)));
            var localSize = (float)(worldSize / scaleCompensation);
            markerVisual.localScale = Vector3.one * localSize;
            markerVisual.localPosition =
                Vector3.up * (float)(worldSize * planeOffsetFraction);

            UpdateElevationLine(elevation);
            SetRenderersVisible(true);
        }

        public UniverseObservationBodyMarkerDecorator CreateRuntimeSibling(
            CelestialBodyRuntimeContext context)
        {
            var marker =
                gameObject.AddComponent<UniverseObservationBodyMarkerDecorator>();
            marker.observationController =
                observationController;
            marker.markerMaterial =
                markerMaterial;
            marker.color =
                color;
            marker.relativeSize =
                relativeSize;
            marker.planeOffsetFraction =
                planeOffsetFraction;
            marker.visible =
                visible;
            marker.ApplyMarkerMaterial();
            marker.ApplyColor();
            marker.SetTarget(
                context);
            return marker;
        }

        public void SetTarget(CelestialBodyRuntimeContext context)
        {
            targetContext = context;

            if (context != null && context.TryGetMotionState(out var motionState))
            {
                targetPosition = motionState.Position;
                hasTargetPosition = true;
            }
        }

        public void SetTarget(UniversePosition position)
        {
            targetContext = null;
            targetPosition = position;
            hasTargetPosition = true;
        }

        public void ClearTarget()
        {
            targetContext = null;
            hasTargetPosition = false;
            hasResolvedTarget = false;
            SetRenderersVisible(false);
        }

        public void SetVisible(bool value)
        {
            visible = value;

            if (!visible)
            {
                SetRenderersVisible(false);
            }
        }

        private bool TryResolveTarget(out UniversePosition position)
        {
            if (targetContext != null &&
                targetContext.TryGetMotionState(out var targetMotion))
            {
                position = targetMotion.Position;
                targetPosition = position;
                hasTargetPosition = true;
                return true;
            }

            if (hasTargetPosition)
            {
                position = targetPosition;
                return true;
            }

            position = default;
            return false;
        }

        private void ResolveReferences()
        {
            if (observationController == null)
            {
                observationController =
                    FindFirstObjectByType<UniverseObservationAnchorController>();
            }
        }

        private void EnsureGeneratedVisuals()
        {
            if (generatedAnchor != null)
            {
                return;
            }

            var anchorObject = new GameObject(
                $"Generated Body Grid Marker {GetInstanceID()}");
            generatedAnchor = anchorObject.transform;
            floatingObject = anchorObject.AddComponent<SgtFloatingObject>();

            var markerObject = new GameObject("Marker");
            markerVisual = markerObject.transform;
            markerVisual.SetParent(generatedAnchor, false);
            var markerFilter = markerObject.AddComponent<MeshFilter>();
            markerRenderer = markerObject.AddComponent<MeshRenderer>();
            markerMesh = CreatePlaneMesh();
            markerFilter.sharedMesh = markerMesh;

            var lineObject = new GameObject("Elevation Line");
            lineObject.transform.SetParent(generatedAnchor, false);
            var lineFilter = lineObject.AddComponent<MeshFilter>();
            elevationLineRenderer = lineObject.AddComponent<MeshRenderer>();
            elevationLineMesh = new Mesh
            {
                name = "Observation Body Elevation Line"
            };
            elevationLineMesh.MarkDynamic();
            lineFilter.sharedMesh = elevationLineMesh;

            var lineShader = Shader.Find(
                "jcan/Celestial Systems/Universe Observation Grid");

            if (lineShader != null)
            {
                elevationLineMaterial = new Material(lineShader)
                {
                    name = "Observation Body Elevation Line (Runtime)",
                    hideFlags = HideFlags.DontSave
                };
                elevationLineRenderer.sharedMaterial = elevationLineMaterial;
            }

            ApplyMarkerMaterial();
            ApplyColor();
        }

        private void ApplyMarkerMaterial()
        {
            if (markerRenderer != null)
            {
                markerRenderer.sharedMaterial = markerMaterial;
                ApplyColor();
            }
        }

        private void ApplyColor()
        {
            if (markerRenderer != null)
            {
                markerPropertyBlock ??= new MaterialPropertyBlock();
                markerRenderer.GetPropertyBlock(markerPropertyBlock);
                markerPropertyBlock.SetColor("_BaseColor", color);
                markerPropertyBlock.SetColor("_Color", color);
                markerRenderer.SetPropertyBlock(markerPropertyBlock);
            }

            if (elevationLineMesh != null && elevationLineMesh.vertexCount == 2)
            {
                elevationLineMesh.colors = new[] { color, color };
            }
        }

        private void UpdateElevationLine(double elevation)
        {
            elevationLineMesh.Clear();
            elevationLineMesh.vertices = new[]
            {
                Vector3.zero,
                Vector3.up * (float)elevation
            };
            elevationLineMesh.colors = new[] { color, color };
            elevationLineMesh.SetIndices(
                new[] { 0, 1 },
                MeshTopology.Lines,
                0,
                false);
            elevationLineMesh.RecalculateBounds();
        }

        private void SetRenderersVisible(bool value)
        {
            if (markerRenderer != null)
            {
                markerRenderer.enabled = value;
            }

            if (elevationLineRenderer != null)
            {
                elevationLineRenderer.enabled = value;
            }
        }

        private static bool TryProjectOntoPlane(
            UniversePosition pivot,
            UniversePosition target,
            Vector3 planeRight,
            Vector3 planeForward,
            Vector3 planeUp,
            out UniversePosition projection,
            out double elevation)
        {
            GetDelta(pivot, target, out var x, out var y, out var z);
            var right = Dot(x, y, z, planeRight);
            var forward = Dot(x, y, z, planeForward);
            elevation = Dot(x, y, z, planeUp);

            if (double.IsNaN(right) || double.IsInfinity(right) ||
                double.IsNaN(forward) || double.IsInfinity(forward) ||
                double.IsNaN(elevation) || double.IsInfinity(elevation))
            {
                projection = default;
                return false;
            }

            projection = pivot;
            projection.AddLocalMeters(
                planeRight.x * right + planeForward.x * forward,
                planeRight.y * right + planeForward.y * forward,
                planeRight.z * right + planeForward.z * forward);
            return true;
        }

        private static void GetDelta(
            UniversePosition from,
            UniversePosition to,
            out double x,
            out double y,
            out double z)
        {
            x = ((double)to.CellX - from.CellX) *
                UniversePosition.CellSizeMeters + to.LocalXMeters - from.LocalXMeters;
            y = ((double)to.CellY - from.CellY) *
                UniversePosition.CellSizeMeters + to.LocalYMeters - from.LocalYMeters;
            z = ((double)to.CellZ - from.CellZ) *
                UniversePosition.CellSizeMeters + to.LocalZMeters - from.LocalZMeters;
        }

        private static double Dot(double x, double y, double z, Vector3 axis)
        {
            return x * axis.x + y * axis.y + z * axis.z;
        }

        private static Mesh CreatePlaneMesh()
        {
            var mesh = new Mesh
            {
                name = "Generated Observation Body Marker Plane",
                vertices = new[]
                {
                    new Vector3(-0.5f, 0.0f, -0.5f),
                    new Vector3(-0.5f, 0.0f, 0.5f),
                    new Vector3(0.5f, 0.0f, 0.5f),
                    new Vector3(0.5f, 0.0f, -0.5f)
                },
                uv = new[]
                {
                    new Vector2(0.0f, 0.0f),
                    new Vector2(0.0f, 1.0f),
                    new Vector2(1.0f, 1.0f),
                    new Vector2(1.0f, 0.0f)
                },
                normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                triangles = new[]
                {
                    0, 1, 2, 0, 2, 3,
                    2, 1, 0, 3, 2, 0
                }
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
