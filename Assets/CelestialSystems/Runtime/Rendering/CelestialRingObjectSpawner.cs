/*
 * Prototype close-range ring-object streamer. It queries only meter-scale
 * polar cells near the camera, proving deterministic placement and selection
 * before the same data is moved to GPU-instanced rendering.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialRingObjectSpawner :
        MonoBehaviour
    {
        [SerializeField]
        private CelestialRingObjectSet objectSet;

        [SerializeField]
        private Camera observerCamera;

        [Header("Prototype Cell Resolution")]
        [SerializeField]
        [Min(1.0f)]
        [Tooltip("Target radial and outer-edge arc length for one candidate cell.")]
        private float targetCellSizeMeters =
            100.0f;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float minimumPopulation =
            0.05f;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Temporary visibility control for placeholder meshes; leave at 1 for physical scale.")]
        private float prototypeVisualScaleMultiplier =
            1.0f;

        [SerializeField]
        [Tooltip("Disabled by default because this GameObject prototype is not the final shadow solution.")]
        private bool prototypeCastShadows;

        [Header("Streaming")]
        [SerializeField]
        [Min(1.0f)]
        private float streamingRadiusMeters =
            1000.0f;

        [SerializeField]
        [Min(0.05f)]
        private float refreshIntervalSeconds =
            0.25f;

        [Header("Debug Gizmos")]
        [SerializeField]
        [Tooltip("Draw a wire sphere and forward line for every active streamed ring object.")]
        private bool drawObjectGizmos;

        [Header("Runtime Diagnostics")]
        [SerializeField]
        private int activeObjectCount;

        [SerializeField]
        private int candidateCellCount;

        private readonly Dictionary<int, GameObject> activeObjects =
            new Dictionary<int, GameObject>();

        private readonly HashSet<int> refreshedKeys =
            new HashSet<int>();

        private readonly Stack<GameObject> pooledObjects =
            new Stack<GameObject>();

        // Created lazily because an already-instantiated component can survive
        // a Unity script hot reload with nonserialized runtime fields cleared.
        private MaterialPropertyBlock materialProperties;

        private CelestialBodyRuntimeContext body;
        private float nextRefreshTime;

        public int ActiveObjectCount =>
            activeObjectCount;

        public int CandidateCellCount =>
            candidateCellCount;

        /// <summary>
        /// Supplies scene-owned resources used by this generated spawner.
        /// A null camera remains valid and falls back to Camera.main at runtime.
        /// </summary>
        public void Configure(
            CelestialRingObjectSet newObjectSet,
            Camera newObserverCamera)
        {
            if (objectSet != newObjectSet)
            {
                ClearObjects();
            }

            objectSet = newObjectSet;
            observerCamera = newObserverCamera;
            nextRefreshTime = 0.0f;
        }

        private void Awake()
        {
            body =
                GetComponent<CelestialBodyRuntimeContext>();
        }

        private void OnEnable()
        {
            nextRefreshTime =
                0.0f;
        }

        private void OnDisable()
        {
            ClearObjects();
        }

        private void OnValidate()
        {
            targetCellSizeMeters =
                Mathf.Max(
                    1.0f,
                    targetCellSizeMeters);
            minimumPopulation =
                Mathf.Clamp01(
                    minimumPopulation);
            prototypeVisualScaleMultiplier =
                Mathf.Max(
                    0.01f,
                    prototypeVisualScaleMultiplier);
            streamingRadiusMeters =
                Mathf.Max(
                    1.0f,
                    streamingRadiusMeters);
            refreshIntervalSeconds =
                Mathf.Max(
                    0.05f,
                    refreshIntervalSeconds);
        }

        private void Update()
        {
            if (Time.time <
                nextRefreshTime)
            {
                return;
            }

            nextRefreshTime =
                Time.time +
                refreshIntervalSeconds;
            RefreshObjects();
        }

        private void RefreshObjects()
        {
            body ??=
                GetComponent<CelestialBodyRuntimeContext>();

            if (body == null ||
                body.Definition == null ||
                body.VisualRoot == null ||
                objectSet == null ||
                !body.Definition.HasRingSystemProperties)
            {
                ClearObjects();
                return;
            }

            var camera = observerCamera != null
                ? observerCamera
                : Camera.main;

            if (camera == null)
            {
                ClearObjects();
                return;
            }

            refreshedKeys.Clear();
            candidateCellCount = 0;
            var definition =
                body.Definition;
            var orientation =
                Quaternion.FromToRotation(
                    Vector3.up,
                    definition.NorthAxis.normalized);
            var cameraLocal =
                body.VisualRoot.InverseTransformPoint(
                    camera.transform.position);
            var bandCount =
                Mathf.Min(
                    definition.RingBandInnerRadiiMeters.Count,
                    definition.RingBandOuterRadiiMeters.Count);

            for (var bandIndex = 0;
                bandIndex < bandCount;
                bandIndex++)
            {
                RefreshNearbyBandCells(
                    definition,
                    camera,
                    orientation,
                    cameraLocal,
                    bandIndex);
            }

            RemoveStaleObjects();
            activeObjectCount =
                activeObjects.Count;
        }

        private void RefreshNearbyBandCells(
            CelestialBodyDefinition definition,
            Camera camera,
            Quaternion orientation,
            Vector3 cameraLocal,
            int bandIndex)
        {
            var innerRadius =
                definition.RingBandInnerRadiiMeters[
                    bandIndex];
            var outerRadius =
                definition.RingBandOuterRadiiMeters[
                    bandIndex];

            if (outerRadius <=
                    innerRadius ||
                outerRadius >
                    float.MaxValue)
            {
                return;
            }

            var bandWidth =
                (float)(outerRadius -
                    innerRadius);
            var radialCellCount =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        bandWidth /
                        targetCellSizeMeters));
            var radialStep =
                bandWidth /
                radialCellCount;
            var angularCellCount =
                Mathf.Max(
                    4,
                    Mathf.CeilToInt(
                        Mathf.PI *
                        2.0f *
                        (float)outerRadius /
                        targetCellSizeMeters));
            var ringSpaceCamera =
                Quaternion.Inverse(
                    orientation) *
                cameraLocal;
            var cameraRadius =
                new Vector2(
                    ringSpaceCamera.x,
                    ringSpaceCamera.z)
                    .magnitude;
            var cameraAngle =
                Mathf.Atan2(
                    ringSpaceCamera.z,
                    ringSpaceCamera.x);

            if (cameraAngle < 0.0f)
            {
                cameraAngle +=
                    Mathf.PI * 2.0f;
            }

            var radialCenter =
                Mathf.FloorToInt(
                    (cameraRadius -
                        (float)innerRadius) /
                    radialStep);
            var radialRange =
                Mathf.CeilToInt(
                    streamingRadiusMeters /
                    radialStep) + 1;
            var angularStep =
                Mathf.PI * 2.0f /
                angularCellCount;
            var angularCenter =
                Mathf.FloorToInt(
                    cameraAngle /
                    angularStep);
            var angularRange =
                Mathf.CeilToInt(
                    streamingRadiusMeters /
                    Mathf.Max(
                        cameraRadius,
                        (float)innerRadius) /
                    angularStep) + 1;
            var radialFirst =
                Mathf.Max(
                    0,
                    radialCenter -
                    radialRange);
            var radialLast =
                Mathf.Min(
                    radialCellCount - 1,
                    radialCenter +
                    radialRange);

            for (var radialIndex =
                    radialFirst;
                radialIndex <=
                    radialLast;
                radialIndex++)
            {
                var radiusFraction =
                    (radialIndex + 0.5f) /
                    radialCellCount;

                if (!CelestialRingSampling.TrySample(
                        definition,
                        bandIndex,
                        radiusFraction,
                        out var sample))
                {
                    continue;
                }

                for (var angularOffset =
                        -angularRange;
                    angularOffset <=
                        angularRange;
                    angularOffset++)
                {
                    var angularIndex =
                        WrapIndex(
                            angularCenter +
                            angularOffset,
                            angularCellCount);
                    var cell =
                        new CelestialRingPolarCell(
                            bandIndex,
                            radialIndex,
                            angularIndex);
                    var localPosition =
                        GetCellLocalPosition(
                            orientation,
                            sample,
                            cell,
                            angularCellCount);
                    var worldPosition =
                        body.VisualRoot.TransformPoint(
                            localPosition);

                    if ((worldPosition -
                            camera.transform.position)
                        .sqrMagnitude >
                        streamingRadiusMeters *
                        streamingRadiusMeters)
                    {
                        continue;
                    }

                    candidateCellCount++;
                    var occupancy =
                        CelestialRingSampling
                            .HashToUnitFloat(
                                CelestialRingSampling
                                    .GetStableCellHash(
                                        definition.GenerationSeed,
                                        definition.DefinitionId,
                                        cell,
                                        0u));

                    if (sample.Population <
                            minimumPopulation ||
                        occupancy >=
                            sample.Population ||
                        !objectSet.TrySelectFamily(
                            sample,
                            CelestialRingSampling
                                .HashToUnitFloat(
                                    CelestialRingSampling
                                        .GetStableCellHash(
                                            definition.GenerationSeed,
                                            definition.DefinitionId,
                                            cell,
                                            1u)),
                            out var family) ||
                        family == null)
                    {
                        continue;
                    }

                    var key =
                        GetCellKey(
                            cell,
                            radialCellCount,
                            angularCellCount);
                    refreshedKeys.Add(
                        key);
                    CreateOrRefreshObject(
                        key,
                        family,
                        sample,
                        definition,
                        cell,
                        localPosition);
                }
            }
        }

        private Vector3 GetCellLocalPosition(
            Quaternion orientation,
            CelestialRingSample sample,
            CelestialRingPolarCell cell,
            int angularCellCount)
        {
            var angle =
                (cell.AngularIndex +
                    CelestialRingSampling
                        .HashToUnitFloat(
                            CelestialRingSampling
                                .GetStableCellHash(
                                    body.Definition.GenerationSeed,
                                    body.Definition.DefinitionId,
                                    cell,
                                    4u))) /
                angularCellCount *
                Mathf.PI *
                2.0f;
            var direction =
                orientation *
                new Vector3(
                    Mathf.Cos(angle),
                    0.0f,
                    Mathf.Sin(angle));
            var verticalOffset =
                (CelestialRingSampling
                    .HashToUnitFloat(
                        CelestialRingSampling
                            .GetStableCellHash(
                                body.Definition.GenerationSeed,
                                body.Definition.DefinitionId,
                                cell,
                                5u)) - 0.5f) *
                sample.VerticalThicknessMeters;

            return direction *
                    (float)sample.RadiusMeters +
                orientation * Vector3.up *
                    verticalOffset;
        }

        private void CreateOrRefreshObject(
            int key,
            CelestialRingObjectFamily family,
            CelestialRingSample sample,
            CelestialBodyDefinition definition,
            CelestialRingPolarCell cell,
            Vector3 localPosition)
        {
            if (!activeObjects.TryGetValue(
                    key,
                    out var instance) ||
                instance == null)
            {
                instance =
                    AcquireObject();
                instance.name =
                    $"Ring Object {cell.BandIndex + 1}:{cell.RadialIndex}:{cell.AngularIndex}";
                instance.transform.SetParent(
                    body.VisualRoot,
                    false);
                instance.SetActive(
                    true);
                activeObjects[key] =
                    instance;
            }

            var filter =
                instance.GetComponent<MeshFilter>();
            var renderer =
                instance.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                renderer.shadowCastingMode =
                    prototypeCastShadows
                        ? ShadowCastingMode.On
                        : ShadowCastingMode.Off;
                renderer.receiveShadows =
                    prototypeCastShadows;
            }

            var diameter =
                Mathf.Lerp(
                    family.MinimumDiameterMeters,
                    family.MaximumDiameterMeters,
                    CelestialRingSampling
                        .HashToUnitFloat(
                            CelestialRingSampling
                                .GetStableCellHash(
                                    definition.GenerationSeed,
                                    definition.DefinitionId,
                                    cell,
                                    6u))) *
                Mathf.Max(
                    0.05f,
                    sample.ParticleScale) *
                prototypeVisualScaleMultiplier;
            var sourceRequest = new CelestialRingBodyRequest
            {
                Seed = CelestialRingSampling.GetStableCellHash(
                    definition.GenerationSeed,
                    definition.DefinitionId,
                    cell,
                    10u),
                FamilyKind = family.Kind,
                NominalDiameterMeters = diameter
            };
            var sourceBody = default(CelestialRingResolvedBody);
            var resolvedBySource =
                family.BodySource != null &&
                family.BodySource.TryResolve(
                    sourceRequest,
                    out sourceBody) &&
                sourceBody.HasVisual;
            var hasExplicitMaterial =
                resolvedBySource &&
                sourceBody.Material != null;

            if (filter != null)
            {
                filter.sharedMesh =
                    resolvedBySource &&
                    sourceBody.Mesh != null
                        ? sourceBody.Mesh
                        : family.MeshVariants.Count > 0
                            ? family.MeshVariants[
                                GetVariantIndex(
                                    definition,
                                    cell,
                                    2u,
                                    family.MeshVariants.Count)]
                            : CelestialRingProceduralMeshes
                                .GetMesh(
                                    family.Kind,
                                    sourceRequest.Seed);
            }

            if (renderer != null)
            {
                if (hasExplicitMaterial)
                {
                    renderer.sharedMaterial =
                        sourceBody.Material;
                }
                else if (family.MaterialVariants.Count > 0)
                {
                    renderer.sharedMaterial =
                        family.MaterialVariants[
                            GetVariantIndex(
                                definition,
                                cell,
                                3u,
                                family.MaterialVariants.Count)];
                    hasExplicitMaterial = true;
                }
            }

            if (resolvedBySource)
            {
                diameter *= sourceBody.SafeDiameterMultiplier;
            }
            instance.transform.localPosition =
                localPosition;
            instance.transform.localRotation =
                RandomRotation(
                    definition,
                    cell);
            instance.transform.localScale =
                Vector3.one *
                diameter;

            var debugGizmo =
                instance.GetComponent<CelestialRingObjectDebugGizmo>();
            if (debugGizmo == null)
            {
                debugGizmo =
                    instance.AddComponent<CelestialRingObjectDebugGizmo>();
            }

            debugGizmo.Configure(
                drawObjectGizmos,
                GetGizmoColor(family.Kind));

            if (renderer != null)
            {
                ConfigureMaterialProperties(
                    renderer,
                    family,
                    hasExplicitMaterial,
                    sample,
                    definition,
                    cell);
            }
        }

        private static Color GetGizmoColor(
            CelestialRingObjectFamilyKind kind)
        {
            return kind switch
            {
                CelestialRingObjectFamilyKind.FineIce =>
                    new Color(0.35f, 0.9f, 1.0f, 1.0f),
                CelestialRingObjectFamilyKind.IceChunk =>
                    new Color(0.2f, 0.55f, 1.0f, 1.0f),
                CelestialRingObjectFamilyKind.DarkRubble =>
                    new Color(0.8f, 0.55f, 0.25f, 1.0f),
                CelestialRingObjectFamilyKind.DustCluster =>
                    new Color(0.9f, 0.75f, 0.25f, 1.0f),
                CelestialRingObjectFamilyKind.LargeClump =>
                    new Color(1.0f, 0.35f, 0.7f, 1.0f),
                _ => Color.cyan
            };
        }

        private void ConfigureMaterialProperties(
            MeshRenderer renderer,
            CelestialRingObjectFamily family,
            bool hasExplicitMaterial,
            CelestialRingSample sample,
            CelestialBodyDefinition definition,
            CelestialRingPolarCell cell)
        {
            materialProperties ??=
                new MaterialPropertyBlock();
            materialProperties.Clear();

            if (!hasExplicitMaterial)
            {
                materialProperties.SetColor(
                    "_BaseColor",
                    sample.Albedo);
                materialProperties.SetColor(
                    "_Color",
                    sample.Albedo);
            }

            var material =
                renderer.sharedMaterial;

            if (material != null &&
                material.HasProperty(
                    "_ChunkSeed"))
            {
                materialProperties.SetFloat(
                    "_ChunkSeed",
                    CelestialRingSampling
                        .HashToUnitFloat(
                            CelestialRingSampling
                                .GetStableCellHash(
                                    definition.GenerationSeed,
                                    definition.DefinitionId,
                                    cell,
                                    11u)) *
                        4096.0f);
            }

            var isIceFamily =
                family.Kind ==
                    CelestialRingObjectFamilyKind.FineIce ||
                family.Kind ==
                    CelestialRingObjectFamilyKind.IceChunk;

            if (material != null &&
                isIceFamily)
            {
                if (material.HasProperty(
                        "_IceColor"))
                {
                    materialProperties.SetColor(
                        "_IceColor",
                        sample.Albedo);
                }

                if (material.HasProperty(
                        "_MineralAmount"))
                {
                    materialProperties.SetFloat(
                        "_MineralAmount",
                        Mathf.Clamp01(
                            sample.RockWeight *
                            0.8f));
                }

                if (material.HasProperty(
                        "_DirtAmount"))
                {
                    materialProperties.SetFloat(
                        "_DirtAmount",
                        Mathf.Clamp01(
                            sample.DustWeight *
                                0.75f +
                            sample.RockWeight *
                                0.10f));
                }
            }

            renderer.SetPropertyBlock(
                materialProperties);
        }

        private static Quaternion RandomRotation(
            CelestialBodyDefinition definition,
            CelestialRingPolarCell cell)
        {
            return Quaternion.Euler(
                CelestialRingSampling
                    .HashToUnitFloat(
                        CelestialRingSampling
                            .GetStableCellHash(
                                definition.GenerationSeed,
                                definition.DefinitionId,
                                cell,
                                7u)) * 360.0f,
                CelestialRingSampling
                    .HashToUnitFloat(
                        CelestialRingSampling
                            .GetStableCellHash(
                                definition.GenerationSeed,
                                definition.DefinitionId,
                                cell,
                                8u)) * 360.0f,
                CelestialRingSampling
                    .HashToUnitFloat(
                        CelestialRingSampling
                            .GetStableCellHash(
                                definition.GenerationSeed,
                                definition.DefinitionId,
                                cell,
                                9u)) * 360.0f);
        }

        private static int GetVariantIndex(
            CelestialBodyDefinition definition,
            CelestialRingPolarCell cell,
            uint stream,
            int count)
        {
            return count <= 1
                ? 0
                : Mathf.Min(
                    count - 1,
                    Mathf.FloorToInt(
                        CelestialRingSampling
                            .HashToUnitFloat(
                                CelestialRingSampling
                                    .GetStableCellHash(
                                        definition.GenerationSeed,
                                        definition.DefinitionId,
                                        cell,
                                        stream)) *
                        count));
        }

        private static int GetCellKey(
            CelestialRingPolarCell cell,
            int radialCellCount,
            int angularCellCount)
        {
            unchecked
            {
                var hash =
                    17;
                hash = hash * 31 +
                    cell.BandIndex;
                hash = hash * 31 +
                    radialCellCount;
                hash = hash * 31 +
                    angularCellCount;
                hash = hash * 31 +
                    cell.RadialIndex;
                return hash * 31 +
                    cell.AngularIndex;
            }
        }

        private static int WrapIndex(
            int index,
            int count)
        {
            var result =
                index % count;
            return result < 0
                ? result + count
                : result;
        }

        private void RemoveStaleObjects()
        {
            var staleKeys =
                new List<int>();

            foreach (var pair in activeObjects)
            {
                if (!refreshedKeys.Contains(
                        pair.Key))
                {
                    if (pair.Value != null)
                    {
                        pair.Value.SetActive(
                            false);
                        pooledObjects.Push(
                            pair.Value);
                    }

                    staleKeys.Add(
                        pair.Key);
                }
            }

            for (var index = 0;
                index < staleKeys.Count;
                index++)
            {
                activeObjects.Remove(
                    staleKeys[index]);
            }
        }

        private GameObject AcquireObject()
        {
            while (pooledObjects.Count > 0)
            {
                var pooled =
                    pooledObjects.Pop();

                if (pooled != null)
                {
                    return pooled;
                }
            }

            var instance =
                GameObject.CreatePrimitive(
                    PrimitiveType.Sphere);
            var collider =
                instance.GetComponent<Collider>();

            if (collider != null)
            {
                Destroy(collider);
            }

            return instance;
        }

        private void ClearObjects()
        {
            foreach (var pair in activeObjects)
            {
                if (pair.Value != null)
                {
                    Destroy(
                        pair.Value);
                }
            }

            while (pooledObjects.Count > 0)
            {
                var pooled =
                    pooledObjects.Pop();

                if (pooled != null)
                {
                    Destroy(
                        pooled);
                }
            }

            activeObjects.Clear();
            refreshedKeys.Clear();
            activeObjectCount = 0;
            candidateCellCount = 0;
        }
    }
}
