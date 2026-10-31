#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Two ways to build sub-state machines from what's already on the graph, all inside this table's file.
    //
    // Move to New Sub State Machine (right-click a state, or a selection of states):
    //   arrows between them       -> move inside with them
    //   arrows coming in          -> point at the new sub-state (it starts at its own initial state)
    //   arrows going out to X     -> inside: to an exit state "ToX"; here: sub-state -> X once that exit is taken
    //   Any State arrows to them  -> asked: move inside, or keep here pointing at the sub-state
    // The states, actions and conditions themselves don't change, so exposed parameters keep their bindings.
    //
    // Duplicate as Variant (right-click a sub-state): a second sub-state next to it, running a Variant of
    // its child table (see EmbeddedAssets.CreateSubStateMachineVariant), e.g. a boss's phase 2.
    public partial class StateMachineGraphView
    {
        // ---------- Move to New Sub State Machine ----------

        private void AppendMoveToSubMenu(DropdownMenu menu, StateNodeView stateNode)
        {
            List<StateSO> states = StatesToMove(stateNode);

            // A variant's arrows are partly the Base's, which can't move out of the Base.
            bool canMove = states.Count > 0 && SafeBaseOf(_table) == null;

            menu.AppendAction(
                "Move to New Sub State Machine",
                _ => MoveToNewSubStateMachine(states),
                canMove ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        // The right-clicked state, or the whole selection when it's part of it (like Delete).
        private List<StateSO> StatesToMove(StateNodeView clicked)
        {
            List<StateNodeView> nodes = selection.Contains(clicked)
                ? selection.OfType<StateNodeView>().ToList()
                : new List<StateNodeView> { clicked };

            return nodes.Any(node => node.IsInherited)
                ? new List<StateSO>()
                : nodes.Select(node => node.State).Distinct().ToList();
        }

        private void MoveToNewSubStateMachine(List<StateSO> states)
        {
            var moved = new HashSet<StateSO>(states);
            Dictionary<StateSO, Vector2> positions = states.ToDictionary(
                state => state,
                state => FindNode(state)?.GetPosition().position ?? Vector2.zero);

            _serializedTable.Update();
            SerializedProperty transitions = _serializedTable.FindProperty(StateMachineFields.Transitions);
            SerializedProperty anyTransitions = _serializedTable.FindProperty(StateMachineFields.AnyTransitions);
            StateSO initialState = GetInitialState();

            var outsideTargets = new List<StateSO>();
            var incomingTargets = new List<StateSO>();
            for (int i = 0; i < transitions.arraySize; i++)
            {
                (StateSO from, StateSO to) = FromAndTo(transitions.GetArrayElementAtIndex(i));
                if (moved.Contains(from) && !moved.Contains(to) && to != null && !outsideTargets.Contains(to))
                {
                    outsideTargets.Add(to);
                }
                else if (!moved.Contains(from) && moved.Contains(to))
                {
                    incomingTargets.Add(to);
                }
            }

            int anyIn = 0;
            for (int i = 0; i < anyTransitions.arraySize; i++)
            {
                if (moved.Contains(ToStateOf(anyTransitions.GetArrayElementAtIndex(i))))
                {
                    anyIn++;
                }
            }

            if (!ConfirmMoveToSub(states.Count, outsideTargets.Count, anyIn, out bool moveAnyInside))
            {
                return;
            }

            if (!moveAnyInside)
            {
                for (int i = 0; i < anyTransitions.arraySize; i++)
                {
                    StateSO to = ToStateOf(anyTransitions.GetArrayElementAtIndex(i));
                    if (moved.Contains(to))
                    {
                        incomingTargets.Add(to);
                    }
                }
            }

            // Where the sub-state machine starts: this table's own start if it moves, else where most
            // arrows came in, else the top-left state.
            StateSO childInitial = moved.Contains(initialState)
                ? initialState
                : incomingTargets.GroupBy(state => state).OrderByDescending(group => group.Count()).FirstOrDefault()?.Key ??
                  states.OrderBy(state => positions[state].y).ThenBy(state => positions[state].x).First();

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            StateSO subState = EmbeddedAssets.CreateSubStateMachine(_table, false, out TransitionTableSO childTable);
            string prefix = EmbeddedAssets.StatePrefix(subState.name);

            // One exit per state outside that the moved ones went to, shared by every arrow going there.
            var exitStates = new Dictionary<StateSO, StateSO>();
            var exitConditions = new Dictionary<StateSO, StateConditionSO>();
            var usedExitIds = new HashSet<string>();
            foreach (StateSO target in outsideTargets)
            {
                string exitId = "To" + EmbeddedAssets.StatePrefix(target.name).Replace(" ", "");
                for (int number = 1; !usedExitIds.Add(exitId); number++)
                {
                    exitId = $"To{EmbeddedAssets.StatePrefix(target.name).Replace(" ", "")}{number}";
                }

                exitStates[target] = EmbeddedAssets.CreateExit(null, childTable, prefix, exitId);
                exitConditions[target] = (StateConditionSO)EmbeddedAssets.CreateExitCondition(_table, prefix, exitId);
            }

            // The pieces above were written through their own SerializedObjects; read them fresh.
            _serializedTable.Update();
            transitions = _serializedTable.FindProperty(StateMachineFields.Transitions);
            anyTransitions = _serializedTable.FindProperty(StateMachineFields.AnyTransitions);
            var serializedChild = new SerializedObject(childTable);
            SerializedProperty childTransitions = serializedChild.FindProperty(StateMachineFields.Transitions);
            SerializedProperty childAnyTransitions = serializedChild.FindProperty(StateMachineFields.AnyTransitions);

            serializedChild.FindProperty(StateMachineFields.InitialState).objectReferenceValue = childInitial;

            var transitionsToRemove = new HashSet<int>();
            for (int i = 0; i < transitions.arraySize; i++)
            {
                SerializedProperty item = transitions.GetArrayElementAtIndex(i);
                (StateSO from, StateSO to) = FromAndTo(item);

                if (moved.Contains(from))
                {
                    // Inside, as it was, or to the exit state for where it went.
                    SerializedProperty copy = AppendCopy(childTransitions, item);
                    if (!moved.Contains(to) && to != null)
                    {
                        copy.FindPropertyRelative("ToState").objectReferenceValue = exitStates[to];
                    }

                    transitionsToRemove.Add(i);
                }
                else if (moved.Contains(to))
                {
                    item.FindPropertyRelative("ToState").objectReferenceValue = subState;
                }
            }

            var anyToRemove = new HashSet<int>();
            for (int i = 0; i < anyTransitions.arraySize; i++)
            {
                SerializedProperty item = anyTransitions.GetArrayElementAtIndex(i);
                if (!moved.Contains(ToStateOf(item)))
                {
                    continue;
                }

                if (moveAnyInside)
                {
                    AppendCopy(childAnyTransitions, item);
                    anyToRemove.Add(i);
                }
                else
                {
                    item.FindPropertyRelative("ToState").objectReferenceValue = subState;
                }
            }

            DeleteArrayIndices(transitions, transitionsToRemove);
            DeleteArrayIndices(anyTransitions, anyToRemove);

            // Here: sub-state -> each outside state, when its exit was taken.
            foreach (StateSO target in outsideTargets)
            {
                AppendExitTransition(transitions, subState, target, exitConditions[target]);
            }

            MoveNodePositions(serializedChild, moved, positions, exitStates.Values.ToList(), subState);

            if (moved.Contains(initialState))
            {
                _serializedTable.FindProperty(StateMachineFields.InitialState).objectReferenceValue = subState;
            }

            serializedChild.ApplyModifiedProperties();
            _serializedTable.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssetIfDirty(_table);

            foreach (StateSO target in incomingTargets.Distinct().Where(state => state != childInitial))
            {
                Debug.LogWarning(
                    $"An arrow that went to {target.name} now goes to {subState.name}, which starts at " +
                    $"{childInitial.name}. Set the sub-state machine's initial state, or add an arrow inside, if it should start there.",
                    childTable);
            }

            StateMachineGraphWindow.ReloadAll();
            RenameNewState(subState);
        }

        // In the confirm dialog. moveAnyInside: what to do with Any State arrows to the moved states.
        private bool ConfirmMoveToSub(int stateCount, int exitCount, int anyIn, out bool moveAnyInside)
        {
            moveAnyInside = false;

            string message =
                $"Move {stateCount} state(s) into a new sub-state machine in this file?\n\n" +
                "Arrows between them move inside. Arrows coming in point at the new sub-state, which " +
                "starts at its own initial state.";

            if (exitCount > 0)
            {
                message += $"\n\n{exitCount} state(s) outside are reached from them: each gets an exit state inside " +
                           "and an arrow out of the sub-state here.";
            }

            List<string> variants = VariantsOf(_table);
            if (variants.Count > 0)
            {
                message += $"\n\nVariants of {_table.name} ({string.Join(", ", variants)}) change its arrows by id. " +
                           "Their changes to the moved arrows stop applying.";
            }

            if (anyIn == 0)
            {
                return EditorUtility.DisplayDialog("Move to New Sub State Machine", message, "Move", "Cancel");
            }

            message += $"\n\n{anyIn} Any State arrow(s) go to these states. Move them inside (they then only " +
                       "fire while the sub-state runs), or keep them here, pointing at the sub-state?";

            int choice = EditorUtility.DisplayDialogComplex(
                "Move to New Sub State Machine", message, "Move Inside", "Cancel", "Keep Here");

            moveAnyInside = choice == 0;
            return choice != 1;
        }

        // Moved states keep where they were, so the child graph looks like the part that was selected.
        // Entry / Any State go on its left, the exit states on its right, the sub-state here in the middle.
        private void MoveNodePositions(
            SerializedObject serializedChild,
            HashSet<StateSO> moved,
            Dictionary<StateSO, Vector2> positions,
            List<StateSO> exitStates,
            StateSO subState)
        {
            SerializedProperty nodePositions = _serializedTable.FindProperty(StateMachineFields.NodePositions);
            SerializedProperty childPositions = serializedChild.FindProperty(StateMachineFields.NodePositions);

            for (int i = nodePositions.arraySize - 1; i >= 0; i--)
            {
                if (moved.Contains(nodePositions.GetArrayElementAtIndex(i).FindPropertyRelative("State").objectReferenceValue as StateSO))
                {
                    nodePositions.DeleteArrayElementAtIndex(i);
                }
            }

            foreach (StateSO state in moved)
            {
                FindOrAddNodePosition(childPositions, state).FindPropertyRelative("Position").vector2Value = positions[state];
            }

            float left = positions.Values.Min(position => position.x);
            float right = positions.Values.Max(position => position.x);
            float top = positions.Values.Min(position => position.y);

            for (int i = 0; i < exitStates.Count; i++)
            {
                FindOrAddNodePosition(childPositions, exitStates[i]).FindPropertyRelative("Position").vector2Value =
                    new Vector2(right + 250f, top + 80f * i);
            }

            serializedChild.FindProperty("_entryNodePosition").vector2Value = new Vector2(left - 250f, top);
            serializedChild.FindProperty("_anyStateNodePosition").vector2Value = new Vector2(left - 250f, top + 120f);

            Vector2 center = new Vector2(positions.Values.Average(position => position.x), positions.Values.Average(position => position.y));
            FindOrAddNodePosition(nodePositions, subState).FindPropertyRelative("Position").vector2Value = center;
        }

        private static (StateSO From, StateSO To) FromAndTo(SerializedProperty transition)
        {
            return (transition.FindPropertyRelative("FromState").objectReferenceValue as StateSO, ToStateOf(transition));
        }

        private static StateSO ToStateOf(SerializedProperty transition)
        {
            return transition.FindPropertyRelative("ToState").objectReferenceValue as StateSO;
        }

        // The whole item (states, condition groups, id) at the end of another table's array.
        private static SerializedProperty AppendCopy(SerializedProperty array, SerializedProperty item)
        {
            array.arraySize++;
            SerializedProperty copy = array.GetArrayElementAtIndex(array.arraySize - 1);
            copy.boxedValue = item.boxedValue;
            return copy;
        }

        private static void AppendExitTransition(SerializedProperty transitions, StateSO from, StateSO to, StateConditionSO exited)
        {
            // Growing the array copies the last item, so every field is set.
            transitions.arraySize++;
            SerializedProperty item = transitions.GetArrayElementAtIndex(transitions.arraySize - 1);
            item.FindPropertyRelative("FromState").objectReferenceValue = from;
            item.FindPropertyRelative("ToState").objectReferenceValue = to;
            item.FindPropertyRelative("Id").stringValue = Guid.NewGuid().ToString();

            SerializedProperty groups = item.FindPropertyRelative("ConditionGroups");
            groups.arraySize = 1;
            SerializedProperty conditions = groups.GetArrayElementAtIndex(0).FindPropertyRelative("Conditions");
            conditions.arraySize = 1;
            SerializedProperty usage = conditions.GetArrayElementAtIndex(0);
            usage.FindPropertyRelative("Condition").objectReferenceValue = exited;
            usage.FindPropertyRelative("ExpectedResult").enumValueIndex = (int)TransitionTableSO.Result.True;
        }

        // Names of the tables (anywhere in the project, embedded ones too) whose Base is `table`.
        private static List<string> VariantsOf(TransitionTableSO table)
        {
            return AssetDatabase.FindAssets("t:TransitionTableSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<TransitionTableSO>())
                .Where(other => other != table && BaseOf(other) == table)
                .Select(other => other.name)
                .ToList();
        }

        // ---------- Duplicate as Variant ----------

        private void AppendDuplicateAsVariantMenu(DropdownMenu menu, StateNodeView stateNode)
        {
            List<(SubStateMachineActionSO Action, TransitionTableSO SubTable)> subActions =
                StateNodeView.FindSubActions(stateNode.State, _table);

            foreach ((SubStateMachineActionSO action, TransitionTableSO subTable) in subActions)
            {
                string label = subActions.Count == 1
                    ? "Duplicate as Variant"
                    : $"Duplicate as Variant/{subTable.name}";

                menu.AppendAction(label, _ => DuplicateAsVariant(stateNode, action, subTable));
            }
        }

        // The new sub-state goes under the original, with no arrows yet.
        private void DuplicateAsVariant(StateNodeView stateNode, SubStateMachineActionSO action, TransitionTableSO subTable)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            StateSO state = EmbeddedAssets.CreateSubStateMachineVariant(
                _table, action, subTable, EmbeddedAssets.StatePrefix(stateNode.State.name));
            AddStateToTable(state, stateNode.GetPosition().position + new Vector2(0f, 120f));

            Undo.CollapseUndoOperations(undoGroup);

            StateMachineGraphWindow.ReloadAll();
            RenameNewState(state);
        }

        // Selects a state just made and starts renaming it, so it gets a real name right away.
        private void RenameNewState(StateSO state)
        {
            if (FindNode(state) is StateNodeView node)
            {
                ClearSelection();
                AddToSelection(node);
                BeginRename(node);
            }
        }
    }
}

#endif
