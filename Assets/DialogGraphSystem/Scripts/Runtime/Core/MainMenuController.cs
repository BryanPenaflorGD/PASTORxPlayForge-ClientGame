using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;

namespace DialogSystem.Runtime.Core
{
    public class MainMenuController : MonoBehaviour
    {
        // ... (Your existing Struct code stays the same) ...
        [System.Serializable]
        public class StageUIRow
        {
            [Header("Scene Button Setup")]
            public Button sceneButton;
            public GameObject sceneLockIcon;
            [Header("Quiz Button Setup")]
            public Button quizButton;
            public GameObject quizLockIcon;
        }

        [Header("UI References")]
        public List<StageUIRow> stageUIRows;

        // --- NEW: Direct Reference to the Player ---
        public DialogPlayer dialogPlayer;

        [Header("State References")]
        public GameFlowConfig config;

        private void Start()
        {
            RefreshButtons();
        }

        private void OnDestroy()
        {
            // --- FIX 2: Always unsubscribe events to prevent errors ---
            if (ProgressionManager.Instance != null)
            {
                ProgressionManager.Instance.OnStageUnlocked -= RefreshButtons;
            }
        }

        private void Update()
        {
            // Press 'R' on your keyboard to reset everything for testing
            if (Input.GetKeyDown(KeyCode.R))
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                Debug.Log("--- DATA WIPED: Game Reset to Level 1 ---");

                // Refresh the buttons immediately to show the locks
                RefreshButtons();
            }
        }

        public void RefreshButtons()
        {

            // (Your existing setup code...)
            int playerCurrentStageIdx = ProgressionManager.Instance.GetCurrentStageIndex();
            bool isDialogDone = ProgressionManager.Instance.IsDialogFinishedForCurrentStage();

            for (int i = 0; i < stageUIRows.Count; i++)
            {
                // ... (Loop checks) ...
                if (i >= config.stages.Count) continue;

                StageUIRow uiRow = stageUIRows[i];
                GameStage stageData = config.stages[i];
                int stageNumber = i + 1;

                // --- SETUP SCENE BUTTON ---
                bool isSceneUnlocked = playerCurrentStageIdx >= i;
                bool isSceneFinished = playerCurrentStageIdx > i || (playerCurrentStageIdx == i && isDialogDone);

                SetupButtonState(uiRow.sceneButton, uiRow.sceneLockIcon, isSceneUnlocked, isSceneFinished, $"SCENE {stageNumber}", () => {

                    // --- FIX: Use the direct reference instead of FindObjectOfType ---
                    if (dialogPlayer != null)
                    {
                        dialogPlayer.StartDialog(stageData.dialogID);
                    }
                    else
                    {
                        Debug.LogError("DialogPlayer is missing! Assign it in the MainMenuController Inspector.");
                    }
                });

                // --- SETUP QUIZ BUTTON ---
                bool isQuizUnlocked = isSceneFinished;
                bool isQuizFinished = playerCurrentStageIdx > i;

                SetupButtonState(uiRow.quizButton, uiRow.quizLockIcon, isQuizUnlocked, isQuizFinished, $"QUIZ {stageNumber}", () => {
                    SceneManager.LoadScene(stageData.quizSceneName);
                });
            }
        }



        // ... (Keep the SetupButtonState function exactly as it was in the fixed version) ...
        private void SetupButtonState(Button btn, GameObject lockIcon, bool unlocked, bool finished, string baseLabel, UnityEngine.Events.UnityAction action)
        {
            if (btn == null) return;

            btn.onClick.RemoveAllListeners();
            btn.interactable = unlocked; // Lock/Unlock physically

            if (lockIcon != null) lockIcon.SetActive(!unlocked);

            if (unlocked) btn.onClick.AddListener(action);

            // Text visual updates...
            TextMeshProUGUI textComp = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp != null)
            {
                textComp.color = unlocked ? Color.black : new Color(0.5f, 0.5f, 0.5f, 1f);
                textComp.text = finished ? $"{baseLabel}" : baseLabel;
            }
        }
    }
}