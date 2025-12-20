using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Utils;

namespace DialogSystem.EditorTools.ExportImport
{
    public class DialogJsonIOWindow : EditorWindow
    {
        #region ---------------- Debug & Tabs ----------------
        [SerializeField] private bool doDebug = true;

        private enum Tab { Export, Import }
        private Tab _tab = Tab.Export;

        private Vector2 _scroll;
        private GUIStyle _headerStyle;
        private GUIStyle _boxStyle;
        #endregion

        #region ---------------- Icons & Menu ----------------
        private Texture2D _iconExport;
        private Texture2D _iconImport;

        [MenuItem("Tools/Dialog System/JSON Import-Export...")]
        public static void Open()
        {
            var win = GetWindow<DialogJsonIOWindow>("Dialog JSON I/O");
            win.minSize = new Vector2(520, 420);
            win.Show();
        }
        #endregion

        #region ---------------- EditorPrefs Keys ----------------
        private const string K_PREFS_KEY_ROOT = "DialogJsonIOWindow";
        private const string K_PREFS_EXPORT_FOLDER = K_PREFS_KEY_ROOT + ".exportFolder";
        private const string K_PREFS_IMPORT_FOLDER = K_PREFS_KEY_ROOT + ".importFolder";
        #endregion

        #region ---------------- Export State ----------------
        private DialogGraph _exportGraph;
        private string _exportFileName = "";
        private string _exportFolder = TextResources.EXPORT_FOLDER;
        private bool _exportPretty = true;
        private bool _exportIncludePositions = true;
        #endregion

        #region ---------------- Import State ----------------
        private TextAsset _importJsonAsset;
        private string _importExternalPath = "";
        private string _importFolder = TextResources.IMPORT_FOLDER;
        private string _importTargetName = "ImportedConversation";

        private string _loadedJson = "";
        private string _jsonError = "";
        private DialogGraphExport _previewDto;
        #endregion

        #region ---------------- Unity ----------------
        private void OnEnable()
        {
            _exportFolder = EditorPrefs.GetString(K_PREFS_EXPORT_FOLDER, _exportFolder);
            _importFolder = EditorPrefs.GetString(K_PREFS_IMPORT_FOLDER, _importFolder);

            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _boxStyle = new GUIStyle("HelpBox") { padding = new RectOffset(8, 8, 8, 8) };

            _iconExport = AssetDatabase.LoadAssetAtPath<Texture2D>(TextResources.ICON_EXPORT);
            _iconImport = AssetDatabase.LoadAssetAtPath<Texture2D>(TextResources.ICON_IMPORT);
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(K_PREFS_EXPORT_FOLDER, _exportFolder);
            EditorPrefs.SetString(K_PREFS_IMPORT_FOLDER, _importFolder);
        }
        #endregion

        #region ---------------- GUI Root ----------------
        private void OnGUI()
        {
            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Export:
                    DrawExport();
                    break;
                case Tab.Import:
                    DrawImport();
                    break;
            }
            EditorGUILayout.EndScrollView();

            DrawDragAndDropArea();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var exportTex = _iconExport ? _iconExport : (Texture2D)EditorGUIUtility.IconContent("d_SaveAs").image;
                var importTex = _iconImport ? _iconImport : (Texture2D)EditorGUIUtility.IconContent("d_Import").image;

                var prevIconSize = EditorGUIUtility.GetIconSize();
                EditorGUIUtility.SetIconSize(new Vector2(16, 16));

                var exportContent = new GUIContent(" Export", exportTex);
                var importContent = new GUIContent(" Import", importTex);

                var style = EditorStyles.toolbarButton;
                float w = Mathf.Ceil(Mathf.Max(style.CalcSize(exportContent).x, style.CalcSize(importContent).x));

                if (GUILayout.Toggle(_tab == Tab.Export, exportContent, style, GUILayout.Width(w)))
                    _tab = Tab.Export;

                if (GUILayout.Toggle(_tab == Tab.Import, importContent, style, GUILayout.Width(w)))
                    _tab = Tab.Import;

                EditorGUIUtility.SetIconSize(prevIconSize);
                GUILayout.FlexibleSpace();
            }
        }
        #endregion

        #region ---------------- Export UI ----------------
        private void DrawExport()
        {
            EditorGUILayout.LabelField("Export DialogGraph → JSON", _headerStyle);
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.VerticalScope(_boxStyle))
            {
                _exportGraph = (DialogGraph)EditorGUILayout.ObjectField("Graph", _exportGraph, typeof(DialogGraph), false);

                using (new EditorGUILayout.HorizontalScope())
                {
                    string suggested = (_exportGraph != null && string.IsNullOrEmpty(_exportFileName))
                        ? SafeFile(_exportGraph.name)
                        : _exportFileName;

                    _exportFileName = EditorGUILayout.TextField("File Name", suggested);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel(new GUIContent("Folder (Assets)"));
                    EditorGUILayout.SelectableLabel(_exportFolder, EditorStyles.textField, GUILayout.Height(18));
                    if (GUILayout.Button("Select...", GUILayout.Width(80)))
                    {
                        var abs = EditorUtility.OpenFolderPanel("Choose export folder (inside Assets)", Application.dataPath, "");
                        if (IsUnderAssets(abs, out var rel))
                            _exportFolder = rel;
                        else if (!string.IsNullOrEmpty(abs))
                            EditorUtility.DisplayDialog("Folder not under Assets", "Please choose a folder inside your project's Assets.", "OK");
                    }
                }

                _exportPretty = EditorGUILayout.ToggleLeft("Pretty Print", _exportPretty);
                _exportIncludePositions = EditorGUILayout.ToggleLeft("Include Node Positions", _exportIncludePositions);

                EditorGUILayout.Space(6);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Use Selected Graph", GUILayout.Width(160)))
                    {
                        var sel = Selection.activeObject as DialogGraph;
                        if (sel) _exportGraph = sel;
                    }

                    var canExport = _exportGraph != null && Directory.Exists(AbsoluteFromAssets(_exportFolder));
                    EditorGUI.BeginDisabledGroup(!canExport);
                    if (GUILayout.Button(new GUIContent("Export", EditorGUIUtility.IconContent("d_SaveAs").image), GUILayout.Width(120)))
                        DoExport();
                    EditorGUI.EndDisabledGroup();
                }
            }
        }

        private void DoExport()
        {
            if (_exportGraph == null) return;

            var fileName = string.IsNullOrEmpty(_exportFileName)
                ? SafeFile(_exportGraph.name)
                : SafeFile(_exportFileName);

            if (string.IsNullOrEmpty(fileName))
                fileName = "DialogGraph";

            var assetRelDir = _exportFolder;
            var absDir = AbsoluteFromAssets(assetRelDir);
            if (!Directory.Exists(absDir))
                Directory.CreateDirectory(absDir);

            var relPath = $"{assetRelDir}/{fileName}.json";
            var absPath = AbsoluteFromAssets(relPath);

            if (File.Exists(absPath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "File exists",
                    $"A JSON file already exists at:\n{relPath}\n\nOverwrite it?",
                    "Overwrite",
                    "Cancel"
                );
                if (!overwrite)
                    return;
            }

            var export = BuildExportDTO(_exportGraph);
            var json = JsonUtility.ToJson(export, _exportPretty);
            File.WriteAllText(absPath, json, Encoding.UTF8);
            AssetDatabase.Refresh();

            ShowNotification(new GUIContent($"Exported → {relPath}"));

            if (doDebug)
                Debug.Log($"[DialogJsonIOWindow] Exported DialogGraph '{_exportGraph.name}' to: {relPath}");
        }
        #endregion

        #region ---------------- Import UI ----------------
        private void DrawImport()
        {
            EditorGUILayout.LabelField("Import JSON → DialogGraph", _headerStyle);
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.VerticalScope(_boxStyle))
            {
                // Source JSON
                EditorGUILayout.LabelField("Source JSON", EditorStyles.boldLabel);

                _importJsonAsset = (TextAsset)EditorGUILayout.ObjectField("TextAsset (optional)", _importJsonAsset, typeof(TextAsset), false);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel(new GUIContent("External File"));
                    EditorGUILayout.SelectableLabel(_importExternalPath, EditorStyles.textField, GUILayout.Height(18));
                    if (GUILayout.Button("Browse...", GUILayout.Width(80)))
                    {
                        var p = EditorUtility.OpenFilePanel("Choose JSON file", GetInitialImportFolder(), "json");
                        if (!string.IsNullOrEmpty(p))
                            _importExternalPath = p;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Load & Validate", GUILayout.Height(22)))
                        LoadJsonForPreview();
                }

                if (!string.IsNullOrEmpty(_jsonError))
                    EditorGUILayout.HelpBox(_jsonError, MessageType.Error);

                if (_previewDto != null)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
                        int dialogCount = _previewDto.dialogNodes?.Count ?? 0;
                        int choiceCount = _previewDto.choiceNodes?.Count ?? 0;
                        int actionCount = _previewDto.actionNodes?.Count ?? 0;
                        int linkCount = _previewDto.links?.Count ?? 0;

                        EditorGUILayout.LabelField($"Dialog Nodes: {dialogCount}");
                        EditorGUILayout.LabelField($"Choice Nodes: {choiceCount}");
                        EditorGUILayout.LabelField($"Action Nodes: {actionCount}");
                        EditorGUILayout.LabelField($"Links: {linkCount}");
                    }
                }

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Target Asset", EditorStyles.boldLabel);

                _importTargetName = EditorGUILayout.TextField("Asset Name", _importTargetName);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel(new GUIContent("Folder (Assets)"));
                    EditorGUILayout.SelectableLabel(_importFolder, EditorStyles.textField, GUILayout.Height(18));
                    if (GUILayout.Button("Select...", GUILayout.Width(80)))
                    {
                        var abs = EditorUtility.OpenFolderPanel("Choose graph folder (inside Assets)", Application.dataPath, "");
                        if (IsUnderAssets(abs, out var rel))
                            _importFolder = rel;
                        else if (!string.IsNullOrEmpty(abs))
                            EditorUtility.DisplayDialog("Folder not under Assets", "Please choose a folder inside your project's Assets.", "OK");
                    }
                }

                EditorGUILayout.HelpBox(
                    "Import will create or overwrite a DialogGraph asset using the name and folder above.\n" +
                    "If an asset with the same name already exists, you will be asked whether to overwrite it or create a copy.",
                    MessageType.Info
                );

                EditorGUILayout.Space(8);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    bool canImport = _previewDto != null;
                    EditorGUI.BeginDisabledGroup(!canImport);
                    if (GUILayout.Button(new GUIContent("Import", EditorGUIUtility.IconContent("d_Import").image), GUILayout.Width(140), GUILayout.Height(24)))
                        DoImport();
                    EditorGUI.EndDisabledGroup();
                }
            }
        }

        private void DoImport()
        {
            if (_previewDto == null)
            {
                EditorUtility.DisplayDialog("No JSON", "Load a JSON file and validate it first.", "OK");
                return;
            }

            CreateOrOverwriteGraphFromDto(_previewDto);
        }
        #endregion

        #region ---------------- Export DTO Builder ----------------
        private DialogGraphExport BuildExportDTO(DialogGraph graph)
        {
            var export = new DialogGraphExport
            {
                dialogNodes = new List<DialogExportDialogNode>(),
                choiceNodes = new List<DialogExportChoiceNode>(),
                actionNodes = new List<DialogExportActionNode>(),
                links = new List<ExportLink>()
            };

            // Dialog nodes
            if (graph.nodes != null)
            {
                foreach (var n in graph.nodes.Where(n => n != null))
                {
                    export.dialogNodes.Add(new DialogExportDialogNode
                    {
                        title = (n.name ?? "Node").Replace("Node_", ""),
                        guid = n.GetGuid(),
                        speaker = n.speakerName,
                        question = n.questionText,
                        nodePositionX = _exportIncludePositions ? n.GetPosition().x : 0f,
                        nodePositionY = _exportIncludePositions ? n.GetPosition().y : 0f,
                        displayTime = n.displayTime,
                    });
                }
            }

            // Choice nodes
            if (graph.choiceNodes != null)
            {
                foreach (var c in graph.choiceNodes.Where(c => c != null))
                {
                    var dto = new DialogExportChoiceNode
                    {
                        guid = c.GetGuid(),
                        text = c.text,
                        nodePositionX = _exportIncludePositions ? c.GetPosition().x : 0f,
                        nodePositionY = _exportIncludePositions ? c.GetPosition().y : 0f,
                        choices = new List<ExportChoice>(),
                    };

                    if (c.choices != null)
                    {
                        foreach (var ch in c.choices)
                        {
                            dto.choices.Add(new ExportChoice
                            {
                                answerText = ch.answerText,
                                nextNodeGUID = ch.nextNodeGUID
                            });
                        }
                    }

                    export.choiceNodes.Add(dto);
                }
            }

            // Action nodes
            if (graph.actionNodes != null)
            {
                foreach (var a in graph.actionNodes.Where(a => a != null))
                {
                    export.actionNodes.Add(new DialogExportActionNode
                    {
                        guid = a.GetGuid(),
                        actionId = a.actionId,
                        payloadJson = a.payloadJson,
                        waitForCompletion = a.waitForCompletion,
                        waitSeconds = a.waitSeconds,
                        nodePositionX = _exportIncludePositions ? a.GetPosition().x : 0f,
                        nodePositionY = _exportIncludePositions ? a.GetPosition().y : 0f
                    });
                }
            }

            // Start / End
            if (!string.IsNullOrEmpty(graph.startGuid))
            {
                export.startNode = new ExportStartNode
                {
                    isInitialized = graph.startInitialized,
                    guid = graph.startGuid,
                    nodePositionX = graph.startPosition.x,
                    nodePositionY = graph.startPosition.y
                };
            }

            if (!string.IsNullOrEmpty(graph.endGuid))
            {
                export.endNode = new ExportEndNode
                {
                    isInitialized = graph.endInitialized,
                    guid = graph.endGuid,
                    nodePositionX = graph.endPosition.x,
                    nodePositionY = graph.endPosition.y
                };
            }

            // Links
            if (graph.links != null)
            {
                export.links = graph.links.Select(l => new ExportLink
                {
                    fromGuid = l.fromGuid,
                    toGuid = l.toGuid,
                    fromPortIndex = l.fromPortIndex
                }).ToList();
            }

            return export;
        }
        #endregion

        #region ---------------- Import Logic (Safe) ----------------
        private void CreateOrOverwriteGraphFromDto(DialogGraphExport dto)
        {
            var absDir = AbsoluteFromAssets(_importFolder);
            if (!Directory.Exists(absDir))
                Directory.CreateDirectory(absDir);

            var baseName = string.IsNullOrEmpty(_importTargetName)
                ? "ImportedConversation"
                : SafeFile(_importTargetName);

            var targetRelPath = $"{_importFolder}/{baseName}.asset";
            var targetAbsPath = AbsoluteFromAssets(targetRelPath);

            // If asset already exists at that path → ask user
            if (File.Exists(targetAbsPath))
            {
                int option = EditorUtility.DisplayDialogComplex(
                    "DialogGraph already exists",
                    $"A DialogGraph asset with this name already exists:\n\n{targetRelPath}\n\n" +
                    "What would you like to do?",
                    "Overwrite",      // 0
                    "Create Copy",    // 1
                    "Cancel"          // 2
                );

                if (option == 2)
                    return;

                if (option == 0)
                {
                    OverwriteExistingGraph(targetRelPath, dto);
                    return;
                }

                if (option == 1)
                {
                    targetRelPath = AssetDatabase.GenerateUniqueAssetPath(targetRelPath);
                    targetAbsPath = AbsoluteFromAssets(targetRelPath);
                }
            }

            // Create brand-new asset
            var newGraph = ScriptableObject.CreateInstance<DialogGraph>();
            AssetDatabase.CreateAsset(newGraph, targetRelPath);

            BuildFromDto(newGraph, dto);

            EditorUtility.SetDirty(newGraph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = newGraph;
            ShowNotification(new GUIContent($"Created {Path.GetFileName(targetRelPath)}"));

            if (doDebug)
                Debug.Log($"[DialogJsonIOWindow] Created new DialogGraph at: {targetRelPath}");
        }

        private void OverwriteExistingGraph(string targetRelPath, DialogGraphExport dto)
        {
            var graph = AssetDatabase.LoadAssetAtPath<DialogGraph>(targetRelPath);
            if (graph == null)
            {
                EditorUtility.DisplayDialog(
                    "Cannot overwrite",
                    "The existing asset at this path is not a DialogGraph.\nImport cancelled.",
                    "OK"
                );
                return;
            }

            // Clear existing data
            if (graph.nodes != null) graph.nodes.Clear();
            if (graph.choiceNodes != null) graph.choiceNodes.Clear();
            if (graph.actionNodes != null) graph.actionNodes.Clear();
            if (graph.links != null) graph.links.Clear();

            // Remove sub-assets (old nodes, etc.)
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(targetRelPath))
            {
                if (sub != graph)
                    AssetDatabase.RemoveObjectFromAsset(sub);
            }

            BuildFromDto(graph, dto);

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = graph;
            ShowNotification(new GUIContent($"Overwrote {Path.GetFileName(targetRelPath)}"));

            if (doDebug)
                Debug.Log($"[DialogJsonIOWindow] Overwrote DialogGraph at: {targetRelPath}");
        }

        /// <summary>
        /// Creates nodes and links from DTO into the given graph.
        /// Simple policy: preserve GUIDs unless they conflict with existing ones,
        /// in which case new GUIDs are generated.
        /// </summary>
        private void BuildFromDto(DialogGraph graph, DialogGraphExport dto)
        {
            if (graph.nodes == null) graph.nodes = new List<DialogNode>();
            if (graph.choiceNodes == null) graph.choiceNodes = new List<ChoiceNode>();
            if (graph.actionNodes == null) graph.actionNodes = new List<DialogSystem.Runtime.Models.Nodes.ActionNode>();
            if (graph.links == null) graph.links = new List<GraphLink>();

            var existing = new HashSet<string>();
            var map = new Dictionary<string, string>();

            // Collect existing GUIDs from graph
            existing.UnionWith(graph.nodes.Where(n => n != null).Select(n => n.GetGuid()));
            existing.UnionWith(graph.choiceNodes.Where(n => n != null).Select(n => n.GetGuid()));
            existing.UnionWith(graph.actionNodes.Where(n => n != null).Select(n => n.GetGuid()));

            // Start / End (editor markers)
            if (dto.startNode != null && !string.IsNullOrEmpty(dto.startNode.guid))
            {
                graph.startGuid = dto.startNode.guid;
                graph.startPosition = new Vector2(dto.startNode.nodePositionX, dto.startNode.nodePositionY);
            }
            else
            {
                graph.startGuid = Guid.NewGuid().ToString("N");
                graph.startPosition = new Vector2(-320f, 80f);
            }

            if (dto.endNode != null && !string.IsNullOrEmpty(dto.endNode.guid))
            {
                graph.endGuid = dto.endNode.guid;
                graph.endPosition = new Vector2(dto.endNode.nodePositionX, dto.endNode.nodePositionY);
            }
            else
            {
                graph.endGuid = Guid.NewGuid().ToString("N");
                graph.endPosition = new Vector2(720f, 80f);
            }

            graph.startInitialized = dto.startNode != null && dto.startNode.isInitialized;
            graph.endInitialized = dto.endNode != null && dto.endNode.isInitialized;

            // Map dialog nodes
            foreach (var d in dto.dialogNodes ?? Enumerable.Empty<DialogExportDialogNode>())
            {
                var src = string.IsNullOrEmpty(d.guid) ? Guid.NewGuid().ToString("N") : d.guid;
                var dst = src;

                if (existing.Contains(dst))
                    dst = Guid.NewGuid().ToString("N");

                map[src] = dst;
                existing.Add(dst);
            }

            // Map choice nodes
            foreach (var c in dto.choiceNodes ?? Enumerable.Empty<DialogExportChoiceNode>())
            {
                var src = string.IsNullOrEmpty(c.guid) ? Guid.NewGuid().ToString("N") : c.guid;
                var dst = src;

                if (existing.Contains(dst))
                    dst = Guid.NewGuid().ToString("N");

                map[src] = dst;
                existing.Add(dst);
            }

            // Map action nodes
            foreach (var a in dto.actionNodes ?? Enumerable.Empty<DialogExportActionNode>())
            {
                var src = string.IsNullOrEmpty(a.guid) ? Guid.NewGuid().ToString("N") : a.guid;
                var dst = src;

                if (existing.Contains(dst))
                    dst = Guid.NewGuid().ToString("N");

                map[src] = dst;
                existing.Add(dst);
            }

            // Create DialogNode sub-assets
            foreach (var d in dto.dialogNodes ?? Enumerable.Empty<DialogExportDialogNode>())
            {
                var so = ScriptableObject.CreateInstance<DialogNode>();

                // original guid might be empty; map covers that
                if (!map.TryGetValue(d.guid, out var mapped))
                    mapped = Guid.NewGuid().ToString("N");

                so.SetGuid(mapped);
                so.name = "Node_" + (string.IsNullOrEmpty(d.title) ? "Untitled" : SafeFile(d.title));
                so.speakerName = d.speaker;
                so.questionText = d.question;
                so.displayTime = d.displayTime;
                so.SetPosition(new Vector2(d.nodePositionX, d.nodePositionY));

                graph.nodes.Add(so);
                AssetDatabase.AddObjectToAsset(so, graph);
            }

            // Create ChoiceNode sub-assets
            foreach (var c in dto.choiceNodes ?? Enumerable.Empty<DialogExportChoiceNode>())
            {
                var so = ScriptableObject.CreateInstance<ChoiceNode>();

                if (!map.TryGetValue(c.guid, out var mapped))
                    mapped = Guid.NewGuid().ToString("N");

                so.SetGuid(mapped);
                so.name = "ChoiceNode";
                so.text = c.text;
                so.SetPosition(new Vector2(c.nodePositionX, c.nodePositionY));

                so.choices = new List<Choice>();
                foreach (var ch in c.choices ?? Enumerable.Empty<ExportChoice>())
                {
                    so.choices.Add(new Choice
                    {
                        answerText = ch.answerText,
                        nextNodeGUID = null // authoritative links created below
                    });
                }

                graph.choiceNodes.Add(so);
                AssetDatabase.AddObjectToAsset(so, graph);
            }

            // Create ActionNode sub-assets
            foreach (var a in dto.actionNodes ?? Enumerable.Empty<DialogExportActionNode>())
            {
                var so = ScriptableObject.CreateInstance<DialogSystem.Runtime.Models.Nodes.ActionNode>();

                if (!map.TryGetValue(a.guid, out var mapped))
                    mapped = Guid.NewGuid().ToString("N");

                so.SetGuid(mapped);
                so.name = "ActionNode";
                so.actionId = a.actionId;
                so.payloadJson = a.payloadJson;
                so.waitForCompletion = a.waitForCompletion;
                so.waitSeconds = a.waitSeconds;
                so.SetPosition(new Vector2(a.nodePositionX, a.nodePositionY));

                graph.actionNodes.Add(so);
                AssetDatabase.AddObjectToAsset(so, graph);
            }

            // Build links buffer (JSON links primary; fallback to embedded choice links)
            var linkBuffer = new List<ExportLink>();
            if (dto.links != null && dto.links.Count > 0)
            {
                linkBuffer.AddRange(dto.links);
            }
            else
            {
                int idx;
                foreach (var c in dto.choiceNodes ?? Enumerable.Empty<DialogExportChoiceNode>())
                {
                    idx = 0;
                    foreach (var ch in c.choices ?? Enumerable.Empty<ExportChoice>())
                    {
                        if (!string.IsNullOrEmpty(ch.nextNodeGUID))
                        {
                            linkBuffer.Add(new ExportLink
                            {
                                fromGuid = c.guid,
                                toGuid = ch.nextNodeGUID,
                                fromPortIndex = idx
                            });
                        }
                        idx++;
                    }
                }
            }

            // Map & add links
            foreach (var l in linkBuffer)
            {
                if (string.IsNullOrEmpty(l.fromGuid) || string.IsNullOrEmpty(l.toGuid))
                    continue;

                if (!map.TryGetValue(l.fromGuid, out var fromMapped))
                    continue;
                if (!map.TryGetValue(l.toGuid, out var toMapped))
                    continue;

                graph.links.Add(new GraphLink
                {
                    fromGuid = fromMapped,
                    toGuid = toMapped,
                    fromPortIndex = l.fromPortIndex
                });
            }
        }
        #endregion

        #region ---------------- JSON Load/Validate ----------------
        private void LoadJsonForPreview()
        {
            _jsonError = "";
            _previewDto = null;

            try
            {
                string json = null;
                if (_importJsonAsset != null)
                {
                    _importExternalPath = "";
                    json = _importJsonAsset.text;
                }
                else if (!string.IsNullOrEmpty(_importExternalPath) && File.Exists(_importExternalPath))
                {
                    json = File.ReadAllText(_importExternalPath, Encoding.UTF8);
                }
                else
                {
                    EditorUtility.DisplayDialog("No Source", "Assign a TextAsset or choose an external JSON file.", "OK");
                    return;
                }

                _loadedJson = json;

                // Validate
                _previewDto = JsonUtility.FromJson<DialogGraphExport>(_loadedJson);
                bool ok = _previewDto != null &&
                          (_previewDto.dialogNodes != null ||
                           _previewDto.choiceNodes != null ||
                           _previewDto.actionNodes != null);

                if (!ok)
                {
                    _previewDto = null;
                    _jsonError = "Invalid or empty JSON for DialogGraphExport.";
                }

                if (doDebug && ok)
                    Debug.Log("[DialogJsonIOWindow] JSON validated successfully for import.");
            }
            catch (Exception ex)
            {
                _previewDto = null;
                _jsonError = "JSON parse error: " + ex.Message;
            }
        }
        #endregion

        #region ---------------- Drag & Drop ----------------
        private void DrawDragAndDropArea()
        {
            var evt = Event.current;
            var rect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "Drag & Drop a .json file here to preview/import", EditorStyles.helpBox);

            if (!rect.Contains(evt.mousePosition))
                return;

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                var paths = DragAndDrop.paths;
                if (paths != null && paths.Length > 0 && paths[0].EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        _importJsonAsset = null;
                        _importExternalPath = paths[0];
                        LoadJsonForPreview();
                    }
                    evt.Use();
                }
            }
        }
        #endregion

        #region ---------------- Path Helpers ----------------
        private static bool IsUnderAssets(string absolutePath, out string assetsRelative)
        {
            assetsRelative = null;
            if (string.IsNullOrEmpty(absolutePath))
                return false;

            absolutePath = absolutePath.Replace("\\", "/");
            var assetsAbs = Application.dataPath.Replace("\\", "/");
            if (!absolutePath.StartsWith(assetsAbs, StringComparison.OrdinalIgnoreCase))
                return false;

            assetsRelative = "Assets" + absolutePath.Substring(assetsAbs.Length);
            return true;
        }

        private static string AbsoluteFromAssets(string assetsRelative)
        {
            if (string.IsNullOrEmpty(assetsRelative))
                return null;

            var rel = assetsRelative.Replace("\\", "/");
            if (!rel.StartsWith("Assets/") && rel != "Assets")
                throw new Exception("Path must start with 'Assets/'");

            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", rel)).Replace("\\", "/");
        }

        private static string SafeFile(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Unnamed";

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (var ch in name)
                sb.Append(invalid.Contains(ch) ? '_' : ch);
            return sb.ToString();
        }

        private string GetInitialImportFolder()
        {
            if (!string.IsNullOrEmpty(_importExternalPath))
                return Path.GetDirectoryName(_importExternalPath);
            if (!string.IsNullOrEmpty(_importFolder))
                return AbsoluteFromAssets(_importFolder);
            return Application.dataPath;
        }
        #endregion
    }
}
