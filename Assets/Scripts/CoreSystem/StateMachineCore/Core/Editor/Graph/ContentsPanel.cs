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
    // nothing uses it. Clicking a row shows it in Unity's Inspector (Ctrl / Shift-click select several,
    // and dragging a selected row drags them all); the right-click menus create,
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

        // Which group (States, Actions, Conditions) each row is in; rows outside those groups aren't here.
        // Shift-click ranges and multi-item drags stay inside one group.
        private readonly Dictionary<Object, string> _rowGroups = new Dictionary<Object, string>();

        // The last row clicked without Shift: where a Shift-click range starts.
        private Object _selectionAnchor;

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
                Selection.selectionChanged += RevealSelection;
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                ObjectChangeEvents.changesPublished -= OnChangesPublished;
                Selection.selectionChanged -= RevealSelection;
            });

            RegisterDragEndCallbacks();
            RegisterClipboardCommands();
        }

        public void SetTable(TransitionTableSO table)
        {
            _table = table;
            LoadCollapsed();
            Rebuild();
        }

        public void Rebuild()
        {
            _list.Clear();
            _rows.Clear();
            _rowGroups.Clear();
            _parentRows.Clear();
            ResetDropHighlight();
            ResetRenameState();

            if (_table == null)
            {
                _list.Add(CreateEmptyHint("No Transition Table."));
                return;
            }

            // What the table uses, plus (in the file's main table) anything in the file nothing uses yet.
            var referenced = new List<StateSO>();
            var used = new HashSet<ScriptableObject>(TableContents.CollectUsed(_table, referenced));
            referenced.RemoveAll(state => used.Contains(state));
            List<ScriptableObject> unused = AssetDatabase.IsMainAsset(_table)
                ? TableContents.UnusedInFile(_table)
                : new List<ScriptableObject>();
            List<ScriptableObject> shown = used.Union(unused).ToList();
            string tablePath = AssetDatabase.GetAssetPath(_table);

            // The file's main table also lists the actions and conditions only its sub-state machines use
            // (the Sub group, one part per sub-state machine table), so everything in the file is here.
            Dictionary<ScriptableObject, List<TransitionTableSO>> inSubTables = AssetDatabase.IsMainAsset(_table)
                ? UsedOnlyInSubTables(_table, used, unused)
                : new Dictionary<ScriptableObject, List<TransitionTableSO>>();
            var listed = new HashSet<ScriptableObject>(used);
            listed.UnionWith(inSubTables.Keys);

            Dictionary<StateSO, TableContents.SubStateParts> subStates = TableContents.FindSubStateParts(_table, used);

            // Inside a sub-state machine's table in its parent's file: the parent's actions and conditions.
            (TransitionTableSO parentTable, StateSO parentState) = TableContents.FindParentInFile(_table);
            List<ScriptableObject> parentItems = parentTable != null
                ? ParentItems(parentTable, used)
                : new List<ScriptableObject>();

            if (_search.Length > 0)
            {
                AddSearchResults(
                    shown.Concat(inSubTables.Keys).Concat(subStates.Values.Select(parts => parts.Table)),
                    listed, subStates, tablePath, parentItems);
                ShowSelection();
                return;
            }

            // A sub-state machine's own pieces show under it, not in the Actions / Conditions groups.
            var shownElsewhere = new HashSet<ScriptableObject>(
                subStates.Values.SelectMany(parts => parts.Actions.Concat(parts.ExitConditions)));

            // Inside a sub-state machine's own table: its exit states and their exit actions (SubStates group).
            Dictionary<StateSO, List<ScriptableObject>> exits = TableContents.FindExitStates(_table, used);
            VisualElement exitsSection = null;
            if (parentTable != null || exits.Count > 0)
            {
                exitsSection = CreateExitsSection(exits, parentTable, parentState, used, tablePath);
                shownElsewhere.UnionWith(exits.Keys);
                shownElsewhere.UnionWith(exits.Values.SelectMany(actions => actions));
            }

            // A variant table: what its Base uses goes in the From Base group instead (ContentsPanel.Base.cs).
            TransitionTableSO baseTable = StateMachineGraphView.SafeBaseOf(_table);
            HashSet<ScriptableObject> baseItems = baseTable != null
                ? BaseItems(baseTable)
                : new HashSet<ScriptableObject>();
            shownElsewhere.UnionWith(baseItems);

            FolderData folders = FolderData.Load(_table);
            AddGroup(StatesGroup,
                shown.OfType<StateSO>().Where(item => !shownElsewhere.Contains(item) && !subStates.ContainsKey(item)),
                used, folders, tablePath, referenced);
            AddSubStatesGroup(subStates, used, tablePath, exitsSection, inSubTables);
            AddGroup(ActionsGroup,
                shown.OfType<StateActionSO>().Where(item => !shownElsewhere.Contains(item)),
                used, folders, tablePath);
            AddGroup(ConditionsGroup,
                shown.OfType<StateConditionSO>().Where(item => !shownElsewhere.Contains(item)),
                used, folders, tablePath);
            AddBaseGroup(baseTable, baseItems, subStates, tablePath);
            AddUnusedTablesGroup(unused.OfType<TransitionTableSO>().ToList(), tablePath);
            AddParentGroup(parentTable, parentItems, tablePath);

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

        // Item -> the sub-state machine tables using it: actions and conditions in the file that only
        // those tables use (not `used` by this table, not left unused). Their wiring (sub-state / exit
        // actions, exited conditions) stays under SubStates.
        private static Dictionary<ScriptableObject, List<TransitionTableSO>> UsedOnlyInSubTables(
            TransitionTableSO table,
            HashSet<ScriptableObject> used,
            List<ScriptableObject> unused)
        {
            string tablePath = AssetDatabase.GetAssetPath(table);
            var tablesByItem = new Dictionary<ScriptableObject, List<TransitionTableSO>>();

            foreach (TransitionTableSO subTable in AssetDatabase.LoadAllAssetsAtPath(tablePath).OfType<TransitionTableSO>())
            {
                // A leftover table nothing runs: its items count as unused, not "in" it.
                if (subTable == table || unused.Contains(subTable))
                {
                    continue;
                }

                foreach (ScriptableObject item in TableContents.CollectUsed(subTable))
                {
                    if (!(item is StateActionSO || item is StateConditionSO) ||
                        used.Contains(item) ||
                        IsSubStateMachineWiring(item) ||
                        AssetDatabase.GetAssetPath(item) != tablePath)
                    {
                        continue;
                    }

                    if (!tablesByItem.TryGetValue(item, out List<TransitionTableSO> tables))
                    {
                        tables = new List<TransitionTableSO>();
                        tablesByItem[item] = tables;
                    }

                    if (!tables.Contains(subTable))
                    {
                        tables.Add(subTable);
                    }
                }
            }

            return tablesByItem;
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
        // parentItems: From Parent's items, found too and marked "Parent".
        private void AddSearchResults(
            IEnumerable<ScriptableObject> items,
            HashSet<ScriptableObject> used,
            Dictionary<StateSO, TableContents.SubStateParts> subStates,
            string tablePath,
            List<ScriptableObject> parentItems)
        {
            var fromParent = new HashSet<ScriptableObject>(parentItems);

            List<ScriptableObject> matches = items
                .Concat(fromParent)
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
                if (fromParent.Contains(item))
                {
                    _list.Add(CreateRow(item, null, null, true, tablePath, null,
                        hint: $"Parent {KindOf(item, subStates)}", isFromParent: true));
                    continue;
                }

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

        // Selected anywhere (here, the graph, the Inspector, the Project window): highlight it, and open
        // the groups, folders and fold arrows hiding its row so it can be seen. Only on a selection
        // change, not on every rebuild, so a folder closed with something selected inside stays closed.
        private void RevealSelection()
        {
            Object selectedItem = _rows.Keys.FirstOrDefault(IsSelected);
            if (selectedItem != null)
            {
                OpenAncestors(_rows[selectedItem].Row);
            }

            ShowSelection();

            // Opened containers lay out next frame; scroll once they have a size. By then the panel may
            // have been rebuilt (new rows), so look the row up again and skip it if it's gone.
            if (selectedItem != null)
            {
                schedule.Execute(() =>
                {
                    if (_rows.TryGetValue(selectedItem, out (VisualElement Row, Label Label) entry) &&
                        _list.contentContainer.Contains(entry.Row))
                    {
                        _list.ScrollTo(entry.Row);
                    }
                });
            }
        }

        // Opens every closed Foldout and fold arrow between the row and the list.
        private void OpenAncestors(VisualElement row)
        {
            for (VisualElement parent = row.parent; parent != null && parent != _list; parent = parent.parent)
            {
                if (parent is Foldout foldout && !foldout.value)
                {
                    foldout.value = true;   // its callback remembers it as open
                }
                else if (parent.userData is Action openFold && parent.style.display == DisplayStyle.None)
                {
                    openFold();
                }
            }
        }

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

        // ---------- Multi-select (like the Project window) ----------

        // Ctrl / Cmd-click: adds the item to the selection, or takes it out.
        private void ToggleSelected(Object item)
        {
            List<Object> selected = SelectedRowItems().ToList();
            if (!selected.Remove(item))
            {
                selected.Add(item);
            }

            _selectionAnchor = item;
            Selection.objects = selected.ToArray();
        }

        // Shift-click: everything shown between the anchor and this item, in the same group.
        // Without an anchor in that group, just this item.
        private void SelectRange(Object item)
        {
            if (_selectionAnchor == null ||
                !_rowGroups.TryGetValue(item, out string group) ||
                !_rowGroups.TryGetValue(_selectionAnchor, out string anchorGroup) ||
                group != anchorGroup)
            {
                _selectionAnchor = item;
                Selection.objects = new[] { item };
                return;
            }

            List<Object> shown = ShownItemsInOrder()
                .Where(shownItem => _rowGroups.TryGetValue(shownItem, out string shownGroup) && shownGroup == group)
                .ToList();

            int from = shown.IndexOf(_selectionAnchor);
            int to = shown.IndexOf(item);
            if (from < 0 || to < 0)
            {
                Selection.objects = new[] { item };
                return;
            }

            // The anchor stays where it is, so the next Shift-click grows or shrinks from it.
            Selection.objects = shown
                .Skip(Math.Min(from, to))
                .Take(Math.Abs(to - from) + 1)
                .ToArray();
        }

        // Selected things that have a row here (not a graph arrow, not something from another table,
        // not a From Parent row).
        private IEnumerable<Object> SelectedRowItems()
        {
            return Selection.objects.Where(selected =>
                selected != null && _rows.ContainsKey(selected) && !_parentRows.Contains(selected));
        }

        // Rows top to bottom as drawn, skipping ones inside a closed group, folder or fold arrow.
        private IEnumerable<Object> ShownItemsInOrder()
        {
            var itemsByRow = _rows.ToDictionary(pair => pair.Value.Row, pair => pair.Key);

            foreach (VisualElement row in _list.Query(className: RowClass).ToList())
            {
                if (itemsByRow.TryGetValue(row, out Object item) && !IsInsideHidden(row))
                {
                    yield return item;
                }
            }
        }

        private bool IsInsideHidden(VisualElement element)
        {
            for (VisualElement parent = element.parent; parent != null && parent != _list; parent = parent.parent)
            {
                if (parent.resolvedStyle.display == DisplayStyle.None)
                {
                    return true;
                }
            }

            return false;
        }

        // ---------- Groups ----------

        // States, Actions or Conditions: folders first, then the items not in a folder.
        // referenced: states that an action or condition here names, but this table doesn't run;
        // they go under a Referenced heading at the end, outside folders.
        private void AddGroup(
            string group,
            IEnumerable<ScriptableObject> items,
            HashSet<ScriptableObject> used,
            FolderData folders,
            string tablePath,
            IEnumerable<StateSO> referenced = null)
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

            List<StateSO> referencedSorted = referenced?.OrderBy(item => item.name).ToList();
            if (referencedSorted != null && referencedSorted.Count > 0)
            {
                VisualElement heading = CreateHeading("Referenced");
                heading.tooltip = "Named by an action or condition here, but not a state of this table.";
                heading.style.paddingLeft = 12;   // lines up with the rows, like "Sub State Machine"
                groupView.Add(heading);

                foreach (StateSO item in referencedSorted)
                {
                    groupView.Add(CreateRow(item, null, null, true, tablePath, null));
                }
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
        // startOpen false: folded until opened by hand (remembered as "Open:<key>" in the same set).
        private void AddFoldArrow(VisualElement row, VisualElement children, string collapseKey, bool startOpen = true)
        {
            string storedKey = startOpen ? collapseKey : OpenKeyPrefix + collapseKey;
            bool isOpen = startOpen ? !_collapsed.Contains(storedKey) : _collapsed.Contains(storedKey);
            children.AddToClassList(ChildrenClass);
            children.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;

            var arrow = new Label(isOpen ? "▼" : "▶");
            arrow.AddToClassList("contents-fold-arrow");

            void SetOpen(bool open)
            {
                isOpen = open;
                children.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;
                arrow.text = isOpen ? "▼" : "▶";
                SetCollapsed(storedKey, startOpen ? !isOpen : isOpen);
            }

            arrow.RegisterCallback<ClickEvent>(evt =>
            {
                SetOpen(!isOpen);
                evt.StopPropagation();
            });

            // RevealSelection opens it through this when something inside is selected elsewhere.
            children.userData = (Action)(() => SetOpen(true));

            row.Insert(0, arrow);
        }

        private const string OpenKeyPrefix = "Open:";

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

            SaveCollapsed();
        }

        // What's folded is remembered per table in EditorPrefs, so it survives reopening the table,
        // closing the window and script reloads. A table seen for the first time has only From Parent closed
        // (and the sub-states, which start folded: AddFoldArrow startOpen).
        private const string CollapsedPrefsKey = "StateMachineGraph.Contents.Collapsed.";

        private string CollapsedPrefsKeyOf(TransitionTableSO table)
        {
            return CollapsedPrefsKey + GlobalObjectId.GetGlobalObjectIdSlow(table);
        }

        private void LoadCollapsed()
        {
            _collapsed.Clear();
            if (_table == null)
            {
                return;
            }

            string key = CollapsedPrefsKeyOf(_table);
            if (!EditorPrefs.HasKey(key))
            {
                _collapsed.Add(ParentGroupKey);
                return;
            }

            foreach (string collapseKey in EditorPrefs.GetString(key).Split('\n'))
            {
                if (collapseKey.Length > 0)
                {
                    _collapsed.Add(collapseKey);
                }
            }
        }

        private void SaveCollapsed()
        {
            if (_table != null)
            {
                EditorPrefs.SetString(CollapsedPrefsKeyOf(_table), string.Join("\n", _collapsed));
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
        // isFromParent / isFromBase: a From Parent / From Base row, which can be selected and dragged but
        // not edited from here (it's the other table's).
        private VisualElement CreateRow(
            ScriptableObject item,
            string group,
            string folder,
            bool isUsed,
            string tablePath,
            List<string> groupFolders,
            Action onDoubleClick = null,
            string hint = null,
            bool isFromParent = false,
            bool isFromBase = false)
        {
            bool isReadOnly = isFromParent || isFromBase;

            // A sub-asset has the same asset path as the file it lives in.
            string itemPath = AssetDatabase.GetAssetPath(item);
            bool isEmbedded = itemPath == tablePath;

            TransitionTableSO subTable = item is StateSO state
                ? StateNodeView.FindSubTables(state, _table).FirstOrDefault()
                : null;

            var row = new VisualElement();
            row.AddToClassList(RowClass);
            row.EnableInClassList("contents-row--shared", isUsed && !isEmbedded);
            row.EnableInClassList("contents-row--unused", !isUsed);
            row.EnableInClassList("contents-row--from-base", isFromBase);
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

            if (isFromParent)
            {
                row.tooltip += "\nFrom the parent table: drag it into a transition or an action list to use it here.";
            }

            if (isFromBase)
            {
                row.tooltip += "\nFrom the Base table: change it there, or override it for this table in the graph.";
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
            if (group != null)
            {
                _rowGroups[item] = group;
            }

            if (isReadOnly)
            {
                _parentRows.Add(item);
            }

            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount == 2 && isReadOnly)
                {
                    // Not renamed from here.
                }
                else if (evt.clickCount == 2 && onDoubleClick != null)
                {
                    onDoubleClick();
                }
                else if (evt.clickCount == 2)
                {
                    BeginRename(row, label, item);
                }
                else if (evt.actionKey)
                {
                    ToggleSelected(item);
                }
                else if (evt.shiftKey)
                {
                    SelectRange(item);
                }
                else
                {
                    _selectionAnchor = item;
                    _onItemClicked?.Invoke(item);
                }
            });

            // From Parent / From Base: no F2 / Delete and no menu, so nothing of the other table changes here.
            if (!isReadOnly)
            {
                RegisterRowKeys(row, label, item, isEmbedded);
                row.AddManipulator(new ContextualMenuManipulator(evt =>
                    BuildRowMenu(evt, row, label, item, isEmbedded, group, folder, groupFolders)));
            }

            RegisterDragSource(row, new DragPayload { Group = group, Item = item });

            return row;
        }

        // F2 renames. Delete, like the Project window: Delete... for something in this file, Remove from
        // Table... for a shared file. Unity sends Delete as a "SoftDelete" / "Delete" command.
        // With several rows selected, Delete asks once for all of them (see ContentsPanel.Clipboard.cs).
        // Copy / Paste / Duplicate / Select All are handled for the whole panel there too.
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
                    List<ScriptableObject> targets = SelectionTargetsFor(item);
                    if (targets.Count > 1)
                    {
                        DeleteItems(targets);
                    }
                    else if (isEmbedded)
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
