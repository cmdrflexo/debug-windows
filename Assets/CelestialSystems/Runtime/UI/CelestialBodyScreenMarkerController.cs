/*
 * Creates screen-space markers for active celestial-body runtime contexts and keeps them aligned with their rendered positions.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class CelestialBodyScreenMarkerController :
        MonoBehaviour
    {
        private const string OffScreenSymbol = "Ø";

        [Header("Scene References")]
        [SerializeField]
        private Camera observerCamera;

        [Tooltip("Optional screen-space canvas. A private overlay canvas is created when this is unassigned.")]
        [SerializeField]
        private Canvas targetCanvas;

        [Header("Text")]
        [SerializeField]
        [Min(1)]
        private int instanceIdFontSize = 18;

        [SerializeField]
        [Min(1)]
        private int offScreenSymbolFontSize = 24;

        [SerializeField]
        private Color instanceIdColor = Color.white;

        [SerializeField]
        private Color offScreenSymbolColor = Color.white;

        [SerializeField]
        private Font font;

        [Header("Layout")]
        [SerializeField]
        private Vector2 markerSize = new Vector2(240.0f, 40.0f);

        [SerializeField]
        [Min(0.0f)]
        private float screenEdgePaddingPixels = 8.0f;

        [SerializeField]
        private int canvasSortingOrder = 100;

        [SerializeField]
        [Min(0.0f)]
        private float overlapPaddingPixels = 2.0f;

        [Header("Input")]
        [SerializeField]
        private bool markersVisible = true;

        [Tooltip("Optional button action that toggles all body markers.")]
        [SerializeField]
        private InputActionReference toggleMarkersAction;

        [Header("Discovery")]
        [SerializeField]
        [Min(0.05f)]
        private float contextRefreshIntervalSeconds = 0.5f;

        private readonly Dictionary<
            CelestialBodyRuntimeContext,
            Text> markers =
                new Dictionary<
                    CelestialBodyRuntimeContext,
                    Text>();

        private readonly List<
            CelestialBodyRuntimeContext> staleContexts =
                new List<
                    CelestialBodyRuntimeContext>();

        private readonly List<
            MarkerCandidate> markerCandidates =
                new List<
                    MarkerCandidate>();

        private readonly List<
            MarkerCandidate> acceptedMarkers =
                new List<
                    MarkerCandidate>();

        private Canvas activeCanvas;
        private RectTransform markerContainer;
        private bool ownsCanvas;
        private bool toggleActionEnabledByThisComponent;
        private float nextContextRefreshTime;

        private void OnEnable()
        {
            SubscribeToToggleAction();
            ResolveObserverCamera();
            EnsureCanvas();
            RefreshContexts();
            ApplyMarkerVisibility();
        }

        private void OnDisable()
        {
            UnsubscribeFromToggleAction();
            ClearMarkers();

            if (ownsCanvas &&
                activeCanvas != null)
            {
                Destroy(
                    activeCanvas.gameObject);
            }

            activeCanvas = null;
            markerContainer = null;
            ownsCanvas = false;
        }

        private void OnValidate()
        {
            instanceIdFontSize =
                Mathf.Max(
                    1,
                    instanceIdFontSize);
            offScreenSymbolFontSize =
                Mathf.Max(
                    1,
                    offScreenSymbolFontSize);
            markerSize.x =
                Mathf.Max(
                    1.0f,
                    markerSize.x);
            markerSize.y =
                Mathf.Max(
                    1.0f,
                    markerSize.y);
            screenEdgePaddingPixels =
                Mathf.Max(
                    0.0f,
                    screenEdgePaddingPixels);
            overlapPaddingPixels =
                Mathf.Max(
                    0.0f,
                    overlapPaddingPixels);
            contextRefreshIntervalSeconds =
                Mathf.Max(
                    0.05f,
                    contextRefreshIntervalSeconds);

            if (activeCanvas != null &&
                ownsCanvas)
            {
                activeCanvas.sortingOrder =
                    canvasSortingOrder;
            }

            ApplyMarkerVisibility();
        }

        private void LateUpdate()
        {
            ResolveObserverCamera();
            EnsureCanvas();

            if (Time.unscaledTime >=
                nextContextRefreshTime)
            {
                RefreshContexts();
            }

            UpdateMarkers();
        }

        private void ResolveObserverCamera()
        {
            if (observerCamera == null)
            {
                observerCamera =
                    Camera.main;
            }
        }

        private void EnsureCanvas()
        {
            if (activeCanvas != null &&
                markerContainer != null)
            {
                return;
            }

            activeCanvas =
                targetCanvas;

            if (activeCanvas == null)
            {
                var canvasObject =
                    new GameObject(
                        "Celestial Body Screen Markers",
                        typeof(RectTransform),
                        typeof(Canvas));

                canvasObject.transform.SetParent(
                    transform,
                    false);

                activeCanvas =
                    canvasObject.GetComponent<
                        Canvas>();
                activeCanvas.renderMode =
                    RenderMode.ScreenSpaceOverlay;
                activeCanvas.sortingOrder =
                    canvasSortingOrder;
                ownsCanvas = true;
            }

            var containerObject =
                new GameObject(
                    "Markers",
                    typeof(RectTransform));

            markerContainer =
                containerObject.GetComponent<
                    RectTransform>();
            markerContainer.SetParent(
                activeCanvas.transform,
                false);
            markerContainer.anchorMin =
                Vector2.zero;
            markerContainer.anchorMax =
                Vector2.one;
            markerContainer.offsetMin =
                Vector2.zero;
            markerContainer.offsetMax =
                Vector2.zero;

            ApplyMarkerVisibility();
        }

        private void RefreshContexts()
        {
            nextContextRefreshTime =
                Time.unscaledTime +
                contextRefreshIntervalSeconds;

            var foundContexts =
                FindObjectsByType<
                    CelestialBodyRuntimeContext>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);
            var foundSet =
                new HashSet<
                    CelestialBodyRuntimeContext>(
                        foundContexts);

            for (var index = 0;
                index < foundContexts.Length;
                index++)
            {
                var context =
                    foundContexts[index];

                if (context == null ||
                    markers.ContainsKey(
                        context))
                {
                    continue;
                }

                markers.Add(
                    context,
                    CreateMarker(
                        context));
            }

            staleContexts.Clear();

            foreach (var pair in
                markers)
            {
                if (pair.Key == null ||
                    !foundSet.Contains(
                        pair.Key))
                {
                    staleContexts.Add(
                        pair.Key);
                }
            }

            for (var index = 0;
                index < staleContexts.Count;
                index++)
            {
                RemoveMarker(
                    staleContexts[index]);
            }
        }

        private Text CreateMarker(
            CelestialBodyRuntimeContext context)
        {
            var markerObject =
                new GameObject(
                    GetMarkerObjectName(
                        context),
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text),
                    typeof(Outline));

            var markerRect =
                markerObject.GetComponent<
                    RectTransform>();
            markerRect.SetParent(
                markerContainer,
                false);
            markerRect.anchorMin =
                new Vector2(
                    0.5f,
                    0.5f);
            markerRect.anchorMax =
                markerRect.anchorMin;
            markerRect.pivot =
                new Vector2(
                    0.5f,
                    0.5f);
            markerRect.sizeDelta =
                markerSize;

            var markerText =
                markerObject.GetComponent<
                    Text>();
            markerText.alignment =
                TextAnchor.MiddleCenter;
            markerText.raycastTarget =
                false;
            markerText.horizontalOverflow =
                HorizontalWrapMode.Overflow;
            markerText.verticalOverflow =
                VerticalWrapMode.Overflow;
            markerText.font =
                ResolveFont();

            var outline =
                markerObject.GetComponent<
                    Outline>();
            outline.effectColor =
                new Color(
                    0.0f,
                    0.0f,
                    0.0f,
                    0.8f);
            outline.effectDistance =
                new Vector2(
                    1.0f,
                    -1.0f);
            outline.useGraphicAlpha =
                true;

            return markerText;
        }

        private Font ResolveFont()
        {
            if (font == null)
            {
                font =
                    Resources.GetBuiltinResource<
                        Font>(
                            "LegacyRuntime.ttf");
            }

            return font;
        }

        private void UpdateMarkers()
        {
            if (observerCamera == null ||
                activeCanvas == null ||
                markerContainer == null ||
                !markersVisible)
            {
                return;
            }

            markerCandidates.Clear();
            acceptedMarkers.Clear();

            foreach (var pair in
                markers)
            {
                var context =
                    pair.Key;
                var markerText =
                    pair.Value;

                if (context == null ||
                    markerText == null)
                {
                    continue;
                }

                markerText.enabled =
                    false;

                var trackedTransform =
                    context.VisualRoot != null
                        ? context.VisualRoot
                        : context.transform;
                var screenPoint =
                    observerCamera.WorldToScreenPoint(
                        trackedTransform.position);
                var isOnScreen =
                    IsOnScreen(
                        screenPoint);
                var markerScreenPosition =
                    isOnScreen
                        ? ClampToScreen(
                            screenPoint)
                        : GetOffScreenPosition(
                            screenPoint);

                markerText.text =
                    isOnScreen
                        ? GetInstanceId(
                            context)
                        : OffScreenSymbol;
                markerText.fontSize =
                    isOnScreen
                        ? instanceIdFontSize
                        : offScreenSymbolFontSize;
                markerText.color =
                    isOnScreen
                        ? instanceIdColor
                        : offScreenSymbolColor;
                markerText.rectTransform.sizeDelta =
                    markerSize;
                markerText.gameObject.name =
                    GetMarkerObjectName(
                        context);

                if (!TryGetCanvasPosition(
                        markerScreenPosition,
                        out var canvasPosition))
                {
                    continue;
                }

                markerText.rectTransform.anchoredPosition =
                    canvasPosition;

                var renderedSize =
                    new Vector2(
                        Mathf.Max(
                            1.0f,
                            markerText.preferredWidth) +
                            overlapPaddingPixels *
                                2.0f,
                        Mathf.Max(
                            1.0f,
                            markerText.preferredHeight) +
                            overlapPaddingPixels *
                                2.0f);
                var screenRect =
                    new Rect(
                        markerScreenPosition -
                            renderedSize *
                                0.5f,
                        renderedSize);
                var cameraDelta =
                    trackedTransform.position -
                    observerCamera.transform.position;

                markerCandidates.Add(
                    new MarkerCandidate(
                        markerText,
                        screenRect,
                        cameraDelta.sqrMagnitude));
            }

            markerCandidates.Sort(
                CompareMarkerCandidates);

            for (var candidateIndex = 0;
                candidateIndex < markerCandidates.Count;
                candidateIndex++)
            {
                var candidate =
                    markerCandidates[candidateIndex];
                var overlapsAcceptedMarker =
                    false;

                for (var acceptedIndex = 0;
                    acceptedIndex < acceptedMarkers.Count;
                    acceptedIndex++)
                {
                    if (!candidate.ScreenRect.Overlaps(
                            acceptedMarkers[
                                acceptedIndex].ScreenRect))
                    {
                        continue;
                    }

                    overlapsAcceptedMarker =
                        true;
                    break;
                }

                candidate.MarkerText.enabled =
                    !overlapsAcceptedMarker;

                if (!overlapsAcceptedMarker)
                {
                    acceptedMarkers.Add(
                        candidate);
                }
            }
        }

        private static int CompareMarkerCandidates(
            MarkerCandidate left,
            MarkerCandidate right)
        {
            var distanceComparison =
                left.DistanceSquared.CompareTo(
                    right.DistanceSquared);

            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            return string.CompareOrdinal(
                left.MarkerText.text,
                right.MarkerText.text);
        }

        private bool IsOnScreen(
            Vector3 screenPoint)
        {
            return
                screenPoint.z > 0.0f &&
                screenPoint.x >= 0.0f &&
                screenPoint.x <= Screen.width &&
                screenPoint.y >= 0.0f &&
                screenPoint.y <= Screen.height;
        }

        private Vector2 ClampToScreen(
            Vector3 screenPoint)
        {
            GetScreenInsets(
                out var horizontalInset,
                out var verticalInset);

            return new Vector2(
                Mathf.Clamp(
                    screenPoint.x,
                    horizontalInset,
                    Mathf.Max(
                        horizontalInset,
                        Screen.width -
                            horizontalInset)),
                Mathf.Clamp(
                    screenPoint.y,
                    verticalInset,
                    Mathf.Max(
                        verticalInset,
                        Screen.height -
                            verticalInset)));
        }

        private Vector2 GetOffScreenPosition(
            Vector3 screenPoint)
        {
            var screenCenter =
                new Vector2(
                    Screen.width * 0.5f,
                    Screen.height * 0.5f);
            var direction =
                new Vector2(
                    screenPoint.x,
                    screenPoint.y) -
                screenCenter;

            if (screenPoint.z <= 0.0f)
            {
                direction =
                    -direction;
            }

            if (direction.sqrMagnitude <=
                Mathf.Epsilon)
            {
                direction =
                    Vector2.up;
            }

            GetScreenInsets(
                out var horizontalInset,
                out var verticalInset);

            var halfWidth =
                Mathf.Max(
                    0.0f,
                    Screen.width * 0.5f -
                        horizontalInset);
            var halfHeight =
                Mathf.Max(
                    0.0f,
                    Screen.height * 0.5f -
                        verticalInset);
            var horizontalScale =
                Mathf.Abs(
                    direction.x) >
                    Mathf.Epsilon
                        ? halfWidth /
                            Mathf.Abs(
                                direction.x)
                        : float.PositiveInfinity;
            var verticalScale =
                Mathf.Abs(
                    direction.y) >
                    Mathf.Epsilon
                        ? halfHeight /
                            Mathf.Abs(
                                direction.y)
                        : float.PositiveInfinity;
            var edgeScale =
                Mathf.Min(
                    horizontalScale,
                    verticalScale);

            return
                screenCenter +
                direction *
                    edgeScale;
        }

        private void GetScreenInsets(
            out float horizontalInset,
            out float verticalInset)
        {
            horizontalInset =
                Mathf.Min(
                    Screen.width * 0.5f,
                    screenEdgePaddingPixels +
                        markerSize.x * 0.5f);
            verticalInset =
                Mathf.Min(
                    Screen.height * 0.5f,
                    screenEdgePaddingPixels +
                        markerSize.y * 0.5f);
        }

        private bool TryGetCanvasPosition(
            Vector2 screenPosition,
            out Vector2 canvasPosition)
        {
            var canvasCamera =
                activeCanvas.renderMode ==
                    RenderMode.ScreenSpaceOverlay
                        ? null
                        : activeCanvas.worldCamera != null
                            ? activeCanvas.worldCamera
                            : observerCamera;

            return
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    markerContainer,
                    screenPosition,
                    canvasCamera,
                    out canvasPosition);
        }

        public void SetMarkersVisible(
            bool visible)
        {
            markersVisible =
                visible;
            ApplyMarkerVisibility();
        }

        public void ToggleMarkers()
        {
            SetMarkersVisible(
                !markersVisible);
        }

        private void SubscribeToToggleAction()
        {
            var action =
                toggleMarkersAction != null
                    ? toggleMarkersAction.action
                    : null;

            if (action == null)
            {
                return;
            }

            action.performed +=
                OnToggleMarkersPerformed;

            if (!action.enabled)
            {
                action.Enable();
                toggleActionEnabledByThisComponent =
                    true;
            }
        }

        private void UnsubscribeFromToggleAction()
        {
            var action =
                toggleMarkersAction != null
                    ? toggleMarkersAction.action
                    : null;

            if (action == null)
            {
                toggleActionEnabledByThisComponent =
                    false;
                return;
            }

            action.performed -=
                OnToggleMarkersPerformed;

            if (toggleActionEnabledByThisComponent)
            {
                action.Disable();
            }

            toggleActionEnabledByThisComponent =
                false;
        }

        private void OnToggleMarkersPerformed(
            InputAction.CallbackContext context)
        {
            ToggleMarkers();
        }

        private void ApplyMarkerVisibility()
        {
            if (markerContainer != null &&
                markerContainer.gameObject.activeSelf !=
                    markersVisible)
            {
                markerContainer.gameObject.SetActive(
                    markersVisible);
            }
        }

        private void RemoveMarker(
            CelestialBodyRuntimeContext context)
        {
            if (!markers.TryGetValue(
                    context,
                    out var markerText))
            {
                return;
            }

            markers.Remove(
                context);

            if (markerText != null)
            {
                Destroy(
                    markerText.gameObject);
            }
        }

        private void ClearMarkers()
        {
            foreach (var pair in
                markers)
            {
                if (pair.Value != null)
                {
                    Destroy(
                        pair.Value.gameObject);
                }
            }

            markers.Clear();

            if (!ownsCanvas &&
                markerContainer != null)
            {
                Destroy(
                    markerContainer.gameObject);
            }

            markerContainer = null;
        }

        private readonly struct MarkerCandidate
        {
            public MarkerCandidate(
                Text markerText,
                Rect screenRect,
                float distanceSquared)
            {
                MarkerText =
                    markerText;
                ScreenRect =
                    screenRect;
                DistanceSquared =
                    distanceSquared;
            }

            public Text MarkerText
            {
                get;
            }

            public Rect ScreenRect
            {
                get;
            }

            public float DistanceSquared
            {
                get;
            }
        }

        private static string GetInstanceId(
            CelestialBodyRuntimeContext context)
        {
            return
                !string.IsNullOrWhiteSpace(
                    context.InstanceId)
                    ? context.InstanceId
                    : "body-instance";
        }

        private static string GetMarkerObjectName(
            CelestialBodyRuntimeContext context)
        {
            return
                GetInstanceId(
                    context) +
                " Marker";
        }
    }
}
