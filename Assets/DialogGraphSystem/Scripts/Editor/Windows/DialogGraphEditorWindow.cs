using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using DialogSystem.Runtime.Models;
using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.View.Elements.Nodes;
using DialogSystem.Runtime.Utils;

namespace DialogSystem.EditorTools.Windows
{
    public class DialogGraphEditorWindow : EditorWindow
    {
        #region -------------------- SETTINGS --------------------
        [SerializeField] private bool doDebug = true;

        [SerializeField, Tooltip("Initial width of the Sidebar when shown.")]
        private float initialSidebarWidth = 340f;

        private const string LastGraphPrefKey = "DialogGraph_LastGraphName";
        #endregion

        #region -------------------- STATE --------------------
        // Graph + layout
        private DialogGraphView _graphView;
        private VisualElement _toolbarWrapper;
        private TwoPaneSplitView _split;          // graph (left) | sidebar (right)
        private VisualElement _graphHost;         // container to keep GraphView sizing stable
        private VisualElement _rightSidebarRoot;  // sidebar container
        private VisualElement _soloHost;          // used when sidebar is collapsed

        // Toolbar widgets
        private PopupField<string> _graphPopup;
        private Button _addNodeBtn, _loadBtn, _saveBtn, _clearBtn, _toggleSidebarBtn;

        // Sidebar (separate class)
        private EditorSidbarWindow _editorSidebar;

        // Runtime
        private string _loadedGraphName;
        private bool _sidebarVisible = true;
        private float _sidebarWidthMemo = 440f;
        #endregion

        #region -------------------- STATIC API (Launcher entry) --------------------
        /// <summary>
        /// Called from DialogGraphLauncherWindow. Opens the editor and loads the given graph.
        /// </summary>
        public static void OpenWithGraph(string graphName)
        {
            var window = GetWindow<DialogGraphEditorWindow>();
            window.titleContent = new GUIContent($"Dialog Graph - {graphName}");
            window.Show();
            window.Focus();
            window.LoadGraphExternal(graphName);
        }
        #endregion

        #region -------------------- UNITY --------------------
        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedoPerformed;

            var ss = Resources.Load<StyleSheet>(TextResources.STYLE_PATH);
            if (ss != null) rootVisualElement.styleSheets.Add(ss);

            BuildUI();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;

            // Auto-save if there’s a loaded graph
            if (!string.IsNullOrEmpty(_loadedGraphName) && _graphView != null)
            {
                _graphView.SaveGraph(_loadedGraphName);

                PlayerPrefs.SetString(LastGraphPrefKey, _loadedGraphName);
                PlayerPrefs.Save();
            }

            rootVisualElement.Clear();
        }

        private void OnUndoRedoPerformed()
        {
            if (_graphView == null || string.IsNullOrEmpty(_loadedGraphName))
                return;

            // Cache current pan/zoom of the GraphView before we rebuild it.
            var cachedPosition = _graphView.contentViewContainer.transform.position;
            var cachedScale = _graphView.contentViewContainer.transform.scale;

            // Reload the graph data (nodes, edges, etc.) so the view matches Undo state.
            LoadGraph(_loadedGraphName, true);

            // Restore camera (pan/zoom) so Undo does *not* jump back to the Start node.
            _graphView.contentViewContainer.transform.position = cachedPosition;
            _graphView.contentViewContainer.transform.scale = cachedScale;

            Repaint();
        }

        #endregion

        #region -------------------- UI BUILD --------------------
        private void BuildUI()
        {
            rootVisualElement.Clear();

            // Top toolbar
            _toolbarWrapper = new VisualElement();
            _toolbarWrapper.AddToClassList("dlg-toolbar");
            rootVisualElement.Add(_toolbarWrapper);
            GenerateToolbar();

            CreateGraphAndSidebar();
            CreateSplit();

            _sidebarWidthMemo = Mathf.Max(240f, initialSidebarWidth);
            UpdateSidebarToggleText();
        }

        private void CreateGraphAndSidebar()
        {
            // Graph
            _graphView = new DialogGraphView { name = "Dialog Graph" };
            _graphHost = new VisualElement();
            _graphHost.AddToClassList("dlg-graph-host");
            _graphHost.style.flexGrow = 1;
            _graphHost.Add(_graphView);
            _graphView.StretchToParentSize();

            // Sidebar
            _rightSidebarRoot = new VisualElement();
            _rightSidebarRoot.AddToClassList("dlg-sidebar");

            _editorSidebar = new EditorSidbarWindow(this);
            _rightSidebarRoot.Add(_editorSidebar);

            _graphView?.EnsureStartEndNodes();
        }

        private void CreateSplit()
        {
            _split = new TwoPaneSplitView(1, _sidebarWidthMemo, TwoPaneSplitViewOrientation.Horizontal);
            _split.AddToClassList("dlg-split");
            _split.style.flexGrow = 1;

            _split.Add(_graphHost);
            _split.Add(_rightSidebarRoot);

            rootVisualElement.Add(_split);
        }
        #endregion

        #region -------------------- TOOLBAR --------------------
        private void GenerateToolbar()
        {
            _loadedGraphName = null;

            _graphPopup = CreateGraphPopup();
            _toolbarWrapper.Add(_graphPopup);

            _loadBtn = CreateButton("Load", TextResources.ICON_LOAD, "Load graph", OnClickLoad, "secondary");
            _toolbarWrapper.Add(_loadBtn);

            _toolbarWrapper.Add(MakeSpacer());

            _addNodeBtn = CreateButton("Add Node", TextResources.ICON_ADD, "Create a new node", OnClickAddNode, "success");
            _toolbarWrapper.Add(_addNodeBtn);

            _saveBtn = CreateButton("Save", TextResources.ICON_SAVE, "Save graph", OnClickSave, "primary");
            _toolbarWrapper.Add(_saveBtn);

            _clearBtn = CreateButton("Clear", TextResources.ICON_CLEAR, "Clear current graph", OnClickClear, "danger");
            _toolbarWrapper.Add(_clearBtn);

            _toggleSidebarBtn = CreateButton("Hide Sidebar", TextResources.ICON_SIDEBAR, "Show/Hide Sidebar", ToggleSidebar, "secondary");
            _toggleSidebarBtn.style.marginLeft = 10;
            _toolbarWrapper.Add(_toggleSidebarBtn);
        }

        private PopupField<string> CreateGraphPopup()
        {
            var graphNames = GetAllGraphAssetNamesFallback();
            var pop = new PopupField<string>("Dialogs:", graphNames, graphNames.Count > 0 ? 0 : -1)
            {
                style = { maxWidth = 250, marginRight = 0, marginLeft = 15, flexGrow = 1 },
                tooltip = "Select a graph to load"
            };
            pop.AddToClassList("dlg-popup");
            pop.AddToClassList("tight-label");

            var popLabel = pop.labelElement;
            popLabel.style.width = StyleKeyword.Auto;
            popLabel.style.minWidth = StyleKeyword.Auto;
            popLabel.style.flexShrink = 0;
            popLabel.style.marginRight = 6;

            return pop;
        }

        private VisualElement MakeSpacer()
        {
            var spacer = new VisualElement();
            spacer.AddToClassList("dlg-spacer");
            return spacer;
        }

        private Button CreateButton(string label, string iconPath, string tooltip, Action onClick, string styleClass = null, bool iconOnly = false)
        {
            var btn = new Button { tooltip = string.IsNullOrEmpty(tooltip) ? label : tooltip };
            btn.AddToClassList("dlg-btn");
            if (!string.IsNullOrEmpty(styleClass)) btn.AddToClassList(styleClass);
            if (onClick != null) btn.clicked += onClick;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1;

#if UNITY_EDITOR
            Texture2D tex = (!string.IsNullOrEmpty(iconPath)) ? AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath) : null;
            if (tex != null)
            {
                var img = new Image { image = tex, scaleMode = ScaleMode.ScaleToFit };
                img.style.width = 20;
                img.style.height = 20;
                img.style.marginRight = iconOnly ? 0 : 8;
                row.Add(img);
            }
#endif
            var lbl = new Label(iconOnly ? "" : label);
            lbl.style.flexShrink = 1;
            lbl.style.overflow = Overflow.Hidden;
#if UNITY_2021_3_OR_NEWER
            lbl.style.textOverflow = TextOverflow.Ellipsis;
#endif
            row.Add(lbl);

            btn.text = ""; // avoid default overlay
            btn.Add(row);

            btn.style.minHeight = 26;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 10;

            return btn;
        }
        #endregion

        #region -------------------- TOOLBAR HANDLERS --------------------
        private void OnClickAddNode() => ShowCreateNodeMenu();

        private void OnClickLoad()
        {
            var selected = _graphPopup != null ? _graphPopup.value : null;
            if (string.IsNullOrEmpty(selected))
            {
                EditorUtility.DisplayDialog("No graph selected", "Please choose a graph from the popup.", "OK");
                return;
            }
            LoadGraph(selected, false);
            _loadedGraphName = selected;

            if (doDebug)
                Debug.Log($"[DialogGraphEditorWindow] Loaded graph '{selected}' via toolbar Load.");

            _editorSidebar?.RebuildFromGraph();
            PlayerPrefs.SetString(LastGraphPrefKey, selected);
            PlayerPrefs.Save();
        }

        private void OnClickSave() => OpenSavePopup();
        private void OnClickClear() => ClearLoadedGraph();
        #endregion

        #region -------------------- SAVE / LOAD / CLEAR --------------------
        /// <summary>
        /// Entry used by the launcher to load/switch graphs.
        /// Ensures UI, loads graph, updates popup, updates last-graph pref.
        /// </summary>
        internal void LoadGraphExternal(string graphName)
        {
            // Ensure UI exists
            if (_graphView == null || _toolbarWrapper == null)
            {
                BuildUI();
            }

            // Auto-save currently loaded graph if switching
            if (!string.IsNullOrEmpty(_loadedGraphName) &&
                _loadedGraphName != graphName &&
                _graphView != null)
            {
                _graphView.SaveGraph(_loadedGraphName);
            }

            LoadGraph(graphName, false);
            _loadedGraphName = graphName;

            // Refresh popup choices and select the requested graph
            var names = GetAllGraphAssetNamesFallback();
            if (!names.Contains(graphName))
            {
                names.Add(graphName);
                names = names.Distinct().OrderBy(n => n).ToList();
            }

            if (_graphPopup != null)
            {
                _graphPopup.choices.Clear();
                foreach (var n in names)
                    _graphPopup.choices.Add(n);

                if (_graphPopup.choices.Contains(graphName))
                    _graphPopup.SetValueWithoutNotify(graphName);
            }

            _editorSidebar?.RebuildFromGraph();

            PlayerPrefs.SetString(LastGraphPrefKey, graphName);
            PlayerPrefs.Save();

            if (doDebug)
                Debug.Log($"[DialogGraphEditorWindow] Loaded graph '{graphName}' via launcher.");
        }

        private void OpenSavePopup()
        {
            var existing = GetAllGraphAssetNamesFallback();
            var suggestion = string.IsNullOrEmpty(_loadedGraphName) ? "NewDialogGraph" : _loadedGraphName;
            bool isEmpty = _graphView != null && _graphView.IsGraphEmptyForSave();

            SaveGraphPromptWindow.Open(
                currentName: suggestion,
                loadedGraphName: _loadedGraphName,
                existingNames: existing,
                isGraphEmpty: isEmpty,
                onConfirm: finalName =>
                {
                    SaveAsset(finalName);

                    // Refresh popup choices / selection
                    var namesAfter = GetAllGraphAssetNamesFallback();
                    if (namesAfter.Contains(finalName))
                    {
                        _graphPopup.choices.Clear();
                        foreach (var n in namesAfter)
                            _graphPopup.choices.Add(n);

                        _graphPopup.SetValueWithoutNotify(finalName);
                    }

                    _loadedGraphName = finalName;

                    PlayerPrefs.SetString(LastGraphPrefKey, finalName);
                    PlayerPrefs.Save();

                    if (doDebug)
                        Debug.Log($"[DialogGraphEditorWindow] Saved graph as '{finalName}'.");
                }
            );
        }

        private void SaveAsset(string fileName) => _graphView.SaveGraph(fileName);

        private void LoadGraph(string fileName, bool onUndo)
        {
            _graphView.LoadGraph(fileName, onUndo);
            _loadedGraphName = fileName;
            _editorSidebar.RebuildFromGraph();
        }

        private void ClearLoadedGraph()
        {
            _graphView.ClearGraphWithConfirmation();
            _editorSidebar.ClearAll();
        }

        private List<string> GetAllGraphAssetNamesFallback()
        {
            if (!Directory.Exists(TextResources.GRAPHS_FOLDER) &&
                !Directory.Exists(TextResources.CONVERSATION_FOLDER))
                return new List<string>();

            var assetPaths = new List<string>();

            if (Directory.Exists(TextResources.GRAPHS_FOLDER))
            {
                assetPaths.AddRange(
                    Directory.GetFiles(TextResources.GRAPHS_FOLDER, "*.asset", SearchOption.AllDirectories)
                        .Where(path => !path.EndsWith(".meta"))
                        .Select(path => path.Replace('\\', '/'))
                );
            }

            if (Directory.Exists(TextResources.CONVERSATION_FOLDER))
            {
                assetPaths.AddRange(
                    Directory.GetFiles(TextResources.CONVERSATION_FOLDER, "*.asset", SearchOption.AllDirectories)
                        .Where(path => !path.EndsWith(".meta"))
                        .Select(path => path.Replace('\\', '/'))
                );
            }

            List<string> validNames = new();

            foreach (var path in assetPaths)
            {
                var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (obj is DialogGraph)
                    validNames.Add(Path.GetFileNameWithoutExtension(path));
            }

            return validNames.Distinct().OrderBy(n => n).ToList();
        }
        #endregion

        #region -------------------- SIDEBAR SHOW/HIDE --------------------
        public void ToggleSidebar()
        {
            if (_sidebarVisible) CollapseSidebar();
            else ExpandSidebar();
        }

        private void CollapseSidebar()
        {
            if (!_sidebarVisible) return;
            _sidebarVisible = false;

            _sidebarWidthMemo = Mathf.Max(240f, _rightSidebarRoot.resolvedStyle.width);

            rootVisualElement.Remove(_split);

            if (_soloHost == null)
            {
                _soloHost = new VisualElement();
                _soloHost.AddToClassList("dlg-graph-host");
                _soloHost.style.flexGrow = 1;
            }

            _graphHost.RemoveFromHierarchy();
            _soloHost.Add(_graphHost);
            rootVisualElement.Add(_soloHost);

            UpdateSidebarToggleText();
        }

        private void ExpandSidebar()
        {
            if (_sidebarVisible) return;
            _sidebarVisible = true;

            if (_soloHost != null && _soloHost.parent != null)
                rootVisualElement.Remove(_soloHost);

            CreateSplit();
            UpdateSidebarToggleText();
        }

        private void UpdateSidebarToggleText()
        {
            if (_toggleSidebarBtn == null) return;

            _toggleSidebarBtn.text = string.Empty;
            var label = _toggleSidebarBtn.Q<Label>();
            if (label != null)
                label.text = _sidebarVisible ? "Hide Sidebar" : "Show Sidebar";
        }
        #endregion

        #region -------------------- HELPERS (exposed to sidebar) --------------------
        internal DialogGraphView GetGraphView() => _graphView;

        internal IEnumerable<string> CollectSpeakersFromNodes()
        {
            if (_graphView == null) yield break;

            foreach (var ge in _graphView.nodes.ToList())
            {
                if (ge is not DialogNodeView dnv) continue;
                var speaker = dnv.speakerName;
                if (!string.IsNullOrWhiteSpace(speaker))
                    yield return speaker.Trim();
            }
        }

        internal Sprite FindFirstSpriteForSpeaker(string speaker)
        {
            if (_graphView == null || string.IsNullOrWhiteSpace(speaker)) return null;
            var key = speaker.Trim();

            foreach (var ge in _graphView.nodes.ToList())
            {
                if (ge is not DialogNodeView dnv) continue;
                var s = dnv.speakerName;
                if (string.Equals(s?.Trim(), key, StringComparison.Ordinal))
                {
                    var spr = dnv.portraitSprite;
                    if (spr != null) return spr;
                }
            }
            return null;
        }

        internal (bool found, Color color) FindFirstNameColorForSpeaker(string speaker)
        {
            // Placeholder for future per-speaker color
            return (false, default);
        }

        internal void ApplySpritesToNodes(List<CharacterBinding> bindings)
        {
            if (_graphView == null || bindings == null || bindings.Count == 0) return;

            foreach (var ge in _graphView.nodes.ToList())
            {
                if (ge is not DialogNodeView dnv) continue;
                var speaker = dnv.speakerName?.Trim();
                if (string.IsNullOrEmpty(speaker)) continue;

                var bind = bindings.FirstOrDefault(b =>
                    !string.IsNullOrWhiteSpace(b.originalName) &&
                    b.originalName.Trim().Equals(speaker, StringComparison.Ordinal));

                if (bind == null) continue;

                var newName = bind.currentName?.Trim();
                if (!string.IsNullOrEmpty(newName) && !string.Equals(newName, speaker, StringComparison.Ordinal))
                    dnv.SetSpeakerName(newName);

                if (bind.sprite != null && bind.sprite != dnv.portraitSprite)
                    dnv.SetPortraitSprite(bind.sprite);

                dnv.MarkDirtyRepaint();
            }

            _graphView.schedule.Execute(() =>
            {
                foreach (var n in _graphView.nodes.ToList()) n.MarkDirtyRepaint();
            }).ExecuteLater(16);
        }

        internal IEnumerable<ActionNodeView> CollectActionNodes()
        {
            if (_graphView == null) return Enumerable.Empty<ActionNodeView>();
            return _graphView.nodes.ToList().OfType<ActionNodeView>();
        }

        internal void ApplyActionsToNodes(List<ActionBinding> bindings)
        {
            if (_graphView == null || bindings == null || bindings.Count == 0) return;

            var actionViews = CollectActionNodes().ToList();

            foreach (var bind in bindings)
            {
                var originalKey = bind.originalActionId ?? "";
                foreach (var av in actionViews)
                {
                    var curId = av.actionId ?? "";
                    if (!string.Equals(curId, originalKey, StringComparison.Ordinal)) continue;

                    av.LoadNodeData(
                        bind.actionId ?? "",
                        bind.payloadJson ?? "",
                        bind.waitForCompletion,
                        bind.waitSeconds
                    );

                    av.MarkDirtyRepaint();
                }
            }

            _graphView.schedule.Execute(() =>
            {
                foreach (var n in actionViews) n.MarkDirtyRepaint();
            }).ExecuteLater(16);
        }

        #endregion

        #region -------------------- DATA TYPES --------------------
        [Serializable]
        public class CharacterBinding
        {
            public string originalName;
            public string currentName;
            public Sprite sprite;
        }

        [Serializable]
        public class ActionBinding
        {
            public string originalActionId;
            public string actionId;
            public string payloadJson;
            public bool waitForCompletion;
            public float waitSeconds;
        }
        #endregion

        #region -------------------- CREATE NODE MENU --------------------
        private void ShowCreateNodeMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Dialog"), false, () =>
            {
                _graphView?.CreateDialogNode("New Dialog", true);
            });

            menu.AddItem(new GUIContent("Choice"), false, () =>
            {
                _graphView?.CreateChoiceNode("Choice", true);
            });

            menu.AddItem(new GUIContent("Action"), false, () =>
            {
                _graphView?.CreateActionNode("Action", true);
            });

            try
            {
                if (_addNodeBtn != null)
                {
                    var btnWorld = _addNodeBtn.worldBound;
                    var screenPos = new Vector2(position.x + btnWorld.x, position.y + btnWorld.y + btnWorld.height);
                    var anchor = new Rect(screenPos, new Vector2(1f, 1f));
                    menu.DropDown(anchor);
                    return;
                }
            }
            catch
            {
                // fallback
            }

            menu.ShowAsContext();
        }
        #endregion
    }
}
