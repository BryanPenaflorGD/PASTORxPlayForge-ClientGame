using DialogSystem.Runtime.Core.Effects;
using DialogSystem.Runtime.Interfaces;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Settings;
using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.Runtime.UI;
using DialogSystem.Runtime.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DialogSystem.Runtime.Core
{
    [DisallowMultipleComponent]
    public class DialogManager : MonoSingleton<DialogManager>
    {
        #region ---------------- Inspector: Debug & Scene References ----------------
        [Header("Debug")]
        [SerializeField] private bool doDebug = true;

        [Serializable]
        public class DialogGraphModel
        {
            [Tooltip("Graph asset to use for this conversation id.")]
            public DialogGraph dialogGraph;
            [Tooltip("String id used to play this conversation via PlayDialogByID().")]
            public string dialogID;
        }

        [Header("Dialog Graphs List")]
        public List<DialogGraphModel> dialogGraphs = new List<DialogGraphModel>();

        [Header("UI Panel Reference")]
        public DialogUIController uiPanel;

        [Header("Audio (Scene Reference)")]
        public AudioSource audioSource;
        #endregion

        #region ---------------- Inspector: Local Overrides (Optional) ----------------
        [Header("Overrides (Optional)")]
        [SerializeField] private DialogTextSettings localTextSettings;
        [SerializeField] private DialogAudioSettings localAudioSettings;
        #endregion

        #region ---------------- Events ----------------
        public Action onDialogEnter;
        public Action onDialogExit;
        public event Action<string, string, string> OnLineShown;
        public event Action<string, string> OnChoicePicked;
        public event Action OnConversationReset;
        #endregion

        #region ---------------- Optional Actions ----------------
        [Header("Optional Actions Runner (leave null to disable)")]
        public DialogActionRunner actionRunner;
        #endregion

        #region ---------------- State ----------------
        // Graph/node state
        private DialogGraph currentGraph;
        private string currentDialogID = null;
        private string currentGuid;
        private DialogNode currentDialog;
        private ChoiceNode currentChoice;

        private ChoiceNode pendingChoiceFromDialog;
        private string pendingNextGuidAfterDialog;

        // Coroutines
        private Coroutine typingCoroutine;
        private Coroutine autoAdvanceCoroutine;
        private Coroutine audioFadeCoroutine;

        // Flags
        private bool isTyping = false;
        private bool conversationActive = false;
        private bool isPausedByHistory = false;

        // Prevent user advance while action chain runs (you already added this earlier).
        private bool _isWaitingForActions = false;
        private float _genericCheckTimer = 0;

        // Effective settings
        private DialogTextSettings dialogTextSettings;
        private DialogAudioSettings dialogAudioSettings;
        private DialogChoiceSettings choiceSettings;

        // Autoplay (instance-level)
        private bool autoPlayState;

        // Choices/UI state
        private readonly System.Collections.Generic.List<ChoiceButtonView> _choiceViews = new();
        private int _selectedChoiceIndex = -1;

        // Typing control
        private ITextRevealEffect _activeEffect;
        private int _typingEpoch = 0;

        private Action OnDialogEndedCallback;
        #endregion

        #region ---------------- Unity ----------------
        protected override void Awake()
        {
            ResolveEffectiveSettings();

            if (uiPanel != null)
            {
                uiPanel.panelRoot?.SetActive(false);
                uiPanel.skipButton?.SetActive(false);
                uiPanel.UpdateAutoPlayIcon(autoPlayState);
            }

            if (audioSource && dialogAudioSettings != null) audioSource.volume = dialogAudioSettings.sfxVolume;

            doDebug = doDebug ? true : DialogSettingsRuntime.DoDebug();

            if (doDebug && DialogSettingsRuntime.Master?.enableDebugLogs == true)
                Debug.Log("[DialogManager] Awake: effective settings resolved.");
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SafeStopAllRuntimeActivity();
        }

        private void OnDestroy()
        {
            SafeStopAllRuntimeActivity();
        }

        private void OnActiveSceneChanged(Scene prev, Scene next)
        {
            if (doDebug) Debug.Log($"[DialogManager] Scene changed: {prev.name} -> {next.name}. Stopping all coroutines & audio.");
            SafeStopAllRuntimeActivity();
        }

        private void Update()
        {
            if (!conversationActive || isPausedByHistory) return;

            // block any input while actions are resolving
            if (_isWaitingForActions) return;

            // Choice overlay navigation takes priority
            if (IsChoiceOverlayActive())
            {
                HandleChoiceNavigation();
                return;
            }

            // Generic advance
            _genericCheckTimer += Time.deltaTime;
            if (_genericCheckTimer >= 0.5 && InputHelper.CheckGenericAdvanceInput())
            {
                _genericCheckTimer = 0f;
                OnDialogAreaClick();
            }
        }
        #endregion

        #region ---------------- Public API ----------------
        public void PlayDialogByID(string targetDialogID, Action onDialogEnded = null)
        {
            currentDialog = null;
            var target = dialogGraphs.Find(d => d.dialogID == targetDialogID && d.dialogGraph != null);
            if (target != null)
            {
                StartDialog(target.dialogGraph, onDialogEnded);
                currentDialogID = targetDialogID;
            }
            else if (doDebug)
            {
                Debug.LogWarning($"[DialogManager] No dialog found for id: {targetDialogID}");
            }
        }

        public void StartDialog(DialogGraph graph, Action onDialogEnded = null)
        {
            if (uiPanel == null)
            {
                if (doDebug) Debug.LogError("[DialogManager] UI Panel not assigned.");
                return;
            }

            uiPanel.panelRoot?.SetActive(false);
            uiPanel.skipButton?.SetActive(false);

            currentGraph = graph;
            currentGuid = null;
            currentDialog = null;
            currentChoice = null;
            pendingChoiceFromDialog = null;
            pendingNextGuidAfterDialog = null;

            currentGuid = ResolveEntryGuid(graph);

            if (doDebug && currentGuid != null)
                Debug.Log($"[DialogManager] Starting dialog: {graph.name} entry={currentGuid}");

            uiPanel.panelRoot?.SetActive(true);
            uiPanel.skipButton?.SetActive(CanSkipAll());

            conversationActive = true;
            OnDialogEndedCallback = onDialogEnded;

            OnConversationReset?.Invoke();
            onDialogEnter?.Invoke();

            GoTo(currentGuid);
        }

        public bool ToggleAutoPlay()
        {
            autoPlayState = !autoPlayState;
            uiPanel?.UpdateAutoPlayIcon(autoPlayState);
            return autoPlayState;
        }

        public void SkipAll()
        {
            if (!conversationActive || !CanSkipAll()) return;

            if (ShouldStopOnSkipAll()) StopAudio(ShouldFadeOutOnStop());
            StopImmediately();
        }

        public Coroutine InvokeGlobalAction(string actionId, string payloadJson = "", bool waitForCompletion = false, float preDelaySeconds = 0f)
        {
            if (actionRunner == null) { WarnOnceNoRunner(); return null; }
            return StartCoroutine(actionRunner.RunActionGlobal(actionId, payloadJson, waitForCompletion, preDelaySeconds));
        }

        public Coroutine InvokeConversationAction(string dialogId, string actionId, string payloadJson = "", bool waitForCompletion = false, float preDelaySeconds = 0f)
        {
            if (actionRunner == null) { WarnOnceNoRunner(); return null; }
            return StartCoroutine(actionRunner.RunActionForConversation(dialogId, actionId, payloadJson, waitForCompletion, preDelaySeconds));
        }
        #endregion

        #region ---------------- Core Flow ----------------
        private void GoTo(string guid)
        {
            SafeStopTyping();
            CancelAutoAdvance();
            StopAudioImmediate();

            pendingChoiceFromDialog = null;
            pendingNextGuidAfterDialog = null;

            if (string.IsNullOrEmpty(guid)) { EndDialog(); return; }

            // Action chain
            var act = FindActionByGuid(guid);
            if (act != null)
            {
                // Lock user advance while we process actions (which may include waits)
                _isWaitingForActions = true;

                StartCoroutine(ResolveNextAfterActions(guid, resolved =>
                {
                    // Unlock once we've resolved to a non-action destination or the end
                    _isWaitingForActions = false;

                    if (string.IsNullOrEmpty(resolved)) { EndDialog(); return; }

                    var d = FindDialogByGuid(resolved);
                    if (d != null)
                    {
                        currentGuid = resolved;
                        currentDialog = d;
                        currentChoice = null;
                        ShowCurrentNode();
                        return;
                    }

                    var c = FindChoiceByGuid(resolved);
                    if (c != null)
                    {
                        currentGuid = resolved;
                        currentDialog = null;
                        currentChoice = c;
                        ShowCurrentNode();
                        return;
                    }

                    EndDialog();
                }));
                return;
            }

            // Dialog or Choice
            currentGuid = guid;
            currentDialog = FindDialogByGuid(guid);
            currentChoice = (currentDialog == null) ? FindChoiceByGuid(guid) : null;

            if (currentDialog == null && currentChoice == null) { EndDialog(); return; }
            ShowCurrentNode();
        }

        private void ShowCurrentNode()
        {
            if (currentDialog == null && currentChoice == null) { EndDialog(); return; }

            if (uiPanel != null)
            {
                if (currentDialog != null)
                {
                    if (uiPanel.speakerName != null) uiPanel.speakerName.text = currentDialog.speakerName;
                    if (uiPanel.portraitImage != null) uiPanel.portraitImage.sprite = currentDialog.speakerPortrait;
                }
                else
                {
                    if (uiPanel.speakerName != null) uiPanel.speakerName.text = "";
                    if (uiPanel.portraitImage != null) uiPanel.portraitImage.sprite = null;
                }
            }

            var shownText = currentDialog != null ? currentDialog.questionText : currentChoice.text;
            var speaker = currentDialog != null ? currentDialog.speakerName : string.Empty;
            OnLineShown?.Invoke(currentGuid, speaker, shownText);

            if (currentDialog != null) PlayLineAudio(currentDialog.dialogAudio);

            SafeStopTyping();
            StartTyping(shownText);
        }

        private void StartTyping(string line)
        {
            if (uiPanel?.dialogText == null)
                return;

            // Build effect (null => instant)
            var effect = CreateRevealEffect(line);

            if (effect == null)
            {
                uiPanel.dialogText.text = line;
                isTyping = false;
                HandleAfterTyping();
                return;
            }

            // Cancel any previous effect/coroutine
            SafeStopTyping();

            _activeEffect = effect;
            isTyping = true;

            _typingEpoch++;
            int epoch = _typingEpoch;
            typingCoroutine = StartCoroutine(RunEffect(effect, epoch));
        }


        private IEnumerator RunEffect(ITextRevealEffect effect, int epoch)
        {
            yield return effect.Play();

            // If cancelled or superseded by a newer epoch, do nothing.
            if (effect.IsCancelled || epoch != _typingEpoch)
                yield break;

            isTyping = false;
            _activeEffect = null;
            typingCoroutine = null;

            // Normal completion path
            HandleAfterTyping();
        }

        private void HandleAfterTyping()
        {
            if (isPausedByHistory) return;

            if (currentChoice != null) 
            { 
                ShowChoices(currentChoice); 
                return; 
            }

            if (currentDialog != null)
            {
                var nextDirect = GetNextFromDialog(currentDialog.GetGuid());

                StartCoroutine(ResolveNextAfterActions(nextDirect, resolvedNext =>
                {
                    if (string.IsNullOrEmpty(resolvedNext))
                    {
                        if (autoPlayState)
                        {
                            CancelAutoAdvance();
                            autoAdvanceCoroutine = StartCoroutine(AutoEndAfterDelay(currentDialog));
                        }
                        return;
                    }

                    var choice = FindChoiceByGuid(resolvedNext);
                    if (choice != null)
                    {
                        pendingChoiceFromDialog = choice;
                        ShowChoices(choice);
                        return;
                    }

                    pendingNextGuidAfterDialog = resolvedNext;

                    if (autoPlayState)
                    {
                        CancelAutoAdvance();
                        autoAdvanceCoroutine = StartCoroutine(AutoAdvanceAfterDelay(pendingNextGuidAfterDialog, currentDialog));
                    }
                }));
            }
        }

        private void SafeStopTyping()
        {
            // Stop coroutine if running
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }

            // Cancel & snap effect to full text
            if (_activeEffect != null)
            {
                _activeEffect.Cancel();
                _activeEffect.CompleteImmediately();
                _activeEffect = null;
            }

            isTyping = false;
        }


        #endregion

        #region ---------------- Choices ----------------

        private void ShowChoices(ChoiceNode cnode)
        {
            if (uiPanel == null || uiPanel.choicesContainer == null || uiPanel.choiceButtonPrefab == null) return;

            // Clear local references & UI
            _choiceViews.Clear();
            uiPanel.ClearChoices();

            // Rebuild via UI controller
            uiPanel.BuildChoices(cnode, choiceSettings, OnChoiceSelected);

            // Cache references for selection visuals
            for (int i = 0; i < uiPanel.choicesContainer.childCount; i++)
            {
                var v = uiPanel.choicesContainer.GetChild(i).GetComponent<ChoiceButtonView>();
                if (v != null) _choiceViews.Add(v);
            }

            _selectedChoiceIndex = (choiceSettings.selectFirstOnEnable && _choiceViews.Count > 0) ? 0 : -1;
            ApplyChoiceHighlight();
        }

        // Navigation (called from Update when IsChoiceOverlayActive())
        private void HandleChoiceNavigation()
        {
            if (InputHelper.WasMoveDownPressedThisFrame()) { MoveChoice(+1); ApplyChoiceHighlight(); return; }
            if (InputHelper.WasMoveUpPressedThisFrame()) { MoveChoice(-1); ApplyChoiceHighlight(); return; }

            for (int n = 1; n <= 9; n++)
                if (InputHelper.WasNumberKeyPressedThisFrame(n)) { OnChoiceSelected(n - 1); return; }

            if (InputHelper.WasSubmitPressedThisFrame() ||
                InputHelper.WasLetterPressedThisFrame(choiceSettings.keyboardConfirmLetter[0]))
            {
                int pick = _selectedChoiceIndex;
                if (pick < 0 && _choiceViews.Count > 0) pick = 0;
                if (pick >= 0 && pick < _choiceViews.Count) { OnChoiceSelected(pick); return; }
            }
        }

        private void MoveChoice(int delta)
        {
            int count = _choiceViews.Count;
            if (count == 0) return;

            if (_selectedChoiceIndex < 0) { _selectedChoiceIndex = 0; return; }

            int next = _selectedChoiceIndex + delta;
            if (choiceSettings.wrapNavigation)
            {
                if (next < 0) next = count - 1;
                if (next >= count) next = 0;
            }
            else next = Mathf.Clamp(next, 0, count - 1);

            _selectedChoiceIndex = next;
        }

        private void ApplyChoiceHighlight()
        {
            string hint = (choiceSettings != null && choiceSettings.showKeyHints && choiceSettings.enableKeyboardConfirmKey)
                ? $"{choiceSettings.keyboardConfirmLetter}"
                : string.Empty;

            for (int i = 0; i < _choiceViews.Count; i++)
            {
                if(_choiceViews[i] != null)
                    _choiceViews[i].ApplySelected(i == _selectedChoiceIndex, hint);
            }
        }

        public void SelectChoiceIndex(int index)
        {
            if (index < 0 || index >= _choiceViews.Count) return;
            _selectedChoiceIndex = index;
            ApplyChoiceHighlight();
        }

        private bool IsChoiceOverlayActive()
        {
            if (uiPanel?.choicesContainer == null) return false;
            return uiPanel.choicesContainer.gameObject.activeInHierarchy && (_choiceViews.Count > 0);
        }

        public void OnChoiceSelected(int index)
        {
            var choiceNode = pendingChoiceFromDialog != null ? pendingChoiceFromDialog : currentChoice;
            if (choiceNode == null) return;
            if (index < 0 || index >= choiceNode.choices.Count) return;

            var picked = choiceNode.choices[index];
            OnChoicePicked?.Invoke(choiceNode.GetGuid(), picked.answerText);
            picked.onSelected?.Invoke();

            var nextGUID = GetNextFromChoice(choiceNode.GetGuid(), index);

            CancelAutoAdvance();
            StopAudioImmediate();

            // Hide choices but DO NOT clear dialog text here.
            uiPanel.SetChoicesVisible(false);
            uiPanel.ClearChoices();
            _choiceViews.Clear();
            pendingChoiceFromDialog = null;

            if (!string.IsNullOrEmpty(nextGUID)) GoTo(nextGUID);
            else EndDialog();
        }


        #endregion

        #region ---------------- Click Handling ----------------
        public void OnDialogAreaClick()
        {
            if (!conversationActive || isPausedByHistory) return;

            // block any input while actions are resolving
            if (_isWaitingForActions) return;

            // When choices visible, rely on buttons/navigation (avoid accidental skip)
            if (IsChoiceOverlayActive()) return;

            if (isTyping)
            {
                if (!CanSkipCurrentLine()) return;

                SafeStopTyping(); // snaps to full text via CompleteImmediately()
                var full = currentDialog != null ? currentDialog.questionText : currentChoice?.text ?? string.Empty;
                if (uiPanel?.dialogText != null) uiPanel.dialogText.text = full;

                if (ShouldStopOnSkipLine() && currentDialog != null) StopAudio(ShouldFadeOutOnStop());

                CancelAutoAdvance();
                HandleAfterTyping();
                return;
            }


            if (currentDialog != null)
            {
                if (!string.IsNullOrEmpty(pendingNextGuidAfterDialog))
                {
                    var next = pendingNextGuidAfterDialog;
                    pendingNextGuidAfterDialog = null;
                    GoTo(next);
                    return;
                }

                var nextGuid = GetNextFromDialog(currentDialog.GetGuid());
                if (!string.IsNullOrEmpty(nextGuid)) { GoTo(nextGuid); return; }
            }

            EndDialog();
        }
        #endregion

        #region ---------------- Auto-Advance ----------------
        private IEnumerator AutoAdvanceAfterDelay(string nextGuid, DialogNode nodeForTiming)
        {
            float wait = (nodeForTiming == null || nodeForTiming.displayTime < 0.01f)
                ? AutoAdvanceDelay()
                : nodeForTiming.displayTime;

            yield return new WaitForSeconds(wait);
            autoAdvanceCoroutine = null;

            if (!isPausedByHistory && !string.IsNullOrEmpty(nextGuid)) GoTo(nextGuid);
        }

        private IEnumerator AutoEndAfterDelay(DialogNode nodeForTiming)
        {
            float wait = (nodeForTiming == null || nodeForTiming.displayTime < 0.01f)
                ? AutoAdvanceDelay()
                : nodeForTiming.displayTime;

            yield return new WaitForSeconds(wait);
            autoAdvanceCoroutine = null;

            if (!isPausedByHistory) EndDialog();
        }
        #endregion

        #region ---------------- Stop / End ----------------
        public void StopImmediately()
        {
            SafeStopTyping();
            CancelAutoAdvance();
            EndDialog();
        }

        private void EndDialog()
        {
            conversationActive = false;

            CancelAutoAdvance();
            SafeStopTyping();
            StopAudio(ShouldFadeOutOnStop());

            if (uiPanel != null)
            {
                uiPanel.panelRoot?.SetActive(false);
                if (uiPanel.dialogText != null) uiPanel.dialogText.text = "";
                if (uiPanel.speakerName != null) uiPanel.speakerName.text = "";
                if (uiPanel.portraitImage != null) uiPanel.portraitImage.sprite = null;
            }

            pendingChoiceFromDialog = null;
            pendingNextGuidAfterDialog = null;

            OnConversationReset?.Invoke();
            onDialogExit?.Invoke();
            OnDialogEndedCallback?.Invoke();
            OnDialogEndedCallback = null;
        }
        #endregion

        #region ---------------- Graph Helpers ----------------
        private DialogNode FindDialogByGuid(string guid)
        {
            if (currentGraph?.nodes == null) return null;
            return currentGraph.nodes.FirstOrDefault(n => n != null && n.GetGuid() == guid);
        }

        private ChoiceNode FindChoiceByGuid(string guid)
        {
            if (currentGraph?.choiceNodes == null) return null;
            return currentGraph.choiceNodes.FirstOrDefault(n => n != null && n.GetGuid() == guid);
        }

        private ActionNode FindActionByGuid(string guid)
        {
            if (currentGraph?.actionNodes == null) return null;
            return currentGraph.actionNodes.FirstOrDefault(n => n != null && n.GetGuid() == guid);
        }

        private string ResolveEntryGuid(DialogGraph graph)
        {
            if (graph == null) return null;

            if (!string.IsNullOrEmpty(graph.startGuid))
            {
                var next = GetFirstOutgoingTarget(graph, graph.startGuid);
                if (!string.IsNullOrEmpty(next)) return next;

                if (doDebug) Debug.LogWarning("[DialogManager] Start node has no outgoing link. Connect Start → first node.");
                return null;
            }

            if (doDebug) Debug.LogWarning("[DialogManager] startGuid is empty. Set it in the graph (Start node).");
            return null;
        }

        private static string GetFirstOutgoingTarget(DialogGraph graph, string fromGuid)
        {
            if (graph?.links == null || string.IsNullOrEmpty(fromGuid)) return null;

            var link = graph.links
                .Where(l => l != null && l.fromGuid == fromGuid)
                .OrderBy(l => l.fromPortIndex)
                .FirstOrDefault();

            return link?.toGuid;
        }

        private string GetNextFromDialog(string guid)
        {
            if (string.IsNullOrEmpty(guid) || currentGraph?.links == null) return null;
            var link = currentGraph.links.FirstOrDefault(l => l.fromGuid == guid && l.fromPortIndex == 0);
            return link?.toGuid;
        }

        private string GetNextFromChoice(string guid, int choiceIndex)
        {
            if (string.IsNullOrEmpty(guid) || currentGraph?.links == null) return null;
            var link = currentGraph.links.FirstOrDefault(l => l.fromGuid == guid && l.fromPortIndex == choiceIndex);
            return link?.toGuid;
        }

        private string GetNextFromAction(string guid)
        {
            if (string.IsNullOrEmpty(guid) || currentGraph?.links == null) return null;
            var link = currentGraph.links.FirstOrDefault(l => l.fromGuid == guid && l.fromPortIndex == 0);
            return link?.toGuid;
        }
        #endregion

        #region ---------------- Helpers ----------------

        private void CancelAutoAdvance()
        {
            if (autoAdvanceCoroutine != null)
            {
                StopCoroutine(autoAdvanceCoroutine);
                autoAdvanceCoroutine = null;
            }
        }

        private void SafeStopAllRuntimeActivity()
        {
            SafeStopTyping();
            CancelAutoAdvance();
            if (audioFadeCoroutine != null) { StopCoroutine(audioFadeCoroutine); audioFadeCoroutine = null; }
            try { StopAllCoroutines(); } catch { }
            StopAudioImmediate();
            pendingChoiceFromDialog = null;
            pendingNextGuidAfterDialog = null;
        }
        #endregion

        #region ---------------- Actions Traversal ----------------
        private IEnumerator ResolveNextAfterActions(string nextDirect, Action<string> onResolved)
        {
            string cursor = nextDirect;

            while (!string.IsNullOrEmpty(cursor))
            {
                var act = FindActionByGuid(cursor);
                if (act == null) break;

                if (doDebug)
                    Debug.Log($"[DialogManager] Action '{act.actionId}' wait={act.waitForCompletion} delay={act.waitSeconds}");

                if (actionRunner != null)
                    yield return StartCoroutine(actionRunner.RunAction(act, currentDialogID));

                cursor = GetNextFromAction(act.GetGuid());
            }

            onResolved?.Invoke(cursor);
        }
        #endregion

        #region ---------------- Audio ----------------
        private void PlayLineAudio(AudioClip clip)
        {
            if (audioSource == null) return;

            if (doDebug)
                Debug.Log($"[DialogManager] Play audio: {(clip ? clip.name : "null")} for node {(currentDialog?.name ?? "n/a")}");

            StopAudioImmediate();
            if (clip == null) return;

            audioSource.clip = clip;
            audioSource.time = 0f;
            audioSource.Play();
        }

        private void StopAudio(bool withFade)
        {
            if (audioSource == null || !audioSource.isPlaying) return;

            float fadeTime = FadeOutTime();
            if (!withFade || fadeTime <= 0f)
            {
                StopAudioImmediate();
                return;
            }

            if (audioFadeCoroutine != null) StopCoroutine(audioFadeCoroutine);
            audioFadeCoroutine = StartCoroutine(FadeOutAudio(fadeTime));
        }

        private void StopAudioImmediate()
        {
            if (audioFadeCoroutine != null)
            {
                StopCoroutine(audioFadeCoroutine);
                audioFadeCoroutine = null;
            }

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
                if (dialogAudioSettings != null) audioSource.volume = dialogAudioSettings.sfxVolume;
            }
        }

        private IEnumerator FadeOutAudio(float duration)
        {
            if (audioSource == null || !audioSource.isPlaying)
            {
                StopAudioImmediate();
                yield break;
            }

            float startVol = audioSource.volume;
            float t = 0f;

            while (t < duration && audioSource != null && audioSource.isPlaying)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / duration);
                audioSource.volume = startVol * k;
                yield return null;
            }

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
                audioSource.volume = startVol;
            }

            audioFadeCoroutine = null;
        }
        #endregion

        #region ---------------- History ----------------
        public void PauseForHistory()
        {
            isPausedByHistory = true;

            if (isTyping && typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;

                if (uiPanel?.dialogText != null)
                {
                    uiPanel.dialogText.text = currentDialog != null
                        ? currentDialog.questionText
                        : currentChoice != null ? currentChoice.text
                        : string.Empty;
                }

                isTyping = false;
            }

            CancelAutoAdvance();
        }

        public void ResumeAfterHistory() => isPausedByHistory = false;

        public string GetCurrentLineText()
        {
            if (currentDialog != null) return currentDialog.questionText ?? string.Empty;
            if (currentChoice != null) return currentChoice.text ?? string.Empty;
            return string.Empty;
        }

        public string GetCurrentGuid() => currentGuid;
        #endregion

        #region ---------------- Effective Settings ----------------
        private void ResolveEffectiveSettings()
        {
            dialogTextSettings = localTextSettings != null ? localTextSettings : DialogSettingsRuntime.Text;
            if (dialogTextSettings == null)
            {
                dialogTextSettings = ScriptableObject.CreateInstance<DialogTextSettings>();
                if (doDebug) Debug.LogWarning("[DialogManager] No TextSettings found. Created temporary default instance.");
            }

            dialogAudioSettings = localAudioSettings != null ? localAudioSettings : DialogSettingsRuntime.Audio;
            if (dialogAudioSettings == null)
            {
                dialogAudioSettings = ScriptableObject.CreateInstance<DialogAudioSettings>();
                if (doDebug) Debug.LogWarning("[DialogManager] No AudioSettings found. Created temporary default instance.");
            }

            // Choice settings from master
            choiceSettings = DialogSettingsRuntime.Master != null
                ? DialogSettingsRuntime.Master.choiceSettings
                : null;

            if (choiceSettings == null)
            {
                choiceSettings = ScriptableObject.CreateInstance<DialogChoiceSettings>();
                if (doDebug) Debug.LogWarning("[DialogManager] No ChoiceSettings found. Created temporary default instance.");
            }

            autoPlayState = dialogTextSettings.autoAdvance;
        }

        private TypewriterEffect GetTypewriterEffect()
        {
            return (localTextSettings != null ? localTextSettings.typewriterEffect : dialogTextSettings?.typewriterEffect)
                   ?? TypewriterEffect.Typing;
        }

        private bool CanSkipCurrentLine()
        {
            return localTextSettings != null
                ? localTextSettings.allowSkipCurrentLine
                : dialogTextSettings?.allowSkipCurrentLine ?? true;
        }

        private bool CanSkipAll()
        {
            // block any input while actions are resolving
            if (_isWaitingForActions) return false;

            return localTextSettings != null
                ? localTextSettings.allowSkipAll
                : dialogTextSettings?.allowSkipAll ?? true;
        }

        private float AutoAdvanceDelay()
        {
            return localTextSettings != null
                ? localTextSettings.autoAdvanceDelay
                : dialogTextSettings?.autoAdvanceDelay ?? 1.0f;
        }

        private float PauseFor(char c)
        {
            var src = localTextSettings != null ? localTextSettings : dialogTextSettings;
            if (src == null) return 0f;

            return c switch
            {
                ',' => src.commaPause,
                '.' => src.periodPause,
                '?' => src.questionPause,
                '!' => src.exclamationPause,
                _ => 0f
            };
        }

        public float GetCurrentCps(bool isHoldingFastForward)
        {
            var src = localTextSettings != null ? localTextSettings : dialogTextSettings;
            if (src == null) return 40f;
            if (src.typewriterEffect == TypewriterEffect.None) return float.MaxValue;

            float cps = Mathf.Max(1f, src.charsPerSecond);
            if (isHoldingFastForward && src.allowFastForwardHold)
                cps *= Mathf.Max(1f, src.fastForwardMultiplier);
            return cps;
        }

        public bool GetAutoPlayState() => autoPlayState;

        private bool ShouldStopOnSkipLine()
        {
            return localAudioSettings != null
                ? localAudioSettings.stopOnSkipLine
                : dialogAudioSettings?.stopOnSkipLine ?? true;
        }

        private bool ShouldStopOnSkipAll()
        {
            return localAudioSettings != null
                ? localAudioSettings.stopOnSkipAll
                : dialogAudioSettings?.stopOnSkipAll ?? true;
        }

        private bool ShouldFadeOutOnStop()
        {
            return localAudioSettings != null
                ? localAudioSettings.fadeOutOnStop
                : dialogAudioSettings?.fadeOutOnStop ?? true;
        }

        private float FadeOutTime()
        {
            return localAudioSettings != null
                ? localAudioSettings.fadeOutTime
                : dialogAudioSettings?.fadeOutTime ?? 0.08f;
        }
        #endregion

        #region ---------------- Reveal Effect Factory ----------------
        private ITextRevealEffect CreateRevealEffect(string line)
        {
            if (uiPanel?.dialogText == null) return null;

            var type = localTextSettings != null
                ? localTextSettings.typewriterEffect
                : dialogTextSettings?.typewriterEffect ?? TypewriterEffect.Typing;

            switch (type)
            {
                case TypewriterEffect.Typing:
                    return new TypingRevealEffect(
                        line,
                        uiPanel.dialogText,
                        () => GetCurrentCps(InputHelper.IsFastForwardHeld()),
                        PauseFor,
                        doDebug);

                case TypewriterEffect.WordByWord:
                    return new WordRevealEffect(
                        line,
                        uiPanel.dialogText,
                        () => Mathf.Max(1f, ((dialogTextSettings?.charsPerSecond ?? 5f) / 5f)));

                case TypewriterEffect.FadeIn:
                    return new FadeInRevealEffect(
                        line,
                        uiPanel.dialogText,
                        () => 1.5f);

                default:
                    return null; // show instantly
            }
        }
        #endregion

        #region ---------------- Internal ----------------
        private bool warnedNoRunner = false;
        private void WarnOnceNoRunner()
        {
            if (warnedNoRunner) return;
            warnedNoRunner = true;
            if (doDebug) Debug.LogWarning("[DialogManager] No DialogActionRunner assigned. Action calls will be ignored.");
        }
        #endregion
    }
}
