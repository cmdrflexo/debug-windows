/*
 * Provides a reusable scrollable list with side actions, a text input, and one input action.
 */

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace jcan.DebugWindows
{
    public sealed class DebugListItem
    {
        public DebugListItem(string uniqueId, string displayName)
        {
            UniqueId = uniqueId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }

        public string UniqueId { get; }
        public string DisplayName { get; }
    }

    public sealed class DebugListAction
    {
        public DebugListAction(string actionId, string text)
        {
            ActionId = actionId ?? string.Empty;
            Text = text ?? string.Empty;
        }

        public string ActionId { get; }
        public string Text { get; }
    }

    public sealed class DebugListActionInvocation
    {
        internal DebugListActionInvocation(
            string actionId,
            IReadOnlyList<DebugListItem> items,
            DebugListItem selectedItem,
            string inputText)
        {
            ActionId = actionId;
            Items = items;
            SelectedItem = selectedItem;
            InputText = inputText;
        }

        public string ActionId { get; }
        public IReadOnlyList<DebugListItem> Items { get; }
        public DebugListItem SelectedItem { get; }
        public string InputText { get; }
    }

    public sealed class DebugListActionWindow
    {
        private readonly List<DebugListItem> items = new List<DebugListItem>();
        private string inputText = string.Empty;

        public DebugListActionWindow(
            string uniqueId,
            string title,
            IReadOnlyList<DebugListAction> sideActions,
            DebugListAction inputAction,
            Vector2 preferredContentSize,
            DebugWindowDisplayState defaultState = DebugWindowDisplayState.Closed,
            Vector2? defaultPosition = null)
        {
            if (string.IsNullOrWhiteSpace(uniqueId))
                throw new ArgumentException("A list-action window requires a unique ID.", nameof(uniqueId));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("A list-action window requires a title.", nameof(title));

            UniqueId = uniqueId.Trim();
            Title = title.Trim();
            SideActions = sideActions ?? Array.Empty<DebugListAction>();
            InputAction = inputAction;
            PreferredContentSize = new Vector2(
                Mathf.Max(180.0f, preferredContentSize.x),
                Mathf.Max(120.0f, preferredContentSize.y));
            DefaultState = defaultState;
            DefaultPosition = defaultPosition ?? Vector2.zero;
        }

        public string UniqueId { get; }
        public string Title { get; }
        public IReadOnlyList<DebugListAction> SideActions { get; }
        public DebugListAction InputAction { get; }
        public Vector2 PreferredContentSize { get; }
        public DebugWindowDisplayState DefaultState { get; }
        public Vector2 DefaultPosition { get; }
        public IReadOnlyList<DebugListItem> Items => items;
        public string InputText => inputText;

        public event Action<DebugListActionInvocation> ActionInvoked;
        internal event Action ItemsChanged;
        internal event Action<string> InputTextChanged;

        public DebugWindowRegistration CreateRegistration()
        {
            return new DebugWindowRegistration(
                UniqueId,
                Title,
                BuildContent,
                DefaultState,
                DefaultPosition);
        }

        public void SetInputText(string value)
        {
            inputText = value ?? string.Empty;
            InputTextChanged?.Invoke(inputText);
        }

        public void SetItems(IEnumerable<DebugListItem> newItems)
        {
            items.Clear();
            if (newItems != null)
            {
                foreach (var item in newItems)
                {
                    if (item != null)
                        items.Add(item);
                }
            }

            ItemsChanged?.Invoke();
        }

        private void BuildContent(DebugWindowContent content)
        {
            DebugListActionControl.Create(content, this);
        }

        internal void UpdateInputText(string value)
        {
            inputText = value ?? string.Empty;
        }

        internal void Invoke(string actionId, DebugListItem selectedItem)
        {
            ActionInvoked?.Invoke(new DebugListActionInvocation(
                actionId,
                items.ToArray(),
                selectedItem,
                inputText));
        }
    }

    internal sealed class DebugListActionControl : MonoBehaviour
    {
        private DebugWindowContent windowContent;
        private DebugListActionWindow definition;
        private RectTransform listContent;
        private TMP_InputField input;
        private DebugListItem selectedItem;

        public static void Create(
            DebugWindowContent content,
            DebugListActionWindow definition)
        {
            var root = DebugWindowUi.CreateRect("List Actions", content.Root);
            var control = root.gameObject.AddComponent<DebugListActionControl>();
            control.Initialize(content, definition, root);
        }

        private void Initialize(
            DebugWindowContent content,
            DebugListActionWindow newDefinition,
            RectTransform root)
        {
            windowContent = content;
            definition = newDefinition;

            var size = root.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = definition.PreferredContentSize.x;
            size.preferredHeight = definition.PreferredContentSize.y;

            var vertical = root.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = content.Manager.Spacing;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            BuildMainRow(root);
            BuildInputRow(root);
            definition.ItemsChanged += RebuildItems;
            definition.InputTextChanged += SetInputText;
            SetInputText(definition.InputText);
            RebuildItems();
        }

        private void OnDestroy()
        {
            if (definition != null)
            {
                definition.ItemsChanged -= RebuildItems;
                definition.InputTextChanged -= SetInputText;
            }
        }

        private void BuildMainRow(RectTransform parent)
        {
            var row = DebugWindowUi.CreateRect("Main Row", parent);
            var rowSize = row.gameObject.AddComponent<LayoutElement>();
            rowSize.flexibleHeight = 1.0f;

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = windowContent.Manager.Spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            BuildList(row);
            BuildSideActions(row);
        }

        private void BuildList(RectTransform parent)
        {
            var frame = DebugWindowUi.CreateRect("List", parent);
            var frameSize = frame.gameObject.AddComponent<LayoutElement>();
            frameSize.flexibleWidth = 1.0f;
            frameSize.flexibleHeight = 1.0f;

            var frameImage = DebugWindowUi.AddImage(frame.gameObject, windowContent.Manager.ElementColor);
            if (windowContent.Manager.ListFrameSprite != null)
            {
                frameImage.sprite = windowContent.Manager.ListFrameSprite;
                frameImage.type = Image.Type.Sliced;
                frameImage.color = windowContent.Manager.ElementColor;
            }

            var viewport = DebugWindowUi.CreateRect("Viewport", frame);
            DebugWindowUi.Stretch(viewport);
            viewport.offsetMin = new Vector2(6.0f, 6.0f);
            viewport.offsetMax = new Vector2(-12.0f, -6.0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            listContent = DebugWindowUi.CreateRect("Content", viewport);
            listContent.anchorMin = new Vector2(0.0f, 1.0f);
            listContent.anchorMax = new Vector2(1.0f, 1.0f);
            listContent.pivot = new Vector2(0.5f, 1.0f);
            listContent.offsetMin = Vector2.zero;
            listContent.offsetMax = Vector2.zero;
            var listLayout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var scrollbarRect = DebugWindowUi.CreateRect("Scrollbar", frame);
            scrollbarRect.anchorMin = new Vector2(1.0f, 0.0f);
            scrollbarRect.anchorMax = Vector2.one;
            scrollbarRect.pivot = new Vector2(1.0f, 0.5f);
            scrollbarRect.sizeDelta = new Vector2(4.0f, 0.0f);
            scrollbarRect.anchoredPosition = Vector2.zero;

            var slidingArea = DebugWindowUi.CreateRect("Sliding Area", scrollbarRect);
            DebugWindowUi.Stretch(slidingArea);
            var handle = DebugWindowUi.CreateRect("Handle", slidingArea);
            DebugWindowUi.Stretch(handle);
            var handleImage = DebugWindowUi.AddImage(
                handle.gameObject,
                windowContent.Manager.AccentColor);

            var scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            var scrollRect = frame.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = listContent;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = windowContent.Manager.TextSize;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void BuildSideActions(RectTransform parent)
        {
            var actions = DebugWindowUi.CreateRect("Actions", parent);
            var layout = actions.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = windowContent.Manager.Spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (var i = 0; i < definition.SideActions.Count; i++)
            {
                var action = definition.SideActions[i];
                if (action == null)
                    continue;

                var captured = action;
                DebugWindowUi.CreateButton(
                    captured.ActionId,
                    actions,
                    captured.Text,
                    windowContent.Manager.TextSize,
                    windowContent.Manager.ButtonColor,
                    windowContent.Manager.TextColor,
                    () => Invoke(captured.ActionId),
                    Mathf.Max(64.0f, windowContent.Manager.MinimumWindowWidth * 0.4f),
                    windowContent.Manager.ControlHorizontalPadding,
                    windowContent.Manager.ControlVerticalPadding);
            }
        }

        private void BuildInputRow(RectTransform parent)
        {
            var row = DebugWindowUi.CreateRect("Input Row", parent);
            var rowSize = row.gameObject.AddComponent<LayoutElement>();
            rowSize.preferredHeight =
                windowContent.Manager.TextSize +
                windowContent.Manager.ControlVerticalPadding * 2.0f;
            rowSize.flexibleHeight = 0.0f;

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = windowContent.Manager.Spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var inputFrame = DebugWindowUi.CreateRect("Input", row);
            var inputSize = inputFrame.gameObject.AddComponent<LayoutElement>();
            inputSize.flexibleWidth = 1.0f;
            inputSize.preferredHeight =
                windowContent.Manager.TextSize +
                windowContent.Manager.ControlVerticalPadding * 2.0f;
            var inputImage = DebugWindowUi.AddImage(inputFrame.gameObject, windowContent.Manager.ElementColor);
            if (windowContent.Manager.ListFrameSprite != null)
            {
                inputImage.sprite = windowContent.Manager.ListFrameSprite;
                inputImage.type = Image.Type.Sliced;
                inputImage.color = windowContent.Manager.ElementColor;
            }

            var textArea = DebugWindowUi.CreateRect("Text Area", inputFrame);
            DebugWindowUi.Stretch(textArea);
            textArea.offsetMin = new Vector2(
                windowContent.Manager.ControlHorizontalPadding,
                windowContent.Manager.ControlVerticalPadding * 0.5f);
            textArea.offsetMax = new Vector2(
                -windowContent.Manager.ControlHorizontalPadding,
                -windowContent.Manager.ControlVerticalPadding * 0.5f);
            textArea.gameObject.AddComponent<RectMask2D>();

            var text = DebugWindowUi.CreateText(
                "Text",
                textArea,
                string.Empty,
                windowContent.Manager.TextSize,
                windowContent.Manager.TextColor,
                TextAlignmentOptions.MidlineLeft);
            DebugWindowUi.Stretch(text.rectTransform);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;

            input = inputFrame.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = textArea;
            input.textComponent = text;
            input.targetGraphic = inputImage;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            input.customCaretColor = true;
            input.caretColor = windowContent.Manager.AccentColor;
            input.selectionColor = windowContent.Manager.SelectionColor;
            input.caretWidth = 2;
            input.onValueChanged.AddListener(definition.UpdateInputText);

            var action = definition.InputAction;
            if (action != null)
            {
                var submitActionId = action.ActionId;
                input.onSubmit.AddListener(_ => Invoke(submitActionId));
            }


            if (action != null)
            {
                DebugWindowUi.CreateButton(
                    action.ActionId,
                    row,
                    action.Text,
                    windowContent.Manager.TextSize,
                    windowContent.Manager.ButtonColor,
                    windowContent.Manager.TextColor,
                    () => Invoke(action.ActionId),
                    Mathf.Max(64.0f, windowContent.Manager.MinimumWindowWidth * 0.4f),
                    windowContent.Manager.ControlHorizontalPadding,
                    windowContent.Manager.ControlVerticalPadding);
            }
        }

        private void RebuildItems()
        {
            var selectedId = selectedItem?.UniqueId;
            selectedItem = null;

            for (var i = listContent.childCount - 1; i >= 0; i--)
                Destroy(listContent.GetChild(i).gameObject);

            for (var i = 0; i < definition.Items.Count; i++)
            {
                var item = definition.Items[i];
                var row = DebugWindowUi.CreateRect(item.DisplayName, listContent);
                var image = DebugWindowUi.AddImage(row.gameObject, Color.clear);
                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                var rowSize = row.gameObject.AddComponent<LayoutElement>();
                rowSize.preferredHeight =
                    windowContent.Manager.TextSize +
                    windowContent.Manager.ControlVerticalPadding * 2.0f;

                var label = DebugWindowUi.CreateText(
                    "Label",
                    row,
                    item.DisplayName,
                    windowContent.Manager.TextSize,
                    windowContent.Manager.TextColor,
                    TextAlignmentOptions.MidlineLeft);
                DebugWindowUi.Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(
                    windowContent.Manager.ControlHorizontalPadding,
                    0.0f);
                label.rectTransform.offsetMax = new Vector2(
                    -windowContent.Manager.ControlHorizontalPadding,
                    0.0f);

                var capturedItem = item;
                button.onClick.AddListener(() => Select(capturedItem));

                if (string.Equals(item.UniqueId, selectedId, StringComparison.Ordinal))
                    Select(item, image);
            }
        }

        private void Select(DebugListItem item)
        {
            selectedItem = item;
            definition.SetInputText(item?.DisplayName ?? string.Empty);
            RebuildItems();
        }

        private void Select(DebugListItem item, Image selectedImage)
        {
            selectedItem = item;
            selectedImage.color = windowContent.Manager.SelectionColor;
            definition.SetInputText(item?.DisplayName ?? string.Empty);
        }

        private void SetInputText(string value)
        {
            if (input != null && input.text != value)
                input.SetTextWithoutNotify(value ?? string.Empty);
        }

        private void Invoke(string actionId)
        {
            definition.UpdateInputText(input.text);
            definition.Invoke(actionId, selectedItem);
        }
    }
}
