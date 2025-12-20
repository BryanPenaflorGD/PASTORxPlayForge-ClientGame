using System;
using System.Linq;
using UnityEditor;                         // Undo, AssetDatabase, EditorUtility
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Utils;         // TextResources

namespace DialogSystem.EditorTools.View.Elements.Nodes
{
    /// <summary>
    /// GraphView UI for a <see cref="DialogNode"/>.
    /// - 1 input, 1 output.
    /// - Edits title/speaker/portrait/text/audio/displayTime.
    /// - Persists changes to the backing ScriptableObject (via GUID) with Undo support.
    /// </summary>
    public class DialogNodeView : BaseNodeView<DialogNode>
    {
        #region ---------------- Debug ----------------
        [SerializeField] private bool doDebug = true;
        #endregion

        #region Layout
        private const float NODE_WIDTH = 400f;
        private const float PORT_HOLDER_WIDTH = 28f;
        #endregion

        #region Data
        public string GUID { get; set; }
        public string speakerName;
        public string questionText;
        public string nodeTitle;
        public Sprite portraitSprite;
        public AudioClip dialogueAudio;
        public float displayTimeSeconds;
        #endregion

        #region Graph / UI
        public DialogGraphView graphView;

        private VisualElement _header;
        private Image _avatar;
        private Label _titleLabel;
        private VisualElement _portraitPreview;

        private TextField _titleField;
        private TextField _speakerField;
        private ObjectField _spriteField;
        private TextField _questionField;
        private FloatField _displayTimeField;
        private ObjectField _audioField;

        public Port inputPort;
        public Port outputPort;

        private static StyleSheet s_uss;
        #endregion

        #region Asset helpers

        private static string CombineAssetPath(string folder, string fileWithExt)
            => $"{folder.TrimEnd('/')}/{fileWithExt.TrimStart('/')}";

        /// <summary>
        /// Loads the DialogGraph asset using the graphView.GraphId.
        /// </summary>
        private DialogGraph GetAssetSafe()
        {
            if (graphView == null || string.IsNullOrEmpty(graphView.graphId))
                return null;

            var path = CombineAssetPath(TextResources.CONVERSATION_FOLDER, $"{graphView.graphId}.asset");
            return AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
        }

        /// <summary>
        /// Finds the ScriptableObject DialogNode by GUID inside the given asset.
        /// </summary>
        private DialogNode FindSoNode(DialogGraph asset)
        {
            if (asset == null || string.IsNullOrEmpty(GUID))
                return null;

            return asset.nodes.FirstOrDefault(n => n != null && n.GetGuid() == GUID);
        }

        /// <summary>
        /// Convenience helper: locate asset + node and apply an action with Undo.
        /// </summary>
        private void WithAssetNode(string undoLabel, Action<DialogGraph, DialogNode> act)
        {
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

        #region API (setters called by other tools)

        public void SetPortraitSprite(Sprite sprite)
        {
            portraitSprite = sprite;

            if (_spriteField != null)
                _spriteField.SetValueWithoutNotify(sprite);

            UpdatePortraitPreview();
            UpdateAvatarVisual();
        }

        public void SetSpeakerName(string name)
        {
            speakerName = name;

            if (_speakerField != null)
                _speakerField.SetValueWithoutNotify(name);

            UpdatePortraitPreview();
            UpdateAvatarVisual();
        }

        #endregion

        #region Ctor

        public DialogNodeView(string nodeTitle, DialogGraphView graph)
        {
            graphView = graph;
            this.nodeTitle = nodeTitle;
            title = nodeTitle;
            GUID = Guid.NewGuid().ToString("N");

            if (s_uss == null)
                s_uss = Resources.Load<StyleSheet>("USS/NodeViewUSS");
            if (s_uss != null && !styleSheets.Contains(s_uss))
                styleSheets.Add(s_uss);

            AddToClassList("dlg-node");
            AddToClassList("type-dialogue");

            style.width = NODE_WIDTH;

            BuildHeader();
            BuildBody();
            BuildPorts();

            RefreshExpandedState();
            RefreshPorts();

            // Context menu: Duplicate selection (delegates to GraphView)
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

        #region Header

        private void BuildHeader()
        {
            titleContainer?.AddToClassList("action-header");

            _titleLabel = titleContainer?.Q<Label>();
            if (_titleLabel != null)
            {
                _titleLabel.text = nodeTitle;
                _titleLabel.style.color = Color.white;
#if UNITY_2021_3_OR_NEWER
                _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
#endif
            }

            _header = new VisualElement { name = "header" };

            var headRow = new VisualElement();
            headRow.style.flexDirection = FlexDirection.Row;
            headRow.style.alignItems = Align.Center;

            _avatar = new Image { name = "avatar", scaleMode = ScaleMode.ScaleToFit };
            headRow.Add(_avatar);

            _header.Add(headRow);
            titleContainer.Add(_header);

            UpdateAvatarVisual();
        }

        private void UpdateAvatarVisual()
        {
            if (_avatar == null) return;

            if (portraitSprite != null)
            {
                _avatar.image = portraitSprite.texture;
                _avatar.style.display = DisplayStyle.Flex;
            }
            else
            {
                _avatar.image = null;
                _avatar.style.display = DisplayStyle.None;
            }
        }

        #endregion

        #region Body

        private void BuildBody()
        {
            // Node title
            _titleField = new TextField("Node Title")
            {
                value = nodeTitle,
                isDelayed = true        // commit on focus change / Enter
            };
            _titleField.RegisterValueChangedCallback(e =>
            {
                nodeTitle = e.newValue;
                title = e.newValue;
                if (_titleLabel != null) _titleLabel.text = e.newValue;

                WithAssetNode("Edit Node Title", (_, soNode) =>
                {
                    var clean = string.IsNullOrWhiteSpace(nodeTitle) ? "Untitled" : nodeTitle.Trim();
                    soNode.name = "Node_" + clean;
                });
            });

            // Speaker
            _speakerField = new TextField("Speaker")
            {
                value = "",
                isDelayed = true
            };
            _speakerField.RegisterValueChangedCallback(e =>
            {
                speakerName = e.newValue;
                WithAssetNode("Edit Speaker", (_, soNode) => soNode.speakerName = speakerName);
            });

            // Portrait Sprite
            _spriteField = new ObjectField("Portrait")
            {
                objectType = typeof(Sprite),
                allowSceneObjects = false
            };
            _spriteField.RegisterValueChangedCallback(e =>
            {
                portraitSprite = e.newValue as Sprite;
                UpdatePortraitPreview();
                UpdateAvatarVisual();

                WithAssetNode("Change Portrait", (_, soNode) =>
                {
                    soNode.speakerPortrait = portraitSprite;
                });
            });

            // Visual preview next to dialog text
            _portraitPreview = new VisualElement { name = "portrait-preview" };
            _portraitPreview.style.width = 64;
            _portraitPreview.style.height = 64;
            _portraitPreview.style.marginRight = 6;
            _portraitPreview.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);

            // Dialog text
            _questionField = new TextField("Dialog")
            {
                multiline = true,
                isDelayed = true
            };
            _questionField.name = "Dialog";
            _questionField.style.minHeight = 60;
            _questionField.style.maxWidth = NODE_WIDTH - 20;
            _questionField.style.whiteSpace = WhiteSpace.Normal;
            _questionField.RegisterValueChangedCallback(e =>
            {
                questionText = e.newValue;
                WithAssetNode("Edit Dialogue Text", (_, soNode) => soNode.questionText = questionText);
            });

            // Display time
            _displayTimeField = new FloatField("Display Time (sec)")
            {
                value = 0f
            };
            _displayTimeField.RegisterValueChangedCallback(e =>
            {
                displayTimeSeconds = e.newValue;
                WithAssetNode("Edit Display Time", (_, soNode) => soNode.displayTime = displayTimeSeconds);
            });

            // Audio clip
            _audioField = new ObjectField("Audio Clip")
            {
                objectType = typeof(AudioClip),
                allowSceneObjects = false
            };
            _audioField.RegisterValueChangedCallback(e =>
            {
                dialogueAudio = e.newValue as AudioClip;
                WithAssetNode("Change Dialogue Audio", (_, soNode) => soNode.dialogAudio = dialogueAudio);
            });

            // Layout row for portrait preview + dialog text
            var dialogRow = new VisualElement { name = "dialogue-row" };
            dialogRow.style.flexDirection = FlexDirection.Row;
            dialogRow.style.alignItems = Align.FlexStart;

            dialogRow.Add(_portraitPreview);
            dialogRow.Add(_questionField);

            mainContainer.Add(_titleField);
            mainContainer.Add(_speakerField);
            mainContainer.Add(_spriteField);
            mainContainer.Add(dialogRow);
            mainContainer.Add(_displayTimeField);
            mainContainer.Add(_audioField);

            UpdatePortraitPreview();
        }

        #endregion

        #region Ports

        private void BuildPorts()
        {
            inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            inputPort.portName = "In";
            inputContainer.Add(inputPort);

            AddOutputPort();
        }

        public void AddOutputPort()
        {
            outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            outputPort.portName = "Out";
            outputContainer.Add(outputPort);
        }

        public override void RebuildPorts()
        {
            inputContainer.Clear();
            outputContainer.Clear();
            BuildPorts();
            RefreshExpandedState();
            RefreshPorts();
        }

        #endregion

        #region Load / Visual

        /// <summary>
        /// Populate the view from existing DialogNode data (LoadGraph path).
        /// Does not record Undo; Undo is handled by changes after this point.
        /// </summary>
        public void LoadNodeData(
            string speaker, string question, string titleText, Sprite sprite,
            AudioClip audioClip, float displayTime)
        {
            speakerName = speaker;
            questionText = question;
            nodeTitle = titleText;
            portraitSprite = sprite;
            dialogueAudio = audioClip;
            displayTimeSeconds = displayTime;

            if (_titleLabel != null) _titleLabel.text = titleText;
            title = titleText;

            if (_speakerField != null) _speakerField.SetValueWithoutNotify(speaker);
            if (_questionField != null) _questionField.SetValueWithoutNotify(question);
            if (_titleField != null) _titleField.SetValueWithoutNotify(titleText);
            if (_spriteField != null) _spriteField.SetValueWithoutNotify(sprite);
            if (_audioField != null) _audioField.SetValueWithoutNotify(audioClip);
            if (_displayTimeField != null) _displayTimeField.SetValueWithoutNotify(displayTime);

            UpdatePortraitPreview();
            UpdateAvatarVisual();
        }

        private void UpdatePortraitPreview()
        {
            if (_portraitPreview == null) return;

            if (portraitSprite != null)
            {
                _portraitPreview.style.backgroundImage = new StyleBackground(portraitSprite);
                _portraitPreview.style.backgroundColor = Color.clear;
            }
            else
            {
                _portraitPreview.style.backgroundImage = null;
                _portraitPreview.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            }
        }

        #endregion

        #region Position persistence

        /// <summary>
        /// We deliberately do NOT write to the ScriptableObject here.
        /// Node movement persistence is handled centrally in DialogGraphView.OnGraphViewChanged
        /// so that all selected nodes move as a single Undo step.
        /// </summary>
        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
        }

        #endregion
    }
}
