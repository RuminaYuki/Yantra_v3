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
    // Editing the table from the graph: right-click menus, Make Transition, Add State, Delete.
    // Every change goes through _serializedTable, so it marks the asset dirty and supports Undo.
    public partial class StateMachineGraphView
    {
        // Set while "Make Transition" is waiting for the target click.
        private Node _transitionSource;
        private TransitionPreviewView _transitionPreview;

        private StateSearchProvider _stateSearchProvider;

        private void RegisterTransitionCreationCallbacks()
        {
            RegisterCallback<MouseMoveEvent>(OnMouseMoveWhileConnecting);

            // TrickleDown: runs before the clicked node/edge and the GraphView manipulators see the click.
            RegisterCallback<MouseDownEvent>(OnMouseDownWhileConnecting, TrickleDown.TrickleDown);
            RegisterCallback<MouseUpEvent>(OnMouseUpWhileConnecting, TrickleDown.TrickleDown);
            RegisterCallback<KeyDownEvent>(OnKeyDownWhileConnecting);
            RegisterCallback<KeyDownEvent>(OnKeyDownRename);

            RegisterCallback<DetachFromPanelEvent>(_ => DestroyStateSearchProvider());
        }

        // ---------- Dropping states onto the graph ----------

        // A StateSO dragged from the Contents panel (or the Project window) and dropped on the graph is
        // placed where it was dropped: added to the table if it isn't on the graph yet, moved if it is.
        private void RegisterStateDropCallbacks()
        {
            RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (_table == null || !DraggedStates().Any())
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                evt.StopPropagation();
            });

            RegisterCallback<DragPerformEvent>(evt =>
            {
                List<StateSO> states = DraggedStates().ToList();
                if (_table == null || states.Count == 0)
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                evt.StopPropagation();

                // Several at once are stepped down so they don't land on top of each other.
                Vector2 position = contentViewContainer.WorldToLocal(evt.mousePosition);
                foreach (StateSO state in states)
                {
                    AddStateToTable(state, position);
                    position += new Vector2(30f, 40f);
                }
            });
        }

        private static IEnumerable<StateSO> DraggedStates()
        {
            return DragAndDrop.objectReferences.OfType<StateSO>();
        }

        // ---------- Dropping Data onto the graph ----------

        // Any other .asset (an AnchorIDSO, a CooldownSO, ...) dragged from the Project window onto the
        // graph moves into the table's file; the window hands it to the Data panel, which asks first.
        public event System.Action<List<ScriptableObject>> DataDropped;

        private void RegisterDataDropCallbacks()
        {
            RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (DraggedData().Count == 0)
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                evt.StopPropagation();
            });

            RegisterCallback<DragPerformEvent>(evt =>
            {
                List<ScriptableObject> items = DraggedData();
                if (items.Count == 0)
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                evt.StopPropagation();

                // A dialog isn't safe while the drop is still being sent around.
                schedule.Execute(() => DataDropped?.Invoke(items));
            });
        }

        private List<ScriptableObject> DraggedData()
        {
            if (_table == null)
            {
                return new List<ScriptableObject>();
            }

            string tablePath = AssetDatabase.GetAssetPath(_table);
            return DragAndDrop.objectReferences
                .OfType<ScriptableObject>()
                .Where(item => TableContents.CanMoveIn(item, tablePath))
                .Distinct()
                .ToList();
        }

        // ---------- Dropping actions on a state ----------

        // Actions dragged from Contents or the Project window onto a state node go at the end of its
        // action list (ones it already has are skipped). The node under the mouse is outlined while
        // dragging. Without Undo, like the panel drops: Unity can revert the current Undo group right
        // after a drag ends. Remove one again with − in the state's Inspector.
        private StateNodeView _actionDropTarget;

        private void RegisterActionDropCallbacks()
        {
            RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (_table == null || !DraggedActions().Any())
                {
                    return;
                }

                StateNodeView node = (evt.target as VisualElement)?.GetFirstOfType<StateNodeView>();
                SetActionDropTarget(node);
                DragAndDrop.visualMode = node != null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                evt.StopPropagation();
            });

            RegisterCallback<DragLeaveEvent>(_ => SetActionDropTarget(null));
            RegisterCallback<DragExitedEvent>(_ => SetActionDropTarget(null));

            RegisterCallback<DragPerformEvent>(evt =>
            {
                List<StateActionSO> actions = DraggedActions().ToList();
                StateNodeView node = (evt.target as VisualElement)?.GetFirstOfType<StateNodeView>();
                SetActionDropTarget(null);

                if (_table == null || actions.Count == 0 || node?.State == null)
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                evt.StopPropagation();

                // Editing while the drop event is still being sent around is unsafe, so wait a frame.
                StateSO state = node.State;

                // A Base's state in a variant table: added for this table only, never to the Base's state.
                if (IsBaseStateHere(state))
                {
                    TransitionTableSO table = _table;
                    schedule.Execute(() =>
                    {
                        VariantStatePanel.AddInTable(table, state, actions);
                        RefreshActionChangeLabels();

                        // The Inspector may be showing this state's overrides: show the new ones.
                        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    });
                    return;
                }

                schedule.Execute(() => AddActions(state, actions));
            });
        }

        private static IEnumerable<StateActionSO> DraggedActions()
        {
            return DragAndDrop.objectReferences.OfType<StateActionSO>().Distinct();
        }

        private void SetActionDropTarget(StateNodeView node)
        {
            if (_actionDropTarget == node)
            {
                return;
            }

            _actionDropTarget?.SetDropTarget(false);
            _actionDropTarget = node;
            _actionDropTarget?.SetDropTarget(true);
        }

        private static void AddActions(StateSO state, List<StateActionSO> actions)
        {
            var serializedState = new SerializedObject(state);
            SerializedProperty list = serializedState.FindProperty(StateMachineFields.Actions);

            var existing = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                existing.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
            }

            int added = 0;
            foreach (StateActionSO action in actions)
            {
                if (!existing.Add(action))
                {
                    continue;
                }

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = action;
                added++;
            }

            if (added == 0)
            {
                return;
            }

            serializedState.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(state);
            AssetDatabase.SaveAssetIfDirty(state);

            // A Sub State Machine action turns the node into a sub-state machine, so redraw everything.
            StateMachineGraphWindow.ReloadAll();
        }

        // ---------- Dropping conditions on an arrow ----------

        // Conditions dragged from Contents or the Project window onto an arrow are ANDed onto it: added to
        // every OR group (or a new group when it has none), expecting True. Ones a group already has are
        // skipped. Only this table's own arrows with a single transition: an inherited arrow is overridden
        // from its Inspector, and on a several-transitions arrow it isn't clear which one is meant.
        // Without Undo, like the action drops.
        private TransitionEdgeView _conditionDropTarget;

        private void RegisterConditionDropCallbacks()
        {
            RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (_table == null || !DraggedConditions().Any())
                {
                    return;
                }

                TransitionEdgeView edge = ConditionDropEdgeAt(evt.target);
                SetConditionDropTarget(edge);
                DragAndDrop.visualMode = edge != null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                evt.StopPropagation();
            });

            RegisterCallback<DragLeaveEvent>(_ => SetConditionDropTarget(null));
            RegisterCallback<DragExitedEvent>(_ => SetConditionDropTarget(null));

            RegisterCallback<DragPerformEvent>(evt =>
            {
                List<StateConditionSO> conditions = DraggedConditions().ToList();
                TransitionEdgeView edge = ConditionDropEdgeAt(evt.target);
                SetConditionDropTarget(null);

                if (_table == null || conditions.Count == 0 || edge == null)
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                evt.StopPropagation();

                // Editing while the drop event is still being sent around is unsafe, so wait a frame.
                object fromKey = GetNodeKey(edge.From);
                object toKey = GetNodeKey(edge.To);
                string arrayName = edge.IsAnyState ? StateMachineFields.AnyTransitions : StateMachineFields.Transitions;
                int index = edge.TransitionIndices[0];

                schedule.Execute(() => AddConditions(arrayName, index, conditions, fromKey, toKey));
            });
        }

        private static IEnumerable<StateConditionSO> DraggedConditions()
        {
            return DragAndDrop.objectReferences.OfType<StateConditionSO>().Distinct();
        }

        private static TransitionEdgeView ConditionDropEdgeAt(IEventHandler target)
        {
            TransitionEdgeView edge = (target as VisualElement)?.GetFirstOfType<TransitionEdgeView>();
            return edge != null && !edge.IsInherited && edge.TransitionIndices.Count == 1 ? edge : null;
        }

        private void SetConditionDropTarget(TransitionEdgeView edge)
        {
            if (_conditionDropTarget == edge)
            {
                return;
            }

            _conditionDropTarget?.SetDropTarget(false);
            _conditionDropTarget = edge;
            _conditionDropTarget?.SetDropTarget(true);
        }

        private void AddConditions(
            string arrayName,
            int transitionIndex,
            List<StateConditionSO> conditions,
            object fromKey,
            object toKey)
        {
            _serializedTable.Update();
            SerializedProperty array = _serializedTable.FindProperty(arrayName);
            if (transitionIndex >= array.arraySize)
            {
                return;
            }

            SerializedProperty groups = array.GetArrayElementAtIndex(transitionIndex).FindPropertyRelative("ConditionGroups");
            if (groups.arraySize == 0)
            {
                groups.InsertArrayElementAtIndex(0);
                groups.GetArrayElementAtIndex(0).FindPropertyRelative("Conditions").arraySize = 0;
            }

            int added = 0;
            for (int g = 0; g < groups.arraySize; g++)
            {
                SerializedProperty list = groups.GetArrayElementAtIndex(g).FindPropertyRelative("Conditions");

                var existing = new HashSet<Object>();
                for (int i = 0; i < list.arraySize; i++)
                {
                    existing.Add(list.GetArrayElementAtIndex(i).FindPropertyRelative("Condition").objectReferenceValue);
                }

                foreach (StateConditionSO condition in conditions)
                {
                    if (!existing.Add(condition))
                    {
                        continue;
                    }

                    list.InsertArrayElementAtIndex(list.arraySize);
                    SerializedProperty usage = list.GetArrayElementAtIndex(list.arraySize - 1);
                    usage.FindPropertyRelative("Condition").objectReferenceValue = condition;
                    usage.FindPropertyRelative("ExpectedResult").enumValueIndex = (int)TransitionTableSO.Result.True;
                    added++;
                }
            }

            if (added == 0)
            {
                return;
            }

            _serializedTable.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_table);
            AssetDatabase.SaveAssetIfDirty(_table);

            // Redraw (parameter tags, ⚙) and keep the arrow selected so its Inspector shows the new conditions.
            Load(_table);
            SelectEdge(fromKey, toKey);
        }

        // ---------- Right-click menu ----------

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // No base call: GraphView's default Cut / Copy / Paste / Duplicate aren't supported here.
            if (_serializedTable == null)
            {
                return;
            }

            // The click may land on a child (e.g. the title label), so look up to the node / edge.
            var target = evt.target as VisualElement;
            Node node = target?.GetFirstOfType<Node>();
            TransitionEdgeView edge = target?.GetFirstOfType<TransitionEdgeView>();
            Vector2 mousePosition = evt.mousePosition;

            if (node is StateNodeView stateNode)
            {
                bool isInitial = stateNode.State == GetInitialState();

                if (stateNode.IsSubStateMachine)
                {
                    // One item normally, a submenu when the state runs several sub-state machines.
                    foreach (TransitionTableSO subTable in stateNode.SubTables)
                    {
                        string label = stateNode.SubTables.Count == 1
                            ? "Open Sub-State Machine"
                            : $"Open Sub-State Machine/{subTable.name}";

                        evt.menu.AppendAction(label, _ => OpenSubStateMachine(stateNode.State, subTable));
                    }

                    AppendMakeSubVariantMenu(evt.menu, stateNode);
                    evt.menu.AppendSeparator();
                }

                evt.menu.AppendAction("Make Transition", _ => StartTransitionCreation(stateNode, mousePosition));
                evt.menu.AppendAction(
                    "Set as Initial State",
                    _ => SetInitialState(stateNode.State),
                    isInitial ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Rename", _ => BeginRename(stateNode));

                // A Base's state isn't in this table to delete (its transitions are removed in the Inspector).
                if (!stateNode.IsInherited)
                {
                    // A state inside this table's file is deleted with it; a shared state file stays.
                    bool isEmbedded = AssetDatabase.GetAssetPath(stateNode.State) == AssetDatabase.GetAssetPath(_table);
                    evt.menu.AppendAction(isEmbedded ? "Delete" : "Remove from Table", _ => DeleteFromMenu(stateNode));
                }
            }
            else if (node is SpecialNodeView { NodeKind: SpecialNodeView.Kind.AnyState } anyStateNode)
            {
                evt.menu.AppendAction("Make Transition", _ => StartTransitionCreation(anyStateNode, mousePosition));
            }
            else if (node is SpecialNodeView)
            {
                // Entry: nothing to do here, the initial state is set from a state node.
            }
            else if (edge != null && edge.IsInherited)
            {
                AppendChangeTargetMenu(evt.menu, edge, mousePosition);
                AppendInheritedEdgeMenu(evt.menu, edge);
            }
            else if (edge != null)
            {
                AppendChangeTargetMenu(evt.menu, edge, mousePosition);

                if (IsDeletable(edge))
                {
                    evt.menu.AppendAction("Delete", _ => DeleteFromMenu(edge));
                }
            }
            else
            {
                evt.menu.AppendAction("Add State...", _ => OpenAddStateSearch(mousePosition));
            }
        }

        // Deletes the whole selection if the right-clicked element is part of it, like Animator.
        private void DeleteFromMenu(GraphElement element)
        {
            var toDelete = new List<GraphElement>();

            if (selection.Contains(element))
            {
                foreach (ISelectable selectable in selection)
                {
                    if (selectable is GraphElement selected && IsDeletable(selected))
                    {
                        toDelete.Add(selected);
                    }
                }
            }
            else
            {
                toDelete.Add(element);
            }

            // Goes through graphViewChanged -> RemoveFromTable, same as the Delete key.
            DeleteElements(toDelete);
        }

        private static bool IsDeletable(GraphElement element)
        {
            return (element.capabilities & Capabilities.Deletable) != 0;
        }

        // ---------- Delete ----------

        // Deleted states that live inside the table's file also leave the file (with what only they
        // used), like Animator; shared state files are only taken out of this table. One Undo step.
        private void RemoveFromTable(List<GraphElement> elements)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            _serializedTable.Update();

            SerializedProperty transitions = _serializedTable.FindProperty("_transitions");
            SerializedProperty anyTransitions = _serializedTable.FindProperty("_anyTransitions");
            SerializedProperty nodePositions = _serializedTable.FindProperty("_nodePositions");
            SerializedProperty initialState = _serializedTable.FindProperty("_initialState");

            var localToRemove = new HashSet<int>();
            var anyToRemove = new HashSet<int>();
            var removedStates = new HashSet<StateSO>();

            foreach (GraphElement element in elements)
            {
                // Inherited ones aren't deletable; skipped here too, since their data isn't in this table.
                if (element is StateNodeView stateNode && !stateNode.IsInherited)
                {
                    removedStates.Add(stateNode.State);
                }
                else if (element is TransitionEdgeView edge && !edge.IsInherited)
                {
                    // Entry edge has no indices, so it's never removed from the data.
                    foreach (int index in edge.TransitionIndices)
                    {
                        (edge.IsAnyState ? anyToRemove : localToRemove).Add(index);
                    }
                }
            }

            if (removedStates.Count > 0)
            {
                // A removed state takes every transition that touches it along.
                for (int i = 0; i < transitions.arraySize; i++)
                {
                    SerializedProperty item = transitions.GetArrayElementAtIndex(i);
                    if (removedStates.Contains(item.FindPropertyRelative("FromState").objectReferenceValue as StateSO) ||
                        removedStates.Contains(item.FindPropertyRelative("ToState").objectReferenceValue as StateSO))
                    {
                        localToRemove.Add(i);
                    }
                }

                for (int i = 0; i < anyTransitions.arraySize; i++)
                {
                    SerializedProperty item = anyTransitions.GetArrayElementAtIndex(i);
                    if (removedStates.Contains(item.FindPropertyRelative("ToState").objectReferenceValue as StateSO))
                    {
                        anyToRemove.Add(i);
                    }
                }

                // Otherwise CollectStates would bring the node back.
                for (int i = nodePositions.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty item = nodePositions.GetArrayElementAtIndex(i);
                    if (removedStates.Contains(item.FindPropertyRelative("State").objectReferenceValue as StateSO))
                    {
                        nodePositions.DeleteArrayElementAtIndex(i);
                    }
                }

                if (removedStates.Contains(initialState.objectReferenceValue as StateSO))
                {
                    initialState.objectReferenceValue = null;
                    Debug.LogWarning(
                        $"{_table.name} has no initial state now. Right-click a state > Set as Initial State.",
                        _table);
                }
            }

            DeleteArrayIndices(transitions, localToRemove);
            DeleteArrayIndices(anyTransitions, anyToRemove);

            _serializedTable.ApplyModifiedProperties();

            EmbeddedAssets.DestroyUnreachable(_table, removedStates);
            Undo.CollapseUndoOperations(undoGroup);
        }

        private static void DeleteArrayIndices(SerializedProperty array, HashSet<int> indices)
        {
            // Highest first, so deleting doesn't shift the ones still to delete.
            var sorted = new List<int>(indices);
            sorted.Sort((a, b) => b.CompareTo(a));

            foreach (int index in sorted)
            {
                if (index < array.arraySize)
                {
                    array.DeleteArrayElementAtIndex(index);
                }
            }
        }

        // ---------- Initial state ----------

        private StateSO GetInitialState()
        {
            return _serializedTable.FindProperty("_initialState").objectReferenceValue as StateSO;
        }

        private void SetInitialState(StateSO state)
        {
            _serializedTable.Update();
            _serializedTable.FindProperty("_initialState").objectReferenceValue = state;
            _serializedTable.ApplyModifiedProperties();
            Reload();
        }

        // ---------- Make Transition ----------

        private void StartTransitionCreation(Node source, Vector2 mousePosition)
        {
            CancelTransitionCreation();

            _transitionSource = source;
            _transitionPreview = new TransitionPreviewView();
            Add(_transitionPreview);
            UpdateTransitionPreview(mousePosition);

            // So Escape reaches OnKeyDownWhileConnecting.
            Focus();
        }

        private void CancelTransitionCreation()
        {
            _transitionPreview?.RemoveFromHierarchy();
            _transitionPreview = null;
            _transitionSource = null;
            _retargetEdge = null;
        }

        // mousePosition is in panel space, as given by mouse events.
        private void UpdateTransitionPreview(Vector2 mousePosition)
        {
            // The preview covers this GraphView, so this GraphView's local space is the preview's too.
            Rect sourceRect = _transitionSource.parent.ChangeCoordinatesTo(this, _transitionSource.layout);
            _transitionPreview.SetPoints(sourceRect.center, this.WorldToLocal(mousePosition));
        }

        private void OnMouseMoveWhileConnecting(MouseMoveEvent evt)
        {
            if (_transitionSource != null)
            {
                UpdateTransitionPreview(evt.mousePosition);
            }
        }

        private void OnMouseDownWhileConnecting(MouseDownEvent evt)
        {
            if (_transitionSource == null)
            {
                return;
            }

            // This click only finishes (or cancels) the transition: no selecting, no dragging.
            evt.StopImmediatePropagation();
            FinishConnecting(evt.button, evt.mousePosition);
        }

        // Dragging an arrow's end: finishes where the mouse is let go.
        private void OnMouseUpWhileConnecting(MouseUpEvent evt)
        {
            if (_transitionSource == null || _retargetEdge == null || !_retargetByDrag)
            {
                return;
            }

            evt.StopImmediatePropagation();
            FinishConnecting(evt.button, evt.mousePosition);
        }

        private void FinishConnecting(int button, Vector2 mousePosition)
        {
            Node source = _transitionSource;
            TransitionEdgeView retargetEdge = _retargetEdge;
            CancelTransitionCreation();

            if (button != (int)MouseButton.LeftMouse)
            {
                return;
            }

            // Preview ignores picking, so this finds whatever is under the mouse.
            StateNodeView target = panel.Pick(mousePosition)?.GetFirstOfType<StateNodeView>();

            if (target == null || target == source)
            {
                return;
            }

            if (retargetEdge == null)
            {
                CreateTransition(source, target);
            }
            else if (target != retargetEdge.To)
            {
                RetargetTransition(retargetEdge, target);
            }
        }

        // ---------- Change Target ----------
        // Drag the end of an arrow onto another state, or right-click > Change Target and click one.
        // Its conditions stay. A Base's arrow becomes this table's own (see RetargetInherited).

        // Set while an arrow's end is being moved (the preview runs from its From node).
        private TransitionEdgeView _retargetEdge;
        private bool _retargetByDrag;

        private void AppendChangeTargetMenu(DropdownMenu menu, TransitionEdgeView edge, Vector2 mousePosition)
        {
            // Entry arrow (this table's or the Base's): Set as Initial State instead.
            if (edge.IsInherited ? edge.InheritedTransitions.Count == 0 : edge.TransitionIndices.Count == 0)
            {
                return;
            }

            menu.AppendAction(
                "Change Target",
                _ => StartRetarget(edge, mousePosition, false),
                edge.CanChangeTarget ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendSeparator();
        }

        // byDrag: finish on mouse up (dragged from the arrow's end), otherwise on the next click (menu).
        public void StartRetarget(TransitionEdgeView edge, Vector2 mousePosition, bool byDrag)
        {
            StartTransitionCreation(edge.From, mousePosition);
            _retargetEdge = edge;
            _retargetByDrag = byDrag;
        }

        private void RetargetTransition(TransitionEdgeView edge, StateNodeView target)
        {
            Undo.IncrementCurrentGroup();
            _serializedTable.Update();

            if (edge.IsInherited)
            {
                RetargetInherited(edge, target.State);
            }
            else
            {
                SerializedProperty array = _serializedTable.FindProperty(
                    edge.IsAnyState ? "_anyTransitions" : "_transitions");

                foreach (int index in edge.TransitionIndices)
                {
                    if (index < array.arraySize)
                    {
                        array.GetArrayElementAtIndex(index).FindPropertyRelative("ToState").objectReferenceValue =
                            target.State;
                    }
                }
            }

            _serializedTable.ApplyModifiedProperties();

            object fromKey = GetNodeKey(edge.From);
            Load(_table);
            SelectEdge(fromKey, target.State);
        }

        // A Base's transition stays the Base's (its conditions keep following the Base): this table only
        // notes where it goes instead (_transitionRetargets). Dropped back on the Base's own state, the note goes.
        private void RetargetInherited(TransitionEdgeView edge, StateSO target)
        {
            TransitionTableSO baseTable = SafeBaseOf(_table);
            if (baseTable == null)
            {
                return;
            }

            foreach (InheritedTransition inherited in edge.InheritedTransitions.Where(item => !item.Removed))
            {
                StateSO baseTo = edge.IsAnyState
                    ? baseTable.EffectiveAnyTransitions.FirstOrDefault(any => any.Id == inherited.Id).ToState
                    : baseTable.EffectiveTransitions.FirstOrDefault(transition => transition.Id == inherited.Id).ToState;

                SetRetarget(_serializedTable, inherited.Id, target == baseTo ? null : target);
            }
        }

        // to null: the transition goes where the Base sends it again. Also used by the arrow's Inspector.
        internal static void SetRetarget(SerializedObject serializedTable, string transitionId, StateSO to)
        {
            SerializedProperty retargets = serializedTable.FindProperty("_transitionRetargets");

            for (int i = retargets.arraySize - 1; i >= 0; i--)
            {
                if (retargets.GetArrayElementAtIndex(i).FindPropertyRelative("_transitionId").stringValue == transitionId)
                {
                    retargets.DeleteArrayElementAtIndex(i);
                }
            }

            if (to == null)
            {
                return;
            }

            retargets.arraySize++;
            SerializedProperty entry = retargets.GetArrayElementAtIndex(retargets.arraySize - 1);
            entry.FindPropertyRelative("_transitionId").stringValue = transitionId;
            entry.FindPropertyRelative("_toState").objectReferenceValue = to;
        }

        // ---------- Rename ----------

        // F2 renames the selected state, like the Project window.
        private void OnKeyDownRename(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.F2 && selection.Count == 1 && selection[0] is StateNodeView stateNode)
            {
                BeginRename(stateNode);
                evt.StopPropagation();
            }
        }

        // The window does the rename (the Contents panel's rules: Undo, sub-state machine pieces follow).
        // A Base's state in a variant asks first: a name shown only in this table's graph
        // (_stateDisplayNames), or the StateSO itself renamed, in the Base and every table that uses it.
        public void BeginRename(StateNodeView stateNode)
        {
            if (IsBaseStateHere(stateNode.State))
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Rename the Base's state?",
                    $"{stateNode.State.name} is the Base's.\n\n" +
                    "Rename Here: only this table's graph shows the new name. The state keeps its name " +
                    "everywhere else (Inspector, Project window, Play Mode debug).\n\n" +
                    "Rename in Base: the state itself is renamed, in every table that uses it.",
                    "Rename Here",
                    "Cancel",
                    "Rename in Base");

                if (choice == 0)
                {
                    stateNode.BeginRename(newName => SetDisplayName(stateNode.State, newName));
                    return;
                }

                if (choice == 1)
                {
                    return;
                }
            }

            stateNode.BeginRename(newName => StateRenameRequested?.Invoke(stateNode.State, newName));
        }

        private void OnKeyDownWhileConnecting(KeyDownEvent evt)
        {
            if (_transitionSource != null && evt.keyCode == KeyCode.Escape)
            {
                CancelTransitionCreation();
                evt.StopPropagation();
            }
        }

        private void CreateTransition(Node source, StateNodeView target)
        {
            _serializedTable.Update();

            bool isAnyState = source is SpecialNodeView;
            SerializedProperty array = _serializedTable.FindProperty(
                isAnyState ? "_anyTransitions" : "_transitions");

            // Growing the array copies the last item, so every field is set below.
            int index = array.arraySize;
            array.arraySize++;
            SerializedProperty item = array.GetArrayElementAtIndex(index);

            if (!isAnyState)
            {
                item.FindPropertyRelative("FromState").objectReferenceValue = ((StateNodeView)source).State;
            }

            item.FindPropertyRelative("ToState").objectReferenceValue = target.State;
            item.FindPropertyRelative("ConditionGroups").arraySize = 0;

            _serializedTable.ApplyModifiedProperties();

            // Select the new edge so its (empty) conditions show up in the panel right away.
            object fromKey = GetNodeKey(source);
            Load(_table);
            SelectEdge(fromKey, target.State);
        }

        // ---------- Add State ----------

        private void OpenAddStateSearch(Vector2 mousePosition)
        {
            // Where the new node goes, in the same space as node positions.
            Vector2 graphPosition = contentViewContainer.WorldToLocal(mousePosition);

            var alreadyInTable = new HashSet<StateSO>();
            foreach (GraphElement element in graphElements.ToList())
            {
                if (element is StateNodeView stateNode)
                {
                    alreadyInTable.Add(stateNode.State);
                }
            }

            if (_stateSearchProvider == null)
            {
                _stateSearchProvider = ScriptableObject.CreateInstance<StateSearchProvider>();
                _stateSearchProvider.hideFlags = HideFlags.HideAndDontSave;
            }

            _stateSearchProvider.Setup(
                alreadyInTable,
                state => AddStateToTable(state, graphPosition),
                () => CreateNewState(graphPosition),
                () => CreateNewSubStateMachine(graphPosition),
                () => CreateNewStateFile(graphPosition));

            // SearchWindow wants screen coordinates.
            Vector2 screenPosition = _window.position.position + mousePosition;
            SearchWindow.Open(new SearchWindowContext(screenPosition), _stateSearchProvider);
        }

        private void DestroyStateSearchProvider()
        {
            if (_stateSearchProvider != null)
            {
                Object.DestroyImmediate(_stateSearchProvider);
                _stateSearchProvider = null;
            }
        }

        private void AddStateToTable(StateSO state, Vector2 position)
        {
            // A position entry is what keeps a state without transitions in the table (see CollectStates).
            _serializedTable.Update();
            FindOrAddNodePosition(_serializedTable.FindProperty("_nodePositions"), state)
                .FindPropertyRelative("Position").vector2Value = position;
            _serializedTable.ApplyModifiedProperties();

            Load(_table);

            Node node = FindNode(state);
            if (node != null)
            {
                AddToSelection(node);
            }
        }

        // The new state goes inside the table's file; it can be renamed in the Contents panel.
        private void CreateNewState(Vector2 position)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            StateSO state = EmbeddedAssets.Create<StateSO>(_table, "New_State");
            AddStateToTable(state, position);

            Undo.CollapseUndoOperations(undoGroup);
        }

        // Variant tables only: a sub-state machine whose child table lives in another file (the Base's)
        // can get a Variant of its own here, like Prefab Variant for a nested prefab. Gone once it has one.
        private void AppendMakeSubVariantMenu(DropdownMenu menu, StateNodeView stateNode)
        {
            List<(SubStateMachineActionSO Action, TransitionTableSO SubTable)> subActions = SubVariantCandidates(stateNode);

            foreach ((SubStateMachineActionSO action, TransitionTableSO subTable) in subActions)
            {
                string label = subActions.Count == 1
                    ? "Make Sub Variant"
                    : $"Make Sub Variant/{subTable.name}";

                menu.AppendAction(label, _ => MakeSubVariant(stateNode.State, action, subTable));
            }
        }

        // The node's sub-state machines that could get a Variant here: empty unless this table is a variant.
        private List<(SubStateMachineActionSO Action, TransitionTableSO SubTable)> SubVariantCandidates(StateNodeView stateNode)
        {
            if (SafeBaseOf(_table) == null)
            {
                return new List<(SubStateMachineActionSO, TransitionTableSO)>();
            }

            string tablePath = AssetDatabase.GetAssetPath(_table);
            return StateNodeView.FindSubActions(stateNode.State, _table)
                .Where(sub => AssetDatabase.GetAssetPath(sub.SubTable) != tablePath)
                .ToList();
        }

        // Double-click on a sub-state machine. A Base's one asks first, since changing the Base's
        // child table changes it for every variant: make a Variant here, or open the Base's anyway.
        public void EnterSubStateMachine(StateNodeView stateNode)
        {
            TransitionTableSO subTable = stateNode.SubTables[0];
            var candidate = SubVariantCandidates(stateNode).FirstOrDefault(sub => sub.SubTable == subTable);
            if (candidate.Action == null)
            {
                OpenSubStateMachine(stateNode.State, subTable);
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "Make Sub Variant?",
                $"{subTable.name} is the Base's. Changing it changes every table that uses it.\n\n" +
                $"Make a Variant of it that only {_table.name} uses?",
                "Make Sub Variant",
                "Cancel",
                "Open Base's");

            switch (choice)
            {
                case 0:
                    MakeSubVariant(stateNode.State, candidate.Action, subTable);
                    break;
                case 2:
                    OpenSubStateMachine(stateNode.State, subTable);
                    break;
            }
        }

        // The Variant goes inside this table's file and opens right away (see EmbeddedAssets.CreateSubVariant).
        private void MakeSubVariant(StateSO state, SubStateMachineActionSO action, TransitionTableSO baseSubTable)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            TransitionTableSO subVariant = EmbeddedAssets.CreateSubVariant(_table, action, baseSubTable);

            Undo.CollapseUndoOperations(undoGroup);

            StateMachineGraphWindow.ReloadAll();
            OpenSubStateMachine(state, subVariant);
        }

        // A sub-state machine with its action and child table, already wired (see EmbeddedAssets).
        private void CreateNewSubStateMachine(Vector2 position)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            StateSO state = EmbeddedAssets.CreateSubStateMachine(_table);
            AddStateToTable(state, position);

            Undo.CollapseUndoOperations(undoGroup);
        }

        // The new state is its own file, for a state meant to be shared by several tables.
        private void CreateNewStateFile(Vector2 position)
        {
            // Default to the table's folder, where its states usually live.
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(_table))?.Replace('\\', '/');

            string path = EditorUtility.SaveFilePanelInProject(
                "Create State",
                "New_State",
                "asset",
                "Choose where to save the new StateSO.",
                folder);

            // Cancelled.
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var state = ScriptableObject.CreateInstance<StateSO>();
            AssetDatabase.CreateAsset(state, path);

            AddStateToTable(state, position);
        }

        // Called from the Contents panel: puts a state in the middle of what's on screen.
        public void AddStateAtViewCenter(StateSO state)
        {
            AddStateToTable(state, contentViewContainer.WorldToLocal(worldBound.center));
        }
    }
}

#endif
