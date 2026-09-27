#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Play Mode: outlines the state a running StateMachineController is in, like Animator,
    // at any sub-state machine depth. "Follow" also moves the graph in and out of sub-state
    // machines as the character does, and scrolls to the highlighted node.
    //
    // Which controller: the selected GameObject's, otherwise the first one in the scene that runs
    // the root table of the navigation path. From there the running machines are walked down
    // (StateMachine.CurrentState -> SubStateMachineAction.ChildStateMachine -> ...).
    public partial class StateMachineGraphWindow
    {
        // Seconds between checks. Fast enough to look live, cheap enough to ignore.
        private const double PollInterval = 0.1;

        // Stops the walk if a table (indirectly) runs itself.
        private const int MaxDepth = 16;

        // Serialized so it survives the domain reload when entering Play Mode.
        [SerializeField]
        private bool _follow;

        private StateMachineController _controller;
        private double _nextPollTime;
        private ToolbarToggle _followToggle;
        private Label _playModeLabel;

        // What the Follow camera last moved to, so it only moves when that changes.
        private string _lastPannedStateName;
        private TransitionTableSO _lastPannedTable;

        private void ForgetLastPan()
        {
            _lastPannedStateName = null;
            _lastPannedTable = null;
        }

        // One level of what is running right now, from the root table down.
        private struct RunningLevel
        {
            public TransitionTableSO Table;
            public StateSO EnteredFrom;   // State whose SubStateMachineAction runs Table. Null at the root.
            public StateMachine Machine;
        }

        private void CreatePlayModeToolbarItems(Toolbar toolbar)
        {
            // Pushes the items to the right end of the toolbar.
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            toolbar.Add(spacer);

            _playModeLabel = new Label();
            _playModeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _playModeLabel.style.marginRight = 6;
            toolbar.Add(_playModeLabel);

            _followToggle = new ToolbarToggle
            {
                text = "Follow",
                tooltip = "Play Mode: go into and out of sub-state machines as the character does."
            };
            _followToggle.SetValueWithoutNotify(_follow);
            _followToggle.RegisterValueChangedCallback(evt =>
            {
                _follow = evt.newValue;
                _nextPollTime = 0; // Jump to the running state right away.
                ForgetLastPan();   // And move the camera to it, even if the state didn't change.
            });
            toolbar.Add(_followToggle);
        }

        // Moving around by hand turns Follow off, otherwise the next poll would pull the graph back.
        private void SetFollow(bool follow)
        {
            _follow = follow;
            _followToggle?.SetValueWithoutNotify(follow);
        }

        // EditorWindow message, called several times per second while the window is open.
        private void Update()
        {
            if (_graphView == null)
            {
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                StopWatching();
                _graphView.SetActiveState(null);
                _playModeLabel.text = string.Empty;
                return;
            }

            if (EditorApplication.timeSinceStartup < _nextPollTime)
            {
                return;
            }

            _nextPollTime = EditorApplication.timeSinceStartup + PollInterval;

            // Unity's == null is also true after the GameObject was destroyed.
            if (_controller == null)
            {
                _controller = FindController();
            }

            List<RunningLevel> chain = _controller != null
                ? BuildRunningChain()
                : new List<RunningLevel>();

            if (_follow && chain.Count > 0 && !PathMatches(chain, chain.Count))
            {
                FollowTo(chain);
            }

            string activeStateName = GetActiveStateName(chain);
            _graphView.SetActiveState(activeStateName);

            // Follow also moves the camera, once per new highlighted state (or new table).
            if (_follow && activeStateName != null &&
                (activeStateName != _lastPannedStateName || _table != _lastPannedTable))
            {
                _graphView.PanToActiveState();
                _lastPannedStateName = activeStateName;
                _lastPannedTable = _table;
            }

            _playModeLabel.text = _controller != null
                ? $"▶ {_controller.name}"
                : "▶ Select a GameObject running this table";
        }

        // EditorWindow message.
        private void OnSelectionChange()
        {
            if (!EditorApplication.isPlaying || Selection.activeGameObject == null)
            {
                return;
            }

            if (Selection.activeGameObject.TryGetComponent(out StateMachineController selected))
            {
                _controller = selected;
                _nextPollTime = 0;
            }
        }

        private void StopWatching()
        {
            _controller = null;
            ForgetLastPan();
        }

        private StateMachineController FindController()
        {
            if (Selection.activeGameObject != null &&
                Selection.activeGameObject.TryGetComponent(out StateMachineController selected))
            {
                return selected;
            }

            // Includes prefab assets, so keep only objects that live in a scene.
            foreach (StateMachineController controller in Resources.FindObjectsOfTypeAll<StateMachineController>())
            {
                if (controller.gameObject.scene.IsValid() && IndexOfTable(controller, RootTable) >= 0)
                {
                    return controller;
                }
            }

            return null;
        }

        // Index of `table` in the controller's _transitionTables, or -1.
        // Read every poll because ChangeTable() can swap tables at runtime.
        private static int IndexOfTable(StateMachineController controller, TransitionTableSO table)
        {
            using var serializedController = new SerializedObject(controller);
            SerializedProperty tables = serializedController.FindProperty("_transitionTables");

            for (int i = 0; i < tables.arraySize; i++)
            {
                if (tables.GetArrayElementAtIndex(i).objectReferenceValue == table)
                {
                    return i;
                }
            }

            return -1;
        }

        // What is running now: the root table's machine, then the sub-state machine its current
        // state runs, and so on down to the deepest one.
        private List<RunningLevel> BuildRunningChain()
        {
            var chain = new List<RunningLevel>();

            TransitionTableSO table = RootTable;
            int tableIndex = IndexOfTable(_controller, table);
            if (tableIndex < 0)
            {
                return chain;
            }

            StateMachine machine = _controller.GetStateMachine(tableIndex);
            StateSO enteredFrom = null;

            while (machine != null && chain.Count < MaxDepth)
            {
                chain.Add(new RunningLevel { Table = table, EnteredFrom = enteredFrom, Machine = machine });

                State current = machine.CurrentState;
                SubStateMachineAction subAction = current != null ? FindRunningSubAction(current) : null;
                if (subAction == null)
                {
                    break;
                }

                // The runtime State only has a name; the breadcrumb wants the StateSO.
                enteredFrom = FindStateByName(table, current.DebugName);
                table = subAction.TransitionTable;
                machine = subAction.ChildStateMachine;
            }

            return chain;
        }

        private static SubStateMachineAction FindRunningSubAction(State state)
        {
            foreach (StateAction action in state.Actions)
            {
                if (action is SubStateMachineAction { ChildStateMachine: not null } subAction)
                {
                    return subAction;
                }
            }

            return null;
        }

        // True if the first `count` levels of the chain are exactly the navigation path.
        private bool PathMatches(List<RunningLevel> chain, int count)
        {
            if (_path.Count != count || chain.Count < count)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (_path[i] != chain[i].Table)
                {
                    return false;
                }
            }

            return true;
        }

        // Name of the running state in the table shown now, or null if that table isn't running
        // (e.g. looking inside a sub-state machine the character isn't in).
        private string GetActiveStateName(List<RunningLevel> chain)
        {
            int level = _path.Count - 1;

            for (int i = 0; i <= level; i++)
            {
                if (i >= chain.Count || chain[i].Table != _path[i])
                {
                    return null;
                }
            }

            return chain[level].Machine.CurrentState?.DebugName;
        }

        private void FollowTo(List<RunningLevel> chain)
        {
            _path.Clear();
            _pathStates.Clear();

            foreach (RunningLevel level in chain)
            {
                _path.Add(level.Table);
                _pathStates.Add(level.EnteredFrom);
            }

            _table = _path[_path.Count - 1];
            ShowTable();
        }

        // First StateSO in the table with this name (runtime State.DebugName is StateSO.name).
        private static StateSO FindStateByName(TransitionTableSO table, string stateName)
        {
            using var serializedTable = new SerializedObject(table);

            bool Matches(SerializedProperty property, out StateSO state)
            {
                state = property.objectReferenceValue as StateSO;
                return state != null && state.name == stateName;
            }

            if (Matches(serializedTable.FindProperty("_initialState"), out StateSO found))
            {
                return found;
            }

            foreach (string arrayName in new[] { "_transitions", "_anyTransitions", "_nodePositions" })
            {
                SerializedProperty array = serializedTable.FindProperty(arrayName);
                for (int i = 0; i < array.arraySize; i++)
                {
                    SerializedProperty item = array.GetArrayElementAtIndex(i);

                    foreach (string field in new[] { "FromState", "ToState", "State" })
                    {
                        SerializedProperty property = item.FindPropertyRelative(field);
                        if (property != null && Matches(property, out found))
                        {
                            return found;
                        }
                    }
                }
            }

            return null;
        }
    }
}

#endif
