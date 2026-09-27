#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Editor window that hosts the graph. Opened by double-clicking a TransitionTableSO.
    // Double-clicking a sub-state machine node goes into its table; the breadcrumbs go back out.
    // Play Mode highlighting is in StateMachineGraphWindow.PlayMode.cs.
    public partial class StateMachineGraphWindow : EditorWindow
    {
        // The table shown now (always the last item of _path).
        // Serialized so the window stays where it was after a script recompile.
        [SerializeField]
        private TransitionTableSO _table;

        // Sub-state machine navigation: _path[0] is the table that was opened, each next one is a
        // sub-state machine entered from the one before. _pathStates[i] is the state that led into
        // _path[i] (null for the first).
        [SerializeField]
        private List<TransitionTableSO> _path = new List<TransitionTableSO>();

        [SerializeField]
        private List<StateSO> _pathStates = new List<StateSO>();

        private StateMachineGraphView _graphView;
        private GraphInspectorPanel _inspectorPanel;
        private ToolbarBreadcrumbs _breadcrumbs;

        // The table at the top of the navigation path, the one a StateMachineController runs.
        private TransitionTableSO RootTable => _path.Count > 0 ? _path[0] : _table;

        [OnOpenAsset]
        private static bool OnOpenAsset(EntityId entityId, int line)
        {
            if (EditorUtility.EntityIdToObject(entityId) is not TransitionTableSO table)
            {
                // Not ours, let Unity open it normally.
                return false;
            }

            Open(table);
            return true;
        }

        public static void Open(TransitionTableSO table)
        {
            var window = GetWindow<StateMachineGraphWindow>();
            window.titleContent = new GUIContent("State Machine Graph");
            window.SetTable(table);
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += Reload;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Reload;
            StopWatching();
        }

        private void CreateGUI()
        {
            var toolbar = new Toolbar();
            _breadcrumbs = new ToolbarBreadcrumbs();
            toolbar.Add(_breadcrumbs);
            CreatePlayModeToolbarItems(toolbar);
            rootVisualElement.Add(toolbar);

            _graphView = new StateMachineGraphView(this);
            _inspectorPanel = new GraphInspectorPanel(Reload, _graphView.SelectTransitionEdge);
            _graphView.SelectionChanged += _inspectorPanel.ShowSelection;
            _graphView.SubStateMachineOpenRequested += EnterSubStateMachine;

            // Graph on the left, panel on the right with a draggable divider. Panel starts 320px wide.
            var split = new TwoPaneSplitView(1, 320f, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            split.Add(_graphView);
            split.Add(_inspectorPanel);
            rootVisualElement.Add(split);

            ShowTable();
        }

        // Opened from the Project window: starts a new navigation path.
        private void SetTable(TransitionTableSO table)
        {
            _path.Clear();
            _pathStates.Clear();
            _path.Add(table);
            _pathStates.Add(null);

            // A new root table may be run by a different GameObject.
            StopWatching();

            _table = table;
            ShowTable();
        }

        private void EnterSubStateMachine(StateSO state, TransitionTableSO subTable)
        {
            SetFollow(false);

            // Already on the path (a table that contains itself): just go back up to it.
            int existing = _path.IndexOf(subTable);
            if (existing >= 0)
            {
                NavigateTo(existing);
                return;
            }

            _path.Add(subTable);
            _pathStates.Add(state);

            _table = subTable;
            ShowTable();
        }

        // Breadcrumb click: go back up to that level.
        private void NavigateTo(int level)
        {
            SetFollow(false);

            int removeCount = _path.Count - (level + 1);
            _path.RemoveRange(level + 1, removeCount);
            _pathStates.RemoveRange(level + 1, removeCount);

            _table = _path[level];
            ShowTable();
        }

        // A different table (or the first one) was opened.
        private void ShowTable()
        {
            // CreateGUI may not have run yet.
            if (_graphView == null)
            {
                return;
            }

            // Windows saved before navigation existed have a table but no path.
            if (_path.Count == 0 && _table != null)
            {
                _path.Add(_table);
                _pathStates.Add(null);
            }

            RebuildBreadcrumbs();

            _inspectorPanel.SetTable(_table);
            _graphView.Load(_table);
        }

        // PlayerAction_TransitionTable > PlayerGun_State (PlayerGun_TransitionTable)
        private void RebuildBreadcrumbs()
        {
            _breadcrumbs.Clear();

            if (_table == null)
            {
                _breadcrumbs.PushItem("No Transition Table (double-click one in the Project window)");
                return;
            }

            for (int i = 0; i < _path.Count; i++)
            {
                TransitionTableSO table = _path[i];
                string tableName = table != null ? table.name : "Missing";

                string label = i == 0 || _pathStates[i] == null
                    ? tableName
                    : $"{_pathStates[i].name} ({tableName})";

                // The current level isn't clickable.
                int level = i;
                Action onClick = i < _path.Count - 1 ? () => NavigateTo(level) : null;
                _breadcrumbs.PushItem(label, onClick);
            }
        }

        // Same table, but its data changed (Undo/Redo, new initial state, ...).
        private void Reload()
        {
            _graphView?.Reload();
        }
    }
}

#endif
