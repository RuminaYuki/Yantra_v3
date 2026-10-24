#if UNITY_EDITOR

using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Stand-in that lets Unity's Inspector show "one of the Base's states, as this variant table runs it",
    // like TransitionSelection does for an arrow. Selecting the StateSO itself would edit the Base's state,
    // which every table using it shares. Lives in memory only (DontSave): the changes are in the table.
    public class StateOverrideSelection : ScriptableObject
    {
        [SerializeField] private TransitionTableSO _table;
        [SerializeField] private TransitionTableSO _baseTable;
        [SerializeField] private StateSO _state;

        public TransitionTableSO Table => _table;
        public TransitionTableSO BaseTable => _baseTable;
        public StateSO State => _state;

        public static StateOverrideSelection Create(TransitionTableSO table, TransitionTableSO baseTable, StateSO state)
        {
            var selection = CreateInstance<StateOverrideSelection>();
            selection.hideFlags = HideFlags.DontSave;
            selection.name = state.name;   // Inspector title

            selection._table = table;
            selection._baseTable = baseTable;
            selection._state = state;
            return selection;
        }
    }
}

#endif
