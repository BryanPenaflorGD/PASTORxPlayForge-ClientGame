using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Models; // For VNCharacterEntry
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Settings.Panels;
using System;
using System.Collections; // For IEnumerator
using System.Collections.Generic; // For List
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


namespace DialogSystem.Runtime.UI
{   
    /// <summary>Bridge between DialogManager and concrete UI. Simple choice container (vertical).</summary>
    [DisallowMultipleComponent]
    public class DialogUIController : MonoBehaviour
    {
        #region ---------------- Inspector ----------------
        [Header("Main UI Elements")]
        public GameObject panelRoot;
        public GameObject dialogPanel;
        public GameObject characterStage;
        public TextMeshProUGUI speakerName;
        public TextMeshProUGUI dialogText;
        public Image panelBackground;
        public Image portraitImage;


        // -------- ADD THIS SECTION --------
        [Header("VN Character Positions")]
        public Image posFarLeft;
        public Image posLeft;
        public Image posCenter;
        public Image posRight;
        public Image posFarRight;
        // ----------------------------------

        [Header("Choices UI (Vertical Container)")]
        [SerializeField] private bool doDebug = true;
        public Transform choicesContainer;     // Must have VerticalLayoutGroup + ContentSizeFitter
        public GameObject choiceButtonPrefab;  // Prefab with Button + ChoiceButtonView on root

        [Header("Skip Button")]
        public GameObject skipButton;

        [Header("Dialog Panel Btn")]
        public Button dialogPanelButton;

        [Header("AutoPlay Button Config")]
        public Button autoPlayButton;
        public GameObject pauseIcon;
        public GameObject playIcon;

        [Header("Panel Themes")]
        public List<SpeakerTheme> speakerThemes = new List<SpeakerTheme>();

        [Header("Effects")]
        public CanvasGroup fadeOverlay; // ASSIGN THIS IN INSPECTOR (Black Image)

        [System.Serializable]
        public struct SpeakerTheme
        {
            public string speakerId; // e.g. "Player"
            public Sprite backgroundSprite; // The Blue or Red box image
        }

        private Sprite defaultBackgroundSprite;

        public void HidePanel()
        {
            if (dialogPanel != null) dialogPanel.SetActive(false);
            if (characterStage != null) characterStage.SetActive(false);

        }

        // 2. Helper to Show the Panel (Connect this to "ShowUI" action)
        public void ShowPanel()
        {
            if (dialogPanel != null) dialogPanel.SetActive(true);
            if (characterStage != null) characterStage.SetActive(true);
        }

        public void SetTheme(string name)
        {
            if (panelBackground == null) return;

            // 1. Find a matching theme for this speaker
            // (This looks through the list you made in the Inspector)
            var theme = speakerThemes.Find(x => x.speakerId == name);

            // 2. If we found a custom sprite, use it. Otherwise, use default.
            if (theme.backgroundSprite != null)
            {
                panelBackground.sprite = theme.backgroundSprite;
            }
            else
            {
                panelBackground.sprite = defaultBackgroundSprite;
            }
        }
        #endregion

        private void Awake()
        {
            // Save whatever sprite is currently on the panel as the "Default"
            if (panelBackground != null)
            {
                defaultBackgroundSprite = panelBackground.sprite;
            }
        }

        #region ---------------- Public API ----------------
        public void UpdateAutoPlayIcon(bool isAutoPlay)
        { if (pauseIcon && playIcon) { pauseIcon.SetActive(isAutoPlay); playIcon.SetActive(!isAutoPlay); } }

        public void ToggleAutoPlayIcon()
        {
            var mgr = DialogManager.Instance;
            if (!mgr) return;
            UpdateAutoPlayIcon(mgr.ToggleAutoPlay());
        }

        public void SetDialogPanelBtnListener(UnityAction action)
        {
            if (!dialogPanelButton || action == null) return;
            dialogPanelButton.onClick.RemoveAllListeners();
            dialogPanelButton.onClick.AddListener(action);
        }

        public void SetPanelVisible(bool v) => Safe(panelRoot, v);
        public void SetSkipVisible(bool v) => Safe(skipButton, v);
        public void SetChoicesVisible(bool v)
        { if (choicesContainer) choicesContainer.gameObject.SetActive(v); }

        public void SetSpeaker(string name) { if (speakerName) speakerName.text = name ?? string.Empty; }
        public void SetText(string text) { if (dialogText) dialogText.text = text ?? string.Empty; }
        public void SetPortrait(Sprite s) { if (portraitImage) portraitImage.sprite = s; }

        #endregion

        #region Visual Novel Runtime Logic

        // Inside DialogUIController.cs

        // Inside DialogUIController.cs -> UpdateVNStage

        public void UpdateVNStage(List<VNCharacterEntry> characters)
        {
            // 1. Hide everything first
            ResetImage(posFarLeft);
            ResetImage(posLeft);
            ResetImage(posCenter);
            ResetImage(posRight);
            ResetImage(posFarRight);

            if (characters == null) return;

            foreach (var entry in characters)
            {
                Image targetImage = GetImageByPosition(entry.position);
                if (targetImage == null) continue;

                // Hide if state is Hidden
                if (entry.state == VNCharacterState.Hidden)
                {
                    targetImage.gameObject.SetActive(false);
                    continue;
                }

                targetImage.gameObject.SetActive(true);

                // --- ANIMATOR LOGIC ---
                // 2. Setup Animator
                if (entry.animatorController != null)
                {
                    var anim = targetImage.GetComponent<Animator>();
                    if (anim == null) anim = targetImage.gameObject.AddComponent<Animator>();

                    anim.runtimeAnimatorController = entry.animatorController;

                    // 1. Enable it first so we can force it to render the first frame
                    anim.enabled = true;

                    // 2. If a specific animation is requested, jump to it immediately
                    if (!string.IsNullOrEmpty(entry.animationName))
                    {
                        anim.Play(entry.animationName, 0, 0f);
                    }

                    // 3. FORCE UPDATE: This tells the Animator to apply the sprite 
                    // to the Image component right now.
                    anim.Update(0f);

                    // 4. Handle State
                    if (entry.state == VNCharacterState.Dimmed)
                    {
                        // Freeze the animation now that the sprite is visible
                        anim.enabled = false;

                        targetImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                        targetImage.preserveAspect = true;
                    }
                    else
                    {
                        // Keep playing
                        anim.enabled = true;
                        targetImage.color = Color.white;
                        targetImage.preserveAspect = true;
                    }
                }
                else
                {
                    // Fallback for non-animator characters
                    var anim = targetImage.GetComponent<Animator>();
                    if (anim != null) anim.enabled = false;

                    targetImage.color = (entry.state == VNCharacterState.Dimmed)
                        ? new Color(0.5f, 0.5f, 0.5f, 1f)
                        : Color.white;
                    targetImage.preserveAspect = true;
                }

                // --- DIM LOGIC ---
                targetImage.color = (entry.state == VNCharacterState.Dimmed) ? new Color(0.5f, 0.5f, 0.5f, 1f) : Color.white;
                targetImage.preserveAspect = true;

                // --- FLIP LOGIC (Restored!) ---
                // If flipX is true, scale X becomes -1. Otherwise 1.
                if (entry.flipX)
                    targetImage.rectTransform.localScale = new Vector3(-1, 1, 1);
                else
                    targetImage.rectTransform.localScale = Vector3.one;

            }
        }
        private Image GetImageByPosition(VNPosition pos)
        {
            switch (pos)
            {
                case VNPosition.FarLeft: return posFarLeft;
                case VNPosition.Left: return posLeft;
                case VNPosition.Center: return posCenter;
                case VNPosition.Right: return posRight;
                case VNPosition.FarRight: return posFarRight;
                default: return posCenter;
            }
        }

        private void ResetImage(Image img)
        {
            if (img != null)
            {
                img.gameObject.SetActive(false);
                img.color = Color.white;
                img.rectTransform.localScale = Vector3.one;
            }
        }
        #endregion



        public IEnumerator FadeFromBlackDelayed(float delay, float duration)
        {
            // Wait for the next node (Video) to initialize
            yield return new WaitForSeconds(delay);

            // Now Fade In
            yield return FadeFromBlack(duration);
        }
        // Add this to DialogUIController.cs

        public IEnumerator FadeTransition(float duration)
        {
            // 1. Fade OUT (to black) - uses half the duration
            yield return FadeToBlack(duration * 0.5f);

            // 2. Wait a tiny bit while black (optional, feels smoother)
            yield return new WaitForSeconds(0.2f);

            // 3. Fade IN (to clear) - uses remaining duration
            yield return FadeFromBlack(duration * 0.5f);
        }

        public IEnumerator FadeToBlack(float duration)
        {
            if (fadeOverlay == null) yield break;
            fadeOverlay.blocksRaycasts = true; // Block clicks
            float start = fadeOverlay.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                fadeOverlay.alpha = Mathf.Lerp(start, 1f, elapsed / duration);
                yield return null;
            }
            fadeOverlay.alpha = 1f;
        }

        public IEnumerator FadeFromBlack(float duration)
        {
            if (fadeOverlay == null) yield break;
            float start = fadeOverlay.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                fadeOverlay.alpha = Mathf.Lerp(start, 0f, elapsed / duration);
                yield return null;
            }
            fadeOverlay.alpha = 0f;
            fadeOverlay.blocksRaycasts = false; // Allow clicks
        }

        #region ---------------- Choices (simple build) ----------------
        /// <summary>Destroys existing children and rebuilds one prefab per choice. No pooling.</summary>
        public void BuildChoices(ChoiceNode node, DialogChoiceSettings settings, Action<int> onPick)
        {
            if (!choicesContainer || !choiceButtonPrefab)
            {
                if (doDebug) Debug.LogError("[DialogUIController] Choices UI not assigned.");
                return;
            }

            // Clear
            for (int i = choicesContainer.childCount - 1; i >= 0; i--)
                Destroy(choicesContainer.GetChild(i).gameObject);

            // Build
            for (int i = 0; i < node.choices.Count; i++)
            {
                int idx = i;
                var ch = node.choices[i];

                var go = Instantiate(choiceButtonPrefab, choicesContainer);
                var view = go.GetComponent<ChoiceButtonView>() ?? go.AddComponent<ChoiceButtonView>();
                view.Init(DialogManager.Instance, idx, settings);
                view.SetHotkey(string.Empty);
                view.SetContent(ch.answerText, string.Empty, /*interactable*/ true, () => onPick?.Invoke(idx));
            }

            SetChoicesVisible(true);
        }

        public void ClearChoices()
        {
            if (!choicesContainer) return;
            for (int i = choicesContainer.childCount - 1; i >= 0; i--)
                Destroy(choicesContainer.GetChild(i).gameObject);
            SetChoicesVisible(false);
        }
        #endregion
        private static void Safe(GameObject go, bool v) { if (go) go.SetActive(v); }
    }
}
