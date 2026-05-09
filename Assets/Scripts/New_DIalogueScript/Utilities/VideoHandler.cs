using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using System;

public class VideoHandler : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public RawImage videoDisplayUI;

    private Action onVideoCompleteCallback;

    void Start()
    {
        videoPlayer.loopPointReached += OnVideoEnded;
        videoDisplayUI.gameObject.SetActive(false);
    }

    public void PlayVideo(VideoClip clip, Action onComplete = null)
    {
        onVideoCompleteCallback = onComplete;
        videoDisplayUI.gameObject.SetActive(true);
        videoPlayer.clip = clip;
        videoPlayer.Play();
    }

    void OnVideoEnded(VideoPlayer vp)
    {
        // We do NOT turn off the UI here anymore! We hold the last frame of the video
        // so the DialogueManager can smoothly fade to black over it.
        onVideoCompleteCallback?.Invoke();
        onVideoCompleteCallback = null;
    }

    // New method called by DialogueManager when the screen is safely black
    public void StopAndClearVideo()
    {
        videoPlayer.Stop();
        videoDisplayUI.gameObject.SetActive(false);
    }
}