#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor
{
    // Right-click in the Project window > Create > State Machine SO...: makes an SO as its own file, for
    // when the Graph isn't open. A searchable popup of the same list the Contents panel's Create menus
    // use (FolderMenu), so its groups are the scripts' folders too.
    // SearchWindow needs the provider to be a ScriptableObject.
    public class ScriptableObjectCreateMenu : ScriptableObject, ISearchWindowProvider
    {
        private const string MenuPath = "Assets/Create/State Machine SO...";

        private static ScriptableObjectCreateMenu _provider;

        [MenuItem(MenuPath, false, 0)]
        private static void Open()
        {
            // The right-click menu is still closing here, and it would take a popup opened now with it.
            EditorApplication.delayCall += OpenSearchWindow;
        }

        private static void OpenSearchWindow()
        {
            if (_provider == null)
            {
                _provider = CreateInstance<ScriptableObjectCreateMenu>();
                _provider.hideFlags = HideFlags.HideAndDontSave;
            }

            // A menu click has no mouse position to read, so it opens over the window that was clicked in.
            EditorWindow window = EditorWindow.focusedWindow;
            Vector2 screenPosition = window != null
                ? window.position.center
                : EditorGUIUtility.GetMainWindowPosition().center;

            SearchWindow.Open(new SearchWindowContext(screenPosition), _provider);
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create State Machine SO"), 0)
            };

            // ExposedParametersSO is made by the Graph, as part of a table's file.
            List<FolderMenu.Entry> entries = FolderMenu.Find(type => type != typeof(ExposedParametersSO));

            // '\0' sorts a group's subgroups right after it, before a sibling like "Action Extra", so
            // each group appears once, also when [MenuGroup] puts an entry in a folder's group.
            IEnumerable<FolderMenu.Entry> sorted = entries
                .OrderBy(entry => FoldersOf(entry), StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.MenuPath, StringComparer.OrdinalIgnoreCase);

            // A group is added the first time it appears. Its entries follow it, one level deeper.
            string[] openGroups = Array.Empty<string>();
            foreach (FolderMenu.Entry entry in sorted)
            {
                string[] parts = entry.MenuPath.Split('/');
                string[] groups = parts.Take(parts.Length - 1).ToArray();

                for (int i = SharedGroupCount(openGroups, groups); i < groups.Length; i++)
                {
                    tree.Add(new SearchTreeGroupEntry(new GUIContent(groups[i]), i + 1));
                }

                tree.Add(new SearchTreeEntry(new GUIContent(parts[parts.Length - 1]))
                {
                    level = groups.Length + 1,
                    userData = entry
                });

                openGroups = groups;
            }

            return tree;
        }

        // "A/B/Name" -> "A\0B"
        private static string FoldersOf(FolderMenu.Entry entry)
        {
            int lastSlash = entry.MenuPath.LastIndexOf('/');
            return lastSlash < 0 ? "" : entry.MenuPath.Substring(0, lastSlash).Replace('/', '\0');
        }

        private static int SharedGroupCount(string[] a, string[] b)
        {
            int count = 0;
            while (count < a.Length && count < b.Length && a[count] == b[count])
            {
                count++;
            }

            return count;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            var picked = (FolderMenu.Entry)entry.userData;

            // After the popup has closed, so that closing it doesn't end the rename. A path with no
            // folder goes in the folder open in the Project window, like Create > anything.
            EditorApplication.delayCall += () =>
                ProjectWindowUtil.CreateAsset(CreateInstance(picked.Type), $"{picked.BaseName}.asset");

            // true closes the popup.
            return true;
        }
    }
}

#endif
