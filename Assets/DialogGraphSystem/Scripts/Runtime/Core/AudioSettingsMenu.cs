using UnityEngine;
using UnityEngine.UI;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Settings.Panels;

public class AudioSettingsMenu : MonoBehaviour
{
    [Header("UI Sliders")]
    public Slider masterSlider;
    public Slider voSlider;     // Voice Over
    public Slider bgmSlider;    // Background Music
    public Slider sfxSlider;    // SFX

    [Header("References")]
    // Drag your 'DialogAudioSettings' asset here
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

        // 2. Setup Sliders
        // SetValueWithoutNotify prevents the slider from triggering the 'Save' logic during startup
        if (masterSlider)
        {
            masterSlider.SetValueWithoutNotify(masterVol);
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
        }
        if (voSlider)
        {
            voSlider.SetValueWithoutNotify(voVol);
            voSlider.onValueChanged.AddListener(SetVOVolume);
        }
        if (bgmSlider)
        {
            bgmSlider.SetValueWithoutNotify(bgmVol);
            bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        }
        if (sfxSlider)
        {
            sfxSlider.SetValueWithoutNotify(sfxVol);
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }

        // 3. Apply Volumes Immediately
        UpdateMasterLogic(masterVol);
        UpdateVOLogic(voVol);
        UpdateBGMLogic(bgmVol);
        UpdateSFXLogic(sfxVol);
    }

    // --- SLIDER EVENTS ---

    public void SetMasterVolume(float value)
    {
        PlayerPrefs.SetFloat(PREF_MASTER, value);
        PlayerPrefs.Save();
        UpdateMasterLogic(value);
    }

    public void SetVOVolume(float value)
    {
        PlayerPrefs.SetFloat(PREF_VO, value);
        PlayerPrefs.Save();
        UpdateVOLogic(value);
    }

    public void SetBGMVolume(float value)
    {
        PlayerPrefs.SetFloat(PREF_BGM, value);
        PlayerPrefs.Save();
        UpdateBGMLogic(value);
    }

    public void SetSFXVolume(float value)
    {
        PlayerPrefs.SetFloat(PREF_SFX, value);
        PlayerPrefs.Save();
        UpdateSFXLogic(value);
    }

    // --- UPDATE LOGIC ---

    private void UpdateMasterLogic(float value)
    {
        AudioListener.volume = value; // Controls EVERYTHING (including Video)
    }

    private void UpdateVOLogic(float value)
    {
        if (dialogSettings != null) dialogSettings.voiceVolume = value;

        // Force update active speaker
        if (DialogSystem.Runtime.Core.DialogManager.Instance != null &&
            DialogSystem.Runtime.Core.DialogManager.Instance.audioSource != null)
        {
            DialogSystem.Runtime.Core.DialogManager.Instance.audioSource.volume = value;
        }
    }

    private void UpdateBGMLogic(float value)
    {
        // Update the Singleton Handler if it exists in the scene
        if (AudioActionHandler.Instance != null && AudioActionHandler.Instance.musicSource != null)
        {
            AudioActionHandler.Instance.musicSource.volume = value;
        }
    }

    private void UpdateSFXLogic(float value)
    {
        // Update the Singleton Handler if it exists
        if (AudioActionHandler.Instance != null && AudioActionHandler.Instance.sfxSource != null)
        {
            AudioActionHandler.Instance.sfxSource.volume = value;
        }

        if (dialogSettings != null) dialogSettings.sfxVolume = value;
    }
}