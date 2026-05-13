using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class VolumeSettings : MonoBehaviour
{
    public AudioMixer mainMixer;
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;

    void Awake()
    {
        // Load saved values or default to 0.75f
        masterSlider.value = PlayerPrefs.GetFloat("MasterVol", 0.75f);
        bgmSlider.value = PlayerPrefs.GetFloat("BGMVol", 0.75f);
        sfxSlider.value = PlayerPrefs.GetFloat("SFXVol", 0.75f);

        masterSlider.onValueChanged.AddListener(SetMasterVolume);
        bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        sfxSlider.onValueChanged.AddListener(SetSFXVolume);
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