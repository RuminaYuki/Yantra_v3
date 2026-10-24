#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // The From Base group, in a variant table (TransitionTableSO._base): the States, Actions and
    // Conditions its Bases use, in the Base's folders and tinted, so what's the Base's and what's this
    // table's own is clear at a glance. Read-only here, like From Parent: they're changed in the Base,
    // or overridden for this table only in the graph (select a Base's state). They can still be dragged
    // into a transition or the override panel.
    public partial class ContentsPanel
    {
        private const string BaseGroupKey = "FromBase";

        // Everything the Base chain uses (nearest Base first). A Base that is its file's main table also
        // brings the actions and conditions only its sub-state machines use, like its own Contents shows.
        private static HashSet<ScriptableObject> BaseItems(TransitionTableSO baseTable)
        {
            var items = new HashSet<ScriptableObject>();
            var visited = new HashSet<TransitionTableSO>();

            for (TransitionTableSO current = baseTable;
                 current != null && visited.Add(current);
                 current = StateMachineGraphView.BaseOf(current))
            {
                var used = new HashSet<ScriptableObject>(TableContents.CollectUsed(current));
                items.UnionWith(used);

                if (AssetDatabase.IsMainAsset(current))
                {
                    items.UnionWith(UsedOnlyInSubTables(current, used, TableContents.UnusedInFile(current)).Keys);
                }
            }

            return items;
        }

        private void AddBaseGroup(
            TransitionTableSO baseTable,
            HashSet<ScriptableObject> items,
            Dictionary<StateSO, TableContents.SubStateParts> subStates,
            string tablePath)
        {
            if (baseTable == null || items.Count == 0)
            {
                return;
            }

            Foldout group = CreateFoldout($"From Base · {baseTable.name}", BaseGroupKey);
            group.AddToClassList("contents-group--base");
            MakeGroup(group, items.Count(item => item is StateSO || item is StateActionSO || item is StateConditionSO));
            group.Q<Toggle>().tooltip =
                $"What {baseTable.name} uses. Change it in the Base, or override it for this table in the " +
                "graph (select a Base's state). Drag one into a transition or the override panel to use it here.";

            FolderData folders = FolderData.Load(baseTable);
            AddParentPart(group, BaseGroupKey, StatesGroup,
                items.OfType<StateSO>().Where(state => !subStates.ContainsKey(state)), folders, tablePath, true);
            AddParentPart(group, BaseGroupKey, ActionsGroup, items.OfType<StateActionSO>(), folders, tablePath, true);
            AddParentPart(group, BaseGroupKey, ConditionsGroup, items.OfType<StateConditionSO>(), folders, tablePath, true);

            _list.Add(group);
        }
    }
}

#endif
