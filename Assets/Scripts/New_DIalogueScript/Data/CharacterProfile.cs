using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "Dialogue System/Character Profile")]
public class CharacterProfile : ScriptableObject
{
    [Header("Identity")]
    public string characterName;
    public Color nameTextColor = Color.white;

    [Header("Custom UI")]
    public Sprite customDialogueBox;

    [Header("Audio")]
    public AudioClip defaultBlipSound;
}