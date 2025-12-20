using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Settings.Panels
{
    /// <summary>
    /// Base class for content panels with built-in scroll support.
    /// </summary>
    public abstract class BasePanel : VisualElement
    {
        #region ---------------- Fields ----------------
        protected ScrollView _scrollView;
        #endregion

        #region ---------------- Init ----------------
        protected BasePanel()
        {
            AddToClassList("dgs-content");

            // Setup the panel layout
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            // Create ScrollView for content
            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.AddToClassList("dgs-content-scroll");
            _scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            _scrollView.style.flexGrow = 1;

            // Add scrollView to the panel
            base.Add(_scrollView);
        }
        #endregion

        #region ---------------- API ----------------
        public abstract void BuildUI(SerializedObject masterSo);
        #endregion

        #region ---------------- Override Add Methods ----------------
        /// <summary>
        /// Override Add to automatically add to ScrollView content instead of root.
        /// </summary>
        public new void Add(VisualElement element)
        {
            // If it's a footer, add it outside the scroll view
            if (element.ClassListContains("dgs-footer"))
            {
                base.Add(element);
            }
            else
            {
                // Everything else goes inside the scroll view
                _scrollView.Add(element);
            }
        }

        /// <summary>
        /// Method to explicitly add to root (outside scroll view).
        /// </summary>
        protected void AddToRoot(VisualElement element)
        {
            base.Add(element);
        }
        #endregion

        #region ---------------- Helpers ----------------
        protected VisualElement FooterSave(System.Action onClick)
        {
            var footer = new VisualElement();
            footer.AddToClassList("dgs-footer");

            var save = new Button(() => onClick?.Invoke()) { text = "SAVE" };
            save.AddToClassList("dgs-save");

            footer.Add(save);
            return footer;
        }
        #endregion
    }
}