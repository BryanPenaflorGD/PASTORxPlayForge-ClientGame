Dialog Graph System (v1.3.1)

Lightweight, production-ready dialogue system for Unity.
Author conversations with a node-based Graph Editor and ship a polished runtime with UGUI + TextMeshPro: typewriter effect, skip, autoplay, and history overlay.

New in 1.3.x: Action Nodes with a flexible Action Runner (UnityEvents + async handlers), plus a cleaner node model (Start / Dialog / Choice / Action / End).

✨ Features

Node-based Graph Editor (Start, Dialog, Choice, Action, End)

JSON Import/Export for backups or AI-assisted authoring

UGUI + TextMeshPro runtime with typewriter, skip, and autoplay

History/Backlog overlay with distinct styling for lines/choices

Action Nodes for triggering UnityEvents or async/blocking handlers

Example demo scenes included

📦 Requirements

Unity 2021.3 LTS or newer

Packages: TextMesh Pro, UGUI

Graph editor uses UnityEditor.Experimental.GraphView (Editor-only)

🚀 Installation

Download and import the .unitypackage into a clean Unity project (LTS recommended).

Open one of the included demo scenes to try features immediately.

🎮 Demo Scenes

DialogDemo.unity — linear and branching basics.

ActionDialogDemo.unity — demonstrates Action Nodes, UnityEvent bindings, and blocking handlers.

⚡ Quickstart

Create a graph: Tools → Dialog Graph Editor → New Graph.

Add nodes (Start → Dialog → Choice/Action → End).

Place DialogManager and DialogUI_Panel in your scene.

Assign your graph in the DialogManager.

Start a conversation from code:

DialogSystem.Runtime.Core.DialogManager.Instance
    .PlayDialogByID("YourDialogID", () => Debug.Log("Dialog finished!"));

🔧 Actions

Action Nodes let you trigger gameplay or UI logic during a conversation.

Fire-and-forget: via UnityEvents.

Blocking: via IActionHandler coroutines.

Example use cases: UI flashes, sound effects, countdowns, fades, or custom gameplay events.

📚 Documentation

History Overlay — pause/resume and browse past lines/choices.

PayloadHelper — safely parse JSON payloads into strongly typed data.

Runtime API — start conversations, toggle autoplay, pause/resume history, listen to line/choice events.

📄 License

MIT License © 2025 Arjan Beka