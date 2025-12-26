using UnityEngine;

[CreateAssetMenu(menuName = "Quiz/Quiz Question")]
public class QuizQuestionSO : ScriptableObject
{
    [TextArea]
    public string questionText;

    public string[] answers = new string[4];

    [Range(0, 3)]
    public int correctIndex;
}