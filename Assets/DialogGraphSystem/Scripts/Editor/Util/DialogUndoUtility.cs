using UnityEditor;
using UnityEngine;
using DialogSystem.Runtime.Models;

namespace DialogSystem.EditorTools.Util
{
    public static class DialogUndoUtility
    {
        // For future debug logs, if you ever add them.
        [SerializeField] private static bool doDebug = true;

        /// <summary>
        /// Records an undo snapshot for the DialogGraph root.
        /// Use this before structural changes: adding/removing nodes, links, etc.
        /// </summary>
        public static void RecordGraph(string label, DialogGraph graph)
        {
            if (graph == null) return;
            Undo.RegisterCompleteObjectUndo(graph, label);
        }

        /// <summary>
        /// Records an undo snapshot for a single node ScriptableObject.
        /// Use this for small, local changes (text, speaker, flags…).
        /// </summary>
        public static void RecordNode(string label, ScriptableObject node)
        {
            if (node == null) return;
            Undo.RecordObject(node, label);
        }

        /// <summary>
        /// Records undo for both the graph and a node in a single step.
        /// Helpful when a node change also affects graph data (links, lists, etc.).
        /// </summary>
        public static void RecordGraphAndNode(string label, DialogGraph graph, ScriptableObject node)
        {
            if (graph == null && node == null) return;

            if (graph != null && node != null)
            {
                Undo.RegisterCompleteObjectUndo(new Object[] { graph, node }, label);
            }
            else if (graph != null)
            {
                Undo.RegisterCompleteObjectUndo(graph, label);
            }
            else
            {
                Undo.RecordObject(node, label);
            }
        }

        /// <summary>
        /// Use when creating a new node sub-asset and adding it to the graph.
        /// </summary>
        public static void RegisterCreatedNode(string label, DialogGraph graph, ScriptableObject node)
        {
            if (node == null) return;

            Undo.RegisterCreatedObjectUndo(node, label);

            if (graph != null)
            {
                Undo.RegisterCompleteObjectUndo(graph, label);
            }
        }

        /// <summary>
        /// Use when deleting a node sub-asset from the graph.
        /// Wraps graph snapshot + Undo.DestroyObjectImmediate.
        /// </summary>
        public static void DestroyNodeWithUndo(string label, DialogGraph graph, ScriptableObject node)
        {
            if (node == null) return;

            if (graph != null)
            {
                Undo.RegisterCompleteObjectUndo(graph, label);
            }

            Undo.DestroyObjectImmediate(node);
        }
    }
}
