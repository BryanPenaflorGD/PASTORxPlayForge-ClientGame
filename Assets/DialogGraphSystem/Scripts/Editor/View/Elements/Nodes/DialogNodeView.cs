using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Utils;         // TextResources
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;                         // Undo, AssetDatabase, EditorUtility
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

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
        public float videoEndTime;

        // [NEW] Local list to store the visual novel characters
        public List<VNCharacterEntry> sceneCharacters = new List<VNCharacterEntry>();
        #endregion

        #region Graph / UI
        public DialogGraphView graphView;

        private VisualElement _header;
        private Image _avatar;
        private Label _titleLabel;
        private VisualElement _portraitPreview;
        private VisualElement _charactersContainer;

        private TextField _titleField;
        private TextField _speakerField;
        private ObjectField _spriteField;
        private TextField _questionField;
        private FloatField _displayTimeField;
        private ObjectField _audioField;
        private FloatField _videoTimeField;

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


            _videoTimeField = new FloatField("Video Target Time")
            {
                value = 0f // Default to 0, don't use 'data' here!
            };
            _videoTimeField.tooltip = "Wait until video player reaches this second.";
            _videoTimeField.RegisterValueChangedCallback(e =>
            {
                videoEndTime = e.newValue;
                WithAssetNode("Edit Video Time", (_, soNode) => soNode.videoEndTime = videoEndTime);
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
            mainContainer.Add(_videoTimeField);
            mainContainer.Add(_audioField);

            BuildCharacterListUI();
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
            string speaker,
            string question,
            string titleText,
            Sprite sprite,
            AudioClip audioClip,
            float displayTime,
            float videoEnd,
            List<VNCharacterEntry> loadedCharacters)
        {
            speakerName = speaker;
            questionText = question;
            nodeTitle = titleText;
            portraitSprite = sprite;
            dialogueAudio = audioClip;
            displayTimeSeconds = displayTime;
            videoEndTime = videoEnd;

            // [NEW] Load the list
            sceneCharacters = new List<VNCharacterEntry>(loadedCharacters ?? new List<VNCharacterEntry>());
            RefreshCharacterList();

            if (_titleLabel != null) _titleLabel.text = titleText;
            title = titleText;

            if (_speakerField != null) _speakerField.SetValueWithoutNotify(speaker);
            if (_questionField != null) _questionField.SetValueWithoutNotify(question);
            if (_titleField != null) _titleField.SetValueWithoutNotify(titleText);
            if (_spriteField != null) _spriteField.SetValueWithoutNotify(sprite);
            if (_audioField != null) _audioField.SetValueWithoutNotify(audioClip);
            if (_displayTimeField != null) _displayTimeField.SetValueWithoutNotify(displayTime);
            if (_videoTimeField != null) _videoTimeField.SetValueWithoutNotify(videoEnd);

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

        #region Visual Novel Characters List

        private void BuildCharacterListUI()
        {
            // Separator / Header
            var header = new Label("Stage Characters");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 8;
            header.style.marginLeft = 4;
            mainContainer.Add(header);

            // Container for the rows
            _charactersContainer = new VisualElement();
            _charactersContainer.style.marginBottom = 8;
            mainContainer.Add(_charactersContainer);

            // "Add Character" Button
            var addButton = new Button(() =>
            {
                // Add default entry
                sceneCharacters.Add(new VNCharacterEntry
                {
                    characterName = "New Char",
                    position = VNPosition.Center,
                    flipX = false
                });

                SaveCharactersToAsset();
                RefreshCharacterList();
            })
            { text = "+ Add Character" };

            mainContainer.Add(addButton);
        }

        // Inside DialogNodeView.cs

        private void RefreshCharacterList()
        {
            if (_charactersContainer == null) return;
            _charactersContainer.Clear();

            for (int i = 0; i < sceneCharacters.Count; i++)
            {
                int index = i; // Cache index for closures
                var entry = sceneCharacters[i];

                // Create Row Container
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.backgroundColor = new Color(0, 0, 0, 0.2f);
                row.style.marginBottom = 4;
                row.style.paddingBottom = 4;
                row.style.marginLeft = 4;
                row.style.marginRight = 4;
                row.style.marginTop = 4;
                row.style.alignItems = Align.Center;

                // --- LEFT COLUMN: ANIMATOR FIELD ---
                var animField = new ObjectField
                {
                    objectType = typeof(RuntimeAnimatorController),
                    value = entry.animatorController,
                    allowSceneObjects = false
                };
                animField.tooltip = "Drag an Animator Controller here";
                animField.style.width = 100;
                animField.style.height = 65; // Taller for visibility
                animField.RegisterValueChangedCallback(evt =>
                {
                    sceneCharacters[index].animatorController = evt.newValue as RuntimeAnimatorController;
                    SaveCharactersToAsset();
                });

                // --- MIDDLE COLUMN: NAME, ANIM STATE, POS, STATE ---
                var midCol = new VisualElement();
                midCol.style.flexGrow = 2;
                midCol.style.marginLeft = 5;
                midCol.style.marginRight = 5;

                // Name
                var nameField = new TextField { value = entry.characterName, isDelayed = true };
                nameField.tooltip = "Character Name (for reference)";
                nameField.RegisterValueChangedCallback(evt => { sceneCharacters[index].characterName = evt.newValue; SaveCharactersToAsset(); });

                // Anim State Name (String)
                var animNameField = new TextField { value = entry.animationName, isDelayed = true };
                animNameField.tooltip = "Animation State;";
                animNameField.RegisterValueChangedCallback(evt => { sceneCharacters[index].animationName = evt.newValue; SaveCharactersToAsset(); });

                // Position
                var posField = new EnumField(entry.position);
                posField.tooltip = "Screen Position";
                posField.RegisterValueChangedCallback(evt => { sceneCharacters[index].position = (VNPosition)evt.newValue; SaveCharactersToAsset(); });

                // Visual State (Normal/Dimmed)
                var stateField = new EnumField(entry.state);
                stateField.tooltip = "Visual State (Normal, Dimmed, Hidden)";
                stateField.RegisterValueChangedCallback(evt => { sceneCharacters[index].state = (VNCharacterState)evt.newValue; SaveCharactersToAsset(); });

                midCol.Add(nameField);
                midCol.Add(animNameField);
                midCol.Add(posField);
                midCol.Add(stateField);

                // --- RIGHT COLUMN: FLIP & DELETE ---
                var rightCol = new VisualElement();
                rightCol.style.alignItems = Align.Center;
                rightCol.style.justifyContent = Justify.SpaceBetween;

                // 1. FLIP TOGGLE (Restored!)
                var flipToggle = new Toggle("Flip") { value = entry.flipX };
                flipToggle.RegisterValueChangedCallback(evt =>
                {
                    sceneCharacters[index].flipX = evt.newValue;
                    SaveCharactersToAsset();
                });

                // 2. DELETE BUTTON
                var removeBtn = new Button(() =>
                {
                    sceneCharacters.RemoveAt(index);
                    SaveCharactersToAsset();
                    RefreshCharacterList();
                })
                { text = "X" };
                removeBtn.style.color = new Color(1f, 0.4f, 0.4f); // Red
                removeBtn.style.marginTop = 5;

                rightCol.Add(flipToggle);
                rightCol.Add(removeBtn);

                // Add columns to row
                row.Add(animField);
                row.Add(midCol);
                row.Add(rightCol);

                _charactersContainer.Add(row);
            }
        }

        private void SaveCharactersToAsset()
        {
            WithAssetNode("Edit Stage Characters", (_, soNode) =>
            {
                // Create a new list copy to ensure Unity serialization picks it up
                soNode.sceneCharacters = new List<VNCharacterEntry>(sceneCharacters);
            });
        }

        #endregion
    }
}
