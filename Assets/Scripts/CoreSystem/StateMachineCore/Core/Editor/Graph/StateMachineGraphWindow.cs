#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Editor window that hosts the graph. Opened by double-clicking a TransitionTableSO.
    // Double-clicking a sub-state machine node goes into its table; the breadcrumbs go back out.
    // Play Mode highlighting is in StateMachineGraphWindow.PlayMode.cs.
    // Several can be open at once (e.g. a Base and its variant side by side): right-click a table >
    // Open in New State Machine Graph, or the window's ⋮ menu > Open in New Window.
    public partial class StateMachineGraphWindow : EditorWindow, IHasCustomMenu
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
        private ToolbarBreadcrumbs _breadcrumbs;
        private ContentsPanel _contentsPanel;

        // Exposed parameters panel floating over the graph (like VFX Graph's Blackboard).
        private ParameterBlackboard _blackboard;

        [SerializeField]
        private bool _showParameters = true;

        // What Unity's Inspector shows while an arrow is selected. See TransitionSelection.
        // The Inspector stand-in for the selected arrow / Base state (TransitionSelection, StateOverrideSelection).
        private ScriptableObject _transitionSelection;

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

        // Double-click: a window already showing the table comes to the front; otherwise the last used
        // window shows it (like before there could be several).
        public static void Open(TransitionTableSO table)
        {
            foreach (StateMachineGraphWindow open in Resources.FindObjectsOfTypeAll<StateMachineGraphWindow>())
            {
                if (open.RootTable == table)
                {
                    open.Focus();
                    return;
                }
            }

            var window = GetWindow<StateMachineGraphWindow>();
            window.SetTable(table);
        }

        // Another window, as a tab next to the open ones; drag its tab aside to see both at once.
        public static void OpenInNewWindow(TransitionTableSO table)
        {
            var window = CreateWindow<StateMachineGraphWindow>(typeof(StateMachineGraphWindow));
            window.SetTable(table);
        }

        private const string NewWindowMenuPath = "Assets/Open in New State Machine Graph";

        [MenuItem(NewWindowMenuPath, false, 19)]
        private static void OpenSelectionInNewWindow()
        {
            OpenInNewWindow((TransitionTableSO)Selection.activeObject);
        }

        [MenuItem(NewWindowMenuPath, true)]
        private static bool CanOpenSelectionInNewWindow()
        {
            return Selection.activeObject is TransitionTableSO;
        }

        // The ⋮ menu at the window's top right.
        public void AddItemsToMenu(GenericMenu menu)
        {
            if (RootTable != null)
            {
                menu.AddItem(new GUIContent("Open in New Window"), false, () => OpenInNewWindow(RootTable));
            }
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += Reload;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Reload;
            StopWatching();
            DestroyImmediate(_transitionSelection);
        }

        private void CreateGUI()
        {
            var toolbar = new Toolbar();
            _breadcrumbs = new ToolbarBreadcrumbs();
            toolbar.Add(_breadcrumbs);
            CreatePlayModeToolbarItems(toolbar);
            rootVisualElement.Add(toolbar);

            _graphView = new StateMachineGraphView(this);
            _graphView.SelectionChanged += ShowInUnityInspector;
            _graphView.SubStateMachineOpenRequested += EnterSubStateMachine;

            // Added to the view itself, not as a graph element, so it doesn't pan/zoom
            // and Load() doesn't clear it.
            _blackboard = new ParameterBlackboard(_graphView);
            _graphView.Add(_blackboard);
            CreateParametersToggle(toolbar);

            _contentsPanel = new ContentsPanel(
                this,
                ShowContentsItem,
                state => _graphView.AddStateAtViewCenter(state),
                EnterSubStateMachine);

            // Contents on the left, graph on the right, with a draggable divider. Contents starts 220px wide.
            // Details of the selection show in Unity's own Inspector (see ShowInUnityInspector).
            var split = new TwoPaneSplitView(0, 220f, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            split.Add(_contentsPanel);

            // Renaming on a node uses the Contents panel's rename, so both work the same way.
            _graphView.StateRenameRequested += (state, newName) => _contentsPanel.RenameFromGraph(state, newName);
            split.Add(_graphView);
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

            // The tab says which table, so two windows can be told apart.
            titleContent = new GUIContent(table != null ? table.name : "State Machine Graph", "State Machine Graph");

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

            _contentsPanel.SetTable(_table);
            _graphView.Load(_table);
            _blackboard.SetTable(_table);
        }

        private void CreateParametersToggle(Toolbar toolbar)
        {
            var toggle = new ToolbarToggle
            {
                text = "Parameters",
                tooltip = "Show the exposed parameters of this table."
            };
            toggle.SetValueWithoutNotify(_showParameters);
            toggle.RegisterValueChangedCallback(evt =>
            {
                _showParameters = evt.newValue;
                _blackboard.style.display = _showParameters ? DisplayStyle.Flex : DisplayStyle.None;
                _graphView.ShowParameterTags = _showParameters;
            });
            toolbar.Add(toggle);

            _blackboard.style.display = _showParameters ? DisplayStyle.Flex : DisplayStyle.None;
            _graphView.ShowParameterTags = _showParameters;
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
            _contentsPanel?.Rebuild();
            _blackboard?.Rebuild();
        }

        // Like Animator: the selected state or arrow also shows in Unity's own Inspector.
        private void ShowInUnityInspector(GraphElement element)
        {
            if (element is StateNodeView baseState && _graphView.IsBaseStateHere(baseState.State))
            {
                // A Base's state in a variant table: its overrides here, not the shared state itself.
                SelectStandIn(StateOverrideSelection.Create(
                    _table, StateMachineGraphView.SafeBaseOf(_table), baseState.State));
            }
            else if (element is StateNodeView stateNode && stateNode.State != null)
            {
                Selection.activeObject = stateNode.State;
            }
            else if (element is TransitionEdgeView inheritedEdge && inheritedEdge.InheritedTransitions.Count > 0)
            {
                // A Base's arrow in a variant table: what this table does with it, not the Base's data.
                SelectStandIn(TransitionOverrideSelection.Create(
                    _table, StateMachineGraphView.SafeBaseOf(_table), inheritedEdge));
            }
            else if (element is TransitionEdgeView edge)
            {
                // Including the faint Entry arrow of a variant: setting an initial state here overrides the Base's.
                SelectStandIn(TransitionSelection.Create(_table, edge));
            }
            else if (element is SpecialNodeView { NodeKind: SpecialNodeView.Kind.AnyState })
            {
                SelectStandIn(TransitionSelection.CreateForAnyState(_table));
            }
        }

        // A row clicked in the Contents panel. A state selects its node, which also shows it in
        // the Inspector; anything else (or a state with no node) goes to the Inspector directly.
        private void ShowContentsItem(UnityEngine.Object item)
        {
            if (item is StateSO state && _graphView.SelectStateNode(state))
            {
                return;
            }

            Selection.activeObject = item;
        }

        // A new stand-in per click, so the Inspector rebuilds for the new selection.
        private void SelectStandIn(ScriptableObject selection)
        {
            ScriptableObject previous = _transitionSelection;
            _transitionSelection = selection;
            Selection.activeObject = selection;
            DestroyImmediate(previous);
        }

        // An override changed in the Inspector: only the "✎ changes" labels on the nodes need redrawing
        // (a full reload would reselect the node and rebuild the Inspector mid-edit).
        public static void RefreshOverrideLabels()
        {
            foreach (StateMachineGraphWindow window in Resources.FindObjectsOfTypeAll<StateMachineGraphWindow>())
            {
                window._graphView?.RefreshActionChangeLabels();
            }
        }

        // Enters the sub-state machine in every open graph showing `table` (e.g. Open in a state's Inspector).
        public static void OpenSubStateMachine(TransitionTableSO table, StateSO state, TransitionTableSO subTable)
        {
            foreach (StateMachineGraphWindow window in Resources.FindObjectsOfTypeAll<StateMachineGraphWindow>())
            {
                if (window._table == table)
                {
                    window.EnterSubStateMachine(state, subTable);
                }
            }
        }

        // A table was changed from outside the graph (e.g. reordered in the StateSO Inspector).
        public static void ReloadAll()
        {
            foreach (StateMachineGraphWindow window in Resources.FindObjectsOfTypeAll<StateMachineGraphWindow>())
            {
                window.Reload();
            }
        }

        // Selects the from → to arrow in every open graph showing this table
        // (e.g. a transition row clicked in the Inspector). fromKey is a StateSO, or SpecialNodeView.Kind.AnyState.
        public static void SelectTransition(TransitionTableSO table, object fromKey, StateSO to)
        {
            foreach (StateMachineGraphWindow window in Resources.FindObjectsOfTypeAll<StateMachineGraphWindow>())
            {
                if (window._table == table)
                {
                    window._graphView?.SelectTransitionEdge(fromKey, to);
                }
            }
        }

    }
}

#endif
