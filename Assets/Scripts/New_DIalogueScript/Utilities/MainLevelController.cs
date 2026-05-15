using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainLevelController : MonoBehaviour
{
    [Header("Sequence Buttons")]
    public Button scene1Btn;
    public Button quiz1Btn;
    public Button scene2Btn;
    public Button quiz2Btn;
    public Button scene3Btn;

    [Header("Settings")]
    public string scene1Name = "Scene_1";
    public string quiz1Name = "Quiz_1";
    public string scene2Name = "Scene_2";
    public string quiz2Name = "Quiz_2";
    public string scene3Name = "Scene_3";

    private void Start()
    {
        RefreshButtons();
    }

    public void RefreshButtons()
    {
        int progress = ProgressionManager.Instance.GetProgress();

        // Level 0: Scene 1 (Always unlocked)
        SetupButton(scene1Btn, true, progress > 0);

        // Level 1: Quiz 1 (Requires Scene 1 done)
        SetupButton(quiz1Btn, progress >= 1, progress > 1);

        // Level 2: Scene 2 (Requires Quiz 1 done)
        SetupButton(scene2Btn, progress >= 2, progress > 2);

        // Level 3: Quiz 2 (Requires Scene 2 done)
        SetupButton(quiz2Btn, progress >= 3, progress > 3);

        // Level 4: Scene 3 (Requires Quiz 2 done)
        SetupButton(scene3Btn, progress >= 4, progress > 4);
    }

    private void SetupButton(Button btn, bool unlocked, bool finished)
    {
        btn.interactable = unlocked;
        TextMeshProUGUI txt = btn.GetComponentInChildren<TextMeshProUGUI>();

        if (txt != null)
        {
            txt.color = unlocked ? Color.black : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            if (finished) txt.text += "";
        }
    }

    // Button Click Methods
    public void LoadScene1() => SceneManager.LoadScene(scene1Name);
    public void LoadQuiz1() => SceneManager.LoadScene(quiz1Name);
    public void LoadScene2() => SceneManager.LoadScene(scene2Name);
    public void LoadQuiz2() => SceneManager.LoadScene(quiz2Name);
    public void LoadScene3() => SceneManager.LoadScene(scene3Name);

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ProgressionManager.Instance.ResetProgress();
            RefreshButtons();
            Debug.Log("Progress Reset");
        }
    }
}