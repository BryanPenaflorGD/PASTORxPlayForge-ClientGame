using UnityEngine;
using UnityEngine.Video;

// This line ensures Unity automatically adds the required components
[RequireComponent(typeof(VideoPlayer))]
public class VideoVolumeSync : MonoBehaviour
{
    private VideoPlayer myVideoPlayer;

    private void Awake()
    {
        myVideoPlayer = GetComponent<VideoPlayer>();
    }

    private void Update()
    {
        if (myVideoPlayer.isPrepared)
        {
            // 1. Get the Voice Over volume from your Settings (saved by the slider)
            float voVolume = PlayerPrefs.GetFloat("VoiceVolume", 1f);

            // 2. (Optional) If you want Master Slider to ALSO affect this, uncomment the next line:
            // voVolume *= AudioListener.volume; 

            // 3. Apply it to the Video's Direct Audio Track (Track 0)
            myVideoPlayer.SetDirectAudioVolume(0, voVolume);
        }
    }
}