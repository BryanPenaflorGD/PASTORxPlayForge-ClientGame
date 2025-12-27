using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DialogSystem.Runtime.Core 
{
    public class VideoActionHandler : MonoBehaviour
    {
        [Header("UI References")]
        public RawImage targetDisplay;
        public VideoPlayer videoPlayer;

        [Header("Input Control")]
        [Tooltip("Drag the transparent 'InputBlocker' Image here.")]
        public GameObject inputBlocker; // <--- Drag your InputBlocker Image here!

        private void Awake()
        {
            // Ensure everything is hidden at start
            if (targetDisplay != null) targetDisplay.gameObject.SetActive(false);
            if (inputBlocker != null) inputBlocker.SetActive(false);
        }

        // =========================================================
        // OPTION A: Background Video (Player CAN click/skip text)
        // Use this for ambience (Rain, Clouds, Moving Train)
        // =========================================================
        public void PlayVideoByName(string videoFileName)
        {
            string cleanName = videoFileName?.Trim();
            VideoClip clip = Resources.Load<VideoClip>(cleanName);

            if (clip != null)
            {
                // Background videos usually loop
                videoPlayer.isLooping = true;
                PlayVideo(clip, false); // false = Do NOT block input
            }
            else
            {
                Debug.LogError($"[VideoActionHandler] Video not found: {cleanName}");
            }
        }

        // =========================================================
        // OPTION B: Cutscene Video (BLOCKS player input)
        // Use this for story events (Door Opening, Monster Roaring)
        // =========================================================
        public void PlayVideoBlocking(string videoFileName)
        {
            string cleanName = videoFileName?.Trim();
            VideoClip clip = Resources.Load<VideoClip>(cleanName);

            if (clip != null)
            {
                // 1. Force Loop OFF
                videoPlayer.isLooping = false;

                // 2. LOCK THE DIALOG MANAGER (Stops player from clicking Next)
                if (DialogSystem.Runtime.Core.DialogManager.Instance != null)
                {
                    DialogSystem.Runtime.Core.DialogManager.Instance.isInputLocked = true;
                }

                // 3. Play
                PlayVideo(clip, true);
            }
            else
            {
                Debug.LogError($"[VideoActionHandler] Video not found: {cleanName}");
            }
        }

        // =========================================================
        // Internal Logic
        // =========================================================
        private void PlayVideo(VideoClip clip, bool isBlocking)
        {
            if (targetDisplay == null || videoPlayer == null) return;

            targetDisplay.gameObject.SetActive(true);
            videoPlayer.clip = clip;
            videoPlayer.renderMode = VideoRenderMode.APIOnly;

            // Cleanup old events (prevent bugs if we play 2 videos in a row)
            videoPlayer.loopPointReached -= OnVideoFinished;

            if (isBlocking)
            {
                // If blocking, we need to know when it finishes to unblock
                videoPlayer.loopPointReached += OnVideoFinished;
            }

            videoPlayer.prepareCompleted += (vp) => {
                targetDisplay.texture = vp.texture;
                vp.Play();
            };

            videoPlayer.Prepare();
        }

        // Unlocks the game when the blocking video ends
        private void OnVideoFinished(VideoPlayer vp)
        {
            if (DialogSystem.Runtime.Core.DialogManager.Instance != null)
            {
                DialogSystem.Runtime.Core.DialogManager.Instance.isInputLocked = false;
            }
            if (inputBlocker != null) inputBlocker.SetActive(false);

            // Optional: Hide the video screen when done?
            targetDisplay.gameObject.SetActive(false);

        }

        public void StopVideo()
        {
            if (videoPlayer != null) videoPlayer.Stop();
            if (targetDisplay != null) targetDisplay.gameObject.SetActive(false);
            if (inputBlocker != null) inputBlocker.SetActive(false);
        }
    }
}
