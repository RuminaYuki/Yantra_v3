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
        // A state that an action or condition only names in a field (e.g. a sub-state machine's
        // force exit state) isn't used by this table; it goes into referenced instead, when given.
        public static List<ScriptableObject> CollectUsed(TransitionTableSO table, List<StateSO> referenced = null)
        {
            var used = new List<ScriptableObject>();
            var visited = new HashSet<ScriptableObject> { table };
            var pending = new Stack<ScriptableObject>();
            pending.Push(table);

            while (pending.Count > 0)
            {
                ScriptableObject current = pending.Pop();
                bool isActionOrCondition = current is StateActionSO || current is StateConditionSO;

                SerializedProperty property = new SerializedObject(current).GetIterator();
                while (property.Next(true))
                {
                    // A Contents folder entry only files an item away; it doesn't use it (same as
                    // ReachableInSameFile). Otherwise anything put in a folder would never show as unused.
                    // A state's name in this graph (_stateDisplayNames) only names it, too.
                    if (current is TransitionTableSO &&
                        (property.propertyPath.StartsWith(StateMachineFields.ContentsFolders, StringComparison.Ordinal) ||
                         property.propertyPath.StartsWith(StateMachineFields.StateDisplayNames, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    // A variant's notes about the Base's items don't use them either: "don't run this
                    // action here" (an action change of kind Remove), the action order (only lists ones
                    // that run anyway) and field overrides (they name a field, like ExposedParameters).
                    if (current is TransitionTableSO && IsVariantNoteNotUse(property))
                    {
                        continue;
                    }

                    if (property.propertyType != SerializedPropertyType.ObjectReference ||
                        property.objectReferenceValue is not ScriptableObject child ||
                        child is TransitionTableSO ||
                        child is ExposedParametersSO)   // naming a field doesn't use the item
                    {
                        continue;
                    }

                    if (isActionOrCondition && child is StateSO referencedState)
                    {
                        if (referenced != null && !referenced.Contains(referencedState))
                        {
                            referenced.Add(referencedState);
                        }

                        continue;
                    }

                    if (!visited.Add(child))
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

        // See CollectUsed. propertyPath looks like "_actionChanges.Array.data[2]._action".
        private static bool IsVariantNoteNotUse(SerializedProperty property)
        {
            string path = property.propertyPath;

            // A replaced condition (_conditionReplaces ... _from) only names the Base's one; its _to runs.
            if (path.StartsWith("_actionOrders", StringComparison.Ordinal) ||
                path.StartsWith("_fieldOverrides", StringComparison.Ordinal) ||
                (path.StartsWith("_conditionReplaces", StringComparison.Ordinal) &&
                 path.EndsWith("._from", StringComparison.Ordinal)))
            {
                return true;
            }

            const string ChangesPrefix = "_actionChanges.Array.data[";
            if (!path.StartsWith(ChangesPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            int end = path.IndexOf(']', ChangesPrefix.Length);
            string entryPath = path.Substring(0, end + 1);
            SerializedProperty kind = property.serializedObject.FindProperty(entryPath + "._kind");
            // Remove names the Base's action; Replace too (its _action), while its _with runs.
            return kind != null &&
                   (kind.intValue == (int)ActionChange.ChangeKind.Remove ||
                    (kind.intValue == (int)ActionChange.ChangeKind.Replace &&
                     path.EndsWith("._action", StringComparison.Ordinal)));
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
                         property.propertyPath.StartsWith(StateMachineFields.StateDisplayNames, StringComparison.Ordinal) ||
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
                path.StartsWith(StateMachineFields.StateDisplayNames, StringComparison.Ordinal) ||
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

        // ---------- Data ----------

        // Anything else the table's file holds: ids and keys that its actions and conditions share with
        // components in the scene (AnchorIDSO, CooldownSO, RandomPoolSO, flags, counters, ...).
        // The Data panel lists these.
        public static bool IsDataItem(Object item)
        {
            return item is ScriptableObject && IsDataType(item.GetType());
        }

        public static bool IsDataType(Type type)
        {
            return typeof(ScriptableObject).IsAssignableFrom(type) &&
                   !typeof(StateSO).IsAssignableFrom(type) &&
                   !typeof(StateActionSO).IsAssignableFrom(type) &&
                   !typeof(StateConditionSO).IsAssignableFrom(type) &&
                   !typeof(TransitionTableSO).IsAssignableFrom(type) &&
                   !typeof(ExposedParametersSO).IsAssignableFrom(type);
        }

        public static List<ScriptableObject> CollectData(TransitionTableSO table)
        {
            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(table))
                .OfType<ScriptableObject>()
                .Where(IsDataItem)
                .ToList();
        }

        // A data item that can move into the table's file (EmbeddedAssets.MoveInto): alone in its own
        // .asset file. One inside another file (e.g. another table's Data) stays where it is.
        public static bool CanMoveIn(ScriptableObject item, string tablePath)
        {
            if (!IsDataItem(item))
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(item);
            return path.EndsWith(".asset", StringComparison.Ordinal) &&
                   path != tablePath &&
                   AssetDatabase.IsMainAsset(item) &&
                   AssetDatabase.LoadAllAssetsAtPath(path).Length == 1;
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
                // The one this table runs (a sub variant's own table when it has one).
                TransitionTableSO subTable = StateNodeView.FindSubTables(state, table).FirstOrDefault();
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

                // A sub variant (Make Sub Variant): the variant's field override points the Base's
                // Sub State Machine action at it, and the state running that action is the Base's.
                StateSO baseState = FindStateRunningSubVariant(table, childTable);
                if (baseState != null)
                {
                    return (table, baseState);
                }
            }

            return (null, null);
        }

        private static StateSO FindStateRunningSubVariant(TransitionTableSO table, TransitionTableSO childTable)
        {
            bool isOverriddenToChild = table.EffectiveFieldOverrides.Any(fieldOverride =>
                fieldOverride.FieldPath == StateMachineFields.TransitionTable &&
                fieldOverride.ObjectValue == childTable);
            if (!isOverriddenToChild)
            {
                return null;
            }

            var visited = new HashSet<TransitionTableSO>();
            for (TransitionTableSO current = table; current != null && visited.Add(current);
                current = StateMachineGraphView.SafeBaseOf(current))
            {
                foreach (StateSO state in CollectUsed(current).OfType<StateSO>())
                {
                    if (StateNodeView.FindSubTables(state, table).Contains(childTable))
                    {
                        return state;
                    }
                }
            }

            return null;
        }
    }
}

#endif
