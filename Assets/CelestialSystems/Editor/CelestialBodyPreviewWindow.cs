/*
 * Provides an editor window for generating and deleting a scaled spherical preview of a celestial-body MapMagic graph.
 */

using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public sealed class CelestialBodyPreviewWindow :
        EditorWindow
    {
        private const double AutoUpdatePollIntervalSeconds =
            0.25;

        private const double AutoUpdateDebounceSeconds =
            0.75;

        private static readonly GUIContent BodyDefinitionLabel =
            new GUIContent(
                "Celestial Body Definition",
                "Provides the real physical radius used while evaluating the spherical graph.");

        private static readonly GUIContent SurfaceDefinitionLabel =
            new GUIContent(
                "Surface Definition Override",
                "Optional. When empty, the surface definition assigned to the celestial body is used.");

        private static readonly GUIContent PreviewDiameterLabel =
            new GUIContent(
                "Preview Diameter",
                "Displayed diameter in Unity scene units. Graph evaluation still uses the body's real physical radius.");

        private static readonly GUIContent MeshResolutionLabel =
            new GUIContent(
                "Mesh Resolution / Face",
                "Number of vertices along each side of every cube-sphere face.");

        private static readonly GUIContent GraphResolutionLabel =
            new GUIContent(
                "Graph Resolution / Face",
                "MapMagic height and texture-control samples along each side of every cube-sphere face.");

        private static readonly GUIContent HeightMultiplierLabel =
            new GUIContent(
                "Height Multiplier",
                "Multiplies generated elevation before the full-scale body is reduced to the preview diameter.");

        private static readonly GUIContent AutoUpdateLabel =
            new GUIContent(
                "Auto-update",
                "Rebuilds an existing preview after the selected graph or preview settings stop changing.");

        private static readonly GUIContent[] MeshResolutionLabels =
        {
            new GUIContent(
                "9"),
            new GUIContent(
                "17"),
            new GUIContent(
                "33"),
            new GUIContent(
                "65"),
            new GUIContent(
                "129"),
            new GUIContent(
                "257")
        };

        private static readonly int[] MeshResolutionValues =
        {
            9,
            17,
            33,
            65,
            129,
            257
        };

        private static readonly GUIContent[] GraphResolutionLabels =
        {
            new GUIContent(
                "17"),
            new GUIContent(
                "33"),
            new GUIContent(
                "65"),
            new GUIContent(
                "129"),
            new GUIContent(
                "257"),
            new GUIContent(
                "513")
        };

        private static readonly int[] GraphResolutionValues =
        {
            17,
            33,
            65,
            129,
            257,
            513
        };

        [SerializeField]
        private CelestialBodyDefinition bodyDefinition;

        [SerializeField]
        private RoundMapMagicSurfaceDefinition surfaceDefinitionOverride;

        [SerializeField]
        private Vector3 previewPosition;

        [SerializeField]
        private float previewDiameter =
            10.0f;

        [SerializeField]
        private int meshResolution =
            65;

        [SerializeField]
        private int graphResolution =
            129;

        [SerializeField]
        private float heightMultiplier =
            1.0f;

        [SerializeField]
        private bool autoUpdate =
            true;

        private double nextAutoUpdatePollTime;

        private double pendingAutoUpdateTime;

        private string observedPreviewSignature;

        private string pendingPreviewSignature;

        private bool isGenerating;

        [MenuItem(
            "Tools/Celestial Systems/Body Preview")]
        private static void OpenWindow()
        {
            var window =
                GetWindow<CelestialBodyPreviewWindow>();
            window.titleContent =
                new GUIContent(
                    "Body Preview");
            window.minSize =
                new Vector2(
                    390.0f,
                    335.0f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent =
                new GUIContent(
                    "Body Preview");

            if (bodyDefinition == null &&
                Selection.activeObject is
                    CelestialBodyDefinition selectedBody)
            {
                bodyDefinition =
                    selectedBody;
            }

            EditorApplication.update -=
                HandleEditorUpdate;
            EditorApplication.update +=
                HandleEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -=
                HandleEditorUpdate;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Spherical MapMagic Preview",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The graph is evaluated using the body's real radius and meter-based coordinates. Only the completed result is scaled to the preview diameter.",
                MessageType.Info);

            EditorGUILayout.Space();
            bodyDefinition =
                (CelestialBodyDefinition)
                    EditorGUILayout.ObjectField(
                        BodyDefinitionLabel,
                        bodyDefinition,
                        typeof(
                            CelestialBodyDefinition),
                        false);
            surfaceDefinitionOverride =
                (RoundMapMagicSurfaceDefinition)
                    EditorGUILayout.ObjectField(
                        SurfaceDefinitionLabel,
                        surfaceDefinitionOverride,
                        typeof(
                            RoundMapMagicSurfaceDefinition),
                        false);

            var resolvedSurfaceDefinition =
                ResolveSurfaceDefinition();

            using (new EditorGUI.DisabledScope(
                       true))
            {
                EditorGUILayout.ObjectField(
                    "Resolved Surface Definition",
                    resolvedSurfaceDefinition,
                    typeof(
                        RoundMapMagicSurfaceDefinition),
                    false);
                EditorGUILayout.ObjectField(
                    "Ocean Definition",
                    bodyDefinition != null
                        ? bodyDefinition.OceanDefinition
                        : null,
                    typeof(
                        OceanDefinition),
                    false);
            }

            EditorGUILayout.Space();
            previewPosition =
                EditorGUILayout.Vector3Field(
                    "Position",
                    previewPosition);
            previewDiameter =
                EditorGUILayout.FloatField(
                    PreviewDiameterLabel,
                    previewDiameter);
            meshResolution =
                EditorGUILayout.IntPopup(
                    MeshResolutionLabel,
                    meshResolution,
                    MeshResolutionLabels,
                    MeshResolutionValues);
            graphResolution =
                EditorGUILayout.IntPopup(
                    GraphResolutionLabel,
                    graphResolution,
                    GraphResolutionLabels,
                    GraphResolutionValues);
            heightMultiplier =
                EditorGUILayout.FloatField(
                    HeightMultiplierLabel,
                    heightMultiplier);
            autoUpdate =
                EditorGUILayout.Toggle(
                    AutoUpdateLabel,
                    autoUpdate);

            EditorGUILayout.Space();

            var previewExists =
                CelestialBodyPreviewBuilder.HasPreview;
            var isEditorBusy =
                EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(
                           isEditorBusy))
                {
                    if (GUILayout.Button(
                            "Generate",
                            GUILayout.Height(
                                28.0f)))
                    {
                        GeneratePreview(
                            resolvedSurfaceDefinition,
                            true);
                    }
                }

                using (new EditorGUI.DisabledScope(
                           isEditorBusy ||
                           !previewExists))
                {
                    if (GUILayout.Button(
                            "Delete",
                            GUILayout.Height(
                                28.0f)))
                    {
                        CelestialBodyPreviewBuilder.DeletePreview();
                        observedPreviewSignature =
                            null;
                        pendingPreviewSignature =
                            null;
                        SceneView.RepaintAll();
                    }
                }
            }

            if (previewExists)
            {
                EditorGUILayout.HelpBox(
                    "A preview is present in the scene. Generating again will replace it. It will be deleted before Play Mode starts.",
                    MessageType.None);
            }
        }

        private RoundMapMagicSurfaceDefinition ResolveSurfaceDefinition()
        {
            if (surfaceDefinitionOverride != null)
            {
                return surfaceDefinitionOverride;
            }

            return
                bodyDefinition != null
                    ? bodyDefinition.RoundMapMagicSurface
                    : null;
        }

        private bool GeneratePreview(
            RoundMapMagicSurfaceDefinition resolvedSurfaceDefinition,
            bool showDialogs)
        {
            if (!TryValidateInputs(
                    resolvedSurfaceDefinition,
                    out var error))
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(
                        "Cannot Generate Body Preview",
                        error,
                        "OK");
                }

                return false;
            }

            isGenerating =
                true;

            try
            {
                var preview =
                    CelestialBodyPreviewBuilder.Generate(
                        bodyDefinition,
                        resolvedSurfaceDefinition,
                        previewPosition,
                        previewDiameter,
                        meshResolution,
                        graphResolution,
                        heightMultiplier);

                observedPreviewSignature =
                    CreatePreviewSignature(
                        resolvedSurfaceDefinition);
                pendingPreviewSignature =
                    null;
                Selection.activeGameObject =
                    preview;
                EditorGUIUtility.PingObject(
                    preview);
                SceneView.RepaintAll();
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(
                    exception);

                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(
                        "Body Preview Generation Failed",
                        exception.GetBaseException().Message,
                        "OK");
                }

                return false;
            }
            finally
            {
                isGenerating =
                    false;
            }
        }

        private void HandleEditorUpdate()
        {
            if (!autoUpdate ||
                isGenerating ||
                !CelestialBodyPreviewBuilder.HasPreview ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.timeSinceStartup <
                    nextAutoUpdatePollTime)
            {
                return;
            }

            nextAutoUpdatePollTime =
                EditorApplication.timeSinceStartup +
                AutoUpdatePollIntervalSeconds;

            var resolvedSurfaceDefinition =
                ResolveSurfaceDefinition();
            var currentSignature =
                CreatePreviewSignature(
                    resolvedSurfaceDefinition);

            if (string.IsNullOrEmpty(
                    observedPreviewSignature))
            {
                observedPreviewSignature =
                    currentSignature;
                return;
            }

            if (currentSignature ==
                observedPreviewSignature)
            {
                pendingPreviewSignature =
                    null;
                return;
            }

            if (currentSignature !=
                pendingPreviewSignature)
            {
                pendingPreviewSignature =
                    currentSignature;
                pendingAutoUpdateTime =
                    EditorApplication.timeSinceStartup +
                    AutoUpdateDebounceSeconds;
                Repaint();
                return;
            }

            if (EditorApplication.timeSinceStartup <
                pendingAutoUpdateTime)
            {
                return;
            }

            observedPreviewSignature =
                currentSignature;
            pendingPreviewSignature =
                null;
            GeneratePreview(
                resolvedSurfaceDefinition,
                false);
            Repaint();
        }

        private string CreatePreviewSignature(
            RoundMapMagicSurfaceDefinition resolvedSurfaceDefinition)
        {
            var builder =
                new StringBuilder();

            AppendObjectState(
                builder,
                bodyDefinition);
            AppendObjectState(
                builder,
                resolvedSurfaceDefinition);
            AppendObjectState(
                builder,
                resolvedSurfaceDefinition != null
                    ? resolvedSurfaceDefinition.Graph
                    : null);
            AppendObjectState(
                builder,
                bodyDefinition != null
                    ? bodyDefinition.OceanDefinition
                    : null);
            AppendObjectState(
                builder,
                bodyDefinition != null &&
                    bodyDefinition.OceanDefinition != null
                    ? bodyDefinition.OceanDefinition.Material
                    : null);
            AppendFloat(
                builder,
                previewPosition.x);
            AppendFloat(
                builder,
                previewPosition.y);
            AppendFloat(
                builder,
                previewPosition.z);
            AppendFloat(
                builder,
                previewDiameter);
            builder.Append(
                meshResolution);
            builder.Append('|');
            builder.Append(
                graphResolution);
            builder.Append('|');
            AppendFloat(
                builder,
                heightMultiplier);
            return Hash128.Compute(
                builder.ToString()).ToString();
        }

        private static void AppendObjectState(
            StringBuilder builder,
            Object value)
        {
            if (value == null)
            {
                builder.Append(
                    "<null>|");
                return;
            }

            var assetPath =
                AssetDatabase.GetAssetPath(
                    value);
            builder.Append(
                assetPath);
            builder.Append('|');

            if (!string.IsNullOrEmpty(
                    assetPath))
            {
                builder.Append(
                    AssetDatabase.GetAssetDependencyHash(
                        assetPath));
                builder.Append('|');
            }

            builder.Append(
                EditorJsonUtility.ToJson(
                    value));
            builder.Append('|');
        }

        private static void AppendFloat(
            StringBuilder builder,
            float value)
        {
            builder.Append(
                value.ToString(
                    "R",
                    CultureInfo.InvariantCulture));
            builder.Append('|');
        }

        private bool TryValidateInputs(
            RoundMapMagicSurfaceDefinition resolvedSurfaceDefinition,
            out string error)
        {
            if (bodyDefinition == null)
            {
                error =
                    "Assign a Celestial Body Definition.";
                return false;
            }

            if (!IsFinite(
                    bodyDefinition.ReferenceRadiusMeters) ||
                bodyDefinition.ReferenceRadiusMeters <=
                    0.0)
            {
                error =
                    "The celestial body must have a positive, finite reference radius.";
                return false;
            }

            if (resolvedSurfaceDefinition == null)
            {
                error =
                    "Assign a Surface Definition Override or assign a Round MapMagic surface to the celestial body.";
                return false;
            }

            if (resolvedSurfaceDefinition.Graph ==
                null)
            {
                error =
                    "The resolved surface definition does not have a MapMagic graph.";
                return false;
            }

            if (!IsFinite(
                    previewDiameter) ||
                previewDiameter <= 0.0f)
            {
                error =
                    "Preview Diameter must be positive and finite.";
                return false;
            }

            if (!IsFinite(
                    heightMultiplier) ||
                heightMultiplier < 0.0f)
            {
                error =
                    "Height Multiplier must be non-negative and finite.";
                return false;
            }

            if (!IsFinite(
                    previewPosition.x) ||
                !IsFinite(
                    previewPosition.y) ||
                !IsFinite(
                    previewPosition.z))
            {
                error =
                    "Position must contain finite values.";
                return false;
            }

            error =
                string.Empty;
            return true;
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
