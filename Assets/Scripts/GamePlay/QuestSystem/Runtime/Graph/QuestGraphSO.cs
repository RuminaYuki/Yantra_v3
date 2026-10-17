using System.Collections.Generic;
using UnityEngine;

namespace SDFcl.GamePlay.QuestSystem
{
    // The whole quest graph: nodes, edges and their positions.
    // Edit it with the Quest Graph window. QuestSO assets are stored inside it as sub-assets.
    [CreateAssetMenu(fileName = "QuestGraph", menuName = "Quest/Quest Graph")]
    public class QuestGraphSO : ScriptableObject
    {
        [SerializeReference] private List<QuestGraphNode> nodes = new List<QuestGraphNode>();
        [SerializeField] private List<QuestGraphEdge> edges = new List<QuestGraphEdge>();

        [Tooltip("Graph editor only: Y position where the Sup lane starts. Has no effect on logic.")]
        [SerializeField] private float laneSplitY = 220f;

        public IReadOnlyList<QuestGraphNode> Nodes => nodes;
        public IReadOnlyList<QuestGraphEdge> Edges => edges;
        public float LaneSplitY => laneSplitY;

        // Field names for SerializedObject access from editor code.
        public const string NodesField = nameof(nodes);
        public const string LaneSplitField = nameof(laneSplitY);

        public int IndexOfNode(string guid) => nodes.FindIndex(n => n != null && n.guid == guid);

        public QuestNode FindQuestNode(QuestSO quest)
        {
            foreach (var n in nodes)
            {
                if (n is QuestNode qn && qn.quest == quest) return qn;
            }
            return null;
        }

        // Lookups built on demand at runtime.
        private Dictionary<string, QuestGraphNode> _byGuid;
        private Dictionary<string, List<string>> _inputs;
        private Dictionary<string, List<string>> _outputs;

        public void BuildLookup()
        {
            _byGuid = new Dictionary<string, QuestGraphNode>();
            _inputs = new Dictionary<string, List<string>>();
            _outputs = new Dictionary<string, List<string>>();

            foreach (var n in nodes)
            {
                if (n == null) continue;
                _byGuid[n.guid] = n;
                _inputs[n.guid] = new List<string>();
                _outputs[n.guid] = new List<string>();
            }

            foreach (var e in edges)
            {
                if (!_byGuid.ContainsKey(e.fromNode) || !_byGuid.ContainsKey(e.toNode)) continue;
                _inputs[e.toNode].Add(e.fromNode);
                _outputs[e.fromNode].Add(e.toNode);
            }
        }

        public QuestGraphNode GetNode(string guid)
        {
            EnsureLookup();
            return guid != null && _byGuid.TryGetValue(guid, out var n) ? n : null;
        }

        public IReadOnlyList<string> GetInputs(string guid)
        {
            EnsureLookup();
            return _inputs.TryGetValue(guid, out var list) ? list : (IReadOnlyList<string>)System.Array.Empty<string>();
        }

        public IReadOnlyList<string> GetOutputs(string guid)
        {
            EnsureLookup();
            return _outputs.TryGetValue(guid, out var list) ? list : (IReadOnlyList<string>)System.Array.Empty<string>();
        }

        private void EnsureLookup()
        {
            if (_byGuid == null) BuildLookup();
        }

        private void OnEnable() => _byGuid = null; // Rebuild after domain reload / edits.

#if UNITY_EDITOR
        // ---- Editing API (used by the graph editor and the sample builder) ----

        public T AddNode<T>(T node) where T : QuestGraphNode
        {
            nodes.Add(node);
            _byGuid = null;
            return node;
        }

        public void RemoveNode(string guid)
        {
            nodes.RemoveAll(n => n == null || n.guid == guid);
            edges.RemoveAll(e => e.fromNode == guid || e.toNode == guid);
            _byGuid = null;
        }

        public bool AddEdge(string from, string to)
        {
            if (from == to || edges.Exists(e => e.fromNode == from && e.toNode == to)) return false;
            edges.Add(new QuestGraphEdge(from, to));
            _byGuid = null;
            return true;
        }

        public void RemoveEdge(string from, string to)
        {
            edges.RemoveAll(e => e.fromNode == from && e.toNode == to);
            _byGuid = null;
        }

        // Removes a deleted quest from every Reset node's target list.
        public void RemoveQuestReferences(QuestSO quest)
        {
            foreach (var n in nodes)
            {
                if (n is ResetNode r) r.targets.RemoveAll(t => t == null || t == quest);
            }
        }

        public void MarkChanged() => _byGuid = null;

        public QuestSO CreateQuestAsset(string title, QuestTrack track)
        {
            var quest = CreateInstance<QuestSO>();
            quest.name = title;
            quest.EditorInit(title, track);
            UnityEditor.AssetDatabase.AddObjectToAsset(quest, this);
            UnityEditor.EditorUtility.SetDirty(this);
            return quest;
        }

        // Builds the graph from our design doc so the runtime can be tested before the editor exists.
        [ContextMenu("Debug/Build Sample Graph")]
        private void BuildSampleGraph()
        {
            if (nodes.Count > 0 && !UnityEditor.EditorUtility.DisplayDialog(
                    "Build Sample Graph", "This adds sample nodes to the graph. Continue?", "Add", "Cancel"))
                return;

            QuestNode Q(string title, QuestTrack track, float x, float y) =>
                AddNode(new QuestNode { quest = CreateQuestAsset(title, track), position = new Vector2(x, y) });
            GateNode G(GateType type, float x, float y) =>
                AddNode(new GateNode { gateType = type, position = new Vector2(x, y) });

            var q1 = Q("Quest 1", QuestTrack.Main, 0, 0);
            var q2 = Q("Quest 2", QuestTrack.Main, 250, 0);
            var and1 = G(GateType.And, 450, 0);
            var q3 = Q("Quest 3", QuestTrack.Main, 600, 0);
            var and2 = G(GateType.And, 800, 0);
            var or1 = G(GateType.Or, 1000, 0);
            var final = Q("Final Quest", QuestTrack.Main, 1200, 0);
            var supA = Q("Sup A", QuestTrack.Sup, 400, 250);
            var supB = Q("Sup B", QuestTrack.Sup, 750, 250);
            var supC = Q("Sup C", QuestTrack.Sup, 1000, 450);

            AddEdge(q1.guid, q2.guid);
            AddEdge(q2.guid, and1.guid);
            AddEdge(q2.guid, supA.guid);
            AddEdge(supA.guid, and1.guid);
            AddEdge(and1.guid, q3.guid);
            AddEdge(q3.guid, and2.guid);
            AddEdge(q3.guid, supB.guid);
            AddEdge(q3.guid, supC.guid);
            AddEdge(supB.guid, and2.guid);
            AddEdge(and2.guid, or1.guid);
            AddEdge(supC.guid, or1.guid);
            AddEdge(or1.guid, final.guid);

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif
    }
}
