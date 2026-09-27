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
            Undo.undoRedoPerformed += Refresh;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Refresh;
        }

        private void CreateGUI()
        {
            var toolbar = new Toolbar();
            _titleLabel = new Label();
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolbar.Add(_titleLabel);
            rootVisualElement.Add(toolbar);

            _graphView = new StateMachineGraphView();
            _graphView.style.flexGrow = 1;
            rootVisualElement.Add(_graphView);

            Refresh();
        }

        private void SetTable(TransitionTableSO table)
        {
            _table = table;
            Refresh();
        }

        private void Refresh()
        {
            // CreateGUI may not have run yet.
            if (_graphView == null)
            {
                return;
            }

            _titleLabel.text = _table != null
                ? _table.name
                : "No Transition Table (double-click one in the Project window)";

            _graphView.Load(_table);
        }
    }
}

#endif
