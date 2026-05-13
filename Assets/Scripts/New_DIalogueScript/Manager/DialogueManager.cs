using DialogSystem.Runtime.Core;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class DialogueManager : MonoBehaviour
{
    [Header("Progression")]
    public int chapterIndex;

    [Header("UI Elements")]
    public GameObject dialoguePanel;
    public Image characterNameBackground;
    public Sprite defaultNameBox; // NEW: Fallback sprite
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;
    public Button advanceButton;

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
    private bool isPaused = false;
    private Coroutine typingCoroutine;
    private Coroutine cinematicSubtitleRoutine;
    private Coroutine cinematicSFXRoutine;

    void Start()
    {
        ClearUI();
        foreach (Animator slot in characterSlots) { slot.gameObject.SetActive(false); }

        if (advanceButton != null)
            advanceButton.onClick.AddListener(OnAdvanceInput);

        if (currentChapter != null) StartChapter(currentChapter);
    }

    public void TogglePause(bool pauseState)
    {
        isPaused = pauseState;
        if (isPaused)
        {
            videoHandler.PauseVideo();
            audioHandler.PauseAudio();
        }
        else
        {
            audioHandler.ResumeAudio();
            DialogueLine line = GetCurrentLine();
            if (line != null && line.lineType == LineType.VideoCutscene)
            {
                videoHandler.ResumeVideo();
            }
        }
    }

    public void OnAdvanceInput()
    {
        if (isPaused || isWaitingForEvent) return;

        DialogueLine line = GetCurrentLine();
        if (line == null || line.lineType != LineType.DialogueAndCharacters) return;

        if (isTyping)
        {
            StopCoroutine(typingCoroutine);
            dialogueText.text = line.dialogueText;
            isTyping = false;
            audioHandler.StopVoiceLine();
            audioHandler.DuckBGM(false);

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
            transitionHandler.FadeToClear(() => { isWaitingForEvent = false; });
        }
    }

    void StartBeat()
    {
        currentLineIndex = 0;
        DisplayLine();
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

    private void SetSpeakerUI(CharacterProfile speaker)
    {
        if (speaker != null)
        {
            nameText.text = speaker.characterName;
            nameText.color = speaker.nameTextColor;

            if (characterNameBackground != null)
            {
                characterNameBackground.sprite = speaker.customDialogueBox != null
                    ? speaker.customDialogueBox
                    : defaultNameBox;
            }
        }
        else
        {
            nameText.text = "";
            if (characterNameBackground != null) characterNameBackground.sprite = defaultNameBox;
        }
    }

    void DisplayLine()
    {
        DialogueLine line = GetCurrentLine();
        if (line == null) return;

        if (line.bgmChange != null) audioHandler.PlayBGM(line.bgmChange);

        switch (line.lineType)
        {
            case LineType.DialogueAndCharacters:
                PreloadNextVideo();
                line.onLineTriggered?.Invoke();
                if (line.voiceLine != null) audioHandler.PlayVoiceLine(line.voiceLine);

                dialoguePanel.SetActive(true);
                SetSpeakerUI(line.speaker);

                UpdateCharacterSprites(line);
                typingCoroutine = StartCoroutine(TypeText(line));
                break;

            case LineType.VideoCutscene:
                ClearUI();
                foreach (Animator slot in characterSlots)
                {
                    slot.gameObject.SetActive(false);
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

    void ClearUI()
    {
        dialoguePanel.SetActive(false);
        nameText.text = "";
        dialogueText.text = "";
        if (characterNameBackground != null) characterNameBackground.sprite = defaultNameBox;
        audioHandler.DuckBGM(false);
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
                    transitionHandler.FadeToClear(() => { isWaitingForEvent = false; });
                }
            });
        }
        else NextLine();
    }

    void PlayVideoWithTransitions(DialogueLine line, System.Action onVideoComplete)
    {
        isWaitingForEvent = true;
        ClearUI();

        transitionHandler.FadeToBlack(() => {
            ClearUI();
            videoHandler.PlayVideo(line.cutsceneVideo,
                onStart: () =>
                {
                    audioHandler.DuckBGM(true);
                    if (line.cinematicSubtitles != null && line.cinematicSubtitles.Length > 0)
                    {
                        if (cinematicSubtitleRoutine != null) StopCoroutine(cinematicSubtitleRoutine);
                        cinematicSubtitleRoutine = StartCoroutine(TrackVideoSubtitles(line.cinematicSubtitles));
                    }

                    if (line.cinematicSFX != null && line.cinematicSFX.Length > 0)
                    {
                        if (cinematicSFXRoutine != null) StopCoroutine(cinematicSFXRoutine);
                        cinematicSFXRoutine = StartCoroutine(TrackVideoSFX(line.cinematicSFX));
                    }

                    float videoLength = (float)line.cutsceneVideo.length;
                    StartCoroutine(WaitAndFadeOutVideo(videoLength, onVideoComplete));
                    transitionHandler.FadeToClear();
                },
                onComplete: () => {
                    audioHandler.DuckBGM(false);
                }
            );
        });
    }

    IEnumerator TrackVideoSFX(TimedSFX[] sfxList)
    {
        List<TimedSFX> sortedSFX = new List<TimedSFX>(sfxList);
        sortedSFX.Sort((a, b) => a.playAtTime.CompareTo(b.playAtTime));
        int currentIndex = 0;

        // Wait for video to actually be prepared and playing, or if it resumed from pause
        while (!videoHandler.videoPlayer.isPlaying && videoHandler.videoPlayer.time == 0 && !isPaused) yield return null;

        // Loop indefinitely. WaitAndFadeOutVideo will explicitly stop this coroutine when the video sequence ends.
        // This prevents 'isPlaying' from returning false and killing the coroutine on Alt-Tab.
        while (true)
        {
            while (isPaused) yield return null;

            if (currentIndex < sortedSFX.Count && videoHandler.videoPlayer.time >= sortedSFX[currentIndex].playAtTime)
            {
                audioHandler.PlaySFX(sortedSFX[currentIndex].sfxClip, sortedSFX[currentIndex].volume);
                currentIndex++;
            }
            yield return null;
        }
    }

    IEnumerator TrackVideoSubtitles(TimedSubtitle[] subtitles)
    {
        List<TimedSubtitle> sortedSubtitles = new List<TimedSubtitle>(subtitles);
        sortedSubtitles.Sort((a, b) => a.showAtTime.CompareTo(b.showAtTime));

        int currentIndex = 0;
        Coroutine showSubRoutine = null;

        // Wait for video to actually be prepared and playing, or if it resumed from pause
        while (!videoHandler.videoPlayer.isPlaying && videoHandler.videoPlayer.time == 0 && !isPaused) yield return null;

        // Loop indefinitely until explicitly stopped by WaitAndFadeOutVideo.
        // This prevents the subtitles from breaking when Alt-Tabbing causes isPlaying to temporarily toggle false.
        while (true)
        {
            while (isPaused) yield return null;

            if (currentIndex < sortedSubtitles.Count)
            {
                if (videoHandler.videoPlayer.time >= sortedSubtitles[currentIndex].showAtTime)
                {
                    if (showSubRoutine != null) StopCoroutine(showSubRoutine);
                    showSubRoutine = StartCoroutine(ShowCinematicSubtitle(sortedSubtitles[currentIndex]));
                    currentIndex++;
                }
            }
            yield return null;
        }
    }

    IEnumerator ShowCinematicSubtitle(TimedSubtitle sub)
    {
        dialoguePanel.SetActive(true);
        SetSpeakerUI(sub.speaker);

        if (sub.voiceLine != null)
        {
            audioHandler.PlayVoiceLine(sub.voiceLine);
            audioHandler.DuckBGM(true);
        }

        dialogueText.text = "";
        int charCount = 0;
        foreach (char letter in sub.dialogueText.ToCharArray())
        {
            while (isPaused) yield return null;
            dialogueText.text += letter;
            if (letter != ' ' && charCount % blipFrequency == 0 && sub.speaker?.defaultBlipSound != null)
                audioHandler.PlayBlip(sub.speaker.defaultBlipSound);
            charCount++;
            yield return new WaitForSeconds(typingSpeed);
        }

        if (sub.hideAfterSeconds > 0)
        {
            float timer = 0;
            while (timer < sub.hideAfterSeconds)
            {
                while (isPaused) yield return null;
                timer += Time.deltaTime;
                yield return null;
            }
            dialoguePanel.SetActive(false);
            dialogueText.text = "";
            audioHandler.DuckBGM(false);
        }
    }

    IEnumerator WaitAndFadeOutVideo(float videoDuration, System.Action onVideoComplete)
    {
        float waitTime = videoDuration - videoFadeHeadstart;
        float elapsed = 0;

        // Use the VideoPlayer's actual time to check when to fade.
        // This completely prevents fade desyncs if the game lags or the user Alt-Tabs.
        // 'elapsed' acts purely as a failsafe timeout if the video player crashes.
        while (videoHandler.videoPlayer.time < waitTime && elapsed < videoDuration + 2.0f)
        {
            while (isPaused) yield return null;
            elapsed += Time.deltaTime;
            yield return null;
        }

        transitionHandler.FadeToBlack(() => {
            if (cinematicSubtitleRoutine != null) StopCoroutine(cinematicSubtitleRoutine);
            if (cinematicSFXRoutine != null) StopCoroutine(cinematicSFXRoutine);
            ClearUI();
            videoHandler.StopAndClearVideo();
            onVideoComplete?.Invoke();

            DialogueLine nextLine = GetCurrentLine();
            if (nextLine != null && nextLine.lineType != LineType.VideoCutscene)
            {
                transitionHandler.FadeToClear(() => { isWaitingForEvent = false; });
            }
            else isWaitingForEvent = false;
        });
    }

    void NextLine()
    {
        currentLineIndex++;
        if (currentLineIndex < currentChapter.storyBeats[currentBeatIndex].lines.Length) DisplayLine();
        else NextBeat();
    }

    void NextBeat()
    {
        currentBeatIndex++;
        if (currentBeatIndex < currentChapter.storyBeats.Length) StartBeat();
        else
        {
            isWaitingForEvent = true;
            transitionHandler.FadeToBlack(() => {
                ClearUI();
                audioHandler.StopBGM();
                CompleteChapter();
            });
        }
    }

    void CompleteChapter()
    {
        isWaitingForEvent = true;
        transitionHandler.FadeToBlack(() => {
            ClearUI();
            audioHandler.StopBGM();

            if (ProgressionManager.Instance != null)
            {
                ProgressionManager.Instance.CompleteCurrentStep(chapterIndex);
            }

            SceneManager.LoadScene("Main_Menu");
        });
    }

    void UpdateCharacterSprites(DialogueLine line)
    {
        foreach (Animator slot in characterSlots)
        {
            slot.gameObject.SetActive(false);
        }

        foreach (StageCharacterSetup setup in line.stageCharacters)
        {
            if (setup.character == null || setup.character.animatorController == null) continue;
            Animator slot = characterSlots[(int)setup.position];
            slot.gameObject.SetActive(true);
            slot.runtimeAnimatorController = setup.character.animatorController;
            slot.Update(0f);
            if (!string.IsNullOrEmpty(setup.expression)) slot.Play(setup.expression, 0, 0f);
            slot.speed = setup.isTalking ? 1f : 0f;

            slot.GetComponent<RectTransform>().localScale = new Vector3(setup.flipX ? -1 : 1, 1, 1);

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

        if (!string.IsNullOrEmpty(line.dialogueText) || line.voiceLine != null)
            audioHandler.DuckBGM(true);

        int charCount = 0;
        if (!string.IsNullOrEmpty(line.dialogueText))
        {
            foreach (char letter in line.dialogueText.ToCharArray())
            {
                while (isPaused) yield return null;
                dialogueText.text += letter;
                if (letter != ' ' && charCount % blipFrequency == 0 && line.speaker?.defaultBlipSound != null)
                    audioHandler.PlayBlip(line.speaker.defaultBlipSound);
                charCount++;
                yield return new WaitForSeconds(typingSpeed);
            }
        }

        isTyping = false;
        if (line.voiceLine == null) audioHandler.DuckBGM(false);

        if (isAutoPlay) autoPlayCoroutine = StartCoroutine(AutoPlayWait());
        else if (string.IsNullOrWhiteSpace(line.dialogueText)) HandlePostLineLogic();
    }

    IEnumerator AutoPlayWait()
    {
        float elapsed = 0;
        while (elapsed < autoPlayDelay)
        {
            while (isPaused) yield return null;
            elapsed += Time.deltaTime;
            yield return null;
        }
        while (audioHandler != null && audioHandler.IsVoicePlaying() || isPaused) yield return null;
        audioHandler.DuckBGM(false);
        HandlePostLineLogic();
    }
}