#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Left panel of the graph window: what the open table is made of, like a Project window for it.
    //   States      plain states (with folders)
    //   SubStates   sub-state machines, each folding open to its Actions, Exit Conditions and Transition
    //               Table; inside a sub-state machine's own table, its State Exit section too
    //   Actions     (with folders)
    //   Conditions  (with folders)
    // Marks: ● inside this table's file (a sub-asset), ◆ a separate shared file, ○ inside the file but
    // nothing uses it. Clicking a row shows it in Unity's Inspector; the right-click menus create,
    // rename, share, copy and delete. Typing in the search field shows a flat list of matches instead.
    // Looks are in ContentsPanel.uss. The code is split by job:
    //   ContentsPanel.cs            layout, rows, selection, search
    //   ContentsPanel.Folders.cs    folders and drag and drop
    //   ContentsPanel.SubStates.cs  the SubStates group and State Exit
    //   ContentsPanel.Editing.cs    create, rename, share / copy, delete, menus
    public partial class ContentsPanel : VisualElement
    {
        private const string StyleSheetName = "ContentsPanel";

        private const string StatesGroup = "States";
        private const string ActionsGroup = "Actions";
        private const string ConditionsGroup = "Conditions";

        private const string EmbeddedMark = "●";
        private const string SharedMark = "◆";
        private const string UnusedMark = "○";

        // USS classes (see ContentsPanel.uss).
        private const string GroupClass = "contents-group";
        private const string RowClass = "contents-row";
        private const string SelectedRowClass = "contents-row--selected";
        private const string ChildrenClass = "contents-children";

        private readonly EditorWindow _window;
        private readonly Action<Object> _onItemClicked;
        private readonly Action<StateSO> _onStateCreated;
        private readonly Action<StateSO, TransitionTableSO> _onOpenSubStateMachine;
        private readonly ScrollView _list;

        // Each item's row, for the selection highlight and so a new item can go straight into rename.
        private readonly Dictionary<Object, (VisualElement Row, Label Label)> _rows =
            new Dictionary<Object, (VisualElement Row, Label Label)>();

        // Groups, folders and fold arrows the user closed, so a rebuild keeps them closed.
        private readonly HashSet<string> _collapsed = new HashSet<string>();

        private TransitionTableSO _table;

        // What's typed in the search field; empty shows the normal groups.
        private string _search = "";

        // onItemClicked: a state selects its node in the graph, anything else goes to the Inspector.
        // onStateCreated: a state made here has to be put on the graph by the window.
        // onOpenSubStateMachine: double-clicking a sub-state machine goes into its table, like in the graph.
        public ContentsPanel(
            EditorWindow window,
            Action<Object> onItemClicked,
            Action<StateSO> onStateCreated,
            Action<StateSO, TransitionTableSO> onOpenSubStateMachine)
        {
            _window = window;
            _onItemClicked = onItemClicked;
            _onStateCreated = onStateCreated;
            _onOpenSubStateMachine = onOpenSubStateMachine;

            GraphStyleSheets.AddTo(this, StyleSheetName);
            AddToClassList("contents");

            var header = new VisualElement();
            header.AddToClassList("contents__header");
            Add(header);

            var title = new Label("Contents");
            title.AddToClassList("contents__title");
            header.Add(title);

            var search = new ToolbarSearchField { tooltip = "Find by name" };
            search.AddToClassList("contents__search");
            search.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue?.Trim() ?? "";
                Rebuild();
            });
            header.Add(search);

            _list = new ScrollView();
            _list.style.flexGrow = 1;
            Add(_list);

            // Rebuild after any edit (e.g. an action added to a state in the Inspector), and highlight
            // whatever is selected wherever it was selected (here, the graph, the Project window).
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ObjectChangeEvents.changesPublished += OnChangesPublished;
                Selection.selectionChanged += ShowSelection;
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                ObjectChangeEvents.changesPublished -= OnChangesPublished;
                Selection.selectionChanged -= ShowSelection;
            });

            RegisterDragEndCallbacks();
        }

        public void SetTable(TransitionTableSO table)
        {
            _table = table;
            Rebuild();
        }

        public void Rebuild()
        {
            _list.Clear();
            _rows.Clear();
            ResetDropHighlight();
            ResetRenameState();

            if (_table == null)
            {
                _list.Add(CreateEmptyHint("No Transition Table."));
                return;
            }

            // What the table uses, plus (in the file's main table) anything in the file nothing uses yet.
            var used = new HashSet<ScriptableObject>(TableContents.CollectUsed(_table));
            List<ScriptableObject> unused = AssetDatabase.IsMainAsset(_table)
                ? TableContents.UnusedInFile(_table)
                : new List<ScriptableObject>();
            List<ScriptableObject> shown = used.Union(unused).ToList();
            string tablePath = AssetDatabase.GetAssetPath(_table);

            Dictionary<StateSO, TableContents.SubStateParts> subStates = TableContents.FindSubStateParts(_table, used);

            if (_search.Length > 0)
            {
                AddSearchResults(shown.Concat(subStates.Values.Select(parts => parts.Table)), used, subStates, tablePath);
                ShowSelection();
                return;
            }

            // A sub-state machine's own pieces show under it, not in the Actions / Conditions groups.
            var shownElsewhere = new HashSet<ScriptableObject>(
                subStates.Values.SelectMany(parts => parts.Actions.Concat(parts.ExitConditions)));

            // Inside a sub-state machine's own table: its exit states and their exit actions (SubStates group).
            Dictionary<StateSO, List<ScriptableObject>> exits = TableContents.FindExitStates(_table, used);
            (TransitionTableSO parentTable, StateSO parentState) = TableContents.FindParentInFile(_table);
            VisualElement exitsSection = null;
            if (parentTable != null || exits.Count > 0)
            {
                exitsSection = CreateExitsSection(exits, parentTable, parentState, used, tablePath);
                shownElsewhere.UnionWith(exits.Keys);
                shownElsewhere.UnionWith(exits.Values.SelectMany(actions => actions));
            }

            FolderData folders = FolderData.Load(_table);
            AddGroup(StatesGroup,
                shown.OfType<StateSO>().Where(item => !shownElsewhere.Contains(item) && !subStates.ContainsKey(item)),
                used, folders, tablePath);
            AddSubStatesGroup(subStates, used, tablePath, exitsSection);
            AddGroup(ActionsGroup,
                shown.OfType<StateActionSO>().Where(item => !shownElsewhere.Contains(item)),
                used, folders, tablePath);
            AddGroup(ConditionsGroup,
                shown.OfType<StateConditionSO>().Where(item => !shownElsewhere.Contains(item)),
                used, folders, tablePath);
            AddUnusedTablesGroup(unused.OfType<TransitionTableSO>().ToList(), tablePath);

            ShowSelection();
        }

        private void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            // Rebuilding while a name is being typed would throw the text field away.
            if (IsRenaming)
            {
                IsRebuildPending = true;
                return;
            }

            Rebuild();
        }

        private static string GroupOf(Object item)
        {
            switch (item)
            {
                case StateSO _: return StatesGroup;
                case StateActionSO _: return ActionsGroup;
                case StateConditionSO _: return ConditionsGroup;
                default: return null;
            }
        }

        // ---------- Search ----------

        // Every item whose name contains the search text, in one list like the Project window's search,
        // each with what kind it is. Double-clicking a sub-state machine still goes inside.
        private void AddSearchResults(
            IEnumerable<ScriptableObject> items,
            HashSet<ScriptableObject> used,
            Dictionary<StateSO, TableContents.SubStateParts> subStates,
            string tablePath)
        {
            List<ScriptableObject> matches = items
                .Where(item => item != null && item.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct()
                .OrderBy(item => item.name)
                .ToList();

            if (matches.Count == 0)
            {
                _list.Add(CreateEmptyHint($"Nothing named \"{_search}\"."));
                return;
            }

            foreach (ScriptableObject item in matches)
            {
                Action open = item is StateSO state && subStates.TryGetValue(state, out TableContents.SubStateParts parts)
                    ? () => _onOpenSubStateMachine?.Invoke(state, parts.Table)
                    : (Action)null;

                bool isUsed = used.Contains(item) || item is TransitionTableSO;
                _list.Add(CreateRow(item, null, null, isUsed, tablePath, null, open, KindOf(item, subStates)));
            }
        }

        private static string KindOf(Object item, Dictionary<StateSO, TableContents.SubStateParts> subStates)
        {
            switch (item)
            {
                case StateSO state: return subStates.ContainsKey(state) ? "Sub State" : "State";
                case StateActionSO _: return "Action";
                case StateConditionSO _: return "Condition";
                case TransitionTableSO _: return "Table";
                default: return null;
            }
        }

        // ---------- Selection highlight ----------

        private void ShowSelection()
        {
            VisualElement firstSelected = null;

            foreach (KeyValuePair<Object, (VisualElement Row, Label Label)> pair in _rows)
            {
                bool isSelected = IsSelected(pair.Key);
                pair.Value.Row.EnableInClassList(SelectedRowClass, isSelected);

                if (isSelected && firstSelected == null)
                {
                    firstSelected = pair.Value.Row;
                }
            }

            // Selected somewhere else: bring its row into view (only if its folders are open).
            if (firstSelected != null && firstSelected.resolvedStyle.display != DisplayStyle.None)
            {
                _list.ScrollTo(firstSelected);
            }
        }

        private static bool IsSelected(Object item) => Array.IndexOf(Selection.objects, item) >= 0;

        // ---------- Groups ----------

        // States, Actions or Conditions: folders first, then the items not in a folder.
        private void AddGroup(
            string group,
            IEnumerable<ScriptableObject> items,
            HashSet<ScriptableObject> used,
            FolderData folders,
            string tablePath)
        {
            List<ScriptableObject> sorted = items.OrderBy(item => item.name).ToList();
            List<string> groupFolders = folders.FoldersIn(group, sorted);

            Foldout groupView = CreateFolderFoldout(group, group, "");
            MakeGroup(groupView, sorted.Count);

            if (sorted.Count == 0 && groupFolders.Count == 0)
            {
                groupView.Add(CreateEmptyHint($"No {group.ToLowerInvariant()} yet. Right-click {group} to create one."));
            }

            var folderViews = new Dictionary<string, Foldout>();
            foreach (string path in groupFolders)
            {
                GetOrCreateFolder(groupView, group, path, folderViews);
            }

            foreach (ScriptableObject item in sorted)
            {
                folders.ItemFolders.TryGetValue(item, out string folder);
                VisualElement parent = folder != null ? folderViews[folder] : groupView;
                parent.Add(CreateRow(item, group, folder, used.Contains(item), tablePath, groupFolders));
            }

            _list.Add(groupView);
        }

        // Sub-state machine tables left in the file that nothing runs (e.g. their sub-state was deleted).
        private void AddUnusedTablesGroup(List<TransitionTableSO> unusedTables, string tablePath)
        {
            if (unusedTables.Count == 0)
            {
                return;
            }

            Foldout group = CreateFoldout("Unused Tables", "UnusedTables");
            MakeGroup(group, unusedTables.Count);
            group.Q<Toggle>().AddManipulator(new ContextualMenuManipulator(evt =>
                evt.menu.AppendAction("Delete All Unused (○)...", _ => DeleteAllUnused())));

            foreach (TransitionTableSO unusedTable in unusedTables.OrderBy(item => item.name))
            {
                group.Add(CreateRow(unusedTable, null, null, false, tablePath, null));
            }

            _list.Add(group);
        }

        // A top-level group: a header band with the item count on the right.
        private static void MakeGroup(Foldout foldout, int count)
        {
            foldout.AddToClassList(GroupClass);

            var countLabel = new Label(count.ToString());
            countLabel.AddToClassList("contents-group__count");
            foldout.Q(className: Foldout.inputUssClassName)?.Add(countLabel);
        }

        // A Foldout whose open / closed state survives a rebuild.
        private Foldout CreateFoldout(string text, string collapseKey)
        {
            var foldout = new Foldout
            {
                text = text,
                value = !_collapsed.Contains(collapseKey)
            };

            foldout.RegisterValueChangedCallback(evt =>
            {
                // Only the foldout's own toggle, not change events bubbling up from inside it.
                if (evt.target == foldout)
                {
                    SetCollapsed(collapseKey, !evt.newValue);
                }
            });

            return foldout;
        }

        // A ▶ / ▼ at the start of `row` that shows or hides `children`, remembered like a Foldout.
        // Clicking the arrow doesn't count as clicking the row.
        private void AddFoldArrow(VisualElement row, VisualElement children, string collapseKey)
        {
            bool isOpen = !_collapsed.Contains(collapseKey);
            children.AddToClassList(ChildrenClass);
            children.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;

            var arrow = new Label(isOpen ? "▼" : "▶");
            arrow.AddToClassList("contents-fold-arrow");
            arrow.RegisterCallback<ClickEvent>(evt =>
            {
                isOpen = !isOpen;
                children.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;
                arrow.text = isOpen ? "▼" : "▶";
                SetCollapsed(collapseKey, !isOpen);
                evt.StopPropagation();
            });

            row.Insert(0, arrow);
        }

        private void SetCollapsed(string collapseKey, bool isCollapsed)
        {
            if (isCollapsed)
            {
                _collapsed.Add(collapseKey);
            }
            else
            {
                _collapsed.Remove(collapseKey);
            }
        }

        // A small grey title inside a group ("ACTIONS", "EXIT CONDITIONS", "STATE EXIT", ...), with a
        // [+] after it when onAdd is given (onAdd gets the heading, to place a popup under it).
        private static VisualElement CreateHeading(string text, string addTooltip = null, Action<VisualElement> onAdd = null)
        {
            var heading = new VisualElement();
            heading.AddToClassList("contents-heading");

            var label = new Label(text.ToUpperInvariant());
            label.AddToClassList("contents-heading__label");
            heading.Add(label);

            if (onAdd != null)
            {
                var addButton = new Button(() => onAdd(heading)) { text = "+", tooltip = addTooltip };
                addButton.AddToClassList("contents-add-button");
                heading.Add(addButton);
            }

            return heading;
        }

        private static Label CreateEmptyHint(string text)
        {
            var hint = new Label(text);
            hint.AddToClassList("contents-empty");
            return hint;
        }

        // ---------- Rows ----------

        // [mark] [icon] [name] [hint]. Renaming swaps the name label for a text field.
        // group / groupFolders null: the row can't go into folders (e.g. it belongs to a sub-state machine).
        // onDoubleClick null: double-click renames. hint: small grey text after the name (search results).
        private VisualElement CreateRow(
            ScriptableObject item,
            string group,
            string folder,
            bool isUsed,
            string tablePath,
            List<string> groupFolders,
            Action onDoubleClick = null,
            string hint = null)
        {
            // A sub-asset has the same asset path as the file it lives in.
            string itemPath = AssetDatabase.GetAssetPath(item);
            bool isEmbedded = itemPath == tablePath;

            TransitionTableSO subTable = item is StateSO state
                ? StateNodeView.FindSubTables(state).FirstOrDefault()
                : null;

            var row = new VisualElement();
            row.AddToClassList(RowClass);
            row.EnableInClassList("contents-row--shared", isUsed && !isEmbedded);
            row.EnableInClassList("contents-row--unused", !isUsed);
            row.focusable = true;   // so F2 and Delete reach it after a click
            row.tooltip =
                !isUsed ? "Inside this table's file, but nothing uses it yet." :
                isEmbedded ? "Inside this table's file." :
                $"Shared file: {itemPath}";

            var mark = new Label(!isUsed ? UnusedMark : isEmbedded ? EmbeddedMark : SharedMark);
            mark.AddToClassList("contents-row__mark");
            row.Add(mark);

            // A sub-state machine gets Animator's sub-state machine icon; anything else its script's icon.
            Texture icon = subTable != null
                ? EditorGUIUtility.FindTexture("AnimatorStateMachine Icon")
                : AssetPreview.GetMiniThumbnail(item);
            if (icon != null)
            {
                var image = new Image { image = icon };
                image.AddToClassList("contents-row__icon");
                row.Add(image);
            }

            if (subTable != null)
            {
                row.tooltip += $"\nSub-state machine ({subTable.name}): double-click to open it.";
            }

            var label = new Label(string.IsNullOrEmpty(item.name) ? "(no name)" : item.name);
            label.AddToClassList("contents-row__name");
            row.Add(label);

            if (hint != null)
            {
                var hintLabel = new Label(hint);
                hintLabel.AddToClassList("contents-row__hint");
                row.Add(hintLabel);
            }

            _rows[item] = (row, label);

            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount == 2 && onDoubleClick != null)
                {
                    onDoubleClick();
                }
                else if (evt.clickCount == 2)
                {
                    BeginRename(row, label, item);
                }
                else
                {
                    _onItemClicked?.Invoke(item);
                }
            });

            RegisterRowKeys(row, label, item, isEmbedded);
            row.AddManipulator(new ContextualMenuManipulator(evt =>
                BuildRowMenu(evt, row, label, item, isEmbedded, group, folder, groupFolders)));
            RegisterDragSource(row, new DragPayload { Group = group, Item = item });

            return row;
        }

        // F2 renames. Delete, like the Project window: Delete... for something in this file, Remove from
        // Table... for a shared file. Unity sends Delete as a "SoftDelete" / "Delete" command.
        private void RegisterRowKeys(VisualElement row, Label label, ScriptableObject item, bool isEmbedded)
        {
            row.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.F2)
                {
                    BeginRename(row, label, item);
                    evt.StopPropagation();
                }
            });

            row.RegisterCallback<ValidateCommandEvent>(evt =>
            {
                if (IsDeleteCommand(evt.commandName))
                {
                    evt.StopPropagation();
                }
            });

            row.RegisterCallback<ExecuteCommandEvent>(evt =>
            {
                if (!IsDeleteCommand(evt.commandName))
                {
                    return;
                }

                evt.StopPropagation();

                // Running the dialog while the command is still being sent around isn't safe.
                schedule.Execute(() =>
                {
                    if (isEmbedded)
                    {
                        DeleteItem(item);
                    }
                    else if (item is not TransitionTableSO)
                    {
                        RemoveFromTable(item);
                    }
                });
            });
        }

        private static bool IsDeleteCommand(string commandName)
        {
            return commandName == "SoftDelete" || commandName == "Delete";
        }
    }
}

#endif
