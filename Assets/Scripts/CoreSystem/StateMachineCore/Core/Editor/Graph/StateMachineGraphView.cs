#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // The canvas that shows a TransitionTableSO as nodes and edges.
    // Node positions are saved here; states and conditions are edited in GraphInspectorPanel.
    // Adding / removing states and transitions is in StateMachineGraphView.Editing.cs.
    public partial class StateMachineGraphView : GraphView
    {
        private const string StyleSheetName = "StateMachineGraph";

        private readonly EditorWindow _window;

        private TransitionTableSO _table;
        private SerializedObject _serializedTable;

        // True while Load() rebuilds the view, so removing old elements isn't treated as a user delete.
        private bool _isLoading;

        // Play Mode: name of the state the running StateMachine is in (StateSO.name), or null.
        private string _activeStateName;

        // The single selected element (state node or edge), or null when nothing or several things are selected.
        public event Action<GraphElement> SelectionChanged;

        public StateMachineGraphView(EditorWindow window)
        {
            _window = window;
            graphViewChanged = OnGraphViewChanged;
            RegisterTransitionCreationCallbacks();

            // Mouse wheel zoom.
            SetupZoom(
                ContentZoomer.DefaultMinScale,
                ContentZoomer.DefaultMaxScale);

            // Order matters: the first manipulator gets the mouse event first.
            this.AddManipulator(new ContentDragger());     // Middle mouse / Alt + drag to pan.
            this.AddManipulator(new SelectionDragger());   // Drag selected nodes.
            this.AddManipulator(new RectangleSelector());  // Drag a box to select.
            this.AddManipulator(new ClickSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // GridBackground needs a stylesheet for its colors, otherwise the lines are almost invisible.
            StyleSheet styleSheet = LoadStyleSheet();
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }
        }

        private static StyleSheet LoadStyleSheet()
        {
            // Find by name so the Graph folder can be moved without breaking the path.
            foreach (string guid in AssetDatabase.FindAssets($"{StyleSheetName} t:StyleSheet"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == StyleSheetName)
                {
                    return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                }
            }

            Debug.LogWarning($"{StyleSheetName}.uss not found. The graph will have no grid colors.");
            return null;
        }

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            NotifySelectionChanged();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            NotifySelectionChanged();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            NotifySelectionChanged();
        }

        private void NotifySelectionChanged()
        {
            GraphElement element = selection.Count == 1
                ? selection[0] as GraphElement
                : null;

            SelectionChanged?.Invoke(element);
        }

        // Rebuilds the current table and selects the same node or edge again if it still exists,
        // so Undo or a data change doesn't close the inspector panel.
        public void Reload()
        {
            GraphElement previous = selection.Count == 1 ? selection[0] as GraphElement : null;

            Load(_table);

            GraphElement match = previous switch
            {
                Node node => FindNode(GetNodeKey(node)),
                TransitionEdgeView edge => FindEdge(edge),
                _ => null
            };

            if (match != null)
            {
                AddToSelection(match);
            }
        }

        // Called from the panel's transition lists. fromKey is a StateSO, or SpecialNodeView.Kind.AnyState.
        public void SelectTransitionEdge(object fromKey, StateSO to)
        {
            SelectEdge(fromKey, to);
        }

        // Keys as returned by GetNodeKey: a StateSO, or a SpecialNodeView.Kind.
        private void SelectEdge(object fromKey, object toKey)
        {
            foreach (GraphElement element in graphElements.ToList())
            {
                if (element is TransitionEdgeView edge &&
                    Equals(GetNodeKey(edge.From), fromKey) &&
                    Equals(GetNodeKey(edge.To), toKey))
                {
                    ClearSelection();
                    AddToSelection(edge);
                    return;
                }
            }
        }

        private Node FindNode(object key)
        {
            foreach (GraphElement element in graphElements.ToList())
            {
                if (element is Node node && Equals(GetNodeKey(node), key))
                {
                    return node;
                }
            }

            return null;
        }

        private TransitionEdgeView FindEdge(TransitionEdgeView previous)
        {
            // The Entry edge has a new target after the initial state changes, so match it by From only.
            bool wasEntryEdge = previous.From is SpecialNodeView { NodeKind: SpecialNodeView.Kind.Entry };

            foreach (GraphElement element in graphElements.ToList())
            {
                if (element is TransitionEdgeView edge &&
                    edge.IsAnyState == previous.IsAnyState &&
                    Equals(GetNodeKey(edge.From), GetNodeKey(previous.From)) &&
                    (wasEntryEdge || Equals(GetNodeKey(edge.To), GetNodeKey(previous.To))))
                {
                    return edge;
                }
            }

            return null;
        }

        // What a node stands for, stable across reloads: its StateSO, or Entry / Any State.
        private static object GetNodeKey(Node node)
        {
            return node switch
            {
                StateNodeView stateNode => stateNode.State,
                SpecialNodeView specialNode => specialNode.NodeKind,
                _ => null
            };
        }

        public void Load(TransitionTableSO table)
        {
            _table = table;

            // Clear whatever was shown for the previous table.
            // ToList so we don't modify the collection while iterating it.
            CancelTransitionCreation();
            ClearSelection();

            _isLoading = true;
            DeleteElements(graphElements.ToList());
            _isLoading = false;

            _serializedTable = null;

            if (_table == null)
            {
                return;
            }

            // TransitionTableSO fields are private, so read them the same way the Inspector does.
            _serializedTable = new SerializedObject(_table);
            var initialState =
                _serializedTable.FindProperty("_initialState").objectReferenceValue as StateSO;

            SpecialNodeView entryNode = CreateSpecialNode(
                SpecialNodeView.Kind.Entry,
                _serializedTable.FindProperty("_entryNodePosition").vector2Value);
            SpecialNodeView anyStateNode = CreateSpecialNode(
                SpecialNodeView.Kind.AnyState,
                _serializedTable.FindProperty("_anyStateNodePosition").vector2Value);
            AddElement(entryNode);
            AddElement(anyStateNode);

            List<StateSO> states = CollectStates(_serializedTable, initialState);
            Dictionary<StateSO, Vector2> savedPositions = ReadNodePositions(_serializedTable);
            var stateNodes = new Dictionary<StateSO, StateNodeView>();

            for (int i = 0; i < states.Count; i++)
            {
                // States never dragged yet fall back to the grid.
                if (!savedPositions.TryGetValue(states[i], out Vector2 position))
                {
                    position = GetGridPosition(i);
                }

                var node = new StateNodeView(states[i], states[i] == initialState);
                node.SetPosition(new Rect(position, Vector2.zero));
                AddElement(node);
                stateNodes.Add(states[i], node);
            }

            CreateEdges(_serializedTable, initialState, entryNode, anyStateNode, stateNodes);

            // New nodes start un-highlighted, so apply the Play Mode highlight again.
            ApplyActiveState();
        }

        // Called by the window while in Play Mode. The runtime State only knows its StateSO's name.
        public void SetActiveState(string stateName)
        {
            if (stateName == _activeStateName)
            {
                return;
            }

            _activeStateName = stateName;
            ApplyActiveState();
        }

        private void ApplyActiveState()
        {
            foreach (GraphElement element in graphElements.ToList())
            {
                if (element is StateNodeView stateNode)
                {
                    stateNode.SetActive(_activeStateName != null && stateNode.State.name == _activeStateName);
                }
            }
        }

        private static Dictionary<StateSO, Vector2> ReadNodePositions(SerializedObject serializedTable)
        {
            var positions = new Dictionary<StateSO, Vector2>();
            SerializedProperty nodePositions = serializedTable.FindProperty("_nodePositions");

            for (int i = 0; i < nodePositions.arraySize; i++)
            {
                SerializedProperty item = nodePositions.GetArrayElementAtIndex(i);
                var state = item.FindPropertyRelative("State").objectReferenceValue as StateSO;

                if (state != null)
                {
                    positions[state] = item.FindPropertyRelative("Position").vector2Value;
                }
            }

            return positions;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_serializedTable == null || _isLoading)
            {
                return change;
            }

            // movedElements is filled when the user lets go after dragging nodes.
            if (change.movedElements != null && change.movedElements.Count > 0)
            {
                SavePositions(change.movedElements);
            }

            // elementsToRemove is filled by the Delete key or the "Delete" menu item.
            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                RemoveFromTable(change.elementsToRemove);

                // Array indexes shifted, so rebuild. Next frame, since GraphView is still removing elements.
                schedule.Execute(Reload);
            }

            return change;
        }

        private void SavePositions(List<GraphElement> movedElements)
        {
            // Pick up any change made in the Inspector since we loaded.
            _serializedTable.Update();

            SerializedProperty nodePositions = _serializedTable.FindProperty("_nodePositions");

            foreach (GraphElement element in movedElements)
            {
                Vector2 position = element.GetPosition().position;

                switch (element)
                {
                    case StateNodeView stateNode:
                        FindOrAddNodePosition(nodePositions, stateNode.State)
                            .FindPropertyRelative("Position").vector2Value = position;
                        break;

                    case SpecialNodeView { NodeKind: SpecialNodeView.Kind.Entry }:
                        _serializedTable.FindProperty("_entryNodePosition").vector2Value = position;
                        break;

                    case SpecialNodeView { NodeKind: SpecialNodeView.Kind.AnyState }:
                        _serializedTable.FindProperty("_anyStateNodePosition").vector2Value = position;
                        break;
                }
            }

            // Marks the asset dirty and records Undo. Saved to disk with Ctrl+S like any asset.
            _serializedTable.ApplyModifiedProperties();
        }

        private static SerializedProperty FindOrAddNodePosition(SerializedProperty nodePositions, StateSO state)
        {
            for (int i = 0; i < nodePositions.arraySize; i++)
            {
                SerializedProperty item = nodePositions.GetArrayElementAtIndex(i);
                if (item.FindPropertyRelative("State").objectReferenceValue == state)
                {
                    return item;
                }
            }

            nodePositions.arraySize++;
            SerializedProperty newItem = nodePositions.GetArrayElementAtIndex(nodePositions.arraySize - 1);
            newItem.FindPropertyRelative("State").objectReferenceValue = state;
            return newItem;
        }

        private void CreateEdges(
            SerializedObject serializedTable,
            StateSO initialState,
            Node entryNode,
            Node anyStateNode,
            Dictionary<StateSO, StateNodeView> stateNodes)
        {
            if (initialState != null)
            {
                AddElement(new TransitionEdgeView(
                    entryNode, stateNodes[initialState], new List<int>(), false));
            }

            // Local transitions: one edge per From -> To pair, remembering which array items it covers.
            var localGroups = new Dictionary<(StateSO from, StateSO to), List<int>>();
            SerializedProperty transitions = serializedTable.FindProperty("_transitions");

            for (int i = 0; i < transitions.arraySize; i++)
            {
                SerializedProperty item = transitions.GetArrayElementAtIndex(i);
                var from = item.FindPropertyRelative("FromState").objectReferenceValue as StateSO;
                var to = item.FindPropertyRelative("ToState").objectReferenceValue as StateSO;

                // Skip broken items and self transitions (nothing sensible to draw).
                if (from == null || to == null || from == to)
                {
                    continue;
                }

                if (!localGroups.TryGetValue((from, to), out List<int> indices))
                {
                    indices = new List<int>();
                    localGroups.Add((from, to), indices);
                }

                indices.Add(i);
            }

            foreach (KeyValuePair<(StateSO from, StateSO to), List<int>> group in localGroups)
            {
                AddElement(new TransitionEdgeView(
                    stateNodes[group.Key.from], stateNodes[group.Key.to], group.Value, false));
            }

            // Any State transitions: grouped by target only.
            var anyGroups = new Dictionary<StateSO, List<int>>();
            SerializedProperty anyTransitions = serializedTable.FindProperty("_anyTransitions");

            for (int i = 0; i < anyTransitions.arraySize; i++)
            {
                var to = anyTransitions.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("ToState").objectReferenceValue as StateSO;

                if (to == null)
                {
                    continue;
                }

                if (!anyGroups.TryGetValue(to, out List<int> indices))
                {
                    indices = new List<int>();
                    anyGroups.Add(to, indices);
                }

                indices.Add(i);
            }

            foreach (KeyValuePair<StateSO, List<int>> group in anyGroups)
            {
                AddElement(new TransitionEdgeView(
                    anyStateNode, stateNodes[group.Key], group.Value, true));
            }
        }

        private static SpecialNodeView CreateSpecialNode(SpecialNodeView.Kind kind, Vector2 position)
        {
            var node = new SpecialNodeView(kind);
            node.SetPosition(new Rect(position, Vector2.zero));
            return node;
        }

        // Every StateSO used anywhere in the table, without duplicates, in the order found.
        // The initial state goes first so it ends up at the top-left of the grid.
        // States in _nodePositions count too, so a state added from the graph shows up
        // before it has any transitions.
        private static List<StateSO> CollectStates(SerializedObject serializedTable, StateSO initialState)
        {
            var states = new List<StateSO>();
            var seen = new HashSet<StateSO>();

            void Add(StateSO state)
            {
                if (state != null && seen.Add(state))
                {
                    states.Add(state);
                }
            }

            Add(initialState);

            SerializedProperty transitions = serializedTable.FindProperty("_transitions");
            for (int i = 0; i < transitions.arraySize; i++)
            {
                SerializedProperty item = transitions.GetArrayElementAtIndex(i);
                Add(item.FindPropertyRelative("FromState").objectReferenceValue as StateSO);
                Add(item.FindPropertyRelative("ToState").objectReferenceValue as StateSO);
            }

            SerializedProperty anyTransitions = serializedTable.FindProperty("_anyTransitions");
            for (int i = 0; i < anyTransitions.arraySize; i++)
            {
                SerializedProperty item = anyTransitions.GetArrayElementAtIndex(i);
                Add(item.FindPropertyRelative("ToState").objectReferenceValue as StateSO);
            }

            SerializedProperty nodePositions = serializedTable.FindProperty("_nodePositions");
            for (int i = 0; i < nodePositions.arraySize; i++)
            {
                SerializedProperty item = nodePositions.GetArrayElementAtIndex(i);
                Add(item.FindPropertyRelative("State").objectReferenceValue as StateSO);
            }

            return states;
        }

        // Default layout for states that have no saved position yet.
        private static Vector2 GetGridPosition(int index)
        {
            const int Columns = 4;
            const float StartX = 250f;
            const float SpacingX = 220f;
            const float SpacingY = 110f;

            int column = index % Columns;
            int row = index / Columns;
            return new Vector2(StartX + column * SpacingX, row * SpacingY);
        }
    }
}

#endif
