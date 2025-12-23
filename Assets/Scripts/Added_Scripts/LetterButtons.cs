using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LetterButton : MonoBehaviour
{
    public TextMeshProUGUI letterText;
    private char currentLetter;
    private JumbleQuiz quiz;
    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
    }
    public void Setup(char letter, JumbleQuiz jumbleQuiz)
    {
        currentLetter = letter;
        letterText.text = letter.ToString();
        quiz = jumbleQuiz;
    }

    public void OnClick()
    {
        quiz.AddLetter(currentLetter);
        button.interactable = false;
    }
    public void EnableButton(bool state)
    {
        button.interactable = state;
    }
}
