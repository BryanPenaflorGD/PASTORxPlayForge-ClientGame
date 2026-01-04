using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Xml.Serialization;
using DialogSystem.Runtime.Core;

public class SoundManager : MonoBehaviour
{
    void Start()
    {
        if (AudioActionHandler.Instance != null)
        {
            AudioActionHandler.Instance.PlayBGM("Music/Quiz1BGM");
        }    
    }

    public void ButtonSFX()
    {
        if (AudioActionHandler.Instance != null)
        {
            AudioActionHandler.Instance.PlaySFX("SFX/popping");
        }
    }
}
