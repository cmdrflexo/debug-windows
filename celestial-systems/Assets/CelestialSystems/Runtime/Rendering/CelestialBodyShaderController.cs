/*
 * Binds floating-origin-safe body data and optional diagnostic modes to custom celestial surface shaders.
 */

using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(350)]
    [DisallowMultipleComponent]
    public sealed class CelestialBodyShaderController :
        MonoBehaviour
    {
        private static readonly int SurfaceTypeId =
            Shader.PropertyToID(
                "_CelestialSurfaceType");

        private static readonly int PlanetCenterId =
            Shader.PropertyToID(
                "_PlanetCenterScenePosition");

        private static readonly int PlanetRadiusId =
            Shader.PropertyToID(
                "_PlanetRadiusMeters");

        private static readonly int OceanSurfaceElevationId =
            Shader.PropertyToID(
                "_OceanSurfaceElevationMeters");

        private static readonly int OceanRadiusId =
            Shader.PropertyToID(
                "_OceanRadiusMeters");

        private static readonly int BodyNorthDirectionId =
            Shader.PropertyToID(
                "_BodyNorthDirection");

        private static readonly int BodyPoleReferenceDirectionId =
            Shader.PropertyToID(
                "_BodyPoleReferenceDirection");

        private static readonly int ElevationDebugMinimumId =
            Shader.PropertyToID(
                "_ElevationDebugMinMeters");

        private static readonly int ElevationDebugMaximumId =
            Shader.PropertyToID(
                "_ElevationDebugMaxMeters");

        private static readonly int SlopeDebugMinimumDegreesId =
            Shader.PropertyToID(
                "_SlopeDebugMinDegrees");

        private static readonly int SlopeDebugMaximumDegreesId =
            Shader.PropertyToID(
                "_SlopeDebugMaxDegrees");

        private static readonly int CoordinateDebugScaleId =
            Shader.PropertyToID(
                "_CoordinateDebugScaleMeters");

        private static readonly int DebugModeId =
            Shader.PropertyToID(
                "_DebugMode");

        private const float OceanSurfaceType =
            1.0f;

        private const string TerrainShaderName =
            "jcan/Celestial Systems/Celestial Body Terrain";

        private const string OceanShaderName =
            "jcan/Celestial Systems/Celestial Body Ocean";

        [Header("Configuration")]
        [SerializeField]
        private CelestialBodyRuntimeContext bodyContext;

        [SerializeField]
        [Tooltip("Optional transform that defines the body's center and orientation. Defaults to the runtime context's Visual Root.")]
        private Transform bodyFrameOverride;

        [SerializeField]
        [Tooltip("Optional floating-origin surface frame used to resolve the current scene-space planet center.")]
        private GePlanetSurfaceFrame surfaceFrameOverride;

        [SerializeField]
        [Tooltip("Root searched for renderers using the custom celestial shaders. Defaults to this transform.")]
        private Transform rendererRoot;

        [SerializeField]
        [Min(0.05f)]
        private float rendererRefreshIntervalSeconds =
            0.25f;

        [Header("Diagnostic Views")]
        [SerializeField]
        private CelestialBodyShaderDebugMode terrainDebugMode;

        [SerializeField]
        private CelestialBodyShaderDebugMode oceanDebugMode;

        [SerializeField]
        private float elevationDebugMinimumMeters =
            -5000.0f;

        [SerializeField]
        private float elevationDebugMaximumMeters =
            5000.0f;

        [SerializeField]
        [Range(0.0f, 180.0f)]
        private float slopeDebugMinimumDegrees;

        [SerializeField]
        [Range(0.0f, 180.0f)]
        private float slopeDebugMaximumDegrees =
            20.0f;

        [SerializeField]
        [Min(0.001f)]
        private float coordinateDebugScaleMeters =
            1000.0f;

        [Header("Runtime")]
        [SerializeField]
        private bool hasValidShaderData;

        [SerializeField]
        private int cachedRendererCount;

        [SerializeField]
        private int boundRendererCount;

        [SerializeField]
        private int terrainRendererCount;

        [SerializeField]
        private int oceanRendererCount;

        [SerializeField]
        private Vector3 bodyCenterScenePosition;

        [SerializeField]
        private double datumRadiusMeters;

        [SerializeField]
        private double oceanSurfaceElevationMeters;

        [SerializeField]
        private double oceanRadiusMeters;

        [SerializeField]
        private string lastError;

        private readonly List<Renderer> rendererCache =
            new List<Renderer>();

        private MaterialPropertyBlock propertyBlock;

        private float nextRendererRefreshTime;
        private bool rendererCacheDirty =
            true;

        public bool HasValidShaderData =>
            hasValidShaderData;

        public CelestialBodyRuntimeContext BodyContext =>
            bodyContext;

        public Transform BodyFrame =>
            ResolveBodyFrame();

        public GePlanetSurfaceFrame SurfaceFrame =>
            surfaceFrameOverride;

        public int CachedRendererCount =>
            cachedRendererCount;

        public int BoundRendererCount =>
            boundRendererCount;

        public int TerrainRendererCount =>
            terrainRendererCount;

        public int OceanRendererCount =>
            oceanRendererCount;

        public Vector3 BodyCenterScenePosition =>
            bodyCenterScenePosition;

        public double DatumRadiusMeters =>
            datumRadiusMeters;

        public double OceanSurfaceElevationMeters =>
            oceanSurfaceElevationMeters;

        public double OceanRadiusMeters =>
            oceanRadiusMeters;

        public CelestialBodyShaderDebugMode TerrainDebugMode =>
            terrainDebugMode;

        public CelestialBodyShaderDebugMode OceanDebugMode =>
            oceanDebugMode;

        public float SlopeDebugMinimumDegrees =>
            slopeDebugMinimumDegrees;

        public float SlopeDebugMaximumDegrees =>
            slopeDebugMaximumDegrees;

        public string LastError =>
            lastError;

        private void Reset()
        {
            ResolveLocalReferences();
            rendererRoot =
                transform;
        }

        private void Awake()
        {
            EnsurePropertyBlock();
            ResolveLocalReferences();
            ResolveRendererRoot();
        }

        private void OnEnable()
        {
            rendererCacheDirty =
                true;
        }

        private void OnTransformChildrenChanged()
        {
            rendererCacheDirty =
                true;
        }

        private void LateUpdate()
        {
            ResolveLocalReferences();
            ResolveRendererRoot();
            RefreshRendererCacheIfNeeded();
            BindShaderData();
        }

        private void OnValidate()
        {
            rendererRefreshIntervalSeconds =
                Mathf.Max(
                    0.05f,
                    rendererRefreshIntervalSeconds);
            slopeDebugMinimumDegrees =
                Mathf.Clamp(
                    slopeDebugMinimumDegrees,
                    0.0f,
                    179.99f);
            slopeDebugMaximumDegrees =
                Mathf.Clamp(
                    slopeDebugMaximumDegrees,
                    slopeDebugMinimumDegrees +
                        0.01f,
                    180.0f);
            coordinateDebugScaleMeters =
                Mathf.Max(
                    0.001f,
                    coordinateDebugScaleMeters);
            rendererCacheDirty =
                true;
        }

        [ContextMenu("Refresh Shader Renderers")]
        private void RefreshShaderRenderers()
        {
            rendererCacheDirty =
                true;
            RefreshRendererCacheIfNeeded(
                true);
        }

        private void BindShaderData()
        {
            EnsurePropertyBlock();
            ClearBindingCounts();

            if (!TryResolveBodyData(
                    out var definition,
                    out var resolvedBodyCenter,
                    out var bodyNorthDirection,
                    out var bodyPoleReferenceDirection))
            {
                return;
            }

            bodyCenterScenePosition =
                resolvedBodyCenter;
            datumRadiusMeters =
                definition.ReferenceRadiusMeters;
            oceanSurfaceElevationMeters =
                definition.OceanDefinition != null
                    ? definition.OceanDefinition
                        .GlobalSurfaceElevationMeters
                    : 0.0;
            oceanRadiusMeters =
                datumRadiusMeters +
                oceanSurfaceElevationMeters;
            hasValidShaderData =
                true;
            lastError =
                string.Empty;

            for (var index = 0;
                index < rendererCache.Count;
                index++)
            {
                var targetRenderer =
                    rendererCache[index];

                if (!TryResolveSurfaceType(
                        targetRenderer,
                        out var isOcean))
                {
                    continue;
                }

                propertyBlock.Clear();
                targetRenderer.GetPropertyBlock(
                    propertyBlock);
                propertyBlock.SetVector(
                    PlanetCenterId,
                    new Vector4(
                        resolvedBodyCenter.x,
                        resolvedBodyCenter.y,
                        resolvedBodyCenter.z,
                        1.0f));
                propertyBlock.SetFloat(
                    PlanetRadiusId,
                    (float)datumRadiusMeters);
                propertyBlock.SetFloat(
                    OceanSurfaceElevationId,
                    (float)oceanSurfaceElevationMeters);
                propertyBlock.SetFloat(
                    OceanRadiusId,
                    (float)oceanRadiusMeters);
                propertyBlock.SetVector(
                    BodyNorthDirectionId,
                    new Vector4(
                        bodyNorthDirection.x,
                        bodyNorthDirection.y,
                        bodyNorthDirection.z,
                        0.0f));
                propertyBlock.SetVector(
                    BodyPoleReferenceDirectionId,
                    new Vector4(
                        bodyPoleReferenceDirection.x,
                        bodyPoleReferenceDirection.y,
                        bodyPoleReferenceDirection.z,
                        0.0f));
                propertyBlock.SetFloat(
                    ElevationDebugMinimumId,
                    elevationDebugMinimumMeters);
                propertyBlock.SetFloat(
                    ElevationDebugMaximumId,
                    elevationDebugMaximumMeters);
                propertyBlock.SetFloat(
                    SlopeDebugMinimumDegreesId,
                    slopeDebugMinimumDegrees);
                propertyBlock.SetFloat(
                    SlopeDebugMaximumDegreesId,
                    slopeDebugMaximumDegrees);
                propertyBlock.SetFloat(
                    CoordinateDebugScaleId,
                    coordinateDebugScaleMeters);
                propertyBlock.SetFloat(
                    DebugModeId,
                    isOcean
                        ? (float)oceanDebugMode
                        : (float)terrainDebugMode);
                targetRenderer.SetPropertyBlock(
                    propertyBlock);

                boundRendererCount++;

                if (isOcean)
                {
                    oceanRendererCount++;
                }
                else
                {
                    terrainRendererCount++;
                }
            }
        }

        private void EnsurePropertyBlock()
        {
            if (propertyBlock == null)
            {
                propertyBlock =
                    new MaterialPropertyBlock();
            }
        }

        private bool TryResolveBodyData(
            out CelestialBodyDefinition definition,
            out Vector3 resolvedBodyCenter,
            out Vector3 bodyNorthDirection,
            out Vector3 bodyPoleReferenceDirection)
        {
            definition =
                null;
            resolvedBodyCenter =
                default;
            bodyNorthDirection =
                Vector3.up;
            bodyPoleReferenceDirection =
                Vector3.forward;
            hasValidShaderData =
                false;

            if (bodyContext == null)
            {
                lastError =
                    "A celestial shader controller requires a body context.";
                return false;
            }

            definition =
                bodyContext.Definition;

            if (definition == null)
            {
                lastError =
                    "Waiting for a celestial body definition.";
                return false;
            }

            if (!definition.HasValidPhysicalSettings)
            {
                lastError =
                    "The assigned body definition has invalid physical settings.";
                return false;
            }

            var orientation =
                ResolveBodyFrame();
            resolvedBodyCenter =
                orientation.position;

            if (surfaceFrameOverride != null &&
                surfaceFrameOverride
                    .TryGetPlanetCenterScenePosition(
                        out var surfaceFrameCenter))
            {
                resolvedBodyCenter =
                    surfaceFrameCenter;
            }
            bodyNorthDirection =
                orientation.TransformDirection(
                    definition.NorthAxis).normalized;
            bodyPoleReferenceDirection =
                orientation.TransformDirection(
                    definition.PoleReferenceAxis).normalized;
            return true;
        }

        private Transform ResolveBodyFrame()
        {
            if (bodyFrameOverride != null)
            {
                return bodyFrameOverride;
            }

            if (bodyContext != null &&
                bodyContext.VisualRoot != null)
            {
                return bodyContext.VisualRoot;
            }

            return
                bodyContext != null
                    ? bodyContext.transform
                    : transform;
        }

        private static bool TryResolveSurfaceType(
            Renderer targetRenderer,
            out bool isOcean)
        {
            isOcean =
                false;

            if (targetRenderer == null)
            {
                return false;
            }

            var material =
                targetRenderer.sharedMaterial;

            if (material == null)
            {
                return false;
            }

            var shaderName =
                material.shader != null
                    ? material.shader.name
                    : string.Empty;

            if (shaderName ==
                OceanShaderName)
            {
                isOcean =
                    true;
                return true;
            }

            if (shaderName ==
                TerrainShaderName)
            {
                return true;
            }

            if (!material.HasProperty(
                    SurfaceTypeId))
            {
                return false;
            }

            isOcean =
                material.GetFloat(
                    SurfaceTypeId) >=
                OceanSurfaceType;
            return true;
        }

        private void RefreshRendererCacheIfNeeded(
            bool force = false)
        {
            if (!force &&
                !rendererCacheDirty &&
                Time.unscaledTime <
                    nextRendererRefreshTime)
            {
                return;
            }

            rendererCache.Clear();
            rendererRoot.GetComponentsInChildren<Renderer>(
                true,
                rendererCache);
            cachedRendererCount =
                rendererCache.Count;
            nextRendererRefreshTime =
                Time.unscaledTime +
                rendererRefreshIntervalSeconds;
            rendererCacheDirty =
                false;
        }

        private void ResolveLocalReferences()
        {
            if (bodyContext == null)
            {
                bodyContext =
                    GetComponentInParent<
                        CelestialBodyRuntimeContext>();
            }

            if (surfaceFrameOverride == null)
            {
                surfaceFrameOverride =
                    GetComponentInChildren<
                        GePlanetSurfaceFrame>(
                        true);
            }
        }

        private void ResolveRendererRoot()
        {
            if (rendererRoot == null)
            {
                rendererRoot =
                    transform;
                rendererCacheDirty =
                    true;
            }
        }

        private void ClearBindingCounts()
        {
            hasValidShaderData =
                false;
            boundRendererCount =
                default;
            terrainRendererCount =
                default;
            oceanRendererCount =
                default;
        }
    }
}
