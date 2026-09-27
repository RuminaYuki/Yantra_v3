#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Editor window that hosts the graph. Opened by double-clicking a TransitionTableSO.
    public class StateMachineGraphWindow : EditorWindow
    {
        // Serialized so the window keeps its table after a script recompile.
        [SerializeField]
        private TransitionTableSO _table;

        private StateMachineGraphView _graphView;
        private GraphInspectorPanel _inspectorPanel;
        private Label _titleLabel;

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
        }

        private void CreateGUI()
        {
            var toolbar = new Toolbar();
            _titleLabel = new Label();
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolbar.Add(_titleLabel);
            rootVisualElement.Add(toolbar);

            _graphView = new StateMachineGraphView(this);
            _inspectorPanel = new GraphInspectorPanel(Reload, _graphView.SelectTransitionEdge);
            _graphView.SelectionChanged += _inspectorPanel.ShowSelection;

            // Graph on the left, panel on the right with a draggable divider. Panel starts 320px wide.
            var split = new TwoPaneSplitView(1, 320f, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            split.Add(_graphView);
            split.Add(_inspectorPanel);
            rootVisualElement.Add(split);

            ShowTable();
        }

        private void SetTable(TransitionTableSO table)
        {
            _table = table;
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

            _titleLabel.text = _table != null
                ? _table.name
                : "No Transition Table (double-click one in the Project window)";

            _inspectorPanel.SetTable(_table);
            _graphView.Load(_table);
        }

        // Same table, but its data changed (Undo/Redo, new initial state, ...).
        private void Reload()
        {
            _graphView?.Reload();
        }
    }
}

#endif
