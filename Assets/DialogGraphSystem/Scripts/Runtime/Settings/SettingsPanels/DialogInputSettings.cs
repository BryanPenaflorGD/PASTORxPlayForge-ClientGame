using UnityEngine;

namespace DialogSystem.Runtime.Settings.Panels
{
    /// <summary>
    /// Input bindings for keyboard, mouse, and basic gamepad (old Input Manager).
    /// </summary>
    public class DialogInputSettings : ScriptableObject
    {
        #region ---------------- Inspector ----------------
        [Header("Keys (Keyboard)")]
        public KeyCode[] confirmKeys = { KeyCode.Return, KeyCode.Space, KeyCode.E, KeyCode.F };
        public KeyCode[] skipKeys = { KeyCode.LeftControl, KeyCode.RightControl };
        public KeyCode[] fastForwardKeys = { KeyCode.LeftShift, KeyCode.RightShift };
        public KeyCode[] cancelKeys = { KeyCode.Escape };

        [Header("Navigation (Keyboard)")]
        public KeyCode[] navUpKeys = { KeyCode.W, KeyCode.UpArrow };
        public KeyCode[] navDownKeys = { KeyCode.S, KeyCode.DownArrow };

        [Header("Axes / Gamepad")]
        public string verticalAxis = "Vertical";
        [Tooltip("JoystickButton index for confirm (0 = A on many pads).")]
        public int joystickConfirmButton = 0;

        [Header("Mouse")]
        public bool allowMouseClickConfirm = true;
        #endregion
    }

}
