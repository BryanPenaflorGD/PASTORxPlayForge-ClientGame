using System;
using System.Collections.Generic;
using UnityEngine;
using DialogSystem.Runtime.Core;

namespace DialogSystem.Runtime.DialogHistory
{
    /// <summary>Type of entry stored in the dialog history.</summary>
    public enum HistoryKind { Line, Choice }

    /// <summary>One line of history: a spoken line or a chosen option.</summary>
    [Serializable]
    public class HistoryEntry
    {
        #region -------- Data --------
        public HistoryKind kind;
        public string speaker;
        public Sprite portrait;    // optional UI icon
        [TextArea] public string text;
        public string nodeGuid;
        public DateTime time = DateTime.Now;
        #endregion

        #region -------- Ctors --------
        public HistoryEntry(HistoryKind k, string spk, string txt, string guid)
        {
            kind = k; speaker = spk; text = txt; nodeGuid = guid;
        }

        public HistoryEntry(HistoryKind k, string spk, string txt, string guid, Sprite p)
        {
            kind = k; speaker = spk; text = txt; nodeGuid = guid; portrait = p;
        }
        #endregion
    }

    /// <summary>
    /// Collects and displays dialog history. Subscribes to <see cref="DialogManager"/> events,
    /// buffers entries up to <see cref="maxEntries"/>, and drives a <see cref="DialogueHistoryView"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueHistory : MonoBehaviour
    {
        #region -------- Refs --------
        [Header("Refs")]
        [Tooltip("DialogManager to listen to. Defaults to DialogManager.Instance at runtime.")]
        private DialogManager manager;

        [Tooltip("View that renders the history list.")]
        public DialogueHistoryView view;
        #endregion

        #region -------- Settings --------
        [Header("Settings")]
        [Range(50, 2000)]
        [Tooltip("Maximum number of entries to keep in memory.")]
        public int maxEntries = 500;

        [Tooltip("If enabled and autoplay was ON when opening, autoplay will be toggled back ON when closing.")]
        public bool resumeAutoplayOnClose = false;
        #endregion

        #region -------- State --------
        private readonly List<HistoryEntry> _entries = new();
        private bool _isOpen;
        private bool _lastAutoPlayState;

        /// <summary>Current in-memory history buffer.</summary>
        public IReadOnlyList<HistoryEntry> Entries => _entries;
        #endregion

        #region -------- Unity Lifecycle --------
        private void Awake()
        {
            if (!manager) manager = DialogManager.Instance;
        }

        private void OnEnable()
        {
            if (manager == null) return;
            manager.OnLineShown += HandleLine;
            manager.OnChoicePicked += HandleChoice;
            manager.OnConversationReset += ClearAll;
        }

        private void OnDisable()
        {
            if (manager == null) return;
            manager.OnLineShown -= HandleLine;
            manager.OnChoicePicked -= HandleChoice;
            manager.OnConversationReset -= ClearAll;
        }
        #endregion

        #region -------- Event Handlers --------
        private void HandleLine(string guid, string speaker, string text)
        {
            var safeSpeaker = string.IsNullOrEmpty(speaker) ? "???" : speaker;
            var portrait = GetCurrentPortraitSafe();
            Add(new HistoryEntry(HistoryKind.Line, safeSpeaker, text, guid, portrait));
        }

        private void HandleChoice(string guid, string text)
        {
            Add(new HistoryEntry(HistoryKind.Choice, "Your Choice", text, guid, null));
        }
        #endregion

        #region -------- Core List Ops --------
        /// <summary>Adds a history entry, trimming the buffer if needed, and updates the view if open.</summary>
        private void Add(HistoryEntry e)
        {
            _entries.Add(e);
            if (_entries.Count > maxEntries) _entries.RemoveAt(0);
            if (_isOpen && view != null) view.AppendItem(e);
        }

        /// <summary>Clears all history and refreshes the view if open.</summary>
        public void ClearAll()
        {
            _entries.Clear();
            if (_isOpen && view != null) view.Refresh(_entries);
        }
        #endregion

        #region -------- Panel Open/Close --------
        /// <summary>Toggles the history panel open/closed.</summary>
        public void Toggle()
        {
            if (_isOpen) Close(); else Open();
        }

        /// <summary>Opens the history panel and pauses dialog playback.</summary>
        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;

            if (manager != null)
            {
                _lastAutoPlayState = manager.GetAutoPlayState();
                manager.PauseForHistory();
            }

            if (view != null)
            {
                view.Show();
                view.Refresh(_entries);
            }
        }

        /// <summary>Closes the history panel and resumes dialog (optionally restoring autoplay).</summary>
        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            if (view != null) view.Hide();

            if (manager != null)
            {
                manager.ResumeAfterHistory();

                // Resume autoplay only if opted in and it was on before opening.
                if (resumeAutoplayOnClose && _lastAutoPlayState && !manager.GetAutoPlayState())
                    manager.ToggleAutoPlay();
            }
        }
        #endregion

        #region -------- Helpers --------
        private Sprite GetCurrentPortraitSafe()
        {
            var ui = manager != null ? manager.uiPanel : null;
            return (ui != null && ui.portraitImage != null) ? ui.portraitImage.sprite : null;
        }
        #endregion
    }
}
