using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Interfaces;

namespace DialogSystem.Runtime.Core
{
    /// <summary>
    /// Runs conversation-scoped actions (and optional global fallbacks).
    /// Supports:
    /// - Sync UnityEvent bindings per actionId.
    /// - Async, waitable handlers via <see cref="IActionHandler"/> when requested.
    /// - Global-only and per-conversation convenience invocations.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogActionRunner : MonoBehaviour
    {
        #region -------- Inspector --------
        [Header("Global (shared across conversations)")]
        [Tooltip("Bindings/handlers applied to all conversations (used when falling back or invoking global-only).")]
        public ConversationActionSet global = new ConversationActionSet { conversationKey = "<global>" };

        [Header("Per-conversation sets (key = dialogID)")]
        [Tooltip("Bindings/handlers for specific conversations (dialogID = ConversationActionSet.conversationKey).")]
        public List<ConversationActionSet> conversations = new List<ConversationActionSet>();

        [Tooltip("If true, use Global when a conversation set doesn't contain the requested action/handler.")]
        public bool useGlobalFallback = true;
        #endregion

        #region -------- ActionNode entrypoint (used by DialogManager) --------
        /// <summary>
        /// Executes an <see cref="ActionNode"/>:
        /// - Optional pre-delay (<see cref="ActionNode.waitSeconds"/>).
        /// - Invokes UnityEvent binding(s) matching <see cref="ActionNode.actionId"/>.
        /// - If <see cref="ActionNode.waitForCompletion"/> is true, runs an async handler if available and waits for it.
        /// </summary>
        public IEnumerator RunAction(ActionNode node, string dialogID = null)
        {
            if (node == null) yield break;

            var payload = node.payloadJson ?? string.Empty;

            // 1. Wait (Delay) before starting any actions
            if (node.waitSeconds > 0f)
                yield return new WaitForSeconds(Mathf.Max(0f, node.waitSeconds));

            // 2. SPLIT Action IDs (e.g. "FadeOut, PlaySound")
            string[] actionList = node.actionId.Split(',');

            var convSet = FindSet(dialogID);

            foreach (var rawId in actionList)
            {
                string currentID = rawId.Trim(); // Remove spaces around commas
                if (string.IsNullOrEmpty(currentID)) continue;

                // --- COMBINED ACTION (Fixed for Video Transitions) ---
                if (currentID == "SceneTransition")
                {
                    if (DialogManager.Instance != null && DialogManager.Instance.uiPanel != null)
                    {
                        var ui = DialogManager.Instance.uiPanel;

                        // 1. Fade OUT (We wait for this to finish)
                        yield return ui.FadeToBlack(1.0f);

                        // 2. Schedule Fade IN to happen LATER (in parallel)
                        // We give it 0.5s delay to ensure the next Video Node has time to load/start.
                        ui.StartCoroutine(ui.FadeFromBlackDelayed(0.5f, 5f));
                    }

                    // 3. Stop the Action Node HERE. 
                    // This forces DialogManager to advance to the next node (The Video)
                    // while the screen is still black.
                    continue;
                }

                // --- A. HARDCODED SYSTEM ACTIONS ---
                if (currentID == "FadeOut")
                {
                    if (DialogManager.Instance != null && DialogManager.Instance.uiPanel != null)
                        yield return DialogManager.Instance.uiPanel.FadeToBlack(1.0f);
                    continue; // Done with this ID, move to next
                }

                if (currentID == "FadeIn")
                {
                    if (DialogManager.Instance != null && DialogManager.Instance.uiPanel != null)
                        yield return DialogManager.Instance.uiPanel.FadeFromBlack(1.0f);
                    continue; // Done with this ID, move to next
                }

                // --- B. STANDARD BINDINGS (UnityEvents) ---
                bool invoked = false;
                if (convSet != null)
                    invoked |= TryInvokeBinding(convSet, currentID, payload);

                if (!invoked && useGlobalFallback && global != null)
                    invoked |= TryInvokeBinding(global, currentID, payload);

                // --- C. ASYNC HANDLERS (Scripts) ---
                // We use your existing TryHandleAsync method here
                IEnumerator asyncRoutine = null;
                bool handledAsync = false;

                // Try Conversation Set first
                if (convSet != null)
                {
                    if (TryHandleAsync(convSet, currentID, payload, out asyncRoutine))
                    {
                        handledAsync = true;
                        if (asyncRoutine != null) yield return asyncRoutine;
                    }
                }

                // Try Global Set fallback if not handled yet
                if (!handledAsync && useGlobalFallback && global != null)
                {
                    if (TryHandleAsync(global, currentID, payload, out asyncRoutine))
                    {
                        if (asyncRoutine != null) yield return asyncRoutine;
                    }
                }
            }
        }
        #endregion

        #region -------- Convenience API --------
        /// <summary>
        /// Invokes an action only against the Global set (optionally waiting and/or delaying).
        /// </summary>
        public IEnumerator RunActionGlobal(string actionId, string payloadJson = "", bool waitForCompletion = false, float waitSeconds = 0f)
            => RunActionInternal(
                dialogId: null,
                actionId: actionId,
                payloadJson: payloadJson,
                waitForCompletion: waitForCompletion,
                waitSeconds: waitSeconds,
                forceGlobalOnly: true
            );

        /// <summary>
        /// Invokes an action for a specific conversation (by dialogId). If not found and
        /// <see cref="useGlobalFallback"/> is true, it will try Global.
        /// </summary>
        public IEnumerator RunActionForConversation(string dialogId, string actionId, string payloadJson = "", bool waitForCompletion = false, float waitSeconds = 0f)
            => RunActionInternal(
                dialogId: dialogId,
                actionId: actionId,
                payloadJson: payloadJson,
                waitForCompletion: waitForCompletion,
                waitSeconds: waitSeconds,
                forceGlobalOnly: false
            );
        #endregion

        #region -------- Internals --------
        private IEnumerator RunActionInternal(string dialogId, string actionId, string payloadJson, bool waitForCompletion, float waitSeconds, bool forceGlobalOnly)
        {
            if (waitSeconds > 0f)
                yield return new WaitForSeconds(Mathf.Max(0f, waitSeconds));

            var payload = payloadJson ?? string.Empty;
            var set = forceGlobalOnly ? null : FindSet(dialogId);

            // 1) Sync invoke
            var invoked = false;
            if (!forceGlobalOnly && set != null)
                invoked = TryInvokeBinding(set, actionId, payload);

            if (!invoked && (forceGlobalOnly || useGlobalFallback) && global != null)
                TryInvokeBinding(global, actionId, payload);

            // 2) Optional async wait
            if (waitForCompletion)
            {
                if (!forceGlobalOnly && set != null && TryHandleAsync(set, actionId, payload, out var coConv) && coConv != null)
                {
                    yield return coConv;
                    yield break;
                }

                if ((forceGlobalOnly || useGlobalFallback) && global != null && TryHandleAsync(global, actionId, payload, out var coGlobal) && coGlobal != null)
                {
                    yield return coGlobal;
                }
            }
        }

        private ConversationActionSet FindSet(string key)
        {
            if (string.IsNullOrEmpty(key) || conversations == null) return null;
            for (int i = 0; i < conversations.Count; i++)
            {
                var s = conversations[i];
                if (s != null && string.Equals(s.conversationKey, key, StringComparison.Ordinal))
                    return s;
            }
            return null;
        }

        private static bool TryInvokeBinding(ConversationActionSet set, string actionId, string payload)
        {
            if (set == null || set.bindings == null) return false;

            for (int i = 0; i < set.bindings.Count; i++)
            {
                var b = set.bindings[i];
                if (b != null && string.Equals(b.actionId, actionId, StringComparison.Ordinal))
                {
                    try
                    {
                        b.onInvoke?.Invoke(payload);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[DialogActionRunner] Exception in binding '{actionId}': {ex.Message}");
                    }
                    return true;
                }
            }
            return false;
        }

        private static bool TryHandleAsync(ConversationActionSet set, string actionId, string payload, out IEnumerator routine)
        {
            routine = null;
            if (set == null || set.handlers == null) return false;

            for (int i = 0; i < set.handlers.Count; i++)
            {
                var mb = set.handlers[i];
                if (mb != null && mb is IActionHandler handler && handler.CanHandle(actionId))
                {
                    routine = handler.Handle(actionId, payload);
                    return true;
                }
            }
            return false;
        }
        #endregion
    }

    /// <summary>
    /// Group of bindings/handlers for a specific conversation (key = dialogID).
    /// </summary>
    [Serializable]
    public class ConversationActionSet
    {
        [Tooltip("Use your dialogID here (the same you pass to DialogManager.PlayDialogByID).")]
        public string conversationKey;

        [Tooltip("UnityEvent bindings keyed by actionId (synchronous).")]
        public List<ActionBinding> bindings = new List<ActionBinding>();

        [Tooltip("MonoBehaviours implementing IActionHandler for async, waitable actions.")]
        public List<MonoBehaviour> handlers = new List<MonoBehaviour>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (bindings == null) bindings = new List<ActionBinding>();
            if (handlers == null) handlers = new List<MonoBehaviour>();
        }
#endif
    }

    /// <summary>
    /// UnityEvent binding for a single action id.
    /// </summary>
    [Serializable]
    public class ActionBinding
    {
        [Header("Action")]
        [Tooltip("Must match ActionNode.actionId (or the id passed to the runner).")]
        public string actionId;

        [Header("UnityEvent Callback")]
        [Tooltip("Tip: To pass a constant string (e.g., JSON), select the NON-dynamic overload (shows “(String)”). Dynamic mode forwards the runtime payload.")]
        [InspectorName("On Invoke (string payload)")]
        public UnityEvent<string> onInvoke;
    }
}
