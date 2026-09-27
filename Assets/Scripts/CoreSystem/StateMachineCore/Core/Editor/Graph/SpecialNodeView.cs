#if UNITY_EDITOR

using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Entry and Any State. They are not StateSO assets, they stand for
    // _initialState and _anyTransitions in the TransitionTableSO.
    public class SpecialNodeView : Node
    {
        public enum Kind
        {
            Entry,
            AnyState
        }

        private static readonly Color EntryColor = new Color(0.16f, 0.5f, 0.2f);
        private static readonly Color AnyStateColor = new Color(0.15f, 0.45f, 0.55f);

        public Kind NodeKind { get; }

        public SpecialNodeView(Kind kind)
        {
            NodeKind = kind;
            title = kind == Kind.Entry ? "Entry" : "Any State";

            // There is always exactly one of each, so they can never be deleted.
            capabilities &= ~Capabilities.Deletable;

            titleButtonContainer.style.display = DisplayStyle.None;
            style.minWidth = 120;

            // Hide the empty port area under the title, same as StateNodeView.
            VisualElement contents = mainContainer.Q("contents");
            if (contents != null)
            {
                contents.style.display = DisplayStyle.None;
            }
            titleContainer.style.backgroundColor =
                kind == Kind.Entry ? EntryColor : AnyStateColor;
        }

        // The graph builds the whole menu (StateMachineGraphView.BuildContextualMenu).
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
        }
    }
}

#endif
