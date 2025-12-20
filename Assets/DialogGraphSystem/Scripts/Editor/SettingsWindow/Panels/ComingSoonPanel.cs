using DialogSystem.Runtime.Settings.Panels;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using static DialogSystem.EditorTools.Settings.DialogSettingsEditorUtils;

namespace DialogSystem.EditorTools.Settings.Panels
{
    public class ComingSoonPanel : BasePanel
    {
        private readonly string _title;
        public ComingSoonPanel(string title) { this._title = title; }

        public override void BuildUI(SerializedObject masterSo)
        {
            var card = new VisualElement(); card.AddToClassList("dgs-card");
            var h = new Label(_title); h.AddToClassList("dgs-card-title");
            var l = new Label("This section is coming soon. Stay tuned!");
            l.AddToClassList("dgs-coming");
            card.Add(h); card.Add(l);
            Add(card);
        }
    }
}
