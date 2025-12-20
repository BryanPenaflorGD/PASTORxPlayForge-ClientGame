using System;
using System.Reflection;
using UnityEngine;

namespace DialogSystem.Runtime.Utils
{
    /// <summary>
    /// Centralized input utilities (legacy + new Input System via reflection) + XR.
    /// Adds letter/digit confirm support without breaking other confirm paths.
    /// </summary>
    public static class InputHelper
    {
        public static bool doDebug = false;

        private static float _lastAdvanceTime;
        private const float ADVANCE_COOLDOWN = 0.15f;

        private static bool _checkedNewInput;
        private static Type _keyboardType;
        private static Type _mouseType;
        private static Type _touchType;
        private static Type _gamepadType;
        private static Type _xrControllerType;
        private static PropertyInfoCache _xrLeft, _xrRight;

        // ------------------------------------------------------------------------------------
        // Generic "advance"
        // ------------------------------------------------------------------------------------
        public static bool CheckGenericAdvanceInput()
        {
            if (Time.time - _lastAdvanceTime < ADVANCE_COOLDOWN) return false;
            EnsureNewInputReflectionInitialized();

#if ENABLE_INPUT_SYSTEM
            try
            {
                if (WasPressedThisFrameOnControl(_keyboardType, "current", "anyKey")) return RegisterAdvance();
                if (WasPressedThisFrameOnControl(_mouseType, "current", "leftButton")) return RegisterAdvance();
                if (WasPressedThisFrameOnNestedControl(_touchType, "current", "primaryTouch", "press")) return RegisterAdvance();

                var pad = _gamepadType?.GetProperty("current")?.GetValue(null);
                if (pad != null)
                {
                    if (WasPressedThisFrameOnProperty(pad, "buttonSouth") ||
                        WasPressedThisFrameOnProperty(pad, "buttonEast")  ||
                        WasPressedThisFrameOnProperty(pad, "startButton"))
                        return RegisterAdvance();
                }

                if (XRActionWasPressedThisFrame()) return RegisterAdvance();
            } catch { }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.anyKeyDown) return RegisterAdvance();
            if (Input.GetMouseButtonDown(0)) return RegisterAdvance();

            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == TouchPhase.Began) return RegisterAdvance();

            if (Input.GetButtonDown("Submit") || Input.GetButtonDown("Fire1") || Input.GetButtonDown("Jump"))
                return RegisterAdvance();
#endif
            return false;
        }

        // ------------------------------------------------------------------------------------
        // Choice-specific helpers
        // ------------------------------------------------------------------------------------

        /// <summary>True if the configured letter/digit was pressed this frame.</summary>
        public static bool WasLetterPressedThisFrame(char letterOrDigit)
        {
            char c = char.ToUpperInvariant(letterOrDigit);
#if ENABLE_LEGACY_INPUT_MANAGER
            if (IsAlphaPressedLegacy(c) || IsDigitPressedLegacy(c)) return true;
#endif
#if ENABLE_INPUT_SYSTEM
            try
            {
                var kb = _keyboardType?.GetProperty("current")?.GetValue(null);
                if (kb != null)
                {
                    string prop = GetKeyboardControlPropertyName(c);
                    if (!string.IsNullOrEmpty(prop))
                    {
                        var ctrl = kb.GetType().GetProperty(prop)?.GetValue(kb);
                        var down = ctrl?.GetType().GetProperty("wasPressedThisFrame")?.GetValue(ctrl);
                        if (down is bool b && b) return true;
                    }
                }
            } catch { }
#endif
            return false;
        }

        /// <summary>Return/Enter/Space.</summary>
        public static bool WasSubmitPressedThisFrame()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                return true;
            if (Input.GetButtonDown("Submit")) return true;
#endif
#if ENABLE_INPUT_SYSTEM
            try
            {
                var kb = _keyboardType?.GetProperty("current")?.GetValue(null);
                if (kb != null)
                {
                    if (WasPressedThisFrameOnProperty(kb, "enterKey")) return true;
                    if (WasPressedThisFrameOnProperty(kb, "numpadEnterKey")) return true;
                    if (WasPressedThisFrameOnProperty(kb, "spaceKey")) return true;
                }
            } catch { }
#endif
            return false;
        }

        /// <summary>Gamepad South/East/Start.</summary>
        public static bool WasGamepadConfirmPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            try
            {
                var pad = _gamepadType?.GetProperty("current")?.GetValue(null);
                if (pad != null)
                {
                    if (WasPressedThisFrameOnProperty(pad, "buttonSouth") ||
                        WasPressedThisFrameOnProperty(pad, "buttonEast")  ||
                        WasPressedThisFrameOnProperty(pad, "startButton"))
                        return true;
                }
            } catch { }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            // Optional: if you've mapped gamepad to "Submit" in old Input Manager
            if (Input.GetButtonDown("Submit")) return true;
#endif
            return false;
        }

        /// <summary>XR select pressed this frame.</summary>
        public static bool WasXRConfirmPressedThisFrame() => XRActionWasPressedThisFrame();

        // ------------------------------------------------------------------------------------
        // Held: fast-forward (unchanged)
        // ------------------------------------------------------------------------------------
        public static bool IsFastForwardHeld()
        {
            EnsureNewInputReflectionInitialized();

#if ENABLE_INPUT_SYSTEM
            try
            {
                if (IsPressedOnControl(_mouseType, "current", "leftButton")) return true;
                if (IsPressedOnNestedControl(_touchType, "current", "primaryTouch", "press")) return true;

                var kb = _keyboardType?.GetProperty("current")?.GetValue(null);
                if (kb != null)
                {
                    if (IsPressedOnProperty(kb, "spaceKey")) return true;
                    if (IsPressedOnProperty(kb, "enterKey")) return true;
                    if (IsPressedOnProperty(kb, "numpadEnterKey")) return true;
                }

                var pad = _gamepadType?.GetProperty("current")?.GetValue(null);
                if (pad != null)
                {
                    if (IsPressedOnProperty(pad, "buttonSouth") ||
                        IsPressedOnProperty(pad, "buttonEast")  ||
                        IsPressedOnProperty(pad, "startButton"))
                        return true;
                }

                if (XRActionIsPressed()) return true;
            } catch { }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButton(0)) return true;

            for (int i = 0; i < Input.touchCount; i++)
            {
                var ph = Input.GetTouch(i).phase;
                if (ph == TouchPhase.Began || ph == TouchPhase.Moved || ph == TouchPhase.Stationary)
                    return true;
            }

            if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return)) return true;
            if (Input.GetButton("Submit") || Input.GetButton("Fire1") || Input.GetButton("Jump")) return true;
#endif
            return false;
        }

        // ------------------------------------------------------------------------------------
        // Small helpers you already had
        // ------------------------------------------------------------------------------------
        public static bool WasNumberKeyPressedThisFrame(int n)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (n >= 0 && n <= 9)
            {
                KeyCode code = KeyCode.Alpha0 + n;
                if (Input.GetKeyDown(code)) return true;
            }
#endif
#if ENABLE_INPUT_SYSTEM
            try
            {
                var kb = _keyboardType?.GetProperty("current")?.GetValue(null);
                if (kb != null && n >= 0 && n <= 9)
                {
                    var keyProp = kb.GetType().GetProperty($"digit{n}");
                    var keyCtrl = keyProp?.GetValue(kb);
                    var down = keyCtrl?.GetType().GetProperty("wasPressedThisFrame")?.GetValue(keyCtrl);
                    if (down is bool b && b) return true;
                }
            } catch { }
#endif
            return false;
        }

        public static bool WasMoveUpPressedThisFrame()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) return true;
#endif
#if ENABLE_INPUT_SYSTEM
            try
            {
                var kb = _keyboardType?.GetProperty("current")?.GetValue(null);
                if (kb != null)
                {
                    if (WasPressedThisFrameOnProperty(kb, "upArrowKey")) return true;
                    if (WasPressedThisFrameOnProperty(kb, "wKey")) return true;
                }
            } catch { }
#endif
            return false;
        }

        public static bool WasMoveDownPressedThisFrame()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return true;
#endif
#if ENABLE_INPUT_SYSTEM
            try
            {
                var kb = _keyboardType?.GetProperty("current")?.GetValue(null);
                if (kb != null)
                {
                    if (WasPressedThisFrameOnProperty(kb, "downArrowKey")) return true;
                    if (WasPressedThisFrameOnProperty(kb, "sKey")) return true;
                }
            } catch { }
#endif
            return false;
        }

        // ------------------------------------------------------------------------------------
        // Internals (reflection + legacy key mapping)
        // ------------------------------------------------------------------------------------
        private static bool RegisterAdvance()
        {
            _lastAdvanceTime = Time.time;
            if (doDebug) Debug.Log("[InputHelper] Advance registered");
            return true;
        }

        private static void EnsureNewInputReflectionInitialized()
        {
            if (_checkedNewInput) return;
            _checkedNewInput = true;

            _keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            _mouseType = Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
            _touchType = Type.GetType("UnityEngine.InputSystem.Touchscreen, Unity.InputSystem");
            _gamepadType = Type.GetType("UnityEngine.InputSystem.Gamepad, Unity.InputSystem");
            _xrControllerType = Type.GetType("UnityEngine.InputSystem.XR.XRController, Unity.InputSystem");

            if (_xrControllerType != null)
            {
                _xrLeft = new PropertyInfoCache(_xrControllerType, "leftHand");
                _xrRight = new PropertyInfoCache(_xrControllerType, "rightHand");
            }

            if (doDebug)
            {
                Debug.Log($"[InputHelper] NewInput present? keyboard={_keyboardType != null} gamepad={_gamepadType != null} xr={_xrControllerType != null}");
            }
        }

        private static bool IsAlphaPressedLegacy(char c)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (c >= 'A' && c <= 'Z')
            {
                KeyCode kc = KeyCode.A + (c - 'A');
                if (Input.GetKeyDown(kc)) return true;
            }
#endif
            return false;
        }

        private static bool IsDigitPressedLegacy(char c)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (c >= '0' && c <= '9')
            {
                KeyCode kc = KeyCode.Alpha0 + (c - '0');
                if (Input.GetKeyDown(kc)) return true;
            }
#endif
            return false;
        }

        private static string GetKeyboardControlPropertyName(char c)
        {
            if (c >= 'A' && c <= 'Z') return char.ToLowerInvariant(c) + "Key"; // e.g., 'F' -> "fKey"
            if (c >= '0' && c <= '9') return "digit" + c;                      // '1' -> "digit1"
            return null;
        }

        // ---- generic reflection helpers ----
        private static bool WasPressedThisFrameOnControl(Type deviceType, string deviceProp, string controlProp)
        {
            var dev = deviceType?.GetProperty(deviceProp)?.GetValue(null);
            return WasPressedThisFrameOnProperty(dev, controlProp);
        }
        private static bool WasPressedThisFrameOnNestedControl(Type deviceType, string deviceProp, string midProp, string controlProp)
        {
            var dev = deviceType?.GetProperty(deviceProp)?.GetValue(null);
            var mid = dev?.GetType().GetProperty(midProp)?.GetValue(dev);
            return WasPressedThisFrameOnProperty(mid, controlProp);
        }
        private static bool WasPressedThisFrameOnProperty(object obj, string btnProp)
        {
            var btn = obj?.GetType().GetProperty(btnProp)?.GetValue(obj);
            var p = btn?.GetType().GetProperty("wasPressedThisFrame");
            return (bool)(p?.GetValue(btn) ?? false);
        }
        private static bool IsPressedOnControl(Type deviceType, string deviceProp, string controlProp)
        {
            var dev = deviceType?.GetProperty(deviceProp)?.GetValue(null);
            return IsPressedOnProperty(dev, controlProp);
        }
        private static bool IsPressedOnNestedControl(Type deviceType, string deviceProp, string midProp, string controlProp)
        {
            var dev = deviceType?.GetProperty(deviceProp)?.GetValue(null);
            var mid = dev?.GetType().GetProperty(midProp)?.GetValue(dev);
            return IsPressedOnProperty(mid, controlProp);
        }
        private static bool IsPressedOnProperty(object obj, string btnProp)
        {
            var btn = obj?.GetType().GetProperty(btnProp)?.GetValue(obj);
            var p = btn?.GetType().GetProperty("isPressed");
            if (p != null) return (bool)(p.GetValue(btn) ?? false);

            var press = btn?.GetType().GetProperty("press")?.GetValue(btn);
            var p2 = press?.GetType().GetProperty("isPressed");
            return (bool)(p2?.GetValue(press) ?? false);
        }

        // ---- XR helpers ----
        private static bool XRActionWasPressedThisFrame()
        {
            object left = _xrLeft?.GetValue();
            object right = _xrRight?.GetValue();
            return XRWasPressedThisFrame(left) || XRWasPressedThisFrame(right);
        }
        private static bool XRActionIsPressed()
        {
            object left = _xrLeft?.GetValue();
            object right = _xrRight?.GetValue();
            return XRIsPressed(left) || XRIsPressed(right);
        }
        private static bool XRWasPressedThisFrame(object hand)
        {
            if (hand == null) return false;
            var act = hand.GetType().GetProperty("selectAction")?.GetValue(hand);
            var action = act?.GetType().GetProperty("action")?.GetValue(act);
            var m = action?.GetType().GetMethod("WasPressedThisFrame", Type.EmptyTypes);
            if (m != null) return (bool)(m.Invoke(action, null) ?? false);
            return false;
        }
        private static bool XRIsPressed(object hand)
        {
            if (hand == null) return false;
            var act = hand.GetType().GetProperty("selectAction")?.GetValue(hand);
            var action = act?.GetType().GetProperty("action")?.GetValue(act);
            var isPressed = action?.GetType().GetMethod("IsPressed", Type.EmptyTypes);
            if (isPressed != null) return (bool)(isPressed.Invoke(action, null) ?? false);
            var inProg = action?.GetType().GetProperty("inProgress");
            if (inProg != null) return (bool)(inProg.GetValue(action) ?? false);
            return false;
        }

        private class PropertyInfoCache
        {
            private readonly PropertyInfo _prop;
            public PropertyInfoCache(Type type, string name) =>
                _prop = type?.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            public object GetValue() => _prop?.GetValue(null);
        }
    }
}
