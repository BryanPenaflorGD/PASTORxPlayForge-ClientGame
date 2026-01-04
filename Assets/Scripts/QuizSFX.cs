using DialogSystem.Runtime.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuizSFX : MonoBehaviour
{
    private void Start()
    {
        if (AudioActionHandler.Instance != null)
        {
            AudioActionHandler.Instance.PlayBGM("Music/BGM_Happy");
        } 
    }

    public void ClickButton()
    {
        if (AudioActionHandler.Instance != null)
        {
            AudioActionHandler.Instance.PlaySFX("SFX/POP");
        } 
    }
}
