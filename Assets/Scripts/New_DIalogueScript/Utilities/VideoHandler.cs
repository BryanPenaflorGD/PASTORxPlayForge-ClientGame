using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using System;

public class VideoHandler : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public RawImage videoDisplayUI;

    [Header("Audio Routing")]
    [Tooltip("Assign an AudioSource that is routed to the SFX Mixer Group")]
    public AudioSource videoAudioSource;

    private Action onVideoCompleteCallback;
    private Action onVideoPreparedCallback;

    void Awake()
    {
        videoPlayer.playOnAwake = false;

        // Ensure the player is set to route to an AudioSource
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.prepareCompleted += OnVideoPrepared;
    }

    void Start()
    {
        videoPlayer.loopPointReached += OnVideoEnded;
        videoDisplayUI.gameObject.SetActive(false);
    }

    public void PauseVideo()
    {
        if (videoPlayer.isPlaying) videoPlayer.Pause();
    }

    public void ResumeVideo()
    {
        if (videoPlayer.isPrepared) videoPlayer.Play();
    }

    public void PrepareVideo(VideoClip clip)
    {
        if (clip == null) return;
        if (videoPlayer.clip == clip) return;

        videoPlayer.clip = clip;

        // --- FIX: Re-bind the AudioSource to the specific clip's track ---
        if (clip.audioTrackCount > 0)
        {
            videoPlayer.controlledAudioTrackCount = 1;
            videoPlayer.EnableAudioTrack(0, true);
            // This line ensures the VideoPlayer sends audio to your specific AudioSource
            videoPlayer.SetTargetAudioSource(0, videoAudioSource);
        }

        videoPlayer.Prepare();
    }

    public void PlayVideo(VideoClip clip, Action onStart, Action onComplete)
    {
        onVideoCompleteCallback = onComplete;
        videoDisplayUI.gameObject.SetActive(true);

        if (videoPlayer.clip == clip && videoPlayer.isPrepared)
        {
            videoPlayer.frame = 0;
            onStart?.Invoke();
            videoPlayer.Play();
        }
        else
        {
            PrepareVideo(clip);
            onVideoPreparedCallback = () => {
                videoPlayer.frame = 0;
                onStart?.Invoke();
                videoPlayer.Play();
            };
        }
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        onVideoPreparedCallback?.Invoke();
        onVideoPreparedCallback = null;
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