using UnityEngine;

namespace DialogSystem.Runtime.Core
{
    public class ProgressionManager : MonoBehaviour
    {
        public static ProgressionManager Instance;
        public GameFlowConfig gameConfig;

        private const string PREF_STAGE_INDEX = "CurrentStageIndex";
        private const string PREF_DIALOG_DONE = "IsDialogDone";

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            DontDestroyOnLoad(gameObject); // Keep this alive across scenes
        }

        // --- Getters for UI ---

        public int GetCurrentStageIndex()
        {
            return PlayerPrefs.GetInt(PREF_STAGE_INDEX, 0);
        }

        public bool IsDialogFinishedForCurrentStage()
        {
            // 1 = True, 0 = False
            return PlayerPrefs.GetInt(PREF_DIALOG_DONE, 0) == 1;
        }

        public GameStage GetStageData(int index)
        {
            if (index < gameConfig.stages.Count)
                return gameConfig.stages[index];
            return null;
        }

        // --- Action Methods ---

        // Call this when Dialog Ends
        public void CompleteCurrentDialog()
        {
            PlayerPrefs.SetInt(PREF_DIALOG_DONE, 1);
            PlayerPrefs.Save();
            Debug.Log("Dialog Completed. Quiz Button Unlocked.");
        }

        // Call this when Quiz Ends (Correctly)
        public void CompleteCurrentQuiz()
        {
            int currentIndex = GetCurrentStageIndex();

            // Move to next stage, and reset dialog progress for the new stage
            PlayerPrefs.SetInt(PREF_STAGE_INDEX, currentIndex + 1);
            PlayerPrefs.SetInt(PREF_DIALOG_DONE, 0);
            PlayerPrefs.Save();

            Debug.Log("Quiz Completed. Next Dialog Unlocked.");
        }
    }
}