using UnityEngine;

namespace DialogSystem.Runtime.Models.Nodes
{
    /// <summary>
    /// Terminal node type indicating conversation end.
    /// </summary>
    [CreateAssetMenu(fileName = "EndNode", menuName = "Dialog System/Graph/End Node")]
    public class EndNode : BaseNode
    {
        public EndNode()
        {
            nodeKind = NodeKind.End;
        }
    }
}
