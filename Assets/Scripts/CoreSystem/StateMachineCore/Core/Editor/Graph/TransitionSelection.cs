#if UNITY_EDITOR

using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Stand-in that lets Unity's Inspector show a graph arrow or the Any State node. A transition is
    // a struct inside the table, not an Object, so it can't be selected itself; this points at it instead.
    // Lives in memory only (DontSave): the transition data stays in the table.
    public class TransitionSelection : ScriptableObject
    {
        public enum Kind { Transition, AnyStateTransition, Entry, AnyStateNode }

        [SerializeField] private TransitionTableSO _table;
        [SerializeField] private Kind _kind;
        [SerializeField] private StateSO _from;
        [SerializeField] private StateSO _to;

        public TransitionTableSO Table => _table;
        public Kind EdgeKind => _kind;
        public StateSO From => _from;   // null for Any State and Entry
        public StateSO To => _to;

        public static TransitionSelection Create(TransitionTableSO table, TransitionEdgeView edge)
        {
            var selection = CreateInstance<TransitionSelection>();
            selection.hideFlags = HideFlags.DontSave;
            selection.name = $"{edge.From.title}  →  {edge.To.title}";   // Inspector title

            selection._table = table;
            selection._from = (edge.From as StateNodeView)?.State;
            selection._to = (edge.To as StateNodeView)?.State;
            selection._kind =
                edge.From is SpecialNodeView { NodeKind: SpecialNodeView.Kind.Entry } ? Kind.Entry :
                edge.IsAnyState ? Kind.AnyStateTransition :
                Kind.Transition;

            return selection;
        }

        // The Any State node itself: the Inspector lists all Any State transitions.
        public static TransitionSelection CreateForAnyState(TransitionTableSO table)
        {
            var selection = CreateInstance<TransitionSelection>();
            selection.hideFlags = HideFlags.DontSave;
            selection.name = "Any State";
            selection._table = table;
            selection._kind = Kind.AnyStateNode;
            return selection;
        }
    }
}

#endif
