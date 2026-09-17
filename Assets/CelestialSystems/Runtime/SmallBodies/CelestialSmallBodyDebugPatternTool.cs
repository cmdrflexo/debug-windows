/*
 * Deliberately obvious small-body source for validating pool LODs and
 * multi-view impostor selection. It is not a production visual tool.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialSmallBodyDebugPatternTool :
        MonoBehaviour,
        ICelestialSmallBodyGenerationTool,
        ICelestialSmallBodyImpostorProvider
    {
        [SerializeField]
        private string toolId = "debug-color-cube";

        [SerializeField]
        [Range(1, 16)]
        [Tooltip("Horizontal camera views captured for the color cube.")]
        private int impostorViewCount = 8;

        [SerializeField]
        [Range(32, 1024)]
        private int impostorResolution = 256;

        [SerializeField]
        [Range(1, 8)]
        private int impostorVariantCount = 1;

        [SerializeField]
        private CelestialSmallBodyLod impostorSourceLod =
            CelestialSmallBodyLod.Lod2;

        [SerializeField]
        private CelestialSmallBodyImpostorMaps requestedImpostorMaps =
            CelestialSmallBodyImpostorMaps.AlbedoTransparency |
            CelestialSmallBodyImpostorMaps.Normal;

        [SerializeField]
        [Min(0.01f)]
        private float diameterMeters = 4.0f;

        private Mesh colorCubeMesh;
        private Mesh billboardMesh;
        private Material[] faceMaterials;
        private Material fallbackBillboardMaterial;

        public string ToolId => toolId;
        public int LodCount => 5;
        public int ImpostorVariantCount => impostorVariantCount;
        public int ImpostorResolution => impostorResolution;
        public int ImpostorViewCount => impostorViewCount;
        public CelestialSmallBodyLod ImpostorSourceLod => impostorSourceLod;
        public CelestialSmallBodyImpostorMaps RequestedImpostorMaps => requestedImpostorMaps;

        public uint GetImpostorVariantSeed(int variantIndex)
        {
            return (uint)(variantIndex + 1);
        }

        private void OnValidate()
        {
            toolId = toolId?.Trim() ?? string.Empty;
            impostorViewCount = Mathf.Clamp(impostorViewCount, 1, 16);
            impostorResolution = Mathf.Clamp(impostorResolution, 32, 1024);
            impostorVariantCount = Mathf.Clamp(impostorVariantCount, 1, 8);
            diameterMeters = Mathf.Max(0.01f, diameterMeters);
            impostorSourceLod = (CelestialSmallBodyLod)Mathf.Clamp(
                (int)impostorSourceLod,
                (int)CelestialSmallBodyLod.Lod0,
                (int)CelestialSmallBodyLod.Lod3);
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(colorCubeMesh);
            DestroyRuntimeObject(billboardMesh);
            DestroyRuntimeObject(fallbackBillboardMaterial);

            if (faceMaterials == null) return;

            foreach (var material in faceMaterials)
            {
                DestroyRuntimeObject(material);
            }
        }

        public bool CanGenerate(CelestialSmallBodyRequest request)
        {
            return string.Equals(request.ToolId, ToolId,
                StringComparison.Ordinal) &&
                (int)request.DesiredLod >= 0 &&
                (int)request.DesiredLod < LodCount;
        }

        public bool TryBeginGeneration(CelestialSmallBodyRequest request,
            Action<CelestialSmallBodyGenerationResult> completed)
        {
            if (!CanGenerate(request))
            {
                return false;
            }

            GameObject representation;

            if (request.DesiredLod == CelestialSmallBodyLod.Lod4)
            {
                representation = CreateBillboard(request);
            }
            else
            {
                representation = CreateColorCube(request);
            }

            completed?.Invoke(new CelestialSmallBodyGenerationResult(
                request,
                representation));
            return true;
        }

        private GameObject CreateColorCube(CelestialSmallBodyRequest request)
        {
            var body = new GameObject(
                $"Debug Color Cube {request.Seed} LOD {(int)request.DesiredLod}");
            body.AddComponent<MeshFilter>().sharedMesh = GetColorCubeMesh();
            body.AddComponent<MeshRenderer>().sharedMaterials = GetFaceMaterials();
            body.transform.localScale = Vector3.one * diameterMeters;
            return body;
        }

        private GameObject CreateBillboard(CelestialSmallBodyRequest request)
        {
            var body = new GameObject($"Debug Color Cube {request.Seed} Billboard");
            body.AddComponent<MeshFilter>().sharedMesh = GetBillboardMesh();
            body.AddComponent<MeshRenderer>().sharedMaterial =
                GetFallbackBillboardMaterial();
            body.AddComponent<CelestialSmallBodyBillboard>();
            body.transform.localScale = Vector3.one * diameterMeters;
            return body;
        }

        private Mesh GetColorCubeMesh()
        {
            if (colorCubeMesh != null) return colorCubeMesh;

            var vertices = new[]
            {
                // +X red
                new Vector3( .5f,-.5f,-.5f), new Vector3( .5f,-.5f, .5f), new Vector3( .5f, .5f, .5f), new Vector3( .5f, .5f,-.5f),
                // -X cyan
                new Vector3(-.5f,-.5f, .5f), new Vector3(-.5f,-.5f,-.5f), new Vector3(-.5f, .5f,-.5f), new Vector3(-.5f, .5f, .5f),
                // +Y green
                new Vector3(-.5f, .5f,-.5f), new Vector3( .5f, .5f,-.5f), new Vector3( .5f, .5f, .5f), new Vector3(-.5f, .5f, .5f),
                // -Y magenta
                new Vector3(-.5f,-.5f, .5f), new Vector3( .5f,-.5f, .5f), new Vector3( .5f,-.5f,-.5f), new Vector3(-.5f,-.5f,-.5f),
                // +Z blue
                new Vector3( .5f,-.5f, .5f), new Vector3(-.5f,-.5f, .5f), new Vector3(-.5f, .5f, .5f), new Vector3( .5f, .5f, .5f),
                // -Z yellow
                new Vector3(-.5f,-.5f,-.5f), new Vector3( .5f,-.5f,-.5f), new Vector3( .5f, .5f,-.5f), new Vector3(-.5f, .5f,-.5f)
            };

            colorCubeMesh = new Mesh { name = "Celestial Debug Color Cube" };
            colorCubeMesh.vertices = vertices;
            colorCubeMesh.subMeshCount = 6;

            for (var face = 0; face < 6; face++)
            {
                var baseIndex = face * 4;
                colorCubeMesh.SetTriangles(new[]
                {
                    baseIndex, baseIndex + 1, baseIndex + 2,
                    baseIndex, baseIndex + 2, baseIndex + 3
                }, face);
            }

            colorCubeMesh.RecalculateNormals();
            colorCubeMesh.RecalculateBounds();
            return colorCubeMesh;
        }

        private Mesh GetBillboardMesh()
        {
            if (billboardMesh != null) return billboardMesh;

            billboardMesh = new Mesh { name = "Celestial Debug Billboard" };
            billboardMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f)
            };
            billboardMesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            billboardMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            billboardMesh.RecalculateNormals();
            billboardMesh.RecalculateBounds();
            return billboardMesh;
        }

        private Material[] GetFaceMaterials()
        {
            if (faceMaterials != null) return faceMaterials;

            var colors = new[]
            {
                Color.red, Color.cyan, Color.green,
                Color.magenta, Color.blue, Color.yellow
            };
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Sprites/Default");
            faceMaterials = new Material[colors.Length];

            for (var index = 0; index < colors.Length; index++)
            {
                var material = new Material(shader)
                {
                    name = $"Debug Cube Face {index}"
                };
                material.SetColor("_BaseColor", colors[index]);
                material.SetColor("_Color", colors[index]);
                faceMaterials[index] = material;
            }

            return faceMaterials;
        }

        private Material GetFallbackBillboardMaterial()
        {
            if (fallbackBillboardMaterial != null) return fallbackBillboardMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Sprites/Default");
            fallbackBillboardMaterial = new Material(shader)
            {
                name = "Debug Billboard Fallback"
            };
            fallbackBillboardMaterial.SetColor("_BaseColor", Color.white);
            fallbackBillboardMaterial.SetColor("_Color", Color.white);
            return fallbackBillboardMaterial;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (value == null) return;

            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
