using System.Collections.Generic;

namespace SDFcl.GamePlay.QuestSystem
{
    // Turns a quest's unlock condition into "routes" for display:
    // the quest unlocks when every quest in ANY one route is done.
    // Example: OR(AND(Quest 3, Sup B), Sup C) -> [Quest 3, Sup B] or [Sup C].
    public static class QuestRouteUtility
    {
        private const int MaxRoutes = 16;
        private const int MaxDepth = 64;

        // Routes that unlock this quest. Empty list = no condition (starts unlocked).
        public static List<List<QuestSO>> GetUnlockRoutes(QuestGraphSO graph, QuestSO quest)
        {
            var result = new List<List<QuestSO>>();
            if (graph == null || quest == null) return result;

            var node = graph.FindQuestNode(quest);
            if (node == null) return result;

            var inputs = graph.GetInputs(node.guid);
            if (inputs.Count == 0) return result;

            return RoutesOf(graph, inputs[0], 0);
        }

        private static List<List<QuestSO>> RoutesOf(QuestGraphSO graph, string guid, int depth)
        {
            var routes = new List<List<QuestSO>>();
            if (depth > MaxDepth) return routes;

            switch (graph.GetNode(guid))
            {
                case QuestNode qn when qn.quest != null:
                    routes.Add(new List<QuestSO> { qn.quest });
                    break;

                case GateNode gate when gate.gateType == GateType.Or:
                    foreach (var input in graph.GetInputs(guid))
                    {
                        foreach (var r in RoutesOf(graph, input, depth + 1))
                        {
                            if (routes.Count >= MaxRoutes) return routes;
                            routes.Add(r);
                        }
                    }
                    break;

                case GateNode _:
                    // AND: every combination of one route from each input.
                    routes.Add(new List<QuestSO>());
                    foreach (var input in graph.GetInputs(guid))
                    {
                        var inputRoutes = RoutesOf(graph, input, depth + 1);
                        if (inputRoutes.Count == 0) return new List<List<QuestSO>>();

                        var combined = new List<List<QuestSO>>();
                        foreach (var a in routes)
                        {
                            foreach (var b in inputRoutes)
                            {
                                if (combined.Count >= MaxRoutes) break;
                                var merged = new List<QuestSO>(a);
                                foreach (var q in b) if (!merged.Contains(q)) merged.Add(q);
                                combined.Add(merged);
                            }
                        }
                        routes = combined;
                    }
                    break;
            }
            return routes;
        }
    }
}
