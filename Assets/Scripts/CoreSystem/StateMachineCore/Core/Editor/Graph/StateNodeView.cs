#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // One node per StateSO. Initial state is orange, like the default state in Animator.
    public class StateNodeView : Node
    {
        private static readonly Color InitialColor = new Color(0.75f, 0.42f, 0.12f, 1f);
        private static readonly Color NormalColor = new Color(0.25f, 0.25f, 0.25f, 1f);

        public StateSO State { get; }

        public StateNodeView(StateSO state, bool isInitial)
        {
            State = state;
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

            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        // The graph builds the whole menu (StateMachineGraphView.BuildContextualMenu).
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.clickCount != 2)
            {
                return;
            }

            // Double-click shows the StateSO (and its actions) in the Inspector.
            Selection.activeObject = State;
            EditorGUIUtility.PingObject(State);
            evt.StopPropagation();
        }
    }
}

#endif
