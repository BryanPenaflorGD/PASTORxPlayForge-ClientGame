using UnityEngine;

public class AudioHandler : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource voiceSource;
    public AudioSource sfxSource;

    public void PlayVoiceLine(AudioClip clip)
    {
        if (clip == null) return;
        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.Play();
    }

    // --- NEW PAUSE METHODS ---
    public void PauseAudio()
    {
        voiceSource.Pause();
    }

    public void ResumeAudio()
    {
        voiceSource.UnPause();
    }

    public void StopVoiceLine() => voiceSource.Stop();

    public void PlayBlip(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, 0.5f);
    }

    public bool IsVoicePlaying()
    {
        return voiceSource != null && voiceSource.isPlaying;
    }
}