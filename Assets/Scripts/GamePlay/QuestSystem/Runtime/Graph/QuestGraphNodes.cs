using System;
using System.Collections.Generic;
using UnityEngine;

namespace SDFcl.GamePlay.QuestSystem
{
    public enum GateType
    {
        And,    // Passes when every input passes.
        Or      // Passes when any input passes.
    }

    // Base node. Stored with [SerializeReference] in QuestGraphSO.
    [Serializable]
    public abstract class QuestGraphNode
    {
        public string guid = Guid.NewGuid().ToString("N");
        public Vector2 position;
    }

    [Serializable]
    public class QuestNode : QuestGraphNode
    {
        public QuestSO quest;
    }

    [Serializable]
    public class GateNode : QuestGraphNode
    {
        public GateType gateType = GateType.And;

        [Tooltip("OR only: what happens to unfinished quests on the routes that lost.")]
        public LoserPolicy loserPolicy = LoserPolicy.Keep;
    }

    // When its input passes, the target quests go back to Locked and are evaluated again.
    // Used to loop a whole chain (A -> B -> C -> Reset[A, B, C]).
    [Serializable]
    public class ResetNode : QuestGraphNode
    {
        public List<QuestSO> targets = new List<QuestSO>();

        [Tooltip("Also clear objective progress of the targets.")]
        public bool clearProgress = true;
    }

    // Edge from an output port to an input port.
    // Rule: a QuestNode or ResetNode has at most one input edge. Gates can have many.
    [Serializable]
    public class QuestGraphEdge
    {
        public string fromNode;
        public string toNode;

        public QuestGraphEdge() { }

        public QuestGraphEdge(string from, string to)
        {
            fromNode = from;
            toNode = to;
        }
    }
}
