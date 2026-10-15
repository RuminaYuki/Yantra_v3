#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Read-only questions about what a table and its file hold: what the table uses, what nothing uses,
    // which pieces belong to a sub-state machine. Nothing here changes an asset (see EmbeddedAssets).
    internal static class TableContents
    {
        // ---------- What a table uses ----------

        // States, Actions and Conditions: what the Contents panel lists.
        public static bool IsContentItem(Object item)
        {
            return item is StateSO || item is StateActionSO || item is StateConditionSO;
        }

        public static bool IsInSameFile(Object item, Object other)
        {
            return AssetDatabase.GetAssetPath(item) == AssetDatabase.GetAssetPath(other);
        }

        // Every State, Action and Condition reachable from the table through its fields: states ->
        // actions, transitions -> conditions, wrapper conditions -> the conditions inside them.
        // Stops at other tables, so a sub-state machine's contents stay in its own table.
        public static List<ScriptableObject> CollectUsed(TransitionTableSO table)
        {
            var used = new List<ScriptableObject>();
            var visited = new HashSet<ScriptableObject> { table };
            var pending = new Stack<ScriptableObject>();
            pending.Push(table);

            while (pending.Count > 0)
            {
                SerializedProperty property = new SerializedObject(pending.Pop()).GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference ||
                        property.objectReferenceValue is not ScriptableObject child ||
                        child is TransitionTableSO ||
                        child is ExposedParametersSO ||   // naming a field doesn't use the item
                        !visited.Add(child))
                    {
                        continue;
                    }

                    if (IsContentItem(child))
                    {
                        used.Add(child);
                    }

                    pending.Push(child);
                }
            }

            return used;
        }

        // The item, and everything it uses (directly or through other things) that lives in its file,
        // following into sub-state machine tables embedded there.
        public static List<ScriptableObject> ReachableInSameFile(ScriptableObject item)
        {
            string path = AssetDatabase.GetAssetPath(item);
            var found = new List<ScriptableObject> { item };
            var visited = new HashSet<ScriptableObject> { item };
            var pending = new Stack<ScriptableObject>();
            pending.Push(item);

            while (pending.Count > 0)
            {
                ScriptableObject current = pending.Pop();
                SerializedProperty property = new SerializedObject(current).GetIterator();
                while (property.Next(true))
                {
                    // A Contents folder entry only files an item away, and an exposed parameter only
                    // names one of its fields; neither uses it.
                    if (current is TransitionTableSO &&
                        (property.propertyPath.StartsWith(StateMachineFields.ContentsFolders, StringComparison.Ordinal) ||
                         property.propertyPath.StartsWith(StateMachineFields.ExposedParameters, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue is ScriptableObject child &&
                        AssetDatabase.GetAssetPath(child) == path &&
                        visited.Add(child))
                    {
                        found.Add(child);
                        pending.Push(child);
                    }
                }
            }

            return found;
        }

        // Every State, Action and Condition inside the table's file (its sub-assets).
        public static IEnumerable<ScriptableObject> CollectEmbedded(TransitionTableSO table)
        {
            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(table))
                .OfType<ScriptableObject>()
                .Where(IsContentItem);
        }

        // Items in the file (and leftover sub-state machine tables) that the file's main table doesn't
        // reach, following into sub-state machine tables embedded in the same file. A leftover sub-table
        // and the states inside it only use each other, so they count as unused too.
        public static List<ScriptableObject> UnusedInFile(TransitionTableSO table)
        {
            string path = AssetDatabase.GetAssetPath(table);

            var root = AssetDatabase.LoadMainAssetAtPath(path) as ScriptableObject;
            if (root == null)
            {
                root = table;
            }

            var reached = new HashSet<ScriptableObject>(ReachableInSameFile(root));

            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<ScriptableObject>()
                .Where(asset => !reached.Contains(asset) && (IsContentItem(asset) || asset is TransitionTableSO))
                .ToList();
        }

        // Target -> every (object, field path) in the table and what it uses that points at it. The
        // table's editor-only bookkeeping (node positions, Contents folders) doesn't count as using.
        public static Dictionary<Object, List<(Object Source, string Path)>> CollectReferences(
            TransitionTableSO table,
            IEnumerable<ScriptableObject> used)
        {
            var references = new Dictionary<Object, List<(Object Source, string Path)>>();

            foreach (ScriptableObject source in used.Prepend(table))
            {
                SerializedProperty property = new SerializedObject(source).GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference ||
                        property.objectReferenceValue == null ||
                        (source == table && IsEditorOnly(property.propertyPath)))
                    {
                        continue;
                    }

                    Object target = property.objectReferenceValue;
                    if (!references.TryGetValue(target, out List<(Object Source, string Path)> list))
                    {
                        list = new List<(Object Source, string Path)>();
                        references[target] = list;
                    }

                    list.Add((source, property.propertyPath));
                }
            }

            return references;
        }

        private static bool IsEditorOnly(string path)
        {
            return path.StartsWith(StateMachineFields.NodePositions, StringComparison.Ordinal) ||
                path.StartsWith(StateMachineFields.ContentsFolders, StringComparison.Ordinal) ||
                path.StartsWith(StateMachineFields.ExposedParameters, StringComparison.Ordinal);
        }

        // "_transitions.Array.data[3].ConditionGroups..." -> 3; anything else -> -1.
        private static int TransitionIndexOf(string path)
        {
            string prefix = StateMachineFields.Transitions + ".Array.data[";
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
            {
                return -1;
            }

            int end = path.IndexOf(']', prefix.Length);
            return int.Parse(path.Substring(prefix.Length, end - prefix.Length));
        }

        // ---------- Actions in a state ----------

        // The state's action list, without empty slots or repeats.
        public static List<ScriptableObject> ActionsOf(StateSO state)
        {
            var result = new List<ScriptableObject>();
            SerializedProperty actions = new SerializedObject(state).FindProperty(StateMachineFields.Actions);

            for (int i = 0; i < actions.arraySize; i++)
            {
                if (actions.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableObject action &&
                    !result.Contains(action))
                {
                    result.Add(action);
                }
            }

            return result;
        }

        // The Exit Sub State Machine actions in a state's action list.
        public static List<ScriptableObject> ExitActionsOf(StateSO state)
        {
            return ActionsOf(state).Where(action => action is ExitSubStateMachineActionSO).ToList();
        }

        // ---------- Sub-state machines ----------

        // What belongs to one sub-state machine, shown under it like a folder (the way Yantra_v3 keeps a
        // "KrasueAttack_State" folder with its own Action / Condition / TransitionTable inside).
        public class SubStateParts
        {
            public TransitionTableSO Table;
            public readonly List<ScriptableObject> Actions = new List<ScriptableObject>();
            public readonly List<ScriptableObject> ExitConditions = new List<ScriptableObject>();
        }

        // For each sub-state machine in the table: its child table, the actions only it uses, and the
        // conditions only its outgoing transitions use (e.g. "Sub State Machine Exited"), plus exit
        // conditions made for it that no arrow uses yet. Anything also used elsewhere isn't included.
        public static Dictionary<StateSO, SubStateParts> FindSubStateParts(
            TransitionTableSO table,
            HashSet<ScriptableObject> used)
        {
            var result = new Dictionary<StateSO, SubStateParts>();
            Dictionary<Object, List<(Object Source, string Path)>> references = CollectReferences(table, used);
            SerializedProperty transitions = new SerializedObject(table).FindProperty(StateMachineFields.Transitions);

            foreach (StateSO state in used.OfType<StateSO>())
            {
                TransitionTableSO subTable = StateNodeView.FindSubTables(state).FirstOrDefault();
                if (subTable == null)
                {
                    continue;
                }

                var parts = new SubStateParts { Table = subTable };

                parts.Actions.AddRange(ActionsOf(state)
                    .Where(action => references[action].All(reference => reference.Source == state)));

                // Indexes of the transitions that leave this state.
                var outgoing = new HashSet<int>();
                for (int i = 0; i < transitions.arraySize; i++)
                {
                    if (transitions.GetArrayElementAtIndex(i).FindPropertyRelative("FromState").objectReferenceValue == state)
                    {
                        outgoing.Add(i);
                    }
                }

                foreach (StateConditionSO condition in used.OfType<StateConditionSO>())
                {
                    if (references.TryGetValue(condition, out List<(Object Source, string Path)> conditionReferences) &&
                        conditionReferences.All(reference =>
                            reference.Source == table && outgoing.Contains(TransitionIndexOf(reference.Path))))
                    {
                        parts.ExitConditions.Add(condition);
                    }
                }

                parts.ExitConditions.AddRange(WaitingExitConditionsOf(table, state, used));
                result[state] = parts;
            }

            return result;
        }

        // Exit conditions made for this sub-state machine (named after it, see EmbeddedAssets) that no
        // arrow uses yet, e.g. "AttackToPatrolSubSMExited_Condition" for "Attack_State".
        private static IEnumerable<SubStateMachineExitedConditionSO> WaitingExitConditionsOf(
            TransitionTableSO table,
            StateSO state,
            HashSet<ScriptableObject> used)
        {
            string prefix = EmbeddedAssets.StatePrefix(state.name);

            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(table))
                .OfType<SubStateMachineExitedConditionSO>()
                .Where(condition => !used.Contains(condition) && EmbeddedAssets.IsExitConditionOf(condition, prefix));
        }

        // Inside a sub-state machine's table: the exit states (states running an Exit Sub State Machine
        // action), each with the exit actions only it uses.
        public static Dictionary<StateSO, List<ScriptableObject>> FindExitStates(
            TransitionTableSO table,
            HashSet<ScriptableObject> used)
        {
            var exits = new Dictionary<StateSO, List<ScriptableObject>>();
            Dictionary<Object, List<(Object Source, string Path)>> references = CollectReferences(table, used);

            foreach (StateSO state in used.OfType<StateSO>())
            {
                List<ScriptableObject> exitActions = ExitActionsOf(state);
                if (exitActions.Count > 0)
                {
                    exits[state] = exitActions
                        .Where(action => references[action].All(reference => reference.Source == state))
                        .ToList();
                }
            }

            return exits;
        }

        // The table and sub-state that run `childTable`, when both are in the same file (embedded).
        public static (TransitionTableSO Table, StateSO State) FindParentInFile(TransitionTableSO childTable)
        {
            foreach (TransitionTableSO table in AssetDatabase
                .LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(childTable))
                .OfType<TransitionTableSO>())
            {
                if (table == childTable)
                {
                    continue;
                }

                foreach (StateSO state in CollectUsed(table).OfType<StateSO>())
                {
                    if (StateNodeView.FindSubTables(state).Contains(childTable))
                    {
                        return (table, state);
                    }
                }
            }

            return (null, null);
        }
    }
}

#endif
