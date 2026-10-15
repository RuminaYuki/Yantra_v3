#if UNITY_EDITOR

using System.Collections.Generic;

namespace SDFcl.GamePlay.QuestSystem.GraphEditor
{
    // Finds mistakes in a quest graph. Messages are keyed by node guid so node views can show them.
    public static class QuestGraphValidator
    {
        public static Dictionary<string, List<string>> Validate(QuestGraphSO graph)
        {
            var issues = new Dictionary<string, List<string>>();
            if (graph == null) return issues;

            graph.BuildLookup();

            void Add(string guid, string message)
            {
                if (!issues.TryGetValue(guid, out var list)) issues[guid] = list = new List<string>();
                list.Add(message);
            }

            var seenQuests = new Dictionary<QuestSO, string>();

            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;
                var inputs = graph.GetInputs(node.guid);
                var outputs = graph.GetOutputs(node.guid);

                switch (node)
                {
                    case QuestNode qn:
                        if (qn.quest == null)
                        {
                            Add(node.guid, "No QuestSO assigned.");
                            break;
                        }
                        if (seenQuests.ContainsKey(qn.quest)) Add(node.guid, "This QuestSO is used by another node too.");
                        else seenQuests[qn.quest] = node.guid;

                        if (inputs.Count > 1) Add(node.guid, "More than one input. Put an AND or OR gate in front.");
                        if (qn.quest.Objectives.Count == 0) Add(node.guid, "No objectives: it can only be completed from code / events.");
                        var ids = new HashSet<string>();
                        foreach (var o in qn.quest.Objectives)
                        {
                            if (string.IsNullOrEmpty(o.id)) Add(node.guid, "An objective has an empty id.");
                            else if (!ids.Add(o.id)) Add(node.guid, $"Objective id '{o.id}' is used twice.");
                        }
                        break;

                    case GateNode gate:
                        if (inputs.Count == 0) Add(node.guid, "Gate has no inputs, it can never pass.");
                        else if (inputs.Count == 1) Add(node.guid, "Gate has only one input. It is not needed.");
                        if (outputs.Count == 0) Add(node.guid, "Gate output is not connected.");
                        break;

                    case ResetNode reset:
                        if (inputs.Count == 0) Add(node.guid, "Reset has no input, it never fires.");
                        if (inputs.Count > 1) Add(node.guid, "More than one input. Put an AND or OR gate in front.");
                        if (reset.targets.Count == 0) Add(node.guid, "Reset has no target quests.");
                        foreach (var t in reset.targets)
                        {
                            if (t == null) Add(node.guid, "A target is empty.");
                            else if (graph.FindQuestNode(t) == null) Add(node.guid, $"Target '{t.Title}' is not in this graph.");
                        }
                        break;
                }
            }

            foreach (var guid in FindCycleNodes(graph))
                Add(guid, "Part of a loop. Edges cannot loop back; use a Reset node to repeat a chain.");

            return issues;
        }

        // Nodes that sit on a directed cycle (DFS with colors).
        private static HashSet<string> FindCycleNodes(QuestGraphSO graph)
        {
            var result = new HashSet<string>();
            var color = new Dictionary<string, int>(); // 0 = new, 1 = on stack, 2 = done
            var stack = new List<string>();

            void Visit(string guid)
            {
                color[guid] = 1;
                stack.Add(guid);
                foreach (var next in graph.GetOutputs(guid))
                {
                    color.TryGetValue(next, out int c);
                    if (c == 0) Visit(next);
                    else if (c == 1)
                    {
                        // Everything from 'next' to the top of the stack is in the cycle.
                        for (int i = stack.IndexOf(next); i < stack.Count; i++) result.Add(stack[i]);
                    }
                }
                stack.RemoveAt(stack.Count - 1);
                color[guid] = 2;
            }

            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;
                color.TryGetValue(node.guid, out int c);
                if (c == 0) Visit(node.guid);
            }
            return result;
        }
    }
}

#endif
