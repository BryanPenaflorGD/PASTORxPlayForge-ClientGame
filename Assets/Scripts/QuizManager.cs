using System;
using System.Collections.Generic;
using UnityEngine;

public class QuizManager : MonoBehaviour
{
    public static QuizManager Instance;

    public event Action<QuizQuestionSO> OnQuestionLoaded;
    public event Action<int, int, List<int>> OnQuizFinished;

    private QuizSetSO currentQuiz;
    private int currentIndex;
    private int correctCount;
    private List<int> wrongQuestionIndices = new();

    public float timePerQuestion = 30f;
    private float timer;
    private bool timerRunning;

    public float CurrentTimer => timer;
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Update()
    {
        if (!timerRunning) return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            timerRunning = false;
            wrongQuestionIndices.Add(currentIndex);
            currentIndex++;

            if (currentIndex >= currentQuiz.questions.Count)
                FinishQuiz();
            else
                LoadQuestion();
        }
    }


    public void StartQuiz(QuizSetSO quizSet)
    {
        currentQuiz = quizSet;
        currentIndex = 0;
        correctCount = 0;
        wrongQuestionIndices.Clear();

        LoadQuestion();
    }

    private void LoadQuestion()
    {
        timer = timePerQuestion;
        timerRunning = true;
        OnQuestionLoaded?.Invoke(currentQuiz.questions[currentIndex]);
    }


    public void SubmitAnswer(int selectedIndex)
    {
        QuizQuestionSO question = currentQuiz.questions[currentIndex];

        if (selectedIndex == question.correctIndex)
        {
            correctCount++;
        }
        else
        {
            wrongQuestionIndices.Add(currentIndex);
        }

        currentIndex++;

        if (currentIndex >= currentQuiz.questions.Count)
        {
            FinishQuiz();
        }
        else
        {
            LoadQuestion();
        }
    }

    private void FinishQuiz()
    {
        OnQuizFinished?.Invoke(
            correctCount,
            currentQuiz.questions.Count,
            wrongQuestionIndices
        );
    }

    public QuizQuestionSO GetQuestionAt(int index)
    {
        return currentQuiz.questions[index];
    }

    public int GetCurrentCorrectIndex()
    {
        return currentQuiz.questions[currentIndex].correctIndex;
    }
}
