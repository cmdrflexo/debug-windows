/*
 * Builds a simple runtime annulus for every generated planetary-ring band.
 * A dedicated two-sided ring shader evaluates the authored radial gradient and
 * curve keys from GPU buffers for both visible transparency and shadow coverage.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialRingMeshPresentation :
        MonoBehaviour
    {
        private const string GeneratedRootName =
            "Generated Rings";
        private const string RingShaderName =
            "jcan/Celestial Systems/Celestial Ring";
        private const int DefaultAngularSegments =
            256;

        private static readonly HashSet<CelestialRingMeshPresentation>
            activePresentations =
                new HashSet<CelestialRingMeshPresentation>();

        [SerializeField]
        [Min(16)]
        private int angularSegments =
            DefaultAngularSegments;

        [SerializeField]
        private Transform generatedRoot;

        [SerializeField]
        private int generatedRingCount;

        [SerializeField]
        private string lastError;

        private readonly List<Mesh> runtimeMeshes =
            new List<Mesh>();
        private readonly List<Material> runtimeMaterials =
            new List<Material>();
        private readonly List<GraphicsBuffer> runtimeDataBuffers =
            new List<GraphicsBuffer>();
        private CelestialBodyRuntimeContext body;

        public static IReadOnlyCollection<CelestialRingMeshPresentation>
            ActivePresentations =>
                activePresentations;

        public CelestialBodyRuntimeContext Body =>
            body;

        public int GeneratedRingCount =>
            generatedRingCount;

        public string LastError =>
            lastError;

        public bool Initialize(
            CelestialBodyRuntimeContext newBody)
        {
            body = newBody;
            lastError = string.Empty;

            ReleaseRuntimeResources();
            ClearGeneratedChildren();

            if (body == null ||
                body.Definition == null ||
                body.VisualRoot == null)
            {
                return Fail(
                    "Ring mesh presentation requires an initialized body runtime context.");
            }

            var definition =
                body.Definition;

            if (!definition.HasRingSystemProperties)
            {
                generatedRingCount = 0;
                enabled = false;
                return true;
            }

            var innerRadii =
                definition.RingBandInnerRadiiMeters;
            var outerRadii =
                definition.RingBandOuterRadiiMeters;
            var bandCount =
                Mathf.Min(
                    innerRadii.Count,
                    outerRadii.Count);

            if (bandCount < 1)
            {
                return Fail(
                    "Ring-system properties contain no valid ring bands.");
            }

            var shader =
                Shader.Find(
                    RingShaderName);

            if (shader == null)
            {
                return Fail(
                    $"Ring mesh presentation could not resolve shader '{RingShaderName}'.");
            }

            generatedRoot =
                FindOrCreateDirectChild(
                    body.VisualRoot,
                    GeneratedRootName);
            generatedRoot.localPosition =
                Vector3.zero;
            generatedRoot.localRotation =
                Quaternion.FromToRotation(
                    Vector3.up,
                    ResolveNorthAxis(
                        definition.NorthAxis));
            generatedRoot.localScale =
                Vector3.one;

            for (var index = 0;
                index < bandCount;
                index++)
            {
                var innerRadius =
                    innerRadii[index];
                var outerRadius =
                    outerRadii[index];

                if (!IsFinitePositive(innerRadius) ||
                    !IsFinitePositive(outerRadius) ||
                    outerRadius <= innerRadius ||
                    outerRadius > float.MaxValue)
                {
                    ReleaseRuntimeResources();
                    ClearGeneratedChildren();
                    return Fail(
                        $"Ring band {index} has invalid inner or outer radii.");
                }

                var gradient =
                    index <
                        definition.RingAlbedoGradients.Count
                        ? definition.RingAlbedoGradients[index]
                        : null;
                var mesh =
                    BuildAnnulus(
                        innerRadius,
                        outerRadius,
                        Mathf.Max(
                            16,
                            angularSegments));
                var density =
                    index <
                        definition.RingDensityGradients.Count
                        ? definition.RingDensityGradients[index]
                        : null;
                var population =
                    index <
                        definition.RingPopulationGradients.Count
                        ? definition.RingPopulationGradients[index]
                        : null;
                var material =
                    BuildMaterial(
                        shader,
                        gradient,
                        density,
                        population,
                        definition.DefinitionId,
                        index,
                        out var dataBuffers);

                var ringObject =
                    new GameObject(
                        $"Ring Band {index + 1}");
                ringObject.transform.SetParent(
                    generatedRoot,
                    false);

                var filter =
                    ringObject.AddComponent<MeshFilter>();
                filter.sharedMesh =
                    mesh;

                var renderer =
                    ringObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial =
                    material;
                renderer.shadowCastingMode =
                    ShadowCastingMode.TwoSided;
                renderer.receiveShadows =
                    true;

                runtimeMeshes.Add(
                    mesh);
                runtimeDataBuffers.AddRange(
                    dataBuffers);
                runtimeMaterials.Add(
                    material);
            }

            generatedRingCount =
                bandCount;
            enabled = true;
            return true;
        }

        private void OnEnable()
        {
            activePresentations.Add(this);
        }

        private void OnDisable()
        {
            activePresentations.Remove(this);
        }

        // Reports distance to the actual annulus geometry in scene metres. This
        // intentionally does not use renderer bounds, whose square footprint
        // makes navigation speed jump outside a ring's circular edge.
        public bool TryGetNearestSurfaceDistance(
            Vector3 scenePosition,
            out double distanceMeters,
            out double characteristicScaleMeters)
        {
            distanceMeters = default;
            characteristicScaleMeters = default;

            if (generatedRoot == null || body == null || body.Definition == null ||
                generatedRingCount < 1)
            {
                return false;
            }

            var local = generatedRoot.InverseTransformPoint(scenePosition);
            var radialDistance = Math.Sqrt(
                local.x * local.x + local.z * local.z);
            var verticalDistance = Math.Abs(local.y);
            var nearestDistance = double.PositiveInfinity;
            var largestOuterRadius = 0.0;
            var innerRadii = body.Definition.RingBandInnerRadiiMeters;
            var outerRadii = body.Definition.RingBandOuterRadiiMeters;
            var bandCount = Mathf.Min(innerRadii.Count, outerRadii.Count);

            for (var index = 0; index < bandCount; index++)
            {
                var innerRadius = innerRadii[index];
                var outerRadius = outerRadii[index];
                if (!IsFinitePositive(innerRadius) || !IsFinitePositive(outerRadius) ||
                    outerRadius < innerRadius)
                {
                    continue;
                }

                largestOuterRadius = Math.Max(largestOuterRadius, outerRadius);
                var radialOffset = radialDistance < innerRadius
                    ? innerRadius - radialDistance
                    : radialDistance > outerRadius
                        ? radialDistance - outerRadius
                        : 0.0;
                var candidateDistance = Math.Sqrt(
                    radialOffset * radialOffset +
                    verticalDistance * verticalDistance);
                nearestDistance = Math.Min(nearestDistance, candidateDistance);
            }

            if (double.IsNaN(nearestDistance) ||
                double.IsInfinity(nearestDistance) ||
                largestOuterRadius <= 0.0)
            {
                return false;
            }

            distanceMeters = nearestDistance;
            characteristicScaleMeters = largestOuterRadius;
            return true;
        }

        private void OnDestroy()
        {
            ReleaseRuntimeResources();
        }

        private void OnValidate()
        {
            angularSegments =
                Mathf.Max(
                    16,
                    angularSegments);
        }

        private static Mesh BuildAnnulus(
            double innerRadiusMeters,
            double outerRadiusMeters,
            int segments)
        {
            var vertices =
                new Vector3[(segments + 1) * 2];
            var normals =
                new Vector3[vertices.Length];
            var tangents =
                new Vector4[vertices.Length];
            var uvs =
                new Vector2[vertices.Length];
            var triangles =
                new int[segments * 6];
            var innerRadius =
                (float)innerRadiusMeters;
            var outerRadius =
                (float)outerRadiusMeters;

            for (var segment = 0;
                segment <= segments;
                segment++)
            {
                var fraction =
                    (float)segment /
                    segments;
                var angle =
                    fraction *
                    Mathf.PI *
                    2.0f;
                var direction =
                    new Vector3(
                        Mathf.Cos(angle),
                        0.0f,
                        Mathf.Sin(angle));
                var innerIndex =
                    segment * 2;
                var outerIndex =
                    innerIndex + 1;

                vertices[innerIndex] =
                    direction *
                    innerRadius;
                vertices[outerIndex] =
                    direction *
                    outerRadius;
                normals[innerIndex] =
                    Vector3.up;
                normals[outerIndex] =
                    Vector3.up;
                tangents[innerIndex] =
                    new Vector4(
                        -direction.z,
                        0.0f,
                        direction.x,
                        1.0f);
                tangents[outerIndex] =
                    tangents[innerIndex];
                uvs[innerIndex] =
                    new Vector2(
                        0.0f,
                        fraction);
                uvs[outerIndex] =
                    new Vector2(
                        1.0f,
                        fraction);
            }

            for (var segment = 0;
                segment < segments;
                segment++)
            {
                var vertex =
                    segment * 2;
                var triangle =
                    segment * 6;

                triangles[triangle] =
                    vertex;
                triangles[triangle + 1] =
                    vertex + 3;
                triangles[triangle + 2] =
                    vertex + 1;
                triangles[triangle + 3] =
                    vertex;
                triangles[triangle + 4] =
                    vertex + 2;
                triangles[triangle + 5] =
                    vertex + 3;
            }

            var mesh =
                new Mesh
                {
                    name =
                        "Celestial Ring Annulus (Runtime)",
                    indexFormat =
                        vertices.Length > 65535
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16,
                    vertices =
                        vertices,
                    normals =
                        normals,
                    tangents =
                        tangents,
                    uv =
                        uvs,
                    triangles =
                        triangles
                };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material BuildMaterial(
            Shader shader,
            Gradient albedo,
            AnimationCurve density,
            AnimationCurve population,
            string definitionId,
            int bandIndex,
            out GraphicsBuffer[] dataBuffers)
        {
            var material =
                new Material(
                    shader)
                {
                    name =
                        $"{definitionId} Ring {bandIndex + 1} (Runtime)",
                    renderQueue =
                        (int)RenderQueue.Transparent
                };

            var albedoBuffer =
                CreateAlbedoBuffer(
                    albedo,
                    out var albedoKeyCount);
            var densityBuffer =
                CreateCurveBuffer(
                    density,
                    1.0f,
                    out var densityKeyCount);
            var populationBuffer =
                CreateCurveBuffer(
                    population,
                    1.0f,
                    out var populationKeyCount);
            dataBuffers =
                new[]
                {
                    albedoBuffer,
                    densityBuffer,
                    populationBuffer
                };

            material.SetBuffer(
                "_RingAlbedoKeys",
                albedoBuffer);
            material.SetInt(
                "_RingAlbedoKeyCount",
                albedoKeyCount);
            material.SetBuffer(
                "_RingDensityKeys",
                densityBuffer);
            material.SetInt(
                "_RingDensityKeyCount",
                densityKeyCount);
            material.SetBuffer(
                "_RingPopulationKeys",
                populationBuffer);
            material.SetInt(
                "_RingPopulationKeyCount",
                populationKeyCount);
            material.SetFloat(
                "_Opacity",
                1.0f);
            material.SetFloat(
                "_DensityCutoff",
                0.005f);
            material.SetFloat(
                "_AmbientStrength",
                0.2f);
            return material;
        }

        private static GraphicsBuffer CreateAlbedoBuffer(
            Gradient gradient,
            out int keyCount)
        {
            var keys =
                gradient != null &&
                gradient.colorKeys != null &&
                gradient.colorKeys.Length > 0
                    ? gradient.colorKeys
                    : new[]
                    {
                        new GradientColorKey(
                            Color.white,
                            0.0f),
                        new GradientColorKey(
                            Color.white,
                            1.0f)
                    };
            var data =
                new Vector4[keys.Length];

            for (var index = 0;
                index < keys.Length;
                index++)
            {
                data[index] =
                    new Vector4(
                        keys[index].time,
                        keys[index].color.r,
                        keys[index].color.g,
                        keys[index].color.b);
            }

            keyCount =
                data.Length;
            return CreateDataBuffer(
                data);
        }

        private static GraphicsBuffer CreateCurveBuffer(
            AnimationCurve curve,
            float fallback,
            out int keyCount)
        {
            var keys =
                curve != null &&
                curve.length > 0
                    ? curve.keys
                    : new[]
                    {
                        new Keyframe(
                            0.0f,
                            fallback),
                        new Keyframe(
                            1.0f,
                            fallback)
                    };
            var data =
                new Vector4[keys.Length];

            for (var index = 0;
                index < keys.Length;
                index++)
            {
                data[index] =
                    new Vector4(
                        keys[index].time,
                        keys[index].value,
                        keys[index].inTangent,
                        keys[index].outTangent);
            }

            keyCount =
                data.Length;
            return CreateDataBuffer(
                data);
        }

        private static GraphicsBuffer CreateDataBuffer(
            Vector4[] data)
        {
            var buffer =
                new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    Mathf.Max(
                        1,
                        data.Length),
                    sizeof(float) * 4);
            buffer.SetData(
                data);
            return buffer;
        }

        private void ClearGeneratedChildren()
        {
            if (generatedRoot == null &&
                body != null &&
                body.VisualRoot != null)
            {
                generatedRoot =
                    FindDirectChild(
                        body.VisualRoot,
                        GeneratedRootName);
            }

            if (generatedRoot == null)
            {
                return;
            }

            for (var index =
                generatedRoot.childCount - 1;
                index >= 0;
                index--)
            {
                Destroy(
                    generatedRoot.GetChild(
                        index).gameObject);
            }
        }

        private void ReleaseRuntimeResources()
        {
            for (var index = 0;
                index < runtimeMaterials.Count;
                index++)
            {
                if (runtimeMaterials[index] != null)
                {
                    Destroy(
                        runtimeMaterials[index]);
                }
            }

            for (var index = 0;
                index < runtimeDataBuffers.Count;
                index++)
            {
                runtimeDataBuffers[index]?.Release();
            }

            for (var index = 0;
                index < runtimeMeshes.Count;
                index++)
            {
                if (runtimeMeshes[index] != null)
                {
                    Destroy(
                        runtimeMeshes[index]);
                }
            }

            runtimeMaterials.Clear();
            runtimeDataBuffers.Clear();
            runtimeMeshes.Clear();
            generatedRingCount = 0;
        }

        private bool Fail(
            string error)
        {
            lastError = error;
            enabled = false;
            Debug.LogError(
                error,
                this);
            return false;
        }

        private static Vector3 ResolveNorthAxis(
            Vector3 northAxis)
        {
            return northAxis.sqrMagnitude >
                0.000001f
                    ? northAxis.normalized
                    : Vector3.up;
        }

        private static bool IsFinitePositive(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
        }

        private static Transform FindDirectChild(
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

            return null;
        }

        private static Transform FindOrCreateDirectChild(
            Transform parent,
            string childName)
        {
            var existing =
                FindDirectChild(
                    parent,
                    childName);

            if (existing != null)
            {
                return existing;
            }

            var childObject =
                new GameObject(
                    childName);
            childObject.transform.SetParent(
                parent,
                false);
            return childObject.transform;
        }
    }
}
