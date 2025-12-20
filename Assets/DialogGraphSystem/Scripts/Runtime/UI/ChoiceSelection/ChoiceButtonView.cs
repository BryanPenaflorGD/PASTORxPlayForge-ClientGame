using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Settings;

namespace DialogSystem.Runtime.UI
{
    /// <summary>Clickable, highlightable row used for dialog choices.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class ChoiceButtonView : MonoBehaviour, IPointerEnterHandler
    {
        #region ---------------- Inspector ----------------
        [SerializeField] private bool _doDebug = true;

        [Header("Optional explicit refs (auto-detected if null)")]
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private TextMeshProUGUI _subLabel;
        [SerializeField] private TextMeshProUGUI _hotkeyLabel;
        [SerializeField] private Outline _outline;
        [SerializeField] private GameObject _hintHolder;
        #endregion

        #region ---------------- Runtime ----------------
        private DialogManager _mgr;
        private int _index;
        private DialogChoiceSettings _settings;
        private Vector3 _baseScale;
        private bool _selected;
        private float _pulseT;
        private string _currentHotkey = string.Empty;
        private Action _onClick;
        #endregion

        private void Awake()
        {
            _doDebug = _doDebug ? true : DialogSettingsRuntime.DoDebug();

            if (!_button) _button = GetComponentInChildren<Button>();
            if (!_outline) _outline = GetComponentInChildren<Outline>();
            if (!_label) Debug.LogError($"[ChoiceButtonView] '{name}' has no Label assigned or found!");
            if (!_hotkeyLabel) Debug.LogWarning($"[ChoiceButtonView] '{name}' has no HotkeyLabel assigned or found; creating one.");

            // Optional sub label
            if (!_subLabel)
            {
                var t = transform.Find("SubLabel");
                if (t) _subLabel = t.GetComponent<TextMeshProUGUI>();
            }

            // Make labels not intercept clicks
            if (_label) _label.raycastTarget = false;
            if (_subLabel) _subLabel.raycastTarget = false;
            if (_hotkeyLabel) _hotkeyLabel.raycastTarget = false;

            _baseScale = transform.localScale;
            ApplySelected(false, string.Empty);
        }

        public void Init(DialogManager manager, int rowIndex, DialogChoiceSettings choiceSettings)
        {
            _mgr = manager;
            _index = rowIndex;
            _settings = choiceSettings;

            _hintHolder?.SetActive(_settings.showKeyHints);

            if (_outline && _settings != null)
            {
                _outline.effectColor = _settings.selectedOutlineColor;
                float t = Mathf.Max(0.5f, _settings.outlineThickness);
                _outline.effectDistance = new Vector2(t, -t);
            }
        }

        public void SetContent(string text, string subText, bool interactable, Action onClick)
        {
            if (_label) _label.text = text ?? string.Empty;
            if (_subLabel) _subLabel.text = subText ?? string.Empty;

            _onClick = onClick;

            if (_button)
            {
                _button.interactable = interactable;
                _button.onClick.RemoveListener(OnUIButtonClicked);
                if (onClick != null) _button.onClick.AddListener(OnUIButtonClicked);

                if (_doDebug)
                    Debug.Log($"[ChoiceButtonView] '{name}' wired. Interactable={_button.interactable}, HasHandler={(onClick != null)}");
            }
            else if (_doDebug)
            {
                Debug.LogWarning($"[ChoiceButtonView] '{name}' has NO Button; click will not be handled.");
            }
        }

        public void SetHotkey(string hotkey)
        {
            _currentHotkey = hotkey ?? string.Empty;
            if (_hotkeyLabel) _hotkeyLabel.text = string.Empty;
        }

        public void ApplySelected(bool isSelected, string hintText)
        {
            _selected = isSelected;

            if (_outline) _outline.enabled = isSelected;
            if (_label) _label.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;

            if (_hotkeyLabel)
                _hotkeyLabel.text = isSelected
                    ? (string.IsNullOrEmpty(hintText) ? _currentHotkey : hintText)
                    : string.Empty;

            if (!isSelected)
            {
                _pulseT = 0f;
                transform.localScale = _baseScale;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_mgr == null) return;

            if (_settings != null && _settings.mouseHoverMovesSelection)
            {
                if (_doDebug) Debug.Log($"[ChoiceButtonView] Hover select index {_index}");
                _mgr.SelectChoiceIndex(_index);
            }
        }

        private void OnUIButtonClicked()
        {
            if (_doDebug) Debug.Log($"[ChoiceButtonView] Click received on '{name}'. HandlerNull={_onClick == null}");
            _onClick?.Invoke();
        }

        private void Update()
        {
            if (!_selected || _settings == null || !_settings.animateSelected) return;

            _pulseT += Time.deltaTime * Mathf.Max(0.01f, _settings.animatePulseSpeed);
            float s = Mathf.Lerp(1f, _settings.animatePulseScale, 0.5f + 0.5f * Mathf.Sin(_pulseT * Mathf.PI * 2f));
            transform.localScale = _baseScale * s;
        }

        private TextMeshProUGUI FindOrCreateHotkeyLabel()
        {
            var t = transform.Find("HotkeyLabel");
            if (t) return t.GetComponent<TextMeshProUGUI>();

            var go = new GameObject("HotkeyLabel", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-8f, 0f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.MidlineRight;
            tmp.fontSize = 20f;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
