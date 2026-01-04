using DialogSystem.Runtime.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuizSFX : MonoBehaviour
{
    private void Start()
    {
        AudioActionHandler.Instance.PlayBGM("Music/BGM_Happy");
    }

    public void ClickButton()
    {
        AudioActionHandler.Instance.PlaySFX("SFX/POP");
    }
}
