#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Stand-in that lets Unity's Inspector show one of the Base's arrows in a variant table's graph:
    // the Base's transitions behind it (by Id), and what this table does with them. Like
    // TransitionSelection for the table's own arrows. Lives in memory only (DontSave).
    public class TransitionOverrideSelection : ScriptableObject
    {
        [SerializeField] private TransitionTableSO _table;
        [SerializeField] private TransitionTableSO _baseTable;
        [SerializeField] private bool _isAnyState;
        [SerializeField] private List<string> _transitionIds = new List<string>();

        public TransitionTableSO Table => _table;
        public TransitionTableSO BaseTable => _baseTable;
        public bool IsAnyState => _isAnyState;
        public IReadOnlyList<string> TransitionIds => _transitionIds;

        public static TransitionOverrideSelection Create(
            TransitionTableSO table,
            TransitionTableSO baseTable,
            TransitionEdgeView edge)
        {
            var selection = CreateInstance<TransitionOverrideSelection>();
            selection.hideFlags = HideFlags.DontSave;
            selection.name = $"{edge.From.title}  →  {edge.To.title}";   // Inspector title

            selection._table = table;
            selection._baseTable = baseTable;
            selection._isAnyState = edge.IsAnyState;
            selection._transitionIds = edge.InheritedTransitions.Select(item => item.Id).ToList();
            return selection;
        }
    }
}

#endif
