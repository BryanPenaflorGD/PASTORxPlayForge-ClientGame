using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic; // Needed for Lists/Arrays
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Settings.Panels;

public class AudioSettingsMenu : MonoBehaviour
{
    [Header("UI Slider Arrays")]
    // Changed from single variables to Arrays []
    public Slider[] masterSliders;
    public Slider[] voSliders;
    public Slider[] bgmSliders;
    public Slider[] sfxSliders;

    [Header("References")]
    public DialogAudioSettings dialogSettings;

    // Constants
    private const string PREF_MASTER = "MasterVolume";
    private const string PREF_VO = "VoiceVolume";
    private const string PREF_BGM = "BGMVolume";
    private const string PREF_SFX = "SFXVolume";

    private void Start()
    {
        InitializeVolume();
    }

    private void InitializeVolume()
    {
        // 1. Load Saved Data
        float masterVol = PlayerPrefs.GetFloat(PREF_MASTER, 1f);
        float voVol = PlayerPrefs.GetFloat(PREF_VO, 1f);
        float bgmVol = PlayerPrefs.GetFloat(PREF_BGM, 1f);
        float sfxVol = PlayerPrefs.GetFloat(PREF_SFX, 1f);

        // 2. Setup All Sliders (Using a helper function to keep code clean)
        SetupSliderArray(masterSliders, masterVol, SetMasterVolume);
        SetupSliderArray(voSliders, voVol, SetVOVolume);
        SetupSliderArray(bgmSliders, bgmVol, SetBGMVolume);
        SetupSliderArray(sfxSliders, sfxVol, SetSFXVolume);

        // 3. Apply Volumes Immediately
        UpdateMasterLogic(masterVol);
        UpdateVOLogic(voVol);
        UpdateBGMLogic(bgmVol);
        UpdateSFXLogic(sfxVol);
    }

    // --- HELPER TO SETUP ARRAYS ---
    private void SetupSliderArray(Slider[] sliders, float value, UnityEngine.Events.UnityAction<float> action)
    {
        if (sliders == null) return;

        foreach (Slider s in sliders)
        {
            if (s != null)
            {
                // Set visual position without triggering code
                s.SetValueWithoutNotify(value);
                // Add the listener
                s.onValueChanged.AddListener(action);
            }
        }
    }

    // --- HELPER TO SYNC VISUALS ---
    // This makes sure if you move Slider A, Slider B moves too.
    private void SyncVisuals(Slider[] sliders, float value)
    {
        if (sliders == null) return;

        foreach (Slider s in sliders)
        {
            if (s != null)
            {
                // Update the visual knob, but DO NOT run the 'onValueChanged' event again
                s.SetValueWithoutNotify(value);
            }
        }
    }

    // --- SLIDER EVENTS ---

    public void SetMasterVolume(float value)
    {
        PlayerPrefs.SetFloat(PREF_MASTER, value);
        PlayerPrefs.Save();

        SyncVisuals(masterSliders, value); // <--- Sync other sliders
        UpdateMasterLogic(value);
    }

    public void SetVOVolume(float value)
    {
        PlayerPrefs.SetFloat(PREF_VO, value);
        PlayerPrefs.Save();

        SyncVisuals(voSliders, value); // <--- Sync other sliders
        UpdateVOLogic(value);
    }

    public void SetBGMVolume(float value)
    {
        PlayerPrefs.SetFloat(PREF_BGM, value);
        PlayerPrefs.Save();

        SyncVisuals(bgmSliders, value); // <--- Sync other sliders
        UpdateBGMLogic(value);
    }

    public void SetSFXVolume(float value)
    {
        PlayerPrefs.SetFloat(PREF_SFX, value);
        PlayerPrefs.Save();

        SyncVisuals(sfxSliders, value); // <--- Sync other sliders
        UpdateSFXLogic(value);
    }

    // --- UPDATE LOGIC (Same as before) ---

    private void UpdateMasterLogic(float value)
    {
        AudioListener.volume = value;
    }

    private void UpdateVOLogic(float value)
    {
        if (dialogSettings != null) dialogSettings.voiceVolume = value;

        if (DialogSystem.Runtime.Core.DialogManager.Instance != null &&
            DialogSystem.Runtime.Core.DialogManager.Instance.audioSource != null)
        {
            DialogSystem.Runtime.Core.DialogManager.Instance.audioSource.volume = value;
        }
    }

    private void UpdateBGMLogic(float value)
    {
        if (AudioActionHandler.Instance != null && AudioActionHandler.Instance.musicSource != null)
        {
            AudioActionHandler.Instance.musicSource.volume = value;
        }
    }

    private void UpdateSFXLogic(float value)
    {
        if (AudioActionHandler.Instance != null && AudioActionHandler.Instance.sfxSource != null)
        {
            AudioActionHandler.Instance.sfxSource.volume = value;
        }

        if (dialogSettings != null) dialogSettings.sfxVolume = value;
    }
}