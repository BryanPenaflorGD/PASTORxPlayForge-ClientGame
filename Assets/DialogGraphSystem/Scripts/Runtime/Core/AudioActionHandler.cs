using UnityEngine;
using System.Collections; // Required for Coroutines

namespace DialogSystem.Runtime.Core
{
    public class AudioActionHandler : MonoBehaviour
    {
        public static AudioActionHandler Instance { get; private set; }
        [Header("Audio Sources")]
        [Tooltip("Assign an AudioSource for Background Music (Looping)")]
        public AudioSource musicSource;

        [Tooltip("Assign an AudioSource for Sound Effects (One Shots)")]
        public AudioSource sfxSource;

        private Coroutine blockingCoroutine;

        private void Awake()
        {
            // SINGLETON PATTERN
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep alive across scenes

            // LOAD SAVED VOLUMES IMMEDIATELY
            LoadVolumes();
        }

        public void LoadVolumes()
        {
            // Read from the same keys used in the Menu Script
            if (musicSource != null)
                musicSource.volume = PlayerPrefs.GetFloat("BGMVolume", 1f);

            if (sfxSource != null)
                sfxSource.volume = PlayerPrefs.GetFloat("SFXVolume", 1f);

            // Note: We don't set Master/VO here because:
            // 1. Master is handled by AudioListener (Global)
            // 2. VO is handled by the DialogSettings Asset (Persistent)
        }

        // =========================================================
        // OPTION A: Background Music (Loops, Doesn't block)
        // Use this for: Level Music, Ambience
        // =========================================================
        public void PlayBGM(string audioFileName)
        {
            string cleanName = audioFileName?.Trim();
            AudioClip clip = Resources.Load<AudioClip>(cleanName);

            if (clip != null && musicSource != null)
            {
                // If it's the same song, don't restart it!
                if (musicSource.clip == clip && musicSource.isPlaying) return;

                musicSource.clip = clip;
                musicSource.loop = true;
                musicSource.Play();
            }
        }

        // =========================================================
        // OPTION B: Simple SFX (Doesn't block)
        // Use this for: UI sounds, Footsteps, things happening while reading
        // =========================================================
        public void PlaySFX(string audioFileName)
        {
            string cleanName = audioFileName?.Trim();
            AudioClip clip = Resources.Load<AudioClip>(cleanName);

            if (clip != null)
            {
                if (sfxSource != null)
                {
                    // PlayOneShot allows multiple sounds to overlap
                    sfxSource.PlayOneShot(clip);
                }
            }
            else
            {
                Debug.LogError($"[AudioActionHandler] SFX Clip not found in Resources: {cleanName}");
            }
        }

        // =========================================================
        // OPTION C: Blocking SFX (BLOCKS player input)
        // Use this for: Explosions, Voiceovers, Important story cues
        // =========================================================
        public void PlaySFXBlocking(string audioFileName)
        {
            string cleanName = audioFileName?.Trim();
            AudioClip clip = Resources.Load<AudioClip>(cleanName);

            if (clip != null && sfxSource != null)
            {
                // 1. Stop any running blocking routines
                if (blockingCoroutine != null) StopCoroutine(blockingCoroutine);

                // 2. LOCK THE DIALOG MANAGER
                if (DialogSystem.Runtime.Core.DialogManager.Instance != null)
                {
                    DialogSystem.Runtime.Core.DialogManager.Instance.isInputLocked = true;
                }

                // 3. Play the sound
                sfxSource.clip = clip;
                sfxSource.loop = false;
                sfxSource.Play();

                // 4. Start counting time to unlock
                blockingCoroutine = StartCoroutine(WaitForSoundToFinish(clip.length));
            }
            else
            {
                Debug.LogError($"[AudioActionHandler] SFX Clip not found in Resources: {cleanName}");
            }
        }

        // =========================================================
        // Internal Logic
        // =========================================================
        private IEnumerator WaitForSoundToFinish(float duration)
        {
            // Wait for the exact length of the audio clip
            yield return new WaitForSeconds(duration);

            // Unlock the game
            if (DialogSystem.Runtime.Core.DialogManager.Instance != null)
            {
                DialogSystem.Runtime.Core.DialogManager.Instance.isInputLocked = false;
            }

            blockingCoroutine = null;
        }

        private IEnumerator FadeOutCoroutine(float duration)
        {
            float startVolume = musicSource.volume;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                // Lerp lowers the volume from startVolume to 0 over 'duration' seconds
                musicSource.volume = Mathf.Lerp(startVolume, 0f, timer / duration);
                yield return null;
            }

            musicSource.Stop();
            musicSource.volume = startVolume; // Reset volume for next time
        }

        public void StopBGM(float fadeDuration = 1.0f)
        {
            if (musicSource != null && musicSource.isPlaying)
            {
                StartCoroutine(FadeOutCoroutine(fadeDuration));
            }
        }
    }
}