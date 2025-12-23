using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Collections;

public class JumbleQuiz : MonoBehaviour
{
    [Header("----Letter Buttons----")]
    public List<LetterButton> buttons;
    public List<TextMeshProUGUI> letterTextAnswer;

    [Header("-----Word Source-----")]
    public string word = "ENERGY";

    [Header("-----Word Answer------")]
    private string wordResult = "";

    [Header("-----Auto Next------")]
    public GameObject currentQuestion;
    public GameObject nextQuestion;

    void Start()
    {
        ShuffleAndAssign();
    }

    public void ShuffleAndAssign()
    {
        wordResult = ""; // reset result

        List<char> letters = new List<char>(word.ToCharArray());
        ShuffleLetters(letters);

        for (int i = 0; i < buttons.Count; i++)
        {
            if (i < letters.Count)
            {
                buttons[i].gameObject.SetActive(true);
                buttons[i].Setup(letters[i], this);
            }
            else
            {
                buttons[i].gameObject.SetActive(false);
            }
        }
    }

    // Called when a letter button is clicked
    public void AddLetter(char letter)
    {
        wordResult += letter;
        Debug.LogError("Result: " + wordResult);

        if (letterTextAnswer != null && wordResult.Length <= letterTextAnswer.Count)
        {
            letterTextAnswer[wordResult.Length - 1].text = letter.ToString();
        }

        if (wordResult.Length == word.Length)
        {
            if (wordResult == word)
            {
                StartCoroutine(Correct());
            }
            else
            {
                StartCoroutine(Retry());
            }
        }
    }
    private IEnumerator Retry()
    {
        yield return new WaitForSeconds(1f);

        foreach (var btn in buttons)
        {
            btn.EnableButton(true);
        }

        for (int i = 0; i < letterTextAnswer.Count; i++)
        {
            letterTextAnswer[i].text = ""; // clear each slot
        }
        ShuffleAndAssign();
    }
    private IEnumerator Correct()
    {
        yield return new WaitForSeconds(1f);
        currentQuestion.SetActive(false);
        nextQuestion.SetActive(true);
    }
    void ShuffleLetters(List<char> letters)
    {
        for (int i = 0; i < letters.Count; i++)
        {
            int randomIndex = Random.Range(i, letters.Count);
            char temp = letters[i];
            letters[i] = letters[randomIndex];
            letters[randomIndex] = temp;
        }
    }
}
