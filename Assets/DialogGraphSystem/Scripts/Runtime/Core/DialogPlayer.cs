using UnityEngine;

namespace DialogSystem.Runtime.Core
{
    public class DialogPlayer : MonoBehaviour
    {
        public GameObject mainMenu;

        // Optional: Trigger specific logic if the graph sends an Action
        // e.g., Action Node payload: "CompleteLevel"
        public void HandleGraphAction(string actionName)
        {
            if (actionName == "UnlockQuiz")
            {
                ProgressionManager.Instance.CompleteCurrentDialog();
            }
        }

        public void StartDialog(string dialogID)
        {
            // Assuming DialogManager takes a callback for completion
            DialogManager.Instance.PlayDialogByID(dialogID, OnDialogEnded);
            mainMenu.SetActive(false);
        }

        public void OnDialogEnded()
        {
            // NOTIFY MANAGER: The player finished the text!
            ProgressionManager.Instance.CompleteCurrentDialog();

            mainMenu.SetActive(true);

            // Optional: Force the menu to refresh its button states immediately
            MainMenuController menuController = mainMenu.GetComponent<MainMenuController>();
            if (menuController != null) menuController.RefreshButtons();
        }

        public void QuitGame(string payloadJson)
        {
            Debug.Log("Quit Game");
            Application.Quit();
        }
    }
}