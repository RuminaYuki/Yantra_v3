#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // What a variant table changes from its Base, edited from its graph (no table Inspector needed):
    // right-click a Base's arrow to remove / restore it (_removedIds); click a Base's state and the
    // Inspector shows its overrides here (StateOverrideSelection, like an arrow's TransitionSelection).
    public partial class StateMachineGraphView
    {
        // States the Base uses. Set by Load.
        private HashSet<StateSO> _baseStates = new HashSet<StateSO>();

        // ---------- A Base's state's actions ----------

        // This graph is a variant table and the state comes from its Base: changes go to this table,
        // never to the shared state.
        public bool IsBaseStateHere(StateSO state)
        {
            return _table != null && SafeBaseOf(_table) != null && _baseStates.Contains(state);
        }

        // "✎ 2 changes" on each state whose actions this table changes.
        public void RefreshActionChangeLabels()
        {
            var counts = new Dictionary<StateSO, int>();

            if (_serializedTable != null)
            {
                _serializedTable.Update();
                // Each added / removed action counts one, a changed order counts one.
                foreach (string listName in new[] { "_actionChanges", "_actionOrders" })
                {
                    SerializedProperty list = _serializedTable.FindProperty(listName);
                    for (int i = 0; i < list.arraySize; i++)
                    {
                        if (list.GetArrayElementAtIndex(i).FindPropertyRelative("_state").objectReferenceValue is StateSO state)
                        {
                            counts[state] = counts.TryGetValue(state, out int count) ? count + 1 : 1;
                        }
                    }
                }
            }

            foreach (GraphElement element in graphElements.ToList())
            {
                if (element is StateNodeView node)
                {
                    node.SetActionChangeCount(counts.TryGetValue(node.State, out int count) ? count : 0);
                }
            }
        }

        // ---------- A Base's state's name here ----------

        // This table's own names for its Base's states (_stateDisplayNames). Graph only.
        private static Dictionary<StateSO, string> ReadDisplayNames(SerializedObject serializedTable)
        {
            var names = new Dictionary<StateSO, string>();
            SerializedProperty list = serializedTable.FindProperty(StateMachineFields.StateDisplayNames);

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                string name = entry.FindPropertyRelative("Name").stringValue;
                if (entry.FindPropertyRelative("State").objectReferenceValue is StateSO state && !string.IsNullOrEmpty(name))
                {
                    names[state] = name;
                }
            }

            return names;
        }

        // Empty or the state's own name: show the state's name again.
        private void SetDisplayName(StateSO state, string name)
        {
            name = name?.Trim();

            _serializedTable.Update();
            SerializedProperty list = _serializedTable.FindProperty(StateMachineFields.StateDisplayNames);

            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("State").objectReferenceValue == state)
                {
                    list.DeleteArrayElementAtIndex(i);
                }
            }

            if (!string.IsNullOrEmpty(name) && name != state.name)
            {
                list.arraySize++;
                SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                entry.FindPropertyRelative("State").objectReferenceValue = state;
                entry.FindPropertyRelative("Name").stringValue = name;
            }

            // Recorded for Undo.
            _serializedTable.ApplyModifiedProperties();
            Reload();
        }

        // ---------- A Base's state: remove / restore ----------

        // A variant can't delete a Base's state, but it can stop using it (_removedStates): every Base
        // arrow into / out of it is then off here, at runtime too (TransitionTableSO.EffectiveTransitions).
        private void AppendRemoveStateMenu(DropdownMenu menu, StateNodeView stateNode, bool isInitial)
        {
            if (!IsBaseStateHere(stateNode.State))
            {
                return;
            }

            if (stateNode.IsRemovedHere)
            {
                menu.AppendAction("Restore State", _ => SetStateRemoved(stateNode.State, false));
                return;
            }

            // The machine would have nowhere to start.
            if (isInitial)
            {
                menu.AppendAction(
                    "Remove State from This Table (it's the initial state: set another one first)",
                    _ => { },
                    DropdownMenuAction.Status.Disabled);
                return;
            }

            menu.AppendAction("Remove State from This Table", _ => SetStateRemoved(stateNode.State, true));
        }

        public void SetStateRemoved(StateSO state, bool remove)
        {
            SetStateRemoved(_serializedTable, new[] { state }, remove);
        }

        // Delete key: a Base's state isn't Deletable, and GraphView's Delete command only runs when
        // something Deletable is selected, so it never sees one. Catch the key here and remove the
        // selected Base states from this table instead, like the menu. Not stopped: this table's own
        // selected states / arrows still go through the normal Delete command.
        private void OnKeyDownRemoveBaseStates(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Delete || _serializedTable == null)
            {
                return;
            }

            StateSO initial = GetInitialState();
            List<StateNodeView> baseNodes = selection.OfType<StateNodeView>()
                .Where(node => IsBaseStateHere(node.State) && !node.IsRemovedHere)
                .ToList();
            if (baseNodes.Count == 0)
            {
                return;
            }

            if (baseNodes.Any(node => node.State == initial))
            {
                Debug.LogWarning($"{initial.name} is the initial state, so it stays. Set another initial state first.", _table);
            }

            List<StateSO> toRemove = baseNodes
                .Select(node => node.State)
                .Where(state => state != initial)
                .ToList();
            if (toRemove.Count == 0)
            {
                return;
            }

            // Next frame: let the normal Delete command finish with this table's own elements first
            // (removing reloads the graph, which would pull the selection out from under it).
            schedule.Execute(() => SetStateRemoved(_serializedTable, toRemove, true));
        }

        internal static void SetStateRemoved(SerializedObject serializedTable, StateSO state, bool remove)
        {
            SetStateRemoved(serializedTable, new[] { state }, remove);
        }

        // Also used by the Inspector panel of a Base's state (VariantStatePanel). One Undo step.
        internal static void SetStateRemoved(SerializedObject serializedTable, IEnumerable<StateSO> states, bool remove)
        {
            if (serializedTable == null || serializedTable.targetObject == null)
            {
                return;
            }

            serializedTable.Update();
            SerializedProperty removedStates = serializedTable.FindProperty("_removedStates");
            if (removedStates == null)
            {
                return;
            }

            foreach (StateSO state in states)
            {
                if (state == null)
                {
                    continue;
                }

                int index = -1;
                for (int i = 0; i < removedStates.arraySize; i++)
                {
                    if (removedStates.GetArrayElementAtIndex(i).objectReferenceValue == state)
                    {
                        index = i;
                        break;
                    }
                }

                if (remove && index < 0)
                {
                    removedStates.arraySize++;
                    removedStates.GetArrayElementAtIndex(removedStates.arraySize - 1).objectReferenceValue = state;
                }
                else if (!remove && index >= 0)
                {
                    // Clear first: on older Unity a non-null reference element needs two deletes.
                    removedStates.GetArrayElementAtIndex(index).objectReferenceValue = null;
                    removedStates.DeleteArrayElementAtIndex(index);
                }
            }

            serializedTable.ApplyModifiedProperties();
            StateMachineGraphWindow.ReloadAll();
        }

        // ---------- A Base's transitions ----------

        // One transition: Remove / Restore. Several on one arrow: one submenu entry each, plus all at once.
        private void AppendInheritedEdgeMenu(DropdownMenu menu, TransitionEdgeView edge)
        {
            IReadOnlyList<InheritedTransition> items = edge.InheritedTransitions;

            if (items.Count == 0)
            {
                menu.AppendAction("Initial state from the Base (set one here to replace it)", _ => { }, DropdownMenuAction.Status.Disabled);
                return;
            }

            // One arrow = one From -> To pair, so its state being removed applies to all of them.
            if (items.Any(item => item.RemovedByState))
            {
                menu.AppendAction("Off with its removed state (Restore State to bring it back)", _ => { }, DropdownMenuAction.Status.Disabled);
                return;
            }

            // A Base never opened since IDs were added has none yet, so nothing can point at them.
            if (items.Any(item => string.IsNullOrEmpty(item.Id)))
            {
                menu.AppendAction("No ID yet: select the Base table once", _ => { }, DropdownMenuAction.Status.Disabled);
                return;
            }

            if (items.Count == 1)
            {
                InheritedTransition item = items[0];
                menu.AppendAction(
                    item.Removed ? "Restore" : "Remove from This Table",
                    _ => SetRemoved(new[] { item.Id }, !item.Removed));
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                InheritedTransition item = items[i];
                string group = item.Removed ? "Restore" : "Remove from This Table";
                menu.AppendAction($"{group}/{i + 1}. {item.Summary}", _ => SetRemoved(new[] { item.Id }, !item.Removed));
            }

            menu.AppendSeparator();
            menu.AppendAction(
                "Remove All from This Table",
                _ => SetRemoved(items.Select(item => item.Id), true),
                items.Any(item => !item.Removed) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction(
                "Restore All",
                _ => SetRemoved(items.Select(item => item.Id), false),
                items.Any(item => item.Removed) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        private void SetRemoved(IEnumerable<string> ids, bool remove)
        {
            _serializedTable.Update();
            SerializedProperty removedIds = _serializedTable.FindProperty("_removedIds");

            foreach (string id in ids)
            {
                int index = IndexOfString(removedIds, id);
                if (remove && index < 0)
                {
                    removedIds.arraySize++;
                    removedIds.GetArrayElementAtIndex(removedIds.arraySize - 1).stringValue = id;
                }
                else if (!remove && index >= 0)
                {
                    removedIds.DeleteArrayElementAtIndex(index);
                }
            }

            // Recorded for Undo. The table's Inspector shows the same list.
            _serializedTable.ApplyModifiedProperties();
            StateMachineGraphWindow.ReloadAll();
        }

        private static int IndexOfString(SerializedProperty list, string value)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).stringValue == value)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

#endif
