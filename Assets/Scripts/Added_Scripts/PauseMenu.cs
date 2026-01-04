using DialogSystem.Runtime.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video; // Required for VideoPlayer

public class PauseMenu : MonoBehaviour
{
    [Header("Settings")]
    public bool resumeAutoplayOnClose = true;

    [Header("References")]
    public VideoPlayer videoPlayer;

    // --- INTERNAL STATE MEMORY ---
    private bool _lastAutoPlayState;
    private bool _wasVideoPlaying;
    private bool _wasMusicPlaying; // New: Tracks if music was on

    private void Start()
    {
        if (videoPlayer == null)
            videoPlayer = FindObjectOfType<VideoPlayer>();

    }

    public void Pause()
    {
        Time.timeScale = 0f;

        // 1. PAUSE VIDEO (If playing)
        if (videoPlayer != null)
        {
            _wasVideoPlaying = videoPlayer.isPlaying;
            if (_wasVideoPlaying) videoPlayer.Pause();
        }

        // 2. PAUSE MUSIC (If playing)
        // We check the Singleton AudioHandler
        if (AudioActionHandler.Instance != null && AudioActionHandler.Instance.musicSource != null)
        {
            if (AudioActionHandler.Instance.musicSource.isPlaying)
            {
                _wasMusicPlaying = true;
                AudioActionHandler.Instance.musicSource.Pause();
            }
            else
            {
                _wasMusicPlaying = false;
            }
        }

        // 3. PAUSE DIALOG
        if (DialogManager.Instance != null)
        {
            _lastAutoPlayState = DialogManager.Instance.GetAutoPlayState();
            DialogManager.Instance.PauseForHistory();
        }
    }

    public void Resume()
    {
        Time.timeScale = 1f;

        // 1. RESUME VIDEO
        if (videoPlayer != null && _wasVideoPlaying)
        {
            videoPlayer.Play();
        }

        // 2. RESUME MUSIC
        // Use UnPause() so it continues exactly where it left off
        if (_wasMusicPlaying && AudioActionHandler.Instance != null && AudioActionHandler.Instance.musicSource != null)
        {
            AudioActionHandler.Instance.musicSource.UnPause();
        }

        // 3. RESUME DIALOG
        if (DialogManager.Instance != null)
        {
            DialogManager.Instance.ResumeAfterHistory();

            if (resumeAutoplayOnClose && _lastAutoPlayState)
            {
                if (!DialogManager.Instance.GetAutoPlayState())
                {
                    DialogManager.Instance.ToggleAutoPlay();
                }
            }
        }
    }

    public void Selection()
    {
        // IMPORTANT: Always reset time scale before changing scenes, 
        // otherwise the new scene will be frozen!
        Time.timeScale = 1f;

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        AudioActionHandler.Instance.musicSource.Stop();
    }
}