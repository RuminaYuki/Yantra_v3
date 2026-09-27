#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Play Mode: outlines the state a running StateMachineController is in, like Animator.
    //
    // Which controller: the selected GameObject's, otherwise the first one in the scene that runs
    // this table. Tables run as a sub-state machine (SubStateMachineAction) aren't in the
    // controller's list, so those follow ChildStateChanged and need the GameObject selected.
    public partial class StateMachineGraphWindow
    {
        // Seconds between checks. Fast enough to look live, cheap enough to ignore.
        private const double PollInterval = 0.1;

        private StateMachineController _controller;
        private string _childStateName;
        private double _nextPollTime;
        private Label _playModeLabel;

        private void CreatePlayModeLabel(Toolbar toolbar)
        {
            // Pushes the label to the right end of the toolbar.
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            toolbar.Add(spacer);

            _playModeLabel = new Label();
            _playModeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _playModeLabel.style.marginRight = 6;
            toolbar.Add(_playModeLabel);
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
                WatchController(FindController());
            }

            string activeStateName = null;

            if (_controller != null)
            {
                int tableIndex = IndexOfTable(_controller);
                activeStateName = tableIndex >= 0
                    ? _controller.GetCurrentStateName(tableIndex)
                    : _childStateName;
            }

            _graphView.SetActiveState(activeStateName);
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

            var selected = Selection.activeGameObject.GetComponent<StateMachineController>();
            if (selected != null && selected != _controller)
            {
                WatchController(selected);
            }
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
                if (controller.gameObject.scene.IsValid() && IndexOfTable(controller) >= 0)
                {
                    return controller;
                }
            }

            return null;
        }

        // Index of the open table in the controller's _transitionTables, or -1.
        // Read every poll because ChangeTable() can swap tables at runtime.
        private int IndexOfTable(StateMachineController controller)
        {
            using var serializedController = new SerializedObject(controller);
            SerializedProperty tables = serializedController.FindProperty("_transitionTables");

            for (int i = 0; i < tables.arraySize; i++)
            {
                if (tables.GetArrayElementAtIndex(i).objectReferenceValue == _table)
                {
                    return i;
                }
            }

            return -1;
        }

        private void WatchController(StateMachineController controller)
        {
            StopWatching();

            _controller = controller;
            if (_controller == null)
            {
                return;
            }

            _controller.MainStateChanged += OnMainStateChanged;
            _controller.ChildStateChanged += OnChildStateChanged;
        }

        private void StopWatching()
        {
            if (_controller != null)
            {
                _controller.MainStateChanged -= OnMainStateChanged;
                _controller.ChildStateChanged -= OnChildStateChanged;
            }

            _controller = null;
            _childStateName = null;
        }

        private void OnMainStateChanged(string previousStateName, string currentStateName)
        {
            // Leaving a state disposes its sub-state machine, so the old child state is gone.
            _childStateName = null;
        }

        private void OnChildStateChanged(string previousStateName, string currentStateName)
        {
            _childStateName = currentStateName;
        }
    }
}

#endif
