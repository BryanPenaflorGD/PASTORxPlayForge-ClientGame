using DialogSystem.EditorTools.Util;
using DialogSystem.EditorTools.View.Elements.Nodes;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.View
{
    /// <summary>
    /// Main GraphView responsible for authoring dialog graphs.
    /// - Owns all node views & edges.
    /// - Synchronizes with a DialogGraph ScriptableObject (via GraphId).
    /// - Handles Undo/Redo, link management, duplication, and save/load.
    /// </summary>
    public class DialogGraphView : GraphView
    {
        #region ---------------- Fields ----------------

        [SerializeField] private bool doDebug = true;

        private MiniMap miniMap;
        private const float MIN_WIDTH = 200f;
        private const float MIN_HEIGHT = 140f;
        private const float MIN_MARGIN = 10f;
        private static readonly Vector2 kDefaultNodeSize = new Vector2(200, 120);

        /// <summary>
        /// Logical id / file name of the graph (without .asset extension).
        /// The editor window sets this before calling SaveGraph/LoadGraph.
        /// </summary>
        public string graphId { get; set; } = "NewDialogGraph";

        #endregion

        #region ---------------- Ctor ----------------

        /// <summary>
        /// Constructs the graph view:
        /// - Configures zoom, dragging, selection.
        /// - Adds grid + minimap.
        /// - Registers context menu for node creation.
        /// - Hooks GraphViewChanged for Undo-aware data sync.
        /// </summary>
        public DialogGraphView()
        {
            name = "Dialog Graph";

            // Core interactions
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // Grid background
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // MiniMap: bottom-right
            miniMap = new MiniMap { anchored = true };
            Add(miniMap);
            this.RegisterCallback<GeometryChangedEvent>(_ => RepositionMiniMap());
            RepositionMiniMap();

            // Context menu: create nodes at mouse position
            this.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.InsertAction(0, "Create Dialog Node", action =>
                {
                    Vector2 mouse = action.eventInfo?.mousePosition ?? Vector2.zero;
                    Vector2 pos = contentViewContainer.WorldToLocal(mouse);
                    _ = CreateDialogNode("New Node", false, pos.x, pos.y);
                });

                evt.menu.InsertAction(1, "Create Choice Node", action =>
                {
                    Vector2 mouse = action.eventInfo?.mousePosition ?? Vector2.zero;
                    Vector2 pos = contentViewContainer.WorldToLocal(mouse);
                    _ = CreateChoiceNode("Choice", false, pos.x, pos.y);
                });

                evt.menu.InsertAction(2, "Create Action Node", action =>
                {
                    Vector2 mouse = action.eventInfo?.mousePosition ?? Vector2.zero;
                    Vector2 pos = contentViewContainer.WorldToLocal(mouse);
                    _ = CreateActionNode("Action", false, pos.x, pos.y);
                });
            }));

            // Global graph change callback (undo-friendly)
            graphViewChanged = OnGraphViewChanged;

            // Always ensure there is a start/end pair in the view
            EnsureStartEndNodes();
        }

        #endregion

        #region ---------------- MiniMap ----------------

        /// <summary>
        /// Repositions the minimap in the bottom-right corner of the GraphView.
        /// </summary>
        private void RepositionMiniMap()
        {
            var w = layout.width;
            var h = layout.height;
            miniMap.SetPosition(new Rect(
                Mathf.Max(MIN_MARGIN, w - MIN_WIDTH - MIN_MARGIN),
                Mathf.Max(MIN_MARGIN, h - MIN_HEIGHT - MIN_MARGIN),
                MIN_WIDTH, MIN_HEIGHT));
        }

        #endregion

        #region ---------------- Node Create ----------------

        /// <summary>
        /// Ensures there is a DialogGraph asset for the current GraphId.
        /// If it does not exist, an empty asset is auto-created under the conversations folder.
        /// </summary>
        private DialogGraph RequireGraphAsset()
        {
            var path = CombineAssetPath(TextResources.CONVERSATION_FOLDER, $"{graphId}.asset");
            var asset = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<DialogGraph>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (doDebug)
                Debug.Log($"[DialogGraphView] Auto-created DialogGraph at: {path}");

            return asset;
        }

        /// <summary>
        /// Creates a new DialogNode:
        /// - Allocates a sub-asset in the DialogGraph with Undo.
        /// - Spawns a DialogNodeView bound to it at the desired position.
        /// </summary>
        public DialogNodeView CreateDialogNode(string nodeName, bool autoPosition = false, float xPos = 0, float yPos = 0)
        {
            var asset = RequireGraphAsset();
            asset.nodes ??= new List<DialogNode>();

            // Decide position
            Vector2 size = new Vector2(220, 170);
            Vector2 position = new Vector2(xPos, yPos);

            if (autoPosition)
            {
                Vector2 viewCenter = contentViewContainer.WorldToLocal(layout.center);
                position = viewCenter - (size * 0.5f);
            }

            // 1) Create data sub-asset
            var data = ScriptableObject.CreateInstance<DialogNode>();
            data.name = "Node_" + nodeName;
            data.SetGuid();
            data.SetPosition(position);

            // 2) Register Undo for creation + graph change
            DialogUndoUtility.RegisterCreatedNode("Create Dialog Node", asset, data);

            // 3) Attach to graph asset
            asset.nodes.Add(data);
            AssetDatabase.AddObjectToAsset(data, asset);
            EditorUtility.SetDirty(asset);

            // 4) Create the view bound to this data
            var view = new DialogNodeView(nodeName, this)
            {
                GUID = data.GetGuid()
            };
            view.SetPosition(new Rect(position, size));
            AddElement(view);

            if (doDebug)
                Debug.Log($"[DialogGraphView] Created DialogNode '{nodeName}' at {position}");

            return view;
        }

        /// <summary>
        /// Creates a new ChoiceNode with one or more answers.
        /// </summary>
        public ChoiceNodeView CreateChoiceNode(string nodeName, bool autoPosition = false, float xPos = 0, float yPos = 0)
        {
            var asset = RequireGraphAsset();
            asset.choiceNodes ??= new List<ChoiceNode>();

            // Decide position
            Vector2 size = new Vector2(260, 220);
            Vector2 position = new Vector2(xPos, yPos);

            if (autoPosition)
            {
                Vector2 viewCenter = contentViewContainer.WorldToLocal(layout.center);
                position = viewCenter - (size * 0.5f);
            }

            // 1) Create data sub-asset
            var data = ScriptableObject.CreateInstance<ChoiceNode>();
            data.name = "ChoiceNode";
            data.SetGuid();
            data.SetPosition(position);

            // 2) Register Undo for creation + graph change
            DialogUndoUtility.RegisterCreatedNode("Create Choice Node", asset, data);

            // 3) Attach to graph asset
            asset.choiceNodes.Add(data);
            AssetDatabase.AddObjectToAsset(data, asset);
            EditorUtility.SetDirty(asset);

            // 4) Create the view bound to this data
            var view = new ChoiceNodeView(nodeName, this)
            {
                GUID = data.GetGuid()
            };

            view.SetPosition(new Rect(position, size));
            view.LoadNodeData(null); // start with one empty row
            AddElement(view);

            if (doDebug)
                Debug.Log($"[DialogGraphView] Created ChoiceNode '{nodeName}' at {position}");

            return view;
        }

        /// <summary>
        /// Creates a new ActionNode view and backing sub-asset.
        /// </summary>
        public ActionNodeView CreateActionNode(string nodeName, bool autoPosition = false, float xPos = 0, float yPos = 0)
        {
            var asset = RequireGraphAsset();
            asset.actionNodes ??= new List<ActionNode>();

            // Decide position
            Vector2 size = new Vector2(240, 170);
            Vector2 position = new Vector2(xPos, yPos);

            if (autoPosition)
            {
                Vector2 viewCenter = contentViewContainer.WorldToLocal(layout.center);
                position = viewCenter - (size * 0.5f);
            }

            // 1) Create data sub-asset
            var data = ScriptableObject.CreateInstance<ActionNode>();
            data.name = "ActionNode";
            data.SetGuid();
            data.SetPosition(position);

            // 2) Register Undo for creation + graph change
            DialogUndoUtility.RegisterCreatedNode("Create Action Node", asset, data);

            // 3) Attach to graph asset
            asset.actionNodes.Add(data);
            AssetDatabase.AddObjectToAsset(data, asset);
            EditorUtility.SetDirty(asset);

            // 4) Create the view bound to this data
            var view = new ActionNodeView(data.GetGuid(), this);
            view.Initialize(data, position, nodeName);
            view.LoadNodeData("", "", false, 0f);
            view.SetPosition(new Rect(position, size));
            AddElement(view);

            if (doDebug)
                Debug.Log($"[DialogGraphView] Created ActionNode '{nodeName}' at {position}");

            return view;
        }

        /// <summary>
        /// Returns an existing StartNode view or creates one at the given position.
        /// </summary>
        private StartNodeView GetOrCreateStartView(string guid, Vector2 pos)
        {
            var existing = nodes.ToList().OfType<StartNodeView>().FirstOrDefault();
            if (existing != null)
            {
                if (!string.IsNullOrEmpty(guid)) existing.GUID = guid;
                var r = existing.GetPosition();
                var size = (r.width <= 0f || r.height <= 0f)
                    ? kDefaultNodeSize
                    : new Vector2(r.width, r.height);
                existing.SetPosition(new Rect(pos, size));
                return existing;
            }

            var view = new StartNodeView(string.IsNullOrEmpty(guid) ? System.Guid.NewGuid().ToString("N") : guid);
            AddElement(view);
            view.SetPosition(new Rect(pos, kDefaultNodeSize));
            return view;
        }

        /// <summary>
        /// Returns an existing EndNode view or creates one at the given position.
        /// </summary>
        private EndNodeView GetOrCreateEndView(string guid, Vector2 pos)
        {
            var existing = nodes.ToList().OfType<EndNodeView>().FirstOrDefault();
            if (existing != null)
            {
                if (!string.IsNullOrEmpty(guid)) existing.GUID = guid;
                var r = existing.GetPosition();
                var size = (r.width <= 0f || r.height <= 0f)
                    ? kDefaultNodeSize
                    : new Vector2(r.width, r.height);
                existing.SetPosition(new Rect(pos, size));
                return existing;
            }

            var view = new EndNodeView(string.IsNullOrEmpty(guid) ? System.Guid.NewGuid().ToString("N") : guid);
            AddElement(view);
            view.SetPosition(new Rect(pos, kDefaultNodeSize));
            return view;
        }

        /// <summary>
        /// Ensures there is exactly one non-deletable Start and End node in the view.
        /// </summary>
        public void EnsureStartEndNodes()
        {
            bool hasStart = nodes.ToList().Any(n => n is StartNodeView);
            bool hasEnd = nodes.ToList().Any(n => n is EndNodeView);

            if (!hasStart)
            {
                var start = GetOrCreateStartView("Start", new Vector2(-320f, 80f));
                start.capabilities &= ~Capabilities.Deletable;
            }
            if (!hasEnd)
            {
                var end = GetOrCreateEndView("End", new Vector2(720f, 80f));
                end.capabilities &= ~Capabilities.Deletable;
            }
        }

        #endregion

        #region ---------------- Graph Change Handling ----------------

        /// <summary>
        /// Central callback for all GraphView structural changes (moves, deletions, link changes).
        /// Applies Undo-aware changes to the DialogGraph asset.
        /// </summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            var asset = LoadGraphAsset(graphId);

            // Never allow deleting Start/End views
            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                change.elementsToRemove = change.elementsToRemove
                    .Where(e => e is not StartNodeView && e is not EndNodeView)
                    .ToList();
            }

            // Node moves → update data positions with a single Undo step
            if (change.movedElements != null && change.movedElements.Count > 0 && asset != null)
            {
                // One undo step for the whole move batch (Dialog / Choice / Action / Start / End)
                DialogUndoUtility.RecordGraph("Move Nodes", asset);

                foreach (var element in change.movedElements)
                {
                    // Dialog node
                    if (element is DialogNodeView dv)
                    {
                        var data = asset.nodes?.FirstOrDefault(n => n != null && n.GetGuid() == dv.GUID);
                        if (data != null)
                        {
                            data.SetPosition(dv.GetPosition().position);
                            EditorUtility.SetDirty(data);
                        }
                        continue;
                    }

                    // Choice node
                    if (element is ChoiceNodeView cv)
                    {
                        var data = asset.choiceNodes?.FirstOrDefault(n => n != null && n.GetGuid() == cv.GUID);
                        if (data != null)
                        {
                            data.SetPosition(cv.GetPosition().position);
                            EditorUtility.SetDirty(data);
                        }
                        continue;
                    }

                    // Action node
                    if (element is ActionNodeView av)
                    {
                        var data = asset.actionNodes?.FirstOrDefault(n => n != null && n.GetGuid() == av.GUID);
                        if (data != null)
                        {
                            data.SetPosition(av.GetPosition().position);
                            EditorUtility.SetDirty(data);
                        }
                        continue;
                    }

                    // START node (no ScriptableObject, stored directly on DialogGraph)
                    if (element is StartNodeView sv)
                    {
                        asset.startPosition = sv.GetPosition().position;
                        asset.startInitialized = true;
                        continue;
                    }

                    // END node (no ScriptableObject, stored directly on DialogGraph)
                    if (element is EndNodeView ev)
                    {
                        asset.endPosition = ev.GetPosition().position;
                        asset.endInitialized = true;
                        continue;
                    }
                }

                EditorUtility.SetDirty(asset);
            }

            // Deletions (nodes or edges)
            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0 && asset != null)
            {
                DialogUndoUtility.RecordGraph("Delete Elements", asset);

                foreach (var element in change.elementsToRemove)
                {
                    if (element is Edge edge)
                    {
                        HandleDeleteEdge(asset, edge);
                        continue;
                    }

                    if (element is DialogNodeView || element is ChoiceNodeView || element is ActionNodeView)
                    {
                        HandleDeleteNode(asset, element);
                        continue;
                    }
                }

                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }

            // Edge create → add/update link + set choice nextNodeGUID
            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0 && asset != null)
            {
                DialogUndoUtility.RecordGraph("Connect Nodes", asset);

                foreach (var e in change.edgesToCreate)
                {
                    var fromGuid = ExtractGuidFromView(e.output?.node as Node);
                    var toGuid = ExtractGuidFromView(e.input?.node as Node);
                    if (string.IsNullOrEmpty(fromGuid) || string.IsNullOrEmpty(toGuid)) continue;

                    int portIdx = GetOutputPortIndex(e.output);

                    // Unique per (fromGuid, portIdx)
                    asset.links.RemoveAll(l => l.fromGuid == fromGuid && l.fromPortIndex == portIdx);
                    asset.links.Add(new GraphLink { fromGuid = fromGuid, toGuid = toGuid, fromPortIndex = portIdx });

                    // If from is a ChoiceNode, update the saved next
                    var cSo = asset.choiceNodes?.FirstOrDefault(c => c != null && c.GetGuid() == fromGuid);
                    if (cSo != null && cSo.choices != null && portIdx >= 0 && portIdx < cSo.choices.Count)
                    {
                        cSo.choices[portIdx].nextNodeGUID = toGuid;
                        EditorUtility.SetDirty(cSo);
                    }
                }

                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                // Update UI mapping in the view too
                foreach (var e in change.edgesToCreate)
                {
                    if (e.output?.node is ChoiceNodeView chv)
                    {
                        var toGuid = ExtractGuidFromView(e.input?.node as Node);
                        chv.SetNextForPort((Port)e.output, toGuid);
                    }
                }
            }

            return change;
        }

        #endregion

        #region ---------------- Port Rules ----------------

        /// <summary>
        /// Prevents connecting a port to itself or to the same node; allows only opposite directions.
        /// </summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(port =>
                startPort != port &&
                startPort.node != port.node &&
                startPort.direction != port.direction).ToList();
        }

        #endregion

        #region ---------------- Utils ----------------

        /// <summary>
        /// Returns true if the graph has no user nodes or edges (only start/end).
        /// Used to warn about overwriting with an empty graph.
        /// </summary>
        public bool IsGraphEmptyForSave()
        {
            var anyNodes = this.nodes != null && this.nodes.ToList().Count > 0;
            var anyEdges = this.edges != null && this.edges.ToList().Count > 0;
            return !(anyNodes || anyEdges);
        }

        /// <summary>
        /// Clears all visual elements from the GraphView and re-adds the start/end nodes.
        /// </summary>
        public void ClearGraph()
        {
            graphElements.ToList().ForEach(RemoveElement);
            EnsureStartEndNodes();
        }

        private static string ExtractGuidFromView(Node nodeView)
        {
            if (nodeView is EndNodeView ev) return ev.GUID;
            if (nodeView is StartNodeView sv) return sv.GUID;
            if (nodeView is DialogNodeView dv) return dv.GUID;
            if (nodeView is ChoiceNodeView cv) return cv.GUID;
            if (nodeView is ActionNodeView av) return av.GUID;
            return string.Empty;
        }

        private static int GetOutputPortIndex(Port output)
        {
            if (output?.node is DialogNodeView) return 0;
            if (output?.node is ChoiceNodeView chv)
                return chv.GetPortIndex(output);
            if (output?.node is StartNodeView) return 0;
            if (output?.node is ActionNodeView) return 0;
            return 0;
        }

        private static string CombineAssetPath(string folder, string fileWithExt)
            => $"{folder.TrimEnd('/')}/{fileWithExt.TrimStart('/')}";

        private DialogGraph LoadGraphAsset(string graphId)
        {
            var path = CombineAssetPath(TextResources.CONVERSATION_FOLDER, $"{graphId}.asset");
            return AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
        }

        /// <summary>
        /// Creates a DialogNodeView from existing data (used in LoadGraph).
        /// Does NOT modify asset.nodes.
        /// </summary>
        private DialogNodeView CreateDialogNodeViewFromData(DialogNode dNode)
        {
            if (dNode == null) return null;

            var title = string.IsNullOrEmpty(dNode.name)
                ? "Node"
                : dNode.name.Replace("Node_", "");

            var view = new DialogNodeView(title, this)
            {
                GUID = dNode.GetGuid()
            };

            var pos = dNode.GetPosition();
            var size = new Vector2(200f, 150f);

            view.SetPosition(new Rect(pos, size));
            view.LoadNodeData(
                dNode.speakerName,
                dNode.questionText,
                title,
                dNode.speakerPortrait,
                dNode.dialogAudio,
                dNode.displayTime
            );

            AddElement(view);
            return view;
        }

        /// <summary>
        /// Creates a ChoiceNodeView from existing data (used in LoadGraph).
        /// Does NOT modify asset.choiceNodes.
        /// </summary>
        private ChoiceNodeView CreateChoiceNodeViewFromData(ChoiceNode chNode)
        {
            if (chNode == null) return null;

            var view = new ChoiceNodeView("Choice", this)
            {
                GUID = chNode.GetGuid()
            };

            var pos = chNode.GetPosition();
            var size = new Vector2(260f, 220f);

            view.SetPosition(new Rect(pos, size));
            view.LoadNodeData(chNode.choices);

            AddElement(view);
            return view;
        }

        /// <summary>
        /// Creates an ActionNodeView from existing data (used in LoadGraph).
        /// Does NOT modify asset.actionNodes.
        /// </summary>
        private ActionNodeView CreateActionNodeViewFromData(ActionNode aNode)
        {
            if (aNode == null) return null;

            var view = new ActionNodeView(aNode.GetGuid(), this);
            var pos = aNode.GetPosition();
            var size = new Vector2(320f, 240f);

            view.Initialize(aNode, pos, "Action");
            view.LoadNodeData(
                aNode.actionId,
                aNode.payloadJson,
                aNode.waitForCompletion,
                aNode.waitSeconds
            );
            view.SetPosition(new Rect(pos, size));

            AddElement(view);
            return view;
        }

        private void HandleDeleteEdge(DialogGraph asset, Edge edge)
        {
            var fromGuid = ExtractGuidFromView(edge.output?.node as Node);
            var toGuid = ExtractGuidFromView(edge.input?.node as Node);
            if (string.IsNullOrEmpty(fromGuid) || string.IsNullOrEmpty(toGuid)) return;

            int portIdx = GetOutputPortIndex(edge.output);

            // Remove the link record
            asset.links.RemoveAll(l => l.fromGuid == fromGuid && l.toGuid == toGuid && l.fromPortIndex == portIdx);

            // If it's a ChoiceNode -> clear its saved next on that port
            var cSo = asset.choiceNodes?.FirstOrDefault(c => c != null && c.GetGuid() == fromGuid);
            if (cSo != null && cSo.choices != null && portIdx >= 0 && portIdx < cSo.choices.Count)
            {
                cSo.choices[portIdx].nextNodeGUID = null;
                EditorUtility.SetDirty(cSo);
            }
        }

        private void HandleDeleteNode(DialogGraph asset, GraphElement element)
        {
            string guid = null;

            if (element is DialogNodeView dView) guid = dView.GUID;
            else if (element is ChoiceNodeView cView) guid = cView.GUID;
            else if (element is ActionNodeView aView) guid = aView.GUID;
            else return;

            if (string.IsNullOrEmpty(guid)) return;

            // 1) remove links to/from this node
            asset.links.RemoveAll(l => l.fromGuid == guid || l.toGuid == guid);

            // 2) purge references from choice nodes (their nextNodeGUID may point to this)
            if (asset.choiceNodes != null)
            {
                foreach (var ch in asset.choiceNodes)
                {
                    if (ch?.choices == null) continue;
                    foreach (var choice in ch.choices)
                        if (choice != null && choice.nextNodeGUID == guid)
                            choice.nextNodeGUID = null;
                }
            }

            // 3) remove the node sub-asset + list entry
            var path = AssetDatabase.GetAssetPath(asset);
            var all = AssetDatabase.LoadAllAssetsAtPath(path);

            // Dialog
            var dSo = asset.nodes?.FirstOrDefault(n => n != null && n.GetGuid() == guid);
            if (dSo != null)
            {
                asset.nodes.Remove(dSo);
                var match = all.FirstOrDefault(x => x == dSo);
                if (match != null) AssetDatabase.RemoveObjectFromAsset(match);
                Undo.DestroyObjectImmediate(dSo);
            }

            // Choice
            var chSo = asset.choiceNodes?.FirstOrDefault(n => n != null && n.GetGuid() == guid);
            if (chSo != null)
            {
                asset.choiceNodes.Remove(chSo);
                var match = all.FirstOrDefault(x => x == chSo);
                if (match != null) AssetDatabase.RemoveObjectFromAsset(match);
                Undo.DestroyObjectImmediate(chSo);
            }

            // Action
            var aSo = asset.actionNodes?.FirstOrDefault(n => n != null && n.GetGuid() == guid);
            if (aSo != null)
            {
                asset.actionNodes.Remove(aSo);
                var match = all.FirstOrDefault(x => x == aSo);
                if (match != null) AssetDatabase.RemoveObjectFromAsset(match);
                Undo.DestroyObjectImmediate(aSo);
            }
        }

        /// <summary>
        /// Centers the GraphView camera on a given node.
        /// Uses a delayed schedule so layout is valid before calling FrameSelection().
        /// </summary>
        private void FocusOnNode(Node node)
        {
            if (node == null) return;

            // Run after layout so FrameSelection can compute a proper rect.
            schedule.Execute(() =>
            {
                if (node.parent == null) // node might have been deleted
                    return;

                ClearSelection();
                AddToSelection(node);

                // Primary: frame the selected node.
                FrameSelection();

                // Fallback: if something went wrong, at least frame the entire graph.
                if (selection == null || selection.Count == 0)
                {
                    FrameAll();
                }

            }).ExecuteLater(1);
        }

        #endregion

        #region ---------------- Duplicate Node Functionality ----------------

        /// <summary>
        /// Duplicates all selected Dialog / Choice / Action nodes, including edges between them.
        /// New nodes are offset diagonally and fully wired up.
        /// </summary>
        public void DuplicateSelectedNodes()
        {
            var dialogNodeOriginals = selection.OfType<DialogNodeView>().ToList();
            var choiceNodeOriginals = selection.OfType<ChoiceNodeView>().ToList();
            var actionNodeOriginals = selection.OfType<ActionNodeView>().ToList();

            if (dialogNodeOriginals.Count == 0 &&
                choiceNodeOriginals.Count == 0 &&
                actionNodeOriginals.Count == 0)
                return;

            var existingEdges = this.edges.ToList().OfType<Edge>().ToList();

            var mapDialogNodes = new Dictionary<DialogNodeView, DialogNodeView>();
            var mapChoiceNodes = new Dictionary<ChoiceNodeView, ChoiceNodeView>();
            var mapActionNodes = new Dictionary<ActionNodeView, ActionNodeView>();

            // Clone dialog nodes
            foreach (var src in dialogNodeOriginals)
            {
                var srcRect = src.GetPosition();
                var pos = srcRect.position + new Vector2(40f, 40f);

                var clone = CreateDialogNode(src.nodeTitle, false, pos.x, pos.y);
                clone.LoadNodeData(
                    src.speakerName,
                    src.questionText,
                    src.nodeTitle,
                    src.portraitSprite,
                    src.dialogueAudio,
                    src.displayTimeSeconds
                );
                clone.SetPosition(new Rect(pos, srcRect.size));
                mapDialogNodes[src] = clone;
            }

            // Clone choice nodes
            foreach (var src in choiceNodeOriginals)
            {
                var srcRect = src.GetPosition();
                var pos = srcRect.position + new Vector2(40f, 40f);

                var clone = CreateChoiceNode("Choice", false, pos.x, pos.y);
                clone.LoadNodeData(null);
                clone.LoadAnswers(src.answers.Select(a => new Choice { answerText = a }).ToList());
                clone.SetPosition(new Rect(pos, srcRect.size));
                mapChoiceNodes[src] = clone;
            }

            // Clone action nodes
            foreach (var src in actionNodeOriginals)
            {
                var srcRect = src.GetPosition();
                var pos = srcRect.position + new Vector2(40f, 40f);

                var clone = CreateActionNode("Action", false, pos.x, pos.y);
                clone.LoadNodeData(src.actionId, src.payloadJson, src.waitForCompletion, src.waitSeconds);
                clone.SetPosition(new Rect(pos, srcRect.size));
                mapActionNodes[src] = clone;
            }

            // Re-create edges between cloned nodes when both endpoints were selected
            foreach (var e in existingEdges)
            {
                var from = e.output?.node as Node;
                var to = e.input?.node as Node;

                Node fromClone = null, toClone = null;

                if (from is DialogNodeView fd && mapDialogNodes.TryGetValue(fd, out var fdc)) fromClone = fdc;
                else if (from is ChoiceNodeView fc && mapChoiceNodes.TryGetValue(fc, out var fcc)) fromClone = fcc;

                if (to is DialogNodeView td && mapDialogNodes.TryGetValue(td, out var tdc)) toClone = tdc;
                else if (to is ChoiceNodeView tc && mapChoiceNodes.TryGetValue(tc, out var tcc)) toClone = tcc;

                if (fromClone == null || toClone == null) continue;

                Port outPort = null, inPort = null;

                if (fromClone is DialogNodeView fdv) outPort = fdv.outputPort;
                else if (fromClone is ChoiceNodeView fcv)
                {
                    int idx = ((ChoiceNodeView)e.output.node).GetPortIndex((Port)e.output);
                    if (idx >= 0 && idx < fcv.outputPorts.Count)
                        outPort = fcv.outputPorts[idx];
                }

                if (toClone is DialogNodeView tdv) inPort = tdv.inputPort;
                else if (toClone is ChoiceNodeView tcv) inPort = tcv.inputPort;

                if (outPort != null && inPort != null)
                {
                    var newEdge = outPort.ConnectTo(inPort);
                    if (newEdge != null) AddElement(newEdge);
                }
            }

            // Update selection to clones
            ClearSelection();
            foreach (var kv in mapDialogNodes) AddToSelection(kv.Value);
            foreach (var kv in mapChoiceNodes) AddToSelection(kv.Value);
        }

        #endregion

        #region ---------------- Save / Load ----------------

        /// <summary>
        /// Writes graph metadata (Start/End positions + links) into the DialogGraph asset.
        /// Node sub-assets are kept in sync live while you edit.
        /// </summary>
        public void SaveGraph(string fileName)
        {
            graphId = fileName;

            string path = CombineAssetPath(TextResources.CONVERSATION_FOLDER, $"{fileName}.asset");
            var asset = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DialogGraph>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.nodes ??= new List<DialogNode>();
            asset.choiceNodes ??= new List<ChoiceNode>();
            asset.actionNodes ??= new List<ActionNode>();
            asset.links ??= new List<GraphLink>();

            // Start / End views
            var startView = nodes.ToList().OfType<StartNodeView>().FirstOrDefault();
            var endView = nodes.ToList().OfType<EndNodeView>().FirstOrDefault();

            if (startView != null)
            {
                asset.startGuid = startView.GUID;
                asset.startPosition = startView.GetPosition().position;
                asset.startInitialized = true;
            }
            else
            {
                if (string.IsNullOrEmpty(asset.startGuid))
                    asset.startGuid = Guid.NewGuid().ToString("N");
                if (!asset.startInitialized)
                    asset.startPosition = new Vector2(-320f, 80f);
            }

            if (endView != null)
            {
                asset.endGuid = endView.GUID;
                asset.endPosition = endView.GetPosition().position;
                asset.endInitialized = true;
            }
            else
            {
                if (string.IsNullOrEmpty(asset.endGuid))
                    asset.endGuid = Guid.NewGuid().ToString("N");
                if (!asset.endInitialized)
                    asset.endPosition = new Vector2(720f, 80f);
            }

            // Rebuild link list from current edges
            asset.links.Clear();

            var edges = this.edges.ToList().OfType<Edge>().ToList();
            foreach (var e in edges)
            {
                var fromGuid = ExtractGuidFromView(e.output?.node as Node);
                var toGuid = ExtractGuidFromView(e.input?.node as Node);
                if (string.IsNullOrEmpty(fromGuid) || string.IsNullOrEmpty(toGuid)) continue;

                int portIdx = GetOutputPortIndex(e.output);
                asset.links.Add(new GraphLink
                {
                    fromGuid = fromGuid,
                    toGuid = toGuid,
                    fromPortIndex = portIdx
                });
            }

            // Wire ChoiceNode nextNodeGUID based on links
            if (asset.choiceNodes != null)
            {
                // Clear previous next GUIDs
                foreach (var c in asset.choiceNodes)
                {
                    if (c?.choices == null) continue;
                    foreach (var ch in c.choices)
                        if (ch != null) ch.nextNodeGUID = null;
                }

                // Reapply based on link table
                foreach (var link in asset.links)
                {
                    var cSo = asset.choiceNodes.FirstOrDefault(c => c != null && c.GetGuid() == link.fromGuid);
                    if (cSo == null || cSo.choices == null) continue;
                    if (link.fromPortIndex < 0 || link.fromPortIndex >= cSo.choices.Count) continue;
                    cSo.choices[link.fromPortIndex].nextNodeGUID = link.toGuid;
                }
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (doDebug)
                Debug.Log($"[DialogGraphView] Saved DialogGraph to '{path}'");
        }

        /// <summary>
        /// Clears the current view and reconstructs all nodes/edges
        /// from the DialogGraph asset on disk, then centers the view on the start node.
        /// </summary>
        public void LoadGraph(string fileName, bool onUndo)
        {
            graphId = fileName;

            string path = CombineAssetPath(TextResources.CONVERSATION_FOLDER, $"{fileName}.asset");
            var asset = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);

            if (asset == null)
            {
                if (doDebug)
                    Debug.LogError("[DialogGraphView] DialogGraph asset not found: " + path);
                return;
            }

            ClearGraph();
            var viewLookup = new Dictionary<string, Node>();

            // Start node
            if (!asset.startInitialized)
            {
                asset.startPosition = new Vector2(-320f, 80f);
                asset.startGuid = System.Guid.NewGuid().ToString("N");
                asset.startInitialized = true;
            }

            var startView = GetOrCreateStartView(asset.startGuid, asset.startPosition);
            asset.startGuid = startView.GUID;
            viewLookup[startView.GUID] = startView;

            // End node
            if (!asset.endInitialized)
            {
                asset.endPosition = new Vector2(720f, 80f);
                asset.endGuid = System.Guid.NewGuid().ToString("N");
                asset.endInitialized = true;
            }

            var endView = GetOrCreateEndView(asset.endGuid, asset.endPosition);
            asset.endGuid = endView.GUID;
            viewLookup[endView.GUID] = endView;

            // Dialog nodes
            if (asset.nodes != null)
            {
                foreach (var dNode in asset.nodes)
                {
                    if (dNode == null) continue;
                    var view = CreateDialogNodeViewFromData(dNode);
                    if (view != null)
                        viewLookup[dNode.GetGuid()] = view;
                }
            }

            // Choice nodes
            if (asset.choiceNodes != null)
            {
                foreach (var chNode in asset.choiceNodes)
                {
                    if (chNode == null) continue;
                    var view = CreateChoiceNodeViewFromData(chNode);
                    if (view != null)
                        viewLookup[chNode.GetGuid()] = view;
                }
            }

            // Action nodes
            if (asset.actionNodes != null)
            {
                foreach (var aNode in asset.actionNodes)
                {
                    if (aNode == null) continue;
                    var view = CreateActionNodeViewFromData(aNode);
                    if (view != null)
                        viewLookup[aNode.GetGuid()] = view;
                }
            }

            // Rebuild edges
            if (asset.links != null)
            {
                foreach (var link in asset.links)
                {
                    if (!viewLookup.TryGetValue(link.fromGuid, out var fromView)) continue;
                    if (!viewLookup.TryGetValue(link.toGuid, out var toView)) continue;

                    Port outPort = null, inPort = null;

                    if (fromView is DialogNodeView fdv) outPort = fdv.outputPort;
                    else if (fromView is ChoiceNodeView fcv)
                    {
                        if (link.fromPortIndex >= 0 && link.fromPortIndex < fcv.outputPorts.Count)
                            outPort = fcv.outputPorts[link.fromPortIndex];
                    }
                    else if (fromView is StartNodeView fsv) outPort = fsv.outputPort;
                    else if (fromView is ActionNodeView fav) outPort = fav.outputPort;

                    if (toView is DialogNodeView tdv) inPort = tdv.inputPort;
                    else if (toView is ChoiceNodeView tcv) inPort = tcv.inputPort;
                    else if (toView is EndNodeView tev) inPort = tev.inputPort;
                    else if (toView is ActionNodeView tav) inPort = tav.inputPort;

                    if (outPort != null && inPort != null)
                    {
                        var edge = outPort.ConnectTo(inPort);
                        if (edge != null) AddElement(edge);
                    }
                }
            }

            EditorUtility.SetDirty(asset);

            if (doDebug)
                Debug.Log($"[DialogGraphView] Loaded DialogGraph from '{path}'");

            // Center the view on the Start node after everything is laid out.
            if(!onUndo)
                FocusOnNode(startView);
        }

        /// <summary>
        /// Clears the current dialog graph (all Dialog / Choice / Action nodes and links),
        /// after a confirmation dialog. The operation is fully Undo-able.
        /// Start and End nodes are kept.
        /// </summary>
        public void ClearGraphWithConfirmation()
        {
            var asset = LoadGraphAsset(graphId);
            if (asset == null)
            {
                if (doDebug)
                    Debug.LogWarning($"[DialogGraphView] Cannot clear graph – asset for '{graphId}' not found.");
                return;
            }

            // Confirmation prompt
            bool confirm = EditorUtility.DisplayDialog(
                "Clear this dialog graph?",
                "This will delete all Dialog, Choice, and Action nodes and all connections in this graph.\n\n" +
                "The Start and End nodes will be kept.\n\n" +
                "You can undo this via Edit → Undo.",
                "Clear Graph",
                "Cancel");

            if (!confirm)
                return;

            // Record Undo for the graph asset (and its hierarchy via your utility)
            DialogUndoUtility.RecordGraph("Clear Graph", asset);

            // Ensure lists exist
            asset.nodes ??= new List<DialogNode>();
            asset.choiceNodes ??= new List<ChoiceNode>();
            asset.actionNodes ??= new List<ActionNode>();
            asset.links ??= new List<GraphLink>();

            // 1) Clear link table
            asset.links.Clear();

            // 2) Destroy node sub-assets with Undo so they can be restored
            if (asset.nodes != null)
            {
                foreach (var n in asset.nodes.ToArray())
                {
                    if (n == null) continue;
                    Undo.DestroyObjectImmediate(n);
                }
                asset.nodes.Clear();
            }

            if (asset.choiceNodes != null)
            {
                foreach (var n in asset.choiceNodes.ToArray())
                {
                    if (n == null) continue;
                    Undo.DestroyObjectImmediate(n);
                }
                asset.choiceNodes.Clear();
            }

            if (asset.actionNodes != null)
            {
                foreach (var n in asset.actionNodes.ToArray())
                {
                    if (n == null) continue;
                    Undo.DestroyObjectImmediate(n);
                }
                asset.actionNodes.Clear();
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            // 3) Clear visual graph elements except Start/End
            var toRemove = graphElements.ToList()
                .Where(e =>
                    e is Edge ||
                    (e is Node n && n is not StartNodeView && n is not EndNodeView))
                .ToList();

            foreach (var ge in toRemove)
                RemoveElement(ge);

            // Ensure Start/End exist (in case something went wrong visually)
            EnsureStartEndNodes();

            // 4) Re-frame the start node for a clean, centered view
            var startView = nodes.ToList().OfType<StartNodeView>().FirstOrDefault();
            FocusOnNode(startView);

            if (doDebug)
                Debug.Log($"[DialogGraphView] Cleared graph '{graphId}' (Undo available).");
        }

        #endregion
    }
}
