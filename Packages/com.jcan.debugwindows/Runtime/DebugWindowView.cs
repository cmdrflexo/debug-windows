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
        private TMP_Text collapseLabel;
        private Vector2 dragStartPosition;
        private Vector2 pointerStartPosition;
        private DebugWindowDisplayState state;

        public string UniqueId => registration.UniqueId;
        public DebugWindowDisplayState State => state;
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
            gameObject.SetActive(state != DebugWindowDisplayState.Closed);
            contentRoot.gameObject.SetActive(state == DebugWindowDisplayState.Open);
            collapseLabel.text = state == DebugWindowDisplayState.Open ? "−" : "+";

            if (gameObject.activeSelf)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                manager.ClampToCanvas(this);
            }

            if (save)
                manager.NotifyWindowStateChanged(this);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.SetAsLastSibling();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
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
            manager.ClampToCanvas(this);
            manager.SaveLayout();
        }

        internal RectTransform RectTransform => rectTransform;

        private void BuildUi()
        {
            rectTransform.anchorMin = new Vector2(0.0f, 1.0f);
            rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
            rectTransform.pivot = new Vector2(0.0f, 1.0f);

            DebugWindowUi.AddImage(gameObject, manager.WindowColor);
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
            var titleImage = DebugWindowUi.AddImage(titleBar.gameObject, manager.TitleColor);
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

            if (registration.CanClose)
            {
                DebugWindowUi.CreateButton(
                    "Close",
                    titleBar,
                    "×",
                    manager.TitleTextSize,
                    manager.ButtonColor,
                    manager.TextColor,
                    () => manager.SetWindowState(UniqueId, DebugWindowDisplayState.Closed),
                    manager.TitleTextSize + 8.0f);
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
