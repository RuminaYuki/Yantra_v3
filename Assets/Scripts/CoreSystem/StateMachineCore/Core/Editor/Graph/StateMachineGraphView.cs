#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // The canvas that shows a TransitionTableSO as nodes and edges.
    // Node positions are saved here; states and conditions are edited in Unity's Inspector (StateSOEditor, TransitionSelectionEditor).
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

        // Follow mode camera: seconds to scroll to the highlighted node.
        private const double PanDuration = 0.25;
        private const int PanRetryFrames = 10;
        private IVisualElementScheduledItem _panAnimation;

        // The single selected element (state node or edge), or null when nothing or several things are selected.
        public event Action<GraphElement> SelectionChanged;

        // Asks the window to show a sub-state machine's table (the state it belongs to, its table).
        public event Action<StateSO, TransitionTableSO> SubStateMachineOpenRequested;

        // The window this graph is in, for popups that need screen positions (e.g. the Parameters panel).
        public EditorWindow Window => _window;

        // Delete key on rows of the Parameters panel (ParameterBlackboard listens).
        public event Action<List<BlackboardField>> ParameterFieldsDeleted;

        // A state's new name typed on its node (the state, the new name).
        public event Action<StateSO, string> StateRenameRequested;

        public StateMachineGraphView(EditorWindow window)
        {
            _window = window;
            graphViewChanged = OnGraphViewChanged;
            RegisterTransitionCreationCallbacks();
            RegisterStateDropCallbacks();
            RegisterActionDropCallbacks();
            RegisterConditionDropCallbacks();

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
            GraphStyleSheets.AddTo(this, StyleSheetName);

            // An action / condition selected anywhere lights up where it's used here.
            RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += HighlightUsesOfSelection);
            RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= HighlightUsesOfSelection);
        }

        // ---------- Uses of the selection ----------

        // The selected actions outline the states that run them, the selected conditions color the arrows
        // that check them (wrapped ones too), without selecting anything in the graph. Selecting a state
        // or an arrow here selects it as usual and lights nothing extra.
        private void HighlightUsesOfSelection()
        {
            var selectedActions = new HashSet<ScriptableObject>(Selection.objects.OfType<StateActionSO>());
            var selectedConditions = new HashSet<ScriptableObject>(Selection.objects.OfType<StateConditionSO>());

            _serializedTable?.Update();

            foreach (GraphElement element in graphElements.ToList())
            {
                switch (element)
                {
                    case StateNodeView node when node.State != null:
                        node.SetUsesSelection(selectedActions.Count > 0 &&
                                              TableContents.ActionsOf(node.State).Any(selectedActions.Contains));
                        break;
                    case TransitionEdgeView edge:
                        edge.SetUsesSelection(selectedConditions.Count > 0 &&
                                              ConditionsOf(edge).Any(selectedConditions.Contains));
                        break;
                }
            }
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
        // so Undo or a data change doesn't change what the Inspector shows.
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

        // Called from the Inspector's transition lists (StateSOEditor). fromKey is a StateSO, or SpecialNodeView.Kind.AnyState.
        // Called from the Contents panel. Returns false when the state has no node in this graph.
        public bool SelectStateNode(StateSO state)
        {
            Node node = FindNode(state);
            if (node == null)
            {
                return false;
            }

            ClearSelection();
            AddToSelection(node);
            return true;
        }

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
                    edge.IsInherited == previous.IsInherited &&
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
            var ownInitialState =
                _serializedTable.FindProperty("_initialState").objectReferenceValue as StateSO;

            // A variant also shows what it gets from its Base, faint (StateMachineGraphView.Inherited.cs).
            InheritedParts inherited = ReadInherited(_serializedTable, SafeBaseOf(_table));
            StateSO initialState = ownInitialState != null ? ownInitialState : inherited.InitialState;

            SpecialNodeView entryNode = CreateSpecialNode(
                SpecialNodeView.Kind.Entry,
                _serializedTable.FindProperty("_entryNodePosition").vector2Value);
            SpecialNodeView anyStateNode = CreateSpecialNode(
                SpecialNodeView.Kind.AnyState,
                _serializedTable.FindProperty("_anyStateNodePosition").vector2Value);
            AddElement(entryNode);
            AddElement(anyStateNode);

            List<StateSO> states = CollectStates(_serializedTable, initialState);
            foreach (StateSO state in inherited.States)
            {
                if (!states.Contains(state))
                {
                    states.Add(state);
                }
            }

            Dictionary<StateSO, Vector2> savedPositions = ReadNodePositions(_serializedTable);
            Dictionary<StateSO, string> displayNames = ReadDisplayNames(_serializedTable);
            AddBasePositions(SafeBaseOf(_table), savedPositions);
            _baseStates = inherited.States;
            var stateNodes = new Dictionary<StateSO, StateNodeView>();

            for (int i = 0; i < states.Count; i++)
            {
                // States never dragged yet (here or in a Base) fall back to the grid.
                if (!savedPositions.TryGetValue(states[i], out Vector2 position))
                {
                    position = GetGridPosition(i);
                }

                // A Base's state stays the Base's even when this table's own arrows go to / from it.
                bool isInherited = inherited.States.Contains(states[i]);
                displayNames.TryGetValue(states[i], out string displayName);
                var node = new StateNodeView(states[i], states[i] == initialState, isInherited, _table, displayName);
                node.SetPosition(new Rect(position, Vector2.zero));
                AddElement(node);
                stateNodes.Add(states[i], node);
            }

            CreateEdges(_serializedTable, initialState, ownInitialState == null, entryNode, anyStateNode, stateNodes);
            CreateInheritedEdges(inherited, anyStateNode, stateNodes);
            RefreshActionChangeLabels();
            RefreshParameterTags();

            // New nodes start un-highlighted, so apply the Play Mode highlight again.
            ApplyActiveState();
            HighlightUsesOfSelection();
        }

        // ---------- Parameter tags ----------

        // Off when the window's Parameters toggle hides the panel.
        private bool _showParameterTags = true;

        public bool ShowParameterTags
        {
            get => _showParameterTags;
            set
            {
                _showParameterTags = value;
                RefreshParameterTags();
            }
        }

        // Writes each exposed parameter's name where it's used: under the state whose action it sets,
        // beside the arrow whose condition it sets. Called after Load and whenever the panel rebuilds.
        public void RefreshParameterTags()
        {
            var namesByTarget = new Dictionary<ScriptableObject, List<string>>();

            // This table's parameters, and in a variant its Bases' too (they run here as well).
            var parameterSets = new List<ExposedParametersSO>();
            var visited = new HashSet<TransitionTableSO>();
            for (TransitionTableSO current = _table; current != null && visited.Add(current); current = BaseOf(current))
            {
                ExposedParametersSO found = ParameterBlackboard.Find(current);
                if (found != null)
                {
                    parameterSets.Add(found);
                }
            }

            if (_showParameterTags)
            {
                // A Base's parameters this variant removed in its Parameters panel get no tag either.
                ExposedParametersSO own = ParameterBlackboard.Find(_table);
                HashSet<string> removed = ParameterBlackboard.RemovedParametersFor(_table);

                foreach (ExposedParametersSO parameters in parameterSets)
                foreach (ExposedParameter parameter in parameters.Parameters)
                foreach (ExposedBinding binding in parameter.Bindings)
                {
                    if (binding.Target == null || (parameters != own && removed.Contains(parameter.Name)))
                    {
                        continue;
                    }

                    if (!namesByTarget.TryGetValue(binding.Target, out List<string> names))
                    {
                        names = new List<string>();
                        namesByTarget[binding.Target] = names;
                    }

                    names.Add(parameter.Name);
                }
            }

            _serializedTable?.Update();

            foreach (GraphElement element in graphElements.ToList())
            {
                switch (element)
                {
                    case StateNodeView node when node.State != null:
                        node.SetParameterTags(NamesFor(TableContents.ActionsOf(node.State), namesByTarget));
                        break;
                    case TransitionEdgeView edge:
                        edge.SetParameterTags(NamesFor(ConditionsOf(edge), namesByTarget));
                        break;
                }
            }
        }

        private static List<string> NamesFor(
            IEnumerable<ScriptableObject> items,
            Dictionary<ScriptableObject, List<string>> namesByTarget)
        {
            var result = new List<string>();
            foreach (ScriptableObject item in items)
            {
                if (item != null && namesByTarget.TryGetValue(item, out List<string> names))
                {
                    foreach (string name in names)
                    {
                        if (!result.Contains(name))
                        {
                            result.Add(name);
                        }
                    }
                }
            }

            return result;
        }

        // Every condition in the arrow's transitions, and the ones wrapped inside those (e.g. a delayed
        // condition holding another).
        private List<ScriptableObject> ConditionsOf(TransitionEdgeView edge)
        {
            var found = new List<ScriptableObject>();

            // Its indices aren't into this table: it carries the Base's conditions itself.
            if (edge.IsInherited)
            {
                foreach (InheritedTransition item in edge.InheritedTransitions)
                foreach (StateConditionSO condition in item.Conditions)
                {
                    AddWithInner(condition, found);
                }

                return found;
            }

            if (_serializedTable == null)
            {
                return found;
            }

            SerializedProperty list = _serializedTable.FindProperty(
                edge.IsAnyState ? StateMachineFields.AnyTransitions : StateMachineFields.Transitions);

            foreach (int index in edge.TransitionIndices)
            {
                if (index >= list.arraySize)
                {
                    continue;
                }

                SerializedProperty transition = list.GetArrayElementAtIndex(index);
                SerializedProperty end = transition.GetEndProperty();
                SerializedProperty property = transition.Copy();

                while (property.Next(true) && !SerializedProperty.EqualContents(property, end))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue is StateConditionSO condition)
                    {
                        AddWithInner(condition, found);
                    }
                }
            }

            return found;
        }

        private static void AddWithInner(StateConditionSO condition, List<ScriptableObject> found)
        {
            if (found.Contains(condition))
            {
                return;
            }

            found.Add(condition);

            SerializedProperty property = new SerializedObject(condition).GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue is StateConditionSO inner)
                {
                    AddWithInner(inner, found);
                }
            }
        }

        // From a sub-state node's double-click or its right-click menu.
        public void OpenSubStateMachine(StateSO state, TransitionTableSO subTable)
        {
            SubStateMachineOpenRequested?.Invoke(state, subTable);
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

        // Follow mode: smoothly scroll so the highlighted node is in the middle, keeping the zoom.
        public void PanToActiveState()
        {
            foreach (GraphElement element in graphElements.ToList())
            {
                if (element is StateNodeView stateNode && stateNode.State.name == _activeStateName)
                {
                    PanToNode(stateNode, PanRetryFrames);
                    return;
                }
            }
        }

        private void PanToNode(Node node, int retriesLeft)
        {
            Rect nodeRect = node.GetPosition();

            // Right after Load() the node has no size yet: try again next frame.
            if (float.IsNaN(nodeRect.width) || nodeRect.width <= 0f || float.IsNaN(layout.width))
            {
                if (retriesLeft > 0)
                {
                    schedule.Execute(() => PanToNode(node, retriesLeft - 1));
                }

                return;
            }

            // viewTransform.position is where content (0,0) is drawn, so to put the node's
            // center in the middle of the view: middle - center * zoom.
            Vector3 scale = viewTransform.scale;
            Vector3 start = viewTransform.position;
            Vector3 target = (Vector3)(layout.size * 0.5f - nodeRect.center * scale.x);
            double startTime = EditorApplication.timeSinceStartup;
            bool done = false;

            _panAnimation?.Pause();
            _panAnimation = schedule.Execute(() =>
                {
                    float t = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - startTime) / PanDuration));
                    UpdateViewTransform(Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t)), scale);
                    done = t >= 1f;
                })
                .Every(16)
                .Until(() => done);
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

            // Rows of the Parameters panel aren't part of the table: that panel deletes them from its own
            // data and rebuilds (see ParameterBlackboard).
            if (change.elementsToRemove != null && change.elementsToRemove.Exists(element => element is BlackboardField))
            {
                var fields = change.elementsToRemove.FindAll(element => element is BlackboardField);
                change.elementsToRemove.RemoveAll(element => element is BlackboardField);
                ParameterFieldsDeleted?.Invoke(fields.ConvertAll(element => (BlackboardField)element));
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

        // initialIsInherited: the table has none of its own, so the Entry arrow shows the Base's.
        private void CreateEdges(
            SerializedObject serializedTable,
            StateSO initialState,
            bool initialIsInherited,
            Node entryNode,
            Node anyStateNode,
            Dictionary<StateSO, StateNodeView> stateNodes)
        {
            if (initialState != null)
            {
                AddElement(new TransitionEdgeView(
                    entryNode, stateNodes[initialState], new List<int>(), false, initialIsInherited));
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
