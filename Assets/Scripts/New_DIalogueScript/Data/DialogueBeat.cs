using UnityEngine;

[CreateAssetMenu(fileName = "NewBeat", menuName = "Dialogue System/Dialogue Beat")]
public class DialogueBeat : ScriptableObject
{
    [Header("System Tracking")]
    [Tooltip("A unique identifier for Save/Load systems. Do not change manually!")]
    public string beatGUID;

    [Space(15)]
    [Header("Dialogue Flow")]
    public DialogueLine[] lines;

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(beatGUID))
        {
            beatGUID = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}