using DialogSystem.Runtime.Settings.Panels;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using static DialogSystem.EditorTools.Settings.DialogSettingsEditorUtils;

namespace DialogSystem.EditorTools.Settings.Panels
{
    public class AudioPanel : BasePanel
    {
        [SerializeField] private bool doDebug = true;

        public override void BuildUI(SerializedObject masterSo)
        {
            var audioProp = masterSo.FindProperty("audioSettings");
            var audioObj = (DialogAudioSettings)audioProp.objectReferenceValue;
            var audioSo = new SerializedObject(audioObj);

            // Volumes: recommended 0.5–0.8
            var vol = Card("Volumes");
            vol.Add(AdvancedSliderWithValue(
                audioSo,
                "voiceVolume",
                0f, 1f,
                "Voice Volume",
                new Vector2(0.5f, 0.8f)
            ));
            vol.Add(AdvancedSliderWithValue(
                audioSo,
                "sfxVolume",
                0f, 1f,
                "SFX Volume",
                new Vector2(0.5f, 0.8f)
            ));
            Add(vol);

            var sfx = Card("UI SFX");
            sfx.Add(ToggleRow(audioSo, "enableUiSfx", "Enable UI SFX"));
            sfx.Add(new PropertyField(audioSo.FindProperty("sfxNavigate"), "Navigate SFX"));
            sfx.Add(new PropertyField(audioSo.FindProperty("sfxConfirm"), "Confirm SFX"));
            sfx.Add(new PropertyField(audioSo.FindProperty("sfxSkip"), "Skip SFX"));
            Add(sfx);

            Add(FooterSave(() =>
            {
                audioSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(audioSo.targetObject);
                AssetDatabase.SaveAssets();
            }));
        }
    }
}
