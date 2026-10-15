#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace SDFcl.GamePlay.QuestSystem.GraphEditor
{
    // Space / right-click "Create Node" search window.
    public class QuestSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public enum Kind
        {
            MainQuest,
            SupQuest,
            And,
            Or,
            Reset
        }

        // Called with what was picked and the screen position the search was opened at.
        public Action<Kind, Vector2> OnPick;

        private Texture2D _indent;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            if (_indent == null)
            {
                // Transparent icon so entries line up like Unity's own search windows.
                _indent = new Texture2D(1, 1);
                _indent.SetPixel(0, 0, Color.clear);
                _indent.Apply();
            }

            return new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create Quest Node"), 0),
                new SearchTreeGroupEntry(new GUIContent("Quest"), 1),
                new SearchTreeEntry(new GUIContent("Main Quest", _indent)) { level = 2, userData = Kind.MainQuest },
                new SearchTreeEntry(new GUIContent("Sup Quest", _indent)) { level = 2, userData = Kind.SupQuest },
                new SearchTreeGroupEntry(new GUIContent("Gate"), 1),
                new SearchTreeEntry(new GUIContent("AND (all inputs)", _indent)) { level = 2, userData = Kind.And },
                new SearchTreeEntry(new GUIContent("OR (any input)", _indent)) { level = 2, userData = Kind.Or },
                new SearchTreeEntry(new GUIContent("Reset (loop a chain)", _indent)) { level = 1, userData = Kind.Reset },
            };
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (!(entry.userData is Kind kind)) return false;
            OnPick?.Invoke(kind, context.screenMousePosition);
            return true;
        }

        private void OnDestroy()
        {
            if (_indent != null) DestroyImmediate(_indent);
        }
    }
}

#endif
