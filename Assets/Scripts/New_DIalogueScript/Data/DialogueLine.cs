using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Events;
using NaughtyAttributes;
using System.Collections.Generic;

public enum CharacterPosition { FarLeft, Left, Center, Right, FarRight }
public enum LineType { DialogueAndCharacters, VideoCutscene, LogicHookOnly }

// --- NEW: The Cinematic Subtitle Structure ---
[System.Serializable]
public class TimedSubtitle
{
    [Tooltip("Exact second in the video to show this dialogue (e.g., 4.5)")]
    public float showAtTime;

    [Tooltip("How many seconds the text stays on screen. Set to 0 to keep it visible until the next subtitle appears.")]
    public float hideAfterSeconds = 3f;

    public CharacterProfile speaker;
    [TextArea(2, 4)] public string dialogueText;

    [Tooltip("Optional Voice Over for this specific cinematic line")]
    public AudioClip voiceLine;
}

[System.Serializable]
public class StageCharacterSetup
{
    [Tooltip("Drag the Character Profile here first to populate the expression dropdown!")]
    public CharacterProfile character;

    [Dropdown("GetExpressionList")]
    [Tooltip("Select the character's expression.")]
    public string expression;

    private List<string> GetExpressionList()
    {
        if (character != null && character.expressionStates != null && character.expressionStates.Count > 0)
        {
            return character.expressionStates;
        }
        return new List<string> { "No Character Selected" };
    }

    [Space(10)]
    public CharacterPosition position;

    [Tooltip("Uncheck to dim the character (e.g., when they aren't speaking)")]
    public bool isTalking = true;
    public bool flipX = false;
}

[System.Serializable]
public class DialogueLine
{
    [Header("--- BEAT TYPE ---")]
    [Tooltip("Select what this specific beat should do.")]
    public LineType lineType = LineType.DialogueAndCharacters;

    // ==========================================
    // 1. DIALOGUE FIELDS
    // ==========================================

    [ShowIf("lineType", LineType.DialogueAndCharacters)]
    public CharacterProfile speaker;

    [ShowIf("lineType", LineType.DialogueAndCharacters)]
    [TextArea(2, 4)] public string dialogueText;

    [ShowIf("lineType", LineType.DialogueAndCharacters)]
    public StageCharacterSetup[] stageCharacters;

    [ShowIf("lineType", LineType.DialogueAndCharacters)]
    public AudioClip voiceLine;

    [ShowIf("lineType", LineType.DialogueAndCharacters)]
    [Tooltip("Check this to fade the screen to black after this line finishes")]
    public bool fadeOutAfterLine;

    // ==========================================
    // 2. VIDEO FIELDS 
    // ==========================================

    [ShowIf("lineType", LineType.VideoCutscene)]
    public VideoClip cutsceneVideo;

    [ShowIf("lineType", LineType.VideoCutscene)]
    [Tooltip("Add precise subtitles that play over the video!")]
    public TimedSubtitle[] cinematicSubtitles;

    // ==========================================
    // 3. GLOBAL LOGIC FIELDS
    // ==========================================

    [HideIf("lineType", LineType.VideoCutscene)]
    public UnityEvent onLineTriggered;
}