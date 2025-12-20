using DialogSystem.EditorTools.Settings.Panels;
using DialogSystem.Runtime.Settings.Panels;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static DialogSystem.EditorTools.Settings.DialogSettingsEditorUtils;

namespace DialogSystem.EditorTools.Settings
{
    /// <summary>
    /// Main settings window with side navigation and card-based panels.
    /// </summary>
    public class DialogSettingsEditorWindow : EditorWindow
    {
        #region ---------------- Constants ----------------
        private const string RES_DIR = "Assets/DialogGraphSystem/Resources/DialogSettingsSO";
        private const string MASTER_PATH = RES_DIR + "/DialogSystemSettings.asset";
        private const string USS_PATH = "Assets/DialogGraphSystem/Resources/USS/DialogSettingsStyles.uss";
        #endregion

        #region ---------------- State ----------------
        private DialogSystemSettings _master;
        private SerializedObject _masterSo;

        private VisualElement _header;
        private Label _breadcrumbLabel;
        private VisualElement _body;
        private ScrollView _navScrollView;
        private VisualElement _navRoot;
        private VisualElement _contentRoot;

        private enum Tab { Text, Choices, Input, Audio, Localization, Accessibility, Integrations, About }
        private Tab _activeTab = Tab.Text;
        #endregion

        #region ---------------- Menu ----------------
        [MenuItem("Tools/Dialog System/Settings", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<DialogSettingsEditorWindow>("Dialog Settings");
            w.minSize = new Vector2(800, 600);
            w.Show();
        }
        #endregion

        #region ---------------- Unity ----------------
        private void OnEnable()
        {
            LoadOrCreateMaster();
            BuildUI();
            RefreshContent();
        }
        #endregion

        #region ---------------- Load/Create ----------------
        private void LoadOrCreateMaster()
        {
            _master = AssetDatabase.LoadAssetAtPath<DialogSystemSettings>(MASTER_PATH);

            if (_master == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/DialogGraphSystem/Resources"))
                    AssetDatabase.CreateFolder("Assets/DialogGraphSystem", "Resources");
                if (!AssetDatabase.IsValidFolder("Assets/DialogGraphSystem/Resources/DialogSettingsSO"))
                    AssetDatabase.CreateFolder("Assets/DialogGraphSystem/Resources", "DialogSettingsSO");

                _master = CreateInstance<DialogSystemSettings>();
                AssetDatabase.CreateAsset(_master, MASTER_PATH);

                _master.textSettings = CreateSubAsset<DialogTextSettings>("TextSettings");
                _master.choiceSettings = CreateSubAsset<DialogChoiceSettings>("ChoiceSettings");
                // master.inputSettings = CreateSubAsset<DialogInputSettings>("InputSettings");
                _master.audioSettings = CreateSubAsset<DialogAudioSettings>("AudioSettings");

                EditorUtility.SetDirty(_master);
                AssetDatabase.SaveAssets();
            }
            else
            {
                if (_master.textSettings == null) _master.textSettings = CreateSubAsset<DialogTextSettings>("TextSettings");
                if (_master.choiceSettings == null) _master.choiceSettings = CreateSubAsset<DialogChoiceSettings>("ChoiceSettings");
                if (_master.inputSettings == null) _master.inputSettings = CreateSubAsset<DialogInputSettings>("InputSettings");
                if (_master.audioSettings == null) _master.audioSettings = CreateSubAsset<DialogAudioSettings>("AudioSettings");
                EditorUtility.SetDirty(_master);
                AssetDatabase.SaveAssets();
            }

            _masterSo = new SerializedObject(_master);
        }

        private T CreateSubAsset<T>(string name) where T : ScriptableObject
        {
            var obj = CreateInstance<T>();
            obj.name = name;
            AssetDatabase.AddObjectToAsset(obj, _master);
            return obj;
        }
        #endregion

        #region ---------------- UI ----------------
        private void BuildUI()
        {
            rootVisualElement.Clear();
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(USS_PATH);
            if (styleSheet != null) rootVisualElement.styleSheets.Add(styleSheet);

            rootVisualElement.AddToClassList("dgs-root");

            // ========== HEADER ==========
            _header = BuildHeader();
            rootVisualElement.Add(_header);

            // ========== BODY ==========
            _body = new VisualElement();
            _body.AddToClassList("dgs-body");
            rootVisualElement.Add(_body);

            // ========== NAVIGATION ==========
            var navContainer = BuildNavigation();
            _body.Add(navContainer);

            // ========== CONTENT ==========
            _contentRoot = new VisualElement();
            _contentRoot.AddToClassList("dgs-content");
            _body.Add(_contentRoot);
        }

        private VisualElement BuildHeader()
        {
            var headerContainer = new VisualElement();
            headerContainer.AddToClassList("dgs-header");

            // Left section
            var leftSection = new VisualElement();
            leftSection.AddToClassList("dgs-header-left");

            _breadcrumbLabel = new Label($"Dialog System Settings  ›  {GetTabTitle(_activeTab)}");
            _breadcrumbLabel.AddToClassList("dgs-breadcrumb");
            leftSection.Add(_breadcrumbLabel);

            headerContainer.Add(leftSection);

            // Right section
            var rightSection = new VisualElement();
            rightSection.AddToClassList("dgs-header-right");

            var versionLabel = new Label($"v{_master.version}");
            versionLabel.AddToClassList("dgs-version");
            rightSection.Add(versionLabel);

            headerContainer.Add(rightSection);

            return headerContainer;
        }

        private VisualElement BuildNavigation()
        {
            // Navigation container
            _navRoot = new VisualElement();
            _navRoot.AddToClassList("dgs-nav");

            // ScrollView for navigation
            _navScrollView = new ScrollView(ScrollViewMode.Vertical);
            _navScrollView.AddToClassList("dgs-nav-scroll");
            _navScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _navScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;

            // Navigation title
            var navTitle = new Label("DIALOG SYSTEM");
            navTitle.AddToClassList("dgs-nav-title");
            _navScrollView.Add(navTitle);

            // Core settings
            AddNavButton("Text", Tab.Text, "d_Text Icon");
            AddNavButton("Choices", Tab.Choices, "d_FilterByType");
            AddNavButton("Audio", Tab.Audio, "d_Audio Mixer");
            AddNavButton("Input", Tab.Input, "d_ScaleTool On");

            // Separator
            var separator = new VisualElement();
            separator.AddToClassList("dgs-nav-sep");
            _navScrollView.Add(separator);

            // Extensions title
            var extensionsTitle = new Label("EXTENSIONS");
            extensionsTitle.AddToClassList("dgs-nav-title");
            _navScrollView.Add(extensionsTitle);

            // Extension settings
            AddNavButton("Localization", Tab.Localization, "d_SceneViewOrtho");
            // AddNavButton("Accessibility", Tab.Accessibility, "d_SceneViewOrtho");
            // AddNavButton("Integrations", Tab.Integrations, "d_Favorite");

            // Spacer
            var spacer = new VisualElement();
            spacer.style.height = 6;
            _navScrollView.Add(spacer);

            // About button
            AddNavButton("About", Tab.About, "d__Help");

            _navRoot.Add(_navScrollView);
            return _navRoot;
        }

        private void AddNavButton(string label, Tab tab, string iconName)
        {
            var btn = new Button(() => SwitchTab(tab));
            btn.AddToClassList("dgs-nav-button");
            if (_activeTab == tab) btn.AddToClassList("active");

            // Button content container
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1;

            // Icon
            var icon = Icon(iconName);
            row.Add(icon);

            // Label
            var labelElement = new Label(label);
            labelElement.style.flexGrow = 1;
            row.Add(labelElement);

            btn.Add(row);
            _navScrollView.Add(btn);
        }

        private void SwitchTab(Tab newTab)
        {
            if (_activeTab == newTab) return;

            _activeTab = newTab;
            RefreshContent();
        }

        private void RefreshContent()
        {
            // Update breadcrumb
            _breadcrumbLabel.text = $"Dialog System Settings  ›  {GetTabTitle(_activeTab)}";

            // Update active button state
            foreach (var child in _navScrollView.Children())
            {
                if (child is Button btn)
                {
                    btn.RemoveFromClassList("active");

                    // Check if this button matches the active tab
                    var btnLabel = btn.Q<Label>();
                    if (btnLabel != null && btnLabel.text == GetTabTitle(_activeTab))
                    {
                        btn.AddToClassList("active");
                    }
                }
            }

            // Clear and rebuild content
            _contentRoot.Clear();

            BasePanel panel = _activeTab switch
            {
                Tab.Text => new TextPanel(),
                Tab.Choices => new ChoicePanel(),
                Tab.Audio => new AudioPanel(),
                Tab.Input => new ComingSoonPanel("Input"),// new InputPanel(),
                Tab.Localization => new ComingSoonPanel("Localization"),
                // Tab.Accessibility => new ComingSoonPanel("Accessibility"),
                // Tab.Integrations => new ComingSoonPanel("Integrations"),
                Tab.About => new AboutPanel(),
                _ => new AboutPanel(),
            };

            panel.BuildUI(_masterSo);
            _contentRoot.Add(panel);
        }

        private static string GetTabTitle(Tab t) => t switch
        {
            Tab.Text => "Text",
            Tab.Choices => "Choices",
            Tab.Audio => "Audio",
            Tab.Input => "Input",
            Tab.Localization => "Localization",
            // Tab.Accessibility => "Accessibility",
            // Tab.Integrations => "Integrations",
            Tab.About => "About",
            _ => "Unknown"
        };
        #endregion
    }
}