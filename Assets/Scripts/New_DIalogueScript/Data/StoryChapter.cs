using UnityEngine;

[CreateAssetMenu(fileName = "NewChapter", menuName = "Dialogue System/Story Chapter")]
public class StoryChapter : ScriptableObject
{
    [Header("Chapter Info")]
    public string chapterTitle;

    [Tooltip("A unique identifier for Save/Load systems. Do not change manually!")]
    public string chapterGUID;

    [Space(15)]
    [Header("Story Flow")]
    public DialogueBeat[] storyBeats;

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(chapterGUID))
        {
            chapterGUID = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}