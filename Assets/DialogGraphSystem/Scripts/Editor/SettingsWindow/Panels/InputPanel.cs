using DialogSystem.Runtime.Settings.Panels;
using UnityEditor;
using UnityEditor.UIElements;
using static DialogSystem.EditorTools.Settings.DialogSettingsEditorUtils;

namespace DialogSystem.EditorTools.Settings.Panels
{
    public class InputPanel : BasePanel
    {
        public override void BuildUI(SerializedObject masterSo)
        {
            var inputProp = masterSo.FindProperty("inputSettings");
            var inputSo = new SerializedObject((DialogInputSettings)inputProp.objectReferenceValue);

            var kbd = Card("Keyboard");
            kbd.Add(new PropertyField(inputSo.FindProperty("confirmKeys"), "Confirm Keys"));
            kbd.Add(new PropertyField(inputSo.FindProperty("skipKeys"), "Skip Keys"));
            kbd.Add(new PropertyField(inputSo.FindProperty("fastForwardKeys"), "Fast-Forward Keys"));
            kbd.Add(new PropertyField(inputSo.FindProperty("cancelKeys"), "Cancel Keys"));
            Add(kbd);

            var nav = Card("Navigation");
            nav.Add(new PropertyField(inputSo.FindProperty("navUpKeys"), "Up Keys"));
            nav.Add(new PropertyField(inputSo.FindProperty("navDownKeys"), "Down Keys"));
            Add(nav);

            var gp = Card("Gamepad / Axes");
            gp.Add(new PropertyField(inputSo.FindProperty("verticalAxis"), "Vertical Axis"));
            gp.Add(IntSliderWithPill(inputSo, "joystickConfirmButton", 0, 15, "Joystick Confirm Button"));
            Add(gp);

            var mouse = Card("Mouse");
            mouse.Add(ToggleRow(inputSo, "allowMouseClickConfirm", "Allow Click To Confirm"));
            Add(mouse);

            Add(FooterSave(() =>
            {
                inputSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(inputSo.targetObject);
                AssetDatabase.SaveAssets();
            }));
        }
    }

}
