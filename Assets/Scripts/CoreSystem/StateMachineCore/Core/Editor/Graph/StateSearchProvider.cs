#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // The searchable popup behind "Add State...": create a new StateSO (inside the table's file, or
    // as its own file to share), or pick an existing one, including states embedded in other tables' files.
    // SearchWindow needs the provider to be a ScriptableObject.
    public class StateSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private const string CreateEmbedded = "CreateEmbedded";
        private const string CreateSubStateMachine = "CreateSubStateMachine";
        private const string CreateFile = "CreateFile";

        private HashSet<StateSO> _alreadyInTable;
        private Action<StateSO> _onPickExisting;
        private Action _onCreateEmbedded;
        private Action _onCreateSubStateMachine;
        private Action _onCreateFile;

        public void Setup(
            HashSet<StateSO> alreadyInTable,
            Action<StateSO> onPickExisting,
            Action onCreateEmbedded,
            Action onCreateSubStateMachine,
            Action onCreateFile)
        {
            _alreadyInTable = alreadyInTable;
            _onPickExisting = onPickExisting;
            _onCreateEmbedded = onCreateEmbedded;
            _onCreateSubStateMachine = onCreateSubStateMachine;
            _onCreateFile = onCreateFile;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Add State"), 0),
                new SearchTreeEntry(new GUIContent("Create New State (Sub-Asset)")) { level = 1, userData = CreateEmbedded },
                new SearchTreeEntry(new GUIContent("Create New Sub State Machine (Sub-Asset)")) { level = 1, userData = CreateSubStateMachine },
                new SearchTreeEntry(new GUIContent("Create New State as File...")) { level = 1, userData = CreateFile },
                new SearchTreeGroupEntry(new GUIContent("Existing States"), 1)
            };

            // States in their own file sit right under Existing States; states embedded in a table's
            // file (e.g. a shared Glory sub-state machine) are grouped under that file's name.
            var ownFile = new List<StateSO>();
            var embedded = new SortedDictionary<string, List<StateSO>>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in FilesWithStates())
            {
                Object main = AssetDatabase.LoadMainAssetAtPath(path);
                foreach (StateSO state in AssetDatabase.LoadAllAssetsAtPath(path).OfType<StateSO>())
                {
                    if (_alreadyInTable.Contains(state))
                    {
                        continue;
                    }

                    if (state == main)
                    {
                        ownFile.Add(state);
                        continue;
                    }

                    string file = main != null ? main.name : Path.GetFileNameWithoutExtension(path);
                    if (!embedded.TryGetValue(file, out List<StateSO> list))
                    {
                        list = new List<StateSO>();
                        embedded[file] = list;
                    }

                    list.Add(state);
                }
            }

            foreach (KeyValuePair<string, List<StateSO>> file in embedded)
            {
                tree.Add(new SearchTreeGroupEntry(new GUIContent(file.Key), 2));
                AddStates(tree, file.Value, 3);
            }

            AddStates(tree, ownFile, 2);

            return tree;
        }

        private static void AddStates(List<SearchTreeEntry> tree, List<StateSO> states, int level)
        {
            states.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            foreach (StateSO state in states)
            {
                tree.Add(new SearchTreeEntry(new GUIContent(state.name)) { level = level, userData = state });
            }
        }

        // Files that are a state, or a table whose file may hold embedded states.
        private static IEnumerable<string> FilesWithStates()
        {
            return AssetDatabase.FindAssets($"t:{nameof(StateSO)}")
                .Concat(AssetDatabase.FindAssets($"t:{nameof(TransitionTableSO)}"))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct();
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            switch (entry.userData)
            {
                case StateSO state:
                    _onPickExisting?.Invoke(state);
                    break;
                case CreateEmbedded:
                    _onCreateEmbedded?.Invoke();
                    break;
                case CreateSubStateMachine:
                    _onCreateSubStateMachine?.Invoke();
                    break;
                case CreateFile:
                    _onCreateFile?.Invoke();
                    break;
            }

            // true closes the popup.
            return true;
        }
    }
}

#endif
