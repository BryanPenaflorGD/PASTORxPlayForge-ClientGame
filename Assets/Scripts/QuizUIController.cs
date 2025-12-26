using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class QuizUIController : MonoBehaviour
{
    [Header("Question UI")]
    public TMP_Text questionText;
    public Button[] answerButtons;
    public TMP_Text[] answerTexts;

    [Header("Feedback")]
    public Color correctColor = Color.green;
    public Color wrongColor = Color.red;
    public Color defaultColor = Color.white;
    public float feedbackDelay = 0.75f;

    [Header("Result UI")]
    public GameObject resultPanel;
    public TMP_Text resultText;


    public TMP_Text reviewText;

    public TMP_Text timerText;
    private void OnEnable()
    {
        if (QuizManager.Instance == null)
        {
            Debug.LogError("QuizManager.Instance is NULL. QuizManager not initialized.");
            return;
        }

        QuizManager.Instance.OnQuestionLoaded += DisplayQuestion;
        QuizManager.Instance.OnQuizFinished += ShowResults;
    }

    private void Update()
    {
        // Only update the text if the QuizManager instance exists 
        // and a quiz is currently running
        if (QuizManager.Instance != null && timerText != null)
        {
            float time = QuizManager.Instance.CurrentTimer;

            // CeilToInt turns 29.1 into 30 so the player sees whole numbers
            timerText.text = Mathf.CeilToInt(time).ToString();

            // Optional: Turn the text red when time is low (less than 5 seconds)
            if (time <= 5f)
            {
                timerText.color = Color.red;
            }
            else
            {
                timerText.color = Color.white;
            }
        }
    }


    private void OnDisable()
    {
        QuizManager.Instance.OnQuestionLoaded -= DisplayQuestion;
        QuizManager.Instance.OnQuizFinished -= ShowResults;
    }

    private void DisplayQuestion(QuizQuestionSO question)
    {
        resultPanel.SetActive(false);
        questionText.transform.parent.gameObject.SetActive(true);
        questionText.text = question.questionText;

        for (int i = 0; i < 4; i++)
        {
            answerTexts[i].text = question.answers[i];
            int index = i;
            answerButtons[i].onClick.RemoveAllListeners();
            answerButtons[i].onClick.AddListener(() =>
            StartCoroutine(HandleAnswer(index))
             );
        }
    }

    private void ShowResults(int correct, int total, List<int> wrongIndices)
    {
        resultPanel.SetActive(true);
        resultText.text = $"Score: {correct} / {total}";

        if (wrongIndices.Count == 0)
        {
            reviewText.text = "Perfect score. No mistakes.";
            return;
        }

        reviewText.text = "Incorrect Questions:\n\n";

        foreach (int index in wrongIndices)
        {
            QuizQuestionSO q = QuizManager.Instance.GetQuestionAt(index);
            reviewText.text += $"- {q.questionText}\n";
        }
    }

    private System.Collections.IEnumerator HandleAnswer(int selectedIndex)
    {
        DisableButtons();

        int correctIndex =
            QuizManager.Instance.GetCurrentCorrectIndex();

        Image selectedImage = answerButtons[selectedIndex].GetComponent<Image>();
        Image correctImage = answerButtons[correctIndex].GetComponent<Image>();

        if (selectedIndex == correctIndex)
        {
            selectedImage.color = correctColor;
        }
        else
        {
            selectedImage.color = wrongColor;
            correctImage.color = correctColor;
        }

        yield return new WaitForSeconds(feedbackDelay);

        ResetButtonColors();
        QuizManager.Instance.SubmitAnswer(selectedIndex);
    }

    private void DisableButtons()
    {
        foreach (var btn in answerButtons)
            btn.interactable = false;
    }

    private void EnableButtons()
    {
        foreach (var btn in answerButtons)
            btn.interactable = true;
    }

    private void ResetButtonColors()
    {
        foreach (var btn in answerButtons)
            btn.GetComponent<Image>().color = defaultColor;

        EnableButtons();
    }
}
