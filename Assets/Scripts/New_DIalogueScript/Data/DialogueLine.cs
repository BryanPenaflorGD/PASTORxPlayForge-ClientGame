using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Events;
using NaughtyAttributes;
using System.Collections.Generic;

public enum CharacterPosition { FarLeft, Left, Center, Right, FarRight }
public enum LineType { DialogueAndCharacters, VideoCutscene, LogicHookOnly }

[System.Serializable]
public class TimedSubtitle
{
    public float showAtTime;
    public float hideAfterSeconds = 3f;
    public CharacterProfile speaker;
    [TextArea(2, 4)] public string dialogueText;
    public AudioClip voiceLine;
}

// --- NEW: Timed SFX Structure ---
[System.Serializable]
public class TimedSFX
{
    [Tooltip("Exact second in the video to play this sound")]
    public float playAtTime;
    public AudioClip sfxClip;
    [Range(0, 1)] public float volume = 1f;
}

[System.Serializable]
public class StageCharacterSetup
{
    public CharacterProfile character;
    [Dropdown("GetExpressionList")]
    public string expression;

    private List<string> GetExpressionList()
    {
        if (character != null && character.expressionStates != null && character.expressionStates.Count > 0)
            return character.expressionStates;
        return new List<string> { "No Character Selected" };
    }

    [Space(10)]
    public CharacterPosition position;
    public bool isTalking = true;
    public bool flipX = false;
}

[System.Serializable]
public class DialogueLine
{
    [Header("--- BEAT TYPE ---")]
    public LineType lineType = LineType.DialogueAndCharacters;

    [Header("--- AUDIO CONTROL ---")]
    [Tooltip("If assigned, the BGM will change when this line triggers.")]
    public AudioClip bgmChange;

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
    public bool fadeOutAfterLine;

    // ==========================================
    // 2. VIDEO FIELDS 
    // ==========================================
    [ShowIf("lineType", LineType.VideoCutscene)]
    public VideoClip cutsceneVideo;

    [ShowIf("lineType", LineType.VideoCutscene)]
    public TimedSubtitle[] cinematicSubtitles;

    [ShowIf("lineType", LineType.VideoCutscene)]
    public TimedSFX[] cinematicSFX; // --- NEW: Timed SFX for videos ---

    // ==========================================
    // 3. GLOBAL LOGIC FIELDS
    // ==========================================
    [HideIf("lineType", LineType.VideoCutscene)]
    public UnityEvent onLineTriggered;
}