#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // The SubStates group. In a table: each sub-state machine, folding open to its Actions, Exit
    // Conditions and Transition Table (like a Yantra_v3 "KrasueAttack_State" folder). Inside a
    // sub-state machine's own table: its State Exit section, the exit states and their exit actions.
    // Both [+] buttons make one exit on both sides, with the same exit id (see EmbeddedAssets.CreateExit).
    public partial class ContentsPanel
    {
        // exitsSection: State Exit, when this table is a sub-state machine's own table; otherwise null.
        private void AddSubStatesGroup(
            Dictionary<StateSO, TableContents.SubStateParts> subStates,
            HashSet<ScriptableObject> used,
            string tablePath,
            VisualElement exitsSection)
        {
            Foldout group = CreateFoldout("SubStates", "SubStates");
            MakeGroup(group, subStates.Count);
            group.Q<Toggle>().AddManipulator(new ContextualMenuManipulator(evt =>
                evt.menu.AppendAction("Create Sub State Machine", _ => CreateSubStateMachine(""))));

            if (subStates.Count == 0 && exitsSection == null)
            {
                group.Add(CreateEmptyHint("No sub-state machines yet. Right-click SubStates to create one."));
            }

            // A heading only when the group holds both kinds.
            if (exitsSection != null && subStates.Count > 0)
            {
                VisualElement heading = CreateHeading("Sub State Machine");
                heading.style.paddingLeft = 12;   // lines up with "State Exit" after its arrow
                group.Add(heading);
            }

            foreach (KeyValuePair<StateSO, TableContents.SubStateParts> subState in subStates.OrderBy(pair => pair.Key.name))
            {
                group.Add(CreateSubStateView(subState.Key, subState.Value, used, tablePath));
            }

            if (exitsSection != null)
            {
                group.Add(exitsSection);
            }

            _list.Add(group);
        }

        // The sub-state row with a ▶ / ▼ in front, and under it its Actions, Exit Conditions [+] and
        // Transition Table. Double-clicking the row or the table goes inside, like in the graph.
        private VisualElement CreateSubStateView(
            StateSO state,
            TableContents.SubStateParts parts,
            HashSet<ScriptableObject> used,
            string tablePath)
        {
            void Open() => _onOpenSubStateMachine?.Invoke(state, parts.Table);

            var view = new VisualElement();
            VisualElement row = CreateRow(state, null, null, used.Contains(state), tablePath, null, Open);
            view.Add(row);

            var children = new VisualElement();
            view.Add(children);
            AddFoldArrow(row, children, $"Sub/{AssetDatabase.GetAssetPath(state)}/{state.name}");

            AddSubStatePart(children, CreateHeading("Actions"), parts.Actions, used, tablePath, null);
            AddSubStatePart(
                children,
                CreateHeading("Exit Conditions", "New exit", heading => AddExitCondition(heading, state, parts.Table)),
                parts.ExitConditions,
                used,
                tablePath,
                null,
                showWhenEmpty: true);
            AddSubStatePart(children, CreateHeading("Transition Table"), new List<ScriptableObject> { parts.Table },
                null, tablePath, Open);

            return view;
        }

        // A heading and its rows. Rows here belong to the sub-state machine, so they don't go into folders.
        // used null: every row counts as used (the child table).
        private void AddSubStatePart(
            VisualElement parent,
            VisualElement heading,
            List<ScriptableObject> items,
            HashSet<ScriptableObject> used,
            string tablePath,
            System.Action onDoubleClick,
            bool showWhenEmpty = false)
        {
            if (items.Count == 0 && !showWhenEmpty)
            {
                return;
            }

            parent.Add(heading);

            foreach (ScriptableObject item in items.OrderBy(item => item.name))
            {
                bool isUsed = used == null || used.Contains(item);
                parent.Add(CreateRow(item, null, null, isUsed, tablePath, null, onDoubleClick));
            }
        }

        // State Exit [+], with each exit state and, under it, its exit actions. Both fold.
        private VisualElement CreateExitsSection(
            Dictionary<StateSO, List<ScriptableObject>> exits,
            TransitionTableSO parentTable,
            StateSO parentState,
            HashSet<ScriptableObject> used,
            string tablePath)
        {
            var section = new VisualElement();
            section.style.marginBottom = 4;

            string tooltip = parentTable != null
                ? $"New exit: an exit state here and the matching condition in {parentTable.name}."
                : "New exit state (the parent table isn't in this file, so make its condition there).";
            VisualElement heading = CreateHeading("State Exit", tooltip, anchor => AddExit(anchor, parentTable, parentState));
            section.Add(heading);

            var exitList = new VisualElement();
            section.Add(exitList);
            AddFoldArrow(heading, exitList, $"StateExit/{AssetDatabase.GetAssetPath(_table)}/{_table.name}");

            foreach (KeyValuePair<StateSO, List<ScriptableObject>> exit in exits.OrderBy(pair => pair.Key.name))
            {
                VisualElement row = CreateRow(exit.Key, null, null, used.Contains(exit.Key), tablePath, null);
                exitList.Add(row);

                var actions = new VisualElement();
                foreach (ScriptableObject action in exit.Value.OrderBy(item => item.name))
                {
                    actions.Add(CreateRow(action, null, null, used.Contains(action), tablePath, null));
                }

                exitList.Add(actions);
                AddFoldArrow(row, actions, $"Exit/{AssetDatabase.GetAssetPath(exit.Key)}/{exit.Key.name}");
            }

            return section;
        }

        // [+] next to Exit Conditions (in the parent table): the exit condition here, and the matching
        // exit state in the child table when it's in this file. A shared child table is left alone,
        // since other tables use it too.
        private void AddExitCondition(VisualElement anchor, StateSO state, TransitionTableSO childTable)
        {
            ShowNamePopup(anchor, "New Exit (exit id)", "ToPatrol", exitId =>
            {
                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();

                string prefix = EmbeddedAssets.StatePrefix(state.name);
                if (TableContents.IsInSameFile(childTable, _table))
                {
                    EmbeddedAssets.CreateExit(_table, childTable, prefix, exitId.Trim());
                }
                else
                {
                    EmbeddedAssets.CreateExitCondition(_table, prefix, exitId.Trim());
                }

                Undo.CollapseUndoOperations(undoGroup);
                StateMachineGraphWindow.ReloadAll();
                Rebuild();
            });
        }

        // [+] next to State Exit (in the child table): the exit state here, and the matching condition in
        // the parent table when it's in this file.
        private void AddExit(VisualElement anchor, TransitionTableSO parentTable, StateSO parentState)
        {
            // Names follow the sub-state ("Attack_State" -> "Attack..."), or this table's own name.
            string prefix = parentState != null
                ? EmbeddedAssets.StatePrefix(parentState.name)
                : EmbeddedAssets.TablePrefix(_table.name);

            ShowNamePopup(anchor, "New Exit (exit id)", "ToPatrol", exitId =>
            {
                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();

                StateSO exitState = EmbeddedAssets.CreateExit(parentTable, _table, prefix, exitId.Trim());

                Undo.CollapseUndoOperations(undoGroup);
                StateMachineGraphWindow.ReloadAll();
                Rebuild();
                Selection.activeObject = exitState;
            });
        }
    }
}

#endif
