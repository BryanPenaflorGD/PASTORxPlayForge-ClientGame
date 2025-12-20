using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Utils;

namespace DialogSystem.EditorTools.View.Elements.Nodes
{
    /// <summary>
    /// GraphView UI for a <see cref="ChoiceNode"/>:
    /// - One input port
    /// - N output ports (one per answer)
    /// - Keeps ScriptableObject asset in sync via GUID and Undo.
    /// </summary>
    public class ChoiceNodeView : BaseNodeView<ChoiceNode>
    {
        #region ---------------- Inspector / Debug ----------------
        [SerializeField] private bool doDebug = false;
        #endregion

        #region ---------------- Layout ----------------
        private const float NODE_WIDTH = 400f;
        private const float PORT_HOLDER_WIDTH = 28f;
        #endregion

        #region ---------------- Data / Graph ----------------
        public string GUID { get; set; }
        public DialogGraphView graphView;
        #endregion

        #region ---------------- UI ----------------
        private VisualElement _answerSection;
        private Button _addAnswerBtn;

        public Port inputPort;
        public readonly List<Port> outputPorts = new();   // index == choice index
        public readonly List<string> answers = new();

        private static StyleSheet _s_uss;
        private bool _suppressAssetSync = false;
        #endregion

        #region ---------------- Asset helpers ----------------
        private static string CombineAssetPath(string folder, string fileWithExt)
            => $"{folder.TrimEnd('/')}/{fileWithExt.TrimStart('/')}";

        private DialogGraph GetAssetSafe()
        {
            if (graphView == null || string.IsNullOrEmpty(graphView.graphId)) return null;
            var path = CombineAssetPath(TextResources.CONVERSATION_FOLDER, $"{graphView.graphId}.asset");
            return AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
        }

        // Resolve the ChoiceNode sub-asset by this view's GUID.
        private ChoiceNode FindSoNode(DialogGraph asset)
        {
            if (asset == null || string.IsNullOrEmpty(GUID)) return null;

            // Prefer the list on the asset if available
            if (asset.choiceNodes != null && asset.choiceNodes.Count > 0)
            {
                var direct = asset.choiceNodes.FirstOrDefault(n => n != null && n.GetGuid() == GUID);
                if (direct != null) return direct;
            }

            // Fallback: scan sub-assets at the same path (defensive)
            var path = AssetDatabase.GetAssetPath(asset);
            var subs = AssetDatabase.LoadAllAssetsAtPath(path);
            return subs.OfType<ChoiceNode>().FirstOrDefault(n => n != null && n.GetGuid() == GUID);
        }

        private void WithAssetNode(string undoLabel, Action<DialogGraph, ChoiceNode> act)
        {
            if (_suppressAssetSync) return;

            var asset = GetAssetSafe();
            if (asset == null) return;

            var soNode = FindSoNode(asset);
            if (soNode == null) return;

            Undo.RecordObject(soNode, undoLabel);
            act(asset, soNode);
            EditorUtility.SetDirty(soNode);
            EditorUtility.SetDirty(asset);
        }
        #endregion

        #region ---------------- Ctor ----------------
        public ChoiceNodeView(string nodeTitle, DialogGraphView graph)
        {
            graphView = graph;
            GUID = Guid.NewGuid().ToString("N");

            if (_s_uss == null)
                _s_uss = Resources.Load<StyleSheet>("USS/NodeViewUSS");

            if (_s_uss != null && !styleSheets.Contains(_s_uss))
                styleSheets.Add(_s_uss);

            AddToClassList("dlg-node");
            AddToClassList("type-choice");

            style.width = NODE_WIDTH;
            title = "Choice Node";

            BuildHeader();
            BuildBody();
            BuildPorts();

            RefreshExpandedState();
            RefreshPorts();

            // Context menu: Duplicate (delegates to GraphView selection duplicator)
            this.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Duplicate", _ =>
                {
                    if (!selected)
                    {
                        graphView?.ClearSelection();
                        graphView?.AddToSelection(this);
                    }
                    graphView?.DuplicateSelectedNodes();
                });
            }));
        }
        #endregion

        #region ---------------- Header ----------------
        private void BuildHeader()
        {
            titleContainer?.AddToClassList("action-header");
            var titleLabel = titleContainer?.Q<Label>();
            if (titleLabel != null)
            {
                titleLabel.style.color = Color.white;
#if UNITY_2021_3_OR_NEWER
                titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
#endif
            }
        }
        #endregion

        #region ---------------- Body ----------------
        private void BuildBody()
        {
            _answerSection = new VisualElement { name = "answers" };
            _answerSection.style.flexDirection = FlexDirection.Column;
            _answerSection.style.marginBottom = 4;

            _addAnswerBtn = new Button(() => AddChoicePort("New Choice", true))
            {
                text = "+ Add Choice"
            };

            mainContainer.Add(_answerSection);
            mainContainer.Add(_addAnswerBtn);
        }
        #endregion

        #region ---------------- Ports ----------------
        private void BuildPorts()
        {
            inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            inputPort.portName = "In";
            inputContainer.Add(inputPort);
        }

        public override void RebuildPorts()
        {
            // Only input is fixed; outputs are created per-answer row.
            inputContainer.Clear();
            BuildPorts();
            RefreshExpandedState();
            RefreshPorts();
        }
        #endregion

        #region ---------------- Public API ----------------
        /// <summary>Populate from a list of saved choices (no asset writes).</summary>
        public void LoadNodeData(IList<Choice> choiceList)
        {
            _suppressAssetSync = true;
            LoadAnswers(choiceList);
            _suppressAssetSync = false;
        }

        public void LoadAnswers(IList<Choice> choiceList)
        {
            answers.Clear();
            outputPorts.Clear();
            _answerSection.Clear();

            if (choiceList != null && choiceList.Count > 0)
            {
                foreach (var c in choiceList)
                    AddChoicePort(c?.answerText ?? string.Empty, syncAsset: false);
            }
            else
            {
                AddChoicePort("New Choice", syncAsset: false);
            }
        }
        #endregion

        #region ---------------- Answers ----------------
        public void AddChoicePort(string answerText, bool syncAsset)
        {
            // Port per answer
            var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            port.AddToClassList("choice-port");
            port.portName = ""; // mapping is by index

            // Row: [TextField][+][×][Port]
            var row = new VisualElement { name = "answer-row" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.overflow = Overflow.Visible;

            var field = new TextField
            {
                value = answerText,
                name = "answer-text",
                isDelayed = false
            };
            field.style.flexGrow = 1;
            field.style.minWidth = 80;

            field.RegisterValueChangedCallback(e =>
            {
                int index = outputPorts.IndexOf(port);
                if (index < 0) return;

                answers[index] = e.newValue;

                WithAssetNode("Edit Answer Text", (asset, soNode) =>
                {
                    if (soNode.choices == null)
                        soNode.choices = new List<Choice>();

                    while (soNode.choices.Count <= index)
                        soNode.choices.Add(new Choice());

                    soNode.choices[index].answerText = e.newValue;
                });

                // Optional: auto-delete empty answers for a cleaner UX
                if (string.IsNullOrWhiteSpace(e.newValue))
                    RemoveChoice(port, row);
            });

            var dupBtn = TinyIconButton("+", "Duplicate", "btn-dup");
            dupBtn.clicked += () => AddChoicePort(field.value, true);

            var delBtn = TinyIconButton("×", "Delete", "btn-del");
            delBtn.clicked += () => RemoveChoice(port, row);

            var portHolder = new VisualElement { name = "port-holder" };
            portHolder.style.width = PORT_HOLDER_WIDTH;
            portHolder.style.overflow = Overflow.Visible;
            portHolder.Add(port);

            row.Add(field);
            row.Add(dupBtn);
            row.Add(delBtn);
            row.Add(portHolder);

            _answerSection.Add(row);
            outputPorts.Add(port);
            answers.Add(answerText);

            if (syncAsset)
            {
                WithAssetNode("Add Answer", (asset, soNode) =>
                {
                    if (soNode.choices == null)
                        soNode.choices = new List<Choice>();

                    soNode.choices.Add(new Choice
                    {
                        answerText = answerText,
                        nextNodeGUID = null
                    });
                });
            }

            if (doDebug)
            {
                Debug.Log($"[ChoiceNodeView] Added choice '{answerText}' at index {outputPorts.Count - 1} (GUID={GUID})");
            }
        }

        private static Button TinyIconButton(string txt, string tip, string extraClass = null)
        {
            var b = new Button { text = txt, tooltip = tip };
            b.AddToClassList("tiny");
            if (!string.IsNullOrEmpty(extraClass)) b.AddToClassList(extraClass);
            return b;
        }

        private void RemoveChoice(Port port, VisualElement row)
        {
            int index = outputPorts.IndexOf(port);
            if (index < 0) return;

            if (doDebug)
            {
                Debug.Log($"[ChoiceNodeView] Removing choice at index {index} (GUID={GUID})");
            }

            // Disconnect existing edge (if any)
            var edge = port.connections.FirstOrDefault();
            if (edge != null)
            {
                edge.input?.Disconnect(edge);
                edge.output?.Disconnect(edge);
                edge.RemoveFromHierarchy();
            }

            WithAssetNode("Delete Answer", (asset, soNode) =>
            {
                if (soNode.choices != null && index < soNode.choices.Count)
                {
                    var nodeGuid = soNode.GetGuid();

                    // Clean up graph links for this port
                    if (asset.links != null)
                    {
                        // Remove links from this answer's port
                        asset.links.RemoveAll(l =>
                            l.fromGuid == nodeGuid &&
                            l.fromPortIndex == index
                        );

                        // Shift indices for answers after this one
                        for (int i = 0; i < asset.links.Count; i++)
                        {
                            var link = asset.links[i];
                            if (link.fromGuid == nodeGuid && link.fromPortIndex > index)
                            {
                                link.fromPortIndex -= 1;
                                asset.links[i] = link;
                            }
                        }
                    }

                    // Clear and remove the choice itself
                    soNode.choices[index].nextNodeGUID = null;
                    soNode.choices.RemoveAt(index);
                }
            });

            // Remove UI state
            outputPorts.RemoveAt(index);
            answers.RemoveAt(index);
            row.RemoveFromHierarchy();
        }
        #endregion

        #region ---------------- Edge helpers (for GraphView) ----------------
        public int GetPortIndex(Port p) => outputPorts.IndexOf(p);

        public void SetNextForPort(Port p, string targetGuid)
        {
            int i = GetPortIndex(p);
            if (i < 0) return;

            WithAssetNode("Link Choice", (asset, soNode) =>
            {
                if (soNode.choices == null)
                    soNode.choices = new List<Choice>();

                while (soNode.choices.Count <= i)
                    soNode.choices.Add(new Choice());

                soNode.choices[i].nextNodeGUID = targetGuid;
            });

            if (doDebug)
            {
                Debug.Log($"[ChoiceNodeView] Set next node for choice {i} to GUID={targetGuid}");
            }
        }

        public void ClearNextForPort(Port p, string targetGuid)
        {
            int i = GetPortIndex(p);
            if (i < 0) return;

            WithAssetNode("Unlink Choice", (asset, soNode) =>
            {
                if (soNode.choices != null && i < soNode.choices.Count &&
                    soNode.choices[i].nextNodeGUID == targetGuid)
                {
                    soNode.choices[i].nextNodeGUID = null;
                }
            });

            if (doDebug)
            {
                Debug.Log($"[ChoiceNodeView] Cleared next node for choice {i} (target was GUID={targetGuid})");
            }
        }
        #endregion
    }
}
