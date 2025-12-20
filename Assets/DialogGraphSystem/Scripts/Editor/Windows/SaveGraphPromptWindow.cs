using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows
{
    internal class SaveGraphPromptWindow : EditorWindow
    {
        #region ---------------- Debug ----------------
        [SerializeField] private bool doDebug = true;
        #endregion

        #region ---------------- Types ----------------
        public enum SaveMode
        {
            UseLoaded,
            OverwriteExisting,
            SaveAsNew
        }
        #endregion

        #region ---------------- Callbacks & State ----------------
        private Action<string> _onConfirm;
        private Action _onCancel;

        private string _currentSuggestedName;
        private string _loadedGraphName;
        private List<string> _existingNames = new();
        private bool _isGraphEmpty;
        #endregion

        #region ---------------- UI Elements ----------------
        private EnumField _modeField;
        private TextField _newNameField;
        private PopupField<string> _existingPopup;
        private Label _warningLabel;
        private Label _modeDescriptionLabel;
        private Label _targetPreviewLabel;
        private Button _saveBtn;
        #endregion

        #region ---------------- Open ----------------
        public static void Open(
            string currentName,
            string loadedGraphName,
            List<string> existingNames,
            bool isGraphEmpty,
            Action<string> onConfirm,
            Action onCancel = null)
        {
            var w = CreateInstance<SaveGraphPromptWindow>();
            w.titleContent = new GUIContent("Save Dialog Graph…");

            w._onConfirm = onConfirm;
            w._onCancel = onCancel;
            w._currentSuggestedName = string.IsNullOrEmpty(currentName) ? "NewDialogGraph" : currentName;
            w._loadedGraphName = loadedGraphName;
            w._existingNames = existingNames != null
                ? existingNames.Distinct().OrderBy(n => n).ToList()
                : new List<string>();
            w._isGraphEmpty = isGraphEmpty;

            w.minSize = new Vector2(480, 260);
            w.maxSize = new Vector2(860, 360);
            w.ShowUtility();

            w.BuildUI();
            w.Focus();

            if (w.doDebug)
                Debug.Log($"[SaveGraphPromptWindow] Opened – current='{w._currentSuggestedName}', loaded='{w._loadedGraphName}', existing={w._existingNames.Count}, empty={w._isGraphEmpty}");
        }
        #endregion

        #region ---------------- UI Build ----------------
        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();

            // Attach shared stylesheet so we use same visual language as other windows
            var ss = Resources.Load<StyleSheet>(TextResources.STYLE_PATH);
            if (ss != null)
                root.styleSheets.Add(ss);

            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.flexDirection = FlexDirection.Column;

            // Title
            var title = new Label("Choose how to save the current graph");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            root.Add(title);

            var subtitle = new Label("You can save over the loaded graph, overwrite another one, or save as a new asset.");
            subtitle.style.opacity = 0.75f;
            subtitle.style.marginBottom = 8;
            root.Add(subtitle);

            // Mode
            var defaultMode = !string.IsNullOrEmpty(_loadedGraphName)
                ? SaveMode.UseLoaded
                : SaveMode.SaveAsNew;

            _modeField = new EnumField("Mode", defaultMode);
            _modeField.style.marginBottom = 4;
            root.Add(_modeField);

            // Mode description
            _modeDescriptionLabel = new Label();
            _modeDescriptionLabel.style.opacity = 0.85f;
            _modeDescriptionLabel.style.marginBottom = 6;
            _modeDescriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_modeDescriptionLabel);

            // Loaded info
            if (!string.IsNullOrEmpty(_loadedGraphName))
            {
                var loadedRow = new VisualElement();
                loadedRow.style.marginTop = 2;
                var loadedLbl = new Label($"Loaded graph: {_loadedGraphName}");
                loadedLbl.style.opacity = 0.85f;
                loadedRow.Add(loadedLbl);
                root.Add(loadedRow);
            }

            // Overwrite existing popup
            var names = _existingNames.Count > 0 ? _existingNames : new List<string> { "(no DialogGraph assets found)" };
            _existingPopup = new PopupField<string>("Overwrite", names, 0);
            _existingPopup.AddToClassList("dlg-popup");
            _existingPopup.AddToClassList("tight-label");
            _existingPopup.SetEnabled(_existingNames.Count > 0);
            _existingPopup.style.marginBottom = 4;
            root.Add(_existingPopup);

            // New name field
            _newNameField = new TextField("Save As")
            {
                value = _currentSuggestedName,
                isDelayed = true
            };
            _newNameField.AddToClassList("dlg-textfield");
            _newNameField.style.marginBottom = 4;
            root.Add(_newNameField);

            // Target preview label (shows final file name)
            _targetPreviewLabel = new Label();
            _targetPreviewLabel.style.marginTop = 2;
            _targetPreviewLabel.style.marginBottom = 6;
            _targetPreviewLabel.style.opacity = 0.85f;
            _targetPreviewLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_targetPreviewLabel);

            // Warning label
            _warningLabel = new Label();
            _warningLabel.style.marginTop = 2;
            _warningLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_warningLabel);

            if (_isGraphEmpty)
            {
                _warningLabel.text = "Warning: current graph appears to be EMPTY. Overwriting an existing graph will erase its content.";
                _warningLabel.style.color = Color.yellow;
            }

            // Buttons row
            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.FlexEnd;
            btnRow.style.marginTop = 12;

            var cancelBtn = new Button(() =>
            {
                _onCancel?.Invoke();
                Close();
            })
            {
                text = "Cancel"
            };

            _saveBtn = new Button(OnClickSave)
            {
                text = "Save"
            };
            _saveBtn.AddToClassList("primary");

            btnRow.Add(cancelBtn);
            btnRow.Add(_saveBtn);
            root.Add(btnRow);

            // Events
            _modeField.RegisterValueChangedCallback(_ => UpdateUIState());
            _newNameField.RegisterValueChangedCallback(_ => UpdateUIState());
            if (_existingPopup != null)
                _existingPopup.RegisterValueChangedCallback(_ => UpdateUIState());

            // Keyboard shortcuts: Enter = Save, Esc = Cancel
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    OnClickSave();
                    evt.StopImmediatePropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    _onCancel?.Invoke();
                    Close();
                    evt.StopImmediatePropagation();
                }
            });

            // Initial state
            UpdateUIState();
        }
        #endregion

        #region ---------------- Logic ----------------
        /// <summary>
        /// Updates enabled/disabled fields, description, preview and warnings based on mode & current values.
        /// </summary>
        private void UpdateUIState()
        {
            var mode = (SaveMode)_modeField.value;

            // Defensive: disable UseLoaded if none loaded
            if (string.IsNullOrEmpty(_loadedGraphName) && mode == SaveMode.UseLoaded)
            {
                _modeField.SetValueWithoutNotify(SaveMode.SaveAsNew);
                mode = SaveMode.SaveAsNew;
            }

            // Enable / disable controls by mode
            bool canOverwrite = mode == SaveMode.OverwriteExisting && _existingNames.Count > 0;
            bool canSaveAsNew = mode == SaveMode.SaveAsNew;

            _existingPopup?.SetEnabled(canOverwrite);
            _newNameField?.SetEnabled(canSaveAsNew);

            // Mode description text
            switch (mode)
            {
                case SaveMode.UseLoaded:
                    _modeDescriptionLabel.text =
                        "Save directly into the currently loaded graph. This is the fastest way to update your existing conversation.";
                    break;

                case SaveMode.OverwriteExisting:
                    _modeDescriptionLabel.text =
                        "Select another existing DialogGraph and overwrite its content with the current graph.";
                    break;

                case SaveMode.SaveAsNew:
                    _modeDescriptionLabel.text =
                        "Create a new DialogGraph asset with the specified name. This is ideal when branching or making a safe copy.";
                    break;
            }

            // Build target preview
            string previewName = null;
            switch (mode)
            {
                case SaveMode.UseLoaded:
                    previewName = _loadedGraphName;
                    break;

                case SaveMode.OverwriteExisting:
                    previewName = _existingNames.Count > 0 ? _existingPopup.value : "(no target)";
                    break;

                case SaveMode.SaveAsNew:
                    previewName = SanitizeName(_newNameField.value);
                    break;
            }

            if (string.IsNullOrEmpty(previewName))
            {
                _targetPreviewLabel.text = "Target: (no valid target selected)";
            }
            else
            {
                // We keep this generic because the actual folder is decided in the editor logic.
                _targetPreviewLabel.text = $"Target: {previewName}.asset (in the configured DialogGraph folder)";
            }

            // Reset warning unless explicitly set
            if (!_isGraphEmpty)
            {
                _warningLabel.text = string.Empty;
                _warningLabel.style.color = Color.clear;
            }

            // Extra warning if SaveAsNew uses an existing name
            if (mode == SaveMode.SaveAsNew && !string.IsNullOrEmpty(previewName))
            {
                bool exists = _existingNames.Any(n =>
                    string.Equals(n, previewName, StringComparison.OrdinalIgnoreCase));

                if (exists)
                {
                    _warningLabel.text =
                        $"A DialogGraph named \"{previewName}\" already exists. Saving will overwrite it.";
                    _warningLabel.style.color = Color.yellow;
                }
                else if (!_isGraphEmpty)
                {
                    // Non-empty graph, non-existing target: no special warning needed
                    _warningLabel.text = string.Empty;
                    _warningLabel.style.color = Color.clear;
                }
            }

            // If graph is empty we keep the original empty warning (set in BuildUI)
            if (_isGraphEmpty)
            {
                _warningLabel.text =
                    string.IsNullOrEmpty(_warningLabel.text)
                        ? "Warning: current graph appears to be EMPTY. Overwriting an existing graph will erase its content."
                        : _warningLabel.text;
                _warningLabel.style.color = Color.yellow;
            }

            // Save button availability (user cannot proceed with obviously invalid state)
            bool canSave =
                (mode == SaveMode.UseLoaded && !string.IsNullOrEmpty(_loadedGraphName)) ||
                (mode == SaveMode.OverwriteExisting && _existingNames.Count > 0) ||
                (mode == SaveMode.SaveAsNew && !string.IsNullOrEmpty(previewName));

            _saveBtn.SetEnabled(canSave);
        }

        private void OnClickSave()
        {
            var mode = (SaveMode)_modeField.value;
            string finalName = null;

            switch (mode)
            {
                case SaveMode.UseLoaded:
                    finalName = _loadedGraphName;
                    break;

                case SaveMode.OverwriteExisting:
                    finalName = _existingNames.Count > 0 ? _existingPopup.value : null;
                    break;

                case SaveMode.SaveAsNew:
                    finalName = SanitizeName(_newNameField.value);
                    if (string.IsNullOrEmpty(finalName))
                    {
                        EditorUtility.DisplayDialog("Invalid Name", "Please enter a valid file name.", "OK");
                        return;
                    }

                    // If name already exists, confirm overwrite explicitly
                    if (_existingNames.Any(n =>
                        string.Equals(n, finalName, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!EditorUtility.DisplayDialog(
                                "Name Already Exists",
                                $"A DialogGraph named \"{finalName}\" already exists.\n\nOverwrite it?",
                                "Overwrite", "Cancel"))
                            return;
                    }
                    break;
            }

            if (string.IsNullOrEmpty(finalName))
            {
                EditorUtility.DisplayDialog("No Selection", "Please choose a valid target to save.", "OK");
                return;
            }

            // If overwriting with empty graph, demand explicit confirmation
            if (_isGraphEmpty)
            {
                if (!EditorUtility.DisplayDialog(
                        "Overwrite with an EMPTY graph?",
                        $"You are about to save an empty graph as \"{finalName}\". " +
                        "If a graph with this name exists, its content will be lost.",
                        "I understand, continue", "Cancel"))
                    return;
            }

            if (doDebug)
                Debug.Log($"[SaveGraphPromptWindow] Confirmed save as '{finalName}' (mode={mode}).");

            _onConfirm?.Invoke(finalName);
            Close();
        }
        #endregion

        #region ---------------- Helpers ----------------
        private static string SanitizeName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var invalid = Path.GetInvalidFileNameChars();
            var clean = new string(s.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
            return clean;
        }
        #endregion
    }
}
