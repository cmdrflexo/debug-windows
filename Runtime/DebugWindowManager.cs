/*
 * Registers portable runtime debug windows, generates their uGUI views, and persists current layouts to JSON.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace jcan.DebugWindows
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class DebugWindowManager : MonoBehaviour
    {
        public const string CoreWindowId = "jcan.debugwindows.core";
        public const string FpsWindowId = "jcan.debugwindows.fps";

        [Serializable]
        private sealed class SavedLayout
        {
            public int version = 1;
            public List<SavedWindow> windows = new List<SavedWindow>();
        }

        [Serializable]
        private sealed class SavedWindow
        {
            public string uniqueId;
            public DebugWindowDisplayState state;
            public float positionX;
            public float positionY;
            public bool pinned;
            public string customState;
        }

        [Header("Scene")]
        [SerializeField]
        private RectTransform windowsRoot;

        [Header("Typography")]
        [SerializeField]
        [Min(6.0f)]
        private float textSize = 18.0f;

        [SerializeField]
        [Min(6.0f)]
        private float titleTextSize = 18.0f;

        [Header("Layout")]
        [SerializeField]
        [Min(80.0f)]
        private float minimumWindowWidth = 180.0f;

        [SerializeField]
        [Min(0)]
        private int padding = 6;

        [SerializeField]
        [Min(0.0f)]
        private float spacing = 4.0f;

        [SerializeField]
        [Min(0.0f)]
        private float controlHorizontalPadding = 8.0f;

        [SerializeField]
        [Min(0.0f)]
        private float controlVerticalPadding = 6.0f;

        [SerializeField]
        [Min(40.0f)]
        private float fieldLabelWidth = 120.0f;

        [SerializeField]
        [Min(60.0f)]
        private float preferredFieldWidth = 180.0f;

        [Header("Visuals")]
        [SerializeField]
        [Tooltip("Optional 9-sliced sprite used by every generated window background.")]
        private Sprite windowBackgroundSprite;

        [SerializeField]
        [Tooltip("Optional 9-sliced outline sprite used by list and text-input frames.")]
        private Sprite listFrameSprite;

        [SerializeField]
        [Tooltip("Optional 9-sliced sprite used by tab buttons.")]
        private Sprite tabBackgroundSprite;

        [Header("Colors")]
        [SerializeField]
        private Color windowColor = new Color(0.06f, 0.07f, 0.09f, 0.94f);

        [SerializeField]
        private Color titleColor = new Color(0.12f, 0.16f, 0.22f, 1.0f);

        [SerializeField]
        private Color buttonColor = new Color(0.22f, 0.27f, 0.34f, 1.0f);

        [SerializeField]
        [Tooltip("Tint used by framed controls such as tabs, lists, and text fields.")]
        private Color elementColor = new Color(0.06f, 0.07f, 0.09f, 0.94f);

        [SerializeField]
        private Color textColor = Color.white;

        [SerializeField]
        [FormerlySerializedAs("checkColor")]
        private Color accentColor = new Color(0.30f, 0.85f, 0.50f, 1.0f);

        [SerializeField]
        private Color selectionColor = new Color(0.20f, 0.45f, 0.70f, 0.65f);

        [Header("Persistence")]
        [SerializeField]
        private string layoutFileName = "jcan-debug-windows.json";

        [Header("FPS Window")]
        [SerializeField]
        [Min(0.01f)]
        private float fpsSmoothingSeconds = 0.5f;

        [SerializeField]
        [Min(0.0f)]
        private float fpsRefreshIntervalSeconds = 0.1f;

        [Header("Runtime")]
        [SerializeField]
        private string layoutFilePath;

        [SerializeField]
        private string lastPersistenceError;

        private readonly Dictionary<string, DebugWindowRegistration> registrations =
            new Dictionary<string, DebugWindowRegistration>(StringComparer.Ordinal);
        private readonly Dictionary<string, DebugWindowView> views =
            new Dictionary<string, DebugWindowView>(StringComparer.Ordinal);
        private readonly Dictionary<string, SavedWindow> loadedStates =
            new Dictionary<string, SavedWindow>(StringComparer.Ordinal);

        private RectTransform coreContentRoot;
        private float smoothedUnscaledDeltaTime;
        private bool initialized;
        private bool applicationIsQuitting;
        private bool menuVisible = true;

        public static DebugWindowManager Instance { get; private set; }

        public RectTransform WindowsRoot => windowsRoot;
        public float TextSize => textSize;
        public float TitleTextSize => titleTextSize;
        public float MinimumWindowWidth => minimumWindowWidth;
        public int Padding => padding;
        public float Spacing => spacing;
        public float ControlHorizontalPadding => controlHorizontalPadding;
        public float ControlVerticalPadding => controlVerticalPadding;
        public float FieldLabelWidth => fieldLabelWidth;
        public float PreferredFieldWidth => preferredFieldWidth;
        public Sprite WindowBackgroundSprite => windowBackgroundSprite;
        public Sprite ListFrameSprite => listFrameSprite;
        public Sprite TabBackgroundSprite => tabBackgroundSprite;
        public Color WindowColor => windowColor;
        public Color TitleColor => titleColor;
        public Color ButtonColor => buttonColor;
        public Color ElementColor => elementColor;
        public Color TextColor => textColor;
        public Color AccentColor => accentColor;
        public Color SelectionColor => selectionColor;
        public string LayoutFilePath => layoutFilePath;
        public string LastPersistenceError => lastPersistenceError;
        public bool MenuVisible => menuVisible;

        public event Action<bool> MenuVisibilityChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one DebugWindowManager can be active at a time.", this);
                enabled = false;
                return;
            }

            Instance = this;
            ResolveWindowsRoot();
            layoutFilePath = Path.Combine(
                Application.persistentDataPath,
                string.IsNullOrWhiteSpace(layoutFileName)
                    ? "jcan-debug-windows.json"
                    : layoutFileName.Trim());

            LoadLayout();
            DebugWindowRegistry.WindowRegistered += HandleRegisteredWindow;
            DebugWindowRegistry.WindowUnregistered += HandleUnregisteredWindow;
            RegisterBuiltInWindows();

            foreach (var registration in DebugWindowRegistry.GetRegistrations())
                RegisterWindow(registration);

            initialized = windowsRoot != null;

            if (initialized)
                StartCoroutine(FinalizeInitialLayout());
        }

        private void Update()
        {
            var deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= Mathf.Epsilon)
                return;

            if (smoothedUnscaledDeltaTime <= Mathf.Epsilon)
            {
                smoothedUnscaledDeltaTime = deltaTime;
                return;
            }

            var blend = 1.0f - Mathf.Exp(
                -deltaTime / Mathf.Max(0.01f, fpsSmoothingSeconds));
            smoothedUnscaledDeltaTime = Mathf.Lerp(
                smoothedUnscaledDeltaTime,
                deltaTime,
                blend);
        }

        private void OnDisable()
        {
            if (initialized && !applicationIsQuitting)
                SaveLayout();
        }

        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
            SaveLayout();
        }

        private void OnDestroy()
        {
            DebugWindowRegistry.WindowRegistered -= HandleRegisteredWindow;
            DebugWindowRegistry.WindowUnregistered -= HandleUnregisteredWindow;

            if (Instance == this)
                Instance = null;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!initialized)
                return;

            foreach (var view in views.Values)
                ClampToCanvas(view);
        }

        public bool RegisterWindow(DebugWindowRegistration registration)
        {
            if (registration == null)
                throw new ArgumentNullException(nameof(registration));

            if (registrations.ContainsKey(registration.UniqueId))
            {
                Debug.LogError($"A debug window with ID '{registration.UniqueId}' is already registered.", this);
                return false;
            }

            registrations.Add(registration.UniqueId, registration);

            if (windowsRoot == null)
            {
                Debug.LogError("DebugWindowManager requires a Debug Windows RectTransform.", this);
                return false;
            }

            var windowRect = DebugWindowUi.CreateRect(registration.Title, windowsRoot);
            var view = windowRect.gameObject.AddComponent<DebugWindowView>();
            view.Initialize(this, registration);
            views.Add(registration.UniqueId, view);

            if (loadedStates.TryGetValue(registration.UniqueId, out var saved))
            {
                registration.RestoreCustomState?.Invoke(saved.customState);
                view.SetPosition(new Vector2(saved.positionX, saved.positionY), false);
                view.SetState(saved.state, false);
                view.SetPinned(saved.pinned, false);
            }
            else
            {
                view.SetPosition(registration.DefaultPosition, false);
                view.SetState(registration.DefaultState, false);
                view.SetPinned(false, false);
            }

            if (registration.UniqueId != CoreWindowId)
                RebuildCoreWindowList();
            return true;
        }

        public bool UnregisterWindow(string uniqueId)
        {
            if (string.IsNullOrWhiteSpace(uniqueId) || uniqueId == CoreWindowId)
                return false;

            if (!registrations.Remove(uniqueId))
                return false;

            if (views.Remove(uniqueId, out var view) && view != null)
                Destroy(view.gameObject);

            loadedStates.Remove(uniqueId);
            RebuildCoreWindowList();
            SaveLayout();
            return true;
        }

        public bool SetWindowState(string uniqueId, DebugWindowDisplayState state)
        {
            if (!views.TryGetValue(uniqueId, out var view))
                return false;

            view.SetState(state, true);
            return true;
        }

        public bool TryGetWindowState(string uniqueId, out DebugWindowDisplayState state)
        {
            if (views.TryGetValue(uniqueId, out var view))
            {
                state = view.State;
                return true;
            }

            state = DebugWindowDisplayState.Closed;
            return false;
        }

        public void ToggleCollapsed(string uniqueId)
        {
            if (!views.TryGetValue(uniqueId, out var view))
                return;

            view.SetState(
                view.State == DebugWindowDisplayState.Open
                    ? DebugWindowDisplayState.Collapsed
                    : DebugWindowDisplayState.Open,
                true);
        }

        public void TogglePinned(string uniqueId)
        {
            if (views.TryGetValue(uniqueId, out var view))
                view.SetPinned(!view.Pinned, true);
        }

        public void ToggleMenuVisibility()
        {
            SetMenuVisible(!menuVisible);
        }

        public void SetMenuVisible(bool visible)
        {
            if (menuVisible == visible)
                return;

            menuVisible = visible;
            foreach (var view in views.Values)
                view.RefreshPresentation();

            MenuVisibilityChanged?.Invoke(menuVisible);
        }

        public void ShowConfirmation(
            string message,
            Action confirmed,
            Action cancelled = null,
            string confirmText = "Confirm",
            string cancelText = "Cancel",
            Vector2? minimumSize = null)
        {
            DebugConfirmationDialog.Show(
                this,
                message,
                confirmed,
                cancelled,
                confirmText,
                cancelText,
                minimumSize ?? new Vector2(300.0f, 140.0f));
        }

        public void SaveLayout()
        {
            if (!initialized || string.IsNullOrWhiteSpace(layoutFilePath))
                return;

            var layout = new SavedLayout();
            foreach (var pair in views)
            {
                var view = pair.Value;
                if (view == null)
                    continue;

                registrations.TryGetValue(pair.Key, out var registration);
                layout.windows.Add(new SavedWindow
                {
                    uniqueId = pair.Key,
                    state = view.State,
                    positionX = view.Position.x,
                    positionY = view.Position.y,
                    pinned = view.Pinned,
                    customState = registration?.CaptureCustomState?.Invoke()
                });
            }

            layout.windows.Sort((first, second) =>
                string.CompareOrdinal(first.uniqueId, second.uniqueId));

            try
            {
                var directory = Path.GetDirectoryName(layoutFilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(layoutFilePath, JsonUtility.ToJson(layout, true));
                lastPersistenceError = string.Empty;

                loadedStates.Clear();
                for (var i = 0; i < layout.windows.Count; i++)
                    loadedStates[layout.windows[i].uniqueId] = layout.windows[i];
            }
            catch (Exception exception)
            {
                lastPersistenceError = exception.Message;
                Debug.LogWarning($"Could not save debug window layout: {exception.Message}", this);
            }
        }

        private void LoadLayout()
        {
            loadedStates.Clear();
            lastPersistenceError = string.Empty;

            if (string.IsNullOrWhiteSpace(layoutFilePath) ||
                !File.Exists(layoutFilePath))
                return;

            try
            {
                var layout = JsonUtility.FromJson<SavedLayout>(
                    File.ReadAllText(layoutFilePath));

                if (layout?.windows == null)
                    return;

                for (var i = 0; i < layout.windows.Count; i++)
                {
                    var saved = layout.windows[i];
                    if (saved == null ||
                        string.IsNullOrWhiteSpace(saved.uniqueId) ||
                        !Enum.IsDefined(typeof(DebugWindowDisplayState), saved.state) ||
                        !IsFinite(saved.positionX) ||
                        !IsFinite(saved.positionY))
                        continue;

                    // The last valid duplicate wins. The next save writes one current entry.
                    loadedStates[saved.uniqueId] = saved;
                }
            }
            catch (Exception exception)
            {
                lastPersistenceError = exception.Message;
                Debug.LogWarning($"Could not load debug window layout: {exception.Message}", this);
            }
        }

        internal void NotifyWindowStateChanged(DebugWindowView changedView)
        {
            if (changedView != null && changedView.UniqueId != CoreWindowId)
                RebuildCoreWindowList();
            SaveLayout();
        }

        internal void ClampToCanvas(DebugWindowView view)
        {
            if (windowsRoot == null || view == null || !view.gameObject.activeSelf)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.RectTransform);
            var rootSize = windowsRoot.rect.size;
            var windowSize = view.RectTransform.rect.size;
            var position = view.RectTransform.anchoredPosition;
            position.x = Mathf.Clamp(position.x, 0.0f, Mathf.Max(0.0f, rootSize.x - windowSize.x));
            position.y = Mathf.Clamp(position.y, -Mathf.Max(0.0f, rootSize.y - windowSize.y), 0.0f);
            view.RectTransform.anchoredPosition = position;
        }

        private IEnumerator FinalizeInitialLayout()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (var view in views.Values)
                ClampToCanvas(view);
        }

        private void ResolveWindowsRoot()
        {
            if (windowsRoot != null)
                return;

            var candidate = transform.parent != null
                ? transform.parent.Find("Debug Windows") as RectTransform
                : null;
            windowsRoot = candidate;
        }

        private void RegisterBuiltInWindows()
        {
            RegisterWindow(new DebugWindowRegistration(
                FpsWindowId,
                "FPS",
                BuildFpsContent,
                DebugWindowDisplayState.Open,
                new Vector2(12.0f, -118.0f)));

            RegisterWindow(new DebugWindowRegistration(
                CoreWindowId,
                "Debug Windows",
                BuildCoreContent,
                DebugWindowDisplayState.Open,
                new Vector2(12.0f, -12.0f),
                false));
        }

        private void HandleRegisteredWindow(DebugWindowRegistration registration)
        {
            RegisterWindow(registration);
        }

        private void HandleUnregisteredWindow(string uniqueId)
        {
            UnregisterWindow(uniqueId);
        }

        private void BuildFpsContent(DebugWindowContent content)
        {
            content.AddUpdatingText(FormatFps, fpsRefreshIntervalSeconds);
        }

        private string FormatFps()
        {
            if (smoothedUnscaledDeltaTime <= Mathf.Epsilon)
                return "FPS: --\nFrame: -- ms";

            return $"FPS: {1.0f / smoothedUnscaledDeltaTime:N0}\n" +
                $"Frame: {smoothedUnscaledDeltaTime * 1000.0f:N2} ms";
        }

        private void BuildCoreContent(DebugWindowContent content)
        {
            coreContentRoot = content.Root;
            PopulateCoreWindowList();
        }

        private void RebuildCoreWindowList()
        {
            if (coreContentRoot == null)
                return;

            for (var i = coreContentRoot.childCount - 1; i >= 0; i--)
            {
                var child = coreContentRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            PopulateCoreWindowList();
            if (views.TryGetValue(CoreWindowId, out var coreView) && coreView.gameObject.activeSelf)
                ClampToCanvas(coreView);
        }

        private void PopulateCoreWindowList()
        {
            if (coreContentRoot == null)
                return;

            var windowList = new List<DebugWindowRegistration>();
            foreach (var registration in registrations.Values)
            {
                if (registration.UniqueId != CoreWindowId)
                    windowList.Add(registration);
            }

            windowList.Sort((first, second) =>
                string.Compare(first.Title, second.Title, StringComparison.OrdinalIgnoreCase));

            for (var i = 0; i < windowList.Count; i++)
            {
                var registration = windowList[i];
                var uniqueId = registration.UniqueId;
                var isOpen = views.TryGetValue(uniqueId, out var view) &&
                    view.State != DebugWindowDisplayState.Closed;

                DebugWindowUi.CreateToggle(
                    registration.Title,
                    coreContentRoot,
                    registration.Title,
                    isOpen,
                    textSize,
                    textColor,
                    buttonColor,
                    accentColor,
                    value => SetWindowState(
                        uniqueId,
                        value ? DebugWindowDisplayState.Open : DebugWindowDisplayState.Closed),
                    controlVerticalPadding);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
