using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class DialogueManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject dialoguePanel;
    public Image characterNameBackground;
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
    private Coroutine cinematicSubtitleRoutine;

    void Start()
    {
        // GHOST UI FIX 1: Ensure everything is hidden and empty at the very start
        ClearUI();
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

    void PreloadNextVideo()
    {
        int searchLineIndex = currentLineIndex + 1;
        int searchBeatIndex = currentBeatIndex;

        while (searchBeatIndex < currentChapter.storyBeats.Length)
        {
            while (searchLineIndex < currentChapter.storyBeats[searchBeatIndex].lines.Length)
            {
                DialogueLine nextLine = currentChapter.storyBeats[searchBeatIndex].lines[searchLineIndex];
                if (nextLine.lineType == LineType.VideoCutscene && nextLine.cutsceneVideo != null)
                {
                    videoHandler.PrepareVideo(nextLine.cutsceneVideo);
                    return;
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
                PreloadNextVideo();
                line.onLineTriggered?.Invoke();
                if (line.voiceLine != null) audioHandler.PlayVoiceLine(line.voiceLine);

                dialoguePanel.SetActive(true);

                if (line.speaker != null)
                {
                    nameText.text = line.speaker.characterName;
                    nameText.color = line.speaker.nameTextColor;
                    if (line.speaker.customDialogueBox != null)
                        characterNameBackground.sprite = line.speaker.customDialogueBox;
                }
                else
                {
                    nameText.text = "";
                }

                UpdateCharacterSprites(line);
                typingCoroutine = StartCoroutine(TypeText(line));
                break;

            case LineType.VideoCutscene:
                // GHOST UI FIX 2: Clear UI immediately before the video transition logic even starts
                ClearUI();
                foreach (Animator slot in characterSlots)
                {
                    slot.gameObject.SetActive(false);
                    slot.runtimeAnimatorController = null;
                }

                PlayVideoWithTransitions(line, NextLine);
                break;

            case LineType.LogicHookOnly:
                PreloadNextVideo();
                line.onLineTriggered?.Invoke();
                NextLine();
                break;
        }
    }

    // Helper to completely clear and hide the Dialogue UI
    void ClearUI()
    {
        dialoguePanel.SetActive(false);
        nameText.text = "";
        dialogueText.text = "";
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

    void PlayVideoWithTransitions(DialogueLine line, System.Action onVideoComplete)
    {
        isWaitingForEvent = true;

        // GHOST UI FIX 3: Clear UI BEFORE starting the fade to black
        ClearUI();

        transitionHandler.FadeToBlack(() => {
            // Re-confirm hidden state just in case
            ClearUI();

            videoHandler.PlayVideo(line.cutsceneVideo,
                onStart: () =>
                {
                    if (line.cinematicSubtitles != null && line.cinematicSubtitles.Length > 0)
                    {
                        if (cinematicSubtitleRoutine != null) StopCoroutine(cinematicSubtitleRoutine);
                        cinematicSubtitleRoutine = StartCoroutine(TrackVideoSubtitles(line.cinematicSubtitles));
                    }

                    float timeToWait = (float)line.cutsceneVideo.length - videoFadeHeadstart;
                    if (timeToWait < 0) timeToWait = 0;

                    StartCoroutine(WaitAndFadeOutVideo(timeToWait, onVideoComplete));
                    transitionHandler.FadeToClear();
                },
                onComplete: null
            );
        });
    }

    IEnumerator TrackVideoSubtitles(TimedSubtitle[] subtitles)
    {
        List<TimedSubtitle> sortedSubtitles = new List<TimedSubtitle>(subtitles);
        sortedSubtitles.Sort((a, b) => a.showAtTime.CompareTo(b.showAtTime));

        int currentIndex = 0;
        Coroutine showSubRoutine = null;

        // GHOST UI FIX 4: Ensure panel is off before we start the video playback loop
        dialoguePanel.SetActive(false);

        while (videoHandler.videoPlayer.isPlaying || videoHandler.videoPlayer.isPrepared)
        {
            double videoTime = videoHandler.videoPlayer.time;

            if (currentIndex < sortedSubtitles.Count && videoTime >= sortedSubtitles[currentIndex].showAtTime)
            {
                if (showSubRoutine != null) StopCoroutine(showSubRoutine);
                showSubRoutine = StartCoroutine(ShowCinematicSubtitle(sortedSubtitles[currentIndex]));
                currentIndex++;
            }
            yield return null;
        }

        dialoguePanel.SetActive(false);
    }

    IEnumerator ShowCinematicSubtitle(TimedSubtitle sub)
    {
        dialoguePanel.SetActive(true);
        nameText.text = sub.speaker != null ? sub.speaker.characterName : "";
        if (sub.speaker != null) nameText.color = sub.speaker.nameTextColor;

        if (sub.speaker != null && sub.speaker.customDialogueBox != null)
            characterNameBackground.sprite = sub.speaker.customDialogueBox;

        if (sub.voiceLine != null) audioHandler.PlayVoiceLine(sub.voiceLine);

        dialogueText.text = "";
        int charCount = 0;

        foreach (char letter in sub.dialogueText.ToCharArray())
        {
            dialogueText.text += letter;
            if (letter != ' ' && charCount % blipFrequency == 0)
            {
                if (sub.speaker != null && sub.speaker.defaultBlipSound != null)
                    audioHandler.PlayBlip(sub.speaker.defaultBlipSound);
            }
            charCount++;
            yield return new WaitForSeconds(typingSpeed);
        }

        if (sub.hideAfterSeconds > 0)
        {
            yield return new WaitForSeconds(sub.hideAfterSeconds);
            dialoguePanel.SetActive(false);
            dialogueText.text = "";
        }
    }

    IEnumerator WaitAndFadeOutVideo(float waitTime, System.Action onVideoComplete)
    {
        yield return new WaitForSeconds(waitTime);

        transitionHandler.FadeToBlack(() => {
            if (cinematicSubtitleRoutine != null) StopCoroutine(cinematicSubtitleRoutine);
            ClearUI();
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
                ClearUI();
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
                        audioHandler.PlayBlip(line.speaker.defaultBlipSound);
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