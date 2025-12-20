using UnityEngine;

namespace DialogSystem.Runtime.Settings.Panels
{
    /// <summary>
    /// Master settings that references all sub-settings (created as sub-assets).
    /// </summary>
    [CreateAssetMenu(fileName = "DialogSystemSettings", menuName = "Dialog System/Settings/Dialog System Settings (Master)", order = 0)]
    public class DialogSystemSettings : ScriptableObject
    {
        #region ---------------- Inspector ----------------
        [Header("References")]
        public DialogTextSettings textSettings;
        public DialogChoiceSettings choiceSettings;
        public DialogInputSettings inputSettings;
        public DialogAudioSettings audioSettings;

        [Header("Meta / Debug")]
        public string version = "1.3.0";
        public bool enableDebugLogs = false;
        #endregion
    }
}
