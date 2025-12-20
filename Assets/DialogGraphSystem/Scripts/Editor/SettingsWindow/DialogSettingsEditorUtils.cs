using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Settings
{
    /// <summary>
    /// Helper builders for consistent UI Toolkit rows (slider+pill, toggles, cards, icons).
    /// </summary>
    internal static class DialogSettingsEditorUtils
    {
        #region ---------------- Slider + Pill (float) ----------------
        public static VisualElement SliderWithPill(SerializedObject so, string floatProp, float min, float max, string labelText)
        {
            var row = new VisualElement(); row.AddToClassList("dgs-row");

            var label = new Label(labelText); label.AddToClassList("dgs-muted");
            row.Add(label);

            var slider = new Slider(min, max) { showInputField = false };
            slider.AddToClassList("dgs-slider");

            var prop = so.FindProperty(floatProp);
            slider.value = prop.floatValue;

            slider.RegisterValueChangedCallback(evt =>
            {
                prop.floatValue = evt.newValue;
                so.ApplyModifiedProperties();
            });

            var pill = new FloatField() { value = prop.floatValue };
            pill.AddToClassList("dgs-pill");
            pill.RegisterValueChangedCallback(evt =>
            {
                prop.floatValue = Mathf.Clamp(evt.newValue, min, max);
                so.ApplyModifiedProperties();
                slider.SetValueWithoutNotify(prop.floatValue);
            });

            // Passive keep-in-sync; light frequency to avoid perf cost in editor.
            row.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                slider.schedule.Execute(() =>
                {
                    prop.serializedObject.Update();
                    slider.SetValueWithoutNotify(prop.floatValue);
                    pill.SetValueWithoutNotify(prop.floatValue);
                }).Every(150);
            });

            row.Add(slider);
            row.Add(pill);
            return row;
        }
        #endregion

        #region ---------------- Slider + Pill (int) ----------------
        public static VisualElement IntSliderWithPill(SerializedObject so, string intProp, int min, int max, string labelText)
        {
            var row = new VisualElement(); row.AddToClassList("dgs-row");

            var label = new Label(labelText); label.AddToClassList("dgs-muted");
            row.Add(label);

            var slider = new SliderInt(min, max);
            slider.AddToClassList("dgs-slider");

            var prop = so.FindProperty(intProp);
            slider.value = prop.intValue;

            slider.RegisterValueChangedCallback(evt =>
            {
                prop.intValue = evt.newValue;
                so.ApplyModifiedProperties();
            });

            var pill = new IntegerField() { value = prop.intValue };
            pill.AddToClassList("dgs-pill");
            pill.RegisterValueChangedCallback(evt =>
            {
                prop.intValue = Mathf.Clamp(evt.newValue, min, max);
                so.ApplyModifiedProperties();
                slider.SetValueWithoutNotify(prop.intValue);
            });

            row.Add(slider);
            row.Add(pill);
            return row;
        }
        #endregion

        #region ---------------- Toggle Row ----------------
        public static Toggle ToggleRow(SerializedObject so, string boolProp, string labelText)
        {
            var t = new Toggle(labelText);
            t.AddToClassList("dgs-row");
            var p = so.FindProperty(boolProp);
            t.value = p.boolValue;
            t.RegisterValueChangedCallback(evt =>
            {
                p.boolValue = evt.newValue;
                so.ApplyModifiedProperties();
            });
            return t;
        }
        #endregion

        #region ---------------- Card & Icon ----------------
        public static VisualElement Card(string title)
        {
            var card = new VisualElement(); card.AddToClassList("dgs-card");
            var h = new Label(title); h.AddToClassList("dgs-card-title");
            card.Add(h);
            return card;
        }

        public static VisualElement Icon(string editorIconName)
        {
            var ve = new VisualElement(); ve.AddToClassList("dgs-nav-icon");
            var tex = EditorGUIUtility.IconContent(editorIconName).image as Texture2D;
            if (tex != null) ve.style.backgroundImage = new StyleBackground(tex);
            return ve;
        }
        #endregion

        #region ---------------- Advanced Slider (with labels + live value + recommended range) ----------------
        public static VisualElement AdvancedSliderWithValue(
         SerializedObject so,
         string propertyName,
         float min,
         float max,
         string labelText,
         Vector2 recommendedRange)
        {
            var root = new VisualElement();
            root.AddToClassList("dgs-adv-row");

            var prop = so.FindProperty(propertyName);

            // ---- Header with value on the same line ----
            var header = new VisualElement();
            header.AddToClassList("dgs-adv-header");

            var label = new Label(labelText);
            label.AddToClassList("dgs-adv-label");
            header.Add(label);

            // Value badge on the right side of header
            var valueBadge = new Label(prop.floatValue.ToString("0.##"));
            valueBadge.AddToClassList("dgs-adv-value-badge");
            valueBadge.style.backgroundColor = new Color(0.18f, 0.20f, 0.24f, 1f);
            valueBadge.style.color = new Color(0.82f, 0.87f, 0.95f, 1f);
            valueBadge.style.paddingLeft = 8;
            valueBadge.style.paddingRight = 8;
            valueBadge.style.paddingTop = 2;
            valueBadge.style.paddingBottom = 2;
            valueBadge.style.borderTopLeftRadius = 4;
            valueBadge.style.borderTopRightRadius = 4;
            valueBadge.style.borderBottomLeftRadius = 4;
            valueBadge.style.borderBottomRightRadius = 4;
            valueBadge.style.fontSize = 11;
            valueBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueBadge.style.minWidth = 40;
            valueBadge.style.unityTextAlign = TextAnchor.MiddleCenter;

            header.Add(valueBadge);
            root.Add(header);

            // ---- Slider Container with Floating Value ----
            var sliderRow = new VisualElement();
            sliderRow.AddToClassList("dgs-adv-slider-container");
            sliderRow.style.position = Position.Relative;

            // --- Track background container ---
            var trackContainer = new VisualElement
            {
                style =
            {
                position = Position.Relative,
                flexGrow = 1,
                marginTop = 2,
                marginBottom = 2,
                minHeight = 24
            }
            };

            // Recommended range overlay (centered vertically)
            var recommendedOverlay = new VisualElement
            {
                style =
            {
                position = Position.Absolute,
                top = new StyleLength(new Length(25, LengthUnit.Percent)),
                height = 10,
                backgroundColor = new Color(0.25f, 0.85f, 0.55f, 0.25f),
                borderTopLeftRadius = 3,
                borderBottomLeftRadius = 3,
                borderTopRightRadius = 3,
                borderBottomRightRadius = 3,
                unityBackgroundImageTintColor = new Color(1, 1, 1, 0.3f)
            }
            };

            // Slider
            var slider = new Slider(min, max) { value = prop.floatValue };
            slider.AddToClassList("dgs-adv-slider");

            // Floating value tooltip (follows the handle)
            var floatingValue = new Label(prop.floatValue.ToString("0.##"));
            floatingValue.style.position = Position.Absolute;
            floatingValue.style.top = -24;
            floatingValue.style.backgroundColor = new Color(0.2f, 0.42f, 0.84f, 0.95f);
            floatingValue.style.color = Color.white;
            floatingValue.style.paddingLeft = 6;
            floatingValue.style.paddingRight = 6;
            floatingValue.style.paddingTop = 3;
            floatingValue.style.paddingBottom = 3;
            floatingValue.style.borderTopLeftRadius = 4;
            floatingValue.style.borderTopRightRadius = 4;
            floatingValue.style.borderBottomLeftRadius = 4;
            floatingValue.style.borderBottomRightRadius = 4;
            floatingValue.style.fontSize = 10;
            floatingValue.style.unityFontStyleAndWeight = FontStyle.Bold;
            floatingValue.style.minWidth = 35;
            floatingValue.style.unityTextAlign = TextAnchor.MiddleCenter;
            floatingValue.style.display = DisplayStyle.None; // Hidden by default

            // Add elements in order
            trackContainer.Add(recommendedOverlay);
            trackContainer.Add(slider);
            trackContainer.Add(floatingValue);

            sliderRow.Add(trackContainer);
            root.Add(sliderRow);

            // ---- Recommended range text ----
            var rec = new Label($"recommended: {recommendedRange.x:0.#}–{recommendedRange.y:0.#}");
            rec.AddToClassList("dgs-adv-range-label");
            root.Add(rec);

            // ---- Logic to position the green overlay ----
            void UpdateOverlayPosition()
            {
                float rangeMinRatio = Mathf.InverseLerp(min, max, recommendedRange.x);
                float rangeMaxRatio = Mathf.InverseLerp(min, max, recommendedRange.y);

                float leftPercent = Mathf.Clamp01(rangeMinRatio) * 100f;
                float widthPercent = Mathf.Clamp01(rangeMaxRatio - rangeMinRatio) * 100f;

                recommendedOverlay.style.left = new Length(leftPercent, LengthUnit.Percent);
                recommendedOverlay.style.width = new Length(widthPercent, LengthUnit.Percent);
            }

            UpdateOverlayPosition();

            // ---- Update floating value position ----
            void UpdateFloatingValuePosition(float value)
            {
                float ratio = Mathf.InverseLerp(min, max, value);
                float leftPercent = Mathf.Clamp01(ratio) * 100f;
                floatingValue.style.left = new Length(leftPercent, LengthUnit.Percent);
            }

            UpdateFloatingValuePosition(prop.floatValue);

            // ---- Event handlers ----
            slider.RegisterValueChangedCallback(evt =>
            {
                prop.floatValue = evt.newValue;
                so.ApplyModifiedProperties();

                string valueText = evt.newValue.ToString("0.##");
                valueBadge.text = valueText;
                floatingValue.text = valueText;

                UpdateFloatingValuePosition(evt.newValue);
            });

            // Show floating value on hover/focus
            slider.RegisterCallback<MouseEnterEvent>(evt =>
            {
                floatingValue.style.display = DisplayStyle.Flex;
            });

            slider.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                floatingValue.style.display = DisplayStyle.None;
            });

            slider.RegisterCallback<FocusInEvent>(evt =>
            {
                floatingValue.style.display = DisplayStyle.Flex;
            });

            slider.RegisterCallback<FocusOutEvent>(evt =>
            {
                floatingValue.style.display = DisplayStyle.None;
            });

            return root;
        }

        #endregion

        #region ---------------- Advanced Enum Dropdown ----------------
        public static VisualElement AdvancedEnumDropdown(
            SerializedObject so,
            string propertyName,
            string labelText,
            string hintText = "",
            bool isLocked = false)
        {
            var root = new VisualElement();
            root.AddToClassList("dgs-adv-dropdown");

            var prop = so.FindProperty(propertyName);

            // Header label
            var header = new VisualElement();
            header.AddToClassList("dgs-adv-dropdown-row");
            var label = new Label(labelText);
            label.AddToClassList("dgs-adv-dropdown-label");
            header.Add(label);
            root.Add(header);

            // Dropdown itself
            var enumField = new PropertyField(prop, "");
            enumField.AddToClassList("dgs-adv-enumfield");
            root.Add(enumField);

            // Disable interaction if locked
            if (isLocked)
                enumField.SetEnabled(false);

            // Optional hint
            if (!string.IsNullOrEmpty(hintText))
            {
                var hint = new Label(hintText);
                hint.AddToClassList("dgs-adv-hint");
                root.Add(hint);
            }

            return root;
        }
        #endregion
    }
}
