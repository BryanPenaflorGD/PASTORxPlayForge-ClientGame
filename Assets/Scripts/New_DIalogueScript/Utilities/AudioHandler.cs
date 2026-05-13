using UnityEngine;
using System.Collections;
using UnityEngine.Audio;

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

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || bgmSource.clip == clip) return;

        if (bgmSource.isPlaying) StartCoroutine(CrossFadeBGM(clip));
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
        while (bgmSource.volume > 0)
        {
            bgmSource.volume -= Time.deltaTime * fadeSpeed;
            yield return null;
        }

        bgmSource.Stop();
        bgmSource.clip = newClip;
        bgmSource.Play();

        while (bgmSource.volume < defaultBGMVolume)
        {
            bgmSource.volume += Time.deltaTime * fadeSpeed;
            yield return null;
        }
    }

    public void PauseAudio()
    {
        if (voiceSource.isPlaying) voiceSource.Pause();
        if (sfxSource.isPlaying) sfxSource.Pause();
        if (bgmSource.isPlaying) bgmSource.Pause();
    }

    public void ResumeAudio()
    {
        // UnPause only if the sources were active (not just having a clip assigned)
        if (voiceSource.clip != null && voiceSource.time > 0 && voiceSource.time < voiceSource.clip.length)
            voiceSource.UnPause();

        if (sfxSource.clip != null)
            sfxSource.UnPause();

        if (bgmSource.clip != null)
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