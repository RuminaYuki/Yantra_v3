#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // The searchable popup the Parameters panel uses to pick an [Exposed] field (like "Add State..."):
    // Actions / Conditions → the SO → its fields. Type to filter when the table has many.
    // Fields already bound to a parameter show ✓ and can't be picked.
    // SearchWindow needs the provider to be a ScriptableObject.
    public class FieldSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public struct Choice
        {
            public StateElementSO Item;
            public string FieldName;
            public string Label;
            public bool IsTaken;
        }

        private string _title;
        private List<Choice> _choices;
        private Action<StateElementSO, string> _onPicked;

        public void Setup(string title, List<Choice> choices, Action<StateElementSO, string> onPicked)
        {
            _title = title;
            _choices = choices;
            _onPicked = onPicked;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry> { new SearchTreeGroupEntry(new GUIContent(_title), 0) };

            if (_choices == null || _choices.Count == 0)
            {
                tree.Add(new SearchTreeEntry(new GUIContent("No matching [Exposed] fields")) { level = 1 });
                return tree;
            }

            AddGroup(tree, "Actions", _choices.Where(choice => choice.Item is StateActionSO));
            AddGroup(tree, "Conditions", _choices.Where(choice => choice.Item is StateConditionSO));
            return tree;
        }

        private static void AddGroup(List<SearchTreeEntry> tree, string title, IEnumerable<Choice> choices)
        {
            List<IGrouping<StateElementSO, Choice>> byItem = choices
                .GroupBy(choice => choice.Item)
                .OrderBy(group => group.Key.name)
                .ToList();

            if (byItem.Count == 0)
            {
                return;
            }

            tree.Add(new SearchTreeGroupEntry(new GUIContent(title), 1));

            foreach (IGrouping<StateElementSO, Choice> item in byItem)
            {
                tree.Add(new SearchTreeGroupEntry(new GUIContent(item.Key.name), 2));

                foreach (Choice choice in item)
                {
                    tree.Add(new SearchTreeEntry(new GUIContent(choice.IsTaken ? $"✓ {choice.Label} (bound)" : choice.Label))
                    {
                        level = 3,
                        userData = choice.IsTaken ? null : (object)choice
                    });
                }
            }
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            // Taken fields and the "nothing here" line keep the popup open.
            if (entry.userData is not Choice choice)
            {
                return false;
            }

            _onPicked?.Invoke(choice.Item, choice.FieldName);
            return true;
        }
    }
}

#endif
