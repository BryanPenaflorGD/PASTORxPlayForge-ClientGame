using UnityEngine;

public class AudioHandler : MonoBehaviour
{
    [Header("Audio Sources")]
    [Tooltip("Assign an AudioSource component for Voice Over")]
    public AudioSource voiceSource;
    [Tooltip("Assign an AudioSource component for Sound Effects/Blips")]
    public AudioSource sfxSource;

    public void PlayVoiceLine(AudioClip clip)
    {
        if (clip == null) return;

        voiceSource.Stop(); // Stop the previous voice line if it's still playing
        voiceSource.clip = clip;
        voiceSource.Play();
    }

    public void StopVoiceLine()
    {
        voiceSource.Stop();
    }

    public void PlayBlip(AudioClip clip)
    {
        if (clip == null) return;

        // PlayOneShot allows multiple quick sounds to overlap naturally
        // We lower the volume slightly so the blips don't overpower the music
        sfxSource.PlayOneShot(clip, 0.5f);
    }

    // --- NEW: Allows the DialogueManager to check if the voice is still talking! ---
    public bool IsVoicePlaying()
    {
        if (voiceSource == null) return false;
        return voiceSource.isPlaying;
    }
}