using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class VolumeSettings : MonoBehaviour
{
    public AudioMixer mainMixer;
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;

    void Start() // Changed to Start for better reliability with UI/Mixer initialization
    {
        // 1. Add listeners FIRST
        masterSlider.onValueChanged.AddListener(SetMasterVolume);
        bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        sfxSlider.onValueChanged.AddListener(SetSFXVolume);

        // 2. Load and set values. 
        // Setting .value now triggers the listeners added above, 
        // which automatically updates the AudioMixer.
        masterSlider.value = PlayerPrefs.GetFloat("MasterVol", 0.75f);
        bgmSlider.value = PlayerPrefs.GetFloat("BGMVol", 0.75f);
        sfxSlider.value = PlayerPrefs.GetFloat("SFXVol", 0.75f);

        // 3. Force an update in case the slider values were already 
        // equal to the saved values (which wouldn't trigger onValueChanged)
        SetMasterVolume(masterSlider.value);
        SetBGMVolume(bgmSlider.value);
        SetSFXVolume(sfxSlider.value);
    }

    public void SetMasterVolume(float value) => SetMixerVolume("MasterVol", value);
    public void SetBGMVolume(float value) => SetMixerVolume("BGMVol", value);
    public void SetSFXVolume(float value) => SetMixerVolume("SFXVol", value);

    private void SetMixerVolume(string parameter, float value)
    {
        // Convert 0-1 slider value to -80 to 0 Decibels
        float dB = Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20;
        mainMixer.SetFloat(parameter, dB);
        PlayerPrefs.SetFloat(parameter, value);
    }
}