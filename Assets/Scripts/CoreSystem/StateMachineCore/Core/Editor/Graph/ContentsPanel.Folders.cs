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
    // Folders, like the Project window: a folder belongs to one group (States, Actions, Conditions or Data)
    // and only organizes this panel. Right-click a group or folder to create, rename or delete folders;
    // drag items or folders onto a folder, or onto a group header to take them out.
    // Stored in the table's hidden _contentsFolders (which folder each item is in) and
    // _contentsFolderPaths (folders the user created, so empty ones survive).
    public partial class ContentsPanel
    {
        private const string DragDataKey = "StateMachineContentsDrag";
        private const float DragStartDistance = 5f;
        private const string DropTargetClass = "contents-drop-target";

        // Drag: what was pressed and where, until the mouse moves far enough to start dragging.
        private DragPayload _dragCandidate;
        private Vector2 _dragStart;

        // The header lit up as the current drop target.
        private VisualElement _dropHighlight;

        // What is being dragged: an item, or a folder (Item null).
        private class DragPayload
        {
            public string Group;
            public Object Item;
            public string FolderPath;

            // Set when the drag starts: Item, plus the rest of the selection in its group if Item was selected.
            public Object[] Items;
        }

        // ---------- Folder data ----------

        // Loads the table's two folder arrays, runs the edit, writes them back (one Undo step unless
        // recordUndo is false), redraws.
        private void EditFolders(Action<FolderData> edit, bool recordUndo = true)
        {
            FolderData folders = FolderData.Load(_table);
            edit(folders);

            // Entries may stay for anything the table uses or holds in its file.
            var known = new HashSet<ScriptableObject>(TableContents.CollectUsed(_table));
            known.UnionWith(TableContents.CollectEmbedded(_table));
            known.UnionWith(TableContents.CollectData(_table));
            folders.Save(_table, known, recordUndo);

            Rebuild();
        }

        // The table's folder arrays as plain C# collections.
        private class FolderData
        {
            // Which folder each item is in ("Combat", "Combat/Melee").
            public readonly Dictionary<Object, string> ItemFolders = new Dictionary<Object, string>();

            // Folders the user created, by group, so empty ones survive.
            public readonly List<(string Group, string Path)> Paths = new List<(string Group, string Path)>();

            public static FolderData Load(TransitionTableSO table)
            {
                var data = new FolderData();
                var serializedTable = new SerializedObject(table);

                SerializedProperty items = serializedTable.FindProperty(StateMachineFields.ContentsFolders);
                for (int i = 0; i < items.arraySize; i++)
                {
                    SerializedProperty entry = items.GetArrayElementAtIndex(i);
                    Object item = entry.FindPropertyRelative("Item").objectReferenceValue;
                    string folder = CleanPath(entry.FindPropertyRelative("Folder").stringValue);
                    if (item != null && folder.Length > 0)
                    {
                        data.ItemFolders[item] = folder;
                    }
                }

                SerializedProperty paths = serializedTable.FindProperty(StateMachineFields.ContentsFolderPaths);
                for (int i = 0; i < paths.arraySize; i++)
                {
                    SerializedProperty entry = paths.GetArrayElementAtIndex(i);
                    string group = entry.FindPropertyRelative("Group").stringValue;
                    string path = CleanPath(entry.FindPropertyRelative("Path").stringValue);
                    if (path.Length > 0)
                    {
                        data.Paths.Add((group, path));
                    }
                }

                return data;
            }

            // Also cleans up: entries for items not in `known`, empty names and duplicate folders are dropped.
            public void Save(TransitionTableSO table, HashSet<ScriptableObject> known, bool recordUndo = true)
            {
                var serializedTable = new SerializedObject(table);

                var keptItems = ItemFolders
                    .Where(pair => pair.Key is ScriptableObject item && known.Contains(item) && pair.Value.Length > 0)
                    .ToList();

                SerializedProperty items = serializedTable.FindProperty(StateMachineFields.ContentsFolders);
                items.arraySize = keptItems.Count;
                for (int i = 0; i < keptItems.Count; i++)
                {
                    SerializedProperty entry = items.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("Item").objectReferenceValue = keptItems[i].Key;
                    entry.FindPropertyRelative("Folder").stringValue = keptItems[i].Value;
                }

                var keptPaths = Paths.Where(folder => folder.Path.Length > 0).Distinct().ToList();

                SerializedProperty paths = serializedTable.FindProperty(StateMachineFields.ContentsFolderPaths);
                paths.arraySize = keptPaths.Count;
                for (int i = 0; i < keptPaths.Count; i++)
                {
                    SerializedProperty entry = paths.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("Group").stringValue = keptPaths[i].Group;
                    entry.FindPropertyRelative("Path").stringValue = keptPaths[i].Path;
                }

                if (recordUndo)
                {
                    serializedTable.ApplyModifiedProperties();
                }
                else
                {
                    serializedTable.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(table);
                }
            }

            // Every folder to draw in a group: created ones, ones holding items, and all their parents.
            public List<string> FoldersIn(string group, IEnumerable<ScriptableObject> groupItems)
            {
                var folders = new SortedSet<string>(StringComparer.Ordinal);

                foreach ((string folderGroup, string path) in Paths)
                {
                    if (folderGroup == group)
                    {
                        folders.Add(path);
                    }
                }

                foreach (ScriptableObject item in groupItems)
                {
                    if (ItemFolders.TryGetValue(item, out string folder))
                    {
                        folders.Add(folder);
                    }
                }

                foreach (string path in folders.ToList())
                {
                    for (string parent = ParentOf(path); parent.Length > 0; parent = ParentOf(parent))
                    {
                        folders.Add(parent);
                    }
                }

                return folders.ToList();
            }

            public void SetItemFolder(Object item, string folder)
            {
                folder = CleanPath(folder);
                if (folder.Length > 0)
                {
                    ItemFolders[item] = folder;
                }
                else
                {
                    ItemFolders.Remove(item);
                }
            }

            public void CreateFolder(string group, string path)
            {
                path = CleanPath(path);
                if (path.Length > 0)
                {
                    Paths.Add((group, path));
                }
            }

            // Moves (or renames) a folder with everything inside it: its items and subfolders.
            // newPath "" moves the contents to the top of the group.
            public void MoveFolder(string group, string oldPath, string newPath)
            {
                for (int i = 0; i < Paths.Count; i++)
                {
                    if (Paths[i].Group == group && IsSameOrInside(Paths[i].Path, oldPath))
                    {
                        Paths[i] = (group, CleanPath(newPath + Paths[i].Path.Substring(oldPath.Length)));
                    }
                }

                foreach (Object item in ItemFolders.Keys.ToList())
                {
                    string folder = ItemFolders[item];
                    if (GroupOf(item) == group && IsSameOrInside(folder, oldPath))
                    {
                        SetItemFolder(item, newPath + folder.Substring(oldPath.Length));
                    }
                }
            }
        }

        // ---------- Folder paths ----------

        // " Combat//Melee/ " -> "Combat/Melee"
        private static string CleanPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "";
            }

            return string.Join("/", path.Split('/').Select(part => part.Trim()).Where(part => part.Length > 0));
        }

        private static string JoinPath(string parent, string name) => CleanPath(parent + "/" + name);

        // "Combat/Melee" -> "Combat", "Combat" -> ""
        private static string ParentOf(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash < 0 ? "" : path.Substring(0, slash);
        }

        // "Combat/Melee" -> "Melee"
        private static string NameOf(string path) => path.Substring(path.LastIndexOf('/') + 1);

        private static bool IsSameOrInside(string path, string folder)
        {
            return path == folder || path.StartsWith(folder + "/", StringComparison.Ordinal);
        }

        // ---------- Folder views ----------

        // Creates "Combat" and then "Combat/Melee" inside it, reusing ones already made.
        private Foldout GetOrCreateFolder(Foldout groupView, string group, string path, Dictionary<string, Foldout> folderViews)
        {
            if (folderViews.TryGetValue(path, out Foldout existing))
            {
                return existing;
            }

            string parentPath = ParentOf(path);
            VisualElement parent = parentPath.Length == 0
                ? groupView
                : GetOrCreateFolder(groupView, group, parentPath, folderViews);

            Foldout folder = CreateFolderFoldout(NameOf(path), group, path);
            parent.Add(folder);
            folderViews[path] = folder;
            return folder;
        }

        // A group header (folderPath "") or a folder. Both take drops and have a right-click menu;
        // only folders can be dragged.
        private Foldout CreateFolderFoldout(string text, string group, string folderPath)
        {
            bool isFolder = folderPath.Length > 0;
            Foldout foldout = CreateFoldout(text, isFolder ? $"{group}/{folderPath}" : group);
            VisualElement header = foldout.Q<Toggle>();

            if (isFolder)
            {
                // Unity's own folder icon, between the arrow and the name.
                VisualElement toggleInput = foldout.Q(className: Foldout.inputUssClassName);
                if (toggleInput != null)
                {
                    var icon = new Image { image = EditorGUIUtility.IconContent("Folder Icon").image };
                    icon.AddToClassList("contents-folder-icon");
                    toggleInput.Insert(1, icon);
                }

                RegisterDragSource(header, new DragPayload { Group = group, FolderPath = folderPath });
            }

            header.AddManipulator(new ContextualMenuManipulator(evt => BuildFolderMenu(evt, header, group, folderPath)));
            RegisterDropTarget(foldout, header, group, folderPath);

            return foldout;
        }

        // ---------- Drag and drop ----------

        // A drag that ends or leaves the panel turns the drop highlight off.
        private void RegisterDragEndCallbacks()
        {
            RegisterCallback<DragExitedEvent>(_ => SetDropHighlight(null));
            RegisterCallback<DragLeaveEvent>(evt =>
            {
                if (evt.target == this)
                {
                    SetDropHighlight(null);
                }
            });
        }

        // Press, then move a few pixels: starts a Unity drag. An item drag also carries the object
        // itself, so it can be dropped into Inspector fields or onto the graph, like a drag from the
        // Project window.
        private void RegisterDragSource(VisualElement element, DragPayload payload)
        {
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    _dragCandidate = payload;
                    _dragStart = evt.position;
                }
            });

            element.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_dragCandidate != payload ||
                    (evt.pressedButtons & 1) == 0 ||
                    Vector2.Distance(evt.position, _dragStart) < DragStartDistance)
                {
                    return;
                }

                _dragCandidate = null;

                // A new payload per drag: the row's own one is reused, but the selection changes.
                DragPayload dragged = payload.Item != null
                    ? new DragPayload { Group = payload.Group, Item = payload.Item, Items = DraggedItems(payload) }
                    : payload;

                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(DragDataKey, dragged);
                DragAndDrop.objectReferences = dragged.Items ?? Array.Empty<Object>();
                DragAndDrop.StartDrag(
                    dragged.Items == null ? NameOf(dragged.FolderPath) :
                    dragged.Items.Length > 1 ? $"{dragged.Items.Length} items" :
                    dragged.Item.name);
            });

            element.RegisterCallback<PointerUpEvent>(_ => _dragCandidate = null);
        }

        // Like the Project window: dragging a selected row takes everything selected in its group along;
        // dragging a row that isn't selected takes only that one.
        private Object[] DraggedItems(DragPayload payload)
        {
            if (payload.Group == null || !IsSelected(payload.Item))
            {
                return new[] { payload.Item };
            }

            return SelectedRowItems()
                .Where(item => _rowGroups.TryGetValue(item, out string group) && group == payload.Group)
                .ToArray();
        }

        // The whole foldout takes drops (rows inside it too), but only the innermost one handles it.
        private void RegisterDropTarget(Foldout foldout, VisualElement header, string group, string folderPath)
        {
            foldout.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                bool isFromProject = DraggedFromProject().Count > 0;
                bool canDrop = isFromProject || CanDrop(group, folderPath);
                DragAndDrop.visualMode = isFromProject && !_isDataPanel ? DragAndDropVisualMode.Copy
                    : canDrop ? DragAndDropVisualMode.Move
                    : DragAndDropVisualMode.Rejected;
                SetDropHighlight(canDrop ? header : null);
                evt.StopPropagation();
            });

            foldout.RegisterCallback<DragPerformEvent>(evt =>
            {
                List<ScriptableObject> fromProject = DraggedFromProject();
                if (fromProject.Count > 0)
                {
                    DragAndDrop.AcceptDrag();
                    SetDropHighlight(null);
                    evt.StopPropagation();
                    if (_isDataPanel)
                    {
                        schedule.Execute(() => MoveIn(fromProject, folderPath));
                    }
                    else
                    {
                        schedule.Execute(() => CopyFromProject(fromProject, group, folderPath));
                    }

                    return;
                }

                if (CanDrop(group, folderPath) &&
                    DragAndDrop.GetGenericData(DragDataKey) is DragPayload payload)
                {
                    DragAndDrop.AcceptDrag();

                    // Rebuilding while the drop event is still being sent around is unsafe, so wait a frame.
                    schedule.Execute(() => Drop(payload, group, folderPath));
                }

                SetDropHighlight(null);
                evt.StopPropagation();
            });
        }

        // ---------- Dropping files from the Project window ----------

        // States, actions and conditions dragged in from the Project window (not a drag that started in
        // this panel), from other files than this table's. In the Data panel: ids that can move in.
        private List<ScriptableObject> DraggedFromProject()
        {
            if (_table == null || DragAndDrop.GetGenericData(DragDataKey) != null)
            {
                return new List<ScriptableObject>();
            }

            string tablePath = AssetDatabase.GetAssetPath(_table);
            if (_isDataPanel)
            {
                return DragAndDrop.objectReferences
                    .OfType<ScriptableObject>()
                    .Where(item => TableContents.CanMoveIn(item, tablePath))
                    .Distinct()
                    .ToList();
            }

            return DragAndDrop.objectReferences
                .OfType<ScriptableObject>()
                .Where(TableContents.IsContentItem)
                .Where(item =>
                {
                    string itemPath = AssetDatabase.GetAssetPath(item);
                    return !string.IsNullOrEmpty(itemPath) && itemPath != tablePath;
                })
                .Distinct()
                .ToList();
        }

        // Copies into this table's file, used by nothing yet (○), like Ctrl+V but states stay off the
        // graph. What they use from their own file comes along (see EmbeddedAssets.CopyInto). Copies of
        // the dropped group's kind go into the folder dropped on. The original files are left alone.
        // ○ items show in the file's main table, and in the sub-state machine table they were dropped in.
        // Without Undo, like the other drops: Unity's own revert right after a drag ends would destroy
        // the copies in memory while the file on disk kept them (they showed in Project, not here).
        // Remove one again with Delete... in its right-click menu.
        private void CopyFromProject(List<ScriptableObject> items, string group, string folderPath)
        {
            Dictionary<ScriptableObject, ScriptableObject> copies =
                EmbeddedAssets.CopyInto(_table, items, recordUndo: false);
            foreach (ScriptableObject copy in copies.Values)
            {
                ClaimForSubTable(_table, copy);
            }

            if (folderPath.Length > 0)
            {
                EditFolders(folders =>
                {
                    foreach (ScriptableObject copy in copies.Values.Where(copy => GroupOf(copy) == group))
                    {
                        folders.SetItemFolder(copy, folderPath);
                    }
                }, recordUndo: false);
            }
            else
            {
                Rebuild();
            }

            Selection.objects = copies.Values.Cast<Object>().ToArray();
        }

        // Same group only, and a folder can't go inside itself.
        private static bool CanDrop(string group, string targetFolder)
        {
            if (DragAndDrop.GetGenericData(DragDataKey) is not DragPayload payload || payload.Group != group)
            {
                return false;
            }

            return payload.Item != null || !IsSameOrInside(targetFolder, payload.FolderPath);
        }

        // Without Undo: right after a drag ends, Unity can revert the current Undo group on its own
        // (undoRedoPerformed fires), which would throw the move away. Folders only organize this panel.
        private void Drop(DragPayload payload, string group, string targetFolder)
        {
            if (payload.Item != null)
            {
                EditFolders(folders =>
                {
                    foreach (Object item in payload.Items)
                    {
                        folders.SetItemFolder(item, targetFolder);
                    }
                }, recordUndo: false);
            }
            else
            {
                string newPath = JoinPath(targetFolder, NameOf(payload.FolderPath));
                EditFolders(folders => folders.MoveFolder(group, payload.FolderPath, newPath), recordUndo: false);
            }
        }

        private void SetDropHighlight(VisualElement header)
        {
            if (_dropHighlight == header)
            {
                return;
            }

            _dropHighlight?.RemoveFromClassList(DropTargetClass);
            _dropHighlight = header;
            _dropHighlight?.AddToClassList(DropTargetClass);
        }

        // The highlighted header is gone after a rebuild.
        private void ResetDropHighlight()
        {
            _dropHighlight = null;
        }
    }
}

#endif
