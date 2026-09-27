#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // One node per StateSO. Initial state is orange, like the default state in Animator.
    // A state with a SubStateMachineActionSO shows "Sub-State Machine" and opens it on double-click.
    public class StateNodeView : Node
    {
        private static readonly Color InitialColor = new Color(0.75f, 0.42f, 0.12f, 1f);
        private static readonly Color NormalColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color ActiveBorderColor = new Color(0.3f, 0.8f, 1f);
        private const float ActiveBorderWidth = 3f;

        public StateSO State { get; }

        // Tables run by this state's SubStateMachineActionSO actions. Empty for a normal state.
        public IReadOnlyList<TransitionTableSO> SubTables { get; }
        public bool IsSubStateMachine => SubTables.Count > 0;

        public StateNodeView(StateSO state, bool isInitial)
        {
            State = state;
            SubTables = FindSubTables(state);
            title = state.name;

            // Hide the collapse arrow, a state node has nothing to collapse.
            titleButtonContainer.style.display = DisplayStyle.None;
            style.minWidth = 160;

            // Hide the empty port area under the title, so the node is one box like in Animator.
            VisualElement contents = mainContainer.Q("contents");
            if (contents != null)
            {
                contents.style.display = DisplayStyle.None;
            }

            titleContainer.style.backgroundColor = isInitial ? InitialColor : NormalColor;

            if (IsSubStateMachine)
            {
                // Second line under the title, so sub-state machines stand out (double-click to enter).
                var subLabel = new Label("Sub-State Machine");
                subLabel.pickingMode = PickingMode.Ignore;
                subLabel.style.fontSize = 10;
                subLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                subLabel.style.color = new Color(1f, 1f, 1f, 0.6f);
                subLabel.style.paddingLeft = 8;
                subLabel.style.paddingTop = 2;
                subLabel.style.paddingBottom = 3;
                mainContainer.Add(subLabel);
            }

            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        private static List<TransitionTableSO> FindSubTables(StateSO state)
        {
            var tables = new List<TransitionTableSO>();

            // _actions and _transitionTable are private, so read them like the Inspector does.
            using var serializedState = new SerializedObject(state);
            SerializedProperty actions = serializedState.FindProperty("_actions");
            if (actions == null)
            {
                return tables;
            }

            for (int i = 0; i < actions.arraySize; i++)
            {
                if (actions.GetArrayElementAtIndex(i).objectReferenceValue is not SubStateMachineActionSO action)
                {
                    continue;
                }

                using var serializedAction = new SerializedObject(action);
                if (serializedAction.FindProperty("_transitionTable").objectReferenceValue is TransitionTableSO table &&
                    !tables.Contains(table))
                {
                    tables.Add(table);
                }
            }

            return tables;
        }

        // The graph builds the whole menu (StateMachineGraphView.BuildContextualMenu).
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
        }

        // Play Mode: outline the state the running StateMachine is in.
        public void SetActive(bool active)
        {
            // StyleKeyword.Null removes the inline value, back to GraphView's own style.
            StyleFloat width = active ? ActiveBorderWidth : new StyleFloat(StyleKeyword.Null);
            StyleColor color = active ? ActiveBorderColor : new StyleColor(StyleKeyword.Null);

            mainContainer.style.borderTopWidth = width;
            mainContainer.style.borderBottomWidth = width;
            mainContainer.style.borderLeftWidth = width;
            mainContainer.style.borderRightWidth = width;

            mainContainer.style.borderTopColor = color;
            mainContainer.style.borderBottomColor = color;
            mainContainer.style.borderLeftColor = color;
            mainContainer.style.borderRightColor = color;
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.clickCount != 2)
            {
                return;
            }

            if (IsSubStateMachine)
            {
                // Double-click enters the sub-state machine, like Animator.
                GetFirstAncestorOfType<StateMachineGraphView>()?.OpenSubStateMachine(State, SubTables[0]);
            }
            else
            {
                // Double-click shows the StateSO (and its actions) in the Inspector.
                Selection.activeObject = State;
                EditorGUIUtility.PingObject(State);
            }

            evt.StopPropagation();
        }
    }
}

#endif
