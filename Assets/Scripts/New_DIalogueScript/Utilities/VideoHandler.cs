using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using System;

public class VideoHandler : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public RawImage videoDisplayUI;

    private Action onVideoCompleteCallback;
    private Action onVideoPreparedCallback;

    void Awake()
    {
        // 1. THE SKIPPING FIX: Prevent Unity from auto-playing the video while it buffers in the background!
        videoPlayer.playOnAwake = false;

        // 2. BACK TO DIRECT MODE: Since we pre-buffer now, Direct mode will work perfectly.
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

        // Listen for the exact moment the video finishes buffering
        videoPlayer.prepareCompleted += OnVideoPrepared;
    }

    void Start()
    {
        videoPlayer.loopPointReached += OnVideoEnded;
        videoDisplayUI.gameObject.SetActive(false);
    }

    public void PrepareVideo(VideoClip clip)
    {
        if (clip == null) return;

        // If already prepared, do nothing!
        if (videoPlayer.clip == clip) return;

        videoPlayer.clip = clip;

        // Setup Direct Audio routing before buffering
        if (clip.audioTrackCount > 0)
        {
            videoPlayer.controlledAudioTrackCount = 1;
            videoPlayer.EnableAudioTrack(0, true);

            // Ensure the Direct audio volume is at 100%
            videoPlayer.SetDirectAudioVolume(0, 1f);
        }

        // Start buffering silently in the background
        videoPlayer.Prepare();
    }

    public void PlayVideo(VideoClip clip, Action onStart, Action onComplete)
    {
        onVideoCompleteCallback = onComplete;
        videoDisplayUI.gameObject.SetActive(true);

        if (videoPlayer.clip == clip && videoPlayer.isPrepared)
        {
            // CRITICAL SKIPPING FIX: Force the video to rewind to the very beginning just in case!
            videoPlayer.frame = 0;

            onStart?.Invoke();
            videoPlayer.Play();
        }
        else
        {
            PrepareVideo(clip);

            onVideoPreparedCallback = () => {
                // CRITICAL SKIPPING FIX: Force rewind here too!
                videoPlayer.frame = 0;

                onStart?.Invoke();
                videoPlayer.Play();
            };
        }
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        if (onVideoPreparedCallback != null)
        {
            onVideoPreparedCallback.Invoke();
            onVideoPreparedCallback = null;
        }
    }

    void OnVideoEnded(VideoPlayer vp)
    {
        onVideoCompleteCallback?.Invoke();
        onVideoCompleteCallback = null;
    }

    public void StopAndClearVideo()
    {
        videoPlayer.Stop();
        videoDisplayUI.gameObject.SetActive(false);
    }
}