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

    [Tooltip("Assign the 5 UI slots here. They MUST have an Animator component attached!")]
    public Animator[] characterSlots;

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
        foreach (Animator slot in characterSlots) { slot.gameObject.SetActive(false); }
        if (currentChapter != null) StartChapter(currentChapter);
    }

    public void StartChapter(StoryChapter chapter)
    {
        currentChapter = chapter;
        currentBeatIndex = 0;
        currentLineIndex = 0;

        DialogueLine firstLine = GetCurrentLine();

        if (firstLine != null && firstLine.lineType == LineType.VideoCutscene)
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
            if (line == null) return;

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

    DialogueLine GetCurrentLine()
    {
        if (currentChapter == null || currentBeatIndex >= currentChapter.storyBeats.Length) return null;
        if (currentLineIndex >= currentChapter.storyBeats[currentBeatIndex].lines.Length) return null;
        return currentChapter.storyBeats[currentBeatIndex].lines[currentLineIndex];
    }

    // --- NEW: THE LOOKAHEAD SYSTEM ---
    void PreloadNextVideo()
    {
        int searchLineIndex = currentLineIndex + 1;
        int searchBeatIndex = currentBeatIndex;

        // Scan the upcoming lines in the background
        while (searchBeatIndex < currentChapter.storyBeats.Length)
        {
            while (searchLineIndex < currentChapter.storyBeats[searchBeatIndex].lines.Length)
            {
                DialogueLine nextLine = currentChapter.storyBeats[searchBeatIndex].lines[searchLineIndex];

                // The moment we spot an upcoming video, tell the VideoPlayer to prepare it instantly!
                if (nextLine.lineType == LineType.VideoCutscene)
                {
                    if (nextLine.cutsceneVideo != null)
                    {
                        videoHandler.PrepareVideo(nextLine.cutsceneVideo);
                    }
                    return; // Stop searching once we find the next video
                }
                searchLineIndex++;
            }
            searchBeatIndex++;
            searchLineIndex = 0;
        }
    }

    void DisplayLine()
    {
        DialogueLine line = GetCurrentLine();
        if (line == null) return;

        switch (line.lineType)
        {
            case LineType.DialogueAndCharacters:

                // Tell the VideoPlayer to pre-load upcoming videos while the player reads!
                PreloadNextVideo();

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
                foreach (Animator slot in characterSlots)
                {
                    slot.gameObject.SetActive(false);
                    slot.runtimeAnimatorController = null;
                }
                dialoguePanel.SetActive(false);

                PlayVideoWithTransitions(line.cutsceneVideo, NextLine);
                break;

            case LineType.LogicHookOnly:
                // Preload here too just in case!
                PreloadNextVideo();
                line.onLineTriggered?.Invoke();
                NextLine();
                break;
        }
    }

    void HandlePostLineLogic()
    {
        DialogueLine line = GetCurrentLine();
        if (line == null) return;

        if (line.lineType == LineType.DialogueAndCharacters && line.fadeOutAfterLine)
        {
            isWaitingForEvent = true;
            transitionHandler.FadeToBlack(() => {

                NextLine();
                DialogueLine nextLine = GetCurrentLine();

                if (nextLine != null && nextLine.lineType != LineType.VideoCutscene)
                {
                    transitionHandler.FadeToClear(() => {
                        isWaitingForEvent = false;
                    });
                }
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

            // Tell the Video Handler to play, but provide an "OnStart" callback
            // so we don't start our timers until the video has successfully buffered!
            videoHandler.PlayVideo(clip,
                onStart: () =>
                {
                    // Video has officially started playing! Now we can fade in.
                    float timeToWait = (float)clip.length - videoFadeHeadstart;
                    if (timeToWait < 0) timeToWait = 0;

                    StartCoroutine(WaitAndFadeOutVideo(timeToWait, onVideoComplete));
                    transitionHandler.FadeToClear();
                },
                onComplete: null
            );
        });
    }

    IEnumerator WaitAndFadeOutVideo(float waitTime, System.Action onVideoComplete)
    {
        yield return new WaitForSeconds(waitTime);

        transitionHandler.FadeToBlack(() => {
            videoHandler.StopAndClearVideo();
            onVideoComplete?.Invoke();

            DialogueLine nextLine = GetCurrentLine();
            if (nextLine != null && nextLine.lineType != LineType.VideoCutscene)
            {
                transitionHandler.FadeToClear(() => {
                    isWaitingForEvent = false;
                });
            }
            else
            {
                isWaitingForEvent = false;
            }
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
        foreach (Animator slot in characterSlots)
        {
            slot.gameObject.SetActive(false);
            slot.runtimeAnimatorController = null;
        }

        foreach (StageCharacterSetup setup in line.stageCharacters)
        {
            if (setup.character == null || setup.character.animatorController == null) continue;

            int slotIndex = (int)setup.position;
            Animator slot = characterSlots[slotIndex];

            slot.gameObject.SetActive(true);
            slot.runtimeAnimatorController = setup.character.animatorController;
            slot.Update(0f);

            if (!string.IsNullOrEmpty(setup.expression))
            {
                slot.Play(setup.expression, 0, 0f);
            }

            slot.speed = setup.isTalking ? 1f : 0f;

            RectTransform rect = slot.GetComponent<RectTransform>();
            Vector3 currentScale = rect.localScale;
            currentScale.x = setup.flipX ? -1 : 1;
            rect.localScale = currentScale;

            Image img = slot.GetComponent<Image>();
            if (img != null)
            {
                img.color = setup.isTalking ? Color.white : dimmedColor;
                img.preserveAspect = true;
            }
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

        while (audioHandler != null && audioHandler.IsVoicePlaying())
        {
            yield return null;
        }

        HandlePostLineLogic();
    }
}