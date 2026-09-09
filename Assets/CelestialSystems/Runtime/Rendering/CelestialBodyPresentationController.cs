/*
 * Creates a low-budget body immediately and owns readiness-safe switching to the dynamic surface presentation.
 */

using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    public enum CelestialBodyPresentationMode
    {
        Simple,
        Dynamic
    }

    [DefaultExecutionOrder(400)]
    [DisallowMultipleComponent]
    public sealed class CelestialBodyPresentationController :
        MonoBehaviour
    {
        private const string TerrainShaderName =
            "jcan/Celestial Systems/Celestial Body Terrain";

        [Header("Presentation")]
        [SerializeField]
        private CelestialBodyPresentationMode requestedMode =
            CelestialBodyPresentationMode.Simple;

        [Header("Runtime")]
        [SerializeField]
        private CelestialBodyPresentationMode activeMode;

        [SerializeField]
        private bool initialized;

        [SerializeField]
        private bool waitingForDynamicSurface;

        [SerializeField]
        private Transform simpleRoot;

        [SerializeField]
        private Renderer simpleSurfaceRenderer;

        [SerializeField]
        private Renderer simpleOceanRenderer;

        [SerializeField]
        private string lastError;

        private CelestialBodyRuntimeContext body;
        private CelestialSurfaceQuadtreeRenderer dynamicRenderer;
        private CelestialSurfaceCollisionRuntime collisionRuntime;
        private CelestialAdaptiveSurfaceRenderMode dynamicRenderMode;
        private Material fallbackTerrainMaterial;

        public CelestialBodyPresentationMode RequestedMode =>
            requestedMode;

        public CelestialBodyPresentationMode ActiveMode =>
            activeMode;

        public bool Initialized =>
            initialized;

        public bool WaitingForDynamicSurface =>
            waitingForDynamicSurface;

        public string LastError =>
            lastError;

        public bool Initialize(
            CelestialBodyRuntimeContext newBody,
            CelestialSurfaceQuadtreeRenderer newDynamicRenderer,
            CelestialSurfaceCollisionRuntime newCollisionRuntime,
            CelestialAdaptiveSurfaceRenderMode newDynamicRenderMode)
        {
            body = newBody;
            dynamicRenderer = newDynamicRenderer;
            collisionRuntime = newCollisionRuntime;
            dynamicRenderMode = newDynamicRenderMode;
            initialized = false;
            lastError = string.Empty;

            if (body == null ||
                body.Definition == null ||
                body.VisualRoot == null ||
                dynamicRenderer == null)
            {
                return Fail(
                    "A body presentation controller requires an initialized body package and dynamic renderer.");
            }

            simpleRoot =
                FindOrCreateDirectChild(
                    body.VisualRoot,
                    "Simple");
            ClearSimpleChildren();

            var definition =
                body.Definition;
            var terrainMaterial =
                definition.RoundMapMagicSurface != null
                    ? definition.RoundMapMagicSurface.Material
                    : null;

            if (terrainMaterial == null)
            {
                var shader =
                    Shader.Find(
                        TerrainShaderName);

                if (shader == null)
                {
                    return Fail(
                        "The simple presentation could not resolve the celestial terrain shader.");
                }

                fallbackTerrainMaterial =
                    new Material(
                        shader)
                    {
                        name =
                            $"{definition.DefinitionId} Simple Terrain (Runtime)"
                    };
                terrainMaterial =
                    fallbackTerrainMaterial;
            }

            simpleSurfaceRenderer =
                BuildSphere(
                    "Simple Surface",
                    definition.ReferenceRadiusMeters,
                    terrainMaterial);

            var ocean =
                definition.OceanDefinition;

            if (ocean != null &&
                ocean.HasValidSettings)
            {
                simpleOceanRenderer =
                    BuildSphere(
                        "Simple Ocean",
                        definition.ReferenceRadiusMeters +
                            ocean.GlobalSurfaceElevationMeters,
                        ocean.Material);
            }

            if (simpleSurfaceRenderer == null)
            {
                return Fail(
                    "The simple body surface could not be created.");
            }

            if (body.VisualRoot.GetComponent<
                    CelestialBodyShaderController>() == null)
            {
                body.VisualRoot.gameObject.AddComponent<
                    CelestialBodyShaderController>();
            }

            initialized = true;
            ApplyRequestedMode();
            return true;
        }

        public void RequestMode(
            CelestialBodyPresentationMode mode)
        {
            requestedMode = mode;

            if (initialized)
            {
                ApplyRequestedMode();
            }
        }

        [ContextMenu("Use Simple Presentation")]
        private void UseSimplePresentation()
        {
            RequestMode(
                CelestialBodyPresentationMode.Simple);
        }

        [ContextMenu("Use Dynamic Presentation")]
        private void UseDynamicPresentation()
        {
            RequestMode(
                CelestialBodyPresentationMode.Dynamic);
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                return;
            }

            ApplyRequestedMode();
            ReportReadiness();
        }

        private void ApplyRequestedMode()
        {
            if (requestedMode ==
                CelestialBodyPresentationMode.Simple)
            {
                SetSimpleVisible(
                    true);
                SetDynamicEnabled(
                    false);
                activeMode =
                    CelestialBodyPresentationMode.Simple;
                waitingForDynamicSurface = false;
                lastError = string.Empty;
                return;
            }

            if (dynamicRenderMode ==
                CelestialAdaptiveSurfaceRenderMode.Hidden)
            {
                SetSimpleVisible(
                    true);
                SetDynamicEnabled(
                    false);
                activeMode =
                    CelestialBodyPresentationMode.Simple;
                waitingForDynamicSurface = true;
                lastError =
                    "Dynamic presentation was requested while the adaptive render mode is Hidden.";
                return;
            }

            SetDynamicEnabled(
                true);
            var dynamicReady =
                dynamicRenderer.VisibleSurfaceReady;
            SetSimpleVisible(
                !dynamicReady);
            activeMode =
                dynamicReady
                    ? CelestialBodyPresentationMode.Dynamic
                    : CelestialBodyPresentationMode.Simple;
            waitingForDynamicSurface =
                !dynamicReady;
            lastError = string.Empty;
        }

        private Renderer BuildSphere(
            string objectName,
            double radiusMeters,
            Material material)
        {
            if (!IsFinite(
                    radiusMeters) ||
                radiusMeters <= 0.0 ||
                radiusMeters * 2.0 >
                    float.MaxValue ||
                material == null)
            {
                return null;
            }

            var sphere =
                GameObject.CreatePrimitive(
                    PrimitiveType.Sphere);
            sphere.name = objectName;
            sphere.transform.SetParent(
                simpleRoot,
                false);
            sphere.transform.localPosition =
                Vector3.zero;
            sphere.transform.localRotation =
                Quaternion.identity;
            sphere.transform.localScale =
                Vector3.one *
                (float)(
                    radiusMeters *
                    2.0);

            var collider =
                sphere.GetComponent<Collider>();

            if (collider != null)
            {
                collider.enabled = false;
                Destroy(
                    collider);
            }

            var renderer =
                sphere.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode =
                ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void SetSimpleVisible(
            bool visible)
        {
            if (simpleRoot != null &&
                simpleRoot.gameObject.activeSelf !=
                    visible)
            {
                simpleRoot.gameObject.SetActive(
                    visible);
            }
        }

        private void SetDynamicEnabled(
            bool enabled)
        {
            dynamicRenderer.enabled = enabled;

            if (collisionRuntime != null)
            {
                collisionRuntime.enabled = enabled;
            }
        }

        private void ReportReadiness()
        {
            if (activeMode ==
                CelestialBodyPresentationMode.Simple)
            {
                body.ReportSurfaceReadiness(
                    true,
                    true,
                    false);
                body.ReportOceanReadiness(
                    simpleOceanRenderer != null);
                return;
            }

            body.ReportSurfaceReadiness(
                dynamicRenderer.CoarseSurfaceReady,
                dynamicRenderer.VisibleSurfaceReady,
                collisionRuntime != null &&
                    collisionRuntime.CoverageReady);
        }

        private void ClearSimpleChildren()
        {
            for (var index =
                simpleRoot.childCount - 1;
                index >= 0;
                index--)
            {
                Destroy(
                    simpleRoot.GetChild(
                        index).gameObject);
            }
        }

        private void OnDestroy()
        {
            if (fallbackTerrainMaterial != null)
            {
                Destroy(
                    fallbackTerrainMaterial);
            }
        }

        private bool Fail(
            string error)
        {
            initialized = false;
            lastError = error;
            Debug.LogError(
                error,
                this);
            return false;
        }

        private static Transform FindOrCreateDirectChild(
            Transform parent,
            string childName)
        {
            for (var index = 0;
                index < parent.childCount;
                index++)
            {
                var child =
                    parent.GetChild(
                        index);

                if (child.name ==
                    childName)
                {
                    return child;
                }
            }

            var childObject =
                new GameObject(
                    childName);
            childObject.transform.SetParent(
                parent,
                false);
            return childObject.transform;
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
