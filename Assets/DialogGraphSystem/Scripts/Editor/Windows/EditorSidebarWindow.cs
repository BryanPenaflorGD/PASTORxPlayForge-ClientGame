using DialogSystem.Runtime.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows
{
    /// <summary>
    /// Sidebar panel with tabs (Characters / Actions).
    /// - Characters: rename + portrait sprite (bulk apply by speaker)
    /// - Actions: edit Action Id / Payload / Wait settings (bulk apply by Action Id)
    /// </summary>
    public class EditorSidbarWindow : VisualElement
    {
        private readonly DialogGraphEditorWindow _owner;

        private enum Tab { Characters, Actions }
        private Tab _currentTab = Tab.Characters;

        // Header
        private Toolbar _headerBar;
        private Button _collapseBtn;
        private Button _rescanBtn;
        private Button _applyBtn;
        private Label _titleLabel;

        // Tab bar + search
        private Toolbar _tabBar;
        private ToolbarToggle _tabCharacters;
        private ToolbarToggle _tabActions;
        private ToolbarSearchField _searchField;

        // Content
        private ScrollView _listView;

        // Data
        private readonly Dictionary<string, CharacterRow> _characterRows = new();
        private readonly Dictionary<string, ActionRow> _actionRows = new();

        public EditorSidbarWindow(DialogGraphEditorWindow owner)
        {
            this._owner = owner;

            // Keep your existing USS hooks so styles continue to apply
            AddToClassList("dlg-char-sidebar");

            BuildHeader();
            BuildTabs();
            BuildList();

            SetTab(Tab.Characters, refresh: true);
        }

        #region ---------- Build UI ----------

        private void BuildHeader()
        {
            _headerBar = new Toolbar();
            _headerBar.AddToClassList("dlg-char-toolbar");

            // Collapse button (icon-only if your ICON_COLLAPSE exists, else shows text)
            _collapseBtn = MakeToolbarButton("⮌", TextResources.ICON_COLLAPSE, "Collapse Sidebar", () => _owner.ToggleSidebar());
            _headerBar.Add(_collapseBtn);

            _titleLabel = new Label("Sidebar");
            _titleLabel.AddToClassList("dlg-char-title");
            _headerBar.Add(_titleLabel);

            _rescanBtn = MakeToolbarButton("Rescan", TextResources.ICON_RESCAN, "Rescan content from nodes", OnClickRescan);
            _rescanBtn.AddToClassList("dlg-char-btn");
            _headerBar.Add(_rescanBtn);

            _applyBtn = MakeToolbarButton("Apply", TextResources.ICON_APPLY, "Apply edits to matching nodes", OnClickApply);
            _applyBtn.AddToClassList("dlg-char-btn");
            _headerBar.Add(_applyBtn);

            Add(_headerBar);
        }

        private void BuildTabs()
        {
            _tabBar = new Toolbar();

            _tabCharacters = new ToolbarToggle { text = "Characters" };
            _tabCharacters.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    _tabActions.SetValueWithoutNotify(false);
                    SetTab(Tab.Characters, refresh: true);
                }
                else if (!_tabActions.value) // keep one selected
                {
                    _tabCharacters.SetValueWithoutNotify(true);
                }
            });
            _tabBar.Add(_tabCharacters);

            _tabActions = new ToolbarToggle { text = "Actions" };
            _tabActions.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    _tabCharacters.SetValueWithoutNotify(false);
                    SetTab(Tab.Actions, refresh: true);
                }
                else if (!_tabCharacters.value) // keep one selected
                {
                    _tabActions.SetValueWithoutNotify(true);
                }
            });
            _tabBar.Add(_tabActions);

            _tabCharacters.SetValueWithoutNotify(true);

            _tabBar.Add(new ToolbarSpacer());

            _searchField = new ToolbarSearchField();
            _searchField.style.flexGrow = 1;
            _searchField.RegisterValueChangedCallback(_ => FilterVisible());
            _tabBar.Add(_searchField);

            Add(_tabBar);
        }

        private void BuildList()
        {
            _listView = new ScrollView(ScrollViewMode.Vertical);
            _listView.AddToClassList("dlg-char-list");
            Add(_listView);
        }

        private Button MakeToolbarButton(string fallbackText, string iconPath, string tooltip, Action onClick)
        {
            var btn = new Button(onClick) { tooltip = tooltip };
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems = Align.Center;

#if UNITY_EDITOR
            Texture2D tex = (!string.IsNullOrEmpty(iconPath)) ? AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath) : null;
            if (tex != null)
            {
                var img = new Image { image = tex, scaleMode = ScaleMode.ScaleToFit };
                img.style.width = 18;
                img.style.height = 18;
                btn.Add(img);
            }
            else
#endif
            {
                btn.text = fallbackText;
            }

            return btn;
        }

        #endregion

        #region ---------- External API ----------

        public void ClearAll()
        {
            _characterRows.Clear();
            _actionRows.Clear();
            _listView?.Clear();
        }

        public void RebuildFromGraph()
        {
            _listView.Clear();

            if (_currentTab == Tab.Characters)
            {
                BuildCharactersFace();
            }
            else
            {
                BuildActionsFace();
            }

            // Apply search filter after building
            FilterVisible();
        }

        #endregion

        #region ---------- Characters face ----------

        private void BuildCharactersFace()
        {
            _characterRows.Clear();

            var names = _owner.CollectSpeakersFromNodes()
                             .Where(n => !string.IsNullOrWhiteSpace(n))
                             .Select(n => n.Trim())
                             .Distinct(StringComparer.Ordinal)
                             .OrderBy(n => n)
                             .ToList();

            foreach (var name in names)
            {
                var firstSprite = _owner.FindFirstSpriteForSpeaker(name);
                var row = new CharacterRow(name, name, firstSprite);
                _characterRows[name] = row;
                _listView.Add(row);
            }
        }

        private class CharacterRow : VisualElement
        {
            public string OriginalName { get; private set; }
            public string CurrentName => nameField.value?.Trim();
            public Sprite Sprite => (Sprite)spriteField.value;

            private readonly TextField nameField;
            private readonly ObjectField spriteField;
            private readonly Image preview;

            public CharacterRow(string originalName, string currentName, Sprite sprite)
            {
                OriginalName = originalName;

                AddToClassList("char-row");

                var header = new VisualElement();
                header.AddToClassList("char-row-header");
                nameField = new TextField("Name") { value = currentName };
                nameField.AddToClassList("char-name-field");
                header.Add(nameField);
                Add(header);

                var body = new VisualElement(); body.AddToClassList("char-row-body");

                preview = new Image { scaleMode = ScaleMode.ScaleToFit };
                preview.AddToClassList("char-preview");
                body.Add(preview);

                var right = new VisualElement(); right.AddToClassList("char-row-right");
                spriteField = new ObjectField("Portrait")
                {
                    objectType = typeof(Sprite),
                    allowSceneObjects = false,
                    value = sprite
                };
                spriteField.AddToClassList("char-sprite-field");
                spriteField.RegisterValueChangedCallback(_ =>
                {
                    UpdatePreview(spriteField.value as Sprite);
                });
                right.Add(spriteField);

                body.Add(right);
                Add(body);

                UpdatePreview(sprite);
            }

            private void UpdatePreview(Sprite s) => preview.image = s ? s.texture : null;
        }

        #endregion

        #region ---------- Actions face ----------

        private void BuildActionsFace()
        {
            _actionRows.Clear();

            var nodes = _owner.CollectActionNodes()
                             .OrderBy(v => string.IsNullOrEmpty(v.actionId) ? "~" : v.actionId, StringComparer.OrdinalIgnoreCase)
                             .ThenBy(v => v.GUID, StringComparer.OrdinalIgnoreCase)
                             .ToList();

            // group by Action Id (bulk edit template per id)
            var groups = nodes.GroupBy(v => v.actionId ?? "", StringComparer.OrdinalIgnoreCase);

            foreach (var g in groups)
            {
                var first = g.First();
                var row = new ActionRow(
                    originalActionId: g.Key,
                    actionId: first.actionId ?? "",
                    payload: first.payloadJson ?? "",
                    wait: first.waitForCompletion,
                    waitSeconds: first.waitSeconds);

                row.OnSelectAll = () =>
                {
                    var gv = _owner.GetGraphView();
                    if (gv == null) return;

                    var matching = nodes.Where(v =>
                        string.Equals(v.actionId ?? "", g.Key, StringComparison.OrdinalIgnoreCase)).Cast<GraphElement>();

                    gv.ClearSelection();
                    bool any = false;
                    foreach (var m in matching) { gv.AddToSelection(m); any = true; }
                    if (any) gv.FrameSelection();
                };

                _actionRows[g.Key] = row;
                _listView.Add(row);
            }
        }

        private class ActionRow : VisualElement
        {
            public string OriginalActionId { get; private set; }

            public string ActionId => idField.value?.Trim();
            public string Payload => payloadField.value ?? "";
            public bool WaitForCompletion => waitToggle.value;
            public float WaitSeconds => waitField.value;

            public Action OnSelectAll;

            private readonly TextField idField;
            private readonly TextField payloadField;
            private readonly Toggle waitToggle;
            private readonly FloatField waitField;

            public ActionRow(string originalActionId, string actionId, string payload, bool wait, float waitSeconds)
            {
                OriginalActionId = originalActionId ?? "";

                AddToClassList("char-row");   // reuse your existing row styling
                AddToClassList("action-row"); // optional hook to style differently

                // Header
                var header = new VisualElement();
                header.AddToClassList("char-row-header");

                idField = new TextField("Action Id") { value = actionId ?? "" };
                idField.AddToClassList("char-name-field");
                header.Add(idField);

                var selectBtn = new Button(() => OnSelectAll?.Invoke()) { text = "Go to" };
                selectBtn.AddToClassList("dlg-btn");
                selectBtn.AddToClassList("secondary");
                header.Add(selectBtn);

                Add(header);

                // Body
                var body = new VisualElement(); body.AddToClassList("char-row-body");

                var right = new VisualElement(); right.AddToClassList("char-row-right");

                payloadField = new TextField("Payload (JSON)") { multiline = true, value = payload ?? "" };
                payloadField.style.minHeight = 80;
                right.Add(payloadField);

                waitToggle = new Toggle("Wait For Completion") { value = wait };
                right.Add(waitToggle);

                waitField = new FloatField("Delay (sec)") { value = waitSeconds };
                right.Add(waitField);

                body.Add(right);
                Add(body);
            }
        }

        #endregion

        #region ---------- Actions ----------

        private void SetTab(Tab tab, bool refresh)
        {
            _currentTab = tab;
            _titleLabel.text = tab == Tab.Characters ? "Characters" : "Actions";

            if (refresh) RebuildFromGraph();
        }

        private void OnClickRescan() => RebuildFromGraph();

        private void OnClickApply()
        {
            if (_currentTab == Tab.Characters)
            {
                var data = ExportCharacterBindings();
                _owner.ApplySpritesToNodes(data);

                if (data.Any(b => !string.Equals(b.originalName?.Trim(), b.currentName?.Trim(), StringComparison.Ordinal)))
                    RebuildFromGraph();
            }
            else
            {
                var data = ExportActionBindings();
                _owner.ApplyActionsToNodes(data);

                if (data.Any(b => !string.Equals(b.originalActionId ?? "", b.actionId ?? "", StringComparison.Ordinal)))
                    RebuildFromGraph();
            }
        }

        private void FilterVisible()
        {
            var q = _searchField?.value ?? string.Empty;
            q = q.Trim();
            bool hasQ = !string.IsNullOrEmpty(q);

            foreach (var child in _listView.Children())
            {
                if (!hasQ)
                {
                    child.style.display = DisplayStyle.Flex;
                    continue;
                }

                string hay = child is CharacterRow cr ? (cr.CurrentName ?? "")
                             : child is ActionRow ar ? ((ar.ActionId ?? "") + "\n" + (ar.Payload ?? ""))
                             : child.name ?? "";

                bool match = hay.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                child.style.display = match ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private List<DialogGraphEditorWindow.CharacterBinding> ExportCharacterBindings()
        {
            var list = new List<DialogGraphEditorWindow.CharacterBinding>(_characterRows.Count);
            foreach (var kv in _characterRows)
            {
                var r = kv.Value;
                list.Add(new DialogGraphEditorWindow.CharacterBinding
                {
                    originalName = r.OriginalName,
                    currentName = r.CurrentName,
                    sprite = r.Sprite,
                });
            }
            return list;
        }

        private List<DialogGraphEditorWindow.ActionBinding> ExportActionBindings()
        {
            var list = new List<DialogGraphEditorWindow.ActionBinding>(_actionRows.Count);
            foreach (var kv in _actionRows)
            {
                var r = kv.Value;
                list.Add(new DialogGraphEditorWindow.ActionBinding
                {
                    originalActionId = r.OriginalActionId,
                    actionId = r.ActionId,
                    payloadJson = r.Payload,
                    waitForCompletion = r.WaitForCompletion,
                    waitSeconds = r.WaitSeconds
                });
            }
            return list;
        }

        #endregion
    }
}
