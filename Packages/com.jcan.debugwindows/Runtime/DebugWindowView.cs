/*
 * Owns one generated debug panel and reports drag and display-state changes to its manager.
 */

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace jcan.DebugWindows
{
    [DisallowMultipleComponent]
    internal sealed class DebugWindowView :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerDownHandler
    {
        private DebugWindowManager manager;
        private DebugWindowRegistration registration;
        private RectTransform rectTransform;
        private RectTransform contentRoot;
        private Image windowImage;
        private Image titleImage;
        private TMP_Text collapseLabel;
        private TMP_Text pinLabel;
        private GameObject collapseButton;
        private GameObject pinButton;
        private GameObject closeButton;
        private Vector2 dragStartPosition;
        private Vector2 pointerStartPosition;
        private DebugWindowDisplayState state;
        private bool pinned;

        public string UniqueId => registration.UniqueId;
        public DebugWindowDisplayState State => state;
        public bool Pinned => pinned;
        public Vector2 Position => rectTransform.anchoredPosition;

        public void Initialize(
            DebugWindowManager newManager,
            DebugWindowRegistration newRegistration)
        {
            manager = newManager;
            registration = newRegistration;
            rectTransform = (RectTransform)transform;
            BuildUi();
        }

        public void SetPosition(Vector2 position, bool save)
        {
            rectTransform.anchoredPosition = position;
            manager.ClampToCanvas(this);
            if (save)
                manager.SaveLayout();
        }

        public void SetState(DebugWindowDisplayState newState, bool save)
        {
            if (!registration.CanClose && newState == DebugWindowDisplayState.Closed)
                newState = DebugWindowDisplayState.Collapsed;

            state = newState;
            RefreshPresentation();

            if (gameObject.activeSelf)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                manager.ClampToCanvas(this);
            }

            if (save)
                manager.NotifyWindowStateChanged(this);
        }

        public void SetPinned(bool value, bool save)
        {
            pinned = value;
            pinLabel.text = pinned ? "●" : "○";
            RefreshPresentation();

            if (save)
                manager.SaveLayout();
        }

        internal void RefreshPresentation()
        {
            var pinnedOverlay = !manager.MenuVisible && pinned &&
                state != DebugWindowDisplayState.Closed;
            var visible = state != DebugWindowDisplayState.Closed &&
                (manager.MenuVisible || pinned);

            gameObject.SetActive(visible);
            if (!visible)
                return;

            windowImage.enabled = !pinnedOverlay;
            titleImage.enabled = !pinnedOverlay;
            collapseButton.SetActive(!pinnedOverlay);
            pinButton.SetActive(!pinnedOverlay);
            if (closeButton != null)
                closeButton.SetActive(!pinnedOverlay);

            contentRoot.gameObject.SetActive(
                pinnedOverlay || state == DebugWindowDisplayState.Open);
            collapseLabel.text = state == DebugWindowDisplayState.Open ? "−" : "+";
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.SetAsLastSibling();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (pinned)
                return;

            transform.SetAsLastSibling();
            dragStartPosition = rectTransform.anchoredPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                manager.WindowsRoot,
                eventData.position,
                eventData.pressEventCamera,
                out pointerStartPosition);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (pinned)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    manager.WindowsRoot,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var currentPointerPosition))
                return;

            rectTransform.anchoredPosition = dragStartPosition +
                currentPointerPosition - pointerStartPosition;
            manager.ClampToCanvas(this);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (pinned)
                return;

            manager.ClampToCanvas(this);
            manager.SaveLayout();
        }

        internal RectTransform RectTransform => rectTransform;

        private void BuildUi()
        {
            rectTransform.anchorMin = new Vector2(0.0f, 1.0f);
            rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
            rectTransform.pivot = new Vector2(0.0f, 1.0f);

            windowImage = DebugWindowUi.AddImage(gameObject, manager.WindowColor);
            if (manager.WindowBackgroundSprite != null)
            {
                windowImage.sprite = manager.WindowBackgroundSprite;
                windowImage.type = Image.Type.Sliced;
            }

            var fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(manager.Padding, manager.Padding, manager.Padding, manager.Padding);
            layout.spacing = manager.Spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var titleBar = DebugWindowUi.CreateRect("Title Bar", transform);
            titleImage = DebugWindowUi.AddImage(titleBar.gameObject, manager.TitleColor);
            titleImage.raycastTarget = true;
            var titleLayout = titleBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            titleLayout.padding = new RectOffset(4, 4, 2, 2);
            titleLayout.spacing = 4.0f;
            titleLayout.childAlignment = TextAnchor.MiddleLeft;
            titleLayout.childControlWidth = true;
            titleLayout.childControlHeight = true;
            titleLayout.childForceExpandWidth = false;
            titleLayout.childForceExpandHeight = true;
            var titleSize = titleBar.gameObject.AddComponent<LayoutElement>();
            titleSize.minWidth = manager.MinimumWindowWidth;
            titleSize.minHeight = manager.TitleTextSize + 8.0f;

            var title = DebugWindowUi.CreateText(
                "Title",
                titleBar,
                registration.Title,
                manager.TitleTextSize,
                manager.TextColor,
                TextAlignmentOptions.MidlineLeft);
            title.fontStyle = FontStyles.Bold;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1.0f;

            var collapse = DebugWindowUi.CreateButton(
                "Collapse",
                titleBar,
                "−",
                manager.TitleTextSize,
                manager.ButtonColor,
                manager.TextColor,
                () => manager.ToggleCollapsed(UniqueId),
                manager.TitleTextSize + 8.0f);
            collapseLabel = collapse.GetComponentInChildren<TMP_Text>();
            collapseButton = collapse.gameObject;

            var pin = DebugWindowUi.CreateButton(
                "Pin",
                titleBar,
                "○",
                manager.TitleTextSize,
                manager.ButtonColor,
                manager.TextColor,
                () => manager.TogglePinned(UniqueId),
                manager.TitleTextSize + 8.0f);
            pinLabel = pin.GetComponentInChildren<TMP_Text>();
            pinButton = pin.gameObject;

            if (registration.CanClose)
            {
                var close = DebugWindowUi.CreateButton(
                    "Close",
                    titleBar,
                    "×",
                    manager.TitleTextSize,
                    manager.ButtonColor,
                    manager.TextColor,
                    () => manager.SetWindowState(UniqueId, DebugWindowDisplayState.Closed),
                    manager.TitleTextSize + 8.0f);
                closeButton = close.gameObject;
            }

            contentRoot = DebugWindowUi.CreateRect("Content", transform);
            var contentLayout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = manager.Spacing;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = false;
            contentLayout.childForceExpandHeight = false;

            registration.BuildContent?.Invoke(new DebugWindowContent(manager, contentRoot));
        }
    }
}
