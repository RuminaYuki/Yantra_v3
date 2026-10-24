#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Unity's Inspector for one of the Base's states clicked in a variant table's graph
    // (StateOverrideSelectionEditor): that state's actions in the order this table runs them, as a list
    // you can drag to reorder. What differs from the Base is marked blue: actions added here, and the
    // order once changed here. Actions removed here are listed under it with Restore. Stored in
    // TransitionTableSO._actionChanges / _actionOrders; the runtime builds the same list with
    // ElementResolver.BuildActionList.
    public class VariantStatePanel : VisualElement
    {
        private const string ChangesProperty = "_actionChanges";
        private const string OrdersProperty = "_actionOrders";
        private const string StateProperty = "_state";
        private const string KindProperty = "_kind";
        private const string ActionProperty = "_action";
        private const string OrderActionsProperty = "_actions";

        private static readonly Color ChangedColor = OverrideInspectorGUI.ChangedColor;
        private static readonly Color RemovedColor = OverrideInspectorGUI.RemovedColor;

        private readonly Action _onChanged;

        private TransitionTableSO _table;
        private TransitionTableSO _baseTable;
        private StateSO _state;
        private SerializedObject _serializedTable;

        // Kept between repaints: it remembers a drag in progress.
        private readonly ReorderableList _list;
        private List<StateActionSO> _runHere = new List<StateActionSO>();
        private List<StateActionSO> _fromBase = new List<StateActionSO>();

        // The order without this table's own reordering: Reset order only shows when it differs.
        private List<StateActionSO> _defaultOrder = new List<StateActionSO>();

        private int _pickerControlId;
        private bool _openPickerOnNextDraw;

        // A click's edit, done after the whole panel is drawn: rows drawn after the click in the same
        // pass still read the lists, and a deleted / moved element can't be read.
        private Action<SerializedProperty, SerializedProperty> _pendingEdit;

        private static GUIStyle _tagStyle;
        private static GUIStyle _statusStyle;

        // onChanged: after any edit (the graph redraws its "changes" labels).
        public VariantStatePanel(Action onChanged)
        {
            _onChanged = onChanged;

            style.paddingLeft = 4;
            style.paddingRight = 4;
            style.paddingTop = 4;
            style.paddingBottom = 4;

            _list = new ReorderableList(_runHere, typeof(StateActionSO), true, true, true, false)
            {
                elementHeight = EditorGUIUtility.singleLineHeight + 4f,
                drawHeaderCallback = DrawListHeader,
                drawElementCallback = DrawListRow,
                onReorderCallback = _ => _pendingEdit = (_, orderList) => SaveOrder(orderList, new List<StateActionSO>(_runHere)),
                onAddDropdownCallback = (buttonRect, _) => ShowAddMenu(buttonRect)
            };

            Add(new IMGUIContainer(OnGUI));
            RegisterDropCallbacks();
        }

        public void Show(TransitionTableSO table, TransitionTableSO baseTable, StateSO state)
        {
            _table = table;
            _baseTable = baseTable;
            _state = state;
            _serializedTable = new SerializedObject(table);
            MarkDirtyRepaint();
        }

        private void OnGUI()
        {
            if (_state == null || _table == null || _baseTable == null)
            {
                return;
            }

            CreateStyles();
            _serializedTable.Update();
            SerializedProperty changes = _serializedTable.FindProperty(ChangesProperty);
            SerializedProperty orders = _serializedTable.FindProperty(OrdersProperty);

            List<StateActionSO> stateActions = TableContents.ActionsOf(_state).OfType<StateActionSO>().ToList();
            _fromBase = ElementResolver.BuildActionList(
                _state, stateActions, _baseTable.EffectiveActionChanges, _baseTable.EffectiveActionOrders);
            _runHere = ElementResolver.BuildActionList(
                _state, stateActions, _table.EffectiveActionChanges, _table.EffectiveActionOrders);
            _defaultOrder = ElementResolver.BuildActionList(
                _state, stateActions, _table.EffectiveActionChanges, _baseTable.EffectiveActionOrders);
            _list.list = _runHere;

            OverrideInspectorGUI.DrawHeader(_state.name, _table.name);

            _list.DoLayoutList();
            EditorGUILayout.Space(4f);

            if (_openPickerOnNextDraw)
            {
                _openPickerOnNextDraw = false;
                OpenActionPicker();
            }

            HandleActionPicked();
            DrawSubStateMachines();
            DrawRemoved(changes);
            DrawUnknownChanges(changes);

            StateSO state = _state;
            OverrideInspectorGUI.DrawEditInBase(
                $"Open {state.name} itself. Changes there affect {_baseTable.name} and every table that uses it.",
                () => Selection.activeObject = state);

            if (_pendingEdit != null)
            {
                _pendingEdit(changes, orders);
                _pendingEdit = null;
            }

            if (_serializedTable.ApplyModifiedProperties())
            {
                _onChanged?.Invoke();
            }
        }

        private static void CreateStyles()
        {
            if (_tagStyle != null)
            {
                return;
            }

            _tagStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = ChangedColor }
            };
        }

        // ---------- Sub-state machine ----------

        // One box per Sub State Machine action this state runs here:
        //   Sub-State Machine   Overridden here   [Open] [Revert]     AttackVariant_TransitionTable / Base: Attack_TransitionTable
        //   Sub-State Machine   Same as the Base  [Make Sub Variant]  Attack_TransitionTable
        private void DrawSubStateMachines()
        {
            List<(SubStateMachineActionSO Action, TransitionTableSO SubTable)> baseSubs =
                StateNodeView.FindSubActions(_state, _baseTable);

            foreach ((SubStateMachineActionSO action, TransitionTableSO baseSubTable) in baseSubs)
            {
                if (!_runHere.Contains(action))
                {
                    continue;
                }

                TransitionTableSO ownSubTable = OverriddenSubTable(action);
                string title = baseSubs.Count > 1 ? $"Sub-State Machine · {action.name}" : "Sub-State Machine";

                if (ownSubTable != null)
                {
                    int clicked = OverrideInspectorGUI.DrawBoxHeader(
                        title, "Overridden here", ChangedColor,
                        new GUIContent("Open", "Open it in the graph"),
                        new GUIContent("Revert", "Run the Base's sub-state machine here again. The Variant table stays in the file, unused."));

                    if (clicked == 0)
                    {
                        StateMachineGraphWindow.OpenSubStateMachine(_table, _state, ownSubTable);
                    }
                    else if (clicked == 1)
                    {
                        _pendingEdit = (_, _) => RemoveSubOverride(action);
                    }

                    OverrideInspectorGUI.BeginBoxBody();
                    EditorGUILayout.LabelField(ownSubTable.name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Base: {baseSubTable.name}", EditorStyles.miniLabel);
                    OverrideInspectorGUI.EndBoxBody();
                }
                else
                {
                    int clicked = OverrideInspectorGUI.DrawBoxHeader(
                        title, "Same as the Base", OverrideInspectorGUI.SameColor,
                        new GUIContent("Make Sub Variant", "A Variant of it that only this table runs"));

                    if (clicked == 0)
                    {
                        StateSO state = _state;
                        TransitionTableSO table = _table;

                        // After this pass: it creates assets and reloads the graph.
                        EditorApplication.delayCall += () =>
                        {
                            Undo.IncrementCurrentGroup();
                            int undoGroup = Undo.GetCurrentGroup();
                            TransitionTableSO subVariant = EmbeddedAssets.CreateSubVariant(table, action, baseSubTable);
                            Undo.CollapseUndoOperations(undoGroup);

                            StateMachineGraphWindow.ReloadAll();
                            StateMachineGraphWindow.OpenSubStateMachine(table, state, subVariant);
                        };
                    }

                    OverrideInspectorGUI.BeginBoxBody();
                    EditorGUILayout.LabelField(baseSubTable.name, EditorStyles.label);
                    OverrideInspectorGUI.EndBoxBody();
                }
            }
        }

        // This table's own field override that points the action at another table, or null.
        private TransitionTableSO OverriddenSubTable(SubStateMachineActionSO action)
        {
            SerializedProperty overrides = _serializedTable.FindProperty(StateMachineFields.FieldOverrides);
            TransitionTableSO found = null;

            for (int i = 0; i < overrides.arraySize; i++)
            {
                SerializedProperty entry = overrides.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("_target").objectReferenceValue == action &&
                    entry.FindPropertyRelative("_fieldPath").stringValue == StateMachineFields.TransitionTable &&
                    entry.FindPropertyRelative("_object").objectReferenceValue is TransitionTableSO table)
                {
                    found = table;
                }
            }

            return found;
        }

        private void RemoveSubOverride(SubStateMachineActionSO action)
        {
            SerializedProperty overrides = _serializedTable.FindProperty(StateMachineFields.FieldOverrides);

            for (int i = overrides.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty entry = overrides.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("_target").objectReferenceValue == action &&
                    entry.FindPropertyRelative("_fieldPath").stringValue == StateMachineFields.TransitionTable)
                {
                    overrides.DeleteArrayElementAtIndex(i);
                }
            }

            // The node opens the Base's sub-state machine again.
            EditorApplication.delayCall += StateMachineGraphWindow.ReloadAll;
        }

        // ---------- The list ----------

        // Same layout as the other override boxes: "Actions" on the left, the status on the right
        // (grey "Same order as the Base" / blue "Order changed here" with Reset).
        private void DrawListHeader(Rect rect)
        {
            if (_statusStyle == null)
            {
                _statusStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            }

            bool isOrderChanged = !_runHere.SequenceEqual(_defaultOrder);
            float right = rect.xMax;

            if (isOrderChanged)
            {
                var content = new GUIContent("Reset", "Back to the Base's order");
                float width = EditorStyles.miniButton.CalcSize(content).x + 6f;
                var reset = new Rect(right - width, rect.y + 1f, width, rect.height - 2f);
                if (GUI.Button(reset, content, EditorStyles.miniButton))
                {
                    _pendingEdit = (_, orderList) => orderList.DeleteArrayElementAtIndex(OwnOrderIndex(orderList));
                }

                right -= width + 4f;
            }

            var labelRect = new Rect(rect.x, rect.y, right - rect.x, rect.height);
            EditorGUI.LabelField(labelRect, new GUIContent("Actions", "Drag to change the order. Drop actions here to add them."));

            Color previous = GUI.contentColor;
            GUI.contentColor = isOrderChanged ? OverrideInspectorGUI.ChangedColor : OverrideInspectorGUI.SameColor;
            EditorGUI.LabelField(labelRect, isOrderChanged ? "Order changed here" : "Drag to reorder · drop to add", _statusStyle);
            GUI.contentColor = previous;
        }

        // [name ............ added] [×]. Added here: blue bar and "added" tag.
        private void DrawListRow(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index >= _runHere.Count)
            {
                return;
            }

            StateActionSO action = _runHere[index];
            bool isAddedHere = !_fromBase.Contains(action);
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;

            var removeRect = new Rect(rect.xMax - 20f, rect.y, 20f, rect.height);
            var nameRect = new Rect(rect.x, rect.y, rect.width - 24f, rect.height);

            // A Base's Sub State Machine action running this table's own sub table (Make Sub Variant).
            bool isSubOverridden = !isAddedHere &&
                action is SubStateMachineActionSO subAction &&
                OverriddenSubTable(subAction) != null;

            if ((isAddedHere || isSubOverridden) && Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(rect.x - 4f, rect.y, 2f, rect.height), ChangedColor);
            }

            EditorGUI.LabelField(nameRect, new GUIContent(action.name, isAddedHere ? "Added in this table." : "From the Base."));
            if (isAddedHere)
            {
                EditorGUI.LabelField(nameRect, "added", _tagStyle);
            }
            else if (isSubOverridden)
            {
                EditorGUI.LabelField(nameRect, "sub-state machine overridden", _tagStyle);
            }

            string tooltip = isAddedHere ? "Take it out" : "Don't run it in this table (the Base keeps it)";
            if (GUI.Button(removeRect, new GUIContent("×", tooltip), EditorStyles.miniButton))
            {
                if (isAddedHere)
                {
                    _pendingEdit = (changeList, _) => DeleteChanges(changeList, ActionChange.ChangeKind.Add, action);
                }
                else
                {
                    _pendingEdit = (changeList, _) => AddChange(changeList, ActionChange.ChangeKind.Remove, action);
                }
            }
        }

        // ---------- Drop actions here ----------

        private void RegisterDropCallbacks()
        {
            RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (_state != null && DraggedActions().Any())
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    SetDropHighlight(true);
                }

                evt.StopPropagation();
            });

            RegisterCallback<DragLeaveEvent>(_ => SetDropHighlight(false));
            RegisterCallback<DragExitedEvent>(_ => SetDropHighlight(false));

            RegisterCallback<DragPerformEvent>(evt =>
            {
                SetDropHighlight(false);
                List<StateActionSO> actions = DraggedActions().ToList();
                evt.StopPropagation();

                if (_state == null || actions.Count == 0)
                {
                    return;
                }

                DragAndDrop.AcceptDrag();

                // Editing while the drop event is still being sent around is unsafe, so wait a frame.
                StateSO state = _state;
                TransitionTableSO table = _table;
                schedule.Execute(() =>
                {
                    AddInTable(table, state, actions);
                    _onChanged?.Invoke();
                    MarkDirtyRepaint();
                });
            });
        }

        private static IEnumerable<StateActionSO> DraggedActions()
        {
            return DragAndDrop.objectReferences.OfType<StateActionSO>().Distinct();
        }

        private void SetDropHighlight(bool on)
        {
            StyleColor color = on ? ChangedColor : new StyleColor(StyleKeyword.Null);
            StyleFloat width = on ? 2f : new StyleFloat(StyleKeyword.Null);
            style.borderTopColor = color;
            style.borderBottomColor = color;
            style.borderLeftColor = color;
            style.borderRightColor = color;
            style.borderTopWidth = width;
            style.borderBottomWidth = width;
            style.borderLeftWidth = width;
            style.borderRightWidth = width;
        }

        // Actions added to a Base's state for this table only: one it removed comes back (Restore),
        // one it already runs is skipped, anything else is added at the end. Without Undo, like the
        // graph's other drops: Unity can revert the current Undo group right after a drag ends.
        public static void AddInTable(TransitionTableSO table, StateSO state, IEnumerable<StateActionSO> actions)
        {
            var serializedTable = new SerializedObject(table);
            SerializedProperty changes = serializedTable.FindProperty(ChangesProperty);

            List<StateActionSO> runHere = ElementResolver.BuildActionList(
                state,
                TableContents.ActionsOf(state).OfType<StateActionSO>(),
                table.EffectiveActionChanges,
                table.EffectiveActionOrders);

            foreach (StateActionSO action in actions)
            {
                bool wasRemovedHere = false;
                for (int i = changes.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty change = changes.GetArrayElementAtIndex(i);
                    if (change.FindPropertyRelative(StateProperty).objectReferenceValue == state &&
                        change.FindPropertyRelative(KindProperty).intValue == (int)ActionChange.ChangeKind.Remove &&
                        change.FindPropertyRelative(ActionProperty).objectReferenceValue == action)
                    {
                        changes.DeleteArrayElementAtIndex(i);
                        wasRemovedHere = true;
                    }
                }

                if (wasRemovedHere || runHere.Contains(action))
                {
                    continue;
                }

                changes.arraySize++;
                SerializedProperty added = changes.GetArrayElementAtIndex(changes.arraySize - 1);
                added.FindPropertyRelative(StateProperty).objectReferenceValue = state;
                added.FindPropertyRelative(KindProperty).intValue = (int)ActionChange.ChangeKind.Add;
                added.FindPropertyRelative(ActionProperty).objectReferenceValue = action;
            }

            serializedTable.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- + : pick an action ----------

        // The actions inside this table's file and its Bases' files (sub-assets), plus a new one made
        // in this file. Anything else in the project takes one more step: "Other Action in Project...",
        // so actions from unrelated tables don't end up here by accident.
        private void ShowAddMenu(Rect buttonRect)
        {
            var menu = new GenericMenu();

            AddFileActionsToMenu(menu, _table, "This Table");
            var visited = new HashSet<TransitionTableSO> { _table };
            for (TransitionTableSO current = _baseTable;
                 current != null && visited.Add(current);
                 current = StateMachineGraphView.BaseOf(current))
            {
                AddFileActionsToMenu(menu, current, $"Base · {current.name}");
            }

            menu.AddSeparator(string.Empty);

            foreach (Type type in TypeCache.GetTypesDerivedFrom<StateActionSO>()
                         .Where(type => !type.IsAbstract)
                         .OrderBy(type => type.Name))
            {
                Type actionType = type;
                menu.AddItem(new GUIContent($"New Action (Sub-Asset)/{ObjectNames.NicifyVariableName(type.Name)}"), false,
                    () => CreateAndAdd(actionType));
            }

            // The picker has to be opened from inside the panel's draw to report back to it.
            menu.AddItem(new GUIContent("Other Action in Project..."), false, () =>
            {
                _openPickerOnNextDraw = true;
                MarkDirtyRepaint();
            });
            menu.DropDown(buttonRect);
        }

        // "This Table/IdleDebugLog_Action": the file's actions this state doesn't run here yet.
        private void AddFileActionsToMenu(GenericMenu menu, TransitionTableSO table, string heading)
        {
            List<StateActionSO> actions = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(table))
                .OfType<StateActionSO>()
                .Where(action => !_runHere.Contains(action))
                .OrderBy(action => action.name)
                .ToList();

            if (actions.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent($"{heading}/(nothing else to add)"));
                return;
            }

            foreach (StateActionSO action in actions)
            {
                menu.AddItem(new GUIContent($"{heading}/{action.name}"), false, () => AddLater(action));
            }
        }

        // A new action inside this table's file, added to the state here. Rename it in Contents (F2).
        private void CreateAndAdd(Type actionType)
        {
            string baseName = actionType.Name.EndsWith("SO") ? actionType.Name.Substring(0, actionType.Name.Length - 2) : actionType.Name;
            var action = (StateActionSO)EmbeddedAssets.Create(_table, actionType, $"New_{baseName}");
            AddLater(action);
            Selection.activeObject = action;
        }

        // Menu clicks arrive outside OnGUI: the add waits for the next panel draw.
        private void AddLater(StateActionSO action)
        {
            _pendingEdit = (changeList, _) => AddChange(changeList, ActionChange.ChangeKind.Add, action);
            MarkDirtyRepaint();
        }

        private void OpenActionPicker()
        {
            _pickerControlId = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.ShowObjectPicker<StateActionSO>(null, false, string.Empty, _pickerControlId);
        }

        // The object picker reports back with a command event once it closes.
        private void HandleActionPicked()
        {
            Event current = Event.current;
            if (current.type != EventType.ExecuteCommand ||
                current.commandName != "ObjectSelectorClosed" ||
                EditorGUIUtility.GetObjectPickerControlID() != _pickerControlId)
            {
                return;
            }

            if (EditorGUIUtility.GetObjectPickerObject() is StateActionSO picked)
            {
                _pendingEdit = (changeList, _) => AddChange(changeList, ActionChange.ChangeKind.Add, picked);
            }

            _pickerControlId = 0;
            current.Use();
        }

        // ---------- Removed here ----------

        // The Base's actions this table doesn't run, folded under the list, each with Restore.
        private void DrawRemoved(SerializedProperty changes)
        {
            var removed = new List<StateActionSO>();
            for (int i = 0; i < changes.arraySize; i++)
            {
                SerializedProperty change = changes.GetArrayElementAtIndex(i);
                if (IsFor(change, ActionChange.ChangeKind.Remove) &&
                    change.FindPropertyRelative(ActionProperty).objectReferenceValue is StateActionSO action &&
                    !removed.Contains(action))
                {
                    removed.Add(action);
                }
            }

            if (removed.Count == 0)
            {
                return;
            }

            OverrideInspectorGUI.DrawBoxHeader("Removed here", $"{removed.Count}", OverrideInspectorGUI.RemovedColor);
            OverrideInspectorGUI.BeginBoxBody();

            foreach (StateActionSO action in removed)
            {
                // The Base took it out too: removing it here does nothing now. Kept until cleared,
                // so it's still removed here if the Base puts it back.
                bool isStillInBase = _fromBase.Contains(action);

                EditorGUILayout.BeginHorizontal();

                Color previous = GUI.contentColor;
                GUI.contentColor = isStillInBase ? RemovedColor : Color.gray;
                var label = isStillInBase
                    ? new GUIContent(action.name, "The Base runs it, this table doesn't.")
                    : new GUIContent($"{action.name}  (not in the Base any more)",
                        "The Base doesn't run it either, so this does nothing now. If the Base adds it back, " +
                        "it stays removed here until you clear this.");
                EditorGUILayout.LabelField(label, isStillInBase ? EditorStyles.label : EditorStyles.miniLabel);
                GUI.contentColor = previous;

                var button = isStillInBase
                    ? new GUIContent("Restore", "Run it again in this table")
                    : new GUIContent("Clear", "Forget this removal");
                if (GUILayout.Button(button, EditorStyles.miniButton, GUILayout.Width(60f)))
                {
                    _pendingEdit = (changeList, _) => DeleteChanges(changeList, ActionChange.ChangeKind.Remove, action);
                }

                EditorGUILayout.EndHorizontal();
            }

            OverrideInspectorGUI.EndBoxBody();
        }

        // Entries that do nothing: an old kind (Replace, taken out), or an Add with no action picked.
        private void DrawUnknownChanges(SerializedProperty changes)
        {
            int count = 0;
            for (int i = 0; i < changes.arraySize; i++)
            {
                if (IsLeftover(changes.GetArrayElementAtIndex(i)))
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            Color previous = GUI.contentColor;
            GUI.contentColor = RemovedColor;
            EditorGUILayout.LabelField($"{count} old entries that do nothing", EditorStyles.miniLabel);
            GUI.contentColor = previous;

            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(60f)))
            {
                _pendingEdit = (changeList, _) =>
                {
                    for (int i = changeList.arraySize - 1; i >= 0; i--)
                    {
                        if (IsLeftover(changeList.GetArrayElementAtIndex(i)))
                        {
                            changeList.DeleteArrayElementAtIndex(i);
                        }
                    }
                };
            }

            EditorGUILayout.EndHorizontal();
        }

        private bool IsLeftover(SerializedProperty change)
        {
            if (change.FindPropertyRelative(StateProperty).objectReferenceValue != _state)
            {
                return false;
            }

            int kind = change.FindPropertyRelative(KindProperty).intValue;
            bool knownKind = kind == (int)ActionChange.ChangeKind.Remove || kind == (int)ActionChange.ChangeKind.Add;
            bool hasAction = change.FindPropertyRelative(ActionProperty).objectReferenceValue != null;
            return !knownKind || !hasAction;
        }

        // ---------- Edits ----------

        private void AddChange(SerializedProperty changes, ActionChange.ChangeKind kind, StateActionSO action)
        {
            changes.arraySize++;
            SerializedProperty change = changes.GetArrayElementAtIndex(changes.arraySize - 1);
            change.FindPropertyRelative(StateProperty).objectReferenceValue = _state;
            change.FindPropertyRelative(KindProperty).intValue = (int)kind;
            change.FindPropertyRelative(ActionProperty).objectReferenceValue = action;
        }

        // Every change of this kind for the action in this state (last first, so indexes stay valid).
        private void DeleteChanges(SerializedProperty changes, ActionChange.ChangeKind kind, StateActionSO action)
        {
            for (int i = changes.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty change = changes.GetArrayElementAtIndex(i);
                if (IsFor(change, kind) && change.FindPropertyRelative(ActionProperty).objectReferenceValue == action)
                {
                    changes.DeleteArrayElementAtIndex(i);
                }
            }
        }

        // The whole run order as shown, so the runtime puts them back exactly like this.
        private void SaveOrder(SerializedProperty orders, List<StateActionSO> runOrder)
        {
            int index = OwnOrderIndex(orders);

            // Dragged back to the Base's order: nothing to keep.
            if (runOrder.SequenceEqual(_defaultOrder))
            {
                if (index >= 0)
                {
                    orders.DeleteArrayElementAtIndex(index);
                }

                return;
            }
            if (index < 0)
            {
                orders.arraySize++;
                index = orders.arraySize - 1;
                orders.GetArrayElementAtIndex(index).FindPropertyRelative(StateProperty).objectReferenceValue = _state;
            }

            SerializedProperty actions = orders.GetArrayElementAtIndex(index).FindPropertyRelative(OrderActionsProperty);
            actions.arraySize = runOrder.Count;
            for (int i = 0; i < runOrder.Count; i++)
            {
                actions.GetArrayElementAtIndex(i).objectReferenceValue = runOrder[i];
            }
        }

        private int OwnOrderIndex(SerializedProperty orders)
        {
            for (int i = 0; i < orders.arraySize; i++)
            {
                if (orders.GetArrayElementAtIndex(i).FindPropertyRelative(StateProperty).objectReferenceValue == _state)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsFor(SerializedProperty change, ActionChange.ChangeKind kind)
        {
            return change.FindPropertyRelative(StateProperty).objectReferenceValue == _state &&
                   change.FindPropertyRelative(KindProperty).intValue == (int)kind;
        }
    }
}

#endif
