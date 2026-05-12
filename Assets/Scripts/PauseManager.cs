using UnityEngine;
using UnityEngine.Video;
using System.Collections.Generic;

public class PauseManager : MonoBehaviour
{
    private List<VideoPlayer> videoPlayers = new List<VideoPlayer>();

    void Start()
    {
        // Register all videos at start
        videoPlayers.AddRange(FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None));
    }

    public void PauseAll()
    {
        foreach (var vp in videoPlayers)
            vp.Pause();
    }

    public void ResumeAll()
    {
        foreach (var vp in videoPlayers)
            vp.Play();
    }

    public void StopAll()
    {
        foreach (var vp in videoPlayers)
            vp.Stop();
    }
}