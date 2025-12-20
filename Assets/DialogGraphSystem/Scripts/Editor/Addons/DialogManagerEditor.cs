#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using DialogSystem.Runtime.Core;

namespace DialogSystem.EditorTools.Addons
{
    /// <summary>
    /// Custom inspector addon: adds a button to open the Dialog System Settings window.
    /// </summary>
    [CustomEditor(typeof(DialogManager))]
    public class DialogManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open Dialog System Settings", GUILayout.Height(26)))
                {
                    System.Type type = null;

                    // Try current official namespace first
                    type = System.Type.GetType("DialogSystem.EditorTools.Settings.DialogSettingsEditorWindow, Assembly-CSharp-Editor");

                    // Fallbacks for old or renamed namespaces
                    if (type == null)
                        type = System.Type.GetType("DialogGraphSystem.Editor.SettingsWindow.DialogSettingsEditorWindow, Assembly-CSharp-Editor");

                    if (type == null)
                        type = System.Type.GetType("DialogGraphSystem.Editor.SettingsWindow.DialogSettingsEditorWindow");

                    if (type == null)
                        type = System.Type.GetType("DialogSystem.EditorTools.Settings.DialogSettingsEditorWindow");

                    if (type != null)
                    {
                        var method = type.GetMethod("Open", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                        if (method != null)
                            method.Invoke(null, null);
                        else
                            EditorUtility.DisplayDialog("Dialog System", "Settings window found, but missing Open() method.", "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog(
                            "Dialog System",
                            "Settings window not found.\n\nMake sure your Settings Editor scripts are inside:\nAssets/DialogGraphSystem/Scripts/Editor/SettingsWindow/\n\nand the class namespace matches:\nDialogSystem.EditorTools.Settings",
                            "OK"
                        );
                    }
                }
            }
        }
    }
}
#endif
