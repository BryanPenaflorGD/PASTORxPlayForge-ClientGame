using UnityEngine;
using System.Collections.Generic; // Required for using Lists!

[CreateAssetMenu(fileName = "NewCharacter", menuName = "Dialogue System/Character Profile")]
public class CharacterProfile : ScriptableObject
{
    [Header("Identity")]
    public string characterName;
    public Color nameTextColor = Color.white;

    [Header("Custom UI")]
    public Sprite customDialogueBox;

    [Header("Animation Setup")]
    [Tooltip("The Animator Controller for this specific character.")]
    public RuntimeAnimatorController animatorController;

    [Tooltip("List the exact names of the Animation States (e.g., 'Idle', 'Happy', 'Angry').")]
    public List<string> expressionStates = new List<string> { "Idle", "Talking" };

    [Header("Audio")]
    public AudioClip defaultBlipSound;
}