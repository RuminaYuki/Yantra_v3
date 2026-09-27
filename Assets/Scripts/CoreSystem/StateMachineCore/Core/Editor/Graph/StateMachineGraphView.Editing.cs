#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Editing the table from the graph: right-click menus, Make Transition, Add State, Delete.
    // Every change goes through _serializedTable, so it marks the asset dirty and supports Undo.
    public partial class StateMachineGraphView
    {
        // Set while "Make Transition" is waiting for the target click.
        private Node _transitionSource;
        private TransitionPreviewView _transitionPreview;

        private StateSearchProvider _stateSearchProvider;

        private void RegisterTransitionCreationCallbacks()
        {
            RegisterCallback<MouseMoveEvent>(OnMouseMoveWhileConnecting);

            // TrickleDown: runs before the clicked node/edge and the GraphView manipulators see the click.
            RegisterCallback<MouseDownEvent>(OnMouseDownWhileConnecting, TrickleDown.TrickleDown);
            RegisterCallback<KeyDownEvent>(OnKeyDownWhileConnecting);

            RegisterCallback<DetachFromPanelEvent>(_ => DestroyStateSearchProvider());
        }

        // ---------- Right-click menu ----------

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // No base call: GraphView's default Cut / Copy / Paste / Duplicate aren't supported here.
            if (_serializedTable == null)
            {
                return;
            }

            // The click may land on a child (e.g. the title label), so look up to the node / edge.
            var target = evt.target as VisualElement;
            Node node = target?.GetFirstOfType<Node>();
            TransitionEdgeView edge = target?.GetFirstOfType<TransitionEdgeView>();
            Vector2 mousePosition = evt.mousePosition;

            if (node is StateNodeView stateNode)
            {
                bool isInitial = stateNode.State == GetInitialState();

                evt.menu.AppendAction("Make Transition", _ => StartTransitionCreation(stateNode, mousePosition));
                evt.menu.AppendAction(
                    "Set as Initial State",
                    _ => SetInitialState(stateNode.State),
                    isInitial ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Remove from Table", _ => DeleteFromMenu(stateNode));
            }
            else if (node is SpecialNodeView { NodeKind: SpecialNodeView.Kind.AnyState } anyStateNode)
            {
                evt.menu.AppendAction("Make Transition", _ => StartTransitionCreation(anyStateNode, mousePosition));
            }
            else if (node is SpecialNodeView)
            {
                // Entry: nothing to do here, the initial state is set from a state node.
            }
            else if (edge != null)
            {
                if (IsDeletable(edge))
                {
                    evt.menu.AppendAction("Delete", _ => DeleteFromMenu(edge));
                }
            }
            else
            {
                evt.menu.AppendAction("Add State...", _ => OpenAddStateSearch(mousePosition));
            }
        }

        // Deletes the whole selection if the right-clicked element is part of it, like Animator.
        private void DeleteFromMenu(GraphElement element)
        {
            var toDelete = new List<GraphElement>();

            if (selection.Contains(element))
            {
                foreach (ISelectable selectable in selection)
                {
                    if (selectable is GraphElement selected && IsDeletable(selected))
                    {
                        toDelete.Add(selected);
                    }
                }
            }
            else
            {
                toDelete.Add(element);
            }

            // Goes through graphViewChanged -> RemoveFromTable, same as the Delete key.
            DeleteElements(toDelete);
        }

        private static bool IsDeletable(GraphElement element)
        {
            return (element.capabilities & Capabilities.Deletable) != 0;
        }

        // ---------- Delete ----------

        private void RemoveFromTable(List<GraphElement> elements)
        {
            _serializedTable.Update();

            SerializedProperty transitions = _serializedTable.FindProperty("_transitions");
            SerializedProperty anyTransitions = _serializedTable.FindProperty("_anyTransitions");
            SerializedProperty nodePositions = _serializedTable.FindProperty("_nodePositions");
            SerializedProperty initialState = _serializedTable.FindProperty("_initialState");

            var localToRemove = new HashSet<int>();
            var anyToRemove = new HashSet<int>();
            var removedStates = new HashSet<StateSO>();

            foreach (GraphElement element in elements)
            {
                if (element is StateNodeView stateNode)
                {
                    removedStates.Add(stateNode.State);
                }
                else if (element is TransitionEdgeView edge)
                {
                    // Entry edge has no indices, so it's never removed from the data.
                    foreach (int index in edge.TransitionIndices)
                    {
                        (edge.IsAnyState ? anyToRemove : localToRemove).Add(index);
                    }
                }
            }

            if (removedStates.Count > 0)
            {
                // A removed state takes every transition that touches it along.
                for (int i = 0; i < transitions.arraySize; i++)
                {
                    SerializedProperty item = transitions.GetArrayElementAtIndex(i);
                    if (removedStates.Contains(item.FindPropertyRelative("FromState").objectReferenceValue as StateSO) ||
                        removedStates.Contains(item.FindPropertyRelative("ToState").objectReferenceValue as StateSO))
                    {
                        localToRemove.Add(i);
                    }
                }

                for (int i = 0; i < anyTransitions.arraySize; i++)
                {
                    SerializedProperty item = anyTransitions.GetArrayElementAtIndex(i);
                    if (removedStates.Contains(item.FindPropertyRelative("ToState").objectReferenceValue as StateSO))
                    {
                        anyToRemove.Add(i);
                    }
                }

                // Otherwise CollectStates would bring the node back.
                for (int i = nodePositions.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty item = nodePositions.GetArrayElementAtIndex(i);
                    if (removedStates.Contains(item.FindPropertyRelative("State").objectReferenceValue as StateSO))
                    {
                        nodePositions.DeleteArrayElementAtIndex(i);
                    }
                }

                if (removedStates.Contains(initialState.objectReferenceValue as StateSO))
                {
                    initialState.objectReferenceValue = null;
                    Debug.LogWarning(
                        $"{_table.name} has no initial state now. Right-click a state > Set as Initial State.",
                        _table);
                }
            }

            DeleteArrayIndices(transitions, localToRemove);
            DeleteArrayIndices(anyTransitions, anyToRemove);

            _serializedTable.ApplyModifiedProperties();
        }

        private static void DeleteArrayIndices(SerializedProperty array, HashSet<int> indices)
        {
            // Highest first, so deleting doesn't shift the ones still to delete.
            var sorted = new List<int>(indices);
            sorted.Sort((a, b) => b.CompareTo(a));

            foreach (int index in sorted)
            {
                if (index < array.arraySize)
                {
                    array.DeleteArrayElementAtIndex(index);
                }
            }
        }

        // ---------- Initial state ----------

        private StateSO GetInitialState()
        {
            return _serializedTable.FindProperty("_initialState").objectReferenceValue as StateSO;
        }

        private void SetInitialState(StateSO state)
        {
            _serializedTable.Update();
            _serializedTable.FindProperty("_initialState").objectReferenceValue = state;
            _serializedTable.ApplyModifiedProperties();
            Reload();
        }

        // ---------- Make Transition ----------

        private void StartTransitionCreation(Node source, Vector2 mousePosition)
        {
            CancelTransitionCreation();

            _transitionSource = source;
            _transitionPreview = new TransitionPreviewView();
            Add(_transitionPreview);
            UpdateTransitionPreview(mousePosition);

            // So Escape reaches OnKeyDownWhileConnecting.
            Focus();
        }

        private void CancelTransitionCreation()
        {
            _transitionPreview?.RemoveFromHierarchy();
            _transitionPreview = null;
            _transitionSource = null;
        }

        // mousePosition is in panel space, as given by mouse events.
        private void UpdateTransitionPreview(Vector2 mousePosition)
        {
            // The preview covers this GraphView, so this GraphView's local space is the preview's too.
            Rect sourceRect = _transitionSource.parent.ChangeCoordinatesTo(this, _transitionSource.layout);
            _transitionPreview.SetPoints(sourceRect.center, this.WorldToLocal(mousePosition));
        }

        private void OnMouseMoveWhileConnecting(MouseMoveEvent evt)
        {
            if (_transitionSource != null)
            {
                UpdateTransitionPreview(evt.mousePosition);
            }
        }

        private void OnMouseDownWhileConnecting(MouseDownEvent evt)
        {
            if (_transitionSource == null)
            {
                return;
            }

            // This click only finishes (or cancels) the transition: no selecting, no dragging.
            evt.StopImmediatePropagation();

            Node source = _transitionSource;
            CancelTransitionCreation();

            if (evt.button != (int)MouseButton.LeftMouse)
            {
                return;
            }

            // Preview ignores picking, so this finds whatever is under the mouse.
            StateNodeView target = panel.Pick(evt.mousePosition)?.GetFirstOfType<StateNodeView>();

            if (target != null && target != source)
            {
                CreateTransition(source, target);
            }
        }

        private void OnKeyDownWhileConnecting(KeyDownEvent evt)
        {
            if (_transitionSource != null && evt.keyCode == KeyCode.Escape)
            {
                CancelTransitionCreation();
                evt.StopPropagation();
            }
        }

        private void CreateTransition(Node source, StateNodeView target)
        {
            _serializedTable.Update();

            bool isAnyState = source is SpecialNodeView;
            SerializedProperty array = _serializedTable.FindProperty(
                isAnyState ? "_anyTransitions" : "_transitions");

            // Growing the array copies the last item, so every field is set below.
            int index = array.arraySize;
            array.arraySize++;
            SerializedProperty item = array.GetArrayElementAtIndex(index);

            if (!isAnyState)
            {
                item.FindPropertyRelative("FromState").objectReferenceValue = ((StateNodeView)source).State;
            }

            item.FindPropertyRelative("ToState").objectReferenceValue = target.State;
            item.FindPropertyRelative("ConditionGroups").arraySize = 0;

            _serializedTable.ApplyModifiedProperties();

            // Select the new edge so its (empty) conditions show up in the panel right away.
            object fromKey = GetNodeKey(source);
            Load(_table);
            SelectEdge(fromKey, target.State);
        }

        // ---------- Add State ----------

        private void OpenAddStateSearch(Vector2 mousePosition)
        {
            // Where the new node goes, in the same space as node positions.
            Vector2 graphPosition = contentViewContainer.WorldToLocal(mousePosition);

            var alreadyInTable = new HashSet<StateSO>();
            foreach (GraphElement element in graphElements.ToList())
            {
                if (element is StateNodeView stateNode)
                {
                    alreadyInTable.Add(stateNode.State);
                }
            }

            if (_stateSearchProvider == null)
            {
                _stateSearchProvider = ScriptableObject.CreateInstance<StateSearchProvider>();
                _stateSearchProvider.hideFlags = HideFlags.HideAndDontSave;
            }

            _stateSearchProvider.Setup(
                alreadyInTable,
                state => AddStateToTable(state, graphPosition),
                () => CreateNewState(graphPosition));

            // SearchWindow wants screen coordinates.
            Vector2 screenPosition = _window.position.position + mousePosition;
            SearchWindow.Open(new SearchWindowContext(screenPosition), _stateSearchProvider);
        }

        private void DestroyStateSearchProvider()
        {
            if (_stateSearchProvider != null)
            {
                Object.DestroyImmediate(_stateSearchProvider);
                _stateSearchProvider = null;
            }
        }

        private void AddStateToTable(StateSO state, Vector2 position)
        {
            // A position entry is what keeps a state without transitions in the table (see CollectStates).
            _serializedTable.Update();
            FindOrAddNodePosition(_serializedTable.FindProperty("_nodePositions"), state)
                .FindPropertyRelative("Position").vector2Value = position;
            _serializedTable.ApplyModifiedProperties();

            Load(_table);

            Node node = FindNode(state);
            if (node != null)
            {
                AddToSelection(node);
            }
        }

        private void CreateNewState(Vector2 position)
        {
            // Default to the table's folder, where its states usually live.
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(_table))?.Replace('\\', '/');

            string path = EditorUtility.SaveFilePanelInProject(
                "Create State",
                "New_State",
                "asset",
                "Choose where to save the new StateSO.",
                folder);

            // Cancelled.
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var state = ScriptableObject.CreateInstance<StateSO>();
            AssetDatabase.CreateAsset(state, path);

            AddStateToTable(state, position);
        }
    }
}

#endif
