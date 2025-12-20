using System;
using DialogSystem.Runtime.Models.Nodes;
using UnityEditor;                        // Undo, EditorUtility
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.View.Elements.Nodes
{
    /// <summary>
    /// GraphView editor node for <see cref="ActionNode"/>.
    /// - One input, one output.
    /// - Editable ActionId, JSON payload and flow-control settings.
    /// - All changes are Undo-able and persisted to the backing <see cref="ActionNode"/> asset.
    /// </summary>
    public class ActionNodeView : BaseNodeView<ActionNode>
    {
        #region ---------------- Inspector / Debug ----------------
        [SerializeField] private bool doDebug = true;
        #endregion

        #region ---------------- Ports & UI ----------------
        /// <summary>Input port (can receive multiple connections).</summary>
        public Port inputPort { get; private set; }

        /// <summary>Output port (single connection).</summary>
        public Port outputPort { get; private set; }

        private DialogGraphView _graphView;
        private TextField _actionIdField;
        private TextField _payloadField;
        private Toggle _waitToggle;
        private FloatField _waitSecondsField;
        #endregion

        #region ---------------- Data Mirror ----------------
        /// <summary>GUID of the backing <see cref="ActionNode"/> ScriptableObject.</summary>
        public string GUID { get; set; }

        public string actionId => _actionIdField?.value ?? string.Empty;
        public string payloadJson => _payloadField?.value ?? string.Empty;
        public bool waitForCompletion => _waitToggle != null && _waitToggle.value;
        public float waitSeconds => _waitSecondsField != null ? _waitSecondsField.value : 0f;
        #endregion

        #region ---------------- Ctor ----------------
        /// <summary>
        /// Creates a new Action node view for the given data GUID.
        /// The actual <see cref="ActionNode"/> is assigned via <see cref="BaseNodeView{T}.Initialize"/>.
        /// </summary>
        public ActionNodeView(string guid, DialogGraphView graphView)
        {
            GUID = guid;
            title = "Action";
            this._graphView = graphView;

            AddToClassList("dlg-node");
            AddToClassList("type-action");

            style.minWidth = 280f;
            style.minHeight = 170f;

            BuildHeader();
            BuildBody();
            RebuildPorts();

            RefreshExpandedState();
            RefreshPorts();

            // Context menu: Duplicate (delegates to GraphView's DuplicateSelectedNodes)
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
            this._graphView = graphView;
        }
        #endregion

        #region ---------------- Header ----------------
        /// <summary>Builds the header strip (title styling).</summary>
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
        /// <summary>Builds the main inspector UI for the node.</summary>
        private void BuildBody()
        {
            var content = mainContainer;

            // ---------- Action & Payload section ----------
            var sectionAction = new VisualElement();
            sectionAction.AddToClassList("section");
            content.Add(sectionAction);

            var hdrAction = new Label("Action & Payload");
            hdrAction.AddToClassList("section-title");
            sectionAction.Add(hdrAction);

            // (small helper / hint text)
            var hint = new Label("Use Action ID to map to your runtime handler. Payload is optional JSON.");
            hint.AddToClassList("section-hint");
            hint.style.fontSize = 10;
            hint.style.opacity = 0.75f;
            hint.style.marginBottom = 4;
            sectionAction.Add(hint);

            // --- Action ID ---
            _actionIdField = new TextField("Action ID")
            {
                tooltip = "Identifier for your runtime action (must match your handler/binding).",
                isDelayed = true
            };
            _actionIdField.RegisterValueChangedCallback(e =>
            {
                if (data == null) return;

                Undo.RecordObject(data, "Edit Action ID");
                data.actionId = e.newValue ?? string.Empty;
                MarkDirty(data);

                if (doDebug)
                    Debug.Log($"[ActionNodeView] ({GUID}) ActionId changed to '{data.actionId}'");
            });
            sectionAction.Add(_actionIdField);

            // --- JSON Payload (large code-style box) ---
            _payloadField = new TextField("Payload (JSON)")
            {
                multiline = true,
                tooltip = "Optional JSON payload consumed by your runtime action.",
                isDelayed = true
            };
            _payloadField.AddToClassList("json-box");          // let USS give it a 'code' look
            _payloadField.style.minHeight = 120f;              // bigger editing area
            _payloadField.style.maxHeight = 260f;
            _payloadField.style.whiteSpace = WhiteSpace.Normal;
            _payloadField.RegisterValueChangedCallback(e =>
            {
                if (data == null) return;

                Undo.RecordObject(data, "Edit Action Payload");
                data.payloadJson = e.newValue ?? string.Empty;
                MarkDirty(data);

                if (doDebug)
                    Debug.Log($"[ActionNodeView] ({GUID}) Payload changed.");
            });
            sectionAction.Add(_payloadField);

            // Optional mini-hint for JSON formatting
            var jsonHint = new Label("Tip: Store structured data here (JSON). Example: { \"type\": \"Heal\", \"amount\": 10 }");
            jsonHint.AddToClassList("section-hint");
            jsonHint.style.fontSize = 9;
            jsonHint.style.opacity = 0.7f;
            jsonHint.style.marginTop = 2;
            sectionAction.Add(jsonHint);

            // Separator
            var sep = new VisualElement();
            sep.AddToClassList("separator");
            content.Add(sep);

            // ---------- Flow Control section ----------
            var sectionFlow = new VisualElement();
            sectionFlow.AddToClassList("section");
            content.Add(sectionFlow);

            var hdrFlow = new Label("Flow Control");
            hdrFlow.AddToClassList("section-title");
            sectionFlow.Add(hdrFlow);

            // Wait toggle
            _waitToggle = new Toggle("Wait For Completion")
            {
                value = false,
                tooltip = "If enabled, the conversation waits until your action finishes."
            };
            _waitToggle.RegisterValueChangedCallback(e =>
            {
                if (data == null) return;

                Undo.RecordObject(data, "Toggle Wait For Completion");
                data.waitForCompletion = e.newValue;
                MarkDirty(data);

                if (doDebug)
                    Debug.Log($"[ActionNodeView] ({GUID}) WaitForCompletion = {data.waitForCompletion}");
            });
            sectionFlow.Add(_waitToggle);

            // Wait seconds
            _waitSecondsField = new FloatField("Delay (sec)")
            {
                value = 0f,
                tooltip = "Optional delay before continuing. Use 0 for none."
            };
            _waitSecondsField.RegisterValueChangedCallback(e =>
            {
                if (data == null) return;

                Undo.RecordObject(data, "Edit Action Delay");
                data.waitSeconds = e.newValue;
                MarkDirty(data);

                if (doDebug)
                    Debug.Log($"[ActionNodeView] ({GUID}) WaitSeconds = {data.waitSeconds}");
            });
            sectionFlow.Add(_waitSecondsField);
        }
        #endregion

        #region ---------------- Ports ----------------
        /// <summary>
        /// Rebuilds input/output ports. Called on construction and when the node is rebuilt.
        /// </summary>
        public override void RebuildPorts()
        {
            inputContainer.Clear();
            outputContainer.Clear();

            inputPort = Port.Create<Edge>(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Multi,
                typeof(float));
            inputPort.portName = "In";
            inputContainer.Add(inputPort);

            outputPort = Port.Create<Edge>(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Single,
                typeof(float));
            outputPort.portName = "Out";
            outputContainer.Add(outputPort);
        }
        #endregion

        #region ---------------- Load ----------------
        /// <summary>
        /// Populate UI from existing <see cref="ActionNode"/> data without firing change events.
        /// </summary>
        public void LoadNodeData(string actionId, string payload, bool waitForCompletion, float waitSeconds)
        {
            _actionIdField?.SetValueWithoutNotify(actionId ?? string.Empty);
            _payloadField?.SetValueWithoutNotify(payload ?? string.Empty);
            _waitToggle?.SetValueWithoutNotify(waitForCompletion);
            _waitSecondsField?.SetValueWithoutNotify(waitSeconds);
        }
        #endregion
    }
}
