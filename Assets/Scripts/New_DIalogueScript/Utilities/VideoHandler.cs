using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using System;

public class VideoHandler : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public RawImage videoDisplayUI;

    [Header("Audio Routing")]
    public AudioSource videoAudioSource;

    private Action onVideoCompleteCallback;
    private Action onVideoPreparedCallback;
    private bool isPausedInternally = false;

    void Awake()
    {
        videoPlayer.playOnAwake = false;
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
        if (videoPlayer.isPlaying)
        {
            isPausedInternally = true;
            videoPlayer.Pause();
            if (videoAudioSource != null) videoAudioSource.Pause();
        }
    }

    public void ResumeVideo()
    {
        // Only resume if the video was intentionally paused during playback
        if (isPausedInternally && videoPlayer.isPrepared)
        {
            videoPlayer.Play();
            if (videoAudioSource != null) videoAudioSource.UnPause();
            isPausedInternally = false;
        }
    }

    public void PrepareVideo(VideoClip clip)
    {
        if (clip == null || videoPlayer.clip == clip) return;

        videoPlayer.clip = clip;
        if (clip.audioTrackCount > 0)
        {
            videoPlayer.controlledAudioTrackCount = 1;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, videoAudioSource);
        }
        videoPlayer.Prepare();
    }

    public void PlayVideo(VideoClip clip, Action onStart, Action onComplete)
    {
        onVideoCompleteCallback = onComplete;
        videoDisplayUI.gameObject.SetActive(true);
        isPausedInternally = false;

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
        isPausedInternally = false;
        onVideoCompleteCallback?.Invoke();
        onVideoCompleteCallback = null;
    }

    public void StopAndClearVideo()
    {
        videoPlayer.Stop();
        isPausedInternally = false;
        if (videoAudioSource != null) videoAudioSource.Stop();
        videoDisplayUI.gameObject.SetActive(false);
    }
}