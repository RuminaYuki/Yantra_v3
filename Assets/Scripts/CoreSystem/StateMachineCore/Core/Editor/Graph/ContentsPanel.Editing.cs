#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // What the right-click menus do: create (inside the table's file), rename, make shared / local,
    // delete. The asset work itself is in EmbeddedAssets; this part asks, then redraws.
    public partial class ContentsPanel
    {
        // ---------- Create ----------

        // One entry of the Create Action / Create Condition menus.
        private struct CreatableType
        {
            public Type Type;
            public string MenuPath;   // "Standard/Enable Mesh Renderer"
            public string BaseName;   // "NewEnableMeshrenderer_Action"
        }

        // Every Action or Condition blueprint that can be created. The menu path and default name come
        // from the class's [CreateAssetMenu], so the list matches the Project window's Create menu.
        private static IEnumerable<CreatableType> CreatableTypes(Type baseType, string menuMarker)
        {
            var types = new List<CreatableType>();

            foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (type.IsAbstract || type.IsGenericType)
                {
                    continue;
                }

                var attribute = (CreateAssetMenuAttribute)Attribute.GetCustomAttribute(type, typeof(CreateAssetMenuAttribute));

                // "YUKI Learning State Machine/StateMachineList/Actions/Standard/Enable Mesh Renderer"
                // -> "Standard/Enable Mesh Renderer"
                string menuPath = attribute?.menuName ?? $"Other/{type.Name}";
                int marker = menuPath.IndexOf(menuMarker, StringComparison.Ordinal);
                if (marker >= 0)
                {
                    menuPath = menuPath.Substring(marker + menuMarker.Length);
                }

                string baseName = string.IsNullOrEmpty(attribute?.fileName) ? type.Name : attribute.fileName;
                types.Add(new CreatableType { Type = type, MenuPath = menuPath, BaseName = baseName });
            }

            return types.OrderBy(type => type.MenuPath);
        }

        private void CreateItem(string folderPath, Type type, string baseName)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            ScriptableObject item = EmbeddedAssets.Create(_table, type, baseName);
            ClaimForSubTable(_table, item);
            AfterCreate(item, folderPath, undoGroup);
        }

        // A state, its SubStateMachine action and its child table, already wired (see EmbeddedAssets).
        private void CreateSubStateMachine(string folderPath)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            StateSO state = EmbeddedAssets.CreateSubStateMachine(_table);
            AfterCreate(state, folderPath, undoGroup);
        }

        // Plain states, actions and conditions (this file's or shared). Sub-state machine pieces are wired
        // to each other by exit ids and child tables, so a copy of one on its own would be broken.
        private static bool CanCopy(ScriptableObject item)
        {
            if (item is TransitionTableSO ||
                item is SubStateMachineActionSO ||
                item is ExitSubStateMachineActionSO ||
                item is SubStateMachineExitedConditionSO)
            {
                return false;
            }

            return item is not StateSO state ||
                   !TableContents.ActionsOf(state).Any(action => action is SubStateMachineActionSO);
        }

        // A new State goes on the graph; a new Action / Condition is shown in the Inspector (and as ○
        // until something uses it). Then it goes in the folder, the Undo steps become one, and its name
        // is ready to type.
        private void AfterCreate(ScriptableObject item, string folderPath, int undoGroup)
        {
            if (item is StateSO state)
            {
                _onStateCreated?.Invoke(state);
            }
            else
            {
                Selection.activeObject = item;
            }

            EditFolders(folders => folders.SetItemFolder(item, folderPath));
            Undo.CollapseUndoOperations(undoGroup);

            // Wait a frame so the new row is on screen before its text field takes focus.
            schedule.Execute(() =>
            {
                if (_rows.TryGetValue(item, out (VisualElement Row, Label Label) row))
                {
                    BeginRename(row.Row, row.Label, item);
                }
            });
        }

        // ---------- Shared / local ----------

        private void MakeShared(ScriptableObject item)
        {
            // Default to the table's folder, next to the table.
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(_table))?.Replace('\\', '/');
            string path = EditorUtility.SaveFilePanelInProject(
                "Make Shared",
                item.name,
                "asset",
                $"Save {item.name} as its own file so other tables can use it.",
                folder);

            // Cancelled.
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            AfterSwap(EmbeddedAssets.MakeShared(_table, item, path));
        }

        // A shared sub-state machine brings its wiring listed under it along (its Sub State Machine
        // action, exit conditions and table), so they become this file's. Its other actions (e.g. a
        // speed setter) and what's inside the child table stay shared.
        private void MakeLocalCopy(ScriptableObject item)
        {
            var alongWith = new List<ScriptableObject>();

            var used = new HashSet<ScriptableObject>(TableContents.CollectUsed(_table));
            if (item is StateSO state &&
                TableContents.FindSubStateParts(_table, used).TryGetValue(state, out TableContents.SubStateParts parts))
            {
                alongWith.AddRange(parts.Actions.Where(action => action is SubStateMachineActionSO));
                alongWith.AddRange(parts.ExitConditions);
                alongWith.Add(parts.Table);

                // Already in this file: nothing to copy.
                alongWith.RemoveAll(part => part == null || TableContents.IsInSameFile(part, _table));
            }

            AfterSwap(EmbeddedAssets.MakeLocalCopy(_table, item, alongWith));
        }

        // The graph's nodes and the Inspector still hold the old object.
        private void AfterSwap(ScriptableObject newItem)
        {
            StateMachineGraphWindow.ReloadAll();
            Rebuild();
            Selection.activeObject = newItem;
        }

        // ---------- Delete ----------
        // Not undoable, like deleting in the Project window, so each one asks first.
        // (Deleting a state node in the graph is undoable; see StateMachineGraphView.RemoveFromTable.)

        private const string UsesWarning =
            "Anything using it is removed too: a transition from or to it, its slot in a state's action " +
            "list, or its place in a condition group.";

        // ● / ○: out of the table's file, with whatever only it used there (a sub-state machine takes its
        // action, its table and the states inside that table along).
        private void DeleteItem(ScriptableObject item)
        {
            int uses = EmbeddedAssets.CountUses(_table, item);
            string message = uses == 0
                ? $"Delete {item.name}? Nothing uses it."
                : $"Delete {item.name}? It's used in {uses} place(s) in this table. {UsesWarning}";
            message += "\n\nAnything in this file that only it used is deleted too. This can't be undone.";

            if (EditorUtility.DisplayDialog("Delete", message, "Delete", "Cancel"))
            {
                EmbeddedAssets.Delete(_table, item);
                AfterDelete();
            }
        }

        // ◆: this table stops using the shared file; the file itself stays for the other tables.
        private void RemoveFromTable(ScriptableObject item)
        {
            string message =
                $"Stop using {item.name} in this table? {UsesWarning}\n\n" +
                "The shared file isn't deleted. This can't be undone.";

            if (EditorUtility.DisplayDialog("Remove from Table", message, "Remove", "Cancel"))
            {
                EmbeddedAssets.RemoveUses(_table, item);
                AfterDelete();
            }
        }

        // Every ○ in the file, including leftover sub-state machine tables and the states inside them.
        private void DeleteAllUnused()
        {
            List<ScriptableObject> unused = TableContents.UnusedInFile(_table);
            if (unused.Count == 0)
            {
                EditorUtility.DisplayDialog("Delete Unused", "Nothing in this table's file is unused.", "OK");
                return;
            }

            string names = string.Join("\n", unused.Take(15).Select(item => "  " + item.name));
            if (unused.Count > 15)
            {
                names += $"\n  ...and {unused.Count - 15} more";
            }

            if (!EditorUtility.DisplayDialog(
                    "Delete Unused",
                    $"Delete these from the table's file? Nothing uses them.\n\n{names}\n\nThis can't be undone.",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            // Deleting one also deletes what only it used, which may be later in the list (already gone).
            foreach (ScriptableObject item in unused.Where(item => item != null))
            {
                EmbeddedAssets.Delete(_table, item);
            }

            AfterDelete();
        }

        // Graph edges remember array indexes, and the Inspector may show something that's gone.
        private void AfterDelete()
        {
            StateMachineGraphWindow.ReloadAll();
            Rebuild();
        }

        // ---------- Rename ----------

        // While a name is being typed, a rebuild would throw the text field away; it waits until done.
        private bool IsRenaming { get; set; }
        private bool IsRebuildPending { get; set; }

        // A rebuild drops any text field that was open.
        private void ResetRenameState()
        {
            IsRenaming = false;
            IsRebuildPending = false;
        }

        // Like the Project window: the name becomes a text field. Enter or clicking away keeps the new
        // name, Escape cancels.
        private void BeginRename(VisualElement row, Label label, Object item)
        {
            // Already renaming this row.
            if (label.style.display == DisplayStyle.None)
            {
                return;
            }

            var field = new TextField { value = item.name };
            field.AddToClassList("contents-row__field");
            label.style.display = DisplayStyle.None;
            row.Add(field);
            IsRenaming = true;

            bool isFinished = false;
            void Finish(bool keep)
            {
                if (isFinished)
                {
                    return;
                }

                isFinished = true;
                IsRenaming = false;
                string newName = field.value;
                field.RemoveFromHierarchy();
                label.style.display = DisplayStyle.Flex;

                // Renaming rebuilds the panel, which isn't safe while this event is still being sent.
                if (keep)
                {
                    schedule.Execute(() => Rename(item, newName));
                }
                else if (IsRebuildPending)
                {
                    schedule.Execute(Rebuild);
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

            // Selecting text with the mouse must not start a drag or select the row again.
            field.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            field.RegisterCallback<PointerMoveEvent>(evt => evt.StopPropagation());
            field.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            // The field has to be on screen before it can take focus.
            field.schedule.Execute(() =>
            {
                field.Focus();
                field.SelectAll();
            });
        }

        // A state renamed from its node in the graph, same rules as renaming its row here.
        public void RenameFromGraph(Object item, string newName)
        {
            Rename(item, newName);
        }

        private void Rename(Object item, string newName)
        {
            newName = newName?.Trim();
            if (item == null || string.IsNullOrEmpty(newName) || newName == item.name)
            {
                // Edits that came in while typing still need to show.
                Rebuild();
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            // A sub-state machine's pieces follow its name (worked out before it changes).
            List<(Object Item, string Name)> followers = SubStateMachineNameFollowers(item, newName);

            if (!RenameOne(item, newName))
            {
                return;
            }

            foreach ((Object follower, string followerName) in followers)
            {
                RenameOne(follower, followerName);
            }

            Undo.CollapseUndoOperations(undoGroup);

            // The Project window only shows a sub-asset's new name after its file is re-imported
            // (the item's own file too, e.g. a Base's state renamed from a variant's graph).
            string tablePath = AssetDatabase.GetAssetPath(_table);
            string itemPath = AssetDatabase.GetAssetPath(item);
            AssetDatabase.ImportAsset(tablePath);
            if (!string.IsNullOrEmpty(itemPath) && itemPath != tablePath)
            {
                AssetDatabase.ImportAsset(itemPath);
            }

            // Node titles in the graph are read when it loads.
            StateMachineGraphWindow.ReloadAll();
            Rebuild();
        }

        // The main object of a file (a shared file) is renamed as a file, like F2 in the Project window
        // (no Undo, same as there). Something inside a file (a sub-asset) can't be renamed in the Project
        // window, so only its name changes (with Undo).
        private static bool RenameOne(Object item, string newName)
        {
            if (AssetDatabase.IsMainAsset(item))
            {
                string error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(item), newName);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning($"[Contents] Couldn't rename {item.name}: {error}", item);
                    return false;
                }

                return true;
            }

            Undo.RecordObject(item, "Rename");
            item.name = newName;
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssetIfDirty(item);
            return true;
        }

        // Renaming "NewSub_State" to "Attack_State" also renames its pieces that are inside this table's
        // file and still carry the old name in front (names as in EmbeddedAssets):
        //   NewSubSubStateMachine_Action -> AttackSubStateMachine_Action
        //   NewSub_TransitionTable       -> Attack_TransitionTable
        //   its exit states, exit actions and exit conditions
        // Shared files are left alone, since other tables use them under their own names.
        private List<(Object Item, string Name)> SubStateMachineNameFollowers(Object item, string newName)
        {
            var followers = new List<(Object Item, string Name)>();
            if (item is not StateSO state)
            {
                return followers;
            }

            string oldPrefix = EmbeddedAssets.StatePrefix(state.name);
            string newPrefix = EmbeddedAssets.StatePrefix(newName);

            var linked = new List<Object>();
            linked.AddRange(TableContents.ActionsOf(state).OfType<SubStateMachineActionSO>());

            List<TransitionTableSO> subTables = StateNodeView.FindSubTables(state);
            linked.AddRange(subTables);

            foreach (TransitionTableSO subTable in subTables)
            {
                foreach (StateSO subTableState in TableContents.CollectUsed(subTable).OfType<StateSO>())
                {
                    List<ScriptableObject> exitActions = TableContents.ExitActionsOf(subTableState);
                    if (exitActions.Count > 0)
                    {
                        linked.Add(subTableState);
                        linked.AddRange(exitActions);
                    }
                }
            }

            linked.AddRange(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(_table))
                .OfType<SubStateMachineExitedConditionSO>()
                .Where(condition => EmbeddedAssets.IsExitConditionOf(condition, oldPrefix)));

            foreach (Object linkedItem in linked.Distinct())
            {
                if (TableContents.IsInSameFile(linkedItem, _table) &&
                    linkedItem.name.StartsWith(oldPrefix, StringComparison.Ordinal))
                {
                    followers.Add((linkedItem, newPrefix + linkedItem.name.Substring(oldPrefix.Length)));
                }
            }

            return followers;
        }

        // ---------- Right-click menus ----------

        // On a group header or folder: create (into that folder), and folder commands.
        private void BuildFolderMenu(ContextualMenuPopulateEvent evt, VisualElement header, string group, string folderPath)
        {
            switch (group)
            {
                case StatesGroup:
                    evt.menu.AppendAction("Create State", _ => CreateItem(folderPath, typeof(StateSO), "New_State"));
                    evt.menu.AppendAction("Create Sub State Machine", _ => CreateSubStateMachine(folderPath));
                    break;

                case ActionsGroup:
                    foreach (CreatableType type in CreatableTypes(typeof(StateActionSO), "/Actions/"))
                    {
                        evt.menu.AppendAction("Create Action/" + type.MenuPath, _ => CreateItem(folderPath, type.Type, type.BaseName));
                    }
                    break;

                case ConditionsGroup:
                    foreach (CreatableType type in CreatableTypes(typeof(StateConditionSO), "/Conditions/"))
                    {
                        evt.menu.AppendAction("Create Condition/" + type.MenuPath, _ => CreateItem(folderPath, type.Type, type.BaseName));
                    }
                    break;
            }

            evt.menu.AppendSeparator();

            evt.menu.AppendAction("Create Folder", _ =>
                ShowNamePopup(header, "New Folder", "", name =>
                    EditFolders(folders => folders.CreateFolder(group, JoinPath(folderPath, name)))));

            // The group header itself can't be renamed or deleted.
            if (folderPath.Length == 0)
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Delete All Unused (○)...", _ => DeleteAllUnused());
                return;
            }

            evt.menu.AppendAction("Rename...", _ =>
                ShowNamePopup(header, "Rename Folder", NameOf(folderPath), name =>
                    EditFolders(folders => folders.MoveFolder(group, folderPath, JoinPath(ParentOf(folderPath), name)))));

            // Nothing is deleted from the table: what was inside moves up one level.
            evt.menu.AppendAction("Delete Folder (keep contents)", _ =>
                EditFolders(folders => folders.MoveFolder(group, folderPath, ParentOf(folderPath))));
        }

        // On a row: rename, share / copy, delete, and (in a group with folders) move to a folder.
        private void BuildRowMenu(
            ContextualMenuPopulateEvent evt,
            VisualElement row,
            Label label,
            ScriptableObject item,
            bool isEmbedded,
            string group,
            string currentFolder,
            List<string> groupFolders)
        {
            evt.menu.AppendAction("Rename", _ => BeginRename(row, label, item));

            // A sub-state machine's table: bring a shared one into this file (with everything embedded in
            // its own file), or delete an embedded one. Making an embedded one shared isn't done yet.
            if (item is TransitionTableSO)
            {
                if (isEmbedded)
                {
                    evt.menu.AppendAction("Delete...", _ => DeleteItem(item));
                }
                else
                {
                    evt.menu.AppendAction("Make Local Copy (into this file)", _ => MakeLocalCopy(item));
                }

                return;
            }

            AppendClipboardActions(evt.menu, item);

            if (isEmbedded)
            {
                evt.menu.AppendAction("Make Shared (own file)...", _ => MakeShared(item));
                evt.menu.AppendAction("Delete...", _ => DeleteItem(item));
            }
            else
            {
                evt.menu.AppendAction("Make Local Copy", _ => MakeLocalCopy(item));
                evt.menu.AppendAction("Remove from Table...", _ => RemoveFromTable(item));
            }

            if (groupFolders == null)
            {
                return;
            }

            evt.menu.AppendSeparator();

            // "/" would make a submenu, so show nested folders as "Combat › Melee".
            foreach (string folder in groupFolders)
            {
                DropdownMenuAction.Status status = folder == currentFolder
                    ? DropdownMenuAction.Status.Checked | DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal;

                evt.menu.AppendAction(
                    "Move to Folder/" + folder.Replace("/", " › "),
                    _ => EditFolders(folders => folders.SetItemFolder(item, folder)),
                    status);
            }

            evt.menu.AppendAction("Move to Folder/New Folder...", _ =>
                ShowNamePopup(row, "New Folder", "", name =>
                    EditFolders(folders =>
                    {
                        folders.CreateFolder(group, name);
                        folders.SetItemFolder(item, name);
                    })));

            evt.menu.AppendAction(
                "Remove from Folder",
                _ => EditFolders(folders => folders.SetItemFolder(item, null)),
                currentFolder != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        // ---------- Name popup ----------

        private void ShowNamePopup(VisualElement anchor, string title, string initialName, Action<string> onConfirm)
        {
            // worldBound is inside the window; the popup needs screen coordinates.
            Rect bound = anchor.worldBound;
            var screenRect = new Rect(_window.position.position + bound.position, bound.size);
            NamePopup.Show(screenRect, title, initialName, onConfirm);
        }

        // Small dropdown with a text field: new folder, folder rename, new exit.
        // A folder name like "Combat/Melee" makes a folder inside a folder.
        private class NamePopup : EditorWindow
        {
            private string _title;
            private string _name;
            private Action<string> _onConfirm;
            private bool _isFocused;

            public static void Show(Rect screenRect, string title, string initialName, Action<string> onConfirm)
            {
                var popup = CreateInstance<NamePopup>();
                popup._title = title;
                popup._name = initialName;
                popup._onConfirm = onConfirm;
                popup.ShowAsDropDown(screenRect, new Vector2(240f, 64f));
            }

            private void OnGUI()
            {
                // Read before the text field, which would use up the key.
                bool isEnter = Event.current.type == EventType.KeyDown &&
                    (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
                bool isEscape = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;

                EditorGUILayout.LabelField(_title, EditorStyles.boldLabel);

                GUI.SetNextControlName("Name");
                _name = EditorGUILayout.TextField(_name);

                if (!_isFocused)
                {
                    EditorGUI.FocusTextInControl("Name");
                    _isFocused = true;
                }

                if (isEscape)
                {
                    Close();
                    return;
                }

                if (GUILayout.Button("OK") || isEnter)
                {
                    if (!string.IsNullOrWhiteSpace(_name))
                    {
                        _onConfirm?.Invoke(_name);
                    }

                    Close();
                }
            }
        }
    }
}

#endif
