using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows
{
    public class DialogGraphLauncherWindow : EditorWindow
    {
        #region -------------------- Settings --------------------
        [SerializeField] private bool doDebug = true;
        private const string LAST_GRAPH_PREF_KEY = "DialogGraph_LastGraphName";
        #endregion

        #region -------------------- UI State --------------------
        private PopupField<string> _existingGraphsPopup;
        private TextField _newGraphNameField;
        private Button _openLastButton;
        private Label _lastGraphLabel;

        private string _lastGraphName;
        #endregion

        #region -------------------- Menu --------------------
        // This is now the ONLY entry point for Dialog Graphs
        [MenuItem("Tools/Dialog System/Dialog Graph")]
        public static void Open()
        {
            var window = GetWindow<DialogGraphLauncherWindow>();
            window.titleContent = new GUIContent("Dialog Graphs");
            window.minSize = new Vector2(420f, 260f);
            window.Show();
        }
        #endregion

        #region -------------------- Unity --------------------
        private void OnEnable()
        {
            var ss = Resources.Load<StyleSheet>(TextResources.STYLE_PATH);
            if (ss != null)
                rootVisualElement.styleSheets.Add(ss);

            BuildUI();
        }
        #endregion

        #region -------------------- UI Build --------------------
        private void BuildUI()
        {
            rootVisualElement.Clear();

            var allGraphs = GetAllGraphAssetNames();
            _lastGraphName = PlayerPrefs.GetString(LAST_GRAPH_PREF_KEY, string.Empty);
            bool lastExists = !string.IsNullOrEmpty(_lastGraphName) && allGraphs.Contains(_lastGraphName);

            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.flexGrow = 1;
            root.style.unityFontStyleAndWeight = FontStyle.Normal;
            rootVisualElement.Add(root);

            // Title
            var title = new Label("Dialog Graphs");
            title.AddToClassList("dlg-title");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 16;
            title.style.marginBottom = 4;
            root.Add(title);

            var subtitle = new Label("Choose a graph to edit or create a new one.");
            subtitle.style.opacity = 0.8f;
            subtitle.style.marginBottom = 12;
            root.Add(subtitle);

            // ---------------- LAST GRAPH SECTION ----------------
            if (lastExists)
            {
                var lastBox = MakeSectionBox("Last Opened Graph");

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.justifyContent = Justify.SpaceBetween;

                _lastGraphLabel = new Label(_lastGraphName);
                _lastGraphLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                _openLastButton = new Button(OnClickOpenLast)
                {
                    text = "Open Last"
                };
                _openLastButton.AddToClassList("primary");

                row.Add(_lastGraphLabel);
                row.Add(_openLastButton);

                lastBox.Add(row);
                root.Add(lastBox);
            }

            // ---------------- EXISTING GRAPHS SECTION ----------------
            var existingBox = MakeSectionBox("Existing Graphs");

            if (allGraphs.Count > 0)
            {
                _existingGraphsPopup = new PopupField<string>(
                    label: "Graph:",
                    choices: allGraphs,
                    defaultIndex: 0
                );
                _existingGraphsPopup.style.flexGrow = 1;
                _existingGraphsPopup.AddToClassList("dlg-popup");
                _existingGraphsPopup.AddToClassList("tight-label");

                var openBtn = new Button(OnClickOpenSelected)
                {
                    text = "Open Selected"
                };
                openBtn.AddToClassList("secondary");
                openBtn.style.marginLeft = 8;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.Add(_existingGraphsPopup);
                row.Add(openBtn);

                existingBox.Add(row);
            }
            else
            {
                existingBox.Add(new Label("No DialogGraph assets found yet."));
            }

            root.Add(existingBox);

            // ---------------- CREATE NEW SECTION ----------------
            var createBox = MakeSectionBox("Create New Graph");

            _newGraphNameField = new TextField("Name:")
            {
                value = string.IsNullOrEmpty(_lastGraphName) ? "NewDialogGraph" : _lastGraphName + "_Copy"
            };
            _newGraphNameField.style.flexGrow = 1;
            _newGraphNameField.AddToClassList("dlg-textfield");

            var createBtn = new Button(OnClickCreateNew)
            {
                text = "Create & Open"
            };
            createBtn.AddToClassList("success");
            createBtn.style.marginTop = 6;

            createBox.Add(_newGraphNameField);
            createBox.Add(createBtn);

            root.Add(createBox);

            // If absolutely no graphs exist and no last graph, focus on create
            if (allGraphs.Count == 0 && _newGraphNameField != null)
                _newGraphNameField.Focus();
        }

        private VisualElement MakeSectionBox(string header)
        {
            var box = new VisualElement();
            box.AddToClassList("dlg-section");
            box.style.borderTopWidth = 1;
            box.style.borderBottomWidth = 1;
            box.style.borderLeftWidth = 1;
            box.style.borderRightWidth = 1;
            box.style.borderTopColor = new Color(0, 0, 0, 0.25f);
            box.style.borderBottomColor = new Color(0, 0, 0, 0.25f);
            box.style.borderLeftColor = new Color(0, 0, 0, 0.25f);
            box.style.borderRightColor = new Color(0, 0, 0, 0.25f);
            box.style.marginBottom = 10;
            box.style.paddingTop = 6;
            box.style.paddingBottom = 6;
            box.style.paddingLeft = 8;
            box.style.paddingRight = 8;

            var headerLabel = new Label(header);
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.marginBottom = 4;
            box.Add(headerLabel);

            return box;
        }
        #endregion

        #region -------------------- Button Handlers --------------------
        private void OnClickOpenLast()
        {
            if (string.IsNullOrEmpty(_lastGraphName))
                return;

            var all = GetAllGraphAssetNames();
            if (!all.Contains(_lastGraphName))
            {
                EditorUtility.DisplayDialog("Graph Not Found",
                    $"The last graph '{_lastGraphName}' no longer exists.", "OK");
                return;
            }

            OpenGraphAndClose(_lastGraphName);
        }

        private void OnClickOpenSelected()
        {
            if (_existingGraphsPopup == null || string.IsNullOrEmpty(_existingGraphsPopup.value))
            {
                EditorUtility.DisplayDialog("No Graph Selected", "Please select a graph from the list.", "OK");
                return;
            }

            OpenGraphAndClose(_existingGraphsPopup.value);
        }

        private void OnClickCreateNew()
        {
            var rawName = _newGraphNameField != null ? _newGraphNameField.value : "NewDialogGraph";
            var finalName = rawName.Trim();

            if (string.IsNullOrEmpty(finalName))
            {
                EditorUtility.DisplayDialog("Invalid Name", "Please enter a valid graph name.", "OK");
                return;
            }

            var allGraphs = GetAllGraphAssetNames();
            if (allGraphs.Contains(finalName))
            {
                var choice = EditorUtility.DisplayDialogComplex(
                    "Graph Already Exists",
                    $"A DialogGraph named '{finalName}' already exists.\n\nWhat would you like to do?",
                    "Open Existing",
                    "Cancel",
                    "Create Anyway (Overwrite)"
                );

                // 0 = Open Existing
                if (choice == 0)
                {
                    OpenGraphAndClose(finalName);
                    return;
                }
                // 1 = Cancel
                if (choice == 1)
                    return;
                // 2 = Overwrite → continue to create new asset with same name
            }

            CreateDialogGraphAsset(finalName);
            OpenGraphAndClose(finalName);
        }
        #endregion

        #region -------------------- Helpers --------------------
        private void OpenGraphAndClose(string graphName)
        {
            if (doDebug)
                Debug.Log($"[DialogGraphLauncherWindow] Opening graph '{graphName}'");

            PlayerPrefs.SetString(LAST_GRAPH_PREF_KEY, graphName);
            PlayerPrefs.Save();

            DialogGraphEditorWindow.OpenWithGraph(graphName);
            Close(); // launcher closes, editor takes over
        }

        private void CreateDialogGraphAsset(string graphName)
        {
            var folder = TextResources.CONVERSATION_FOLDER.TrimEnd('/');
            EnsureFolderExistsUnderAssets(folder);

            var path = $"{folder}/{graphName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (doDebug)
                Debug.Log($"[DialogGraphLauncherWindow] Created DialogGraph asset at: {path}");
        }

        private void EnsureFolderExistsUnderAssets(string folder)
        {
            var normalized = folder.Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(normalized))
                return;

            var parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                Debug.LogError($"[DialogGraphLauncherWindow] Folder must be under 'Assets/'. Current: '{folder}'");
                return;
            }

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private List<string> GetAllGraphAssetNames()
        {
            var names = new HashSet<string>();

            void CollectFromFolder(string folder)
            {
                if (string.IsNullOrEmpty(folder)) return;
                var phys = folder.Replace("\\", "/");
                if (!Directory.Exists(phys)) return;

                var assetPaths = Directory.GetFiles(phys, "*.asset", SearchOption.AllDirectories)
                    .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    .Select(path => path.Replace('\\', '/'))
                    .ToList();

                foreach (var path in assetPaths)
                {
                    var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (obj is DialogGraph)
                    {
                        var name = Path.GetFileNameWithoutExtension(path);
                        names.Add(name);
                    }
                }
            }

            // Look in both folders to be robust
            CollectFromFolder(TextResources.GRAPHS_FOLDER);
            CollectFromFolder(TextResources.CONVERSATION_FOLDER);

            return names.OrderBy(n => n).ToList();
        }
        #endregion
    }
}
