using System;
using UnityEditor;
using UnityEngine;

namespace IntuitiveCreative
{
    public abstract class IntuitiveCreativeEditorWindow : EditorWindow
    {
        private const string BannerTexturePath = "Assets/IntuitiveCreative/Shared/Editor/Base/Textures/ICBanner.png";
        private static Texture2D bannerTexture;
        private const float HeaderHeight = 44f;
        private const float HeaderTitleYOffset = 4f;
        private const float HeaderSubtitleYOffset = 20f;
        private const float HeaderLeftPadding = 16f;

        protected virtual string HeaderTitle => "Intuitive Creative";
        protected abstract string HeaderSubtitle { get; }
        protected virtual Texture HeaderIcon => IntuitiveCreativeStyles.HeaderIcon;

        protected void DrawHeader()
        {
            Rect headerRect = GUILayoutUtility.GetRect(0f, HeaderHeight, GUILayout.ExpandWidth(true));

            if (bannerTexture == null)
            {
                bannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BannerTexturePath);
            }

            if (bannerTexture != null)
            {
                GUI.DrawTexture(headerRect, bannerTexture, ScaleMode.ScaleAndCrop);
            }
            else
            {
                EditorGUI.DrawRect(headerRect, IntuitiveCreativeStyles.HeaderBackground);
            }

            GUIContent title = new GUIContent(HeaderTitle, HeaderIcon);
            GUI.Label(new Rect(headerRect.x + HeaderLeftPadding, headerRect.y + HeaderTitleYOffset, headerRect.width - HeaderLeftPadding, 22f), title, IntuitiveCreativeStyles.HeaderTitle);
            GUI.Label(new Rect(headerRect.x + HeaderLeftPadding, headerRect.y + HeaderSubtitleYOffset, headerRect.width - HeaderLeftPadding, 18f), HeaderSubtitle, IntuitiveCreativeStyles.HeaderSubtitle);

            Rect dividerRect = new Rect(headerRect.x, headerRect.yMax - 3f, headerRect.width, 3f);
            EditorGUI.DrawRect(dividerRect, Color.black);
        }

        protected void DrawSaveButtons(Action backupAction, Action overwriteAction, Action saveCopyAction, string label = "Save")
        {
            EditorGUILayout.BeginVertical(IntuitiveCreativeStyles.CardStyle);
            EditorGUILayout.LabelField(label, IntuitiveCreativeStyles.SectionHeader);

            EditorGUILayout.BeginHorizontal();
            if (backupAction != null && GUILayout.Button("Backup Original", IntuitiveCreativeStyles.ActionButton))
            {
                backupAction.Invoke();
            }

            if (overwriteAction != null && GUILayout.Button("Overwrite Existing", IntuitiveCreativeStyles.ActionButton))
            {
                overwriteAction.Invoke();
            }

            if (saveCopyAction != null && GUILayout.Button("Save As Copy", IntuitiveCreativeStyles.PrimaryButton))
            {
                saveCopyAction.Invoke();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
    }
}
