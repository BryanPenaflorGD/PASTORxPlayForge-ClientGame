using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.Runtime.Models; // For VNCharacterEntry
using System.Collections.Generic; // For List

namespace DialogSystem.Runtime.UI
{   
    /// <summary>Bridge between DialogManager and concrete UI. Simple choice container (vertical).</summary>
    [DisallowMultipleComponent]
    public class DialogUIController : MonoBehaviour
    {
        #region ---------------- Inspector ----------------
        [Header("Main UI Elements")]
        public GameObject panelRoot;
        public TextMeshProUGUI speakerName;
        public TextMeshProUGUI dialogText;
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
        #endregion

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

        // -------- ADD THIS METHOD --------
        // -------- ADD THIS METHOD --------
        public void UpdateVNStage(List<VNCharacterEntry> characters)
        {
            // 1. Reset all images (Hide them first)
            HideImage(posFarLeft);
            HideImage(posLeft);
            HideImage(posCenter);
            HideImage(posRight);
            HideImage(posFarRight);

            if (characters == null || characters.Count == 0) return;

            // 2. Loop through the list and activate the correct ones
            foreach (var entry in characters)
            {
                if (entry.expression == null) continue;

                Image target = null;

                switch (entry.position)
                {
                    case VNPosition.FarLeft: target = posFarLeft; break;
                    case VNPosition.Left: target = posLeft; break;
                    case VNPosition.Center: target = posCenter; break;
                    case VNPosition.Right: target = posRight; break;
                    case VNPosition.FarRight: target = posFarRight; break;
                }

                if (target != null)
                {
                    target.sprite = entry.expression;
                    target.gameObject.SetActive(true);
                    target.preserveAspect = true; // Keep sprite proportions

                    // Handle Flip
                    if (entry.flipX) target.transform.localScale = new Vector3(-1, 1, 1);
                    else target.transform.localScale = Vector3.one;
                }
            }
        }

        private void HideImage(Image img)
        {
            if (img)
            {
                img.gameObject.SetActive(false);
                img.sprite = null;
                img.transform.localScale = Vector3.one; // Reset flip
            }
        }
        // ----------------------------------
        #endregion

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
