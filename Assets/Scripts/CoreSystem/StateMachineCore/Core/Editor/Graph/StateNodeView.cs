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

        // Also used by the Contents panel to mark sub-state machines.
        internal static List<TransitionTableSO> FindSubTables(StateSO state)
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

        private ParameterTagLabel _parameterTags;

        // How many exposed parameters this state's actions have, as a badge inside the node under the
        // title (see StateMachineGraphView.RefreshParameterTags). Inside, so it never runs into arrows.
        public void SetParameterTags(IReadOnlyList<string> names)
        {
            if (_parameterTags == null)
            {
                _parameterTags = new ParameterTagLabel();
                _parameterTags.style.marginLeft = 6f;
                _parameterTags.style.marginTop = 3f;
                _parameterTags.style.marginBottom = 3f;
                mainContainer.Add(_parameterTags);
            }

            _parameterTags.Show(names);
        }

        private static readonly Color UsesSelectionBorderColor = new Color(1f, 0.65f, 0.2f);
        private const float UsesSelectionBorderWidth = 2f;

        private static readonly Color DropTargetBorderColor = new Color(0.27f, 0.6f, 1f);

        private bool _isActive;
        private bool _usesSelection;
        private bool _isDropTarget;

        // An action is being dragged over this node (see StateMachineGraphView.RegisterActionDropCallbacks).
        public void SetDropTarget(bool isTarget)
        {
            _isDropTarget = isTarget;
            ApplyBorder();
        }

        // Play Mode: outline the state the running StateMachine is in.
        public void SetActive(bool active)
        {
            _isActive = active;
            ApplyBorder();
        }

        // An action selected elsewhere (Contents, Project, Inspector) is one of this state's actions.
        // The Play Mode outline wins when both are on.
        public void SetUsesSelection(bool uses)
        {
            _usesSelection = uses;
            ApplyBorder();
        }

        private void ApplyBorder()
        {
            // StyleKeyword.Null removes the inline value, back to GraphView's own style.
            StyleFloat width = _isDropTarget ? ActiveBorderWidth
                : _isActive ? ActiveBorderWidth
                : _usesSelection ? UsesSelectionBorderWidth
                : new StyleFloat(StyleKeyword.Null);
            StyleColor color = _isDropTarget ? DropTargetBorderColor
                : _isActive ? ActiveBorderColor
                : _usesSelection ? UsesSelectionBorderColor
                : new StyleColor(StyleKeyword.Null);

            mainContainer.style.borderTopWidth = width;
            mainContainer.style.borderBottomWidth = width;
            mainContainer.style.borderLeftWidth = width;
            mainContainer.style.borderRightWidth = width;

            mainContainer.style.borderTopColor = color;
            mainContainer.style.borderBottomColor = color;
            mainContainer.style.borderLeftColor = color;
            mainContainer.style.borderRightColor = color;
        }

        // Like the Contents panel: the title becomes a text field. Enter or clicking away keeps
        // the new name, Escape cancels. onRenamed gets the typed name (the node is rebuilt after).
        public void BeginRename(Action<string> onRenamed)
        {
            Label titleLabel = titleContainer.Q<Label>("title-label");

            // Already renaming.
            if (titleLabel == null || titleLabel.style.display == DisplayStyle.None)
            {
                return;
            }

            var field = new TextField { value = State.name };
            field.style.flexGrow = 1;
            field.style.marginLeft = 4;
            field.style.marginRight = 4;
            titleLabel.style.display = DisplayStyle.None;
            titleContainer.Insert(titleContainer.IndexOf(titleLabel) + 1, field);

            bool isFinished = false;
            void Finish(bool keep)
            {
                if (isFinished)
                {
                    return;
                }

                isFinished = true;
                string newName = field.value;
                field.RemoveFromHierarchy();
                titleLabel.style.display = DisplayStyle.Flex;

                // Renaming reloads the graph, which isn't safe while this event is still being sent.
                if (keep)
                {
                    schedule.Execute(() => onRenamed(newName));
                }
            }

            // TrickleDown: catch Enter / Escape before the text field itself uses them.
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    Finish(true);
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    Finish(false);
                    evt.StopPropagation();
                }
            }, TrickleDown.TrickleDown);
            field.RegisterCallback<FocusOutEvent>(_ => Finish(true));

            // Keys typed here are for the text, not the graph (Delete must not delete the node).
            field.RegisterCallback<KeyDownEvent>(evt => evt.StopPropagation());
            field.RegisterCallback<ValidateCommandEvent>(evt => evt.StopPropagation());
            field.RegisterCallback<ExecuteCommandEvent>(evt => evt.StopPropagation());

            // Selecting text with the mouse must not drag the node or open it (double-click).
            field.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
            field.RegisterCallback<MouseMoveEvent>(evt => evt.StopPropagation());
            field.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            field.RegisterCallback<PointerMoveEvent>(evt => evt.StopPropagation());

            // The field has to be on screen before it can take focus.
            field.schedule.Execute(() =>
            {
                field.Focus();
                field.SelectAll();
            });
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
                // Double-click renames (a click already shows the state in the Inspector).
                GetFirstAncestorOfType<StateMachineGraphView>()?.BeginRename(this);
            }

            evt.StopPropagation();
        }
    }
}

#endif
