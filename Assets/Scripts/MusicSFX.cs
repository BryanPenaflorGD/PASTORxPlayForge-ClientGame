using DialogSystem.Runtime.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicSFX : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        AudioActionHandler.Instance.PlayBGM("Music/BGM_Happy");
    }

    public void Clicking()
    {
        AudioActionHandler.Instance.PlaySFX("SFX/POP");
    }
    public void Quit()
    {
        Application.Quit();
    }
}
