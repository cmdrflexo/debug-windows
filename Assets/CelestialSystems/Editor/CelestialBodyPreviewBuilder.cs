/*
 * Evaluates a round MapMagic graph in physical planetary coordinates and builds a disposable editor-only cube-sphere preview.
 */

using System;
using System.Collections.Generic;
using Den.Tools;
using Den.Tools.Matrices;
using MapMagic.Core;
using MapMagic.Nodes.MatrixGenerators;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems.Editor
{
    [InitializeOnLoad]
    internal static class CelestialBodyPreviewBuilder
    {
        private const int MaximumLayerCount =
            4;

        private const int GenerationMargins =
            2;

        private const string PreviewRootName =
            "[Celestial Systems] Body Preview";

        private const string PreviewResourcePrefix =
            "Celestial Body Preview ";

        private const string DefaultLayerShaderName =
            "jcan/Celestial Systems/Curved MapMagic Terrain Layers";

        private sealed class FaceGenerationData
        {
            public CubeSphereFace Face;
            public int Resolution;
            public float[] NormalizedHeights;
            public float HeightScaleMeters;
            public float[,,] ControlWeights;
            public TerrainLayer[] TerrainLayers;
        }

        private static readonly CubeSphereFace[] Faces =
        {
            CubeSphereFace.PositiveX,
            CubeSphereFace.NegativeX,
            CubeSphereFace.PositiveY,
            CubeSphereFace.NegativeY,
            CubeSphereFace.PositiveZ,
            CubeSphereFace.NegativeZ
        };

        static CelestialBodyPreviewBuilder()
        {
            EditorApplication.playModeStateChanged +=
                HandlePlayModeStateChanged;
        }

        public static bool HasPreview =>
            FindPreview() != null;

        public static GameObject Generate(
            CelestialBodyDefinition bodyDefinition,
            RoundMapMagicSurfaceDefinition surfaceDefinition,
            Vector3 position,
            float previewDiameter,
            int meshResolution,
            int graphResolution,
            float heightMultiplier,
            bool showOcean)
        {
            if (bodyDefinition == null)
            {
                throw new ArgumentNullException(
                    nameof(bodyDefinition));
            }

            if (surfaceDefinition == null)
            {
                throw new ArgumentNullException(
                    nameof(surfaceDefinition));
            }

            var graph =
                surfaceDefinition.Graph;

            if (graph == null)
            {
                throw new InvalidOperationException(
                    "The surface definition does not have a MapMagic graph.");
            }

            var planetRadiusMeters =
                bodyDefinition.ReferenceRadiusMeters;

            if (!IsFinite(
                    planetRadiusMeters) ||
                planetRadiusMeters <=
                    0.0 ||
                !IsFinite(
                    previewDiameter) ||
                previewDiameter <=
                    0.0f ||
                !IsFinite(
                    heightMultiplier) ||
                heightMultiplier <
                    0.0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(previewDiameter),
                    "The preview requires positive body and display sizes with a non-negative height multiplier.");
            }

            var resolvedMeshResolution =
                Mathf.Clamp(
                    meshResolution,
                    3,
                    257);
            var resolvedGraphResolution =
                Mathf.Clamp(
                    graphResolution,
                    2,
                    513);
            var faceData =
                GenerateFaces(
                    graph,
                    surfaceDefinition.SurfaceSeed,
                    planetRadiusMeters,
                    resolvedGraphResolution);
            var previousPreview =
                FindPreview();
            var preview =
                new GameObject(
                    PreviewRootName +
                    " (Building)")
                {
                    tag =
                        "EditorOnly",
                    hideFlags =
                        HideFlags.DontSaveInBuild
                };
            preview.transform.SetPositionAndRotation(
                position,
                Quaternion.identity);
            preview.transform.localScale =
                Vector3.one;

            try
            {
                BuildPreview(
                    preview,
                    faceData,
                    surfaceDefinition,
                    bodyDefinition.OceanDefinition,
                    planetRadiusMeters,
                    previewDiameter,
                    resolvedMeshResolution,
                    heightMultiplier,
                    showOcean);
            }
            catch
            {
                DestroyPreviewObject(
                    preview);
                throw;
            }

            if (previousPreview != null)
            {
                DestroyPreviewObject(
                    previousPreview);
            }

            preview.name =
                PreviewRootName;
            Undo.RegisterCreatedObjectUndo(
                preview,
                "Generate Celestial Body Preview");
            EditorSceneManager.MarkSceneDirty(
                preview.scene);
            return preview;
        }

        public static void DeletePreview()
        {
            DeletePreviewInternal();
        }

        private static FaceGenerationData[] GenerateFaces(
            MapMagic.Nodes.Graph graph,
            int surfaceSeed,
            double planetRadiusMeters,
            int resolution)
        {
            var generationSource =
                ResolveGenerationSource(
                    graph,
                    out var temporarySourceObject);
            var results =
                new FaceGenerationData[
                    Faces.Length];

            try
            {
                for (var index = 0;
                    index < Faces.Length;
                    index++)
                {
                    EditorUtility.DisplayProgressBar(
                        "Generating Celestial Body Preview",
                        $"Evaluating {Faces[index]} ({index + 1}/{Faces.Length})",
                        index /
                            (float)Faces.Length);
                    results[index] =
                        GenerateFace(
                            graph,
                            generationSource,
                            Faces[index],
                            surfaceSeed,
                            planetRadiusMeters,
                            resolution);

                    if (index > 0)
                    {
                        ValidateFaceCompatibility(
                            results[0],
                            results[index]);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                if (temporarySourceObject !=
                    null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        temporarySourceObject);
                }
            }

            return results;
        }

        private static FaceGenerationData GenerateFace(
            MapMagic.Nodes.Graph graph,
            MapMagicObject generationSource,
            CubeSphereFace face,
            int surfaceSeed,
            double planetRadiusMeters,
            int resolution)
        {
            var halfFaceSizeMeters =
                CubeSphereMapping.FaceCoordinateToMeters(
                    1.0,
                    planetRadiusMeters);
            var faceSizeMeters =
                halfFaceSizeMeters *
                2.0;
            var area =
                new Area(
                    new Vector2D(
                        (float)-halfFaceSizeMeters,
                        (float)-halfFaceSizeMeters),
                    new Vector2D(
                        (float)faceSizeMeters,
                        (float)faceSizeMeters),
                    resolution,
                    GenerationMargins);
            var data =
                new RoundMapMagicSphericalTileData
                {
                    Face =
                        face,
                    PlanetRadiusMeters =
                        planetRadiusMeters,
                    SurfaceSeed =
                        surfaceSeed,
                    MapWorldOriginXMeters =
                        -halfFaceSizeMeters,
                    MapWorldOriginZMeters =
                        -halfFaceSizeMeters,
                    MapWorldSizeXMeters =
                        faceSizeMeters,
                    MapWorldSizeZMeters =
                        faceSizeMeters,
                    area =
                        area,
                    globals =
                        generationSource.globals,
                    random =
                        graph.random,
                    isPreview =
                        false,
                    isDraft =
                        true
                };
            var stop =
                new StopToken();

            try
            {
                graph.Prepare(
                    data,
                    null);
                graph.Generate(
                    data,
                    stop);
                graph.Finalize(
                    data,
                    stop);

                if (stop.stop)
                {
                    throw new InvalidOperationException(
                        $"MapMagic stopped the {face} preview request.");
                }

                if (data.heights == null)
                {
                    throw new InvalidOperationException(
                        $"The MapMagic graph did not produce a finalized Height output for {face}.");
                }

                var heightScaleMeters =
                    data.heights.worldSize.y;

                if (!IsFinite(
                        heightScaleMeters) ||
                    heightScaleMeters <=
                        0.0f)
                {
                    throw new InvalidOperationException(
                        $"The MapMagic graph produced an invalid height scale for {face}.");
                }

                var result =
                    new FaceGenerationData
                    {
                        Face =
                            face,
                        Resolution =
                            resolution,
                        NormalizedHeights =
                            CopyActiveHeights(
                                data,
                                resolution),
                        HeightScaleMeters =
                            heightScaleMeters
                    };
                CaptureTextureData(
                    result,
                    data.ApplyOfType<
                        TexturesOutput200.ApplyData>(),
                    resolution);
                return result;
            }
            finally
            {
                data.Clear(
                    clearApply: true,
                    inSubs: true);
            }
        }

        private static float[] CopyActiveHeights(
            RoundMapMagicSphericalTileData data,
            int resolution)
        {
            var heights =
                new float[
                    resolution *
                    resolution];

            for (var z = 0;
                z < resolution;
                z++)
            {
                var pixelZ =
                    data.area.active.rect.offset.z +
                    z;

                for (var x = 0;
                    x < resolution;
                    x++)
                {
                    var pixelX =
                        data.area.active.rect.offset.x +
                        x;
                    heights[
                        z *
                        resolution +
                        x] =
                        data.heights[
                            pixelX,
                            pixelZ];
                }
            }

            return heights;
        }

        private static void CaptureTextureData(
            FaceGenerationData result,
            TexturesOutput200.ApplyData textureData,
            int resolution)
        {
            if (textureData == null ||
                textureData.splats == null ||
                textureData.prototypes == null ||
                textureData.prototypes.Length == 0)
            {
                return;
            }

            var layerCount =
                textureData.prototypes.Length;

            if (layerCount >
                MaximumLayerCount)
            {
                throw new InvalidOperationException(
                    $"The preview supports up to {MaximumLayerCount} terrain layers, but the graph produced {layerCount}.");
            }

            if (textureData.splats.GetLength(0) !=
                    resolution ||
                textureData.splats.GetLength(1) !=
                    resolution ||
                textureData.splats.GetLength(2) !=
                    layerCount)
            {
                throw new InvalidOperationException(
                    "The MapMagic texture-control dimensions do not match the preview request.");
            }

            result.ControlWeights =
                textureData.splats;
            result.TerrainLayers =
                textureData.prototypes;
        }

        private static void ValidateFaceCompatibility(
            FaceGenerationData first,
            FaceGenerationData current)
        {
            if (!Approximately(
                    first.HeightScaleMeters,
                    current.HeightScaleMeters))
            {
                throw new InvalidOperationException(
                    "The MapMagic graph produced different height scales across spherical faces.");
            }

            var firstHasTextures =
                first.ControlWeights != null &&
                first.TerrainLayers != null;
            var currentHasTextures =
                current.ControlWeights != null &&
                current.TerrainLayers != null;

            if (firstHasTextures !=
                currentHasTextures)
            {
                throw new InvalidOperationException(
                    "The MapMagic graph produced inconsistent texture outputs across spherical faces.");
            }

            if (firstHasTextures &&
                !TerrainLayersMatch(
                    first.TerrainLayers,
                    current.TerrainLayers))
            {
                throw new InvalidOperationException(
                    "The MapMagic graph produced different terrain layers across spherical faces.");
            }
        }

        private static bool TerrainLayersMatch(
            TerrainLayer[] first,
            TerrainLayer[] second)
        {
            if (first == null ||
                second == null ||
                first.Length !=
                    second.Length)
            {
                return false;
            }

            for (var index = 0;
                index < first.Length;
                index++)
            {
                if (first[index] !=
                    second[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static MapMagicObject ResolveGenerationSource(
            MapMagic.Nodes.Graph graph,
            out GameObject temporarySourceObject)
        {
            var sceneSources =
                Resources.FindObjectsOfTypeAll<
                    MapMagicObject>();

            for (var index = 0;
                index < sceneSources.Length;
                index++)
            {
                var source =
                    sceneSources[index];

                if (source == null ||
                    source.graph !=
                        graph ||
                    EditorUtility.IsPersistent(
                        source) ||
                    !source.gameObject.scene.IsValid())
                {
                    continue;
                }

                temporarySourceObject =
                    null;
                return source;
            }

            temporarySourceObject =
                new GameObject(
                    PreviewResourcePrefix +
                    "MapMagic Source")
                {
                    hideFlags =
                        HideFlags.HideAndDontSave
                };
            temporarySourceObject.SetActive(
                false);
            var temporarySource =
                temporarySourceObject.AddComponent<
                    MapMagicObject>();
            temporarySource.graph =
                graph;
            return temporarySource;
        }

        private static void BuildPreview(
            GameObject preview,
            FaceGenerationData[] faceData,
            RoundMapMagicSurfaceDefinition surfaceDefinition,
            OceanDefinition oceanDefinition,
            double planetRadiusMeters,
            float previewDiameter,
            int meshResolution,
            float heightMultiplier,
            bool showOcean)
        {
            var displayScale =
                previewDiameter /
                (planetRadiusMeters *
                    2.0);
            var previewRadius =
                previewDiameter *
                0.5f;

            for (var index = 0;
                index < faceData.Length;
                index++)
            {
                var data =
                    faceData[index];
                var child =
                    new GameObject(
                        $"Preview Surface {data.Face}")
                    {
                        tag =
                            "EditorOnly",
                        hideFlags =
                            HideFlags.DontSaveInBuild
                    };
                child.transform.SetParent(
                    preview.transform,
                    false);
                PositionFace(
                    child.transform,
                    data.Face,
                    previewRadius);

                var meshFilter =
                    child.AddComponent<MeshFilter>();
                var meshRenderer =
                    child.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode =
                    ShadowCastingMode.Off;
                meshRenderer.receiveShadows =
                    false;

                var mesh =
                    CreateFaceMesh(
                        data,
                        meshResolution,
                        planetRadiusMeters,
                        surfaceDefinition.ElevationOffsetMeters,
                        displayScale,
                        heightMultiplier);
                meshFilter.sharedMesh =
                    mesh;
                var material =
                    CreateFaceMaterial(
                        data,
                        surfaceDefinition.Material,
                        planetRadiusMeters);
                meshRenderer.sharedMaterial =
                    material;
            }

            if (showOcean)
            {
                BuildOcean(
                    preview,
                    oceanDefinition,
                    planetRadiusMeters,
                    previewDiameter,
                    meshResolution);
            }
        }

        private static void BuildOcean(
            GameObject preview,
            OceanDefinition oceanDefinition,
            double planetRadiusMeters,
            float previewDiameter,
            int meshResolution)
        {
            if (oceanDefinition == null)
            {
                return;
            }

            if (!oceanDefinition.HasValidSettings)
            {
                throw new InvalidOperationException(
                    "The body's ocean definition requires a material and a finite global surface elevation.");
            }

            var oceanRadiusMeters =
                planetRadiusMeters +
                oceanDefinition.GlobalSurfaceElevationMeters;

            if (!IsFinite(
                    oceanRadiusMeters) ||
                oceanRadiusMeters <=
                    0.0)
            {
                throw new InvalidOperationException(
                    "The ocean's global surface elevation produces a non-positive or non-finite radius.");
            }

            var displayScale =
                previewDiameter /
                (planetRadiusMeters *
                    2.0);
            var previewRadius =
                previewDiameter *
                0.5f;
            var oceanRoot =
                new GameObject(
                    "Preview Ocean")
                {
                    tag =
                        "EditorOnly",
                    hideFlags =
                        HideFlags.DontSaveInBuild
                };
            oceanRoot.transform.SetParent(
                preview.transform,
                false);
            oceanRoot.transform.localPosition =
                Vector3.zero;
            oceanRoot.transform.localRotation =
                Quaternion.identity;
            oceanRoot.transform.localScale =
                Vector3.one;

            for (var index = 0;
                index < Faces.Length;
                index++)
            {
                var face =
                    Faces[index];
                var child =
                    new GameObject(
                        $"Preview Ocean {face}")
                    {
                        tag =
                            "EditorOnly",
                        hideFlags =
                            HideFlags.DontSaveInBuild
                    };
                child.transform.SetParent(
                    oceanRoot.transform,
                    false);
                PositionFace(
                    child.transform,
                    face,
                    previewRadius);

                var meshFilter =
                    child.AddComponent<MeshFilter>();
                var meshRenderer =
                    child.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode =
                    ShadowCastingMode.Off;
                meshRenderer.receiveShadows =
                    false;
                meshFilter.sharedMesh =
                    CreateOceanFaceMesh(
                        face,
                        meshResolution,
                        planetRadiusMeters,
                        oceanRadiusMeters,
                        displayScale);
                meshRenderer.sharedMaterial =
                    oceanDefinition.Material;
            }
        }

        private static Mesh CreateOceanFaceMesh(
            CubeSphereFace face,
            int resolution,
            double planetRadiusMeters,
            double oceanRadiusMeters,
            double displayScale)
        {
            var faceNormal =
                CubeSphereTopology.GetFaceNormal(
                    face);
            var faceUAxis =
                CubeSphereTopology.GetFaceUAxis(
                    face);
            var faceVAxis =
                CubeSphereTopology.GetFaceVAxis(
                    face);
            var referencePosition =
                faceNormal *
                planetRadiusMeters;
            var vertices =
                new Vector3[
                    resolution *
                    resolution];
            var normals =
                new Vector3[
                    vertices.Length];
            var uv =
                new Vector2[
                    vertices.Length];
            var triangles =
                new int[
                    (resolution - 1) *
                    (resolution - 1) *
                    6];

            for (var z = 0;
                z < resolution;
                z++)
            {
                var normalizedZ =
                    z /
                    (double)(
                        resolution -
                        1);
                var faceV =
                    1.0 -
                    normalizedZ *
                    2.0;

                for (var x = 0;
                    x < resolution;
                    x++)
                {
                    var normalizedX =
                        x /
                        (double)(
                            resolution -
                            1);
                    var faceU =
                        normalizedX *
                        2.0 -
                        1.0;
                    var address =
                        new CubeSphereAddress(
                            face,
                            faceU,
                            faceV,
                            0.0);
                    var direction =
                        CubeSphereMapping.AddressToDirection(
                            address);
                    var delta =
                        direction *
                            oceanRadiusMeters -
                        referencePosition;
                    var vertexIndex =
                        z *
                        resolution +
                        x;
                    vertices[vertexIndex] =
                        new Vector3(
                            (float)(
                                Dot(
                                    delta,
                                    faceUAxis) *
                                displayScale),
                            (float)(
                                Dot(
                                    delta,
                                    faceNormal) *
                                displayScale),
                            (float)(
                                -Dot(
                                    delta,
                                    faceVAxis) *
                                displayScale));
                    normals[vertexIndex] =
                        new Vector3(
                            (float)Dot(
                                direction,
                                faceUAxis),
                            (float)Dot(
                                direction,
                                faceNormal),
                            (float)-Dot(
                                direction,
                                faceVAxis)).normalized;
                    uv[vertexIndex] =
                        new Vector2(
                            (float)normalizedX,
                            (float)normalizedZ);
                }
            }

            var triangleIndex =
                0;

            for (var z = 0;
                z < resolution -
                    1;
                z++)
            {
                for (var x = 0;
                    x < resolution -
                        1;
                    x++)
                {
                    var lowerLeft =
                        z *
                        resolution +
                        x;
                    var upperLeft =
                        lowerLeft +
                        resolution;
                    var lowerRight =
                        lowerLeft +
                        1;
                    var upperRight =
                        upperLeft +
                        1;
                    triangles[triangleIndex++] =
                        lowerLeft;
                    triangles[triangleIndex++] =
                        upperLeft;
                    triangles[triangleIndex++] =
                        lowerRight;
                    triangles[triangleIndex++] =
                        lowerRight;
                    triangles[triangleIndex++] =
                        upperLeft;
                    triangles[triangleIndex++] =
                        upperRight;
                }
            }

            var mesh =
                new Mesh
                {
                    name =
                        PreviewResourcePrefix +
                        face +
                        " Ocean Mesh",
                    hideFlags =
                        HideFlags.HideAndDontSave,
                    indexFormat =
                        vertices.Length >
                            65535
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16,
                    vertices =
                        vertices,
                    normals =
                        normals,
                    uv =
                        uv,
                    triangles =
                        triangles
                };
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void PositionFace(
            Transform faceTransform,
            CubeSphereFace face,
            float previewRadius)
        {
            var faceNormal =
                ToVector3(
                    CubeSphereTopology.GetFaceNormal(
                        face)).normalized;
            var forward =
                -ToVector3(
                    CubeSphereTopology.GetFaceVAxis(
                        face)).normalized;
            faceTransform.localPosition =
                faceNormal *
                previewRadius;
            faceTransform.localRotation =
                Quaternion.LookRotation(
                    forward,
                    faceNormal);
            faceTransform.localScale =
                Vector3.one;
        }

        private static Mesh CreateFaceMesh(
            FaceGenerationData data,
            int resolution,
            double planetRadiusMeters,
            double elevationOffsetMeters,
            double displayScale,
            float heightMultiplier)
        {
            var faceNormal =
                CubeSphereTopology.GetFaceNormal(
                    data.Face);
            var faceUAxis =
                CubeSphereTopology.GetFaceUAxis(
                    data.Face);
            var faceVAxis =
                CubeSphereTopology.GetFaceVAxis(
                    data.Face);
            var referencePosition =
                faceNormal *
                planetRadiusMeters;
            var vertices =
                new Vector3[
                    resolution *
                    resolution];
            var normals =
                new Vector3[
                    vertices.Length];
            var uv =
                new Vector2[
                    vertices.Length];
            var triangles =
                new int[
                    (resolution - 1) *
                    (resolution - 1) *
                    6];

            for (var z = 0;
                z < resolution;
                z++)
            {
                var normalizedZ =
                    z /
                    (double)(
                        resolution -
                        1);
                var faceV =
                    1.0 -
                    normalizedZ *
                    2.0;

                for (var x = 0;
                    x < resolution;
                    x++)
                {
                    var normalizedX =
                        x /
                        (double)(
                            resolution -
                            1);
                    var faceU =
                        normalizedX *
                        2.0 -
                        1.0;
                    var address =
                        new CubeSphereAddress(
                            data.Face,
                            faceU,
                            faceV,
                            0.0);
                    var direction =
                        CubeSphereMapping.AddressToDirection(
                            address);
                    var normalizedHeight =
                        SampleBilinear(
                            data.NormalizedHeights,
                            data.Resolution,
                            normalizedX,
                            normalizedZ);
                    var heightMeters =
                        (normalizedHeight *
                            data.HeightScaleMeters +
                        elevationOffsetMeters) *
                        heightMultiplier;
                    var surfaceRadiusMeters =
                        planetRadiusMeters +
                        heightMeters;

                    if (!IsFinite(
                            surfaceRadiusMeters) ||
                        surfaceRadiusMeters <=
                            0.0)
                    {
                        throw new InvalidOperationException(
                            "The height multiplier produced a non-positive or non-finite preview radius.");
                    }

                    var delta =
                        direction *
                            surfaceRadiusMeters -
                        referencePosition;
                    var vertexIndex =
                        z *
                        resolution +
                        x;
                    vertices[vertexIndex] =
                        new Vector3(
                            (float)(
                                Dot(
                                    delta,
                                    faceUAxis) *
                                displayScale),
                            (float)(
                                Dot(
                                    delta,
                                    faceNormal) *
                                displayScale),
                            (float)(
                                -Dot(
                                    delta,
                                    faceVAxis) *
                                displayScale));
                    normals[vertexIndex] =
                        new Vector3(
                            (float)Dot(
                                direction,
                                faceUAxis),
                            (float)Dot(
                                direction,
                                faceNormal),
                            (float)-Dot(
                                direction,
                                faceVAxis)).normalized;
                    uv[vertexIndex] =
                        new Vector2(
                            (float)normalizedX,
                            (float)normalizedZ);
                }
            }

            var triangleIndex =
                0;

            for (var z = 0;
                z < resolution -
                    1;
                z++)
            {
                for (var x = 0;
                    x < resolution -
                        1;
                    x++)
                {
                    var lowerLeft =
                        z *
                        resolution +
                        x;
                    var upperLeft =
                        lowerLeft +
                        resolution;
                    var lowerRight =
                        lowerLeft +
                        1;
                    var upperRight =
                        upperLeft +
                        1;
                    triangles[triangleIndex++] =
                        lowerLeft;
                    triangles[triangleIndex++] =
                        upperLeft;
                    triangles[triangleIndex++] =
                        lowerRight;
                    triangles[triangleIndex++] =
                        lowerRight;
                    triangles[triangleIndex++] =
                        upperLeft;
                    triangles[triangleIndex++] =
                        upperRight;
                }
            }

            var mesh =
                new Mesh
                {
                    name =
                        PreviewResourcePrefix +
                        data.Face +
                        " Mesh",
                    hideFlags =
                        HideFlags.HideAndDontSave,
                    indexFormat =
                        vertices.Length >
                            65535
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16,
                    vertices =
                        vertices,
                    normals =
                        normals,
                    uv =
                        uv,
                    triangles =
                        triangles
                };
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static float SampleBilinear(
            float[] values,
            int resolution,
            double normalizedX,
            double normalizedZ)
        {
            var sampleX =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        normalizedX)) *
                (resolution -
                    1);
            var sampleZ =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        normalizedZ)) *
                (resolution -
                    1);
            var x0 =
                (int)Math.Floor(
                    sampleX);
            var z0 =
                (int)Math.Floor(
                    sampleZ);
            var x1 =
                Math.Min(
                    x0 +
                        1,
                    resolution -
                        1);
            var z1 =
                Math.Min(
                    z0 +
                        1,
                    resolution -
                        1);
            var blendX =
                (float)(
                    sampleX -
                    x0);
            var blendZ =
                (float)(
                    sampleZ -
                    z0);
            var lower =
                Mathf.Lerp(
                    values[
                        z0 *
                        resolution +
                        x0],
                    values[
                        z0 *
                        resolution +
                        x1],
                    blendX);
            var upper =
                Mathf.Lerp(
                    values[
                        z1 *
                        resolution +
                        x0],
                    values[
                        z1 *
                        resolution +
                        x1],
                    blendX);
            return
                Mathf.Lerp(
                    lower,
                    upper,
                    blendZ);
        }

        private static Material CreateFaceMaterial(
            FaceGenerationData data,
            Material templateMaterial,
            double planetRadiusMeters)
        {
            Material material =
                null;
            Texture2D controlTexture =
                null;

            try
            {
                if (CanAdaptTerrainLayers(
                        data))
                {
                    material =
                        CreateTerrainLayerMaterial(
                            templateMaterial);
                    controlTexture =
                        CreateControlTexture(
                            data);
                    ConfigureTerrainLayerMaterial(
                        material,
                        controlTexture,
                        data.TerrainLayers,
                        planetRadiusMeters);
                }
                else
                {
                    material =
                        CreateFallbackMaterial(
                            templateMaterial);
                    SetMaterialColor(
                        material,
                        new Color(
                            0.8f,
                            0.15f,
                            0.1f,
                            1.0f));
                }

                material.name =
                    PreviewResourcePrefix +
                    data.Face +
                    " Material";
                material.hideFlags =
                    HideFlags.HideAndDontSave;
                return material;
            }
            catch
            {
                if (controlTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        controlTexture);
                }

                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        material);
                }

                throw;
            }
        }

        private static bool CanAdaptTerrainLayers(
            FaceGenerationData data)
        {
            if (data.ControlWeights == null ||
                data.TerrainLayers == null ||
                data.TerrainLayers.Length < 1 ||
                data.TerrainLayers.Length >
                    MaximumLayerCount)
            {
                return false;
            }

            for (var index = 0;
                index < data.TerrainLayers.Length;
                index++)
            {
                if (data.TerrainLayers[index] ==
                    null)
                {
                    return false;
                }
            }

            return true;
        }

        private static Texture2D CreateControlTexture(
            FaceGenerationData data)
        {
            var weights =
                data.ControlWeights;
            var height =
                weights.GetLength(
                    0);
            var width =
                weights.GetLength(
                    1);
            var layerCount =
                weights.GetLength(
                    2);
            var pixels =
                new Color[
                    width *
                    height];

            for (var z = 0;
                z < height;
                z++)
            {
                for (var x = 0;
                    x < width;
                    x++)
                {
                    pixels[
                        z *
                        width +
                        x] =
                        new Color(
                            layerCount > 0
                                ? weights[z, x, 0]
                                : 0.0f,
                            layerCount > 1
                                ? weights[z, x, 1]
                                : 0.0f,
                            layerCount > 2
                                ? weights[z, x, 2]
                                : 0.0f,
                            layerCount > 3
                                ? weights[z, x, 3]
                                : 0.0f);
                }
            }

            var texture =
                new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false,
                    true)
                {
                    name =
                        PreviewResourcePrefix +
                        data.Face +
                        " Control",
                    hideFlags =
                        HideFlags.HideAndDontSave,
                    wrapMode =
                        TextureWrapMode.Clamp,
                    filterMode =
                        FilterMode.Bilinear
                };
            texture.SetPixels(
                pixels);
            texture.Apply(
                updateMipmaps: false,
                makeNoLongerReadable: false);
            return texture;
        }

        private static Material CreateTerrainLayerMaterial(
            Material templateMaterial)
        {
            if (IsCompatibleLayerTemplate(
                    templateMaterial))
            {
                return new Material(
                    templateMaterial);
            }

            var shader =
                Shader.Find(
                    DefaultLayerShaderName);

            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"Shader '{DefaultLayerShaderName}' was not found for the celestial-body preview.");
            }

            return new Material(
                shader);
        }

        private static Material CreateFallbackMaterial(
            Material templateMaterial)
        {
            if (templateMaterial != null)
            {
                return new Material(
                    templateMaterial);
            }

            var shader =
                ResolveFallbackShader();

            if (shader == null)
            {
                throw new InvalidOperationException(
                    "No compatible fallback shader was found for the celestial-body preview.");
            }

            return new Material(
                shader);
        }

        private static void ConfigureTerrainLayerMaterial(
            Material material,
            Texture2D controlTexture,
            TerrainLayer[] terrainLayers,
            double planetRadiusMeters)
        {
            ApplyTexture(
                material,
                "_Control",
                controlTexture,
                Vector2.one,
                Vector2.zero);
            SetFloatIfPresent(
                material,
                "_LayerCount",
                terrainLayers.Length);
            SetFloatIfPresent(
                material,
                "_TileFade",
                1.0f);
            SetFloatIfPresent(
                material,
                "_LodMaskMode",
                0.0f);

            for (var index = 0;
                index < MaximumLayerCount;
                index++)
            {
                ConfigureTerrainLayer(
                    material,
                    index <
                        terrainLayers.Length
                        ? terrainLayers[index]
                        : null,
                    index,
                    planetRadiusMeters);
            }
        }

        private static void ConfigureTerrainLayer(
            Material material,
            TerrainLayer terrainLayer,
            int layerIndex,
            double planetRadiusMeters)
        {
            var suffix =
                layerIndex.ToString();

            if (terrainLayer == null)
            {
                ApplyTexture(
                    material,
                    "_Splat" +
                        suffix,
                    Texture2D.whiteTexture,
                    Vector2.one,
                    Vector2.zero);
                ApplyTexture(
                    material,
                    "_Normal" +
                        suffix,
                    null,
                    Vector2.one,
                    Vector2.zero);
                ApplyTexture(
                    material,
                    "_Mask" +
                        suffix,
                    null,
                    Vector2.one,
                    Vector2.zero);
                SetFloatIfPresent(
                    material,
                    "_HasNormal" +
                        suffix,
                    0.0f);
                SetFloatIfPresent(
                    material,
                    "_HasMask" +
                        suffix,
                    0.0f);
                return;
            }

            ResolveTextureTransform(
                terrainLayer,
                planetRadiusMeters,
                out var textureScale,
                out var textureOffset);
            var layerDiffuse =
                terrainLayer.diffuseTexture != null
                    ? terrainLayer.diffuseTexture
                    : Texture2D.whiteTexture;
            var layerNormal =
                terrainLayer.normalMapTexture;
            var layerMask =
                terrainLayer.maskMapTexture;
            ApplyTexture(
                material,
                "_Splat" +
                    suffix,
                layerDiffuse,
                textureScale,
                textureOffset);
            ApplyTexture(
                material,
                "_Normal" +
                    suffix,
                layerNormal,
                textureScale,
                textureOffset);
            ApplyTexture(
                material,
                "_Mask" +
                    suffix,
                layerMask,
                textureScale,
                textureOffset);
            SetFloatIfPresent(
                material,
                "_HasNormal" +
                    suffix,
                layerNormal != null
                    ? 1.0f
                    : 0.0f);
            SetFloatIfPresent(
                material,
                "_HasMask" +
                    suffix,
                layerMask != null
                    ? 1.0f
                    : 0.0f);
            SetFloatIfPresent(
                material,
                "_NormalScale" +
                    suffix,
                terrainLayer.normalScale);
            SetFloatIfPresent(
                material,
                "_Metallic" +
                    suffix,
                terrainLayer.metallic);
            SetFloatIfPresent(
                material,
                "_Smoothness" +
                    suffix,
                terrainLayer.smoothness);
        }

        private static void ResolveTextureTransform(
            TerrainLayer terrainLayer,
            double planetRadiusMeters,
            out Vector2 scale,
            out Vector2 offset)
        {
            var halfFaceSizeMeters =
                CubeSphereMapping.FaceCoordinateToMeters(
                    1.0,
                    planetRadiusMeters);
            var faceSizeMeters =
                halfFaceSizeMeters *
                2.0;
            var tileSizeX =
                SafeTileSize(
                    terrainLayer.tileSize.x);
            var tileSizeZ =
                SafeTileSize(
                    terrainLayer.tileSize.y);
            scale =
                new Vector2(
                    (float)(
                        faceSizeMeters /
                        tileSizeX),
                    (float)(
                        faceSizeMeters /
                        tileSizeZ));
            offset =
                new Vector2(
                    Repeat01(
                        (-halfFaceSizeMeters +
                            terrainLayer.tileOffset.x) /
                        tileSizeX),
                    Repeat01(
                        (-halfFaceSizeMeters +
                            terrainLayer.tileOffset.y) /
                        tileSizeZ));
        }

        private static bool IsCompatibleLayerTemplate(
            Material material)
        {
            return
                material != null &&
                material.HasProperty(
                    "_Control") &&
                material.HasProperty(
                    "_LayerCount") &&
                material.HasProperty(
                    "_Splat0");
        }

        private static void ApplyTexture(
            Material material,
            string propertyName,
            Texture texture,
            Vector2 scale,
            Vector2 offset)
        {
            if (!material.HasProperty(
                    propertyName))
            {
                return;
            }

            material.SetTexture(
                propertyName,
                texture);
            material.SetTextureScale(
                propertyName,
                scale);
            material.SetTextureOffset(
                propertyName,
                offset);
        }

        private static void SetFloatIfPresent(
            Material material,
            string propertyName,
            float value)
        {
            if (material.HasProperty(
                    propertyName))
            {
                material.SetFloat(
                    propertyName,
                    value);
            }
        }

        private static float SafeTileSize(
            float value)
        {
            return
                IsFinite(
                    value) &&
                Mathf.Abs(
                    value) >
                    Mathf.Epsilon
                    ? Mathf.Abs(
                        value)
                    : 1.0f;
        }

        private static float Repeat01(
            double value)
        {
            return
                (float)(
                    value -
                    Math.Floor(
                        value));
        }

        private static Shader ResolveFallbackShader()
        {
            var renderPipeline =
                GraphicsSettings.currentRenderPipeline;

            if (renderPipeline == null)
            {
                return
                    Shader.Find(
                        "Unlit/Color") ??
                    Shader.Find(
                        "Standard");
            }

            var pipelineName =
                renderPipeline.GetType().Name;

            if (pipelineName.IndexOf(
                    "Universal",
                    StringComparison.OrdinalIgnoreCase) >=
                0)
            {
                return
                    Shader.Find(
                        "Universal Render Pipeline/Unlit");
            }

            if (pipelineName.IndexOf(
                    "HDRender",
                    StringComparison.OrdinalIgnoreCase) >=
                0 ||
                pipelineName.IndexOf(
                    "HighDefinition",
                    StringComparison.OrdinalIgnoreCase) >=
                0)
            {
                return
                    Shader.Find(
                        "HDRP/Unlit");
            }

            return null;
        }

        private static void SetMaterialColor(
            Material material,
            Color color)
        {
            if (material.HasProperty(
                    "_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    color);
            }

            if (material.HasProperty(
                    "_Color"))
            {
                material.SetColor(
                    "_Color",
                    color);
            }
        }

        private static void HandlePlayModeStateChanged(
            PlayModeStateChange state)
        {
            if (state ==
                PlayModeStateChange.ExitingEditMode)
            {
                DeletePreviewInternal();
            }
        }

        private static void DeletePreviewInternal()
        {
            var preview =
                FindPreview();

            if (preview == null)
            {
                return;
            }

            DestroyPreviewObject(
                preview);
        }

        private static GameObject FindPreview()
        {
            var objects =
                Resources.FindObjectsOfTypeAll<
                    GameObject>();

            for (var index = 0;
                index < objects.Length;
                index++)
            {
                var candidate =
                    objects[index];

                if (candidate == null ||
                    candidate.name !=
                        PreviewRootName ||
                    EditorUtility.IsPersistent(
                        candidate) ||
                    !candidate.scene.IsValid())
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private static void DestroyPreviewObject(
            GameObject preview)
        {
            if (preview == null)
            {
                return;
            }

            DestroyPreviewResources(
                preview);
            UnityEngine.Object.DestroyImmediate(
                preview);
        }

        private static void DestroyPreviewResources(
            GameObject preview)
        {
            var resources =
                new HashSet<UnityEngine.Object>();
            var renderers =
                preview.GetComponentsInChildren<
                    Renderer>(
                    true);

            for (var rendererIndex = 0;
                rendererIndex < renderers.Length;
                rendererIndex++)
            {
                var materials =
                    renderers[rendererIndex]
                        .sharedMaterials;

                for (var materialIndex = 0;
                    materialIndex < materials.Length;
                    materialIndex++)
                {
                    var material =
                        materials[materialIndex];

                    if (!IsPreviewResource(
                            material))
                    {
                        continue;
                    }

                    if (material.HasProperty(
                            "_Control"))
                    {
                        var controlTexture =
                            material.GetTexture(
                                "_Control");

                        if (IsPreviewResource(
                                controlTexture))
                        {
                            resources.Add(
                                controlTexture);
                        }
                    }

                    resources.Add(
                        material);
                }
            }

            var meshFilters =
                preview.GetComponentsInChildren<
                    MeshFilter>(
                    true);

            for (var index = 0;
                index < meshFilters.Length;
                index++)
            {
                var mesh =
                    meshFilters[index]
                        .sharedMesh;

                if (IsPreviewResource(
                        mesh))
                {
                    resources.Add(
                        mesh);
                }
            }

            foreach (var resource in
                resources)
            {
                if (resource != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        resource);
                }
            }
        }

        private static bool IsPreviewResource(
            UnityEngine.Object value)
        {
            return
                value != null &&
                value.name.StartsWith(
                    PreviewResourcePrefix,
                    StringComparison.Ordinal);
        }

        private static Vector3 ToVector3(
            DoubleVector3 value)
        {
            return new Vector3(
                (float)value.x,
                (float)value.y,
                (float)value.z);
        }

        private static double Dot(
            DoubleVector3 first,
            DoubleVector3 second)
        {
            return
                first.x *
                    second.x +
                first.y *
                    second.y +
                first.z *
                    second.z;
        }

        private static bool Approximately(
            float first,
            float second)
        {
            return
                Mathf.Approximately(
                    first,
                    second);
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(
                    value) &&
                !double.IsInfinity(
                    value);
        }

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(
                    value) &&
                !float.IsInfinity(
                    value);
        }
    }
}
