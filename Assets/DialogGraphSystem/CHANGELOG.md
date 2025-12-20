CHANGELOG  
All notable changes to this project will be documented in this file.  

Version 1.3.2 – 2025-11-08  
ADDED  
• Universal Input Compatibility: The system now supports both Old Input Manager and New Input System setups without requiring manual changes.  
  Works across keyboard, mouse, touch, controller, and VR input sources.  
  Added cooldown logic to prevent accidental double-skip when advancing dialog lines.  
  Fully compatible with Unity’s Active Input Handling modes (Old, New, or Both).  

• InputHelper Utility: New runtime helper that centralizes input detection for dialog advancement.  
  Supports keyboard, mouse, touch, gamepad, and VR inputs for future-proof flexibility.  

CHANGED  
• Runtime safety: Fixed compile issues in projects using only the New Input System by improving backend handling.  
• Cross-version compatibility: Verified stable behavior across Unity 2021–2025 LTS versions.  
• Minor editor and runtime adjustments for consistent import and initialization.  

NOTES  
• If your project uses Input System (New) only, make sure your EventSystem uses the Input System UI Input Module instead of the Standalone Input Module to avoid UI exceptions.  
• Requires Unity’s Input System package (com.unity.inputsystem, version 1.7.0 or newer).  
  To install: Window → Package Manager → Unity Registry → Input System → Install.  

Version 1.3.1 – 2025-09-16  
ADDED  
• Action Nodes and Handlers: New ActionNode type with actionId + payloadJson.  
  Coroutine-based IActionHandler lets actions block dialog until completion.  

• Demo Handlers: DemoHandler_Countdown (blocking countdown) and DemoUnityEventActions (UnityEvents) included.  

• Runtime UI Bridge: DialogUIController with explicit APIs to show or hide the panel,  
  set text, speaker, portrait, bind click, toggle autoplay, and skip UI.  

• Helper Utilities: PayloadHelper (typed parsing, color/vector parsing, token interpolation),  
  TextResources (paths), and small audio samples for the demo.  

• Editor Views per Node: StartNodeView, DialogNodeView, ChoiceNodeView, ActionNodeView, EndNodeView, and DialogEdge.  

• Scenes: Added ActionDialogDemo showcasing action chains and wait-for-completion flows.  

• Build Hygiene: Assets.csc.rsp to generate XML docs and suppress CS1591.  

CHANGED  
• Data Model: Split node classes (Start, Dialog, Choice, Action, End) inheriting from BaseNode with explicit NodeKind for cleaner branching and rendering.  
• Editor Structure: All GraphView code is under Scripts/Editor; runtime assembly is leaner.  
• Samples: Example conversations now ship as ScriptableObject graphs under Resources/Conversation.  
• Runtime UI: Renamed panel script to DialogUIController and clarified method names and responsibilities.  

FIXED  
• Improved link handling between typed node views and edges.  
• Consistent autoplay icon initialization and panel click listener binding.  
• Minor improvements to history item presentation (lines vs choices).  

Version 1.3.0  
ADDED  
• Characters Sidebar: Rescan speakers, set portrait per speaker, and apply to nodes.  
• JSON Import and Export: Round-trip JSON via DialogJsonIOWindow (backup, version control, AI workflows).  
• Runtime UI Panel (UGUI) with typing effect, choices, skip line, skip all, and autoplay.  
• DialogManager API: Play by graph or ID, with hooks for line start and completion.  
• Sample Scene: Three demo conversations and portraits to showcase flow.  
• Minimap in the editor and improved toolbar (Add Node, Save, Clear, Hide/Show Characters).  
• Asmdefs and Namespaces: DialogSystem.Runtime and DialogSystem.Editor.  

CHANGED  
• Refined node layout and port styling for improved readability.  
• Safer defaults: No debug logs in release; editor code isolated in Editor folders.  

FIXED  
• Edge and link instability on fast undo and redo.  
• Minor GUID and entry-node validation issues when duplicating nodes.  

Version 1.2 
ADDED  
• Dialogue History: Data model, DialogueHistoryView, and DialogueHistoryPanel prefab.  
  Manager pauses autoplay while open.  

• JSON Import and Export Window: Export and Import tabs, JSON preview, drag-and-drop, recent files, and backup/undo safety.  

• GUID Policies on Import: Preserve, Regenerate on conflict, or Regenerate all.  

• Graph Editor Features: Minimap, duplicate selection, safe delete (cleans links), undo/redo awareness.  

• Node Fields: Display time (seconds), audio clip, portrait preview, entry badge, and Auto Next port.  

• UI Prefabs: DialogUI_Panel, Choice_Btn, and DialogueHistoryPanel.  

• Runtime Events: Line shown, choice picked, conversation reset, and Play by Type mapping.  

CHANGED  
• Reorganized folders into _Scripts/Runtime and _Scripts/Editor, plus _Scenes, Prefabs, and Resources.  
• DialogManager refactored into a MonoSingleton with clearer UI references and options (typing speed, skip, auto-advance).  

FIXED  
• Safer asset deletion from graph editor (prevents dangling references).  
• Multiple UX polish improvements across graph toolbar and node inspectors.  

Version 1.0  
INITIAL  
• Basic dialog graph asset with entry node and choices.  
• Early graph editor (add or remove nodes, connect ports).  
• Simple runtime playback (speaker, text, choices).  
• Basic JSON export utility.  
• Minimal demo UI.  
