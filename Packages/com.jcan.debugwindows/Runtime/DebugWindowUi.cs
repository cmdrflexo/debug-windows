/*
 * Creates the minimal uGUI and TextMeshPro controls used by runtime debug windows.
 */

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace jcan.DebugWindows
{
    internal static class DebugWindowUi
    {
        public static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = parent.gameObject.layer;
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image AddImage(GameObject target, Color color)
        {
            var image = target.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string text,
            float size,
            Color color,
            TextAlignmentOptions alignment)
        {
            var rect = CreateRect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            return label;
        }

        public static Button CreateButton(
            string name,
            Transform parent,
            string text,
            float textSize,
            Color background,
            Color foreground,
            Action clicked,
            float width,
            float horizontalPadding = 0.0f,
            float verticalPadding = 4.0f,
            string feedbackMessage = null,
            float feedbackDuration = 3.0f,
            bool showFeedbackOnClick = true)
        {
            var rect = CreateRect(name, parent);
            var image = AddImage(rect.gameObject, background);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var buttonSize = rect.gameObject.AddComponent<LayoutElement>();
            buttonSize.preferredWidth = width;
            buttonSize.preferredHeight = textSize + verticalPadding * 2.0f;

            var label = CreateText(
                "Label",
                rect,
                text,
                textSize,
                foreground,
                TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(horizontalPadding, 0.0f);
            label.rectTransform.offsetMax = new Vector2(-horizontalPadding, 0.0f);

            var feedback = string.IsNullOrWhiteSpace(feedbackMessage)
                ? null
                : rect.gameObject.AddComponent<DebugButtonFeedback>();
            feedback?.Initialize(label, text, feedbackMessage.Trim(), feedbackDuration);

            if (clicked != null || feedback != null && showFeedbackOnClick)
            {
                button.onClick.AddListener(() =>
                {
                    clicked?.Invoke();
                    if (showFeedbackOnClick)
                        feedback?.Show();
                });
            }

            return button;
        }

        public static Toggle CreateToggle(
            string name,
            Transform parent,
            string text,
            bool isOn,
            float textSize,
            Color foreground,
            Color controlBackground,
            Color checkColor,
            Action<bool> changed,
            float verticalPadding = 0.0f)
        {
            var row = CreateRect(name, parent);
            var rowSize = row.gameObject.AddComponent<LayoutElement>();
            rowSize.preferredHeight = textSize + verticalPadding * 2.0f;
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 6.0f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            var box = CreateRect("Box", row);
            var boxImage = AddImage(box.gameObject, controlBackground);
            var boxLayout = box.gameObject.AddComponent<LayoutElement>();
            boxLayout.preferredWidth = textSize;
            boxLayout.preferredHeight = textSize;

            var check = CreateRect("Checkmark", box);
            var checkImage = AddImage(check.gameObject, checkColor);
            check.anchorMin = new Vector2(0.2f, 0.2f);
            check.anchorMax = new Vector2(0.8f, 0.8f);
            check.offsetMin = Vector2.zero;
            check.offsetMax = Vector2.zero;

            var label = CreateText(
                "Label",
                row,
                text,
                textSize,
                foreground,
                TextAlignmentOptions.MidlineLeft);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1.0f;

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.isOn = isOn;
            if (changed != null)
                toggle.onValueChanged.AddListener(value => changed(value));
            return toggle;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    internal sealed class DebugButtonFeedback : MonoBehaviour
    {
        private TMP_Text label;
        private string normalText;
        private string feedbackText;
        private float duration;
        private Coroutine restoreRoutine;

        public void Initialize(
            TMP_Text targetLabel,
            string originalText,
            string message,
            float seconds)
        {
            label = targetLabel;
            normalText = originalText ?? string.Empty;
            feedbackText = message ?? string.Empty;
            duration = Mathf.Max(0.0f, seconds);
        }

        public void Show()
        {
            if (label == null)
                return;

            if (restoreRoutine != null)
                StopCoroutine(restoreRoutine);

            label.text = feedbackText;
            restoreRoutine = StartCoroutine(RestoreLabel());
        }

        private IEnumerator RestoreLabel()
        {
            if (duration > 0.0f)
                yield return new WaitForSecondsRealtime(duration);
            else
                yield return null;

            if (label != null)
                label.text = normalText;

            restoreRoutine = null;
        }
    }
}
