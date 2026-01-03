using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using System.Xml.Serialization;

public class JumbleQuiz : MonoBehaviour
{
    [Header("----Letter Buttons----")]
    public List<LetterButton> buttons;
    public List<TextMeshProUGUI> letterTextAnswer;
    public List<GameObject> questions;

    [Header("-----Word Source-----")]
    public string word = "ENERGY";

    [Header("-----Word Answer------")]
    private string wordResult = "";
    private int correctAnswer = 0;

    [Header("-----Auto Next------")]
    public int currentQuestionIndex = 0;
    public GameObject startObject;

    void Start()
    {
        startObject.SetActive(true);
        ShuffleAndAssign();
        correctAnswer = 0;
    }

    public void ShuffleAndAssign()
    {
        startObject.SetActive(false);

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
    public void RetryBTN()
    {
        StartCoroutine(Retry());
    }

    private IEnumerator Retry()
    {
        //IGDI MAGLAAG SOUND PAGSALA 

        // Change text color to red
        foreach (var txt in letterTextAnswer)
        {
            txt.color = Color.red;
        }

        yield return new WaitForSeconds(1f);

        // Change text color to red
        foreach (var txt in letterTextAnswer)
        {
            txt.color = Color.black;
        }

        //Enable All BTN
        foreach (var btn in buttons)
        {
            btn.EnableButton(true);
        }

        //Clear All Slot
        for (int i = 0; i < letterTextAnswer.Count; i++)
        {
            letterTextAnswer[i].text = "";
        }
        ShuffleAndAssign();
    }
    private IEnumerator Correct()
    {
        //IGDI MAGLAAG NING SOUND PAGTAMA ANG SIMBAG

        foreach (var txt in letterTextAnswer)
        {
            txt.color = Color.green;
        }

        yield return new WaitForSeconds(1f);

        foreach (var txt in letterTextAnswer)
        {
            txt.color = Color.black;
        }

        questions[currentQuestionIndex].SetActive(false);

        currentQuestionIndex++;

        if (currentQuestionIndex < questions.Count)
        {
            questions[currentQuestionIndex].SetActive(true);
           
        }
        else
        {
            //IGDI ANG PAGLIPAT NING SCENE

            //SceneManager.LoadSceneAsync();
            DialogSystem.Runtime.Core.ProgressionManager.Instance.CompleteCurrentQuiz();
            SceneManager.LoadScene("SelectionScene");
        }

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
