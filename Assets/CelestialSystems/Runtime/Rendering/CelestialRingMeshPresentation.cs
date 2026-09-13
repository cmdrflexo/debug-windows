/*
 * Builds a simple runtime annulus for every generated planetary-ring band.
 * This is the baseline presentation pass; a dedicated ring shader can replace
 * the temporary URP/Lit material without changing the generated mesh data.
 */

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
        private const string LitShaderName =
            "Universal Render Pipeline/Lit";
        private const int DefaultAngularSegments =
            256;
        private const int GradientTextureWidth =
            256;

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
        private readonly List<Texture2D> runtimeTextures =
            new List<Texture2D>();
        private CelestialBodyRuntimeContext body;

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
                    LitShaderName);

            if (shader == null)
            {
                return Fail(
                    $"Ring mesh presentation could not resolve shader '{LitShaderName}'.");
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
                var texture =
                    BakeGradient(
                        gradient);
                var material =
                    BuildMaterial(
                        shader,
                        texture,
                        definition.DefinitionId,
                        index);

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
                runtimeTextures.Add(
                    texture);
                runtimeMaterials.Add(
                    material);
            }

            generatedRingCount =
                bandCount;
            enabled = true;
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

        private static Texture2D BakeGradient(
            Gradient gradient)
        {
            var texture =
                new Texture2D(
                    GradientTextureWidth,
                    1,
                    TextureFormat.RGBA32,
                    false,
                    true)
                {
                    name =
                        "Celestial Ring Albedo (Runtime)",
                    wrapMode =
                        TextureWrapMode.Clamp,
                    filterMode =
                        FilterMode.Bilinear
                };
            var colors =
                new Color[GradientTextureWidth];

            for (var index = 0;
                index < colors.Length;
                index++)
            {
                var fraction =
                    (float)index /
                    (colors.Length - 1);
                colors[index] =
                    gradient != null
                        ? gradient.Evaluate(
                            fraction)
                        : Color.white;
            }

            texture.SetPixels(
                colors);
            texture.Apply(
                false,
                true);
            return texture;
        }

        private static Material BuildMaterial(
            Shader shader,
            Texture2D texture,
            string definitionId,
            int bandIndex)
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

            material.SetTexture(
                "_BaseMap",
                texture);
            material.SetColor(
                "_BaseColor",
                Color.white);
            material.SetFloat(
                "_Surface",
                1.0f);
            material.SetFloat(
                "_Blend",
                0.0f);
            material.SetFloat(
                "_ZWrite",
                0.0f);
            material.SetFloat(
                "_Cull",
                0.0f);
            material.SetFloat(
                "_AlphaClip",
                1.0f);
            material.SetFloat(
                "_Cutoff",
                0.01f);
            material.EnableKeyword(
                "_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword(
                "_ALPHATEST_ON");
            material.SetOverrideTag(
                "RenderType",
                "Transparent");
            return material;
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
                index < runtimeTextures.Count;
                index++)
            {
                if (runtimeTextures[index] != null)
                {
                    Destroy(
                        runtimeTextures[index]);
                }
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
            runtimeTextures.Clear();
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
