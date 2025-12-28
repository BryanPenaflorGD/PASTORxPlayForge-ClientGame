using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace DialogSystem.Runtime.Core
{
    public class MainMenuController : MonoBehaviour
    {
        // --- UPDATED STRUCT ---
        [System.Serializable]
        public class StageUIRow
        {
            [Header("Scene Button Setup")]
            public Button sceneButton;
            public GameObject sceneLockIcon; // Reference to the Lock Image GameObject

            [Header("Quiz Button Setup")]
            public Button quizButton;
            public GameObject quizLockIcon;  // Reference to the Lock Image GameObject
        }

        [Header("UI References")]
        public List<StageUIRow> stageUIRows;

        [Header("State References")]
        public GameFlowConfig config;

        private void OnEnable()
        {
            RefreshButtons();
        }

        public void RefreshButtons()
        {
            int playerCurrentStageIdx = ProgressionManager.Instance.GetCurrentStageIndex();
            bool isCurrentDialogDone = ProgressionManager.Instance.IsDialogFinishedForCurrentStage();

            for (int i = 0; i < stageUIRows.Count; i++)
            {
                StageUIRow uiRow = stageUIRows[i];

                // Safety check
                if (i >= config.stages.Count) continue;

                GameStage stageData = config.stages[i];
                int stageNumber = i + 1;

                // --- SETUP SCENE BUTTON ---
                bool isSceneUnlocked = playerCurrentStageIdx >= i;
                bool isSceneFinished = playerCurrentStageIdx > i || (playerCurrentStageIdx == i && isCurrentDialogDone);

                // We now pass the lock icon reference to the helper function
                SetupButtonState(uiRow.sceneButton, uiRow.sceneLockIcon, isSceneUnlocked, isSceneFinished, $"SCENE {stageNumber}", () => {
                    FindObjectOfType<DialogPlayer>().StartDialog(stageData.dialogID);
                });

                // --- SETUP QUIZ BUTTON ---
                bool isQuizUnlocked = isSceneFinished;
                bool isQuizFinished = playerCurrentStageIdx > i;

                // We now pass the lock icon reference to the helper function
                SetupButtonState(uiRow.quizButton, uiRow.quizLockIcon, isQuizUnlocked, isQuizFinished, $"QUIZ {stageNumber}", () => {
                    SceneManager.LoadScene(stageData.quizSceneName);
                });
            }
        }

        // --- UPDATED HELPER FUNCTION ---
        // Added GameObject lockIcon parameter
        private void SetupButtonState(Button btn, GameObject lockIcon, bool unlocked, bool finished, string baseLabel, UnityEngine.Events.UnityAction action)
        {
            btn.onClick.RemoveAllListeners();
            Text textComp = btn.GetComponentInChildren<Text>();

            // --- VISUAL LOCK LOGIC ---
            // If the lock icon exists, turn it ON if locked, OFF if unlocked.
            if (lockIcon != null)
            {
                lockIcon.SetActive(!unlocked);
            }
            // -------------------------

            if (!unlocked)
            {
                btn.interactable = false;
                // We don't necessarily need to change text to "LOCKED" anymore if we have visual icon
                // But let's keep it simple for now or maybe change color.
                textComp.color = new Color(0.5f, 0.5f, 0.5f, 1f); // Gray out text
            }
            else
            {
                btn.interactable = true;
                btn.onClick.AddListener(action);
                textComp.color = Color.white; // Reset color

                if (finished)
                {
                    textComp.text = $"{baseLabel} ✓";
                }
                else
                {
                    textComp.text = baseLabel;
                }
            }
        }
    }
}