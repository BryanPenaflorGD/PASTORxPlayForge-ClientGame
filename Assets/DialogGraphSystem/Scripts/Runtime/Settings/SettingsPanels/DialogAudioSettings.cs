using UnityEngine;

namespace DialogSystem.Runtime.Settings.Panels
{
    /// <summary>
    /// Voice/SFX volumes, UI SFX clips, and stop/fade behavior when skipping.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogAudioSettings", menuName = "Dialog System/Settings/Audio Settings")]
    public class DialogAudioSettings : ScriptableObject
    {
        #region ---------------- Inspector ----------------
        [Header("Volumes")]
        [Range(0f, 1f)] public float voiceVolume = 0.85f;
        [Range(0f, 1f)] public float sfxVolume = 0.90f;

        [Header("UI SFX")]
        public bool enableUiSfx = true;
        public AudioClip sfxNavigate;
        public AudioClip sfxConfirm;
        public AudioClip sfxSkip;

        [Header("Stop & Fade Behaviour")]
        [Tooltip("If the player skips a typing line, stop any playing line audio.")]
        public bool stopOnSkipLine = true;

        [Tooltip("If the player skips the entire conversation, stop audio immediately.")]
        public bool stopOnSkipAll = true;

        [Tooltip("Fade out audio when stopping instead of cutting instantly.")]
        public bool fadeOutOnStop = true;

        [Tooltip("Fade-out time in seconds when stopping audio with fade.")]
        [Range(0f, 1f)] public float fadeOutTime = 0.08f;
        #endregion
    }
}
