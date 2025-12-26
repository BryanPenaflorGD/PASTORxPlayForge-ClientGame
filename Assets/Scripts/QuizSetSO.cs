using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Quiz/Quiz Set")]
public class QuizSetSO : ScriptableObject
{
    public string quizName;
    public List<QuizQuestionSO> questions;
}
