/*
 * Creates one shared, smooth unit-diameter sphere mesh for all simple celestial-body presentations.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    internal static class CelestialSimpleSphereMesh
    {
        private const int LongitudeSegments = 96;
        private const int LatitudeSegments = 48;

        private static Mesh sharedMesh;

        public static Mesh Get()
        {
            if (sharedMesh == null)
            {
                sharedMesh =
                    Build();
            }

            return sharedMesh;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            sharedMesh = null;
        }

        private static Mesh Build()
        {
            var rowLength =
                LongitudeSegments +
                1;
            var vertices =
                new Vector3[
                    (LatitudeSegments + 1) *
                    rowLength];
            var normals =
                new Vector3[
                    vertices.Length];
            var uv =
                new Vector2[
                    vertices.Length];
            var triangles =
                new int[
                    LatitudeSegments *
                    LongitudeSegments *
                    6];
            var vertexIndex =
                0;

            for (var latitude = 0;
                latitude <=
                    LatitudeSegments;
                latitude++)
            {
                var latitudeFraction =
                    latitude /
                    (float)LatitudeSegments;
                var polarAngle =
                    latitudeFraction *
                    Mathf.PI;
                var ringRadius =
                    Mathf.Sin(
                        polarAngle);
                var y =
                    Mathf.Cos(
                        polarAngle);

                for (var longitude = 0;
                    longitude <=
                        LongitudeSegments;
                    longitude++)
                {
                    var longitudeFraction =
                        longitude /
                        (float)LongitudeSegments;
                    var azimuth =
                        longitudeFraction *
                        Mathf.PI *
                        2.0f;
                    var normal =
                        new Vector3(
                            ringRadius *
                                Mathf.Cos(
                                    azimuth),
                            y,
                            ringRadius *
                                Mathf.Sin(
                                    azimuth));
                    normals[vertexIndex] =
                        normal;
                    vertices[vertexIndex] =
                        normal *
                        0.5f;
                    uv[vertexIndex] =
                        new Vector2(
                            longitudeFraction,
                            1.0f -
                                latitudeFraction);
                    vertexIndex++;
                }
            }

            var triangleIndex =
                0;

            for (var latitude = 0;
                latitude <
                    LatitudeSegments;
                latitude++)
            {
                var row =
                    latitude *
                    rowLength;
                var nextRow =
                    row +
                    rowLength;

                for (var longitude = 0;
                    longitude <
                        LongitudeSegments;
                    longitude++)
                {
                    var topLeft =
                        row +
                        longitude;
                    var topRight =
                        topLeft +
                        1;
                    var bottomLeft =
                        nextRow +
                        longitude;
                    var bottomRight =
                        bottomLeft +
                        1;

                    triangles[triangleIndex++] =
                        topLeft;
                    triangles[triangleIndex++] =
                        topRight;
                    triangles[triangleIndex++] =
                        bottomLeft;
                    triangles[triangleIndex++] =
                        topRight;
                    triangles[triangleIndex++] =
                        bottomRight;
                    triangles[triangleIndex++] =
                        bottomLeft;
                }
            }

            var mesh =
                new Mesh
                {
                    name =
                        "Celestial Simple Sphere",
                    hideFlags =
                        HideFlags.HideAndDontSave,
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
            mesh.UploadMeshData(
                true);
            return mesh;
        }
    }
}
