#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // The searchable popup behind "Add State...": create a new StateSO, or pick an existing one.
    // SearchWindow needs the provider to be a ScriptableObject.
    public class StateSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private HashSet<StateSO> _alreadyInTable;
        private Action<StateSO> _onPickExisting;
        private Action _onCreateNew;

        public void Setup(HashSet<StateSO> alreadyInTable, Action<StateSO> onPickExisting, Action onCreateNew)
        {
            _alreadyInTable = alreadyInTable;
            _onPickExisting = onPickExisting;
            _onCreateNew = onCreateNew;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Add State"), 0),
                new SearchTreeEntry(new GUIContent("Create New State...")) { level = 1 },
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
            if (entry.userData is StateSO state)
            {
                _onPickExisting?.Invoke(state);
            }
            else
            {
                _onCreateNew?.Invoke();
            }

            // true closes the popup.
            return true;
        }
    }
}

#endif
