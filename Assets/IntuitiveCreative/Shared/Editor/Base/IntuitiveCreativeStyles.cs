using UnityEditor;
using UnityEngine;

namespace IntuitiveCreative
{
    public static class IntuitiveCreativeStyles
    {
        public static GUIStyle HeaderTitle { get; private set; }
        public static GUIStyle HeaderSubtitle { get; private set; }
        public static GUIStyle SectionHeader { get; private set; }
        public static GUIStyle CardStyle { get; private set; }
        public static GUIStyle ToolbarStyle { get; private set; }
        public static GUIStyle PrimaryButton { get; private set; }
        public static GUIStyle ActionButton { get; private set; }

        public static Color HeaderBackground { get; private set; }
        public static Color Accent { get; private set; }
        public static Color WaveformBackground { get; private set; }
        public static Color WaveformColor { get; private set; }
        public static Color PlayheadColor { get; private set; }

        public static Texture HeaderIcon { get; private set; }

        private static bool initialized;
        private static bool wasProSkin;

        public static void Ensure()
        {
            if (!initialized || wasProSkin != EditorGUIUtility.isProSkin)
            {
                Setup();
            }
        }

        public static void DrawSeparator()
        {
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(10f));
            rect.height = 1f;
            rect.y += 4f;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.35f));
        }

        private static void Setup()
        {
            wasProSkin = EditorGUIUtility.isProSkin;
            bool isDark = wasProSkin;

            HeaderBackground = isDark ? new Color(0.11f, 0.12f, 0.16f) : new Color(0.9f, 0.92f, 0.96f);
            Accent = isDark ? new Color(0.35f, 0.75f, 0.95f) : new Color(0.15f, 0.45f, 0.7f);
            WaveformBackground = isDark ? new Color(0.08f, 0.09f, 0.12f) : new Color(0.92f, 0.92f, 0.92f);
            WaveformColor = new Color(0.235f, 0.51f, 0.718f);
            PlayheadColor = isDark ? new Color(1f, 0.35f, 0.35f) : new Color(0.85f, 0.1f, 0.1f);

            HeaderTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 15,
                fontStyle = FontStyle.BoldAndItalic,
                normal = { textColor = new Color(0.88f, 0.88f, 0.88f) }
            };

            HeaderSubtitle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.65f, 0.65f, 0.65f) }
            };

            SectionHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.235f, 0.51f, 0.718f) }
            };

            CardStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 10)
            };

            ToolbarStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fixedHeight = 26f,
                fontStyle = FontStyle.Bold
            };

            PrimaryButton = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };

            ActionButton = new GUIStyle(GUI.skin.button);

            HeaderIcon = null;

            initialized = true;
        }
    }
}
