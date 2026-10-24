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
    // The From Parent group. Inside a sub-state machine's table that lives in its parent's file: the
    // parent table's Actions and Conditions, plus the file's unused (○) ones (in the parent's folders), so they can be dragged into a
    // transition or an action list here and both tables use the same one (e.g. the input conditions).
    // Read-only here (no rename, delete or folders; do those in the parent). Closed until opened.
    // Items this table already uses show in its own groups instead, and the sub-state machine wiring
    // (sub-state / exit actions, exited conditions) isn't offered, since it belongs to one machine.
    public partial class ContentsPanel
    {
        private const string ParentGroupKey = "FromParent";

        // Rows from the parent: not part of multi-select edits (delete, copy, ...) here.
        private readonly HashSet<Object> _parentRows = new HashSet<Object>();

        // A variant parent runs its Base's too (e.g. the Base's AttackRange_Condition), so those are offered as well.
        private static List<ScriptableObject> ParentItems(TransitionTableSO parentTable, HashSet<ScriptableObject> used)
        {
            TransitionTableSO parentBase = StateMachineGraphView.SafeBaseOf(parentTable);
            IEnumerable<ScriptableObject> fromParentBase = parentBase != null
                ? BaseItems(parentBase)
                : Enumerable.Empty<ScriptableObject>();

            return TableContents.CollectUsed(parentTable)
                .Concat(TableContents.UnusedInFile(parentTable))
                .Concat(fromParentBase)
                .Distinct()
                .Where(item => item is StateActionSO || item is StateConditionSO)
                .Where(item => !IsSubStateMachineWiring(item) && !used.Contains(item))
                .ToList();
        }

        private static bool IsSubStateMachineWiring(Object item)
        {
            return item is SubStateMachineActionSO ||
                   item is ExitSubStateMachineActionSO ||
                   item is SubStateMachineExitedConditionSO;
        }

        private void AddParentGroup(TransitionTableSO parentTable, List<ScriptableObject> items, string tablePath)
        {
            if (parentTable == null || items.Count == 0)
            {
                return;
            }

            Foldout group = CreateFoldout($"From Parent · {parentTable.name}", ParentGroupKey);
            MakeGroup(group, items.Count);
            group.Q<Toggle>().tooltip =
                $"{parentTable.name}'s actions and conditions. Drag one into a transition or an action list " +
                "here to use it too. Rename, delete and folders are done in the parent.";

            // The parent's folders, then its Bases' for what the parent didn't file (its Base's items).
            FolderData folders = FolderData.Load(parentTable);
            var visited = new HashSet<TransitionTableSO> { parentTable };
            for (TransitionTableSO baseTable = StateMachineGraphView.SafeBaseOf(parentTable);
                 baseTable != null && visited.Add(baseTable);
                 baseTable = StateMachineGraphView.BaseOf(baseTable))
            {
                foreach (KeyValuePair<Object, string> entry in FolderData.Load(baseTable).ItemFolders)
                {
                    if (!folders.ItemFolders.ContainsKey(entry.Key))
                    {
                        folders.ItemFolders[entry.Key] = entry.Value;
                    }
                }
            }

            AddParentPart(group, ParentGroupKey, ActionsGroup, items.OfType<StateActionSO>(), folders, tablePath, false);
            AddParentPart(group, ParentGroupKey, ConditionsGroup, items.OfType<StateConditionSO>(), folders, tablePath, false);

            _list.Add(group);
        }

        // States, Actions or Conditions of another table (the parent, or the Base: isFromBase), with
        // that table's folders that hold something (empty ones are left out). Read-only rows.
        private void AddParentPart(
            VisualElement group,
            string groupKey,
            string kind,
            IEnumerable<ScriptableObject> items,
            FolderData folders,
            string tablePath,
            bool isFromBase)
        {
            List<ScriptableObject> sorted = items.OrderBy(item => item.name).ToList();
            if (sorted.Count == 0)
            {
                return;
            }

            Foldout part = CreateFoldout(kind, $"{groupKey}/{kind}");
            group.Add(part);

            var folderPaths = new SortedSet<string>(System.StringComparer.Ordinal);
            foreach (ScriptableObject item in sorted)
            {
                if (folders.ItemFolders.TryGetValue(item, out string folder))
                {
                    for (string path = folder; path.Length > 0; path = ParentOf(path))
                    {
                        folderPaths.Add(path);
                    }
                }
            }

            var folderViews = new Dictionary<string, Foldout>();
            foreach (string path in folderPaths)
            {
                GetOrCreateParentFolder(part, groupKey, kind, path, folderViews);
            }

            foreach (ScriptableObject item in sorted)
            {
                folders.ItemFolders.TryGetValue(item, out string folder);
                VisualElement parent = folder != null ? folderViews[folder] : part;
                parent.Add(CreateRow(item, null, null, true, tablePath, null,
                    isFromParent: !isFromBase, isFromBase: isFromBase));
            }
        }

        // Like GetOrCreateFolder, but a plain folder: no drops, no menu, since the folders are the other table's.
        private Foldout GetOrCreateParentFolder(
            Foldout part,
            string groupKey,
            string kind,
            string path,
            Dictionary<string, Foldout> folderViews)
        {
            if (folderViews.TryGetValue(path, out Foldout existing))
            {
                return existing;
            }

            string parentPath = ParentOf(path);
            VisualElement parent = parentPath.Length == 0
                ? part
                : GetOrCreateParentFolder(part, groupKey, kind, parentPath, folderViews);

            Foldout folder = CreateFoldout(NameOf(path), $"{groupKey}/{kind}/{path}");
            VisualElement toggleInput = folder.Q(className: Foldout.inputUssClassName);
            if (toggleInput != null)
            {
                var icon = new Image { image = EditorGUIUtility.IconContent("Folder Icon").image };
                icon.AddToClassList("contents-folder-icon");
                toggleInput.Insert(1, icon);
            }

            parent.Add(folder);
            folderViews[path] = folder;
            return folder;
        }
    }
}

#endif
