using DialogSystem.Runtime.Settings.Panels;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static DialogSystem.EditorTools.Settings.DialogSettingsEditorUtils;

namespace DialogSystem.EditorTools.Settings.Panels
{
    /// <summary>
    /// Settings panel for all text and typewriter flow configurations.
    /// </summary>
    public class TextPanel : BasePanel
    {
        public override void BuildUI(SerializedObject masterSo)
        {
            // ---------------- Fetch ScriptableObject ----------------
            var textProp = masterSo.FindProperty("textSettings");
            var textAsset = textProp.objectReferenceValue as DialogTextSettings;
            if (textAsset == null)
                return; // safeguard in case asset missing

            var textSo = new SerializedObject(textAsset);

            //  WRITING CONFIG
            var typeCard = Card("Writing Config");

            // Dropdown (locked for now)
            var dropdown = AdvancedEnumDropdown(
                textSo,
                "typewriterEffect",
                "Typewriter Effect",
                "Currently only the Typing effect is available.",
                isLocked: false
            );
            typeCard.Add(dropdown);

            // Speed slider (with recommended highlight)
            typeCard.Add(AdvancedSliderWithValue(
                textSo,
                "charsPerSecond",
                1f, 120f,
                "Characters Per Second",
                new Vector2(20f, 80f)
            ));

            typeCard.Bind(textSo);
            Add(typeCard); // ✅ Automatically goes into ScrollView

            //  SKIP & FAST-FORWARD
            var skipCard = Card("Skip & Fast-Forward");
            skipCard.Add(ToggleRow(textSo, "allowSkipCurrentLine", "Allow Skip Current Line"));
            skipCard.Add(ToggleRow(textSo, "allowSkipAll", "Allow Skip Entire Conversation"));
            skipCard.Add(ToggleRow(textSo, "allowFastForwardHold", "Allow Hold for Fast-Forward"));
            skipCard.Add(AdvancedSliderWithValue(
                textSo,
                "fastForwardMultiplier",
                1f, 8f,
                "Fast-Forward Multiplier",
                new Vector2(2f, 5f)
            ));

            skipCard.Bind(textSo);
            Add(skipCard); // ✅ Automatically goes into ScrollView

            //  AUTO ADVANCE
            var autoCard = Card("Auto Advance");
            autoCard.Add(ToggleRow(textSo, "autoAdvance", "Enable Auto Advance"));
            autoCard.Add(AdvancedSliderWithValue(
                textSo,
                "autoAdvanceDelay",
                0.0f, 3.0f,
                "Auto Advance Delay (s)",
                new Vector2(0.3f, 1.5f)
            ));

            autoCard.Bind(textSo);
            Add(autoCard); // ✅ Automatically goes into ScrollView

            //  PUNCTUATION PAUSES
            var puncCard = Card("Punctuation Pauses");
            puncCard.Add(AdvancedSliderWithValue(
                textSo,
                "commaPause",
                0f, 0.5f,
                "Comma Pause (s)",
                new Vector2(0.05f, 0.15f)
            ));
            puncCard.Add(AdvancedSliderWithValue(
                textSo,
                "periodPause",
                0f, 0.5f,
                "Period Pause (s)",
                new Vector2(0.1f, 0.25f)
            ));
            puncCard.Add(AdvancedSliderWithValue(
                textSo,
                "questionPause",
                0f, 0.5f,
                "Question Pause (s)",
                new Vector2(0.15f, 0.3f)
            ));
            puncCard.Add(AdvancedSliderWithValue(
                textSo,
                "exclamationPause",
                0f, 0.5f,
                "Exclamation Pause (s)",
                new Vector2(0.15f, 0.3f)
            ));

            puncCard.Bind(textSo);
            Add(puncCard); // ✅ Automatically goes into ScrollView

            //  SAVE FOOTER
            Add(FooterSave(() => // ✅ Automatically goes outside ScrollView (footer detection)
            {
                textSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(textSo.targetObject);
                AssetDatabase.SaveAssets();
            }));
        }
    }
}