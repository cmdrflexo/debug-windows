using System;
using System.Collections.Generic;
using SpaceGraphicsToolkit;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(395)]
    [RequireComponent(typeof(UniverseObservationGridRenderer))]
    public sealed class UniverseObservationTrailDecorator : MonoBehaviour
    {
        private const string ContainerName = "Generated Observation Decorations";

        private struct Sample
        {
            public UniversePosition Position;
            public double Time;
            public Sample(UniversePosition position, double time)
            {
                Position = position;
                Time = time;
            }
        }

        public static bool GlobalVisibility { get; private set; }

        [Header("References")]
        [SerializeField] private CelestialBodyRuntimeContext targetContext;
        [SerializeField]
        [Tooltip("Assign only on the authored template. Runtime siblings share its visibility.")]
        private InputActionReference toggleVisibilityAction;

        [Header("Trail")]
        [SerializeField] private Material trailMaterial;
        [SerializeField] private Color color = new Color(0.25f, 0.8f, 1.0f, 0.85f);
        [SerializeField, Min(0.1f)]
        [Tooltip("Real-time seconds that each universe-position sample remains visible.")]
        private double durationSeconds = 20.0;
        [SerializeField, Range(0.01f, 1.0f)]
        private float fadeOutFraction = 0.2f;
        [SerializeField, Min(0.001f)]
        private float sampleIntervalSeconds = 0.05f;
        [SerializeField, Range(16, 8192)]
        private int maximumSamples = 4096;
        [SerializeField] private bool visible = true;

        [Header("Runtime")]
        [SerializeField] private int sampleCount;
        [SerializeField] private bool recording;
        [SerializeField] private string lastError;

        private readonly List<Sample> samples = new List<Sample>();
        private Transform generatedAnchor;
        private SgtFloatingObject floatingObject;
        private MeshRenderer trailRenderer;
        private Mesh trailMesh;
        private Material generatedMaterial;
        private Camera observationCamera;
        private Bounds geometryBounds;
        private double lastSampleTime = double.NegativeInfinity;
        private bool enabledToggleAction;

        public CelestialBodyRuntimeContext TargetContext => targetContext;
        public int SampleCount => sampleCount;
        public bool Recording => recording;
        public string LastError => lastError;
        public Color TrailColor { get => color; set => color = value; }
        public double DurationSeconds
        {
            get => durationSeconds;
            set => durationSeconds = Math.Max(0.1, value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGlobalVisibility() => GlobalVisibility = false;

        private void Awake() => EnsureVisual();

        private void OnEnable()
        {
            EnsureVisual();
            enabledToggleAction = EnableAction(toggleVisibilityAction);
            if (toggleVisibilityAction != null && toggleVisibilityAction.action != null)
                toggleVisibilityAction.action.performed += OnToggle;
        }

        private void OnDisable()
        {
            if (toggleVisibilityAction != null && toggleVisibilityAction.action != null)
                toggleVisibilityAction.action.performed -= OnToggle;
            DisableAction(toggleVisibilityAction, enabledToggleAction);
            enabledToggleAction = false;
            ClearTrail();
        }

        private void OnDestroy()
        {
            if (generatedAnchor != null) Destroy(generatedAnchor.gameObject);
            if (trailMesh != null) Destroy(trailMesh);
            if (generatedMaterial != null) Destroy(generatedMaterial);
        }

        private void OnValidate()
        {
            durationSeconds = Math.Max(0.1, durationSeconds);
            fadeOutFraction = Mathf.Clamp(fadeOutFraction, 0.01f, 1.0f);
            sampleIntervalSeconds = Mathf.Max(0.001f, sampleIntervalSeconds);
            maximumSamples = Mathf.Clamp(maximumSamples, 16, 8192);
            ApplyMaterial();
        }

        private void LateUpdate()
        {
            EnsureVisual();
            var active = visible && GlobalVisibility && targetContext != null;
            if (!active)
            {
                if (recording || samples.Count > 0) ClearTrail();
                recording = false;
                return;
            }

            recording = true;
            var now = Time.unscaledTimeAsDouble;
            RemoveExpired(now);

            if (now - lastSampleTime >= sampleIntervalSeconds &&
                targetContext.TryGetMotionState(out var state))
            {
                samples.Add(new Sample(state.Position, now));
                lastSampleTime = now;
                while (samples.Count > maximumSamples) samples.RemoveAt(0);
            }

            sampleCount = samples.Count;
            if (samples.Count < 2)
            {
                SetRendererVisible(false);
                return;
            }

            TryRebuild(now);
        }

        public UniverseObservationTrailDecorator CreateRuntimeSibling(
            CelestialBodyRuntimeContext context)
        {
            var trail = gameObject.AddComponent<UniverseObservationTrailDecorator>();
            trail.trailMaterial = trailMaterial;
            trail.color = color;
            trail.durationSeconds = durationSeconds;
            trail.fadeOutFraction = fadeOutFraction;
            trail.sampleIntervalSeconds = sampleIntervalSeconds;
            trail.maximumSamples = maximumSamples;
            trail.visible = visible;
            trail.ApplyMaterial();
            trail.SetTarget(context);
            return trail;
        }

        public void SetTarget(CelestialBodyRuntimeContext context)
        {
            targetContext = context;
            ClearTrail();
        }

        public void ClearTarget()
        {
            targetContext = null;
            ClearTrail();
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (!value) ClearTrail();
        }

        private void RemoveExpired(double now)
        {
            var count = 0;
            while (count < samples.Count &&
                now - samples[count].Time >= durationSeconds) count++;
            if (count > 0) samples.RemoveRange(0, count);
        }

        private bool TryRebuild(double now)
        {
            var anchor = samples[0].Position;
            if (!SgtUniversePositionConverter.TryToSgtPosition(
                    anchor, 0.0, 0.0, 0.0, out var floatingPosition))
            {
                lastError = "The trail anchor could not enter the SGT universe frame.";
                return false;
            }

            var vertices = new Vector3[samples.Count];
            var colors = new Color[samples.Count];
            var indices = new int[samples.Count];
            var fadeDuration = Math.Max(0.001, durationSeconds * fadeOutFraction);

            for (var i = 0; i < samples.Count; i++)
            {
                if (!samples[i].Position.TryGetOffsetMetersFrom(anchor, out var offset) ||
                    !TryToVector3(offset, out vertices[i]))
                {
                    lastError = "A trail sample exceeds the runtime mesh range.";
                    return false;
                }

                var remaining = Math.Max(0.0, durationSeconds - (now - samples[i].Time));
                var alpha = Mathf.Clamp01((float)(remaining / fadeDuration));
                colors[i] = new Color(color.r, color.g, color.b, color.a * alpha);
                indices[i] = i;
            }

            trailMesh.Clear();
            trailMesh.vertices = vertices;
            trailMesh.colors = colors;
            trailMesh.SetIndices(indices, MeshTopology.LineStrip, 0, false);
            trailMesh.RecalculateBounds();
            geometryBounds = trailMesh.bounds;

            floatingObject.SetPosition(floatingPosition);
            floatingObject.ApplyPosition();
            generatedAnchor.SetPositionAndRotation(generatedAnchor.position, Quaternion.identity);
            generatedAnchor.localScale = Vector3.one;
            UpdateBounds();
            lastError = string.Empty;
            SetRendererVisible(true);
            return true;
        }

        private void ClearTrail()
        {
            samples.Clear();
            sampleCount = 0;
            lastSampleTime = double.NegativeInfinity;
            if (trailMesh != null) trailMesh.Clear();
            SetRendererVisible(false);
        }

        private Transform GetContainer()
        {
            var go = GameObject.Find(ContainerName);
            if (go == null) go = new GameObject(ContainerName);
            var container = go.transform;
            container.SetParent(null, false);
            container.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            container.localScale = Vector3.one;
            return container;
        }

        private void EnsureVisual()
        {
            if (generatedAnchor != null) return;

            var root = new GameObject($"Generated Observation Trail {GetInstanceID()}");
            generatedAnchor = root.transform;
            generatedAnchor.SetParent(GetContainer(), false);
            floatingObject = root.AddComponent<SgtFloatingObject>();

            var line = new GameObject("Trail Line");
            line.transform.SetParent(generatedAnchor, false);
            var filter = line.AddComponent<MeshFilter>();
            trailRenderer = line.AddComponent<MeshRenderer>();
            trailRenderer.allowOcclusionWhenDynamic = false;
            trailMesh = new Mesh { name = "Observation Universe Trail" };
            trailMesh.MarkDynamic();
            filter.sharedMesh = trailMesh;
            trailRenderer.enabled = false;
            ApplyMaterial();
        }

        private void ApplyMaterial()
        {
            if (trailRenderer == null) return;
            if (trailMaterial != null)
            {
                if (generatedMaterial != null)
                {
                    Destroy(generatedMaterial);
                    generatedMaterial = null;
                }
                trailRenderer.sharedMaterial = trailMaterial;
                return;
            }

            if (generatedMaterial == null)
            {
                var shader = Shader.Find("jcan/Celestial Systems/Universe Observation Grid");
                if (shader != null)
                    generatedMaterial = new Material(shader)
                    {
                        name = "Observation Trail Line (Runtime)",
                        hideFlags = HideFlags.DontSave
                    };
            }
            trailRenderer.sharedMaterial = generatedMaterial;
        }

        private void UpdateBounds()
        {
            if (trailRenderer == null) return;
            if (observationCamera == null) observationCamera = Camera.main;
            var bounds = geometryBounds;
            if (observationCamera != null)
                bounds.Encapsulate(trailRenderer.transform.InverseTransformPoint(
                    observationCamera.transform.position));
            bounds.Expand(Mathf.Max(1.0f, bounds.size.magnitude * 0.01f));
            trailRenderer.localBounds = bounds;
        }

        private void SetRendererVisible(bool value)
        {
            if (trailRenderer != null) trailRenderer.enabled = value;
        }

        private void OnToggle(InputAction.CallbackContext context)
        {
            GlobalVisibility = !GlobalVisibility;
        }

        private static bool EnableAction(InputActionReference reference)
        {
            var action = reference != null ? reference.action : null;
            if (action == null || action.enabled) return false;
            action.Enable();
            return true;
        }

        private static void DisableAction(InputActionReference reference, bool enabledHere)
        {
            if (enabledHere && reference != null && reference.action != null)
                reference.action.Disable();
        }

        private static bool TryToVector3(DoubleVector3 value, out Vector3 result)
        {
            result = default;
            if (!FiniteFloat(value.x) || !FiniteFloat(value.y) || !FiniteFloat(value.z))
                return false;
            result = new Vector3((float)value.x, (float)value.y, (float)value.z);
            return true;
        }

        private static bool FiniteFloat(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) &&
                Math.Abs(value) <= float.MaxValue;
        }
    }
}
