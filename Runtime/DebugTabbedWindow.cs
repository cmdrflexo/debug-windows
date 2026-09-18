/*
 * Provides code-defined tabbed debug windows with scrollable pages, persistent footer content, and basic form fields.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace jcan.DebugWindows
{
    public sealed class DebugTabbedPage
    {
        public DebugTabbedPage(
            string uniqueId,
            string tabText,
            Action<DebugWindowFormContent> buildContent)
        {
            if (string.IsNullOrWhiteSpace(uniqueId))
                throw new ArgumentException("A tab page requires a unique ID.", nameof(uniqueId));
            if (string.IsNullOrWhiteSpace(tabText))
                throw new ArgumentException("A tab page requires text.", nameof(tabText));

            UniqueId = uniqueId.Trim();
            TabText = tabText.Trim();
            BuildContent = buildContent;
        }

        public string UniqueId { get; }
        public string TabText { get; }
        public Action<DebugWindowFormContent> BuildContent { get; }
    }

    public sealed class DebugTabbedWindow
    {
        private readonly List<DebugTabbedPage> pages;
        private string activePageId;

        public DebugTabbedWindow(
            string uniqueId,
            string title,
            IReadOnlyList<DebugTabbedPage> pages,
            Action<DebugWindowFormContent> buildFooter,
            Vector2 preferredContentSize,
            DebugWindowDisplayState defaultState = DebugWindowDisplayState.Closed,
            Vector2? defaultPosition = null)
        {
            if (string.IsNullOrWhiteSpace(uniqueId))
                throw new ArgumentException("A tabbed window requires a unique ID.", nameof(uniqueId));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("A tabbed window requires a title.", nameof(title));
            if (pages == null || pages.Count == 0)
                throw new ArgumentException("A tabbed window requires at least one page.", nameof(pages));

            UniqueId = uniqueId.Trim();
            Title = title.Trim();
            this.pages = new List<DebugTabbedPage>(pages);
            BuildFooter = buildFooter;
            PreferredContentSize = new Vector2(
                Mathf.Max(220.0f, preferredContentSize.x),
                Mathf.Max(160.0f, preferredContentSize.y));
            DefaultState = defaultState;
            DefaultPosition = defaultPosition ?? Vector2.zero;
            activePageId = this.pages[0].UniqueId;
        }

        public string UniqueId { get; }
        public string Title { get; }
        public IReadOnlyList<DebugTabbedPage> Pages => pages;
        public Action<DebugWindowFormContent> BuildFooter { get; }
        public Vector2 PreferredContentSize { get; }
        public DebugWindowDisplayState DefaultState { get; }
        public Vector2 DefaultPosition { get; }
        public string ActivePageId => activePageId;

        internal event Action ActivePageChanged;

        public DebugWindowRegistration CreateRegistration()
        {
            return new DebugWindowRegistration(
                UniqueId,
                Title,
                BuildContent,
                DefaultState,
                DefaultPosition,
                true,
                () => activePageId,
                RestoreActivePage);
        }

        public bool SetActivePage(string pageId)
        {
            if (string.IsNullOrWhiteSpace(pageId) ||
                !pages.Exists(page => page.UniqueId == pageId) ||
                activePageId == pageId)
            {
                return false;
            }

            activePageId = pageId;
            ActivePageChanged?.Invoke();
            DebugWindowManager.Instance?.SaveLayout();
            return true;
        }

        public void RefreshActivePage()
        {
            ActivePageChanged?.Invoke();
        }

        internal DebugTabbedPage GetActivePage()
        {
            return pages.Find(page => page.UniqueId == activePageId) ?? pages[0];
        }

        private void RestoreActivePage(string pageId)
        {
            if (!string.IsNullOrWhiteSpace(pageId) &&
                pages.Exists(page => page.UniqueId == pageId))
            {
                activePageId = pageId;
                ActivePageChanged?.Invoke();
            }
        }

        private void BuildContent(DebugWindowContent content)
        {
            DebugTabbedWindowControl.Create(content, this);
        }
    }

    public sealed class DebugWindowFormContent
    {
        private readonly DebugWindowManager manager;

        internal DebugWindowFormContent(
            DebugWindowManager manager,
            RectTransform root)
        {
            this.manager = manager;
            Root = root;
        }

        public RectTransform Root { get; }

        public TMP_Text AddReadOnly(string label, Func<string> valueProvider)
        {
            var row = CreateRow(label);
            return CreateUpdatingValue(row, valueProvider);
        }

        public TMP_InputField AddTextField(
            string label,
            string value,
            Action<string> changed)
        {
            var row = CreateRow(label);
            return CreateInput(row, value, changed);
        }

        public TMP_InputField AddNumberField(
            string label,
            double value,
            Action<double> changed,
            string units = null)
        {
            var row = CreateRow(label);
            var input = CreateInput(
                row,
                FormatNumber(value),
                text =>
                {
                    if (double.TryParse(
                            text,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out var parsed))
                    {
                        changed?.Invoke(parsed);
                    }
                },
                true);

            if (!string.IsNullOrWhiteSpace(units))
            {
                DebugWindowUi.CreateText(
                    "Units",
                    row,
                    units,
                    manager.TextSize,
                    manager.TextColor,
                    TextAlignmentOptions.MidlineLeft);
            }

            return input;
        }

        public DebugChoiceField AddChoice(
            string label,
            IReadOnlyList<DebugChoiceOption> options,
            string selectedId,
            Action<DebugChoiceOption> changed)
        {
            var row = CreateRow(label);
            return DebugChoiceField.Create(
                manager,
                row,
                options,
                selectedId,
                changed);
        }

        public DebugSliderField AddSlider(
            string label,
            float value,
            float minimum,
            float maximum,
            Action<float> changed,
            string units = null,
            float step = 0.0f)
        {
            var row = CreateRow(label);
            return DebugSliderField.Create(
                manager,
                row,
                value,
                minimum,
                maximum,
                changed,
                units,
                step);
        }

        public Toggle AddToggle(
            string label,
            bool value,
            Action<bool> changed)
        {
            return DebugWindowUi.CreateToggle(
                label,
                Root,
                label,
                value,
                manager.TextSize,
                manager.TextColor,
                manager.ButtonColor,
                manager.AccentColor,
                changed,
                manager.ControlVerticalPadding);
        }

        public Button AddButton(
            string actionId,
            string text,
            Action clicked,
            string feedbackMessage = null,
            float feedbackDuration = 3.0f)
        {
            return DebugWindowUi.CreateButton(
                actionId,
                Root,
                text,
                manager.TextSize,
                manager.ButtonColor,
                manager.TextColor,
                clicked,
                Mathf.Max(72.0f, manager.MinimumWindowWidth * 0.45f),
                manager.ControlHorizontalPadding,
                manager.ControlVerticalPadding,
                feedbackMessage,
                feedbackDuration);
        }

        private static string FormatNumber(double value)
        {
            if (value == 0.0)
                return "0";

            var magnitude = Math.Abs(value);
            return magnitude >= 1.0e15 || magnitude < 1.0e-6
                ? value.ToString("0.###############E+0", CultureInfo.InvariantCulture)
                : value.ToString("0.###############", CultureInfo.InvariantCulture);
        }

        private RectTransform CreateRow(string label)
        {
            var row = DebugWindowUi.CreateRect(label ?? "Field", Root);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = manager.Spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var name = DebugWindowUi.CreateText(
                "Label",
                row,
                label ?? string.Empty,
                manager.TextSize,
                manager.TextColor,
                TextAlignmentOptions.MidlineLeft);
            var labelSize = name.gameObject.AddComponent<LayoutElement>();
            labelSize.preferredWidth = manager.FieldLabelWidth;
            labelSize.flexibleWidth = 0.0f;
            return row;
        }

        private TMP_Text CreateUpdatingValue(
            RectTransform row,
            Func<string> provider)
        {
            var value = DebugWindowUi.CreateText(
                "Value",
                row,
                provider?.Invoke() ?? string.Empty,
                manager.TextSize,
                manager.TextColor,
                TextAlignmentOptions.MidlineRight);
            value.gameObject.AddComponent<LayoutElement>().preferredWidth =
                manager.PreferredFieldWidth;

            if (provider != null)
            {
                var updater = value.gameObject.AddComponent<DebugWindowTextUpdater>();
                updater.Initialize(value, provider, 0.1f);
            }

            return value;
        }

        private TMP_InputField CreateInput(
            RectTransform row,
            string value,
            Action<string> changed,
            bool rightAligned = false)
        {
            var frame = DebugWindowUi.CreateRect("Input", row);
            var frameSize = frame.gameObject.AddComponent<LayoutElement>();
            frameSize.preferredWidth = manager.PreferredFieldWidth;
            frameSize.preferredHeight = manager.TextSize + manager.ControlVerticalPadding * 2.0f;

            var image = DebugWindowUi.AddImage(frame.gameObject, manager.ElementColor);
            if (manager.ListFrameSprite != null)
            {
                image.sprite = manager.ListFrameSprite;
                image.type = Image.Type.Sliced;
            }

            var viewport = DebugWindowUi.CreateRect("Text Area", frame);
            DebugWindowUi.Stretch(viewport);
            viewport.offsetMin = new Vector2(
                manager.ControlHorizontalPadding,
                manager.ControlVerticalPadding * 0.5f);
            viewport.offsetMax = new Vector2(
                -manager.ControlHorizontalPadding,
                -manager.ControlVerticalPadding * 0.5f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var text = DebugWindowUi.CreateText(
                "Text",
                viewport,
                value ?? string.Empty,
                manager.TextSize,
                manager.TextColor,
                rightAligned
                    ? TextAlignmentOptions.MidlineRight
                    : TextAlignmentOptions.MidlineLeft);
            DebugWindowUi.Stretch(text.rectTransform);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;

            var input = frame.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.targetGraphic = image;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            input.customCaretColor = true;
            input.caretColor = manager.AccentColor;
            input.selectionColor = manager.SelectionColor;
            input.caretWidth = 2;
            input.SetTextWithoutNotify(value ?? string.Empty);
            if (changed != null)
                input.onEndEdit.AddListener(changed.Invoke);
            return input;
        }
    }

    internal sealed class DebugTabbedWindowControl : MonoBehaviour
    {
        private DebugWindowContent content;
        private DebugTabbedWindow definition;
        private RectTransform pageContent;
        private readonly Dictionary<string, Image> tabImages =
            new Dictionary<string, Image>(StringComparer.Ordinal);

        public static void Create(
            DebugWindowContent content,
            DebugTabbedWindow definition)
        {
            var root = DebugWindowUi.CreateRect("Tabbed Content", content.Root);
            var control = root.gameObject.AddComponent<DebugTabbedWindowControl>();
            control.Initialize(content, definition, root);
        }

        private void Initialize(
            DebugWindowContent newContent,
            DebugTabbedWindow newDefinition,
            RectTransform root)
        {
            content = newContent;
            definition = newDefinition;

            var size = root.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = definition.PreferredContentSize.x;
            size.preferredHeight = definition.PreferredContentSize.y;

            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = content.Manager.Spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            BuildTabs(root);
            BuildPage(root);
            BuildFooter(root);

            definition.ActivePageChanged += RefreshActivePage;
            RefreshActivePage();
        }

        private void OnDestroy()
        {
            if (definition != null)
                definition.ActivePageChanged -= RefreshActivePage;
        }

        private void BuildTabs(RectTransform parent)
        {
            var viewport = DebugWindowUi.CreateRect("Tabs", parent);
            var viewportSize = viewport.gameObject.AddComponent<LayoutElement>();
            viewportSize.preferredHeight =
                content.Manager.TextSize +
                content.Manager.ControlVerticalPadding * 2.0f + 4.0f;
            viewport.gameObject.AddComponent<RectMask2D>();

            var tabContent = DebugWindowUi.CreateRect("Content", viewport);
            tabContent.anchorMin = Vector2.zero;
            tabContent.anchorMax = new Vector2(0.0f, 1.0f);
            tabContent.pivot = new Vector2(0.0f, 0.5f);
            tabContent.anchoredPosition = Vector2.zero;
            var tabsLayout = tabContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.padding = new RectOffset(0, 0, 2, 2);
            tabsLayout.spacing = content.Manager.Spacing;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = false;
            tabsLayout.childForceExpandHeight = true;
            tabContent.gameObject.AddComponent<ContentSizeFitter>().horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = tabContent;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = content.Manager.TextSize;

            for (var i = 0; i < definition.Pages.Count; i++)
            {
                var page = definition.Pages[i];
                var width = Mathf.Max(
                    48.0f,
                    page.TabText.Length * content.Manager.TextSize * 0.62f +
                        content.Manager.ControlHorizontalPadding * 2.0f);
                var capturedId = page.UniqueId;
                var button = DebugWindowUi.CreateButton(
                    page.UniqueId,
                    tabContent,
                    page.TabText,
                    content.Manager.TextSize,
                    content.Manager.ElementColor,
                    content.Manager.TextColor,
                    () => definition.SetActivePage(capturedId),
                    width,
                    content.Manager.ControlHorizontalPadding,
                    content.Manager.ControlVerticalPadding);
                var image = button.GetComponent<Image>();
                if (content.Manager.TabBackgroundSprite != null)
                {
                    image.sprite = content.Manager.TabBackgroundSprite;
                    image.type = Image.Type.Sliced;
                }

                tabImages.Add(page.UniqueId, image);
            }
        }

        private void BuildPage(RectTransform parent)
        {
            var viewport = DebugWindowUi.CreateRect("Page", parent);
            viewport.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1.0f;
            viewport.gameObject.AddComponent<RectMask2D>();

            pageContent = DebugWindowUi.CreateRect("Content", viewport);
            pageContent.anchorMin = new Vector2(0.0f, 1.0f);
            pageContent.anchorMax = new Vector2(1.0f, 1.0f);
            pageContent.pivot = new Vector2(0.5f, 1.0f);
            pageContent.offsetMin = new Vector2(2.0f, 0.0f);
            pageContent.offsetMax = new Vector2(-2.0f, 0.0f);
            var layout = pageContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = content.Manager.Spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            pageContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = pageContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = content.Manager.TextSize;
        }

        private void BuildFooter(RectTransform parent)
        {
            if (definition.BuildFooter == null)
                return;

            var footer = DebugWindowUi.CreateRect("Footer", parent);
            var footerSize = footer.gameObject.AddComponent<LayoutElement>();
            footerSize.preferredHeight =
                content.Manager.TextSize +
                content.Manager.ControlVerticalPadding * 2.0f;
            footerSize.flexibleHeight = 0.0f;

            var layout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = content.Manager.Spacing;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            definition.BuildFooter(new DebugWindowFormContent(
                content.Manager,
                footer));
        }

        private void RefreshActivePage()
        {
            for (var i = pageContent.childCount - 1; i >= 0; i--)
            {
                var child = pageContent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            var page = definition.GetActivePage();
            page.BuildContent?.Invoke(new DebugWindowFormContent(
                content.Manager,
                pageContent));

            foreach (var pair in tabImages)
            {
                pair.Value.color = pair.Key == page.UniqueId
                    ? content.Manager.SelectionColor
                    : content.Manager.ElementColor;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(pageContent);
        }
    }
}
