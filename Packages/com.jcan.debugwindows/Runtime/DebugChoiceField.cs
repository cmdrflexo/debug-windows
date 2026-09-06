/*
 * Provides a compact code-defined choice field with a framed, scrollable popup list.
 */

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace jcan.DebugWindows
{
    public sealed class DebugChoiceOption
    {
        public DebugChoiceOption(string uniqueId, string displayText, object value = null)
        {
            if (string.IsNullOrWhiteSpace(uniqueId))
                throw new ArgumentException("A choice option requires a unique ID.", nameof(uniqueId));

            UniqueId = uniqueId.Trim();
            DisplayText = string.IsNullOrWhiteSpace(displayText) ? UniqueId : displayText.Trim();
            Value = value;
        }

        public string UniqueId { get; }
        public string DisplayText { get; }
        public object Value { get; }
    }

    public sealed class DebugChoiceField : MonoBehaviour
    {
        private DebugWindowManager manager;
        private IReadOnlyList<DebugChoiceOption> options;
        private Action<DebugChoiceOption> changed;
        private TMP_Text caption;
        private RectTransform popup;
        private string selectedId;

        internal static DebugChoiceField Create(
            DebugWindowManager manager,
            RectTransform parent,
            IReadOnlyList<DebugChoiceOption> options,
            string selectedId,
            Action<DebugChoiceOption> changed)
        {
            var frame = DebugWindowUi.CreateRect("Choice", parent);
            var size = frame.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = manager.MinimumWindowWidth * 0.65f;
            size.preferredHeight = manager.TextSize + 8.0f;

            var image = DebugWindowUi.AddImage(frame.gameObject, manager.ElementColor);
            if (manager.ListFrameSprite != null)
            {
                image.sprite = manager.ListFrameSprite;
                image.type = Image.Type.Sliced;
            }

            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var field = frame.gameObject.AddComponent<DebugChoiceField>();
            field.manager = manager;
            field.options = options ?? Array.Empty<DebugChoiceOption>();
            field.selectedId = selectedId;
            field.changed = changed;

            field.caption = DebugWindowUi.CreateText(
                "Value", frame, string.Empty, manager.TextSize,
                manager.TextColor, TextAlignmentOptions.MidlineLeft);
            DebugWindowUi.Stretch(field.caption.rectTransform);
            field.caption.rectTransform.offsetMin = new Vector2(6.0f, 1.0f);
            field.caption.rectTransform.offsetMax = new Vector2(-20.0f, -1.0f);
            field.caption.raycastTarget = false;

            var arrow = DebugWindowUi.CreateText(
                "Arrow", frame, "▼", manager.TextSize * 0.7f,
                manager.TextColor, TextAlignmentOptions.Center);
            arrow.rectTransform.anchorMin = new Vector2(1.0f, 0.0f);
            arrow.rectTransform.anchorMax = Vector2.one;
            arrow.rectTransform.pivot = new Vector2(1.0f, 0.5f);
            arrow.rectTransform.sizeDelta = new Vector2(18.0f, 0.0f);
            arrow.rectTransform.anchoredPosition = Vector2.zero;
            arrow.raycastTarget = false;

            button.onClick.AddListener(field.TogglePopup);
            field.RefreshCaption();
            return field;
        }

        public string SelectedId => selectedId;

        public void SetSelection(string uniqueId, bool notify = false)
        {
            selectedId = uniqueId;
            RefreshCaption();
            if (notify)
                changed?.Invoke(FindSelected());
        }

        private void OnDisable()
        {
            ClosePopup();
        }

        private void OnDestroy()
        {
            ClosePopup();
        }

        private void TogglePopup()
        {
            if (popup != null)
            {
                ClosePopup();
                return;
            }

            BuildPopup();
        }

        private void BuildPopup()
        {
            if (manager == null || manager.WindowsRoot == null || options.Count == 0)
                return;

            popup = DebugWindowUi.CreateRect("Choice Popup", manager.WindowsRoot);
            popup.anchorMin = new Vector2(0.0f, 1.0f);
            popup.anchorMax = new Vector2(0.0f, 1.0f);
            popup.pivot = new Vector2(0.0f, 1.0f);
            popup.SetAsLastSibling();

            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            popup.position = corners[0];
            var visibleRows = Mathf.Min(6, options.Count);
            popup.sizeDelta = new Vector2(
                ((RectTransform)transform).rect.width,
                visibleRows * (manager.TextSize + 4.0f) + 4.0f);

            var popupImage = DebugWindowUi.AddImage(popup.gameObject, manager.ElementColor);
            if (manager.ListFrameSprite != null)
            {
                popupImage.sprite = manager.ListFrameSprite;
                popupImage.type = Image.Type.Sliced;
            }

            var viewport = DebugWindowUi.CreateRect("Viewport", popup);
            DebugWindowUi.Stretch(viewport);
            viewport.offsetMin = new Vector2(4.0f, 2.0f);
            viewport.offsetMax = new Vector2(options.Count > visibleRows ? -8.0f : -4.0f, -2.0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var list = DebugWindowUi.CreateRect("Content", viewport);
            list.anchorMin = new Vector2(0.0f, 1.0f);
            list.anchorMax = new Vector2(1.0f, 1.0f);
            list.pivot = new Vector2(0.5f, 1.0f);
            list.offsetMin = Vector2.zero;
            list.offsetMax = Vector2.zero;
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            for (var i = 0; i < options.Count; i++)
                BuildOption(list, options[i]);

            var scroll = popup.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = list;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = manager.TextSize;

            if (options.Count > visibleRows)
                BuildScrollbar(scroll);
        }

        private void BuildOption(RectTransform parent, DebugChoiceOption option)
        {
            var row = DebugWindowUi.CreateRect(option.UniqueId, parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = manager.TextSize + 4.0f;
            var image = DebugWindowUi.AddImage(
                row.gameObject,
                option.UniqueId == selectedId ? manager.SelectionColor : Color.clear);
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var label = DebugWindowUi.CreateText(
                "Label", row, option.DisplayText, manager.TextSize,
                manager.TextColor, TextAlignmentOptions.MidlineLeft);
            DebugWindowUi.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(3.0f, 0.0f);
            label.raycastTarget = false;
            button.onClick.AddListener(() => Select(option));
        }

        private void BuildScrollbar(ScrollRect scroll)
        {
            var bar = DebugWindowUi.CreateRect("Scrollbar", popup);
            bar.anchorMin = new Vector2(1.0f, 0.0f);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(1.0f, 0.5f);
            bar.sizeDelta = new Vector2(4.0f, -4.0f);
            bar.anchoredPosition = new Vector2(-2.0f, 0.0f);
            var handle = DebugWindowUi.CreateRect("Handle", bar);
            DebugWindowUi.Stretch(handle);
            var handleImage = DebugWindowUi.AddImage(handle.gameObject, manager.AccentColor);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void Select(DebugChoiceOption option)
        {
            selectedId = option.UniqueId;
            RefreshCaption();
            ClosePopup();
            changed?.Invoke(option);
        }

        private void RefreshCaption()
        {
            if (caption == null)
                return;
            var selected = FindSelected();
            caption.text = selected?.DisplayText ?? "None";
        }

        private DebugChoiceOption FindSelected()
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i] != null && options[i].UniqueId == selectedId)
                    return options[i];
            }
            return null;
        }

        private void ClosePopup()
        {
            if (popup == null)
                return;
            Destroy(popup.gameObject);
            popup = null;
        }
    }
}
