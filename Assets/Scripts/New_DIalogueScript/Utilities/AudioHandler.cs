using UnityEngine;
using System.Collections;
using UnityEngine.Audio; // Required for AudioMixer interaction

public class AudioHandler : MonoBehaviour
{
    [Header("Audio Mixer")]
    public AudioMixer mainMixer;

    [Header("Audio Sources")]
    public AudioSource voiceSource;
    public AudioSource sfxSource;
    public AudioSource bgmSource;

    [Header("BGM Settings")]
    public float defaultBGMVolume = 0.8f;
    public float duckedBGMVolume = 0.2f;
    public float fadeSpeed = 2f;

    private Coroutine duckCoroutine;

    // IMPORTANT: Ensure these sources are assigned to the correct 
    // Audio Mixer Groups (BGM, SFX, etc.) in the Unity Inspector.

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || bgmSource.clip == clip) return;

        // If music is already playing, crossfade to the new one
        if (bgmSource.isPlaying)
        {
            StartCoroutine(CrossFadeBGM(clip));
        }
        else
        {
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.volume = defaultBGMVolume;
            bgmSource.Play();
        }
    }

    public void PlayVoiceLine(AudioClip clip)
    {
        if (clip == null) return;
        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.Play();
    }

    public void PlaySFX(AudioClip clip, float volume = 1.0f)
    {
        if (clip == null) return;
        // PlayOneShot allows multiple SFX to overlap on one source
        sfxSource.PlayOneShot(clip, volume);
    }

    public void PlayBlip(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, 0.5f);
    }

    public void DuckBGM(bool duck)
    {
        if (duckCoroutine != null) StopCoroutine(duckCoroutine);
        float target = duck ? duckedBGMVolume : defaultBGMVolume;
        duckCoroutine = StartCoroutine(FadeBGM(target));
    }

    private IEnumerator FadeBGM(float targetVolume)
    {
        while (!Mathf.Approximately(bgmSource.volume, targetVolume))
        {
            bgmSource.volume = Mathf.MoveTowards(bgmSource.volume, targetVolume, Time.deltaTime * fadeSpeed);
            yield return null;
        }
    }

    private IEnumerator CrossFadeBGM(AudioClip newClip)
    {
        // Fade Out current music
        while (bgmSource.volume > 0)
        {
            bgmSource.volume -= Time.deltaTime * fadeSpeed;
            yield return null;
        }

        bgmSource.Stop();
        bgmSource.clip = newClip;
        bgmSource.Play();

        // Fade In new music
        while (bgmSource.volume < defaultBGMVolume)
        {
            bgmSource.volume += Time.deltaTime * fadeSpeed;
            yield return null;
        }
    }

    public void PauseAudio()
    {
        voiceSource.Pause();
        sfxSource.Pause();
        bgmSource.Pause();
    }

    public void ResumeAudio()
    {
        voiceSource.UnPause();
        sfxSource.UnPause();
        bgmSource.UnPause();
    }

    public void StopVoiceLine() => voiceSource.Stop();

    public void StopBGM()
    {
        bgmSource.Stop();
        bgmSource.clip = null;
    }

    public bool IsVoicePlaying()
    {
        return voiceSource != null && voiceSource.isPlaying;
    }
}