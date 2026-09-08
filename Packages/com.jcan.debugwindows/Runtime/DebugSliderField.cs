/*
 * Provides a code-defined slider field with editable value input, optional units, and stepped values.
 */

using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace jcan.DebugWindows
{
    public sealed class DebugSliderField : MonoBehaviour
    {
        private Slider slider;
        private TMP_InputField valueInput;
        private Action<float> changed;
        private float step;
        private float currentValue;

        internal static DebugSliderField Create(
            DebugWindowManager manager,
            RectTransform parent,
            float value,
            float minimum,
            float maximum,
            Action<float> changed,
            string units,
            float step)
        {
            var root = DebugWindowUi.CreateRect("Slider Field", parent);
            var rootSize = root.gameObject.AddComponent<LayoutElement>();
            rootSize.preferredWidth = manager.PreferredFieldWidth;
            rootSize.preferredHeight =
                manager.TextSize +
                manager.ControlVerticalPadding * 2.0f;
            rootSize.flexibleWidth = 1.0f;

            var rootLayout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            rootLayout.spacing = manager.Spacing;
            rootLayout.childAlignment = TextAnchor.MiddleLeft;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = false;
            rootLayout.childForceExpandHeight = false;

            var sliderRoot = DebugWindowUi.CreateRect("Slider", root);
            var sliderSize = sliderRoot.gameObject.AddComponent<LayoutElement>();
            sliderSize.minWidth = 72.0f;
            sliderSize.preferredWidth = Mathf.Max(
                96.0f,
                manager.PreferredFieldWidth -
                manager.TextSize * 4.5f -
                manager.Spacing);
            sliderSize.preferredHeight = manager.TextSize;
            sliderSize.flexibleWidth = 1.0f;

            var track = DebugWindowUi.CreateRect("Track", sliderRoot);
            track.anchorMin = new Vector2(0.0f, 0.5f);
            track.anchorMax = new Vector2(1.0f, 0.5f);
            track.offsetMin = new Vector2(4.0f, -1.5f);
            track.offsetMax = new Vector2(-4.0f, 1.5f);
            DebugWindowUi.AddImage(track.gameObject, manager.ButtonColor);

            var fillArea = DebugWindowUi.CreateRect("Fill Area", sliderRoot);
            fillArea.anchorMin = new Vector2(0.0f, 0.5f);
            fillArea.anchorMax = new Vector2(1.0f, 0.5f);
            fillArea.offsetMin = new Vector2(4.0f, -1.5f);
            fillArea.offsetMax = new Vector2(-4.0f, 1.5f);

            var fill = DebugWindowUi.CreateRect("Fill", fillArea);
            DebugWindowUi.Stretch(fill);
            DebugWindowUi.AddImage(fill.gameObject, manager.AccentColor);

            var handleArea = DebugWindowUi.CreateRect("Handle Slide Area", sliderRoot);
            DebugWindowUi.Stretch(handleArea);
            handleArea.offsetMin = new Vector2(4.0f, 0.0f);
            handleArea.offsetMax = new Vector2(-4.0f, 0.0f);

            var handle = DebugWindowUi.CreateRect("Handle", handleArea);
            handle.anchorMin = new Vector2(0.0f, 0.5f);
            handle.anchorMax = new Vector2(0.0f, 0.5f);
            handle.sizeDelta = new Vector2(7.0f, 7.0f);
            var handleImage = DebugWindowUi.AddImage(
                handle.gameObject,
                manager.AccentColor);

            var field = root.gameObject.AddComponent<DebugSliderField>();
            field.changed = changed;
            field.step = Mathf.Max(0.0f, step);

            field.slider = sliderRoot.gameObject.AddComponent<Slider>();
            field.slider.fillRect = fill;
            field.slider.handleRect = handle;
            field.slider.targetGraphic = handleImage;
            field.slider.direction = Slider.Direction.LeftToRight;
            field.slider.minValue = Mathf.Min(minimum, maximum);
            field.slider.maxValue = Mathf.Max(minimum, maximum);
            field.slider.wholeNumbers = false;

            field.BuildValueInput(manager, root);

            if (!string.IsNullOrWhiteSpace(units))
            {
                var unitsLabel = DebugWindowUi.CreateText(
                    "Units",
                    root,
                    units.Trim(),
                    manager.TextSize,
                    manager.TextColor,
                    TextAlignmentOptions.MidlineLeft);
                unitsLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0.0f;
            }

            field.slider.onValueChanged.AddListener(field.HandleSliderChanged);
            field.valueInput.onEndEdit.AddListener(field.HandleInputCommitted);
            field.SetValue(value, false);
            return field;
        }

        public float Value => slider != null ? slider.value : 0.0f;

        public void SetValue(float value, bool notify = false)
        {
            if (slider == null)
                return;

            var adjusted = ClampAndSnap(value);
            var valueChanged = !Mathf.Approximately(currentValue, adjusted);
            currentValue = adjusted;
            slider.SetValueWithoutNotify(adjusted);
            RefreshValueInput(adjusted);

            if (notify && valueChanged)
                changed?.Invoke(adjusted);
        }

        private void BuildValueInput(
            DebugWindowManager manager,
            RectTransform parent)
        {
            var frame = DebugWindowUi.CreateRect("Value Input", parent);
            var frameSize = frame.gameObject.AddComponent<LayoutElement>();
            frameSize.preferredWidth = manager.TextSize * 3.5f;
            frameSize.preferredHeight =
                manager.TextSize +
                manager.ControlVerticalPadding * 2.0f;
            frameSize.flexibleWidth = 0.0f;

            var image = DebugWindowUi.AddImage(
                frame.gameObject,
                manager.ElementColor);
            if (manager.ListFrameSprite != null)
            {
                image.sprite = manager.ListFrameSprite;
                image.type = Image.Type.Sliced;
            }

            var viewport = DebugWindowUi.CreateRect("Text Area", frame);
            DebugWindowUi.Stretch(viewport);
            viewport.offsetMin = new Vector2(
                manager.ControlHorizontalPadding * 0.5f,
                manager.ControlVerticalPadding * 0.5f);
            viewport.offsetMax = new Vector2(
                -manager.ControlHorizontalPadding * 1.5f,
                -manager.ControlVerticalPadding * 0.5f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var text = DebugWindowUi.CreateText(
                "Text",
                viewport,
                string.Empty,
                manager.TextSize,
                manager.TextColor,
                TextAlignmentOptions.MidlineRight);
            DebugWindowUi.Stretch(text.rectTransform);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;

            valueInput = frame.gameObject.AddComponent<TMP_InputField>();
            valueInput.textViewport = viewport;
            valueInput.textComponent = text;
            valueInput.targetGraphic = image;
            valueInput.lineType = TMP_InputField.LineType.SingleLine;
            valueInput.richText = false;
            valueInput.customCaretColor = true;
            valueInput.caretColor = manager.AccentColor;
            valueInput.selectionColor = manager.SelectionColor;
            valueInput.caretWidth = 2;
        }

        private void HandleSliderChanged(float value)
        {
            var adjusted = ClampAndSnap(value);
            var valueChanged = !Mathf.Approximately(currentValue, adjusted);
            currentValue = adjusted;

            if (!Mathf.Approximately(slider.value, adjusted))
                slider.SetValueWithoutNotify(adjusted);

            RefreshValueInput(adjusted);
            if (valueChanged)
                changed?.Invoke(adjusted);
        }

        private void HandleInputCommitted(string text)
        {
            if (float.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                SetValue(parsed, true);
                return;
            }

            RefreshValueInput(currentValue);
        }

        private float ClampAndSnap(float value)
        {
            var clamped = Mathf.Clamp(value, slider.minValue, slider.maxValue);
            if (step <= Mathf.Epsilon)
                return clamped;

            var steps = Mathf.Round((clamped - slider.minValue) / step);
            return Mathf.Clamp(
                slider.minValue + steps * step,
                slider.minValue,
                slider.maxValue);
        }

        private void RefreshValueInput(float value)
        {
            if (valueInput == null)
                return;

            valueInput.SetTextWithoutNotify(
                value.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture));
        }
    }
}
