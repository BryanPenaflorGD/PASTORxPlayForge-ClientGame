using UnityEngine;

public class ProgressionManager : MonoBehaviour
{
    public static ProgressionManager Instance;

    // 0: Scene 1, 1: Quiz 1, 2: Scene 2, 3: Quiz 2, 4: Scene 3, 5: All Done
    private int currentProgress = 0;
    private const string SAVE_KEY = "PlayerGameProgress";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProgress();
        }
        else Destroy(gameObject);
    }

    public int GetProgress() => currentProgress;

    public void CompleteCurrentStep(int stepIndex)
    {
        // Only advance if the completed step is the one we are currently on
        if (stepIndex == currentProgress)
        {
            currentProgress++;
            SaveProgress();
        }
    }

    public void SaveProgress() => PlayerPrefs.SetInt(SAVE_KEY, currentProgress);
    public void LoadProgress() => currentProgress = PlayerPrefs.GetInt(SAVE_KEY, 0);
    public void ResetProgress()
    {
        currentProgress = 0;
        SaveProgress();
    }
}