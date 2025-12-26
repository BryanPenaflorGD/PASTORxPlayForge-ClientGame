using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StartQuiz : MonoBehaviour
{
    public QuizSetSO quizSet;

    public void StartQuizzes()
    {
        QuizManager.Instance.StartQuiz(quizSet);
    }
}
