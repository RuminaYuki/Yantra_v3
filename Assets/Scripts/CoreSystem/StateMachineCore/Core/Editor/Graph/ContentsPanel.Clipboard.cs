#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Ctrl+C / Ctrl+V / Ctrl+D / Ctrl+A and multi-select Delete, like the Project window.
    // The clipboard keeps the originals for this editor session; Paste copies them into whichever table
    // is open then, so copying between tables works too (see EmbeddedAssets.CopyInto).
    public partial class ContentsPanel
    {
        private static readonly List<ScriptableObject> Clipboard = new List<ScriptableObject>();

        // Unity sends the shortcuts as commands to the focused row; they bubble up to here.
        private void RegisterClipboardCommands()
        {
            RegisterCallback<ValidateCommandEvent>(evt =>
            {
                if (!IsTyping(evt.target) && CanRun(evt.commandName))
                {
                    evt.StopPropagation();
                }
            });

            RegisterCallback<ExecuteCommandEvent>(evt =>
            {
                if (IsTyping(evt.target) || !CanRun(evt.commandName))
                {
                    return;
                }

                evt.StopPropagation();

                // Changing assets while the command is still being sent around isn't safe.
                string command = evt.commandName;
                schedule.Execute(() => Run(command));
            });
        }

        // Ctrl+C in the search box or a rename field is for the text, not the rows.
        private static bool IsTyping(IEventHandler target)
        {
            return target is VisualElement element &&
                   (element is TextField || element.GetFirstAncestorOfType<TextField>() != null);
        }

        private bool CanRun(string command)
        {
            if (_table == null || IsRenaming)
            {
                return false;
            }

            switch (command)
            {
                case "Copy":
                case "Duplicate":
                    return SelectedCopyable().Count > 0;
                case "Paste":
                    return Pastable().Count > 0;
                case "SelectAll":
                    return true;
                default:
                    return false;
            }
        }

        private void Run(string command)
        {
            switch (command)
            {
                case "Copy":
                    CopyToClipboard(SelectedCopyable());
                    break;
                case "Duplicate":
                    PasteCopies(SelectedCopyable());
                    break;
                case "Paste":
                    PasteCopies(Pastable());
                    break;
                case "SelectAll":
                    Selection.objects = ShownItemsInOrder().ToArray();
                    break;
            }
        }

        // Right-click menu: Copy / Paste / Duplicate for the selection (or just this row if it isn't selected).
        private void AppendClipboardActions(DropdownMenu menu, ScriptableObject item)
        {
            List<ScriptableObject> targets = SelectionTargetsFor(item).Where(CanCopy).ToList();
            DropdownMenuAction.Status status = targets.Count > 0
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled;
            string count = targets.Count > 1 ? $" ({targets.Count})" : "";

            menu.AppendAction($"Copy{count}", _ => CopyToClipboard(targets), status);
            List<ScriptableObject> pastable = Pastable();
            menu.AppendAction(
                "Paste",
                _ => PasteCopies(pastable),
                pastable.Count > 0
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            menu.AppendAction($"Duplicate{count}", _ => PasteCopies(targets), status);
            menu.AppendSeparator();
        }

        // ---------- Targets ----------

        // The selected rows if the item is one of them, otherwise just the item (like the Project window).
        private List<ScriptableObject> SelectionTargetsFor(ScriptableObject item)
        {
            return IsSelected(item)
                ? SelectedRowItems().OfType<ScriptableObject>().ToList()
                : new List<ScriptableObject> { item };
        }

        private List<ScriptableObject> SelectedCopyable()
        {
            return SelectedRowItems().OfType<ScriptableObject>().Where(CanCopy).ToList();
        }

        // ---------- Copy / Paste ----------

        // What was copied in this kind of panel: Data items paste only into Data, the rest only into Contents.
        private List<ScriptableObject> Pastable()
        {
            return Clipboard.Where(item => item != null && TableContents.IsDataItem(item) == _isDataPanel).ToList();
        }

        private static void CopyToClipboard(List<ScriptableObject> items)
        {
            Clipboard.Clear();
            Clipboard.AddRange(items);
        }

        // Copies go into this table's file, in the same folder as their original when it's from this
        // table. New states go on the graph. The copies end up selected; a single one is ready to rename.
        private void PasteCopies(List<ScriptableObject> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            Dictionary<Object, string> oldFolders = FolderData.Load(_table).ItemFolders;
            Dictionary<ScriptableObject, ScriptableObject> copies = EmbeddedAssets.CopyInto(_table, items);
            foreach (ScriptableObject copy in copies.Values)
            {
                ClaimForSubTable(_table, copy);
            }

            EditFolders(folders =>
            {
                foreach (KeyValuePair<ScriptableObject, ScriptableObject> pair in copies)
                {
                    oldFolders.TryGetValue(pair.Key, out string folder);
                    folders.SetItemFolder(pair.Value, folder);
                }
            });

            foreach (StateSO state in copies.Values.OfType<StateSO>())
            {
                _onStateCreated?.Invoke(state);
            }

            Selection.objects = copies.Values.Cast<Object>().ToArray();
            Undo.CollapseUndoOperations(undoGroup);

            if (copies.Count == 1)
            {
                ScriptableObject copy = copies.Values.First();
                schedule.Execute(() =>
                {
                    if (_rows.TryGetValue(copy, out (VisualElement Row, Label Label) row))
                    {
                        BeginRename(row.Row, row.Label, copy);
                    }
                });
            }
        }

        // ---------- Delete ----------

        // Several rows at once: one question for all of them. In this file → deleted; shared → this
        // table stops using them (the shared file stays).
        private void DeleteItems(List<ScriptableObject> items)
        {
            List<ScriptableObject> embedded = items.Where(item => TableContents.IsInSameFile(item, _table)).ToList();
            List<ScriptableObject> shared = items
                .Where(item => !TableContents.IsInSameFile(item, _table) && item is not TransitionTableSO)
                .ToList();

            var parts = new List<string>();
            if (embedded.Count > 0)
            {
                parts.Add($"delete {embedded.Count} item(s) in this file");
            }

            if (shared.Count > 0)
            {
                parts.Add($"stop using {shared.Count} shared item(s)");
            }

            if (parts.Count == 0)
            {
                return;
            }

            string message =
                $"{string.Join(" and ", parts)}? {UsesWarning}\n\n" +
                "Anything in this file that only they used is deleted too. This can't be undone.";
            message = char.ToUpperInvariant(message[0]) + message.Substring(1);

            if (!EditorUtility.DisplayDialog("Delete", message, "Delete", "Cancel"))
            {
                return;
            }

            foreach (ScriptableObject item in embedded)
            {
                // Deleting an earlier one may already have taken this one with it.
                if (item != null)
                {
                    EmbeddedAssets.Delete(_table, item);
                }
            }

            foreach (ScriptableObject item in shared)
            {
                EmbeddedAssets.RemoveUses(_table, item);
            }

            AfterDelete();
        }
    }
}

#endif
