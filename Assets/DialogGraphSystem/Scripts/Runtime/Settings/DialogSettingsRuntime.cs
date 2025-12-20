using DialogSystem.Runtime.Settings.Panels;
using UnityEngine;

namespace DialogSystem.Runtime.Settings
{
    public static class DialogSettingsRuntime
    {
        private const string PRIMARY_RES_PATHPrimaryResPath = "DialogSettingsSO/DialogSystemSettings";
        private static DialogSystemSettings master;

        public static DialogSystemSettings Master
        {
            get
            {
                if (master == null)
                {
                    master = Resources.Load<DialogSystemSettings>(PRIMARY_RES_PATHPrimaryResPath);
#if UNITY_EDITOR
                    if (master == null)
                        Debug.LogWarning(
                            "[DialogSettingsRuntime] Could not load DialogSystemSettings at Resources path '" +
                            PRIMARY_RES_PATHPrimaryResPath + "'. Check the asset filename and location.");
#endif
                }
                return master;
            }
        }
        public static bool DoDebug() => Master ? Master.enableDebugLogs : false;
        public static DialogTextSettings Text => Master ? Master.textSettings : null;
        public static DialogChoiceSettings Choice => Master ? Master.choiceSettings : null;
        public static DialogInputSettings Input => Master ? Master.inputSettings : null;
        public static DialogAudioSettings Audio => Master ? Master.audioSettings : null;

    }
}
