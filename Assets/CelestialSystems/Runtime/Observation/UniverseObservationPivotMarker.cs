/*
 * Generates a scale-aware pivot marker as a child decoration of the observation grid.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(380)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UniverseObservationGridRenderer))]
    public sealed class UniverseObservationPivotMarker : MonoBehaviour
    {
        private const string GeneratedVisualName = "Generated Observation Pivot Marker";

        [Header("References")]
        [SerializeField] private UniverseObservationAnchorController observationController;
        [SerializeField]
        [Tooltip("Optional moving point that the marker's local Z axis faces.")]
        private CelestialBodyRuntimeContext directionReferenceContext;

        [Header("Direction Reference")]
        [SerializeField]
        [Tooltip("Use the observation target when no explicit direction reference is assigned.")]
        private bool useObservationTargetWhenUnset = true;
        [SerializeField] private bool hasDirectionReferencePosition;
        [SerializeField] private UniversePosition directionReferencePosition;

        [Header("Marker")]
        [SerializeField] private Material markerMaterial;
        [SerializeField] private Color markerColor = Color.white;
        [SerializeField]
        [Tooltip("Marker width as a fraction of the camera's observation distance.")]
        [Min(0.000001f)]
        private float relativeSize = 0.05f;
        [SerializeField]
        [Tooltip("Small lift above the grid as a fraction of marker size.")]
        [Min(0.0f)]
        private float planeOffsetFraction = 0.001f;
        [SerializeField] private bool visible = true;

        [Header("Runtime")]
        [SerializeField] private bool hasPivotPosition;
        [SerializeField] private UniversePosition pivotPosition;
        [SerializeField] private bool hasResolvedDirectionReference;
        [SerializeField] private UniversePosition resolvedDirectionReferencePosition;

        private Transform markerVisual;
        private MeshRenderer markerRenderer;
        private Mesh generatedMesh;
        private MaterialPropertyBlock markerPropertyBlock;

        public UniverseObservationAnchorController ObservationController => observationController;
        public CelestialBodyRuntimeContext DirectionReferenceContext => directionReferenceContext;
        public bool HasPivotPosition => hasPivotPosition;
        public UniversePosition PivotPosition => pivotPosition;
        public bool HasDirectionReference => hasResolvedDirectionReference;
        public UniversePosition DirectionReferencePosition => resolvedDirectionReferencePosition;

        public float RelativeSize
        {
            get => relativeSize;
            set => relativeSize = Mathf.Max(0.000001f, value);
        }

        public Color MarkerColor
        {
            get => markerColor;
            set
            {
                markerColor = value;
                ApplyMarkerColor();
            }
        }

        public Material MarkerMaterial
        {
            get => markerMaterial;
            set
            {
                markerMaterial = value;
                ApplyMaterial();
            }
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureGeneratedVisual();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureGeneratedVisual();
        }

        private void OnDestroy()
        {
            if (generatedMesh != null)
            {
                Destroy(generatedMesh);
            }
        }

        private void OnValidate()
        {
            relativeSize = Mathf.Max(0.000001f, relativeSize);
            planeOffsetFraction = Mathf.Max(0.0f, planeOffsetFraction);
            ApplyMaterial();
            ApplyMarkerColor();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            EnsureGeneratedVisual();

            if (!visible || observationController == null || markerVisual == null ||
                !observationController.TryGetObservationPlane(
                    out var currentPivotPosition,
                    out var planeRight,
                    out var planeForward,
                    out var planeUp))
            {
                var hadPivotPosition = hasPivotPosition;
                hasPivotPosition = false;
                hasResolvedDirectionReference = false;

                // Keep the last valid decoration visible through transient
                // observation-frame failures. Visibility is only explicitly
                // removed with SetVisible(false).
                if (!hadPivotPosition)
                {
                    SetRendererVisible(false);
                }

                return;
            }

            pivotPosition = currentPivotPosition;
            hasPivotPosition = true;
            var markerForward = planeForward;

            if (TryResolveDirectionReference(out var referencePosition))
            {
                resolvedDirectionReferencePosition = referencePosition;
                hasResolvedDirectionReference = true;

                if (TryGetPlanarDirection(
                        currentPivotPosition,
                        referencePosition,
                        planeRight,
                        planeForward,
                        out var planarDirection))
                {
                    markerForward = planarDirection;
                }
            }
            else
            {
                hasResolvedDirectionReference = false;
            }

            markerVisual.rotation = Quaternion.LookRotation(markerForward, planeUp);

            var worldSize = Math.Max(
                0.000001,
                observationController.DistanceMeters * relativeSize);
            var parentScale = transform.lossyScale;
            var scaleCompensation = Math.Max(
                0.000001,
                Math.Max(Math.Abs(parentScale.x), Math.Abs(parentScale.z)));
            var localSize = (float)(worldSize / scaleCompensation);
            markerVisual.localScale = Vector3.one * localSize;
            markerVisual.position =
                transform.position +
                planeUp * (float)(worldSize * planeOffsetFraction);
            SetRendererVisible(true);
        }

        public void SetDirectionReference(CelestialBodyRuntimeContext context)
        {
            directionReferenceContext = context;

            if (context != null && context.TryGetMotionState(out var motionState))
            {
                directionReferencePosition = motionState.Position;
                hasDirectionReferencePosition = true;
            }
        }

        public void SetDirectionReference(UniversePosition position)
        {
            directionReferenceContext = null;
            directionReferencePosition = position;
            hasDirectionReferencePosition = true;
        }

        public void ClearDirectionReference()
        {
            directionReferenceContext = null;
            hasDirectionReferencePosition = false;
            hasResolvedDirectionReference = false;
        }

        public void SetVisible(bool value)
        {
            visible = value;

            if (!visible)
            {
                SetRendererVisible(false);
            }
        }

        private bool TryResolveDirectionReference(out UniversePosition position)
        {
            if (directionReferenceContext != null &&
                directionReferenceContext.TryGetMotionState(out var directionMotion))
            {
                position = directionMotion.Position;
                directionReferencePosition = position;
                hasDirectionReferencePosition = true;
                return true;
            }

            if (hasDirectionReferencePosition)
            {
                position = directionReferencePosition;
                return true;
            }

            var observationTarget = observationController.Target;

            if (useObservationTargetWhenUnset && observationTarget != null &&
                observationTarget.TryGetMotionState(out var targetMotion))
            {
                position = targetMotion.Position;
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

        private void EnsureGeneratedVisual()
        {
            if (markerVisual != null)
            {
                return;
            }

            var existing = transform.Find(GeneratedVisualName);

            if (existing != null)
            {
                markerVisual = existing;
                markerRenderer = existing.GetComponent<MeshRenderer>();
                ApplyMaterial();
                return;
            }

            var visualObject = new GameObject(GeneratedVisualName);
            markerVisual = visualObject.transform;
            markerVisual.SetParent(transform, false);
            var meshFilter = visualObject.AddComponent<MeshFilter>();
            markerRenderer = visualObject.AddComponent<MeshRenderer>();
            generatedMesh = CreatePlaneMesh();
            meshFilter.sharedMesh = generatedMesh;
            ApplyMaterial();
        }

        private void ApplyMaterial()
        {
            if (markerRenderer != null)
            {
                markerRenderer.sharedMaterial = markerMaterial;
                ApplyMarkerColor();
            }
        }

        private void ApplyMarkerColor()
        {
            if (markerRenderer == null)
            {
                return;
            }

            markerPropertyBlock ??= new MaterialPropertyBlock();
            markerRenderer.GetPropertyBlock(markerPropertyBlock);
            markerPropertyBlock.SetColor("_BaseColor", markerColor);
            markerPropertyBlock.SetColor("_Color", markerColor);
            markerRenderer.SetPropertyBlock(markerPropertyBlock);
        }

        private void SetRendererVisible(bool value)
        {
            if (markerRenderer != null)
            {
                markerRenderer.enabled = value;
            }
        }

        private static Mesh CreatePlaneMesh()
        {
            var mesh = new Mesh
            {
                name = "Generated Observation Pivot Marker Plane",
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

        private static bool TryGetPlanarDirection(
            UniversePosition from,
            UniversePosition to,
            Vector3 planeRight,
            Vector3 planeForward,
            out Vector3 direction)
        {
            var deltaX = ((double)to.CellX - from.CellX) *
                UniversePosition.CellSizeMeters + to.LocalXMeters - from.LocalXMeters;
            var deltaY = ((double)to.CellY - from.CellY) *
                UniversePosition.CellSizeMeters + to.LocalYMeters - from.LocalYMeters;
            var deltaZ = ((double)to.CellZ - from.CellZ) *
                UniversePosition.CellSizeMeters + to.LocalZMeters - from.LocalZMeters;
            var right = deltaX * planeRight.x + deltaY * planeRight.y +
                deltaZ * planeRight.z;
            var forward = deltaX * planeForward.x + deltaY * planeForward.y +
                deltaZ * planeForward.z;
            var magnitude = Math.Sqrt(right * right + forward * forward);

            if (magnitude <= 1.0e-9 || double.IsNaN(magnitude) ||
                double.IsInfinity(magnitude))
            {
                direction = default;
                return false;
            }

            direction = planeRight * (float)(right / magnitude) +
                planeForward * (float)(forward / magnitude);
            direction.Normalize();
            return true;
        }
    }
}
