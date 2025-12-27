using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DialogSystem.Runtime.DialogHistory;
using DialogSystem.Runtime.Core;

namespace DialogSystem.Runtime
{
    [DisallowMultipleComponent]
    public class DialogueHistoryView : MonoBehaviour
    {
        #region -------- UI References --------
        [Header("UI References")]
        [Tooltip("The actual panel GameObject to toggle on/off. (Do NOT attach this script to this object!)")]
        public GameObject root;

        [Tooltip("Vertical ScrollRect controlling the content viewport.")]
        public ScrollRect scrollRect;

        [Tooltip("Parent transform for pooled item rows.")]
        public Transform contentRoot;

        [Tooltip("Row prefab (PF_DialogueHistoryItem).")]
        public DialogueHistoryItem itemPrefab;
        #endregion

        #region -------- Settings --------
        [Header("Settings")]
        [Min(0), Tooltip("Pre-instantiate this many pooled rows on start.")]
        public int prewarm = 24;

        [Tooltip("If true, choice rows hide their icon by default.")]
        public bool hideChoiceIcon = true;

        [Tooltip("If AutoPlay was ON when opening history, should we turn it back ON when closing?")]
        public bool resumeAutoplayOnClose = true;
        #endregion

        #region -------- Internal State --------
        private readonly List<DialogueHistoryItem> _pool = new();
        private int _activeCount = 0;
        private bool _dirtyScrollToBottom;

        // History Data
        private List<HistoryEntry> _historyLog = new List<HistoryEntry>();

        // AutoPlay State Memory
        private bool _lastAutoPlayState;
        #endregion

        #region -------- Unity Lifecycle --------
        private void Start()
        {
            // Safety Check
            if (root == gameObject)
            {
                Debug.LogError("[DialogueHistoryView] CRITICAL ERROR: This script is on the object it disables. Move it to DialogManager.");
            }

            Prewarm(prewarm);

            // Subscribe to DialogManager events
            if (DialogManager.Instance != null)
            {
                DialogManager.Instance.OnLineShown += OnLineShownRecorded;
                DialogManager.Instance.OnChoicePicked += OnChoicePickedRecorded;
            }
        }

        private void OnDestroy()
        {
            if (DialogManager.Instance != null)
            {
                DialogManager.Instance.OnLineShown -= OnLineShownRecorded;
                DialogManager.Instance.OnChoicePicked -= OnChoicePickedRecorded;
            }
        }

        private void LateUpdate()
        {
            if (!_dirtyScrollToBottom) return;
            _dirtyScrollToBottom = false;

            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f; // Snap to bottom
        }
        #endregion

        #region -------- Event Listeners (Fixing Speaker Name) --------
        private void OnLineShownRecorded(string guid, string speaker, string text)
        {
            // 1. Get Portrait
            Sprite currentPortrait = null;
            if (DialogManager.Instance != null && DialogManager.Instance.uiPanel != null && DialogManager.Instance.uiPanel.portraitImage != null)
            {
                currentPortrait = DialogManager.Instance.uiPanel.portraitImage.sprite;
            }

            // 2. FIX SPEAKER NAME
            // If speaker is null, empty, or "None", change it to "???"
            string finalSpeaker = speaker;
            if (string.IsNullOrEmpty(finalSpeaker) || finalSpeaker.Equals("None", System.StringComparison.OrdinalIgnoreCase))
            {
                finalSpeaker = "???";
            }

            // 3. Create Entry
            var entry = new HistoryEntry(HistoryKind.Line, finalSpeaker, text, guid, currentPortrait);
            _historyLog.Add(entry);

            // 4. Update UI if open
            if (root != null && root.activeSelf)
            {
                AppendItem(entry);
            }
        }

        private void OnChoicePickedRecorded(string guid, string text)
        {
            var entry = new HistoryEntry(HistoryKind.Choice, "Choice", text, guid, null);
            _historyLog.Add(entry);

            if (root != null && root.activeSelf)
            {
                AppendItem(entry);
            }
        }
        #endregion

        #region -------- Public API (Toggle) --------
        public void Toggle()
        {
            if (root != null && root.activeSelf)
                Hide();
            else
                Show();
        }

        public void Show()
        {
            if (root) root.SetActive(true);

            if (DialogManager.Instance != null)
            {
                _lastAutoPlayState = DialogManager.Instance.GetAutoPlayState();
                DialogManager.Instance.PauseForHistory();
            }

            Refresh(_historyLog);
        }

        public void Hide()
        {
            if (DialogManager.Instance != null)
            {
                DialogManager.Instance.ResumeAfterHistory();

                if (resumeAutoplayOnClose && _lastAutoPlayState && !DialogManager.Instance.GetAutoPlayState())
                {
                    DialogManager.Instance.ToggleAutoPlay();
                }
            }

            if (root) root.SetActive(false);
        }
        #endregion

        #region -------- Display Logic --------
        public void Refresh(IReadOnlyList<HistoryEntry> entries)
        {
            ReturnAll();
            EnsurePool(entries != null ? entries.Count : 0);

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                    BindToNext(entries[i]);
            }
            _dirtyScrollToBottom = true;
        }

        public void AppendItem(HistoryEntry entry)
        {
            EnsurePool(_activeCount + 1);
            BindToNext(entry);
            _dirtyScrollToBottom = true;
        }

        private void Prewarm(int count) => EnsurePool(count);

        private void EnsurePool(int count)
        {
            if (!itemPrefab || !contentRoot) return;
            while (_pool.Count < count)
            {
                var item = Instantiate(itemPrefab, contentRoot);
                item.gameObject.SetActive(false);
                item.hideIconForChoices = hideChoiceIcon;
                _pool.Add(item);
            }
        }

        private void ReturnAll()
        {
            for (int i = 0; i < _activeCount; i++)
                if (_pool[i]) _pool[i].gameObject.SetActive(false);
            _activeCount = 0;
        }

        private void BindToNext(HistoryEntry e)
        {
            if (_activeCount >= _pool.Count || e == null) return;

            var item = _pool[_activeCount++];
            item.gameObject.SetActive(true);

            var portrait = (e.kind == HistoryKind.Choice && item.hideIconForChoices) ? null : e.portrait;

            item.Bind(
                e.kind == HistoryKind.Choice ? "Your Choice" : e.speaker,
                e.text,
                portrait,
                e.kind == HistoryKind.Choice
            );
        }
        #endregion
    }
}