/*
 * Main-thread capture utility. It renders a generated LOD0 representation into
 * one atlas slot, then destroys the temporary representation in the caller.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    public static class CelestialSmallBodyImpostorCapture
    {
        private const int CaptureLayer = 31;
        private static Camera captureCamera;
        private static Light captureLight;
        private static RenderTexture captureTexture;
        private static Shader normalCaptureShader;

        public static bool TryCaptureVariant(
            CelestialSmallBodyImpostorLibrary library,
            int variantIndex,
            int viewIndex,
            GameObject representation,
            out string error)
        {
            error = string.Empty;

            if (library == null ||
                representation == null)
            {
                error = "The impostor library or generated representation is missing.";
                return false;
            }

            var renderers =
                representation.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                error = "The generated representation has no renderers to capture.";
                return false;
            }

            EnsureCaptureObjects(
                library.VariantResolution);
            var originalPosition =
                representation.transform.position;
            var originalRotation =
                representation.transform.rotation;
            var originalLayers =
                new int[renderers.Length];

            try
            {
                var yawDegrees =
                    library.ViewCount <= 1
                        ? 0.0f
                        : 360.0f * viewIndex / library.ViewCount;
                representation.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.Euler(0.0f, yawDegrees, 0.0f));

                var bounds =
                    GetBounds(
                        renderers,
                        originalLayers);
                SetLayerRecursively(
                    representation.transform,
                    CaptureLayer);
                ConfigureCamera(
                    bounds);
                library.SetVariantBillboardSize(
                    variantIndex,
                    GetCaptureFrameSize(bounds));

                if ((library.RequestedMaps &
                    CelestialSmallBodyImpostorMaps.AlbedoTransparency) != 0)
                {
                    RenderWithCurrentMaterials(
                        Color.clear);
                    library.TryCopyCapture(
                        CelestialSmallBodyImpostorMaps.AlbedoTransparency,
                        captureTexture,
                        variantIndex, viewIndex);
                }

                if ((library.RequestedMaps &
                    CelestialSmallBodyImpostorMaps.Normal) != 0)
                {
                    RenderNormals();
                    library.TryCopyCapture(
                        CelestialSmallBodyImpostorMaps.Normal,
                        captureTexture,
                        variantIndex, viewIndex);
                }

                if ((library.RequestedMaps &
                    CelestialSmallBodyImpostorMaps.Emission) != 0)
                {
                    ClearCapture(
                        Color.clear);
                    library.TryCopyCapture(
                        CelestialSmallBodyImpostorMaps.Emission,
                        captureTexture,
                        variantIndex, viewIndex);
                }

                if ((library.RequestedMaps &
                    CelestialSmallBodyImpostorMaps.MetallicSmoothness) != 0)
                {
                    // Default non-metallic / mid-smoothness packing. Material-
                    // aware metallic and emission extraction is added later.
                    ClearCapture(
                        new Color(
                            0.0f,
                            0.0f,
                            0.0f,
                            0.5f));
                    library.TryCopyCapture(
                        CelestialSmallBodyImpostorMaps.MetallicSmoothness,
                        captureTexture,
                        variantIndex, viewIndex);
                }

                library.MarkCaptureReady(
                    variantIndex,
                    viewIndex);
                return true;
            }
            catch (System.Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                representation.transform.SetPositionAndRotation(
                    originalPosition,
                    originalRotation);
                RestoreLayers(
                    renderers,
                    originalLayers);
            }
        }

        private static void EnsureCaptureObjects(
            int resolution)
        {
            if (captureCamera == null)
            {
                var cameraObject =
                    new GameObject(
                        "Celestial Small Body Impostor Capture Camera")
                    {
                        hideFlags =
                            HideFlags.HideAndDontSave
                    };
                captureCamera =
                    cameraObject.AddComponent<Camera>();
                captureCamera.enabled = false;
                captureCamera.orthographic = true;
                captureCamera.clearFlags =
                    CameraClearFlags.SolidColor;
                captureCamera.backgroundColor =
                    Color.clear;
                captureCamera.cullingMask =
                    1 << CaptureLayer;

                var lightObject =
                    new GameObject(
                        "Celestial Small Body Impostor Capture Light")
                    {
                        hideFlags =
                            HideFlags.HideAndDontSave
                    };
                captureLight =
                    lightObject.AddComponent<Light>();
                captureLight.type =
                    LightType.Directional;
                captureLight.intensity = 1.0f;
                captureLight.cullingMask =
                    1 << CaptureLayer;
                captureLight.transform.rotation =
                    Quaternion.Euler(
                        35.0f,
                        -30.0f,
                        0.0f);
            }

            if (captureTexture != null &&
                captureTexture.width == resolution &&
                captureTexture.height == resolution)
            {
                return;
            }

            if (captureTexture != null)
            {
                captureTexture.Release();
                Object.Destroy(
                    captureTexture);
            }

            captureTexture =
                new RenderTexture(
                    resolution,
                    resolution,
                    24,
                    RenderTextureFormat.ARGBHalf,
                    RenderTextureReadWrite.Linear)
                {
                    name =
                        "Celestial Small Body Impostor Capture",
                    antiAliasing = 1
                };
            captureTexture.Create();
        }

        private static Bounds GetBounds(
            Renderer[] renderers,
            int[] originalLayers)
        {
            var bounds =
                new Bounds(
                    renderers[0].bounds.center,
                    Vector3.zero);

            for (var index = 0;
                index < renderers.Length;
                index++)
            {
                originalLayers[index] =
                    renderers[index].gameObject.layer;
                bounds.Encapsulate(
                    renderers[index].bounds);
            }

            return bounds;
        }

        private static void ConfigureCamera(
            Bounds bounds)
        {
            // Every yaw capture needs the same square framing so the runtime
            // billboard keeps a stable size while changing view tiles.
            var extent =
                Mathf.Max(
                    0.01f,
                    bounds.extents.magnitude);
            var distance =
                Mathf.Max(
                    1.0f,
                    bounds.extents.z * 2.0f +
                    extent * 2.0f);

            captureCamera.orthographicSize =
                Mathf.Max(
                    0.01f,
                    extent * 1.10f);
            captureCamera.transform.position =
                bounds.center -
                Vector3.forward * distance;
            captureCamera.transform.rotation =
                Quaternion.LookRotation(
                    Vector3.forward,
                    Vector3.up);
        }

        private static float GetCaptureFrameSize(
            Bounds bounds)
        {
            return Mathf.Max(
                0.0001f,
                bounds.extents.magnitude * 2.20f);
        }

        private static void RenderWithCurrentMaterials(
            Color clearColor)
        {
            captureCamera.ResetReplacementShader();
            captureCamera.backgroundColor =
                clearColor;
            captureCamera.targetTexture =
                captureTexture;
            captureCamera.Render();
        }

        private static void RenderNormals()
        {
            normalCaptureShader ??=
                Shader.Find(
                    "Hidden/jcan/Celestial Impostor Capture Normal");

            if (normalCaptureShader == null)
            {
                ClearCapture(
                    new Color(
                        0.5f,
                        0.5f,
                        1.0f,
                        0.0f));
                return;
            }

            captureCamera.backgroundColor =
                new Color(
                    0.5f,
                    0.5f,
                    1.0f,
                    0.0f);
            captureCamera.targetTexture =
                captureTexture;
            captureCamera.RenderWithShader(
                normalCaptureShader,
                string.Empty);
            captureCamera.ResetReplacementShader();
        }

        private static void ClearCapture(
            Color color)
        {
            var previous =
                RenderTexture.active;
            RenderTexture.active =
                captureTexture;
            GL.Clear(
                true,
                true,
                color);
            RenderTexture.active =
                previous;
        }

        private static void SetLayerRecursively(
            Transform root,
            int layer)
        {
            root.gameObject.layer =
                layer;

            for (var index = 0;
                index < root.childCount;
                index++)
            {
                SetLayerRecursively(
                    root.GetChild(
                        index),
                    layer);
            }
        }

        private static void RestoreLayers(
            Renderer[] renderers,
            int[] originalLayers)
        {
            for (var index = 0;
                index < renderers.Length;
                index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].gameObject.layer =
                        originalLayers[index];
                }
            }
        }
    }
}
