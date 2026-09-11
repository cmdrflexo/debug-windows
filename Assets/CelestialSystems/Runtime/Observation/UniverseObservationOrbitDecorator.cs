/*
 * Draws one body's closed trajectory as an observation-grid decoration.
 */

using System;
using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(390)]
    [RequireComponent(typeof(UniverseObservationGridRenderer))]
    public sealed class UniverseObservationOrbitDecorator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UniverseObservationAnchorController observationController;
        [SerializeField] private UniverseObservationSelectionController selectionController;
        [SerializeField] private CelestialBodyRuntimeContext targetContext;

        [Header("Appearance")]
        [SerializeField] private Material orbitMaterial;
        [SerializeField]
        private Color normalColor =
            new Color(0.25f, 0.65f, 1.0f, 0.65f);
        [SerializeField]
        private Color targetedColor =
            new Color(1.0f, 0.55f, 0.1f, 1.0f);
        [SerializeField]
        [Range(16, 512)]
        private int pathSegments = 128;
        [SerializeField] private bool visible = true;

        [Header("Runtime")]
        [SerializeField] private bool hasResolvedOrbit;
        [SerializeField] private bool isTargeted;
        [SerializeField] private string lastError;

        private Transform generatedAnchor;
        private SgtFloatingObject floatingObject;
        private MeshRenderer orbitRenderer;
        private Mesh orbitMesh;
        private Material generatedMaterial;
        private TrajectoryCelestialBodyMotionProvider trajectoryProvider;
        private CelestialTrajectoryDefinition builtTrajectory;
        private int builtPathSegments;
        private bool builtTargeted;

        public CelestialBodyRuntimeContext TargetContext => targetContext;
        public bool HasResolvedOrbit => hasResolvedOrbit;
        public bool IsTargeted => isTargeted;
        public string LastError => lastError;

        public Color NormalColor
        {
            get => normalColor;
            set
            {
                normalColor = value;
                ApplyOrbitColor();
            }
        }

        public Color TargetedColor
        {
            get => targetedColor;
            set
            {
                targetedColor = value;
                ApplyOrbitColor();
            }
        }

        public Material OrbitMaterial
        {
            get => orbitMaterial;
            set
            {
                orbitMaterial = value;
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
            if (generatedAnchor != null)
            {
                Destroy(generatedAnchor.gameObject);
            }

            if (orbitMesh != null)
            {
                Destroy(orbitMesh);
            }

            if (generatedMaterial != null)
            {
                Destroy(generatedMaterial);
            }
        }

        private void OnValidate()
        {
            pathSegments = Mathf.Clamp(pathSegments, 16, 512);
            builtTrajectory = null;
            ApplyMaterial();
            ApplyOrbitColor();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            EnsureGeneratedVisual();

            if (!visible)
            {
                SetRendererVisible(false);
                return;
            }

            if (!TryResolveTrajectory(out var provider, out var referenceBody) ||
                !referenceBody.TryGetMotionState(out var referenceMotion) ||
                !SgtUniversePositionConverter.TryToSgtPosition(
                    referenceMotion.Position,
                    0.0,
                    0.0,
                    0.0,
                    out var floatingPosition))
            {
                hasResolvedOrbit = false;

                // Preserve the last valid rendered orbit through transient
                // motion or universe-frame conversion failures.
                return;
            }

            isTargeted =
                selectionController != null &&
                selectionController.SelectedTarget == targetContext;

            if (builtTrajectory != provider.Trajectory ||
                builtPathSegments != pathSegments)
            {
                if (!TryBuildOrbit(provider))
                {
                    hasResolvedOrbit = false;
                    return;
                }
            }

            if (builtTargeted != isTargeted)
            {
                ApplyOrbitColor();
            }

            floatingObject.SetPosition(floatingPosition);
            floatingObject.ApplyPosition();
            generatedAnchor.rotation = Quaternion.identity;
            generatedAnchor.localScale = Vector3.one;
            hasResolvedOrbit = true;
            lastError = string.Empty;
            SetRendererVisible(true);
        }

        public UniverseObservationOrbitDecorator CreateRuntimeSibling(
            CelestialBodyRuntimeContext context)
        {
            var orbit =
                gameObject.AddComponent<UniverseObservationOrbitDecorator>();
            orbit.observationController = observationController;
            orbit.selectionController = selectionController;
            orbit.orbitMaterial = orbitMaterial;
            orbit.normalColor = normalColor;
            orbit.targetedColor = targetedColor;
            orbit.pathSegments = pathSegments;
            orbit.visible = visible;
            orbit.ApplyMaterial();
            orbit.SetTarget(context);
            return orbit;
        }

        public void SetTarget(CelestialBodyRuntimeContext context)
        {
            targetContext = context;
            trajectoryProvider = null;
            builtTrajectory = null;
            hasResolvedOrbit = false;
            lastError = string.Empty;
        }

        public void ClearTarget()
        {
            targetContext = null;
            trajectoryProvider = null;
            builtTrajectory = null;
            hasResolvedOrbit = false;
            lastError = string.Empty;
            SetRendererVisible(false);
        }

        public void SetVisible(bool value)
        {
            visible = value;

            if (!visible)
            {
                SetRendererVisible(false);
            }
        }

        private bool TryResolveTrajectory(
            out TrajectoryCelestialBodyMotionProvider provider,
            out CelestialBodyRuntimeContext referenceBody)
        {
            if (targetContext == null)
            {
                provider = null;
                referenceBody = null;
                lastError = "The orbit decorator has no target body.";
                return false;
            }

            if (trajectoryProvider == null)
            {
                trajectoryProvider =
                    targetContext.MotionProviderSource as
                        TrajectoryCelestialBodyMotionProvider;
            }

            provider = trajectoryProvider;
            referenceBody =
                provider != null
                    ? provider.ReferenceBody
                    : null;

            if (provider == null ||
                !provider.IsReady ||
                !provider.UsesTrajectory ||
                provider.Trajectory == null ||
                referenceBody == null)
            {
                lastError =
                    "The target body does not expose a ready trajectory and reference body.";
                return false;
            }

            if (!provider.TryGetClosedOrbitPeriodSeconds(out _))
            {
                lastError =
                    "The target trajectory is not a supported closed orbit.";
                return false;
            }

            return true;
        }

        private bool TryBuildOrbit(
            TrajectoryCelestialBodyMotionProvider provider)
        {
            if (!provider.TryGetClosedOrbitPeriodSeconds(
                    out var periodSeconds))
            {
                lastError =
                    "The target trajectory has no closed orbital period.";
                return false;
            }

            var segmentCount = Mathf.Clamp(pathSegments, 16, 512);
            var vertices = new Vector3[segmentCount + 1];
            var indices = new int[segmentCount + 1];
            var startTime = provider.Trajectory.EpochUniversalTimeSeconds;

            for (var index = 0; index <= segmentCount; index++)
            {
                var sampleTime =
                    startTime +
                    periodSeconds * index / segmentCount;

                if (!provider.TryEvaluateRelativeState(
                        sampleTime,
                        out var relativePosition,
                        out _) ||
                    !TryToVector3(relativePosition, out vertices[index]))
                {
                    lastError =
                        "An orbit sample could not be represented by the runtime line mesh.";
                    return false;
                }

                indices[index] = index;
            }

            orbitMesh.Clear();
            orbitMesh.vertices = vertices;
            orbitMesh.SetIndices(
                indices,
                MeshTopology.LineStrip,
                0,
                false);
            orbitMesh.RecalculateBounds();

            builtTrajectory = provider.Trajectory;
            builtPathSegments = segmentCount;
            builtTargeted = !isTargeted;
            ApplyOrbitColor();
            return true;
        }

        private void ResolveReferences()
        {
            if (observationController == null)
            {
                observationController =
                    GetComponent<UniverseObservationAnchorController>();
            }

            if (observationController == null)
            {
                observationController =
                    FindFirstObjectByType<UniverseObservationAnchorController>();
            }

            if (selectionController == null)
            {
                selectionController =
                    GetComponent<UniverseObservationSelectionController>();
            }

            if (selectionController == null)
            {
                selectionController =
                    FindFirstObjectByType<UniverseObservationSelectionController>();
            }
        }

        private void EnsureGeneratedVisual()
        {
            if (generatedAnchor != null)
            {
                return;
            }

            var anchorObject = new GameObject(
                $"Generated Observation Orbit {GetInstanceID()}");
            generatedAnchor = anchorObject.transform;
            floatingObject = anchorObject.AddComponent<SgtFloatingObject>();

            var lineObject = new GameObject("Orbit Line");
            lineObject.transform.SetParent(generatedAnchor, false);
            var meshFilter = lineObject.AddComponent<MeshFilter>();
            orbitRenderer = lineObject.AddComponent<MeshRenderer>();
            orbitMesh = new Mesh
            {
                name = "Observation Orbit Line"
            };
            orbitMesh.MarkDynamic();
            meshFilter.sharedMesh = orbitMesh;
            orbitRenderer.enabled = false;
            ApplyMaterial();
        }

        private void ApplyMaterial()
        {
            if (orbitRenderer == null)
            {
                return;
            }

            if (orbitMaterial != null)
            {
                if (generatedMaterial != null)
                {
                    Destroy(generatedMaterial);
                    generatedMaterial = null;
                }

                orbitRenderer.sharedMaterial = orbitMaterial;
                return;
            }

            if (generatedMaterial == null)
            {
                var shader = Shader.Find(
                    "jcan/Celestial Systems/Universe Observation Grid");

                if (shader != null)
                {
                    generatedMaterial = new Material(shader)
                    {
                        name = "Observation Orbit Line (Runtime)",
                        hideFlags = HideFlags.DontSave
                    };
                }
            }

            orbitRenderer.sharedMaterial = generatedMaterial;
        }

        private void ApplyOrbitColor()
        {
            if (orbitMesh == null || orbitMesh.vertexCount == 0)
            {
                return;
            }

            var appliedColor =
                isTargeted
                    ? targetedColor
                    : normalColor;
            var colors = new Color[orbitMesh.vertexCount];

            for (var index = 0; index < colors.Length; index++)
            {
                colors[index] = appliedColor;
            }

            orbitMesh.colors = colors;
            builtTargeted = isTargeted;
        }

        private void SetRendererVisible(bool value)
        {
            if (orbitRenderer != null)
            {
                orbitRenderer.enabled = value;
            }
        }

        private static bool TryToVector3(
            DoubleVector3 value,
            out Vector3 result)
        {
            result = default;

            if (!IsFloatRepresentable(value.x) ||
                !IsFloatRepresentable(value.y) ||
                !IsFloatRepresentable(value.z))
            {
                return false;
            }

            result = new Vector3(
                (float)value.x,
                (float)value.y,
                (float)value.z);
            return true;
        }

        private static bool IsFloatRepresentable(double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                Math.Abs(value) <= float.MaxValue;
        }
    }
}
