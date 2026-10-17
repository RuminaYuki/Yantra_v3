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
    // Actions / Conditions → the SO → its fields, for the open table. The other tables in its file
    // (the Blackboard is shared by them all) wait under Other Tables → the table → Actions / Conditions,
    // so the first page stays short. Typing searches all of them.
    // Fields already bound to a parameter show ✓ and can't be picked.
    // SearchWindow needs the provider to be a ScriptableObject.
    public class FieldSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        // Under Other Tables, last: things in the file that no table uses.
        public const string UnusedTable = "(Unused)";

        public struct Choice
        {
            public StateElementSO Item;
            public string FieldName;
            public string Label;
            public bool IsTaken;
            public string Table;   // null: the open table; otherwise listed under Other Tables
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

            List<Choice> own = _choices.Where(choice => choice.Table == null).ToList();
            AddKinds(tree, own, 1);

            List<IGrouping<string, Choice>> others = _choices
                .Where(choice => choice.Table != null)
                .GroupBy(choice => choice.Table)
                .OrderBy(group => group.Key == UnusedTable ? 1 : 0)
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .ToList();

            if (others.Count > 0)
            {
                tree.Add(new SearchTreeGroupEntry(new GUIContent("Other Tables"), 1));
                foreach (IGrouping<string, Choice> table in others)
                {
                    tree.Add(new SearchTreeGroupEntry(new GUIContent(table.Key), 2));
                    AddKinds(tree, table, 3);
                }
            }

            return tree;
        }

        // Actions, then Conditions, at `level`.
        private static void AddKinds(List<SearchTreeEntry> tree, IEnumerable<Choice> choices, int level)
        {
            List<Choice> list = choices.ToList();
            AddGroup(tree, "Actions", list.Where(choice => choice.Item is StateActionSO), level);
            AddGroup(tree, "Conditions", list.Where(choice => choice.Item is StateConditionSO), level);
        }

        // The group at `level`, each SO one deeper, its fields one deeper again.
        private static void AddGroup(List<SearchTreeEntry> tree, string title, IEnumerable<Choice> choices, int level)
        {
            List<IGrouping<StateElementSO, Choice>> byItem = choices
                .GroupBy(choice => choice.Item)
                .OrderBy(group => group.Key.name)
                .ToList();

            if (byItem.Count == 0)
            {
                return;
            }

            tree.Add(new SearchTreeGroupEntry(new GUIContent(title), level));

            foreach (IGrouping<StateElementSO, Choice> item in byItem)
            {
                tree.Add(new SearchTreeGroupEntry(new GUIContent(item.Key.name), level + 1));

                foreach (Choice choice in item)
                {
                    tree.Add(new SearchTreeEntry(new GUIContent(choice.IsTaken ? $"✓ {choice.Label} (bound)" : choice.Label))
                    {
                        level = level + 2,
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
