using UnityEngine;

namespace DialogSystem.Runtime.Settings.Panels
{
    [CreateAssetMenu(fileName = "DialogChoiceSettings", menuName = "Dialog System/Choice Settings")]
    public class DialogChoiceSettings : ScriptableObject
    {
        [SerializeField] private bool doDebug = true;

        #region Navigation
        [Header("Navigation")]
        public bool wrapNavigation = true;
        [Min(0.01f)] public float holdRepeatDelay = 0.15f;
        public bool selectFirstOnEnable = true;
        public bool mouseHoverMovesSelection = true;
        #endregion

        #region Visuals
        [Header("Visuals")]
        public Color selectedOutlineColor = Color.white;
        [Range(0.5f, 10f)] public float outlineThickness = 1.0f;
        public bool animateSelected = true;
        [Range(1.0f, 1.2f)] public float animatePulseScale = 1.06f;
        [Range(0.25f, 3f)] public float animatePulseSpeed = 1.0f;
        #endregion

        #region Hints & Confirm
        [Header("Hints & Confirm")]
        public bool showKeyHints = true;

        [Tooltip("Allow confirming with a specific keyboard letter (e.g., F).")]
        public bool enableKeyboardConfirmKey = true;

        [Tooltip("Single letter used to confirm the highlighted choice (first char is used).")]
        public string keyboardConfirmLetter = "F";

        public bool alsoAcceptSubmit = true;
        public bool acceptGamepadConfirm = true;
        public bool acceptXRSelect = true;

        // Legacy/compat (kept for serialized backwards-compat)
        [HideInInspector] public string keyboardConfirm = "";
        [HideInInspector] public string altConfirm = "";
        [HideInInspector] public string gamepadConfirm = "South";
        [HideInInspector] public string mouseHint = "Click";
        #endregion

        private void OnValidate()
        {
            // Normalize: exactly one letter A–Z. Fallback to 'F'
            var v = (keyboardConfirmLetter ?? "").Trim();
            if (v.Length > 0) v = v.Substring(0, 1);
            char c = v.Length == 1 ? char.ToUpperInvariant(v[0]) : 'F';
            if (c < 'A' || c > 'Z') c = 'F';
            keyboardConfirmLetter = c.ToString();
        }
    }
}
