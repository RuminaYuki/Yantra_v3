#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // The searchable popup behind "Add State...": create a new StateSO (inside the table's file, or
    // as its own file to share), or pick an existing one.
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

            var states = new List<StateSO>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(StateSO)}"))
            {
                var state = AssetDatabase.LoadAssetAtPath<StateSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (state != null && !_alreadyInTable.Contains(state))
                {
                    states.Add(state);
                }
            }

            states.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

            foreach (StateSO state in states)
            {
                tree.Add(new SearchTreeEntry(new GUIContent(state.name)) { level = 2, userData = state });
            }

            return tree;
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
