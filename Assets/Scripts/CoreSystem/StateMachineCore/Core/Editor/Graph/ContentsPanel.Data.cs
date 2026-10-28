#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // The Data panel: everything else inside the table's file (TableContents.IsDataItem), such as the
    // AnchorIDSO, CooldownSO and RandomPoolSO its actions and conditions share with scene components.
    // One Data group with folders, stored with the Contents folders (group "Data").
    // It always shows the file's main table, also while a sub-state machine is open, since the whole
    // file shares one set of ids.
    // A .asset dragged from the Project window onto the graph or here moves into the file (MoveIn).
    public partial class ContentsPanel
    {
        private static TransitionTableSO MainTableOf(TransitionTableSO table)
        {
            if (table == null)
            {
                return null;
            }

            return AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(table)) as TransitionTableSO ?? table;
        }

        private const string StateMachineMenuRoot = "YUKI Learning State Machine/";

        // Right-click Data > Create: every data type whose [CreateAssetMenu] is under the state machine's
        // menu but not under StateMachineList (states, actions, conditions):
        //   "YUKI Learning State Machine/Random Pick/Random Pool" -> "Random Pick/Random Pool"
        private static IEnumerable<CreatableType> DataCreatableTypes()
        {
            var types = new List<CreatableType>();

            foreach (System.Type type in TypeCache.GetTypesWithAttribute<CreateAssetMenuAttribute>())
            {
                if (type.IsAbstract || type.IsGenericType || !TableContents.IsDataType(type))
                {
                    continue;
                }

                var attribute = (CreateAssetMenuAttribute)System.Attribute.GetCustomAttribute(type, typeof(CreateAssetMenuAttribute));
                string menuPath = attribute.menuName ?? "";
                if (!menuPath.StartsWith(StateMachineMenuRoot, System.StringComparison.Ordinal) ||
                    menuPath.StartsWith(StateMachineMenuRoot + "StateMachineList/", System.StringComparison.Ordinal))
                {
                    continue;
                }

                string baseName = string.IsNullOrEmpty(attribute.fileName) ? type.Name : attribute.fileName;
                types.Add(new CreatableType
                {
                    Type = type,
                    MenuPath = menuPath.Substring(StateMachineMenuRoot.Length),
                    BaseName = baseName
                });
            }

            return types.OrderBy(type => type.MenuPath);
        }

        private void RebuildData()
        {
            List<ScriptableObject> items = TableContents.CollectData(_table);
            var all = new HashSet<ScriptableObject>(items);
            string tablePath = AssetDatabase.GetAssetPath(_table);

            if (_search.Length > 0)
            {
                AddSearchResults(items, all, new Dictionary<StateSO, TableContents.SubStateParts>(), tablePath,
                    new List<ScriptableObject>());
            }
            else
            {
                AddGroup(DataGroup, items, all, FolderData.Load(_table), tablePath);
            }

            ShowSelection();
        }

        // Moves the items into the table's file after asking (EmbeddedAssets.MoveInto), and files each
        // one into folderPath, or with folderPath null into the folder it was in next to the table
        // (".../Tanee/Anchor/X.asset" next to ".../Tanee/TransitionTable/" goes into "Anchor").
        // Without Undo, like the other drops; it can't be undone anyway (files are deleted).
        public void MoveIn(List<ScriptableObject> items, string folderPath = null)
        {
            if (_table == null)
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Move into Table", "Stop Play Mode first.", "OK");
                return;
            }

            string tablePath = AssetDatabase.GetAssetPath(_table);
            items = items.Where(item => TableContents.CanMoveIn(item, tablePath)).Distinct().ToList();
            if (items.Count == 0)
            {
                return;
            }

            if (!ConfirmMoveIn(items))
            {
                return;
            }

            Dictionary<string, string> folderByPath = items
                .Select(item => AssetDatabase.GetAssetPath(item))
                .ToDictionary(path => path, path => folderPath ?? FolderNextTo(path, tablePath));

            List<(string OldPath, ScriptableObject Moved)> moved = EmbeddedAssets.MoveInto(_table, items);

            EditFolders(folders =>
            {
                foreach ((string oldPath, ScriptableObject item) in moved)
                {
                    folders.SetItemFolder(item, folderByPath[oldPath]);
                }
            }, recordUndo: false);

            // Other tables and the graph's nodes may still hold the old objects.
            StateMachineGraphWindow.ReloadAll();
            Selection.objects = moved.Select(pair => (Object)pair.Moved).ToArray();
        }

        private bool ConfirmMoveIn(List<ScriptableObject> items)
        {
            string names = ListText(items.Select(item => item.name));
            List<string> users = EmbeddedAssets.FilesUsing(items);

            string message = $"Move into {Path.GetFileName(AssetDatabase.GetAssetPath(_table))}?\n\n{names}\n\n";
            message += users.Count > 0
                ? $"These point at them and will point at the moved ones:\n{ListText(users)}\n\n"
                : "Nothing else points at them.\n\n";
            message += "Their own files are deleted. This can't be undone (commit first to be safe).";

            return EditorUtility.DisplayDialog("Move into Table", message, "Move", "Cancel");
        }

        private static string ListText(IEnumerable<string> lines)
        {
            List<string> all = lines.ToList();
            string text = string.Join("\n", all.Take(10).Select(line => "  " + line));
            return all.Count > 10 ? $"{text}\n  ...and {all.Count - 10} more" : text;
        }

        // The item's folder, from where its path and the table's folder part ways.
        // "A/Tanee/Picker/CooldownID/X.asset" with "A/Tanee/TransitionTable/T.asset" -> "Picker/CooldownID"
        private static string FolderNextTo(string itemPath, string tablePath)
        {
            string[] itemFolders = Path.GetDirectoryName(itemPath).Replace('\\', '/').Split('/');
            string[] tableFolders = Path.GetDirectoryName(tablePath).Replace('\\', '/').Split('/');

            int shared = 0;
            while (shared < itemFolders.Length &&
                   shared < tableFolders.Length &&
                   itemFolders[shared] == tableFolders[shared])
            {
                shared++;
            }

            return string.Join("/", itemFolders.Skip(shared));
        }
    }
}

#endif
