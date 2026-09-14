/*
 * Prototype close-range ring-object streamer. It proves deterministic cell
 * placement, family selection, and camera-local lifecycle before GPU instancing.
 */

using System.Collections.Generic;
using UnityEngine;

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
        [Range(1, 512)]
        private int radialCellCount =
            64;

        [SerializeField]
        [Range(4, 2048)]
        private int angularCellCount =
            256;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float minimumPopulation =
            0.05f;

        [Header("Streaming")]
        [SerializeField]
        [Min(1.0f)]
        private float streamingRadiusMeters =
            1000.0f;

        [SerializeField]
        [Min(0.05f)]
        private float refreshIntervalSeconds =
            0.25f;

        [Header("Runtime Diagnostics")]
        [SerializeField]
        private int activeObjectCount;

        [SerializeField]
        private int candidateCellCount;

        private readonly Dictionary<int, GameObject> activeObjects =
            new Dictionary<int, GameObject>();

        private readonly HashSet<int> refreshedKeys =
            new HashSet<int>();

        private CelestialBodyRuntimeContext body;
        private float nextRefreshTime;

        public int ActiveObjectCount =>
            activeObjectCount;

        public int CandidateCellCount =>
            candidateCellCount;

        private void Awake()
        {
            body =
                GetComponent<CelestialBodyRuntimeContext>();
        }

        private void OnEnable()
        {
            nextRefreshTime = 0.0f;
        }

        private void OnDisable()
        {
            ClearObjects();
        }

        private void OnValidate()
        {
            radialCellCount =
                Mathf.Clamp(radialCellCount, 1, 512);
            angularCellCount =
                Mathf.Clamp(angularCellCount, 4, 2048);
            minimumPopulation =
                Mathf.Clamp01(minimumPopulation);
            streamingRadiusMeters =
                Mathf.Max(1.0f, streamingRadiusMeters);
            refreshIntervalSeconds =
                Mathf.Max(0.05f, refreshIntervalSeconds);
        }

        private void Update()
        {
            if (Time.time < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime =
                Time.time + refreshIntervalSeconds;
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
            var definition = body.Definition;
            var orientation =
                Quaternion.FromToRotation(
                    Vector3.up,
                    definition.NorthAxis.normalized);
            var bandCount =
                Mathf.Min(
                    definition.RingBandInnerRadiiMeters.Count,
                    definition.RingBandOuterRadiiMeters.Count);

            for (var bandIndex = 0;
                bandIndex < bandCount;
                bandIndex++)
            {
                RefreshBand(
                    definition,
                    camera,
                    orientation,
                    bandIndex);
            }

            RemoveStaleObjects();
            activeObjectCount = activeObjects.Count;
        }

        private void RefreshBand(
            CelestialBodyDefinition definition,
            Camera camera,
            Quaternion orientation,
            int bandIndex)
        {
            for (var radialIndex = 0;
                radialIndex < radialCellCount;
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

                for (var angularIndex = 0;
                    angularIndex < angularCellCount;
                    angularIndex++)
                {
                    var cell =
                        new CelestialRingPolarCell(
                            bandIndex,
                            radialIndex,
                            angularIndex);
                    var localPosition =
                        GetCellLocalPosition(
                            orientation,
                            sample,
                            cell);
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
                    var occupancy = CelestialRingSampling
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
                            sample.Population)
                    {
                        continue;
                    }

                    if (!objectSet.TrySelectFamily(
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
                        GetCellKey(cell);
                    refreshedKeys.Add(key);
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
            CelestialRingPolarCell cell)
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
            var direction = orientation *
                new Vector3(
                    Mathf.Cos(angle),
                    0.0f,
                    Mathf.Sin(angle));
            var verticalOffset =
                (CelestialRingSampling.HashToUnitFloat(
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
                    GameObject.CreatePrimitive(
                        PrimitiveType.Sphere);
                instance.name =
                    $"Ring Object {cell.BandIndex + 1}:{cell.RadialIndex}:{cell.AngularIndex}";
                instance.transform.SetParent(
                    body.VisualRoot,
                    false);
                var collider =
                    instance.GetComponent<Collider>();

                if (collider != null)
                {
                    Destroy(collider);
                }

                activeObjects[key] = instance;
            }

            var meshIndex =
                GetVariantIndex(
                    definition,
                    cell,
                    2u,
                    family.MeshVariants.Count);
            var materialIndex =
                GetVariantIndex(
                    definition,
                    cell,
                    3u,
                    family.MaterialVariants.Count);
            var filter =
                instance.GetComponent<MeshFilter>();
            var renderer =
                instance.GetComponent<MeshRenderer>();

            if (filter != null &&
                family.MeshVariants.Count > 0)
            {
                filter.sharedMesh =
                    family.MeshVariants[meshIndex];
            }

            if (renderer != null &&
                family.MaterialVariants.Count > 0)
            {
                renderer.sharedMaterial =
                    family.MaterialVariants[materialIndex];
            }

            var diameter = Mathf.Lerp(
                family.MinimumDiameterMeters,
                family.MaximumDiameterMeters,
                CelestialRingSampling.HashToUnitFloat(
                    CelestialRingSampling
                        .GetStableCellHash(
                            definition.GenerationSeed,
                            definition.DefinitionId,
                            cell,
                            6u))) *
                Mathf.Max(0.05f, sample.ParticleScale);
            instance.transform.localPosition =
                localPosition;
            instance.transform.localRotation =
                RandomRotation(
                    definition,
                    cell);
            instance.transform.localScale =
                Vector3.one * diameter;

            if (renderer != null &&
                family.MaterialVariants.Count == 0)
            {
                var block =
                    new MaterialPropertyBlock();
                block.SetColor(
                    "_BaseColor",
                    sample.Albedo);
                block.SetColor(
                    "_Color",
                    sample.Albedo);
                renderer.SetPropertyBlock(block);
            }
        }

        private static Quaternion RandomRotation(
            CelestialBodyDefinition definition,
            CelestialRingPolarCell cell)
        {
            return Quaternion.Euler(
                CelestialRingSampling.HashToUnitFloat(
                    CelestialRingSampling.GetStableCellHash(
                        definition.GenerationSeed,
                        definition.DefinitionId,
                        cell,
                        7u)) * 360.0f,
                CelestialRingSampling.HashToUnitFloat(
                    CelestialRingSampling.GetStableCellHash(
                        definition.GenerationSeed,
                        definition.DefinitionId,
                        cell,
                        8u)) * 360.0f,
                CelestialRingSampling.HashToUnitFloat(
                    CelestialRingSampling.GetStableCellHash(
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
            CelestialRingPolarCell cell)
        {
            return cell.BandIndex *
                    1048576 +
                cell.RadialIndex *
                    2048 +
                cell.AngularIndex;
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
                        Destroy(pair.Value);
                    }

                    staleKeys.Add(pair.Key);
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

        private void ClearObjects()
        {
            foreach (var pair in activeObjects)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }

            activeObjects.Clear();
            refreshedKeys.Clear();
            activeObjectCount = 0;
            candidateCellCount = 0;
        }
    }
}
