using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class DialogueManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject dialoguePanel;
    public Image dialogueBoxBackground;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;
    public Image[] characterSlots;

    [Header("Settings")]
    public float typingSpeed = 0.03f;
    [Range(1, 5)] public int blipFrequency = 2;
    public Color dimmedColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    [Header("Auto-Read Settings")]
    public bool isAutoPlay = false;
    public float autoPlayDelay = 1.5f;
    private Coroutine autoPlayCoroutine;

    [Header("Video Settings")]
    [Tooltip("How many seconds before the video ends should the fade-out begin?")]
    public float videoFadeHeadstart = 1.0f;

    [Header("References")]
    public VideoHandler videoHandler;
    public AudioHandler audioHandler;
    public TransitionHandler transitionHandler;
    public StoryChapter currentChapter;

    private int currentBeatIndex = 0;
    private int currentLineIndex = 0;
    private bool isTyping = false;
    private bool isWaitingForEvent = false;
    private Coroutine typingCoroutine;

    void Start()
    {
        foreach (Image slot in characterSlots) { slot.gameObject.SetActive(false); }
        if (currentChapter != null) StartChapter(currentChapter);
    }

    public void StartChapter(StoryChapter chapter)
    {
        currentChapter = chapter;
        currentBeatIndex = 0;
        currentLineIndex = 0;

        DialogueLine firstLine = GetCurrentLine();

        // If the scene starts with a video, we skip the initial fade-in and let the video do it
        if (firstLine.lineType == LineType.VideoCutscene)
        {
            StartBeat();
        }
        else
        {
            isWaitingForEvent = true;
            StartBeat();

            transitionHandler.FadeToClear(() => {
                isWaitingForEvent = false;
            });
        }
    }

    void StartBeat()
    {
        currentLineIndex = 0;
        DisplayLine();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !isWaitingForEvent)
        {
            DialogueLine line = GetCurrentLine();

            // Only allow clicking to skip/advance if we are on a Dialogue line!
            if (line.lineType != LineType.DialogueAndCharacters) return;

            if (isTyping)
            {
                StopCoroutine(typingCoroutine);
                dialogueText.text = line.dialogueText;
                isTyping = false;
                audioHandler.StopVoiceLine();

                if (isAutoPlay)
                {
                    if (autoPlayCoroutine != null) StopCoroutine(autoPlayCoroutine);
                    autoPlayCoroutine = StartCoroutine(AutoPlayWait());
                }
                else if (string.IsNullOrWhiteSpace(line.dialogueText))
                {
                    HandlePostLineLogic();
                }
            }
            else
            {
                if (autoPlayCoroutine != null) StopCoroutine(autoPlayCoroutine);
                HandlePostLineLogic();
            }
        }
    }

    DialogueLine GetCurrentLine() => currentChapter.storyBeats[currentBeatIndex].lines[currentLineIndex];

    void DisplayLine()
    {
        DialogueLine line = GetCurrentLine();

        // 1. EVALUATE LINE TYPE
        switch (line.lineType)
        {
            case LineType.DialogueAndCharacters:
                line.onLineTriggered?.Invoke();
                if (line.voiceLine != null) audioHandler.PlayVoiceLine(line.voiceLine);

                dialoguePanel.SetActive(true);

                if (line.speaker != null)
                {
                    nameText.text = line.speaker.characterName;
                    nameText.color = line.speaker.nameTextColor;
                    if (line.speaker.customDialogueBox != null)
                        dialogueBoxBackground.sprite = line.speaker.customDialogueBox;
                }
                else
                {
                    nameText.text = "";
                }

                UpdateCharacterSprites(line);
                typingCoroutine = StartCoroutine(TypeText(line));
                break;

            case LineType.VideoCutscene:
                // Clear the stage and hide UI for the video
                foreach (Image slot in characterSlots) { slot.gameObject.SetActive(false); slot.sprite = null; }
                dialoguePanel.SetActive(false);

                PlayVideoWithTransitions(line.cutsceneVideo, NextLine);
                break;

            case LineType.LogicHookOnly:
                // Fire the logic and immediately proceed to the next line in the background!
                line.onLineTriggered?.Invoke();
                NextLine();
                break;
        }
    }

    void HandlePostLineLogic()
    {
        DialogueLine line = GetCurrentLine();

        if (line.lineType == LineType.DialogueAndCharacters && line.fadeOutAfterLine)
        {
            isWaitingForEvent = true;
            transitionHandler.FadeToBlack(() => {
                NextLine();
                transitionHandler.FadeToClear(() => {
                    isWaitingForEvent = false;
                });
            });
        }
        else
        {
            NextLine();
        }
    }

    void PlayVideoWithTransitions(VideoClip clip, System.Action onVideoComplete)
    {
        isWaitingForEvent = true;

        transitionHandler.FadeToBlack(() => {
            dialoguePanel.SetActive(false);

            videoHandler.PlayVideo(clip, null);

            float timeToWait = (float)clip.length - videoFadeHeadstart;
            if (timeToWait < 0) timeToWait = 0;

            StartCoroutine(WaitAndFadeOutVideo(timeToWait, onVideoComplete));

            transitionHandler.FadeToClear();
        });
    }

    IEnumerator WaitAndFadeOutVideo(float waitTime, System.Action onVideoComplete)
    {
        yield return new WaitForSeconds(waitTime);

        transitionHandler.FadeToBlack(() => {
            videoHandler.StopAndClearVideo();
            isWaitingForEvent = false;
            onVideoComplete?.Invoke();
            transitionHandler.FadeToClear();
        });
    }

    void NextLine()
    {
        currentLineIndex++;
        if (currentLineIndex < currentChapter.storyBeats[currentBeatIndex].lines.Length)
        {
            DisplayLine();
        }
        else
        {
            NextBeat();
        }
    }

    void NextBeat()
    {
        currentBeatIndex++;
        if (currentBeatIndex < currentChapter.storyBeats.Length)
        {
            StartBeat();
        }
        else
        {
            isWaitingForEvent = true;
            transitionHandler.FadeToBlack(() => {
                Debug.Log("Chapter Complete!");
                dialoguePanel.SetActive(false);
            });
        }
    }

    void UpdateCharacterSprites(DialogueLine line)
    {
        foreach (Image slot in characterSlots) { slot.gameObject.SetActive(false); slot.sprite = null; }

        foreach (StageCharacterSetup setup in line.stageCharacters)
        {
            if (setup.expression == null) continue;
            int slotIndex = (int)setup.position;
            Image slot = characterSlots[slotIndex];

            slot.sprite = setup.expression;
            slot.gameObject.SetActive(true);

            Vector3 currentScale = slot.rectTransform.localScale;
            currentScale.x = setup.flipX ? -1 : 1;
            slot.rectTransform.localScale = currentScale;

            slot.color = setup.isTalking ? Color.white : dimmedColor;
        }
    }

    IEnumerator TypeText(DialogueLine line)
    {
        isTyping = true;
        dialogueText.text = "";
        int charCount = 0;

        if (!string.IsNullOrEmpty(line.dialogueText))
        {
            foreach (char letter in line.dialogueText.ToCharArray())
            {
                dialogueText.text += letter;

                if (letter != ' ' && charCount % blipFrequency == 0)
                {
                    if (line.speaker != null && line.speaker.defaultBlipSound != null)
                    {
                        audioHandler.PlayBlip(line.speaker.defaultBlipSound);
                    }
                }

                charCount++;
                yield return new WaitForSeconds(typingSpeed);
            }
        }

        isTyping = false;

        if (isAutoPlay)
        {
            autoPlayCoroutine = StartCoroutine(AutoPlayWait());
        }
        else if (string.IsNullOrWhiteSpace(line.dialogueText))
        {
            HandlePostLineLogic();
        }
    }

    IEnumerator AutoPlayWait()
    {
        yield return new WaitForSeconds(autoPlayDelay);
        HandlePostLineLogic();
    }
}