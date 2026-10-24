#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // A variant table's graph also shows what it gets from its Base (TransitionTableSO._base): the Base's
    // initial state when it has none, the Base's transitions (the ones it removed, _removedIds, in faint red),
    // and the states only those use. They're drawn faint and can't be selected or deleted here, since their
    // data is in the Base. Right-clicking an arrow removes / restores it in this table (.Variant.cs).
    public partial class StateMachineGraphView
    {
        private sealed class InheritedParts
        {
            public StateSO InitialState;
            public readonly List<(StateSO From, StateSO To, InheritedTransition Item)> Transitions =
                new List<(StateSO From, StateSO To, InheritedTransition Item)>();
            public readonly List<(StateSO To, InheritedTransition Item)> AnyTransitions =
                new List<(StateSO To, InheritedTransition Item)>();
            public readonly HashSet<StateSO> States = new HashSet<StateSO>();
        }

        private static InheritedParts ReadInherited(SerializedObject serializedTable, TransitionTableSO baseTable)
        {
            var parts = new InheritedParts();
            if (baseTable == null)
            {
                return parts;
            }

            var removed = new HashSet<string>();
            SerializedProperty removedIds = serializedTable.FindProperty("_removedIds");
            for (int i = 0; i < removedIds.arraySize; i++)
            {
                removed.Add(removedIds.GetArrayElementAtIndex(i).stringValue);
            }

            void AddState(StateSO state)
            {
                if (state != null)
                {
                    parts.States.Add(state);
                }
            }

            parts.InitialState = baseTable.EffectiveInitialState;
            AddState(parts.InitialState);

            var overridden = new HashSet<string>();
            SerializedProperty overrides = serializedTable.FindProperty("_transitionOverrides");
            for (int i = 0; i < overrides.arraySize; i++)
            {
                overridden.Add(overrides.GetArrayElementAtIndex(i).FindPropertyRelative("_transitionId").stringValue);
            }

            // Id -> the state this table sends it to instead (drawn there, in blue).
            var retargets = new Dictionary<string, StateSO>();
            SerializedProperty retargetList = serializedTable.FindProperty("_transitionRetargets");
            for (int i = 0; i < retargetList.arraySize; i++)
            {
                SerializedProperty entry = retargetList.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("_toState").objectReferenceValue is StateSO to)
                {
                    retargets[entry.FindPropertyRelative("_transitionId").stringValue] = to;
                }
            }

            InheritedTransition Describe(string id, TransitionTableSO.ConditionGroupUsage[] groups)
            {
                bool isOverridden = overridden.Contains(id);
                bool isRetargeted = retargets.ContainsKey(id);
                string summary = Summary(groups) +
                    (isOverridden ? " (overridden here)" : "") +
                    (isRetargeted ? " (target changed here)" : "");
                return new InheritedTransition(id, summary, removed.Contains(id), isOverridden, ConditionsIn(groups), isRetargeted);
            }

            StateSO ToHere(string id, StateSO baseTo) => retargets.TryGetValue(id, out StateSO to) ? to : baseTo;

            foreach (TransitionTableSO.TransitionItem item in baseTable.EffectiveTransitions)
            {
                StateSO to = ToHere(item.Id, item.ToState);

                // Same as the table's own: broken items and self transitions aren't drawn.
                if (item.FromState == null || to == null || item.FromState == to)
                {
                    continue;
                }

                parts.Transitions.Add((item.FromState, to, Describe(item.Id, item.ConditionGroups)));
                AddState(item.FromState);
                AddState(to);
            }

            foreach (TransitionTableSO.AnyTransitionItem item in baseTable.EffectiveAnyTransitions)
            {
                StateSO to = ToHere(item.Id, item.ToState);
                if (to == null)
                {
                    continue;
                }

                parts.AnyTransitions.Add((to, Describe(item.Id, item.ConditionGroups)));
                AddState(to);
            }

            return parts;
        }

        // "when A and not B or C" (groups are OR, conditions in a group are AND), or "always".
        private static string Summary(TransitionTableSO.ConditionGroupUsage[] groups)
        {
            var parts = new List<string>();

            foreach (TransitionTableSO.ConditionGroupUsage group in groups ?? new TransitionTableSO.ConditionGroupUsage[0])
            {
                var names = new List<string>();
                foreach (TransitionTableSO.ConditionUsage usage in group.Conditions ?? new TransitionTableSO.ConditionUsage[0])
                {
                    string name = usage.Condition != null ? usage.Condition.name : "(missing)";
                    names.Add(usage.ExpectedResult == TransitionTableSO.Result.False ? $"not {name}" : name);
                }

                if (names.Count > 0)
                {
                    parts.Add(string.Join(" and ", names));
                }
            }

            return parts.Count > 0 ? "when " + string.Join(" or ", parts) : "always";
        }

        private static IReadOnlyList<StateConditionSO> ConditionsIn(TransitionTableSO.ConditionGroupUsage[] groups)
        {
            var conditions = new List<StateConditionSO>();

            foreach (TransitionTableSO.ConditionGroupUsage group in groups ?? new TransitionTableSO.ConditionGroupUsage[0])
            foreach (TransitionTableSO.ConditionUsage usage in group.Conditions ?? new TransitionTableSO.ConditionUsage[0])
            {
                if (usage.Condition != null && !conditions.Contains(usage.Condition))
                {
                    conditions.Add(usage.Condition);
                }
            }

            return conditions;
        }

        // One faint arrow per From -> To pair (Any State: per target), like the table's own.
        private void CreateInheritedEdges(
            InheritedParts inherited,
            Node anyStateNode,
            Dictionary<StateSO, StateNodeView> stateNodes)
        {
            var localGroups = new Dictionary<(StateSO From, StateSO To), (List<int> Indices, List<InheritedTransition> Items)>();
            for (int i = 0; i < inherited.Transitions.Count; i++)
            {
                (StateSO from, StateSO to, InheritedTransition item) = inherited.Transitions[i];
                if (!localGroups.TryGetValue((from, to), out var group))
                {
                    group = (new List<int>(), new List<InheritedTransition>());
                    localGroups.Add((from, to), group);
                }

                group.Indices.Add(i);
                group.Items.Add(item);
            }

            foreach (var pair in localGroups)
            {
                AddElement(new TransitionEdgeView(
                    stateNodes[pair.Key.From], stateNodes[pair.Key.To], pair.Value.Indices, false, true, pair.Value.Items));
            }

            var anyGroups = new Dictionary<StateSO, (List<int> Indices, List<InheritedTransition> Items)>();
            for (int i = 0; i < inherited.AnyTransitions.Count; i++)
            {
                (StateSO to, InheritedTransition item) = inherited.AnyTransitions[i];
                if (!anyGroups.TryGetValue(to, out var group))
                {
                    group = (new List<int>(), new List<InheritedTransition>());
                    anyGroups.Add(to, group);
                }

                group.Indices.Add(i);
                group.Items.Add(item);
            }

            foreach (var pair in anyGroups)
            {
                AddElement(new TransitionEdgeView(
                    anyStateNode, stateNodes[pair.Key], pair.Value.Indices, true, true, pair.Value.Items));
            }
        }

        // Where the Bases placed their nodes (nearest Base first), for nodes this table never moved.
        private static void AddBasePositions(TransitionTableSO baseTable, Dictionary<StateSO, Vector2> positions)
        {
            var visited = new HashSet<TransitionTableSO>();
            for (TransitionTableSO current = baseTable; current != null && visited.Add(current); current = BaseOf(current))
            {
                foreach (KeyValuePair<StateSO, Vector2> pair in ReadNodePositions(new SerializedObject(current)))
                {
                    if (!positions.ContainsKey(pair.Key))
                    {
                        positions[pair.Key] = pair.Value;
                    }
                }
            }
        }

        // The table's Base, or null when it has none or the chain loops back (it would never end).
        internal static TransitionTableSO SafeBaseOf(TransitionTableSO table)
        {
            TransitionTableSO first = BaseOf(table);
            var visited = new HashSet<TransitionTableSO> { table };

            for (TransitionTableSO current = first; current != null; current = BaseOf(current))
            {
                if (!visited.Add(current))
                {
                    return null;
                }
            }

            return first;
        }

        internal static TransitionTableSO BaseOf(TransitionTableSO table)
        {
            return new SerializedObject(table).FindProperty("_base").objectReferenceValue as TransitionTableSO;
        }
    }
}

#endif
