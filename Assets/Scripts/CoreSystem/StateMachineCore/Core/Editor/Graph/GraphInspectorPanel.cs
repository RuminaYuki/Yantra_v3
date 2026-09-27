#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Right-hand panel of the graph window, like Animator's Inspector:
    // - state node selected: the StateSO's actions and its outgoing transitions (reorderable)
    // - Any State selected: the Any State transitions (reorderable)
    // - edge selected: the transitions behind it, with their conditions
    public class GraphInspectorPanel : VisualElement
    {
        // Raised after a change that the graph has to redraw for (e.g. a new initial state).
        private readonly Action _onGraphDataChanged;

        // Asks the graph to select an edge. From is a StateSO, or SpecialNodeView.Kind.AnyState.
        private readonly Action<object, StateSO> _onSelectTransition;

        private SerializedObject _serializedTable;
        private TransitionEdgeView _edge;
        private StateNodeView _stateNode;
        private bool _isAnyStateSelected;
        private Vector2 _scroll;

        // Inspector of the selected StateSO, drawn inside this panel.
        private UnityEditor.Editor _stateEditor;

        // Other TransitionTableSO assets that also use the selected StateSO.
        private readonly List<string> _otherTablesUsingState = new List<string>();

        public GraphInspectorPanel(Action onGraphDataChanged, Action<object, StateSO> onSelectTransition)
        {
            _onGraphDataChanged = onGraphDataChanged;
            _onSelectTransition = onSelectTransition;

            style.minWidth = 250;
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.paddingTop = 6;

            var container = new IMGUIContainer(OnGUI);
            container.style.flexGrow = 1;
            Add(container);

            // Editors are UnityEngine.Objects and must be destroyed by hand.
            RegisterCallback<DetachFromPanelEvent>(_ => DestroyStateEditor());
        }

        public void SetTable(TransitionTableSO table)
        {
            _serializedTable = table != null ? new SerializedObject(table) : null;
            ShowSelection(null);
        }

        // The single selected graph element, or null.
        public void ShowSelection(GraphElement element)
        {
            StateSO previousState = _stateNode?.State;

            _edge = element as TransitionEdgeView;
            _stateNode = element as StateNodeView;
            _isAnyStateSelected = element is SpecialNodeView { NodeKind: SpecialNodeView.Kind.AnyState };

            StateSO newState = _stateNode?.State;

            // Keep the editor when Undo reselects the same state, so foldouts don't reset.
            if (newState != previousState)
            {
                DestroyStateEditor();
                _otherTablesUsingState.Clear();
                _scroll = Vector2.zero;

                if (newState != null)
                {
                    _stateEditor = UnityEditor.Editor.CreateEditor(newState);
                    FindOtherTablesUsing(newState);
                }
            }
            else if (_edge != null)
            {
                _scroll = Vector2.zero;
            }

            MarkDirtyRepaint();
        }

        private void DestroyStateEditor()
        {
            if (_stateEditor != null)
            {
                UnityEngine.Object.DestroyImmediate(_stateEditor);
                _stateEditor = null;
            }
        }

        private void FindOtherTablesUsing(StateSO state)
        {
            string statePath = AssetDatabase.GetAssetPath(state);
            string currentTablePath = AssetDatabase.GetAssetPath(_serializedTable.targetObject);

            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(TransitionTableSO)}"))
            {
                string tablePath = AssetDatabase.GUIDToAssetPath(guid);
                if (tablePath == currentTablePath)
                {
                    continue;
                }

                // Direct dependencies only: the assets this table references itself.
                if (Array.IndexOf(AssetDatabase.GetDependencies(tablePath, false), statePath) >= 0)
                {
                    _otherTablesUsingState.Add(System.IO.Path.GetFileNameWithoutExtension(tablePath));
                }
            }
        }

        private void OnGUI()
        {
            if (_serializedTable == null || _serializedTable.targetObject == null)
            {
                EditorGUILayout.HelpBox("No Transition Table.", MessageType.None);
                return;
            }

            if (_edge == null && _stateNode == null && !_isAnyStateSelected)
            {
                EditorGUILayout.HelpBox("Select a state or a transition arrow to edit it.", MessageType.Info);
                return;
            }

            // Pick up changes made in the Inspector or by Undo.
            _serializedTable.Update();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (_stateNode != null)
            {
                DrawState();
            }
            else if (_isAnyStateSelected)
            {
                DrawAnyState();
            }
            else if (IsEntryEdge())
            {
                DrawEdgeTitle();
                DrawEntry();
            }
            else
            {
                DrawEdgeTitle();
                DrawTransitions();
            }

            EditorGUILayout.EndScrollView();

            _serializedTable.ApplyModifiedProperties();
        }

        // ---------- State ----------

        private void DrawState()
        {
            StateSO state = _stateNode.State;

            EditorGUILayout.LabelField(state.name, EditorStyles.boldLabel);

            // Clickable reference to the asset (pings it in the Project window).
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(state, typeof(StateSO), false);
            }

            if (_otherTablesUsingState.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Shared asset: also used by {_otherTablesUsingState.Count} other table(s). " +
                    $"Editing actions here changes them too.\n{string.Join(", ", _otherTablesUsingState)}",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(6f);

            if (_stateEditor != null)
            {
                // The StateSO's own Inspector (applies its own changes and records Undo).
                _stateEditor.OnInspectorGUI();
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Transitions from this state", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Checked top to bottom, after Any State transitions.",
                EditorStyles.miniLabel);

            DrawOrderedTransitions(
                _serializedTable.FindProperty("_transitions"),
                transition => transition.FindPropertyRelative("FromState").objectReferenceValue == state,
                state);
        }

        // ---------- Any State ----------

        private void DrawAnyState()
        {
            EditorGUILayout.LabelField("Any State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Checked top to bottom, before the current state's own transitions.",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            DrawOrderedTransitions(
                _serializedTable.FindProperty("_anyTransitions"),
                _ => true,
                SpecialNodeView.Kind.AnyState);
        }

        // ---------- Priority list (shared by State and Any State) ----------

        // Lists the items of `transitions` that pass `belongs`, in priority order, with ▲ / ▼.
        // Moving swaps two of those items inside the array; items that don't belong keep their place,
        // which is fine because only one state's transitions are ever checked against each other.
        private void DrawOrderedTransitions(
            SerializedProperty transitions,
            Func<SerializedProperty, bool> belongs,
            object fromKey)
        {
            var indices = new List<int>();
            for (int i = 0; i < transitions.arraySize; i++)
            {
                if (belongs(transitions.GetArrayElementAtIndex(i)))
                {
                    indices.Add(i);
                }
            }

            if (indices.Count == 0)
            {
                EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
                return;
            }

            // Moving the array while it's being drawn would mix up the rows, so do it after the loop.
            int moveFrom = -1;
            int moveTo = -1;

            for (int row = 0; row < indices.Count; row++)
            {
                SerializedProperty transition = transitions.GetArrayElementAtIndex(indices[row]);
                var toState = transition.FindPropertyRelative("ToState").objectReferenceValue as StateSO;
                string label = $"{row + 1}.  →  {(toState != null ? toState.name : "None")}";

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(label, EditorStyles.helpBox) && toState != null)
                {
                    // Selecting while IMGUI is still drawing would swap the panel mid-frame.
                    schedule.Execute(() => _onSelectTransition?.Invoke(fromKey, toState));
                }

                using (new EditorGUI.DisabledScope(row == 0))
                {
                    if (GUILayout.Button("▲", GUILayout.Width(24f)))
                    {
                        moveFrom = row;
                        moveTo = row - 1;
                    }
                }

                using (new EditorGUI.DisabledScope(row == indices.Count - 1))
                {
                    if (GUILayout.Button("▼", GUILayout.Width(24f)))
                    {
                        moveFrom = row;
                        moveTo = row + 1;
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            if (moveFrom >= 0)
            {
                SwapArrayElements(transitions, indices[moveFrom], indices[moveTo]);
                _serializedTable.ApplyModifiedProperties();

                // Edges remember array indexes, so the graph has to rebuild.
                schedule.Execute(() => _onGraphDataChanged?.Invoke());
            }
        }

        private static void SwapArrayElements(SerializedProperty array, int a, int b)
        {
            int low = Mathf.Min(a, b);
            int high = Mathf.Max(a, b);

            // [.. L .. H] -> [.. H L ..] -> [.. H .. L]
            array.MoveArrayElement(high, low);
            array.MoveArrayElement(low + 1, high);
        }

        // ---------- Edge ----------

        private void DrawEdgeTitle()
        {
            EditorGUILayout.LabelField(
                $"{_edge.From.title}  →  {_edge.To.title}",
                EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);
        }

        private bool IsEntryEdge()
        {
            return _edge.From is SpecialNodeView { NodeKind: SpecialNodeView.Kind.Entry };
        }

        private void DrawEntry()
        {
            SerializedProperty initialState = _serializedTable.FindProperty("_initialState");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(initialState, new GUIContent("Initial State"));

            if (EditorGUI.EndChangeCheck())
            {
                // Apply before the graph reloads, since the reload reads the asset.
                _serializedTable.ApplyModifiedProperties();
                schedule.Execute(() => _onGraphDataChanged?.Invoke());
            }
        }

        private void DrawTransitions()
        {
            SerializedProperty transitions = _serializedTable.FindProperty(
                _edge.IsAnyState ? "_anyTransitions" : "_transitions");

            foreach (int index in _edge.TransitionIndices)
            {
                if (!IsStillSameTransition(transitions, index))
                {
                    // The table was changed elsewhere (e.g. reordered in the Inspector).
                    EditorGUILayout.HelpBox(
                        "The table changed outside the graph. Reopen the graph window to refresh.",
                        MessageType.Warning);
                    return;
                }

                SerializedProperty transition = transitions.GetArrayElementAtIndex(index);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                transition.isExpanded = EditorGUILayout.Foldout(
                    transition.isExpanded,
                    $"Priority {index + 1}",
                    true);

                if (transition.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    TransitionGUI.DrawConditionGroups(
                        transition.FindPropertyRelative("ConditionGroups"));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
            }
        }

        // Guards against editing the wrong item if the array changed since the graph was built.
        private bool IsStillSameTransition(SerializedProperty transitions, int index)
        {
            if (index >= transitions.arraySize)
            {
                return false;
            }

            SerializedProperty transition = transitions.GetArrayElementAtIndex(index);

            if (transition.FindPropertyRelative("ToState").objectReferenceValue !=
                (_edge.To as StateNodeView)?.State)
            {
                return false;
            }

            return _edge.IsAnyState ||
                transition.FindPropertyRelative("FromState").objectReferenceValue ==
                (_edge.From as StateNodeView)?.State;
        }
    }
}

#endif
