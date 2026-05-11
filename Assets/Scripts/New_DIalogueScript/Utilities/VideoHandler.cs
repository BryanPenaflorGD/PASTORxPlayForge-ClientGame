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
        videoPlayer.playOnAwake = false;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoPlayer.prepareCompleted += OnVideoPrepared;
    }

    void Start()
    {
        videoPlayer.loopPointReached += OnVideoEnded;
        videoDisplayUI.gameObject.SetActive(false);
    }

    // --- NEW PAUSE METHODS ---
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
        if (clip.audioTrackCount > 0)
        {
            videoPlayer.controlledAudioTrackCount = 1;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetDirectAudioVolume(0, 1f);
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